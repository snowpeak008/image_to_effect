using VFXComposer.AI.Contracts.Recipes;

namespace VFXComposer.AI.Providers.Recipes;

/// <summary>
/// The in-memory shape of one store file: every retained version plus, per lineage, the highest revision ordinal
/// ever assigned. The watermark outlives truncation so ordinals are never reused (REQ-004-23).
/// </summary>
internal sealed class RecipeDraftStoreDocument
{
    public RecipeDraftStoreDocument(List<RecipeDraftRecord> records, Dictionary<string, int> revisionWatermarks)
    {
        Records = records ?? throw new ArgumentNullException(nameof(records));
        RevisionWatermarks = revisionWatermarks ?? throw new ArgumentNullException(nameof(revisionWatermarks));
    }

    public List<RecipeDraftRecord> Records { get; }

    /// <summary>Lineage id → highest ordinal ever assigned in that lineage.</summary>
    public Dictionary<string, int> RevisionWatermarks { get; }

    public static RecipeDraftStoreDocument Empty() => new([], new Dictionary<string, int>(StringComparer.Ordinal));

    /// <summary>The versions of one lineage, oldest ordinal first.</summary>
    public List<RecipeDraftRecord> Lineage(string lineageId) => Records
        .Where(record => string.Equals(record.LineageId, lineageId, StringComparison.Ordinal))
        .OrderBy(static record => record.RevisionOrdinal)
        .ToList();

    /// <summary>
    /// Rejects any document that is not a set of linear chains: unique draft identifiers, one root per lineage,
    /// every other version parented to its ordinal predecessor, and a watermark at or above every ordinal. A
    /// hand-edited or foreign file can therefore never smuggle a branch or an orphan into memory.
    /// </summary>
    public void ThrowIfInvalid()
    {
        var draftIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in Records)
        {
            if (!draftIds.Add(record.DraftId))
            {
                throw new InvalidDataException("Recipe draft storage is invalid.");
            }
        }

        var lineageIds = new HashSet<string>(Records.Select(static record => record.LineageId), StringComparer.Ordinal);
        if (lineageIds.Count != RevisionWatermarks.Count || !lineageIds.SetEquals(RevisionWatermarks.Keys))
        {
            throw new InvalidDataException("Recipe draft storage is invalid.");
        }

        foreach (var lineageId in lineageIds)
        {
            var versions = Lineage(lineageId);
            var watermark = RevisionWatermarks[lineageId];
            for (var index = 0; index < versions.Count; index++)
            {
                var version = versions[index];
                var expectedParent = index == 0 ? null : versions[index - 1].DraftId;
                if (version.RevisionOrdinal > watermark ||
                    (index > 0 && version.RevisionOrdinal <= versions[index - 1].RevisionOrdinal) ||
                    !string.Equals(version.ParentDraftId, expectedParent, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Recipe draft storage is invalid.");
                }
            }
        }
    }
}
