using System.Diagnostics.Tracing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.Batch.Core;
using VFXComposer.Cli;
using VFXComposer.Jobs;
using VFXComposer.Mcp;

namespace VFXComposer.Mcp.Tests;

/// <summary>
/// Zero-network evidence for the two entry surfaces, observed rather than argued: the runtime's own
/// networking event sources are switched on and required to stay silent while the surface does the
/// things that could plausibly reach out — validating a manifest, and building the production
/// composition roots the two executables start from.
/// </summary>
[TestClass]
public sealed class EntrySurfaceZeroNetworkTests
{
    [TestMethod]
    public void ValidatingAManifestOpensNoNetwork()
    {
        using var fixture = new McpFixture();
        using var network = new NetworkActivityListener();

        using var session = fixture.RunInitialized(
            McpFrames.ToolCall(2, McpToolNames.ValidateManifest,
                "{\"manifest\":" + McpFrames.Quote(McpManifests.ThreePrompts()) + "}"),
            McpFrames.ToolCall(3, McpToolNames.ValidateManifest,
                "{\"manifest\":" + McpFrames.Quote(McpManifests.EscapingRecipePath()) + "}"));

        Assert.IsFalse(session.ToolIsError(2), session.RawOutput);
        Assert.IsTrue(session.ToolIsError(3), "The escaping path is still refused.");
        network.AssertSilent();
    }

    [TestMethod]
    public void TheGenerationSeamValidationOpensCannotReachAChannelAtAll()
    {
        // Validation needs the executable-capability profile, so it does open the generation seam.
        // That seam exposes one property, which is what makes the zero-network result structural
        // rather than incidental: there is no channel on it to invoke.
        CollectionAssert.AreEqual(
            new[] { nameof(IMcpGenerationRuntime.Capability) },
            typeof(IMcpGenerationRuntime).GetProperties().Select(static property => property.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { nameof(IAsyncDisposable.DisposeAsync) },
            typeof(IMcpGenerationRuntime).GetInterfaces()
                .SelectMany(static contract => contract.GetMethods())
                .Select(static method => method.Name)
                .ToArray());
    }

    [TestMethod]
    public void BuildingAndOpeningTheProductionCompositionRootsOpensNoNetwork()
    {
        using var network = new NetworkActivityListener();

        // Creating a root only binds seams; opening one resolves current-user paths and reads the
        // persisted channel bindings. Both are the real production types, not test doubles.
        var cli = CliProductionEnvironment.Create(TextWriter.Null, TextWriter.Null);
        var mcp = McpProductionEnvironment.Create(TextReader.Null, TextWriter.Null);
        OpenAndClose(() => cli.OpenQueue());
        OpenAndClose(() => cli.OpenGenerationRuntime());
        OpenAndClose(() => mcp.OpenQueue());
        OpenAndClose(() => mcp.OpenGenerationRuntime());

        network.AssertSilent();
    }

    /// <summary>
    /// Opens one production resource and closes it again. A machine with no AI binding or no
    /// readable queue store fails these with their own stable codes; the assertion under test is
    /// the absence of network traffic, so a local-environment refusal is an acceptable outcome and
    /// anything else still propagates.
    /// </summary>
    private static void OpenAndClose<T>(Func<T> open)
        where T : IAsyncDisposable
    {
        T resource;
        try
        {
            resource = open();
        }
        catch (Exception exception) when (exception is AiGatewayException or JobQueueException)
        {
            return;
        }

        resource.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}

/// <summary>
/// Records every event the runtime's networking event sources emit while it is alive. Sockets, DNS,
/// TLS and HTTP all announce themselves here before any byte leaves the process, so an empty record
/// is direct evidence that nothing was opened.
/// </summary>
internal sealed class NetworkActivityListener : EventListener
{
    private readonly List<string> _observed = [];

    public void AssertSilent()
    {
        string[] observed;
        lock (_observed)
        {
            observed = _observed.ToArray();
        }

        Assert.AreEqual(
            0,
            observed.Length,
            "The surface must open no network: " + string.Join(", ", observed));
    }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (IsNetworking(eventSource.Name))
        {
            EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.All);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventSource is null || !IsNetworking(eventData.EventSource.Name))
        {
            return;
        }

        lock (_observed)
        {
            _observed.Add(eventData.EventSource.Name + "/" + (eventData.EventName ?? "<unnamed>"));
        }
    }

    private static bool IsNetworking(string name) =>
        name.Contains("System.Net", StringComparison.Ordinal);
}
