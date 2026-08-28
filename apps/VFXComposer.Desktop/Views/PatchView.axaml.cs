using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace VFXComposer.Desktop.Views;

public sealed partial class PatchView : UserControl
{
    public PatchView() => AvaloniaXamlLoader.Load(this);
}
