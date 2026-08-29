using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.Batch.Core;

namespace VFXComposer.Cli.Tests;

/// <summary>
/// Full-path coverage of the restricted build orchestration with a faked Unity process: the success
/// path, every refusal the Unity entry point can report, every wrapper exit code, cancellation, and
/// the execution-layer write-surface guard. No test starts Unity or touches a project.
/// </summary>
[TestClass]
public sealed class RecipeBuildOrchestratorTests
{
    private const string EffectId = "f2_orchestrator_probe";

    private string _root = string.Empty;

    [TestInitialize]
    public void CreateRoot() => _root = Directory.CreateTempSubdirectory("vfxcomposer-recipe-build-").FullName;

    [TestCleanup]
    public void DeleteRoot()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [TestMethod]
    public async Task AConfirmedDraftBuildsAdvancesToBuiltAndReportsIdentityArtifacts()
    {
        var store = new InMemoryRecipeDraftStore();
        var draft = Confirm(store);
        var runner = SucceedingRunner(draft.CanonicalSha256!);
        var sink = new RecordingSink();

        var decision = await Execute(runner, store, Payload(draft), sink);

        Assert.IsTrue(decision.Succeeded, decision.FailureCode);
        Assert.IsNull(decision.FailureCode);
        Assert.AreEqual(EffectId, decision.Result!.EffectId);
        Assert.AreEqual("Create", decision.Result.DryRunState);
        Assert.AreEqual("1.0.0", decision.Result.DeclaredTemplateCatalogVersion);
        Assert.AreEqual(new string('c', 64), decision.Result.CatalogIdentityHash);
        Assert.AreEqual(RecipeDraftStatus.Built, store.TryGet(draft.DraftId)!.Status);
        CollectionAssert.AreEqual(
            new[] { "effect:" + EffectId, "recipe:sha256:" + draft.CanonicalSha256, "build:sha256:" + new string('b', 64) },
            sink.Artifacts.ToArray());
        CollectionAssert.AreEqual(sink.Progress.OrderBy(static value => value).ToArray(), sink.Progress.ToArray());
        Assert.AreEqual(1, sink.ChildRegistrations.Count);
        Assert.AreEqual(1, sink.ChildClears);
        Assert.AreEqual(0, sink.Logs.Count);
    }

    [TestMethod]
    public async Task TheStagedRequestBindsTheConfirmedHashAndKeepsTheRecipeOutsideTheProject()
    {
        var store = new InMemoryRecipeDraftStore();
        var draft = Confirm(store);
        var runner = SucceedingRunner(draft.CanonicalSha256!);

        await Execute(runner, store, Payload(draft), new RecordingSink());

        using var document = JsonDocument.Parse(runner.ObservedRequestJson!);
        var root = document.RootElement;
        Assert.AreEqual("vfxcomposer.recipe-build-request/1", root.GetProperty("schemaVersion").GetString());
        Assert.AreEqual(draft.DraftId, root.GetProperty("draftId").GetString());
        Assert.AreEqual(draft.CanonicalSha256, root.GetProperty("expectedCanonicalSha256").GetString());

        var stagedRecipe = root.GetProperty("recipePath").GetString()!;
        Assert.IsTrue(stagedRecipe.StartsWith(_root, StringComparison.OrdinalIgnoreCase), stagedRecipe);
        Assert.AreEqual(
            draft.CanonicalSha256,
            RecipeCanonicalJson.ComputeSha256(File.ReadAllText(stagedRecipe)));
    }

    [TestMethod]
    public async Task AManifestEntryWithoutADraftBuildsWithoutTouchingTheDraftStore()
    {
        var runner = SucceedingRunner(RecipeCanonicalJson.ComputeSha256(Recipe()));
        var payload = BatchRecipeBuildPayload.Create(draftId: null, Recipe());

        var decision = await Execute(runner, ThrowingDraftStore.Instance, payload, new RecordingSink());

        Assert.IsTrue(decision.Succeeded, decision.FailureCode);
        Assert.AreEqual("batch-entry", runner.ObservedDraftId);
    }

