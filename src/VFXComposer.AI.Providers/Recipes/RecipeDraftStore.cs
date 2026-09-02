using System.Text.Json;
using VFXComposer.AI.Contracts.Recipes;

namespace VFXComposer.AI.Providers.Recipes;

/// <summary>
/// Current-user draft retention (REQ-001: drafts live only in user application data, never inside a Unity
/// project) organised as linear version chains (REQ-004 §7). Writes are atomic with a .bak fallback in the
/// ProviderConfigurationStore pattern; every state transition re-verifies the canonical SHA-256 so a stale
/// caller can never advance content the user did not see.
/// <para>
/// <b>Cross-process conflicts (REQ-004 RG-6).</b> Desktop, <c>vfxc</c> and <c>vfxc-mcp</c> share one file. Every
/// public member runs its whole load→mutate→persist cycle under <see cref="RecipeDraftStoreLock"/>, a durable
/// lock file (<c>recipe-drafts.json.lock</c>) next to the store whose exclusive handle is held for the cycle.
/// Concurrent writers therefore serialize: the second writer observes the first writer's records and appends to
/// them, so last-write-wins loss of the other entry's records cannot occur. Waiting is bounded by the lock
/// timeout (5 s by default); when the lease cannot be obtained in time the call fails closed with
/// <see cref="RecipeDraftStoreErrorCode.StoreBusy"/>, nothing is read or written, and the caller decides whether
/// to retry. Reads take the same lease so they never observe a half-replaced file. A holder killed mid-cycle
/// releases the OS handle; the file itself is still whole because replacement is atomic.
/// </para>
/// <para>
/// <b>Retention.</b> Level 1 keeps a lineage at or under <see cref="RecipeDraftLineageLimits.MaximumVersionsPerLineage"/>
/// versions and <see cref="RecipeDraftLineageLimits.MaximumLineageRecipeJsonBytes"/> of persisted recipe JSON by
/// removing the oldest unprotected versions and splicing the chain; the head and every confirmed, built,
/// build-failed or superseded version are protected, and when only protected versions remain a new version is
/// refused with <see cref="RecipeDraftStoreErrorCode.LineageCapacityExhausted"/>. Level 2 keeps at most
/// <see cref="RecipeDraftLineageLimits.MaximumLineages"/> lineages by evicting the least recently updated whole
/// lineage. Every removal is reported on the typed outcome; nothing is dropped silently.
/// </para>
/// </summary>
public sealed class RecipeDraftStore : IRecipeDraftLineageStore
{
    /// <summary>Covers every bounded non-recipe field of one record at the writer's worst-case escaping.</summary>
    private const int RecordMetadataMarginBytes = 192 * 1024;

    /// <summary>
    /// Derived from the caps, not from the per-record JSON maximum: 8 lineages × 1 MiB of recipe JSON plus
    /// 8 × 16 records × the metadata margin = 32 MiB, the REQ-004-35 ceiling.
    /// </summary>
    internal const int MaximumFileBytes =
        RecipeDraftLineageLimits.MaximumLineages * RecipeDraftLineageLimits.MaximumLineageRecipeJsonBytes +
        RecipeDraftLineageLimits.MaximumLineages * RecipeDraftLineageLimits.MaximumVersionsPerLineage * RecordMetadataMarginBytes;

    private readonly string _storePath;
    private readonly string _backupPath;
    private readonly RecipeDraftStoreLock _lock;

