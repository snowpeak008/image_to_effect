using System.Globalization;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Chat;
using VFXComposer.AI.Contracts.Desktop;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Recipes;
using VFXComposer.Desktop.Localization;
using VFXComposer.Desktop.ViewModels;
using VFXComposer.Desktop.Views;

namespace VFXComposer.Desktop.Tests;

/// <summary>
/// The parameter panel (F8b3) over the real lineage store: rows render exactly the snapshot declarations, an
/// out-of-range edit is refused with path and range and lands nothing (AC-7), an in-range edit lands a human_edit
/// version and supersedes the earlier confirmation (AC-8), the AI → hand-edit → confirm loop closes, retention is
/// reported through catalog keys, and the whole surface never reaches the gateway.
/// </summary>
[TestClass]
public sealed class ParameterPanelTests
{
    private const string ScalePath = "stages[travel].modules[core].parameters.scale";

    private string _storeDirectory = string.Empty;

    [ClassInitialize]
    public static void InitializeAvalonia(TestContext _) => AvaloniaTestPlatform.EnsureInitialized();

    [TestInitialize]
    public void CreateStoreDirectory() => _storeDirectory = Path.Combine(
        Path.GetTempPath(),
        "vfxcomposer-parameter-panel-tests",
        Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void RemoveStoreDirectory()
    {
        if (Directory.Exists(_storeDirectory))
        {
            Directory.Delete(_storeDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void ThePanelIsHiddenWithoutAHeadAndRendersEveryDeclaredRowOfTheHead()
    {
        var runtime = CreateRuntime();
        var viewModel = CreateViewModel(runtime);
        Assert.IsFalse(viewModel.ParameterPanel.HasHead);
        Assert.IsFalse(viewModel.ApplyParameterEditsCommand.CanExecute(null));

        var card = viewModel.PresetCards.Single(static card => card.Skeleton.PresetId == "trailing-fireball");
        viewModel.ApplyPresetCommand.Execute(card);

        var panel = viewModel.ParameterPanel;
        var snapshot = RecipeTemplateCatalogSnapshot.Default;
        Assert.IsTrue(panel.HasHead);
        Assert.IsTrue(viewModel.ApplyParameterEditsCommand.CanExecute(null));
        Assert.AreEqual(2, panel.Modules.Count);
        Assert.AreEqual(
            card.Skeleton.TemplateIds.Sum(templateId =>
            {
                Assert.IsTrue(snapshot.TryGetTemplate(templateId, out var template));
                return template.Parameters.Count;
            }),
            panel.ParameterCount,
            "One row per declared parameter of every module.");
        foreach (var module in panel.Modules)
        {
            StringAssert.Contains(module.Header, module.TemplateId);
            foreach (var row in module.Parameters)
            {
                Assert.IsTrue(snapshot.TryGetParameter(module.TemplateId, row.Name, out var declaration));
                Assert.AreEqual(declaration.RangeLiteral, row.RangeLiteral);
                StringAssert.Contains(row.BoundsHint, declaration.RangeLiteral, "The range text comes from the snapshot literal.");
                StringAssert.Contains(row.BoundsHint, declaration.DefaultLiteral);
                Assert.AreEqual(row.CurrentValueLiteral, row.EditText, "Rows start at the head's value.");
                Assert.IsFalse(row.HasPendingEdit);
            }
        }

        Assert.AreEqual(0, panel.Warnings.Count);
        Assert.IsFalse(panel.HasWarnings);
        Assert.AreEqual(string.Empty, viewModel.RecipeRetentionNotice, "A fully retained save says nothing.");
        runtime.AssertNoGatewayTraffic();
    }

    [TestMethod]
    public void AnOutOfRangeEditIsRefusedWithPathAndRangeAndLandsNothing()
    {
        // AC-7: PFT_2D_FireCore.scale is declared in [0.6, 2.4]; 3.0 must be refused, not clamped to 2.4.
        var runtime = CreateRuntime();
        var viewModel = CreateViewModel(runtime);
        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));
        var headId = viewModel.DraftId!;
        var head = runtime.Store.TryGet(headId)!;

        ScaleRow(viewModel).EditText = "3.0";
        viewModel.ApplyParameterEditsCommand.Execute(null);

        Assert.AreEqual(
            LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateRecipeStatusParameterEditRejected, 1),
            viewModel.RecipeStatus);
        var report = viewModel.ParameterPanel.IssueReport;
        StringAssert.Contains(report, RecipeParameterEditCodes.ValueOutOfRange);
        StringAssert.Contains(report, ScalePath);
        StringAssert.Contains(report, "[0.6, 2.4]");
        StringAssert.Contains(
            report,
            LocalizationTestSupport.EnglishFormat(UiStringKeys.RecipeParameterEditValueOutOfRange, ScalePath, "3.0", "[0.6, 2.4]"));
        Assert.AreEqual(headId, viewModel.DraftId, "No new version: the head is unchanged.");
        Assert.AreEqual(1, runtime.Store.ListLineage(head.LineageId).Count);
        Assert.AreEqual(head.CanonicalSha256, runtime.Store.TryGet(headId)!.CanonicalSha256);
        Assert.AreEqual("3.0", ScaleRow(viewModel).EditText, "The refused text stays as typed; nothing is corrected.");
        runtime.AssertNoGatewayTraffic();
    }

    [TestMethod]
    public void AnInRangeEditLandsAHumanEditVersionAndSupersedesTheEarlierConfirmation()
    {
        // AC-8: the head is confirmed; a hand edit lands v2 (human_edit, pending) and the confirmation lapses.
        var runtime = CreateRuntime();
        var viewModel = CreateViewModel(runtime);
        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));
        viewModel.ConfirmRecipeDraftCommand.Execute(null);
        var confirmedId = viewModel.DraftId!;
        var confirmed = runtime.Store.TryGet(confirmedId)!;
        Assert.AreEqual(RecipeDraftStatus.ConfirmedAwaitingBuild, confirmed.Status);
        Assert.IsTrue(viewModel.ParameterPanel.HasHead, "A confirmed head is still editable; the edit lands a new version.");

