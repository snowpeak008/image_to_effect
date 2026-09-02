using System.Collections.ObjectModel;
using System.Text;
using VFXComposer.AI.Contracts.Chat;

namespace VFXComposer.AI.Contracts.Recipes;

/// <summary>
/// Bounds for the structured Recipe generation channel. The retry budget implements ADR-007 §2.5: one explicit
/// generate action authorizes at most 1 + N requests on the already resolved ChatLlm route, N ≤ 5 and 2 by default,
/// triggered only by schema/L1 validation failures.
/// </summary>
public static class RecipeChannelLimits
{
    public const int MaximumDescriptionUtf8Bytes = 16 * 1024;
    public const int DefaultRetryLimit = 2;
    public const int MaximumRetryLimit = 5;
    public const int MaximumDraftJsonCharacters = 128 * 1024;
}

/// <summary>
/// The request shape used on the one bound ChatLlm route. Both forms feed the same parse/validate pipeline; the
/// choice is a request-form difference inside the single explicit binding, never a route change.
/// </summary>
public enum RecipeRequestForm
{
    PlainText,
    StructuredOutput,
}

/// <summary>One explicit "generate" action. The user's effect description is never included in diagnostics.</summary>
public sealed class RecipeGenerationRequest
{
    public RecipeGenerationRequest(
        string correlationId,
        string description,
        int retryLimit = RecipeChannelLimits.DefaultRetryLimit,
        RecipeRequestForm form = RecipeRequestForm.PlainText)
    {
        CorrelationId = AiContractGuard.CorrelationId(correlationId, nameof(correlationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(description, nameof(description));
        if (description.IndexOf('\0') >= 0 ||
            Encoding.UTF8.GetByteCount(description) > RecipeChannelLimits.MaximumDescriptionUtf8Bytes)
        {
            throw new ArgumentException("Effect description is invalid.", nameof(description));
        }

        if (retryLimit is < 0 or > RecipeChannelLimits.MaximumRetryLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(retryLimit));
        }

        if (!Enum.IsDefined(form))
        {
            throw new ArgumentOutOfRangeException(nameof(form));
        }

        Description = description;
        RetryLimit = retryLimit;
        Form = form;
    }

    public string ContractVersion => AiContractVersions.RecipeGenerationRequest;
    public string CorrelationId { get; }
    public string Description { get; }
    public int RetryLimit { get; }
    public RecipeRequestForm Form { get; }

    public override string ToString() => "RecipeGenerationRequest(<redacted>)";
}

/// <summary>Severity vocabulary aligned with the Unity-side ValidationReport.</summary>
public enum RecipeValidationSeverity
{
    Error,
    Warning,
    Info,
}

/// <summary>
/// One L1 validation entry. Fields are deliberately isomorphic to the Unity ValidationReport entry
/// (code/severity/path/message/actualValue/allowedRange) so one repair-prompt template serves both layers.
/// </summary>
public sealed class RecipeValidationIssue
{
    public RecipeValidationIssue(
        string code,
        RecipeValidationSeverity severity,
        string path,
        string message,
        string? actualValueJson = null,
        string? allowedRange = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code, nameof(code));
        ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));
        ArgumentException.ThrowIfNullOrWhiteSpace(message, nameof(message));
        if (code.Length > 16 || message.Length > 512 || path.Length > 1024 ||
            actualValueJson?.Length > 4096 || allowedRange?.Length > 4096 ||
            !Enum.IsDefined(severity))
        {
            throw new ArgumentException("Validation issue is invalid.", nameof(code));
        }

        Code = code;
        Severity = severity;
        Path = path;
        Message = message;
        ActualValueJson = actualValueJson;
        AllowedRange = allowedRange;
    }

    public string Code { get; }
    public RecipeValidationSeverity Severity { get; }
    public string Path { get; }
    public string Message { get; }
    public string? ActualValueJson { get; }
    public string? AllowedRange { get; }

    public override string ToString() => "RecipeValidationIssue(" + Code + ")";
}