    public RecipeDraftStore(string storePath, TimeSpan? lockTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storePath);
        _storePath = Path.GetFullPath(storePath);
        _backupPath = _storePath + ".bak";
        _lock = new RecipeDraftStoreLock(_storePath, lockTimeout);
    }

    public override string ToString() => "RecipeDraftStore(<redacted>)";

    /// <summary>Starts a lineage; identical to <see cref="SaveVersion"/> with the retention report discarded by the caller.</summary>
    public RecipeDraftRecord Save(RecipeDraftRecord record) => SaveVersion(record).Record;

    public RecipeDraftSaveOutcome SaveVersion(RecipeDraftRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.ParentDraftId is not null || record.RevisionOrdinal != 1)
        {
            throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.RecordInvalid);
        }

        return _lock.Execute(() =>
        {
            var document = LoadCore();
            if (IndexOf(document, record.DraftId) >= 0 || document.RevisionWatermarks.ContainsKey(record.LineageId))
            {
                throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.RecordInvalid);
            }

            document.Records.Add(record);
            document.RevisionWatermarks[record.LineageId] = record.RevisionOrdinal;
            var (evictedLineageIds, evictedVersionCount) = EvictLineages(document, record.LineageId);
            Persist(document);
            return new RecipeDraftSaveOutcome(
                record,
                Array.Empty<string>(),
                Array.Empty<string>(),
                evictedLineageIds,
                evictedVersionCount);
        });
    }

    public RecipeDraftSaveOutcome AppendVersion(
        string parentDraftId,
        string parentCanonicalSha256,
        RecipeDraftRevision revision,
        DateTimeOffset createdUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentDraftId);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentCanonicalSha256);
        ArgumentNullException.ThrowIfNull(revision);
        if (createdUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must be UTC.", nameof(createdUtc));
        }

        return _lock.Execute(() =>
        {
            var document = LoadCore();
            var parentIndex = IndexOf(document, parentDraftId);
            if (parentIndex < 0)
            {
                throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.NotFound);
            }

            var parent = document.Records[parentIndex];
            if (parent.CanonicalSha256 is null)
            {
                throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.InvalidStatus);
            }

            if (!string.Equals(parent.CanonicalSha256, parentCanonicalSha256, StringComparison.Ordinal))
            {
                throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.HashMismatch);
            }

            var lineage = document.Lineage(parent.LineageId);
            if (!string.Equals(lineage[^1].DraftId, parent.DraftId, StringComparison.Ordinal))
            {
                throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.NotLineageHead);
            }

            var ordinal = checked(document.RevisionWatermarks[parent.LineageId] + 1);
            var draft = revision.Draft;
            var version = new RecipeDraftRecord(
                RecipeDraftRecord.NewDraftId(),
                RecipeDraftStatus.PendingConfirmation,
                createdUtc,
                createdUtc,
                draft.CorrelationId,
                draft.PromptTemplateVersion,
                draft.TemplateCatalogVersion,
                draft.RecipeJson,
                draft.CanonicalSha256,
                draft.RecipeId,
                draft.Archetype,
                draft.Dimension,
                draft.TargetProfile,
                Array.Empty<RecipeValidationIssue>(),
                revision.RequestCount,
                new RecipeDraftProvenance(
                    parent.LineageId,
                    parent.DraftId,
                    ordinal,
                    revision.Origin,
                    revision.FeedbackText,
                    revision.GuardRestorations,
                    revision.GuardRestorationCount));

            // REQ-004 §7.3 rule 6: the confirmation belonged to a version that is no longer the head. The
            // transition goes through the same hash-bound routine as every other status advance.
            var supersededDraftIds = new List<string>();
            for (var index = 0; index < document.Records.Count; index++)
            {
                var candidate = document.Records[index];
                if (string.Equals(candidate.LineageId, parent.LineageId, StringComparison.Ordinal) &&
                    candidate.Status == RecipeDraftStatus.ConfirmedAwaitingBuild)
                {
                    document.Records[index] = Transition(
                        candidate,
                        candidate.CanonicalSha256!,
                        RecipeDraftStatus.ConfirmedAwaitingBuild,
                        RecipeDraftStatus.Superseded,
                        createdUtc);
                    supersededDraftIds.Add(candidate.DraftId);
                }
            }

            document.Records.Add(version);
            var trimmedDraftIds = TrimLineage(document, version.LineageId, version.DraftId);
            document.RevisionWatermarks[version.LineageId] = ordinal;
            Persist(document);
            return new RecipeDraftSaveOutcome(version, supersededDraftIds, trimmedDraftIds, Array.Empty<string>(), 0);
        });
    }

    public RecipeDraftTruncateOutcome TruncateAfter(string draftId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(draftId);
        return _lock.Execute(() =>
        {
            var document = LoadCore();
            var index = IndexOf(document, draftId);
            if (index < 0)
            {
                throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.NotFound);
            }

            var target = document.Records[index];
            var later = document.Lineage(target.LineageId)
                .Where(version => version.RevisionOrdinal > target.RevisionOrdinal)
                .ToList();
            if (later.Any(static version => version.Status is
                    RecipeDraftStatus.ConfirmedAwaitingBuild or RecipeDraftStatus.Built or RecipeDraftStatus.BuildFailed))
            {
                throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.TruncationBlocked);
            }

            var removedDraftIds = later.Select(static version => version.DraftId).ToArray();
            if (removedDraftIds.Length > 0)
            {
                document.Records.RemoveAll(record => removedDraftIds.Contains(record.DraftId, StringComparer.Ordinal));
                Persist(document);
            }

            return new RecipeDraftTruncateOutcome(target, removedDraftIds);
        });
    }

    public RecipeDraftRecord Confirm(string draftId, string canonicalSha256) =>
        Advance(draftId, canonicalSha256, RecipeDraftStatus.PendingConfirmation, RecipeDraftStatus.ConfirmedAwaitingBuild);

    public RecipeDraftRecord MarkBuilt(string draftId, string canonicalSha256) =>
        Advance(draftId, canonicalSha256, RecipeDraftStatus.ConfirmedAwaitingBuild, RecipeDraftStatus.Built);

    public RecipeDraftRecord MarkBuildFailed(string draftId, string canonicalSha256) =>
        Advance(draftId, canonicalSha256, RecipeDraftStatus.ConfirmedAwaitingBuild, RecipeDraftStatus.BuildFailed);

    public RecipeDraftRecord? TryGet(string draftId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(draftId);
        if (!AnyStoreFile())
        {
            return null;
        }

        return _lock.Execute(() =>
        {
            var document = LoadCore();
            var index = IndexOf(document, draftId);
            return index < 0 ? null : document.Records[index];
        });
    }

    public IReadOnlyList<RecipeDraftRecord> ListConfirmedAwaitingBuild()
    {
        if (!AnyStoreFile())
        {
            return Array.Empty<RecipeDraftRecord>();
        }

        return _lock.Execute(() => LoadCore().Records
            .Where(static record => record.Status == RecipeDraftStatus.ConfirmedAwaitingBuild)
            .OrderBy(static record => record.UpdatedUtc)
            .ThenBy(static record => record.DraftId, StringComparer.Ordinal)
            .ToArray());
    }

    public IReadOnlyList<RecipeDraftRecord> ListLineage(string lineageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lineageId);
        if (!AnyStoreFile())
        {
            return Array.Empty<RecipeDraftRecord>();
        }

        return _lock.Execute(() => LoadCore().Lineage(lineageId));
    }

    private RecipeDraftRecord Advance(
        string draftId,
        string canonicalSha256,
        RecipeDraftStatus required,
        RecipeDraftStatus next)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(draftId);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalSha256);
        return _lock.Execute(() =>
        {
            var document = LoadCore();
            var index = IndexOf(document, draftId);
            if (index < 0)
            {
                throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.NotFound);
            }

            var advanced = Transition(document.Records[index], canonicalSha256, required, next, DateTimeOffset.UtcNow);
            document.Records[index] = advanced;
            Persist(document);
            return advanced;
        });
    }

    /// <summary>The one hash-bound state transition every advance and the supersede migration go through.</summary>
    private static RecipeDraftRecord Transition(
        RecipeDraftRecord current,
        string canonicalSha256,
        RecipeDraftStatus required,
        RecipeDraftStatus next,
        DateTimeOffset updatedUtc)
    {
        if (current.Status != required)
        {
            throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.InvalidStatus);
        }

        if (!string.Equals(current.CanonicalSha256, canonicalSha256, StringComparison.Ordinal))
        {
            throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.HashMismatch);
        }

        return current.WithStatus(next, updatedUtc);
    }

    /// <summary>
    /// Level-1 trim. Removes the oldest unprotected version until the lineage fits, re-parenting the version above
    /// each victim to the victim's parent so the chain stays linear (REQ-004-34). Fails closed when only protected
    /// versions remain rather than dropping any of them.
    /// </summary>
    private static List<string> TrimLineage(RecipeDraftStoreDocument document, string lineageId, string headDraftId)
    {
        var trimmed = new List<string>();
        while (true)
        {
            var versions = document.Lineage(lineageId);
            var bytes = versions.Sum(RecipeDraftCodec.PersistedRecipeJsonBytes);
            if (versions.Count <= RecipeDraftLineageLimits.MaximumVersionsPerLineage &&
                bytes <= RecipeDraftLineageLimits.MaximumLineageRecipeJsonBytes)
            {
                return trimmed;
            }

            var victimIndex = versions.FindIndex(version =>
                !string.Equals(version.DraftId, headDraftId, StringComparison.Ordinal) && !IsProtected(version.Status));
            if (victimIndex < 0)
            {
                throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.LineageCapacityExhausted);
            }

            var victim = versions[victimIndex];
            var child = versions[victimIndex + 1];
            document.Records[IndexOf(document, child.DraftId)] = child.WithParentDraftId(victim.ParentDraftId);
            document.Records.RemoveAt(IndexOf(document, victim.DraftId));
            trimmed.Add(victim.DraftId);
        }
    }

    /// <summary>Level-2 eviction: whole lineages, least recently updated first; the lineage being saved is never a candidate.</summary>
    private static (IReadOnlyList<string> LineageIds, int VersionCount) EvictLineages(
        RecipeDraftStoreDocument document,
        string retainedLineageId)
    {
        var evicted = new List<string>();
        var evictedVersions = 0;
        var candidates = document.RevisionWatermarks.Keys
            .Where(lineageId => !string.Equals(lineageId, retainedLineageId, StringComparison.Ordinal))
            .Select(lineageId => (LineageId: lineageId, LastActivity: document.Lineage(lineageId).Max(static record => record.UpdatedUtc)))
            .OrderBy(static candidate => candidate.LastActivity)
            .ThenBy(static candidate => candidate.LineageId, StringComparer.Ordinal)
            .Select(static candidate => candidate.LineageId)
            .ToList();
        foreach (var lineageId in candidates)
        {
            if (document.RevisionWatermarks.Count <= RecipeDraftLineageLimits.MaximumLineages)
            {
                break;
            }

            evictedVersions += document.Records.RemoveAll(record => string.Equals(record.LineageId, lineageId, StringComparison.Ordinal));
            document.RevisionWatermarks.Remove(lineageId);
            evicted.Add(lineageId);
        }

        return (evicted, evictedVersions);
    }

    private static bool IsProtected(RecipeDraftStatus status) => status is
        RecipeDraftStatus.ConfirmedAwaitingBuild or
        RecipeDraftStatus.Built or
        RecipeDraftStatus.BuildFailed or
        RecipeDraftStatus.Superseded;

    private static int IndexOf(RecipeDraftStoreDocument document, string draftId) =>
        document.Records.FindIndex(record => string.Equals(record.DraftId, draftId, StringComparison.Ordinal));

    private bool AnyStoreFile() => File.Exists(_storePath) || File.Exists(_backupPath);

    private RecipeDraftStoreDocument LoadCore()
    {
        var primaryExists = File.Exists(_storePath);
        var backupExists = File.Exists(_backupPath);
        if (!primaryExists && !backupExists)
        {
            return RecipeDraftStoreDocument.Empty();
        }

        // An unsupported version propagates from TryRead untouched: it is not corruption, so the backup is not
        // consulted and neither copy is rewritten, renamed or deleted (REQ-004 §7.4).
        if (primaryExists && TryRead(_storePath, out var primary))
        {
            return primary!;
        }

        if (backupExists && TryRead(_backupPath, out var backup))
        {
            return backup!;
        }

        // Both copies exist but neither parses: fail closed instead of silently discarding retained drafts.
        throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.StorageFailed);
    }

    private void Persist(RecipeDraftStoreDocument document)
    {
        try
        {
            AtomicFileWriter.WriteReplace(_storePath, _backupPath, RecipeDraftCodec.Serialize(document));
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

    private static bool TryRead(string path, out RecipeDraftStoreDocument? document)
    {
        document = null;
        try
        {
            document = RecipeDraftCodec.Deserialize(AtomicFileWriter.ReadBounded(path, MaximumFileBytes));
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
