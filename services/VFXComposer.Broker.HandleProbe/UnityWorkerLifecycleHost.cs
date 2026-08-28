using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using VFXComposer.Broker.Configuration;
using VFXComposer.Broker.Ipc;
using VFXComposer.Broker.Native;
using VFXComposer.Broker.Queries;
using VFXComposer.Broker.Registration;
using VFXComposer.Protocol;
using VFXComposer.Protocol.Diagnostics;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;
using VFXComposer.Protocol.Queries;

namespace VFXComposer.Broker.HandleProbe;

/// <summary>
/// Non-publishable test host for the Unity Worker cross-process lifecycle gate.
/// Command-line values select only an exact test peer and pipe token; they grant no
/// production registration or project path authority.
/// </summary>
internal static class UnityWorkerLifecycleHost
{
    private const string Mode = "--unity-worker-lifecycle";

    internal static int TryRun(string[] args)
    {
        if (args.Length != 3 ||
            !string.Equals(args[0], Mode, StringComparison.Ordinal) ||
            !int.TryParse(args[1], out var workerProcessId) ||
            workerProcessId <= 0 ||
            !IsToken(args[2]))
        {
            return 31;
        }

        try
        {
            return RunAsync(workerProcessId, args[2]).GetAwaiter().GetResult();
        }
        catch
        {
            Console.Error.WriteLine("WORKER_LIFECYCLE_HOST_FAILED");
            return 39;
        }
    }

    private static async Task<int> RunAsync(int workerProcessId, string pipeName)
    {
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8)
        {
            return 32;
        }

        var scratch = Path.Combine(
            Path.GetTempPath(),
            "vfxcomposer-unity-worker-" + Guid.NewGuid().ToString("N"));
        if (Directory.Exists(scratch) || File.Exists(scratch))
        {
            return 33;
        }

        var repository = Path.Combine(scratch, "repository");
        var project = Path.Combine(repository, "project");
        var ownedFiles = new List<string>();
        var ownedDirectories = new List<string>();
        AuthenticatedPeerConnection? connection = null;
        PeerSessionRegistry? sessions = null;
        ProjectRegistrationStore? registrations = null;
        ScratchCleanupPins? cleanupPins = null;
        string? acceptedReceipt = null;
        try
        {
            EnsureOwnedDirectory(scratch, ownedDirectories);
            EnsureOwnedDirectory(repository, ownedDirectories);
            EnsureOwnedDirectory(project, ownedDirectories);
            var readFixtures = CreateReadFixtures(
                repository,
                project,
                ownedFiles,
                ownedDirectories);
            using var workerFacts = WindowsNamedPipePeerFactsSource.ObserveProcess(workerProcessId);
            using var desktopFacts = WindowsNamedPipePeerFactsSource.ObserveProcess(
                System.Diagnostics.Process.GetCurrentProcess().Id);
            var driveRoot = Path.GetPathRoot(scratch)
                ?? throw new InvalidDataException();
            var volumeGuid = GetVolumeGuid(driveRoot);
            var repositorySegments = repository[driveRoot.Length..]
                .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            var definition = new BrokerRegistrationDefinition(
                "project-unity-worker-lifecycle",
                volumeGuid,
                repositorySegments,
                new[] { "project" });
            var cleanupDefinition = new BrokerRegistrationDefinition(
                "project-unity-worker-cleanup",
                volumeGuid,
                scratch[driveRoot.Length..]
                    .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries),
                new[] { "repository" });
            cleanupPins = ScratchCleanupPins.Open(
                cleanupDefinition,
                scratch,
                repository,
                project,
                ownedDirectories);
            var policy = CreateTestPolicy(
                pipeName,
                workerFacts.UserSidIdentity,
                desktopFacts.ImageIdentity,
                workerFacts.ImageIdentity,
                definition);
            sessions = new PeerSessionRegistry(policy);
            registrations = new ProjectRegistrationStore(policy, sessions);
            var host = new NamedPipeBrokerHost(
                policy,
                new NamedPipePeerAuthenticator(new WindowsNamedPipePeerFactsSource(), sessions));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var accept = host.AcceptOneAsync(timeout.Token);