    [TestMethod]
    public async Task AnAuthoritativeValidationRefusalIsRelayedAndTheDraftBecomesBuildFailed()
    {
        var store = new InMemoryRecipeDraftStore();
        var draft = Confirm(store);
        var runner = new FakeUnityRecipeBuildRunner(
            UnityRecipeBuildExitCodes.StructuredFailure,
            _ => FailureResult("VFXB0008"));
        var sink = new RecordingSink();

        var decision = await Execute(runner, store, Payload(draft), sink);

        Assert.IsFalse(decision.Succeeded);
        Assert.AreEqual("VFXB0008", decision.FailureCode);
        CollectionAssert.Contains(decision.Result!.IssueCodes.ToArray(), "E308");
        Assert.AreEqual(RecipeDraftStatus.BuildFailed, store.TryGet(draft.DraftId)!.Status);
        Assert.AreEqual(1, sink.Logs.Count);
        Assert.AreEqual(0, sink.Artifacts.Count);
    }

    [TestMethod]
    public async Task AHashMismatchBetweenPayloadAndConfirmedDraftNeverStartsUnity()
    {
        var store = new InMemoryRecipeDraftStore();
        var draft = Confirm(store);
        var runner = SucceedingRunner(draft.CanonicalSha256!);
        var tampered = BatchRecipeBuildPayload.Create(draft.DraftId, Recipe(revision: 2));

        var decision = await Execute(runner, store, tampered, new RecordingSink());

        Assert.AreEqual(RecipeBuildFailureCodes.DraftHashMismatch, decision.FailureCode);
        Assert.AreEqual(0, runner.StartCount);
        Assert.AreEqual(RecipeDraftStatus.ConfirmedAwaitingBuild, store.TryGet(draft.DraftId)!.Status);
    }

    [TestMethod]
    public async Task AnUnknownOrUnconfirmedDraftIsRefusedBeforeStaging()
    {
        var store = new InMemoryRecipeDraftStore();
        var runner = SucceedingRunner(new string('a', 64));

        var missing = await Execute(runner, store, BatchRecipeBuildPayload.Create("draft-absent", Recipe()), new RecordingSink());
        Assert.AreEqual(RecipeBuildFailureCodes.DraftNotFound, missing.FailureCode);

        var pending = store.Save(Draft());
        var unconfirmed = await Execute(runner, store, Payload(pending), new RecordingSink());
        Assert.AreEqual(RecipeBuildFailureCodes.DraftNotConfirmed, unconfirmed.FailureCode);
        Assert.AreEqual(0, runner.StartCount);
    }

    [TestMethod]
    public async Task AnUnknownPayloadSchemaIsRefusedBeforeStaging()
    {
        var runner = SucceedingRunner(new string('a', 64));

        var decision = await Execute(
            runner,
            ThrowingDraftStore.Instance,
            "{\"schemaVersion\":\"vfxcomposer.recipe-build-payload/2\"}",
            new RecordingSink());

        Assert.AreEqual(RecipeBuildFailureCodes.PayloadInvalid, decision.FailureCode);
        Assert.AreEqual(0, runner.StartCount);
    }

    [TestMethod]
    [DataRow(UnityRecipeBuildExitCodes.ProjectLockHeld, RecipeBuildFailureCodes.ProjectLockHeld)]
    [DataRow(UnityRecipeBuildExitCodes.TimedOut, RecipeBuildFailureCodes.BuildTimedOut)]
    [DataRow(UnityRecipeBuildExitCodes.UnityMissing, RecipeBuildFailureCodes.UnityUnavailable)]
    [DataRow(UnityRecipeBuildExitCodes.Usage, RecipeBuildFailureCodes.UnityUnavailable)]
    [DataRow(UnityRecipeBuildExitCodes.NoResult, RecipeBuildFailureCodes.ResultUnreadable)]
    public async Task EveryWrapperExitCodeMapsToItsOwnStableCode(int exitCode, string expectedFailureCode)
    {
        var store = new InMemoryRecipeDraftStore();
        var draft = Confirm(store);
        var runner = new FakeUnityRecipeBuildRunner(exitCode, _ => null);

        var decision = await Execute(runner, store, Payload(draft), new RecordingSink());

        Assert.AreEqual(expectedFailureCode, decision.FailureCode);
        Assert.AreEqual(RecipeDraftStatus.BuildFailed, store.TryGet(draft.DraftId)!.Status);
    }

