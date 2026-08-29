using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Client;
using VFXComposer.Protocol.Queries;

namespace VFXComposer.LocalE2E.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LocalUserModeHappyPathTests
{
    [TestMethod]
    public async Task PublicDesktopSessionReadsBoundLibraryIndexThroughBrokerAndActualWorker()
    {
        await using var fixture = new LocalUserModeE2EFixture();
        var project = fixture.CreateUnityProject("{\"library\":\"u5-happy\"}");
        await using var session = await LocalUserModeE2EFixture.ConnectDesktopSessionAsync();

        await session.SelectAsync(project.Root);
        var result = await session.ReadAsync(DocumentKinds.LibraryIndex, "project");

        Assert.IsTrue(result.Accepted);
        Assert.AreEqual(DocumentKinds.LibraryIndex, result.DocumentKind);
        Assert.AreEqual("project", result.DocumentId);
        Assert.AreEqual("{\"library\":\"u5-happy\"}", Decode(result.ContentBase64));
        Assert.AreEqual(UserModeDesktopSessionState.Selected, session.State);

        await session.DisposeAsync();
        await LocalUserModeE2EFixture.AssertNoRuntimeResidueAsync();
    }

    [TestMethod]
    public async Task PublicDesktopSessionReadsBoundManifestThroughBrokerAndActualWorker()
    {
        await using var fixture = new LocalUserModeE2EFixture();
        var project = fixture.CreateUnityProject(manifest: "{\"manifest\":\"u5-sample\"}");
        await using var session = await LocalUserModeE2EFixture.ConnectDesktopSessionAsync();

        await session.SelectAsync(project.Root);
        var result = await session.ReadAsync(DocumentKinds.Manifest, "sample");

        Assert.IsTrue(result.Accepted);
        Assert.AreEqual(DocumentKinds.Manifest, result.DocumentKind);
        Assert.AreEqual("sample", result.DocumentId);
        Assert.AreEqual("{\"manifest\":\"u5-sample\"}", Decode(result.ContentBase64));
        Assert.AreEqual(UserModeDesktopSessionState.Selected, session.State);

        await session.DisposeAsync();
        await LocalUserModeE2EFixture.AssertNoRuntimeResidueAsync();
    }

    private static string Decode(string? base64)
    {
        Assert.IsNotNull(base64);
        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }
}
