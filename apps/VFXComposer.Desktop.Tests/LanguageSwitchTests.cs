using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.Client;
using VFXComposer.Desktop.Localization;
using VFXComposer.Desktop.Services;
using VFXComposer.Desktop.ViewModels;

namespace VFXComposer.Desktop.Tests;

[TestClass]
public sealed class LanguageSwitchTests
{
    private static readonly (string Key, string TitleKey, string DescriptionKey, string EmptyStateKey)[] Pages =
    [
        ("dashboard", UiStringKeys.DashboardTitle, UiStringKeys.DashboardDescription, UiStringKeys.DashboardEmptyState),
        ("library", UiStringKeys.LibraryTitle, UiStringKeys.LibraryDescription, UiStringKeys.LibraryEmptyState),
        ("create", UiStringKeys.CreateTitle, UiStringKeys.CreateDescription, UiStringKeys.CreateEmptyState),
        ("preview", UiStringKeys.PreviewTitle, UiStringKeys.PreviewDescription, UiStringKeys.PreviewEmptyState),
        ("patch", UiStringKeys.PatchTitle, UiStringKeys.PatchDescription, UiStringKeys.PatchEmptyState),
        ("review", UiStringKeys.ReviewTitle, UiStringKeys.ReviewDescription, UiStringKeys.ReviewEmptyState),
        ("jobs", UiStringKeys.JobsTitle, UiStringKeys.JobsDescription, UiStringKeys.JobsEmptyState),
        ("settings", UiStringKeys.SettingsTitle, UiStringKeys.SettingsDescription, UiStringKeys.SettingsEmptyState),
    ];

    [TestMethod]
    public void EveryPageHeaderFollowsALiveLanguageSwitch()
    {
        var localization = LocalizationTestSupport.CreateEnglish();
        var shell = MainWindowViewModel.CreateDisconnected(localization: localization);

        AssertPageHeaders(shell, LocalizationTestSupport.English);

        localization.SetLanguage(UiLanguage.ChineseSimplified);

        AssertPageHeaders(shell, LocalizationTestSupport.ChineseSimplified);
    }

    [TestMethod]
    public void NavigationLabelsFollowThePageTitles()
    {
        var localization = LocalizationTestSupport.CreateEnglish();
        var shell = MainWindowViewModel.CreateDisconnected(localization: localization);
        var dashboard = shell.NavigationItems[0];
        var notified = 0;
        ((INotifyPropertyChanged)dashboard).PropertyChanged += (_, args) =>
        {
            if (string.Equals(args.PropertyName, nameof(NavigationItemViewModel.Title), StringComparison.Ordinal))
            {
                notified++;
            }
        };

        localization.SetLanguage(UiLanguage.ChineseSimplified);

        Assert.AreEqual(1, notified);
        CollectionAssert.AreEqual(
            Pages.Select(page => LocalizationTestSupport.ChineseSimplified(page.TitleKey)).ToArray(),
            shell.NavigationItems.Select(item => item.Title).ToArray());
    }

