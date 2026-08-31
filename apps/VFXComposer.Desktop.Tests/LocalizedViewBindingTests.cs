using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Desktop.Localization;
using VFXComposer.Desktop.ViewModels;
using VFXComposer.Desktop.Views;

namespace VFXComposer.Desktop.Tests;

/// <summary>
/// Proves the XAML side of the mechanism: indexer bindings resolve real catalog keys and re-render on a live switch.
/// </summary>
[TestClass]
public sealed class LocalizedViewBindingTests
{
    private static readonly Regex IndexerBindingPattern = new(
        @"Localization\[(?<key>[A-Za-z0-9]+)\]",
        RegexOptions.CultureInvariant);

    private static readonly string[] LocalizedViews =
    [
        "MainWindow.axaml",
        "DashboardView.axaml",
        "SettingsView.axaml",
    ];

    [ClassInitialize]
    public static void InitializeAvalonia(TestContext _) => AvaloniaTestPlatform.EnsureInitialized();

    [TestMethod]
    public void EveryIndexerBindingInTheMigratedViewsResolvesACatalogKey()
    {
        var bound = new List<string>();
        foreach (var view in LocalizedViews)
        {
            var markup = ReadViewMarkup(view);
            foreach (var match in IndexerBindingPattern.Matches(markup).Cast<Match>())
            {
                var key = match.Groups["key"].Value;
                bound.Add(key);
                foreach (var language in UiStringCatalog.Languages)
                {
                    Assert.IsTrue(
                        UiStringCatalog.For(language).ContainsKey(key),
                        $"{view} binds {key}, which {language} does not carry.");
                }
            }
        }

        Assert.IsTrue(bound.Count > 40, $"Only {bound.Count} indexer bindings were found in the migrated views.");
    }

    [TestMethod]
    public void DashboardTextIsRerenderedWhenTheLanguageChanges()
    {
        var localization = LocalizationTestSupport.CreateEnglish();
        var view = new DashboardView { DataContext = new DashboardViewModel(localization) };
        var heading = LocalizationTestSupport.English(UiStringKeys.DashboardProjectConnectionHeading);

        CollectionAssert.Contains(RenderedText(view), heading);

        localization.SetLanguage(UiLanguage.ChineseSimplified);

        var rendered = RenderedText(view);
        CollectionAssert.DoesNotContain(rendered, heading);
        CollectionAssert.Contains(
            rendered,
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.DashboardProjectConnectionHeading));
        CollectionAssert.Contains(
            rendered,
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.DashboardTitle));
        CollectionAssert.Contains(
            rendered,
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.DashboardMachineStatus));
    }

    [TestMethod]
    public void SettingsLanguageSectionIsRenderedInBothLanguages()
    {
        var localization = LocalizationTestSupport.CreateEnglish();
        var view = new SettingsView { DataContext = new SettingsViewModel(localization) };

        CollectionAssert.Contains(
            RenderedText(view),
            LocalizationTestSupport.English(UiStringKeys.SettingsLanguageHeading));

        localization.SetLanguage(UiLanguage.ChineseSimplified);

        CollectionAssert.Contains(
            RenderedText(view),
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.SettingsLanguageHeading));
    }

    [TestMethod]
    public void CheckingTheLanguageOptionSwitchesTheShellLanguage()
    {
        var localization = LocalizationTestSupport.CreateEnglish();
        var view = new SettingsView { DataContext = new SettingsViewModel(localization) };
        var options = view.GetLogicalDescendants().OfType<RadioButton>().ToArray();
        var english = options.Single(option => Equals(
            option.Content,
            LocalizationTestSupport.English(UiStringKeys.SettingsLanguageEnglishOption)));
        var chinese = options.Single(option => Equals(
            option.Content,
            LocalizationTestSupport.English(UiStringKeys.SettingsLanguageChineseSimplifiedOption)));
        Assert.IsTrue(english.IsChecked);

        chinese.IsChecked = true;

        Assert.AreEqual(UiLanguage.ChineseSimplified, localization.Language);
        Assert.IsFalse(english.IsChecked);
        CollectionAssert.Contains(
            RenderedText(view),
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.SettingsProviderProfilesHeading));
    }

    private static List<string?> RenderedText(Control view) => view
        .GetLogicalDescendants()
        .OfType<TextBlock>()
        .Select(block => block.Text)
        .ToList();

    // Compiled bindings validate property paths but not indexer keys, and the compiler keeps no XAML asset to read
    // back, so the markup is read from the project itself.
    private static string ReadViewMarkup(string fileName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "VFXComposer.Desktop", "Views", fileName);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate, Encoding.UTF8);
            }
        }

        throw new AssertFailedException($"{fileName} was not found above {AppContext.BaseDirectory}.");
    }
}
