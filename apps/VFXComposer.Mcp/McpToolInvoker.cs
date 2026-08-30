using System.Buffers;
using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VFXComposer.Batch.Core;
using VFXComposer.Jobs;

namespace VFXComposer.Mcp;

/// <summary>
/// One tool invocation outcome. A <see cref="ProtocolRejection"/> is answered as a JSON-RPC error
/// because the call was never a valid call; anything the tool actually ran and refused comes back
/// as a tool result carrying <see cref="IsError"/>, which is the split the MCP tool contract asks
/// for.
/// </summary>
internal sealed record McpToolResponse(string? Payload, bool IsError, string? ProtocolRejection)
{
    public static McpToolResponse Reject(string diagnosticCode) => new(null, IsError: false, diagnosticCode);

    public static McpToolResponse Ok(string payload) => new(payload, IsError: false, null);

    public static McpToolResponse Failed(string payload) => new(payload, IsError: true, null);
}

/// <summary>
/// The tool implementations. Every tool is a thin adapter over <c>VFXComposer.Batch.Core</c>: it
/// binds arguments, calls the one shared execution-layer API its CLI counterpart calls, and
/// formats the outcome. Nothing here re-implements manifest parsing, validation, enqueueing,
/// cancellation or reporting, and nothing here writes to the Unity project or opens a network
/// connection. Only identifiers, closed vocabulary words, stable codes and counters reach a
/// payload: no prompt text, no secret, no endpoint, no filesystem path (REQ-002 §6.6).
/// </summary>
internal sealed class McpToolInvoker
{
    private const int MaximumItemUtf8Bytes = 64 * 1024;

