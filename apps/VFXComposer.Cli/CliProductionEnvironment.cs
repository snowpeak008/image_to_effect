using VFXComposer.AI.Contracts.Desktop;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Desktop;
using VFXComposer.Batch.Core;
using VFXComposer.Jobs;

namespace VFXComposer.Cli;

/// <summary>
/// The production composition root: the current-user job store and the current-user AI runtime,
/// both placed under local application data and never inside a Unity project.
/// </summary>
internal static class CliProductionEnvironment
{
    public static CliEnvironment Create(TextWriter output, TextWriter error) => new()
    {
        Output = output,
        Error = error,
        OpenQueue = static () => new JobStoreQueueSession(JobQueueFactory.CreateCurrentUserStore()),
        OpenGenerationRuntime = static () => new DesktopGenerationRuntime(),
    };
}

/// <summary>
/// Wraps the durable current-user store and, for a foreground run, one in-process executor host.
/// The host is what makes the queue drain while this process lives; without it the entries stay
/// in the store for the next executor, which is exactly the detached semantics.
/// </summary>
internal sealed class JobStoreQueueSession : ICliQueueSession
{
    private readonly JobStore _store;
    private JobQueueHost? _host;

    public JobStoreQueueSession(JobStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public IJobQueueClient Client => _store;

    public override string ToString() => "JobStoreQueueSession";

    public bool TryStartExecutors(IReadOnlyList<IJobExecutor> executors)
    {
        ArgumentNullException.ThrowIfNull(executors);
        // The real project-lock probe is wired only when a build-capable host is present; without it
        // the queue would happily start a build while the graphical editor owns the project.
        var buildHost = executors.Any(static executor => executor.RequiresProjectLock)
            ? UnityBuildHostLocator.TryLocate()
            : null;
        var host = new JobQueueHost(
            _store,
            executors,
            projectLockProbe: buildHost is null ? null : new UnityProjectLockProbe(buildHost.ProjectPath));
        try
        {
            host.Start();
        }
        catch (JobQueueException exception)
            when (string.Equals(exception.Code, JobQueueDiagnosticCodes.ExecutorLockUnavailable, StringComparison.Ordinal))
        {
            _ = host.DisposeAsync();
            return false;
        }

        _host = host;
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync().ConfigureAwait(false);
            _host = null;
        }
    }
}

/// <summary>
/// Binds the CLI to the same F1 channel and draft store the Desktop uses. Construction resolves
/// local paths and reads the persisted channel bindings; it creates no HTTP client, parses no
/// endpoint and reads no secret. The first network-capable construction still happens inside a
/// running generation job, which is the user's explicit submit action.
/// </summary>
internal sealed class DesktopGenerationRuntime : ICliGenerationRuntime
{
    private readonly IAiDesktopRuntime _runtime;
    private readonly UnityBuildHost? _buildHost;

    public DesktopGenerationRuntime()
    {
        _runtime = AiDesktopRuntimeFactory.CreateCurrentUser();
        _buildHost = UnityBuildHostLocator.TryLocate();
        Capability = BatchCapabilityProbe.FromDesktopRuntime(_runtime, recipeBuildSupported: _buildHost is not null);
    }

    public BatchCapabilityProfile Capability { get; }

    public IRecipeGenerationChannel GenerationChannel => _runtime.RecipeGeneration;

    public IRecipeDraftStore DraftStore => _runtime.RecipeDrafts;

    public override string ToString() => "DesktopGenerationRuntime";

    public ValueTask DisposeAsync() => _runtime.DisposeAsync();

    public IJobExecutor? CreateRecipeBuildExecutor() =>
        _buildHost is null
            ? null
            : new RecipeBuildJobExecutor(new RecipeBuildOrchestrator(
                new UnityBatchmodeRecipeBuildRunner(_buildHost.WrapperScriptPath),
                () => _runtime.RecipeDrafts));
}
