using System.Globalization;

namespace VFXComposer.Desktop.Localization;

/// <summary>
/// First-run language inference. A stored explicit preference always wins over the inferred default.
/// </summary>
public static class UiLanguages
{
    public static UiLanguage FromUiCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        // Every zh* culture maps to Simplified Chinese in v1: no traditional catalog exists, and simplified text is
        // closer to a zh-Hant reader than English.
        return string.Equals(culture.TwoLetterISOLanguageName, "zh", StringComparison.OrdinalIgnoreCase)
            ? UiLanguage.ChineseSimplified
            : UiLanguage.English;
    }

    public static UiLanguage FromCurrentUiCulture() => FromUiCulture(CultureInfo.CurrentUICulture);
}
