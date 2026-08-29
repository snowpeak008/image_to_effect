namespace VFXComposer.Jobs;

/// <summary>Timing policy for one executor host.</summary>
public sealed record JobQueueHostOptions
{
    public static JobQueueHostOptions Default { get; } = new();

    /// <summary>Delay between queue polls while idle.</summary>
    public TimeSpan IdlePollInterval { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Initial backoff while the Unity editor owns the project lock.</summary>
    public TimeSpan ProjectLockInitialBackoff { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Upper bound for the project-lock backoff.</summary>
    public TimeSpan ProjectLockMaximumBackoff { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Poll interval for cross-process cancellation requests while a job runs.</summary>
    public TimeSpan CancellationPollInterval { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Single-job execution ceiling, aligned with the Invoke-Unity 900-second discipline.</summary>
    public TimeSpan JobTimeout { get; init; } = TimeSpan.FromSeconds(900);

    /// <summary>Grace period a cancelled or shutting-down payload gets before it is settled without it.</summary>
    public TimeSpan CancellationGracePeriod { get; init; } = TimeSpan.FromSeconds(5);
}
