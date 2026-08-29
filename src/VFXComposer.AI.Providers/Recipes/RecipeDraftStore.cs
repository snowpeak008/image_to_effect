using System.Text.Json;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Recipes;

namespace VFXComposer.AI.Providers.Recipes;

/// <summary>
/// Current-user draft retention (REQ-001: drafts live only in user application data, never inside a Unity
/// project). Writes are atomic with a .bak fallback in the ProviderConfigurationStore pattern; confirmation
/// re-verifies the canonical SHA-256 so a stale UI can never confirm content the user did not see.
/// </summary>
public sealed class RecipeDraftStore : IRecipeDraftStore
{
    /// <summary>Newest records win; older ones are trimmed so the file stays bounded.</summary>
    private const int MaximumRetainedRecords = 32;

    private const int MaximumFileBytes = MaximumRetainedRecords * (RecipeChannelLimits.MaximumDraftJsonCharacters * 4 + 64 * 1024);

    private readonly object _gate = new();
    private readonly string _storePath;
    private readonly string _backupPath;

    public RecipeDraftStore(string storePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storePath);
        _storePath = Path.GetFullPath(storePath);
        _backupPath = _storePath + ".bak";
    }

    public override string ToString() => "RecipeDraftStore(<redacted>)";

    public RecipeDraftRecord Save(RecipeDraftRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_gate)
        {
            var records = LoadCore();
            records.RemoveAll(existing => string.Equals(existing.DraftId, record.DraftId, StringComparison.Ordinal));
            records.Add(record);
            Persist(records);
            return record;
        }
    }

    public RecipeDraftRecord Confirm(string draftId, string canonicalSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(draftId);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalSha256);
        lock (_gate)
        {
            var records = LoadCore();
            var index = records.FindIndex(record => string.Equals(record.DraftId, draftId, StringComparison.Ordinal));
            if (index < 0)
            {
                throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.NotFound);
            }

            var current = records[index];
            if (current.Status != RecipeDraftStatus.PendingConfirmation)
            {
                throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.InvalidStatus);
            }

            if (!string.Equals(current.CanonicalSha256, canonicalSha256, StringComparison.Ordinal))
            {
                throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.HashMismatch);
            }

            var confirmed = new RecipeDraftRecord(
                current.DraftId,
                RecipeDraftStatus.ConfirmedAwaitingBuild,
                current.CreatedUtc,
                DateTimeOffset.UtcNow,
                current.CorrelationId,
                current.PromptTemplateVersion,
                current.TemplateCatalogVersion,
                current.RecipeJson,
                current.CanonicalSha256,
                current.RecipeId,
                current.Archetype,
                current.Dimension,
                current.TargetProfile,
                current.Issues,
                current.RequestCount);
            records[index] = confirmed;
            Persist(records);
            return confirmed;
        }
    }

    public RecipeDraftRecord? TryGet(string draftId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(draftId);
        lock (_gate)
        {
            return LoadCore().Find(record => string.Equals(record.DraftId, draftId, StringComparison.Ordinal));
        }
    }

    private List<RecipeDraftRecord> LoadCore()
    {
        if (!File.Exists(_storePath) && !File.Exists(_backupPath))
        {
            return [];
        }

        if (TryRead(_storePath, out var primary))
        {
            return primary!;
        }

        if (TryRead(_backupPath, out var backup))
        {
            return backup!;
        }

        // Both copies exist but neither parses: fail closed instead of silently discarding retained drafts.
        throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.StorageFailed);
    }

    private void Persist(List<RecipeDraftRecord> records)
    {
        var retained = records
            .OrderByDescending(static record => record.UpdatedUtc)
            .ThenBy(static record => record.DraftId, StringComparer.Ordinal)
            .Take(MaximumRetainedRecords)
            .ToList();
        try
        {
            AtomicFileWriter.WriteReplace(_storePath, _backupPath, RecipeDraftCodec.Serialize(retained));
        }
        catch (IOException)
        {
            throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.StorageFailed);
        }
        catch (UnauthorizedAccessException)
        {
            throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.StorageFailed);
        }
        catch (InvalidOperationException)
        {
            throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.StorageFailed);
        }
    }

    private static bool TryRead(string path, out List<RecipeDraftRecord>? records)
    {
        records = null;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            records = RecipeDraftCodec.Deserialize(AtomicFileWriter.ReadBounded(path, MaximumFileBytes));
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
