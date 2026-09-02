using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Chat;
using VFXComposer.AI.Contracts.Desktop;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Recipes;
using VFXComposer.Desktop.Localization;
using VFXComposer.Desktop.Services;
using VFXComposer.Desktop.ViewModels;
using VFXComposer.Desktop.Views;

namespace VFXComposer.Desktop.Tests;

/// <summary>
/// The generation-mode switch and the Create-page gating (F8b4, REQ-004-01/07): switching applies immediately,
/// persists as a /2 document and has zero side effects (AC-19 â€?no request, no draft byte changes); professional
/// mode shows the gated sections while simple mode hides them without hiding the example cards or the AI entry.
/// </summary>
[TestClass]
public sealed class ModeGatingTests
{
    private string _rootDirectory = string.Empty;

    [ClassInitialize]
    public static void InitializeAvalonia(TestContext _) => AvaloniaTestPlatform.EnsureInitialized();

    [TestInitialize]
    public void CreateRootDirectory() => _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        "vfxcomposer-mode-gating-tests",
        Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void RemoveRootDirectory()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void SwitchingModesHasZeroSideEffectsOnDraftsAndTheGateway()
    {
        // AC-19: three lineages with several versions exist; five round-trip switches leave the store file
        // byte-identical, reach no gateway, and persist the preference round-trip consistently.
        var runtime = CreateRuntime();
        var preferences = new UiPreferencesStore(PreferencesDirectory);
        var modes = new GenerationModeService(GenerationMode.Simple, preferences);
        var viewModel = NewCreatePage(runtime, modes);
        viewModel.ApplyPresetCommand.Execute(Card(viewModel, "fire-bolt"));
        Edit(viewModel, "1.5");
        viewModel.ApplyPresetCommand.Execute(Card(viewModel, "shock-impact"));
        viewModel.ApplyPresetCommand.Execute(Card(viewModel, "launch-flash"));
        Edit(viewModel, "1.1");
        var storeBytesBefore = File.ReadAllBytes(StorePath);

        for (var round = 0; round < 5; round++)
        {
            modes.SetMode(GenerationMode.Professional);
            modes.SetMode(GenerationMode.Simple);
        }

        modes.SetMode(GenerationMode.Professional);

        CollectionAssert.AreEqual(
            storeBytesBefore,
            File.ReadAllBytes(StorePath),
            "A mode switch never touches a draft record, not even updatedUtc.");
        runtime.AssertNoGatewayTraffic();
        var persisted = new UiPreferencesStore(PreferencesDirectory).Load();
        Assert.IsNotNull(persisted);
        Assert.AreEqual(GenerationMode.Professional, persisted.GenerationMode, "Every switch persisted immediately.");
        StringAssert.Contains(
            File.ReadAllText(Path.Combine(PreferencesDirectory, "ui-preferences.json")),
            UiPreferencesCodec.SchemaId);
    }

    [TestMethod]
    public void ProfessionalModeShowsTheGatedSectionsAndSimpleModeHidesThemWithoutHidingTheSimpleSurface()
    {
        var runtime = CreateRuntime();
        var modes = new GenerationModeService();
        var viewModel = NewCreatePage(runtime, modes);
        var view = new CreateView { DataContext = viewModel };
        viewModel.ApplyPresetCommand.Execute(Card(viewModel, "fire-bolt"));

        // Simple mode: the head exists, but the professional sections stay hidden.
        Assert.IsTrue(viewModel.ParameterPanel.HasHead, "The gate hides presentation, not data.");
        Assert.IsTrue(viewModel.Lineage.IsCardVisible);
        Assert.IsFalse(viewModel.IsParameterPanelVisible);
        Assert.IsFalse(viewModel.IsLineageVisible);
        var rendered = RenderedText(view);
        CollectionAssert.Contains(rendered, LocalizationTestSupport.English(UiStringKeys.CreateSimpleModeHeading));
        CollectionAssert.Contains(rendered, LocalizationTestSupport.English(UiStringKeys.CreateGenerateRecipeHeading));
        Assert.IsFalse(SectionCard(view, UiStringKeys.CreateParameterPanelHeading).IsVisible);
        Assert.IsFalse(SectionCard(view, UiStringKeys.CreateLineageHeading).IsVisible);

        modes.SetMode(GenerationMode.Professional);

        // Professional mode adds the gated sections and hides nothing of the simple surface (REQ-004-07).
        Assert.IsTrue(viewModel.IsParameterPanelVisible);
        Assert.IsTrue(viewModel.IsLineageVisible);
        Assert.IsTrue(SectionCard(view, UiStringKeys.CreateParameterPanelHeading).IsVisible);
        Assert.IsTrue(SectionCard(view, UiStringKeys.CreateLineageHeading).IsVisible);
        rendered = RenderedText(view);
        CollectionAssert.Contains(rendered, LocalizationTestSupport.English(UiStringKeys.CreateSimpleModeHeading));
        CollectionAssert.Contains(rendered, LocalizationTestSupport.English(UiStringKeys.CreateGenerateRecipeHeading));
        Assert.IsTrue(viewModel.PresetCards.Count >= 4, "The example cards stay in professional mode.");
        Assert.IsTrue(viewModel.GenerateRecipeCommand.CanExecute(null) || string.IsNullOrEmpty(viewModel.EffectDescription));

        modes.SetMode(GenerationMode.Simple);

        Assert.IsFalse(viewModel.IsParameterPanelVisible, "Switching back re-gates immediately.");
        Assert.IsFalse(viewModel.IsLineageVisible);
        runtime.AssertNoGatewayTraffic();
    }

