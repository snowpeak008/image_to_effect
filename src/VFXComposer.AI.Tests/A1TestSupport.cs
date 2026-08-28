using VFXComposer.AI.Contracts;
using VFXComposer.AI.Providers;

namespace VFXComposer.AI.Tests;

internal static class A1TestSupport
{
    public static AiProviderSettings Settings(
        long revision = 1,
        bool includeChatBinding = true,
        bool includeImageBinding = false,
        Uri? endpoint = null,
        SecretScope secretScope = SecretScope.Production,
        bool enabled = true,
        string protocolId = ProviderProtocols.OpenAiCompatibleV1)
    {
        endpoint ??= new Uri("https://provider.example.invalid/v1/");
        var profile = new ProviderProfile(
            "profile-primary",
            "Primary provider",
            ProviderOrigin.Official,
            enabled,
            new ProtocolBinding(protocolId),
            new EndpointDefinition(endpoint, endpoint.Scheme == Uri.UriSchemeHttp),
            new AuthDescriptor(new SecretRef("secret-primary"), secretScope),
            30,
            [
                new CapabilityDefinition("chat-main", AiChannel.ChatLlm, "chat-model-1"),
                new CapabilityDefinition("image-main", AiChannel.ImageGeneration, "image-model-1"),
            ]);
        var bindings = new List<ChannelBinding>();
        if (includeChatBinding)
        {
            bindings.Add(new ChannelBinding(AiChannel.ChatLlm, "profile-primary", "chat-main", "chat-model-1"));
        }

        if (includeImageBinding)
        {
            bindings.Add(new ChannelBinding(AiChannel.ImageGeneration, "profile-primary", "image-main", "image-model-1"));
        }

        return new AiProviderSettings(revision, [profile], bindings);
    }

    public static ProviderConfigurationReadResult Read(AiProviderSettings settings) =>
        ProviderConfigurationCodec.Deserialize(ProviderConfigurationCodec.Serialize(settings));

    public static ProviderConfigurationResolver Resolver(
        ProviderHealthRegistry health,
        bool secretReadable = true) =>
        new(AllowlistedProviderRegistry.Default, health, new TestSecretVerifier(secretReadable));

    public static ProviderHealth VerifiedHealth(ProviderConfigurationReadResult configuration, AiChannel channel, string capabilityId) =>
        new(
            "profile-primary",
            capabilityId,
            channel,
            configuration.Fingerprint,
            ProviderHealthState.Verified,
            DateTimeOffset.UtcNow);

    public static AiGatewayException Throws(AiErrorCode expectedCode, Action action)
    {
        var exception = Assert.ThrowsExactly<AiGatewayException>(action);
        Assert.AreEqual(expectedCode, exception.Code);
        return exception;
    }

    internal sealed class TestSecretVerifier(bool readable) : ISecretReferenceVerifier
    {
        public bool IsReadable(SecretRef secretRef) => readable;
    }
}

internal sealed class A1TestDirectory : IDisposable
{
    public A1TestDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "vfxcomposer-a1-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
