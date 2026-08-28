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
    public void TomImportUsesTheRealFixedShapeSkipsSensitiveFieldsAndKeepsRelayAutoAsAConfirmationOnlySuggestion()
    {
        var importer = new TomProviderDraftImporter();
        var fixtures = new[]
        {
            (Type: "openai", Origin: ProviderOrigin.Official, BaseUrl: "https://official.example.invalid/v1/", Model: "chat-model-1"),
            (Type: "relay-api", Origin: ProviderOrigin.Relay, BaseUrl: "https://relay.example.invalid/v1/", Model: "chat-model-2"),
            (Type: "openai-compatible", Origin: ProviderOrigin.Friend, BaseUrl: "https://friend.example.invalid/v1/", Model: "chat-model-3"),
            (Type: "openai-codex-login", Origin: ProviderOrigin.Subscription, BaseUrl: string.Empty, Model: "codex"),
            (Type: "custom", Origin: ProviderOrigin.Custom, BaseUrl: "https://custom.example.invalid/v1/", Model: "chat-model-5"),
        };

        foreach (var fixture in fixtures)
        {
            var source = TomFixture(fixture.Type, fixture.BaseUrl, fixture.Model, relayProtocol: "auto");
            try
            {
                if (fixture.Origin == ProviderOrigin.Relay)
                {
                    A1TestSupport.Throws(
                        AiErrorCode.ImportConfirmationRequired,
                        () => importer.Import(source, relayProtocolConfirmed: false));
                    continue;
                }

                var draft = importer.Import(source, relayProtocolConfirmed: false);
                Assert.AreEqual(fixture.Origin, draft.OriginSuggestion, fixture.Type);
                Assert.AreEqual(fixture.Model, draft.ModelId, fixture.Type);
                Assert.AreEqual(fixture.Origin == ProviderOrigin.Subscription, draft.RequiresEndpointConfiguration, fixture.Type);
                Assert.IsFalse(draft.RequiresRelayProtocolConfirmation, fixture.Type);
                Assert.AreEqual(
                    null,
                    draft.RelayProtocolSuggestion,
                    fixture.Type);
                Assert.IsFalse(draft.ToString().Contains("synthetic-never-import", StringComparison.Ordinal));
                Assert.IsFalse(draft.ToString().Contains("synthetic-command-path", StringComparison.Ordinal));
            }
            finally
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(source);
            }
        }

        var relay = TomFixture("relay-api", "https://relay.example.invalid/v1/", "chat-model-relay", relayProtocol: "auto");
        try
        {
            var confirmedRelay = importer.Import(relay, relayProtocolConfirmed: true);
            Assert.IsFalse(confirmedRelay.RequiresRelayProtocolConfirmation);
            Assert.AreEqual("auto", confirmedRelay.RelayProtocolSuggestion);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(relay);
        }

        var draftPropertyNames = typeof(TomProviderDraft).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(static property => property.Name)
            .ToArray();
        Assert.IsFalse(draftPropertyNames.Any(static name =>
            name.Contains("ApiKey", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Command", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Verification", StringComparison.OrdinalIgnoreCase)));
    }

    private static byte[] TomFixture(string type, string baseUrl, string model, string relayProtocol)
    {
        var json = $$"""
        {
          "Id":"fixture-{{type}}",
          "Type":"{{type}}",
          "DisplayName":"{{type}} fixture",
          "Enabled":true,
          "BaseUrl":"{{baseUrl}}",
          "ApiKeyProtected":"synthetic-never-import",
          "DefaultModel":"{{model}}",
          "CommandPath":"synthetic-command-path",
          "RelayWebsiteName":"synthetic-relay-site",
          "RelayProtocol":"{{relayProtocol}}",
          "RelayDetectionSummary":"synthetic-detection",
          "RelayDetectionConfidence":99,
          "TimeoutSeconds":30,
          "UseJsonSchema":true,
          "SaveRawResponse":true,
          "VerificationAvailable":true,
          "VerificationSignature":"synthetic-verification-signature",
          "VerificationMessage":"synthetic-verification-message",
          "LastVerifiedAtUtc":"2026-08-28T00:00:00Z"
        }
        """;
        return Encoding.UTF8.GetBytes(json);
    }
}
