using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Providers;

namespace VFXComposer.AI.Tests;

[TestClass]
public sealed class ProviderResolutionTests
{
    [TestMethod]
    public void EndpointPolicy_RejectsCredentialsQueriesOversizeNonHttpAndNonLoopbackHttp()
    {
        var credentials = A1TestSupport.Settings(endpoint: new Uri("https://user:password@provider.example.invalid/v1/"));
        A1TestSupport.Throws(AiErrorCode.EndpointRejected, () => ProviderConfigurationValidator.Validate(credentials));

        Assert.ThrowsExactly<ArgumentException>(() => new EndpointDefinition(
            new Uri("https://provider.example.invalid/v1/?api_key=query-credentials-are-not-a-service-root"),
            allowLoopbackHttp: false));

        const string endpointPrefix = "https://provider.example.invalid/";
        var oversizedEndpoint = endpointPrefix + new string('a', EndpointDefinition.MaximumUriLength - endpointPrefix.Length + 1);
        Assert.ThrowsExactly<ArgumentException>(() => new EndpointDefinition(new Uri(oversizedEndpoint), allowLoopbackHttp: false));

        var nonHttp = A1TestSupport.Settings(endpoint: new Uri("ftp://provider.example.invalid/"));
        A1TestSupport.Throws(AiErrorCode.EndpointRejected, () => ProviderConfigurationValidator.Validate(nonHttp));

        var nonLoopback = A1TestSupport.Settings(
            endpoint: new Uri("http://provider.example.invalid/"),
            secretScope: SecretScope.DevelopmentOnly);
        A1TestSupport.Throws(AiErrorCode.EndpointRejected, () => ProviderConfigurationValidator.Validate(nonLoopback));
    }

    [TestMethod]
    public void EndpointPolicy_AllowsOnlyExplicitDevelopmentLoopbackHttp()
    {
        var settings = A1TestSupport.Settings(
            endpoint: new Uri("http://127.0.0.1:8787/v1/"),
            secretScope: SecretScope.DevelopmentOnly);
        ProviderConfigurationValidator.Validate(settings);

        var productionSecret = A1TestSupport.Settings(
            endpoint: new Uri("http://localhost:8787/v1/"),
            secretScope: SecretScope.Production);
        A1TestSupport.Throws(AiErrorCode.EndpointRejected, () => ProviderConfigurationValidator.Validate(productionSecret));
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
            new EndpointDefinition(new Uri("https://provider.example.invalid/"), false),
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
