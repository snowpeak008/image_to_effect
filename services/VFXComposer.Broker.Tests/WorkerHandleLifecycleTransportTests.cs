using System.Globalization;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32.SafeHandles;
using VFXComposer.Client;
using VFXComposer.Broker.Configuration;
using VFXComposer.Broker.Ipc;
using VFXComposer.Broker.Native;
using VFXComposer.Broker.Queries;
using VFXComposer.Broker.Registration;
using VFXComposer.Protocol;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;
using VFXComposer.Protocol.Json;
using VFXComposer.Protocol.Queries;
using VFXComposer.Protocol.Registration;

namespace VFXComposer.Broker.Tests;

[TestClass]
public sealed class WorkerHandleLifecycleTransportTests
{
    [TestMethod]
    public async Task CurrentUserOnlyWorkerPipeCompletesGrantAndRevokeAcknowledgementLifecycle()
    {
        RequireWindows64();
        await using var fixture = await ConnectedFixture.CreateAsync();
        var transport = new WorkerHandleLifecycleTransport(fixture.Registrations);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var worker = RunWorkerLifecycleAsync(fixture.Client, false, timeout.Token);

        Assert.IsTrue(await transport.PublishGrantAndAwaitAcknowledgementAsync(
            fixture.Connection,
            fixture.Lease,
            "pipe-grant-01",
            timeout.Token));
        Assert.AreEqual(WorkerHandleLeaseState.GrantAcknowledged, fixture.Lease.HandleState);

        Assert.IsTrue(await transport.RevokeAndAwaitAcknowledgementAsync(
            fixture.Connection,
            fixture.Lease,
            "pipe-revoke-01",
            timeout.Token));
        Assert.AreEqual(WorkerHandleLeaseState.Revoked, fixture.Lease.HandleState);
        Assert.IsNull(fixture.Lease.WorkerHandles);

        var result = await worker;
        Assert.AreEqual(fixture.Lease.LeaseId, result.Grant.LeaseId);
        Assert.IsNotNull(result.Revoke);
        Assert.IsTrue(result.Grant.SelfHash.FixedTimeEquals(result.Revoke!.GrantSelfHash));
    }

    [TestMethod]
    public async Task GrantAcknowledgedPipeRoutesExactReadResultBeforeRevoke()
    {
        RequireWindows64();
        await using var fixture = await ConnectedFixture.CreateAsync();
        var lifecycle = new WorkerHandleLifecycleTransport(fixture.Registrations);
        var readTransport = new WorkerReadQueryTransport(fixture.Router);
        var content = Encoding.UTF8.GetBytes("{\"effectId\":\"effect_fire\"}");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var worker = RunWorkerReadLifecycleAsync(
            fixture.Client,
            content,
            sendWrongCorrelation: false,
            queryReceived: null,
            releaseResponse: null,
            timeout.Token);

        Assert.IsTrue(await lifecycle.PublishGrantAndAwaitAcknowledgementAsync(
            fixture.Connection,
            fixture.Lease,
            "pipe-read-grant",
            timeout.Token));
        var query = CreateReadQuery(fixture, "pipe-read-query", content);
        var result = await readTransport.RouteAndReadAsync(
            fixture.Connection,
            fixture.DesktopSession,
            fixture.Lease,
            query,
            timeout.Token);

        Assert.IsNotNull(result);
        Assert.IsTrue(result!.Accepted);
        CollectionAssert.AreEqual(content, Convert.FromBase64String(result.ContentBase64!));
        Assert.IsTrue(query.ExpectedContentHash!.FixedTimeEquals(result.ContentHash!));
        Assert.IsTrue(await lifecycle.RevokeAndAwaitAcknowledgementAsync(
            fixture.Connection,
            fixture.Lease,
            "pipe-read-revoke",
            timeout.Token));
        Assert.IsNotNull((await worker).Revoke);
    }

    [TestMethod]
    public async Task AuthenticatedDesktopPipeRoutesClientBuiltReadWithoutProjectPath()
    {
        RequireWindows64();
        await using var fixture = await ConnectedFixture.CreateAsync();
        var lifecycle = new WorkerHandleLifecycleTransport(fixture.Registrations);
        var desktopTransport = new DesktopReadQueryTransport(
            new WorkerReadQueryTransport(fixture.Router));
        var content = Encoding.UTF8.GetBytes("{\"effectId\":\"effect_fire\"}");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var worker = RunWorkerReadLifecycleAsync(
            fixture.Client,
            content,
            sendWrongCorrelation: false,
            queryReceived: null,
            releaseResponse: null,
            timeout.Token);

        Assert.IsTrue(await lifecycle.PublishGrantAndAwaitAcknowledgementAsync(
            fixture.Connection,
            fixture.Lease,
            "desktop-read-grant",
            timeout.Token));

        var serve = desktopTransport.ServeOneAsync(
            fixture.DesktopConnection,
            fixture.Connection,
            fixture.Lease,
            timeout.Token).AsTask();
        var client = new VfxComposerClient(
            new PipeClientConnection(fixture.DesktopClient));
        var result = await client.ReadDocumentAsync(
            fixture.LeaseDescriptor,
            DocumentKinds.Manifest,
            "effect_fire",
            TypedHash.Compute(ReadDocumentResult.ContentHashType, content),
            new RequestCorrelation("desktop-read-query", "desktop-read-idempotency"),
            timeout.Token);

        Assert.IsTrue(await serve);
        Assert.IsTrue(result.Accepted);
        CollectionAssert.AreEqual(content, Convert.FromBase64String(result.ContentBase64!));
        Assert.IsTrue(await lifecycle.RevokeAndAwaitAcknowledgementAsync(
            fixture.Connection,
            fixture.Lease,
            "desktop-read-revoke",
            timeout.Token));
        Assert.IsNotNull((await worker).Revoke);
    }

