using System.Collections.Frozen;
using System.Text.Json;

namespace VFXComposer.Mcp;

/// <summary>The JSON-RPC method names of the implemented protocol subset.</summary>
public static class McpMethods
{
    public const string Initialize = "initialize";
    public const string Initialized = "notifications/initialized";
    public const string ToolsList = "tools/list";
    public const string ToolsCall = "tools/call";
}

/// <summary>Process exit codes of the MCP host.</summary>
public static class McpExitCodes
{
    /// <summary>The client closed the transport; the session ended normally.</summary>
    public const int Success = 0;

    /// <summary>The command line did not ask for the one transport this server serves.</summary>
    public const int UsageError = 64;

    /// <summary>The transport could no longer be trusted and the session was closed.</summary>
    public const int TransportFault = 69;
}

/// <summary>
/// The MCP server: a hand-written JSON-RPC 2.0 dispatcher over the stdio transport.
///
/// <para>Framing is the stdio transport's newline-delimited JSON, one message per line, bounded
/// (see <see cref="McpFrameReader"/>). The implemented subset is the initialize handshake, the
/// initialized notification, <c>tools/list</c> and <c>tools/call</c>; resources, prompts, sampling,
/// logging and completion are not implemented and any other method is answered with the standard
/// method-not-found error. No listener is ever created and no environment variable is trusted, so
/// this surface adds no network face (REQ-002-09).</para>
///
/// <para>Failure discipline: a malformed frame, a non-request envelope, an unknown envelope member,
/// a call before the handshake, an unknown tool and an argument that is missing, mistyped, unbounded
/// or not part of a tool's field set are all answered with a JSON-RPC error and never partially
/// interpreted. A tool that ran and refused reports the refusal as a tool result with an error flag,
/// which is the split the tool contract asks for.</para>
/// </summary>
public sealed class McpServer
{
    /// <summary>
    /// The single protocol revision this server declares. The client's requested revision is
    /// accepted as informational and never changes behaviour: there is one wire shape here.
    /// </summary>
    public const string ProtocolVersion = "2025-06-18";

    public const string ServerName = "vfxcomposer-batch";

    public const string ServerVersion = "1.0.0";

    private const int MaximumProtocolVersionLength = 32;

    private const string Instructions =
        "Closed tool set over the local batch execution layer. Submitting is always detached: a tool " +
        "enqueues work and the queue executor runs it strictly one at a time. There is no authority, " +
        "approval or skip-validation argument, and every result is limited to identifiers, closed " +
        "vocabulary words, stable codes and counters.";

    private static readonly FrozenSet<string> InitializeParameters =
        new[] { "protocolVersion", "capabilities", "clientInfo" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ToolsListParameters =
        new[] { "cursor" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ToolsCallParameters =
        new[] { "name", "arguments" }.ToFrozenSet(StringComparer.Ordinal);

    private readonly McpFrameReader _reader;
    private readonly McpFrameWriter _writer;
    private readonly McpToolInvoker _tools;
    private bool _initialized;

    public McpServer(McpEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _reader = new McpFrameReader(environment.Input, environment.MaximumFrameCharacters);
        _writer = new McpFrameWriter(environment.Output);
        _tools = new McpToolInvoker(environment);
    }

    public override string ToString() => "McpServer(" + ProtocolVersion + ")";

    /// <summary>
    /// Serves the session until the client closes the transport. An oversized frame ends the
    /// session with a transport fault because a bounded reader cannot resynchronise a stream whose
    /// current line it had to abandon.
    /// </summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var frame = _reader.Read();
            if (frame.Status == McpFrameStatus.EndOfStream)
            {
                return McpExitCodes.Success;
            }

            if (frame.Status == McpFrameStatus.Oversized)
            {
                _writer.Write(JsonRpcResponseWriter.Error(
                    JsonRpcId.Absent,
                    JsonRpcErrorCodes.ParseError,
                    McpDiagnosticCodes.FrameTooLarge));
                return McpExitCodes.TransportFault;
            }

            await HandleFrameAsync(frame.Text).ConfigureAwait(false);
        }

        return McpExitCodes.Success;
    }

    private async Task HandleFrameAsync(string frame)
    {
        var parsed = JsonRpcReader.Parse(frame);
        if (parsed.Rejection is JsonRpcRejection rejection)
        {
            // A notification cannot be answered, but a frame this broken may not even have a
            // recoverable id; the JSON-RPC parse-error rules put a null id on the wire in that
            // case, which is what an absent id serialises to.
            _writer.Write(JsonRpcResponseWriter.Error(
                rejection.Id,
                rejection.ErrorCode,
                rejection.DiagnosticCode));
            return;
        }

        using var request = parsed.Request!;
        switch (request.Method)
        {
            case McpMethods.Initialize:
                Respond(request, Initialize(request));
                return;
            case McpMethods.Initialized:
                // A notification is never answered. The handshake is already complete once
                // initialize succeeded, so this only acknowledges the client's own sequencing.
                return;
            case McpMethods.ToolsList:
                Respond(request, ListTools(request));
                return;
            case McpMethods.ToolsCall:
                Respond(request, await CallToolAsync(request).ConfigureAwait(false));
                return;
            default:
                Respond(request, Rejected(
                    JsonRpcErrorCodes.MethodNotFound,
                    McpDiagnosticCodes.MethodNotFound));
                return;
        }
    }

