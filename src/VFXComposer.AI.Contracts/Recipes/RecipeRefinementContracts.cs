using System.Collections.ObjectModel;
using System.Text;
using VFXComposer.AI.Contracts.Chat;

namespace VFXComposer.AI.Contracts.Recipes;

/// <summary>
/// One explicit "refine" action on the one bound ChatLlm route (REQ-004 §6). The request carries the anchored
/// triple — the lineage's original description, the current head (inside <see cref="Lineage"/>), and this
/// round's feedback — plus the retained lineage itself, which the override guard walks for hand-tuned values.
/// Neither the description nor the feedback ever reaches diagnostics.
/// </summary>
public sealed class RecipeRefinementRequest
{
    /// <summary>
    /// <paramref name="lineage"/> is the retained version chain oldest-first, exactly as
    /// <see cref="IRecipeDraftLineageStore.ListLineage"/> returns it; its last record is the head being refined
    /// and must carry a canonical hash. An empty, unlinked or hash-less chain is refused here, before any
    /// assembly or network work.
    /// </summary>
    public RecipeRefinementRequest(
        string correlationId,
        string originalDescription,
        IReadOnlyList<RecipeDraftRecord> lineage,
        string feedbackText,
        int retryLimit = RecipeChannelLimits.DefaultRetryLimit)
    {
        CorrelationId = AiContractGuard.CorrelationId(correlationId, nameof(correlationId));
        OriginalDescription = GuardBoundedText(originalDescription, nameof(originalDescription));
        FeedbackText = GuardBoundedText(feedbackText, nameof(feedbackText));
        Lineage = GuardLineage(lineage);
        if (retryLimit is < 0 or > RecipeChannelLimits.MaximumRetryLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(retryLimit));
        }

        RetryLimit = retryLimit;
    }

    public string ContractVersion => AiContractVersions.RecipeRefinementRequest;
    public string CorrelationId { get; }

    /// <summary>The lineage's first-version user description; immutable across the whole chain (REQ-004 §6.1).</summary>
    public string OriginalDescription { get; }

    /// <summary>The retained chain, oldest first. The guard reads hand-edited values from it.</summary>
    public IReadOnlyList<RecipeDraftRecord> Lineage { get; }

    /// <summary>This round's feedback. Earlier rounds are never carried (REQ-004-13).</summary>
    public string FeedbackText { get; }

    public int RetryLimit { get; }

    /// <summary>The version being refined: the last retained record of the chain.</summary>
    public RecipeDraftRecord Head => Lineage[^1];

    public override string ToString() => "RecipeRefinementRequest(<redacted>)";

    private static string GuardBoundedText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.IndexOf('\0') >= 0 ||
            Encoding.UTF8.GetByteCount(value) > RecipeChannelLimits.MaximumDescriptionUtf8Bytes)
        {
            throw new ArgumentException("Refinement text is invalid.", parameterName);
        }

        return value;
    }

    private static IReadOnlyList<RecipeDraftRecord> GuardLineage(IReadOnlyList<RecipeDraftRecord> lineage)
    {
        ArgumentNullException.ThrowIfNull(lineage);
        if (lineage.Count == 0 || lineage.Any(static record => record is null))
        {
            throw new ArgumentException("The lineage is invalid.", nameof(lineage));
        }

        for (var index = 1; index < lineage.Count; index++)
        {
            if (!string.Equals(lineage[index].ParentDraftId, lineage[index - 1].DraftId, StringComparison.Ordinal) ||
                !string.Equals(lineage[index].LineageId, lineage[index - 1].LineageId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The lineage must be the retained chain oldest-first with linked parents.",
                    nameof(lineage));
            }
        }

        if (lineage[^1].CanonicalSha256 is null)
        {
            throw new ArgumentException("The lineage head must carry a canonical hash.", nameof(lineage));
        }

        return new ReadOnlyCollection<RecipeDraftRecord>(lineage.ToArray());
    }
}

