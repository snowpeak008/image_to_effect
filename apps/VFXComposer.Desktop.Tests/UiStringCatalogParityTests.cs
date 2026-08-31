using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.Tests;

[TestClass]
public sealed class UiStringCatalogParityTests
{
    // Values that are deliberately identical in both languages: a product name, and language options that always
    // name themselves in their own language.
    private static readonly string[] IdenticalByDesignKeys =
    [
        UiStringKeys.AppProductName,
        UiStringKeys.SettingsLanguageChineseSimplifiedOption,
        UiStringKeys.SettingsLanguageEnglishOption,
    ];

    private static readonly Regex PlaceholderPattern = new(@"\{(\d+)\}", RegexOptions.CultureInvariant);

    [TestMethod]
    public void EveryKeyConstantEqualsItsOwnName()
    {
        foreach (var field in DeclaredKeyFields())
        {
            Assert.AreEqual(
                field.Name,
                (string?)field.GetRawConstantValue(),
                $"XAML indexer bindings spell the constant name, so {field.Name} must equal its own value.");
        }
    }

    [TestMethod]
    public void BothLanguagesCarryExactlyTheDeclaredKeys()
    {
        var declared = DeclaredKeys();
        Assert.AreEqual(2, UiStringCatalog.Languages.Count);

        foreach (var language in UiStringCatalog.Languages)
        {
            CollectionAssert.AreEquivalent(
                declared,
                UiStringCatalog.For(language).Keys.ToArray(),
                $"{language} must carry exactly the declared key set.");
        }
    }

    [TestMethod]
    public void PlaceholderSetsAreIdenticalAcrossLanguages()
    {
        foreach (var key in DeclaredKeys())
        {
            CollectionAssert.AreEqual(
                Placeholders(UiStringCatalog.Resolve(UiLanguage.English, key)),
                Placeholders(UiStringCatalog.Resolve(UiLanguage.ChineseSimplified, key)),
                $"{key} must format the same arguments in every language.");
        }
    }

    [TestMethod]
    public void EveryValueIsPresentAndTranslated()
    {
        foreach (var key in DeclaredKeys())
        {
            var english = UiStringCatalog.Resolve(UiLanguage.English, key);
            var chinese = UiStringCatalog.Resolve(UiLanguage.ChineseSimplified, key);

            Assert.IsFalse(string.IsNullOrWhiteSpace(english), $"{key} has no English value.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(chinese), $"{key} has no Chinese value.");
            Assert.AreNotEqual(key, english, $"{key} still holds its key as a value.");
            Assert.AreNotEqual(key, chinese, $"{key} still holds its key as a value.");

            if (IdenticalByDesignKeys.Contains(key, StringComparer.Ordinal))
            {
                Assert.AreEqual(english, chinese, $"{key} is declared identical in both languages.");
                continue;
            }

            Assert.AreNotEqual(english, chinese, $"{key} is untranslated.");
            Assert.IsTrue(
                chinese.Any(character => character >= '\u4e00' && character <= '\u9fff'),
                $"{key} carries no Chinese text.");
        }
    }

    [TestMethod]
    public void ResolveRejectsUnknownAndBlankKeys()
    {
        Assert.ThrowsExactly<KeyNotFoundException>(() =>
            UiStringCatalog.Resolve(UiLanguage.English, "NotACatalogKey"));
        Assert.ThrowsExactly<ArgumentException>(() =>
            UiStringCatalog.Resolve(UiLanguage.English, "  "));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            UiStringCatalog.For((UiLanguage)42));
    }

    private static string[] Placeholders(string value) => PlaceholderPattern
        .Matches(value)
        .Select(match => match.Groups[1].Value)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static string[] DeclaredKeys() => DeclaredKeyFields()
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToArray();

    private static IEnumerable<FieldInfo> DeclaredKeyFields() => typeof(UiStringKeys)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string));
}