    [TestMethod]
    public async Task ACrashedChildProcessThatStillLeftAResultIsAFailedBuild()
    {
        var store = new InMemoryRecipeDraftStore();
        var draft = Confirm(store);
        var runner = new FakeUnityRecipeBuildRunner(exitCode: 1, _ => FailureResult("VFXB0013"));

        var decision = await Execute(runner, store, Payload(draft), new RecordingSink());

        Assert.AreEqual(RecipeBuildFailureCodes.ProcessFailed, decision.FailureCode);
        Assert.AreEqual(RecipeDraftStatus.BuildFailed, store.TryGet(draft.DraftId)!.Status);
    }

    [TestMethod]
    public async Task AResultThatDriftedInShapeOrIdentityIsRefused()
    {
        // Each sub-case needs its own confirmed draft: a build outcome is terminal, so the second
        // attempt on the same draft would be refused as unconfirmed instead of for its own reason.
        var shapeStore = new InMemoryRecipeDraftStore();
        var shapeDraft = Confirm(shapeStore);
        var unknownField = new FakeUnityRecipeBuildRunner(
            UnityRecipeBuildExitCodes.Succeeded,
            _ => "{\"schemaVersion\":\"vfxcomposer.recipe-build-result/1\",\"draftId\":\"d\",\"succeeded\":true,\"surprise\":1}");
        Assert.AreEqual(
            RecipeBuildFailureCodes.ResultUnreadable,
            (await Execute(unknownField, shapeStore, Payload(shapeDraft), new RecordingSink())).FailureCode);

        var identityStore = new InMemoryRecipeDraftStore();
        var identityDraft = Confirm(identityStore);
        var wrongHash = new FakeUnityRecipeBuildRunner(
            UnityRecipeBuildExitCodes.Succeeded,
            _ => SuccessResult(new string('9', 64), EffectId));
        Assert.AreEqual(
            RecipeBuildFailureCodes.ResultIdentityMismatch,
            (await Execute(wrongHash, identityStore, Payload(identityDraft), new RecordingSink())).FailureCode);
    }

    [TestMethod]
    [DataRow("../escape", DisplayName = "TraversalIsRefused")]
    [DataRow("nested/effect", DisplayName = "SeparatorIsRefused")]
    [DataRow("C:/absolute", DisplayName = "AbsolutePathIsRefused")]
    [DataRow("con", DisplayName = "ReservedDeviceNameIsRefused")]
    [DataRow("nul", DisplayName = "ReservedNulIsRefused")]
    [DataRow("com9", DisplayName = "ReservedComPortIsRefused")]
    [DataRow("Uppercase", DisplayName = "UppercaseIsRefused")]
    [DataRow("_leading", DisplayName = "LeadingUnderscoreIsRefused")]
    public async Task AReportedSuccessOutsideTheClosedWriteSurfaceIsRefused(string effectId)
    {
        var store = new InMemoryRecipeDraftStore();
        var draft = Confirm(store);
        var runner = new FakeUnityRecipeBuildRunner(
            UnityRecipeBuildExitCodes.Succeeded,
            _ => SuccessResult(draft.CanonicalSha256!, effectId));

        var decision = await Execute(runner, store, Payload(draft), new RecordingSink());

        Assert.AreEqual(RecipeBuildFailureCodes.ResultIdentityMismatch, decision.FailureCode, effectId);
        Assert.AreEqual(RecipeDraftStatus.BuildFailed, store.TryGet(draft.DraftId)!.Status);
    }

