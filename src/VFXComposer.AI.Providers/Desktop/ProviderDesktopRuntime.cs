using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Desktop;
using VFXComposer.AI.Providers.Chat;
using VFXComposer.AI.Providers.Image;

namespace VFXComposer.AI.Providers.Desktop;

/// <summary>
/// Composes settings, the one explicit ChatLlm route, the one explicit ImageGeneration route, and private artifact
/// ownership. Network-capable adapters are created lazily only inside a deliberate user request.
/// </summary>
public sealed class ProviderDesktopRuntime : IAiDesktopRuntime
{
    private readonly DesktopAiGateway _gateway;

    public ProviderDesktopRuntime(
        ProviderConfigurationStore configurationStore,
        ProviderSecretStore secretStore,
        ProviderHealthRegistry healthRegistry)
    {
        ArgumentNullException.ThrowIfNull(configurationStore);
        ArgumentNullException.ThrowIfNull(secretStore);
        ArgumentNullException.ThrowIfNull(healthRegistry);
        _gateway = new DesktopAiGateway(configurationStore, secretStore, healthRegistry);
        Settings = new ProviderDesktopSettings(
            configurationStore,
            secretStore,
            healthRegistry,
            _gateway.InvalidateConfiguration);
    }

    public IAiGateway Gateway => _gateway;

    public IAiDesktopSettings Settings { get; }

    public ValueTask<Stream> OpenImageArtifactAsync(string privateArtifactId, CancellationToken cancellationToken = default) =>
        _gateway.OpenImageArtifactAsync(privateArtifactId, cancellationToken);

    public ValueTask DisposeAsync()
    {
        _gateway.Dispose();
        return ValueTask.CompletedTask;
    }
}

internal sealed class DesktopAiGateway : IAiGateway, IDisposable
{
    private readonly object _gate = new();
    private readonly ProviderConfigurationStore _configurationStore;
    private readonly ProviderSecretStore _secretStore;
    private readonly ProviderHealthRegistry _health;
    private readonly ProviderConfigurationResolver _resolver;
    private ChatChannelGateway? _chat;
    private OpenAiCompatibleImageGateway? _image;
    private ConfigurationFingerprint? _imageFingerprint;
    private int _disposed;

    public DesktopAiGateway(
        ProviderConfigurationStore configurationStore,
        ProviderSecretStore secretStore,
        ProviderHealthRegistry health)
    {
        _configurationStore = configurationStore ?? throw new ArgumentNullException(nameof(configurationStore));
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _health = health ?? throw new ArgumentNullException(nameof(health));
        _resolver = new ProviderConfigurationResolver(AllowlistedProviderRegistry.Default, _health, _secretStore);
    }

    public async ValueTask<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfDisposed();
        ChatChannelGateway gateway;
        lock (_gate)
        {
            ThrowIfDisposed();
            // This is deliberately the first HTTP-client construction point for Chat, and is reached only by a
            // submitted user prompt. Settings/save/start/navigation do not call this method.
            gateway = _chat ??= ChatChannelGateway.Create(_configurationStore, _health, _secretStore);
        }

        return await gateway.ChatAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<ImageGenerationResponse> GenerateImageAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfDisposed();

        // Resolve the one saved ImageGeneration binding only for this explicit generation action. Unknown health is
        // allowed to make this first request; neither route resolution nor Settings invokes a health probe.
        var route = _resolver.Resolve(
            AiChannel.ImageGeneration,
            _configurationStore.Load().Configuration,
            allowUnknownHealth: true);
        OpenAiCompatibleImageGateway gateway;
        OpenAiCompatibleImageGateway? retired = null;
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_image is null || _imageFingerprint is null || !_imageFingerprint.Equals(route.ConfigurationFingerprint))
            {
                retired = _image;
                _image = OpenAiCompatibleImageGateway.Create(route, _secretStore);
                _imageFingerprint = route.ConfigurationFingerprint;
            }

            gateway = _image;
        }

        retired?.Dispose();
        try
        {
            var response = await gateway.GenerateImageAsync(request, cancellationToken).ConfigureAwait(false);
            RecordImageHealth(route, ProviderHealthState.Verified, reasonCode: null);
            return response;
        }
        catch (ImageGatewayException exception) when (exception.Code != ImageErrorCode.Cancelled)
        {
            RecordImageHealth(route, ProviderHealthState.Unhealthy, AiErrorCode.AdapterUnavailable);
            throw;
        }
    }

    public ValueTask<Stream> OpenImageArtifactAsync(string privateArtifactId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateArtifactId);
        OpenAiCompatibleImageGateway gateway;
        lock (_gate)
        {
            ThrowIfDisposed();
            gateway = _image ?? throw new AiGatewayException(AiErrorCode.AdapterUnavailable);
        }

        return gateway.OpenReadAsync(privateArtifactId, cancellationToken);
    }

    public void InvalidateConfiguration()
    {
        OpenAiCompatibleImageGateway? retiredImage;
        ChatChannelGateway? retiredChat;
        lock (_gate)
        {
            retiredImage = _image;
            _image = null;
            _imageFingerprint = null;
            retiredChat = _chat;
            _chat = null;
        }

        retiredImage?.Dispose();
        retiredChat?.Dispose();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            InvalidateConfiguration();
        }
    }

    public override string ToString() => "DesktopAiGateway(<redacted>)";

    private void RecordImageHealth(
        ResolvedProviderRoute route,
        ProviderHealthState state,
        AiErrorCode? reasonCode)
    {
        _health.Record(new ProviderHealth(
            route.Profile.Id,
            route.Capability.Id,
            AiChannel.ImageGeneration,
            route.ConfigurationFingerprint,
            state,
            DateTimeOffset.UtcNow,
            reasonCode));
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(DesktopAiGateway));
        }
    }
}
