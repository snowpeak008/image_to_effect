using System.Globalization;
using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.Tests;

/// <summary>
/// Tests pin semantics, not wording: every expectation is resolved through the catalog with an explicitly fixed
/// language, so translating a string can never break an unrelated test.
/// </summary>
internal static class LocalizationTestSupport
{
    public static LocalizationService CreateEnglish() => new(UiLanguage.English);

    public static LocalizationService CreateChineseSimplified() => new(UiLanguage.ChineseSimplified);

    public static string English(string key) => UiStringCatalog.Resolve(UiLanguage.English, key);

    public static string ChineseSimplified(string key) => UiStringCatalog.Resolve(UiLanguage.ChineseSimplified, key);

    public static string EnglishFormat(string key, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, English(key), arguments);

    public static string ChineseSimplifiedFormat(string key, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, ChineseSimplified(key), arguments);
}
