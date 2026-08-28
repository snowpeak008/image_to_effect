using System.Text.Json;
using VFXComposer.Broker.Queries;
using VFXComposer.Broker.Registration;
using VFXComposer.Protocol.Diagnostics;
using VFXComposer.Protocol.Ipc;
using VFXComposer.Protocol.Json;
using VFXComposer.Protocol.Queries;

namespace VFXComposer.Broker.Ipc;

/// <summary>
/// Routes one already-validated Desktop query over an authenticated Worker connection.
/// The Broker never opens project content and returns no result after route/lease drift.
/// </summary>
internal sealed class WorkerReadQueryTransport
{
    private static readonly HashSet<string> WorkerReadDiagnosticCodes = new(
        new[]
        {
            StableDiagnosticCodes.ProjectLeaseRejected,
            StableDiagnosticCodes.ProjectDocumentUnavailable,
            StableDiagnosticCodes.ProjectDocumentContentMismatch,
        },
        StringComparer.Ordinal);

    private readonly ReadOnlyQueryRouter _router;

    internal WorkerReadQueryTransport(ReadOnlyQueryRouter router) =>
        _router = router ?? throw new ArgumentNullException(nameof(router));

    internal async ValueTask<ReadDocumentResult?> RouteAndReadAsync(
        AuthenticatedPeerConnection workerConnection,
        AuthenticatedPeerSession desktopSession,
        RegisteredProjectLease lease,
        ReadDocumentQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workerConnection);
        ArgumentNullException.ThrowIfNull(desktopSession);
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(query);

        if (!TryReplayRoute(workerConnection, desktopSession, lease, query))
        {
            return null;
        }

