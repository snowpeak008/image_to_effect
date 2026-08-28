using System.Text.Json;
using System.Runtime.Versioning;
using VFXComposer.Broker.Registration;
using VFXComposer.Protocol;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Json;
using VFXComposer.Protocol.Queries;

namespace VFXComposer.Broker.Ipc;

/// <summary>Path-free query exchange bound to one exact U2 Worker process epoch.</summary>
[SupportedOSPlatform("windows")]
internal sealed class UserModeProjectReadSession
{
    private readonly UserModeProjectSelectionStore _store;
    private readonly UserModeProjectLease _lease;
    private readonly UserModeBrokerWorkerSession _workerSession;

    internal UserModeProjectReadSession(
        UserModeProjectSelectionStore store,
        UserModeProjectLease lease,
        UserModeBrokerWorkerSession workerSession)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(workerSession);
        if (!store.IsCurrent(lease, workerSession))
        {
            throw new InvalidOperationException("U3FS001");
        }

        _store = store;
        _lease = lease;
        _workerSession = workerSession;
    }

    internal bool IsUsable => _store.IsCurrent(_lease, _workerSession);

    internal ReadDocumentQuery CreateQuery(
        string documentKind,
        string documentId,
        TypedHash? expectedContentHash = null)
    {
        if (!IsUsable)
        {
            throw new InvalidOperationException("U3FS001");
        }

        return new ReadDocumentQuery(
            ProtocolVersions.Current,
            MessageKinds.ReadDocumentQuery,
            "um-read-" + Guid.NewGuid().ToString("N"),
            _lease.LeaseId,
            _lease.Locator.ProjectIdentity,
            _lease.LeaseGeneration,
            documentKind,
            documentId,
            expectedContentHash);
    }

    internal byte[] EncodeQuery(
        string documentKind,
        string documentId,
        TypedHash? expectedContentHash = null)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            CreateQuery(documentKind, documentId, expectedContentHash));
        _ = StrictWireCodec.Decode<ReadDocumentQuery>(bytes);
        return bytes;
    }

    internal async ValueTask<ReadDocumentResult> ReadAsync(
        string documentKind,
        string documentId,
        TypedHash? expectedContentHash = null,
        CancellationToken cancellationToken = default)
    {
        using var exchange = await _store.BeginExchangeAsync(
            _lease,
            _workerSession,
            cancellationToken).ConfigureAwait(false);
        var query = CreateQuery(documentKind, documentId, expectedContentHash);
        var payload = JsonSerializer.SerializeToUtf8Bytes(query);
        await NamedPipeBrokerHost.WriteFrameAsync(
            _lease.Transport,
            payload,
            cancellationToken).ConfigureAwait(false);
        await _lease.Transport.FlushAsync(cancellationToken).ConfigureAwait(false);
        var resultBytes = await NamedPipeBrokerHost.ReadFrameAsync(
            _lease.Transport,
            cancellationToken).ConfigureAwait(false);
        var result = StrictWireCodec.Decode<ReadDocumentResult>(resultBytes);
        if (!string.Equals(result.RequestId, query.RequestId, StringComparison.Ordinal) ||
            !result.ProjectIdentity.FixedTimeEquals(query.ProjectIdentity) ||
            !string.Equals(result.DocumentKind, query.DocumentKind, StringComparison.Ordinal) ||
            !string.Equals(result.DocumentId, query.DocumentId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("U3FS001");
        }

        return result;
    }

    public override string ToString() =>
        $"UserModeProjectReadSession(Generation={_lease.LeaseGeneration}, Usable={IsUsable})";
}
