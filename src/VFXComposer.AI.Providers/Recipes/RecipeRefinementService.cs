using System.Text.Json;
using VFXComposer.AI.Contracts.Chat;
using VFXComposer.AI.Contracts.Recipes;

namespace VFXComposer.AI.Providers.Recipes;

/// <summary>
/// Orchestrates one explicit "refine" action on the one bound ChatLlm route (REQ-004 §6, ADR-007 §2.5): the
/// action authorizes at most 1 + N requests, further requests are triggered only by parse/L1 validation
/// failures, and a channel failure (network, upstream, timeout, cancellation) ends the action immediately
/// without a retry and without a version. A validated output runs the override guard (REQ-004 §9.3) before the
/// result is returned, so the caller persists exactly one <c>ai_refine</c> version via
/// <see cref="RecipeRefinementResult.ToRevision"/> and <c>IRecipeDraftLineageStore.AppendVersion</c>. Neither
/// the description, the feedback, nor any provider payload ever reaches diagnostics.
/// </summary>
internal sealed class RecipeRefinementService : IRecipeRefinementChannel
{
    private readonly Func<IChatChannelGateway> _acquireChatGateway;

    /// <summary>
    /// The gateway accessor is invoked only inside <see cref="RefineAsync"/>, preserving the invariant that the
    /// first HTTP-capable construction happens inside a deliberate user request.
    /// </summary>
    public RecipeRefinementService(Func<IChatChannelGateway> acquireChatGateway)
    {
        _acquireChatGateway = acquireChatGateway ?? throw new ArgumentNullException(nameof(acquireChatGateway));
    }

    public override string ToString() => "RecipeRefinementService(<redacted>)";

    public async ValueTask<RecipeRefinementResult> RefineAsync(
        RecipeRefinementRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var snapshot = RecipeTemplateCatalogSnapshot.Default;
        var gateway = _acquireChatGateway() ?? throw new InvalidOperationException("The chat gateway accessor returned null.");

        var head = request.Head;
        var attempts = new List<RecipeGenerationAttempt>();
        var messages = RecipePromptAssembler.CreateRefinementMessages(
            request.OriginalDescription,
            head.RecipeJson,
            request.FeedbackText);
        string? lastOutputText = null;
        IReadOnlyList<RecipeValidationIssue> lastIssues = Array.Empty<RecipeValidationIssue>();

        for (var requestNumber = 1; requestNumber <= 1 + request.RetryLimit; requestNumber++)
        {
            ChatChannelResult channelResult;
            try
            {
                channelResult = await gateway.CompleteAsync(
                    new ChatChannelRequest(request.CorrelationId, messages),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ChatChannelException exception)
            {
                // Channel-class failures end the whole action (REQ-004-18): the retry budget belongs to
                // validation failures only, never to transport, upstream, timeout, or cancellation.
                return RecipeRefinementResult.ChannelFailed(
                    request.CorrelationId,
                    exception.Code,
                    attempts,
                    RecipePromptAssembler.Version,
                    snapshot.TemplateCatalogVersion);
            }
            catch (OperationCanceledException)
            {
                return RecipeRefinementResult.ChannelFailed(
                    request.CorrelationId,
                    ChatChannelErrorCode.Cancelled,
                    attempts,
                    RecipePromptAssembler.Version,
                    snapshot.TemplateCatalogVersion);
            }

            var issues = EvaluateOutput(channelResult, out var extractedJson);
            lastOutputText = extractedJson ?? BoundOutputText(channelResult.Text);
            lastIssues = issues;
            var errorCodes = issues
                .Where(static issue => issue.Severity == RecipeValidationSeverity.Error)
                .Select(static issue => issue.Code)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            attempts.Add(new RecipeGenerationAttempt(requestNumber, errorCodes));

            if (errorCodes.Length == 0 && extractedJson is not null)
            {
                return CreateRefinedResult(request, snapshot, extractedJson, attempts);
            }

            if (requestNumber == 1 + request.RetryLimit)
            {
                break;
            }

            messages = RecipePromptAssembler.CreateRefinementRepairMessages(
                request.OriginalDescription,
                head.RecipeJson,
                request.FeedbackText,
                lastOutputText,
                issues);
        }

        // Budget exhausted: the head stays, no version lands, the last output and its report are preserved
        // for inspection (REQ-004-17).
        return RecipeRefinementResult.ValidationFailed(
            request.CorrelationId,
            lastOutputText,
            lastIssues,
            attempts,
            RecipePromptAssembler.Version,
            snapshot.TemplateCatalogVersion);
    }

    /// <summary>Extracts and L1-validates the output; the pipeline is the generation service's.</summary>
    private static IReadOnlyList<RecipeValidationIssue> EvaluateOutput(
        ChatChannelResult channelResult,
        out string? extractedJson)
    {
        extractedJson = null;
        if (!RecipeOutputParser.TryExtractJson(channelResult.Text, out extractedJson, out var parseIssue))
        {
            return [parseIssue];
        }

        return RecipeL1Validator.Validate(extractedJson!);
    }

    /// <summary>
    /// Runs the override guard on the validated output (after L1, before any version exists) and shapes the
    /// refined result. The guard is deterministic post-processing inside the same round: its restored document
    /// is the content of the one <c>ai_refine</c> version the caller will append (REQ-004-45).
    /// </summary>
    private static RecipeRefinementResult CreateRefinedResult(
        RecipeRefinementRequest request,
        RecipeTemplateCatalogSnapshot snapshot,
        string recipeJson,
        IReadOnlyList<RecipeGenerationAttempt> attempts)
    {
        var ancestorChainHeadFirst = request.Lineage.Reverse().ToArray();
        var guardOutcome = RecipeRefineOverrideGuard.Apply(
            ancestorChainHeadFirst,
            recipeJson,
            request.FeedbackText,
            RecipeRefineKnowledge.Default);

        var canonicalJson = guardOutcome.GuardedRecipeJson;
        using var document = JsonDocument.Parse(canonicalJson);
        var root = document.RootElement;
        var head = request.Head;
        var refinedDraft = new RecipeDraft(
            request.CorrelationId,
            canonicalJson,
            RecipeCanonicalJson.ComputeSha256(canonicalJson),
            ReadSummaryString(root, "id"),
            ReadSummaryString(root, "archetype"),
            ReadSummaryString(root, "dimension"),
            ReadSummaryString(root, "targetProfile"),
            RecipePromptAssembler.Version,
            snapshot.TemplateCatalogVersion);
        return RecipeRefinementResult.Refined(
            refinedDraft,
            head.DraftId,
            head.CanonicalSha256!,
            request.FeedbackText,
            guardOutcome.Restorations.Select(static restoration => new RecipeRefinementGuardRestoration(
                restoration.ParameterPath,
                restoration.SourceDraftId,
                restoration.AiValueLiteral,
                restoration.RestoredValueLiteral)),
            attempts);
    }

    private static string ReadSummaryString(JsonElement root, string propertyName) =>
        root.GetProperty(propertyName).GetString()
            ?? throw new InvalidOperationException("A validated recipe lost a required summary field.");

    private static string? BoundOutputText(string text) =>
        text.Length is > 0 and <= RecipeChannelLimits.MaximumDraftJsonCharacters && text.IndexOf('\0') < 0
            ? text
            : null;
}