/// <summary>An L1-valid Recipe v1 draft. It exists only in user application data and never in a Unity project.</summary>
public sealed class RecipeDraft
{
    public RecipeDraft(
        string correlationId,
        string recipeJson,
        string canonicalSha256,
        string recipeId,
        string archetype,
        string dimension,
        string targetProfile,
        string promptTemplateVersion,
        string templateCatalogVersion)
    {
        CorrelationId = AiContractGuard.CorrelationId(correlationId, nameof(correlationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(recipeJson, nameof(recipeJson));
        if (recipeJson.Length > RecipeChannelLimits.MaximumDraftJsonCharacters || recipeJson.IndexOf('\0') >= 0)
        {
            throw new ArgumentException("Recipe draft JSON is invalid.", nameof(recipeJson));
        }

        CanonicalSha256 = GuardSha256(canonicalSha256, nameof(canonicalSha256));
        RecipeId = GuardShortText(recipeId, nameof(recipeId));
        Archetype = GuardShortText(archetype, nameof(archetype));
        Dimension = GuardShortText(dimension, nameof(dimension));
        TargetProfile = GuardShortText(targetProfile, nameof(targetProfile));
        PromptTemplateVersion = GuardShortText(promptTemplateVersion, nameof(promptTemplateVersion));
        TemplateCatalogVersion = GuardShortText(templateCatalogVersion, nameof(templateCatalogVersion));
        RecipeJson = recipeJson;
    }

    public string CorrelationId { get; }
    public string RecipeJson { get; }
    public string CanonicalSha256 { get; }
    public string RecipeId { get; }
    public string Archetype { get; }
    public string Dimension { get; }
    public string TargetProfile { get; }
    public string PromptTemplateVersion { get; }
    public string TemplateCatalogVersion { get; }

    public override string ToString() => "RecipeDraft(<redacted>)";

    internal static string GuardSha256(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length != 64 ||
            value.Any(static character => character is not ((>= '0' and <= '9') or (>= 'a' and <= 'f'))))
        {
            throw new ArgumentException("Canonical hash is invalid.", parameterName);
        }

        return value;
    }

    private static string GuardShortText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > 256 || AiContractGuard.HasControl(value))
        {
            throw new ArgumentException("Draft summary value is invalid.", parameterName);
        }

        return value;
    }
}

/// <summary>Terminal outcome of one explicit generate action.</summary>
public enum RecipeGenerationOutcome
{
    /// <summary>The draft passed L1 validation and awaits explicit user confirmation.</summary>
    Drafted,

    /// <summary>The retry budget is exhausted; the last output and its full report are preserved.</summary>
    ValidationFailed,

    /// <summary>The ChatLlm channel failed. Network-class failures never trigger an automatic retry.</summary>
    ChannelFailed,

    /// <summary>The user cancelled the generation task.</summary>
    Cancelled,
}

/// <summary>One AI request inside the task timeline (REQ-001-23): its ordinal and the resulting stable error codes.</summary>
public sealed class RecipeGenerationAttempt
{
    public RecipeGenerationAttempt(int requestNumber, IEnumerable<string> errorCodes)
    {
        if (requestNumber < 1 || requestNumber > 1 + RecipeChannelLimits.MaximumRetryLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(requestNumber));
        }

        RequestNumber = requestNumber;
        ErrorCodes = Copy(errorCodes, nameof(errorCodes));
    }

    public int RequestNumber { get; }
    public IReadOnlyList<string> ErrorCodes { get; }

    public override string ToString() => "RecipeGenerationAttempt(" + RequestNumber + ")";

    private static IReadOnlyList<string> Copy(IEnumerable<string> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        var copied = values.ToArray();
        if (copied.Length > 256 || copied.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Error code list is invalid.", parameterName);
        }

        return new ReadOnlyCollection<string>(copied);
    }
}

/// <summary>
/// Typed result of one generate action. It never carries the prompt, the endpoint, or provider payloads; the draft
/// JSON itself is a product and may be persisted by the caller.
/// </summary>
public sealed class RecipeGenerationResult
{
    private RecipeGenerationResult(
        RecipeGenerationOutcome outcome,
        string correlationId,
        RecipeDraft? draft,
        string? lastOutputText,
        IReadOnlyList<RecipeValidationIssue> issues,
        IReadOnlyList<RecipeGenerationAttempt> attempts,
        ChatChannelErrorCode? channelError,
        string promptTemplateVersion,
        string templateCatalogVersion)
    {
        Outcome = outcome;
        CorrelationId = correlationId;
        Draft = draft;
        LastOutputText = lastOutputText;
        Issues = issues;
        Attempts = attempts;
        ChannelError = channelError;
        PromptTemplateVersion = promptTemplateVersion;
        TemplateCatalogVersion = templateCatalogVersion;
    }

