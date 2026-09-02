using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Chat;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Recipes;

namespace VFXComposer.AI.Tests.Recipes;

/// <summary>
/// REQ-004 §6 refinement channel semantics on a zero-network fake gateway: the 1 + N budget with
/// validation-only retries, no automatic retry on channel failures (exactly one request), the validation-failed
/// terminal state that lands no version, the guard running before the result, the request-shape assertions of
/// AC-2/AC-3/AC-4/AC-18, and the ToRevision persistence factory feeding AppendVersion.
/// </summary>
[TestClass]
public sealed class RecipeRefinementServiceTests
{
    private const string OriginalDescription = "a short blue spark bolt";
    private const string Feedback = "make the fire core bigger";

    private static string BaseRecipeJson => RecipeCanonicalJson.Canonicalize(RecipePromptAssembler.ReferenceRecipeJson);

    private static string WithScale(string recipeJson, string literal)
    {
        var root = JsonNode.Parse(recipeJson)!.AsObject();
        foreach (var stage in root["stages"]!.AsArray())
        {
            foreach (var module in stage!["modules"]!.AsArray())
            {
                if (string.Equals(module!["id"]!.GetValue<string>(), "core", StringComparison.Ordinal))
                {
                    module["parameters"]!.AsObject()["scale"] = JsonNode.Parse(literal);
                }
            }
        }

        return RecipeCanonicalJson.Canonicalize(root.ToJsonString());
    }

    private static RecipeDraftRecord Version(
        string draftId,
        string recipeJson,
        RecipeDraftOrigin origin,
        string? parentDraftId,
        int ordinal) => new(
        draftId,
        RecipeDraftStatus.PendingConfirmation,
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch,
        "corr-refine-tests",
        RecipeDraftTestData.PromptVersion,
        RecipeDraftTestData.CatalogVersion,
        recipeJson,
        RecipeCanonicalJson.ComputeSha256(recipeJson),
        "spark_projectile_2d",
        "projectile",
        "2d",
        "mobile_medium",
        Array.Empty<RecipeValidationIssue>(),
        requestCount: origin == RecipeDraftOrigin.HumanEdit ? 0 : 1,
        new RecipeDraftProvenance("lineage-refine-tests", parentDraftId, ordinal, origin));

    /// <summary>v1 ai_draft → v2 human_edit (scale 1.2 → 1.5), oldest first as ListLineage returns it.</summary>
    private static List<RecipeDraftRecord> Lineage()
    {
        var v1 = Version("draft-v1", BaseRecipeJson, RecipeDraftOrigin.AiDraft, parentDraftId: null, ordinal: 1);
        var v2 = Version("draft-v2", WithScale(BaseRecipeJson, "1.5"), RecipeDraftOrigin.HumanEdit, v1.DraftId, ordinal: 2);
        return [v1, v2];
    }

    private static RecipeRefinementRequest Request(int retryLimit = RecipeChannelLimits.DefaultRetryLimit) =>
        new(Guid.NewGuid().ToString("N"), OriginalDescription, Lineage(), Feedback, retryLimit);

    // ---- AC-2: one refine round succeeds with exactly one request ----

    [TestMethod]
    public async Task ASuccessfulRoundMakesExactlyOneRequestAndCarriesEverythingForPersistence()
    {
        var lineage = Lineage();
        var refinedJson = WithScale(lineage[^1].RecipeJson, "1.8");
        var gateway = new FakeChatChannelGateway();
        gateway.EnqueueText(refinedJson);
        var service = new RecipeRefinementService(() => gateway);

        var result = await service.RefineAsync(Request());

        Assert.AreEqual(RecipeGenerationOutcome.Drafted, result.Outcome);
        Assert.AreEqual(1, result.RequestCount);
        Assert.AreEqual(1, gateway.Requests.Count);
        Assert.IsNotNull(result.RefinedDraft);
        Assert.AreEqual(refinedJson, result.RefinedDraft.RecipeJson);
        Assert.AreEqual("draft-v2", result.ParentDraftId, "The parent is the refined head.");
        Assert.AreEqual(lineage[^1].CanonicalSha256, result.ParentCanonicalSha256);
        Assert.AreEqual(Feedback, result.FeedbackText);
        Assert.AreEqual(0, result.GuardRestorations.Count, "Scale was hand-set to 1.5 but the feedback names the core.");
        Assert.AreEqual(RecipePromptAssembler.Version, result.PromptTemplateVersion);

        var revision = result.ToRevision();
        Assert.AreEqual(RecipeDraftOrigin.AiRefine, revision.Origin);
        Assert.AreEqual(Feedback, revision.FeedbackText);
        Assert.AreEqual(1, revision.RequestCount);
    }

