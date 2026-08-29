namespace VFXComposer.Jobs;

/// <summary>Boundedness and retention policy for the store (REQ-003 §7.1, REQ-003-16).</summary>
public sealed record JobStoreOptions
{
    public static JobStoreOptions Default { get; } = new();

    /// <summary>Maximum number of jobs waiting in QUEUED; further submissions are rejected.</summary>
    public int MaximumPendingJobs { get; init; } = 256;

    /// <summary>Maximum retained terminal jobs; the oldest completions beyond this are pruned.</summary>
    public int MaximumTerminalJobs { get; init; } = 256;

    /// <summary>Maximum age of a retained terminal job.</summary>
    public TimeSpan TerminalRetention { get; init; } = TimeSpan.FromDays(30);
}
