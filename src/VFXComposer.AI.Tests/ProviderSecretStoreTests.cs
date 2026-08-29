using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Providers;

namespace VFXComposer.AI.Tests;

[TestClass]
public sealed class ProviderSecretStoreTests
{
    [TestMethod]
    public void CurrentUserDpapiSecret_IsCiphertextProfileBoundAndOldEnvelopeFailsClosed()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("DPAPI CurrentUser is Windows-only.");
        }

        using var directory = new A1TestDirectory();
        var store = new ProviderSecretStore(directory.Path);
        var secretRef = new SecretRef("secret-dpapi");
        const string ownerProfileId = "profile-primary";
        const string otherProfileId = "profile-secondary";
        const string testToken = "synthetic-a1-token-only";
        store.SaveSecret(ownerProfileId, secretRef, testToken.AsSpan());
        Assert.IsTrue(store.IsReadable(ownerProfileId, secretRef));
        Assert.IsFalse(store.IsReadable(otherProfileId, secretRef));

        var ownerPath = Directory.EnumerateFiles(directory.Path, "*.secret", SearchOption.TopDirectoryOnly).Single();
        store.SaveSecret(otherProfileId, secretRef, "synthetic-other-token".AsSpan());
        var otherPath = Directory.EnumerateFiles(directory.Path, "*.secret", SearchOption.TopDirectoryOnly)
            .Single(path => !string.Equals(path, ownerPath, StringComparison.Ordinal));
        File.Copy(ownerPath, otherPath, overwrite: true);
        Assert.IsFalse(store.IsReadable(otherProfileId, secretRef));

        var encrypted = File.ReadAllBytes(ownerPath);
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

        File.WriteAllBytes(ownerPath, "VFXAIDP1legacy-envelope-must-not-migrate"u8.ToArray());
        Assert.IsFalse(store.IsReadable(ownerProfileId, secretRef));
    }
}
