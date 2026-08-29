using System.Collections.Frozen;

namespace VFXComposer.Cli;

/// <summary>
/// Closed CLI-surface notice code set. These describe entry-surface conditions that are not job
/// outcomes, so they cannot borrow the jobs catalog; messages are fixed, single-line and free of
/// paths, prompts and endpoints.
/// </summary>
public static class CliNoticeCodes
{
    public const string ObservingForeignExecutor = "VFXC0001";
    public const string BatchDetached = "VFXC0002";
    public const string ReportWritten = "VFXC0003";
    public const string WaitingProjectLock = "VFXC0004";
    public const string Interrupted = "VFXC0005";
    public const string ProjectLockTimeout = "VFXC0006";
    public const string QueueUnavailable = "VFXC0007";
    public const string NotFound = "VFXC0008";
    public const string DryRunPlanOnly = "VFXC0009";
    public const string ReportNotWritten = "VFXC0010";
    public const string ManifestRejected = "VFXC0011";

    public static IReadOnlySet<string> All => CliNoticeCatalog.Codes;
}

/// <summary>Closed catalog resolving CLI notice codes to their fixed messages.</summary>
public static class CliNoticeCatalog
{
    private static readonly FrozenDictionary<string, string> Messages =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CliNoticeCodes.ObservingForeignExecutor] =
                "Another process owns queue execution; this run only observes while that executor drains the queue.",
            [CliNoticeCodes.BatchDetached] =
                "The batch was enqueued and this process detached; the executor keeps applying the batch policy.",
            [CliNoticeCodes.ReportWritten] = "The batch report was written.",
            [CliNoticeCodes.WaitingProjectLock] =
                "The Unity editor holds the project; the queue is waiting for the project lock.",
            [CliNoticeCodes.Interrupted] =
                "Tracking was interrupted; already enqueued jobs keep running under the executor.",
            [CliNoticeCodes.ProjectLockTimeout] =
                "The queue stayed blocked on the Unity project lock past the configured bound.",
            [CliNoticeCodes.QueueUnavailable] = "The job queue store is unavailable.",
            [CliNoticeCodes.NotFound] = "No queue entry matches the requested identifier.",
            [CliNoticeCodes.DryRunPlanOnly] = "Dry run: the plan was printed and nothing was enqueued.",
            [CliNoticeCodes.ReportNotWritten] = "The batch report could not be written to the requested destination.",
            [CliNoticeCodes.ManifestRejected] = "The manifest was rejected; no entry was enqueued.",
        }.ToFrozenDictionary(StringComparer.Ordinal);

    internal static FrozenSet<string> Codes { get; } = Messages.Keys.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, string> All => Messages;

    public static string Require(string code) =>
        Messages.TryGetValue(code, out var message) ? message : throw new ArgumentOutOfRangeException(nameof(code));
}
