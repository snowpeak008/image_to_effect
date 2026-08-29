using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using VFXComposer.AI.Providers.Desktop;
using VFXComposer.Desktop.Services;
using VFXComposer.Desktop.ViewModels;
using VFXComposer.Desktop.Views;
using VFXComposer.Jobs;

namespace VFXComposer.Desktop;

public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var diagnostics = new InMemoryDiagnosticSink();
            var errorBoundary = new UiErrorBoundary(diagnostics);

            try
            {
                var session = new VFXComposer.Client.UserModeDesktopSession();
                // Local composition only: the runtime derives current-user storage but creates no provider adapter,
                // health probe, DNS request, or HTTP client until an explicit Create/Preview action.
                var aiRuntime = AiDesktopRuntimeFactory.CreateCurrentUser();
                // Local current-user job store; no payload executors are registered yet (F1/F2
                // plug in later), so this process only hosts recovery + observation for now.
                var jobStore = JobQueueFactory.CreateCurrentUserStore();
                JobQueueHost? jobHost = null;
                try
                {
                    jobHost = new JobQueueHost(jobStore, Array.Empty<IJobExecutor>());
                    jobHost.Start();
                }
                catch (JobQueueException exception)
                {
                    // Another entry-surface process already executes the queue, or the store is
                    // unavailable: this shell stays a submit/observe surface, never a shadow executor.
                    jobHost = null;
                    diagnostics.Record(
                        "JOBS_EXECUTOR_STANDBY",
                        "Job executor not hosted in this process: " + exception.Code + ".");
                }

                var window = new MainWindow();
                var selectionDialog = new AvaloniaProjectSelectionDialog(() => desktop.MainWindow);
                var viewModel = MainWindowViewModel.CreateUserMode(
                    session,
                    selectionDialog,
                    new AvaloniaUiDispatcher(),
                    diagnostics,
                    errorBoundary,
                    aiRuntime,
                    jobStore);
                window.DataContext = viewModel;
                desktop.MainWindow = window;
                desktop.Exit += async (_, _) =>
                {
                    if (jobHost is not null)
                    {
                        await jobHost.DisposeAsync();
                    }

                    await viewModel.DisposeAsync();
                };
                viewModel.ConnectCommand.Execute(null);
            }
            catch (Exception exception)
            {
                errorBoundary.Capture("DESKTOP_STARTUP", exception);
                throw;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
