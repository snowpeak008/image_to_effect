using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
                desktop.MainWindow = new MainWindow
                {
                    DataContext = MainWindowViewModel.CreateDisconnected(
                        diagnostics,
                        errorBoundary),
                };
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
