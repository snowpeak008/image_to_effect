using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace VFXComposer.Desktop.Views;

public sealed partial class PreviewView : UserControl
{
    public PreviewView() => AvaloniaXamlLoader.Load(this);
}
