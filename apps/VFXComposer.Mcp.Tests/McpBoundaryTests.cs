using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.Batch.Core;
using VFXComposer.Cli;
using VFXComposer.Jobs;
using VFXComposer.Mcp;

namespace VFXComposer.Mcp.Tests;

/// <summary>
/// The boundaries the MCP surface shares with the CLI: one execution layer behind both entry
/// points, one redaction rule over every output, zero writes into a Unity project, and a host that
/// refuses to start into anything but the stdio transport.
/// </summary>
[TestClass]
public sealed class McpBoundaryTests
{
    [TestMethod]
    public async Task TheSameManifestProducesEquivalentQueueEntriesThroughBothEntrySurfaces()
    {
        using var mcp = new McpFixture();
        using var cli = new CliBridge();
        var manifest = McpManifests.ThreePrompts();

        using (var session = mcp.RunInitialized(SubmitCall(2, manifest)))
        {
            Assert.IsFalse(session.ToolIsError(2), session.RawOutput);
        }

        var cliExit = await cli.SubmitDetachedAsync(manifest);

        Assert.AreEqual(CliExitCodes.Success, cliExit, cli.Errors);
        var mcpJobs = mcp.Store.ReadSnapshot().Jobs;
        var cliJobs = cli.Store.ReadSnapshot().Jobs;
        Assert.AreEqual(3, mcpJobs.Count);
        Assert.AreEqual(cliJobs.Count, mcpJobs.Count);
        for (var index = 0; index < mcpJobs.Count; index++)
        {
            var fromMcp = mcpJobs[index];
            var fromCli = cliJobs[index];
            Assert.AreEqual(fromCli.JobKind, fromMcp.JobKind);
            Assert.AreEqual(fromCli.Payload, fromMcp.Payload);
            Assert.AreEqual(fromCli.BatchId, fromMcp.BatchId);
            Assert.AreEqual(fromCli.BatchPolicy, fromMcp.BatchPolicy);
            Assert.AreEqual(fromCli.QueuePosition, fromMcp.QueuePosition);
            Assert.AreEqual(fromCli.State, fromMcp.State);
            Assert.AreEqual(
                fromCli.EntryIdempotencyKey,
                fromMcp.EntryIdempotencyKey,
                "The content key is derived from the entry, not from the surface that submitted it.");
            Assert.AreEqual(JobSourceEntries.Cli, fromCli.SourceEntry);
            Assert.AreEqual(
                JobSourceEntries.Mcp,
                fromMcp.SourceEntry,
                "Provenance is the one field that is meant to differ.");
        }
    }

    [TestMethod]
    public async Task BothEntrySurfacesReportTheSameBatchOutcome()
    {
        using var mcp = new McpFixture();
        using var cli = new CliBridge();
        var manifest = McpManifests.ThreePrompts();
        using (var session = mcp.RunInitialized(SubmitCall(2, manifest)))
        {
            Assert.IsFalse(session.ToolIsError(2), session.RawOutput);
        }

        await cli.SubmitDetachedAsync(manifest);
        var cliReport = cli.ReadReport();

        using var report = mcp.RunInitialized(
            McpFrames.ToolCall(2, McpToolNames.GetBatchReport, "{\"batchId\":\"fire-pack\"}"));
        using var payload = report.ToolPayload(2);
        var mcpReport = BatchReportBuilder.Deserialize(payload.RootElement.GetProperty("report").GetRawText());

        Assert.AreEqual(cliReport.SchemaVersion, mcpReport.SchemaVersion);
        Assert.AreEqual(cliReport.BatchId, mcpReport.BatchId);
        Assert.AreEqual(cliReport.OnFailure, mcpReport.OnFailure);
        Assert.AreEqual(cliReport.Summary, mcpReport.Summary);
        CollectionAssert.AreEqual(
            cliReport.Items.Select(static item => item.State).ToArray(),
            mcpReport.Items.Select(static item => item.State).ToArray());
        CollectionAssert.AreEqual(
            cliReport.Items.Select(static item => item.Outcome).ToArray(),
            mcpReport.Items.Select(static item => item.Outcome).ToArray());
    }

