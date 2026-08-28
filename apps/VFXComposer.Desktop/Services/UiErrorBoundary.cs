namespace VFXComposer.Desktop.Services;

public sealed class UiErrorBoundary : IUiErrorBoundary
{
    private readonly IInMemoryDiagnosticSink _diagnostics;

    public UiErrorBoundary(IInMemoryDiagnosticSink diagnostics)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public void Capture(string operation, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentNullException.ThrowIfNull(exception);

        _diagnostics.Record(
            "DESKTOP_UNHANDLED",
            $"The UI operation '{operation}' failed.",
            exception.GetType().Name);
    }

    public async ValueTask RunAsync(
        string operation,
        Func<ValueTask> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Capture(operation, exception);
        }
    }
}
