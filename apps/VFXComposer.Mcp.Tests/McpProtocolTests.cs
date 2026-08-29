using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Mcp;

namespace VFXComposer.Mcp.Tests;

/// <summary>
/// The hand-written protocol subset: framing, the initialize handshake, tools/list, and the
/// refusal behaviour for every shape of message this server will not serve.
/// </summary>
[TestClass]
public sealed class McpProtocolTests
{
    [TestMethod]
    public void TheHandshakeDeclaresOneProtocolRevisionAndTheToolCapability()
    {
        using var fixture = new McpFixture();

        using var session = fixture.Run(McpFrames.Initialize(1));

        Assert.AreEqual(McpExitCodes.Success, session.ExitCode);
        Assert.AreEqual(1, session.Count, session.RawOutput);
        var result = session.Response(1).GetProperty("result");
        Assert.AreEqual(McpServer.ProtocolVersion, result.GetProperty("protocolVersion").GetString());
        Assert.AreEqual(McpServer.ServerName, result.GetProperty("serverInfo").GetProperty("name").GetString());
        Assert.IsFalse(result.GetProperty("capabilities").GetProperty("tools").GetProperty("listChanged").GetBoolean());
        Assert.AreEqual("2.0", session.Response(1).GetProperty("jsonrpc").GetString());
    }

    [TestMethod]
    public void AClientRevisionDifferentFromOursIsAcceptedAndAnsweredWithOurs()
    {
        using var fixture = new McpFixture();

        using var session = fixture.Run(McpFrames.Initialize(1, "2024-11-05"));

        Assert.AreEqual(
            McpServer.ProtocolVersion,
            session.Response(1).GetProperty("result").GetProperty("protocolVersion").GetString(),
            "The declared revision never follows the client.");
    }

    [TestMethod]
    public void ToolsListReturnsTheClosedToolSetWithBoundedSchemas()
    {
        using var fixture = new McpFixture();

        using var session = fixture.RunInitialized(McpFrames.ToolsList(2));

        var tools = session.Response(2).GetProperty("result").GetProperty("tools");
        var names = tools.EnumerateArray().Select(tool => tool.GetProperty("name").GetString()).ToArray();
        CollectionAssert.AreEqual(
            new[]
            {
                McpToolNames.ValidateManifest,
                McpToolNames.SubmitBatch,
                McpToolNames.GenerateEffect,
                McpToolNames.BatchStatus,
                McpToolNames.JobStatus,
                McpToolNames.CancelJob,
                McpToolNames.CancelBatch,
                McpToolNames.GetBatchReport,
            },
            names);
        foreach (var tool in tools.EnumerateArray())
        {
            var schema = tool.GetProperty("inputSchema");
            Assert.AreEqual("object", schema.GetProperty("type").GetString());
            Assert.IsFalse(
                schema.GetProperty("additionalProperties").GetBoolean(),
                "Every tool schema advertises a closed field set.");
            Assert.IsTrue(schema.TryGetProperty("required", out _));
        }
    }