    [TestMethod]
    public async Task TheRefinedVersionAppendsToARealStoreAsTheOneAiRefineVersion()
    {
        using var directory = new A1TestDirectory();
        var store = new RecipeDraftStore(RecipeDraftTestData.StorePath(directory));
        var rootOutcome = store.SaveVersion(Version("draft-v1", BaseRecipeJson, RecipeDraftOrigin.AiDraft, null, 1));
        var editJson = WithScale(BaseRecipeJson, "1.5");
        var editOutcome = store.AppendVersion(
            rootOutcome.Record.DraftId,
            rootOutcome.Record.CanonicalSha256!,
            RecipeDraftTestData.Revision(RecipeDraftOrigin.HumanEdit, editJson),
            DateTimeOffset.UtcNow);
        var lineage = store.ListLineage(rootOutcome.Record.LineageId);

        var gateway = new FakeChatChannelGateway();
        gateway.EnqueueText(WithScale(editJson, "1.8"));
        var service = new RecipeRefinementService(() => gateway);
        var result = await service.RefineAsync(new RecipeRefinementRequest(
            Guid.NewGuid().ToString("N"),
            OriginalDescription,
            lineage,
            Feedback));

        var appendOutcome = store.AppendVersion(
            result.ParentDraftId!,
            result.ParentCanonicalSha256!,
            result.ToRevision(),
            DateTimeOffset.UtcNow);

        Assert.AreEqual(RecipeDraftOrigin.AiRefine, appendOutcome.Record.Origin);
        Assert.AreEqual(editOutcome.Record.DraftId, appendOutcome.Record.ParentDraftId);
        Assert.AreEqual(Feedback, appendOutcome.Record.FeedbackText);
        var refreshed = store.ListLineage(rootOutcome.Record.LineageId);
        Assert.AreEqual(3, refreshed.Count, "Exactly one ai_refine version landed; the guard added none.");
        RecipeDraftTestData.AssertLinear(refreshed);
    }

    // ---- the guard runs inside the round: an unnamed hand edit comes back restored ----

    [TestMethod]
    public async Task TheGuardRestoresAnUnnamedHandEditBeforeTheResultIsReturned()
    {
        // v2 hand-set scale to 1.5; feedback names the trail, so the AI writing scale back to 1.2 is unnamed
        // collateral and the guard restores 1.5 inside the same round.
        var gateway = new FakeChatChannelGateway();
        gateway.EnqueueText(WithScale(BaseRecipeJson, "1.2"));
        var service = new RecipeRefinementService(() => gateway);

        var result = await service.RefineAsync(new RecipeRefinementRequest(
            Guid.NewGuid().ToString("N"),
            OriginalDescription,
            Lineage(),
            "shorten the trail"));

        Assert.AreEqual(RecipeGenerationOutcome.Drafted, result.Outcome);
        Assert.AreEqual(1, result.GuardRestorations.Count);
        var restoration = result.GuardRestorations[0];
        Assert.AreEqual("stages[travel].modules[core].parameters.scale", restoration.ParameterPath);
        Assert.AreEqual("draft-v2", restoration.SourceDraftId);
        Assert.AreEqual("1.2", restoration.AiValueLiteral);
        Assert.AreEqual("1.5", restoration.RestoredValueLiteral);
        StringAssert.Contains(result.RefinedDraft!.RecipeJson, "\"scale\":1.5");

        var revision = result.ToRevision();
        Assert.AreEqual(1, revision.GuardRestorationCount);
        Assert.AreEqual(restoration.ParameterPath, revision.GuardRestorations[0].ParameterPath);
    }

    // ---- AC-3: the budget is exhausted by validation failures; no version material exists ----

    [TestMethod]
    public async Task ExhaustingTheBudgetKeepsTheLastOutputAndLandsNothing()
    {
        var gateway = new FakeChatChannelGateway();
        gateway.EnqueueText("not json at all");
        gateway.EnqueueText("still not json");
        gateway.EnqueueText("never json");
        var service = new RecipeRefinementService(() => gateway);

        var result = await service.RefineAsync(Request(retryLimit: 2));

        Assert.AreEqual(RecipeGenerationOutcome.ValidationFailed, result.Outcome);
        Assert.AreEqual(3, gateway.Requests.Count, "Exactly 1 + 2 requests.");
        Assert.AreEqual(3, result.RequestCount);
        Assert.IsTrue(result.Issues.Any(static issue => issue.Code == RecipeOutputParser.InvalidJsonCode));
        Assert.IsNotNull(result.LastOutputText);
        Assert.IsNull(result.RefinedDraft);
        Assert.ThrowsExactly<InvalidOperationException>(() => result.ToRevision());
    }

    // ---- AC-4: a network-class failure ends the round after exactly one request ----

    [TestMethod]
    public async Task ANetworkFailureNeverTriggersARetry()
    {
        var gateway = new FakeChatChannelGateway();
        gateway.EnqueueFailure(ChatChannelErrorCode.TimedOut);
        var service = new RecipeRefinementService(() => gateway);

        var result = await service.RefineAsync(Request(retryLimit: 5));

        Assert.AreEqual(RecipeGenerationOutcome.ChannelFailed, result.Outcome);
        Assert.AreEqual(ChatChannelErrorCode.TimedOut, result.ChannelError);
        Assert.AreEqual(1, gateway.Requests.Count, "Exactly one request; the budget never applies to transport.");
        Assert.IsNull(result.RefinedDraft);
        Assert.ThrowsExactly<InvalidOperationException>(() => result.ToRevision());
    }

