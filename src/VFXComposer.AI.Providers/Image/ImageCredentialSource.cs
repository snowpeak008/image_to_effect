using System.Security.Cryptography;
using VFXComposer.AI.Contracts;

namespace VFXComposer.AI.Providers.Image;

/// <summary>
/// Provides a credential to one image request without putting it in configuration, a default HTTP header, or a
/// result object. Implementations must retain ownership of the supplied memory.
/// </summary>
public interface IImageCredentialSource
{
    ValueTask<T> UseCredentialAsync<T>(
        string profileId,
        SecretRef secretRef,
        ImageCredentialUse<T> use,
        CancellationToken cancellationToken);
}

/// <summary>Receives credential bytes only for the duration of one request construction and send operation.</summary>
public delegate ValueTask<T> ImageCredentialUse<T>(ReadOnlyMemory<byte> credential, CancellationToken cancellationToken);

/// <summary>Adapts the existing per-user provider secret store for the image channel without exposing plaintext.</summary>
public sealed class ProviderSecretStoreImageCredentialSource : IImageCredentialSource
{
    private readonly ProviderSecretStore _store;

    public ProviderSecretStoreImageCredentialSource(ProviderSecretStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async ValueTask<T> UseCredentialAsync<T>(
        string profileId,
        SecretRef secretRef,
        ImageCredentialUse<T> use,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentNullException.ThrowIfNull(secretRef);
        ArgumentNullException.ThrowIfNull(use);
        cancellationToken.ThrowIfCancellationRequested();

        byte[]? copiedCredential = null;
        try
        {
            using var lease = _store.OpenSecret(profileId, secretRef);
            copiedCredential = lease.Bytes.ToArray();
            if (copiedCredential.Length == 0)
            {
                throw new AiGatewayException(AiErrorCode.SecretUnavailable);
            }

            return await use(copiedCredential, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (copiedCredential is not null)
            {
                CryptographicOperations.ZeroMemory(copiedCredential);
            }
        }
    }

    public override string ToString() => "ProviderSecretStoreImageCredentialSource(<redacted>)";
}
