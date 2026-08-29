using System.Diagnostics;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Protocol.Registration;

namespace VFXComposer.LocalE2E.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LocalUserModeContractTests
{
    [TestMethod]
    public void StandaloneWorkerIsProtocolOnlyAndHasNoUnityNewtonsoftOrTestDefineSurface()
    {
        LocalUserModeE2EFixture.AssertRuntimeBundle();
        var repositoryRoot = LocalUserModeE2EFixture.FindRepositoryRoot();
        var project = ReadSource(repositoryRoot, "services/VFXComposer.UnityWorker/VFXComposer.UnityWorker.csproj");
        var workerHost = ReadSource(repositoryRoot, "services/VFXComposer.UnityWorker/UserModeUnityWorkerHost.cs");
        var assembly = Assembly.LoadFrom(Path.Combine(
            LocalUserModeE2EFixture.RuntimeDirectory,
            "VFXComposer.UnityWorker.dll"));

        CollectionAssert.AreEquivalent(
            new[] { "VFXComposer.Protocol" },
            assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(name => name.StartsWith("VFXComposer.", StringComparison.Ordinal))
                .ToArray());
        Assert.IsFalse(project.Contains("Newtonsoft", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(project.Contains("UNITY_INCLUDE_TESTS", StringComparison.Ordinal));
        Assert.IsFalse(project.Contains("Compile Include=", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(workerHost.Contains("UnityEngine", StringComparison.Ordinal));
        Assert.IsTrue(workerHost.Contains("StrictWireCodec.Decode<WorkerProjectLocator>", StringComparison.Ordinal));
        Assert.IsTrue(workerHost.Contains("StrictWireCodec.Decode<ReadDocumentQuery>", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ActualWorkerUsesVfxcUmb1Umh1AndStrictC2AcknowledgementBytes()
    {
        await using var fixture = new LocalUserModeE2EFixture();
        var project = fixture.CreateUnityProject();
        {
            await using var peer = await LocalUserModeE2EFixture.LocalWorkerPeer.StartAsync(project);
            var locator = LocalUserModeE2EFixture.CreateLocator(
                project,
                peer.Generation,
                peer.SessionId,
                peer.WorkerProcessEpoch);
            await peer.SendLocatorAsync(locator);
            var acknowledgement = await peer.ReadLocatorAcknowledgementAsync();

            Assert.AreEqual(WorkerProjectLocatorAcknowledgement.AcceptedDisposition, acknowledgement.Disposition);
            Assert.AreEqual(locator.RequestId, acknowledgement.RequestId);
            Assert.AreEqual(locator.BrokerGeneration, acknowledgement.BrokerGeneration);
            Assert.AreEqual(locator.WorkerSessionId, acknowledgement.WorkerSessionId);
            Assert.IsTrue(locator.SelfHash.FixedTimeEquals(acknowledgement.LocatorSelfHash));
        }

        await LocalUserModeE2EFixture.AssertNoRuntimeResidueAsync();
    }

    [TestMethod]
    public void CurrentUserOnlyIsTheWrongUserBoundaryWithoutCreatingAccountsOrPrivilege()
    {
        var repositoryRoot = LocalUserModeE2EFixture.FindRepositoryRoot();
        var host = ReadSource(repositoryRoot, "services/VFXComposer.UnityWorker/UserModeUnityWorkerHost.cs");
        var peerCodec = ReadSource(repositoryRoot, "services/VFXComposer.UnityWorker/UserModeWorkerBootstrapPeerCodec.cs");
        var fixtureSource = ReadSource(repositoryRoot, "tests/VFXComposer.LocalE2E.Tests/LocalUserModeE2EFixture.cs");
        var accountCreationApi = string.Concat("Create", "User");

        Assert.IsTrue(host.Contains("PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly", StringComparison.Ordinal));
        Assert.IsTrue(peerCodec.Contains("UMB1", StringComparison.Ordinal));
        Assert.IsFalse(fixtureSource.Contains(accountCreationApi, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(fixtureSource.Contains("runas", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(fixtureSource.Contains("SeDebugPrivilege", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task WorkerProgramOnlyAdmitsTheUserModeChildArgument()
    {
        var repositoryRoot = LocalUserModeE2EFixture.FindRepositoryRoot();
        var program = ReadSource(repositoryRoot, "services/VFXComposer.UnityWorker/Program.cs");
        Assert.IsTrue(program.Contains("--user-mode-worker-child", StringComparison.Ordinal));
        Assert.IsFalse(program.Contains("--u4-scripted-worker-peer", StringComparison.Ordinal));
        Assert.IsFalse(program.Contains("HandleProbe", StringComparison.Ordinal));

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(LocalUserModeE2EFixture.WorkerExecutable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        Assert.IsTrue(process.Start());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await process.WaitForExitAsync(timeout.Token);
        Assert.AreEqual(23, process.ExitCode);
        await LocalUserModeE2EFixture.AssertNoRuntimeResidueAsync();
    }

    private static string ReadSource(string repositoryRoot, string relativePath) =>
        File.ReadAllText(Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
}
