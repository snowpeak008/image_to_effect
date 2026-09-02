using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.ViewModels;

/// <summary>
/// The Create page's session timeline (REQ-004 §6.3, REQ-004-20): one entry per round — generation, refinement,
/// hand edit, revert, confirmation, preset — carrying request counts, stable error-code sequences, L1.5 codes,
/// guard restoration counts and paths, the resulting version id with its origin, and the prompt template version.
/// Session-scoped by design (master-plan ruling ⑩): durable inspectability lives in the version chain; this view
/// is not persisted. Every argument is a protocol literal; free text (feedback, descriptions, prompts, endpoints)
/// has no entry point here (REQ-004-21).
/// </summary>
public sealed class SessionTimelineViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly ObservableCollection<SessionTimelineEntryViewModel> _entries = [];

    internal SessionTimelineViewModel(LocalizationService localization)
    {
        _localization = localization;
        Entries = new ReadOnlyObservableCollection<SessionTimelineEntryViewModel>(_entries);
    }

    /// <summary>Chronological, oldest first; the collection lives for the session only.</summary>
    public ReadOnlyObservableCollection<SessionTimelineEntryViewModel> Entries { get; }

    public bool HasEntries => _entries.Count > 0;

    public override string ToString() => "SessionTimelineViewModel(" + _entries.Count + " entries)";

    internal void AppendGenerationDrafted(RecipeGenerationResult result, RecipeDraftSaveOutcome outcome) => Append(
        UiStringKeys.CreateTimelineEntryDraftSaved,
        [
            outcome.Record.DraftId,
            RecipeDraftOriginNames.Of(outcome.Record.Origin),
            result.RequestCount,
            ErrorCodeSequence(result.Attempts),
            result.PromptTemplateVersion,
        ],
        outcome);

    internal void AppendGenerationValidationFailed(RecipeGenerationResult result) => Append(
        UiStringKeys.CreateTimelineEntryGenerationFailed,
        [result.RequestCount, ErrorCodeSequence(result.Attempts)]);

    internal void AppendGenerationChannelFailed(object? stableCode, int requestCount) => Append(
        UiStringKeys.CreateTimelineEntryGenerationUnavailable,
        [stableCode, requestCount]);

    internal void AppendPresetApplied(RecipeDraftSaveOutcome outcome) => Append(
        UiStringKeys.CreateTimelineEntryPresetApplied,
        [outcome.Record.DraftId, outcome.Record.PresetId],
        outcome);

    internal void AppendHumanEditSaved(RecipeDraftSaveOutcome outcome, IReadOnlyList<RecipeValidationIssue> warnings) => Append(
        UiStringKeys.CreateTimelineEntryHumanEditSaved,
        [outcome.Record.DraftId, outcome.Record.RevisionOrdinal, IssueCodes(warnings)],
        outcome);

    internal void AppendRefined(
        RecipeRefinementResult result,
        RecipeDraftSaveOutcome outcome,
        IReadOnlyList<RecipeValidationIssue> warnings) => Append(
        UiStringKeys.CreateTimelineEntryRefined,
        [
            outcome.Record.DraftId,
            RecipeDraftOriginNames.Of(outcome.Record.Origin),
            result.RequestCount,
            ErrorCodeSequence(result.Attempts),
            IssueCodes(warnings),
            result.GuardRestorations.Count,
            GuardPaths(result.GuardRestorations),
            result.PromptTemplateVersion,
        ],
        outcome);

    internal void AppendRefineValidationFailed(RecipeRefinementResult result) => Append(
        UiStringKeys.CreateTimelineEntryRefineValidationFailed,
        [result.RequestCount, ErrorCodeSequence(result.Attempts)]);

    internal void AppendRefineChannelFailed(RecipeRefinementResult result) => Append(
        UiStringKeys.CreateTimelineEntryRefineChannelFailed,
        [result.ChannelError, result.RequestCount]);

    internal void AppendReverted(RecipeDraftTruncateOutcome outcome) => Append(
        UiStringKeys.CreateTimelineEntryReverted,
        [outcome.Head.RevisionOrdinal, outcome.RemovedDraftIds.Count]);

    internal void AppendConfirmed(RecipeDraftRecord record) => Append(
        UiStringKeys.CreateTimelineEntryConfirmed,
        [record.DraftId]);

    internal void RefreshLocalizedText()
    {
        foreach (var entry in _entries)
        {
            entry.RefreshLocalizedText();
        }
    }

    private void Append(string key, object?[] arguments, RecipeDraftSaveOutcome? outcome = null)
    {
        // A save that retained everything folds no line; a trim, eviction or supersede is said in place (REQ-004-33).
        var hasRetention = outcome is not null &&
            (outcome.SupersededDraftIds.Count > 0 || outcome.TrimmedDraftIds.Count > 0 || outcome.EvictedLineageIds.Count > 0);
        _entries.Add(hasRetention
            ? new SessionTimelineEntryViewModel(
                _localization,
                key,
                arguments,
                UiStringKeys.CreateTimelineRetentionLine,
                [
                    outcome!.SupersededDraftIds.Count,
                    outcome.TrimmedDraftIds.Count,
                    outcome.EvictedLineageIds.Count,
                    outcome.EvictedVersionCount,
                ])
            : new SessionTimelineEntryViewModel(_localization, key, arguments));
        OnPropertyChanged(nameof(HasEntries));
    }

    /// <summary>Per-request stable codes, e.g. <c>1:[E101, E102]; 2:[]</c>. Codes are protocol literals.</summary>
    private static string ErrorCodeSequence(IReadOnlyList<RecipeGenerationAttempt> attempts) => string.Join(
        "; ",
        attempts.Select(static attempt => attempt.RequestNumber + ":[" + string.Join(", ", attempt.ErrorCodes) + "]"));

    private static string IssueCodes(IReadOnlyList<RecipeValidationIssue> issues) => issues.Count == 0
        ? "-"
        : string.Join(", ", issues.Select(static issue => issue.Code).Distinct(StringComparer.Ordinal));

    private static string GuardPaths(IReadOnlyList<RecipeRefinementGuardRestoration> restorations) => restorations.Count == 0
        ? "-"
        : string.Join(", ", restorations.Select(static restoration => restoration.ParameterPath));
}