/// <summary>
/// One parameter the override guard restored in this round, with both value literals so the confirmation panel
/// and the timeline can show what was kept and what the AI wrote (REQ-004-48). The persisted, bounded form is
/// <see cref="RecipeGuardRestoration"/>, produced by <see cref="ToGuardRestoration"/>.
/// </summary>
public sealed class RecipeRefinementGuardRestoration
{
    public RecipeRefinementGuardRestoration(
        string parameterPath,
        string sourceDraftId,
        string aiValueLiteral,
        string restoredValueLiteral)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterPath, nameof(parameterPath));
        ArgumentException.ThrowIfNullOrWhiteSpace(aiValueLiteral, nameof(aiValueLiteral));
        ArgumentException.ThrowIfNullOrWhiteSpace(restoredValueLiteral, nameof(restoredValueLiteral));
        if (parameterPath.Length > RecipeDraftLineageLimits.MaximumGuardRestorationPathLength ||
            AiContractGuard.HasControl(parameterPath) ||
            aiValueLiteral.Length > 256 ||
            restoredValueLiteral.Length > 256)
        {
            throw new ArgumentException("Guard restoration is invalid.", nameof(parameterPath));
        }

        ParameterPath = parameterPath;
        SourceDraftId = AiContractGuard.Identifier(sourceDraftId, nameof(sourceDraftId));
        AiValueLiteral = aiValueLiteral;
        RestoredValueLiteral = restoredValueLiteral;
    }

    public string ParameterPath { get; }

    /// <summary>The <c>human_edit</c> version whose value was restored.</summary>
    public string SourceDraftId { get; }

    /// <summary>The value the AI wrote before the guard restored the hand-tuned one.</summary>
    public string AiValueLiteral { get; }

    public string RestoredValueLiteral { get; }

    /// <summary>The persisted contract shape (parameter path + source version).</summary>
    public RecipeGuardRestoration ToGuardRestoration() => new(ParameterPath, SourceDraftId);

    public override string ToString() => "RecipeRefinementGuardRestoration(" + ParameterPath + ")";
}

/// <summary>
/// Typed result of one refine action. A refined outcome carries everything the caller needs to persist the one
/// <c>ai_refine</c> version: <see cref="ToRevision"/> plus the parent coordinates feed
/// <see cref="IRecipeDraftLineageStore.AppendVersion"/> directly; the guard already ran, so persisting is the
/// only step left and no second version ever exists (REQ-004-45). Failure outcomes mirror
/// <see cref="RecipeGenerationResult"/>: a validation failure keeps the last output and its full report, and a
/// channel failure keeps the stable error code with no draft and no retry.
/// </summary>
public sealed class RecipeRefinementResult
{
    private RecipeRefinementResult(
        RecipeGenerationOutcome outcome,
        string correlationId,
        RecipeDraft? refinedDraft,
        string? parentDraftId,
        string? parentCanonicalSha256,
        string? feedbackText,
        IReadOnlyList<RecipeRefinementGuardRestoration> guardRestorations,
        string? lastOutputText,
        IReadOnlyList<RecipeValidationIssue> issues,
        IReadOnlyList<RecipeGenerationAttempt> attempts,
        ChatChannelErrorCode? channelError,
        string promptTemplateVersion,
        string templateCatalogVersion)
    {
        Outcome = outcome;
        CorrelationId = correlationId;
        RefinedDraft = refinedDraft;
        ParentDraftId = parentDraftId;
        ParentCanonicalSha256 = parentCanonicalSha256;
        FeedbackText = feedbackText;
        GuardRestorations = guardRestorations;
        LastOutputText = lastOutputText;
        Issues = issues;
        Attempts = attempts;
        ChannelError = channelError;
        PromptTemplateVersion = promptTemplateVersion;
        TemplateCatalogVersion = templateCatalogVersion;
    }

