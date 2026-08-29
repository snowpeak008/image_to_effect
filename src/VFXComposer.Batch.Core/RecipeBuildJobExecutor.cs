using VFXComposer.Jobs;

namespace VFXComposer.Batch.Core;

/// <summary>
/// Payload executor for recipe build entries. It opens the Unity project through one short-lived
/// batchmode process, so the host must hold off while an editor owns the project lock. A refused or
/// failed build is terminal: the queue never retries it (ADR-007 bounded-retry exclusion).
/// </summary>
public sealed class RecipeBuildJobExecutor : IJobExecutor
{
    private readonly RecipeBuildOrchestrator _orchestrator;

    public RecipeBuildJobExecutor(RecipeBuildOrchestrator orchestrator)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    public string JobKind => BatchJobKinds.RecipeBuild;

    public bool RequiresProjectLock => true;

    public override string ToString() => "RecipeBuildJobExecutor(" + JobKind + ")";

    public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var decision = await _orchestrator
            .ExecuteAsync(context.Payload, context.TemporaryDirectory, new JobContextSink(context), cancellationToken)
            .ConfigureAwait(false);
        if (decision.Succeeded)
        {
            return;
        }

        throw new JobQueueException(
            JobQueueDiagnosticCodes.ExecutionFailed,
            new RecipeBuildFailureException(decision.FailureCode ?? RecipeBuildFailureCodes.ProcessFailed));
    }

    private sealed class JobContextSink : IRecipeBuildSink
    {
        private readonly JobExecutionContext _context;

        internal JobContextSink(JobExecutionContext context)
        {
            _context = context;
        }

        public void ReportProgress(int progressPermille) => _context.ReportProgress(progressPermille);

        public void ReportLog(string level, string diagnosticCode) => _context.ReportLog(level, diagnosticCode);

        public void ReportArtifact(string artifactId) => _context.ReportArtifact(artifactId);

        public void RegisterChildProcess(int processId, DateTimeOffset processStartUtc) =>
            _context.RegisterChildProcess(processId, processStartUtc);

        public void ClearChildProcess() => _context.ClearChildProcess();
    }
}