    [TestMethod]
    public void NoResponseSurfaceEverContainsPromptText()
    {
        using var fixture = new McpFixture();
        var manifest = McpManifests.ThreePrompts();

        using var session = fixture.RunInitialized(
            McpFrames.ToolsList(2),
            SubmitCall(3, manifest),
            McpFrames.ToolCall(4, McpToolNames.BatchStatus, "{\"batchId\":\"fire-pack\"}"),
            McpFrames.ToolCall(5, McpToolNames.GetBatchReport, "{\"batchId\":\"fire-pack\"}"),
            McpFrames.ToolCall(6, McpToolNames.CancelBatch, "{\"batchId\":\"fire-pack\"}"),
            Call(7, McpToolNames.ValidateManifest, McpManifests.EscapingRecipePath()),
            McpFrames.ToolCall(8, McpToolNames.GenerateEffect,
                "{\"item\":{\"itemId\":\"solo\",\"kind\":\"prompt\",\"prompt\":\"a secret ritual flame\"}}"));

        foreach (var forbidden in new[]
        {
            "calm blue spark", "slow ember trail", "POISON", "a secret ritual flame",
        })
        {
            Assert.IsFalse(
                session.RawOutput.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                "The responses must not carry '" + forbidden + "'.");
        }

        Assert.IsFalse(
            session.RawOutput.Contains(fixture.WorkspaceDirectory, StringComparison.OrdinalIgnoreCase),
            "No absolute path reaches a response.");
        Assert.IsFalse(
            session.RawOutput.Contains(":\\\\", StringComparison.Ordinal),
            "No resolved Windows drive path reaches a response.");

        // The one user-authored string a response does quote back is the rejected value itself, as
        // a bounded descriptor: that is how the client learns which value to fix, and it is the
        // client's own input rather than anything resolved on this machine.
        using var rejection = session.ToolPayload(7);
        var issue = rejection.RootElement.GetProperty("issues").EnumerateArray()
            .Single(entry => entry.GetProperty("code").GetString() == BatchDiagnosticCodes.UnsafeRecipePath);
        Assert.AreEqual("..\\..\\ProjectSettings\\x.json", issue.GetProperty("actualValue").GetString());
    }

    [TestMethod]
    public void ASessionWritesNothingIntoTheUnityProjectDirectory()
    {
        using var fixture = new McpFixture();
        var generated = Path.Combine(fixture.WorkspaceDirectory, "project", "Assets", "VFX", "Generated");
        Directory.CreateDirectory(generated);
        File.WriteAllText(Path.Combine(generated, "existing.prefab"), "unchanged");
        var projectRoot = Path.Combine(fixture.WorkspaceDirectory, "project");
        var before = SnapshotTree(projectRoot);

        using var session = fixture.RunInitialized(
            SubmitCall(2, McpManifests.ThreePrompts()),
            McpFrames.ToolCall(3, McpToolNames.GenerateEffect,
                "{\"item\":{\"itemId\":\"solo\",\"kind\":\"prompt\",\"prompt\":\"a single spark\"}}"),
            McpFrames.ToolCall(4, McpToolNames.CancelBatch, "{\"batchId\":\"fire-pack\"}"),
            McpFrames.ToolCall(5, McpToolNames.GetBatchReport, "{\"batchId\":\"fire-pack\"}"));

        Assert.IsFalse(session.ToolIsError(2), session.RawOutput);
        CollectionAssert.AreEqual(
            before,
            SnapshotTree(projectRoot),
            "The entry surface must not touch the Unity project.");
    }

    [TestMethod]
    public void TheHostOnlyServesTheStdioTransport()
    {
        Assert.AreEqual(McpStartupAction.ServeStdio, McpCommandLine.Parse(["--stdio"]).Action);
        Assert.AreEqual(McpStartupAction.PrintUsage, McpCommandLine.Parse(["--help"]).Action);
        foreach (string[] arguments in new[]
        {
            Array.Empty<string>(),
            ["--http"],
            ["--stdio", "--verbose"],
            ["--port", "8080"],
            ["--stdio="],
            ["serve"],
        })
        {
            var decision = McpCommandLine.Parse(arguments);
            Assert.AreEqual(
                McpStartupAction.Refuse,
                decision.Action,
                "'" + string.Join(' ', arguments) + "' must not start a server.");
            Assert.AreEqual(McpDiagnosticCodes.CommandLineRejected, decision.DiagnosticCode);
        }
    }

