using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using VFXComposer.AI.Providers.Desktop;
using VFXComposer.Desktop.Services;
using VFXComposer.Desktop.ViewModels;
using VFXComposer.Desktop.Views;

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
                var window = new MainWindow();
                var selectionDialog = new AvaloniaProjectSelectionDialog(() => desktop.MainWindow);
                var viewModel = MainWindowViewModel.CreateUserMode(
                    session,
                    selectionDialog,
                    new AvaloniaUiDispatcher(),
                    diagnostics,
                    errorBoundary,
                    aiRuntime);
                window.DataContext = viewModel;
                desktop.MainWindow = window;
                desktop.Exit += async (_, _) => await viewModel.DisposeAsync();
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
