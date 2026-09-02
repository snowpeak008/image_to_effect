using System.Text;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.Batch.Core;
using VFXComposer.Jobs;

namespace VFXComposer.BuildHost.Tests;

/// <summary>
/// Synthetic fixtures for the host run: temporary job stores, an in-memory or real temporary-file
/// draft store, and a faked Unity wrapper process. No test starts Unity, touches a project or
/// reaches user application data.
/// </summary>
internal static class BuildHostTestHarness
{
    public const string EffectId = "f8c_host_probe";

    public static readonly JobQueueHostOptions FastHostOptions = new()
    {
        IdlePollInterval = TimeSpan.FromMilliseconds(25),
        ProjectLockInitialBackoff = TimeSpan.FromMilliseconds(25),
        ProjectLockMaximumBackoff = TimeSpan.FromMilliseconds(50),
        CancellationPollInterval = TimeSpan.FromMilliseconds(25),
        JobTimeout = TimeSpan.FromSeconds(30),
        CancellationGracePeriod = TimeSpan.FromMilliseconds(500),
    };

    public static string CreateDirectory() =>
        Directory.CreateTempSubdirectory("vfxc-buildhost-tests-").FullName;

    public static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    /// <summary>A synthetic Unity build host root: the locator's two required parts, nothing else.</summary>
    public static UnityBuildHost CreateBuildHost(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "project", "Assets"));
        Directory.CreateDirectory(Path.Combine(root, "tools"));
        File.WriteAllText(Path.Combine(root, "tools", "Invoke-Unity.ps1"), "param()", new UTF8Encoding(false));
        return UnityBuildHostLocator.TryLocateAt(root)
            ?? throw new InvalidOperationException("The synthetic build host root is incomplete.");
    }

    public static BuildHostEnvironment Environment(
        StringWriter output,
        IBuildHostDraftSession drafts,
        JobStore queue,
        UnityBuildHost buildHost,
        IUnityRecipeBuildRunner runner,
        IProjectLockProbe? probe = null) => new()
    {
        Output = output,
        OpenDrafts = () => drafts,
        OpenQueue = () => queue,
        LocateBuildHost = () => buildHost,
        CreateRunner = _ => runner,
        CreateProjectLockProbe = _ => probe ?? new AlwaysFreeProjectLockProbe(),
        HostOptions = FastHostOptions,
        PollInterval = TimeSpan.FromMilliseconds(25),
    };

    public static RecipeDraftRecord Draft(string? recipeJson = null)
    {
        var json = recipeJson ?? Recipe();
        return new RecipeDraftRecord(
            "draft-" + Guid.NewGuid().ToString("N"),
            RecipeDraftStatus.PendingConfirmation,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"),
            "prompt-template-1",
            "1.0.0",
            json,
            RecipeCanonicalJson.ComputeSha256(json),
            EffectId,
            "projectile",
            "2d",
            "mobile_medium",
            Array.Empty<RecipeValidationIssue>(),
            1);
    }

    public static string Recipe(int revision = 1) =>
        "{\"recipeVersion\":1,\"revision\":" + revision + ",\"id\":\"" + EffectId + "\",\"dimension\":\"2d\"," +
        "\"archetype\":\"projectile\",\"targetProfile\":\"mobile_medium\",\"randomSeed\":7,\"stages\":[]," +
        "\"metadata\":{\"createdBy\":\"test\",\"templateCatalogVersion\":\"1.0.0\"}}";

    /// <summary>A structured wrapper result reporting a clean build of exactly the closed write surface.</summary>
    public static string SuccessResult(string recipeHash) =>
        "{\"schemaVersion\":\"vfxcomposer.recipe-build-result/1\",\"draftId\":\"draft-x\",\"succeeded\":true," +
        "\"failureCode\":null,\"effectId\":\"" + EffectId + "\",\"recipeHash\":\"" + recipeHash + "\"," +
        "\"buildHash\":\"" + new string('b', 64) + "\",\"recipeRevision\":1,\"compilerVersion\":\"0.1.0\"," +
        "\"unityVersion\":\"2022.3.62f3c1\",\"declaredTemplateCatalogVersion\":\"1.0.0\"," +
        "\"catalogIdentityHash\":\"" + new string('c', 64) + "\"," +
        "\"prefabPath\":\"Assets/VFX/Generated/" + EffectId + "/VFX_" + EffectId + ".prefab\"," +
        "\"buildManifestPath\":\"Assets/VFX/Generated/" + EffectId + "/BuildManifest.json\"," +
        "\"ownershipManifestPath\":\"ProjectSettings/VFXComposer/BuildManifests/" + EffectId + ".manifest.json\"," +
        "\"provenanceRecipePath\":\"Assets/VFX/Recipes/" + EffectId + ".json\"," +
        "\"dryRunState\":\"Create\",\"cleanedResiduePaths\":[],\"issues\":[]}";

    public static string FailureResult(string failureCode) =>
        "{\"schemaVersion\":\"vfxcomposer.recipe-build-result/1\",\"draftId\":\"draft-x\",\"succeeded\":false," +
        "\"failureCode\":\"" + failureCode + "\",\"effectId\":\"" + EffectId + "\",\"recipeHash\":null," +
        "\"buildHash\":null,\"recipeRevision\":1,\"compilerVersion\":\"0.1.0\",\"unityVersion\":\"2022.3.62f3c1\"," +
        "\"declaredTemplateCatalogVersion\":\"1.0.0\",\"catalogIdentityHash\":null,\"prefabPath\":null," +
        "\"buildManifestPath\":null,\"ownershipManifestPath\":null,\"provenanceRecipePath\":null," +
        "\"dryRunState\":null,\"cleanedResiduePaths\":[]," +
        "\"issues\":[{\"code\":\"E308\",\"severity\":\"Error\",\"path\":\"/stages/travel/modules/core/templateId\"," +
        "\"message\":\"Template is not in the catalog.\",\"actualValue\":null,\"allowedRange\":null}]}";
}