    [TestMethod]
    public void TheWriteSurfaceTwinAcceptsOnlyTheThreeMembersOfOneEffect()
    {
        Assert.IsTrue(RecipeBuildWriteSurface.DescribesExactly(
            "fireball_2d",
            "Assets/VFX/Generated/fireball_2d/VFX_fireball_2d.prefab",
            "ProjectSettings/VFXComposer/BuildManifests/fireball_2d.manifest.json",
            "Assets/VFX/Recipes/fireball_2d.json"));

        Assert.IsFalse(RecipeBuildWriteSurface.DescribesExactly(
            "fireball_2d",
            "Assets/VFX/Shared/fireball_2d/VFX_fireball_2d.prefab",
            "ProjectSettings/VFXComposer/BuildManifests/fireball_2d.manifest.json",
            "Assets/VFX/Recipes/fireball_2d.json"));

        Assert.IsFalse(RecipeBuildWriteSurface.DescribesExactly(
            "fireball_2d",
            "Assets/VFX/Generated/fireball_2d/nested/VFX_fireball_2d.prefab",
            "ProjectSettings/VFXComposer/BuildManifests/fireball_2d.manifest.json",
            "Assets/VFX/Recipes/fireball_2d.json"));

        Assert.IsFalse(RecipeBuildWriteSurface.DescribesExactly(
            "fireball_2d",
            "Assets/VFX/Generated/fireball_2d/VFX_fireball_2d.prefab",
            "ProjectSettings/VFXComposer/VfxProjectRules.json",
            "Assets/VFX/Recipes/fireball_2d.json"));

        Assert.IsFalse(RecipeBuildWriteSurface.DescribesExactly(
            "fireball_2d",
            "Assets/VFX/Generated/fireball_2d/VFX_fireball_2d.prefab",
            "ProjectSettings/VFXComposer/BuildManifests/fireball_2d.manifest.json",
            "Assets/VFX/Recipes/Projectile/fireball_2d.json"));
    }