    public RecipeGenerationOutcome Outcome { get; }
    public string CorrelationId { get; }

    /// <summary>The guarded refined content; present exactly on <see cref="RecipeGenerationOutcome.Drafted"/>.</summary>
    public RecipeDraft? RefinedDraft { get; }

    /// <summary>The refined head's identifier: the parent of the version to append.</summary>
    public string? ParentDraftId { get; }

    /// <summary>The refined head's canonical hash, which <c>AppendVersion</c> re-verifies.</summary>
    public string? ParentCanonicalSha256 { get; }

    /// <summary>This round's feedback, carried into the persisted <c>ai_refine</c> version.</summary>
    public string? FeedbackText { get; }

    /// <summary>Every restoration the guard applied, ordinal-ordered by path, with both value literals.</summary>
    public IReadOnlyList<RecipeRefinementGuardRestoration> GuardRestorations { get; }

    /// <summary>The last extracted output of a failed round, kept for inspection (REQ-004-17).</summary>
    public string? LastOutputText { get; }

    public IReadOnlyList<RecipeValidationIssue> Issues { get; }
    public IReadOnlyList<RecipeGenerationAttempt> Attempts { get; }
    public ChatChannelErrorCode? ChannelError { get; }
    public string PromptTemplateVersion { get; }
    public string TemplateCatalogVersion { get; }
    public int RequestCount => Attempts.Count;

    public override string ToString() => "RecipeRefinementResult(" + Outcome + ")";

    /// <summary>
    /// The pending <c>ai_refine</c> version of a refined outcome, ready for
    /// <c>AppendVersion(ParentDraftId, ParentCanonicalSha256, ToRevision(), utcNow)</c>. Any other outcome is an
    /// <see cref="InvalidOperationException"/>: failures never land a version (REQ-004-17/18).
    /// </summary>
    public RecipeDraftRevision ToRevision()
    {
        if (Outcome != RecipeGenerationOutcome.Drafted || RefinedDraft is null)
        {
            throw new InvalidOperationException("Only a refined outcome can become a version.");
        }

        return new RecipeDraftRevision(
            RefinedDraft,
            RecipeDraftOrigin.AiRefine,
            RequestCount,
            FeedbackText,
            GuardRestorations.Select(static restoration => restoration.ToGuardRestoration()));
    }

    public static RecipeRefinementResult Refined(
        RecipeDraft refinedDraft,
        string parentDraftId,
        string parentCanonicalSha256,
        string feedbackText,
        IEnumerable<RecipeRefinementGuardRestoration> guardRestorations,
        IEnumerable<RecipeGenerationAttempt> attempts)
    {
        ArgumentNullException.ThrowIfNull(refinedDraft);
        ArgumentException.ThrowIfNullOrWhiteSpace(feedbackText);
        return new RecipeRefinementResult(
            RecipeGenerationOutcome.Drafted,
            refinedDraft.CorrelationId,
            refinedDraft,
            AiContractGuard.Identifier(parentDraftId, nameof(parentDraftId)),
            RecipeDraft.GuardSha256(parentCanonicalSha256, nameof(parentCanonicalSha256)),
            feedbackText,
            CopyRestorations(guardRestorations),
            lastOutputText: null,
            Array.Empty<RecipeValidationIssue>(),
            CopyAttempts(attempts, requireAny: true),
            channelError: null,
            refinedDraft.PromptTemplateVersion,
            refinedDraft.TemplateCatalogVersion);
    }