    [TestMethod]
    public async Task MalformedDesktopQueryClosesOnlyAuthenticatedDesktopRoute()
    {
        RequireWindows64();
        await using var fixture = await ConnectedFixture.CreateAsync();
        var transport = new DesktopReadQueryTransport(
            new WorkerReadQueryTransport(fixture.Router));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var serve = transport.ServeOneAsync(
            fixture.DesktopConnection,
            fixture.Connection,
            fixture.Lease,
            timeout.Token).AsTask();

        await NamedPipeBrokerHost.WriteFrameAsync(
            fixture.DesktopClient,
            Encoding.UTF8.GetBytes("{]"),
            timeout.Token);
        await fixture.DesktopClient.FlushAsync(timeout.Token);

        Assert.IsFalse(await serve);
        Assert.IsFalse(fixture.DesktopConnection.Session.IsUsable);
        Assert.IsTrue(fixture.Connection.Session.IsUsable);
    }

    [TestMethod]
    public async Task UnknownDesktopLeaseClosesBeforeAnyWorkerQuery()
    {
        RequireWindows64();
        await using var fixture = await ConnectedFixture.CreateAsync();
        var transport = new DesktopReadQueryTransport(
            new WorkerReadQueryTransport(fixture.Router));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var serve = transport.ServeOneAsync(
            fixture.DesktopConnection,
            fixture.Connection,
            fixture.Lease,
            timeout.Token).AsTask();
        var client = new VfxComposerClient(
            new PipeClientConnection(fixture.DesktopClient));

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(async () =>
            await client.ReadDocumentAsync(
                CreateMismatchedLeaseDescriptor(fixture.LeaseDescriptor),
                DocumentKinds.Manifest,
                "effect_fire",
                expectedContentHash: null,
                new RequestCorrelation("unknown-lease-query", "unknown-lease-idempotency"),
                timeout.Token));

        Assert.IsFalse(await serve);
        Assert.IsFalse(fixture.DesktopConnection.Session.IsUsable);
        Assert.IsTrue(fixture.Connection.Session.IsUsable);
    }

    [TestMethod]
    public async Task ConcurrentDesktopServeIsRejectedWithoutUnboundedQueue()
    {
        RequireWindows64();
        await using var fixture = await ConnectedFixture.CreateAsync();
        var transport = new DesktopReadQueryTransport(
            new WorkerReadQueryTransport(fixture.Router));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var first = transport.ServeOneAsync(
            fixture.DesktopConnection,
            fixture.Connection,
            fixture.Lease,
            timeout.Token).AsTask();
        var second = transport.ServeOneAsync(
            fixture.DesktopConnection,
            fixture.Connection,
            fixture.Lease,
            timeout.Token).AsTask();

        Assert.IsFalse(await second);
        Assert.IsFalse(await first);
        Assert.IsFalse(fixture.DesktopConnection.Session.IsUsable);
        Assert.IsTrue(fixture.Connection.Session.IsUsable);
    }

