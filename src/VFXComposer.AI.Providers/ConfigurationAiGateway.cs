using VFXComposer.AI.Contracts;

namespace VFXComposer.AI.Providers;

/// <summary>
/// A1 gateway performs fail-closed configuration resolution only. It contains no HTTP implementation and makes
/// no provider request even after a route resolves.
/// </summary>
public sealed class ConfigurationAiGateway : IAiGateway
{
    private readonly ProviderConfigurationStore _store;
    private readonly ProviderConfigurationResolver _resolver;

    public ConfigurationAiGateway(ProviderConfigurationStore store, ProviderConfigurationResolver resolver)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public ValueTask<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        _ = Resolve(AiChannel.ChatLlm);
        return ValueTask.FromException<ChatResponse>(new AiGatewayException(AiErrorCode.AdapterUnavailable));
    }

    public ValueTask<ImageGenerationResponse> GenerateImageAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        _ = Resolve(AiChannel.ImageGeneration);
        return ValueTask.FromException<ImageGenerationResponse>(new AiGatewayException(AiErrorCode.AdapterUnavailable));
    }

    internal ResolvedProviderRoute Resolve(AiChannel channel)
    {
        var loaded = _store.Load();
        return _resolver.Resolve(channel, loaded.Configuration);
    }

    public override string ToString() => "ConfigurationAiGateway(A1-no-transport)";
}
