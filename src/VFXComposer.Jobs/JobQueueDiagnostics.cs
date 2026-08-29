using System.Collections.Frozen;
using VFXComposer.Protocol.Diagnostics;

namespace VFXComposer.Jobs;

/// <summary>
/// Closed jobs-domain diagnostic code set. The Protocol <c>StableDiagnosticCatalog</c> is a
/// sealed wire catalog that this assembly must not extend, so queue outcomes carry their own
/// stable codes with the same discipline: fixed single-line path-free messages, closed set.
/// </summary>
public static class JobQueueDiagnosticCodes
{
    public const string CancelledQueued = "VFXJ0001";
    public const string CancelledRunning = "VFXJ0002";
    public const string DisconnectedRecovery = "VFXJ0003";
    public const string ExecutionFailed = "VFXJ0004";
    public const string ExecutionTimeout = "VFXJ0005";
    public const string JobKindUnsupported = "VFXJ0006";
    public const string QueueFull = "VFXJ0007";
    public const string StoreUnavailable = "VFXJ0008";
    public const string StoreVersionUnsupported = "VFXJ0009";
    public const string ExecutorLockUnavailable = "VFXJ0010";
    public const string WaitingProjectLock = "VFXJ0011";
    public const string InvalidTransition = "VFXJ0012";
    public const string JobNotFound = "VFXJ0013";
    public const string EventLogWriteFailed = "VFXJ0014";
    public const string BatchAborted = "VFXJ0015";
    public const string ExecutorHostFault = "VFXJ0016";

    public static IReadOnlySet<string> All => JobQueueDiagnosticCatalog.Codes;
}

/// <summary>One immutable definition per jobs-domain diagnostic code.</summary>
public sealed record JobQueueDiagnosticDefinition(
    string Code,
    string Severity,
    string Message,
    bool Retryable);

/// <summary>Closed catalog resolving jobs-domain codes to fixed severities and messages.</summary>
public static class JobQueueDiagnosticCatalog
{
    private static readonly FrozenDictionary<string, JobQueueDiagnosticDefinition> Definitions =
        new[]
        {
            new JobQueueDiagnosticDefinition(
                JobQueueDiagnosticCodes.CancelledQueued,
                DiagnosticSeverities.Info,
                "The job was cancelled before execution started.",
                Retryable: false),
            new JobQueueDiagnosticDefinition(
                JobQueueDiagnosticCodes.CancelledRunning,
                DiagnosticSeverities.Info,
                "The job was cancelled during execution.",
                Retryable: false),
            new JobQueueDiagnosticDefinition(
                JobQueueDiagnosticCodes.DisconnectedRecovery,
                DiagnosticSeverities.Error,
                "The executor terminated before the job completed; the job was settled during recovery.",
                Retryable: true),
            new JobQueueDiagnosticDefinition(
                JobQueueDiagnosticCodes.ExecutionFailed,
                DiagnosticSeverities.Error,
                "The job payload failed.",
                Retryable: false),
            new JobQueueDiagnosticDefinition(
                JobQueueDiagnosticCodes.ExecutionTimeout,
                DiagnosticSeverities.Error,
                "The job exceeded its execution timeout and was terminated.",
                Retryable: true),
            new JobQueueDiagnosticDefinition(
                JobQueueDiagnosticCodes.JobKindUnsupported,
                DiagnosticSeverities.Error,
                "No payload executor is registered for the job kind.",
                Retryable: false),
            new JobQueueDiagnosticDefinition(
                JobQueueDiagnosticCodes.QueueFull,
                DiagnosticSeverities.Error,
                "The pending queue is full; the submission was rejected.",
                Retryable: true),
            new JobQueueDiagnosticDefinition(
                JobQueueDiagnosticCodes.StoreUnavailable,
                DiagnosticSeverities.Error,
                "The job store is unavailable or corrupt.",
                Retryable: false),
            new JobQueueDiagnosticDefinition(
                JobQueueDiagnosticCodes.StoreVersionUnsupported,
                DiagnosticSeverities.Error,
                "The job store schema version is unsupported.",
                Retryable: false),
            new JobQueueDiagnosticDefinition(
                JobQueueDiagnosticCodes.ExecutorLockUnavailable,
                DiagnosticSeverities.Error,
                "Another executor already holds the queue execution lock.",
                Retryable: true),
            new JobQueueDiagnosticDefinition(
                JobQueueDiagnosticCodes.WaitingProjectLock,
                DiagnosticSeverities.Info,
                "The Unity editor holds the project; the queue is waiting for the project lock.",
                Retryable: true),
            new JobQueueDiagnosticDefinition(
                JobQueueDiagnosticCodes.InvalidTransition,
                DiagnosticSeverities.Error,
                "The requested job state transition is not allowed.",
                Retryable: false),
            new JobQueueDiagnosticDefinition(
                JobQueueDiagnosticCodes.JobNotFound,
                DiagnosticSeverities.Error,
                "The job does not exist in the store.",
                Retryable: false),
            new JobQueueDiagnosticDefinition(
                JobQueueDiagnosticCodes.EventLogWriteFailed,
                DiagnosticSeverities.Error,
                "A job event could not be persisted; the job was settled as failed.",
                Retryable: false),
            new JobQueueDiagnosticDefinition(
                JobQueueDiagnosticCodes.BatchAborted,
                DiagnosticSeverities.Error,
                "An earlier job in the batch failed and the batch policy is abort.",
                Retryable: false),
            new JobQueueDiagnosticDefinition(
                JobQueueDiagnosticCodes.ExecutorHostFault,
                DiagnosticSeverities.Error,
                "The executor host failed outside the job payload; the job was settled as failed.",
                Retryable: true),
        }.ToFrozenDictionary(definition => definition.Code, StringComparer.Ordinal);

    internal static FrozenSet<string> Codes { get; } =
        Definitions.Keys.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, JobQueueDiagnosticDefinition> All => Definitions;

    public static JobQueueDiagnosticDefinition Require(string code) =>
        Definitions.TryGetValue(code, out var definition)
            ? definition
            : throw new ArgumentOutOfRangeException(nameof(code));
}
