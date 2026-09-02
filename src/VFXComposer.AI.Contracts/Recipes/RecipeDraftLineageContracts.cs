using System.Collections.ObjectModel;
using System.Text;

namespace VFXComposer.AI.Contracts.Recipes;

/// <summary>
/// Bounds of the draft version chain (REQ-004 §7.5). Level 1 caps one lineage by version count and by the
/// persisted recipe JSON bytes of all its versions; level 2 caps the number of lineages in the store.
/// </summary>
public static class RecipeDraftLineageLimits
{
    public const int MaximumVersionsPerLineage = 16;

    /// <summary>Measured as the UTF-8 bytes the store writes for the <c>recipeJson</c> field, summed over the lineage.</summary>
    public const int MaximumLineageRecipeJsonBytes = 1024 * 1024;

    public const int MaximumLineages = 8;

    /// <summary>Guard restorations beyond this count are dropped from the list; the total is kept as a count.</summary>
    public const int MaximumGuardRestorations = 64;

    public const int MaximumGuardRestorationPathLength = 256;

    public const int MaximumFeedbackTextUtf8Bytes = RecipeChannelLimits.MaximumDescriptionUtf8Bytes;
}

/// <summary>Closed set of version origins (REQ-004 §7.2). The wire names live in <see cref="RecipeDraftOriginNames"/>.</summary>
public enum RecipeDraftOrigin
{
    /// <summary>A zero-AI preset skeleton applied from a simple-mode card; always a lineage root.</summary>
    Preset,

    /// <summary>The first AI generation of a lineage.</summary>
    AiDraft,

    /// <summary>An AI refinement of the parent version driven by user feedback.</summary>
    AiRefine,

    /// <summary>A user edit of the parent version made without any AI request.</summary>
    HumanEdit,
}

/// <summary>The persisted spelling of each origin; unknown spellings fail closed on read (REQ-004-24).</summary>
public static class RecipeDraftOriginNames
{
    public const string Preset = "preset";
    public const string AiDraft = "ai_draft";
    public const string AiRefine = "ai_refine";
    public const string HumanEdit = "human_edit";

    public static string Of(RecipeDraftOrigin origin) => origin switch
    {
        RecipeDraftOrigin.Preset => Preset,
        RecipeDraftOrigin.AiDraft => AiDraft,
        RecipeDraftOrigin.AiRefine => AiRefine,
        RecipeDraftOrigin.HumanEdit => HumanEdit,
        _ => throw new ArgumentOutOfRangeException(nameof(origin)),
    };

    public static bool TryParse(string? name, out RecipeDraftOrigin origin)
    {
        switch (name)
        {
            case Preset:
                origin = RecipeDraftOrigin.Preset;
                return true;
            case AiDraft:
                origin = RecipeDraftOrigin.AiDraft;
                return true;
            case AiRefine:
                origin = RecipeDraftOrigin.AiRefine;
                return true;
            case HumanEdit:
                origin = RecipeDraftOrigin.HumanEdit;
                return true;
            default:
                origin = default;
                return false;
        }
    }
}

/// <summary>
/// One parameter the refinement override guard restored in a version: the parameter path and the version whose
/// human-edited value was restored (REQ-004 §7.2, §9.3).
/// </summary>
public sealed class RecipeGuardRestoration
{
    public RecipeGuardRestoration(string parameterPath, string sourceDraftId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterPath, nameof(parameterPath));
        if (parameterPath.Length > RecipeDraftLineageLimits.MaximumGuardRestorationPathLength ||
            AiContractGuard.HasControl(parameterPath))
        {
            throw new ArgumentException("Guard restoration path is invalid.", nameof(parameterPath));
        }

        ParameterPath = parameterPath;
        SourceDraftId = AiContractGuard.Identifier(sourceDraftId, nameof(sourceDraftId));
    }

    public string ParameterPath { get; }
    public string SourceDraftId { get; }

    public override string ToString() => "RecipeGuardRestoration(" + ParameterPath + ")";
}

