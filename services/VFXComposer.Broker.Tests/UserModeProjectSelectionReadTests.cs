using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using VFXComposer.Broker.Ipc;
using VFXComposer.Broker.Registration;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Queries;

namespace VFXComposer.Broker.Tests;

[TestClass]
[DoNotParallelize]
[SupportedOSPlatform("windows")]
public sealed class UserModeProjectSelectionReadTests
{
    [TestMethod]
    public async Task ExplicitCanonicalProjectBindsC1C2LocatorToExactU2SessionWithoutPathWire()
    {
        using var project = TestProject.Create();
        await using var worker = await StartWorkerAsync(project.Root, generation: 301);
        var store = new UserModeProjectSelectionStore();

        var lease = await store.SelectAsync(project.Root, worker);
        var identities = UserModeProjectPathIdentity.Compute(project.Root);
        var locatorText = Encoding.UTF8.GetString(lease.CopyLocatorBytes());

        Assert.IsTrue(store.IsCurrent(lease, worker));
        Assert.IsTrue(lease.Selection.ProjectIdentity.FixedTimeEquals(identities.ProjectIdentity));
        Assert.IsTrue(lease.Locator.ProjectIdentity.FixedTimeEquals(identities.ProjectIdentity));
        Assert.IsTrue(lease.Locator.VolumeIdentity.FixedTimeEquals(identities.VolumeIdentity));
        Assert.IsTrue(lease.Locator.RepositoryIdentity.FixedTimeEquals(identities.RepositoryIdentity));
        Assert.IsTrue(lease.Locator.ProjectRootIdentity.FixedTimeEquals(identities.ProjectRootIdentity));
        Assert.AreEqual(worker.Generation, lease.Locator.BrokerGeneration);
        Assert.AreEqual(worker.SessionId, lease.Locator.WorkerSessionId);
        Assert.AreEqual(worker.ChildProcessEpoch, lease.Locator.WorkerProcessEpoch);
        Assert.IsFalse(locatorText.Contains(project.Root, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(locatorText.Contains("path", StringComparison.OrdinalIgnoreCase));

        var read = new UserModeProjectReadSession(store, lease, worker);
        var encoded = read.EncodeQuery(DocumentKinds.LibraryIndex, "project");
        var queryText = Encoding.UTF8.GetString(encoded);
        Assert.IsTrue(read.IsUsable);
        Assert.IsFalse(queryText.Contains(project.Root, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(queryText.Contains("path", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task LocatorBytesAreDefensivelyCopiedAndRemainStrict()
    {
        using var project = TestProject.Create();
        await using var worker = await StartWorkerAsync(project.Root, generation: 302);
        var store = new UserModeProjectSelectionStore();
        var lease = await store.SelectAsync(project.Root, worker);

        var first = lease.CopyLocatorBytes();
        first[0] ^= 0xff;
        var second = lease.CopyLocatorBytes();

        Assert.AreNotEqual(first[0], second[0]);
        Assert.AreEqual((byte)'{', second[0]);
    }

    [TestMethod]
    public async Task ReselectAndExplicitRevokeMakeEveryPriorReadSessionStale()
    {
        using var firstProject = TestProject.Create();
        using var secondProject = TestProject.Create();
        await using var worker = await StartWorkerAsync(firstProject.Root, generation: 303);
        var store = new UserModeProjectSelectionStore();

        var firstLease = await store.SelectAsync(firstProject.Root, worker);
        var firstRead = new UserModeProjectReadSession(store, firstLease, worker);
        var secondLease = await store.SelectAsync(secondProject.Root, worker);
        var secondRead = new UserModeProjectReadSession(store, secondLease, worker);

        Assert.IsTrue(firstLease.IsRevoked);
        Assert.IsFalse(firstRead.IsUsable);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            firstRead.CreateQuery(DocumentKinds.LibraryIndex, "project"));
        Assert.IsTrue(secondRead.IsUsable);

        await store.RevokeAsync(secondLease);
        Assert.IsTrue(secondLease.IsRevoked);
        Assert.IsFalse(secondRead.IsUsable);
        Assert.IsNull(store.Current);
    }

    [TestMethod]
    public async Task WorkerRestartSessionAndEpochCannotReuseOldLease()
    {
        using var project = TestProject.Create();
        var store = new UserModeProjectSelectionStore();
        UserModeProjectLease staleLease;
        UserModeProjectReadSession staleRead;

        await using (var firstWorker = await StartWorkerAsync(project.Root, generation: 304))
        {
            staleLease = await store.SelectAsync(project.Root, firstWorker);
            staleRead = new UserModeProjectReadSession(store, staleLease, firstWorker);
            Assert.IsTrue(staleRead.IsUsable);
        }

        Assert.IsFalse(staleRead.IsUsable);
        await using var replacement = await StartWorkerAsync(project.Root, generation: 305);
        Assert.IsFalse(staleLease.Matches(replacement));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            using var ignored = await store.BeginExchangeAsync(staleLease, replacement);
        });

        var replacementLease = await store.SelectAsync(project.Root, replacement);
        Assert.IsTrue(replacementLease.MatchesCorrelation(
            replacement.Generation,
            replacement.SessionId,
            replacement.ChildProcessEpoch));
        Assert.IsFalse(replacementLease.MatchesCorrelation(
            replacement.Generation,
            replacement.SessionId,
            staleLease.Locator.WorkerProcessEpoch));
    }

    [TestMethod]
    public void CanonicalDriveRootMarkersAndNoReparseAreMandatory()
    {
        using var project = TestProject.Create();
        Assert.AreEqual(project.Root, UserModeProjectRootValidator.Validate(project.Root));
        Assert.ThrowsExactly<ArgumentException>(() =>
            UserModeProjectRootValidator.Validate(project.Root + Path.DirectorySeparatorChar));
        Assert.ThrowsExactly<ArgumentException>(() =>
            UserModeProjectRootValidator.Validate("relative-project"));

        File.Delete(Path.Combine(project.Root, "Packages", "manifest.json"));
        Assert.ThrowsExactly<ArgumentException>(() =>
            UserModeProjectRootValidator.Validate(project.Root));
    }

    [TestMethod]
    public void ReparseProjectRootIsRejected()
    {
        using var target = TestProject.Create();
        var junction = target.Root + "-junction";
        CreateJunction(junction, target.Root);
        try
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
                UserModeProjectRootValidator.Validate(junction));
        }
        finally
        {
            Directory.Delete(junction);
        }
    }

    [TestMethod]
    public async Task QueryShapeAllowsOnlyBoundedKindsAndTypedExpectedHash()
    {
        using var project = TestProject.Create();
        await using var worker = await StartWorkerAsync(project.Root, generation: 306);
        var store = new UserModeProjectSelectionStore();
        var lease = await store.SelectAsync(project.Root, worker);
        var read = new UserModeProjectReadSession(store, lease, worker);
        var expected = TypedHash.ComputeUtf8(ReadDocumentResult.ContentHashType, "expected");

        var query = read.CreateQuery(DocumentKinds.Manifest, "build-01", expected);
        Assert.AreEqual(DocumentKinds.Manifest, query.DocumentKind);
        Assert.AreEqual("build-01", query.DocumentId);
        Assert.IsTrue(expected.FixedTimeEquals(query.ExpectedContentHash!));
        Assert.ThrowsExactly<ArgumentException>(() =>
            read.CreateQuery(DocumentKinds.Manifest, "../escape"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            read.CreateQuery("FILE", "project"));
    }

    [TestMethod]
    public void BrokerU3SourcesHaveNoProjectContentOrPrivilegedSurface()
    {
        var root = RepositoryRoot();
        foreach (var relative in new[]
                 {
                     "services/VFXComposer.Broker/Registration/UserModeProjectSelectionStore.cs",
                     "services/VFXComposer.Broker/Ipc/UserModeProjectReadSession.cs",
                 })
        {
            var source = File.ReadAllText(Path.Combine(root, relative));
            foreach (var forbidden in new[]
                     {
                         "File.Read", "File.Open", "FileStream", "ReadAllBytes", "ReadAllText",
                         "Directory.Enumerate", "Directory.GetFiles", "AssetDatabase",
                         "ServiceHost", "OpenSCManager", "SeSecurityPrivilege", "SeRestorePrivilege",
                         "FileId", "FileIdentity", "LocalSystem",
                     })
            {
                Assert.IsFalse(source.Contains(forbidden, StringComparison.Ordinal),
                    $"{relative} contains {forbidden}");
            }
        }
    }

    private static Task<UserModeBrokerWorkerSession> StartWorkerAsync(string workingDirectory, long generation)
    {
        var startInfo = UserModeSessionTestChild.Create("valid");
        startInfo.WorkingDirectory = workingDirectory;
        return UserModeBrokerWorkerSession.StartAsync(
            UserModeSessionTestChild.ExpectedExecutablePath,
            startInfo,
            generation,
            TimeSpan.FromSeconds(10));
    }

    private static void CreateJunction(string junction, string target)
    {
        var startInfo = new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("mklink");
        startInfo.ArgumentList.Add("/J");
        startInfo.ArgumentList.Add(junction);
        startInfo.ArgumentList.Add(target);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("U3FS001");
        process.WaitForExit();
        Assert.AreEqual(0, process.ExitCode, process.StandardError.ReadToEnd());
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "VFXComposer.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Repository root unavailable.");
    }

    private sealed class TestProject : IDisposable
    {
        private TestProject(string root) => Root = root;

        internal string Root { get; }

        internal static TestProject Create()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "vfxcomposer-u3-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "Assets"));
            Directory.CreateDirectory(Path.Combine(root, "Packages"));
            Directory.CreateDirectory(Path.Combine(root, "ProjectSettings"));
            File.WriteAllText(Path.Combine(root, "Packages", "manifest.json"), "{}", new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(root, "ProjectSettings", "ProjectVersion.txt"),
                "m_EditorVersion: 2022.3.62f3c1\n",
                new UTF8Encoding(false));
            return new TestProject(Path.GetFullPath(root));
        }

        public void Dispose()
        {
            for (var attempt = 0; Directory.Exists(Root); attempt++)
            {
                try
                {
                    Directory.Delete(Root, recursive: true);
                }
                catch (IOException) when (attempt < 39)
                {
                    Thread.Sleep(50);
                }
            }
        }
    }
}
