using System.Runtime.CompilerServices;

// The MCP test project owns the two-surface equivalence and zero-network cases, so it needs the
// CLI production composition root as well as its own.
[assembly: InternalsVisibleTo("VFXComposer.Cli.Tests")]
[assembly: InternalsVisibleTo("VFXComposer.Mcp.Tests")]
