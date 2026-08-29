using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Chat;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Recipes;

namespace VFXComposer.AI.Tests.Recipes;

[TestClass]
public sealed class RecipeGenerationServiceTests
{
    private static string ValidRecipeJson => RecipeTemplateCatalogSnapshot.Default.CanonicalExampleJson;

    [TestMethod]
    public void TheCommittedCanonicalExamplePassesL1Validation()
    {
        var issues = RecipeL1Validator.Validate(ValidRecipeJson);
        Assert.AreEqual(0, issues.Count, string.Join("; ", issues.Select(static issue => issue.Code + " " + issue.Path)));
    }

    [TestMethod]
    public async Task FencedOutputProducesAPendingDraftBoundToTheCanonicalHash()
    {
        var gateway = new FakeChatChannelGateway();
        gateway.EnqueueText("Here is the recipe:\n```json\n" + ValidRecipeJson + "\n```\nEnjoy!");
        var service = new RecipeGenerationService(() => gateway);

        var result = await service.GenerateAsync(Request());

        Assert.AreEqual(RecipeGenerationOutcome.Drafted, result.Outcome);
        Assert.AreEqual(1, result.RequestCount);
        Assert.AreEqual(0, result.Attempts[0].ErrorCodes.Count);
        Assert.IsNotNull(result.Draft);
        Assert.AreEqual(RecipeCanonicalJson.Canonicalize(ValidRecipeJson), result.Draft.RecipeJson);
        Assert.AreEqual(RecipeCanonicalJson.ComputeSha256(ValidRecipeJson), result.Draft.CanonicalSha256);
        Assert.AreEqual("fireball_2d", result.Draft.RecipeId);
        Assert.AreEqual("projectile", result.Draft.Archetype);
        Assert.AreEqual("2d", result.Draft.Dimension);
        Assert.AreEqual(RecipePromptTemplate.Version, result.PromptTemplateVersion);
    }