    [TestMethod]
    public async Task AnUnboundRouteFailsClosedBeforeAnyNetworkWork()
    {
        var gateway = new FakeChatChannelGateway();
        gateway.EnqueueFailure(ChatChannelErrorCode.ChannelUnbound);
        var service = new RecipeRefinementService(() => gateway);

        var result = await service.RefineAsync(Request());

        Assert.AreEqual(RecipeGenerationOutcome.ChannelFailed, result.Outcome);
        Assert.AreEqual(ChatChannelErrorCode.ChannelUnbound, result.ChannelError);
        Assert.AreEqual(1, gateway.Requests.Count);
    }

    // ---- AC-18 / REQ-004-22: single segment, anchored triple, repair keeps the round's own context ----

    [TestMethod]
    public async Task ARepairRequestKeepsTheTripleEchoesTheOutputAndNeverGrowsASecondSegment()
    {
        var lineage = Lineage();
        var refinedJson = WithScale(lineage[^1].RecipeJson, "1.8");
        var gateway = new FakeChatChannelGateway();
        gateway.EnqueueText("I cannot produce JSON today.");
        gateway.EnqueueText(refinedJson);
        var service = new RecipeRefinementService(() => gateway);

        var result = await service.RefineAsync(Request(retryLimit: 2));

        Assert.AreEqual(RecipeGenerationOutcome.Drafted, result.Outcome);
        Assert.AreEqual(2, gateway.Requests.Count);

        var first = gateway.Requests[0].Messages;
        Assert.AreEqual(2, first.Count, "System + the anchored-triple user message; nothing else.");
        StringAssert.Contains(first[0].Content, "Refinement knowledge for the current template catalog:");
        StringAssert.Contains(first[1].Content, OriginalDescription);
        StringAssert.Contains(first[1].Content, lineage[^1].RecipeJson);
        StringAssert.Contains(first[1].Content, Feedback);

        var repair = gateway.Requests[1].Messages;
        Assert.AreEqual(4, repair.Count);
        Assert.AreEqual(ChatRole.Assistant, repair[2].Role);
        StringAssert.Contains(repair[3].Content, "failed VFX Composer Recipe v1 validation");
        StringAssert.Contains(repair[1].Content, Feedback);
        Assert.IsFalse(
            repair.Any(static message => message.Content.Contains("synthetic earlier feedback", StringComparison.Ordinal)),
            "No earlier round's feedback exists anywhere in the request.");
    }

    // ---- diagnostics never carry the description or the feedback ----

    [TestMethod]
    public async Task DiagnosticsNeverCarryTheDescriptionOrTheFeedback()
    {
        var gateway = new FakeChatChannelGateway();
        gateway.EnqueueText(WithScale(BaseRecipeJson, "1.8"));
        var service = new RecipeRefinementService(() => gateway);

        var result = await service.RefineAsync(Request());

        Assert.IsFalse(result.ToString().Contains(Feedback, StringComparison.Ordinal));
        Assert.IsFalse(service.ToString().Contains(Feedback, StringComparison.Ordinal));
        Assert.IsFalse(new RecipeRefinementRequest(
                Guid.NewGuid().ToString("N"),
                OriginalDescription,
                Lineage(),
                Feedback)
            .ToString()
            .Contains(Feedback, StringComparison.Ordinal));
    }

    // ---- request guards: the triple and the chain are validated before any assembly or network work ----

    [TestMethod]
    public void TheRequestRefusesAMissingTripleOrABrokenChain()
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var lineage = Lineage();

        Assert.ThrowsExactly<ArgumentException>(() =>
            new RecipeRefinementRequest(correlationId, " ", lineage, Feedback));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new RecipeRefinementRequest(correlationId, OriginalDescription, lineage, " "));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new RecipeRefinementRequest(correlationId, OriginalDescription, [], Feedback));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new RecipeRefinementRequest(correlationId, OriginalDescription, [lineage[1], lineage[0]], Feedback));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new RecipeRefinementRequest(
                correlationId,
                OriginalDescription,
                lineage,
                new string('a', RecipeChannelLimits.MaximumDescriptionUtf8Bytes + 1)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new RecipeRefinementRequest(
                correlationId,
                OriginalDescription,
                lineage,
                Feedback,
                RecipeChannelLimits.MaximumRetryLimit + 1));
    }

    private sealed class FakeChatChannelGateway : IChatChannelGateway
    {
        private readonly Queue<Func<ChatChannelRequest, ChatChannelResult>> _steps = new();

        public List<ChatChannelRequest> Requests { get; } = [];

        public void EnqueueText(string text) =>
            _steps.Enqueue(request => new ChatChannelResult(request.CorrelationId, text));

        public void EnqueueFailure(ChatChannelErrorCode code) =>
            _steps.Enqueue(_ => throw new ChatChannelException(code));

        public ValueTask<ChatChannelResult> CompleteAsync(
            ChatChannelRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return ValueTask.FromResult(_steps.Dequeue()(request));
        }

        public ValueTask<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The refinement channel must use CompleteAsync.");
    }
}
