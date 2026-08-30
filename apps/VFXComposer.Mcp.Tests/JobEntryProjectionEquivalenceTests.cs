using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Batch.Core;
using VFXComposer.Cli;
using VFXComposer.Jobs;
using VFXComposer.Mcp;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Mcp.Tests;

/// <summary>
/// Constructive equivalence of the single-entry queue projection across the two executable surfaces.
/// A hand-maintained list of expected fields is exactly how <see cref="JobRecord.ItemId"/> slipped
/// past both surfaces unnoticed (F5 recommendation ②), so this drives the classification straight
/// off JobRecord's persisted members: every one is either surfaced on the entry body or explicitly
/// withheld with a reason, and the CLI and MCP bodies must carry the identical field set (bar the
/// CLI-only <c>kind</c> envelope tag that frames the object on its NDJSON stream).
/// </summary>
[TestClass]
public sealed class JobEntryProjectionEquivalenceTests
{
    /// <summary>JobRecord members the compact entry body intentionally does not carry, each with why.</summary>
    private static readonly IReadOnlyDictionary<string, string> Withheld = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["requestId"] = "correlation identity, not operator-facing",
        ["idempotencyKey"] = "submission identity, not operator-facing",
        ["entryIdempotencyKey"] = "content key, not operator-facing",
        ["batchPolicy"] = "queue bookkeeping; the manifest word rides the batch report, not the entry",
        ["payload"] = "opaque executor input; never leaves the store",
        ["queuePosition"] = "volatile queue bookkeeping",
        ["enqueuedAtUtc"] = "timestamp; absent from the compact body",
        ["startedAtUtc"] = "timestamp; absent from the compact body",
        ["completedAtUtc"] = "timestamp; absent from the compact body",
        ["lastEventSequence"] = "internal event cursor",
        ["childProcessId"] = "runtime execution marker",
        ["childProcessStartUtc"] = "runtime execution marker",
    };

    /// <summary>JobRecord persisted name → the entry-body key that carries it on both surfaces.</summary>
    private static readonly IReadOnlyDictionary<string, string> Surfaced = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["jobId"] = "jobId",
        ["sourceEntry"] = "sourceEntry",
        ["jobKind"] = "jobKind",
        ["state"] = "state",
        ["cancelRequested"] = "cancelRequested",
        ["batchId"] = "batchId",
        ["itemId"] = "itemId",
        ["lastProgressPermille"] = "progressPermille",
        ["finalDiagnosticCode"] = "diagnostic",
        ["artifactIds"] = "artifactIds",
    };

    [TestMethod]
    public void EveryPersistedJobRecordMemberIsEitherSurfacedOrWithheldWithAReason()
    {
        // The guard that would have caught ItemId: a new JobRecord member lands in neither map and
        // this fails until someone decides whether it belongs on the operator-facing entry.
        CollectionAssert.AreEquivalent(
            PersistedJobRecordNames().ToArray(),
            Surfaced.Keys.Concat(Withheld.Keys).ToArray(),
            "A JobRecord member is neither surfaced on the entry body nor withheld with a reason.");
    }

    [TestMethod]
    public void TheCliAndMcpEntryBodiesCarryTheIdenticalFieldSet()
    {
        var job = FullyPopulatedBatchEntry();

        var cli = Keys(RenderCliDetail(job));
        var mcp = Keys(RenderMcpDetail(job));
        cli.Remove("kind");

        CollectionAssert.AreEquivalent(
            mcp.ToArray(),
            cli.ToArray(),
            "CLI body {" + string.Join(",", cli) + "} vs MCP body {" + string.Join(",", mcp) + "}");

        foreach (var projectionKey in Surfaced.Values)
        {
            Assert.IsTrue(mcp.Contains(projectionKey), "Both bodies must surface " + projectionKey + ".");
        }

        foreach (var withheldName in Withheld.Keys)
        {
            Assert.IsFalse(cli.Contains(withheldName), withheldName + " must not leak onto the entry body.");
            Assert.IsFalse(mcp.Contains(withheldName), withheldName + " must not leak onto the entry body.");
        }
    }

    [TestMethod]
    public void TheBodyActuallyCarriesTheBatchItemIdItUsedToDrop()
    {
        var job = FullyPopulatedBatchEntry();

        using var cli = JsonDocument.Parse(RenderCliDetail(job));
        using var mcp = JsonDocument.Parse(RenderMcpDetail(job));

        Assert.AreEqual("item-eq", cli.RootElement.GetProperty("itemId").GetString());
        Assert.AreEqual("item-eq", mcp.RootElement.GetProperty("itemId").GetString());
    }

    private static List<string> PersistedJobRecordNames() =>
        typeof(JobRecord)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(static property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
            .Where(static name => name is not null)
            .Select(static name => name!)
            .ToList();

    private static HashSet<string> Keys(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().Select(static property => property.Name).ToHashSet(StringComparer.Ordinal);
    }

    private static string RenderCliDetail(JobRecord job) => Render(writer =>
    {
        CliPresenter.WriteJobBody(writer, job);
        CliPresenter.WriteArtifactIds(writer, job);
    });

    private static string RenderMcpDetail(JobRecord job) =>
        Render(writer => McpToolInvoker.WriteJobBody(writer, job, includeArtifactIds: true));

    private static string Render(Action<Utf8JsonWriter> body)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            body(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>A terminal batch entry with every optional member set, so no surfaced key is skipped.</summary>
    private static JobRecord FullyPopulatedBatchEntry() => new(
        jobId: "job-eq-1",
        requestId: "req-eq-1",
        idempotencyKey: "idem-eq-1",
        entryIdempotencyKey: "entry-eq-1",
        batchId: "batch-eq",
        batchPolicy: JobBatchPolicies.Continue,
        sourceEntry: JobSourceEntries.Cli,
        jobKind: BatchJobKinds.RecipeBuild,
        payload: "opaque-executor-payload",
        queuePosition: 1,
        enqueuedAtUtc: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        startedAtUtc: new DateTimeOffset(2026, 1, 1, 0, 0, 1, TimeSpan.Zero),
        completedAtUtc: new DateTimeOffset(2026, 1, 1, 0, 0, 2, TimeSpan.Zero),
        state: JobStatusStates.Failed,
        cancelRequested: false,
        lastEventSequence: 5,
        lastProgressPermille: 400,
        finalDiagnosticCode: JobQueueDiagnosticCodes.ExecutionFailed,
        artifactIds: new[] { "failure:VFXB0008" },
        childProcessId: null,
        childProcessStartUtc: null,
        itemId: "item-eq");
}
