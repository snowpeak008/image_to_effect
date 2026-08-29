using VFXComposer.AI.Contracts.Desktop;
using VFXComposer.AI.Providers.Desktop;
using VFXComposer.Batch.Core;
using VFXComposer.Jobs;

namespace VFXComposer.Mcp;

/// <summary>
/// The production composition root: the process standard streams as the only transport, plus the
/// current-user job store and the current-user AI runtime, both under local application data and
/// never inside a Unity project. No listener, no socket and no environment variable participates.
/// </summary>
internal static class McpProductionEnvironment
{
    public static McpEnvironment Create(TextReader input, TextWriter output) => new()
    {
        Input = input,
        Output = output,
        OpenQueue = static () => new JobStoreQueueSession(JobQueueFactory.CreateCurrentUserStore()),
        OpenGenerationRuntime = static () => new DesktopCapabilityRuntime(),
    };
}

/// <summary>
/// The durable current-user store, read and written through the shared queue client. This surface
/// hosts no executor: a submission is always the detached form, and whichever process owns queue
/// execution drains the entries.
/// </summary>
internal sealed class JobStoreQueueSession : IMcpQueueSession
{
    private readonly JobStore _store;

    public JobStoreQueueSession(JobStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public IJobQueueClient Client => _store;

    public override string ToString() => "JobStoreQueueSession";

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Reads the executable-capability profile from the same persisted channel bindings the Desktop
/// and the CLI read. Construction resolves local paths and reads the binding snapshot; it creates
/// no HTTP client, parses no endpoint and reads no secret, and this surface never invokes the
/// channel at all because it never executes a job.
/// </summary>
internal sealed class DesktopCapabilityRuntime : IMcpGenerationRuntime
{
    private readonly IAiDesktopRuntime _runtime;

    public DesktopCapabilityRuntime()
    {
        _runtime = AiDesktopRuntimeFactory.CreateCurrentUser();
        Capability = BatchCapabilityProbe.FromDesktopRuntime(_runtime);
    }

    public BatchCapabilityProfile Capability { get; }

    public override string ToString() => "DesktopCapabilityRuntime";

    public ValueTask DisposeAsync() => _runtime.DisposeAsync();
}
