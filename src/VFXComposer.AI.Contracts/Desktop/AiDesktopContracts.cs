using System.Collections.ObjectModel;

namespace VFXComposer.AI.Contracts.Desktop;

/// <summary>Safe presentation state for one explicit channel binding.</summary>
public enum AiDesktopChannelStatusKind
{
    Unbound,
    Unknown,
    Verified,
    Unhealthy,
    Stale,
    Unavailable,
}

/// <summary>A non-secret capability value used only by the deliberate profile editor.</summary>
public sealed class AiDesktopCapabilityDraft
{
    public AiDesktopCapabilityDraft(string id, AiChannel channel, string modelId)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(modelId);
        Id = id;
        Channel = channel;
        ModelId = modelId;
    }

    public string Id { get; }
    public AiChannel Channel { get; }
    public string ModelId { get; }

    public override string ToString() => "AiDesktopCapabilityDraft(" + Channel + ")";
}

/// <summary>
/// Deliberate profile-edit input. <see cref="OpaqueEndpoint"/> is raw only while the caller is actively editing;
/// formatting never emits it. <paramref name="secretEntry"/> is passed separately and is entry-only.
/// </summary>
public sealed class AiDesktopProfileDraft
{
    public AiDesktopProfileDraft(
        string id,
        string displayName,
        ProviderOrigin origin,
        bool enabled,
        string protocolId,
        string opaqueEndpoint,
        int timeoutSeconds,
        IEnumerable<AiDesktopCapabilityDraft> capabilities,
        SecretScope secretScope = SecretScope.Production)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(protocolId);
        ArgumentNullException.ThrowIfNull(opaqueEndpoint);
        ArgumentNullException.ThrowIfNull(capabilities);

        Id = id;
        DisplayName = displayName;
        Origin = origin;
        Enabled = enabled;
        ProtocolId = protocolId;
        OpaqueEndpoint = opaqueEndpoint;
        TimeoutSeconds = timeoutSeconds;
        SecretScope = secretScope;
        Capabilities = Copy(capabilities);
    }

    public string Id { get; }
    public string DisplayName { get; }
    public ProviderOrigin Origin { get; }
    public bool Enabled { get; }
    public string ProtocolId { get; }
    public string OpaqueEndpoint { get; }
    public int TimeoutSeconds { get; }
    public SecretScope SecretScope { get; }
    public IReadOnlyList<AiDesktopCapabilityDraft> Capabilities { get; }

    public override string ToString() => "AiDesktopProfileDraft(<redacted>)";

    private static IReadOnlyList<AiDesktopCapabilityDraft> Copy(IEnumerable<AiDesktopCapabilityDraft> values)
    {
        var copied = values.ToArray();
        if (copied.Any(static value => value is null))
        {
            throw new ArgumentException("Capabilities cannot contain null values.", nameof(values));
        }

        return new ReadOnlyCollection<AiDesktopCapabilityDraft>(copied);
    }
}

/// <summary>Safe profile information for ordinary Settings presentation. It deliberately has no raw endpoint or secret.</summary>
public sealed class AiDesktopProfileSummary
{
    public AiDesktopProfileSummary(
        string id,
        string displayName,
        ProviderOrigin origin,
        bool enabled,
        string protocolId,
        string endpointSummary,
        int timeoutSeconds,
        bool hasSecret,
        IEnumerable<AiDesktopCapabilityDraft> capabilities)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(protocolId);
        ArgumentNullException.ThrowIfNull(endpointSummary);
        ArgumentNullException.ThrowIfNull(capabilities);
        Id = id;
        DisplayName = displayName;
        Origin = origin;
        Enabled = enabled;
        ProtocolId = protocolId;
        EndpointSummary = endpointSummary;
        TimeoutSeconds = timeoutSeconds;
        HasSecret = hasSecret;
        Capabilities = Copy(capabilities);
    }

    public string Id { get; }
    public string DisplayName { get; }
    public ProviderOrigin Origin { get; }
    public bool Enabled { get; }
    public string ProtocolId { get; }
    public string EndpointSummary { get; }
    public int TimeoutSeconds { get; }
    public bool HasSecret { get; }
    public IReadOnlyList<AiDesktopCapabilityDraft> Capabilities { get; }

    public override string ToString() => "AiDesktopProfileSummary(" + Id + ")";

    private static IReadOnlyList<AiDesktopCapabilityDraft> Copy(IEnumerable<AiDesktopCapabilityDraft> values)
    {
        var copied = values.ToArray();
        if (copied.Any(static value => value is null))
        {
            throw new ArgumentException("Capabilities cannot contain null values.", nameof(values));
        }

        return new ReadOnlyCollection<AiDesktopCapabilityDraft>(copied);
    }
}

