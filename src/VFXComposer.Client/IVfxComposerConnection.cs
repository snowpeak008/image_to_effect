using VFXComposer.Protocol.Queries;

namespace VFXComposer.Client;

/// <summary>
/// Read-only Phase 1 connection abstraction. It intentionally exposes no listener,
/// transport configuration, project path, or mutation operation.
/// </summary>
public interface IVfxComposerConnection
{
    ConnectionState CurrentState { get; }

    ValueTask<ConnectionState> QueryStateAsync(
        RequestCorrelation correlation,
        CancellationToken cancellationToken = default);

    ValueTask<ReadDocumentResult> QueryDocumentAsync(
        ReadDocumentQuery query,
        CancellationToken cancellationToken = default);
}
