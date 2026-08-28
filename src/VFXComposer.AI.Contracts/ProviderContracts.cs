namespace VFXComposer.AI.Contracts;

/// <summary>The two product AI channels. No caller-selected fallback exists.</summary>
public enum AiChannel
{
    ChatLlm,
    ImageGeneration,
}

/// <summary>Descriptive provider provenance. It never selects routing behavior.</summary>
public enum ProviderOrigin
{
    Official,
    Relay,
    Friend,
    Subscription,
    Custom,
}

/// <summary>Whether a secret may be used by an explicitly enabled loopback development endpoint.</summary>
public enum SecretScope
{
    Production,
    DevelopmentOnly,
}

public enum ProviderHealthState
{
    Unknown,
    Verified,
    Unhealthy,
    Stale,
}

public enum AiErrorCode
{
    ConfigurationUnavailable,
    ConfigurationInvalid,
    ChannelUnbound,
    ProfileDisabled,
    CapabilityMismatch,
    ProtocolNotAllowed,
    EndpointRejected,
    SecretUnavailable,
    HealthUnverified,
    HealthStale,
    AdapterUnavailable,
    ImportRejected,
    ImportConfirmationRequired,
    RequestInvalid,
}

public static class ProviderProtocols
{
    /// <summary>
    /// The only A1 registry entry. It is descriptive only; it has no adapter or transport implementation.
    /// </summary>
    public const string OpenAiCompatibleV1 = "openai-compatible-v1";
}

public sealed class SecretRef : IEquatable<SecretRef>
{
    public SecretRef(string id) => Id = AiContractGuard.Identifier(id, nameof(id));

    public string Id { get; }

    public bool Equals(SecretRef? other) => other is not null && string.Equals(Id, other.Id, StringComparison.Ordinal);
    public override bool Equals(object? obj) => obj is SecretRef other && Equals(other);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id);
    public override string ToString() => "SecretRef(<redacted>)";
}

public sealed class ProtocolBinding
{
    public ProtocolBinding(string protocolId) => ProtocolId = AiContractGuard.ProtocolId(protocolId, nameof(protocolId));

    public string ProtocolId { get; }

    public override string ToString() => "ProtocolBinding(" + ProtocolId + ")";
}

public sealed class EndpointDefinition
{
    public const int MaximumUriLength = 2048;

    // EndpointPolicy is the sole constructor caller. Keeping this constructor assembly-internal prevents callers
    // from retaining a non-canonical Uri and makes the policy result the endpoint's only representation.
    internal EndpointDefinition(Uri canonicalUri, string canonicalWireUri, bool allowLoopbackHttp)
    {
        Uri = canonicalUri ?? throw new ArgumentNullException(nameof(canonicalUri));
        CanonicalWireUri = canonicalWireUri ?? throw new ArgumentNullException(nameof(canonicalWireUri));
        AllowLoopbackHttp = allowLoopbackHttp;
    }

    public Uri Uri { get; }
    public string CanonicalWireUri { get; }
    public bool AllowLoopbackHttp { get; }

    public override string ToString() => "EndpointDefinition(<redacted>)";
}

public sealed class AuthDescriptor
{
    public AuthDescriptor(SecretRef secretRef, SecretScope secretScope)
    {
        SecretRef = secretRef ?? throw new ArgumentNullException(nameof(secretRef));
        SecretScope = secretScope;
    }

    public SecretRef SecretRef { get; }
    public SecretScope SecretScope { get; }

    public override string ToString() => "AuthDescriptor(<redacted>)";
}

public sealed class CapabilityDefinition
{
    public CapabilityDefinition(string id, AiChannel channel, string modelId)
    {
        Id = AiContractGuard.Identifier(id, nameof(id));
        Channel = channel;
        ModelId = AiContractGuard.ModelId(modelId, nameof(modelId));
    }

    public string Id { get; }
    public AiChannel Channel { get; }
    public string ModelId { get; }

    public override string ToString() => "CapabilityDefinition(" + Id + ")";
}

public sealed class ProviderProfile
{
    public ProviderProfile(
        string id,
        string displayName,
        ProviderOrigin origin,
        bool enabled,
        ProtocolBinding protocol,
        EndpointDefinition endpoint,
        AuthDescriptor auth,
        int timeoutSeconds,
        IEnumerable<CapabilityDefinition> capabilities)
    {
        Id = AiContractGuard.Identifier(id, nameof(id));
        DisplayName = AiContractGuard.DisplayName(displayName, nameof(displayName));
        Origin = origin;
        Enabled = enabled;
        Protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        var checkedEndpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        var checkedAuth = auth ?? throw new ArgumentNullException(nameof(auth));
        if (!EndpointPolicy.IsValid(checkedEndpoint, checkedAuth.SecretScope))
        {
            throw new ArgumentException("Endpoint does not match the declared secret scope.", nameof(endpoint));
        }

        Endpoint = checkedEndpoint;
        Auth = checkedAuth;
        if (timeoutSeconds is < 1 or > 300)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
        }

        TimeoutSeconds = timeoutSeconds;
        Capabilities = AiContractGuard.CopyList(capabilities, nameof(capabilities), maximumCount: 32);
        if (Capabilities.Count == 0 || Capabilities.Select(static capability => capability.Id).Distinct(StringComparer.Ordinal).Count() != Capabilities.Count)
        {
            throw new ArgumentException("Capabilities must be non-empty and unique.", nameof(capabilities));
        }
    }

    public string Id { get; }
    public string DisplayName { get; }
    public ProviderOrigin Origin { get; }
    public bool Enabled { get; }
    public ProtocolBinding Protocol { get; }
    public EndpointDefinition Endpoint { get; }
    public AuthDescriptor Auth { get; }
    public int TimeoutSeconds { get; }
    public IReadOnlyList<CapabilityDefinition> Capabilities { get; }

    public override string ToString() => "ProviderProfile(" + Id + ")";
}

