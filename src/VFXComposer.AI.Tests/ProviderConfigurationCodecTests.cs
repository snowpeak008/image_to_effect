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

        File.WriteAllText(path + ".bak", "{not-json}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        A1TestSupport.Throws(AiErrorCode.ConfigurationUnavailable, () => store.Load());
    }

    [TestMethod]
    public void Store_RejectsARevisionThatDoesNotAdvance()
    {
        using var directory = new A1TestDirectory();
        var store = new ProviderConfigurationStore(System.IO.Path.Combine(directory.Path, "providers.json"));
        store.Save(A1TestSupport.Settings(revision: 1));
        A1TestSupport.Throws(AiErrorCode.ConfigurationInvalid, () => store.Save(A1TestSupport.Settings(revision: 1)));
    }
}