    [TestMethod]
    public async Task CancellationTerminatesTheChildAndLeavesTheDraftConfirmed()
    {
        var store = new InMemoryRecipeDraftStore();
        var draft = Confirm(store);
        var runner = new FakeUnityRecipeBuildRunner(UnityRecipeBuildExitCodes.Succeeded, _ => null) { BlockForever = true };
        using var cancellation = new CancellationTokenSource();
        var sink = new RecordingSink();

        var pending = Execute(runner, store, Payload(draft), sink, cancellation.Token);
        await runner.Started.Task.ConfigureAwait(false);
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => pending);
        Assert.AreEqual(1, runner.LastProcess!.TerminateCount);
        Assert.AreEqual(1, sink.ChildClears);
        Assert.AreEqual(RecipeDraftStatus.ConfirmedAwaitingBuild, store.TryGet(draft.DraftId)!.Status);
    }

    [TestMethod]
    public async Task AProcessThatCannotBeStartedAtAllIsReportedAsUnityUnavailable()
    {
        var store = new InMemoryRecipeDraftStore();
        var draft = Confirm(store);
        var runner = new FakeUnityRecipeBuildRunner(UnityRecipeBuildExitCodes.Succeeded, _ => null) { ThrowOnStart = true };

        var decision = await Execute(runner, store, Payload(draft), new RecordingSink());

        Assert.AreEqual(RecipeBuildFailureCodes.UnityUnavailable, decision.FailureCode);
    }

    [TestMethod]
    public void ThePayloadRoundTripsAndRefusesAnUnknownSchema()
    {
        var payload = BatchRecipeBuildPayload.Create("draft-1", Recipe());
        var content = BatchRecipeBuildPayload.Parse(payload);

        Assert.AreEqual("draft-1", content.DraftId);
        Assert.AreEqual(RecipeCanonicalJson.ComputeSha256(Recipe()), content.CanonicalSha256);
        Assert.AreEqual(Recipe(), content.RecipeJson);
        Assert.IsFalse(content.ToString().Contains("recipeVersion", StringComparison.Ordinal));

        Assert.IsNull(BatchRecipeBuildPayload.Parse(BatchRecipeBuildPayload.Create(draftId: null, Recipe())).DraftId);
        Assert.ThrowsExactly<InvalidDataException>(() =>
            BatchRecipeBuildPayload.Parse("{\"schemaVersion\":\"vfxcomposer.recipe-build-payload/9\"}"));
    }

    private Task<RecipeBuildDecision> Execute(
        IUnityRecipeBuildRunner runner,
        IRecipeDraftStore store,
        string payload,
        RecordingSink sink,
        CancellationToken cancellationToken = default) =>
        new RecipeBuildOrchestrator(runner, () => store, new RecipeBuildOptions { TimeoutSeconds = 30 })
            .ExecuteAsync(payload, Path.Combine(_root, "job"), sink, cancellationToken);

    private static FakeUnityRecipeBuildRunner SucceedingRunner(string recipeHash) =>
        new(UnityRecipeBuildExitCodes.Succeeded, _ => SuccessResult(recipeHash, EffectId));

    private static string Payload(RecipeDraftRecord draft) =>
        BatchRecipeBuildPayload.Create(draft.DraftId, draft.RecipeJson);

    private static RecipeDraftRecord Confirm(InMemoryRecipeDraftStore store)
    {
        var saved = store.Save(Draft());
        return store.Confirm(saved.DraftId, saved.CanonicalSha256!);
    }

    private static RecipeDraftRecord Draft() => new(
        "draft-" + Guid.NewGuid().ToString("N"),
        RecipeDraftStatus.PendingConfirmation,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow,
        Guid.NewGuid().ToString("N"),
        "prompt-template-1",
        "1.0.0",
        Recipe(),
        RecipeCanonicalJson.ComputeSha256(Recipe()),
        EffectId,
        "projectile",
        "2d",
        "mobile_medium",
        Array.Empty<RecipeValidationIssue>(),
        1);

    private static string Recipe(int revision = 1) =>
        "{\"recipeVersion\":1,\"revision\":" + revision + ",\"id\":\"" + EffectId + "\",\"dimension\":\"2d\"," +
        "\"archetype\":\"projectile\",\"targetProfile\":\"mobile_medium\",\"randomSeed\":7,\"stages\":[]," +
        "\"metadata\":{\"createdBy\":\"test\",\"templateCatalogVersion\":\"1.0.0\"}}";

    private static string SuccessResult(string recipeHash, string effectId) =>
        "{\"schemaVersion\":\"vfxcomposer.recipe-build-result/1\",\"draftId\":\"draft-x\",\"succeeded\":true," +
        "\"failureCode\":null,\"effectId\":\"" + effectId + "\",\"recipeHash\":\"" + recipeHash + "\"," +
        "\"buildHash\":\"" + new string('b', 64) + "\",\"recipeRevision\":1,\"compilerVersion\":\"0.1.0\"," +
        "\"unityVersion\":\"2022.3.62f3c1\",\"declaredTemplateCatalogVersion\":\"1.0.0\"," +
        "\"catalogIdentityHash\":\"" + new string('c', 64) + "\"," +
        "\"prefabPath\":\"Assets/VFX/Generated/" + effectId + "/VFX_" + effectId + ".prefab\"," +
        "\"buildManifestPath\":\"Assets/VFX/Generated/" + effectId + "/BuildManifest.json\"," +
        "\"ownershipManifestPath\":\"ProjectSettings/VFXComposer/BuildManifests/" + effectId + ".manifest.json\"," +
        "\"provenanceRecipePath\":\"Assets/VFX/Recipes/" + effectId + ".json\"," +
        "\"dryRunState\":\"Create\",\"cleanedResiduePaths\":[],\"issues\":[]}";

    private static string FailureResult(string failureCode) =>
        "{\"schemaVersion\":\"vfxcomposer.recipe-build-result/1\",\"draftId\":\"draft-x\",\"succeeded\":false," +
        "\"failureCode\":\"" + failureCode + "\",\"effectId\":\"" + EffectId + "\",\"recipeHash\":null," +
        "\"buildHash\":null,\"recipeRevision\":1,\"compilerVersion\":\"0.1.0\",\"unityVersion\":\"2022.3.62f3c1\"," +
        "\"declaredTemplateCatalogVersion\":\"1.0.0\",\"catalogIdentityHash\":null,\"prefabPath\":null," +
        "\"buildManifestPath\":null,\"ownershipManifestPath\":null,\"provenanceRecipePath\":null," +
        "\"dryRunState\":null,\"cleanedResiduePaths\":[]," +
        "\"issues\":[{\"code\":\"E308\",\"severity\":\"Error\",\"path\":\"/stages/travel/modules/core/templateId\"," +
        "\"message\":\"Template is not in the catalog.\",\"actualValue\":null,\"allowedRange\":null}]}";

    /// <summary>Stands in for the Unity batchmode wrapper: it writes the result file the real one would.</summary>
    private sealed class FakeUnityRecipeBuildRunner : IUnityRecipeBuildRunner
    {
        private readonly int _exitCode;
        private readonly Func<UnityRecipeBuildLaunch, string?> _writeResult;

        internal FakeUnityRecipeBuildRunner(int exitCode, Func<UnityRecipeBuildLaunch, string?> writeResult)
        {
            _exitCode = exitCode;
            _writeResult = writeResult;
        }

        internal bool BlockForever { get; init; }

        internal bool ThrowOnStart { get; init; }

        internal int StartCount { get; private set; }

        internal string? ObservedRequestJson { get; private set; }

        internal string? ObservedDraftId { get; private set; }

        internal FakeProcess? LastProcess { get; private set; }

        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IUnityRecipeBuildProcess Start(UnityRecipeBuildLaunch launch)
        {
            if (ThrowOnStart)
            {
                throw new IOException("The build wrapper could not be started.");
            }

            StartCount++;
            ObservedRequestJson = File.ReadAllText(launch.RequestPath);
            using (var document = JsonDocument.Parse(ObservedRequestJson))
            {
                ObservedDraftId = document.RootElement.GetProperty("draftId").GetString();
            }

            var body = _writeResult(launch);
            if (body is not null)
            {
                File.WriteAllText(launch.ResultPath, body, new UTF8Encoding(false));
            }

            LastProcess = new FakeProcess(_exitCode, BlockForever, Started);
            return LastProcess;
        }
    }

    private sealed class FakeProcess : IUnityRecipeBuildProcess
    {
        private readonly int _exitCode;
        private readonly bool _blockForever;
        private readonly TaskCompletionSource _started;

        internal FakeProcess(int exitCode, bool blockForever, TaskCompletionSource started)
        {
            _exitCode = exitCode;
            _blockForever = blockForever;
            _started = started;
        }

        public int ProcessId => 424_242;

        public DateTimeOffset StartUtc { get; } = DateTimeOffset.UtcNow;

        internal int TerminateCount { get; private set; }

        public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
        {
            _started.TrySetResult();
            if (_blockForever)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }

            return _exitCode;
        }

        public void Terminate() => TerminateCount++;

        public void Dispose()
        {
        }
    }

    private sealed class RecordingSink : IRecipeBuildSink
    {
        internal List<int> Progress { get; } = [];

        internal List<string> Artifacts { get; } = [];

        internal List<string> Logs { get; } = [];

        internal List<(int ProcessId, DateTimeOffset StartUtc)> ChildRegistrations { get; } = [];

        internal int ChildClears { get; private set; }

        public void ReportProgress(int progressPermille) => Progress.Add(progressPermille);

        public void ReportLog(string level, string diagnosticCode) => Logs.Add(level + ":" + diagnosticCode);

        public void ReportArtifact(string artifactId) => Artifacts.Add(artifactId);

        public void RegisterChildProcess(int processId, DateTimeOffset processStartUtc) =>
            ChildRegistrations.Add((processId, processStartUtc));

        public void ClearChildProcess() => ChildClears++;
    }

    /// <summary>Proves the manifest-entry form never reaches draft storage.</summary>
    private sealed class ThrowingDraftStore : IRecipeDraftStore
    {
        internal static ThrowingDraftStore Instance { get; } = new();

        public RecipeDraftRecord Save(RecipeDraftRecord record) => throw new InvalidOperationException();

        public RecipeDraftRecord Confirm(string draftId, string canonicalSha256) => throw new InvalidOperationException();

        public RecipeDraftRecord? TryGet(string draftId) => throw new InvalidOperationException();

        public IReadOnlyList<RecipeDraftRecord> ListConfirmedAwaitingBuild() => throw new InvalidOperationException();

        public RecipeDraftRecord MarkBuilt(string draftId, string canonicalSha256) => throw new InvalidOperationException();

        public RecipeDraftRecord MarkBuildFailed(string draftId, string canonicalSha256) => throw new InvalidOperationException();
    }
}
