using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using VFXComposer.Desktop.Services;

namespace VFXComposer.Desktop.Localization;

/// <summary>
/// Single source of UI language for the shell. XAML binds through the string indexer; view models that hold derived
/// text subscribe to <see cref="LanguageChanged"/>.
/// </summary>
public sealed class LocalizationService : ObservableObject
{
    // Avalonia invalidates an indexer binding on the plain indexer property name, while "Item[]" is the CLR/WPF
    // convention. Both are raised so no bound label can keep stale text after a switch.
    private const string IndexerPropertyName = "Item";
    private const string IndexerCollectionPropertyName = "Item[]";

    private readonly IUiPreferencesStore? _preferences;
    private UiLanguage _language;

    public LocalizationService(UiLanguage language = UiLanguage.English, IUiPreferencesStore? preferences = null)
    {
        ThrowIfUnsupported(language);
        _language = language;
        _preferences = preferences;
    }

    /// <summary>Raised after <see cref="Language"/> changed, for view models that cache derived text.</summary>
    public event EventHandler? LanguageChanged;

    public UiLanguage Language => _language;

    public string this[string key] => UiStringCatalog.Resolve(_language, key);

    /// <summary>Formats a catalog template. Stable codes and technical identifiers are passed through as arguments.</summary>
    public string Format(string key, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return string.Format(CultureInfo.CurrentCulture, this[key], arguments);
    }

    public void SetLanguage(UiLanguage language)
    {
        ThrowIfUnsupported(language);
        if (_language == language)
        {
            return;
        }

        _language = language;
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(IndexerPropertyName);
        OnPropertyChanged(IndexerCollectionPropertyName);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
        if (_preferences is not null)
        {
            // The document carries more than the language (REQ-004 §5.3), so the save merges into the stored
            // preferences instead of rebuilding them from this one field: switching the language must never reset
            // the stored generation mode. An absent or unusable document falls back to the defaults, which is
            // exactly what the next start would read from it anyway.
            var stored = _preferences.Load() ?? new UiPreferences(language);
            _preferences.Save(stored with { Language = language });
        }
    }

    private static void ThrowIfUnsupported(UiLanguage language)
    {
        if (!UiStringCatalog.Languages.Contains(language))
        {
            throw new ArgumentOutOfRangeException(nameof(language));
        }
    }
}