/// <summary>Raw profile values returned only after an explicit Settings edit action.</summary>
public sealed class AiDesktopProfileEdit
{
    public AiDesktopProfileEdit(AiDesktopProfileDraft profile, bool hasSecret)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        HasSecret = hasSecret;
    }

    public AiDesktopProfileDraft Profile { get; }
    public bool HasSecret { get; }

    public override string ToString() => "AiDesktopProfileEdit(<redacted>)";
}

/// <summary>One exact channel binding; no implicit profile, model, or capability selection exists.</summary>
public sealed class AiDesktopChannelBindingDraft
{
    public AiDesktopChannelBindingDraft(AiChannel channel, string profileId, string capabilityId, string modelId)
    {
        ArgumentNullException.ThrowIfNull(profileId);
        ArgumentNullException.ThrowIfNull(capabilityId);
        ArgumentNullException.ThrowIfNull(modelId);
        Channel = channel;
        ProfileId = profileId;
        CapabilityId = capabilityId;
        ModelId = modelId;
    }

    public AiChannel Channel { get; }
    public string ProfileId { get; }
    public string CapabilityId { get; }
    public string ModelId { get; }

    public override string ToString() => "AiDesktopChannelBindingDraft(" + Channel + ")";
}

/// <summary>Safe read model for a saved binding.</summary>
public sealed class AiDesktopChannelBinding
{
    public AiDesktopChannelBinding(AiDesktopChannelBindingDraft binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        Channel = binding.Channel;
        ProfileId = binding.ProfileId;
        CapabilityId = binding.CapabilityId;
        ModelId = binding.ModelId;
    }

    public AiChannel Channel { get; }
    public string ProfileId { get; }
    public string CapabilityId { get; }
    public string ModelId { get; }

    public override string ToString() => "AiDesktopChannelBinding(" + Channel + ")";
}

/// <summary>Redacted observed state for one binding. Health remains <see cref="AiDesktopChannelStatusKind.Unknown"/> until a user prompt.</summary>
public sealed class AiDesktopChannelStatus
{
    public AiDesktopChannelStatus(AiChannel channel, AiDesktopChannelStatusKind state, AiErrorCode? reasonCode = null)
    {
        if (!Enum.IsDefined(channel) || !Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(channel));
        }

        Channel = channel;
        State = state;
        ReasonCode = reasonCode;
    }

    public AiChannel Channel { get; }
    public AiDesktopChannelStatusKind State { get; }
    public AiErrorCode? ReasonCode { get; }

    public override string ToString() => "AiDesktopChannelStatus(" + Channel + "," + State + ")";
}

/// <summary>Safe snapshot for Settings. Endpoint values and secret material are intentionally absent.</summary>
public sealed class AiDesktopSettingsSnapshot
{
    public AiDesktopSettingsSnapshot(
        long revision,
        IEnumerable<AiDesktopProfileSummary> profiles,
        IEnumerable<AiDesktopChannelBinding> bindings,
        IEnumerable<AiDesktopChannelStatus> channelStatuses)
    {
        if (revision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revision));
        }

        Revision = revision;
        Profiles = Copy(profiles, nameof(profiles));
        Bindings = Copy(bindings, nameof(bindings));
        ChannelStatuses = Copy(channelStatuses, nameof(channelStatuses));
    }

    public long Revision { get; }
    public IReadOnlyList<AiDesktopProfileSummary> Profiles { get; }
    public IReadOnlyList<AiDesktopChannelBinding> Bindings { get; }
    public IReadOnlyList<AiDesktopChannelStatus> ChannelStatuses { get; }

    public override string ToString() => "AiDesktopSettingsSnapshot(revision=" + Revision.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";

    private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        var copied = values.ToArray();
        if (copied.Any(static value => value is null))
        {
            throw new ArgumentException("Snapshot collections cannot contain null values.", parameterName);
        }

        return new ReadOnlyCollection<T>(copied);
    }
}

