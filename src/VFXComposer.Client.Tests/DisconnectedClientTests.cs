using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Protocol;
using VFXComposer.Protocol.Diagnostics;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Projects;
using VFXComposer.Protocol.Queries;
using VFXComposer.Protocol.Registration;

namespace VFXComposer.Client.Tests;

[TestClass]
public sealed class DisconnectedClientTests
{
    [TestMethod]
    public async Task DefaultClientRemainsDisconnectedWithoutBrokerOrUnity()
    {
        var client = VfxComposerClient.CreateDisconnected();
        var state = await client.RefreshStateAsync(
            new RequestCorrelation("request-1", "idempotency-1"));

        Assert.IsFalse(state.IsConnected);
        Assert.IsFalse(state.HasRegisteredProject);
        Assert.AreEqual(ProjectConnectionState.Disconnected, state.ProjectState);
        Assert.AreEqual("Disconnected", state.ConnectionDisplay);
        Assert.AreEqual("No registered project", state.ProjectDisplay);
        Assert.AreEqual(ProtocolVersions.Current, state.ProtocolVersion);
        Assert.AreEqual(StableDiagnosticCodes.Disconnected, state.Diagnostic.Code);
    }

    [TestMethod]
    public async Task DisconnectedConnectionHonorsCancellationWithoutDoingIo()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var connection = new DisconnectedVfxComposerConnection();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await connection.QueryStateAsync(
                new RequestCorrelation("request-2", "idempotency-2"),
                cancellation.Token));
    }

    [TestMethod]
    public async Task DisconnectedReadReturnsStableRejectionWithoutContent()
    {
        var lease = CreateLeaseDescriptor();
        var client = VfxComposerClient.CreateDisconnected();

        var result = await client.ReadDocumentAsync(
            lease,
            DocumentKinds.Manifest,
            "effect_fire",
            expectedContentHash: null,
            new RequestCorrelation("read-request-1", "read-idempotency-1"));

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual("read-request-1", result.RequestId);
        Assert.IsTrue(lease.ProjectIdentity.FixedTimeEquals(result.ProjectIdentity));
        Assert.AreEqual(DocumentKinds.Manifest, result.DocumentKind);
        Assert.AreEqual("effect_fire", result.DocumentId);
        Assert.IsNull(result.ContentHash);
        Assert.IsNull(result.ContentBase64);
        Assert.AreEqual(0, result.ByteLength);
        Assert.AreEqual(StableDiagnosticCodes.Disconnected, result.Diagnostic?.Code);
    }

    [TestMethod]
    public async Task DisconnectedReadHonorsCancellationBeforeReturningResult()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var client = VfxComposerClient.CreateDisconnected();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await client.ReadDocumentAsync(
                CreateLeaseDescriptor(),
                DocumentKinds.Contract,
                "effect_fire",
                expectedContentHash: null,
                new RequestCorrelation("read-request-2", "read-idempotency-2"),
                cancellation.Token));
    }

    [TestMethod]
    public async Task ReadClientRejectsCrossRequestResultBeforeReturningContent()
    {
        var lease = CreateLeaseDescriptor();
        var content = System.Text.Encoding.UTF8.GetBytes("{\"effectId\":\"effect_fire\"}");
        var connection = new SyntheticConnection(query => new ReadDocumentResult(
            ProtocolVersions.Current,
            MessageKinds.ReadDocumentResult,
            "different-request",
            accepted: true,
            query.ProjectIdentity,
            query.DocumentKind,
            query.DocumentId,
            TypedHash.Compute(ReadDocumentResult.ContentHashType, content),
            content.Length,
            Convert.ToBase64String(content),
            diagnostic: null));
        var client = new VfxComposerClient(connection);

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await client.ReadDocumentAsync(
                lease,
                DocumentKinds.Manifest,
                "effect_fire",
                expectedContentHash: null,
                new RequestCorrelation("read-request-3", "read-idempotency-3")));

        Assert.AreEqual(
            "The read result does not match the requested identity.",
            exception.Message);
    }

    [TestMethod]
    public async Task ReadClientRejectsUnexpectedAcceptedContentIdentity()
    {
        var lease = CreateLeaseDescriptor();
        var actual = System.Text.Encoding.UTF8.GetBytes("{\"effectId\":\"effect_fire\"}");
        var expected = System.Text.Encoding.UTF8.GetBytes("{\"effectId\":\"effect_other\"}");
        var connection = new SyntheticConnection(query => new ReadDocumentResult(
            ProtocolVersions.Current,
            MessageKinds.ReadDocumentResult,
            query.RequestId,
            accepted: true,
            query.ProjectIdentity,
            query.DocumentKind,
            query.DocumentId,
            TypedHash.Compute(ReadDocumentResult.ContentHashType, actual),
            actual.Length,
            Convert.ToBase64String(actual),
            diagnostic: null));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await new VfxComposerClient(connection).ReadDocumentAsync(
                lease,
                DocumentKinds.Manifest,
                "effect_fire",
                TypedHash.Compute(ReadDocumentResult.ContentHashType, expected),
                new RequestCorrelation("read-request-4", "read-idempotency-4")));
    }

    private static ProjectLeaseDescriptor CreateLeaseDescriptor() => new(
        ProtocolVersions.Current,
        MessageKinds.ProjectLeaseDescriptor,
        "lease-descriptor-request",
        "lease-1",
        "registered-project-1",
        TypedHash.ComputeUtf8(ProjectRegistrationAttestation.ProjectIdentityType, "project-1"),
        brokerGeneration: 1,
        registrationGeneration: 1,
        workerSessionId: "worker-session-1",
        workerProcessEpoch: "worker-epoch-1",
        leaseGeneration: 1,
        selfHash: TypedHash.ComputeUtf8(ProjectLeaseDescriptor.SelfHashType, "descriptor-1"));

    private sealed class SyntheticConnection : IVfxComposerConnection
    {
        private readonly Func<ReadDocumentQuery, ReadDocumentResult> _resultFactory;

        internal SyntheticConnection(Func<ReadDocumentQuery, ReadDocumentResult> resultFactory) =>
            _resultFactory = resultFactory;

        public ConnectionState CurrentState => ConnectionState.CreateDisconnected();

        public ValueTask<ConnectionState> QueryStateAsync(
            RequestCorrelation correlation,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(CurrentState);
        }

        public ValueTask<ReadDocumentResult> QueryDocumentAsync(
            ReadDocumentQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(_resultFactory(query));
        }
    }
}
