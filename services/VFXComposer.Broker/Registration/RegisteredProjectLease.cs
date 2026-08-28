using System.Threading;
using VFXComposer.Broker.Ipc;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Broker.Native;
using VFXComposer.Protocol.Registration;

namespace VFXComposer.Broker.Registration;

internal sealed class RegisteredProjectLease : IDisposable
{
    private readonly object _issuer;
    private readonly object _handleGate = new();
    private int _usable = 1;
    private DuplicatedProjectHandleSet? _workerHandles;
    private WorkerHandleLeaseState _handleState = WorkerHandleLeaseState.Prepared;
    private WorkerProjectHandleGrant? _grant;
    private WorkerProjectHandleGrantAcknowledgement? _grantAcknowledgement;
    private WorkerProjectHandleRevoke? _revoke;
    private WorkerProjectHandleRevokeAcknowledgement? _revokeAcknowledgement;
    private int _activeReadResponsePublications;

    private RegisteredProjectLease(
        object issuer,
        string leaseId,
        RegisteredProjectIdentity project,
        long brokerGeneration,
        long leaseGeneration,
        AuthenticatedPeerSession desktopSession,
        AuthenticatedPeerSession workerSession)
    {
        _issuer = issuer;
        LeaseId = leaseId;
        Project = project;
        BrokerGeneration = brokerGeneration;
        LeaseGeneration = leaseGeneration;
        DesktopSession = desktopSession;
        WorkerSession = workerSession;
    }

    public string LeaseId { get; }
    public RegisteredProjectIdentity Project { get; }
    public long BrokerGeneration { get; }
    public long LeaseGeneration { get; }
    public AuthenticatedPeerSession DesktopSession { get; }
    public AuthenticatedPeerSession WorkerSession { get; }
    public bool IsUsable => Volatile.Read(ref _usable) == 1;
    internal DuplicatedProjectHandleSet? WorkerHandles => Volatile.Read(ref _workerHandles);
    internal WorkerHandleLeaseState HandleState
    {
        get
        {
            lock (_handleGate)
            {
                return _handleState;
            }
        }
    }

    internal bool HasActiveReadResponsePublication
    {
        get
        {
            lock (_handleGate)
            {
                return _activeReadResponsePublications != 0;
            }
        }
    }

    internal WorkerProjectHandleGrant? WorkerHandleGrant
    {
        get
        {
            lock (_handleGate)
            {
                return _grant;
            }
        }
    }

    internal WorkerProjectHandleRevoke? WorkerHandleRevoke
    {
        get
        {
            lock (_handleGate)
            {
                return _revoke;
            }
        }
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _usable, 0);
        DuplicatedProjectHandleSet? handles;
        lock (_handleGate)
        {
            while (_activeReadResponsePublications != 0)
            {
                Monitor.Wait(_handleGate);
            }

            if (_handleState == WorkerHandleLeaseState.Revoked && _workerHandles is null)
            {
                return;
            }

            _handleState = WorkerHandleLeaseState.Revoked;
            handles = Interlocked.Exchange(ref _workerHandles, null);
        }

