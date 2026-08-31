using System.ComponentModel;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Desktop.Localization;
using VFXComposer.Desktop.Services;

namespace VFXComposer.Desktop.Tests;

[TestClass]
public sealed class LocalizationServiceTests
{
    [TestMethod]
    public void IndexerResolvesTheCurrentLanguage()
    {
        var service = LocalizationTestSupport.CreateEnglish();

        Assert.AreEqual(UiLanguage.English, service.Language);
        Assert.AreEqual(
            LocalizationTestSupport.English(UiStringKeys.SettingsTitle),
            service[UiStringKeys.SettingsTitle]);

        service.SetLanguage(UiLanguage.ChineseSimplified);

        Assert.AreEqual(
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.SettingsTitle),
            service[UiStringKeys.SettingsTitle]);
    }

    [TestMethod]
    public void UnknownKeysFailClosedInsteadOfFallingBack()
    {
        var service = LocalizationTestSupport.CreateEnglish();

        Assert.ThrowsExactly<KeyNotFoundException>(() => _ = service["NotACatalogKey"]);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LocalizationService((UiLanguage)42));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            LocalizationTestSupport.CreateEnglish().SetLanguage((UiLanguage)42));
    }

    [TestMethod]
    public void SwitchingLanguageNotifiesBothTheIndexerAndTheEvent()
    {
        var service = LocalizationTestSupport.CreateEnglish();
        var notifications = new List<string?>();
        var languageChanged = 0;
        ((INotifyPropertyChanged)service).PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        service.LanguageChanged += (_, _) => languageChanged++;

        service.SetLanguage(UiLanguage.ChineseSimplified);

        Assert.AreEqual(1, languageChanged);
        CollectionAssert.Contains(notifications, "Item");
        CollectionAssert.Contains(notifications, "Item[]");
        CollectionAssert.Contains(notifications, nameof(LocalizationService.Language));
    }

    [TestMethod]
    public void SelectingTheActiveLanguageIsANoOp()
    {
        var store = new RecordingPreferencesStore();
        var service = new LocalizationService(UiLanguage.English, store);
        var notifications = 0;
        var languageChanged = 0;
        ((INotifyPropertyChanged)service).PropertyChanged += (_, _) => notifications++;
        service.LanguageChanged += (_, _) => languageChanged++;

        service.SetLanguage(UiLanguage.English);

        Assert.AreEqual(0, notifications);
        Assert.AreEqual(0, languageChanged);
        Assert.AreEqual(0, store.Saved.Count);
    }

    [TestMethod]
    public void SwitchingLanguagePersistsTheChoice()
    {
        var store = new RecordingPreferencesStore();
        var service = new LocalizationService(UiLanguage.English, store);

        service.SetLanguage(UiLanguage.ChineseSimplified);

        Assert.AreEqual(1, store.Saved.Count);
        Assert.AreEqual(UiLanguage.ChineseSimplified, store.Saved[0].Language);
    }

    [TestMethod]
    public void FormatKeepsStableCodesVerbatimInEveryLanguage()
    {
        var service = LocalizationTestSupport.CreateEnglish();

        Assert.AreEqual(
            "Read rejected: U4FS001",
            service.Format(UiStringKeys.MainWindowReadRejected, "U4FS001"));

        service.SetLanguage(UiLanguage.ChineseSimplified);

        StringAssert.Contains(service.Format(UiStringKeys.MainWindowReadRejected, "U4FS001"), "U4FS001");
    }

    [TestMethod]
    public void FirstRunFollowsTheOperatingSystemUiCulture()
    {
        Assert.AreEqual(UiLanguage.ChineseSimplified, UiLanguages.FromUiCulture(new CultureInfo("zh-CN")));
        Assert.AreEqual(UiLanguage.ChineseSimplified, UiLanguages.FromUiCulture(new CultureInfo("zh-Hans")));
        Assert.AreEqual(UiLanguage.ChineseSimplified, UiLanguages.FromUiCulture(new CultureInfo("zh-TW")));
        Assert.AreEqual(UiLanguage.English, UiLanguages.FromUiCulture(new CultureInfo("en-US")));
        Assert.AreEqual(UiLanguage.English, UiLanguages.FromUiCulture(new CultureInfo("de-DE")));
        Assert.AreEqual(UiLanguage.English, UiLanguages.FromUiCulture(CultureInfo.InvariantCulture));
    }

    private sealed class RecordingPreferencesStore : IUiPreferencesStore
    {
        public List<UiPreferences> Saved { get; } = [];

        public UiPreferences? Load() => null;

        public void Save(UiPreferences preferences) => Saved.Add(preferences);
    }
}
