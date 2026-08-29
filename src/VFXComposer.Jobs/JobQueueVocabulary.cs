using System.Collections.Frozen;

namespace VFXComposer.Jobs;

/// <summary>Closed submission-surface vocabulary; the queue never invents additional entries.</summary>
public static class JobSourceEntries
{
    public const string Desktop = "DESKTOP";
    public const string Cli = "CLI";
    public const string Mcp = "MCP";

    private static readonly FrozenSet<string> Known =
        new[] { Desktop, Cli, Mcp }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => Known;

    internal static string Require(string value, string parameterName) =>
        Known.Contains(value) ? value : throw new ArgumentOutOfRangeException(parameterName);
}

/// <summary>Closed batch failure-policy vocabulary persisted with every batched job.</summary>
public static class JobBatchPolicies
{
    public const string Continue = "CONTINUE";
    public const string Abort = "ABORT";

    private static readonly FrozenSet<string> Known =
        new[] { Continue, Abort }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => Known;

    internal static string Require(string value, string parameterName) =>
        Known.Contains(value) ? value : throw new ArgumentOutOfRangeException(parameterName);
}

/// <summary>
/// Queue-level observability vocabulary. This is deliberately not a job state: individual jobs
/// only ever use the Protocol <c>JobStatusStates</c> closed set.
/// </summary>
public static class JobQueueStates
{
    public const string Idle = "IDLE";
    public const string Executing = "EXECUTING";
    public const string WaitingProjectLock = "WAITING_PROJECT_LOCK";

    private static readonly FrozenSet<string> Known =
        new[] { Idle, Executing, WaitingProjectLock }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => Known;

    internal static string Require(string value, string parameterName) =>
        Known.Contains(value) ? value : throw new ArgumentOutOfRangeException(parameterName);
}
