using System.Text;
using VFXComposer.Mcp;

var decision = McpCommandLine.Parse(args);
switch (decision.Action)
{
    case McpStartupAction.PrintUsage:
        McpCommandLine.WriteUsage(Console.Out);
        return McpExitCodes.Success;
    case McpStartupAction.Refuse:
        Console.Error.WriteLine(decision.DiagnosticCode + " " +
            McpDiagnosticCatalog.Require(decision.DiagnosticCode!));
        McpCommandLine.WriteUsage(Console.Error);
        return McpExitCodes.UsageError;
}

using var interrupt = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    // Ctrl+C ends this stdio session, exactly as the client closing the transport does. This server
    // only enqueues; jobs already in the store are unaffected and run whenever a host that owns the
    // executor drains them, not "under" this process (REQ-002 §12).
    eventArgs.Cancel = true;
    interrupt.Cancel();
};

// The transport is read and written as raw UTF-8 without a byte-order mark, independent of the
// console code page, so the framing is exactly the newline-delimited JSON the client expects.
var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
using var input = new StreamReader(Console.OpenStandardInput(), encoding);
await using var output = new StreamWriter(Console.OpenStandardOutput(), encoding) { AutoFlush = false };
var environment = McpProductionEnvironment.Create(input, output);
return await new McpServer(environment).RunAsync(interrupt.Token).ConfigureAwait(false);
