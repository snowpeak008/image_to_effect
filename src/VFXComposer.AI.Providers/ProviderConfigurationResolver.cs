using VFXComposer.AI.Contracts;

namespace VFXComposer.AI.Providers;

/// <summary>Resolves only the one explicit binding for the requested channel; it never searches a fallback route.</summary>
public sealed class ProviderConfigurationResolver
{
    private readonly AllowlistedProviderRegistry _registry;
    private readonly ProviderHealthRegistry _health;
    private readonly ISecretReferenceVerifier _secrets;

    public ProviderConfigurationResolver(
        AllowlistedProviderRegistry registry,
        ProviderHealthRegistry health,
        ISecretReferenceVerifier secrets)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _health = health ?? throw new ArgumentNullException(nameof(health));
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
    }

    /// <summary>
    /// Resolves a channel without interpreting its endpoint. Ordinary callers remain fail-closed on unobserved
    /// health; an explicit user prompt may opt into the one bounded <paramref name="allowUnknownHealth"/> admission
    /// path. This method never probes, refreshes, or otherwise changes health itself.
    /// </summary>
    public ResolvedProviderRoute Resolve(
        AiChannel channel,
        ProviderConfigurationReadResult configuration,
        bool allowUnknownHealth = false)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var settings = configuration.Settings;
        ProviderConfigurationValidator.Validate(settings);

        var binding = settings.ChannelBindings.SingleOrDefault(candidate => candidate.Channel == channel);
        if (binding is null)
        {
            throw new AiGatewayException(AiErrorCode.ChannelUnbound);
        }

        var profile = settings.Profiles.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, binding.ProfileId, StringComparison.Ordinal));
        if (profile is null)
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }

        if (!profile.Enabled)
        {
            throw new AiGatewayException(AiErrorCode.ProfileDisabled);
        }

        // Deliberately select by the binding's exact capability ID; do not search another capability or profile.
        var capability = profile.Capabilities.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, binding.CapabilityId, StringComparison.Ordinal));
        if (capability is null ||
            capability.Channel != channel ||
            !string.Equals(capability.ModelId, binding.ModelId, StringComparison.Ordinal))
        {
            throw new AiGatewayException(AiErrorCode.CapabilityMismatch);
        }

        if (!_registry.IsAllowed(profile.Protocol))
        {
            throw new AiGatewayException(AiErrorCode.ProtocolNotAllowed);
        }

        if (!_secrets.IsReadable(profile.Id, profile.Auth.SecretRef))
        {
            throw new AiGatewayException(AiErrorCode.SecretUnavailable);
        }

        var health = _health.Get(profile.Id, capability.Id, channel);
        if (health is null)
        {
            if (!allowUnknownHealth)
            {
                throw new AiGatewayException(AiErrorCode.HealthUnverified);
            }

            return new ResolvedProviderRoute(channel, profile, capability, binding, configuration.Fingerprint);
        }

        if (!health.ConfigurationFingerprint.Equals(configuration.Fingerprint))
        {
            throw new AiGatewayException(AiErrorCode.HealthStale);
        }

        if (health.State == ProviderHealthState.Stale)
        {
            throw new AiGatewayException(AiErrorCode.HealthStale);
        }

        if (health.State == ProviderHealthState.Unknown && allowUnknownHealth)
        {
            return new ResolvedProviderRoute(channel, profile, capability, binding, configuration.Fingerprint);
        }

        if (health.State != ProviderHealthState.Verified)
        {
            throw new AiGatewayException(AiErrorCode.HealthUnverified);
        }

        return new ResolvedProviderRoute(channel, profile, capability, binding, configuration.Fingerprint);
    }
}
