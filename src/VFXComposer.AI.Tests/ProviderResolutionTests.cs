using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Providers;

namespace VFXComposer.AI.Tests;

[TestClass]
public sealed class ProviderResolutionTests
{
    [TestMethod]
    public void Resolver_DoesNotInterpretOpaqueEndpointText()
    {
        const string endpoint = "https://[2001:db8::not-an-ipv6]:99999/v1?token=synthetic#fragment";
        var configuration = A1TestSupport.Read(A1TestSupport.Settings(endpointValue: endpoint));
        var health = new ProviderHealthRegistry();
        health.Record(A1TestSupport.VerifiedHealth(configuration, AiChannel.ChatLlm, "chat-main"));

        var route = A1TestSupport.Resolver(health).Resolve(AiChannel.ChatLlm, configuration);
        Assert.AreEqual(endpoint, route.Profile.Endpoint.Value);
    }

    [TestMethod]
    public void CrossChannelCapabilityAndAbsentChannel_HaveNoFallback()
    {
        var profile = new ProviderProfile(
            "profile-cross",
            "Cross profile",
            ProviderOrigin.Official,
            true,
            new ProtocolBinding(ProviderProtocols.OpenAiCompatibleV1),
            new OpaqueEndpoint("https://provider.example.invalid/"),
            new AuthDescriptor(new SecretRef("secret-cross"), SecretScope.Production),
            30,
            [new CapabilityDefinition("image-only", AiChannel.ImageGeneration, "image-model-1")]);
        var invalid = new AiProviderSettings(
            1,
            [profile],
            [new ChannelBinding(AiChannel.ChatLlm, "profile-cross", "image-only", "image-model-1")]);
        A1TestSupport.Throws(AiErrorCode.CapabilityMismatch, () => ProviderConfigurationValidator.Validate(invalid));

        var noImageBinding = A1TestSupport.Read(A1TestSupport.Settings(includeChatBinding: true, includeImageBinding: false));
        var health = new ProviderHealthRegistry();
        health.Record(A1TestSupport.VerifiedHealth(noImageBinding, AiChannel.ChatLlm, "chat-main"));
        var resolver = A1TestSupport.Resolver(health);
        A1TestSupport.Throws(AiErrorCode.ChannelUnbound, () => resolver.Resolve(AiChannel.ImageGeneration, noImageBinding));
    }

    [TestMethod]
    public void ChangedFingerprintMakesPriorHealthStale()
    {
        var revisionOne = A1TestSupport.Read(A1TestSupport.Settings(revision: 1));
        var revisionTwo = A1TestSupport.Read(A1TestSupport.Settings(revision: 2));
        Assert.AreNotEqual(revisionOne.Fingerprint, revisionTwo.Fingerprint);

        var health = new ProviderHealthRegistry();
        health.Record(A1TestSupport.VerifiedHealth(revisionOne, AiChannel.ChatLlm, "chat-main"));
        var resolver = A1TestSupport.Resolver(health);
        A1TestSupport.Throws(AiErrorCode.HealthStale, () => resolver.Resolve(AiChannel.ChatLlm, revisionTwo));

        health.Record(A1TestSupport.VerifiedHealth(revisionTwo, AiChannel.ChatLlm, "chat-main"));
        var route = resolver.Resolve(AiChannel.ChatLlm, revisionTwo);
        Assert.AreEqual("chat-main", route.Capability.Id);
    }

    [TestMethod]
    public void UnknownProtocolFailsClosedBeforeAnyAdapterExists()
    {
        var configuration = A1TestSupport.Read(A1TestSupport.Settings(protocolId: "unknown-provider-v1"));
        var health = new ProviderHealthRegistry();
        health.Record(A1TestSupport.VerifiedHealth(configuration, AiChannel.ChatLlm, "chat-main"));
        var resolver = A1TestSupport.Resolver(health);
        A1TestSupport.Throws(AiErrorCode.ProtocolNotAllowed, () => resolver.Resolve(AiChannel.ChatLlm, configuration));
    }
}