/// <summary>
/// Where a version sits in its chain and where it came from (REQ-004 §7.2). Origin-conditional fields are
/// enforced here: only <see cref="RecipeDraftOrigin.AiRefine"/> carries feedback and guard restorations, only
/// <see cref="RecipeDraftOrigin.Preset"/> carries a preset identifier.
/// </summary>
public sealed class RecipeDraftProvenance
{
    public RecipeDraftProvenance(
        string lineageId,
        string? parentDraftId,
        int revisionOrdinal,
        RecipeDraftOrigin origin,
        string? feedbackText = null,
        IEnumerable<RecipeGuardRestoration>? guardRestorations = null,
        int guardRestorationCount = 0,
        string? presetId = null)
    {
        LineageId = AiContractGuard.Identifier(lineageId, nameof(lineageId));
        ParentDraftId = parentDraftId is null ? null : AiContractGuard.Identifier(parentDraftId, nameof(parentDraftId));
        if (revisionOrdinal < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(revisionOrdinal));
        }

        if (!Enum.IsDefined(origin))
        {
            throw new ArgumentOutOfRangeException(nameof(origin));
        }

        RevisionOrdinal = revisionOrdinal;
        Origin = origin;
        FeedbackText = GuardFeedbackText(origin, feedbackText);
        GuardRestorations = GuardRestorationList(origin, guardRestorations);
        if (guardRestorationCount < GuardRestorations.Count ||
            (origin != RecipeDraftOrigin.AiRefine && guardRestorationCount != 0))
        {
            throw new ArgumentException("Guard restoration count is invalid.", nameof(guardRestorationCount));
        }

        GuardRestorationCount = guardRestorationCount;
        PresetId = GuardPresetId(origin, presetId);
    }

    public string LineageId { get; }

    /// <summary>The previous version in the chain; null for the oldest retained version.</summary>
    public string? ParentDraftId { get; }

    /// <summary>Strictly increasing inside the lineage; never reused after truncation or trim.</summary>
    public int RevisionOrdinal { get; }

    public RecipeDraftOrigin Origin { get; }

    /// <summary>The user's refinement feedback; only present on <see cref="RecipeDraftOrigin.AiRefine"/> versions.</summary>
    public string? FeedbackText { get; }

    /// <summary>At most <see cref="RecipeDraftLineageLimits.MaximumGuardRestorations"/> entries.</summary>
    public IReadOnlyList<RecipeGuardRestoration> GuardRestorations { get; }

    /// <summary>The total number of restorations, including any dropped from <see cref="GuardRestorations"/>.</summary>
    public int GuardRestorationCount { get; }

    /// <summary>The applied preset; only present on <see cref="RecipeDraftOrigin.Preset"/> versions.</summary>
    public string? PresetId { get; }

    public override string ToString() => "RecipeDraftProvenance(" + LineageId + "," + RevisionOrdinal + ")";

    /// <summary>The provenance of a lineage's first version.</summary>
    public static RecipeDraftProvenance Root(string lineageId, RecipeDraftOrigin origin, string? presetId = null) =>
        new(lineageId, parentDraftId: null, revisionOrdinal: 1, origin, presetId: presetId);

    /// <summary>The provenance of the same version after the chain below it was spliced by a trim.</summary>
    public RecipeDraftProvenance WithParentDraftId(string? parentDraftId) => new(
        LineageId,
        parentDraftId,
        RevisionOrdinal,
        Origin,
        FeedbackText,
        GuardRestorations,
        GuardRestorationCount,
        PresetId);

    /// <summary>Produces a fresh lineage identifier for a new chain root.</summary>
    public static string NewLineageId() => "lineage-" + Guid.NewGuid().ToString("N");

    internal static string? GuardFeedbackText(RecipeDraftOrigin origin, string? feedbackText)
    {
        if (origin != RecipeDraftOrigin.AiRefine)
        {
            if (feedbackText is not null)
            {
                throw new ArgumentException("Feedback text is only allowed on AI refinement versions.", nameof(feedbackText));
            }

            return null;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(feedbackText, nameof(feedbackText));
        if (feedbackText.IndexOf('\0') >= 0 ||
            Encoding.UTF8.GetByteCount(feedbackText) > RecipeDraftLineageLimits.MaximumFeedbackTextUtf8Bytes)
        {
            throw new ArgumentException("Feedback text is invalid.", nameof(feedbackText));
        }

        return feedbackText;
    }

    internal static string? GuardPresetId(RecipeDraftOrigin origin, string? presetId)
    {
        if (origin != RecipeDraftOrigin.Preset)
        {
            if (presetId is not null)
            {
                throw new ArgumentException("A preset identifier is only allowed on preset versions.", nameof(presetId));
            }

            return null;
        }

        return AiContractGuard.Identifier(presetId!, nameof(presetId));
    }

    private static IReadOnlyList<RecipeGuardRestoration> GuardRestorationList(
        RecipeDraftOrigin origin,
        IEnumerable<RecipeGuardRestoration>? guardRestorations)
    {
        var copied = AiContractGuard.CopyList(
            guardRestorations ?? Array.Empty<RecipeGuardRestoration>(),
            nameof(guardRestorations),
            RecipeDraftLineageLimits.MaximumGuardRestorations);
        if (origin != RecipeDraftOrigin.AiRefine && copied.Count != 0)
        {
            throw new ArgumentException("Guard restorations are only allowed on AI refinement versions.", nameof(guardRestorations));
        }

        return copied;
    }
}

