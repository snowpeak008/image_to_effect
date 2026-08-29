using System.Globalization;
using System.Text;
using System.Text.Json;
using VFXComposer.Batch.Core;
using VFXComposer.Jobs;

namespace VFXComposer.Cli;

/// <summary>
/// Formats every CLI output line. Two shapes are supported: readable lines and the NDJSON event
/// stream, whose field names follow the Protocol job DTOs (<c>state</c>, <c>progressPermille</c>,
/// <c>outcome</c>, <c>diagnostic</c>). Only identifiers, closed vocabulary words, stable codes and
/// counters are ever written: no prompt text, no secret, no endpoint, no filesystem path
/// (REQ-002 §6.6).
/// </summary>
internal sealed class CliPresenter
{
    private readonly TextWriter _writer;
    private readonly bool _json;

    public CliPresenter(TextWriter writer, bool json)
    {
        _writer = writer;
        _json = json;
    }

    public void Issue(BatchValidationIssue issue)
    {
        if (_json)
        {
            WriteJson(writer =>
            {
                writer.WriteString("kind", "validationIssue");
                writer.WriteString("code", issue.Code);
                writer.WriteString("severity", issue.Severity);
                writer.WriteString("path", issue.Path);
                writer.WriteString("message", issue.Message);
                WriteOptionalString(writer, "actualValue", issue.ActualValue);
                WriteOptionalString(writer, "allowedRange", issue.AllowedRange);
            });
            return;
        }

        var line = new StringBuilder()
            .Append(issue.Code).Append(' ')
            .Append(issue.Severity).Append(' ')
            .Append(issue.Path).Append(" - ")
            .Append(issue.Message);
        if (issue.ActualValue is not null)
        {
            line.Append(" actual=").Append(issue.ActualValue);
        }

        if (issue.AllowedRange is not null)
        {
            line.Append(" allowed=").Append(issue.AllowedRange);
        }

        _writer.WriteLine(line.ToString());
    }

    public void JobUpdated(string itemId, JobRecord job)
    {
        if (_json)
        {
            WriteJson(writer =>
            {
                writer.WriteString("kind", "jobUpdated");
                writer.WriteString("itemId", itemId);
                writer.WriteString("jobId", job.JobId);
                writer.WriteString("state", job.State);
                writer.WriteNumber("progressPermille", job.LastProgressPermille);
                if (job.IsTerminal)
                {
                    writer.WriteString("outcome", job.State);
                }

                WriteOptionalString(writer, "diagnostic", job.FinalDiagnosticCode);
            });
            return;
        }

        var line = new StringBuilder()
            .Append('[').Append(itemId).Append("] ")
            .Append(job.State).Append(' ')
            .Append(Percent(job.LastProgressPermille));
        if (job.FinalDiagnosticCode is not null)
        {
            line.Append(' ').Append(job.FinalDiagnosticCode);
        }

        if (job.ArtifactIds.Count > 0)
        {
            line.Append(" artifacts=").Append(Number(job.ArtifactIds.Count));
        }

        _writer.WriteLine(line.ToString());
    }

    public void ItemSkipped(string itemId)
    {
        if (_json)
        {
            WriteJson(writer =>
            {
                writer.WriteString("kind", "itemSkipped");
                writer.WriteString("itemId", itemId);
                writer.WriteString("outcome", BatchItemDispositions.SkippedIdempotent);
            });
            return;
        }

        _writer.WriteLine("[" + itemId + "] " + BatchItemDispositions.SkippedIdempotent);
    }

    public void ItemPlanned(string itemId, string entryIdempotencyKey, bool willEnqueue)
    {
        var disposition = willEnqueue ? BatchItemDispositions.Enqueued : BatchItemDispositions.SkippedIdempotent;
        if (_json)
        {
            WriteJson(writer =>
            {
                writer.WriteString("kind", "itemPlanned");
                writer.WriteString("itemId", itemId);
                writer.WriteString("entryIdempotencyKey", entryIdempotencyKey);
                writer.WriteString("disposition", disposition);
            });
            return;
        }

        _writer.WriteLine("[" + itemId + "] PLANNED " + disposition + " key=" + entryIdempotencyKey);
    }

    public void Notice(string code, string message)
    {
        if (_json)
        {
            WriteJson(writer =>
            {
                writer.WriteString("kind", "notice");
                writer.WriteString("code", code);
                writer.WriteString("message", message);
            });
            return;
        }

        _writer.WriteLine(code + " " + message);
    }

