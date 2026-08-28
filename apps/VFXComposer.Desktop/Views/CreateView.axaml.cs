using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace VFXComposer.Desktop.Views;

public sealed partial class CreateView : UserControl
{
    public CreateView() => AvaloniaXamlLoader.Load(this);
}
