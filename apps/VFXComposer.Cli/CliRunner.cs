using System.Text;
using VFXComposer.Batch.Core;
using VFXComposer.Jobs;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Cli;

/// <summary>
/// The command implementations. Every command is a thin adapter over
/// <c>VFXComposer.Batch.Core</c>: this type binds arguments, formats output and maps outcomes to
/// the REQ-002 §6.5 exit codes, and never re-implements parsing, validation, enqueueing or
/// reporting. Only <c>batch run</c> and <c>batch validate</c> open the generation runtime; the
/// query commands touch the queue store alone and therefore construct nothing network-capable.
/// </summary>
public static class CliRunner
{
    private const string ReportSuffix = ".report.json";

    public static async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        CliEnvironment environment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(environment);
        var parse = CliArguments.Parse(arguments);
        if (parse.HelpRequested)
        {
            CliUsage.Write(environment.Output);
            return CliExitCodes.Success;
        }

        if (parse.Command is null)
        {
            environment.Error.WriteLine(parse.UsageError ?? "Invalid command line.");
            CliUsage.Write(environment.Error);
            return CliExitCodes.UsageError;
        }

        var command = parse.Command;
        return (command.Group, command.Action) switch
        {
            (CliCommandGroups.Batch, CliCommandActions.Validate) =>
                await ValidateAsync(command, environment).ConfigureAwait(false),
            (CliCommandGroups.Batch, CliCommandActions.Run) =>
                await RunBatchAsync(command, environment, cancellationToken).ConfigureAwait(false),
            (CliCommandGroups.Batch, CliCommandActions.Status) =>
                await BatchStatusAsync(command, environment).ConfigureAwait(false),
            (CliCommandGroups.Batch, CliCommandActions.Cancel) =>
                await BatchCancelAsync(command, environment).ConfigureAwait(false),
            (CliCommandGroups.Job, CliCommandActions.Status) =>
                await JobStatusAsync(command, environment).ConfigureAwait(false),
            (CliCommandGroups.Job, CliCommandActions.Cancel) =>
                await JobCancelAsync(command, environment).ConfigureAwait(false),
            _ => await QueueListAsync(command, environment).ConfigureAwait(false),
        };
    }

    private static async Task<int> ValidateAsync(CliCommand command, CliEnvironment environment)
    {
        var presenter = new CliPresenter(environment.Output, command.Json);
        var errorPresenter = new CliPresenter(environment.Error, command.Json);
        var manifestPath = command.Argument!;
        if (!TryReadManifest(manifestPath, errorPresenter, out var manifestJson))
        {
            return CliExitCodes.DataError;
        }

        await using var runtime = environment.OpenGenerationRuntime();
        var result = BatchManifestParser.Parse(
            manifestJson,
            new FileSystemBatchRecipeProbe(ManifestDirectory(manifestPath)),
            runtime.Capability);
        foreach (var issue in result.Issues)
        {
            presenter.Issue(issue);
        }

        if (result.IsValid)
        {
            presenter.ManifestAccepted(result.Manifest!);
            return CliExitCodes.Success;
        }

        errorPresenter.Notice(CliNoticeCodes.ManifestRejected, CliNoticeCatalog.Require(CliNoticeCodes.ManifestRejected));
        return CliExitCodes.DataError;
    }

    private static async Task<int> RunBatchAsync(
        CliCommand command,
        CliEnvironment environment,
        CancellationToken cancellationToken)
    {
        var presenter = new CliPresenter(environment.Output, command.Json);
        var errorPresenter = new CliPresenter(environment.Error, command.Json);
        var manifestPath = command.Argument!;
        if (!TryReadManifest(manifestPath, errorPresenter, out var manifestJson))
        {
            return CliExitCodes.DataError;
        }

        await using var runtime = environment.OpenGenerationRuntime();
        var recipes = new FileSystemBatchRecipeProbe(ManifestDirectory(manifestPath));
        var parsed = BatchManifestParser.Parse(manifestJson, recipes, runtime.Capability);
        foreach (var issue in parsed.Issues)
        {
            presenter.Issue(issue);
        }

        if (!parsed.IsValid)
        {
            errorPresenter.Notice(CliNoticeCodes.ManifestRejected, CliNoticeCatalog.Require(CliNoticeCodes.ManifestRejected));
            return CliExitCodes.DataError;
        }

        var manifest = command.Run.OnFailureOverride is string overridePolicy
            ? parsed.Manifest! with { FailurePolicy = overridePolicy }
            : parsed.Manifest!;
        await using var queue = environment.OpenQueue();
        if (command.Run.DryRun)
        {
            return DryRun(manifest, command, queue, presenter, errorPresenter);
        }

        BatchSubmissionResult submission;
        try
        {
            // Skipping entries whose content already succeeded is the default (REQ-002 §12,
            // REQ-002-16); --resume is the explicit spelling of that default and --force is the
            // only switch that turns it off. The parser enforces that the two never combine.
            submission = new BatchSubmissionService(queue.Client, JobSourceEntries.Cli, recipes)
                .Submit(manifest, command.Run.Force);
        }
        catch (JobQueueException exception)
        {
            errorPresenter.Notice(CliNoticeCodes.QueueUnavailable, exception.Message, exception.Code);
            return CliExitCodes.QueueUnavailable;
        }
        catch (InvalidDataException)
        {
            errorPresenter.Notice(CliNoticeCodes.ManifestRejected, CliNoticeCatalog.Require(CliNoticeCodes.ManifestRejected));
            return CliExitCodes.DataError;
        }

        foreach (var skipped in submission.Items.Where(static item => item.JobId is null))
        {
            presenter.ItemSkipped(skipped.ItemId);
        }

        if (command.Run.Detach)
        {
            presenter.Notice(CliNoticeCodes.BatchDetached, CliNoticeCatalog.Require(CliNoticeCodes.BatchDetached));
            var detachedReport = BatchReportBuilder.Create(
                manifest,
                submission,
                new Dictionary<string, JobRecord>(StringComparer.Ordinal),
                environment.UtcNow());
            presenter.BatchSummary(detachedReport);
            return TryWriteReport(ResolveReportPath(command, manifestPath), detachedReport, presenter, errorPresenter)
                ? CliExitCodes.Success
                : CliExitCodes.QueueUnavailable;
        }

        var executors = new List<IJobExecutor>
        {
            new RecipeGenerationJobExecutor(() => runtime.GenerationChannel, () => runtime.DraftStore),
        };
        if (runtime.CreateRecipeBuildExecutor() is IJobExecutor buildExecutor)
        {
            executors.Add(buildExecutor);
        }

        if (!queue.TryStartExecutors(executors))
        {
            presenter.Notice(
                CliNoticeCodes.ObservingForeignExecutor,
                CliNoticeCatalog.Require(CliNoticeCodes.ObservingForeignExecutor));
        }

        var tracking = await new BatchTracker(
                queue.Client,
                environment.Tracking with { ProjectLockTimeout = command.Run.LockTimeout })
            .TrackAsync(submission.Items, new PresenterTrackingSink(presenter), cancellationToken)
            .ConfigureAwait(false);
        var report = BatchReportBuilder.Create(manifest, submission, tracking.JobsByItemId, environment.UtcNow());
        presenter.BatchSummary(report);
        if (!TryWriteReport(ResolveReportPath(command, manifestPath), report, presenter, errorPresenter))
        {
            return CliExitCodes.QueueUnavailable;
        }

        return ResolveExitCode(tracking.Status, report, manifest.FailurePolicy, errorPresenter);
    }

    private static int DryRun(
        BatchManifest manifest,
        CliCommand command,
        ICliQueueSession queue,
        CliPresenter presenter,
        CliPresenter errorPresenter)
    {
        HashSet<string> completedKeys;
        try
        {
            completedKeys = command.Run.Force
                ? new HashSet<string>(StringComparer.Ordinal)
                : queue.Client.ReadSnapshot().Jobs
                    .Where(static job => string.Equals(job.State, JobStatusStates.Succeeded, StringComparison.Ordinal))
                    .Select(static job => job.EntryIdempotencyKey)
                    .ToHashSet(StringComparer.Ordinal);
        }
        catch (JobQueueException exception)
        {
            errorPresenter.Notice(CliNoticeCodes.QueueUnavailable, exception.Message, exception.Code);
            return CliExitCodes.QueueUnavailable;
        }

        var queuePolicy = BatchFailurePolicies.ToQueuePolicy(manifest.FailurePolicy);
        foreach (var item in manifest.Items)
        {
            var request = BatchSubmissionService.CreateRequest(
                JobSourceEntries.Cli,
                manifest,
                queuePolicy,
                item);
            presenter.ItemPlanned(
                item.ItemId,
                request.EntryIdempotencyKey,
                !completedKeys.Contains(request.EntryIdempotencyKey));
        }

        presenter.Notice(CliNoticeCodes.DryRunPlanOnly, CliNoticeCatalog.Require(CliNoticeCodes.DryRunPlanOnly));
        return CliExitCodes.Success;
    }

    private static async Task<int> BatchStatusAsync(CliCommand command, CliEnvironment environment)
    {
        var presenter = new CliPresenter(environment.Output, command.Json);
        var errorPresenter = new CliPresenter(environment.Error, command.Json);
        await using var queue = environment.OpenQueue();
        JobQueueSnapshotView snapshot;
        try
        {
            snapshot = queue.Client.ReadSnapshot();
        }
        catch (JobQueueException exception)
        {
            errorPresenter.Notice(CliNoticeCodes.QueueUnavailable, exception.Message, exception.Code);
            return CliExitCodes.QueueUnavailable;
        }

        var jobs = snapshot.Jobs
            .Where(job => string.Equals(job.BatchId, command.Argument, StringComparison.Ordinal))
            .ToArray();
        if (jobs.Length == 0)
        {
            errorPresenter.Notice(CliNoticeCodes.NotFound, CliNoticeCatalog.Require(CliNoticeCodes.NotFound));
            return CliExitCodes.DataError;
        }

        presenter.QueueState(snapshot.QueueState);
        foreach (var job in jobs)
        {
            presenter.JobLine(job);
        }

        return CliExitCodes.Success;
    }

    private static async Task<int> BatchCancelAsync(CliCommand command, CliEnvironment environment)
    {
        var presenter = new CliPresenter(environment.Output, command.Json);
        var errorPresenter = new CliPresenter(environment.Error, command.Json);
        await using var queue = environment.OpenQueue();
        try
        {
            var result = new BatchCancellationService(queue.Client).Cancel(command.Argument!);
            if (!result.BatchFound)
            {
                errorPresenter.Notice(CliNoticeCodes.NotFound, CliNoticeCatalog.Require(CliNoticeCodes.NotFound));
                return CliExitCodes.DataError;
            }

            presenter.BatchCancellation(result);
            return CliExitCodes.Success;
        }
        catch (JobQueueException exception)
        {
            errorPresenter.Notice(CliNoticeCodes.QueueUnavailable, exception.Message, exception.Code);
            return CliExitCodes.QueueUnavailable;
        }
    }

    private static async Task<int> JobStatusAsync(CliCommand command, CliEnvironment environment)
    {
        var presenter = new CliPresenter(environment.Output, command.Json);
        var errorPresenter = new CliPresenter(environment.Error, command.Json);
        await using var queue = environment.OpenQueue();
        JobQueueSnapshotView snapshot;
        try
        {
            snapshot = queue.Client.ReadSnapshot();
        }
        catch (JobQueueException exception)
        {
            errorPresenter.Notice(CliNoticeCodes.QueueUnavailable, exception.Message, exception.Code);
            return CliExitCodes.QueueUnavailable;
        }

        var job = snapshot.Jobs.FirstOrDefault(record =>
            string.Equals(record.JobId, command.Argument, StringComparison.Ordinal));
        if (job is null)
        {
            errorPresenter.Notice(CliNoticeCodes.NotFound, CliNoticeCatalog.Require(CliNoticeCodes.NotFound));
            return CliExitCodes.DataError;
        }

        presenter.JobLine(job);
        return CliExitCodes.Success;
    }

    private static async Task<int> JobCancelAsync(CliCommand command, CliEnvironment environment)
    {
        var presenter = new CliPresenter(environment.Output, command.Json);
        var errorPresenter = new CliPresenter(environment.Error, command.Json);
        await using var queue = environment.OpenQueue();
        try
        {
            var result = queue.Client.RequestCancel(command.Argument!);
            presenter.CancellationResult(command.Argument!, result);
            return CliExitCodes.Success;
        }
        catch (JobQueueException exception)
            when (string.Equals(exception.Code, JobQueueDiagnosticCodes.JobNotFound, StringComparison.Ordinal))
        {
            errorPresenter.Notice(
                CliNoticeCodes.NotFound,
                CliNoticeCatalog.Require(CliNoticeCodes.NotFound),
                exception.Code);
            return CliExitCodes.DataError;
        }
        catch (JobQueueException exception)
        {
            errorPresenter.Notice(CliNoticeCodes.QueueUnavailable, exception.Message, exception.Code);
            return CliExitCodes.QueueUnavailable;
        }
    }

    private static async Task<int> QueueListAsync(CliCommand command, CliEnvironment environment)
    {
        var presenter = new CliPresenter(environment.Output, command.Json);
        var errorPresenter = new CliPresenter(environment.Error, command.Json);
        await using var queue = environment.OpenQueue();
        JobQueueSnapshotView snapshot;
        try
        {
            snapshot = queue.Client.ReadSnapshot();
        }
        catch (JobQueueException exception)
        {
            errorPresenter.Notice(CliNoticeCodes.QueueUnavailable, exception.Message, exception.Code);
            return CliExitCodes.QueueUnavailable;
        }

        presenter.QueueState(snapshot.QueueState);
        foreach (var job in snapshot.Jobs)
        {
            presenter.JobLine(job);
        }

        return CliExitCodes.Success;
    }

    private static int ResolveExitCode(
        BatchTrackingStatus status,
        BatchReport report,
        string failurePolicy,
        CliPresenter errorPresenter)
    {
        switch (status)
        {
            case BatchTrackingStatus.Interrupted:
                errorPresenter.Notice(CliNoticeCodes.Interrupted, CliNoticeCatalog.Require(CliNoticeCodes.Interrupted));
                return CliExitCodes.Interrupted;
            case BatchTrackingStatus.ProjectLockTimeout:
                errorPresenter.Notice(
                    CliNoticeCodes.ProjectLockTimeout,
                    CliNoticeCatalog.Require(CliNoticeCodes.ProjectLockTimeout));
                return CliExitCodes.ProjectLockTimeout;
            case BatchTrackingStatus.StoreUnavailable:
                errorPresenter.Notice(
                    CliNoticeCodes.QueueUnavailable,
                    CliNoticeCatalog.Require(CliNoticeCodes.QueueUnavailable));
                return CliExitCodes.QueueUnavailable;
            default:
                var verdict = BatchReportBuilder.Evaluate(report, failurePolicy);
                var exitCode = CliExitCodes.ForVerdict(verdict);
                if (verdict is not (BatchVerdict.AllSucceeded or BatchVerdict.CompletedWithFailures
                    or BatchVerdict.Aborted))
                {
                    // Tracking declared every entry terminal while the report still counts open
                    // entries; the two cannot both be right. It is unreachable today and is
                    // announced rather than silently folded into an ordinary failure code.
                    errorPresenter.Notice(
                        CliNoticeCodes.BatchVerdictInconsistent,
                        CliNoticeCatalog.Require(CliNoticeCodes.BatchVerdictInconsistent));
                }

                return exitCode;
        }
    }

    private static bool TryReadManifest(string path, CliPresenter errorPresenter, out string manifestJson)
    {
        manifestJson = string.Empty;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                errorPresenter.Issue(new BatchValidationIssue(BatchDiagnosticCodes.ManifestUnreadable, "$"));
                return false;
            }

            if (info.Length > BatchManifestLimits.MaximumManifestBytes)
            {
                errorPresenter.Issue(new BatchValidationIssue(
                    BatchDiagnosticCodes.ManifestTooLarge,
                    "$",
                    allowedRange: "<= " + BatchManifestLimits.MaximumManifestBytes.ToString(
                        System.Globalization.CultureInfo.InvariantCulture) + " bytes"));
                return false;
            }

            manifestJson = File.ReadAllText(path, Encoding.UTF8);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException or PathTooLongException or DecoderFallbackException)
        {
            // The failing path is deliberately not echoed: manifest locations are user paths and
            // stay out of every output surface (REQ-002 §6.6).
            errorPresenter.Issue(new BatchValidationIssue(BatchDiagnosticCodes.ManifestUnreadable, "$"));
            return false;
        }
    }

    private static bool TryWriteReport(
        string reportPath,
        BatchReport report,
        CliPresenter presenter,
        CliPresenter errorPresenter)
    {
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(reportPath, BatchReportBuilder.Serialize(report), new UTF8Encoding(false));
            presenter.Notice(CliNoticeCodes.ReportWritten, CliNoticeCatalog.Require(CliNoticeCodes.ReportWritten));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException or PathTooLongException)
        {
            errorPresenter.Notice(
                CliNoticeCodes.ReportNotWritten,
                CliNoticeCatalog.Require(CliNoticeCodes.ReportNotWritten));
            return false;
        }
    }

    private static string ResolveReportPath(CliCommand command, string manifestPath) =>
        command.Run.ReportPath ?? manifestPath + ReportSuffix;

    private static string ManifestDirectory(string manifestPath) =>
        Path.GetDirectoryName(Path.GetFullPath(manifestPath)) ?? Directory.GetCurrentDirectory();

    private sealed class PresenterTrackingSink : IBatchTrackingSink
    {
        private readonly CliPresenter _presenter;

        public PresenterTrackingSink(CliPresenter presenter)
        {
            _presenter = presenter;
        }

        public void OnJobUpdated(BatchTrackingUpdate update) =>
            _presenter.JobUpdated(update.ItemId, update.Job);

        public void OnWaitingProjectLock() =>
            _presenter.Notice(
                CliNoticeCodes.WaitingProjectLock,
                CliNoticeCatalog.Require(CliNoticeCodes.WaitingProjectLock));
    }
}
