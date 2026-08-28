using System.Text.Json;
using VFXComposer.Broker.Registration;
using VFXComposer.Protocol.Ipc;
using VFXComposer.Protocol.Json;
using VFXComposer.Protocol.Queries;

namespace VFXComposer.Broker.Ipc;

/// <summary>
/// Serves one identity-only Desktop read request over an already authenticated
/// connection. This class neither opens project content nor creates a listener.
/// </summary>
internal sealed class DesktopReadQueryTransport
{
    private readonly WorkerReadQueryTransport _workerTransport;
    private int _active;

    internal DesktopReadQueryTransport(WorkerReadQueryTransport workerTransport) =>
        _workerTransport = workerTransport ?? throw new ArgumentNullException(nameof(workerTransport));

    internal async ValueTask<bool> ServeOneAsync(
        AuthenticatedPeerConnection desktopConnection,
        AuthenticatedPeerConnection workerConnection,
        RegisteredProjectLease lease,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(desktopConnection);
        ArgumentNullException.ThrowIfNull(workerConnection);
        ArgumentNullException.ThrowIfNull(lease);

        if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
        {
            await DisposeFailClosedAsync(desktopConnection).ConfigureAwait(false);
            return false;
        }

        try
        {
            if (!IsExactDesktopRoute(desktopConnection, lease))
            {
                await DisposeFailClosedAsync(desktopConnection).ConfigureAwait(false);
                return false;
            }

            await using var exchange = await desktopConnection.BeginExclusiveExchangeAsync(
                cancellationToken).ConfigureAwait(false);
            if (!IsExactDesktopRoute(desktopConnection, lease))
            {
                await DisposeFailClosedAsync(desktopConnection).ConfigureAwait(false);
                return false;
            }

            await exchange.ReceiveAndReplyAsync(
                async (requestBytes, requestCancellation) =>
                {
                    var query = StrictWireCodec.Decode<ReadDocumentQuery>(requestBytes);
                    var result = await _workerTransport.RouteAndReadAsync(
                        workerConnection,
                        desktopConnection.Session,
                        lease,
                        query,
                        requestCancellation).ConfigureAwait(false);
                    if (result is null || !IsExactDesktopRoute(desktopConnection, lease))
                    {
                        throw new InvalidDataException(BrokerDiagnosticCodes.QueryRejected);
                    }

                    var responseBytes = JsonSerializer.SerializeToUtf8Bytes(result);
                    if (!_workerTransport.TryReserveResponsePublication(
                            workerConnection,
                            desktopConnection.Session,
                            lease,
                            query,
                            out var reservation) ||
                        reservation is null)
                    {
                        throw new InvalidDataException(BrokerDiagnosticCodes.QueryRejected);
                    }

                    return new AuthenticatedPeerConnection.ExclusiveExchange.GuardedReply(
                        responseBytes,
                        reservation);
                },
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (IsExpectedFailure(exception))
        {
            await DisposeFailClosedAsync(desktopConnection).ConfigureAwait(false);
            return false;
        }
        finally
        {
            Volatile.Write(ref _active, 0);
        }
    }

    private static bool IsExactDesktopRoute(
        AuthenticatedPeerConnection connection,
        RegisteredProjectLease lease) =>
        connection.Session.IsUsable &&
        string.Equals(connection.Session.PeerRole, PeerRoles.Desktop, StringComparison.Ordinal) &&
        ReferenceEquals(connection.Session, lease.DesktopSession);

    private static async ValueTask DisposeFailClosedAsync(AuthenticatedPeerConnection connection)
    {
        try
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // Disposed is published before observer cleanup. Never turn cleanup
            // diagnostics into a query result.
        }
    }

    private static bool IsExpectedFailure(Exception exception) =>
        exception is ArgumentException or
            InvalidDataException or
            InvalidOperationException or
            EndOfStreamException or
            IOException or
            ObjectDisposedException or
            OperationCanceledException or
            WireDecodeException;
}
