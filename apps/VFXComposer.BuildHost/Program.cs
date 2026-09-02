using VFXComposer.AI.Contracts.Desktop;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Desktop;
using VFXComposer.Batch.Core;
using VFXComposer.BuildHost;
using VFXComposer.Jobs;

// The production composition root, deliberately shaped like the CLI's (CliProductionEnvironment):
// the same current-user job store, the same shared draft store through the same runtime factory,
// the same locator/probe/runner wiring. Ctrl+C stops the foreground wait only; a claimed entry is
// settled by the queue host's own shutdown semantics, and a queued one stays for the next host.
using var interrupt = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    interrupt.Cancel();
};

var environment = new BuildHostEnvironment
{
    Output = Console.Out,
    OpenDrafts = static () => new DesktopRuntimeDraftSession(),
    OpenQueue = static () => JobQueueFactory.CreateCurrentUserStore(),
    LocateBuildHost = static () => UnityBuildHostLocator.TryLocate(),
    CreateRunner = static host => new UnityBatchmodeRecipeBuildRunner(host.WrapperScriptPath),
};

try
{
    return await BuildHostRunner.RunAsync(args, environment, interrupt.Token).ConfigureAwait(false);
}
catch (OperationCanceledException)
{
    return BuildHostExitCodes.BuildDisconnected;
}

/// <summary>
/// Binds the host to the exact shared draft store the Desktop and CLI use. Construction resolves
/// local paths only: no HTTP client, no endpoint, no secret — the host never registers a
/// generation executor, so this process is structurally incapable of a network request.
/// </summary>
internal sealed class DesktopRuntimeDraftSession : IBuildHostDraftSession
{
    private readonly IAiDesktopRuntime _runtime = AiDesktopRuntimeFactory.CreateCurrentUser();

    public IRecipeDraftStore Drafts => _runtime.RecipeDrafts;

    public override string ToString() => "DesktopRuntimeDraftSession";

    public ValueTask DisposeAsync() => _runtime.DisposeAsync();
}