    private McpDispatchResult Initialize(JsonRpcRequest request)
    {
        if (_initialized)
        {
            return Rejected(JsonRpcErrorCodes.InvalidRequest, McpDiagnosticCodes.AlreadyInitialized);
        }

        // The member set is closed at the declared protocol revision, like every other surface
        // here: the capability and client-info structures may carry anything the revision allows,
        // but a member the revision does not define refuses the handshake rather than being
        // ignored. A later revision is a version bump, not a silently tolerated extra member.
        if (!AcceptsOnly(request.Parameters, InitializeParameters))
        {
            return Rejected(JsonRpcErrorCodes.InvalidParams, McpDiagnosticCodes.InvalidRequest);
        }

        // protocolVersion is a required member of the handshake, not an optional one: a params
        // object that omits it, or carries it in the wrong shape, refuses the handshake rather than
        // negotiating against an absent version (F5 audit ⑧).
        if (request.Parameters is not JsonElement parameters ||
            !parameters.TryGetProperty("protocolVersion", out var requested) ||
            requested.ValueKind != JsonValueKind.String ||
            (requested.GetString() ?? string.Empty).Length is 0 or > MaximumProtocolVersionLength)
        {
            return Rejected(JsonRpcErrorCodes.InvalidParams, McpDiagnosticCodes.InvalidRequest);
        }

        _initialized = true;
        return McpDispatchResult.Result(writer =>
        {
            writer.WriteString("protocolVersion", ProtocolVersion);
            writer.WriteStartObject("capabilities");
            writer.WriteStartObject("tools");
            writer.WriteBoolean("listChanged", false);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteStartObject("serverInfo");
            writer.WriteString("name", ServerName);
            writer.WriteString("version", ServerVersion);
            writer.WriteEndObject();
            writer.WriteString("instructions", Instructions);
        });
    }

    private McpDispatchResult ListTools(JsonRpcRequest request)
    {
        if (!_initialized)
        {
            return Rejected(JsonRpcErrorCodes.NotInitialized, McpDiagnosticCodes.NotInitialized);
        }

        if (!AcceptsOnly(request.Parameters, ToolsListParameters))
        {
            return Rejected(JsonRpcErrorCodes.InvalidParams, McpDiagnosticCodes.InvalidRequest);
        }

        return McpDispatchResult.Result(writer =>
        {
            writer.WriteStartArray("tools");
            foreach (var tool in McpToolCatalog.All)
            {
                writer.WriteStartObject();
                writer.WriteString("name", tool.Name);
                writer.WriteString("description", tool.Description);
                writer.WritePropertyName("inputSchema");
                writer.WriteRawValue(tool.InputSchemaJson);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        });
    }

    private async Task<McpDispatchResult> CallToolAsync(JsonRpcRequest request)
    {
        if (!_initialized)
        {
            return Rejected(JsonRpcErrorCodes.NotInitialized, McpDiagnosticCodes.NotInitialized);
        }

        if (request.Parameters is not JsonElement parameters ||
            !AcceptsOnly(parameters, ToolsCallParameters) ||
            !parameters.TryGetProperty("name", out var nameElement) ||
            nameElement.ValueKind != JsonValueKind.String)
        {
            return Rejected(JsonRpcErrorCodes.InvalidParams, McpDiagnosticCodes.InvalidRequest);
        }

        var name = nameElement.GetString() ?? string.Empty;
        if (McpToolCatalog.Find(name) is null)
        {
            return Rejected(JsonRpcErrorCodes.InvalidParams, McpDiagnosticCodes.UnknownTool);
        }

        JsonElement? arguments = null;
        if (parameters.TryGetProperty("arguments", out var argumentsElement) &&
            argumentsElement.ValueKind != JsonValueKind.Null)
        {
            if (argumentsElement.ValueKind != JsonValueKind.Object)
            {
                return Rejected(JsonRpcErrorCodes.InvalidParams, McpDiagnosticCodes.InvalidToolArguments);
            }

            arguments = argumentsElement;
        }

        var response = await _tools.InvokeAsync(name, arguments).ConfigureAwait(false);
        if (response.ProtocolRejection is string diagnosticCode)
        {
            return Rejected(JsonRpcErrorCodes.InvalidParams, diagnosticCode);
        }

        var payload = response.Payload!;
        return McpDispatchResult.Result(writer =>
        {
            writer.WriteStartArray("content");
            writer.WriteStartObject();
            writer.WriteString("type", "text");
            writer.WriteString("text", payload);
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteBoolean("isError", response.IsError);
        });
    }

    /// <summary>Writes the response for a request, and nothing at all for a notification.</summary>
    private void Respond(JsonRpcRequest request, McpDispatchResult result)
    {
        if (request.IsNotification)
        {
            return;
        }

        _writer.Write(result.Body is null
            ? JsonRpcResponseWriter.Error(request.Id, result.ErrorCode, result.DiagnosticCode!)
            : JsonRpcResponseWriter.Result(request.Id, result.Body));
    }

    private static McpDispatchResult Rejected(int errorCode, string diagnosticCode) =>
        McpDispatchResult.Error(errorCode, diagnosticCode);

    /// <summary>True when every member of an optional params object is one the method knows.</summary>
    private static bool AcceptsOnly(JsonElement? parameters, IReadOnlySet<string> known)
    {
        if (parameters is not JsonElement element)
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!known.Contains(property.Name))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Either a result body or an error pair; exactly one is set.</summary>
    private sealed record McpDispatchResult(Action<Utf8JsonWriter>? Body, int ErrorCode, string? DiagnosticCode)
    {
        public static McpDispatchResult Result(Action<Utf8JsonWriter> body) => new(body, 0, null);

        public static McpDispatchResult Error(int errorCode, string diagnosticCode) =>
            new(null, errorCode, diagnosticCode);
    }
}
