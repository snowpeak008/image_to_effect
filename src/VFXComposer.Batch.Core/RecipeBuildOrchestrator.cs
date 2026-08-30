using System.Text;
using System.Text.Json;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.Jobs;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Batch.Core;

/// <summary>Execution-layer stable codes for the build orchestration around the Unity entry point.</summary>
public static class RecipeBuildFailureCodes
{
    public const string PayloadInvalid = "VFXB1001";
    public const string DraftNotFound = "VFXB1002";
    public const string DraftNotConfirmed = "VFXB1003";
    public const string DraftHashMismatch = "VFXB1004";
    public const string ProjectLockHeld = "VFXB1005";
    public const string BuildTimedOut = "VFXB1006";
    public const string UnityUnavailable = "VFXB1007";
    public const string ResultUnreadable = "VFXB1008";
    public const string ProcessFailed = "VFXB1009";
    public const string ResultIdentityMismatch = "VFXB1010";
    public const string StagingFailed = "VFXB1011";
    public const string DraftTransitionFailed = "VFXB1012";

    /// <summary>
    /// Artifact-identity prefix that carries the precise build code onto the queue entry. The queue
    /// settles a failed build under its own closed <c>VFXJ</c> vocabulary, and the Unity result file
    /// lives in the job scratch directory that completion deletes, so this artifact is the only
    /// place the exact code survives on a surface a queue reader can reach.
    /// </summary>
    public const string FailureArtifactPrefix = "failure:";

    /// <summary>The artifact identity that reports <paramref name="code"/> on the queue entry.</summary>
    public static string FailureArtifact(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return FailureArtifactPrefix + code;
    }
}

/// <summary>
/// The observation surface the orchestrator needs from its host. It mirrors the queue execution
/// context so the orchestrator itself stays testable without a live queue.
/// </summary>
public interface IRecipeBuildSink
{
    void ReportProgress(int progressPermille);

    void ReportLog(string level, string diagnosticCode);

    void ReportArtifact(string artifactId);

    void RegisterChildProcess(int processId, DateTimeOffset processStartUtc);

    void ClearChildProcess();
}

/// <summary>Bounds for one controlled build.</summary>
public sealed record RecipeBuildOptions
{
    /// <summary>
    /// Wall-clock bound handed to the wrapper. Unity batchmode start-up is minutes on a cold
    /// Library, so this is deliberately larger than the queue's own default job timeout budget.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 1800;
}

/// <summary>Terminal outcome of one orchestrated build.</summary>
public sealed record RecipeBuildDecision(
    bool Succeeded,
    string? FailureCode,
    UnityRecipeBuildResult? Result)
{
    public override string ToString() => "RecipeBuildDecision(" + (Succeeded ? "succeeded" : FailureCode ?? "failed") + ")";
}

/// <summary>Carries the stable build code as the inner cause of the queue's execution failure.</summary>
public sealed class RecipeBuildFailureException : Exception
{
    public RecipeBuildFailureException(string code)
        : base("The restricted recipe build failed: " + code)
    {
        Code = code;
    }

    public string Code { get; }

    public override string ToString() => "RecipeBuildFailureException(" + Code + ")";
}

/// <summary>
/// Drives one restricted build: stage the confirmed recipe outside the Unity project, run one
/// short-lived batchmode process, read its structured result and advance the draft to exactly one
/// terminal build state. A failed build is never retried here (ADR-007): the queue settles the job
/// and the user re-enqueues explicitly.
/// </summary>
public sealed class RecipeBuildOrchestrator
{
    private const int ProgressStaged = 100;
    private const int ProgressUnityStarted = 200;
    private const int ProgressUnityExited = 800;
    private const int ProgressDraftAdvanced = 950;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private readonly IUnityRecipeBuildRunner _runner;
    private readonly Func<IRecipeDraftStore> _acquireDraftStore;
    private readonly RecipeBuildOptions _options;

