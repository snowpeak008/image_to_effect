using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Protocol.Queries;

namespace VFXComposer.LocalE2E.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LocalUserModeAdversarialTests
{
    [TestMethod]
    public async Task ActualWorkerRejectsBootstrapWithWrongNonceLength()
    {
        await using var fixture = new LocalUserModeE2EFixture();
        var project = fixture.CreateUnityProject();
        var invalidBootstrap = LocalUserModeE2EFixture.EncodeBootstrap(
            LocalUserModeE2EFixture.CreatePipeName(),
            generation: 1,
            LocalUserModeE2EFixture.CreateSessionId(1),
            RandomNumberGenerator.GetBytes(31));

        var exitCode = await LocalUserModeE2EFixture.RunMalformedBootstrapWorkerAsync(project, invalidBootstrap);

        Assert.AreEqual(31, exitCode);
        await LocalUserModeE2EFixture.AssertNoRuntimeResidueAsync();
    }

    [TestMethod]
    public async Task ActualWorkerRejectsWrongGenerationSessionAndLocatorIdentityBeforeAcknowledgement()
    {
        await using var fixture = new LocalUserModeE2EFixture();
        var selected = fixture.CreateUnityProject();
        var other = fixture.CreateUnityProject();

        await AssertLocatorRejectedAsync(
            selected,
            peer => LocalUserModeE2EFixture.CreateLocator(
                selected,
                peer.Generation,
                peer.SessionId,
                peer.WorkerProcessEpoch,
                locatorGeneration: peer.Generation + 1,
                locatorSessionId: LocalUserModeE2EFixture.CreateSessionId(peer.Generation + 1)));
        await AssertLocatorRejectedAsync(
            selected,
            peer => LocalUserModeE2EFixture.CreateLocator(
                selected,
                peer.Generation,
                LocalUserModeE2EFixture.CreateSessionId(peer.Generation),
                peer.WorkerProcessEpoch));
        await AssertLocatorRejectedAsync(
            selected,
            peer => LocalUserModeE2EFixture.CreateLocator(
                selected,
                peer.Generation,
                peer.SessionId,
                peer.WorkerProcessEpoch,
                identityProject: other));

        await LocalUserModeE2EFixture.AssertNoRuntimeResidueAsync();
    }

    [TestMethod]
    public async Task ActualWorkerRejectsMalformedC2ProtocolBeforeProjectRead()
    {
        await using var fixture = new LocalUserModeE2EFixture();
        var project = fixture.CreateUnityProject();
        {
            await using var peer = await LocalUserModeE2EFixture.LocalWorkerPeer.StartAsync(project);
            await peer.SendRawFrameAsync("{}"u8.ToArray());
            Assert.AreEqual(31, await peer.WaitForExitAsync());
        }

        await LocalUserModeE2EFixture.AssertNoRuntimeResidueAsync();
    }

    [TestMethod]
    public async Task PublicDesktopRouteRejectsMissingMarkerAndTraversalSelection()
    {
        await using var fixture = new LocalUserModeE2EFixture();
        var missingMarker = fixture.CreateUnityProject();
        File.Delete(Path.Combine(missingMarker.Root, "Packages", "manifest.json"));
        await using (var missingMarkerSession = await LocalUserModeE2EFixture.ConnectDesktopSessionAsync())
        {
            await AssertFailsAsync(() => missingMarkerSession.SelectAsync(missingMarker.Root).AsTask());
        }

        var traversal = fixture.CreateUnityProject();
        var traversalCandidate = traversal.Root + "\\..\\" + Path.GetFileName(traversal.Root);
        await using (var traversalSession = await LocalUserModeE2EFixture.ConnectDesktopSessionAsync())
        {
            await AssertFailsAsync(() => traversalSession.SelectAsync(traversalCandidate).AsTask());
        }

        await LocalUserModeE2EFixture.AssertNoRuntimeResidueAsync();
    }

