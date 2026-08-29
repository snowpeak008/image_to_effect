namespace VFXComposer.Mcp;

/// <summary>What the command line asked the host to do.</summary>
public enum McpStartupAction
{
    /// <summary>Serve one MCP session over the process standard streams.</summary>
    ServeStdio,

    /// <summary>Print the usage text and exit successfully.</summary>
    PrintUsage,

    /// <summary>Refuse to start: the command line did not ask for the one supported transport.</summary>
    Refuse,
}

/// <summary>The startup decision, plus the stable code a refusal is reported under.</summary>
public sealed record McpStartupDecision(McpStartupAction Action, string? DiagnosticCode);

/// <summary>
/// The host's command line. The only serving form is <c>--stdio</c>: a bare launch, an extra
/// argument or an unknown switch refuses to serve and exits non-zero, so this server can never be
/// started into an unintended transport or mode by accident (REQ-002 §7.1).
/// </summary>
public static class McpCommandLine
{
    public const string StdioSwitch = "--stdio";

    public static McpStartupDecision Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count == 1 && IsHelp(arguments[0]))
        {
            return new McpStartupDecision(McpStartupAction.PrintUsage, null);
        }

        if (arguments.Count == 1 && string.Equals(arguments[0], StdioSwitch, StringComparison.Ordinal))
        {
            return new McpStartupDecision(McpStartupAction.ServeStdio, null);
        }

        return new McpStartupDecision(McpStartupAction.Refuse, McpDiagnosticCodes.CommandLineRejected);
    }

    public static void WriteUsage(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteLine("vfxc-mcp - VFX Composer MCP server (stdio transport only)");
        writer.WriteLine();
        writer.WriteLine("Usage:");
        writer.WriteLine("  vfxc-mcp --stdio     Serve one MCP session over standard input and output.");
        writer.WriteLine("  vfxc-mcp --help      Print this text.");
        writer.WriteLine();
        writer.WriteLine("Protocol: JSON-RPC 2.0 over newline-delimited JSON; MCP revision " +
            McpServer.ProtocolVersion + ".");
        writer.WriteLine("Methods: initialize, notifications/initialized, tools/list, tools/call.");
        writer.WriteLine("Tools (closed set):");
        foreach (var tool in McpToolCatalog.All)
        {
            writer.WriteLine("  " + tool.Name);
        }

        writer.WriteLine();
        writer.WriteLine("Exit codes: 0 session ended, 64 usage, 69 transport fault.");
    }

    private static bool IsHelp(string token) =>
        string.Equals(token, "--help", StringComparison.Ordinal) ||
        string.Equals(token, "-h", StringComparison.Ordinal) ||
        string.Equals(token, "help", StringComparison.Ordinal);
}