    public RecipeBuildOrchestrator(
        IUnityRecipeBuildRunner runner,
        Func<IRecipeDraftStore> acquireDraftStore,
        RecipeBuildOptions? options = null)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _acquireDraftStore = acquireDraftStore ?? throw new ArgumentNullException(nameof(acquireDraftStore));
        _options = options ?? new RecipeBuildOptions();
    }

    public override string ToString() => "RecipeBuildOrchestrator";

    /// <summary>
    /// Executes one build. Cancellation propagates as <see cref="OperationCanceledException"/> and
    /// leaves the draft in its confirmed state so the user can re-enqueue it. Every other refusal
    /// leaves its precise build code on the queue entry before returning.
    /// </summary>
    public async Task<RecipeBuildDecision> ExecuteAsync(
        string payload,
        string temporaryDirectory,
        IRecipeBuildSink sink,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryDirectory);
        ArgumentNullException.ThrowIfNull(sink);
        RecipeBuildDecision decision;
        try
        {
            decision = await BuildAsync(payload, temporaryDirectory, sink, cancellationToken).ConfigureAwait(false);
        }
        catch (RecipeBuildFailureException exception)
        {
            ReportFailure(sink, exception.Code);
            throw;
        }

        if (decision.Succeeded && decision.Result is not null)
        {
            ReportArtifacts(sink, decision.Result);
            return decision;
        }

        ReportFailure(sink, decision.FailureCode ?? RecipeBuildFailureCodes.ProcessFailed);
        return decision;
    }

    private async Task<RecipeBuildDecision> BuildAsync(
        string payload,
        string temporaryDirectory,
        IRecipeBuildSink sink,
        CancellationToken cancellationToken)
    {
        BatchRecipeBuildPayloadContent content;
        try
        {
            content = BatchRecipeBuildPayload.Parse(payload);
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException or ArgumentException)
        {
            return Fail(RecipeBuildFailureCodes.PayloadInvalid);
        }

        var draftStore = content.DraftId is null ? null : _acquireDraftStore();
        RecipeDraftRecord? draft = null;
        if (draftStore is not null)
        {
            draft = draftStore.TryGet(content.DraftId!);
            if (draft is null)
            {
                return Fail(RecipeBuildFailureCodes.DraftNotFound);
            }

            if (draft.Status != RecipeDraftStatus.ConfirmedAwaitingBuild)
            {
                return Fail(RecipeBuildFailureCodes.DraftNotConfirmed);
            }

            if (!string.Equals(draft.CanonicalSha256, content.CanonicalSha256, StringComparison.Ordinal))
            {
                return Fail(RecipeBuildFailureCodes.DraftHashMismatch);
            }
        }

        // The retained draft is the authority when there is one: a payload copy that drifted from
        // the confirmed record must never be what gets built.
        var recipeJson = draft?.RecipeJson ?? content.RecipeJson;
        if (!string.Equals(RecipeCanonicalJson.ComputeSha256(recipeJson), content.CanonicalSha256, StringComparison.Ordinal))
        {
            return Fail(RecipeBuildFailureCodes.DraftHashMismatch);
        }

        string requestPath;
        string resultPath;
        try
        {
            (requestPath, resultPath) = Stage(temporaryDirectory, content, recipeJson);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Fail(RecipeBuildFailureCodes.StagingFailed);
        }

        sink.ReportProgress(ProgressStaged);
        var exitCode = await RunUnityAsync(new UnityRecipeBuildLaunch(requestPath, resultPath, _options.TimeoutSeconds), sink, cancellationToken)
            .ConfigureAwait(false);
        sink.ReportProgress(ProgressUnityExited);

        var decision = Interpret(exitCode, resultPath, content);
        AdvanceDraft(draftStore, content, decision, sink);
        return decision;
    }

    private async Task<int> RunUnityAsync(
        UnityRecipeBuildLaunch launch,
        IRecipeBuildSink sink,
        CancellationToken cancellationToken)
    {
        IUnityRecipeBuildProcess process;
        try
        {
            process = _runner.Start(launch);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            return UnityRecipeBuildExitCodes.UnityMissing;
        }

        try
        {
            // Registering the exact PID and start time is what lets cancellation, the job timeout
            // and crash recovery terminate this process and nothing else (REQ-003 §6.4).
            sink.RegisterChildProcess(process.ProcessId, process.StartUtc);
            sink.ReportProgress(ProgressUnityStarted);
            return await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Terminate();
            throw;
        }
        finally
        {
            sink.ClearChildProcess();
            process.Dispose();
        }
    }

    private static (string RequestPath, string ResultPath) Stage(
        string temporaryDirectory,
        BatchRecipeBuildPayloadContent content,
        string recipeJson)
    {
        Directory.CreateDirectory(temporaryDirectory);
        var recipePath = Path.Combine(temporaryDirectory, "staged-recipe.json");
        var requestPath = Path.Combine(temporaryDirectory, "build-request.json");
        var resultPath = Path.Combine(temporaryDirectory, "build-result.json");
        File.WriteAllText(recipePath, recipeJson, StrictUtf8);

        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", "vfxcomposer.recipe-build-request/1");
            writer.WriteString("draftId", content.DraftId ?? "batch-entry");
            writer.WriteString("recipePath", recipePath);
            writer.WriteString("expectedCanonicalSha256", content.CanonicalSha256);
            writer.WriteEndObject();
        }

        File.WriteAllBytes(requestPath, buffer.ToArray());
        if (File.Exists(resultPath))
        {
            File.Delete(resultPath);
        }

        return (requestPath, resultPath);
    }

    private static RecipeBuildDecision Interpret(
        int exitCode,
        string resultPath,
        BatchRecipeBuildPayloadContent content)
    {
        switch (exitCode)
        {
            case UnityRecipeBuildExitCodes.ProjectLockHeld:
                return Fail(RecipeBuildFailureCodes.ProjectLockHeld);
            case UnityRecipeBuildExitCodes.TimedOut:
                return Fail(RecipeBuildFailureCodes.BuildTimedOut);
            case UnityRecipeBuildExitCodes.UnityMissing:
            case UnityRecipeBuildExitCodes.Usage:
                return Fail(RecipeBuildFailureCodes.UnityUnavailable);
            case UnityRecipeBuildExitCodes.NoResult:
                return Fail(RecipeBuildFailureCodes.ResultUnreadable);
        }

        UnityRecipeBuildResult result;
        try
        {
            result = UnityRecipeBuildResultCodec.Read(resultPath);
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException or IOException or UnauthorizedAccessException)
        {
            return Fail(RecipeBuildFailureCodes.ResultUnreadable);
        }

        if (exitCode != UnityRecipeBuildExitCodes.Succeeded &&
            exitCode != UnityRecipeBuildExitCodes.StructuredFailure)
        {
            return new RecipeBuildDecision(false, RecipeBuildFailureCodes.ProcessFailed, result);
        }

        if (!result.Succeeded || exitCode != UnityRecipeBuildExitCodes.Succeeded)
        {
            return new RecipeBuildDecision(false, result.FailureCode ?? RecipeBuildFailureCodes.ProcessFailed, result);
        }

        // A success is only accepted when the entry point built the exact recipe this job submitted
        // and reported exactly the three closed write-surface members for that effect. This is the
        // execution-layer half of the double defence: a reported target outside the surface is
        // treated as a failed build even if Unity claimed success.
        if (!string.Equals(result.RecipeHash, content.CanonicalSha256, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(result.BuildHash) ||
            !RecipeBuildWriteSurface.DescribesExactly(result.EffectId, result.PrefabPath, result.OwnershipManifestPath, result.ProvenanceRecipePath))
        {
            return new RecipeBuildDecision(false, RecipeBuildFailureCodes.ResultIdentityMismatch, result);
        }

        return new RecipeBuildDecision(true, null, result);
    }

    private static void AdvanceDraft(
        IRecipeDraftStore? draftStore,
        BatchRecipeBuildPayloadContent content,
        RecipeBuildDecision decision,
        IRecipeBuildSink sink)
    {
        if (draftStore is null || content.DraftId is null)
        {
            return;
        }

        try
        {
            if (decision.Succeeded)
            {
                draftStore.MarkBuilt(content.DraftId, content.CanonicalSha256);
            }
            else
            {
                draftStore.MarkBuildFailed(content.DraftId, content.CanonicalSha256);
            }

            sink.ReportProgress(ProgressDraftAdvanced);
        }
        catch (RecipeDraftStoreException)
        {
            // The build itself already happened; a store fault must not be reported as a build
            // outcome, so it is surfaced as its own stable code on the decision.
            if (decision.Succeeded)
            {
                throw new RecipeBuildFailureException(RecipeBuildFailureCodes.DraftTransitionFailed);
            }
        }
    }

    /// <summary>
    /// Reports one refusal on the queue entry: the queue-level code in the closed event vocabulary,
    /// and the precise build code as an artifact identity so the entry itself says which stage
    /// refused, without anyone having to read the Unity log.
    /// </summary>
    private static void ReportFailure(IRecipeBuildSink sink, string failureCode)
    {
        sink.ReportArtifact(RecipeBuildFailureCodes.FailureArtifact(failureCode));
        sink.ReportLog(JobLogLevels.Error, JobQueueDiagnosticCodes.ExecutionFailed);
    }

    private static void ReportArtifacts(IRecipeBuildSink sink, UnityRecipeBuildResult result)
    {
        // Artifacts are location-free identities, mirroring the wire contract: the effect identity,
        // the canonical recipe hash and the producing build hash.
        sink.ReportArtifact("effect:" + result.EffectId);
        sink.ReportArtifact("recipe:sha256:" + result.RecipeHash);
        sink.ReportArtifact("build:sha256:" + result.BuildHash);
    }

    private static RecipeBuildDecision Fail(string code) => new(false, code, null);
}