            Console.Out.WriteLine("READY " + workerFacts.ImageIdentity.Digest);
            Console.Out.Flush();
            connection = await accept.ConfigureAwait(false);
            if (connection.Session.ProcessId != workerProcessId ||
                !string.Equals(
                    connection.Session.ProcessEpoch,
                    workerFacts.ProcessEpoch,
                    StringComparison.Ordinal) ||
                !connection.Session.ImageIdentity.FixedTimeEquals(workerFacts.ImageIdentity))
            {
                return 38;
            }

            if (!registrations.TryRegisterPinned(
                    connection.Session,
                    definition.RegisteredProjectId,
                    out _,
                    out _))
            {
                return 34;
            }

            var desktopHello = new PeerHello(
                "desktop-unity-worker-host",
                PeerRoles.Desktop,
                "desktop-unity-worker-host",
                desktopFacts.ProcessId,
                desktopFacts.ProcessEpoch,
                new[] { PeerCapabilityIds.PeerSessionV1, PeerCapabilityIds.ReadOnlyQueryV1 },
                desktopFacts.ImageIdentity);
            if (!sessions.TryAuthenticate(
                    desktopHello,
                    desktopFacts,
                    out var desktopSession,
                    out _,
                    out _))
            {
                return 35;
            }

            if (!registrations.TryAcquirePinnedLease(
                    desktopSession!,
                    connection.Session,
                    definition.RegisteredProjectId,
                    "unity-worker-lease",
                    out var lease,
                    out _,
                    out _))
            {
                return 36;
            }
            var activeLease = lease ?? throw new InvalidDataException();

            var transport = new WorkerHandleLifecycleTransport(registrations);
            if (!await transport.PublishGrantAndAwaitAcknowledgementAsync(
                    connection,
                    activeLease,
                    "unity-worker-grant",
                    timeout.Token).ConfigureAwait(false))
            {
                return 37;
            }

            var queryRouter = new ReadOnlyQueryRouter(registrations, sessions);
            var queryTransport = new WorkerReadQueryTransport(queryRouter);
            for (var index = 0; index < readFixtures.Count; index++)
            {
                var fixture = readFixtures[index];
                var expectedHash = TypedHash.Compute(
                    ReadDocumentResult.ContentHashType,
                    fixture.Content);
                var query = new ReadDocumentQuery(
                    ProtocolVersions.Current,
                    MessageKinds.ReadDocumentQuery,
                    "unity-worker-read-" + index,
                    activeLease.LeaseId,
                    activeLease.Project.ProjectIdentity,
                    activeLease.LeaseGeneration,
                    fixture.DocumentKind,
                    fixture.DocumentId,
                    expectedHash);
                var result = await queryTransport.RouteAndReadAsync(
                    connection,
                    desktopSession!,
                    activeLease,
                    query,
                    timeout.Token).ConfigureAwait(false);
                if (result is null || !result.Accepted ||
                    result.ContentHash is null ||
                    !result.ContentHash.FixedTimeEquals(expectedHash) ||
                    !fixture.Content.AsSpan().SequenceEqual(
                        Convert.FromBase64String(result.ContentBase64!)))
                {
                    return 37;
                }
            }

            var mismatchFixture = readFixtures[0];
            var mismatchQuery = new ReadDocumentQuery(
                ProtocolVersions.Current,
                MessageKinds.ReadDocumentQuery,
                "unity-worker-read-mismatch",
                activeLease.LeaseId,
                activeLease.Project.ProjectIdentity,
                activeLease.LeaseGeneration,
                mismatchFixture.DocumentKind,
                mismatchFixture.DocumentId,
                TypedHash.ComputeUtf8(ReadDocumentResult.ContentHashType, "different-content"));
            var mismatchResult = await queryTransport.RouteAndReadAsync(
                connection,
                desktopSession!,
                activeLease,
                mismatchQuery,
                timeout.Token).ConfigureAwait(false);
            if (mismatchResult is null || mismatchResult.Accepted ||
                mismatchResult.Diagnostic is null ||
                !string.Equals(
                    mismatchResult.Diagnostic.Code,
                    StableDiagnosticCodes.ProjectDocumentContentMismatch,
                    StringComparison.Ordinal))
            {
                return 37;
            }

