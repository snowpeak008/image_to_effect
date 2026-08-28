using VFXComposer.Protocol;
using VFXComposer.Protocol.Diagnostics;
using VFXComposer.Protocol.Queries;

namespace VFXComposer.Client;

/// <summary>
/// Deterministic, zero-I/O connection used until an authenticated Broker exists.
/// </summary>
public sealed class DisconnectedVfxComposerConnection : IVfxComposerConnection
{
    private static readonly ConnectionState DisconnectedState =
        ConnectionState.CreateDisconnected();

    public ConnectionState CurrentState => DisconnectedState;

    public ValueTask<ConnectionState> QueryStateAsync(
        RequestCorrelation correlation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(DisconnectedState);
    }

    public ValueTask<ReadDocumentResult> QueryDocumentAsync(
        ReadDocumentQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new ReadDocumentResult(
            ProtocolVersions.Current,
            MessageKinds.ReadDocumentResult,
            query.RequestId,
            accepted: false,
            query.ProjectIdentity,
            query.DocumentKind,
            query.DocumentId,
            contentHash: null,
            byteLength: 0,
            contentBase64: null,
            StableDiagnosticCatalog.Create(StableDiagnosticCodes.Disconnected)));
    }
}