        try
        {
            await using var exchange = await workerConnection.BeginExclusiveExchangeAsync(
                cancellationToken).ConfigureAwait(false);
            if (!TryReplayRoute(workerConnection, desktopSession, lease, query))
            {
                return null;
            }

            var responseBytes = await exchange.ExchangeAsync(
                JsonSerializer.SerializeToUtf8Bytes(query),
                cancellationToken).ConfigureAwait(false);
            var result = StrictWireCodec.Decode<ReadDocumentResult>(responseBytes);
            if (!MatchesQuery(result, query))
            {
                await DisposeConnectionFailClosedAsync(workerConnection).ConfigureAwait(false);
                return null;
            }

            // A Desktop/session/registration revocation that overlaps the read discards
            // the bytes. The connection stays available for the ordered revoke exchange.
            if (!TryReplayRoute(workerConnection, desktopSession, lease, query))
            {
                return null;
            }

            return result;
        }
        catch (Exception exception) when (IsExpectedTransportFailure(exception))
        {
            await DisposeConnectionFailClosedAsync(workerConnection).ConfigureAwait(false);
            return null;
        }
    }

    internal bool TryReserveResponsePublication(
        AuthenticatedPeerConnection workerConnection,
        AuthenticatedPeerSession desktopSession,
        RegisteredProjectLease lease,
        ReadDocumentQuery query,
        out IDisposable? reservation)
    {
        reservation = null;
        IDisposable? workerConnectionReservation = null;
        AuthenticatedPeerSession.ResponsePublicationReservation? desktopReservation = null;
        AuthenticatedPeerSession.ResponsePublicationReservation? workerReservation = null;
        RegisteredProjectLease.ReadResponsePublicationReservation? leaseReservation = null;
        try
        {
            if (!workerConnection.Session.IsUsable ||
                !string.Equals(
                    workerConnection.Session.PeerRole,
                    PeerRoles.Worker,
                    StringComparison.Ordinal) ||
                !ReferenceEquals(workerConnection.Session, lease.WorkerSession) ||
                !workerConnection.TryReserveResponsePublication(
                    out workerConnectionReservation) ||
                workerConnectionReservation is null ||
                !desktopSession.TryReserveResponsePublication(out desktopReservation) ||
                desktopReservation is null ||
                !workerConnection.Session.TryReserveResponsePublication(out workerReservation) ||
                workerReservation is null)
            {
                return false;
            }

            if (!_router.TryReserveResponsePublication(
                    desktopSession,
                    workerConnection.Session,
                    lease,
                    query,
                    out leaseReservation) ||
                leaseReservation is null)
            {
                return false;
            }

            reservation = new ResponsePublicationReservation(
                workerConnectionReservation,
                desktopReservation,
                workerReservation,
                leaseReservation);
            workerConnectionReservation = null;
            desktopReservation = null;
            workerReservation = null;
            leaseReservation = null;
            return true;
        }
        finally
        {
            leaseReservation?.Dispose();
            workerReservation?.Dispose();
            desktopReservation?.Dispose();
            workerConnectionReservation?.Dispose();
        }
    }

    private sealed class ResponsePublicationReservation : IDisposable
    {
        private IDisposable? _desktopReservation;
        private IDisposable? _workerReservation;
        private IDisposable? _workerConnectionReservation;
        private IDisposable? _leaseReservation;

        internal ResponsePublicationReservation(
            IDisposable workerConnectionReservation,
            IDisposable desktopReservation,
            IDisposable workerReservation,
            IDisposable leaseReservation)
        {
            _workerConnectionReservation = workerConnectionReservation;
            _desktopReservation = desktopReservation;
            _workerReservation = workerReservation;
            _leaseReservation = leaseReservation;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _leaseReservation, null)?.Dispose();
            Interlocked.Exchange(ref _workerReservation, null)?.Dispose();
            Interlocked.Exchange(ref _desktopReservation, null)?.Dispose();
            Interlocked.Exchange(ref _workerConnectionReservation, null)?.Dispose();
        }
    }

    private bool TryReplayRoute(
        AuthenticatedPeerConnection workerConnection,
        AuthenticatedPeerSession desktopSession,
        RegisteredProjectLease lease,
        ReadDocumentQuery query) =>
        workerConnection.Session.IsUsable &&
        string.Equals(workerConnection.Session.PeerRole, PeerRoles.Worker, StringComparison.Ordinal) &&
        ReferenceEquals(workerConnection.Session, lease.WorkerSession) &&
        lease.HandleState == WorkerHandleLeaseState.GrantAcknowledged &&
        _router.TryRoute(desktopSession, lease, query, out var routed, out _) &&
        routed is not null &&
        ReferenceEquals(routed.Lease, lease) &&
        ReferenceEquals(routed.WorkerSession, workerConnection.Session) &&
        ReferenceEquals(routed.Query, query);

    private static bool MatchesQuery(ReadDocumentResult result, ReadDocumentQuery query)
    {
        if (!string.Equals(result.RequestId, query.RequestId, StringComparison.Ordinal) ||
            !result.ProjectIdentity.FixedTimeEquals(query.ProjectIdentity) ||
            !string.Equals(result.DocumentKind, query.DocumentKind, StringComparison.Ordinal) ||
            !string.Equals(result.DocumentId, query.DocumentId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!result.Accepted)
        {
            return result.Diagnostic is not null &&
                   WorkerReadDiagnosticCodes.Contains(result.Diagnostic.Code);
        }

        return result.ContentHash is not null &&
               (query.ExpectedContentHash is null ||
                result.ContentHash.FixedTimeEquals(query.ExpectedContentHash));
    }

    private static async ValueTask DisposeConnectionFailClosedAsync(
        AuthenticatedPeerConnection connection)
    {
        try
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // The disposed state is already published and the pipe-close path runs
            // in finally. Never turn cleanup observer diagnostics into a result.
        }
    }

    private static bool IsExpectedTransportFailure(Exception exception) =>
        exception is ArgumentException or
            InvalidDataException or
            InvalidOperationException or
            EndOfStreamException or
            IOException or
            ObjectDisposedException or
            OperationCanceledException or
            WireDecodeException;
}