            if (!await transport.RevokeAndAwaitAcknowledgementAsync(
                    connection,
                    activeLease,
                    "unity-worker-revoke",
                    timeout.Token).ConfigureAwait(false))
            {
                return 37;
            }

            acceptedReceipt =
                "PASS " + connection.Session.SessionId + " " + activeLease.LeaseId +
                " " + (readFixtures.Count + 1);
        }
        finally
        {
            var cleanupFailures = new List<Exception>();
            if (connection is not null)
            {
                try
                {
                    await connection.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    cleanupFailures.Add(exception);
                }
            }

            try
            {
                registrations?.Dispose();
            }
            catch (Exception exception)
            {
                cleanupFailures.Add(exception);
            }

            try
            {
                sessions?.Dispose();
            }
            catch (Exception exception)
            {
                cleanupFailures.Add(exception);
            }

            try
            {
                if (cleanupPins is null)
                {
                    DeleteScratch(ownedFiles, ownedDirectories);
                }
                else
                {
                    cleanupPins.DeleteOwnedTree(ownedFiles, ownedDirectories);
                    cleanupPins = null;
                }
            }
            catch (Exception exception)
            {
                cleanupFailures.Add(exception);
            }
            finally
            {
                cleanupPins?.Dispose();
            }

            if (cleanupFailures.Count != 0)
            {
                throw new AggregateException("WORKER_LIFECYCLE_CLEANUP_FAILED", cleanupFailures);
            }
        }

        if (acceptedReceipt is null)
        {
            return 38;
        }

        Console.Out.WriteLine(acceptedReceipt);
        Console.Out.Flush();
        return 0;
    }

    private static BrokerPolicy CreateTestPolicy(
        string pipeName,
        TypedHash userSidIdentity,
        TypedHash desktopImageIdentity,
        TypedHash workerImageIdentity,
        BrokerRegistrationDefinition definition)
    {
        var constructor = typeof(BrokerPolicy).GetConstructors(
                BindingFlags.Instance | BindingFlags.NonPublic)
            .Single();
        return (BrokerPolicy)constructor.Invoke(new object[]
        {
            pipeName,
            "broker-unity-worker-host",
            1L,
            userSidIdentity,
            new Dictionary<string, IReadOnlySet<TypedHash>>(StringComparer.Ordinal)
            {
                [PeerRoles.Desktop] = new HashSet<TypedHash> { desktopImageIdentity },
                [PeerRoles.Worker] = new HashSet<TypedHash> { workerImageIdentity }
            },
            new[] { definition }
        });
    }

    private static string GetVolumeGuid(string driveRoot)
    {
        var builder = new StringBuilder(64);
        if (!GetVolumeNameForVolumeMountPoint(driveRoot, builder, builder.Capacity))
        {
            throw new InvalidDataException();
        }

        return builder.ToString();
    }

    private static IReadOnlyList<ReadFixture> CreateReadFixtures(
        string repository,
        string project,
        List<string> ownedFiles,
        List<string> ownedDirectories)
    {
        var projectSettings = Path.Combine(project, "ProjectSettings");
        var vfxSettings = Path.Combine(projectSettings, "VFXComposer");
        var manifests = Path.Combine(vfxSettings, "BuildManifests");
        var docs = Path.Combine(repository, "docs");
        var contracts = Path.Combine(docs, "vfx-contracts");
        var traces = Path.Combine(docs, "vfx-traces");
        foreach (var directory in new[]
                 {
                     projectSettings, vfxSettings, manifests, docs, contracts, traces,
                 })
        {
            EnsureOwnedDirectory(directory, ownedDirectories);
        }

        var fixtures = new[]
        {
            new ReadFixture(
                DocumentKinds.LibraryIndex,
                "project",
                Path.Combine(vfxSettings, "LibraryIndex.json"),
                Encoding.UTF8.GetBytes("{\"schema\":\"vfxcomposer.library-index/1\",\"items\":[]}")),
            new ReadFixture(
                DocumentKinds.Manifest,
                "effect_fire",
                Path.Combine(manifests, "effect_fire.manifest.json"),
                Encoding.UTF8.GetBytes("{\"effectId\":\"effect_fire\",\"buildHash\":\"sha256:" +
                                       new string('a', 64) + "\"}")),
            new ReadFixture(
                DocumentKinds.Contract,
                "effect_fire",
                Path.Combine(contracts, "effect_fire.contract.json"),
                Encoding.UTF8.GetBytes("{\"schema\":\"vfx-design-contract/1\",\"effectId\":\"effect_fire\"}")),
            new ReadFixture(
                DocumentKinds.Trace,
                "effect_fire",
                Path.Combine(traces, "effect_fire.implementation-trace.json"),
                Encoding.UTF8.GetBytes("{\"schema\":\"vfx-implementation-trace/1\",\"effectId\":\"effect_fire\"}")),
        };
        foreach (var fixture in fixtures)
        {
            if (File.Exists(fixture.AbsolutePath) || Directory.Exists(fixture.AbsolutePath))
            {
                throw new InvalidDataException();
            }

            ownedFiles.Add(fixture.AbsolutePath);
            File.WriteAllBytes(fixture.AbsolutePath, fixture.Content);
        }

        return fixtures;
    }

    private static void EnsureOwnedDirectory(string path, List<string> ownedDirectories)
    {
        if (Directory.Exists(path) || File.Exists(path))
        {
            throw new InvalidDataException();
        }

        Directory.CreateDirectory(path);
        ownedDirectories.Add(path);
    }

    private static void DeleteScratch(
        IReadOnlyList<string> ownedFiles,
        IReadOnlyList<string> ownedDirectories)
    {
        for (var index = ownedFiles.Count - 1; index >= 0; index--)
        {
            var path = ownedFiles[index];
            if (!File.Exists(path))
            {
                continue;
            }

            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException();
            }

            File.Delete(path);
        }

        for (var index = ownedDirectories.Count - 1; index >= 0; index--)
        {
            var path = ownedDirectories[index];
            if (!Directory.Exists(path))
            {
                continue;
            }

            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException();
            }

            Directory.Delete(path, recursive: false);
        }
    }

    private sealed record ReadFixture(
        string DocumentKind,
        string DocumentId,
        string AbsolutePath,
        byte[] Content);

    private sealed class ScratchCleanupPins : IDisposable
    {
        private readonly WindowsPinnedProjectRoots _roots;
        private readonly Dictionary<string, WindowsDirectoryHandle> _directories;
        private int _disposed;

        private ScratchCleanupPins(
            WindowsPinnedProjectRoots roots,
            Dictionary<string, WindowsDirectoryHandle> directories)
        {
            _roots = roots;
            _directories = directories;
        }

        internal static ScratchCleanupPins Open(
            BrokerRegistrationDefinition definition,
            string scratch,
            string repository,
            string project,
            IReadOnlyCollection<string> expectedDirectories)
        {
            WindowsPinnedProjectRoots? roots = null;
            var opened = new List<WindowsDirectoryHandle>();
            try
            {
                roots = WindowsPinnedProjectRoots.Open(definition);
                var projectHandle = roots.Project.OpenChild("project");
                opened.Add(projectHandle);
                var projectSettings = projectHandle.OpenChild("ProjectSettings");
                opened.Add(projectSettings);
                var vfxSettings = projectSettings.OpenChild("VFXComposer");
                opened.Add(vfxSettings);
                var manifests = vfxSettings.OpenChild("BuildManifests");
                opened.Add(manifests);
                var docs = roots.Project.OpenChild("docs");
                opened.Add(docs);
                var contracts = docs.OpenChild("vfx-contracts");
                opened.Add(contracts);
                var traces = docs.OpenChild("vfx-traces");
                opened.Add(traces);

                var directories = new Dictionary<string, WindowsDirectoryHandle>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    [scratch] = roots.Repository,
                    [repository] = roots.Project,
                    [project] = projectHandle,
                    [Path.Combine(project, "ProjectSettings")] = projectSettings,
                    [Path.Combine(project, "ProjectSettings", "VFXComposer")] = vfxSettings,
                    [Path.Combine(project, "ProjectSettings", "VFXComposer", "BuildManifests")] = manifests,
                    [Path.Combine(repository, "docs")] = docs,
                    [Path.Combine(repository, "docs", "vfx-contracts")] = contracts,
                    [Path.Combine(repository, "docs", "vfx-traces")] = traces,
                };
                if (directories.Count != expectedDirectories.Count ||
                    expectedDirectories.Any(path => !directories.ContainsKey(path)) ||
                    !roots.ReplayIdentities() ||
                    directories.Values.Any(value => !value.ReplayIdentity()))
                {
                    throw new InvalidDataException();
                }

                opened.Clear();
                var result = new ScratchCleanupPins(roots, directories);
                roots = null;
                return result;
            }
            finally
            {
                for (var index = opened.Count - 1; index >= 0; index--)
                {
                    opened[index].Dispose();
                }

                roots?.Dispose();
            }
        }

        internal void DeleteOwnedTree(
            IReadOnlyList<string> ownedFiles,
            IReadOnlyList<string> ownedDirectories)
        {
            if (Volatile.Read(ref _disposed) != 0 ||
                _directories.Count != ownedDirectories.Count ||
                ownedDirectories.Any(path => !_directories.ContainsKey(path)))
            {
                throw new InvalidDataException();
            }

            try
            {
                for (var index = ownedFiles.Count - 1; index >= 0; index--)
                {
                    var path = ownedFiles[index];
                    var parent = Path.GetDirectoryName(path)
                        ?? throw new InvalidDataException();
                    if (!_directories.TryGetValue(parent, out var parentHandle) ||
                        !parentHandle.ReplayIdentity() ||
                        !File.Exists(path) ||
                        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidDataException();
                    }

                    File.Delete(path);
                    if (!parentHandle.ReplayIdentity())
                    {
                        throw new InvalidDataException();
                    }
                }

                for (var index = ownedDirectories.Count - 1; index >= 0; index--)
                {
                    var path = ownedDirectories[index];
                    if (!_directories.Remove(path, out var targetHandle) ||
                        !targetHandle.ReplayIdentity())
                    {
                        throw new InvalidDataException();
                    }

                    targetHandle.Dispose();
                    var parent = Path.GetDirectoryName(path);
                    WindowsDirectoryHandle? parentHandle = null;
                    if (parent is not null &&
                        _directories.TryGetValue(parent, out parentHandle) &&
                        !parentHandle.ReplayIdentity())
                    {
                        throw new InvalidDataException();
                    }

                    if (!Directory.Exists(path) ||
                        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidDataException();
                    }

                    Directory.Delete(path, recursive: false);
                    if (parentHandle is not null && !parentHandle.ReplayIdentity())
                    {
                        throw new InvalidDataException();
                    }
                }
            }
            finally
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            foreach (var handle in _directories.Values)
            {
                handle.Dispose();
            }

            _directories.Clear();
            _roots.Dispose();
        }
    }

    private static bool IsToken(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 128)
        {
            return false;
        }

        return value.All(character =>
            character is >= 'A' and <= 'Z' or
                >= 'a' and <= 'z' or
                >= '0' and <= '9' or '.' or '_' or ':' or '-');
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeNameForVolumeMountPoint(
        string volumeMountPoint,
        StringBuilder volumeName,
        int bufferLength);
}