    [TestMethod]
    public void TheGatedSectionsStillRequireTheirOwnCondition()
    {
        // Professional mode alone shows nothing: without a head there is no panel and no chain to render.
        var runtime = CreateRuntime();
        var viewModel = NewCreatePage(runtime, new GenerationModeService(GenerationMode.Professional));

        Assert.IsFalse(viewModel.ParameterPanel.HasHead);
        Assert.IsFalse(viewModel.IsParameterPanelVisible);
        Assert.IsFalse(viewModel.IsLineageVisible);

        viewModel.ApplyPresetCommand.Execute(Card(viewModel, "fire-bolt"));

        Assert.IsTrue(viewModel.IsParameterPanelVisible);
        Assert.IsTrue(viewModel.IsLineageVisible);
    }

    [TestMethod]
    public void TheModeSwitchNotifiesTheGatedVisibilityProperties()
    {
        var runtime = CreateRuntime();
        var modes = new GenerationModeService();
        var viewModel = NewCreatePage(runtime, modes);
        viewModel.ApplyPresetCommand.Execute(Card(viewModel, "fire-bolt"));
        var notifications = new List<string?>();
        ((INotifyPropertyChanged)viewModel).PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        modes.SetMode(GenerationMode.Professional);

        CollectionAssert.Contains(notifications, nameof(CreateViewModel.IsParameterPanelVisible));
        CollectionAssert.Contains(notifications, nameof(CreateViewModel.IsLineageVisible));
    }

    [TestMethod]
    public void TheSettingsSectionAppliesImmediatelyPersistsAndFollowsTheServiceBothWays()
    {
        var preferences = new RecordingPreferencesStore();
        var modes = new GenerationModeService(GenerationMode.Simple, preferences);
        var settings = new SettingsViewModel(LocalizationTestSupport.CreateEnglish(), generationModes: modes);

        Assert.IsTrue(settings.IsSimpleModeSelected);
        Assert.IsFalse(settings.IsProfessionalModeSelected);

        settings.IsProfessionalModeSelected = true;

        Assert.AreEqual(GenerationMode.Professional, modes.Mode);
        Assert.IsTrue(settings.IsProfessionalModeSelected);
        Assert.IsFalse(settings.IsSimpleModeSelected);
        Assert.AreEqual(GenerationMode.Professional, preferences.Saved.Single().GenerationMode);

        // Selecting the active mode again is a no-op: nothing is re-saved.
        settings.IsProfessionalModeSelected = true;
        Assert.AreEqual(1, preferences.Saved.Count);

        // A change through the service (another page, a stored preference) reflects back into the section.
        var notifications = new List<string?>();
        ((INotifyPropertyChanged)settings).PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        modes.SetMode(GenerationMode.Simple);
        Assert.IsTrue(settings.IsSimpleModeSelected);
        CollectionAssert.Contains(notifications, nameof(SettingsViewModel.IsSimpleModeSelected));
        CollectionAssert.Contains(notifications, nameof(SettingsViewModel.IsProfessionalModeSelected));
    }