    [TestMethod]
    public async Task RegistrationRevokeWaitsForExactDesktopResponsePublication()
    {
        RequireWindows64();
        await using var fixture = await ConnectedFixture.CreateAsync();
        var lifecycle = new WorkerHandleLifecycleTransport(fixture.Registrations);
        var desktopTransport = new DesktopReadQueryTransport(
            new WorkerReadQueryTransport(fixture.Router));
        var content = Enumerable.Repeat(
            (byte)'a',
            ReadDocumentResult.MaximumDecodedBytes).ToArray();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var worker = RunWorkerReadLifecycleAsync(
            fixture.Client,
            content,
            sendWrongCorrelation: false,
            queryReceived: null,
            releaseResponse: null,
            timeout.Token);

        Assert.IsTrue(await lifecycle.PublishGrantAndAwaitAcknowledgementAsync(
            fixture.Connection,
            fixture.Lease,
            "publication-grant",
            timeout.Token));
        var query = CreateReadQuery(fixture, "publication-query", content);
        var serve = desktopTransport.ServeOneAsync(
            fixture.DesktopConnection,
            fixture.Connection,
            fixture.Lease,
            timeout.Token).AsTask();
        await NamedPipeBrokerHost.WriteFrameAsync(
            fixture.DesktopClient,
            JsonSerializer.SerializeToUtf8Bytes(query),
            timeout.Token);
        await fixture.DesktopClient.FlushAsync(timeout.Token);

        while (!fixture.Lease.HasActiveReadResponsePublication)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), timeout.Token);
        }

        var revoke = Task.Run(
            () => fixture.Registrations.RevokeLease(fixture.Lease.LeaseId),
            timeout.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        Assert.IsFalse(revoke.IsCompleted,
            "Revocation must wait while the exact response owns its publication reservation.");

        var result = StrictWireCodec.Decode<ReadDocumentResult>(
            await NamedPipeBrokerHost.ReadFrameAsync(fixture.DesktopClient, timeout.Token));
        Assert.IsTrue(result.Accepted);
        Assert.AreEqual(content.Length, result.ByteLength);
        Assert.IsTrue(await serve);
        Assert.IsTrue(await revoke);
        Assert.AreEqual(WorkerHandleLeaseState.RevocationPending, fixture.Lease.HandleState);
        Assert.IsTrue(await lifecycle.RevokeAndAwaitAcknowledgementAsync(
            fixture.Connection,
            fixture.Lease,
            "publication-revoke",
            timeout.Token));
        Assert.IsNotNull((await worker).Revoke);
    }

    [TestMethod]
    public async Task DesktopConnectionDisposeWaitsForExactResponsePublication()
    {
        RequireWindows64();
        await using var fixture = await ConnectedFixture.CreateAsync();
        var lifecycle = new WorkerHandleLifecycleTransport(fixture.Registrations);
        var desktopTransport = new DesktopReadQueryTransport(
            new WorkerReadQueryTransport(fixture.Router));
        var content = Enumerable.Repeat(
            (byte)'b',
            ReadDocumentResult.MaximumDecodedBytes).ToArray();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var worker = RunWorkerReadLifecycleAsync(
            fixture.Client,
            content,
            sendWrongCorrelation: false,
            queryReceived: null,
            releaseResponse: null,
            timeout.Token);

        Assert.IsTrue(await lifecycle.PublishGrantAndAwaitAcknowledgementAsync(
            fixture.Connection,
            fixture.Lease,
            "desktop-dispose-grant",
            timeout.Token));
        var query = CreateReadQuery(fixture, "desktop-dispose-query", content);
        var serve = desktopTransport.ServeOneAsync(
            fixture.DesktopConnection,
            fixture.Connection,
            fixture.Lease,
            timeout.Token).AsTask();
        await NamedPipeBrokerHost.WriteFrameAsync(
            fixture.DesktopClient,
            JsonSerializer.SerializeToUtf8Bytes(query),
            timeout.Token);
        await fixture.DesktopClient.FlushAsync(timeout.Token);

        while (!fixture.Lease.HasActiveReadResponsePublication ||
               !fixture.DesktopConnection.HasActiveResponsePublication)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), timeout.Token);
        }

        var dispose = fixture.DesktopConnection.DisposeAsync().AsTask();
        await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        Assert.IsFalse(dispose.IsCompleted,
            "Connection disposal must wait for the exact response publication to drain.");

        var result = StrictWireCodec.Decode<ReadDocumentResult>(
            await NamedPipeBrokerHost.ReadFrameAsync(fixture.DesktopClient, timeout.Token));
        Assert.IsTrue(result.Accepted);
        Assert.AreEqual(content.Length, result.ByteLength);
        Assert.IsTrue(await serve);
        await dispose;
        Assert.IsFalse(fixture.DesktopConnection.Session.IsUsable);
        Assert.AreEqual(WorkerHandleLeaseState.RevocationPending, fixture.Lease.HandleState);
        Assert.IsTrue(await lifecycle.RevokeAndAwaitAcknowledgementAsync(
            fixture.Connection,
            fixture.Lease,
            "desktop-dispose-revoke",
            timeout.Token));
        Assert.IsNotNull((await worker).Revoke);
    }

    [TestMethod]
    public async Task DesktopSessionRevokeWaitsForExactResponsePublication()
    {
        RequireWindows64();
        await using var fixture = await ConnectedFixture.CreateAsync();
        var lifecycle = new WorkerHandleLifecycleTransport(fixture.Registrations);
        var desktopTransport = new DesktopReadQueryTransport(
            new WorkerReadQueryTransport(fixture.Router));
        var content = Enumerable.Repeat(
            (byte)'d',
            ReadDocumentResult.MaximumDecodedBytes).ToArray();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var worker = RunWorkerReadLifecycleAsync(
            fixture.Client,
            content,
            sendWrongCorrelation: false,
            queryReceived: null,
            releaseResponse: null,
            timeout.Token);

        Assert.IsTrue(await lifecycle.PublishGrantAndAwaitAcknowledgementAsync(
            fixture.Connection,
            fixture.Lease,
            "desktop-session-revoke-grant",
            timeout.Token));
        var query = CreateReadQuery(fixture, "desktop-session-revoke-query", content);
        var serve = desktopTransport.ServeOneAsync(
            fixture.DesktopConnection,
            fixture.Connection,
            fixture.Lease,
            timeout.Token).AsTask();
        await NamedPipeBrokerHost.WriteFrameAsync(
            fixture.DesktopClient,
            JsonSerializer.SerializeToUtf8Bytes(query),
            timeout.Token);
        await fixture.DesktopClient.FlushAsync(timeout.Token);

        while (!fixture.Lease.HasActiveReadResponsePublication ||
               !fixture.DesktopConnection.HasActiveResponsePublication)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), timeout.Token);
        }

        var revoke = Task.Run(
            () => fixture.Sessions.Revoke(fixture.DesktopSession.SessionId),
            timeout.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        Assert.IsFalse(revoke.IsCompleted,
            "Desktop-session revocation must wait for the exact response publication.");

        var result = StrictWireCodec.Decode<ReadDocumentResult>(
            await NamedPipeBrokerHost.ReadFrameAsync(fixture.DesktopClient, timeout.Token));
        Assert.IsTrue(result.Accepted);
        Assert.AreEqual(content.Length, result.ByteLength);
        Assert.IsTrue(await serve);
        Assert.IsTrue(await revoke);
        Assert.IsFalse(fixture.DesktopSession.IsUsable);
        Assert.AreEqual(WorkerHandleLeaseState.RevocationPending, fixture.Lease.HandleState);

        await fixture.DesktopConnection.DisposeAsync();
        Assert.IsTrue(await lifecycle.RevokeAndAwaitAcknowledgementAsync(
            fixture.Connection,
            fixture.Lease,
            "desktop-session-revoke-finish",
            timeout.Token));
        Assert.IsNotNull((await worker).Revoke);
    }

    [TestMethod]
    public async Task WorkerSessionRevokeWaitsForExactDesktopResponsePublication()
    {
        RequireWindows64();
        await using var fixture = await ConnectedFixture.CreateAsync();
        var lifecycle = new WorkerHandleLifecycleTransport(fixture.Registrations);
        var desktopTransport = new DesktopReadQueryTransport(
            new WorkerReadQueryTransport(fixture.Router));
        var content = Enumerable.Repeat(
            (byte)'c',
            ReadDocumentResult.MaximumDecodedBytes).ToArray();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var worker = RunWorkerReadLifecycleAsync(
            fixture.Client,
            content,
            sendWrongCorrelation: false,
            queryReceived: null,
            releaseResponse: null,
            timeout.Token);

        Assert.IsTrue(await lifecycle.PublishGrantAndAwaitAcknowledgementAsync(
            fixture.Connection,
            fixture.Lease,
            "worker-session-revoke-grant",
            timeout.Token));
        var query = CreateReadQuery(fixture, "worker-session-revoke-query", content);
        var serve = desktopTransport.ServeOneAsync(
            fixture.DesktopConnection,
            fixture.Connection,
            fixture.Lease,
            timeout.Token).AsTask();
        await NamedPipeBrokerHost.WriteFrameAsync(
            fixture.DesktopClient,
            JsonSerializer.SerializeToUtf8Bytes(query),
            timeout.Token);
        await fixture.DesktopClient.FlushAsync(timeout.Token);

        while (!fixture.Lease.HasActiveReadResponsePublication ||
               !fixture.DesktopConnection.HasActiveResponsePublication)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), timeout.Token);
        }

        var revoke = Task.Run(
            () => fixture.Sessions.Revoke(fixture.Connection.Session.SessionId),
            timeout.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        Assert.IsFalse(revoke.IsCompleted,
            "Worker-session revocation must wait for the exact response publication.");

        var result = StrictWireCodec.Decode<ReadDocumentResult>(
            await NamedPipeBrokerHost.ReadFrameAsync(fixture.DesktopClient, timeout.Token));
        Assert.IsTrue(result.Accepted);
        Assert.AreEqual(content.Length, result.ByteLength);
        Assert.IsTrue(await serve);
        Assert.IsTrue(await revoke);
        Assert.IsFalse(fixture.Connection.Session.IsUsable);
        Assert.AreEqual(WorkerHandleLeaseState.RevocationPending, fixture.Lease.HandleState);

        await fixture.Connection.DisposeAsync();
        await Assert.ThrowsExactlyAsync<EndOfStreamException>(async () => await worker);
    }

    [TestMethod]
    public async Task WorkerConnectionDisposeWaitsForExactDesktopResponsePublication()
    {
        RequireWindows64();
        await using var fixture = await ConnectedFixture.CreateAsync();
        var lifecycle = new WorkerHandleLifecycleTransport(fixture.Registrations);
        var desktopTransport = new DesktopReadQueryTransport(
            new WorkerReadQueryTransport(fixture.Router));
        var content = Enumerable.Repeat(
            (byte)'e',
            ReadDocumentResult.MaximumDecodedBytes).ToArray();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var worker = RunWorkerReadLifecycleAsync(
            fixture.Client,
            content,
            sendWrongCorrelation: false,
            queryReceived: null,
            releaseResponse: null,
            timeout.Token);

        Assert.IsTrue(await lifecycle.PublishGrantAndAwaitAcknowledgementAsync(
            fixture.Connection,
            fixture.Lease,
            "worker-connection-dispose-grant",
            timeout.Token));
        var query = CreateReadQuery(fixture, "worker-connection-dispose-query", content);
        var serve = desktopTransport.ServeOneAsync(
            fixture.DesktopConnection,
            fixture.Connection,
            fixture.Lease,
            timeout.Token).AsTask();
        await NamedPipeBrokerHost.WriteFrameAsync(
            fixture.DesktopClient,
            JsonSerializer.SerializeToUtf8Bytes(query),
            timeout.Token);
        await fixture.DesktopClient.FlushAsync(timeout.Token);

        while (!fixture.Lease.HasActiveReadResponsePublication ||
               !fixture.DesktopConnection.HasActiveResponsePublication ||
               !fixture.Connection.HasActiveResponsePublication)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), timeout.Token);
        }

        var dispose = fixture.Connection.DisposeAsync().AsTask();
        await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        Assert.IsFalse(dispose.IsCompleted,
            "Worker-connection disposal must wait for the exact response publication.");

        var result = StrictWireCodec.Decode<ReadDocumentResult>(
            await NamedPipeBrokerHost.ReadFrameAsync(fixture.DesktopClient, timeout.Token));
        Assert.IsTrue(result.Accepted);
        Assert.AreEqual(content.Length, result.ByteLength);
        Assert.IsTrue(await serve);
        await dispose;
        Assert.IsFalse(fixture.Connection.Session.IsUsable);
        Assert.AreEqual(WorkerHandleLeaseState.RevocationPending, fixture.Lease.HandleState);
        await Assert.ThrowsExactlyAsync<EndOfStreamException>(async () => await worker);
    }

    [TestMethod]
    public async Task SessionRevokeBetweenReservationAndStoreReplayRejectsWithoutDeadlock()
    {
        RequireWindows64();
        await using var fixture = await ConnectedFixture.CreateAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var lifecycle = new WorkerHandleLifecycleTransport(fixture.Registrations);
        var worker = RunWorkerLifecycleAsync(fixture.Client, false, timeout.Token);
        IDisposable? workerConnectionReservation = null;
        AuthenticatedPeerSession.ResponsePublicationReservation? desktopReservation = null;
        AuthenticatedPeerSession.ResponsePublicationReservation? workerReservation = null;

        try
        {
            Assert.IsTrue(await lifecycle.PublishGrantAndAwaitAcknowledgementAsync(
                fixture.Connection,
                fixture.Lease,
                "mid-reservation-grant",
                timeout.Token));
            var query = CreateReadQuery(
                fixture,
                "mid-reservation-query",
                Encoding.UTF8.GetBytes("{\"effectId\":\"effect_fire\"}"));
            Assert.IsTrue(fixture.Connection.TryReserveResponsePublication(
                out workerConnectionReservation));
            Assert.IsNotNull(workerConnectionReservation);
            Assert.IsTrue(fixture.DesktopSession.TryReserveResponsePublication(
                out desktopReservation));
            Assert.IsNotNull(desktopReservation);
            Assert.IsTrue(fixture.Connection.Session.TryReserveResponsePublication(
                out workerReservation));
            Assert.IsNotNull(workerReservation);

            var revoke = Task.Run(
                () => fixture.Sessions.Revoke(fixture.Connection.Session.SessionId),
                timeout.Token);
            while (fixture.Connection.Session.IsUsable)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(5), timeout.Token);
            }

            Assert.IsFalse(revoke.IsCompleted,
                "Session revoke must wait for the already acquired session reservation.");
            Assert.IsFalse(fixture.Router.TryReserveResponsePublication(
                    fixture.DesktopSession,
                    fixture.Connection.Session,
                    fixture.Lease,
                    query,
                    out var leaseReservation),
                "A session removed before store replay must never reserve publication.");
            Assert.IsNull(leaseReservation);
            workerReservation.Dispose();
            workerReservation = null;
            desktopReservation.Dispose();
            desktopReservation = null;
            workerConnectionReservation.Dispose();
            workerConnectionReservation = null;
            Assert.IsTrue(await revoke);
            Assert.AreEqual(WorkerHandleLeaseState.RevocationPending, fixture.Lease.HandleState);

            await fixture.Connection.DisposeAsync();
            await Assert.ThrowsExactlyAsync<EndOfStreamException>(async () => await worker);
        }
        finally
        {
            workerReservation?.Dispose();
            desktopReservation?.Dispose();
            workerConnectionReservation?.Dispose();
        }
    }

    [TestMethod]
    public async Task WrongReadResultCorrelationClosesWorkerConnectionFailClosed()
    {
        RequireWindows64();
        await using var fixture = await ConnectedFixture.CreateAsync();
        var lifecycle = new WorkerHandleLifecycleTransport(fixture.Registrations);
        var readTransport = new WorkerReadQueryTransport(fixture.Router);
        var content = Encoding.UTF8.GetBytes("{\"effectId\":\"effect_fire\"}");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var worker = RunWorkerReadLifecycleAsync(
            fixture.Client,
            content,
            sendWrongCorrelation: true,
            queryReceived: null,
            releaseResponse: null,
            timeout.Token);

        Assert.IsTrue(await lifecycle.PublishGrantAndAwaitAcknowledgementAsync(
            fixture.Connection,
            fixture.Lease,
            "pipe-wrong-read-grant",
            timeout.Token));
        var result = await readTransport.RouteAndReadAsync(
            fixture.Connection,
            fixture.DesktopSession,
            fixture.Lease,
            CreateReadQuery(fixture, "pipe-wrong-read-query", content),
            timeout.Token);

        Assert.IsNull(result);
        Assert.IsFalse(fixture.Connection.Session.IsUsable);
        Assert.IsNull((await worker).Revoke);
    }

    [TestMethod]
    public async Task ReadAndRevokeShareOneExclusiveConnectionExchange()
    {
        RequireWindows64();
        await using var fixture = await ConnectedFixture.CreateAsync();
        var lifecycle = new WorkerHandleLifecycleTransport(fixture.Registrations);
        var readTransport = new WorkerReadQueryTransport(fixture.Router);
        var content = Encoding.UTF8.GetBytes("{\"effectId\":\"effect_fire\"}");
        var queryReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseResponse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var worker = RunWorkerReadLifecycleAsync(
            fixture.Client,
            content,
            sendWrongCorrelation: false,
            queryReceived,
            releaseResponse.Task,
            timeout.Token);

        Assert.IsTrue(await lifecycle.PublishGrantAndAwaitAcknowledgementAsync(
            fixture.Connection,
            fixture.Lease,
            "pipe-ordered-read-grant",
            timeout.Token));
        var read = readTransport.RouteAndReadAsync(
            fixture.Connection,
            fixture.DesktopSession,
            fixture.Lease,
            CreateReadQuery(fixture, "pipe-ordered-read-query", content),
            timeout.Token).AsTask();
        await queryReceived.Task.WaitAsync(timeout.Token);
        var revoke = lifecycle.RevokeAndAwaitAcknowledgementAsync(
            fixture.Connection,
            fixture.Lease,
            "pipe-ordered-read-revoke",
            timeout.Token).AsTask();
        Assert.AreEqual(WorkerHandleLeaseState.GrantAcknowledged, fixture.Lease.HandleState);
        Assert.IsFalse(revoke.IsCompleted);

        releaseResponse.TrySetResult();
        Assert.IsNotNull(await read);
        Assert.IsTrue(await revoke);
        Assert.IsNotNull((await worker).Revoke);
    }

    [TestMethod]
    public async Task WrongGrantAcknowledgementRevokesSessionWithoutAcceptingLifecycle()
    {
        RequireWindows64();
        await using var fixture = await ConnectedFixture.CreateAsync();
        var transport = new WorkerHandleLifecycleTransport(fixture.Registrations);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var worker = RunWorkerLifecycleAsync(fixture.Client, true, timeout.Token);

        Assert.IsFalse(await transport.PublishGrantAndAwaitAcknowledgementAsync(
            fixture.Connection,
            fixture.Lease,
            "pipe-grant-invalid-ack",
            timeout.Token));
        var result = await worker;

        Assert.IsFalse(fixture.Connection.Session.IsUsable);
        Assert.AreEqual(WorkerHandleLeaseState.RevocationPending, fixture.Lease.HandleState);
        Assert.IsNull(result.Revoke);
    }

    [TestMethod]
    public async Task LifecycleStateCannotMutateWhileConnectionExchangeIsReserved()
    {
        RequireWindows64();
        await using var fixture = await ConnectedFixture.CreateAsync();
        var transport = new WorkerHandleLifecycleTransport(fixture.Registrations);
        var reservation = await fixture.Connection.BeginExclusiveExchangeAsync(
            CancellationToken.None);
        using var cancellation = new CancellationTokenSource();

        var publish = transport.PublishGrantAndAwaitAcknowledgementAsync(
            fixture.Connection,
            fixture.Lease,
            "reserved-grant",
            cancellation.Token).AsTask();
        Assert.AreEqual(WorkerHandleLeaseState.Prepared, fixture.Lease.HandleState);

        var revoke = transport.RevokeAndAwaitAcknowledgementAsync(
            fixture.Connection,
            fixture.Lease,
            "reserved-revoke",
            cancellation.Token).AsTask();
        Assert.AreEqual(WorkerHandleLeaseState.Prepared, fixture.Lease.HandleState);

        cancellation.Cancel();
        Assert.IsFalse(await publish);
        Assert.IsFalse(await revoke);
        await reservation.DisposeAsync();
        Assert.IsFalse(fixture.Connection.Session.IsUsable);
    }

    private static async Task<WorkerLifecycleResult> RunWorkerLifecycleAsync(
        NamedPipeClientStream client,
        bool sendWrongGrantHash,
        CancellationToken cancellationToken)
    {
        SafeFileHandle[] handles = [];
        try
        {
            var grantBytes = await NamedPipeBrokerHost.ReadFrameAsync(client, cancellationToken);
            var grant = StrictWireCodec.Decode<WorkerProjectHandleGrant>(grantBytes);
            handles = OpenGrantedHandles(grant);
            Assert.AreEqual(3, handles.Length);
            Assert.IsTrue(handles.All(handle => GetHandleInformation(handle, out _)));

            if (sendWrongGrantHash)
            {
                DisposeAll(handles);
                handles = [];
                var wrongHash = TypedHash.ComputeUtf8(
                    WorkerProjectHandleGrant.SelfHashType,
                    "different-grant");
                await WriteResponseAsync(
                    client,
                    CreateGrantAcknowledgement(grant, wrongHash),
                    cancellationToken);
                return new WorkerLifecycleResult(grant, null);
            }

            await WriteResponseAsync(
                client,
                CreateGrantAcknowledgement(grant, grant.SelfHash),
                cancellationToken);

            var revokeBytes = await NamedPipeBrokerHost.ReadFrameAsync(client, cancellationToken);
            var revoke = StrictWireCodec.Decode<WorkerProjectHandleRevoke>(revokeBytes);
            Assert.AreEqual(grant.LeaseId, revoke.LeaseId);
            Assert.IsTrue(grant.SelfHash.FixedTimeEquals(revoke.GrantSelfHash));

            DisposeAll(handles);
            handles = [];
            await WriteResponseAsync(
                client,
                CreateRevokeAcknowledgement(grant, revoke),
                cancellationToken);
            return new WorkerLifecycleResult(grant, revoke);
        }
        finally
        {
            DisposeAll(handles);
        }
    }

    private static async Task<WorkerLifecycleResult> RunWorkerReadLifecycleAsync(
        NamedPipeClientStream client,
        byte[] content,
        bool sendWrongCorrelation,
        TaskCompletionSource? queryReceived,
        Task? releaseResponse,
        CancellationToken cancellationToken)
    {
        SafeFileHandle[] handles = [];
        try
        {
            var grant = StrictWireCodec.Decode<WorkerProjectHandleGrant>(
                await NamedPipeBrokerHost.ReadFrameAsync(client, cancellationToken));
            handles = OpenGrantedHandles(grant);
            await WriteResponseAsync(
                client,
                CreateGrantAcknowledgement(grant, grant.SelfHash),
                cancellationToken);

            var query = StrictWireCodec.Decode<ReadDocumentQuery>(
                await NamedPipeBrokerHost.ReadFrameAsync(client, cancellationToken));
            queryReceived?.TrySetResult();
            if (releaseResponse is not null)
            {
                await releaseResponse.WaitAsync(cancellationToken);
            }

            var result = new ReadDocumentResult(
                ProtocolVersions.Current,
                MessageKinds.ReadDocumentResult,
                sendWrongCorrelation ? "wrong-read-correlation" : query.RequestId,
                true,
                query.ProjectIdentity,
                query.DocumentKind,
                query.DocumentId,
                TypedHash.Compute(ReadDocumentResult.ContentHashType, content),
                content.Length,
                Convert.ToBase64String(content),
                null);
            await WriteResponseAsync(
                client,
                JsonSerializer.SerializeToUtf8Bytes(result),
                cancellationToken);
            if (sendWrongCorrelation)
            {
                return new WorkerLifecycleResult(grant, null);
            }

            var revoke = StrictWireCodec.Decode<WorkerProjectHandleRevoke>(
                await NamedPipeBrokerHost.ReadFrameAsync(client, cancellationToken));
            DisposeAll(handles);
            handles = [];
            await WriteResponseAsync(
                client,
                CreateRevokeAcknowledgement(grant, revoke),
                cancellationToken);
            return new WorkerLifecycleResult(grant, revoke);
        }
        finally
        {
            DisposeAll(handles);
        }
    }

    private static ReadDocumentQuery CreateReadQuery(
        ConnectedFixture fixture,
        string requestId,
        byte[] expectedContent) =>
        new(
            ProtocolVersions.Current,
            MessageKinds.ReadDocumentQuery,
            requestId,
            fixture.Lease.LeaseId,
            fixture.Lease.Project.ProjectIdentity,
            fixture.Lease.LeaseGeneration,
            DocumentKinds.Manifest,
            "effect_fire",
            TypedHash.Compute(ReadDocumentResult.ContentHashType, expectedContent));

    private static async ValueTask WriteResponseAsync(
        NamedPipeClientStream client,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        await NamedPipeBrokerHost.WriteFrameAsync(client, payload, cancellationToken);
        await client.FlushAsync(cancellationToken);
    }

    private static SafeFileHandle[] OpenGrantedHandles(WorkerProjectHandleGrant grant)
    {
        var values = new[]
        {
            grant.VolumeHandle,
            grant.RepositoryHandle,
            grant.ProjectRootHandle,
        };
        Assert.AreEqual(3, values.Distinct(StringComparer.Ordinal).Count());
        return values
            .Select(value => new SafeFileHandle(
                new IntPtr(unchecked((long)ulong.Parse(
                    value,
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture))),
                ownsHandle: true))
            .ToArray();
    }

    private static void DisposeAll(IEnumerable<SafeFileHandle> handles)
    {
        foreach (var handle in handles)
        {
            handle.Dispose();
        }
    }

    private static byte[] CreateGrantAcknowledgement(
        WorkerProjectHandleGrant grant,
        TypedHash grantSelfHash)
    {
        var root = new JsonObject
        {
            ["protocolVersion"] = ProtocolVersions.Current,
            ["messageKind"] = MessageKinds.WorkerProjectHandleGrantAcknowledgement,
            ["requestId"] = "worker-grant-ack",
            ["leaseId"] = grant.LeaseId,
            ["brokerGeneration"] = grant.BrokerGeneration,
            ["leaseGeneration"] = grant.LeaseGeneration,
            ["workerSessionId"] = grant.WorkerSessionId,
            ["workerProcessEpoch"] = grant.WorkerProcessEpoch,
            ["grantSelfHash"] = JsonSerializer.SerializeToNode(grantSelfHash),
            ["disposition"] = WorkerProjectHandleGrantAcknowledgement.AcceptedDisposition,
            ["selfHash"] = JsonSerializer.SerializeToNode(TypedHash.ComputeUtf8(
                WorkerProjectHandleGrantAcknowledgement.SelfHashType,
                "placeholder")),
        };
        return Seal(root, WorkerProjectHandleGrantAcknowledgement.SelfHashType);
    }

    private static byte[] CreateRevokeAcknowledgement(
        WorkerProjectHandleGrant grant,
        WorkerProjectHandleRevoke revoke)
    {
        var root = new JsonObject
        {
            ["protocolVersion"] = ProtocolVersions.Current,
            ["messageKind"] = MessageKinds.WorkerProjectHandleRevokeAcknowledgement,
            ["requestId"] = "worker-revoke-ack",
            ["leaseId"] = grant.LeaseId,
            ["brokerGeneration"] = grant.BrokerGeneration,
            ["leaseGeneration"] = grant.LeaseGeneration,
            ["workerSessionId"] = grant.WorkerSessionId,
            ["workerProcessEpoch"] = grant.WorkerProcessEpoch,
            ["grantSelfHash"] = JsonSerializer.SerializeToNode(grant.SelfHash),
            ["revokeSelfHash"] = JsonSerializer.SerializeToNode(revoke.SelfHash),
            ["disposition"] = WorkerProjectHandleRevokeAcknowledgement.ClosedDisposition,
            ["selfHash"] = JsonSerializer.SerializeToNode(TypedHash.ComputeUtf8(
                WorkerProjectHandleRevokeAcknowledgement.SelfHashType,
                "placeholder")),
        };
        return Seal(root, WorkerProjectHandleRevokeAcknowledgement.SelfHashType);
    }

    private static byte[] Seal(JsonObject root, string typeTag)
    {
        root["selfHash"] = JsonSerializer.SerializeToNode(SelfHash.Compute(
            JsonSerializer.SerializeToUtf8Bytes(root),
            typeTag));
        return JsonSerializer.SerializeToUtf8Bytes(root);
    }

    private static ProjectLeaseDescriptor CreateMismatchedLeaseDescriptor(
        ProjectLeaseDescriptor descriptor) => new(
        descriptor.ProtocolVersion,
        descriptor.MessageKind,
        descriptor.RequestId,
        "unknown-lease",
        descriptor.RegisteredProjectId,
        descriptor.ProjectIdentity,
        descriptor.BrokerGeneration,
        descriptor.RegistrationGeneration,
        descriptor.WorkerSessionId,
        descriptor.WorkerProcessEpoch,
        descriptor.LeaseGeneration,
        TypedHash.ComputeUtf8(ProjectLeaseDescriptor.SelfHashType, "unknown-lease-descriptor"));

    private static void RequireWindows64()
    {
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8)
        {
            Assert.Inconclusive("The native Worker handle transport gate requires 64-bit Windows.");
        }
    }

    private sealed record WorkerLifecycleResult(
        WorkerProjectHandleGrant Grant,
        WorkerProjectHandleRevoke? Revoke);

    private sealed class PipeClientConnection : IVfxComposerConnection
    {
        private readonly NamedPipeClientStream _pipe;
        private int _active;

        internal PipeClientConnection(NamedPipeClientStream pipe) =>
            _pipe = pipe ?? throw new ArgumentNullException(nameof(pipe));

        public ConnectionState CurrentState => ConnectionState.CreateDisconnected();

        public ValueTask<ConnectionState> QueryStateAsync(
            RequestCorrelation correlation,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(CurrentState);
        }

        public async ValueTask<ReadDocumentResult> QueryDocumentAsync(
            ReadDocumentQuery query,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
            {
                throw new InvalidOperationException("Only one test query may be active.");
            }

            try
            {
                await NamedPipeBrokerHost.WriteFrameAsync(
                    _pipe,
                    JsonSerializer.SerializeToUtf8Bytes(query),
                    cancellationToken);
                await _pipe.FlushAsync(cancellationToken);
                return StrictWireCodec.Decode<ReadDocumentResult>(
                    await NamedPipeBrokerHost.ReadFrameAsync(_pipe, cancellationToken));
            }
            finally
            {
                Volatile.Write(ref _active, 0);
            }
        }
    }

    private sealed class ConnectedFixture : IAsyncDisposable
    {
        private ConnectedFixture(
            string scratch,
            string repository,
            string project,
            NamedPipeClientStream client,
            AuthenticatedPeerConnection connection,
            NamedPipeClientStream desktopClient,
            AuthenticatedPeerConnection desktopConnection,
            PeerSessionRegistry sessions,
            ProjectRegistrationStore registrations,
            ReadOnlyQueryRouter router,
            AuthenticatedPeerSession desktopSession,
            RegisteredProjectLease lease,
            ProjectLeaseDescriptor leaseDescriptor)
        {
            Scratch = scratch;
            Repository = repository;
            Project = project;
            Client = client;
            Connection = connection;
            DesktopClient = desktopClient;
            DesktopConnection = desktopConnection;
            Sessions = sessions;
            Registrations = registrations;
            Router = router;
            DesktopSession = desktopSession;
            Lease = lease;
            LeaseDescriptor = leaseDescriptor;
        }

        private string Scratch { get; }
        private string Repository { get; }
        private string Project { get; }
        internal NamedPipeClientStream Client { get; }
        internal AuthenticatedPeerConnection Connection { get; }
        internal NamedPipeClientStream DesktopClient { get; }
        internal AuthenticatedPeerConnection DesktopConnection { get; }
        internal PeerSessionRegistry Sessions { get; }
        internal ProjectRegistrationStore Registrations { get; }
        internal ReadOnlyQueryRouter Router { get; }
        internal AuthenticatedPeerSession DesktopSession { get; }
        internal RegisteredProjectLease Lease { get; }
        internal ProjectLeaseDescriptor LeaseDescriptor { get; }

        internal static async Task<ConnectedFixture> CreateAsync()
        {
            var scratch = Path.Combine(
                Path.GetTempPath(),
                "vfxcomposer-pipe-lifecycle-" + Guid.NewGuid().ToString("N"));
            if (Directory.Exists(scratch) || File.Exists(scratch))
            {
                throw new InvalidOperationException("The test-owned scratch path already exists.");
            }

            var repository = Path.Combine(scratch, "repository");
            var project = Path.Combine(repository, "project");
            Directory.CreateDirectory(project);
            NamedPipeClientStream? client = null;
            AuthenticatedPeerConnection? connection = null;
            NamedPipeClientStream? desktopClient = null;
            AuthenticatedPeerConnection? desktopConnection = null;
            PeerSessionRegistry? sessions = null;
            ProjectRegistrationStore? registrations = null;
            try
            {
                var currentProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
                using var policyFacts = WindowsNamedPipePeerFactsSource.ObserveProcess(currentProcessId);
                var driveRoot = Path.GetPathRoot(scratch)
                    ?? throw new InvalidOperationException("Scratch drive root is missing.");
                var repositorySegments = repository[driveRoot.Length..]
                    .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                var definition = new BrokerRegistrationDefinition(
                    "project-pipe-lifecycle",
                    GetVolumeGuid(driveRoot),
                    repositorySegments,
                    ["project"]);
                var pipeName = "vfxcomposer-worker-lifecycle-" + Guid.NewGuid().ToString("N");
                var policy = BrokerTestFactory.CreatePolicy(
                    pipeName,
                    "broker-pipe-lifecycle",
                    1,
                    policyFacts.UserSidIdentity,
                    policyFacts.ImageIdentity,
                    policyFacts.ImageIdentity,
                    [definition]);
                sessions = new PeerSessionRegistry(policy);
                registrations = new ProjectRegistrationStore(policy, sessions);
                var host = new NamedPipeBrokerHost(
                    policy,
                    new NamedPipePeerAuthenticator(new WindowsNamedPipePeerFactsSource(), sessions));
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                var accept = host.AcceptOneAsync(timeout.Token);

                client = new NamedPipeClientStream(
                    ".",
                    pipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);
                await client.ConnectAsync(timeout.Token);
                var workerHello = new PeerHello(
                    "worker-pipe-hello",
                    PeerRoles.Worker,
                    "worker-pipe",
                    currentProcessId,
                    policyFacts.ProcessEpoch,
                    [
                        PeerCapabilityIds.PeerSessionV1,
                        PeerCapabilityIds.ReadOnlyQueryV1,
                        PeerCapabilityIds.ProjectRegistrationV1,
                        PeerCapabilityIds.WorkerHandleLifecycleV1,
                    ],
                    policyFacts.ImageIdentity);
                await NamedPipeBrokerHost.WriteFrameAsync(
                    client,
                    JsonSerializer.SerializeToUtf8Bytes(workerHello),
                    timeout.Token);
                await client.FlushAsync(timeout.Token);
                var receipt = StrictWireCodec.Decode<PeerSessionAccepted>(
                    await NamedPipeBrokerHost.ReadFrameAsync(client, timeout.Token));
                connection = await accept;
                Assert.AreEqual(connection.Session.SessionId, receipt.SessionId);

                Assert.IsTrue(registrations.TryRegisterPinned(
                    connection.Session,
                    definition.RegisteredProjectId,
                    out _,
                    out _));

                using var desktopFacts = WindowsNamedPipePeerFactsSource.ObserveProcess(currentProcessId);
                var desktopHello = new PeerHello(
                    "desktop-pipe-hello",
                    PeerRoles.Desktop,
                    "desktop-pipe",
                    desktopFacts.ProcessId,
                    desktopFacts.ProcessEpoch,
                    [PeerCapabilityIds.PeerSessionV1, PeerCapabilityIds.ReadOnlyQueryV1],
                    desktopFacts.ImageIdentity);
                var acceptDesktop = host.AcceptOneAsync(timeout.Token);
                desktopClient = new NamedPipeClientStream(
                    ".",
                    pipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);
                await desktopClient.ConnectAsync(timeout.Token);
                await NamedPipeBrokerHost.WriteFrameAsync(
                    desktopClient,
                    JsonSerializer.SerializeToUtf8Bytes(desktopHello),
                    timeout.Token);
                await desktopClient.FlushAsync(timeout.Token);
                var desktopReceipt = StrictWireCodec.Decode<PeerSessionAccepted>(
                    await NamedPipeBrokerHost.ReadFrameAsync(desktopClient, timeout.Token));
                desktopConnection = await acceptDesktop;
                Assert.AreEqual(desktopConnection.Session.SessionId, desktopReceipt.SessionId);
                Assert.IsTrue(registrations.TryAcquirePinnedLease(
                    desktopConnection.Session,
                    connection.Session,
                    definition.RegisteredProjectId,
                    "pipe-lease",
                    out var lease,
                    out var leaseDescriptor,
                    out _));

                var router = new ReadOnlyQueryRouter(registrations, sessions);

                return new ConnectedFixture(
                    scratch,
                    repository,
                    project,
                    client,
                    connection,
                    desktopClient,
                    desktopConnection,
                    sessions,
                    registrations,
                    router,
                    desktopConnection.Session,
                    lease!,
                    leaseDescriptor!);
            }
            catch (Exception original)
            {
                var failures = new List<Exception> { original };
                if (connection is not null)
                {
                    await CollectAsync(failures, connection.DisposeAsync);
                }

                if (desktopConnection is not null)
                {
                    await CollectAsync(failures, desktopConnection.DisposeAsync);
                }

                Collect(failures, () => client?.Dispose());
                Collect(failures, () => desktopClient?.Dispose());
                Collect(failures, () => registrations?.Dispose());
                Collect(failures, () => sessions?.Dispose());
                Collect(failures, () => DeleteScratch(project, repository, scratch));
                throw new AggregateException("Connected fixture creation failed.", failures);
            }
        }

        public async ValueTask DisposeAsync()
        {
            var failures = new List<Exception>();
            await CollectAsync(failures, DesktopConnection.DisposeAsync);
            await CollectAsync(failures, Connection.DisposeAsync);
            Collect(failures, DesktopClient.Dispose);
            Collect(failures, Client.Dispose);
            Collect(failures, Registrations.Dispose);
            Collect(failures, Sessions.Dispose);
            Collect(failures, () => DeleteScratch(Project, Repository, Scratch));
            if (failures.Count != 0)
            {
                throw new AggregateException("Connected fixture cleanup failed.", failures);
            }
        }

        private static async ValueTask CollectAsync(
            ICollection<Exception> failures,
            Func<ValueTask> operation)
        {
            try
            {
                await operation();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        private static void Collect(
            ICollection<Exception> failures,
            Action operation)
        {
            try
            {
                operation();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }
    }

    private static string GetVolumeGuid(string driveRoot)
    {
        var builder = new StringBuilder(64);
        if (!GetVolumeNameForVolumeMountPoint(driveRoot, builder, builder.Capacity))
        {
            throw new InvalidOperationException("Volume GUID lookup failed.");
        }

        return builder.ToString();
    }

    private static void DeleteScratch(string project, string repository, string scratch)
        => PinnedScratchTreeCleanup.DeleteExactEmptyTree(project, repository, scratch);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeNameForVolumeMountPoint(
        string volumeMountPoint,
        StringBuilder volumeName,
        int bufferLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetHandleInformation(
        SafeFileHandle handle,
        out uint flags);
}
