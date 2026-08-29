using System.Buffers;
using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace VFXComposer.Mcp;

/// <summary>
/// The JSON-RPC 2.0 error codes this server emits. The four standard codes are the specified
/// values; <see cref="NotInitialized"/> uses the value the JSON-RPC implementation-defined range
/// has settled on for "server not initialized", so a client that knows the convention recognises
/// the condition without reading the message.
/// </summary>
public static class JsonRpcErrorCodes
{
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
    public const int NotInitialized = -32002;
}

/// <summary>
/// A JSON-RPC request id: a bounded string, an integer, or absent. Absent means the message is a
/// notification, which per JSON-RPC 2.0 must never be answered.
/// </summary>
public readonly record struct JsonRpcId
{
    public const int MaximumTextLength = 128;

    private JsonRpcId(string? text, long? number)
    {
        Text = text;
        Number = number;
    }

    public static JsonRpcId Absent => default;

    public string? Text { get; }

    public long? Number { get; }

    public bool IsAbsent => Text is null && Number is null;

    public static JsonRpcId FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length is 0 or > MaximumTextLength)
        {
            throw new ArgumentOutOfRangeException(nameof(text));
        }

        return new JsonRpcId(text, null);
    }

    public static JsonRpcId FromNumber(long number) => new(null, number);

    public override string ToString() => Text
        ?? Number?.ToString(CultureInfo.InvariantCulture)
        ?? "<absent>";

    internal void Write(Utf8JsonWriter writer, string propertyName)
    {
        if (Text is string text)
        {
            writer.WriteString(propertyName, text);
        }
        else if (Number is long number)
        {
            writer.WriteNumber(propertyName, number);
        }
        else
        {
            writer.WriteNull(propertyName);
        }
    }
}

/// <summary>
/// One parsed JSON-RPC request. It owns the backing document, so <see cref="Parameters"/> is only
/// valid until the request is disposed.
/// </summary>
public sealed class JsonRpcRequest : IDisposable
{
    private readonly JsonDocument _document;

    internal JsonRpcRequest(JsonDocument document, JsonRpcId id, string method, JsonElement? parameters)
    {
        _document = document;
        Id = id;
        Method = method;
        Parameters = parameters;
    }

    public JsonRpcId Id { get; }

    public string Method { get; }

    public JsonElement? Parameters { get; }

    /// <summary>True when the message carries no id and therefore must not be answered.</summary>
    public bool IsNotification => Id.IsAbsent;

    public override string ToString() => "JsonRpcRequest(" + Method + ")";

    public void Dispose() => _document.Dispose();
}

/// <summary>A refused frame: the id to answer under, the wire code and the stable MCP code.</summary>
public sealed record JsonRpcRejection(JsonRpcId Id, int ErrorCode, string DiagnosticCode);

/// <summary>Exactly one of the two members is set.</summary>
public sealed record JsonRpcParseResult(JsonRpcRequest? Request, JsonRpcRejection? Rejection);

/// <summary>
/// Hand-written JSON-RPC 2.0 request reader for the closed method surface. The envelope member
/// set is closed, every scalar is bounded, and anything that is not a well-formed request for
/// this server is refused rather than partially interpreted. Responses are never accepted: this
/// server issues no outbound requests, so a <c>result</c> or <c>error</c> member is an unknown
/// envelope member and fails the same way any other unknown member does.
/// </summary>
public static class JsonRpcReader
{
    public const int MaximumMethodLength = 64;

    private static readonly FrozenSet<string> EnvelopeMembers =
        new[] { "jsonrpc", "id", "method", "params" }.ToFrozenSet(StringComparer.Ordinal);

    public static JsonRpcParseResult Parse(string frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(
                frame,
                new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
        }
        catch (JsonException)
        {
            return Reject(JsonRpcId.Absent, JsonRpcErrorCodes.ParseError, McpDiagnosticCodes.MalformedFrame);
        }

        var keep = false;
        try
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Reject(JsonRpcId.Absent, JsonRpcErrorCodes.InvalidRequest, McpDiagnosticCodes.InvalidRequest);
            }

            if (!TryReadId(root, out var id))
            {
                return Reject(JsonRpcId.Absent, JsonRpcErrorCodes.InvalidRequest, McpDiagnosticCodes.InvalidRequest);
            }

