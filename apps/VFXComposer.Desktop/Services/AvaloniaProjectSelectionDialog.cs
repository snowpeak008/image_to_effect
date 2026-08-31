using Avalonia.Controls;
using Avalonia.Platform.Storage;
using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.Services;

public sealed class AvaloniaProjectSelectionDialog(Func<Window?> owner, LocalizationService localization)
    : IProjectSelectionDialog
{
    private readonly Func<Window?> _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    private readonly LocalizationService _localization =
        localization ?? throw new ArgumentNullException(nameof(localization));

    public async ValueTask<string?> SelectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var window = _owner() ?? throw new InvalidOperationException("DESKTOP_SELECTION_UNAVAILABLE");
        var choices = await window.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = _localization[UiStringKeys.DialogSelectProjectTitle],
                AllowMultiple = false,
            });
        cancellationToken.ThrowIfCancellationRequested();
        return choices.Count == 1 ? choices[0].TryGetLocalPath() : null;
    }
}
