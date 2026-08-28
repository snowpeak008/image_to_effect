using System.Text.Json;
using VFXComposer.Broker.Registration;
using VFXComposer.Protocol.Ipc;
using VFXComposer.Protocol.Json;
using VFXComposer.Protocol.Registration;

namespace VFXComposer.Broker.Ipc;

/// <summary>
/// Serial Broker-side grant/revoke exchange over one already-authenticated Worker pipe.
/// It owns no listener and cannot construct an authenticated session.
/// </summary>
internal sealed class WorkerHandleLifecycleTransport
{
    private readonly ProjectRegistrationStore _registrations;

    internal WorkerHandleLifecycleTransport(ProjectRegistrationStore registrations) =>
        _registrations = registrations ?? throw new ArgumentNullException(nameof(registrations));

    internal async ValueTask<bool> PublishGrantAndAwaitAcknowledgementAsync(
        AuthenticatedPeerConnection connection,
        RegisteredProjectLease lease,
        string requestId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(lease);
        if (!IsExactWorkerConnection(connection, lease))
        {
            return false;
        }

        try
        {
            await using var exchange = await connection.BeginExclusiveExchangeAsync(
                cancellationToken).ConfigureAwait(false);
            if (!IsExactWorkerConnection(connection, lease) ||
                !_registrations.TryCreateWorkerHandleGrant(
                    connection.Session,
                    lease,
                    requestId,
                    out var grant,
                    out _) ||
                grant is null)
            {
                await DisposeConnectionFailClosedAsync(connection).ConfigureAwait(false);
                return false;
            }

            var response = await exchange.ExchangeAsync(
                JsonSerializer.SerializeToUtf8Bytes(grant),
                cancellationToken).ConfigureAwait(false);
            var acknowledgement = StrictWireCodec.Decode<WorkerProjectHandleGrantAcknowledgement>(response);
            if (!_registrations.TryAcknowledgeWorkerHandleGrant(
                    connection.Session,
                    lease,
                    acknowledgement,
                    out _))
            {
                await DisposeConnectionFailClosedAsync(connection).ConfigureAwait(false);
                return false;
            }

            return true;
        }
        catch (Exception exception) when (IsExpectedTransportFailure(exception))
        {
            await DisposeConnectionFailClosedAsync(connection).ConfigureAwait(false);
            return false;
        }
    }

    internal async ValueTask<bool> RevokeAndAwaitAcknowledgementAsync(
        AuthenticatedPeerConnection connection,
        RegisteredProjectLease lease,
        string requestId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(lease);
        if (!IsExactWorkerConnection(connection, lease))
        {
            return false;
        }

        try
        {
            await using var exchange = await connection.BeginExclusiveExchangeAsync(
                cancellationToken).ConfigureAwait(false);
            if (!IsExactWorkerConnection(connection, lease))
            {
                await DisposeConnectionFailClosedAsync(connection).ConfigureAwait(false);
                return false;
            }

            var state = lease.HandleState;
            if (state is WorkerHandleLeaseState.GrantPublished or
                WorkerHandleLeaseState.GrantAcknowledged)
            {
                if (!_registrations.RevokeLease(lease.LeaseId))
                {
                    await DisposeConnectionFailClosedAsync(connection).ConfigureAwait(false);
                    return false;
                }
            }
            else if (state is not (WorkerHandleLeaseState.RevocationPending or
                         WorkerHandleLeaseState.RevokePublished))
            {
                await DisposeConnectionFailClosedAsync(connection).ConfigureAwait(false);
                return false;
            }

            if (!_registrations.TryCreateWorkerHandleRevoke(
                    connection.Session,
                    lease.LeaseId,
                    requestId,
                    out var revoke,
                    out _) ||
                revoke is null)
            {
                await DisposeConnectionFailClosedAsync(connection).ConfigureAwait(false);
                return false;
            }

            var response = await exchange.ExchangeAsync(
                JsonSerializer.SerializeToUtf8Bytes(revoke),
                cancellationToken).ConfigureAwait(false);
            var acknowledgement = StrictWireCodec.Decode<WorkerProjectHandleRevokeAcknowledgement>(response);
            if (!_registrations.TryAcknowledgeWorkerHandleRevoke(
                    connection.Session,
                    acknowledgement,
                    out _))
            {
                await DisposeConnectionFailClosedAsync(connection).ConfigureAwait(false);
                return false;
            }

            return true;
        }
        catch (Exception exception) when (IsExpectedTransportFailure(exception))
        {
            await DisposeConnectionFailClosedAsync(connection).ConfigureAwait(false);
            return false;
        }
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
            // The connection has already published its disposed state and the
            // pipe-close path runs in a finally block. Do not turn cleanup
            // observer diagnostics into a usable transport result.
        }
    }

    private static bool IsExactWorkerConnection(
        AuthenticatedPeerConnection connection,
        RegisteredProjectLease lease) =>
        connection.Session.IsUsable &&
        string.Equals(connection.Session.PeerRole, PeerRoles.Worker, StringComparison.Ordinal) &&
        ReferenceEquals(connection.Session, lease.WorkerSession) &&
        string.Equals(connection.Session.ProcessEpoch, lease.WorkerSession.ProcessEpoch, StringComparison.Ordinal);

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
