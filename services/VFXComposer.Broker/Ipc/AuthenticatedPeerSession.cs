using System.Threading;
using VFXComposer.Protocol.Hashing;
using Microsoft.Win32.SafeHandles;

namespace VFXComposer.Broker.Ipc;

/// <summary>Opaque broker-local capability. Wire DTOs cannot construct this type.</summary>
internal sealed class AuthenticatedPeerSession : IDisposable
{
    private readonly object _issuer;
    private readonly object _lifecycleGate = new();
    private int _activeResponsePublications;
    private bool _processHandleDisposed;
    private int _usable = 1;

    private AuthenticatedPeerSession(
        object issuer,
        string sessionId,
        string peerRole,
        int processId,
        string processEpoch,
        TypedHash imageIdentity,
        SafeProcessHandle processHandle,
        long brokerGeneration,
        IReadOnlyList<string> capabilities)
    {
        _issuer = issuer;
        SessionId = sessionId;
        PeerRole = peerRole;
        ProcessId = processId;
        ProcessEpoch = processEpoch;
        ImageIdentity = imageIdentity;
        ProcessHandle = processHandle;
        BrokerGeneration = brokerGeneration;
        Capabilities = capabilities;
    }

    public string SessionId { get; }
    public string PeerRole { get; }
    public int ProcessId { get; }
    public string ProcessEpoch { get; }
    public TypedHash ImageIdentity { get; }
    internal SafeProcessHandle ProcessHandle { get; }
    public long BrokerGeneration { get; }
    public IReadOnlyList<string> Capabilities { get; }
    public bool IsUsable => Volatile.Read(ref _usable) == 1;

    public void Dispose()
    {
        lock (_lifecycleGate)
        {
            Volatile.Write(ref _usable, 0);
            while (_activeResponsePublications != 0)
            {
                Monitor.Wait(_lifecycleGate);
            }

            if (_processHandleDisposed)
            {
                return;
            }

            ProcessHandle.Dispose();
            _processHandleDisposed = true;
        }
    }

    internal void Invalidate(object issuer)
    {
        if (!ReferenceEquals(_issuer, issuer))
        {
            throw new InvalidOperationException("Peer session issuer is invalid.");
        }

        lock (_lifecycleGate)
        {
            Volatile.Write(ref _usable, 0);
        }
    }

    internal bool TryReserveResponsePublication(
        out ResponsePublicationReservation? reservation)
    {
        lock (_lifecycleGate)
        {
            reservation = null;
            if (_usable != 1 || _activeResponsePublications != 0)
            {
                return false;
            }

            _activeResponsePublications = 1;
            reservation = new ResponsePublicationReservation(this);
            return true;
        }
    }

    private void ReleaseResponsePublication()
    {
        lock (_lifecycleGate)
        {
            if (_activeResponsePublications != 1)
            {
                throw new InvalidOperationException(
                    "Peer response publication reservation is not active.");
            }

            _activeResponsePublications = 0;
            Monitor.PulseAll(_lifecycleGate);
        }
    }

    internal static AuthenticatedPeerSession Issue(
        object issuer,
        object expectedIssuer,
        string sessionId,
        string peerRole,
        int processId,
        string processEpoch,
        TypedHash imageIdentity,
        SafeProcessHandle processHandle,
        long brokerGeneration,
        IReadOnlyList<string> capabilities)
    {
        if (!ReferenceEquals(issuer, expectedIssuer))
        {
            throw new InvalidOperationException("Peer session issuer is invalid.");
        }

        return new AuthenticatedPeerSession(
            issuer,
            sessionId,
            peerRole,
            processId,
            processEpoch,
            imageIdentity,
            processHandle,
            brokerGeneration,
            capabilities);
    }

    internal bool WasIssuedBy(object issuer) => ReferenceEquals(_issuer, issuer);

    internal sealed class ResponsePublicationReservation : IDisposable
    {
        private AuthenticatedPeerSession? _owner;

        internal ResponsePublicationReservation(AuthenticatedPeerSession owner) =>
            _owner = owner;

        public void Dispose() =>
            Interlocked.Exchange(ref _owner, null)?.ReleaseResponsePublication();
    }
}