    [TestMethod]
    public async Task PublicDesktopRouteRejectsReparseProjectMarkerOrUsesAStaticPredicateWhenSetupIsUnavailable()
    {
        await using var fixture = new LocalUserModeE2EFixture();
        var project = fixture.CreateUnityProject();
        if (!await fixture.TryReplaceAssetsWithReparsePointAsync(project))
        {
            Assert.IsTrue(LocalUserModeE2EFixture.HasWorkerReparseRejectionPredicate(),
                "The no-privilege environment could not create a test-only reparse point, and the Worker predicate was absent.");
            await LocalUserModeE2EFixture.AssertNoRuntimeResidueAsync();
            return;
        }

        await using var session = await LocalUserModeE2EFixture.ConnectDesktopSessionAsync();
        await AssertFailsAsync(() => session.SelectAsync(project.Root).AsTask());

        await session.DisposeAsync();
        await LocalUserModeE2EFixture.AssertNoRuntimeResidueAsync();
    }

    [TestMethod]
    public async Task ActualWorkerBoundsOversizeAndMalformedJsonWithoutReturningProjectContent()
    {
        await using var fixture = new LocalUserModeE2EFixture();
        var oversized = fixture.CreateUnityProject();
        File.WriteAllText(
            Path.Combine(oversized.Root, "ProjectSettings", "VFXComposer", "LibraryIndex.json"),
            "{" + new string(' ', 512 * 1024) + "}",
            new UTF8Encoding(false));
        await using (var oversizedSession = await LocalUserModeE2EFixture.ConnectDesktopSessionAsync())
        {
            await oversizedSession.SelectAsync(oversized.Root);
            var result = await oversizedSession.ReadAsync(DocumentKinds.LibraryIndex, "project");
            Assert.IsFalse(result.Accepted);
            Assert.AreEqual("VFXP0008", result.DiagnosticCode);
            Assert.IsNull(result.ContentBase64);
        }

        var malformed = fixture.CreateUnityProject(libraryIndex: "{\"bad\":}");
        await using (var malformedSession = await LocalUserModeE2EFixture.ConnectDesktopSessionAsync())
        {
            await malformedSession.SelectAsync(malformed.Root);
            var result = await malformedSession.ReadAsync(DocumentKinds.LibraryIndex, "project");
            Assert.IsFalse(result.Accepted);
            Assert.AreEqual("VFXP0008", result.DiagnosticCode);
            Assert.IsNull(result.ContentBase64);
        }

        await LocalUserModeE2EFixture.AssertNoRuntimeResidueAsync();
    }

    [TestMethod]
    public async Task ActualWorkerRejectsUnsupportedAndTraversalShapedDocumentRequests()
    {
        await using var fixture = new LocalUserModeE2EFixture();
        var project = fixture.CreateUnityProject();
        await using var session = await LocalUserModeE2EFixture.ConnectDesktopSessionAsync();
        await session.SelectAsync(project.Root);

        var unsupported = await session.ReadAsync(DocumentKinds.Contract, "sample");
        Assert.IsFalse(unsupported.Accepted);
        Assert.AreEqual("VFXP0008", unsupported.DiagnosticCode);
        await AssertFailsAsync(() => session.ReadAsync(DocumentKinds.Manifest, "sample..escape").AsTask());

        await session.DisposeAsync();
        await LocalUserModeE2EFixture.AssertNoRuntimeResidueAsync();
    }

    private static async Task AssertLocatorRejectedAsync(
        LocalUserModeE2EFixture.LocalUnityProject selected,
        Func<LocalUserModeE2EFixture.LocalWorkerPeer, VFXComposer.Protocol.Registration.WorkerProjectLocator> createLocator)
    {
        await using var peer = await LocalUserModeE2EFixture.LocalWorkerPeer.StartAsync(selected);
        await peer.SendLocatorAsync(createLocator(peer));
        Assert.AreEqual(31, await peer.WaitForExitAsync());
    }

    private static async Task AssertFailsAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or
            EndOfStreamException or IOException or InvalidOperationException or OperationCanceledException)
        {
            return;
        }

        Assert.Fail("The bounded user-mode route accepted an invalid request.");
    }
}
