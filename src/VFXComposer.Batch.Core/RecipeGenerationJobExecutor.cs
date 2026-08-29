using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.Jobs;

namespace VFXComposer.Batch.Core;

/// <summary>
/// Payload executor for prompt entries. It adapts the F1 generation channel to the queue: one
/// job runs one explicit generate action, and a job succeeds once an L1-valid draft has been
/// persisted awaiting confirmation. Its artifacts are the draft identity and the canonical
/// recipe hash — identities only, mirroring the location-free artifact contract. The Unity
/// project is never opened, so the host does not need the project lock for this kind.
/// </summary>
public sealed class RecipeGenerationJobExecutor : IJobExecutor
{
    private const int ProgressGenerating = 100;
    private const int ProgressDraftPersisted = 900;

    private readonly Func<IRecipeGenerationChannel> _acquireChannel;
    private readonly Func<IRecipeDraftStore> _acquireDraftStore;

    /// <summary>
    /// The accessors are invoked only inside <see cref="ExecuteAsync"/>, so constructing the host
    /// never reaches the network-capable chat gateway; the first HTTP-capable construction still
    /// happens inside an explicit generate action.
    /// </summary>
    public RecipeGenerationJobExecutor(
        Func<IRecipeGenerationChannel> acquireChannel,
        Func<IRecipeDraftStore> acquireDraftStore)
    {
        _acquireChannel = acquireChannel ?? throw new ArgumentNullException(nameof(acquireChannel));
        _acquireDraftStore = acquireDraftStore ?? throw new ArgumentNullException(nameof(acquireDraftStore));
    }

    public string JobKind => BatchJobKinds.RecipeGeneration;

    public bool RequiresProjectLock => false;

    public override string ToString() => "RecipeGenerationJobExecutor(" + JobKind + ")";

    public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        BatchGenerationPayloadContent content;
        try
        {
            content = BatchGenerationPayload.Parse(context.Payload);
        }
        catch (Exception exception) when (exception is InvalidDataException or System.Text.Json.JsonException)
        {
            throw new JobQueueException(JobQueueDiagnosticCodes.ExecutionFailed, exception);
        }

        context.ReportProgress(ProgressGenerating);
        var request = new RecipeGenerationRequest(
            context.JobId,
            BatchGenerationPayload.ComposeDescription(content.Prompt, content.Constraints));
        var result = await _acquireChannel().GenerateAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.Outcome == RecipeGenerationOutcome.Cancelled)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        if (result.Outcome != RecipeGenerationOutcome.Drafted)
        {
            // Validation exhaustion and channel failures are both terminal for this job: the queue
            // never retries by itself, and the stable code is all the entry surface may show.
            throw new JobQueueException(JobQueueDiagnosticCodes.ExecutionFailed);
        }

        var record = _acquireDraftStore().Save(RecipeDraftRecord.Create(result, DateTimeOffset.UtcNow));
        context.ReportProgress(ProgressDraftPersisted);
        context.ReportArtifact(record.DraftId);
        context.ReportArtifact("sha256:" + record.CanonicalSha256);
    }
}
