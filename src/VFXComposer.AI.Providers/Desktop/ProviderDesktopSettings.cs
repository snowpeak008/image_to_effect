using System.Security.Cryptography;
using System.Text;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Desktop;

namespace VFXComposer.AI.Providers.Desktop;

/// <summary>
/// Current-user settings facade for the Desktop. It owns profile CRUD, exact opaque endpoint persistence, explicit
/// channel bindings, and entry-only DPAPI secret replacement. It performs no provider transport or health probe.
/// </summary>
internal sealed class ProviderDesktopSettings : IAiDesktopSettings
{
    private readonly ProviderConfigurationStore _store;
    private readonly ProviderSecretStore _secrets;
    private readonly ProviderHealthRegistry _health;
    private readonly Action _configurationChanged;

    public ProviderDesktopSettings(
        ProviderConfigurationStore store,
        ProviderSecretStore secrets,
        ProviderHealthRegistry health,
        Action configurationChanged)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        _health = health ?? throw new ArgumentNullException(nameof(health));
        _configurationChanged = configurationChanged ?? throw new ArgumentNullException(nameof(configurationChanged));
    }

    public AiDesktopSettingsSnapshot Load()
    {
        var loaded = TryLoad();
        return loaded is null ? EmptySnapshot() : Snapshot(loaded.Configuration);
    }

    public AiDesktopProfileEdit BeginProfileEdit(string profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        var configuration = RequireConfiguration();
        var profile = configuration.Settings.Profiles.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, profileId, StringComparison.Ordinal));
        if (profile is null)
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }

        return new AiDesktopProfileEdit(
            new AiDesktopProfileDraft(
                profile.Id,
                profile.DisplayName,
                profile.Origin,
                profile.Enabled,
                profile.Protocol.ProtocolId,
                profile.Endpoint.Value,
                profile.TimeoutSeconds,
                profile.Capabilities.Select(static capability => new AiDesktopCapabilityDraft(
                    capability.Id,
                    capability.Channel,
                    capability.ModelId)),
                profile.Auth.SecretScope),
            _secrets.IsReadable(profile.Id, profile.Auth.SecretRef));
    }

    public AiDesktopSettingsSnapshot SaveProfile(AiDesktopProfileDraft profile, string? secretEntry)
    {
        ArgumentNullException.ThrowIfNull(profile);
        try
        {
            var existing = TryLoad();
            var existingProfile = existing?.Configuration.Settings.Profiles.SingleOrDefault(candidate =>
                string.Equals(candidate.Id, profile.Id, StringComparison.Ordinal));
            var secretRef = existingProfile?.Auth.SecretRef ?? CreateSecretRef(profile.Id);
            var replacement = CreateProfile(profile, secretRef);
            var previousProfiles = existing?.Configuration.Settings.Profiles ?? Array.Empty<ProviderProfile>();
            var profiles = previousProfiles
                .Where(candidate => !string.Equals(candidate.Id, replacement.Id, StringComparison.Ordinal))
                .Append(replacement)
                .ToArray();
            var bindings = existing?.Configuration.Settings.ChannelBindings ?? Array.Empty<ChannelBinding>();
            var settings = new AiProviderSettings(NextRevision(existing), profiles, bindings);
            _store.Save(settings);
            _configurationChanged();

            // Empty means preserve: neither reads nor recreates any prior plaintext. Any nonempty value deliberately
            // replaces the profile-owned DPAPI envelope after the updated reference has been persisted.
            if (!string.IsNullOrEmpty(secretEntry))
            {
                _secrets.SaveSecret(replacement.Id, secretRef, secretEntry.AsSpan());
            }

            return Snapshot(_store.Load().Configuration);
        }
        catch (AiGatewayException)
        {
            throw;
        }
        catch (ArgumentException)
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }
        catch (OverflowException)
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }
    }

    public AiDesktopSettingsSnapshot DeleteProfile(string profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        var configuration = RequireConfiguration();
        var profile = configuration.Settings.Profiles.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, profileId, StringComparison.Ordinal));
        if (profile is null)
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }

        // Revoke before removing the route. A storage failure leaves the old configuration present but fail-closed,
        // never a retained usable credential for a supposedly deleted profile.
        _secrets.RevokeSecret(profile.Id, profile.Auth.SecretRef);
        var settings = new AiProviderSettings(
            NextRevision(configuration),
            configuration.Settings.Profiles.Where(candidate =>
                !string.Equals(candidate.Id, profile.Id, StringComparison.Ordinal)),
            configuration.Settings.ChannelBindings.Where(candidate =>
                !string.Equals(candidate.ProfileId, profile.Id, StringComparison.Ordinal)));
        _store.Save(settings);
        _configurationChanged();
        return Snapshot(_store.Load().Configuration);
    }

    public AiDesktopSettingsSnapshot SaveChannelBinding(AiDesktopChannelBindingDraft binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (!Enum.IsDefined(binding.Channel))
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }

        try
        {
            var configuration = RequireConfiguration();
            var replacement = new ChannelBinding(
                binding.Channel,
                binding.ProfileId,
                binding.CapabilityId,
                binding.ModelId);
            var settings = new AiProviderSettings(
                NextRevision(configuration),
                configuration.Settings.Profiles,
                configuration.Settings.ChannelBindings
                    .Where(candidate => candidate.Channel != binding.Channel)
                    .Append(replacement));
            _store.Save(settings);
            _configurationChanged();
            return Snapshot(_store.Load().Configuration);
        }
        catch (AiGatewayException)
        {
            throw;
        }
        catch (ArgumentException)
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }
    }

    public AiDesktopSettingsSnapshot ClearChannelBinding(AiChannel channel)
    {
        if (!Enum.IsDefined(channel))
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }

        var configuration = RequireConfiguration();
        var settings = new AiProviderSettings(
            NextRevision(configuration),
            configuration.Settings.Profiles,
            configuration.Settings.ChannelBindings.Where(candidate => candidate.Channel != channel));
        _store.Save(settings);
        _configurationChanged();
        return Snapshot(_store.Load().Configuration);
    }

    private ProviderConfigurationStoreReadResult? TryLoad()
    {
        try
        {
            return _store.Load();
        }
        catch (AiGatewayException exception) when (
            exception.Code == AiErrorCode.ConfigurationUnavailable &&
            !_store.Exists())
        {
            return null;
        }
    }

    private ProviderConfigurationReadResult RequireConfiguration() =>
        TryLoad()?.Configuration ?? throw new AiGatewayException(AiErrorCode.ConfigurationUnavailable);

    private AiDesktopSettingsSnapshot Snapshot(ProviderConfigurationReadResult configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var profiles = configuration.Settings.Profiles.Select(profile => new AiDesktopProfileSummary(
            profile.Id,
            profile.DisplayName,
            profile.Origin,
            profile.Enabled,
            profile.Protocol.ProtocolId,
            ProviderRedaction.RedactEndpoint(profile.Endpoint),
            profile.TimeoutSeconds,
            _secrets.IsReadable(profile.Id, profile.Auth.SecretRef),
            profile.Capabilities.Select(static capability => new AiDesktopCapabilityDraft(
                capability.Id,
                capability.Channel,
                capability.ModelId))));
        var bindings = configuration.Settings.ChannelBindings.Select(static binding =>
            new AiDesktopChannelBinding(new AiDesktopChannelBindingDraft(
                binding.Channel,
                binding.ProfileId,
                binding.CapabilityId,
                binding.ModelId)));
        return new AiDesktopSettingsSnapshot(
            configuration.Settings.Revision,
            profiles,
            bindings,
            [
                StatusFor(AiChannel.ChatLlm, configuration),
                StatusFor(AiChannel.ImageGeneration, configuration),
            ]);
    }

    private AiDesktopSettingsSnapshot EmptySnapshot() => new(
        0,
        Array.Empty<AiDesktopProfileSummary>(),
        Array.Empty<AiDesktopChannelBinding>(),
        [
            new AiDesktopChannelStatus(AiChannel.ChatLlm, AiDesktopChannelStatusKind.Unbound),
            new AiDesktopChannelStatus(AiChannel.ImageGeneration, AiDesktopChannelStatusKind.Unbound),
        ]);

    private AiDesktopChannelStatus StatusFor(AiChannel channel, ProviderConfigurationReadResult configuration)
    {
        var binding = configuration.Settings.ChannelBindings.SingleOrDefault(candidate => candidate.Channel == channel);
        if (binding is null)
        {
            return new AiDesktopChannelStatus(channel, AiDesktopChannelStatusKind.Unbound);
        }

        var profile = configuration.Settings.Profiles.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, binding.ProfileId, StringComparison.Ordinal));
        var capability = profile?.Capabilities.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, binding.CapabilityId, StringComparison.Ordinal));
        if (profile is null || capability is null || capability.Channel != channel ||
            !string.Equals(capability.ModelId, binding.ModelId, StringComparison.Ordinal))
        {
            return new AiDesktopChannelStatus(channel, AiDesktopChannelStatusKind.Unavailable, AiErrorCode.ConfigurationInvalid);
        }

        var health = _health.Get(profile.Id, capability.Id, channel);
        if (health is null || health.State == ProviderHealthState.Unknown)
        {
            return new AiDesktopChannelStatus(channel, AiDesktopChannelStatusKind.Unknown, health?.ReasonCode);
        }

        if (!health.ConfigurationFingerprint.Equals(configuration.Fingerprint) || health.State == ProviderHealthState.Stale)
        {
            return new AiDesktopChannelStatus(channel, AiDesktopChannelStatusKind.Stale, health.ReasonCode);
        }

        return health.State switch
        {
            ProviderHealthState.Verified => new AiDesktopChannelStatus(channel, AiDesktopChannelStatusKind.Verified),
            ProviderHealthState.Unhealthy => new AiDesktopChannelStatus(channel, AiDesktopChannelStatusKind.Unhealthy, health.ReasonCode),
            _ => new AiDesktopChannelStatus(channel, AiDesktopChannelStatusKind.Unavailable, health.ReasonCode),
        };
    }

    private static ProviderProfile CreateProfile(AiDesktopProfileDraft draft, SecretRef secretRef) => new(
        draft.Id,
        draft.DisplayName,
        draft.Origin,
        draft.Enabled,
        new ProtocolBinding(draft.ProtocolId),
        new OpaqueEndpoint(draft.OpaqueEndpoint),
        new AuthDescriptor(secretRef, draft.SecretScope),
        draft.TimeoutSeconds,
        draft.Capabilities.Select(static capability => new CapabilityDefinition(
            capability.Id,
            capability.Channel,
            capability.ModelId)));

    private static SecretRef CreateSecretRef(string profileId)
    {
        ArgumentNullException.ThrowIfNull(profileId);
        var source = Encoding.UTF8.GetBytes(profileId);
        try
        {
            var digest = SHA256.HashData(source);
            try
            {
                return new SecretRef("sec-" + Convert.ToHexString(digest).ToLowerInvariant()[..60]);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(digest);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(source);
        }
    }

    private static long NextRevision(ProviderConfigurationStoreReadResult? current) =>
        current is null ? 1 : checked(current.Configuration.Settings.Revision + 1);

    private static long NextRevision(ProviderConfigurationReadResult current)
    {
        ArgumentNullException.ThrowIfNull(current);
        return checked(current.Settings.Revision + 1);
    }
}
