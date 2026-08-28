using VFXComposer.Broker.Security;

namespace VFXComposer.Broker.Configuration;

/// <summary>
/// One-use, in-memory correlation boundary between already host-issued bootstrap
/// material and a live Windows process observation. This class is deliberately
/// dormant: it returns only an opaque pinned observation and never a policy,
/// listener, ACL application, registration, handle grant, or authority result.
/// </summary>
internal sealed class HostBootstrapAttestationAdmission : IDisposable
{
    private readonly object _gate = new();
    private readonly HostIssuedBootstrapMaterial _material;
    private readonly ProductionTrustProfile _profile;
    private readonly HostBootstrapIssuerProvenance _expectedIssuer;
    private readonly WindowsServiceProcessAttestationExpectation _expectation;
    private readonly bool _staticBindingMatches;
    private WindowsServiceProcessAttestation? _activeObservation;
    // 0=unconsumed, 1=correlated or failed, 2=revoked/disposed.
    private int _state;

    internal HostBootstrapAttestationAdmission(
        HostIssuedBootstrapMaterial material,
        ProductionTrustProfile profile,
        HostBootstrapIssuerProvenance expectedIssuer,
        WindowsServiceProcessAttestationExpectation expectation)
    {
        _material = material ?? throw new ArgumentNullException(nameof(material));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _expectedIssuer = expectedIssuer ?? throw new ArgumentNullException(nameof(expectedIssuer));
        _expectation = expectation ?? throw new ArgumentNullException(nameof(expectation));

        // A path baseline is required even for this narrow correlation. It must
        // have originated from a pinned OS observation; it is not accepted as a
        // caller path and it does not prove executable bytes.
        _staticBindingMatches =
            _expectation.ExpectedImagePathObservation is not null &&
            _material.Matches(
                _profile,
                _expectedIssuer,
                _expectation.ExpectedProcess);
    }

    /// <summary>
    /// Correlates one fresh default Windows observation. A successful return is
    /// still only a path/token/process observation. Its content-identity status is
    /// explicitly unavailable, so this method must not be treated as production
    /// admission.
    /// </summary>
    internal bool TryCorrelateObservationAt(
        long observedUnixMilliseconds,
        out WindowsServiceProcessAttestation? observation) =>
        TryCorrelateObservationAtCore(
            observedUnixMilliseconds,
            out observation);

    /// <summary>
    /// Revokes the active observation and waits for its uniquely owned native pin
    /// to close. It is linearizable with an in-flight correlation attempt and is
    /// idempotent after either failure or success.
    /// </summary>
    internal void Revoke()
    {
        lock (_gate)
        {
            if (_state == 2)
            {
                return;
            }

            _state = 2;
            var observation = _activeObservation;
            _activeObservation = null;
            observation?.Revoke();
        }
    }

    public void Dispose() => Revoke();

    private bool TryCorrelateObservationAtCore(
        long observedUnixMilliseconds,
        out WindowsServiceProcessAttestation? observation)
    {
        observation = null;
        lock (_gate)
        {
            if (_state != 0 ||
                !IsCorrelationCurrentAt(observedUnixMilliseconds))
            {
                return false;
            }

            // Consume before touching a native process object so a failed/racing
            // observation cannot be replayed into a later attempt.
            _state = 1;
            WindowsServiceProcessAttestation? candidate = null;
            try
            {
                var observed = WindowsServiceProcessAttestation.TryObserve(
                    _expectation,
                    out candidate);
                if (!observed ||
                    candidate is null ||
                    _state != 1 ||
                    !IsCorrelationCurrentAt(observedUnixMilliseconds))
                {
                    return false;
                }

                // The native observer has no caller-controlled callback seam.
                // Preserve the post-observation state and correlation recheck
                // before ownership transfer; any untransferred candidate closes
                // in finally exactly once.
                _activeObservation = candidate;
                observation = candidate;
                candidate = null;
                return true;
            }
            finally
            {
                candidate?.Dispose();
            }
        }
    }

    private bool IsCorrelationCurrentAt(long observedUnixMilliseconds) =>
        _staticBindingMatches &&
        _material.IsCurrentAt(observedUnixMilliseconds) &&
        _material.Matches(
            _profile,
            _expectedIssuer,
            _expectation.ExpectedProcess);
}
