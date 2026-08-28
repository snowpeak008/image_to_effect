namespace VFXComposer.Client;

/// <summary>
/// Phase 1 facade over a connection. The default factory is deliberately disconnected.
/// </summary>
public sealed class VfxComposerClient
{
    private readonly IVfxComposerConnection _connection;
    private readonly ReadOnlyProjectQueryClient _queries;

    public VfxComposerClient(IVfxComposerConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _queries = new ReadOnlyProjectQueryClient(connection);
        CurrentState = connection.CurrentState;
    }

    public ConnectionState CurrentState { get; private set; }

    public static VfxComposerClient CreateDisconnected() =>
        new(new DisconnectedVfxComposerConnection());

    public async ValueTask<ConnectionState> RefreshStateAsync(
        RequestCorrelation correlation,
        CancellationToken cancellationToken = default)
    {
        CurrentState = await _connection
            .QueryStateAsync(correlation, cancellationToken)
            .ConfigureAwait(false);

        return CurrentState;
    }

    public ValueTask<VFXComposer.Protocol.Queries.ReadDocumentResult> ReadDocumentAsync(
        VFXComposer.Protocol.Registration.ProjectLeaseDescriptor lease,
        string documentKind,
        string documentId,
        VFXComposer.Protocol.Hashing.TypedHash? expectedContentHash,
        RequestCorrelation correlation,
        CancellationToken cancellationToken = default) =>
        _queries.ReadAsync(
            lease,
            documentKind,
            documentId,
            expectedContentHash,
            correlation,
            cancellationToken);
}
