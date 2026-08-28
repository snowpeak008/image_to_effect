using System.IO.Pipes;
using System.Text.Json;
using VFXComposer.Broker.Configuration;
using VFXComposer.Broker.Ipc;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;
using VFXComposer.Protocol.Json;

namespace VFXComposer.Broker.Tests;

[TestClass]
public sealed class NamedPipeScaffoldTests
{
    [TestMethod]
    public async Task CurrentUserOnlyPipeRoundTripsOneStrictAuthenticatedScaffoldSession()
    {
        var pipeName = "vfxcomposer-test-" + Guid.NewGuid().ToString("N");
        var currentProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
        using var observed = WindowsNamedPipePeerFactsSource.ObserveProcess(currentProcessId);
        var sid = observed.UserSidIdentity;
        var desktopImage = observed.ImageIdentity;
        var workerImage = TypedHash.ComputeUtf8(PeerHello.ProcessImageIdentityType, "worker-image");
        var policy = BrokerTestFactory.CreatePolicy(
            pipeName,
            "broker-01",
            1,
            sid,
            desktopImage,
            workerImage);
        using var sessions = new PeerSessionRegistry(policy);
        var facts = new WindowsNamedPipePeerFactsSource();
        var host = new NamedPipeBrokerHost(
            policy,
            new NamedPipePeerAuthenticator(facts, sessions));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var acceptTask = host.AcceptOneAsync(timeout.Token);

        await using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await client.ConnectAsync(timeout.Token);
        var hello = new PeerHello(
            "request-01",
            PeerRoles.Desktop,
            "desktop-01",
            currentProcessId,
            observed.ProcessEpoch,
            [PeerCapabilityIds.PeerSessionV1, PeerCapabilityIds.ReadOnlyQueryV1],
            desktopImage);
        await NamedPipeBrokerHost.WriteFrameAsync(
            client,
            JsonSerializer.SerializeToUtf8Bytes(hello),
            timeout.Token);
        await client.FlushAsync(timeout.Token);
        var response = await NamedPipeBrokerHost.ReadFrameAsync(client, timeout.Token);
        var receipt = StrictWireCodec.Decode<PeerSessionAccepted>(response);
        var connection = await acceptTask;

        Assert.AreEqual(connection.Session.SessionId, receipt.SessionId);
        Assert.AreEqual(PeerRoles.Desktop, receipt.PeerRole);
        Assert.IsTrue(connection.Session.IsUsable);
        Task? reentrantDispose = null;
        sessions.SessionRevoked += session =>
        {
            if (ReferenceEquals(session, connection.Session))
            {
                reentrantDispose = connection.DisposeAsync().AsTask();
            }
        };
        var disposeA = connection.DisposeAsync().AsTask();
        var disposeB = connection.DisposeAsync().AsTask();
        await Task.WhenAll(disposeA, disposeB);
        Assert.IsNotNull(reentrantDispose);
        await reentrantDispose!;
        Assert.IsFalse(connection.Session.IsUsable);
    }

    [TestMethod]
    public void FrameHeaderRejectsWrongMagicFlagsVersionAndLength()
    {
        var valid = new byte[WireFrameHeader.HeaderLength];
        WireFrameHeader.Write(valid, 42);
        Assert.AreEqual(42, WireFrameHeader.Read(valid));

        foreach (var mutation in new Action<byte[]>[]
                 {
                     value => value[0] = (byte)'X',
                     value => value[4] = 2,
                     value => value[5] = 1,
                     value => Array.Clear(value, 6, 4),
                 })
        {
            var candidate = valid.ToArray();
            mutation(candidate);
            Assert.ThrowsExactly<ArgumentException>(() => WireFrameHeader.Read(candidate));
        }
    }
}
