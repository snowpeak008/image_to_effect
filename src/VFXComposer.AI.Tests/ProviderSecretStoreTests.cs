using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Providers;

namespace VFXComposer.AI.Tests;

[TestClass]
public sealed class ProviderSecretStoreTests
{
    [TestMethod]
    public void CurrentUserDpapiSecret_IsCiphertextAndUnreadablePayloadFailsClosed()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("DPAPI CurrentUser is Windows-only.");
        }

        using var directory = new A1TestDirectory();
        var store = new ProviderSecretStore(directory.Path);
        var secretRef = new SecretRef("secret-dpapi");
        const string testToken = "synthetic-a1-token-only";
        store.SaveSecret(secretRef, testToken.AsSpan());
        Assert.IsTrue(store.IsReadable(secretRef));

        var encrypted = File.ReadAllBytes(store.SecretPathFor(secretRef));
        try
        {
            var asText = System.Text.Encoding.UTF8.GetString(encrypted);
            Assert.IsTrue(asText.IndexOf(testToken, StringComparison.Ordinal) < 0);
            Assert.IsTrue(encrypted.Length > testToken.Length);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(encrypted);
        }

        File.WriteAllBytes(store.SecretPathFor(secretRef), "not-a-valid-secret"u8.ToArray());
        Assert.IsFalse(store.IsReadable(secretRef));
        A1TestSupport.Throws(AiErrorCode.SecretUnavailable, () => store.OpenSecret(secretRef));
    }
}