    [TestMethod]
    public void SwitchingTheModeNeverResetsTheStoredLanguageAndViceVersa()
    {
        // Both services merge into the one /2 document, so either switch preserves the other stored choice.
        var preferences = new UiPreferencesStore(PreferencesDirectory);
        var localization = new LocalizationService(UiLanguage.English, preferences);
        var modes = new GenerationModeService(GenerationMode.Simple, preferences);

        localization.SetLanguage(UiLanguage.ChineseSimplified);
        modes.SetMode(GenerationMode.Professional);

        var afterMode = preferences.Load();
        Assert.IsNotNull(afterMode);
        Assert.AreEqual(UiLanguage.ChineseSimplified, afterMode.Language);
        Assert.AreEqual(GenerationMode.Professional, afterMode.GenerationMode);

        localization.SetLanguage(UiLanguage.English);

        var afterLanguage = preferences.Load();
        Assert.IsNotNull(afterLanguage);
        Assert.AreEqual(UiLanguage.English, afterLanguage.Language);
        Assert.AreEqual(GenerationMode.Professional, afterLanguage.GenerationMode, "The language switch kept the mode.");
    }

    [TestMethod]
    public void TheModeServiceRejectsAnUndeclaredMode()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GenerationModeService((GenerationMode)42));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GenerationModeService().SetMode((GenerationMode)42));
    }

    private string PreferencesDirectory => Path.Combine(_rootDirectory, "preferences");

    private string StorePath => Path.Combine(_rootDirectory, "recipe-drafts.json");

    private CountingLineageRuntime CreateRuntime() => new(StorePath);

    private static CreateViewModel NewCreatePage(CountingLineageRuntime runtime, GenerationModeService modes) =>
        new(LocalizationTestSupport.CreateEnglish(), runtime, modes);

    private static PresetCardViewModel Card(CreateViewModel viewModel, string presetId) =>
        viewModel.PresetCards.Single(card => string.Equals(card.Skeleton.PresetId, presetId, StringComparison.Ordinal));

    private static void Edit(CreateViewModel viewModel, string scale)
    {
        viewModel.ParameterPanel.Modules
            .Single(static module => module.ModuleId == "core")
            .Parameters.Single(static row => row.Name == "scale")
            .EditText = scale;
        viewModel.ApplyParameterEditsCommand.Execute(null);
    }

    private static Border SectionCard(Control view, string headingKey)
    {
        var heading = LocalizationTestSupport.English(headingKey);
        return view.GetLogicalDescendants()
            .OfType<TextBlock>()
            .Single(block => string.Equals(block.Text, heading, StringComparison.Ordinal))
            .GetLogicalAncestors()
            .OfType<Border>()
            .First(static border => border.Classes.Contains("card"));
    }

    private static List<string?> RenderedText(Control view) => view
        .GetLogicalDescendants()
        .OfType<TextBlock>()
        .Where(static block => block.IsVisible)
        .Select(static block => block.Text)
        .ToList();

    private sealed class RecordingPreferencesStore : IUiPreferencesStore
    {
        public List<UiPreferences> Saved { get; } = [];

        public UiPreferences? Load() => Saved.Count == 0 ? null : Saved[^1];

        public void Save(UiPreferences preferences) => Saved.Add(preferences);
    }

    /// <summary>The real lineage store behind a counting gateway; the mode switch must never reach either channel.</summary>
    private sealed class CountingLineageRuntime : IAiDesktopRuntime, IAiGateway, IRecipeGenerationChannel
    {
        public CountingLineageRuntime(string storePath)
        {
            Store = new RecipeDraftStore(storePath);
        }

        public RecipeDraftStore Store { get; }
        public int GenerateCalls { get; private set; }
        public int ChatCalls { get; private set; }
        public int ImageCalls { get; private set; }

        public IAiGateway Gateway => this;
        public IAiDesktopSettings Settings => throw new NotSupportedException();
        public IRecipeGenerationChannel RecipeGeneration => this;
        public IRecipeDraftLineageStore RecipeDrafts => Store;

        public void AssertNoGatewayTraffic()
        {
            Assert.AreEqual(0, GenerateCalls, "A mode switch must never start a generation request.");
            Assert.AreEqual(0, ChatCalls, "A mode switch must never send a chat request.");
            Assert.AreEqual(0, ImageCalls, "A mode switch must never send an image request.");
        }

        public ValueTask<RecipeGenerationResult> GenerateAsync(
            RecipeGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            GenerateCalls++;
            return ValueTask.FromException<RecipeGenerationResult>(new AiGatewayException(AiErrorCode.ConfigurationUnavailable));
        }

        public ValueTask<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
        {
            ChatCalls++;
            return ValueTask.FromException<ChatResponse>(new AiGatewayException(AiErrorCode.ConfigurationUnavailable));
        }

        public ValueTask<ImageGenerationResponse> GenerateImageAsync(
            ImageGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            ImageCalls++;
            return ValueTask.FromException<ImageGenerationResponse>(new AiGatewayException(AiErrorCode.ConfigurationUnavailable));
        }

        public ValueTask<Stream> OpenImageArtifactAsync(string privateArtifactId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
