using CommunityToolkit.Mvvm.ComponentModel;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.ViewModels;

/// <summary>
/// The version-chain view of the Create page (REQ-004 §7): the retained versions of the head's lineage, oldest
/// first, one selectable row each, plus the inline two-step revert confirmation (REQ-004-25). The view is pure
/// presentation: it holds the list, the selection and the armed revert; the owning page runs the store. Nothing
/// here ever reaches a gateway.
/// </summary>
public sealed class LineageViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private IReadOnlyList<LineageVersionViewModel> _versions = [];
    private LineageVersionViewModel? _selectedVersion;
    private LineageVersionViewModel? _pendingTarget;
    private int _pendingDiscardCount;
    private string _pendingRangeLiteral = string.Empty;
    private RecipeDraftStoreErrorCode? _listFailureCode;

    internal LineageViewModel(LocalizationService localization)
    {
        _localization = localization;
    }

    /// <summary>Every retained version of the current lineage, oldest first; empty without a head.</summary>
    public IReadOnlyList<LineageVersionViewModel> Versions
    {
        get => _versions;
        private set
        {
            if (SetProperty(ref _versions, value))
            {
                OnPropertyChanged(nameof(HasVersions));
                OnPropertyChanged(nameof(IsCardVisible));
            }
        }
    }

    public bool HasVersions => Versions.Count > 0;

    /// <summary>
    /// The card shows while there is anything to say: a version list or the stable code of a refused listing.
    /// Bound instead of <see cref="HasVersions"/> so a refused listing (which empties the list) keeps the card
    /// and its failure line on screen.
    /// </summary>
    public bool IsCardVisible => HasVersions || HasListFailure;

    /// <summary>
    /// The row the user selected, if any. Changing the selection disarms a pending revert: its prompt named the
    /// versions after the previous selection, so it must not be confirmed against a different one.
    /// </summary>
    public LineageVersionViewModel? SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            if (SetProperty(ref _selectedVersion, value))
            {
                CancelRevert();
            }
        }
    }

    /// <summary>True between the revert click and its confirmation or cancellation; the confirmation bar is shown then.</summary>
    public bool IsRevertPending => _pendingTarget is not null;

    /// <summary>The number of newer versions the armed revert would delete, counted from the listed ordinals.</summary>
    public int PendingDiscardCount => _pendingDiscardCount;

    /// <summary>The armed revert's target; null when no revert is pending.</summary>
    public LineageVersionViewModel? PendingTarget => _pendingTarget;

    /// <summary>The one-time confirmation sentence: the count and ordinal range of the versions about to be deleted.</summary>
    public string RevertPrompt => _pendingTarget is null
        ? string.Empty
        : _localization.Format(UiStringKeys.CreateLineageRevertConfirmPrompt, _pendingDiscardCount, _pendingRangeLiteral);

    /// <summary>Empty while the list loaded; the store's stable code when listing the lineage was refused.</summary>
    public string ListFailure => _listFailureCode is null
        ? string.Empty
        : _localization.Format(UiStringKeys.CreateLineageListFailedWithCode, _listFailureCode);

    public bool HasListFailure => _listFailureCode is not null;

    public override string ToString() => "LineageViewModel(" + Versions.Count + " versions)";

    /// <summary>
    /// Re-renders the list for a new head. The newest listed ordinal is marked head; the previous selection survives
    /// when its version is still listed. A pending revert never survives a reload: the list it was computed from is gone.
    /// </summary>
    internal void Load(IReadOnlyList<RecipeDraftRecord> versions)
    {
        CancelRevert();
        var ordered = versions.OrderBy(static record => record.RevisionOrdinal).ToArray();
        var headId = ordered.Length == 0 ? null : ordered[^1].DraftId;
        var previousSelectionId = _selectedVersion?.DraftId;
        _listFailureCode = null;
        Versions = ordered
            .Select(record => new LineageVersionViewModel(
                _localization,
                record,
                string.Equals(record.DraftId, headId, StringComparison.Ordinal)))
            .ToArray();
        _selectedVersion = Versions.FirstOrDefault(version => string.Equals(version.DraftId, previousSelectionId, StringComparison.Ordinal));
        OnPropertyChanged(nameof(SelectedVersion));
        OnPropertyChanged(nameof(ListFailure));
        OnPropertyChanged(nameof(HasListFailure));
        OnPropertyChanged(nameof(IsCardVisible));
    }

    /// <summary>The store refused to list the lineage: the list empties and the stable code is shown in its place.</summary>
    internal void LoadFailed(RecipeDraftStoreErrorCode code)
    {
        Load([]);
        _listFailureCode = code;
        OnPropertyChanged(nameof(ListFailure));
        OnPropertyChanged(nameof(HasListFailure));
        OnPropertyChanged(nameof(IsCardVisible));
    }

    /// <summary>True when the selected row is an older version the user may revert to; the head and no selection are not.</summary>
    internal bool CanArmRevert() => _selectedVersion is { IsHead: false };

    /// <summary>
    /// Arms the revert for the selected version: the newer versions are counted from the listed ordinals (never
    /// guessed) and the confirmation bar shows. Returns false, arming nothing, without an eligible selection.
    /// </summary>
    internal bool ArmRevert()
    {
        if (_selectedVersion is not { IsHead: false } target)
        {
            return false;
        }

        var newer = Versions.Where(version => version.RevisionOrdinal > target.RevisionOrdinal).ToArray();
        if (newer.Length == 0)
        {
            return false;
        }

        _pendingTarget = target;
        _pendingDiscardCount = newer.Length;
        _pendingRangeLiteral = FormatRangeLiteral(newer);
        OnPropertyChanged(nameof(IsRevertPending));
        OnPropertyChanged(nameof(PendingDiscardCount));
        OnPropertyChanged(nameof(RevertPrompt));
        return true;
    }

    /// <summary>
    /// The versions the confirmation names: a <c>v2..v4</c> range only when the listed ordinals are contiguous.
    /// After a truncation the retained ordinals can skip (1, 2, 4), where a range would contradict the count, so
    /// the ordinals are listed one by one instead, up to five with a trailing ellipsis.
    /// </summary>
    private static string FormatRangeLiteral(IReadOnlyList<LineageVersionViewModel> newer)
    {
        if (newer.Count == 1)
        {
            return "v" + newer[0].RevisionOrdinal;
        }

        var contiguous = newer[^1].RevisionOrdinal - newer[0].RevisionOrdinal == newer.Count - 1;
        if (contiguous)
        {
            return "v" + newer[0].RevisionOrdinal + ".." + "v" + newer[^1].RevisionOrdinal;
        }

        const int maximumListedOrdinals = 5;
        var listed = string.Join(", ", newer.Take(maximumListedOrdinals).Select(static version => "v" + version.RevisionOrdinal));
        return newer.Count > maximumListedOrdinals ? listed + ", \u2026" : listed;
    }

    internal void CancelRevert()
    {
        if (_pendingTarget is null)
        {
            return;
        }

        _pendingTarget = null;
        _pendingDiscardCount = 0;
        _pendingRangeLiteral = string.Empty;
        OnPropertyChanged(nameof(IsRevertPending));
        OnPropertyChanged(nameof(PendingDiscardCount));
        OnPropertyChanged(nameof(RevertPrompt));
    }

    internal void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(RevertPrompt));
        OnPropertyChanged(nameof(ListFailure));
        foreach (var version in Versions)
        {
            version.RefreshLocalizedText();
        }
    }
}
