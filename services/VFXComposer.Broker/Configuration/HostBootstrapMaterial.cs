using System.Globalization;
using System.Threading;
using VFXComposer.Broker.Security;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;

namespace VFXComposer.Broker.Configuration;

/// <summary>
/// An exact Windows service process identity supplied by a future host-side
/// attestation boundary. This type validates shape and correlation only; it does
/// not observe a process or claim that any supplied value has been OS-attested.
/// </summary>
internal sealed class WindowsServiceProcessIdentity
{
    internal WindowsServiceProcessIdentity(
        WindowsSid serviceSid,
        TypedHash imageIdentity,
        int processId,
        string processEpoch,
        long generation,
        string sessionId)
    {
        ArgumentNullException.ThrowIfNull(serviceSid);
        ArgumentNullException.ThrowIfNull(imageIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(processEpoch);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        if (serviceSid.PrincipalKind != WindowsSidPrincipalKind.Service ||
            !string.Equals(
                imageIdentity.TypeTag,
                PeerHello.ProcessImageIdentityType,
                StringComparison.Ordinal) ||
            processId <= 0 ||
            generation <= 0 ||
            !VFXComposer.Broker.Security.ProcessEpoch.IsCanonicalForProcess(
                processId,
                processEpoch) ||
            !IsCanonicalSessionId(sessionId, generation))
        {
            throw new ArgumentException("Windows service process identity is invalid.");
        }

        ServiceSid = serviceSid;
        ImageIdentity = imageIdentity;
        ProcessId = processId;
        ProcessEpoch = processEpoch;
        Generation = generation;
        SessionId = sessionId;
    }

    public WindowsSid ServiceSid { get; }

    public TypedHash ImageIdentity { get; }

    public int ProcessId { get; }

    public string ProcessEpoch { get; }

    public long Generation { get; }

    public string SessionId { get; }

    internal bool FixedEquals(WindowsServiceProcessIdentity? other) =>
        other is not null &&
        ServiceSid.FixedEquals(other.ServiceSid) &&
        ImageIdentity.FixedTimeEquals(other.ImageIdentity) &&
        ProcessId == other.ProcessId &&
        string.Equals(ProcessEpoch, other.ProcessEpoch, StringComparison.Ordinal) &&
        Generation == other.Generation &&
        string.Equals(SessionId, other.SessionId, StringComparison.Ordinal);

    private static bool IsCanonicalSessionId(string value, long generation)
    {
        var prefix = string.Concat(
            "service-",
            generation.ToString(CultureInfo.InvariantCulture),
            "-");
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var ordinalText = value[prefix.Length..];
        return long.TryParse(
                   ordinalText,
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out var ordinal) &&
               ordinal > 0 &&
               string.Equals(
                   ordinalText,
                   ordinal.ToString(CultureInfo.InvariantCulture),
                   StringComparison.Ordinal);
    }
}

/// <summary>
/// Provenance declared by a separately privileged Windows host. It is opaque
/// capability state, not a caller claim, wire DTO, or self-authenticating token.
/// </summary>
internal sealed class HostBootstrapIssuerProvenance
{
    internal HostBootstrapIssuerProvenance(WindowsServiceProcessIdentity issuerProcess)
    {
        IssuerProcess = issuerProcess ?? throw new ArgumentNullException(nameof(issuerProcess));
    }

    public WindowsServiceProcessIdentity IssuerProcess { get; }

    internal bool FixedEquals(HostBootstrapIssuerProvenance? other) =>
        other is not null && IssuerProcess.FixedEquals(other.IssuerProcess);
}

/// <summary>
/// Immutable bootstrap material that a future independently privileged host may
/// supply after its own attestation. This repository neither creates an issuer nor
/// invokes this material from the production entry point.
/// </summary>
internal sealed class HostIssuedBootstrapMaterial
{
    internal const long MaximumLifetimeMilliseconds = 300_000;