    public RecipeGenerationOutcome Outcome { get; }
    public string CorrelationId { get; }
    public RecipeDraft? Draft { get; }

    /// <summary>The last extracted output for a failed validation task, kept so the user can inspect it (X3).</summary>
    public string? LastOutputText { get; }

    public IReadOnlyList<RecipeValidationIssue> Issues { get; }
    public IReadOnlyList<RecipeGenerationAttempt> Attempts { get; }
    public ChatChannelErrorCode? ChannelError { get; }
    public string PromptTemplateVersion { get; }
    public string TemplateCatalogVersion { get; }
    public int RequestCount => Attempts.Count;

    public override string ToString() => "RecipeGenerationResult(" + Outcome + ")";

    public static RecipeGenerationResult Drafted(
        RecipeDraft draft,
        IEnumerable<RecipeGenerationAttempt> attempts)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var copiedAttempts = CopyAttempts(attempts, requireAny: true);
        return new RecipeGenerationResult(
            RecipeGenerationOutcome.Drafted,
            draft.CorrelationId,
            draft,
            lastOutputText: null,
            Array.Empty<RecipeValidationIssue>(),
            copiedAttempts,
            channelError: null,
            draft.PromptTemplateVersion,
            draft.TemplateCatalogVersion);
    }

    public static RecipeGenerationResult ValidationFailed(
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

        return new RecipeGenerationResult(
            RecipeGenerationOutcome.ValidationFailed,
            AiContractGuard.CorrelationId(correlationId, nameof(correlationId)),
            draft: null,
            GuardOutputText(lastOutputText),
            copiedIssues,
            CopyAttempts(attempts, requireAny: true),
            channelError: null,
            promptTemplateVersion,
            templateCatalogVersion);
    }

    public static RecipeGenerationResult ChannelFailed(
        string correlationId,
        ChatChannelErrorCode channelError,
        IEnumerable<RecipeGenerationAttempt> attempts,
        string promptTemplateVersion,
        string templateCatalogVersion)
    {
        var outcome = channelError == ChatChannelErrorCode.Cancelled
            ? RecipeGenerationOutcome.Cancelled
            : RecipeGenerationOutcome.ChannelFailed;
        return new RecipeGenerationResult(
            outcome,
            AiContractGuard.CorrelationId(correlationId, nameof(correlationId)),
            draft: null,
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
/// Draft lifecycle states. Confirmation only flips state; the restricted build path then advances the
/// confirmed draft to exactly one terminal build outcome. Only
/// <see cref="ConfirmedAwaitingBuild"/> may advance to <see cref="Built"/> or <see cref="BuildFailed"/>.
/// </summary>
public enum RecipeDraftStatus
{
    PendingConfirmation,
    Failed,
    ConfirmedAwaitingBuild,

    /// <summary>The restricted build path produced the Prefab and its provenance/audit records.</summary>
    Built,

    /// <summary>The restricted build path refused or failed the build; the build is never retried automatically.</summary>
    BuildFailed,

    /// <summary>
    /// The confirmation lapsed because a newer version landed in the same lineage (REQ-004 §7.3); the version
    /// can never be built and never appears in the awaiting-build backlog.
    /// </summary>
    Superseded,
}

/// <summary>
/// One persisted draft record in user application data. The user can inspect and delete it at any time. Every
/// record is one version of a lineage (REQ-004 §7.2); its content and hash are immutable once persisted.
/// </summary>
public sealed class RecipeDraftRecord
{
    /// <summary>
    /// Lineage-unaware construction kept for callers that predate the version chain: the record is the root of a
    /// lineage named after itself with <see cref="RecipeDraftOrigin.AiDraft"/> origin.
    /// </summary>
    public RecipeDraftRecord(
        string draftId,
        RecipeDraftStatus status,
        DateTimeOffset createdUtc,
        DateTimeOffset updatedUtc,
        string correlationId,
        string promptTemplateVersion,
        string templateCatalogVersion,
        string recipeJson,
        string? canonicalSha256,
        string? recipeId,
        string? archetype,
        string? dimension,
        string? targetProfile,
        IEnumerable<RecipeValidationIssue> issues,
        int requestCount)
        : this(
            draftId,
            status,
            createdUtc,
            updatedUtc,
            correlationId,
            promptTemplateVersion,
            templateCatalogVersion,
            recipeJson,
            canonicalSha256,
            recipeId,
            archetype,
            dimension,
            targetProfile,
            issues,
            requestCount,
            RecipeDraftProvenance.Root(AiContractGuard.Identifier(draftId, nameof(draftId)), RecipeDraftOrigin.AiDraft))
    {
    }

    public RecipeDraftRecord(
        string draftId,
        RecipeDraftStatus status,
        DateTimeOffset createdUtc,
        DateTimeOffset updatedUtc,
        string correlationId,
        string promptTemplateVersion,
        string templateCatalogVersion,
        string recipeJson,
        string? canonicalSha256,
        string? recipeId,
        string? archetype,
        string? dimension,
        string? targetProfile,
        IEnumerable<RecipeValidationIssue> issues,
        int requestCount,
        RecipeDraftProvenance provenance)
    {
        DraftId = AiContractGuard.Identifier(draftId, nameof(draftId));
        ArgumentNullException.ThrowIfNull(provenance);
        if (string.Equals(provenance.ParentDraftId, DraftId, StringComparison.Ordinal))
        {
            throw new ArgumentException("A version cannot be its own parent.", nameof(provenance));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (status == RecipeDraftStatus.Failed ? canonicalSha256 is not null : canonicalSha256 is null)
        {
            throw new ArgumentException("Canonical hash is required exactly for non-failed records.", nameof(canonicalSha256));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(recipeJson, nameof(recipeJson));
        if (recipeJson.Length > RecipeChannelLimits.MaximumDraftJsonCharacters || recipeJson.IndexOf('\0') >= 0)
        {
            throw new ArgumentException("Recipe draft JSON is invalid.", nameof(recipeJson));
        }

        if (requestCount is < 0 or > 1 + RecipeChannelLimits.MaximumRetryLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(requestCount));
        }

        Status = status;
        CreatedUtc = AiContractGuard.Utc(createdUtc, nameof(createdUtc));
        UpdatedUtc = AiContractGuard.Utc(updatedUtc, nameof(updatedUtc));
        CorrelationId = AiContractGuard.CorrelationId(correlationId, nameof(correlationId));
        PromptTemplateVersion = promptTemplateVersion ?? throw new ArgumentNullException(nameof(promptTemplateVersion));
        TemplateCatalogVersion = templateCatalogVersion ?? throw new ArgumentNullException(nameof(templateCatalogVersion));
        RecipeJson = recipeJson;
        CanonicalSha256 = canonicalSha256 is null ? null : RecipeDraft.GuardSha256(canonicalSha256, nameof(canonicalSha256));
        RecipeId = recipeId;
        Archetype = archetype;
        Dimension = dimension;
        TargetProfile = targetProfile;
        Issues = CopyIssues(issues);
        RequestCount = requestCount;
        Provenance = provenance;
    }

    public string DraftId { get; }
    public RecipeDraftStatus Status { get; }
    public DateTimeOffset CreatedUtc { get; }
    public DateTimeOffset UpdatedUtc { get; }
    public string CorrelationId { get; }
    public string PromptTemplateVersion { get; }
    public string TemplateCatalogVersion { get; }
    public string RecipeJson { get; }
    public string? CanonicalSha256 { get; }
    public string? RecipeId { get; }
    public string? Archetype { get; }
    public string? Dimension { get; }
    public string? TargetProfile { get; }
    public IReadOnlyList<RecipeValidationIssue> Issues { get; }
    public int RequestCount { get; }

    /// <summary>The version-chain fields as one bundle; the flat properties below are its REQ-004 §7.2 names.</summary>
    public RecipeDraftProvenance Provenance { get; }

    public string LineageId => Provenance.LineageId;
    public string? ParentDraftId => Provenance.ParentDraftId;
    public int RevisionOrdinal => Provenance.RevisionOrdinal;
    public RecipeDraftOrigin Origin => Provenance.Origin;
    public string? FeedbackText => Provenance.FeedbackText;
    public IReadOnlyList<RecipeGuardRestoration> GuardRestorations => Provenance.GuardRestorations;
    public int GuardRestorationCount => Provenance.GuardRestorationCount;
    public string? PresetId => Provenance.PresetId;

    public override string ToString() => "RecipeDraftRecord(" + DraftId + "," + Status + ")";

    /// <summary>Builds the persistable record for a drafted or validation-failed result as a new lineage root.</summary>
    public static RecipeDraftRecord Create(RecipeGenerationResult result, DateTimeOffset createdUtc)
    {
        ArgumentNullException.ThrowIfNull(result);
        var draftId = NewDraftId();
        var provenance = RecipeDraftProvenance.Root(RecipeDraftProvenance.NewLineageId(), RecipeDraftOrigin.AiDraft);
        return result.Outcome switch
        {
            RecipeGenerationOutcome.Drafted when result.Draft is not null => new RecipeDraftRecord(
                draftId,
                RecipeDraftStatus.PendingConfirmation,
                createdUtc,
                createdUtc,
                result.CorrelationId,
                result.PromptTemplateVersion,
                result.TemplateCatalogVersion,
                result.Draft.RecipeJson,
                result.Draft.CanonicalSha256,
                result.Draft.RecipeId,
                result.Draft.Archetype,
                result.Draft.Dimension,
                result.Draft.TargetProfile,
                Array.Empty<RecipeValidationIssue>(),
                result.RequestCount,
                provenance),
            RecipeGenerationOutcome.ValidationFailed => new RecipeDraftRecord(
                draftId,
                RecipeDraftStatus.Failed,
                createdUtc,
                createdUtc,
                result.CorrelationId,
                result.PromptTemplateVersion,
                result.TemplateCatalogVersion,
                result.LastOutputText ?? "{}",
                canonicalSha256: null,
                recipeId: null,
                archetype: null,
                dimension: null,
                targetProfile: null,
                result.Issues,
                result.RequestCount,
                provenance),
            _ => throw new ArgumentException("Only drafted or validation-failed results are persistable.", nameof(result)),
        };
    }

    /// <summary>Produces a fresh draft identifier.</summary>
    public static string NewDraftId() => "draft-" + Guid.NewGuid().ToString("N");

    /// <summary>The same version in a new lifecycle state; content, hash and chain position are unchanged.</summary>
    public RecipeDraftRecord WithStatus(RecipeDraftStatus status, DateTimeOffset updatedUtc) =>
        Copy(status, updatedUtc, Provenance);

    /// <summary>
    /// The same version re-linked to a new parent. This exists for the store's trim splice: when the version
    /// below is removed, the survivor points at the nearest remaining ancestor so the chain stays linear.
    /// </summary>
    public RecipeDraftRecord WithParentDraftId(string? parentDraftId) =>
        Copy(Status, UpdatedUtc, Provenance.WithParentDraftId(parentDraftId));

    private RecipeDraftRecord Copy(RecipeDraftStatus status, DateTimeOffset updatedUtc, RecipeDraftProvenance provenance) => new(
        DraftId,
        status,
        CreatedUtc,
        updatedUtc,
        CorrelationId,
        PromptTemplateVersion,
        TemplateCatalogVersion,
        RecipeJson,
        CanonicalSha256,
        RecipeId,
        Archetype,
        Dimension,
        TargetProfile,
        Issues,
        RequestCount,
        provenance);

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
}

/// <summary>Stable draft-store failure vocabulary.</summary>
public enum RecipeDraftStoreErrorCode
{
    NotFound,
    InvalidStatus,
    HashMismatch,
    StorageFailed,
    RecordInvalid,

    /// <summary>
    /// The store file parses far enough to expose its integer formatVersion, and that version is not the current
    /// one. Distinct from <see cref="StorageFailed"/> (corruption): the remedy is to delete the old file, not to
    /// report a defect (REQ-004 §7.4).
    /// </summary>
    UnsupportedVersion,

    /// <summary>Another process held the store lock for the whole bounded wait; nothing was read or written.</summary>
    StoreBusy,

    /// <summary>The parent of the new version is not its lineage head; chains are linear, so appending there would branch.</summary>
    NotLineageHead,

    /// <summary>
    /// Protected records fill a retention cap and are never dropped to make room: either the lineage cap is
    /// filled by protected versions (level 1), or every retained lineage holds a version awaiting build, so no
    /// lineage may be evicted for a new one (level 2). Nothing is written in either case.
    /// </summary>
    LineageCapacityExhausted,

    /// <summary>Truncation would delete a confirmed, built or build-failed version, which is an audit record.</summary>
    TruncationBlocked,
}

/// <summary>Deliberately low-detail draft-store failure. It never carries file contents or paths.</summary>
public sealed class RecipeDraftStoreException : Exception
{
    public RecipeDraftStoreException(RecipeDraftStoreErrorCode code)
        : base(MessageFor(code))
    {
        Code = code;
    }

    public RecipeDraftStoreErrorCode Code { get; }

    public override string ToString() => "RecipeDraftStoreException(" + Code + ")";

    private static string MessageFor(RecipeDraftStoreErrorCode code) => code switch
    {
        RecipeDraftStoreErrorCode.NotFound => "The recipe draft does not exist.",
        RecipeDraftStoreErrorCode.InvalidStatus => "The recipe draft is not awaiting confirmation.",
        RecipeDraftStoreErrorCode.HashMismatch => "The recipe draft changed after it was presented for confirmation.",
        RecipeDraftStoreErrorCode.StorageFailed => "The recipe draft storage operation failed.",
        RecipeDraftStoreErrorCode.UnsupportedVersion =>
            "The recipe draft storage was written by an unsupported format version. Delete " +
            UnsupportedVersionRemedyPath + " and its .bak copy under the current user's application data " +
            "directory, then retry; the file is never migrated or deleted automatically.",
        RecipeDraftStoreErrorCode.StoreBusy => "Another process is using the recipe draft storage; retry shortly.",
        RecipeDraftStoreErrorCode.NotLineageHead => "The recipe draft version is not the latest version of its lineage.",
        RecipeDraftStoreErrorCode.LineageCapacityExhausted =>
            "Recipe draft capacity is exhausted: the lineage is full of confirmed or built versions, or every " +
            "retained lineage holds a draft awaiting build. Build or revise an awaiting draft before adding more.",
        RecipeDraftStoreErrorCode.TruncationBlocked =>
            "A later version of the recipe draft lineage is confirmed or built and cannot be deleted.",
        _ => "The recipe draft record is invalid.",
    };

    /// <summary>The store file's position relative to the user application data root; never an absolute path.</summary>
    public const string UnsupportedVersionRemedyPath = "VFXComposer/AI/recipe-drafts.json";
}

/// <summary>
/// The feature-facing structured generation channel. It uses only the one persisted ChatLlm binding, performs no
/// route selection or fallback, and sends requests only inside an explicit generate action.
/// </summary>
public interface IRecipeGenerationChannel
{
    ValueTask<RecipeGenerationResult> GenerateAsync(
        RecipeGenerationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Draft persistence in current-user application data. Every state-advancing member re-verifies the
/// canonical hash, so a stale caller can never advance content the user did not confirm.
/// </summary>
public interface IRecipeDraftStore
{
    RecipeDraftRecord Save(RecipeDraftRecord record);

    /// <summary>Flips a pending draft to confirmed-awaiting-build. A stale hash fails closed with HashMismatch.</summary>
    RecipeDraftRecord Confirm(string draftId, string canonicalSha256);

    RecipeDraftRecord? TryGet(string draftId);

    /// <summary>
    /// The confirmed drafts still awaiting a build, oldest confirmation first so a caller draining the
    /// backlog preserves confirmation order.
    /// </summary>
    IReadOnlyList<RecipeDraftRecord> ListConfirmedAwaitingBuild();

    /// <summary>
    /// Records a successful build. Only <see cref="RecipeDraftStatus.ConfirmedAwaitingBuild"/> may advance;
    /// any other state fails closed with InvalidStatus and a stale hash with HashMismatch.
    /// </summary>
    RecipeDraftRecord MarkBuilt(string draftId, string canonicalSha256);

    /// <summary>Records a refused or failed build under the same transition rules as <see cref="MarkBuilt"/>.</summary>
    RecipeDraftRecord MarkBuildFailed(string draftId, string canonicalSha256);
}