            foreach (var property in root.EnumerateObject())
            {
                if (!EnvelopeMembers.Contains(property.Name))
                {
                    return Reject(id, JsonRpcErrorCodes.InvalidRequest, McpDiagnosticCodes.InvalidRequest);
                }
            }

            if (!root.TryGetProperty("jsonrpc", out var version) ||
                version.ValueKind != JsonValueKind.String ||
                !string.Equals(version.GetString(), "2.0", StringComparison.Ordinal))
            {
                return Reject(id, JsonRpcErrorCodes.InvalidRequest, McpDiagnosticCodes.InvalidRequest);
            }

            if (!root.TryGetProperty("method", out var methodElement) ||
                methodElement.ValueKind != JsonValueKind.String)
            {
                return Reject(id, JsonRpcErrorCodes.InvalidRequest, McpDiagnosticCodes.InvalidRequest);
            }

            var method = methodElement.GetString() ?? string.Empty;
            if (method.Length is 0 or > MaximumMethodLength || HasControl(method))
            {
                return Reject(id, JsonRpcErrorCodes.InvalidRequest, McpDiagnosticCodes.InvalidRequest);
            }

            JsonElement? parameters = null;
            if (root.TryGetProperty("params", out var paramsElement) &&
                paramsElement.ValueKind != JsonValueKind.Null)
            {
                if (paramsElement.ValueKind != JsonValueKind.Object)
                {
                    // By-position parameters exist in JSON-RPC but no MCP method uses them, so an
                    // array is refused instead of being reinterpreted.
                    return Reject(id, JsonRpcErrorCodes.InvalidParams, McpDiagnosticCodes.InvalidRequest);
                }

                parameters = paramsElement;
            }

            keep = true;
            return new JsonRpcParseResult(new JsonRpcRequest(document, id, method, parameters), Rejection: null);
        }
        finally
        {
            if (!keep)
            {
                document.Dispose();
            }
        }
    }

    /// <summary>
    /// Reads the id. An explicit JSON null is treated as absent, matching the JSON-RPC 2.0
    /// guidance that a null id must not be used to correlate a response.
    /// </summary>
    private static bool TryReadId(JsonElement root, out JsonRpcId id)
    {
        id = JsonRpcId.Absent;
        if (!root.TryGetProperty("id", out var element))
        {
            return true;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
                return true;
            case JsonValueKind.String:
                var text = element.GetString() ?? string.Empty;
                if (text.Length is 0 or > JsonRpcId.MaximumTextLength || HasControl(text))
                {
                    return false;
                }

                id = JsonRpcId.FromText(text);
                return true;
            case JsonValueKind.Number:
                if (!element.TryGetInt64(out var number))
                {
                    return false;
                }

                id = JsonRpcId.FromNumber(number);
                return true;
            default:
                return false;
        }
    }

    private static JsonRpcParseResult Reject(JsonRpcId id, int errorCode, string diagnosticCode) =>
        new(Request: null, new JsonRpcRejection(id, errorCode, diagnosticCode));

    private static bool HasControl(string value)
    {
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Serialises JSON-RPC 2.0 responses. Every document is written without insignificant whitespace
/// and with the default escaping, so a response can never contain a raw newline and therefore can
/// never break the newline-delimited framing.
/// </summary>
public static class JsonRpcResponseWriter
{
    public static string Result(JsonRpcId id, Action<Utf8JsonWriter> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return Write(writer =>
        {
            writer.WriteString("jsonrpc", "2.0");
            id.Write(writer, "id");
            writer.WriteStartObject("result");
            body(writer);
            writer.WriteEndObject();
        });
    }

    public static string Error(JsonRpcId id, int errorCode, string diagnosticCode)
    {
        var message = McpDiagnosticCatalog.Require(diagnosticCode);
        return Write(writer =>
        {
            writer.WriteString("jsonrpc", "2.0");
            id.Write(writer, "id");
            writer.WriteStartObject("error");
            writer.WriteNumber("code", errorCode);
            writer.WriteString("message", message);
            writer.WriteStartObject("data");
            writer.WriteString("diagnostic", diagnosticCode);
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    private static string Write(Action<Utf8JsonWriter> body)
    {
        var buffer = new ArrayBufferWriter<byte>(512);
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            body(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
