using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Chat;

namespace VFXComposer.AI.Providers.Chat;

/// <summary>
/// Resolves one immutable ChatLlm route snapshot.  It deliberately does not search another profile, capability,
/// model, protocol, or channel when the selected binding cannot be used.
/// </summary>
internal sealed class ChatRouteResolver
{
    private readonly ProviderHealthRegistry _health;
    private readonly ProviderSecretStore _secrets;
    private readonly ProviderConfigurationResolver _a1Resolver;

    public ChatRouteResolver(ProviderHealthRegistry health, ProviderSecretStore secrets)
    {
        _health = health ?? throw new ArgumentNullException(nameof(health));
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        _a1Resolver = new ProviderConfigurationResolver(AllowlistedProviderRegistry.Default, _health, _secrets);
    }

    public ChatResolvedRoute Resolve(ProviderConfigurationReadResult configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            ProviderConfigurationValidator.Validate(configuration.Settings);
            var settings = configuration.Settings;
            var binding = settings.ChannelBindings.SingleOrDefault(static candidate => candidate.Channel == AiChannel.ChatLlm);
            if (binding is null)
            {
                throw new ChatChannelException(ChatChannelErrorCode.ChannelUnbound);
            }

            var profile = settings.Profiles.SingleOrDefault(candidate =>
                string.Equals(candidate.Id, binding.ProfileId, StringComparison.Ordinal));
            if (profile is null)
            {
                throw new ChatChannelException(ChatChannelErrorCode.ConfigurationInvalid);
            }

            if (!profile.Enabled)
            {
                throw new ChatChannelException(ChatChannelErrorCode.ProfileDisabled);
            }

            var capability = profile.Capabilities.SingleOrDefault(candidate =>
                string.Equals(candidate.Id, binding.CapabilityId, StringComparison.Ordinal));
            if (capability is null ||
                capability.Channel != AiChannel.ChatLlm ||
                !string.Equals(capability.ModelId, binding.ModelId, StringComparison.Ordinal))
            {
                throw new ChatChannelException(ChatChannelErrorCode.CapabilityMismatch);
            }

            if (!ChatProtocolCatalog.TryGet(profile.Protocol.ProtocolId, out var protocol))
            {
                throw new ChatChannelException(ChatChannelErrorCode.ProtocolUnsupported);
            }

            // Preserve the A1 resolver's exact behavior for its existing OpenAI-compatible identifier.  The four
            // additional A2 protocol identifiers are intentionally resolved with the same explicit-binding checks
            // below because A1's closed descriptive registry cannot be widened from this isolated work package.
            if (protocol == ChatWireProtocol.OpenAiCompatible)
            {
                var a1Route = _a1Resolver.Resolve(AiChannel.ChatLlm, configuration);
                return new ChatResolvedRoute(protocol, a1Route.Profile, a1Route.Capability, a1Route.Binding);
            }

            if (!_secrets.IsReadable(profile.Id, profile.Auth.SecretRef))
            {
                throw new ChatChannelException(ChatChannelErrorCode.SecretUnavailable);
            }

            var health = _health.Get(profile.Id, capability.Id, AiChannel.ChatLlm);
            if (health is null)
            {
                throw new ChatChannelException(ChatChannelErrorCode.HealthUnverified);
            }

            if (!health.ConfigurationFingerprint.Equals(configuration.Fingerprint) || health.State == ProviderHealthState.Stale)
            {
                throw new ChatChannelException(ChatChannelErrorCode.HealthStale);
            }

            if (health.State != ProviderHealthState.Verified)
            {
                throw new ChatChannelException(ChatChannelErrorCode.HealthUnverified);
            }

            return new ChatResolvedRoute(protocol, profile, capability, binding);
        }
        catch (ChatChannelException)
        {
            throw;
        }
        catch (AiGatewayException exception)
        {
            throw ChatErrorMapper.FromA1(exception);
        }
    }
}

internal sealed class ChatResolvedRoute
{
    public ChatResolvedRoute(
        ChatWireProtocol protocol,
        ProviderProfile profile,
        CapabilityDefinition capability,
        ChannelBinding binding)
    {
        Protocol = protocol;
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Capability = capability ?? throw new ArgumentNullException(nameof(capability));
        Binding = binding ?? throw new ArgumentNullException(nameof(binding));
    }

    public ChatWireProtocol Protocol { get; }
    public ProviderProfile Profile { get; }
    public CapabilityDefinition Capability { get; }
    public ChannelBinding Binding { get; }
}

internal enum ChatWireProtocol
{
    OpenAiChatCompletions,
    OpenAiResponses,
    AnthropicMessages,
    GeminiGenerateContent,
    OpenAiCompatible,
}

internal static class ChatProtocolCatalog
{
    public static bool TryGet(string protocolId, out ChatWireProtocol protocol)
    {
        protocol = protocolId switch
        {
            ChatProtocolIds.OpenAiChatCompletionsV1 => ChatWireProtocol.OpenAiChatCompletions,
            ChatProtocolIds.OpenAiResponsesV1 => ChatWireProtocol.OpenAiResponses,
            ChatProtocolIds.AnthropicMessagesV1 => ChatWireProtocol.AnthropicMessages,
            ChatProtocolIds.GeminiGenerateContentV1 => ChatWireProtocol.GeminiGenerateContent,
            ChatProtocolIds.OpenAiCompatibleV1 => ChatWireProtocol.OpenAiCompatible,
            _ => default,
        };

        return protocolId is ChatProtocolIds.OpenAiChatCompletionsV1 or
            ChatProtocolIds.OpenAiResponsesV1 or
            ChatProtocolIds.AnthropicMessagesV1 or
            ChatProtocolIds.GeminiGenerateContentV1 or
            ChatProtocolIds.OpenAiCompatibleV1;
    }
}

internal static class ChatErrorMapper
{
    public static ChatChannelException FromA1(AiGatewayException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new ChatChannelException(
            exception.Code switch
            {
                AiErrorCode.ConfigurationUnavailable => ChatChannelErrorCode.ConfigurationUnavailable,
                AiErrorCode.ConfigurationInvalid => ChatChannelErrorCode.ConfigurationInvalid,
                AiErrorCode.ChannelUnbound => ChatChannelErrorCode.ChannelUnbound,
                AiErrorCode.ProfileDisabled => ChatChannelErrorCode.ProfileDisabled,
                AiErrorCode.CapabilityMismatch => ChatChannelErrorCode.CapabilityMismatch,
                AiErrorCode.ProtocolNotAllowed => ChatChannelErrorCode.ProtocolUnsupported,
                AiErrorCode.SecretUnavailable => ChatChannelErrorCode.SecretUnavailable,
                AiErrorCode.HealthUnverified => ChatChannelErrorCode.HealthUnverified,
                AiErrorCode.HealthStale => ChatChannelErrorCode.HealthStale,
                AiErrorCode.RequestInvalid => ChatChannelErrorCode.RequestInvalid,
                _ => ChatChannelErrorCode.ConfigurationInvalid,
            },
            exception.Retryable);
    }
}
