using System.IO.Pipes;
using System.Reflection;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using VFXComposer.Broker.Ipc;
using VFXComposer.Protocol.Ipc;

namespace VFXComposer.Broker.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
public sealed class UserModeNamedPipeServerTests
{
    [TestMethod]
    public void RandomNamesSessionsAndNoncesAreDistinctAndCanonical()
    {
        using var first = UserModeNamedPipeServer.Create(31);
        using var second = UserModeNamedPipeServer.Create(31);
        using var firstBootstrap = first.CreateBootstrap();
        using var secondBootstrap = second.CreateBootstrap();

        Assert.AreNotEqual(first.PipeName, second.PipeName);
        Assert.AreNotEqual(first.SessionId, second.SessionId);
        Assert.IsTrue(first.PipeName.StartsWith("vfxcomposer-um-", StringComparison.Ordinal));
        Assert.AreEqual(64, first.PipeName["vfxcomposer-um-".Length..].Length);
        Assert.AreEqual(32, firstBootstrap.CopyNonce().Length);
        Assert.IsFalse(CryptographicOperations.FixedTimeEquals(
            firstBootstrap.CopyNonce(), secondBootstrap.CopyNonce()));
    }

    [TestMethod]
    public void GenerationMustBePositive()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => UserModeNamedPipeServer.Create(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => UserModeNamedPipeServer.Create(-1));
    }

    [TestMethod]
    public void CanonicalNamesRejectCaseLengthAndCrossGenerationSession()
    {
        using var server = UserModeNamedPipeServer.Create(311);
        Assert.IsTrue(UserModeNamedPipeServer.IsCanonicalPipeName(server.PipeName));
        Assert.IsTrue(UserModeNamedPipeServer.IsCanonicalSessionId(server.SessionId, 311));
        Assert.IsFalse(UserModeNamedPipeServer.IsCanonicalPipeName(server.PipeName.ToUpperInvariant()));
        Assert.IsFalse(UserModeNamedPipeServer.IsCanonicalPipeName(server.PipeName + "0"));
        Assert.IsFalse(UserModeNamedPipeServer.IsCanonicalSessionId(server.SessionId, 312));
    }

    [TestMethod]
    public void CurrentUserOnlyAndCurrentSidAreExplicitStructuralBoundary()
    {
        using var server = UserModeNamedPipeServer.Create(32);
        Assert.IsTrue(server.UsesCurrentUserOnly);
        Assert.IsTrue(server.CurrentUserSid.StartsWith("S-1-", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task BootstrapUsesStrictWireFrameAndRoundTripsExactly()
    {
        using var server = UserModeNamedPipeServer.Create(33);
        using var stream = new MemoryStream();
        await server.WriteBootstrapAsync(stream);
        stream.Position = 0;
        using var decoded = await UserModeNamedPipeServer.ReadBootstrapAsync(stream);

        Assert.AreEqual(server.PipeName, decoded.PipeName);
        Assert.AreEqual(server.SessionId, decoded.SessionId);
        Assert.AreEqual(server.Generation, decoded.Generation);
        Assert.AreEqual(UserModeNamedPipeServer.NonceLength, decoded.CopyNonce().Length);
    }

    [TestMethod]
    public async Task MalformedBootstrapMagicFailsStrictly()
    {
        using var server = UserModeNamedPipeServer.Create(34);
        using var bootstrap = server.CreateBootstrap();
        var payload = UserModeNamedPipeServer.EncodeBootstrap(bootstrap);
        payload[0] ^= 1;
        using var stream = new MemoryStream();
        await NamedPipeBrokerHost.WriteFrameAsync(stream, payload, CancellationToken.None);
        stream.Position = 0;

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
            UserModeNamedPipeServer.ReadBootstrapAsync(stream));
    }

    [TestMethod]
    public void BootstrapAndServerDiagnosticsRedactPipeAndNonce()
    {
        using var server = UserModeNamedPipeServer.Create(35);
        using var bootstrap = server.CreateBootstrap();
        Assert.AreEqual("UserModeNamedPipeServer(REDACTED)", server.ToString());
        Assert.AreEqual("UserModeWorkerBootstrap(REDACTED)", bootstrap.ToString());
        Assert.IsFalse(server.ToString().Contains(server.PipeName, StringComparison.Ordinal));
    }

    [TestMethod]
    public void BootstrapMaterialCanBeIssuedOnlyOnce()
    {
        using var server = UserModeNamedPipeServer.Create(351);
        using var bootstrap = server.CreateBootstrap();
        Assert.ThrowsExactly<InvalidOperationException>(() => server.CreateBootstrap());
    }

    [TestMethod]
    public async Task TimeoutConsumesEndpointAndRejectsReplayAccept()
    {
        using var server = UserModeNamedPipeServer.Create(36);
        await using var child = UserModeChildProcess.Launch(
            UserModeSessionTestChild.ExpectedExecutablePath,
            UserModeSessionTestChild.Create("no-connect"));

        await Assert.ThrowsExactlyAsync<TimeoutException>(() =>
            server.AcceptAsync(child, TimeSpan.FromMilliseconds(150)));
        Assert.IsTrue(server.IsConsumed);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            server.AcceptAsync(child, TimeSpan.FromMilliseconds(150)));
        Assert.ThrowsExactly<InvalidOperationException>(() => server.CreateBootstrap());
    }

    [TestMethod]
    public async Task DifferentProcessCannotClaimPinnedChildEvenAsSameUser()
    {
        using var server = UserModeNamedPipeServer.Create(37);
        await using var child = UserModeChildProcess.Launch(
            UserModeSessionTestChild.ExpectedExecutablePath,
            UserModeSessionTestChild.Create("no-connect"));
        var accept = server.AcceptAsync(child, TimeSpan.FromSeconds(5));
        await using var wrongClient = new NamedPipeClientStream(
            ".",
            server.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await wrongClient.ConnectAsync(CancellationToken.None);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => accept);
        Assert.IsTrue(server.IsConsumed);
    }

    [TestMethod]
    public void OwnedTypesExposeNoServiceSaclOrGlobalPidRegistrySurface()
    {
        var forbidden = new[] { "Service", "Sacl", "LocalSystem", "GlobalOwnership", "Installer" };
        var members = new[]
            {
                typeof(UserModeNamedPipeServer),
                typeof(UserModeWorkerBootstrap),
                typeof(UserModeWorkerConnection),
            }
            .SelectMany(type => type.GetMembers(
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic))
            .Select(member => member.Name)
            .ToArray();
        Assert.IsFalse(members.Any(member => forbidden.Any(word =>
            member.Contains(word, StringComparison.OrdinalIgnoreCase))));
    }
}
