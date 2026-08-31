namespace VFXComposer.Desktop.Localization;

/// <summary>
/// Closed set of shipped UI languages. A new member is only valid once <see cref="UiStringCatalog"/> carries a
/// complete translation for it: the catalog parity test fails otherwise.
/// </summary>
public enum UiLanguage
{
    English = 0,
    ChineseSimplified = 1,
}
