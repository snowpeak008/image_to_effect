using CommunityToolkit.Mvvm.ComponentModel;
using VFXComposer.Jobs;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Desktop.ViewModels;

/// <summary>
/// One row of the Jobs list. It shows identifiers, closed-set states and stable codes only:
/// the payload, prompt text and filesystem paths are deliberately absent (REQ-003 §9.9).
/// </summary>
public sealed class JobListItemViewModel : ObservableObject
{
    private string _state = JobStatusStates.Queued;
    private string _progressDisplay = "0%";
    private string _startedDisplay = "—";
    private string _completedDisplay = "—";
    private string _diagnosticDisplay = "—";
    private bool _isRunning;
    private bool _canCancel;
    private bool _canResubmit;
    private bool _isCancelPending;

    public JobListItemViewModel(JobRecord record)
    {
        JobId = record.JobId;
        ShortJobId = record.JobId.Length > 12 ? record.JobId[..12] : record.JobId;
        SourceEntry = record.SourceEntry;
        JobKind = record.JobKind;
        BatchDisplay = record.BatchId is null
            ? "—"
            : record.BatchId + " (" + record.BatchPolicy + ")";
        EnqueuedDisplay = FormatTime(record.EnqueuedAtUtc);
        Update(record);
    }

    public string JobId { get; }
    public string ShortJobId { get; }
    public string SourceEntry { get; }
    public string JobKind { get; }
    public string BatchDisplay { get; }
    public string EnqueuedDisplay { get; }

    public string State
    {
        get => _state;
        private set => SetProperty(ref _state, value);
    }

    public string ProgressDisplay
    {
        get => _progressDisplay;
        private set => SetProperty(ref _progressDisplay, value);
    }

    public string StartedDisplay
    {
        get => _startedDisplay;
        private set => SetProperty(ref _startedDisplay, value);
    }

    public string CompletedDisplay
    {
        get => _completedDisplay;
        private set => SetProperty(ref _completedDisplay, value);
    }

    /// <summary>Stable jobs-domain code of the final verdict, or a dash while non-terminal.</summary>
    public string DiagnosticDisplay
    {
        get => _diagnosticDisplay;
        private set => SetProperty(ref _diagnosticDisplay, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set => SetProperty(ref _isRunning, value);
    }

    public bool CanCancel
    {
        get => _canCancel;
        private set => SetProperty(ref _canCancel, value);
    }

    public bool CanResubmit
    {
        get => _canResubmit;
        private set => SetProperty(ref _canResubmit, value);
    }

    /// <summary>First cancel click arms the confirmation; the second click performs it.</summary>
    public bool IsCancelPending
    {
        get => _isCancelPending;
        internal set => SetProperty(ref _isCancelPending, value);
    }

    internal void Update(JobRecord record)
    {
        State = record.State;
        ProgressDisplay = (record.LastProgressPermille / 10) + "%";
        StartedDisplay = record.StartedAtUtc is null ? "—" : FormatTime(record.StartedAtUtc.Value);
        CompletedDisplay = record.CompletedAtUtc is null ? "—" : FormatTime(record.CompletedAtUtc.Value);
        DiagnosticDisplay = record.FinalDiagnosticCode ?? "—";
        IsRunning = string.Equals(record.State, JobStatusStates.Running, StringComparison.Ordinal);
        CanCancel = !record.IsTerminal;
        CanResubmit = record.State
            is JobStatusStates.Failed
            or JobStatusStates.Disconnected;
        if (record.IsTerminal)
        {
            IsCancelPending = false;
        }
    }

    private static string FormatTime(DateTimeOffset utc) =>
        utc.ToLocalTime().ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
}
