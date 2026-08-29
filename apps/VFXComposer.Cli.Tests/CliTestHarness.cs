using System.Security.Cryptography;
using System.Text;
using VFXComposer.AI.Contracts.Chat;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.Batch.Core;
using VFXComposer.Cli;
using VFXComposer.Jobs;

namespace VFXComposer.Cli.Tests;

/// <summary>
/// Synthetic fixtures for the batch entry surface: temporary manifest and store directories,
/// a mocked generation channel and an in-memory draft store. No test reaches a provider, a
/// network endpoint or a Unity project.
/// </summary>
internal static class CliTestHarness
{
    public static readonly JobQueueHostOptions FastHostOptions = new()
    {
        IdlePollInterval = TimeSpan.FromMilliseconds(25),
        ProjectLockInitialBackoff = TimeSpan.FromMilliseconds(25),
        ProjectLockMaximumBackoff = TimeSpan.FromMilliseconds(50),
        CancellationPollInterval = TimeSpan.FromMilliseconds(25),
        JobTimeout = TimeSpan.FromSeconds(30),
        CancellationGracePeriod = TimeSpan.FromMilliseconds(500),
    };

    public static readonly BatchTrackingOptions FastTracking = new()
    {
        PollInterval = TimeSpan.FromMilliseconds(25),
    };

    public static string CreateDirectory() =>
        System.IO.Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(),
            "vfxc-cli-tests",
            Guid.NewGuid().ToString("N"))).FullName;

    public static string WriteManifest(string directory, string json)
    {
        var path = Path.Combine(directory, "batch.json");
        File.WriteAllText(path, json, new UTF8Encoding(false));
        return path;
    }

    /// <summary>A three-entry prompt manifest; the second entry is the one tests make fail.</summary>
    public static string ThreePromptManifest(string batchId = "fire-pack", string onFailure = "continue") =>
        """
        {
          "schemaVersion": "vfxcomposer.batch-manifest/1",
          "batchId": "__BATCH__",
          "onFailure": "__POLICY__",
          "defaults": { "targetProfile": "mobile_medium" },
          "items": [
            { "itemId": "alpha", "kind": "prompt", "prompt": "a calm blue spark", "constraints": { "element": "water" } },
            { "itemId": "beta", "kind": "prompt", "prompt": "POISON a broken effect" },
            { "itemId": "gamma", "kind": "prompt", "prompt": "a slow ember trail" }
          ]
        }
        """
            .Replace("__BATCH__", batchId, StringComparison.Ordinal)
            .Replace("__POLICY__", onFailure, StringComparison.Ordinal);

    public static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    /// <summary>A channel that fails exactly the descriptions the predicate selects.</summary>
    public static FakeRecipeGenerationChannel Channel(Func<string, bool> shouldFail) =>
        new(request => shouldFail(request.Description)
            ? ValidationFailedResult(request.CorrelationId)
            : DraftedResult(request.CorrelationId, "fx-generated"));

    /// <summary>Hosts one executor until the expected number of entries has settled.</summary>
    public static async Task DrainAsync(JobStore store, IJobExecutor executor, int expectedTerminalJobs)
    {
        await using var host = new JobQueueHost(store, [executor], FastHostOptions);
        host.Start();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (store.ReadSnapshot().Jobs.Count(static job => job.IsTerminal) < expectedTerminalJobs)
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("The queue did not settle the expected entries in time.");
            }

            await Task.Delay(25);
        }
    }

    public static RecipeGenerationResult DraftedResult(string correlationId, string recipeId)
    {
        var recipeJson =
            "{\"id\":\"" + recipeId + "\",\"archetype\":\"projectile\",\"dimension\":\"2d\",\"targetProfile\":\"mobile_medium\"}";
        var draft = new RecipeDraft(
            correlationId,
            recipeJson,
            Sha256Hex(recipeJson),
            recipeId,
            "projectile",
            "2d",
            "mobile_medium",
            "prompt-template-test",
            "catalog-test");
        return RecipeGenerationResult.Drafted(draft, [new RecipeGenerationAttempt(1, Array.Empty<string>())]);
    }

    public static RecipeGenerationResult ValidationFailedResult(string correlationId) =>
        RecipeGenerationResult.ValidationFailed(
            correlationId,
            "{\"id\":\"broken\"}",
            [new RecipeValidationIssue("E101", RecipeValidationSeverity.Error, "$.archetype", "Missing archetype.")],
            [new RecipeGenerationAttempt(1, ["E101"])],
            "prompt-template-test",
            "catalog-test");

    public static RecipeGenerationResult ChannelFailedResult(string correlationId) =>
        RecipeGenerationResult.ChannelFailed(
            correlationId,
            ChatChannelErrorCode.TimedOut,
            Array.Empty<RecipeGenerationAttempt>(),
            "prompt-template-test",
            "catalog-test");
}