    public void BatchSummary(BatchReport report)
    {
        if (_json)
        {
            WriteJson(writer =>
            {
                writer.WriteString("kind", "batchSummary");
                writer.WriteString("schemaVersion", report.SchemaVersion);
                writer.WriteString("batchId", report.BatchId);
                writer.WriteString("onFailure", report.OnFailure);
                writer.WriteStartObject("summary");
                writer.WriteNumber("total", report.Summary.Total);
                writer.WriteNumber("succeeded", report.Summary.Succeeded);
                writer.WriteNumber("failed", report.Summary.Failed);
                writer.WriteNumber("cancelled", report.Summary.Cancelled);
                writer.WriteNumber("disconnected", report.Summary.Disconnected);
                writer.WriteNumber("skippedIdempotent", report.Summary.SkippedIdempotent);
                writer.WriteNumber("pending", report.Summary.Pending);
                writer.WriteEndObject();
            });
            return;
        }

        _writer.WriteLine(
            "batch " + report.BatchId +
            ": total=" + Number(report.Summary.Total) +
            " succeeded=" + Number(report.Summary.Succeeded) +
            " failed=" + Number(report.Summary.Failed) +
            " cancelled=" + Number(report.Summary.Cancelled) +
            " disconnected=" + Number(report.Summary.Disconnected) +
            " skipped=" + Number(report.Summary.SkippedIdempotent) +
            " pending=" + Number(report.Summary.Pending));
    }

    public void ManifestAccepted(BatchManifest manifest)
    {
        if (_json)
        {
            WriteJson(writer =>
            {
                writer.WriteString("kind", "manifestAccepted");
                writer.WriteString("schemaVersion", manifest.SchemaVersion);
                writer.WriteString("batchId", manifest.BatchId);
                writer.WriteString("onFailure", manifest.FailurePolicy);
                writer.WriteNumber("itemCount", manifest.Items.Count);
            });
            return;
        }

        _writer.WriteLine(
            "manifest " + manifest.BatchId +
            " accepted: items=" + Number(manifest.Items.Count) +
            " onFailure=" + manifest.FailurePolicy);
    }

    public void QueueState(string queueState)
    {
        if (_json)
        {
            WriteJson(writer =>
            {
                writer.WriteString("kind", "queueState");
                writer.WriteString("queueState", queueState);
            });
            return;
        }

        _writer.WriteLine("queue " + queueState);
    }

    public void JobLine(JobRecord job)
    {
        if (_json)
        {
            WriteJson(writer => WriteJobBody(writer, job));
            return;
        }

        var line = new StringBuilder()
            .Append(job.JobId).Append(' ')
            .Append(job.SourceEntry).Append(' ')
            .Append(job.JobKind).Append(' ')
            .Append(job.State).Append(' ')
            .Append(Percent(job.LastProgressPermille));
        if (job.BatchId is not null)
        {
            line.Append(" batch=").Append(job.BatchId);
        }

        if (job.FinalDiagnosticCode is not null)
        {
            line.Append(' ').Append(job.FinalDiagnosticCode);
        }

        if (job.ArtifactIds.Count > 0)
        {
            line.Append(" artifacts=").Append(Number(job.ArtifactIds.Count));
        }

        _writer.WriteLine(line.ToString());
    }

    public void CancellationResult(string jobId, JobCancellationResult result)
    {
        if (_json)
        {
            WriteJson(writer =>
            {
                writer.WriteString("kind", "cancellation");
                writer.WriteString("jobId", jobId);
                writer.WriteString("state", result.State);
                writer.WriteBoolean("accepted", result.Accepted);
            });
            return;
        }

        _writer.WriteLine(jobId + " " + result.State + " accepted=" +
            (result.Accepted ? "true" : "false"));
    }

    public void Line(string text) => _writer.WriteLine(text);

    private static void WriteJobBody(Utf8JsonWriter writer, JobRecord job)
    {
        writer.WriteString("kind", "job");
        writer.WriteString("jobId", job.JobId);
        writer.WriteString("sourceEntry", job.SourceEntry);
        writer.WriteString("jobKind", job.JobKind);
        writer.WriteString("state", job.State);
        writer.WriteNumber("progressPermille", job.LastProgressPermille);
        WriteOptionalString(writer, "batchId", job.BatchId);
        WriteOptionalString(writer, "diagnostic", job.FinalDiagnosticCode);
        if (job.IsTerminal)
        {
            writer.WriteString("outcome", job.State);
        }

        writer.WriteNumber("artifactCount", job.ArtifactIds.Count);
    }

    private static void WriteOptionalString(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(propertyName, value);
        }
    }

    private static string Percent(int permille) =>
        (permille / 10).ToString(CultureInfo.InvariantCulture) + "%";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private void WriteJson(Action<Utf8JsonWriter> body)
    {
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            body(writer);
            writer.WriteEndObject();
        }

        _writer.WriteLine(Encoding.UTF8.GetString(buffer.ToArray()));
    }
}