    [TestMethod]
    public void ShellChromeAndConnectionTextFollowALiveLanguageSwitch()
    {
        var localization = LocalizationTestSupport.CreateEnglish();
        var shell = MainWindowViewModel.CreateDisconnected(localization: localization);
        var notifications = new List<string?>();
        ((INotifyPropertyChanged)shell).PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        localization.SetLanguage(UiLanguage.ChineseSimplified);

        Assert.AreEqual(LocalizationTestSupport.ChineseSimplified(UiStringKeys.AppProductName), shell.ProductName);
        Assert.AreEqual(
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.MainWindowConnectionDisconnected),
            shell.ConnectionDisplay);
        Assert.AreEqual(
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.MainWindowProjectNone),
            shell.ProjectDisplay);
        Assert.AreEqual(
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.MainWindowReadNone),
            shell.ReadDisplay);
        CollectionAssert.Contains(notifications, nameof(MainWindowViewModel.ConnectionDisplay));
        CollectionAssert.Contains(notifications, nameof(MainWindowViewModel.ProjectDisplay));
        CollectionAssert.Contains(notifications, nameof(MainWindowViewModel.ReadDisplay));
    }

    [TestMethod]
    public async Task AReadOutcomeIsRerenderedWithoutRepeatingTheRead()
    {
        var localization = LocalizationTestSupport.CreateEnglish();
        var session = new AcceptingSession();
        await using var shell = MainWindowViewModel.CreateUserMode(
            session,
            new UnusedSelectionDialog(),
            new ImmediateDispatcher(),
            localization: localization);
        await shell.ConnectCommand.ExecuteAsync(null);
        await shell.SelectProjectCommand.ExecuteAsync(null);
        await shell.ReadProjectCommand.ExecuteAsync(null);

        Assert.AreEqual("Read 42 bytes", shell.ReadDisplay);

        localization.SetLanguage(UiLanguage.ChineseSimplified);

        Assert.AreEqual(
            string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                LocalizationTestSupport.ChineseSimplified(UiStringKeys.MainWindowReadAccepted),
                42),
            shell.ReadDisplay);
        Assert.AreEqual(1, session.ReadCount);
        // Session state names stay verbatim: they are protocol words, not prose.
        Assert.AreEqual(UserModeDesktopSessionState.Selected.ToString(), shell.SessionDisplay);
    }

    [TestMethod]
    public void DashboardStatusDomainsFollowALiveLanguageSwitch()
    {
        var localization = LocalizationTestSupport.CreateEnglish();
        var dashboard = new DashboardViewModel(localization);

        Assert.AreEqual(LocalizationTestSupport.English(UiStringKeys.DashboardMachineStatus), dashboard.MachineStatus);

        localization.SetLanguage(UiLanguage.ChineseSimplified);

        Assert.AreEqual(
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.DashboardMachineStatus),
            dashboard.MachineStatus);
        Assert.AreEqual(
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.DashboardVisualStatus),
            dashboard.VisualStatus);
        Assert.AreEqual(
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.DashboardUserVerdictStatus),
            dashboard.UserVerdictStatus);
        Assert.AreEqual(
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.DashboardL3Status),
            dashboard.L3Status);
        Assert.AreEqual(
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.DashboardL4Status),
            dashboard.L4Status);
        // Technical identifiers survive translation.
        StringAssert.Contains(dashboard.VisualStatus, "VISUAL_PENDING");
    }

    [TestMethod]
    public async Task CreateStatusLinesFollowALiveLanguageSwitchAndKeepTheirCodes()
    {
        var localization = LocalizationTestSupport.CreateEnglish();
        var create = new CreateViewModel(localization) { ChatPrompt = "synthetic prompt" };

        Assert.AreEqual(
            LocalizationTestSupport.English(UiStringKeys.CreateChatStatusNotConfigured),
            create.ChatStatus);
        Assert.AreEqual(
            LocalizationTestSupport.English(UiStringKeys.CreateRecipeStatusInitial),
            create.RecipeStatus);

        // The unavailable runtime fails closed without a transport, so this exercises a code-carrying status line.
        await create.SendChatCommand.ExecuteAsync(null);
        localization.SetLanguage(UiLanguage.ChineseSimplified);

        Assert.AreEqual(
            LocalizationTestSupport.ChineseSimplifiedFormat(
                UiStringKeys.CreateChatStatusUnavailableWithCode,
                AiErrorCode.ConfigurationUnavailable),
            create.ChatStatus);
        Assert.AreEqual(
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.CreateRecipeStatusInitial),
            create.RecipeStatus);
        StringAssert.Contains(create.ChatStatus, nameof(AiErrorCode.ConfigurationUnavailable));
    }

    [TestMethod]
    public void SettingsLanguageSelectionAppliesImmediatelyAndPersists()
    {
        var store = new RecordingPreferencesStore();
        var localization = new LocalizationService(UiLanguage.English, store);
        var settings = new SettingsViewModel(localization);

        Assert.IsTrue(settings.IsEnglishSelected);
        Assert.IsFalse(settings.IsChineseSimplifiedSelected);

        settings.IsChineseSimplifiedSelected = true;

        Assert.AreEqual(UiLanguage.ChineseSimplified, localization.Language);
        Assert.IsTrue(settings.IsChineseSimplifiedSelected);
        Assert.IsFalse(settings.IsEnglishSelected);
        Assert.AreEqual(
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.SettingsSecurityNotice),
            settings.SecurityNotice);
        Assert.AreEqual(
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.SettingsTitle),
            settings.Title);
        Assert.AreEqual(UiLanguage.ChineseSimplified, store.Saved.Single().Language);
    }

    private static void AssertPageHeaders(MainWindowViewModel shell, Func<string, string> expected)
    {
        foreach (var page in Pages)
        {
            Assert.IsTrue(shell.TryNavigate(page.Key));
            var current = shell.CurrentPage;
            Assert.AreEqual(expected(page.TitleKey), current.Title, page.Key);
            Assert.AreEqual(expected(page.DescriptionKey), current.Description, page.Key);
            Assert.AreEqual(expected(page.EmptyStateKey), current.EmptyStateMessage, page.Key);
        }
    }

    private sealed class RecordingPreferencesStore : IUiPreferencesStore
    {
        public List<UiPreferences> Saved { get; } = [];

        public UiPreferences? Load() => null;

        public void Save(UiPreferences preferences) => Saved.Add(preferences);
    }

    private sealed class AcceptingSession : IUserModeDesktopSession
    {
        public UserModeDesktopSessionState State { get; private set; } = UserModeDesktopSessionState.Disconnected;
        public long Generation { get; private set; }
        public UserModeDesktopReadPresentation? LastRead { get; private set; }
        public int ReadCount { get; private set; }
        public event EventHandler? StateChanged;

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
        {
            Generation++;
            SetState(UserModeDesktopSessionState.ConnectedNoProject);
            return ValueTask.CompletedTask;
        }

        public ValueTask SelectAsync(string selection, CancellationToken cancellationToken = default)
        {
            SetState(UserModeDesktopSessionState.Selected);
            return ValueTask.CompletedTask;
        }

        public ValueTask<UserModeDesktopReadPresentation> ReadAsync(
            string documentKind = "LIBRARY_INDEX",
            string documentId = "project",
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            LastRead = new UserModeDesktopReadPresentation(true, documentKind, documentId, 42, null, null);
            return ValueTask.FromResult(LastRead);
        }

        public ValueTask RestartAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private void SetState(UserModeDesktopSessionState state)
        {
            State = state;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class UnusedSelectionDialog : IProjectSelectionDialog
    {
        public ValueTask<string?> SelectAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<string?>("C:\\synthetic\\selection");
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public void Post(Action action) => action();
    }
}