/// <summary>Generation channel driven by a delegate over the composed description.</summary>
internal sealed class FakeRecipeGenerationChannel : IRecipeGenerationChannel
{
    private readonly Func<RecipeGenerationRequest, RecipeGenerationResult> _respond;
    private readonly List<string> _descriptions = [];

    public FakeRecipeGenerationChannel(Func<RecipeGenerationRequest, RecipeGenerationResult> respond)
    {
        _respond = respond;
    }

    public IReadOnlyList<string> Descriptions
    {
        get
        {
            lock (_descriptions)
            {
                return _descriptions.ToArray();
            }
        }
    }

    public ValueTask<RecipeGenerationResult> GenerateAsync(
        RecipeGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        lock (_descriptions)
        {
            _descriptions.Add(request.Description);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_respond(request));
    }
}

/// <summary>Draft store kept in memory so no user application data is touched.</summary>
internal sealed class InMemoryRecipeDraftStore : IRecipeDraftStore
{
    private readonly Dictionary<string, RecipeDraftRecord> _records = new(StringComparer.Ordinal);

    public IReadOnlyList<RecipeDraftRecord> Records
    {
        get
        {
            lock (_records)
            {
                return _records.Values.ToArray();
            }
        }
    }

    public RecipeDraftRecord Save(RecipeDraftRecord record)
    {
        lock (_records)
        {
            _records[record.DraftId] = record;
            return record;
        }
    }

    public RecipeDraftRecord Confirm(string draftId, string canonicalSha256) =>
        Advance(draftId, canonicalSha256, RecipeDraftStatus.PendingConfirmation, RecipeDraftStatus.ConfirmedAwaitingBuild);

    public RecipeDraftRecord MarkBuilt(string draftId, string canonicalSha256) =>
        Advance(draftId, canonicalSha256, RecipeDraftStatus.ConfirmedAwaitingBuild, RecipeDraftStatus.Built);

    public RecipeDraftRecord MarkBuildFailed(string draftId, string canonicalSha256) =>
        Advance(draftId, canonicalSha256, RecipeDraftStatus.ConfirmedAwaitingBuild, RecipeDraftStatus.BuildFailed);

    public RecipeDraftRecord? TryGet(string draftId)
    {
        lock (_records)
        {
            return _records.TryGetValue(draftId, out var record) ? record : null;
        }
    }

    public IReadOnlyList<RecipeDraftRecord> ListConfirmedAwaitingBuild()
    {
        lock (_records)
        {
            return _records.Values
                .Where(static record => record.Status == RecipeDraftStatus.ConfirmedAwaitingBuild)
                .OrderBy(static record => record.UpdatedUtc)
                .ThenBy(static record => record.DraftId, StringComparer.Ordinal)
                .ToArray();
        }
    }

    private RecipeDraftRecord Advance(
        string draftId,
        string canonicalSha256,
        RecipeDraftStatus required,
        RecipeDraftStatus next)
    {
        lock (_records)
        {
            if (!_records.TryGetValue(draftId, out var current))
            {
                throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.NotFound);
            }

            if (current.Status != required)
            {
                throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.InvalidStatus);
            }

