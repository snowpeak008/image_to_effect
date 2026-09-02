namespace VFXComposer.BuildHost;

/// <summary>
/// The stable exit-code table of the private build host (ADR-008 §2.1). Exit codes carry no path
/// and no content; the authoritative build outcome lives in the job store and the draft store,
/// and the launcher registers this code as fallback diagnostics only.
/// </summary>
public static class BuildHostExitCodes
{
    /// <summary>The draft built, the draft advanced to Built, the artifacts are on the queue entry.</summary>
    public const int BuildSucceeded = 0;

    /// <summary>The build settled failed; the precise VFXB code survives on the queue entry artifact.</summary>
    public const int BuildFailed = 10;

    /// <summary>The user cancelled the entry while this host owned it.</summary>
    public const int BuildCancelled = 11;

    /// <summary>The host was shut down while the entry ran; recovery semantics settled it.</summary>
    public const int BuildDisconnected = 12;

    /// <summary>The process arguments are not exactly one draft identity plus one canonical hash.</summary>
    public const int UsageError = 64;

    /// <summary>The re-verified identity was refused: unknown draft, not confirmed, or hash drift. Zero enqueue.</summary>
    public const int DraftIdentityRefused = 65;

    /// <summary>The shared job store refused; nothing was executed.</summary>
    public const int QueueUnavailable = 69;

    /// <summary>The Unity project or the build wrapper could not be located from this deployment. Zero enqueue.</summary>
    public const int BuildEnvironmentUnavailable = 70;

    /// <summary>The shared draft store refused before the identity could be re-verified. Zero enqueue.</summary>
    public const int DraftStoreUnavailable = 71;

    /// <summary>
    /// Another process owns queue execution. The entry stays in the queue — the draft-backed
    /// payload is self-sufficient, so the current lock holder or any later host executes it.
    /// </summary>
    public const int ExecutorLockHeld = 75;
}

/// <summary>Host-layer stable codes written to the diagnostic stream, one token per line, no content.</summary>
public static class BuildHostDiagnosticCodes
{
    public const string UsageInvalid = "VFXBH0001";
    public const string DraftStoreUnavailable = "VFXBH0002";
    public const string BuildEnvironmentUnavailable = "VFXBH0003";
    public const string QueueUnavailable = "VFXBH0004";
    public const string ExecutorLockHeld = "VFXBH0005";
}
