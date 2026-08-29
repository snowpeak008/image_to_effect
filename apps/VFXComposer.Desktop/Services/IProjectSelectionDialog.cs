namespace VFXComposer.Desktop.Services;

public interface IProjectSelectionDialog
{
    ValueTask<string?> SelectAsync(CancellationToken cancellationToken = default);
}