/// <summary>Draft session over any store; disposal is observable so leak checks stay possible.</summary>
internal sealed class TestDraftSession : IBuildHostDraftSession
{
    public TestDraftSession(IRecipeDraftStore drafts)
    {
        Drafts = drafts;
    }

    public IRecipeDraftStore Drafts { get; }

    public bool Disposed { get; private set; }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}

/// <summary>Draft store kept in memory so no user application data is touched.</summary>
internal sealed class InMemoryRecipeDraftStore : IRecipeDraftStore
{
    private readonly Dictionary<string, RecipeDraftRecord> _records = new(StringComparer.Ordinal);

    public RecipeDraftRecord Save(RecipeDraftRecord record)
    {
        _records[record.DraftId] = record;
        return record;
    }

    public RecipeDraftRecord Confirm(string draftId, string canonicalSha256) =>
        Advance(draftId, canonicalSha256, RecipeDraftStatus.PendingConfirmation, RecipeDraftStatus.ConfirmedAwaitingBuild);

    public RecipeDraftRecord MarkBuilt(string draftId, string canonicalSha256) =>
        Advance(draftId, canonicalSha256, RecipeDraftStatus.ConfirmedAwaitingBuild, RecipeDraftStatus.Built);

    public RecipeDraftRecord MarkBuildFailed(string draftId, string canonicalSha256) =>
        Advance(draftId, canonicalSha256, RecipeDraftStatus.ConfirmedAwaitingBuild, RecipeDraftStatus.BuildFailed);

    public RecipeDraftRecord? TryGet(string draftId) =>
        _records.TryGetValue(draftId, out var record) ? record : null;

    public IReadOnlyList<RecipeDraftRecord> ListConfirmedAwaitingBuild() => _records.Values
        .Where(static record => record.Status == RecipeDraftStatus.ConfirmedAwaitingBuild)
        .OrderBy(static record => record.UpdatedUtc)
        .ToArray();

    private RecipeDraftRecord Advance(
        string draftId,
        string canonicalSha256,
        RecipeDraftStatus required,
        RecipeDraftStatus next)
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

/// <summary>Stands in for the Unity batchmode wrapper: it writes the result file the real one would.</summary>
internal sealed class FakeUnityRecipeBuildRunner : IUnityRecipeBuildRunner
{
    private readonly int _exitCode;
    private readonly Func<UnityRecipeBuildLaunch, string?> _writeResult;

    internal FakeUnityRecipeBuildRunner(int exitCode, Func<UnityRecipeBuildLaunch, string?> writeResult)
    {
        _exitCode = exitCode;
        _writeResult = writeResult;
    }

    internal int StartCount { get; private set; }

    /// <summary>Blocks the faked process until released, for launch-scope-independence coverage.</summary>
    internal SemaphoreSlim? HoldUntilReleased { get; init; }

    internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IUnityRecipeBuildProcess Start(UnityRecipeBuildLaunch launch)
    {
        StartCount++;
        var body = _writeResult(launch);
        if (body is not null)
        {
            File.WriteAllText(launch.ResultPath, body, new UTF8Encoding(false));
        }

        return new FakeProcess(_exitCode, HoldUntilReleased, Started);
    }

    private sealed class FakeProcess : IUnityRecipeBuildProcess
    {
        private readonly int _exitCode;
        private readonly SemaphoreSlim? _hold;
        private readonly TaskCompletionSource _started;

        internal FakeProcess(int exitCode, SemaphoreSlim? hold, TaskCompletionSource started)
        {
            _exitCode = exitCode;
            _hold = hold;
            _started = started;
        }

        public int ProcessId => 848_484;

        public DateTimeOffset StartUtc { get; } = DateTimeOffset.UtcNow;

        public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
        {
            _started.TrySetResult();
            if (_hold is not null)
            {
                await _hold.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return _exitCode;
        }

        public void Terminate()
        {
        }

        public void Dispose()
        {
        }
    }
}

/// <summary>Probe whose availability the test flips at will.</summary>
internal sealed class TogglingProjectLockProbe : IProjectLockProbe
{
    private volatile bool _busy;

    internal TogglingProjectLockProbe(bool busy)
    {
        _busy = busy;
    }

    internal void SetBusy(bool busy) => _busy = busy;

    public ProjectLockAvailability Probe() => _busy
        ? ProjectLockAvailability.Busy
        : ProjectLockAvailability.Free;
}
