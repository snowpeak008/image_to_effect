using System.Text.Json;
using VFXComposer.AI.Contracts.Chat;
using VFXComposer.AI.Contracts.Recipes;

namespace VFXComposer.AI.Providers.Recipes;

/// <summary>
/// Orchestrates one explicit "generate" action on the one bound ChatLlm route (ADR-007 §2.5): the action
/// authorizes at most 1 + N requests, further requests are triggered only by parse/L1 validation failures, and a
/// channel failure (network, upstream, timeout, cancellation) ends the action immediately without a retry.
/// Neither the effect description nor any provider payload ever reaches diagnostics.
/// </summary>
internal sealed class RecipeGenerationService : IRecipeGenerationChannel
{
    private readonly Func<IChatChannelGateway> _acquireChatGateway;

    /// <summary>
    /// The gateway accessor is invoked only inside <see cref="GenerateAsync"/>, preserving the invariant that the
    /// first HTTP-capable construction happens inside a deliberate user request.
    /// </summary>
    public RecipeGenerationService(Func<IChatChannelGateway> acquireChatGateway)
    {
        _acquireChatGateway = acquireChatGateway ?? throw new ArgumentNullException(nameof(acquireChatGateway));
    }

    public override string ToString() => "RecipeGenerationService(<redacted>)";

    public async ValueTask<RecipeGenerationResult> GenerateAsync(
        RecipeGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var snapshot = RecipeTemplateCatalogSnapshot.Default;
        var gateway = _acquireChatGateway() ?? throw new InvalidOperationException("The chat gateway accessor returned null.");

        var attempts = new List<RecipeGenerationAttempt>();
        var messages = RecipePromptAssembler.CreateInitialMessages(request.Description);
        string? lastOutputText = null;
        IReadOnlyList<RecipeValidationIssue> lastIssues = Array.Empty<RecipeValidationIssue>();

        for (var requestNumber = 1; requestNumber <= 1 + request.RetryLimit; requestNumber++)
        {
            ChatChannelResult channelResult;
            try
            {
                channelResult = await gateway.CompleteAsync(
                    CreateChannelRequest(request, messages),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ChatChannelException exception)
            {
                // Channel-class failures end the whole action: ADR-007 grants further requests only to
                // schema/L1 validation failures, never to transport, upstream, timeout, or cancellation.
                return RecipeGenerationResult.ChannelFailed(
                    request.CorrelationId,
                    exception.Code,
                    attempts,
                    RecipePromptAssembler.Version,
                    snapshot.TemplateCatalogVersion);
            }
            catch (OperationCanceledException)
            {
                return RecipeGenerationResult.ChannelFailed(
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
                return CreateDraftedResult(request, snapshot, extractedJson, attempts);
            }

            if (requestNumber == 1 + request.RetryLimit)
            {
                break;
            }

            messages = RecipePromptAssembler.CreateRepairMessages(request.Description, lastOutputText, issues);
        }

        return RecipeGenerationResult.ValidationFailed(
            request.CorrelationId,
            lastOutputText,
            lastIssues,
            attempts,
            RecipePromptAssembler.Version,
            snapshot.TemplateCatalogVersion);
    }

    private static ChatChannelRequest CreateChannelRequest(
        RecipeGenerationRequest request,
        IReadOnlyList<ChatChannelMessage> messages) =>
        new(
            request.CorrelationId,
            messages,
            request.Form == RecipeRequestForm.StructuredOutput
                ? RecipeTemplateCatalogSnapshot.CreateStructuredOutput()
                : null);

    /// <summary>Extracts and L1-validates the output. Both request forms converge on the same pipeline.</summary>
    private static IReadOnlyList<RecipeValidationIssue> EvaluateOutput(
        ChatChannelResult channelResult,
        out string? extractedJson)
    {
        extractedJson = null;
        if (channelResult.StructuredOutput is JsonElement structured && structured.ValueKind == JsonValueKind.Object)
        {
            extractedJson = structured.GetRawText();
        }
        else if (!RecipeOutputParser.TryExtractJson(channelResult.Text, out extractedJson, out var parseIssue))
        {
            return [parseIssue];
        }

        return RecipeL1Validator.Validate(extractedJson!);
    }

    private static RecipeGenerationResult CreateDraftedResult(
        RecipeGenerationRequest request,
        RecipeTemplateCatalogSnapshot snapshot,
        string recipeJson,
        IReadOnlyList<RecipeGenerationAttempt> attempts)
    {
        // The draft retains the canonical text so that what the user confirms is byte-identical to what the
        // SHA-256 confirmation binding covers (REQ-001-15).
        var canonicalJson = RecipeCanonicalJson.Canonicalize(recipeJson);
        using var document = JsonDocument.Parse(canonicalJson);
        var root = document.RootElement;
        var draft = new RecipeDraft(
            request.CorrelationId,
            canonicalJson,
            RecipeCanonicalJson.ComputeSha256(canonicalJson),
            ReadSummaryString(root, "id"),
            ReadSummaryString(root, "archetype"),
            ReadSummaryString(root, "dimension"),
            ReadSummaryString(root, "targetProfile"),
            RecipePromptAssembler.Version,
            snapshot.TemplateCatalogVersion);
        return RecipeGenerationResult.Drafted(draft, attempts);
    }

    private static string ReadSummaryString(JsonElement root, string propertyName) =>
        root.GetProperty(propertyName).GetString()
            ?? throw new InvalidOperationException("A validated recipe lost a required summary field.");

    private static string? BoundOutputText(string text) =>
        text.Length is > 0 and <= RecipeChannelLimits.MaximumDraftJsonCharacters && text.IndexOf('\0') < 0
            ? text
            : null;
}
