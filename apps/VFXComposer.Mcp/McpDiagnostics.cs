using System.Collections.Frozen;

namespace VFXComposer.Mcp;

/// <summary>
/// Closed MCP entry-surface diagnostic code set (REQ-002 §7.3 prefix). Transport and protocol
/// faults get a code just like tool-level refusals do, so every refusal a client can observe is
/// named by a stable token rather than by prose. Messages are fixed, single-line and free of
/// paths, prompts and endpoints.
/// </summary>
public static class McpDiagnosticCodes
{
    public const string CommandLineRejected = "VFXMCP001";
    public const string FrameTooLarge = "VFXMCP002";
    public const string MalformedFrame = "VFXMCP003";
    public const string InvalidRequest = "VFXMCP004";
    public const string MethodNotFound = "VFXMCP005";
    public const string NotInitialized = "VFXMCP006";
    public const string AlreadyInitialized = "VFXMCP007";
    public const string UnknownTool = "VFXMCP008";
    public const string InvalidToolArguments = "VFXMCP009";
    public const string ManifestRejected = "VFXMCP010";
    public const string QueueUnavailable = "VFXMCP011";
    public const string NotFound = "VFXMCP012";

    public static IReadOnlySet<string> All => McpDiagnosticCatalog.Codes;
}

/// <summary>One immutable definition per MCP entry-surface diagnostic code.</summary>
public sealed record McpDiagnosticDefinition(string Code, string Message);

/// <summary>Closed catalog resolving MCP codes to their fixed messages.</summary>
public static class McpDiagnosticCatalog
{
    private static readonly FrozenDictionary<string, McpDiagnosticDefinition> Definitions =
        new[]
        {
            new McpDiagnosticDefinition(
                McpDiagnosticCodes.CommandLineRejected,
                "This server only serves the stdio transport and must be started with --stdio and no other argument."),
            new McpDiagnosticDefinition(
                McpDiagnosticCodes.FrameTooLarge,
                "A message exceeded the maximum frame size; the session was closed because the stream cannot be resynchronised."),
            new McpDiagnosticDefinition(
                McpDiagnosticCodes.MalformedFrame,
                "The message is not a well-formed JSON document."),
            new McpDiagnosticDefinition(
                McpDiagnosticCodes.InvalidRequest,
                "The message is not a well-formed JSON-RPC 2.0 request."),
            new McpDiagnosticDefinition(
                McpDiagnosticCodes.MethodNotFound,
                "The method is not part of the implemented protocol subset."),
            new McpDiagnosticDefinition(
                McpDiagnosticCodes.NotInitialized,
                "The session has not completed the initialize handshake."),
            new McpDiagnosticDefinition(
                McpDiagnosticCodes.AlreadyInitialized,
                "The session has already completed the initialize handshake."),
            new McpDiagnosticDefinition(
                McpDiagnosticCodes.UnknownTool,
                "The tool is not part of the closed tool set."),
            new McpDiagnosticDefinition(
                McpDiagnosticCodes.InvalidToolArguments,
                "The tool arguments are missing, unbounded, of the wrong type or contain a field the tool does not accept."),
            new McpDiagnosticDefinition(
                McpDiagnosticCodes.ManifestRejected,
                "The manifest was rejected; no entry was enqueued."),
            new McpDiagnosticDefinition(
                McpDiagnosticCodes.QueueUnavailable,
                "The job queue store is unavailable."),
            new McpDiagnosticDefinition(
                McpDiagnosticCodes.NotFound,
                "No queue entry matches the requested identifier."),
        }.ToFrozenDictionary(definition => definition.Code, StringComparer.Ordinal);

    internal static FrozenSet<string> Codes { get; } = Definitions.Keys.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, McpDiagnosticDefinition> All => Definitions;

    public static string Require(string code) =>
        Definitions.TryGetValue(code, out var definition)
            ? definition.Message
            : throw new ArgumentOutOfRangeException(nameof(code));
}
