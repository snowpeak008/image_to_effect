using VFXComposer.Protocol;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Queries;
using VFXComposer.Protocol.Registration;

namespace VFXComposer.Client;

/// <summary>
/// Builds an identity-only read request. A lease descriptor is presentation data,
/// not authority; the authenticated Broker must independently replay its opaque lease.
/// </summary>
public sealed class ReadOnlyProjectQueryClient
{
    private readonly IVfxComposerConnection _connection;

    public ReadOnlyProjectQueryClient(IVfxComposerConnection connection) =>
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    public async ValueTask<ReadDocumentResult> ReadAsync(
        ProjectLeaseDescriptor lease,
        string documentKind,
        string documentId,
        TypedHash? expectedContentHash,
        RequestCorrelation correlation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        var query = new ReadDocumentQuery(
            ProtocolVersions.Current,
            MessageKinds.ReadDocumentQuery,
            correlation.RequestId,
            lease.LeaseId,
            lease.ProjectIdentity,
            lease.LeaseGeneration,
            documentKind,
            documentId,
            expectedContentHash);
        var result = await _connection.QueryDocumentAsync(query, cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(result.RequestId, query.RequestId, StringComparison.Ordinal) ||
            !result.ProjectIdentity.FixedTimeEquals(query.ProjectIdentity) ||
            !string.Equals(result.DocumentKind, query.DocumentKind, StringComparison.Ordinal) ||
            !string.Equals(result.DocumentId, query.DocumentId, StringComparison.Ordinal) ||
            (result.Accepted && query.ExpectedContentHash is not null &&
             (result.ContentHash is null ||
              !result.ContentHash.FixedTimeEquals(query.ExpectedContentHash))))
        {
            throw new InvalidOperationException(
                "The read result does not match the requested identity.");
        }

        return result;
    }
}
