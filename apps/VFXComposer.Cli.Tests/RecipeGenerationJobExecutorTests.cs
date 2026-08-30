using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.Batch.Core;
using VFXComposer.Jobs;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Cli.Tests;

[TestClass]
public sealed class RecipeGenerationJobExecutorTests
{
    [TestMethod]
    public void ExecutorDeclaresItsKindAndNeedsNoProjectLock()
    {
        var executor = Executor(new FakeRecipeGenerationChannel(request =>
            CliTestHarness.DraftedResult(request.CorrelationId, "fx-a")), new InMemoryRecipeDraftStore());

        Assert.AreEqual(BatchJobKinds.RecipeGeneration, executor.JobKind);
        Assert.IsFalse(
            executor.RequiresProjectLock,
            "Generation never opens the Unity project, so it must not block on the project lock.");
    }

    [TestMethod]
    public async Task SuccessfulGenerationPersistsADraftAndReportsItsIdentities()
    {
        var drafts = new InMemoryRecipeDraftStore();
        var channel = new FakeRecipeGenerationChannel(request =>
            CliTestHarness.DraftedResult(request.CorrelationId, "fx-alpha"));
        var (store, job) = await RunAsync(Executor(channel, drafts), Payload("a calm blue spark"));

        Assert.AreEqual(JobStatusStates.Succeeded, job.State);
        Assert.IsNull(job.FinalDiagnosticCode);
        Assert.AreEqual(1000, job.LastProgressPermille);
        var record = drafts.Records.Single();
        Assert.AreEqual(RecipeDraftStatus.PendingConfirmation, record.Status);
        CollectionAssert.AreEqual(
            new[] { record.DraftId, "sha256:" + record.CanonicalSha256 },
            job.ArtifactIds.ToArray());
        Assert.IsTrue(
            channel.Descriptions.Single().StartsWith("a calm blue spark", StringComparison.Ordinal),
            "The prompt reaches the channel as the effect description.");
        Assert.IsFalse(
            store.ReadEvents(job.JobId).Any(storeEvent =>
                (storeEvent.ArtifactId ?? string.Empty).Contains("calm blue", StringComparison.Ordinal)),
            "Events must never carry prompt content.");
    }

    [TestMethod]
    public async Task ValidationExhaustionSettlesTheJobAsFailed()
    {
        var (_, job) = await RunAsync(
            Executor(
                new FakeRecipeGenerationChannel(request => CliTestHarness.ValidationFailedResult(request.CorrelationId)),
                new InMemoryRecipeDraftStore()),
            Payload("a broken effect"));

        Assert.AreEqual(JobStatusStates.Failed, job.State);
        Assert.AreEqual(JobQueueDiagnosticCodes.GenerationValidationExhausted, job.FinalDiagnosticCode);
    }

    [TestMethod]
    public async Task ChannelFailureSettlesTheJobAsFailedWithoutADraft()
    {
        var drafts = new InMemoryRecipeDraftStore();
        var (_, job) = await RunAsync(
            Executor(
                new FakeRecipeGenerationChannel(request => CliTestHarness.ChannelFailedResult(request.CorrelationId)),
                drafts),
            Payload("an unreachable provider"));

        Assert.AreEqual(JobStatusStates.Failed, job.State);
        Assert.AreEqual(JobQueueDiagnosticCodes.GenerationChannelFailed, job.FinalDiagnosticCode);
        Assert.AreEqual(0, drafts.Records.Count);
    }

    [TestMethod]
    public async Task MalformedPayloadFailsWithTheStableExecutionCode()
    {
        var (_, job) = await RunAsync(
            Executor(
                new FakeRecipeGenerationChannel(request => CliTestHarness.DraftedResult(request.CorrelationId, "fx-a")),
                new InMemoryRecipeDraftStore()),
            "{\"schemaVersion\":\"vfxcomposer.generate-payload/9\"}");

        Assert.AreEqual(JobStatusStates.Failed, job.State);
        Assert.AreEqual(JobQueueDiagnosticCodes.ExecutionFailed, job.FinalDiagnosticCode);
    }

    [TestMethod]
    public async Task ACancelledChannelOutcomeWithoutAHostRequestSettlesAsFailed()
    {
        var store = new JobStore(CliTestHarness.CreateDirectory());
        var enqueued = store.Enqueue(new JobEnqueueRequest(
            JobSourceEntries.Cli,
            BatchJobKinds.RecipeGeneration,
            Payload("a cancelled generation")));
        var executor = Executor(
            new FakeRecipeGenerationChannel(request => RecipeGenerationResult.ChannelFailed(
                request.CorrelationId,
                VFXComposer.AI.Contracts.Chat.ChatChannelErrorCode.Cancelled,
                Array.Empty<RecipeGenerationAttempt>(),
                "prompt-template-test",
                "catalog-test")),
            new InMemoryRecipeDraftStore());
        await using var host = new JobQueueHost(store, [executor], CliTestHarness.FastHostOptions);
        host.Start();

        await WaitForTerminalAsync(store, enqueued.JobId);

        var job = store.ReadSnapshot().Jobs.Single(record => record.JobId == enqueued.JobId);
        Assert.AreEqual(JobStatusStates.Failed, job.State);
        Assert.AreEqual(
            JobQueueDiagnosticCodes.ExecutionFailed,
            job.FinalDiagnosticCode,
            "A channel-reported cancellation surfaces as OperationCanceled and, with no host cancel, settles under the generic execution code — not a generation-outcome code.");
    }

    private static RecipeGenerationJobExecutor Executor(
        IRecipeGenerationChannel channel,
        IRecipeDraftStore drafts) =>
        new(() => channel, () => drafts);

    private static string Payload(string prompt) =>
        BatchGenerationPayload.Create(new BatchManifestItem(
            "alpha",
            BatchItemKinds.Prompt,
            prompt,
            null,
            BatchConstraints.Empty));

    private static async Task<(JobStore Store, JobRecord Job)> RunAsync(IJobExecutor executor, string payload)
    {
        var store = new JobStore(CliTestHarness.CreateDirectory());
        var enqueued = store.Enqueue(new JobEnqueueRequest(
            JobSourceEntries.Cli,
            BatchJobKinds.RecipeGeneration,
            payload));
        await using (var host = new JobQueueHost(store, [executor], CliTestHarness.FastHostOptions))
        {
            host.Start();
            await WaitForTerminalAsync(store, enqueued.JobId);
        }

        return (store, store.ReadSnapshot().Jobs.Single(record => record.JobId == enqueued.JobId));
    }

    private static async Task WaitForTerminalAsync(JobStore store, string jobId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (!store.ReadSnapshot().Jobs.Single(record => record.JobId == jobId).IsTerminal)
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("The job did not reach a terminal state in time.");
            }

            await Task.Delay(25);
        }
    }
}
