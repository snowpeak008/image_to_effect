using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace VFXComposer.Desktop.Views;

public sealed partial class JobsView : UserControl
{
    public JobsView() => AvaloniaXamlLoader.Load(this);
}
