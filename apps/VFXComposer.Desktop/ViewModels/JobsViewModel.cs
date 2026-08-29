using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using VFXComposer.Jobs;

namespace VFXComposer.Desktop.ViewModels;

/// <summary>
/// Jobs page over the local serial queue: list, detail timeline, cancellation and
/// re-enqueue. All data comes from the local job store; the page performs no network
/// request and never renders payload text or filesystem paths.
/// </summary>
public sealed class JobsViewModel : WorkspacePageViewModel
{
    private readonly IJobQueueClient? _queue;
    private JobListItemViewModel? _selectedJob;
    private string _queueStatus = "Queue idle.";
    private bool _isWaitingForProjectLock;
    private string _storeStatus = string.Empty;
    private string _selectedDiagnostic = string.Empty;
    private string _selectedArtifacts = string.Empty;

    public JobsViewModel()
        : this(null)
    {
    }

    public JobsViewModel(IJobQueueClient? jobQueue)
        : base(
            "jobs",
            "Jobs",
            "Local serial job queue: strict FIFO, single global execution slot, durable across restarts.",
            "No jobs are running")
    {
        _queue = jobQueue;
        RefreshCommand = new RelayCommand(Refresh);
        RequestCancelCommand = new RelayCommand<JobListItemViewModel>(RequestCancel);
        ConfirmCancelCommand = new RelayCommand<JobListItemViewModel>(ConfirmCancel);
        DismissCancelCommand = new RelayCommand<JobListItemViewModel>(DismissCancel);
        ResubmitCommand = new RelayCommand<JobListItemViewModel>(Resubmit);
    }

    public ObservableCollection<JobListItemViewModel> Jobs { get; } = [];

    public ObservableCollection<string> SelectedJobTimeline { get; } = [];

    public bool HasJobs => Jobs.Count > 0;

    public JobListItemViewModel? SelectedJob
    {
        get => _selectedJob;
        set
        {
            if (SetProperty(ref _selectedJob, value))
            {
                RefreshSelectedDetail();
            }
        }
    }

    /// <summary>Queue-level line: idle, executing, or the waiting-for-project-lock banner state.</summary>
    public string QueueStatus
    {
        get => _queueStatus;
        private set => SetProperty(ref _queueStatus, value);
    }

    public bool IsWaitingForProjectLock
    {
        get => _isWaitingForProjectLock;
        private set => SetProperty(ref _isWaitingForProjectLock, value);
    }

    /// <summary>Empty while healthy; a stable store error code when the queue store is unreadable.</summary>
    public string StoreStatus
    {
        get => _storeStatus;
        private set => SetProperty(ref _storeStatus, value);
    }

    public string SelectedDiagnostic
    {
        get => _selectedDiagnostic;
        private set => SetProperty(ref _selectedDiagnostic, value);
    }

    public string SelectedArtifacts
    {
        get => _selectedArtifacts;
        private set => SetProperty(ref _selectedArtifacts, value);
    }

    public IRelayCommand RefreshCommand { get; }
    public IRelayCommand<JobListItemViewModel> RequestCancelCommand { get; }
    public IRelayCommand<JobListItemViewModel> ConfirmCancelCommand { get; }
    public IRelayCommand<JobListItemViewModel> DismissCancelCommand { get; }
    public IRelayCommand<JobListItemViewModel> ResubmitCommand { get; }

