using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Providers;

namespace VFXComposer.AI.Tests;

[TestClass]
public sealed class ProviderConfigurationCodecTests
{
    [TestMethod]
    public void CanonicalConfiguration_RoundTripsAndContainsOnlySecretRef()
    {
        var settings = A1TestSupport.Settings(includeImageBinding: true);
        var canonical = ProviderConfigurationCodec.Serialize(settings);
        try
        {
            var result = ProviderConfigurationCodec.Deserialize(canonical);
            Assert.IsFalse(result.RequiresMigration);
            Assert.AreEqual(1L, result.Settings.Revision);
            Assert.AreEqual(2, result.Settings.ChannelBindings.Count);
            var text = Encoding.UTF8.GetString(canonical);
            Assert.IsTrue(text.Contains("\"secretRef\"", StringComparison.Ordinal));
            Assert.IsFalse(text.Contains("apiKey", StringComparison.OrdinalIgnoreCase));
            CollectionAssert.AreEqual(canonical, ProviderConfigurationCodec.Serialize(result.Settings));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonical);
        }
    }

    [TestMethod]
    public void LegacyV0_RequiresExplicitMigration_ButFutureVersionFailsClosed()
    {
        var canonical = ProviderConfigurationCodec.Serialize(A1TestSupport.Settings());
        try
        {
            var legacy = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(canonical).Replace("\"formatVersion\":1", "\"formatVersion\":0", StringComparison.Ordinal));
            try
            {
                var result = ProviderConfigurationCodec.Deserialize(legacy);
                Assert.IsTrue(result.RequiresMigration);
                Assert.AreEqual(1, result.Settings.FormatVersion);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(legacy);
            }

            var future = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(canonical).Replace("\"formatVersion\":1", "\"formatVersion\":2", StringComparison.Ordinal));
            try
            {
                A1TestSupport.Throws(AiErrorCode.ConfigurationInvalid, () => ProviderConfigurationCodec.Deserialize(future));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(future);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonical);
        }
    }

    [TestMethod]
    public void DuplicateUnknownAndTrailingConfiguration_FailClosed()
    {
        foreach (var malformed in new[]
        {
            "{\"formatVersion\":1,\"formatVersion\":1}",
            "{\"formatVersion\":1,\"revision\":1,\"profiles\":[],\"channelBindings\":[],\"unknown\":true}",
            "{\"formatVersion\":1,\"revision\":1,\"profiles\":[],\"channelBindings\":[]} trailing",
        })
        {
            var bytes = Encoding.UTF8.GetBytes(malformed);
            try
            {
                A1TestSupport.Throws(AiErrorCode.ConfigurationInvalid, () => ProviderConfigurationCodec.Deserialize(bytes));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }
    }

    [TestMethod]
    public void Store_IsAtomicKeepsBackupAndRecoversOnlyFromValidBackup()
    {
        using var directory = new A1TestDirectory();
        var path = System.IO.Path.Combine(directory.Path, "providers.json");
        var store = new ProviderConfigurationStore(path);
        store.Save(A1TestSupport.Settings(revision: 1));
        store.Save(A1TestSupport.Settings(revision: 2));

        Assert.AreEqual(2L, store.Load().Configuration.Settings.Revision);
        var backupBytes = File.ReadAllBytes(path + ".bak");
        try
        {
            Assert.AreEqual(1L, ProviderConfigurationCodec.Deserialize(backupBytes).Settings.Revision);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(backupBytes);
        }

        File.WriteAllText(path, "{not-json}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var recovered = store.Load();
        Assert.IsTrue(recovered.RecoveredFromBackup);
        Assert.AreEqual(1L, recovered.Configuration.Settings.Revision);

        File.WriteAllText(path + ".bak", "{not-json}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        A1TestSupport.Throws(AiErrorCode.ConfigurationUnavailable, () => store.Load());
    }

    [TestMethod]
    public void Store_SaveAfterBackupRecoveryPreservesARecoverableKnownGoodBackup()
    {
        using var directory = new A1TestDirectory();
        var path = System.IO.Path.Combine(directory.Path, "providers.json");
        var store = new ProviderConfigurationStore(path);
        store.Save(A1TestSupport.Settings(revision: 1));
        store.Save(A1TestSupport.Settings(revision: 2));

        File.WriteAllText(path, "{corrupt-primary}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var recovered = store.Load();
        Assert.IsTrue(recovered.RecoveredFromBackup);
        Assert.AreEqual(1L, recovered.Configuration.Settings.Revision);

        store.Save(A1TestSupport.Settings(revision: 2));
        Assert.AreEqual(2L, store.Load().Configuration.Settings.Revision);

        File.WriteAllText(path, "{corrupt-again}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var recoveredAgain = store.Load();
        Assert.IsTrue(recoveredAgain.RecoveredFromBackup);
        Assert.AreEqual(1L, recoveredAgain.Configuration.Settings.Revision);
    }

    [TestMethod]
    public void Store_RejectsARevisionThatDoesNotAdvance()
    {
        using var directory = new A1TestDirectory();
        var store = new ProviderConfigurationStore(System.IO.Path.Combine(directory.Path, "providers.json"));
        store.Save(A1TestSupport.Settings(revision: 1));
        A1TestSupport.Throws(AiErrorCode.ConfigurationInvalid, () => store.Save(A1TestSupport.Settings(revision: 1)));
    }

    [TestMethod]
    public async Task Store_ConcurrentSameRevisionHasExactlyOneWinner()
    {
        using var directory = new A1TestDirectory();
        var path = System.IO.Path.Combine(directory.Path, "providers.json");
        new ProviderConfigurationStore(path).Save(A1TestSupport.Settings(revision: 1));

        using var start = new ManualResetEventSlim(initialState: false);
        var attempts = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() =>
            {
                start.Wait();
                try
                {
                    new ProviderConfigurationStore(path).Save(A1TestSupport.Settings(revision: 2));
                    return (Succeeded: true, Error: (AiErrorCode?)null);
                }
                catch (AiGatewayException exception)
                {
                    return (Succeeded: false, Error: (AiErrorCode?)exception.Code);
                }
            }))
            .ToArray();

        start.Set();
        var results = await Task.WhenAll(attempts);
        Assert.AreEqual(1, results.Count(result => result.Succeeded));
        Assert.IsTrue(results.Where(result => !result.Succeeded).All(result => result.Error == AiErrorCode.ConfigurationInvalid));
        Assert.AreEqual(2L, new ProviderConfigurationStore(path).Load().Configuration.Settings.Revision);
    }

    [TestMethod]
    public void CrossProfileSecretRefReuseFailsClosedInConfiguration()
    {
        var first = A1TestSupport.Settings().Profiles[0];
        var second = new ProviderProfile(
            "profile-secondary",
            "Secondary provider",
            ProviderOrigin.Custom,
            false,
            new ProtocolBinding(ProviderProtocols.OpenAiCompatibleV1),
            new EndpointDefinition(new Uri("https://secondary.example.invalid/v1/"), allowLoopbackHttp: false),
            new AuthDescriptor(first.Auth.SecretRef, SecretScope.Production),
            30,
            [new CapabilityDefinition("chat-secondary", AiChannel.ChatLlm, "chat-model-2")]);
        var settings = new AiProviderSettings(1, [first, second], []);

        A1TestSupport.Throws(AiErrorCode.ConfigurationInvalid, () => ProviderConfigurationValidator.Validate(settings));
        A1TestSupport.Throws(AiErrorCode.ConfigurationInvalid, () => ProviderConfigurationCodec.Serialize(settings));
    }

    [TestMethod]
    public void CodecAndSchema_StayInParityForProtocolAndEndpointBoundaries()
    {
        const string endpointPrefix = "https://provider.example.invalid/";
        var endpointAtLimit = endpointPrefix + new string('a', EndpointDefinition.MaximumUriLength - endpointPrefix.Length);
        Assert.AreEqual(EndpointDefinition.MaximumUriLength, endpointAtLimit.Length);
        var boundarySettings = A1TestSupport.Settings(endpoint: new Uri(endpointAtLimit));
        var boundaryBytes = ProviderConfigurationCodec.Serialize(boundarySettings);
        try
        {
            Assert.AreEqual(1L, ProviderConfigurationCodec.Deserialize(boundaryBytes).Settings.Revision);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(boundaryBytes);
        }

        Assert.ThrowsExactly<ArgumentException>(() => new ProtocolBinding("1openai-compatible-v1"));
        Assert.ThrowsExactly<ArgumentException>(() => new EndpointDefinition(
            new Uri("https://provider.example.invalid/v1/?api_key=must-not-be-a-service-root"),
            allowLoopbackHttp: false));
        Assert.ThrowsExactly<ArgumentException>(() => new EndpointDefinition(
            new Uri(endpointAtLimit + "a"),
            allowLoopbackHttp: false));

        var canonical = ProviderConfigurationCodec.Serialize(A1TestSupport.Settings());
        try
        {
            var canonicalText = Encoding.UTF8.GetString(canonical);
            AssertCodecRejects(canonicalText.Replace(
                "\"id\":\"openai-compatible-v1\"",
                "\"id\":\"1openai-compatible-v1\"",
                StringComparison.Ordinal),
                AiErrorCode.ConfigurationInvalid);
            AssertCodecRejects(canonicalText.Replace(
                "https://provider.example.invalid/v1/",
                "https://provider.example.invalid/v1/?api_key=must-not-be-an-endpoint-field",
                StringComparison.Ordinal),
                AiErrorCode.EndpointRejected);
            AssertCodecRejects(canonicalText.Replace(
                "https://provider.example.invalid/v1/",
                endpointAtLimit + "a",
                StringComparison.Ordinal),
                AiErrorCode.EndpointRejected);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonical);
        }

        using var schema = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory,
            "Schemas",
            "vfxcomposer-ai-provider-config-v1.schema.json")));
        var protocol = schema.RootElement.GetProperty("$defs").GetProperty("profile")
            .GetProperty("properties").GetProperty("protocol").GetProperty("properties").GetProperty("id");
        var endpoint = schema.RootElement.GetProperty("$defs").GetProperty("profile")
            .GetProperty("properties").GetProperty("endpoint").GetProperty("properties").GetProperty("uri");
        Assert.AreEqual("^[a-z][a-z0-9.-]*-v1$", protocol.GetProperty("pattern").GetString());
        Assert.AreEqual(EndpointDefinition.MaximumUriLength, endpoint.GetProperty("maxLength").GetInt32());
        Assert.AreEqual("^https?://[^?#]+$", endpoint.GetProperty("pattern").GetString());
    }

    private static void AssertCodecRejects(string json, AiErrorCode expectedCode)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        try
        {
            A1TestSupport.Throws(expectedCode, () => ProviderConfigurationCodec.Deserialize(bytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