    // This is intentionally a reference identity rather than a serializable or
    // durable profile fingerprint. A future independent host must provide its
    // own durable profile identity and attestation before production activation.
    private readonly ProductionTrustProfile _profile;

    internal HostIssuedBootstrapMaterial(
        string materialId,
        HostBootstrapIssuerProvenance issuerProvenance,
        WindowsServiceProcessIdentity brokerService,
        ProductionTrustProfile profile,
        long issuedAtUnixMilliseconds,
        long expiresAtUnixMilliseconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(materialId);
        ArgumentNullException.ThrowIfNull(issuerProvenance);
        ArgumentNullException.ThrowIfNull(brokerService);
        ArgumentNullException.ThrowIfNull(profile);

        if (brokerService.Generation != profile.BrokerGeneration ||
            issuerProvenance.IssuerProcess.Generation != brokerService.Generation ||
            issuerProvenance.IssuerProcess.ServiceSid.FixedEquals(brokerService.ServiceSid) ||
            !IsCanonicalMaterialId(materialId, brokerService.Generation) ||
            issuedAtUnixMilliseconds < 0 ||
            expiresAtUnixMilliseconds <= issuedAtUnixMilliseconds ||
            expiresAtUnixMilliseconds - issuedAtUnixMilliseconds > MaximumLifetimeMilliseconds)
        {
            throw new ArgumentException("Host bootstrap material is not independently bounded.");
        }

        MaterialId = materialId;
        _profile = profile;
        IssuerProvenance = issuerProvenance;
        BrokerService = brokerService;
        PipeAclIntent = new WindowsNamedPipeAclProvisioningIntent(profile, brokerService);
        IssuedAtUnixMilliseconds = issuedAtUnixMilliseconds;
        ExpiresAtUnixMilliseconds = expiresAtUnixMilliseconds;
    }

    public string MaterialId { get; }

    public HostBootstrapIssuerProvenance IssuerProvenance { get; }

    public WindowsServiceProcessIdentity BrokerService { get; }

    public WindowsNamedPipeAclProvisioningIntent PipeAclIntent { get; }

    public long IssuedAtUnixMilliseconds { get; }

    public long ExpiresAtUnixMilliseconds { get; }

    internal bool IsCurrentAt(long observedUnixMilliseconds) =>
        observedUnixMilliseconds >= IssuedAtUnixMilliseconds &&
        observedUnixMilliseconds < ExpiresAtUnixMilliseconds;

    internal bool Matches(
        ProductionTrustProfile profile,
        HostBootstrapIssuerProvenance expectedIssuer,
        WindowsServiceProcessIdentity expectedBrokerService) =>
        ReferenceEquals(_profile, profile) &&
        expectedIssuer is not null &&
        expectedBrokerService is not null &&
        IssuerProvenance.FixedEquals(expectedIssuer) &&
        BrokerService.FixedEquals(expectedBrokerService) &&
        PipeAclIntent.Matches(profile, expectedBrokerService);

    private static bool IsCanonicalMaterialId(string value, long generation)
    {
        if (value.Length > 128)
        {
            return false;
        }

        var prefix = string.Concat(
            "bootstrap-",
            generation.ToString(CultureInfo.InvariantCulture),
            "-");
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var ordinalText = value[prefix.Length..];
        return long.TryParse(
                   ordinalText,
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out var ordinal) &&
               ordinal > 0 &&
               string.Equals(
                   ordinalText,
                   ordinal.ToString(CultureInfo.InvariantCulture),
                   StringComparison.Ordinal);
    }
}

/// <summary>
/// One-use, in-memory validation boundary for already host-issued bootstrap
/// material. It is deliberately detached from production loading, listener
/// creation, registration, project access, and peer/handle admission.
/// </summary>
internal sealed class HostBootstrapMaterialValidator : IDisposable
{
    private readonly object _gate = new();
    private readonly ProductionTrustProfile _profile;
    private readonly HostBootstrapIssuerProvenance _expectedIssuer;
    private readonly WindowsServiceProcessIdentity _expectedBrokerService;
    private readonly HashSet<string> _consumedMaterialIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Lease> _activeLeases = new(StringComparer.Ordinal);
    private bool _disposed;

