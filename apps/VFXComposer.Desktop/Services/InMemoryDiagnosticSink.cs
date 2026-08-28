namespace VFXComposer.Desktop.Services;

/// <summary>
/// Bounded diagnostics retained only for the current process. Phase 1 writes no log file.
/// </summary>
public sealed class InMemoryDiagnosticSink : IInMemoryDiagnosticSink
{
    private const int MaximumEntries = 256;
    private readonly object _gate = new();
    private readonly List<UiDiagnostic> _entries = [];
    private long _sequence;

    public IReadOnlyList<UiDiagnostic> Snapshot
    {
        get
        {
            lock (_gate)
            {
                return _entries.ToArray();
            }
        }
    }

    public void Record(string code, string message, string? detail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        lock (_gate)
        {
            _entries.Add(new UiDiagnostic(++_sequence, code, message, detail));
            if (_entries.Count > MaximumEntries)
            {
                _entries.RemoveAt(0);
            }
        }
    }
}
