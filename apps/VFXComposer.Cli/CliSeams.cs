using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.Batch.Core;
using VFXComposer.Jobs;

namespace VFXComposer.Cli;

/// <summary>
/// The generation-side resources one command may need. Opening it resolves local settings and
/// local draft storage only: no HTTP client is constructed, no endpoint is parsed and no secret
/// is read until the F1 channel is actually invoked inside a running job.
/// </summary>
public interface ICliGenerationRuntime : IAsyncDisposable
{
    /// <summary>What this machine can execute right now, derived from the persisted channel bindings.</summary>
    BatchCapabilityProfile Capability { get; }

    IRecipeGenerationChannel GenerationChannel { get; }

    IRecipeDraftStore DraftStore { get; }

    /// <summary>
    /// The restricted build executor, or null when this host cannot reach the Unity project. It is
    /// created only for a foreground run, so a detached submission never resolves build paths.
    /// </summary>
    IJobExecutor? CreateRecipeBuildExecutor();
}

/// <summary>
/// The queue-side resources one command may need: the shared client every entry surface uses,
/// plus optional in-process hosting of the executor for a foreground run.
/// </summary>
public interface ICliQueueSession : IAsyncDisposable
{
    IJobQueueClient Client { get; }

    /// <summary>
    /// Hosts the payload executors in this process. Returns false when another process already
    /// owns queue execution, in which case this process keeps observing while that executor
    /// drains the queue.
    /// </summary>
    bool TryStartExecutors(IReadOnlyList<IJobExecutor> executors);
}

/// <summary>
/// Everything the command implementations reach outside themselves. Production composes it from
/// the current-user job store and the current-user AI runtime; tests compose it from temporary
/// directories and mocked channels, so no test ever touches a real provider.
/// </summary>
public sealed record CliEnvironment
{
    public required TextWriter Output { get; init; }

    public required TextWriter Error { get; init; }

    public required Func<ICliQueueSession> OpenQueue { get; init; }

    public required Func<ICliGenerationRuntime> OpenGenerationRuntime { get; init; }

    public Func<DateTimeOffset> UtcNow { get; init; } = static () => DateTimeOffset.UtcNow;

    public BatchTrackingOptions Tracking { get; init; } = BatchTrackingOptions.Default;
}
