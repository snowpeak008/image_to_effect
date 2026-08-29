using System.Text;
using System.Text.Json;
using VFXComposer.Batch.Core;
using VFXComposer.Jobs;
using VFXComposer.Mcp;

namespace VFXComposer.Mcp.Tests;

/// <summary>
/// Session fixtures for the MCP entry surface: a temporary queue store, a capability profile and
/// an in-memory transport. No test opens a socket, a provider or a Unity project, and the server
/// under test never hosts an executor, so a submission stays queued exactly as it does in
/// production.
/// </summary>
internal sealed class McpFixture : IDisposable
{
    public McpFixture()
    {
        WorkspaceDirectory = Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(),
            "vfxc-mcp-tests",
            Guid.NewGuid().ToString("N"))).FullName;
        Store = new JobStore(Path.Combine(WorkspaceDirectory, "store"));
    }

    public string WorkspaceDirectory { get; }

    public JobStore Store { get; }

    public BatchCapabilityProfile Capability { get; set; } = BatchCapabilityProfile.GenerationOnly;

    /// <summary>Replaces the queue client, for the store-fault paths.</summary>
    public IJobQueueClient? QueueClientOverride { get; set; }

    /// <summary>Makes opening the generation runtime a test failure, for the zero-network paths.</summary>
    public bool ForbidGenerationRuntime { get; set; }

    public int MaximumFrameCharacters { get; set; } = McpFrameReader.DefaultMaximumFrameCharacters;

    public DateTimeOffset Now { get; set; } = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Runs one session over the given frames and returns every response frame. Each session is a
    /// fresh handshake state over the same durable store, which is how a later exchange can build
    /// on identifiers an earlier one returned.
    /// </summary>
    public McpSession Run(params string[] frames)
    {
        var output = new StringWriter();
        var environment = new McpEnvironment
        {
            Input = new StringReader(string.Concat(frames.Select(static frame => frame + "\n"))),
            Output = output,
            OpenQueue = OpenQueue,
            OpenGenerationRuntime = OpenGenerationRuntime,
            UtcNow = () => Now,
            MaximumFrameCharacters = MaximumFrameCharacters,
        };
        var exitCode = new McpServer(environment).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        return new McpSession(exitCode, output.ToString());
    }

    /// <summary>Runs one session that completes the handshake before the given frames.</summary>
    public McpSession RunInitialized(params string[] frames) =>
        Run([McpFrames.Initialize(1), McpFrames.Initialized(), .. frames]);

    public void Dispose()
    {
        try
        {
            Directory.Delete(WorkspaceDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Temporary fixture cleanup is best effort.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above.
        }
    }

    private IMcpQueueSession OpenQueue() =>
        new TestQueueSession(QueueClientOverride ?? Store);

    private IMcpGenerationRuntime OpenGenerationRuntime() => ForbidGenerationRuntime
        ? throw new InvalidOperationException("This tool must not open the generation runtime.")
        : new TestGenerationRuntime(Capability);
}

/// <summary>The response frames of one session plus the host exit code.</summary>
internal sealed class McpSession : IDisposable
{
    private readonly List<JsonDocument> _documents = [];

    public McpSession(int exitCode, string rawOutput)
    {
        ExitCode = exitCode;
        RawOutput = rawOutput;
        Lines = rawOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in Lines)
        {
            _documents.Add(JsonDocument.Parse(line));
        }
    }

    public int ExitCode { get; }

    public string RawOutput { get; }

    public IReadOnlyList<string> Lines { get; }

    public int Count => _documents.Count;

    public JsonElement Message(int index) => _documents[index].RootElement;

    /// <summary>The response correlated to one request id.</summary>
    public JsonElement Response(long id) => _documents
        .Select(static document => document.RootElement)
        .Single(element => element.TryGetProperty("id", out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.GetInt64() == id);

    /// <summary>The parsed payload of a successful <c>tools/call</c> response.</summary>
    public JsonDocument ToolPayload(long id)
    {
        var result = Response(id).GetProperty("result");
        var text = result.GetProperty("content")[0].GetProperty("text").GetString()!;
        return JsonDocument.Parse(text);
    }

    public bool ToolIsError(long id) =>
        Response(id).GetProperty("result").GetProperty("isError").GetBoolean();

    public int ErrorCode(long id) =>
        Response(id).GetProperty("error").GetProperty("code").GetInt32();

    public string ErrorDiagnostic(long id) =>
        Response(id).GetProperty("error").GetProperty("data").GetProperty("diagnostic").GetString()!;

    public void Dispose()
    {
        foreach (var document in _documents)
        {
            document.Dispose();
        }
    }
}