    [TestMethod]
    public async Task InvalidJsonTriggersOneRepairRequestInsideTheBudget()
    {
        var gateway = new FakeChatChannelGateway();
        gateway.EnqueueText("I am sorry, I cannot produce JSON today.");
        gateway.EnqueueText(ValidRecipeJson);
        var service = new RecipeGenerationService(() => gateway);

        var result = await service.GenerateAsync(Request(retryLimit: 2));

        Assert.AreEqual(RecipeGenerationOutcome.Drafted, result.Outcome);
        Assert.AreEqual(2, result.RequestCount);
        CollectionAssert.Contains(result.Attempts[0].ErrorCodes.ToList(), "E104");
        Assert.AreEqual(2, gateway.Requests.Count);
        var repairText = gateway.Requests[1].Messages[^1].Content;
        Assert.IsTrue(repairText.Contains("E104", StringComparison.Ordinal));
        Assert.IsTrue(repairText.Contains("failed VFX Composer Recipe v1 validation", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SchemaViolationsExhaustTheBudgetIntoAFailedFinalState()
    {
        var gateway = new FakeChatChannelGateway();
        gateway.EnqueueText("{}");
        gateway.EnqueueText("{}");
        var service = new RecipeGenerationService(() => gateway);

        var result = await service.GenerateAsync(Request(retryLimit: 1));

        Assert.AreEqual(RecipeGenerationOutcome.ValidationFailed, result.Outcome);
        Assert.AreEqual(2, result.RequestCount);
        Assert.AreEqual(2, gateway.Requests.Count);
        Assert.AreEqual("{}", result.LastOutputText);
        Assert.IsTrue(result.Issues.Any(static issue => issue.Code == "E101"));
        Assert.IsTrue(result.Attempts.All(static attempt => attempt.ErrorCodes.Contains("E101")));
    }

    [TestMethod]
    public async Task ARetryBudgetOfZeroAllowsExactlyOneRequest()
    {
        var gateway = new FakeChatChannelGateway();
        gateway.EnqueueText("not json at all");
        var service = new RecipeGenerationService(() => gateway);

        var result = await service.GenerateAsync(Request(retryLimit: 0));

        Assert.AreEqual(RecipeGenerationOutcome.ValidationFailed, result.Outcome);
        Assert.AreEqual(1, gateway.Requests.Count);
    }

    [TestMethod]
    public async Task NetworkFailuresNeverTriggerARetry()
    {
        var gateway = new FakeChatChannelGateway();
        gateway.EnqueueFailure(ChatChannelErrorCode.TransportFailed);
        var service = new RecipeGenerationService(() => gateway);

        var result = await service.GenerateAsync(Request(retryLimit: 5));

        Assert.AreEqual(RecipeGenerationOutcome.ChannelFailed, result.Outcome);
        Assert.AreEqual(ChatChannelErrorCode.TransportFailed, result.ChannelError);
        Assert.AreEqual(1, gateway.Requests.Count);
        Assert.AreEqual(0, result.Attempts.Count);
    }

    [TestMethod]
    public async Task CancellationDuringARepairLoopEndsTheActionImmediately()
    {
        var gateway = new FakeChatChannelGateway();
        gateway.EnqueueText("not json at all");
        gateway.EnqueueFailure(ChatChannelErrorCode.Cancelled);
        var service = new RecipeGenerationService(() => gateway);

        var result = await service.GenerateAsync(Request(retryLimit: 5));

        Assert.AreEqual(RecipeGenerationOutcome.Cancelled, result.Outcome);
        Assert.AreEqual(ChatChannelErrorCode.Cancelled, result.ChannelError);
        Assert.AreEqual(2, gateway.Requests.Count);
        Assert.AreEqual(1, result.Attempts.Count);
    }

    [TestMethod]
    public async Task APreCancelledTokenSurfacesTheCancelledOutcome()
    {
        var gateway = new FakeChatChannelGateway { ObserveCancellation = true };
        gateway.EnqueueText(ValidRecipeJson);
        var service = new RecipeGenerationService(() => gateway);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await service.GenerateAsync(Request(), cancellation.Token);

        Assert.AreEqual(RecipeGenerationOutcome.Cancelled, result.Outcome);
    }

    [TestMethod]
    public async Task TheStructuredOutputFormCarriesTheSchemaAndConvergesOnTheSamePipeline()
    {
        var gateway = new FakeChatChannelGateway();
        gateway.EnqueueStructured(ValidRecipeJson);
        var service = new RecipeGenerationService(() => gateway);

        var result = await service.GenerateAsync(Request(form: RecipeRequestForm.StructuredOutput));

        Assert.AreEqual(RecipeGenerationOutcome.Drafted, result.Outcome);
        Assert.IsNotNull(gateway.Requests[0].StructuredOutput);
        Assert.AreEqual("vfx-recipe-v1", gateway.Requests[0].StructuredOutput!.Name);
        Assert.AreEqual(RecipeCanonicalJson.ComputeSha256(ValidRecipeJson), result.Draft!.CanonicalSha256);
    }

    [TestMethod]
    public async Task ThePromptEmbedsTheCatalogButNeverLeaksIntoDiagnostics()
    {
        var gateway = new FakeChatChannelGateway();
        gateway.EnqueueText(ValidRecipeJson);
        var service = new RecipeGenerationService(() => gateway);

        var result = await service.GenerateAsync(Request());

        var systemPrompt = gateway.Requests[0].Messages[0].Content;
        Assert.IsTrue(systemPrompt.Contains("PFT_2D_FireCore", StringComparison.Ordinal));
        Assert.IsTrue(gateway.Requests[0].Messages[1].Content.Contains("synthetic effect description", StringComparison.Ordinal));
        Assert.IsFalse(result.ToString().Contains("synthetic", StringComparison.Ordinal));
        Assert.IsFalse(service.ToString().Contains("synthetic", StringComparison.Ordinal));
    }

    private static RecipeGenerationRequest Request(
        int retryLimit = RecipeChannelLimits.DefaultRetryLimit,
        RecipeRequestForm form = RecipeRequestForm.PlainText) =>
        new(Guid.NewGuid().ToString("N"), "synthetic effect description", retryLimit, form);

    private sealed class FakeChatChannelGateway : IChatChannelGateway
    {
        private readonly Queue<Func<ChatChannelRequest, ChatChannelResult>> _steps = new();

        public List<ChatChannelRequest> Requests { get; } = [];

        public bool ObserveCancellation { get; init; }

        public void EnqueueText(string text) =>
            _steps.Enqueue(request => new ChatChannelResult(request.CorrelationId, text));

        public void EnqueueStructured(string json) =>
            _steps.Enqueue(request =>
            {
                using var document = JsonDocument.Parse(json);
                return new ChatChannelResult(request.CorrelationId, json, tokenUsage: null, document.RootElement.Clone());
            });

        public void EnqueueFailure(ChatChannelErrorCode code) =>
            _steps.Enqueue(_ => throw new ChatChannelException(code));

        public ValueTask<ChatChannelResult> CompleteAsync(
            ChatChannelRequest request,
            CancellationToken cancellationToken = default)
        {
            if (ObserveCancellation && cancellationToken.IsCancellationRequested)
            {
                throw new ChatChannelException(ChatChannelErrorCode.Cancelled);
            }

            Requests.Add(request);
            return ValueTask.FromResult(_steps.Dequeue()(request));
        }

        public ValueTask<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The recipe channel must use CompleteAsync.");
    }
}
