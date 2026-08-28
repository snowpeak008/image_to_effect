namespace VFXComposer.Desktop.Services;

public interface IUiErrorBoundary
{
    void Capture(string operation, Exception exception);

    ValueTask RunAsync(
        string operation,
        Func<ValueTask> action,
        CancellationToken cancellationToken = default);
}