/// <summary>Builders for the request frames a client would send.</summary>
internal static class McpFrames
{
    public static string Initialize(long id, string protocolVersion = McpServer.ProtocolVersion) =>
        Request(id, McpMethods.Initialize,
            "{\"protocolVersion\":" + Quote(protocolVersion) +
            ",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1\"}}");

    public static string Initialized() =>
        "{\"jsonrpc\":\"2.0\",\"method\":\"" + McpMethods.Initialized + "\"}";

    public static string ToolsList(long id) => Request(id, McpMethods.ToolsList, parameters: null);

    public static string ToolCall(long id, string name, string argumentsJson) =>
        Request(id, McpMethods.ToolsCall,
            "{\"name\":" + Quote(name) + ",\"arguments\":" + argumentsJson + "}");

    public static string ToolCallWithoutArguments(long id, string name) =>
        Request(id, McpMethods.ToolsCall, "{\"name\":" + Quote(name) + "}");

    public static string Request(long id, string method, string? parameters) =>
        "{\"jsonrpc\":\"2.0\",\"id\":" + id.ToString(System.Globalization.CultureInfo.InvariantCulture) +
        ",\"method\":" + Quote(method) +
        (parameters is null ? string.Empty : ",\"params\":" + parameters) + "}";

    /// <summary>Quotes a value as a JSON string, so a manifest can travel as a tool argument.</summary>
    public static string Quote(string value) => JsonSerializer.Serialize(value);
}

/// <summary>Manifest documents shared by the tool tests.</summary>
internal static class McpManifests
{
    /// <summary>A three-entry prompt manifest, matching the CLI fixture entry for entry.</summary>
    public static string ThreePrompts(string batchId = "fire-pack", string onFailure = "continue") =>
        """
        {
          "schemaVersion": "vfxcomposer.batch-manifest/1",
          "batchId": "__BATCH__",
          "onFailure": "__POLICY__",
          "defaults": { "targetProfile": "mobile_medium" },
          "items": [
            { "itemId": "alpha", "kind": "prompt", "prompt": "a calm blue spark", "constraints": { "element": "water" } },
            { "itemId": "beta", "kind": "prompt", "prompt": "POISON a broken effect" },
            { "itemId": "gamma", "kind": "prompt", "prompt": "a slow ember trail" }
          ]
        }
        """
            .Replace("__BATCH__", batchId, StringComparison.Ordinal)
            .Replace("__POLICY__", onFailure, StringComparison.Ordinal);

    public static string EscapingRecipePath() =>
        """
        {
          "schemaVersion": "vfxcomposer.batch-manifest/1",
          "batchId": "escape-test",
          "items": [ { "itemId": "a", "kind": "recipe", "recipePath": "..\\..\\ProjectSettings\\x.json" } ]
        }
        """;

    public static string UnknownField() =>
        """
        {
          "schemaVersion": "vfxcomposer.batch-manifest/1",
          "batchId": "unknown-field",
          "items": [ { "itemId": "a", "kind": "prompt", "prompt": "text", "authority": "elevated" } ]
        }
        """;
}

/// <summary>Queue session over an arbitrary client; never hosts an executor.</summary>
internal sealed class TestQueueSession : IMcpQueueSession
{
    public TestQueueSession(IJobQueueClient client)
    {
        Client = client;
    }

    public IJobQueueClient Client { get; }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>Capability-only generation runtime seam.</summary>
internal sealed class TestGenerationRuntime : IMcpGenerationRuntime
{
    public TestGenerationRuntime(BatchCapabilityProfile capability)
    {
        Capability = capability;
    }

    public BatchCapabilityProfile Capability { get; }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>Client whose every operation fails with the stable store-unavailable code.</summary>
internal sealed class UnavailableQueueClient : IJobQueueClient
{
    public JobQueueSnapshotView ReadSnapshot() =>
        throw new JobQueueException(JobQueueDiagnosticCodes.StoreUnavailable);

    public IReadOnlyList<JobStoreEvent> ReadEvents(string jobId) =>
        throw new JobQueueException(JobQueueDiagnosticCodes.StoreUnavailable);

    public JobRecord Enqueue(JobEnqueueRequest request) =>
        throw new JobQueueException(JobQueueDiagnosticCodes.StoreUnavailable);

    public JobCancellationResult RequestCancel(string jobId) =>
        throw new JobQueueException(JobQueueDiagnosticCodes.StoreUnavailable);

    public JobRecord Resubmit(string jobId) =>
        throw new JobQueueException(JobQueueDiagnosticCodes.StoreUnavailable);
}
