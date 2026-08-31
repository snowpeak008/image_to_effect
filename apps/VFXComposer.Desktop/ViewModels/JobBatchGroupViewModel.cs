using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.ViewModels;

/// <summary>
/// One collapsible group on the Jobs page: the entries of a single batch, or the standalone jobs
/// that belong to no batch. Grouping and the expand/collapse state live here so the page can fold a
/// long queue by batch without the flat <see cref="JobsViewModel.Jobs"/> list changing shape.
/// </summary>
public sealed class JobBatchGroupViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private bool _isExpanded = true;

    public JobBatchGroupViewModel(LocalizationService localization, string? batchId)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        BatchId = batchId;
    }

    /// <summary>The batch identity, or null for the standalone-jobs group.</summary>
    public string? BatchId { get; }

    public bool IsBatch => BatchId is not null;

    public ObservableCollection<JobListItemViewModel> Items { get; } = [];

    public int Count => Items.Count;

    /// <summary>Group line derived from the catalog, so a language switch re-renders it without a refresh tick.</summary>
    public string Header => BatchId is null
        ? _localization.Format(UiStringKeys.JobsBatchGroupIndividual, Count)
        : _localization.Format(
            UiStringKeys.JobsBatchGroupBatch,
            BatchId.Length > 12 ? BatchId[..12] : BatchId,
            Count);

    /// <summary>Fold state, preserved across refreshes by <see cref="JobsViewModel"/>.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>Re-renders the header line after the item count or the language changed.</summary>
    public void RefreshHeader()
    {
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(Count));
    }
}
