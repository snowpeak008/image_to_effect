using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Providers;

namespace VFXComposer.AI.Tests;

[TestClass]
public sealed class ProviderSafetySurfaceTests
{
    [TestMethod]
    public void ContractAssemblyHasNoProviderTransportFileBrokerOrUnityDependency()
    {
        var references = typeof(AiProviderSettings).Assembly.GetReferencedAssemblies()
            .Select(static reference => reference.Name ?? string.Empty)
            .ToArray();
        Assert.IsFalse(references.Any(static name => name.Contains("Http", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(references.Any(static name => name.Contains("Broker", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(references.Any(static name => name.Contains("Unity", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(references.Any(static name => name.Contains("Providers", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void GatewayPerformsNoTransportWhenConfigurationFailsClosed()
    {
        using var directory = new A1TestDirectory();
        var store = new ProviderConfigurationStore(System.IO.Path.Combine(directory.Path, "providers.json"));
        var gateway = new ConfigurationAiGateway(
            store,
            A1TestSupport.Resolver(new ProviderHealthRegistry(), secretReadable: false));
        A1TestSupport.Throws(
            AiErrorCode.ConfigurationUnavailable,
            () => gateway.ChatAsync(new ChatRequest("correlation-1", [new ChatMessage(ChatRole.User, "synthetic prompt")])).GetAwaiter().GetResult());

        var referencedNames = typeof(ConfigurationAiGateway).Assembly.GetReferencedAssemblies()
            .Select(static reference => reference.Name ?? string.Empty);
        Assert.IsFalse(referencedNames.Any(static name => name.Contains("System.Net.Http", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void RedactionAndDtoFormattingNeverExposeSyntheticSensitiveValues()
    {
        const string prompt = "synthetic prompt should not escape";
        const string endpoint = "https://provider.example.invalid/private";
        var settings = A1TestSupport.Settings(endpoint: new Uri(endpoint));
        var configuration = A1TestSupport.Read(settings);
        var profile = configuration.Settings.Profiles[0];
        var route = new ResolvedProviderRoute(
            AiChannel.ChatLlm,
            profile,
            profile.Capabilities[0],
            configuration.Settings.ChannelBindings[0],
            configuration.Fingerprint);

        Assert.AreEqual(ProviderRedaction.Redacted, ProviderRedaction.Redact(prompt));
        Assert.IsFalse(route.ToString().Contains(endpoint, StringComparison.Ordinal));
        Assert.IsFalse(profile.ToString().Contains(endpoint, StringComparison.Ordinal));
        Assert.IsFalse(new ChatRequest("correlation-2", [new ChatMessage(ChatRole.User, prompt)]).ToString().Contains(prompt, StringComparison.Ordinal));
    }

    [TestMethod]
    public void TomImportIsNonSensitiveAndRelayRequiresConfirmation()
    {
        var importer = new TomProviderDraftImporter();
        var relay = Encoding.UTF8.GetBytes("{\"displayName\":\"Relay draft\",\"originSuggestion\":\"Relay\",\"endpoint\":\"https://relay.example.invalid/v1/\",\"modelId\":\"chat-model-1\",\"timeoutSeconds\":30}");
        try
        {
            A1TestSupport.Throws(AiErrorCode.ImportConfirmationRequired, () => importer.Import(relay, relayConfirmed: false));
            var draft = importer.Import(relay, relayConfirmed: true);
            Assert.AreEqual(ProviderOrigin.Relay, draft.OriginSuggestion);
            Assert.IsFalse(draft.ToString().Contains("relay.example", StringComparison.Ordinal));
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(relay);
        }

        var protectedKey = Encoding.UTF8.GetBytes("{\"ApiKeyProtected\":\"synthetic-never-import\"}");
        try
        {
            A1TestSupport.Throws(AiErrorCode.ImportRejected, () => importer.Import(protectedKey, relayConfirmed: true));
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(protectedKey);
        }
    }
}
