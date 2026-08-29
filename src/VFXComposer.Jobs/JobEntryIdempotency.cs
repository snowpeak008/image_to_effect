using System.Security.Cryptography;
using System.Text;

namespace VFXComposer.Jobs;

/// <summary>
/// Derives the content-based entry idempotency key. Unlike the queue-token
/// <c>idempotencyKey</c>, which changes on every resubmission, this key is derived from the
/// entry content and stays stable across re-enqueues, so batch resume logic can recognise
/// an already-processed entry.
/// </summary>
public static class JobEntryIdempotency
{
    private const string DerivationVersion = "v1";

    /// <summary>Computes <c>sha256(batchId + itemId + normalized content)</c> as a bounded token.</summary>
    public static string Derive(string? batchId, string? itemId, string normalizedContent)
    {
        ArgumentNullException.ThrowIfNull(normalizedContent);
        var material = string.Join(
            '\n',
            DerivationVersion,
            batchId ?? string.Empty,
            itemId ?? string.Empty,
            normalizedContent);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return "sha256:" + Convert.ToHexString(digest).ToLowerInvariant();
    }
}
