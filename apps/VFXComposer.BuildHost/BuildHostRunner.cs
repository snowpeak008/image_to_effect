using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.Batch.Core;
using VFXComposer.Jobs;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.BuildHost;

/// <summary>
/// One restricted build, end to end (ADR-008 §2.1): re-verify the confirmed draft from the shared
/// store against the two identity arguments, enqueue the draft-backed build payload, host the
/// build executor with the real project-lock probe, and exit once the entry settles. The process
/// arguments are identity, not authorization — the store record is the only authority, so a
/// forged or drifted identity is refused before the queue is touched.
/// </summary>
public static class BuildHostRunner
{
    public static async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        BuildHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(environment);
        if (arguments.Count != 2 ||
            string.IsNullOrWhiteSpace(arguments[0]) ||
            string.IsNullOrWhiteSpace(arguments[1]))
        {
            environment.Output.WriteLine(BuildHostDiagnosticCodes.UsageInvalid);
            return BuildHostExitCodes.UsageError;
        }

        var draftId = arguments[0];
        var canonicalSha256 = arguments[1];

        // The build environment is resolved before anything is enqueued: a host that cannot reach
        // the Unity project must refuse with zero queue writes rather than strand an entry it
        // could never execute (§5 fail-closed table).
        var buildHost = environment.LocateBuildHost();
        if (buildHost is null)
        {
            environment.Output.WriteLine(BuildHostDiagnosticCodes.BuildEnvironmentUnavailable);
            return BuildHostExitCodes.BuildEnvironmentUnavailable;
        }

        IBuildHostDraftSession drafts;
        try
        {
            drafts = environment.OpenDrafts();
        }
        catch (Exception exception) when (exception is RecipeDraftStoreException or AiGatewayException)
        {
            environment.Output.WriteLine(BuildHostDiagnosticCodes.DraftStoreUnavailable);
            return BuildHostExitCodes.DraftStoreUnavailable;
        }

