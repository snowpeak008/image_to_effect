using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using VFXComposer.Desktop.ViewModels;

namespace VFXComposer.Desktop.Views;

public sealed partial class JobsView : UserControl
{
    private readonly DispatcherTimer _refreshTimer;

    public JobsView()
    {
        AvaloniaXamlLoader.Load(this);
        // Local store polling only (REQ-003 §9.8, one-second cadence); no network is involved.
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _refreshTimer.Tick += (_, _) => (DataContext as JobsViewModel)?.Refresh();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        (DataContext as JobsViewModel)?.Refresh();
        _refreshTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _refreshTimer.Stop();
        base.OnDetachedFromVisualTree(e);
    }
}