    internal HostBootstrapMaterialValidator(
        ProductionTrustProfile profile,
        HostBootstrapIssuerProvenance expectedIssuer,
        WindowsServiceProcessIdentity expectedBrokerService)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _expectedIssuer = expectedIssuer ?? throw new ArgumentNullException(nameof(expectedIssuer));
        _expectedBrokerService = expectedBrokerService
            ?? throw new ArgumentNullException(nameof(expectedBrokerService));

        if (_expectedIssuer.IssuerProcess.Generation != _profile.BrokerGeneration ||
            _expectedBrokerService.Generation != _profile.BrokerGeneration ||
            _expectedIssuer.IssuerProcess.ServiceSid.FixedEquals(
                _expectedBrokerService.ServiceSid) ||
            !new WindowsNamedPipeAclProvisioningIntent(
                _profile,
                _expectedBrokerService).Matches(_profile, _expectedBrokerService))
        {
            throw new ArgumentException("Host bootstrap validator is not bound to one service generation.");
        }
    }

    internal int ActiveLeaseCount
    {
        get
        {
            lock (_gate)
            {
                return _activeLeases.Count;
            }
        }
    }

    internal bool TryAcquire(
        HostIssuedBootstrapMaterial? material,
        long observedUnixMilliseconds,
        out Lease? lease)
    {
        lease = null;
        if (material is null)
        {
            return false;
        }

        lock (_gate)
        {
            if (_disposed ||
                !material.IsCurrentAt(observedUnixMilliseconds) ||
                !material.Matches(_profile, _expectedIssuer, _expectedBrokerService) ||
                !_consumedMaterialIds.Add(material.MaterialId))
            {
                return false;
            }

            lease = new Lease(this, material.MaterialId);
            _activeLeases.Add(material.MaterialId, lease);
            return true;
        }
    }

    internal bool Revoke(string? materialId)
    {
        if (string.IsNullOrEmpty(materialId))
        {
            return false;
        }

        Lease? lease;
        lock (_gate)
        {
            if (_disposed || !_activeLeases.Remove(materialId, out lease))
            {
                return false;
            }
        }

        lease.Invalidate();
        return true;
    }

    public void Dispose()
    {
        Lease[] leases;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            leases = _activeLeases.Values.ToArray();
            _activeLeases.Clear();
        }

        foreach (var lease in leases)
        {
            lease.Invalidate();
        }
    }

    private void Release(Lease lease)
    {
        lock (_gate)
        {
            if (_activeLeases.TryGetValue(lease.MaterialId, out var active) &&
                ReferenceEquals(active, lease))
            {
                _activeLeases.Remove(lease.MaterialId);
                lease.Invalidate();
            }
        }
    }

    private bool IsActive(Lease lease)
    {
        lock (_gate)
        {
            return !_disposed &&
                _activeLeases.TryGetValue(lease.MaterialId, out var active) &&
                ReferenceEquals(active, lease);
        }
    }

    internal sealed class Lease : IDisposable
    {
        private HostBootstrapMaterialValidator? _owner;
        private int _usable = 1;

        internal Lease(HostBootstrapMaterialValidator owner, string materialId)
        {
            _owner = owner;
            MaterialId = materialId;
        }

        internal string MaterialId { get; }

        internal bool IsUsable =>
            Volatile.Read(ref _usable) != 0 &&
            _owner is { } owner &&
            owner.IsActive(this);

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.Release(this);
        }

        internal void Invalidate()
        {
            Volatile.Write(ref _usable, 0);
            _ = Interlocked.Exchange(ref _owner, null);
        }
    }
}
