using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace VFXComposer.Desktop.Views;

public sealed partial class LibraryView : UserControl
{
    public LibraryView() => AvaloniaXamlLoader.Load(this);
}
