using VFXComposer.Broker.Ipc;
using VFXComposer.Broker.Registration;
using VFXComposer.Protocol.Ipc;
using VFXComposer.Protocol.Queries;

namespace VFXComposer.Broker.Queries;

internal sealed record RoutedReadQuery(
    ReadDocumentQuery Query,
    RegisteredProjectLease Lease,
    AuthenticatedPeerSession WorkerSession);

/// <summary>Identity-only routing. The broker never opens or parses project content.</summary>
internal sealed class ReadOnlyQueryRouter
{
    private readonly ProjectRegistrationStore _registrations;
    private readonly PeerSessionRegistry _sessions;

    public ReadOnlyQueryRouter(ProjectRegistrationStore registrations, PeerSessionRegistry sessions)
    {
        _registrations = registrations ?? throw new ArgumentNullException(nameof(registrations));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
    }

    public bool TryRoute(
        AuthenticatedPeerSession desktopSession,
        RegisteredProjectLease lease,
        ReadDocumentQuery query,
        out RoutedReadQuery? routed,
        out string diagnosticCode)
    {
        routed = null;
        diagnosticCode = BrokerDiagnosticCodes.QueryRejected;
        if (!_sessions.IsCurrent(desktopSession, PeerRoles.Desktop) ||
            !_registrations.IsCurrent(lease) ||
            !ReferenceEquals(desktopSession, lease.DesktopSession) ||
            !query.ProjectIdentity.FixedTimeEquals(lease.Project.ProjectIdentity) ||
            !string.Equals(query.LeaseId, lease.LeaseId, StringComparison.Ordinal) ||
            query.LeaseGeneration != lease.LeaseGeneration)
        {
            return false;
        }

        routed = new RoutedReadQuery(query, lease, lease.WorkerSession);
        diagnosticCode = string.Empty;
        return true;
    }

    internal bool TryReserveResponsePublication(
        AuthenticatedPeerSession desktopSession,
        AuthenticatedPeerSession workerSession,
        RegisteredProjectLease lease,
        ReadDocumentQuery query,
        out RegisteredProjectLease.ReadResponsePublicationReservation? reservation) =>
        _registrations.TryReserveReadResponsePublication(
            desktopSession,
            workerSession,
            lease,
            query,
            out reservation);
}