/// <summary>
/// The content of one new version appended to an existing chain (AI refinement or human edit). Presets never
/// append: they always start a new lineage. An unbounded restoration list is bounded here: the first
/// <see cref="RecipeDraftLineageLimits.MaximumGuardRestorations"/> entries are kept and the total is recorded.
/// </summary>
public sealed class RecipeDraftRevision
{
    public RecipeDraftRevision(
        RecipeDraft draft,
        RecipeDraftOrigin origin,
        int requestCount = 0,
        string? feedbackText = null,
        IEnumerable<RecipeGuardRestoration>? guardRestorations = null)
    {
        Draft = draft ?? throw new ArgumentNullException(nameof(draft));
        if (!Enum.IsDefined(origin))
        {
            throw new ArgumentOutOfRangeException(nameof(origin));
        }

        if (origin == RecipeDraftOrigin.Preset)
        {
            throw new ArgumentException("A preset always starts a new lineage and never appends to one.", nameof(origin));
        }

        if (requestCount is < 0 or > 1 + RecipeChannelLimits.MaximumRetryLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(requestCount));
        }

        Origin = origin;
        RequestCount = requestCount;
        FeedbackText = RecipeDraftProvenance.GuardFeedbackText(origin, feedbackText);

        var restorations = (guardRestorations ?? Array.Empty<RecipeGuardRestoration>()).ToArray();
        if (restorations.Any(static restoration => restoration is null))
        {
            throw new ArgumentException("Guard restoration list is invalid.", nameof(guardRestorations));
        }

        if (origin != RecipeDraftOrigin.AiRefine && restorations.Length != 0)
        {
            throw new ArgumentException("Guard restorations are only allowed on AI refinement versions.", nameof(guardRestorations));
        }

        GuardRestorationCount = restorations.Length;
        GuardRestorations = new ReadOnlyCollection<RecipeGuardRestoration>(
            restorations.Take(RecipeDraftLineageLimits.MaximumGuardRestorations).ToArray());
    }

    public RecipeDraft Draft { get; }
    public RecipeDraftOrigin Origin { get; }
    public int RequestCount { get; }
    public string? FeedbackText { get; }
    public IReadOnlyList<RecipeGuardRestoration> GuardRestorations { get; }
    public int GuardRestorationCount { get; }

    public override string ToString() => "RecipeDraftRevision(" + Origin + ")";
}

/// <summary>
/// Typed result of persisting a version. Retention never happens silently: every version trimmed inside the
/// lineage, every lineage evicted from the store and every confirmation superseded by the new version is listed
/// so the caller can surface it (REQ-004-33).
/// </summary>
public sealed class RecipeDraftSaveOutcome
{
    public RecipeDraftSaveOutcome(
        RecipeDraftRecord record,
        IEnumerable<string> supersededDraftIds,
        IEnumerable<string> trimmedDraftIds,
        IEnumerable<string> evictedLineageIds,
        int evictedVersionCount)
    {
        Record = record ?? throw new ArgumentNullException(nameof(record));
        SupersededDraftIds = CopyIds(supersededDraftIds, nameof(supersededDraftIds));
        TrimmedDraftIds = CopyIds(trimmedDraftIds, nameof(trimmedDraftIds));
        EvictedLineageIds = CopyIds(evictedLineageIds, nameof(evictedLineageIds));
        if (evictedVersionCount < EvictedLineageIds.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(evictedVersionCount));
        }

