using System.Security.Cryptography;
using System.Text;
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
        Assert.AreEqual(1L, store.Load().Configuration.Settings.Revision);

        File.WriteAllText(path + ".bak", "{not-json}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.WriteAllText(path, "{not-json}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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
    public void CrossProfileSecretRefReuseFailsClosedInConfiguration()
    {
        var first = A1TestSupport.Settings().Profiles[0];
        var second = new ProviderProfile(
            "profile-secondary",
            "Secondary provider",
            ProviderOrigin.Custom,
            false,
            new ProtocolBinding(ProviderProtocols.OpenAiCompatibleV1),
            new OpaqueEndpoint("https://secondary.example.invalid/v1/"),
            new AuthDescriptor(first.Auth.SecretRef, SecretScope.Production),
            30,
            [new CapabilityDefinition("chat-secondary", AiChannel.ChatLlm, "chat-model-2")]);
        var settings = new AiProviderSettings(1, [first, second], []);

        A1TestSupport.Throws(AiErrorCode.ConfigurationInvalid, () => ProviderConfigurationValidator.Validate(settings));
        A1TestSupport.Throws(AiErrorCode.ConfigurationInvalid, () => ProviderConfigurationCodec.Serialize(settings));
    }

    [TestMethod]
    public void Codec_EnforcesOpaqueEndpointUtf8StorageBoundWithoutUriAdmission()
    {
        var endpointAtLimit = new string('a', OpaqueEndpoint.MaximumUtf8ByteLength);
        Assert.AreEqual(OpaqueEndpoint.MaximumUtf8ByteLength, Encoding.UTF8.GetByteCount(endpointAtLimit));
        var boundarySettings = A1TestSupport.Settings(endpointValue: endpointAtLimit);
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
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OpaqueEndpoint(endpointAtLimit + "a"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OpaqueEndpoint(new string('\u754c', 2731)));

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
                endpointAtLimit + "a",
                StringComparison.Ordinal),
                AiErrorCode.ConfigurationInvalid);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonical);
        }

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
