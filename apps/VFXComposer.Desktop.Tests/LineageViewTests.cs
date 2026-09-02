using System.Text.RegularExpressions;
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
/// The version-chain view and revert (F8b3b) over the real lineage store: the list renders every retained version
/// oldest first with protocol words verbatim, reverting is a two-step inline confirmation whose count comes from the
/// listed ordinals, a confirmed revert truncates and the next edit continues the line (AC-5), a revert past an audit
/// record is refused with its stable code and changes nothing (AC-6), cancelling touches nothing, and the whole
/// surface never reaches the gateway.
/// </summary>
[TestClass]
public sealed class LineageViewTests
{
    private static readonly Regex CreatedLiteralPattern = new(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$", RegexOptions.CultureInvariant);

    private string _storeDirectory = string.Empty;

    [ClassInitialize]
    public static void InitializeAvalonia(TestContext _) => AvaloniaTestPlatform.EnsureInitialized();

    [TestInitialize]
    public void CreateStoreDirectory() => _storeDirectory = Path.Combine(
        Path.GetTempPath(),
        "vfxcomposer-lineage-view-tests",
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
    public async Task TheListRendersEveryVersionOldestFirstAndFollowsALanguageSwitchWithoutTouchingProtocolWords()
    {
        var localization = LocalizationTestSupport.CreateEnglish();
        var runtime = CreateRuntime(DraftedFireBolt);
        var viewModel = new CreateViewModel(localization, runtime);
        var view = new CreateView { DataContext = viewModel };
        Assert.IsFalse(viewModel.Lineage.HasVersions, "No head, no list.");

        viewModel.EffectDescription = "a synthetic fire bolt";
        await viewModel.GenerateRecipeCommand.ExecuteAsync(null);
        Edit(viewModel, "0.8");
        Edit(viewModel, "1.0");

        var versions = viewModel.Lineage.Versions;
        Assert.IsTrue(viewModel.Lineage.HasVersions);
        Assert.AreEqual(3, versions.Count);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, versions.Select(static version => version.RevisionOrdinal).ToArray(), "Oldest first.");
        CollectionAssert.AreEqual(
            new[] { RecipeDraftOriginNames.AiDraft, RecipeDraftOriginNames.HumanEdit, RecipeDraftOriginNames.HumanEdit },
            versions.Select(static version => version.Origin).ToArray());
        CollectionAssert.AreEqual(new[] { false, false, true }, versions.Select(static version => version.IsHead).ToArray());
        Assert.AreEqual(viewModel.DraftId, versions[2].DraftId, "The head marker sits on the page's current draft.");
        foreach (var version in versions)
        {
            Assert.AreEqual(nameof(RecipeDraftStatus.PendingConfirmation), version.Status);
            Assert.IsTrue(CreatedLiteralPattern.IsMatch(version.CreatedLiteral), version.CreatedLiteral);
            Assert.IsFalse(version.HasFeedback, "Only ai_refine versions carry feedback.");
            Assert.AreEqual(string.Empty, version.FeedbackLine);
            Assert.AreEqual(0, version.GuardRestorationCount);
            Assert.AreEqual(LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateLineageVersionLabel, version.RevisionOrdinal), version.VersionLabel);
            Assert.AreEqual(LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateLineageCreatedLine, version.CreatedLiteral), version.CreatedLine);
            Assert.AreEqual(LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateLineageGuardLine, 0), version.GuardLine);
        }

        Assert.AreEqual(LocalizationTestSupport.English(UiStringKeys.CreateLineageHeadMarker), versions[2].HeadMarker);
        CollectionAssert.Contains(RenderedText(view), LocalizationTestSupport.English(UiStringKeys.CreateLineageHeading));
        CollectionAssert.Contains(RenderedButtonText(view), LocalizationTestSupport.English(UiStringKeys.CreateLineageRevertAction));
        var englishLabel = versions[0].VersionLabel;
        var englishCreated = versions[0].CreatedLine;

        localization.SetLanguage(UiLanguage.ChineseSimplified);

        var rendered = RenderedText(view);
        CollectionAssert.DoesNotContain(rendered, LocalizationTestSupport.English(UiStringKeys.CreateLineageHeading));
        CollectionAssert.Contains(rendered, LocalizationTestSupport.ChineseSimplified(UiStringKeys.CreateLineageHeading));
        CollectionAssert.Contains(RenderedButtonText(view), LocalizationTestSupport.ChineseSimplified(UiStringKeys.CreateLineageRevertAction));
        Assert.AreSame(versions, viewModel.Lineage.Versions, "A language switch re-renders the rows; it does not re-list the store.");
        Assert.AreNotEqual(englishLabel, versions[0].VersionLabel);
        Assert.AreEqual(LocalizationTestSupport.ChineseSimplifiedFormat(UiStringKeys.CreateLineageVersionLabel, 1), versions[0].VersionLabel);
        Assert.AreNotEqual(englishCreated, versions[0].CreatedLine);
        StringAssert.Contains(versions[0].CreatedLine, versions[0].CreatedLiteral, "The UTC literal is a carrier and stays verbatim.");
        Assert.AreEqual(RecipeDraftOriginNames.AiDraft, versions[0].Origin, "Protocol words are not translated.");
        Assert.AreEqual(nameof(RecipeDraftStatus.PendingConfirmation), versions[0].Status);
        Assert.AreEqual(1, versions[0].RevisionOrdinal);
        Assert.AreEqual(LocalizationTestSupport.ChineseSimplified(UiStringKeys.CreateLineageHeadMarker), versions[2].HeadMarker);

        Assert.AreEqual(1, runtime.GenerateCalls, "Only the explicit generate click reached the channel.");
        Assert.AreEqual(0, runtime.ChatCalls);
        Assert.AreEqual(0, runtime.ImageCalls);
    }

    [TestMethod]
    public void RevertingToAnOlderVersionTruncatesAndTheNextEditContinuesTheLineWithoutBranching()
    {
        // AC-5: v1 → v2 → v3 all pending; revert to v2, confirm; v3 is gone; the next hand edit lands v4 under v2.
        var runtime = CreateRuntime();
        var viewModel = CreateViewModel(runtime);
        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));
        var v1 = viewModel.DraftId!;
        Edit(viewModel, "0.8");
        var v2 = viewModel.DraftId!;
        Edit(viewModel, "1.0");
        var v3 = viewModel.DraftId!;
        var lineageId = runtime.Store.TryGet(v1)!.LineageId;
        Assert.AreEqual(3, viewModel.Lineage.Versions.Count);

        viewModel.Lineage.SelectedVersion = Version(viewModel, v2);
        Assert.IsTrue(viewModel.RevertToSelectedVersionCommand.CanExecute(null));
        Assert.IsFalse(viewModel.Lineage.IsRevertPending);
        Assert.IsFalse(viewModel.ConfirmRevertCommand.CanExecute(null));

        viewModel.RevertToSelectedVersionCommand.Execute(null);

        Assert.IsTrue(viewModel.Lineage.IsRevertPending, "Step one only arms the confirmation.");
        Assert.AreEqual(1, viewModel.Lineage.PendingDiscardCount);
        Assert.AreEqual(
            LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateLineageRevertConfirmPrompt, 1, "v3"),
            viewModel.Lineage.RevertPrompt);
        Assert.IsTrue(viewModel.ConfirmRevertCommand.CanExecute(null));
        Assert.IsTrue(viewModel.CancelRevertCommand.CanExecute(null));
        Assert.AreEqual(3, runtime.Store.ListLineage(lineageId).Count, "Nothing is deleted before the confirmation.");
        Assert.AreEqual(0, runtime.TruncateCalls);

        viewModel.ConfirmRevertCommand.Execute(null);

        Assert.AreEqual(1, runtime.TruncateCalls);
        var retained = runtime.Store.ListLineage(lineageId);
        CollectionAssert.AreEqual(new[] { v1, v2 }, retained.Select(static record => record.DraftId).ToArray());
        Assert.IsNull(runtime.Store.TryGet(v3), "The truncated version is deleted, not hidden.");
        Assert.AreEqual(v2, viewModel.DraftId, "The page's head moved to the reverted version.");
        Assert.AreEqual(RecipeDraftStatus.PendingConfirmation, viewModel.DraftStatus);
        Assert.AreEqual(
            LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateRecipeStatusRevertedToVersion, 2, 1),
            viewModel.RecipeStatus);
        Assert.IsFalse(viewModel.HasRetentionNotice);
        Assert.AreEqual(LocalizationTestSupport.English(UiStringKeys.CreateValidationPassed), viewModel.RecipeValidationSummary);
        Assert.AreEqual("0.8", ScaleRow(viewModel).CurrentValueLiteral, "The panel re-renders from the new head.");
        StringAssert.Contains(viewModel.RecipeDraftJson, "0.8");
        Assert.IsFalse(viewModel.Lineage.IsRevertPending);
        Assert.AreEqual(2, viewModel.Lineage.Versions.Count);
        Assert.IsTrue(Version(viewModel, v2).IsHead);
        Assert.IsFalse(Version(viewModel, v1).IsHead);
        Assert.AreEqual(v2, viewModel.Lineage.SelectedVersion?.DraftId, "The selection survives the reload.");
        Assert.IsFalse(viewModel.RevertToSelectedVersionCommand.CanExecute(null), "The reverted version is the head now and cannot be reverted to.");
        Assert.IsTrue(viewModel.ConfirmRecipeDraftCommand.CanExecute(null));

        Edit(viewModel, "1.2");

        var v4 = runtime.Store.TryGet(viewModel.DraftId!)!;
        Assert.AreEqual(v2, v4.ParentDraftId);
        Assert.AreEqual(4, v4.RevisionOrdinal, "Ordinals are never reused after a truncation.");
        Assert.AreEqual(RecipeDraftOrigin.HumanEdit, v4.Origin);
        var chain = runtime.Store.ListLineage(lineageId);
        Assert.AreEqual(3, chain.Count);
        var parents = chain.Select(static record => record.ParentDraftId).Where(static parent => parent is not null).ToArray();
        Assert.AreEqual(parents.Length, parents.Distinct(StringComparer.Ordinal).Count(), "No two versions share a parent: the chain has no branch.");
        var ids = chain.Select(static record => record.DraftId).ToHashSet(StringComparer.Ordinal);
        Assert.IsTrue(parents.All(parent => ids.Contains(parent!)), "Every parent resolves inside the lineage.");
        Assert.AreEqual(3, viewModel.Lineage.Versions.Count);
        CollectionAssert.AreEqual(new[] { 1, 2, 4 }, viewModel.Lineage.Versions.Select(static version => version.RevisionOrdinal).ToArray());
        runtime.AssertNoGatewayTraffic();
    }

    [TestMethod]
    public void ARevertPastAConfirmedVersionIsRefusedWithTheStableCodeAndChangesNothing()
    {
        // AC-6: v3 is ConfirmedAwaitingBuild; reverting to v1 would delete an audit record.
        var runtime = CreateRuntime();
        var viewModel = CreateViewModel(runtime);
        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));
        var v1 = viewModel.DraftId!;
        Edit(viewModel, "0.8");
        Edit(viewModel, "1.0");
        var v3 = viewModel.DraftId!;
        viewModel.ConfirmRecipeDraftCommand.Execute(null);
        var lineageId = runtime.Store.TryGet(v1)!.LineageId;
        var before = runtime.Store.ListLineage(lineageId);
        Assert.AreEqual(RecipeDraftStatus.ConfirmedAwaitingBuild, before[2].Status);
        Assert.AreEqual(nameof(RecipeDraftStatus.ConfirmedAwaitingBuild), Version(viewModel, v3).Status);

        viewModel.Lineage.SelectedVersion = Version(viewModel, v1);
        viewModel.RevertToSelectedVersionCommand.Execute(null);
        Assert.AreEqual(2, viewModel.Lineage.PendingDiscardCount);
        Assert.AreEqual(
            LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateLineageRevertConfirmPrompt, 2, "v2..v3"),
            viewModel.Lineage.RevertPrompt);

        viewModel.ConfirmRevertCommand.Execute(null);

        Assert.AreEqual(1, runtime.TruncateCalls);
        Assert.AreEqual(
            LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateRecipeStatusRevertBlockedWithCode, RecipeDraftStoreErrorCode.TruncationBlocked),
            viewModel.RecipeStatus);
        StringAssert.Contains(viewModel.RecipeStatus, nameof(RecipeDraftStoreErrorCode.TruncationBlocked));
        var after = runtime.Store.ListLineage(lineageId);
        Assert.AreEqual(3, after.Count);
        for (var index = 0; index < before.Count; index++)
        {
            var retained = runtime.Store.TryGet(before[index].DraftId)!;
            Assert.AreEqual(before[index].Status, retained.Status, before[index].DraftId);
            Assert.AreEqual(before[index].CanonicalSha256, retained.CanonicalSha256, before[index].DraftId);
            Assert.AreEqual(before[index].RecipeJson, retained.RecipeJson, before[index].DraftId);
            Assert.AreEqual(before[index].ParentDraftId, retained.ParentDraftId, before[index].DraftId);
            Assert.AreEqual(before[index].UpdatedUtc, retained.UpdatedUtc, before[index].DraftId);
            Assert.AreEqual(before[index].RevisionOrdinal, retained.RevisionOrdinal, before[index].DraftId);
        }

        Assert.AreEqual(v3, viewModel.DraftId, "The page keeps its head.");
        Assert.AreEqual(RecipeDraftStatus.ConfirmedAwaitingBuild, viewModel.DraftStatus);
        Assert.IsFalse(viewModel.Lineage.IsRevertPending, "The refused confirmation is disarmed, not left dangling.");
        Assert.AreEqual(3, viewModel.Lineage.Versions.Count);
        Assert.AreEqual(v1, viewModel.Lineage.SelectedVersion?.DraftId);
        Assert.IsTrue(viewModel.RevertToSelectedVersionCommand.CanExecute(null), "The user may re-arm; the store will refuse again.");
        runtime.AssertNoGatewayTraffic();
    }

    [TestMethod]
    public void CancellingOrReselectingDisarmsTheRevertAndTouchesTheStoreOnlyToList()
    {
        var runtime = CreateRuntime();
        var viewModel = CreateViewModel(runtime);
        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));
        var v1 = viewModel.DraftId!;
        Edit(viewModel, "0.8");
        var v2 = viewModel.DraftId!;
        var lineageId = runtime.Store.TryGet(v1)!.LineageId;

        viewModel.Lineage.SelectedVersion = Version(viewModel, v1);
        viewModel.RevertToSelectedVersionCommand.Execute(null);
        Assert.IsTrue(viewModel.Lineage.IsRevertPending);

        viewModel.CancelRevertCommand.Execute(null);

        Assert.IsFalse(viewModel.Lineage.IsRevertPending);
        Assert.AreEqual(string.Empty, viewModel.Lineage.RevertPrompt);
        Assert.AreEqual(0, viewModel.Lineage.PendingDiscardCount);
        Assert.IsFalse(viewModel.ConfirmRevertCommand.CanExecute(null));
        Assert.IsFalse(viewModel.CancelRevertCommand.CanExecute(null));
        viewModel.ConfirmRevertCommand.Execute(null);
        Assert.AreEqual(0, runtime.TruncateCalls, "Neither the cancel nor a stray confirm reaches TruncateAfter.");
        CollectionAssert.AreEqual(new[] { v1, v2 }, runtime.Store.ListLineage(lineageId).Select(static record => record.DraftId).ToArray());
        Assert.AreEqual(v2, viewModel.DraftId);
        Assert.AreEqual(v1, viewModel.Lineage.SelectedVersion?.DraftId, "Cancelling keeps the selection.");

        // Re-arming and then selecting a different row disarms: the prompt named the versions after v1.
        viewModel.RevertToSelectedVersionCommand.Execute(null);
        Assert.IsTrue(viewModel.Lineage.IsRevertPending);
        viewModel.Lineage.SelectedVersion = Version(viewModel, v2);
        Assert.IsFalse(viewModel.Lineage.IsRevertPending);
        Assert.AreEqual(0, runtime.TruncateCalls);
        runtime.AssertNoGatewayTraffic();
    }

    [TestMethod]
    public void TheHeadAndAnEmptySelectionCannotBeReverted()
    {
        var runtime = CreateRuntime();
        var viewModel = CreateViewModel(runtime);
        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));
        var v1 = viewModel.DraftId!;
        Edit(viewModel, "0.8");
        var v2 = viewModel.DraftId!;

        Assert.IsNull(viewModel.Lineage.SelectedVersion);
        Assert.IsFalse(viewModel.RevertToSelectedVersionCommand.CanExecute(null), "No selection, nothing to revert to.");
        viewModel.RevertToSelectedVersionCommand.Execute(null);
        Assert.IsFalse(viewModel.Lineage.IsRevertPending, "Executing without a selection arms nothing.");

        viewModel.Lineage.SelectedVersion = Version(viewModel, v2);
        Assert.IsTrue(viewModel.Lineage.SelectedVersion!.IsHead);
        Assert.IsFalse(viewModel.RevertToSelectedVersionCommand.CanExecute(null), "The head cannot be reverted to.");
        viewModel.RevertToSelectedVersionCommand.Execute(null);
        Assert.IsFalse(viewModel.Lineage.IsRevertPending);

        viewModel.Lineage.SelectedVersion = Version(viewModel, v1);
        Assert.IsTrue(viewModel.RevertToSelectedVersionCommand.CanExecute(null));
        Assert.AreEqual(0, runtime.TruncateCalls);
        runtime.AssertNoGatewayTraffic();
    }

    [TestMethod]
    public void TheConfirmationCountsTheNewerVersionsFromTheListedOrdinals()
    {
        var runtime = CreateRuntime();
        var viewModel = CreateViewModel(runtime);
        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));
        Edit(viewModel, "0.8");
        var v2 = viewModel.DraftId!;
        Edit(viewModel, "1.0");
        Edit(viewModel, "1.2");
        Edit(viewModel, "1.4");
        Assert.AreEqual(5, viewModel.Lineage.Versions.Count);

        viewModel.Lineage.SelectedVersion = Version(viewModel, v2);
        viewModel.RevertToSelectedVersionCommand.Execute(null);

        Assert.AreEqual(3, viewModel.Lineage.PendingDiscardCount);
        Assert.AreEqual(
            LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateLineageRevertConfirmPrompt, 3, "v3..v5"),
            viewModel.Lineage.RevertPrompt);
        StringAssert.Contains(viewModel.Lineage.RevertPrompt, "3");
        StringAssert.Contains(viewModel.Lineage.RevertPrompt, "v3..v5");
        Assert.AreEqual(0, runtime.TruncateCalls);
        runtime.AssertNoGatewayTraffic();
    }

    [TestMethod]
    public void TheConfirmationListsTheOrdinalsInsteadOfARangeWhenTheyAreNotContiguous()
    {
        // A truncation skips ordinals (1, 2, 4): "v2..v4" would name three versions while the count says two, so
        // the prompt lists the ordinals one by one.
        var runtime = CreateRuntime();
        var viewModel = CreateViewModel(runtime);
        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));
        var v1 = viewModel.DraftId!;
        Edit(viewModel, "0.8");
        var v2 = viewModel.DraftId!;
        Edit(viewModel, "1.0");
        viewModel.Lineage.SelectedVersion = Version(viewModel, v2);
        viewModel.RevertToSelectedVersionCommand.Execute(null);
        viewModel.ConfirmRevertCommand.Execute(null);
        Edit(viewModel, "1.2");
        CollectionAssert.AreEqual(
            new[] { 1, 2, 4 },
            viewModel.Lineage.Versions.Select(static version => version.RevisionOrdinal).ToArray(),
            "The truncation left a gap in the ordinals.");

        viewModel.Lineage.SelectedVersion = Version(viewModel, v1);
        viewModel.RevertToSelectedVersionCommand.Execute(null);

        Assert.AreEqual(2, viewModel.Lineage.PendingDiscardCount);
        Assert.AreEqual(
            LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateLineageRevertConfirmPrompt, 2, "v2, v4"),
            viewModel.Lineage.RevertPrompt);
        Assert.IsFalse(viewModel.Lineage.RevertPrompt.Contains("..", StringComparison.Ordinal));
        runtime.AssertNoGatewayTraffic();
    }

    [TestMethod]
    public void TheUnavailableRuntimeShowsNoVersionsAndDisablesRevertWithoutThrowing()
    {
        var viewModel = new CreateViewModel(LocalizationTestSupport.CreateEnglish(), AiDesktopRuntime.Unavailable);

        Assert.IsFalse(viewModel.Lineage.HasVersions);
        Assert.AreEqual(0, viewModel.Lineage.Versions.Count);
        Assert.IsFalse(viewModel.Lineage.HasListFailure);
        Assert.IsNull(viewModel.Lineage.SelectedVersion);
        Assert.IsFalse(viewModel.RevertToSelectedVersionCommand.CanExecute(null));
        Assert.IsFalse(viewModel.ConfirmRevertCommand.CanExecute(null));
        Assert.IsFalse(viewModel.CancelRevertCommand.CanExecute(null));
        viewModel.RevertToSelectedVersionCommand.Execute(null);
        viewModel.ConfirmRevertCommand.Execute(null);
        viewModel.CancelRevertCommand.Execute(null);
        Assert.IsFalse(viewModel.Lineage.IsRevertPending, "Executing without a lineage is a no-op, not an exception.");

        // The disconnected store refuses the save; there is still no lineage to list.
        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));
        Assert.AreEqual(
            LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateRecipeStatusDraftStorageFailedWithCode, RecipeDraftStoreErrorCode.StorageFailed),
            viewModel.RecipeStatus);
        Assert.IsFalse(viewModel.Lineage.HasVersions);
        Assert.IsFalse(viewModel.RevertToSelectedVersionCommand.CanExecute(null));
    }

    [TestMethod]
    public void ARefusedListingShowsTheStableCodeInPlaceOfTheList()
    {
        var runtime = new ListingRefusedRuntime(RecipeDraftStoreErrorCode.StoreBusy);
        // F8b4 semantic adaptation: the version-chain card is a professional-mode section now (REQ-004-07), so the
        // rendered-visibility half of this test runs under that mode; the stable-code semantics are unchanged.
        var viewModel = new CreateViewModel(
            LocalizationTestSupport.CreateEnglish(),
            runtime,
            new Services.GenerationModeService(Services.GenerationMode.Professional));
        var view = new CreateView { DataContext = viewModel };

        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));

        Assert.IsTrue(viewModel.ParameterPanel.HasHead, "The save succeeded; only the listing was refused.");
        Assert.AreEqual(LocalizationTestSupport.English(UiStringKeys.CreateRecipeStatusPresetApplied), viewModel.RecipeStatus);
        Assert.IsTrue(viewModel.Lineage.HasListFailure);
        var failureLine = LocalizationTestSupport.EnglishFormat(
            UiStringKeys.CreateLineageListFailedWithCode,
            RecipeDraftStoreErrorCode.StoreBusy);
        Assert.AreEqual(failureLine, viewModel.Lineage.ListFailure);
        Assert.IsFalse(viewModel.Lineage.HasVersions);
        Assert.IsTrue(viewModel.Lineage.IsCardVisible, "The emptied list must not take the failure line down with it.");
        Assert.IsFalse(viewModel.RevertToSelectedVersionCommand.CanExecute(null));

        // The stable code actually renders: the failure line and its card stay visible although the list is empty.
        CollectionAssert.Contains(RenderedText(view), failureLine);
        var failureBlock = view.GetLogicalDescendants()
            .OfType<TextBlock>()
            .Single(block => string.Equals(block.Text, failureLine, StringComparison.Ordinal));
        Assert.IsTrue(failureBlock.IsVisible);
        var card = failureBlock.GetLogicalAncestors().OfType<Border>().First(static border => border.Classes.Contains("card"));
        Assert.IsTrue(card.IsVisible, "The lineage card must show for the failure line even without versions.");
    }

    [TestMethod]
    public void ARevertRefusedWithAGenericCodeShowsTheGenericKeyAndChangesNothing()
    {
        // A non-TruncationBlocked refusal (here StoreBusy) lands on the generic revert-failed key, not the audit one.
        var runtime = new LineageRuntime(StorePath) { TruncateRefusal = RecipeDraftStoreErrorCode.StoreBusy };
        var viewModel = CreateViewModel(runtime);
        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));
        var v1 = viewModel.DraftId!;
        Edit(viewModel, "0.8");
        var v2 = viewModel.DraftId!;
        var lineageId = runtime.Store.TryGet(v1)!.LineageId;

        viewModel.Lineage.SelectedVersion = Version(viewModel, v1);
        viewModel.RevertToSelectedVersionCommand.Execute(null);
        viewModel.ConfirmRevertCommand.Execute(null);

        Assert.AreEqual(1, runtime.TruncateCalls);
        Assert.AreEqual(
            LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateRecipeStatusRevertFailedWithCode, RecipeDraftStoreErrorCode.StoreBusy),
            viewModel.RecipeStatus);
        Assert.AreEqual(v2, viewModel.DraftId, "The page keeps its head.");
        Assert.AreEqual(2, viewModel.Lineage.Versions.Count);
        CollectionAssert.AreEqual(
            new[] { v1, v2 },
            runtime.Store.ListLineage(lineageId).Select(static record => record.DraftId).ToArray(),
            "The refusal deleted nothing.");
        Assert.IsFalse(viewModel.Lineage.IsRevertPending, "The refused confirmation is disarmed, not left dangling.");
        runtime.AssertNoGatewayTraffic();
    }

    [TestMethod]
    public void ARefineVersionShowsABoundedFeedbackSummaryAndTheGuardRestorationTotal()
    {
        // The real store cannot produce ai_refine versions before F8b4; a fake lineage pins the row rendering.
        var feedback = new string('f', LineageVersionViewModel.MaximumFeedbackSummaryCharacters + 40);
        var runtime = new RefineLineageRuntime(feedback, guardRestorationCount: 3);
        var viewModel = new CreateViewModel(LocalizationTestSupport.CreateEnglish(), runtime);

        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));

        var versions = viewModel.Lineage.Versions;
        Assert.AreEqual(2, versions.Count);
        var root = versions[0];
        var refine = versions[1];
        Assert.AreEqual(RecipeDraftOriginNames.Preset, root.Origin);
        Assert.IsFalse(root.HasFeedback);
        Assert.AreEqual(0, root.GuardRestorationCount);
        Assert.AreEqual(RecipeDraftOriginNames.AiRefine, refine.Origin);
        Assert.AreEqual(2, refine.RevisionOrdinal);
        Assert.IsTrue(refine.IsHead, "The newest listed ordinal is the head even when the page's own draft is older.");
        Assert.IsFalse(root.IsHead);
        Assert.AreEqual("2026-09-02 10:13", refine.CreatedLiteral, "Invariant UTC literal, minute precision.");
        Assert.IsTrue(refine.HasFeedback);
        Assert.AreEqual(LineageVersionViewModel.MaximumFeedbackSummaryCharacters + 1, refine.FeedbackSummary.Length);
        Assert.IsTrue(refine.FeedbackSummary.EndsWith('\u2026'));
        Assert.AreEqual(feedback[..LineageVersionViewModel.MaximumFeedbackSummaryCharacters], refine.FeedbackSummary[..^1]);
        Assert.AreEqual(
            LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateLineageFeedbackLine, refine.FeedbackSummary),
            refine.FeedbackLine);
        Assert.IsFalse(refine.FeedbackLine.Contains(feedback, StringComparison.Ordinal), "The full feedback text never renders.");
        Assert.AreEqual(3, refine.GuardRestorationCount, "The total counts entries dropped from the bounded list too.");
        Assert.AreEqual(LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateLineageGuardLine, 3), refine.GuardLine);
        Assert.IsFalse(refine.ToString().Contains(feedback[..8], StringComparison.Ordinal), "Diagnostics never carry feedback text.");
    }

    [TestMethod]
    public void TheFeedbackSummaryNeverSplitsASurrogatePairAtTheTruncationBoundary()
    {
        // An emoji (one rune, two UTF-16 code units) straddles the 80-character cut: units 80 and 81. A blind
        // [..80] would keep the lone high surrogate and render a broken character.
        var boundary = LineageVersionViewModel.MaximumFeedbackSummaryCharacters;
        var feedback = new string('f', boundary - 1) + "\U0001F600" + new string('g', 20);
        var runtime = new RefineLineageRuntime(feedback, guardRestorationCount: 1);
        var viewModel = new CreateViewModel(LocalizationTestSupport.CreateEnglish(), runtime);

        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));

        var summary = viewModel.Lineage.Versions[1].FeedbackSummary;
        Assert.IsTrue(summary.EndsWith('\u2026'));
        Assert.AreEqual(new string('f', boundary - 1), summary[..^1], "The cut backed up past the pair's high surrogate.");
        foreach (var character in summary)
        {
            Assert.IsFalse(char.IsSurrogate(character), "Every rune in the summary is whole.");
        }
    }

    [TestMethod]
    public void TheFeedbackSummaryCollapsesNewlinesIntoSpaces()
    {
        var runtime = new RefineLineageRuntime("first line\r\nsecond line\nthird line", guardRestorationCount: 1);
        var viewModel = new CreateViewModel(LocalizationTestSupport.CreateEnglish(), runtime);

        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));

        var summary = viewModel.Lineage.Versions[1].FeedbackSummary;
        Assert.IsFalse(summary.Contains('\r'), summary);
        Assert.IsFalse(summary.Contains('\n'), summary);
        StringAssert.Contains(summary, "first line");
        StringAssert.Contains(summary, "second line");
        StringAssert.Contains(summary, "third line");
    }

    [TestMethod]
    public void ARefusedEditReplacesThePassedVerdictWithANeutralPointerToThePanelReport()
    {
        // F8b3 audit B#6: the previous head's "passed" line must not read as a verdict on the refused edit.
        var runtime = CreateRuntime();
        var viewModel = CreateViewModel(runtime);
        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));
        Assert.AreEqual(LocalizationTestSupport.English(UiStringKeys.CreateValidationPassed), viewModel.RecipeValidationSummary);
        var headId = viewModel.DraftId!;

        ScaleRow(viewModel).EditText = "3.0";
        viewModel.ApplyParameterEditsCommand.Execute(null);

        Assert.AreEqual(
            LocalizationTestSupport.English(UiStringKeys.CreateValidationEditRefusedSeePanel),
            viewModel.RecipeValidationSummary);
        StringAssert.Contains(viewModel.ParameterPanel.IssueReport, RecipeParameterEditCodes.ValueOutOfRange);
        Assert.AreEqual(headId, viewModel.DraftId);
        Assert.AreEqual(1, viewModel.Lineage.Versions.Count, "A refused edit lands nothing.");

        // An accepted edit restores the real verdict of the new head.
        Edit(viewModel, "1.5");
        Assert.AreEqual(LocalizationTestSupport.English(UiStringKeys.CreateValidationPassed), viewModel.RecipeValidationSummary);
        runtime.AssertNoGatewayTraffic();
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

    private static LineageVersionViewModel Version(CreateViewModel viewModel, string draftId) =>
        viewModel.Lineage.Versions.Single(version => string.Equals(version.DraftId, draftId, StringComparison.Ordinal));

    /// <summary>One accepted hand edit of the fire core scale; the status line proves it landed.</summary>
    private static void Edit(CreateViewModel viewModel, string scale)
    {
        ScaleRow(viewModel).EditText = scale;
        viewModel.ApplyParameterEditsCommand.Execute(null);
        var landed = viewModel.Lineage.Versions[^1];
        Assert.AreEqual(RecipeDraftOriginNames.HumanEdit, landed.Origin, "The edit must land as a human_edit version.");
        Assert.AreEqual(viewModel.DraftId, landed.DraftId);
        Assert.AreEqual(
            LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateRecipeStatusHumanEditSaved, landed.RevisionOrdinal),
            viewModel.RecipeStatus);
    }

    private static string FireBoltJson => RecipePresetSkeletons.All.Single(static skeleton => skeleton.PresetId == "fire-bolt").RecipeJson;

    private static RecipeGenerationResult DraftedFireBolt(RecipeGenerationRequest request)
    {
        var canonical = RecipeCanonicalJson.Canonicalize(FireBoltJson);
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

    private static List<string?> RenderedButtonText(Control view) => view
        .GetLogicalDescendants()
        .OfType<Button>()
        .Select(button => button.Content as string)
        .ToList();

    /// <summary>
    /// The real lineage store behind a counting gateway and mock channel. Store calls that matter to the revert
    /// flow are counted through the wrapper so a test can prove the page never truncated on its own.
    /// </summary>
    private sealed class LineageRuntime : IAiDesktopRuntime, IAiGateway, IRecipeGenerationChannel, IRecipeDraftLineageStore
    {
        public LineageRuntime(string storePath)
        {
            Store = new RecipeDraftStore(storePath);
        }

        public RecipeDraftStore Store { get; }
        public Func<RecipeGenerationRequest, RecipeGenerationResult>? NextResult { get; init; }

        /// <summary>When set, <see cref="TruncateAfter"/> refuses with this code instead of reaching the store.</summary>
        public RecipeDraftStoreErrorCode? TruncateRefusal { get; init; }

        public int GenerateCalls { get; private set; }
        public int ChatCalls { get; private set; }
        public int ImageCalls { get; private set; }
        public int TruncateCalls { get; private set; }

        public IAiGateway Gateway => this;
        public IAiDesktopSettings Settings => throw new NotSupportedException();
        public IRecipeGenerationChannel RecipeGeneration => this;
        public IRecipeDraftLineageStore RecipeDrafts => this;

        public void AssertNoGatewayTraffic()
        {
            Assert.AreEqual(0, GenerateCalls, "The version chain must never start a generation request.");
            Assert.AreEqual(0, ChatCalls, "The version chain must never send a chat request.");
            Assert.AreEqual(0, ImageCalls, "The version chain must never send an image request.");
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

        public RecipeDraftSaveOutcome SaveVersion(RecipeDraftRecord record) => Store.SaveVersion(record);

        public RecipeDraftSaveOutcome AppendVersion(
            string parentDraftId,
            string parentCanonicalSha256,
            RecipeDraftRevision revision,
            DateTimeOffset createdUtc) =>
            Store.AppendVersion(parentDraftId, parentCanonicalSha256, revision, createdUtc);

        public RecipeDraftTruncateOutcome TruncateAfter(string draftId)
        {
            TruncateCalls++;
            return TruncateRefusal is { } code ? throw new RecipeDraftStoreException(code) : Store.TruncateAfter(draftId);
        }

        public IReadOnlyList<RecipeDraftRecord> ListLineage(string lineageId) => Store.ListLineage(lineageId);

        public RecipeDraftRecord Save(RecipeDraftRecord record) => Store.Save(record);

        public RecipeDraftRecord Confirm(string draftId, string canonicalSha256) => Store.Confirm(draftId, canonicalSha256);

        public RecipeDraftRecord MarkBuilt(string draftId, string canonicalSha256) => Store.MarkBuilt(draftId, canonicalSha256);

        public RecipeDraftRecord MarkBuildFailed(string draftId, string canonicalSha256) => Store.MarkBuildFailed(draftId, canonicalSha256);

        public RecipeDraftRecord? TryGet(string draftId) => Store.TryGet(draftId);

        public IReadOnlyList<RecipeDraftRecord> ListConfirmedAwaitingBuild() => Store.ListConfirmedAwaitingBuild();
    }

    /// <summary>An in-memory root save whose lineage listing is refused with one stable code. Nothing else is reachable.</summary>
    private sealed class ListingRefusedRuntime : IAiDesktopRuntime, IRecipeDraftLineageStore
    {
        private readonly RecipeDraftStoreErrorCode _code;
        private RecipeDraftRecord? _saved;

        public ListingRefusedRuntime(RecipeDraftStoreErrorCode code)
        {
            _code = code;
        }

        public IAiGateway Gateway => throw new NotSupportedException("The version chain never reaches the gateway.");
        public IAiDesktopSettings Settings => throw new NotSupportedException();
        public IRecipeGenerationChannel RecipeGeneration => throw new NotSupportedException("The version chain never generates.");
        public IRecipeDraftLineageStore RecipeDrafts => this;

        public RecipeDraftSaveOutcome SaveVersion(RecipeDraftRecord record)
        {
            _saved = record;
            return new RecipeDraftSaveOutcome(record, [], [], [], 0);
        }

        public IReadOnlyList<RecipeDraftRecord> ListLineage(string lineageId) => throw new RecipeDraftStoreException(_code);

        public RecipeDraftRecord? TryGet(string draftId) =>
            _saved is not null && string.Equals(_saved.DraftId, draftId, StringComparison.Ordinal) ? _saved : null;

        public IReadOnlyList<RecipeDraftRecord> ListConfirmedAwaitingBuild() => [];

        public RecipeDraftSaveOutcome AppendVersion(
            string parentDraftId,
            string parentCanonicalSha256,
            RecipeDraftRevision revision,
            DateTimeOffset createdUtc) =>
            throw new NotSupportedException();

        public RecipeDraftTruncateOutcome TruncateAfter(string draftId) => throw new NotSupportedException();

        public RecipeDraftRecord Save(RecipeDraftRecord record) => throw new NotSupportedException();

        public RecipeDraftRecord Confirm(string draftId, string canonicalSha256) => throw new NotSupportedException();

        public RecipeDraftRecord MarkBuilt(string draftId, string canonicalSha256) => throw new NotSupportedException();

        public RecipeDraftRecord MarkBuildFailed(string draftId, string canonicalSha256) => throw new NotSupportedException();

        public ValueTask<Stream> OpenImageArtifactAsync(string privateArtifactId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// A store that lists the saved root followed by one synthetic ai_refine version carrying feedback and guard
    /// restorations, so the row rendering of F8b4's fields can be pinned before F8b4 produces them.
    /// </summary>
    private sealed class RefineLineageRuntime : IAiDesktopRuntime, IRecipeDraftLineageStore
    {
        private readonly string _feedback;
        private readonly int _guardRestorationCount;
        private RecipeDraftRecord? _saved;

        public RefineLineageRuntime(string feedback, int guardRestorationCount)
        {
            _feedback = feedback;
            _guardRestorationCount = guardRestorationCount;
        }

        public IAiGateway Gateway => throw new NotSupportedException("The version chain never reaches the gateway.");
        public IAiDesktopSettings Settings => throw new NotSupportedException();
        public IRecipeGenerationChannel RecipeGeneration => throw new NotSupportedException("The version chain never generates.");
        public IRecipeDraftLineageStore RecipeDrafts => this;

        public RecipeDraftSaveOutcome SaveVersion(RecipeDraftRecord record)
        {
            _saved = record;
            return new RecipeDraftSaveOutcome(record, [], [], [], 0);
        }

        public IReadOnlyList<RecipeDraftRecord> ListLineage(string lineageId)
        {
            if (_saved is null || !string.Equals(_saved.LineageId, lineageId, StringComparison.Ordinal))
            {
                return [];
            }

            var refineId = RecipeDraftRecord.NewDraftId();
            var createdUtc = new DateTimeOffset(2026, 9, 2, 10, 13, 45, TimeSpan.Zero);
            var refine = new RecipeDraftRecord(
                refineId,
                RecipeDraftStatus.PendingConfirmation,
                createdUtc,
                createdUtc,
                Guid.NewGuid().ToString("N"),
                "refine/1",
                _saved.TemplateCatalogVersion,
                _saved.RecipeJson,
                _saved.CanonicalSha256,
                _saved.RecipeId,
                _saved.Archetype,
                _saved.Dimension,
                _saved.TargetProfile,
                [],
                requestCount: 1,
                new RecipeDraftProvenance(
                    _saved.LineageId,
                    _saved.DraftId,
                    revisionOrdinal: 2,
                    RecipeDraftOrigin.AiRefine,
                    _feedback,
                    [new RecipeGuardRestoration("stages[travel].modules[core].parameters.scale", _saved.DraftId)],
                    _guardRestorationCount));
            // Newest first on purpose: the view must order by ordinal itself, not trust the store's order.
            return [refine, _saved];
        }

        public RecipeDraftRecord? TryGet(string draftId) =>
            _saved is not null && string.Equals(_saved.DraftId, draftId, StringComparison.Ordinal) ? _saved : null;

        public IReadOnlyList<RecipeDraftRecord> ListConfirmedAwaitingBuild() => [];

        public RecipeDraftSaveOutcome AppendVersion(
            string parentDraftId,
            string parentCanonicalSha256,
            RecipeDraftRevision revision,
            DateTimeOffset createdUtc) =>
            throw new NotSupportedException();

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