    public static RecipeRefinementResult ValidationFailed(
        string correlationId,
        string? lastOutputText,
        IEnumerable<RecipeValidationIssue> issues,
        IEnumerable<RecipeGenerationAttempt> attempts,
        string promptTemplateVersion,
        string templateCatalogVersion)
    {
        var copiedIssues = CopyIssues(issues);
        if (copiedIssues.Count == 0)
        {
            throw new ArgumentException("A failed validation result requires at least one issue.", nameof(issues));
        }

        return new RecipeRefinementResult(
            RecipeGenerationOutcome.ValidationFailed,
            AiContractGuard.CorrelationId(correlationId, nameof(correlationId)),
            refinedDraft: null,
            parentDraftId: null,
            parentCanonicalSha256: null,
            feedbackText: null,
            Array.Empty<RecipeRefinementGuardRestoration>(),
            GuardOutputText(lastOutputText),
            copiedIssues,
            CopyAttempts(attempts, requireAny: true),
            channelError: null,
            promptTemplateVersion,
            templateCatalogVersion);
    }

    public static RecipeRefinementResult ChannelFailed(
        string correlationId,
        ChatChannelErrorCode channelError,
        IEnumerable<RecipeGenerationAttempt> attempts,
        string promptTemplateVersion,
        string templateCatalogVersion)
    {
        var outcome = channelError == ChatChannelErrorCode.Cancelled
            ? RecipeGenerationOutcome.Cancelled
            : RecipeGenerationOutcome.ChannelFailed;
        return new RecipeRefinementResult(
            outcome,
            AiContractGuard.CorrelationId(correlationId, nameof(correlationId)),
            refinedDraft: null,
            parentDraftId: null,
            parentCanonicalSha256: null,
            feedbackText: null,
            Array.Empty<RecipeRefinementGuardRestoration>(),
            lastOutputText: null,
            Array.Empty<RecipeValidationIssue>(),
            CopyAttempts(attempts, requireAny: false),
            channelError,
            promptTemplateVersion,
            templateCatalogVersion);
    }

    private static string? GuardOutputText(string? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value.Length is 0 or > RecipeChannelLimits.MaximumDraftJsonCharacters || value.IndexOf('\0') >= 0)
        {
            throw new ArgumentException("Last output text is invalid.", nameof(value));
        }

        return value;
    }

    private static IReadOnlyList<RecipeRefinementGuardRestoration> CopyRestorations(
        IEnumerable<RecipeRefinementGuardRestoration> restorations)
    {
        ArgumentNullException.ThrowIfNull(restorations);
        var copied = restorations.ToArray();
        if (copied.Any(static restoration => restoration is null))
        {
            throw new ArgumentException("Guard restoration list is invalid.", nameof(restorations));
        }

        return new ReadOnlyCollection<RecipeRefinementGuardRestoration>(copied);
    }

    private static IReadOnlyList<RecipeValidationIssue> CopyIssues(IEnumerable<RecipeValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        var copied = issues.ToArray();
        if (copied.Length > 1024 || copied.Any(static issue => issue is null))
        {
            throw new ArgumentException("Issue list is invalid.", nameof(issues));
        }

        return new ReadOnlyCollection<RecipeValidationIssue>(copied);
    }

    private static IReadOnlyList<RecipeGenerationAttempt> CopyAttempts(
        IEnumerable<RecipeGenerationAttempt> attempts,
        bool requireAny)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        var copied = attempts.ToArray();
        if (copied.Any(static attempt => attempt is null) ||
            copied.Length > 1 + RecipeChannelLimits.MaximumRetryLimit ||
            (requireAny && copied.Length == 0))
        {
            throw new ArgumentException("Attempt list is invalid.", nameof(attempts));
        }

        return new ReadOnlyCollection<RecipeGenerationAttempt>(copied);
    }
}

/// <summary>
/// The feature-facing refinement channel (REQ-004 §6). It uses only the one persisted ChatLlm binding, performs
/// no route selection or fallback, sends requests only inside an explicit refine action, and applies the
/// override guard before returning, so the caller's only remaining step is persisting the version.
/// </summary>
public interface IRecipeRefinementChannel
{
    ValueTask<RecipeRefinementResult> RefineAsync(
        RecipeRefinementRequest request,
        CancellationToken cancellationToken = default);
}
