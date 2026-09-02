using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.Batch.Core;
using VFXComposer.Jobs;

namespace VFXComposer.BuildHost;

/// <summary>
/// The shared draft store for one host run. Production wraps the current-user AI runtime; tests
/// wrap an in-memory or temporary-file store, so no test touches user application data.
/// </summary>
public interface IBuildHostDraftSession : IAsyncDisposable
{
    IRecipeDraftStore Drafts { get; }
}

/// <summary>
/// Everything the host run reaches outside itself, mirroring the CLI's <c>CliEnvironment</c> seam
/// style: production composes the current-user stores and the real batchmode runner; tests compose
/// temporary directories and fakes, so no test starts Unity or touches a project.
/// </summary>
public sealed record BuildHostEnvironment
{
    /// <summary>Diagnostic stream: stable code tokens only, never a path, recipe text or prompt.</summary>
    public required TextWriter Output { get; init; }

    public required Func<IBuildHostDraftSession> OpenDrafts { get; init; }

    /// <summary>The shared current-user job store; the host is one more entry surface over it.</summary>
    public required Func<JobStore> OpenQueue { get; init; }

    /// <summary>Locates the Unity project and wrapper; null reports no build capability (fail-closed).</summary>
    public required Func<UnityBuildHost?> LocateBuildHost { get; init; }

    /// <summary>Launch seam for the batchmode build, so tests fake the wrapper process.</summary>
    public required Func<UnityBuildHost, IUnityRecipeBuildRunner> CreateRunner { get; init; }

    /// <summary>
    /// Project-lock probe wiring, identical to the CLI foreground run: the real probe against the
    /// located project by default, a fake in tests covering the WaitingProjectLock path.
    /// </summary>
    public Func<UnityBuildHost, IProjectLockProbe> CreateProjectLockProbe { get; init; } =
        static host => new UnityProjectLockProbe(host.ProjectPath);

    public JobQueueHostOptions HostOptions { get; init; } = JobQueueHostOptions.Default;

    /// <summary>Snapshot poll interval while waiting for the entry to settle.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(500);
}
