using System.Security.Cryptography;
using VFXComposer.AI.Contracts;

namespace VFXComposer.AI.Providers;

public static class ProviderConfigurationFingerprint
{
    public static ConfigurationFingerprint Compute(AiProviderSettings settings)
    {
        var canonical = ProviderConfigurationCodec.Serialize(settings);
        try
        {
            var digest = SHA256.HashData(canonical);
            try
            {
                return new ConfigurationFingerprint("sha256:" + Convert.ToHexString(digest).ToLowerInvariant());
            }
            finally
            {
                CryptographicOperations.ZeroMemory(digest);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonical);
        }
    }
}
