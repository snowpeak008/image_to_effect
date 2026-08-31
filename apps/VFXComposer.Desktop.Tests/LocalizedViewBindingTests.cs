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

    // Views whose user-visible text is bound through the indexer. Every other view derives its text from the page
    // view model, so a missing entry here cannot hide an unresolved key: the scan below covers the whole project.
    private static readonly string[] IndexerBoundViews =
    [
        "MainWindow.axaml",
        "DashboardView.axaml",
        "SettingsView.axaml",
        "CreateView.axaml",
        "PreviewView.axaml",
        "JobsView.axaml",
    ];

    [ClassInitialize]
    public static void InitializeAvalonia(TestContext _) => AvaloniaTestPlatform.EnsureInitialized();

    [TestMethod]
    public void EveryIndexerBindingInEveryViewResolvesACatalogKey()
    {
        var boundByView = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var file in LocalizationTestSupport.DesktopMarkupFiles())
        {
            var markup = LocalizationTestSupport.ReadProjectText(file);
            foreach (var match in IndexerBindingPattern.Matches(markup).Cast<Match>())
            {
                var key = match.Groups["key"].Value;
                boundByView[file.Name] = boundByView.GetValueOrDefault(file.Name) + 1;
                foreach (var language in UiStringCatalog.Languages)
                {
                    Assert.IsTrue(
                        UiStringCatalog.For(language).ContainsKey(key),
                        $"{file.Name} binds {key}, which {language} does not carry.");
                }
            }
        }

        foreach (var view in IndexerBoundViews)
        {
            Assert.IsTrue(
                boundByView.ContainsKey(view),
                $"{view} carries no indexer binding, so its text is no longer localized.");
        }

        Assert.IsTrue(
            boundByView.Values.Sum() > 70,
            $"Only {boundByView.Values.Sum()} indexer bindings were found across the shell's views.");
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

    [TestMethod]
    public void CreatePageTextIsRerenderedWhenTheLanguageChanges()
    {
        var localization = LocalizationTestSupport.CreateEnglish();
        var view = new CreateView { DataContext = new CreateViewModel(localization) };
        var heading = LocalizationTestSupport.English(UiStringKeys.CreateGenerateRecipeHeading);

        CollectionAssert.Contains(RenderedText(view), heading);

        localization.SetLanguage(UiLanguage.ChineseSimplified);

        var rendered = RenderedText(view);
        CollectionAssert.DoesNotContain(rendered, heading);
        CollectionAssert.Contains(
            rendered,
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.CreateGenerateRecipeHeading));
        CollectionAssert.Contains(
            rendered,
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.CreateChatStatusNotConfigured));
    }

    [TestMethod]
    public void PreviewPageTextIsRerenderedWhenTheLanguageChanges()
    {
        var localization = LocalizationTestSupport.CreateEnglish();
        using var page = new PreviewViewModel(localization);
        var view = new PreviewView { DataContext = page };
        var label = LocalizationTestSupport.English(UiStringKeys.PreviewWidthLabel);

        CollectionAssert.Contains(RenderedText(view), label);

        localization.SetLanguage(UiLanguage.ChineseSimplified);

        var rendered = RenderedText(view);
        CollectionAssert.DoesNotContain(rendered, label);
        CollectionAssert.Contains(
            rendered,
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.PreviewWidthLabel));
        CollectionAssert.Contains(
            rendered,
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.PreviewImageStatusNotConfigured));
    }

    [TestMethod]
    public void JobsPageTextIsRerenderedWhenTheLanguageChanges()
    {
        var localization = LocalizationTestSupport.CreateEnglish();
        var view = new JobsView { DataContext = new JobsViewModel(localization) };
        var heading = LocalizationTestSupport.English(UiStringKeys.JobsTimelineHeading);

        CollectionAssert.Contains(RenderedText(view), heading);

        localization.SetLanguage(UiLanguage.ChineseSimplified);

        var rendered = RenderedText(view);
        CollectionAssert.DoesNotContain(rendered, heading);
        CollectionAssert.Contains(
            rendered,
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.JobsTimelineHeading));
        CollectionAssert.Contains(
            rendered,
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.JobsQueueIdle));
    }

    [TestMethod]
    public void LibraryPatchAndReviewPageTextIsRerenderedWhenTheLanguageChanges()
    {
        var localization = LocalizationTestSupport.CreateEnglish();
        var library = new LibraryView { DataContext = new LibraryViewModel(localization) };
        var patch = new PatchView { DataContext = new PatchViewModel(localization) };
        var review = new ReviewView { DataContext = new ReviewViewModel(localization) };

        CollectionAssert.Contains(
            RenderedText(review),
            LocalizationTestSupport.English(UiStringKeys.ReviewAuthorityNotice));

        localization.SetLanguage(UiLanguage.ChineseSimplified);

        CollectionAssert.Contains(
            RenderedText(library),
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.LibraryTitle));
        CollectionAssert.Contains(
            RenderedText(patch),
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.PatchEmptyState));
        var renderedReview = RenderedText(review);
        CollectionAssert.Contains(
            renderedReview,
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.ReviewAuthorityNotice));
        CollectionAssert.Contains(
            renderedReview,
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.ReviewMachineStatus));
    }

    private static List<string?> RenderedText(Control view) => view
        .GetLogicalDescendants()
        .OfType<TextBlock>()
        .Select(block => block.Text)
        .ToList();
}
