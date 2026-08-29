using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Providers;
using VFXComposer.AI.Providers.Image;

namespace VFXComposer.AI.Tests;

[TestClass]
public sealed class OpenAiCompatibleImageGatewaySurfaceTests
{
    [TestMethod]
    public void PublicSurface_UsesOnlyFactory_AndKeepsHandlerInjectionInternal()
    {
        var gatewayType = typeof(OpenAiCompatibleImageGateway);
        var publicConstructors = gatewayType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
        Assert.AreEqual(0, publicConstructors.Length);

        var injectedHandlerConstructor = gatewayType.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [
                typeof(ResolvedProviderRoute),
                typeof(IImageCredentialSource),
                typeof(PrivateImageArtifactCache),
                typeof(HttpMessageHandler),
                typeof(HttpMessageHandler),
            ],
            modifiers: null);
        Assert.IsNotNull(injectedHandlerConstructor);
        Assert.IsTrue(injectedHandlerConstructor.IsAssembly);

        var factory = gatewayType.GetMethod(
            nameof(OpenAiCompatibleImageGateway.Create),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            [typeof(ResolvedProviderRoute), typeof(ProviderSecretStore), typeof(string)],
            modifiers: null);
        Assert.IsNotNull(factory);
        Assert.AreEqual(gatewayType, factory.ReturnType);

        using var temp = new A3PrivateTempDirectory();
        var secretStore = new ProviderSecretStore(Path.Combine(temp.Path, "secrets"));
        using (var gateway = OpenAiCompatibleImageGateway.Create(A3ImageTestSupport.Route(), secretStore, temp.Path))
        {
            Assert.IsNotNull(gateway);
        }

        Assert.IsFalse(Directory.EnumerateFileSystemEntries(temp.Path).Any());
    }
}
