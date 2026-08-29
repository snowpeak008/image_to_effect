using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Providers;
using VFXComposer.AI.Providers.Chat;

namespace VFXComposer.AI.Tests.Chat;

[TestClass]
public sealed class ChatChannelGatewaySurfaceTests
{
    [TestMethod]
    public void PublicSurface_UsesOnlyFactory_AndKeepsHandlerInjectionInternal()
    {
        var gatewayType = typeof(ChatChannelGateway);
        var publicConstructors = gatewayType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
        Assert.AreEqual(0, publicConstructors.Length);

        var injectedHandlerConstructor = gatewayType.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [
                typeof(ProviderConfigurationStore),
                typeof(ProviderHealthRegistry),
                typeof(ProviderSecretStore),
                typeof(HttpMessageHandler),
            ],
            modifiers: null);
        Assert.IsNotNull(injectedHandlerConstructor);
        Assert.IsTrue(injectedHandlerConstructor.IsAssembly);

        var factory = gatewayType.GetMethod(
            nameof(ChatChannelGateway.Create),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            [
                typeof(ProviderConfigurationStore),
                typeof(ProviderHealthRegistry),
                typeof(ProviderSecretStore),
            ],
            modifiers: null);
        Assert.IsNotNull(factory);
        Assert.AreEqual(gatewayType, factory.ReturnType);

        using var fixture = new ChatTestFixture();
        using var gateway = ChatChannelGateway.Create(fixture.Store, fixture.Health, fixture.Secrets);
        Assert.IsNotNull(gateway);
    }
}