        ScaleRow(viewModel).EditText = "1.5";
        viewModel.ApplyParameterEditsCommand.Execute(null);

        var newHead = runtime.Store.TryGet(viewModel.DraftId!)!;
        Assert.AreNotEqual(confirmedId, newHead.DraftId);
        Assert.AreEqual(RecipeDraftOrigin.HumanEdit, newHead.Origin);
        Assert.AreEqual(RecipeDraftStatus.PendingConfirmation, newHead.Status);
        Assert.AreEqual(confirmedId, newHead.ParentDraftId);
        Assert.AreEqual(confirmed.RevisionOrdinal + 1, newHead.RevisionOrdinal);
        Assert.AreEqual(confirmed.LineageId, newHead.LineageId);
        Assert.AreEqual(RecipeParameterEditor.HumanEditPromptTemplateVersion, newHead.PromptTemplateVersion);
        Assert.AreEqual(0, newHead.RequestCount);
        Assert.AreEqual(RecipeDraftStatus.PendingConfirmation, viewModel.DraftStatus);
        Assert.IsTrue(viewModel.ConfirmRecipeDraftCommand.CanExecute(null));

        var superseded = runtime.Store.TryGet(confirmedId)!;
        Assert.AreEqual(RecipeDraftStatus.Superseded, superseded.Status);
        Assert.AreEqual(confirmed.RecipeJson, superseded.RecipeJson, "The superseded version's content is byte-identical.");
        Assert.AreEqual(confirmed.CanonicalSha256, superseded.CanonicalSha256);
        Assert.AreEqual(0, runtime.Store.ListConfirmedAwaitingBuild().Count, "A superseded confirmation leaves the build backlog.");