    [TestMethod]
    public void TheAdvertisedToolSetMatchesTheCatalogAndCarriesNoAuthorityArgument()
    {
        foreach (var tool in McpToolCatalog.All)
        {
            using var schema = JsonDocument.Parse(tool.InputSchemaJson);
            var properties = schema.RootElement.GetProperty("properties");
            foreach (var property in properties.EnumerateObject())
            {
                Assert.IsFalse(
                    property.Name.Contains("approv", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Contains("authority", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Contains("skip", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Contains("force", StringComparison.OrdinalIgnoreCase),
                    "Tool " + tool.Name + " must not offer " + property.Name + ".");
            }
        }
    }

    [TestMethod]
    public void ToolCallsBeforeTheHandshakeAreRefused()
    {
        using var fixture = new McpFixture();

        using var session = fixture.Run(
            McpFrames.ToolsList(1),
            McpFrames.ToolCall(2, McpToolNames.JobStatus, "{\"jobId\":\"job-1\"}"));

        Assert.AreEqual(JsonRpcErrorCodes.NotInitialized, session.ErrorCode(1));
        Assert.AreEqual(McpDiagnosticCodes.NotInitialized, session.ErrorDiagnostic(1));
        Assert.AreEqual(JsonRpcErrorCodes.NotInitialized, session.ErrorCode(2));
    }

    [TestMethod]
    public void ASecondHandshakeIsRefused()
    {
        using var fixture = new McpFixture();

        using var session = fixture.Run(McpFrames.Initialize(1), McpFrames.Initialize(2));

        Assert.IsTrue(session.Response(1).TryGetProperty("result", out _));
        Assert.AreEqual(JsonRpcErrorCodes.InvalidRequest, session.ErrorCode(2));
        Assert.AreEqual(McpDiagnosticCodes.AlreadyInitialized, session.ErrorDiagnostic(2));
    }

    [TestMethod]
    public void AMalformedFrameIsAnsweredWithTheParseErrorAndTheSessionContinues()
    {
        using var fixture = new McpFixture();

        using var session = fixture.Run("{ not json at all", McpFrames.Initialize(1));

        Assert.AreEqual(2, session.Count, session.RawOutput);
        Assert.AreEqual(JsonRpcErrorCodes.ParseError, session.Message(0).GetProperty("error").GetProperty("code").GetInt32());
        Assert.AreEqual(
            McpDiagnosticCodes.MalformedFrame,
            session.Message(0).GetProperty("error").GetProperty("data").GetProperty("diagnostic").GetString());
        Assert.AreEqual(JsonValueKind.Null, session.Message(0).GetProperty("id").ValueKind);
        Assert.IsTrue(session.Response(1).TryGetProperty("result", out _), "A bad frame does not poison the session.");
    }

    [TestMethod]
    public void EnvelopesThatAreNotRequestsForThisServerAreRefused()
    {
        using var fixture = new McpFixture();

        using var session = fixture.Run(
            "{\"jsonrpc\":\"1.0\",\"id\":1,\"method\":\"initialize\"}",
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"initialize\",\"extra\":true}",
            "{\"jsonrpc\":\"2.0\",\"id\":3,\"result\":{}}",
            "[{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"initialize\"}]",
            "{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"initialize\",\"params\":[]}");

        Assert.AreEqual(JsonRpcErrorCodes.InvalidRequest, session.ErrorCode(1));
        Assert.AreEqual(JsonRpcErrorCodes.InvalidRequest, session.ErrorCode(2));
        Assert.AreEqual(JsonRpcErrorCodes.InvalidRequest, session.ErrorCode(3));
        Assert.AreEqual(JsonRpcErrorCodes.InvalidParams, session.ErrorCode(5));
        var batched = session.Message(3);
        Assert.AreEqual(JsonRpcErrorCodes.InvalidRequest, batched.GetProperty("error").GetProperty("code").GetInt32());
        Assert.AreEqual(JsonValueKind.Null, batched.GetProperty("id").ValueKind, "A batch has no single id to answer under.");
    }

    [TestMethod]
    public void AnUnknownMethodIsAnsweredWithMethodNotFound()
    {
        using var fixture = new McpFixture();

        using var session = fixture.RunInitialized(
            McpFrames.Request(2, "resources/list", null),
            McpFrames.Request(3, "prompts/list", null),
            McpFrames.Request(4, "sampling/createMessage", null));

        foreach (var id in new long[] { 2, 3, 4 })
        {
            Assert.AreEqual(JsonRpcErrorCodes.MethodNotFound, session.ErrorCode(id));
            Assert.AreEqual(McpDiagnosticCodes.MethodNotFound, session.ErrorDiagnostic(id));
        }
    }

    [TestMethod]
    public void NotificationsAreNeverAnswered()
    {
        using var fixture = new McpFixture();

        using var session = fixture.Run(
            McpFrames.Initialize(1),
            McpFrames.Initialized(),
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/cancelled\"}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"tools/list\"}",
            "{\"jsonrpc\":\"2.0\",\"id\":null,\"method\":\"tools/list\"}");

        Assert.AreEqual(1, session.Count, "Only the initialize request is answered: " + session.RawOutput);
    }

    [TestMethod]
    public void BlankLinesAreSkippedRatherThanReportedAsBadFrames()
    {
        using var fixture = new McpFixture();

        using var session = fixture.Run(string.Empty, "   ", McpFrames.Initialize(1), string.Empty);

        Assert.AreEqual(1, session.Count, session.RawOutput);
        Assert.AreEqual(McpExitCodes.Success, session.ExitCode);
    }

    [TestMethod]
    public void AnOversizedFrameClosesTheSessionWithATransportFault()
    {
        using var fixture = new McpFixture { MaximumFrameCharacters = 64 };

        using var session = fixture.Run(
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"" +
                new string('x', 512) + "\"}}",
            McpFrames.Initialize(2));

        Assert.AreEqual(McpExitCodes.TransportFault, session.ExitCode);
        Assert.AreEqual(1, session.Count, "The session stops at the frame it could not bound.");
        Assert.AreEqual(JsonRpcErrorCodes.ParseError, session.Message(0).GetProperty("error").GetProperty("code").GetInt32());
        Assert.AreEqual(
            McpDiagnosticCodes.FrameTooLarge,
            session.Message(0).GetProperty("error").GetProperty("data").GetProperty("diagnostic").GetString());
    }

    [TestMethod]
    public void EveryResponseIsOneCompleteLineFrame()
    {
        using var fixture = new McpFixture();

        using var session = fixture.RunInitialized(
            McpFrames.ToolsList(2),
            McpFrames.ToolCall(3, McpToolNames.ValidateManifest,
                "{\"manifest\":" + McpFrames.Quote(McpManifests.ThreePrompts()) + "}"));

        Assert.IsFalse(session.RawOutput.Contains('\r'), "The framing uses a bare line feed.");
        Assert.AreEqual(session.Lines.Count, session.Count, "Every line is exactly one JSON message.");
        Assert.IsTrue(session.RawOutput.EndsWith('\n'), "Every frame is terminated.");
    }

    [TestMethod]
    public void ToolsListRejectsAParameterItDoesNotDefine()
    {
        using var fixture = new McpFixture();

        using var session = fixture.RunInitialized(
            McpFrames.Request(2, McpMethods.ToolsList, "{\"limit\":10}"));

        Assert.AreEqual(JsonRpcErrorCodes.InvalidParams, session.ErrorCode(2));
    }

    [TestMethod]
    public void TheFrameReaderReportsTheStreamBoundaries()
    {
        var reader = new McpFrameReader(new StringReader("{\"a\":1}\r\n{\"b\":2}"));

        var first = reader.Read();
        var second = reader.Read();
        var third = reader.Read();

        Assert.AreEqual(McpFrameStatus.Message, first.Status);
        Assert.AreEqual("{\"a\":1}", first.Text, "A carriage return is not part of the frame.");
        Assert.AreEqual(McpFrameStatus.Message, second.Status);
        Assert.AreEqual("{\"b\":2}", second.Text, "A final frame without a newline is still delivered.");
        Assert.AreEqual(McpFrameStatus.EndOfStream, third.Status);
    }

    [TestMethod]
    public void TheFrameWriterRefusesAFrameThatWouldBreakTheFraming()
    {
        var writer = new McpFrameWriter(new StringWriter());

        Assert.ThrowsExactly<ArgumentException>(() => writer.Write("{\"a\":\n1}"));
    }
}
