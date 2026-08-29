using VFXComposer.Batch.Core;
using VFXComposer.Jobs;

namespace VFXComposer.Mcp;

/// <summary>
/// The generation-side resource a tool may need. This server never executes a job — submitting is
/// the detached equivalent, and the queue executor runs elsewhere — so the only thing it asks the
/// generation side for is what this machine can execute, which is derived from the persisted
/// channel bindings without constructing an HTTP client or reading a secret.
/// </summary>
public interface IMcpGenerationRuntime : IAsyncDisposable
{
    BatchCapabilityProfile Capability { get; }
}

/// <summary>
/// The queue-side resource a tool may need: the same client every entry surface uses. There is no
/// executor hosting here, which is what makes every submission behave like <c>batch run --detach</c>.
/// </summary>
public interface IMcpQueueSession : IAsyncDisposable
{
    IJobQueueClient Client { get; }
}

/// <summary>
/// Everything the server reaches outside itself. Production composes it from the current-user job
/// store and the current-user AI runtime over the process standard streams; tests compose it from
/// temporary directories and in-memory streams, so no test opens a transport or a provider.
/// </summary>
public sealed record McpEnvironment
{
    public required TextReader Input { get; init; }

    public required TextWriter Output { get; init; }

    public required Func<IMcpQueueSession> OpenQueue { get; init; }

    public required Func<IMcpGenerationRuntime> OpenGenerationRuntime { get; init; }

    public Func<DateTimeOffset> UtcNow { get; init; } = static () => DateTimeOffset.UtcNow;

    public int MaximumFrameCharacters { get; init; } = McpFrameReader.DefaultMaximumFrameCharacters;
}