        Assert.AreEqual(
            LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateRecipeStatusHumanEditSaved, newHead.RevisionOrdinal),
            viewModel.RecipeStatus);
        Assert.AreEqual(
            LocalizationTestSupport.English(UiStringKeys.CreateRetentionNoticeSuperseded),
            viewModel.RecipeRetentionNotice);
        Assert.IsTrue(viewModel.HasRetentionNotice);
        Assert.AreEqual(LocalizationTestSupport.English(UiStringKeys.CreateValidationPassed), viewModel.RecipeValidationSummary);
        Assert.AreEqual("1.5", ScaleRow(viewModel).CurrentValueLiteral, "The panel re-renders from the new head.");
        StringAssert.Contains(viewModel.RecipeDraftJson, "1.5");

        // Confirming the new head saves no version, so the Superseded line of the previous save is retired.
        viewModel.ConfirmRecipeDraftCommand.Execute(null);
        Assert.AreEqual(RecipeDraftStatus.ConfirmedAwaitingBuild, viewModel.DraftStatus);
        Assert.IsFalse(viewModel.HasRetentionNotice);
        Assert.AreEqual(string.Empty, viewModel.RecipeRetentionNotice);
        runtime.AssertNoGatewayTraffic();
    }

    [TestMethod]
    public async Task TheLoopClosesFromAiDraftThroughHandEditToConfirmation()
    {
        var runtime = CreateRuntime(DraftedFireBolt);
        var viewModel = CreateViewModel(runtime);
        viewModel.EffectDescription = "a synthetic fire bolt";

        await viewModel.GenerateRecipeCommand.ExecuteAsync(null);
        Assert.AreEqual(1, runtime.GenerateCalls);
        var root = runtime.Store.TryGet(viewModel.DraftId!)!;
        Assert.AreEqual(RecipeDraftOrigin.AiDraft, root.Origin);
        Assert.AreEqual(1, viewModel.ParameterPanel.ParameterCount);

        ScaleRow(viewModel).EditText = "0.8";
        viewModel.ApplyParameterEditsCommand.Execute(null);
        var edited = runtime.Store.TryGet(viewModel.DraftId!)!;
        Assert.AreEqual(RecipeDraftOrigin.HumanEdit, edited.Origin);
        Assert.AreEqual(root.DraftId, edited.ParentDraftId);

        viewModel.ConfirmRecipeDraftCommand.Execute(null);

        var confirmed = runtime.Store.TryGet(edited.DraftId)!;
        Assert.AreEqual(RecipeDraftStatus.ConfirmedAwaitingBuild, confirmed.Status);
        Assert.AreEqual(edited.CanonicalSha256, confirmed.CanonicalSha256, "Confirmation binds to the hand-edited version's hash.");
        CollectionAssert.AreEqual(
            new[] { edited.DraftId },
            runtime.Store.ListConfirmedAwaitingBuild().Select(static record => record.DraftId).ToArray());
        Assert.AreEqual(RecipeDraftStatus.PendingConfirmation, runtime.Store.TryGet(root.DraftId)!.Status, "The AI root is untouched.");
        Assert.AreEqual(1, runtime.GenerateCalls, "The hand edit and the confirmation made no further request.");
        Assert.AreEqual(0, runtime.ChatCalls);
    }

    [TestMethod]
    public void ALevelOneTrimIsReportedThroughTheRetentionNoticeAndSilenceOtherwise()
    {
        var runtime = CreateRuntime();
        var viewModel = CreateViewModel(runtime);
        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));
        var lineageId = runtime.Store.TryGet(viewModel.DraftId!)!.LineageId;

        // Grow to exactly the level-1 cap through the panel: every edit changes the value, none trims.
        for (var ordinal = 2; ordinal <= RecipeDraftLineageLimits.MaximumVersionsPerLineage; ordinal++)
        {
            ScaleRow(viewModel).EditText = (0.6 + 0.1 * (ordinal - 1)).ToString("0.0", CultureInfo.InvariantCulture);
            viewModel.ApplyParameterEditsCommand.Execute(null);
            Assert.AreEqual(
                LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateRecipeStatusHumanEditSaved, ordinal),
                viewModel.RecipeStatus);
            Assert.AreEqual(string.Empty, viewModel.RecipeRetentionNotice, "Nothing was trimmed, so nothing is announced.");
            Assert.IsFalse(viewModel.HasRetentionNotice);
        }

        Assert.AreEqual(RecipeDraftLineageLimits.MaximumVersionsPerLineage, runtime.Store.ListLineage(lineageId).Count);

        ScaleRow(viewModel).EditText = "2.3";
        viewModel.ApplyParameterEditsCommand.Execute(null);

        Assert.AreEqual(
            LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateRetentionNoticeTrimmed, 1),
            viewModel.RecipeRetentionNotice);
        Assert.IsTrue(viewModel.HasRetentionNotice);
        Assert.AreEqual(RecipeDraftLineageLimits.MaximumVersionsPerLineage, runtime.Store.ListLineage(lineageId).Count);
        Assert.AreEqual(RecipeDraftLineageLimits.MaximumVersionsPerLineage + 1, runtime.Store.TryGet(viewModel.DraftId!)!.RevisionOrdinal);

        // A refused edit saves nothing: the trim line of the previous save must not sit next to the rejection.
        ScaleRow(viewModel).EditText = "3.0";
        viewModel.ApplyParameterEditsCommand.Execute(null);
        Assert.AreEqual(
            LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateRecipeStatusParameterEditRejected, 1),
            viewModel.RecipeStatus);
        Assert.IsFalse(viewModel.HasRetentionNotice);
        Assert.AreEqual(string.Empty, viewModel.RecipeRetentionNotice);
        Assert.AreEqual(RecipeDraftLineageLimits.MaximumVersionsPerLineage, runtime.Store.ListLineage(lineageId).Count);
        runtime.AssertNoGatewayTraffic();
    }

    [TestMethod]
    public void ALevelTwoEvictionIsReportedWithLineageAndVersionCountsInThatOrder()
    {
        // The real store needs dozens of lineages to hit the level-2 cap; a fake outcome pins the VM rendering.
        var runtime = new EvictingRuntime(evictedLineageIds: ["lineage-a", "lineage-b"], evictedVersionCount: 5);
        var viewModel = new CreateViewModel(LocalizationTestSupport.CreateEnglish(), runtime);

        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));

        Assert.AreEqual(1, runtime.SaveVersionCalls);
        Assert.IsTrue(viewModel.ParameterPanel.HasHead);
        Assert.IsTrue(viewModel.HasRetentionNotice);
        Assert.AreEqual(
            LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateRetentionNoticeEvicted, 2, 5),
            viewModel.RecipeRetentionNotice);
        StringAssert.Contains(viewModel.RecipeRetentionNotice, "2 inactive lineage(s) (5 version(s))", "Lineage count first, version count second.");
        Assert.AreEqual(LocalizationTestSupport.English(UiStringKeys.CreateRecipeStatusPresetApplied), viewModel.RecipeStatus);
    }

    [TestMethod]
    public void TheUnavailableRuntimeLeavesThePanelHiddenAndTheApplyCommandDisabledWithoutThrowing()
    {
        var viewModel = new CreateViewModel(LocalizationTestSupport.CreateEnglish(), AiDesktopRuntime.Unavailable);

        Assert.IsFalse(viewModel.ParameterPanel.HasHead);
        Assert.AreEqual(0, viewModel.ParameterPanel.ParameterCount);
        Assert.IsFalse(viewModel.ParameterPanel.HasWarnings);
        Assert.IsFalse(viewModel.ApplyParameterEditsCommand.CanExecute(null));
        Assert.AreEqual(string.Empty, viewModel.ParameterPanel.IssueReport);
        viewModel.ApplyParameterEditsCommand.Execute(null);
        Assert.IsFalse(viewModel.ParameterPanel.HasHead, "Executing without a head is a no-op, not an exception.");

        // The disconnected store refuses the save with its stable code; the panel still has no head to render.
        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));
        Assert.AreEqual(
            LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateRecipeStatusDraftStorageFailedWithCode, RecipeDraftStoreErrorCode.StorageFailed),
            viewModel.RecipeStatus);
        Assert.IsFalse(viewModel.ParameterPanel.HasHead);
        Assert.IsFalse(viewModel.ApplyParameterEditsCommand.CanExecute(null));
        Assert.IsFalse(viewModel.HasRetentionNotice);
    }

    [TestMethod]
    public void AStaleHeadFailsClosedWithTheStoreCodeInTheStatusLine()
    {
        // RG-6: another entry appended to the same lineage after this page presented its head.
        var runtime = CreateRuntime();
        var viewModel = CreateViewModel(runtime);
        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));
        var head = runtime.Store.TryGet(viewModel.DraftId!)!;
        var otherEntry = new RecipeDraftStore(StorePath);
        otherEntry.AppendVersion(
            head.DraftId,
            head.CanonicalSha256!,
            RecipeParameterEditor.CreateHumanEditRevision(
                head,
                RecipeParameterEditor.Apply(head.RecipeJson, [new RecipeParameterEdit("travel", "core", "scale", "2.0")])),
            DateTimeOffset.UtcNow);

        ScaleRow(viewModel).EditText = "1.5";
        viewModel.ApplyParameterEditsCommand.Execute(null);

        Assert.AreEqual(
            LocalizationTestSupport.EnglishFormat(
                UiStringKeys.CreateRecipeStatusDraftStorageFailedWithCode,
                RecipeDraftStoreErrorCode.NotLineageHead),
            viewModel.RecipeStatus);
        Assert.AreEqual(head.DraftId, viewModel.DraftId, "The page keeps its presented head; nothing was written on its behalf.");
        Assert.AreEqual(2, runtime.Store.ListLineage(head.LineageId).Count);
    }

    [TestMethod]
    public void UndeclaredKeysRenderAsWarningRowsWhileDeclaredRowsStayEditable()
    {
        var recipe = FireBoltWith(root => root["stages"]![1]!["modules"]![0]!["parameters"]!["turbulence"] = 1.0);
        var runtime = CreateRuntime(request => Drafted(request, recipe));
        var viewModel = CreateViewModel(runtime);
        viewModel.EffectDescription = "a synthetic fire bolt";

        viewModel.GenerateRecipeCommand.ExecuteAsync(null).GetAwaiter().GetResult();

        var panel = viewModel.ParameterPanel;
        Assert.AreEqual(1, panel.Modules.Count);
        Assert.AreEqual(1, panel.ParameterCount);
        Assert.IsTrue(panel.HasWarnings);
        var warning = panel.Warnings.Single();
        Assert.AreEqual(RecipeParameterPanelWarningKind.ParameterUndeclared, warning.Kind);
        Assert.AreEqual(
            LocalizationTestSupport.EnglishFormat(
                UiStringKeys.CreateParameterWarningParameterUndeclared,
                "stages[travel].modules[core].parameters.turbulence",
                "turbulence"),
            warning.Text);
    }

    [TestMethod]
    public void L15WarningsOnAnAcceptedEditRenderWithTheirRepairSuggestion()
    {
        var recipe = FireBoltWith(root =>
        {
            var travel = root["stages"]![1]!["modules"]!.AsArray();
            travel.Add(new System.Text.Json.Nodes.JsonObject
            {
                ["id"] = "trail",
                ["kind"] = "motion_trail",
                ["templateId"] = "PFT_2D_FireTrail",
                ["parameters"] = new System.Text.Json.Nodes.JsonObject { ["time"] = 0.22, ["width"] = 0.42 },
                ["enabled"] = true,
            });
            root["stages"]![2]!["modules"]!.AsArray().Add(new System.Text.Json.Nodes.JsonObject
            {
                ["id"] = "burst",
                ["kind"] = "impact_burst",
                ["templateId"] = "PFT_2D_FireImpact",
                ["parameters"] = new System.Text.Json.Nodes.JsonObject { ["count"] = 24, ["speed"] = 3.5 },
                ["enabled"] = true,
            });
        });
        var runtime = CreateRuntime(request => Drafted(request, recipe));
        var viewModel = CreateViewModel(runtime);
        viewModel.EffectDescription = "a synthetic fire bolt";
        viewModel.GenerateRecipeCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.AreEqual(3, viewModel.ParameterPanel.Modules.Count);

        ScaleRow(viewModel).EditText = "1.5";
        viewModel.ApplyParameterEditsCommand.Execute(null);

        Assert.AreEqual(RecipeDraftOrigin.HumanEdit, runtime.Store.TryGet(viewModel.DraftId!)!.Origin, "L1.5 findings never block the edit.");
        StringAssert.Contains(viewModel.RecipeValidationSummary, RecipePrevalidationCodes.ModuleBudgetExceeded);
        StringAssert.Contains(
            viewModel.RecipeValidationSummary,
            LocalizationTestSupport.English(UiStringKeys.RecipeSuggestionReduceModuleCount));
        Assert.AreEqual(1, runtime.GenerateCalls, "Only the explicit generate click reached the channel.");
        Assert.AreEqual(0, runtime.ChatCalls);
    }

    [TestMethod]
    public void PanelTextAndTheRejectionReportFollowALiveLanguageSwitch()
    {
        var localization = LocalizationTestSupport.CreateEnglish();
        var runtime = CreateRuntime();
        var viewModel = new CreateViewModel(localization, runtime);
        var view = new CreateView { DataContext = viewModel };
        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));
        ScaleRow(viewModel).EditText = "abc";
        viewModel.ApplyParameterEditsCommand.Execute(null);
        var englishHint = ScaleRow(viewModel).BoundsHint;
        var englishHeader = viewModel.ParameterPanel.Modules[0].Header;
        var englishReport = viewModel.ParameterPanel.IssueReport;
        CollectionAssert.Contains(RenderedText(view), LocalizationTestSupport.English(UiStringKeys.CreateParameterPanelHeading));
        StringAssert.Contains(
            englishReport,
            LocalizationTestSupport.EnglishFormat(UiStringKeys.RecipeParameterEditValueNotFinite, ScalePath, "abc", "float in [0.6, 2.4]"));

        localization.SetLanguage(UiLanguage.ChineseSimplified);

        Assert.AreNotEqual(englishHint, ScaleRow(viewModel).BoundsHint);
        StringAssert.Contains(ScaleRow(viewModel).BoundsHint, "[0.6, 2.4]", "The range literal is a carrier and stays verbatim.");
        Assert.AreNotEqual(englishHeader, viewModel.ParameterPanel.Modules[0].Header);
        StringAssert.Contains(viewModel.ParameterPanel.Modules[0].Header, "PFT_2D_FireCore");
        Assert.AreNotEqual(englishReport, viewModel.ParameterPanel.IssueReport);
        StringAssert.Contains(
            viewModel.ParameterPanel.IssueReport,
            LocalizationTestSupport.ChineseSimplifiedFormat(UiStringKeys.RecipeParameterEditValueNotFinite, ScalePath, "abc", "float in [0.6, 2.4]"));
        Assert.AreEqual(
            LocalizationTestSupport.ChineseSimplifiedFormat(UiStringKeys.CreateRecipeStatusParameterEditRejected, 1),
            viewModel.RecipeStatus);
        CollectionAssert.Contains(RenderedText(view), LocalizationTestSupport.ChineseSimplified(UiStringKeys.CreateParameterPanelHeading));
    }

    [TestMethod]
    public void EveryEditorCodeOfTheClosedProviderSetCarriesPinnedCatalogCopy()
    {
        CollectionAssert.AreEquivalent(RecipeParameterEditCodes.All.ToArray(), RecipeParameterEditCopy.Codes.ToArray());
        foreach (var code in RecipeParameterEditCodes.All)
        {
            Assert.IsTrue(RecipeParameterEditCopy.TryGetCatalogKey(code, out var catalogKey), code + " resolves no catalog copy.");
            foreach (var language in UiStringCatalog.Languages)
            {
                Assert.IsTrue(UiStringCatalog.For(language).ContainsKey(catalogKey), catalogKey + " is missing in " + language);
            }
        }

        Assert.IsFalse(RecipeParameterEditCopy.TryGetCatalogKey(RecipePrevalidationCodes.ParameterOutOfRange, out _));
        Assert.IsFalse(RecipeParameterEditCopy.TryGetCatalogKey("E101", out _));
    }

    private string StorePath => Path.Combine(_storeDirectory, "recipe-drafts.json");

    private LineageRuntime CreateRuntime(Func<RecipeGenerationRequest, RecipeGenerationResult>? nextResult = null) =>
        new(StorePath) { NextResult = nextResult };

    private static CreateViewModel CreateViewModel(LineageRuntime runtime) =>
        new(LocalizationTestSupport.CreateEnglish(), runtime);

    private static PresetCardViewModel FireBoltCard(CreateViewModel viewModel) =>
        viewModel.PresetCards.Single(static card => card.Skeleton.PresetId == "fire-bolt");

    private static ParameterRowViewModel ScaleRow(CreateViewModel viewModel) => viewModel.ParameterPanel.Modules
        .Single(static module => module.ModuleId == "core")
        .Parameters.Single(static row => row.Name == "scale");

    private static string FireBoltJson => RecipePresetSkeletons.All.Single(static skeleton => skeleton.PresetId == "fire-bolt").RecipeJson;

    private static string FireBoltWith(Action<System.Text.Json.Nodes.JsonObject> mutate)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(FireBoltJson)!.AsObject();
        mutate(root);
        return root.ToJsonString();
    }

    private static RecipeGenerationResult DraftedFireBolt(RecipeGenerationRequest request) => Drafted(request, FireBoltJson);

    /// <summary>A mock channel result carrying a real strict-shape recipe, so the panel has declared rows to render.</summary>
    private static RecipeGenerationResult Drafted(RecipeGenerationRequest request, string recipeJson)
    {
        var canonical = RecipeCanonicalJson.Canonicalize(recipeJson);
        var draft = new RecipeDraft(
            request.CorrelationId,
            canonical,
            RecipeCanonicalJson.ComputeSha256(canonical),
            "preset_fire_bolt_2d",
            "projectile",
            "2d",
            "mobile_medium",
            "prompt/1",
            RecipeTemplateCatalogSnapshot.Default.TemplateCatalogVersion);
        return RecipeGenerationResult.Drafted(draft, [new RecipeGenerationAttempt(1, [])]);
    }

    private static List<string?> RenderedText(Control view) => view
        .GetLogicalDescendants()
        .OfType<TextBlock>()
        .Select(block => block.Text)
        .ToList();

    /// <summary>The real lineage store behind a counting gateway and mock channel; every count must stay where the test expects.</summary>
    private sealed class LineageRuntime : IAiDesktopRuntime, IAiGateway, IRecipeGenerationChannel
    {
        public LineageRuntime(string storePath)
        {
            Store = new RecipeDraftStore(storePath);
        }

        public RecipeDraftStore Store { get; }
        public Func<RecipeGenerationRequest, RecipeGenerationResult>? NextResult { get; init; }
        public int GenerateCalls { get; private set; }
        public int ChatCalls { get; private set; }
        public int ImageCalls { get; private set; }

        public IAiGateway Gateway => this;
        public IAiDesktopSettings Settings => throw new NotSupportedException();
        public IRecipeGenerationChannel RecipeGeneration => this;
        public IRecipeDraftLineageStore RecipeDrafts => Store;

        public void AssertNoGatewayTraffic()
        {
            Assert.AreEqual(0, GenerateCalls, "The panel must never start a generation request.");
            Assert.AreEqual(0, ChatCalls, "The panel must never send a chat request.");
            Assert.AreEqual(0, ImageCalls, "The panel must never send an image request.");
        }

        public ValueTask<RecipeGenerationResult> GenerateAsync(
            RecipeGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            GenerateCalls++;
            return ValueTask.FromResult(NextResult!(request));
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

    /// <summary>
    /// A store whose every root save reports a level-2 eviction, so the VM's rendering of that outcome can be
    /// pinned without growing a real store past its lineage cap. Nothing else on this runtime is reachable.
    /// </summary>
    private sealed class EvictingRuntime : IAiDesktopRuntime, IRecipeDraftLineageStore
    {
        private readonly IReadOnlyList<string> _evictedLineageIds;
        private readonly int _evictedVersionCount;
        private RecipeDraftRecord? _saved;

        public EvictingRuntime(IReadOnlyList<string> evictedLineageIds, int evictedVersionCount)
        {
            _evictedLineageIds = evictedLineageIds;
            _evictedVersionCount = evictedVersionCount;
        }

        public int SaveVersionCalls { get; private set; }

        public IAiGateway Gateway => throw new NotSupportedException("The panel never reaches the gateway.");
        public IAiDesktopSettings Settings => throw new NotSupportedException();
        public IRecipeGenerationChannel RecipeGeneration => throw new NotSupportedException("The panel never generates.");
        public IRecipeDraftLineageStore RecipeDrafts => this;

        public RecipeDraftSaveOutcome SaveVersion(RecipeDraftRecord record)
        {
            SaveVersionCalls++;
            _saved = record;
            return new RecipeDraftSaveOutcome(record, [], [], _evictedLineageIds, _evictedVersionCount);
        }

        public RecipeDraftRecord? TryGet(string draftId) =>
            _saved is not null && string.Equals(_saved.DraftId, draftId, StringComparison.Ordinal) ? _saved : null;

        public IReadOnlyList<RecipeDraftRecord> ListLineage(string lineageId) =>
            _saved is not null && string.Equals(_saved.LineageId, lineageId, StringComparison.Ordinal) ? [_saved] : [];

        public IReadOnlyList<RecipeDraftRecord> ListConfirmedAwaitingBuild() => [];

        public RecipeDraftSaveOutcome AppendVersion(
            string parentDraftId,
            string parentCanonicalSha256,
            RecipeDraftRevision revision,
            DateTimeOffset createdUtc) =>
            throw new NotSupportedException("This test only exercises a root save.");

        public RecipeDraftTruncateOutcome TruncateAfter(string draftId) => throw new NotSupportedException();

        public RecipeDraftRecord Save(RecipeDraftRecord record) => throw new NotSupportedException();

        public RecipeDraftRecord Confirm(string draftId, string canonicalSha256) => throw new NotSupportedException();

        public RecipeDraftRecord MarkBuilt(string draftId, string canonicalSha256) => throw new NotSupportedException();

        public RecipeDraftRecord MarkBuildFailed(string draftId, string canonicalSha256) => throw new NotSupportedException();

        public ValueTask<Stream> OpenImageArtifactAsync(string privateArtifactId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