public sealed class ChannelBinding
{
    public ChannelBinding(AiChannel channel, string profileId, string capabilityId, string modelId)
    {
        Channel = channel;
        ProfileId = AiContractGuard.Identifier(profileId, nameof(profileId));
        CapabilityId = AiContractGuard.Identifier(capabilityId, nameof(capabilityId));
        ModelId = AiContractGuard.ModelId(modelId, nameof(modelId));
    }

    public AiChannel Channel { get; }
    public string ProfileId { get; }
    public string CapabilityId { get; }
    public string ModelId { get; }

    public override string ToString() => "ChannelBinding(" + Channel + ")";
}

/// <summary>Immutable non-secret configuration. It contains only SecretRef values, never credential payloads.</summary>
public sealed class AiProviderSettings
{
    public AiProviderSettings(long revision, IEnumerable<ProviderProfile> profiles, IEnumerable<ChannelBinding> channelBindings)
    {
        if (revision < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(revision));
        }

        Revision = revision;
        Profiles = AiContractGuard.CopyList(profiles, nameof(profiles), maximumCount: 32);
        ChannelBindings = AiContractGuard.CopyList(channelBindings, nameof(channelBindings), maximumCount: 2);
        if (Profiles.Select(static profile => profile.Id).Distinct(StringComparer.Ordinal).Count() != Profiles.Count)
        {
            throw new ArgumentException("Profile identifiers must be unique.", nameof(profiles));
        }

        if (ChannelBindings.Select(static binding => binding.Channel).Distinct().Count() != ChannelBindings.Count)
        {
            throw new ArgumentException("Each channel can have at most one binding.", nameof(channelBindings));
        }
    }

    public int FormatVersion => AiContractVersions.ProviderConfigurationFormatVersion;
    public long Revision { get; }
    public IReadOnlyList<ProviderProfile> Profiles { get; }
    public IReadOnlyList<ChannelBinding> ChannelBindings { get; }

    public override string ToString() => "AiProviderSettings(revision=" + Revision.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
}

public sealed class ConfigurationFingerprint : IEquatable<ConfigurationFingerprint>
{
    public ConfigurationFingerprint(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));
        if (value.Length != 71 || !value.StartsWith("sha256:", StringComparison.Ordinal))
        {
            throw new ArgumentException("Configuration fingerprint is invalid.", nameof(value));
        }

        for (var index = 7; index < value.Length; index++)
        {
            if (value[index] is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            {
                throw new ArgumentException("Configuration fingerprint is invalid.", nameof(value));
            }
        }

        Value = value;
    }

    public string Value { get; }

    public bool Equals(ConfigurationFingerprint? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object? obj) => obj is ConfigurationFingerprint other && Equals(other);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
    public override string ToString() => "ConfigurationFingerprint(" + Value + ")";
}

public sealed class ProviderHealth
{
    public ProviderHealth(
        string profileId,
        string capabilityId,
        AiChannel channel,
        ConfigurationFingerprint configurationFingerprint,
        ProviderHealthState state,
        DateTimeOffset checkedUtc,
        AiErrorCode? reasonCode = null)
    {
        ProfileId = AiContractGuard.Identifier(profileId, nameof(profileId));
        CapabilityId = AiContractGuard.Identifier(capabilityId, nameof(capabilityId));
        Channel = channel;
        ConfigurationFingerprint = configurationFingerprint ?? throw new ArgumentNullException(nameof(configurationFingerprint));
        State = state;
        CheckedUtc = AiContractGuard.Utc(checkedUtc, nameof(checkedUtc));
        ReasonCode = reasonCode;
    }

    public string ProfileId { get; }
    public string CapabilityId { get; }
    public AiChannel Channel { get; }
    public ConfigurationFingerprint ConfigurationFingerprint { get; }
    public ProviderHealthState State { get; }
    public DateTimeOffset CheckedUtc { get; }
    public AiErrorCode? ReasonCode { get; }

    public override string ToString() => "ProviderHealth(" + Channel + "," + State + ")";
}

/// <summary>A resolved, exact route. It contains a SecretRef only and never plaintext credentials.</summary>
public sealed class ResolvedProviderRoute
{
    public ResolvedProviderRoute(
        AiChannel channel,
        ProviderProfile profile,
        CapabilityDefinition capability,
        ChannelBinding binding,
        ConfigurationFingerprint configurationFingerprint)
    {
        Channel = channel;
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Capability = capability ?? throw new ArgumentNullException(nameof(capability));
        Binding = binding ?? throw new ArgumentNullException(nameof(binding));
        ConfigurationFingerprint = configurationFingerprint ?? throw new ArgumentNullException(nameof(configurationFingerprint));
    }

    public AiChannel Channel { get; }
    public ProviderProfile Profile { get; }
    public CapabilityDefinition Capability { get; }
    public ChannelBinding Binding { get; }
    public ConfigurationFingerprint ConfigurationFingerprint { get; }

    public override string ToString() => "ResolvedProviderRoute(" + Channel + "," + Profile.Id + "," + Capability.Id + ")";
}
