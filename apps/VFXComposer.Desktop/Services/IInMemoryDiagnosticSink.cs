namespace VFXComposer.Desktop.Services;

public interface IInMemoryDiagnosticSink
{
    IReadOnlyList<UiDiagnostic> Snapshot { get; }

    void Record(string code, string message, string? detail = null);
}
