using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.ViewModels;

/// <summary>
/// One row of the version-chain view (REQ-004 §5.2 item 3): ordinal, origin, creation time, status, the bounded
/// feedback summary and the guard restoration count of one retained version. Origin and status words are protocol
/// vocabulary and stay verbatim in every language; the time is an invariant UTC literal. The recipe JSON, the
/// canonical hash and the full feedback text never leave the record through this row.
/// </summary>
public sealed class LineageVersionViewModel : ObservableObject
{
    /// <summary>The feedback text is shown up to this many characters, then an ellipsis (REQ-004 §5.2: bounded).</summary>
    public const int MaximumFeedbackSummaryCharacters = 80;

    private const string Ellipsis = "\u2026";

    private readonly LocalizationService _localization;

    internal LineageVersionViewModel(LocalizationService localization, RecipeDraftRecord record, bool isHead)
    {
        _localization = localization;
        DraftId = record.DraftId;
        RevisionOrdinal = record.RevisionOrdinal;
        Origin = RecipeDraftOriginNames.Of(record.Origin);
        Status = record.Status.ToString();
        CreatedLiteral = record.CreatedUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        FeedbackSummary = Summarize(record.FeedbackText);
        GuardRestorationCount = record.GuardRestorationCount;
        IsHead = isHead;
    }

    public string DraftId { get; }

    public int RevisionOrdinal { get; }

    /// <summary>The persisted origin spelling (<see cref="RecipeDraftOriginNames"/>), untranslated.</summary>
    public string Origin { get; }

    /// <summary>The <see cref="RecipeDraftStatus"/> name, untranslated.</summary>
    public string Status { get; }

    /// <summary>Creation time as <c>yyyy-MM-dd HH:mm</c> in UTC, invariant culture.</summary>
    public string CreatedLiteral { get; }

    /// <summary>The first <see cref="MaximumFeedbackSummaryCharacters"/> characters of the feedback; empty for non-refine versions.</summary>
    public string FeedbackSummary { get; }

    public bool HasFeedback => FeedbackSummary.Length > 0;

    /// <summary>The total restorations of the override guard, including any dropped from the bounded list.</summary>
    public int GuardRestorationCount { get; }

    /// <summary>True for the lineage's newest retained version, the only one that cannot be reverted to.</summary>
    public bool IsHead { get; }

    public string VersionLabel => _localization.Format(UiStringKeys.CreateLineageVersionLabel, RevisionOrdinal);

    public string HeadMarker => _localization[UiStringKeys.CreateLineageHeadMarker];

    public string CreatedLine => _localization.Format(UiStringKeys.CreateLineageCreatedLine, CreatedLiteral);

    public string GuardLine => _localization.Format(UiStringKeys.CreateLineageGuardLine, GuardRestorationCount);

    public string FeedbackLine => HasFeedback
        ? _localization.Format(UiStringKeys.CreateLineageFeedbackLine, FeedbackSummary)
        : string.Empty;

    public override string ToString() => "LineageVersionViewModel(v" + RevisionOrdinal + "," + Status + ")";

    internal void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(VersionLabel));
        OnPropertyChanged(nameof(HeadMarker));
        OnPropertyChanged(nameof(CreatedLine));
        OnPropertyChanged(nameof(GuardLine));
        OnPropertyChanged(nameof(FeedbackLine));
    }

    private static string Summarize(string? feedbackText)
    {
        if (string.IsNullOrEmpty(feedbackText))
        {
            return string.Empty;
        }

        // The row is a single line: newlines collapse to spaces so a multi-line feedback cannot break the layout.
        var singleLine = feedbackText.Replace('\r', ' ').Replace('\n', ' ');
        if (singleLine.Length <= MaximumFeedbackSummaryCharacters)
        {
            return singleLine;
        }

        // Cutting at a fixed UTF-16 index could split a surrogate pair (an emoji straddling the boundary) and
        // render a broken character, so the cut backs up to the nearest rune boundary instead.
        var cut = MaximumFeedbackSummaryCharacters;
        if (char.IsHighSurrogate(singleLine[cut - 1]))
        {
            cut--;
        }

        return singleLine[..cut] + Ellipsis;
    }
}
