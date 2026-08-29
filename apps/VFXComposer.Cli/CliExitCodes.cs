namespace VFXComposer.Cli;

/// <summary>
/// The exit-code table of REQ-002 §6.5. The sysexits-derived values (64/65/69) and the
/// Invoke-Unity-derived value (73) are kept exactly as the requirement fixes them.
/// </summary>
public static class CliExitCodes
{
    /// <summary>Every entry succeeded or was skipped as already complete.</summary>
    public const int Success = 0;

    /// <summary>Continue policy: the batch finished but at least one entry did not succeed.</summary>
    public const int BatchCompletedWithFailures = 10;

    /// <summary>Abort policy: an entry failed and the batch remainder was cancelled.</summary>
    public const int BatchAborted = 11;

    /// <summary>Argument or usage error.</summary>
    public const int UsageError = 64;

    /// <summary>The manifest, or an identifier argument, could not be read or validated.</summary>
    public const int DataError = 65;

    /// <summary>The queue store is unavailable.</summary>
    public const int QueueUnavailable = 69;

    /// <summary>The queue stayed blocked on the Unity project lock past <c>--lock-timeout</c>.</summary>
    public const int ProjectLockTimeout = 73;

    /// <summary>The user interrupted the foreground run; enqueued jobs keep running.</summary>
    public const int Interrupted = 130;
}
