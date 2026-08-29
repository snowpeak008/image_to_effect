using System.Net;
using System.Text;
using System.Text.Json;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Chat;
using VFXComposer.AI.Providers;
using VFXComposer.AI.Providers.Chat;

namespace VFXComposer.AI.Tests.Chat;

internal sealed class ChatTestFixture : IDisposable
{
    private readonly A1TestDirectory _directory = new();

    public ChatTestFixture(
        string protocolId = ChatProtocolIds.OpenAiChatCompletionsV1,
        string endpoint = "https://synthetic.invalid/a2/chat",
        string model = "chat-model-primary",
        int timeoutSeconds = 30,
        string secret = "synthetic-a2-secret")
    {
        Store = new ProviderConfigurationStore(Path.Combine(_directory.Path, "providers.json"));
        Secrets = new ProviderSecretStore(Path.Combine(_directory.Path, "secrets"));
        Health = new ProviderHealthRegistry();
        SaveConfiguration(1, protocolId, endpoint, model, timeoutSeconds, secret);
    }

    public ProviderConfigurationStore Store { get; }
    public ProviderSecretStore Secrets { get; }
    public ProviderHealthRegistry Health { get; }

    public ChatChannelGateway CreateGateway(HttpMessageHandler handler) => new(Store, Health, Secrets, handler);

    public ProviderConfigurationReadResult SaveConfiguration(
        long revision,
        string protocolId,
        string endpoint,
        string model,
        int timeoutSeconds = 30,
        string secret = "synthetic-a2-secret")
    {
        var settings = SingleSettings(revision, protocolId, endpoint, model, timeoutSeconds);
        SaveSettings(settings, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["profile-primary"] = secret,
        });
        return Store.Load().Configuration;
    }

    public void SaveSettings(AiProviderSettings settings, IReadOnlyDictionary<string, string> secretsByProfile)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(secretsByProfile);
        Store.Save(settings);
        foreach (var profile in settings.Profiles)
        {
            if (!secretsByProfile.TryGetValue(profile.Id, out var secret))
            {
                throw new ArgumentException("A test secret is required for every profile.", nameof(secretsByProfile));
            }

            Secrets.SaveSecret(profile.Id, profile.Auth.SecretRef, secret.AsSpan());
        }

        var loaded = Store.Load().Configuration;
        foreach (var binding in loaded.Settings.ChannelBindings.Where(static binding => binding.Channel == AiChannel.ChatLlm))
        {
            var profile = loaded.Settings.Profiles.Single(profile => string.Equals(profile.Id, binding.ProfileId, StringComparison.Ordinal));
            var capability = profile.Capabilities.Single(capability => string.Equals(capability.Id, binding.CapabilityId, StringComparison.Ordinal));
            Health.Record(new ProviderHealth(
                profile.Id,
                capability.Id,
                AiChannel.ChatLlm,
                loaded.Fingerprint,
                ProviderHealthState.Verified,
                DateTimeOffset.UtcNow));
        }
    }

    public static AiProviderSettings SingleSettings(
        long revision,
        string protocolId,
        string endpoint,
        string model,
        int timeoutSeconds = 30) =>
        new(
            revision,
            [Profile("profile-primary", "chat-main", "secret-primary", protocolId, endpoint, model, timeoutSeconds)],
            [new ChannelBinding(AiChannel.ChatLlm, "profile-primary", "chat-main", model)]);

    public static ProviderProfile Profile(
        string profileId,
        string capabilityId,
        string secretId,
        string protocolId,
        string endpoint,
        string model,
        int timeoutSeconds = 30) =>
        new(
            profileId,
            "Synthetic " + profileId,
            ProviderOrigin.Custom,
            enabled: true,
            new ProtocolBinding(protocolId),
            new OpaqueEndpoint(endpoint),
            new AuthDescriptor(new SecretRef(secretId), SecretScope.DevelopmentOnly),
            timeoutSeconds,
            [new CapabilityDefinition(capabilityId, AiChannel.ChatLlm, model)]);

    public void Dispose() => _directory.Dispose();
}

internal sealed class RecordingHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;
    private readonly object _gate = new();
    private readonly List<ChatRequestCapture> _requests = [];

    public RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        _responder = responder ?? throw new ArgumentNullException(nameof(responder));
    }

    public int RequestCount => Volatile.Read(ref _requestCount);
    private int _requestCount;

    public IReadOnlyList<ChatRequestCapture> Requests
    {
        get
        {
            lock (_gate)
            {
                return _requests.ToArray();
            }
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requestCount);
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var headers = request.Headers.ToDictionary(
            static header => header.Key,
            static header => string.Join(",", header.Value),
            StringComparer.OrdinalIgnoreCase);
        lock (_gate)
        {
            _requests.Add(new ChatRequestCapture(
                request.Method.Method,
                request.RequestUri?.OriginalString ?? string.Empty,
                request.Content?.Headers.ContentType?.MediaType ?? string.Empty,
                body,
                headers));
        }

        return await _responder(request, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed record ChatRequestCapture(
    string Method,
    string Endpoint,
    string ContentType,
    string Body,
    IReadOnlyDictionary<string, string> Headers)
{
    public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;
}

internal static class ChatTestResponses
{
    public static HttpResponseMessage Success(string protocolId, string text = "synthetic-result") =>
        Json(HttpStatusCode.OK, protocolId switch
        {
            ChatProtocolIds.OpenAiChatCompletionsV1 or ChatProtocolIds.OpenAiCompatibleV1 =>
                "{\"unknown\":\"ignored\",\"choices\":[{\"message\":{\"content\":" + JsonSerializer.Serialize(text) + ",\"unknown\":true}}],\"usage\":{\"prompt_tokens\":3,\"completion_tokens\":5,\"total_tokens\":8}}",
            ChatProtocolIds.OpenAiResponsesV1 =>
                "{\"unknown\":{},\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"output_text\",\"text\":" + JsonSerializer.Serialize(text) + ",\"unknown\":0}]}],\"usage\":{\"input_tokens\":3,\"output_tokens\":5,\"total_tokens\":8}}",
            ChatProtocolIds.AnthropicMessagesV1 =>
                "{\"content\":[{\"type\":\"text\",\"text\":" + JsonSerializer.Serialize(text) + ",\"unknown\":\"ignored\"}],\"usage\":{\"input_tokens\":3,\"output_tokens\":5},\"unknown\":true}",
            ChatProtocolIds.GeminiGenerateContentV1 =>
                "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":" + JsonSerializer.Serialize(text) + ",\"unknown\":true}]}}],\"usageMetadata\":{\"promptTokenCount\":3,\"candidatesTokenCount\":5,\"totalTokenCount\":8},\"unknown\":null}",
            _ => throw new ArgumentOutOfRangeException(nameof(protocolId)),
        });

    public static HttpResponseMessage Json(HttpStatusCode statusCode, string json) => new(statusCode)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };
}