/// <summary>Current-user provider settings boundary. All operations are local and never authorize or perform network work.</summary>
public interface IAiDesktopSettings
{
    AiDesktopSettingsSnapshot Load();

    AiDesktopProfileEdit BeginProfileEdit(string profileId);

    /// <summary>Empty <paramref name="secretEntry"/> preserves an existing secret; a nonempty value deliberately replaces it.</summary>
    AiDesktopSettingsSnapshot SaveProfile(AiDesktopProfileDraft profile, string? secretEntry);

    /// <summary>Deletes the profile and revokes its profile-owned secret reference before returning.</summary>
    AiDesktopSettingsSnapshot DeleteProfile(string profileId);

    /// <summary>
    /// Detaches and revokes the selected profile's current secret reference. The profile remains configured, but its
    /// route is fail-closed until a nonempty secret is deliberately entered and saved again.
    /// </summary>
    AiDesktopSettingsSnapshot RevokeSecret(string profileId);

    AiDesktopSettingsSnapshot SaveChannelBinding(AiDesktopChannelBindingDraft binding);

    AiDesktopSettingsSnapshot ClearChannelBinding(AiChannel channel);
}

/// <summary>
/// The Desktop's only AI integration boundary. The image stream is provider-issued private artifact content and must
/// be consumed and closed immediately by the Desktop preview decoder.
/// </summary>
public interface IAiDesktopRuntime : IAsyncDisposable
{
    IAiGateway Gateway { get; }

    IAiDesktopSettings Settings { get; }

    ValueTask<Stream> OpenImageArtifactAsync(string privateArtifactId, CancellationToken cancellationToken = default);
}

/// <summary>Safe no-transport runtime used by disconnected shells and component tests.</summary>
public static class AiDesktopRuntime
{
    public static IAiDesktopRuntime Unavailable { get; } = new UnavailableRuntime();

    private sealed class UnavailableRuntime : IAiDesktopRuntime, IAiGateway, IAiDesktopSettings
    {
        private static readonly AiDesktopSettingsSnapshot Empty = new(
            0,
            Array.Empty<AiDesktopProfileSummary>(),
            Array.Empty<AiDesktopChannelBinding>(),
            [
                new AiDesktopChannelStatus(AiChannel.ChatLlm, AiDesktopChannelStatusKind.Unbound),
                new AiDesktopChannelStatus(AiChannel.ImageGeneration, AiDesktopChannelStatusKind.Unbound),
            ]);

        public IAiGateway Gateway => this;
        public IAiDesktopSettings Settings => this;

        public ValueTask<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<ChatResponse>(new AiGatewayException(AiErrorCode.ConfigurationUnavailable));

        public ValueTask<ImageGenerationResponse> GenerateImageAsync(
            ImageGenerationRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<ImageGenerationResponse>(new AiGatewayException(AiErrorCode.ConfigurationUnavailable));

        public ValueTask<Stream> OpenImageArtifactAsync(string privateArtifactId, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<Stream>(new AiGatewayException(AiErrorCode.ConfigurationUnavailable));

        public AiDesktopSettingsSnapshot Load() => Empty;

        public AiDesktopProfileEdit BeginProfileEdit(string profileId) =>
            throw new AiGatewayException(AiErrorCode.ConfigurationUnavailable);

        public AiDesktopSettingsSnapshot SaveProfile(AiDesktopProfileDraft profile, string? secretEntry) =>
            throw new AiGatewayException(AiErrorCode.ConfigurationUnavailable);

        public AiDesktopSettingsSnapshot DeleteProfile(string profileId) =>
            throw new AiGatewayException(AiErrorCode.ConfigurationUnavailable);

        public AiDesktopSettingsSnapshot RevokeSecret(string profileId) =>
            throw new AiGatewayException(AiErrorCode.ConfigurationUnavailable);

        public AiDesktopSettingsSnapshot SaveChannelBinding(AiDesktopChannelBindingDraft binding) =>
            throw new AiGatewayException(AiErrorCode.ConfigurationUnavailable);

        public AiDesktopSettingsSnapshot ClearChannelBinding(AiChannel channel) =>
            throw new AiGatewayException(AiErrorCode.ConfigurationUnavailable);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
