using CommunityToolkit.Mvvm.ComponentModel;
using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.ViewModels;

/// <summary>
/// One session-timeline row (REQ-004-20): a catalog sentence whose arguments are protocol literals only — counts,
/// stable codes, version ids, origins, prompt template versions. Feedback text, descriptions, prompts and endpoints
/// never reach an entry (REQ-004-21); the owning page hands in nothing else.
/// </summary>
public sealed class SessionTimelineEntryViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly string _key;
    private readonly object?[] _arguments;
    private readonly string? _retentionKey;
    private readonly object?[] _retentionArguments;

    internal SessionTimelineEntryViewModel(
        LocalizationService localization,
        string key,
        object?[] arguments,
        string? retentionKey = null,
        object?[]? retentionArguments = null)
    {
        _localization = localization;
        _key = key;
        _arguments = arguments;
        _retentionKey = retentionKey;
        _retentionArguments = retentionArguments ?? [];
    }

    /// <summary>The rendered entry: the action line, plus the folded retention line when the save trimmed anything.</summary>
    public string Text
    {
        get
        {
            var line = _arguments.Length == 0 ? _localization[_key] : _localization.Format(_key, _arguments);
            return _retentionKey is null
                ? line
                : line + "\n" + _localization.Format(_retentionKey, _retentionArguments);
        }
    }

    public override string ToString() => "SessionTimelineEntryViewModel(" + _key + ")";

    internal void RefreshLocalizedText() => OnPropertyChanged(nameof(Text));
}