    [TestMethod]
    public void TheUsageTextNamesTheClosedToolSetAndNoOtherTransport()
    {
        var writer = new StringWriter();

        McpCommandLine.WriteUsage(writer);

        var usage = writer.ToString();
        foreach (var tool in McpToolCatalog.All)
        {
            StringAssert.Contains(usage, tool.Name);
        }

        StringAssert.Contains(usage, McpServer.ProtocolVersion);
        foreach (var forbidden in new[] { "http", "tcp", "socket", "listen" })
        {
            Assert.IsFalse(
                usage.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                "The usage text must not advertise a '" + forbidden + "' transport.");
        }
    }

    [TestMethod]
    public void EveryMcpDiagnosticCodeResolvesToAFixedPathFreeMessage()
    {
        foreach (var code in McpDiagnosticCodes.All)
        {
            var message = McpDiagnosticCatalog.Require(code);
            StringAssert.StartsWith(code, "VFXMCP");
            Assert.IsFalse(message.Contains('\n'), code + " must be single line.");
            Assert.IsFalse(message.Contains(":\\", StringComparison.Ordinal), code + " must be path free.");
            Assert.IsTrue(message.EndsWith('.'), code + " reads as a sentence.");
        }

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => McpDiagnosticCatalog.Require("VFXMCP999"));
    }

    private static string SubmitCall(long id, string manifest) => Call(id, McpToolNames.SubmitBatch, manifest);

    private static string Call(long id, string tool, string manifest) =>
        McpFrames.ToolCall(id, tool, "{\"manifest\":" + McpFrames.Quote(manifest) + "}");

    private static string[] SnapshotTree(string root) =>
        Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
            .OrderBy(static entry => entry, StringComparer.Ordinal)
            .Select(static entry => File.Exists(entry)
                ? entry + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(entry)))
                : entry)
            .ToArray();

    /// <summary>
    /// One CLI invocation over its own temporary store, used to compare the two entry surfaces.
    /// A detached run never starts an executor, so the generation channel is deliberately a trap:
    /// touching it fails the test.
    /// </summary>
    private sealed class CliBridge : IDisposable
    {
        private readonly StringBuilder _errors = new();
        private readonly string _directory;
        private readonly string _manifestPath;

        public CliBridge()
        {
            _directory = Directory.CreateDirectory(Path.Combine(
                Path.GetTempPath(),
                "vfxc-mcp-tests",
                Guid.NewGuid().ToString("N"))).FullName;
            _manifestPath = Path.Combine(_directory, "batch.json");
            Store = new JobStore(Path.Combine(_directory, "store"));
        }

        public JobStore Store { get; }

        public string Errors => _errors.ToString();

        public async Task<int> SubmitDetachedAsync(string manifestJson)
        {
            File.WriteAllText(_manifestPath, manifestJson, new UTF8Encoding(false));
            var output = new StringWriter();
            var error = new StringWriter();
            try
            {
                return await CliRunner.RunAsync(
                    ["batch", "run", _manifestPath, "--detach"],
                    new CliEnvironment
                    {
                        Output = output,
                        Error = error,
                        OpenQueue = () => new CliQueueSession(Store),
                        OpenGenerationRuntime = static () => new CliCapabilityRuntime(),
                    },
                    CancellationToken.None);
            }
            finally
            {
                _errors.Append(error.ToString());
            }
        }

        public BatchReport ReadReport() =>
            BatchReportBuilder.Deserialize(File.ReadAllText(_manifestPath + ".report.json"));

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
                // Temporary fixture cleanup is best effort.
            }
        }
    }

    private sealed class CliQueueSession : ICliQueueSession
    {
        public CliQueueSession(IJobQueueClient client)
        {
            Client = client;
        }

        public IJobQueueClient Client { get; }

        public bool TryStartExecutors(IReadOnlyList<IJobExecutor> executors) => false;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CliCapabilityRuntime : ICliGenerationRuntime
    {
        public BatchCapabilityProfile Capability => BatchCapabilityProfile.GenerationOnly;

        public IRecipeGenerationChannel GenerationChannel =>
            throw new InvalidOperationException("A detached submission must never touch the channel.");

        public IRecipeDraftStore DraftStore =>
            throw new InvalidOperationException("A detached submission must never touch the draft store.");

        public IJobExecutor? CreateRecipeBuildExecutor() => null;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
