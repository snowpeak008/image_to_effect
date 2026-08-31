using CommunityToolkit.Mvvm.ComponentModel;
using VFXComposer.Desktop.Localization;
using VFXComposer.Jobs;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Desktop.ViewModels;

/// <summary>
/// One row of the Jobs list. It shows identifiers, closed-set states and stable codes only:
/// the payload, prompt text and filesystem paths are deliberately absent (REQ-003 §9.9).
/// State and kind words stay verbatim in every language: they are protocol vocabulary, not prose.
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

    public JobListItemViewModel(LocalizationService localization, JobRecord record)
    {
        Localization = localization ?? throw new ArgumentNullException(nameof(localization));
        JobId = record.JobId;
        ShortJobId = record.JobId.Length > 12 ? record.JobId[..12] : record.JobId;
        SourceEntry = record.SourceEntry;
        JobKind = record.JobKind;
        BatchId = record.BatchId;
        BatchDisplay = record.BatchId is null
            ? "—"
            : record.BatchId + " (" + record.BatchPolicy + ")";
        ItemId = record.ItemId;
        HasItemId = record.ItemId is not null;
        EnqueuedDisplay = FormatTime(record.EnqueuedAtUtc);
        Update(record);
    }

    /// <summary>Bound by the row template through the string indexer, e.g. <c>{Binding Localization[JobsKeepAction]}</c>.</summary>
    public LocalizationService Localization { get; }

    public string JobId { get; }
    public string ShortJobId { get; }
    public string SourceEntry { get; }
    public string JobKind { get; }
    public string BatchDisplay { get; }

    /// <summary>Raw batch identity used to group the Jobs page; null for a non-batch job.</summary>
    public string? BatchId { get; }

    /// <summary>Batch entry name, or null for a submission that is not a batch entry.</summary>
    public string? ItemId { get; }

    /// <summary>False for a non-batch job, whose row and detail omit the item slot entirely.</summary>
    public bool HasItemId { get; }

    public string EnqueuedDisplay { get; }

    /// <summary>Batch entry line of this row; empty for a submission that is not a batch entry.</summary>
    public string ItemLine => HasItemId
        ? Localization.Format(UiStringKeys.JobsItemLabel, ItemId)
        : string.Empty;

    public string QueuedLine => Localization.Format(UiStringKeys.JobsQueuedAtLabel, EnqueuedDisplay);

    public string StartedLine => Localization.Format(UiStringKeys.JobsStartedAtLabel, StartedDisplay);

    public string FinishedLine => Localization.Format(UiStringKeys.JobsFinishedAtLabel, CompletedDisplay);

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
        private set
        {
            if (SetProperty(ref _startedDisplay, value))
            {
                OnPropertyChanged(nameof(StartedLine));
            }
        }
    }

    public string CompletedDisplay
    {
        get => _completedDisplay;
        private set
        {
            if (SetProperty(ref _completedDisplay, value))
            {
                OnPropertyChanged(nameof(FinishedLine));
            }
        }
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

    /// <summary>Re-renders the row's localized lines; rows are transient, so the page pushes the switch instead of subscribing.</summary>
    internal void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(ItemLine));
        OnPropertyChanged(nameof(QueuedLine));
        OnPropertyChanged(nameof(StartedLine));
        OnPropertyChanged(nameof(FinishedLine));
    }

    private static string FormatTime(DateTimeOffset utc) =>
        utc.ToLocalTime().ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
}