    private static readonly FrozenSet<string> ManifestFields =
        new[] { "manifest" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> SubmitFields =
        new[] { "manifest", "onFailure" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ItemFields =
        new[] { "item" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> BatchIdFields =
        new[] { "batchId" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> JobIdFields =
        new[] { "jobId" }.ToFrozenSet(StringComparer.Ordinal);

    private readonly McpEnvironment _environment;

    public McpToolInvoker(McpEnvironment environment)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public override string ToString() => "McpToolInvoker";

    public async Task<McpToolResponse> InvokeAsync(string toolName, JsonElement? arguments)
    {
        ArgumentNullException.ThrowIfNull(toolName);
        return toolName switch
        {
            McpToolNames.ValidateManifest => await ValidateManifestAsync(arguments).ConfigureAwait(false),
            McpToolNames.SubmitBatch => await SubmitBatchAsync(arguments).ConfigureAwait(false),
            McpToolNames.GenerateEffect => await GenerateEffectAsync(arguments).ConfigureAwait(false),
            McpToolNames.BatchStatus => await BatchStatusAsync(arguments).ConfigureAwait(false),
            McpToolNames.JobStatus => await JobStatusAsync(arguments).ConfigureAwait(false),
            McpToolNames.CancelJob => await CancelJobAsync(arguments).ConfigureAwait(false),
            McpToolNames.CancelBatch => await CancelBatchAsync(arguments).ConfigureAwait(false),
            McpToolNames.GetBatchReport => await GetBatchReportAsync(arguments).ConfigureAwait(false),
            _ => McpToolResponse.Reject(McpDiagnosticCodes.UnknownTool),
        };
    }

    private async Task<McpToolResponse> ValidateManifestAsync(JsonElement? arguments)
    {
        if (!McpToolArguments.TryBind(arguments, ManifestFields, out var bound) ||
            !bound.TryReadText("manifest", BatchManifestLimits.MaximumManifestBytes, out var manifestJson))
        {
            return McpToolResponse.Reject(McpDiagnosticCodes.InvalidToolArguments);
        }

        await using var runtime = _environment.OpenGenerationRuntime();
        var parsed = Parse(manifestJson, runtime.Capability);
        return parsed.IsValid
            ? McpToolResponse.Ok(Payload(McpToolNames.ValidateManifest, writer =>
            {
                writer.WriteBoolean("accepted", true);
                writer.WriteString("batchId", parsed.Manifest!.BatchId);
                writer.WriteString("onFailure", parsed.Manifest.FailurePolicy);
                writer.WriteNumber("itemCount", parsed.Manifest.Items.Count);
                WriteIssues(writer, parsed.Issues);
            }))
            : McpToolResponse.Failed(Payload(McpToolNames.ValidateManifest, writer =>
            {
                writer.WriteBoolean("accepted", false);
                WriteDiagnostic(writer, McpDiagnosticCodes.ManifestRejected);
                WriteIssues(writer, parsed.Issues);
            }));
    }

    private async Task<McpToolResponse> SubmitBatchAsync(JsonElement? arguments)
    {
        if (!McpToolArguments.TryBind(arguments, SubmitFields, out var bound) ||
            !bound.TryReadText("manifest", BatchManifestLimits.MaximumManifestBytes, out var manifestJson) ||
            !bound.TryReadOptionalVocabulary("onFailure", BatchFailurePolicies.All, out var policyOverride))
        {
            return McpToolResponse.Reject(McpDiagnosticCodes.InvalidToolArguments);
        }

        return await SubmitAsync(McpToolNames.SubmitBatch, manifestJson, policyOverride, WriteBatchSubmission)
            .ConfigureAwait(false);
    }

    private async Task<McpToolResponse> GenerateEffectAsync(JsonElement? arguments)
    {
        if (!McpToolArguments.TryBind(arguments, ItemFields, out var bound) ||
            !bound.TryReadObject("item", MaximumItemUtf8Bytes, out var item))
        {
            return McpToolResponse.Reject(McpDiagnosticCodes.InvalidToolArguments);
        }

        // The single entry is wrapped into a one-entry manifest and handed to the same parser the
        // batch tools use, so the entry is bound, gated and refused by exactly the same rules
        // rather than by a second validation path.
        var manifestJson = SingleEntryManifest(item);
        return await SubmitAsync(McpToolNames.GenerateEffect, manifestJson, policyOverride: null, WriteSingleSubmission)
            .ConfigureAwait(false);
    }

    private async Task<McpToolResponse> SubmitAsync(
        string toolName,
        string manifestJson,
        string? policyOverride,
        Action<Utf8JsonWriter, BatchSubmissionResult> writeOutcome)
    {
        BatchManifest manifest;
        await using (var runtime = _environment.OpenGenerationRuntime())
        {
            var parsed = Parse(manifestJson, runtime.Capability);
            if (!parsed.IsValid)
            {
                return McpToolResponse.Failed(Payload(toolName, writer =>
                {
                    WriteDiagnostic(writer, McpDiagnosticCodes.ManifestRejected);
                    WriteIssues(writer, parsed.Issues);
                }));
            }

            manifest = policyOverride is string policy
                ? parsed.Manifest! with { FailurePolicy = policy }
                : parsed.Manifest!;
        }

        await using var queue = _environment.OpenQueue();
        try
        {
            // Skipping entries whose content already succeeded is the default (REQ-002 §12); this
            // surface offers no force switch, so a repeated submission of unchanged content is
            // reported as skipped rather than run again.
            var submission = new BatchSubmissionService(queue.Client, JobSourceEntries.Mcp)
                .Submit(manifest, force: false);
            return McpToolResponse.Ok(Payload(toolName, writer =>
            {
                writer.WriteString("onFailure", manifest.FailurePolicy);
                writeOutcome(writer, submission);
            }));
        }
        catch (JobQueueException exception)
        {
            return McpToolResponse.Failed(QueueFailure(toolName, exception));
        }
    }

    private async Task<McpToolResponse> BatchStatusAsync(JsonElement? arguments)
    {
        if (!McpToolArguments.TryBind(arguments, BatchIdFields, out var bound) ||
            !bound.TryReadIdentifier("batchId", out var batchId))
        {
            return McpToolResponse.Reject(McpDiagnosticCodes.InvalidToolArguments);
        }

        await using var queue = _environment.OpenQueue();
        try
        {
            var snapshot = queue.Client.ReadSnapshot();
            var jobs = snapshot.Jobs
                .Where(job => string.Equals(job.BatchId, batchId, StringComparison.Ordinal))
                .ToArray();
            if (jobs.Length == 0)
            {
                return McpToolResponse.Failed(NotFound(McpToolNames.BatchStatus));
            }

            return McpToolResponse.Ok(Payload(McpToolNames.BatchStatus, writer =>
            {
                writer.WriteString("batchId", batchId);
                writer.WriteString("queueState", snapshot.QueueState);
                writer.WriteStartArray("jobs");
                foreach (var job in jobs)
                {
                    WriteJob(writer, job);
                }

                writer.WriteEndArray();
            }));
        }
        catch (JobQueueException exception)
        {
            return McpToolResponse.Failed(QueueFailure(McpToolNames.BatchStatus, exception));
        }
    }

    private async Task<McpToolResponse> JobStatusAsync(JsonElement? arguments)
    {
        if (!McpToolArguments.TryBind(arguments, JobIdFields, out var bound) ||
            !bound.TryReadIdentifier("jobId", out var jobId))
        {
            return McpToolResponse.Reject(McpDiagnosticCodes.InvalidToolArguments);
        }

        await using var queue = _environment.OpenQueue();
        try
        {
            var snapshot = queue.Client.ReadSnapshot();
            var job = snapshot.Jobs.FirstOrDefault(record =>
                string.Equals(record.JobId, jobId, StringComparison.Ordinal));
            if (job is null)
            {
                return McpToolResponse.Failed(NotFound(McpToolNames.JobStatus));
            }

            return McpToolResponse.Ok(Payload(McpToolNames.JobStatus, writer =>
            {
                writer.WriteString("queueState", snapshot.QueueState);
                writer.WritePropertyName("job");
                writer.WriteStartObject();
                WriteJobBody(writer, job, includeArtifactIds: true);
                writer.WriteEndObject();
            }));
        }
        catch (JobQueueException exception)
        {
            return McpToolResponse.Failed(QueueFailure(McpToolNames.JobStatus, exception));
        }
    }

    private async Task<McpToolResponse> CancelJobAsync(JsonElement? arguments)
    {
        if (!McpToolArguments.TryBind(arguments, JobIdFields, out var bound) ||
            !bound.TryReadIdentifier("jobId", out var jobId))
        {
            return McpToolResponse.Reject(McpDiagnosticCodes.InvalidToolArguments);
        }

        await using var queue = _environment.OpenQueue();
        try
        {
            var result = queue.Client.RequestCancel(jobId);
            return McpToolResponse.Ok(Payload(McpToolNames.CancelJob, writer =>
            {
                writer.WriteString("jobId", jobId);
                writer.WriteString("state", result.State);
                writer.WriteBoolean("accepted", result.Accepted);
            }));
        }
        catch (JobQueueException exception)
            when (string.Equals(exception.Code, JobQueueDiagnosticCodes.JobNotFound, StringComparison.Ordinal))
        {
            return McpToolResponse.Failed(NotFound(McpToolNames.CancelJob, exception.Code));
        }
        catch (JobQueueException exception)
        {
            return McpToolResponse.Failed(QueueFailure(McpToolNames.CancelJob, exception));
        }
    }

    private async Task<McpToolResponse> CancelBatchAsync(JsonElement? arguments)
    {
        if (!McpToolArguments.TryBind(arguments, BatchIdFields, out var bound) ||
            !bound.TryReadIdentifier("batchId", out var batchId))
        {
            return McpToolResponse.Reject(McpDiagnosticCodes.InvalidToolArguments);
        }

        await using var queue = _environment.OpenQueue();
        try
        {
            var result = new BatchCancellationService(queue.Client).Cancel(batchId);
            if (!result.BatchFound)
            {
                return McpToolResponse.Failed(NotFound(McpToolNames.CancelBatch));
            }

            return McpToolResponse.Ok(Payload(McpToolNames.CancelBatch, writer =>
            {
                writer.WriteString("batchId", result.BatchId);
                writer.WriteNumber("requested", result.Requested);
                writer.WriteNumber("accepted", result.Accepted);
                writer.WriteNumber("noOp", result.NoOp);
                writer.WriteStartArray("jobs");
                foreach (var item in result.Items)
                {
                    writer.WriteStartObject();
                    writer.WriteString("jobId", item.JobId);
                    writer.WriteString("state", item.State);
                    writer.WriteBoolean("accepted", item.Accepted);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }));
        }
        catch (JobQueueException exception)
        {
            return McpToolResponse.Failed(QueueFailure(McpToolNames.CancelBatch, exception));
        }
    }

    private async Task<McpToolResponse> GetBatchReportAsync(JsonElement? arguments)
    {
        if (!McpToolArguments.TryBind(arguments, BatchIdFields, out var bound) ||
            !bound.TryReadIdentifier("batchId", out var batchId))
        {
            return McpToolResponse.Reject(McpDiagnosticCodes.InvalidToolArguments);
        }

        await using var queue = _environment.OpenQueue();
        try
        {
            var jobs = queue.Client.ReadSnapshot().Jobs
                .Where(job => string.Equals(job.BatchId, batchId, StringComparison.Ordinal))
                .ToArray();
            if (jobs.Length == 0)
            {
                return McpToolResponse.Failed(NotFound(McpToolNames.GetBatchReport));
            }

            var report = BatchQueueReportBuilder.Create(batchId, jobs, _environment.UtcNow());
            return McpToolResponse.Ok(Payload(McpToolNames.GetBatchReport, writer =>
            {
                writer.WritePropertyName("report");
                writer.WriteRawValue(BatchReportBuilder.SerializeCompact(report));
            }));
        }
        catch (JobQueueException exception)
        {
            return McpToolResponse.Failed(QueueFailure(McpToolNames.GetBatchReport, exception));
        }
    }

    private static BatchManifestParseResult Parse(string manifestJson, BatchCapabilityProfile capability) =>
        BatchManifestParser.Parse(manifestJson, InlineManifestRecipeProbe.Instance, capability);

    /// <summary>
    /// Wraps one entry into a one-entry manifest. The batch id is derived from the item id so that
    /// the same entry always lands in the same batch — which keeps the content idempotency key
    /// stable across calls — and is hashed so it is bounded and cannot collide with a batch id the
    /// user authored themselves.
    /// </summary>
    private static string SingleEntryManifest(JsonElement item)
    {
        var itemId = item.TryGetProperty("itemId", out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : string.Empty;
        var buffer = new ArrayBufferWriter<byte>(1024);
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", BatchManifestLimits.SchemaVersion);
            writer.WriteString("batchId", DeriveSingleBatchId(itemId));
            writer.WriteString("onFailure", BatchFailurePolicies.Continue);
            writer.WriteStartArray("items");
            item.WriteTo(writer);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static string DeriveSingleBatchId(string itemId) =>
        "single-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(itemId)))
            .ToLowerInvariant()[..32];

    private static void WriteBatchSubmission(Utf8JsonWriter writer, BatchSubmissionResult submission)
    {
        writer.WriteString("batchId", submission.BatchId);
        writer.WriteNumber("enqueued", submission.Enqueued.Count);
        writer.WriteStartArray("items");
        foreach (var item in submission.Items)
        {
            writer.WriteStartObject();
            writer.WriteString("itemId", item.ItemId);
            writer.WriteString("disposition", item.Disposition);
            if (item.JobId is string jobId)
            {
                writer.WriteString("jobId", jobId);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteSingleSubmission(Utf8JsonWriter writer, BatchSubmissionResult submission)
    {
        var item = submission.Items[0];
        writer.WriteString("batchId", submission.BatchId);
        writer.WriteString("itemId", item.ItemId);
        writer.WriteString("disposition", item.Disposition);
        if (item.JobId is string jobId)
        {
            writer.WriteString("jobId", jobId);
        }
    }

    private static void WriteJob(Utf8JsonWriter writer, JobRecord job)
    {
        writer.WriteStartObject();
        WriteJobBody(writer, job);
        writer.WriteEndObject();
    }

    /// <summary>
    /// The queue-entry projection. Field names follow the Protocol job event DTOs, which is the
    /// same shape the CLI NDJSON stream emits, so both surfaces describe one entry identically.
    /// The single-entry tool additionally spells out the artifact identities the list projections
    /// only count, which is where a refused build's precise code becomes readable.
    /// </summary>
    private static void WriteJobBody(Utf8JsonWriter writer, JobRecord job, bool includeArtifactIds = false)
    {
        writer.WriteString("jobId", job.JobId);
        writer.WriteString("sourceEntry", job.SourceEntry);
        writer.WriteString("jobKind", job.JobKind);
        writer.WriteString("state", job.State);
        writer.WriteNumber("progressPermille", job.LastProgressPermille);
        writer.WriteBoolean("cancelRequested", job.CancelRequested);
        if (job.BatchId is string batchId)
        {
            writer.WriteString("batchId", batchId);
        }

        if (job.FinalDiagnosticCode is string diagnostic)
        {
            writer.WriteString("diagnostic", diagnostic);
        }

        if (job.IsTerminal)
        {
            writer.WriteString("outcome", job.State);
        }

        writer.WriteNumber("artifactCount", job.ArtifactIds.Count);
        if (!includeArtifactIds)
        {
            return;
        }

        writer.WriteStartArray("artifactIds");
        foreach (var artifactId in job.ArtifactIds)
        {
            writer.WriteStringValue(artifactId);
        }

        writer.WriteEndArray();
    }

    private static void WriteIssues(Utf8JsonWriter writer, IReadOnlyList<BatchValidationIssue> issues)
    {
        writer.WriteStartArray("issues");
        foreach (var issue in issues)
        {
            writer.WriteStartObject();
            writer.WriteString("code", issue.Code);
            writer.WriteString("severity", issue.Severity);
            writer.WriteString("path", issue.Path);
            writer.WriteString("message", issue.Message);
            if (issue.ActualValue is string actual)
            {
                writer.WriteString("actualValue", actual);
            }

            if (issue.AllowedRange is string allowed)
            {
                writer.WriteString("allowedRange", allowed);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static string QueueFailure(string toolName, JobQueueException exception) =>
        Payload(toolName, writer =>
            WriteDiagnostic(writer, McpDiagnosticCodes.QueueUnavailable, exception.Code));

    private static string NotFound(string toolName, string? queueDiagnosticCode = null) =>
        Payload(toolName, writer =>
            WriteDiagnostic(writer, McpDiagnosticCodes.NotFound, queueDiagnosticCode));

    private static void WriteDiagnostic(
        Utf8JsonWriter writer,
        string diagnosticCode,
        string? queueDiagnosticCode = null)
    {
        writer.WriteStartObject("error");
        writer.WriteString("code", diagnosticCode);
        writer.WriteString("message", McpDiagnosticCatalog.Require(diagnosticCode));
        if (queueDiagnosticCode is not null)
        {
            writer.WriteString("queueDiagnostic", queueDiagnosticCode);
        }

        writer.WriteEndObject();
    }

    private static string Payload(string toolName, Action<Utf8JsonWriter> body)
    {
        var buffer = new ArrayBufferWriter<byte>(512);
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("tool", toolName);
            body(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>
    /// Recipe probe for a manifest that arrived as inline content. A recipe reference is resolved
    /// relative to the manifest's own directory, and inline content has no directory, so every
    /// reference is reported missing and the manifest is refused (B201). This holds independently
    /// of the capability gate: a build-capable host does accept recipe entries, but only from a
    /// manifest that has a location on disk to resolve them against.
    /// </summary>
    private sealed class InlineManifestRecipeProbe : IBatchRecipeProbe
    {
        public static InlineManifestRecipeProbe Instance { get; } = new();

        public override string ToString() => "InlineManifestRecipeProbe";

        public BatchRecipeProbeResult Probe(string relativePath) => BatchRecipeProbeResult.Missing;
    }
}
