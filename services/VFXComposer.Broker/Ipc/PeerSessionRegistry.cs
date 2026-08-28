using System.Collections.Concurrent;
using VFXComposer.Broker.Configuration;
using VFXComposer.Protocol.Ipc;
using VFXComposer.Broker.Security;

namespace VFXComposer.Broker.Ipc;

internal sealed class PeerSessionRegistry : IDisposable
{
    private readonly object _issuer = new();
    private readonly object _lifecycleGate = new();
    private readonly object _revocationGate = new();
    private readonly BrokerPolicy _policy;
    private readonly ConcurrentDictionary<string, AuthenticatedPeerSession> _sessions =
        new(StringComparer.Ordinal);
    private readonly Dictionary<(int ProcessId, string ProcessEpoch), string> _workerSessionsByProcess = new();
    private long _nextSession;
    private int _disposed;
    private int _observerInvocationDepth;

    internal event Action<AuthenticatedPeerSession>? SessionRevoked;

    public PeerSessionRegistry(BrokerPolicy policy) =>
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));

    public bool TryAuthenticate(
        PeerHello hello,
        ObservedPeerFacts observed,
        out AuthenticatedPeerSession? session,
        out PeerSessionAccepted? receipt,
        out string diagnosticCode)
    {
        ArgumentNullException.ThrowIfNull(hello);
        ArgumentNullException.ThrowIfNull(observed);
        session = null;
        receipt = null;
        diagnosticCode = BrokerDiagnosticCodes.PeerRejected;
        try
        {
            if (observed.ProcessId <= 0 ||
                hello.ProcessId != observed.ProcessId ||
                !string.Equals(hello.ProcessEpoch, observed.ProcessEpoch, StringComparison.Ordinal) ||
                !_policy.UserSidIdentity.FixedTimeEquals(observed.UserSidIdentity) ||
                !hello.ImageIdentity.FixedTimeEquals(observed.ImageIdentity) ||
                !_policy.AllowsImage(hello.PeerRole, observed.ImageIdentity))
            {
                return false;
            }

            var negotiated = hello.OfferedCapabilities
                .Where(PeerCapabilityIds.All.Contains)
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (!negotiated.Contains(PeerCapabilityIds.PeerSessionV1, StringComparer.Ordinal))
            {
                return false;
            }

            lock (_lifecycleGate)
            {
                if (_disposed != 0)
                {
                    return false;
                }

                var workerKey = (observed.ProcessId, observed.ProcessEpoch);
                if (string.Equals(hello.PeerRole, PeerRoles.Worker, StringComparison.Ordinal) &&
                    _workerSessionsByProcess.ContainsKey(workerKey))
                {
                    return false;
                }

                var ordinal = checked(++_nextSession);
                var sessionId = $"session-{_policy.BrokerGeneration}-{ordinal}";
                var candidateReceipt = new PeerSessionAccepted(
                    hello.RequestId,
                    sessionId,
                    hello.PeerRole,
                    _policy.BrokerInstanceId,
                    _policy.BrokerGeneration,
                    observed.ProcessEpoch,
                    negotiated);
                var candidate = AuthenticatedPeerSession.Issue(
                    _issuer,
                    _issuer,
                    sessionId,
                    hello.PeerRole,
                    observed.ProcessId,
                    observed.ProcessEpoch,
                    observed.ImageIdentity,
                    observed.TakeProcessHandle(),
                    _policy.BrokerGeneration,
                    Array.AsReadOnly(negotiated));
                if (!_sessions.TryAdd(sessionId, candidate))
                {
                    candidate.Dispose();
                    return false;
                }

                if (string.Equals(hello.PeerRole, PeerRoles.Worker, StringComparison.Ordinal))
                {
                    if (!_workerSessionsByProcess.TryAdd(workerKey, sessionId))
                    {
                        _sessions.TryRemove(sessionId, out _);
                        candidate.Dispose();
                        return false;
                    }
                }

                session = candidate;
                receipt = candidateReceipt;
                diagnosticCode = string.Empty;
                return true;
            }
        }
        finally
        {
            observed.Dispose();
        }
    }

    public bool IsCurrent(AuthenticatedPeerSession? session, string expectedRole)
    {
        try
        {
            lock (_lifecycleGate)
            {
                if (_disposed != 0 || session is null || !session.IsUsable ||
                    !session.WasIssuedBy(_issuer) ||
                    !string.Equals(session.PeerRole, expectedRole, StringComparison.Ordinal) ||
                    session.BrokerGeneration != _policy.BrokerGeneration ||
                    !ProcessEpoch.IsActive(session.ProcessHandle) ||
                    !string.Equals(
                        ProcessEpoch.Observe(session.ProcessHandle, session.ProcessId),
                        session.ProcessEpoch,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                if (!_sessions.TryGetValue(session.SessionId, out var registered) ||
                    !ReferenceEquals(registered, session))
                {
                    return false;
                }

                return !string.Equals(expectedRole, PeerRoles.Worker, StringComparison.Ordinal) ||
                       _workerSessionsByProcess.TryGetValue(
                           (session.ProcessId, session.ProcessEpoch),
                           out var workerSessionId) &&
                       string.Equals(workerSessionId, session.SessionId, StringComparison.Ordinal);
            }
        }
        catch (Exception exception) when (
            exception is InvalidDataException or ObjectDisposedException)
        {
            return false;
        }
    }

    public bool Revoke(string sessionId)
    {
        lock (_revocationGate)
        {
            return RevokeCore(sessionId);
        }
    }

    private bool RevokeCore(string sessionId)
    {
        AuthenticatedPeerSession session;
        lock (_lifecycleGate)
        {
            if (!_sessions.TryRemove(sessionId, out session!))
            {
                return false;
            }

            // Invalidate the capability before observers run. Keep the Worker
            // PID/epoch reservation until every observer has completed so a
            // replacement cannot overlap cleanup of the old session.
            session.Invalidate(_issuer);
        }

        // Waiting for an in-flight response publication while holding the
        // registry lifecycle gate would invert the publication replay order.
        // Current-map removal and invalidation above are already atomic;
        // drain the session reservation only after releasing that gate.
        session.Dispose();

        List<Exception>? observerFailures = null;
        try
        {
            if (SessionRevoked is { } handlers)
            {
                _observerInvocationDepth++;
                try
                {
                    foreach (Action<AuthenticatedPeerSession> handler in handlers.GetInvocationList())
                    {
                        try
                        {
                            handler(session);
                        }
                        catch (Exception exception)
                        {
                            (observerFailures ??= []).Add(exception);
                        }
                    }
                }
                finally
                {
                    _observerInvocationDepth--;
                }
            }
        }
        finally
        {
            if (string.Equals(session.PeerRole, PeerRoles.Worker, StringComparison.Ordinal))
            {
                lock (_lifecycleGate)
                {
                    var workerKey = (session.ProcessId, session.ProcessEpoch);
                    if (_workerSessionsByProcess.TryGetValue(workerKey, out var current) &&
                        string.Equals(current, session.SessionId, StringComparison.Ordinal))
                    {
                        _workerSessionsByProcess.Remove(workerKey);
                    }
                }
            }
        }

        if (observerFailures is not null)
        {
            throw new AggregateException(
                "One or more peer-session revocation observers failed.",
                observerFailures);
        }

        return true;
    }

    public void Dispose()
    {
        lock (_revocationGate)
        {
            if (_observerInvocationDepth != 0)
            {
                throw new InvalidOperationException(
                    "Peer session observers cannot synchronously dispose the registry.");
            }

            string[] sessionIds;
            lock (_lifecycleGate)
            {
                if (_disposed != 0)
                {
                    return;
                }

                _disposed = 1;
                sessionIds = _sessions.Keys.ToArray();
            }

            List<Exception>? failures = null;
            foreach (var sessionId in sessionIds)
            {
                try
                {
                    RevokeCore(sessionId);
                }
                catch (Exception exception)
                {
                    (failures ??= []).Add(exception);
                }
            }

            if (failures is not null)
            {
                throw new AggregateException(
                    "One or more peer sessions failed during registry disposal.",
                    failures);
            }
        }
    }
}