        await using (drafts.ConfigureAwait(false))
        {
            RecipeDraftRecord? draft;
            try
            {
                draft = drafts.Drafts.TryGet(draftId);
            }
            catch (RecipeDraftStoreException)
            {
                environment.Output.WriteLine(BuildHostDiagnosticCodes.DraftStoreUnavailable);
                return BuildHostExitCodes.DraftStoreUnavailable;
            }

            // Independent re-verification (ADR-008 §4): the record must exist, be exactly
            // ConfirmedAwaitingBuild and carry exactly the named canonical hash. The refusal
            // reuses the orchestrator's own stable code for the same condition, so both layers
            // of the double check speak one vocabulary. Zero enqueue, zero writes.
            var refusal = draft is null
                ? RecipeBuildFailureCodes.DraftNotFound
                : draft.Status != RecipeDraftStatus.ConfirmedAwaitingBuild
                    ? RecipeBuildFailureCodes.DraftNotConfirmed
                    : !string.Equals(draft.CanonicalSha256, canonicalSha256, StringComparison.Ordinal)
                        ? RecipeBuildFailureCodes.DraftHashMismatch
                        : null;
            if (refusal is not null)
            {
                environment.Output.WriteLine(refusal);
                return BuildHostExitCodes.DraftIdentityRefused;
            }

            JobStore queue;
            try
            {
                queue = environment.OpenQueue();
            }
            catch (JobQueueException exception)
            {
                WriteQueueRefusal(environment, exception);
                return BuildHostExitCodes.QueueUnavailable;
            }

            return await ExecuteAsync(environment, buildHost, drafts, draft!, queue, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task<int> ExecuteAsync(
        BuildHostEnvironment environment,
        UnityBuildHost buildHost,
        IBuildHostDraftSession drafts,
        RecipeDraftRecord draft,
        JobStore queue,
        CancellationToken cancellationToken)
    {
        // The payload seals the retained record's bytes plus the confirmed hash, so the entry is
        // self-sufficient: any later executor host can run it even if this process dies (§2.5).
        var payload = BatchRecipeBuildPayload.Create(draft.DraftId, draft.RecipeJson);
        var request = new JobEnqueueRequest(JobSourceEntries.Desktop, BatchJobKinds.RecipeBuild, payload);

        string jobId;
        try
        {
            // One explicit build action builds one draft, but a re-click after a lock-held exit
            // must drain the stranded entry instead of stacking a duplicate behind it: an open
            // entry with this exact content key is adopted, otherwise a fresh one is enqueued.
            var existing = queue.ReadSnapshot().Jobs.FirstOrDefault(job =>
                !job.IsTerminal &&
                string.Equals(job.EntryIdempotencyKey, request.EntryIdempotencyKey, StringComparison.Ordinal));
            jobId = existing?.JobId ?? queue.Enqueue(request).JobId;
        }
        catch (JobQueueException exception)
        {
            WriteQueueRefusal(environment, exception);
            return BuildHostExitCodes.QueueUnavailable;
        }

        var executor = new RecipeBuildJobExecutor(new RecipeBuildOrchestrator(
            environment.CreateRunner(buildHost),
            () => drafts.Drafts));
        var host = new JobQueueHost(
            queue,
            [executor],
            environment.HostOptions,
            environment.CreateProjectLockProbe(buildHost));
        try
        {
            host.Start();
        }
        catch (JobQueueException exception)
            when (string.Equals(exception.Code, JobQueueDiagnosticCodes.ExecutorLockUnavailable, StringComparison.Ordinal))
        {
            // Another live host owns queue execution (ADR-008 §2.2). This host must not become a
            // shadow executor: the entry stays queued for the lock holder or the next host.
            await host.DisposeAsync().ConfigureAwait(false);
            environment.Output.WriteLine(BuildHostDiagnosticCodes.ExecutorLockHeld);
            return BuildHostExitCodes.ExecutorLockHeld;
        }
        catch (JobQueueException exception)
        {
            await host.DisposeAsync().ConfigureAwait(false);
            WriteQueueRefusal(environment, exception);
            return BuildHostExitCodes.QueueUnavailable;
        }

        await using (host.ConfigureAwait(false))
        {
            return await WaitForVerdictAsync(environment, queue, jobId, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Polls the shared store until the entry settles. There is deliberately no host-side wait
    /// bound: an editor holding the project keeps the entry QUEUED under the queue's own bounded
    /// backoff (§2.4), Desktop presents that state, and the user's cancel is what ends the wait.
    /// </summary>
    private static async Task<int> WaitForVerdictAsync(
        BuildHostEnvironment environment,
        JobStore queue,
        string jobId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            JobRecord? job;
            try
            {
                job = queue.ReadSnapshot().Jobs.FirstOrDefault(candidate =>
                    string.Equals(candidate.JobId, jobId, StringComparison.Ordinal));
            }
            catch (JobQueueException exception)
            {
                WriteQueueRefusal(environment, exception);
                return BuildHostExitCodes.QueueUnavailable;
            }

            if (job is null)
            {
                // Retention removed the settled entry between polls; the stores remain the
                // authority and this process has nothing left to host.
                environment.Output.WriteLine(JobQueueDiagnosticCodes.JobNotFound);
                return BuildHostExitCodes.QueueUnavailable;
            }

            if (job.IsTerminal)
            {
                if (job.FinalDiagnosticCode is not null)
                {
                    environment.Output.WriteLine(job.FinalDiagnosticCode);
                }

                return job.State switch
                {
                    JobStatusStates.Succeeded => BuildHostExitCodes.BuildSucceeded,
                    JobStatusStates.Cancelled => BuildHostExitCodes.BuildCancelled,
                    JobStatusStates.Disconnected => BuildHostExitCodes.BuildDisconnected,
                    _ => BuildHostExitCodes.BuildFailed,
                };
            }

            await Task.Delay(environment.PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void WriteQueueRefusal(BuildHostEnvironment environment, JobQueueException exception)
    {
        environment.Output.WriteLine(BuildHostDiagnosticCodes.QueueUnavailable);
        environment.Output.WriteLine(exception.Code);
    }
}