            if (!string.Equals(current.CanonicalSha256, canonicalSha256, StringComparison.Ordinal))
            {
                throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.HashMismatch);
            }

            var advanced = new RecipeDraftRecord(
                current.DraftId,
                next,
                current.CreatedUtc,
                DateTimeOffset.UtcNow,
                current.CorrelationId,
                current.PromptTemplateVersion,
                current.TemplateCatalogVersion,
                current.RecipeJson,
                current.CanonicalSha256,
                current.RecipeId,
                current.Archetype,
                current.Dimension,
                current.TargetProfile,
                current.Issues,
                current.RequestCount);
            _records[draftId] = advanced;
            return advanced;
        }
    }
}

/// <summary>Generation runtime seam backed by the fakes above.</summary>
internal sealed class FakeGenerationRuntime : ICliGenerationRuntime
{
    public FakeGenerationRuntime(
        IRecipeGenerationChannel channel,
        IRecipeDraftStore draftStore,
        BatchCapabilityProfile? capability = null)
    {
        GenerationChannel = channel;
        DraftStore = draftStore;
        Capability = capability ?? BatchCapabilityProfile.GenerationOnly;
    }

    public BatchCapabilityProfile Capability { get; }

    public IRecipeGenerationChannel GenerationChannel { get; }

    public IRecipeDraftStore DraftStore { get; }

    public bool Disposed { get; private set; }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}

/// <summary>Queue session over a real store, optionally hosting the executor in-process.</summary>
internal sealed class TestQueueSession : ICliQueueSession
{
    private readonly JobStore _store;
    private readonly bool _allowExecutor;
    private JobQueueHost? _host;

    public TestQueueSession(JobStore store, bool allowExecutor = true)
    {
        _store = store;
        _allowExecutor = allowExecutor;
    }

    public IJobQueueClient Client => _store;

    public bool TryStartExecutor(IJobExecutor executor)
    {
        if (!_allowExecutor)
        {
            return false;
        }

        var host = new JobQueueHost(_store, [executor], CliTestHarness.FastHostOptions);
        host.Start();
        _host = host;
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
            _host = null;
        }
    }
}

/// <summary>Queue session over an arbitrary client; never hosts an executor.</summary>
internal sealed class StubQueueSession : ICliQueueSession
{
    public StubQueueSession(IJobQueueClient client)
    {
        Client = client;
    }

    public IJobQueueClient Client { get; }

    public bool TryStartExecutor(IJobExecutor executor) => false;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>Client whose every operation fails with the stable store-unavailable code.</summary>
internal sealed class UnavailableQueueClient : IJobQueueClient
{
    public JobQueueSnapshotView ReadSnapshot() =>
        throw new JobQueueException(JobQueueDiagnosticCodes.StoreUnavailable);

    public IReadOnlyList<JobStoreEvent> ReadEvents(string jobId) =>
        throw new JobQueueException(JobQueueDiagnosticCodes.StoreUnavailable);

    public JobRecord Enqueue(JobEnqueueRequest request) =>
        throw new JobQueueException(JobQueueDiagnosticCodes.StoreUnavailable);

    public JobCancellationResult RequestCancel(string jobId) =>
        throw new JobQueueException(JobQueueDiagnosticCodes.StoreUnavailable);

    public JobRecord Resubmit(string jobId) =>
        throw new JobQueueException(JobQueueDiagnosticCodes.StoreUnavailable);
}

/// <summary>Decorator that reports the queue as blocked on the Unity project lock.</summary>
internal sealed class ProjectLockWaitingQueueClient : IJobQueueClient
{
    private readonly IJobQueueClient _inner;

    public ProjectLockWaitingQueueClient(IJobQueueClient inner)
    {
        _inner = inner;
    }

    public JobQueueSnapshotView ReadSnapshot()
    {
        var snapshot = _inner.ReadSnapshot();
        return new JobQueueSnapshotView(JobQueueStates.WaitingProjectLock, snapshot.Jobs);
    }

    public IReadOnlyList<JobStoreEvent> ReadEvents(string jobId) => _inner.ReadEvents(jobId);

    public JobRecord Enqueue(JobEnqueueRequest request) => _inner.Enqueue(request);

    public JobCancellationResult RequestCancel(string jobId) => _inner.RequestCancel(jobId);

    public JobRecord Resubmit(string jobId) => _inner.Resubmit(jobId);
}