    /// <summary>Re-reads the local store; called by the page timer and after every operation.</summary>
    public void Refresh()
    {
        if (_queue is null)
        {
            return;
        }

        JobQueueSnapshotView snapshot;
        try
        {
            snapshot = _queue.ReadSnapshot();
        }
        catch (JobQueueException exception)
        {
            StoreStatus = "Job store unavailable: " + exception.Code + ".";
            return;
        }

        StoreStatus = string.Empty;
        IsWaitingForProjectLock = string.Equals(
            snapshot.QueueState, JobQueueStates.WaitingProjectLock, StringComparison.Ordinal);
        QueueStatus = snapshot.QueueState switch
        {
            JobQueueStates.Executing => "Queue executing.",
            JobQueueStates.WaitingProjectLock =>
                "Unity editor holds the project; the queue is waiting (" +
                JobQueueDiagnosticCodes.WaitingProjectLock + ").",
            _ => "Queue idle.",
        };

        var ordered = snapshot.Jobs
            .OrderBy(record => string.Equals(
                record.State, Protocol.Jobs.JobStatusStates.Running, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(record => record.QueuePosition)
            .ToArray();
        var existing = Jobs.ToDictionary(item => item.JobId, StringComparer.Ordinal);
        var selectedId = SelectedJob?.JobId;
        Jobs.Clear();
        foreach (var record in ordered)
        {
            if (existing.TryGetValue(record.JobId, out var item))
            {
                item.Update(record);
                Jobs.Add(item);
            }
            else
            {
                Jobs.Add(new JobListItemViewModel(record));
            }
        }

        OnPropertyChanged(nameof(HasJobs));
        if (selectedId is not null)
        {
            SelectedJob = Jobs.FirstOrDefault(item =>
                string.Equals(item.JobId, selectedId, StringComparison.Ordinal));
        }

        RefreshSelectedDetail();
    }

    private void RequestCancel(JobListItemViewModel? item)
    {
        if (item is { CanCancel: true })
        {
            item.IsCancelPending = true;
        }
    }

    private void DismissCancel(JobListItemViewModel? item)
    {
        if (item is not null)
        {
            item.IsCancelPending = false;
        }
    }

    private void ConfirmCancel(JobListItemViewModel? item)
    {
        if (_queue is null || item is null || !item.IsCancelPending)
        {
            return;
        }

        item.IsCancelPending = false;
        try
        {
            _queue.RequestCancel(item.JobId);
        }
        catch (JobQueueException exception)
        {
            StoreStatus = "Cancel rejected: " + exception.Code + ".";
            return;
        }

        Refresh();
    }

    private void Resubmit(JobListItemViewModel? item)
    {
        if (_queue is null || item is not { CanResubmit: true })
        {
            return;
        }

        try
        {
            _queue.Resubmit(item.JobId);
        }
        catch (JobQueueException exception)
        {
            StoreStatus = "Re-enqueue rejected: " + exception.Code + ".";
            return;
        }

        Refresh();
    }

    private void RefreshSelectedDetail()
    {
        SelectedJobTimeline.Clear();
        if (_queue is null || SelectedJob is null)
        {
            SelectedDiagnostic = string.Empty;
            SelectedArtifacts = string.Empty;
            return;
        }

        IReadOnlyList<JobStoreEvent> events;
        try
        {
            events = _queue.ReadEvents(SelectedJob.JobId);
        }
        catch (JobQueueException exception)
        {
            SelectedDiagnostic = "Timeline unavailable: " + exception.Code + ".";
            SelectedArtifacts = string.Empty;
            return;
        }

        var artifactCount = 0;
        var artifactIds = new List<string>();
        var diagnostic = string.Empty;
        foreach (var storeEvent in events)
        {
            SelectedJobTimeline.Add(FormatTimelineEntry(storeEvent));
            if (string.Equals(storeEvent.Kind, JobStoreEventKinds.Artifact, StringComparison.Ordinal))
            {
                artifactCount++;
                artifactIds.Add(storeEvent.ArtifactId!);
            }

            if (string.Equals(storeEvent.Kind, JobStoreEventKinds.Completion, StringComparison.Ordinal) &&
                storeEvent.DiagnosticCode is not null)
            {
                var definition = JobQueueDiagnosticCatalog.Require(storeEvent.DiagnosticCode);
                diagnostic = definition.Code + " (" + definition.Severity + "): " + definition.Message +
                    (definition.Retryable ? " Retry is possible." : string.Empty);
            }
        }

        SelectedDiagnostic = diagnostic;
        SelectedArtifacts = artifactCount == 0
            ? "No artifacts"
            : artifactCount + " artifact(s): " + string.Join(", ", artifactIds);
    }

    private static string FormatTimelineEntry(JobStoreEvent storeEvent)
    {
        var time = storeEvent.OccurredAtUtc.ToLocalTime()
            .ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        var prefix = "#" + storeEvent.EventSequence + " " + time + " " + storeEvent.Kind;
        return storeEvent.Kind switch
        {
            JobStoreEventKinds.Status => prefix + " " + storeEvent.State,
            JobStoreEventKinds.Progress => prefix + " " + storeEvent.State + " " +
                (storeEvent.ProgressPermille!.Value / 10) + "%",
            JobStoreEventKinds.Log => prefix + " " + storeEvent.Level + " " + storeEvent.DiagnosticCode +
                " — " + JobQueueDiagnosticCatalog.Require(storeEvent.DiagnosticCode!).Message,
            JobStoreEventKinds.Artifact => prefix + " " + storeEvent.ArtifactId,
            _ => prefix + " " + storeEvent.Outcome +
                (storeEvent.DiagnosticCode is null ? string.Empty : " " + storeEvent.DiagnosticCode),
        };
    }
}