        EvictedVersionCount = evictedVersionCount;
    }

    /// <summary>The persisted new version.</summary>
    public RecipeDraftRecord Record { get; }

    /// <summary>Confirmed-awaiting-build versions of the same lineage that became <see cref="RecipeDraftStatus.Superseded"/>.</summary>
    public IReadOnlyList<string> SupersededDraftIds { get; }

    /// <summary>Versions the level-1 lineage cap removed.</summary>
    public IReadOnlyList<string> TrimmedDraftIds { get; }

    /// <summary>Whole lineages the level-2 store cap removed.</summary>
    public IReadOnlyList<string> EvictedLineageIds { get; }

    /// <summary>The number of versions removed together with <see cref="EvictedLineageIds"/>.</summary>
    public int EvictedVersionCount { get; }

    /// <summary>True when the save changed nothing beyond adding the version.</summary>
    public bool RetainedEverything =>
        SupersededDraftIds.Count == 0 && TrimmedDraftIds.Count == 0 && EvictedLineageIds.Count == 0;

    public override string ToString() => "RecipeDraftSaveOutcome(" + Record.DraftId + ")";

    internal static IReadOnlyList<string> CopyIds(IEnumerable<string> ids, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(ids, parameterName);
        var copied = ids.ToArray();
        if (copied.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Identifier list is invalid.", parameterName);
        }

        return new ReadOnlyCollection<string>(copied);
    }
}

/// <summary>Typed result of a truncation: the new head and every version deleted after it (REQ-004-25).</summary>
public sealed class RecipeDraftTruncateOutcome
{
    public RecipeDraftTruncateOutcome(RecipeDraftRecord head, IEnumerable<string> removedDraftIds)
    {
        Head = head ?? throw new ArgumentNullException(nameof(head));
        RemovedDraftIds = RecipeDraftSaveOutcome.CopyIds(removedDraftIds, nameof(removedDraftIds));
    }

    public RecipeDraftRecord Head { get; }
    public IReadOnlyList<string> RemovedDraftIds { get; }

    public override string ToString() => "RecipeDraftTruncateOutcome(" + Head.DraftId + ")";
}

/// <summary>
/// The version-chain surface of the draft store (REQ-004 §7). <see cref="IRecipeDraftStore.Save"/> remains the
/// entry-neutral way to start a lineage; the members here expose the chain and make retention visible.
/// </summary>
public interface IRecipeDraftLineageStore : IRecipeDraftStore
{
    /// <summary>
    /// Persists a lineage root (first version) and reports any lineage the level-2 cap evicted. A record whose
    /// parent is set, whose ordinal is not 1, or whose draft or lineage identifier already exists is refused with
    /// RecordInvalid: chain positions are assigned only by <see cref="AppendVersion"/>.
    /// </summary>
    RecipeDraftSaveOutcome SaveVersion(RecipeDraftRecord record);

    /// <summary>
    /// Appends a pending version after the lineage head. The parent must exist, carry a canonical hash equal to
    /// <paramref name="parentCanonicalSha256"/> and be the head; the lineage's confirmed-awaiting-build version,
    /// if any, becomes <see cref="RecipeDraftStatus.Superseded"/>. The level-1 cap trims the oldest unprotected
    /// versions and refuses the append when only protected versions remain.
    /// </summary>
    RecipeDraftSaveOutcome AppendVersion(
        string parentDraftId,
        string parentCanonicalSha256,
        RecipeDraftRevision revision,
        DateTimeOffset createdUtc);

    /// <summary>
    /// Makes <paramref name="draftId"/> the head of its lineage by deleting every later version. Refused with
    /// TruncationBlocked when a later version is confirmed, built or build-failed.
    /// </summary>
    RecipeDraftTruncateOutcome TruncateAfter(string draftId);

    /// <summary>Every retained version of one lineage, oldest first; empty for an unknown lineage.</summary>
    IReadOnlyList<RecipeDraftRecord> ListLineage(string lineageId);
}
