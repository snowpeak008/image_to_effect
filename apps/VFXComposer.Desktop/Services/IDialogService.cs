namespace VFXComposer.Desktop.Services;

public interface IDialogService
{
    ValueTask ShowDiagnosticAsync(
        UiDiagnostic diagnostic,
        CancellationToken cancellationToken = default);
}