        // DuplicatedProjectHandleSet itself refuses raw-number close after publish.
        handles?.Dispose();
    }

    internal bool TryAttachWorkerHandles(
        object issuer,
        DuplicatedProjectHandleSet workerHandles)
    {
        ArgumentNullException.ThrowIfNull(workerHandles);
        lock (_handleGate)
        {
            if (!ReferenceEquals(_issuer, issuer) || !IsUsable ||
                _handleState != WorkerHandleLeaseState.Prepared ||
                Interlocked.CompareExchange(ref _workerHandles, workerHandles, null) is not null)
            {
                return false;
            }

            if (!IsUsable)
            {
                Interlocked.Exchange(ref _workerHandles, null)?.Dispose();
                return false;
            }

            return true;
        }
    }

    internal bool TryPublishWorkerHandleGrant(
        object issuer,
        WorkerProjectHandleGrant candidate,
        out WorkerProjectHandleGrant? published)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        lock (_handleGate)
        {
            published = null;
            if (!ReferenceEquals(_issuer, issuer) || !IsUsable || _workerHandles is null)
            {
                return false;
            }

            if (_grant is not null)
            {
                if (_grant.SelfHash.FixedTimeEquals(candidate.SelfHash) &&
                    string.Equals(_grant.RequestId, candidate.RequestId, StringComparison.Ordinal))
                {
                    published = _grant;
                    return true;
                }

                return false;
            }

            if (_handleState != WorkerHandleLeaseState.Prepared ||
                !_workerHandles.TryMarkPublished())
            {
                return false;
            }

            _grant = candidate;
            _handleState = WorkerHandleLeaseState.GrantPublished;
            published = candidate;
            return true;
        }
    }

    internal bool TryAcknowledgeWorkerHandleGrant(
        object issuer,
        WorkerProjectHandleGrantAcknowledgement acknowledgement)
    {
        ArgumentNullException.ThrowIfNull(acknowledgement);
        lock (_handleGate)
        {
            if (!ReferenceEquals(_issuer, issuer) || _grant is null ||
                !MatchesLifecycleIdentity(acknowledgement.LeaseId, acknowledgement.BrokerGeneration,
                    acknowledgement.LeaseGeneration, acknowledgement.WorkerSessionId,
                    acknowledgement.WorkerProcessEpoch) ||
                !_grant.SelfHash.FixedTimeEquals(acknowledgement.GrantSelfHash))
            {
                return false;
            }

            if (_grantAcknowledgement is not null)
            {
                return _grantAcknowledgement.SelfHash.FixedTimeEquals(acknowledgement.SelfHash);
            }

            if (_handleState != WorkerHandleLeaseState.GrantPublished || !IsUsable)
            {
                return false;
            }

            _grantAcknowledgement = acknowledgement;
            _handleState = WorkerHandleLeaseState.GrantAcknowledged;
            return true;
        }
    }

    internal bool TryBeginRevocation(object issuer, out bool requiresWorkerAcknowledgement)
    {
        lock (_handleGate)
        {
            requiresWorkerAcknowledgement = false;
            if (!ReferenceEquals(_issuer, issuer))
            {
                return false;
            }

            Interlocked.Exchange(ref _usable, 0);
            while (_activeReadResponsePublications != 0)
            {
                Monitor.Wait(_handleGate);
            }

            if (_handleState == WorkerHandleLeaseState.Prepared)
            {
                _handleState = WorkerHandleLeaseState.Revoked;
                Interlocked.Exchange(ref _workerHandles, null)?.Dispose();
                return true;
            }

            if (_handleState is WorkerHandleLeaseState.GrantPublished or
                WorkerHandleLeaseState.GrantAcknowledged)
            {
                _handleState = WorkerHandleLeaseState.RevocationPending;
                requiresWorkerAcknowledgement = true;
                return true;
            }

            if (_handleState is WorkerHandleLeaseState.RevocationPending or
                WorkerHandleLeaseState.RevokePublished)
            {
                requiresWorkerAcknowledgement = true;
                return true;
            }

            return _handleState == WorkerHandleLeaseState.Revoked;
        }
    }

    internal bool TryReserveReadResponsePublication(
        object issuer,
        out ReadResponsePublicationReservation? reservation)
    {
        lock (_handleGate)
        {
            reservation = null;
            if (!ReferenceEquals(_issuer, issuer) || !IsUsable ||
                _handleState != WorkerHandleLeaseState.GrantAcknowledged ||
                _activeReadResponsePublications != 0)
            {
                return false;
            }

            _activeReadResponsePublications = 1;
            reservation = new ReadResponsePublicationReservation(this);
            return true;
        }
    }

    private void ReleaseReadResponsePublication()
    {
        lock (_handleGate)
        {
            if (_activeReadResponsePublications != 1)
            {
                throw new InvalidOperationException(
                    "Read response publication reservation is not active.");
            }

            _activeReadResponsePublications = 0;
            Monitor.PulseAll(_handleGate);
        }
    }

    internal bool TryPublishWorkerHandleRevoke(
        object issuer,
        WorkerProjectHandleRevoke candidate,
        out WorkerProjectHandleRevoke? published)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        lock (_handleGate)
        {
            published = null;
            if (!ReferenceEquals(_issuer, issuer) || _grant is null ||
                !MatchesLifecycleIdentity(candidate.LeaseId, candidate.BrokerGeneration,
                    candidate.LeaseGeneration, candidate.WorkerSessionId,
                    candidate.WorkerProcessEpoch) ||
                !_grant.SelfHash.FixedTimeEquals(candidate.GrantSelfHash))
            {
                return false;
            }

            if (_revoke is not null)
            {
                if (_revoke.SelfHash.FixedTimeEquals(candidate.SelfHash) &&
                    string.Equals(_revoke.RequestId, candidate.RequestId, StringComparison.Ordinal))
                {
                    published = _revoke;
                    return true;
                }

                return false;
            }

            if (_handleState != WorkerHandleLeaseState.RevocationPending)
            {
                return false;
            }

            _revoke = candidate;
            _handleState = WorkerHandleLeaseState.RevokePublished;
            published = candidate;
            return true;
        }
    }

    internal bool TryAcknowledgeWorkerHandleRevoke(
        object issuer,
        WorkerProjectHandleRevokeAcknowledgement acknowledgement)
    {
        ArgumentNullException.ThrowIfNull(acknowledgement);
        DuplicatedProjectHandleSet? handles;
        lock (_handleGate)
        {
            if (!ReferenceEquals(_issuer, issuer) || _grant is null || _revoke is null ||
                !MatchesLifecycleIdentity(acknowledgement.LeaseId, acknowledgement.BrokerGeneration,
                    acknowledgement.LeaseGeneration, acknowledgement.WorkerSessionId,
                    acknowledgement.WorkerProcessEpoch) ||
                !_grant.SelfHash.FixedTimeEquals(acknowledgement.GrantSelfHash) ||
                !_revoke.SelfHash.FixedTimeEquals(acknowledgement.RevokeSelfHash))
            {
                return false;
            }

            if (_revokeAcknowledgement is not null)
            {
                return _revokeAcknowledgement.SelfHash.FixedTimeEquals(acknowledgement.SelfHash);
            }

            if (_handleState != WorkerHandleLeaseState.RevokePublished)
            {
                return false;
            }

            _revokeAcknowledgement = acknowledgement;
            _handleState = WorkerHandleLeaseState.Revoked;
            handles = Interlocked.Exchange(ref _workerHandles, null);
            handles?.ConfirmWorkerClosed();
        }

        handles?.Dispose();
        return true;
    }

    internal bool TryFinalizeAfterWorkerExit(object issuer)
    {
        DuplicatedProjectHandleSet? handles;
        lock (_handleGate)
        {
            if (!ReferenceEquals(_issuer, issuer) ||
                _handleState is not (WorkerHandleLeaseState.RevocationPending or
                    WorkerHandleLeaseState.RevokePublished) ||
                _workerHandles is null || _workerHandles.IsTargetProcessActive)
            {
                return false;
            }

            _handleState = WorkerHandleLeaseState.Revoked;
            handles = Interlocked.Exchange(ref _workerHandles, null);
        }

        handles?.Dispose();
        return true;
    }

    private bool MatchesLifecycleIdentity(
        string leaseId,
        long brokerGeneration,
        long leaseGeneration,
        string workerSessionId,
        string workerProcessEpoch) =>
        string.Equals(LeaseId, leaseId, StringComparison.Ordinal) &&
        BrokerGeneration == brokerGeneration &&
        LeaseGeneration == leaseGeneration &&
        string.Equals(WorkerSession.SessionId, workerSessionId, StringComparison.Ordinal) &&
        string.Equals(WorkerSession.ProcessEpoch, workerProcessEpoch, StringComparison.Ordinal);

    internal static RegisteredProjectLease Issue(
        object issuer,
        object expectedIssuer,
        string leaseId,
        RegisteredProjectIdentity project,
        long brokerGeneration,
        long leaseGeneration,
        AuthenticatedPeerSession desktopSession,
        AuthenticatedPeerSession workerSession)
    {
        if (!ReferenceEquals(issuer, expectedIssuer))
        {
            throw new InvalidOperationException("Project lease issuer is invalid.");
        }

        return new RegisteredProjectLease(
            issuer,
            leaseId,
            project,
            brokerGeneration,
            leaseGeneration,
            desktopSession,
            workerSession);
    }

    internal bool WasIssuedBy(object issuer) => ReferenceEquals(_issuer, issuer);

    internal sealed class ReadResponsePublicationReservation : IDisposable
    {
        private RegisteredProjectLease? _owner;

        internal ReadResponsePublicationReservation(RegisteredProjectLease owner) =>
            _owner = owner;

        public void Dispose() =>
            Interlocked.Exchange(ref _owner, null)?.ReleaseReadResponsePublication();
    }
}

internal enum WorkerHandleLeaseState
{
    Prepared,
    GrantPublished,
    GrantAcknowledged,
    RevocationPending,
    RevokePublished,
    Revoked,
}
