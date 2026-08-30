using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VFXComposer.Desktop.ViewModels;

/// <summary>
/// One collapsible group on the Jobs page: the entries of a single batch, or the standalone jobs
/// that belong to no batch. Grouping and the expand/collapse state live here so the page can fold a
/// long queue by batch without the flat <see cref="JobsViewModel.Jobs"/> list changing shape.
/// </summary>
public sealed class JobBatchGroupViewModel : ObservableObject
{
    private bool _isExpanded = true;

    public JobBatchGroupViewModel(string? batchId)
    {
        BatchId = batchId;
    }

    /// <summary>The batch identity, or null for the standalone-jobs group.</summary>
    public string? BatchId { get; }

    public bool IsBatch => BatchId is not null;

    public ObservableCollection<JobListItemViewModel> Items { get; } = [];

    public int Count => Items.Count;

    public string Header { get; private set; } = string.Empty;

    /// <summary>Fold state, preserved across refreshes by <see cref="JobsViewModel"/>.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>Recomputes the header line from the current item count.</summary>
    public void RefreshHeader()
    {
        var jobs = Count == 1 ? "1 job" : Count + " jobs";
        Header = BatchId is null
            ? "Individual jobs · " + jobs
            : "Batch " + (BatchId.Length > 12 ? BatchId[..12] : BatchId) + " · " + jobs;
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(Count));
    }
}
