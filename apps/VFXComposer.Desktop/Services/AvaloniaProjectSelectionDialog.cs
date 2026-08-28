using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace VFXComposer.Desktop.Services;

public sealed class AvaloniaProjectSelectionDialog(Func<Window?> owner) : IProjectSelectionDialog
{
    private readonly Func<Window?> _owner = owner ?? throw new ArgumentNullException(nameof(owner));

    public async ValueTask<string?> SelectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var window = _owner() ?? throw new InvalidOperationException("DESKTOP_SELECTION_UNAVAILABLE");
        var choices = await window.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Select a Unity project",
                AllowMultiple = false,
            });
        cancellationToken.ThrowIfCancellationRequested();
        return choices.Count == 1 ? choices[0].TryGetLocalPath() : null;
    }
}
