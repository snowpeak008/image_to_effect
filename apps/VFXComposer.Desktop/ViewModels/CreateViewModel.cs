using System.Text.Json;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Chat;
using VFXComposer.AI.Contracts.Desktop;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Recipes;
using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.ViewModels;

public sealed class CreateViewModel : WorkspacePageViewModel
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    /// <summary>
    /// Closed card copy map: every committed preset id carries exactly one bilingual title/description key pair.
    /// A preset added without copy fails the construction below at first use, never rendering a raw identifier.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (string TitleKey, string DescriptionKey)> PresetCopyKeys =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["fire-bolt"] =
                (UiStringKeys.CreatePresetFireBoltTitle, UiStringKeys.CreatePresetFireBoltDescription),
            ["trailing-fireball"] =
                (UiStringKeys.CreatePresetTrailingFireballTitle, UiStringKeys.CreatePresetTrailingFireballDescription),
            ["bursting-fireball"] =
                (UiStringKeys.CreatePresetBurstingFireballTitle, UiStringKeys.CreatePresetBurstingFireballDescription),
            ["shock-impact"] =
                (UiStringKeys.CreatePresetShockImpactTitle, UiStringKeys.CreatePresetShockImpactDescription),
            ["launch-flash"] =
                (UiStringKeys.CreatePresetLaunchFlashTitle, UiStringKeys.CreatePresetLaunchFlashDescription),
            ["ember-streak"] =
                (UiStringKeys.CreatePresetEmberStreakTitle, UiStringKeys.CreatePresetEmberStreakDescription),
        };

    private static readonly string[] SuggestionSentenceKeys =
    [
        UiStringKeys.CreateSuggestionSentence1,
        UiStringKeys.CreateSuggestionSentence2,
        UiStringKeys.CreateSuggestionSentence3,
    ];

    private readonly IAiDesktopRuntime _runtime;
    private string _recipeName = string.Empty;
    private string _draftNotes = string.Empty;
    private string _chatPrompt = string.Empty;
    private string _chatResponse = string.Empty;
    private string _chatStatusKey = UiStringKeys.CreateChatStatusNotConfigured;
    private object?[] _chatStatusArguments = [];
    private string _effectDescription = string.Empty;
    private string _recipeStatusKey = UiStringKeys.CreateRecipeStatusInitial;
    private object?[] _recipeStatusArguments = [];
    private string _recipeDraftJson = string.Empty;
    private string? _validationSummaryKey;
    private IReadOnlyList<RecipeValidationIssue> _validationIssueList = [];
    private IReadOnlyList<(string Key, object?[] Arguments)> _retentionLines = [];
    private RecipeDraftRecord? _currentDraft;

    public CreateViewModel(LocalizationService localization, IAiDesktopRuntime? runtime = null)
        : base(
            localization,
            "create",
            UiStringKeys.CreateTitle,
            UiStringKeys.CreateDescription,
            UiStringKeys.CreateEmptyState)
    {
        _runtime = runtime ?? AiDesktopRuntime.Unavailable;
        SendChatCommand = new AsyncRelayCommand(SendChatAsync, CanSendChat);
        GenerateRecipeCommand = new AsyncRelayCommand(GenerateRecipeAsync, CanGenerateRecipe);
        CancelGenerateRecipeCommand = GenerateRecipeCommand.CreateCancelCommand();
        ConfirmRecipeDraftCommand = new RelayCommand(ConfirmRecipeDraft, CanConfirmRecipeDraft);
        ApplyPresetCommand = new RelayCommand<PresetCardViewModel>(ApplyPreset);
        UseSuggestionCommand = new RelayCommand<string>(UseSuggestion);
        ParameterPanel = new ParameterPanelViewModel(localization);
        ApplyParameterEditsCommand = new RelayCommand(ApplyParameterEdits, CanApplyParameterEdits);
        Lineage = new LineageViewModel(localization);
        Lineage.PropertyChanged += OnLineagePropertyChanged;
        RevertToSelectedVersionCommand = new RelayCommand(RevertToSelectedVersion, Lineage.CanArmRevert);
        ConfirmRevertCommand = new RelayCommand(ConfirmRevert, () => Lineage.IsRevertPending);
        CancelRevertCommand = new RelayCommand(Lineage.CancelRevert, () => Lineage.IsRevertPending);
        PresetCards = RecipePresetSkeletons.All
            .Select(skeleton =>
            {
                var (titleKey, descriptionKey) = PresetCopyKeys[skeleton.PresetId];
                return new PresetCardViewModel(localization, skeleton, titleKey, descriptionKey);
            })
            .ToArray();
    }

    public string RecipeName
    {
        get => _recipeName;
        set => SetProperty(ref _recipeName, value ?? string.Empty);
    }

    public string DraftNotes
    {
        get => _draftNotes;
        set => SetProperty(ref _draftNotes, value ?? string.Empty);
    }

    /// <summary>User-entered text for an explicit ChatLlm request. It is never formatted into diagnostics.</summary>
    public string ChatPrompt
    {
        get => _chatPrompt;
        set
        {
            if (SetProperty(ref _chatPrompt, value ?? string.Empty))
            {
                SendChatCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Typed chat text returned by the one selected route, not a raw provider payload.</summary>
    public string ChatResponse
    {
        get => _chatResponse;
        private set => SetProperty(ref _chatResponse, value ?? string.Empty);
    }

    public string ChatStatus => Localized(_chatStatusKey, _chatStatusArguments);

    /// <summary>User-entered effect description for structured generation. It never reaches diagnostics.</summary>
    public string EffectDescription
    {
        get => _effectDescription;
        set
        {
            if (SetProperty(ref _effectDescription, value ?? string.Empty))
            {
                GenerateRecipeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string RecipeStatus => Localized(_recipeStatusKey, _recipeStatusArguments);

    /// <summary>
    /// The typed retention report of the last save (REQ-004-33): superseded confirmations, level-1 trims and level-2
    /// evictions, one catalog line each. Empty when the save retained everything, and cleared again by the next
    /// action that saves nothing (a refused edit, a confirmation), so it never outlives the save it describes.
    /// </summary>
    public string RecipeRetentionNotice =>
        string.Join("\n", _retentionLines.Select(line => Localized(line.Key, line.Arguments)));

    public bool HasRetentionNotice => _retentionLines.Count > 0;

    /// <summary>The retained draft JSON (indented for reading; confirmation binds to the canonical hash).</summary>
    public string RecipeDraftJson
    {
        get => _recipeDraftJson;
        private set => SetProperty(ref _recipeDraftJson, value ?? string.Empty);
    }

    /// <summary>
    /// Either a catalog verdict line or the validator's issue report: stable codes, JSON paths and validator
    /// messages verbatim, each followed by its bilingual repair suggestion when the code maps to one (F8a1).
    /// </summary>
    public string RecipeValidationSummary => _validationSummaryKey is null
        ? FormatIssues(_validationIssueList)
        : Localization[_validationSummaryKey];

    /// <summary>The lifecycle state of the currently displayed draft, if one is retained.</summary>
    public RecipeDraftStatus? DraftStatus => _currentDraft?.Status;

    public string? DraftId => _currentDraft?.DraftId;

    public IAsyncRelayCommand SendChatCommand { get; }

    public IAsyncRelayCommand GenerateRecipeCommand { get; }

    public ICommand CancelGenerateRecipeCommand { get; }

    public IRelayCommand ConfirmRecipeDraftCommand { get; }

    /// <summary>Persists one example card's committed skeleton as a fresh pending draft. Zero AI, zero network.</summary>
    public IRelayCommand<PresetCardViewModel> ApplyPresetCommand { get; }

    /// <summary>Copies one suggestion sentence into the description box. It never triggers generation.</summary>
    public IRelayCommand<string> UseSuggestionCommand { get; }

    /// <summary>The head draft's declared parameters, editable within the catalog bounds (REQ-004 §9).</summary>
    public ParameterPanelViewModel ParameterPanel { get; }

    /// <summary>
    /// Runs the pending panel edits through the editor and appends the accepted document as a human_edit version
    /// after the head. Zero AI, zero network: neither the editor nor the store touches the gateway (REQ-004-44).
    /// </summary>
    public IRelayCommand ApplyParameterEditsCommand { get; }

    /// <summary>The head's lineage as a selectable version list with the inline revert confirmation (REQ-004 §7.3).</summary>
    public LineageViewModel Lineage { get; }

    /// <summary>
    /// Step one of the revert: arms the confirmation for the selected older version and shows how many newer versions
    /// it would delete. Nothing is written yet. Disabled for the head and without a selection.
    /// </summary>
    public IRelayCommand RevertToSelectedVersionCommand { get; }

    /// <summary>
    /// Step two: truncates the lineage after the armed version (REQ-004-25). The store deletes the newer versions or
    /// refuses with a stable code when one of them is an audit record (REQ-004-26). Zero network either way.
    /// </summary>
    public IRelayCommand ConfirmRevertCommand { get; }

    /// <summary>Disarms the pending revert; the store is not touched.</summary>
    public IRelayCommand CancelRevertCommand { get; }

    /// <summary>The fixed simple-mode example cards, one per committed preset skeleton.</summary>
    public IReadOnlyList<PresetCardViewModel> PresetCards { get; }

    /// <summary>
    /// The capability line, derived entirely from the committed catalog snapshot: template count, parameter
    /// count, buildable archetypes and dimensions, catalog version and contract revision. Nothing is hard-coded,
    /// so a snapshot re-export changes this text without a code change (REQ-004-04).
    /// </summary>
    public string CapabilityLine
    {
        get
        {
            var snapshot = RecipeTemplateCatalogSnapshot.Default;
            return Localization.Format(
                UiStringKeys.CreateCapabilityLine,
                snapshot.Templates.Count,
                snapshot.Templates.Sum(static template => template.Parameters.Count),
                string.Join(", ", snapshot.BuildableArchetypes),
                string.Join(", ", snapshot.BuildableDimensions),
                snapshot.TemplateCatalogVersion,
                snapshot.ContractRevision);
        }
    }

    /// <summary>The honest scope line, derived from the snapshot's buildable sets rather than a fixed promise.</summary>
    public string ScopeNotice
    {
        get
        {
            var snapshot = RecipeTemplateCatalogSnapshot.Default;
            return Localization.Format(
                UiStringKeys.CreateScopeNotice,
                string.Join(", ", snapshot.BuildableDimensions),
                string.Join(", ", snapshot.BuildableArchetypes));
        }
    }

    /// <summary>Clickable example sentences; clicking only fills the description box.</summary>
    public IReadOnlyList<string> SuggestionSentences =>
        SuggestionSentenceKeys.Select(key => Localization[key]).ToArray();

    /// <summary>
    /// The copyable command of the honest build handoff. The repository-relative manifest path keeps the line
    /// machine-neutral; the surrounding notice explains the manifest step and the editor-close requirement.
    /// </summary>
    public string BuildCommandLine => "dotnet run --project apps/VFXComposer.Cli -- batch run <manifest.json>";

    private bool CanSendChat() => !string.IsNullOrWhiteSpace(ChatPrompt);

    /// <summary>Stable code for an unexpected failure that carries no code of its own (F1 audit ②).</summary>
    private const string UnexpectedFailureCode = "VFXUI001";

    private async Task SendChatAsync()
    {
        if (!CanSendChat())
        {
            return;
        }

        try
        {
            // This is the sole Create-side request entry point. It does not preflight, select a fallback, or mutate
            // Settings; the runtime records the selected route's observed health from this request result.
            var response = await _runtime.Gateway.ChatAsync(
                new ChatRequest(
                    Guid.NewGuid().ToString("N"),
                    [new ChatMessage(ChatRole.User, ChatPrompt)]),
                CancellationToken.None);
            ChatResponse = response.Text;
            SetChatStatus(UiStringKeys.CreateChatStatusCompleted);
        }
        catch (ChatChannelException exception)
        {
            SetChatStatus(UiStringKeys.CreateChatStatusUnavailableWithCode, exception.Code);
        }
        catch (AiGatewayException exception)
        {
            SetChatStatus(UiStringKeys.CreateChatStatusUnavailableWithCode, exception.Code);
        }
        catch (OperationCanceledException)
        {
            SetChatStatus(UiStringKeys.CreateChatStatusCancelled);
        }
        catch
        {
            // The typed catches above carry the failure's own stable code; an unexpected failure has
            // none, so it settles under this fixed code rather than an untraceable bare message.
            SetChatStatus(UiStringKeys.CreateChatStatusUnavailableWithCode, UnexpectedFailureCode);
        }
    }

    private bool CanGenerateRecipe() => !string.IsNullOrWhiteSpace(EffectDescription);

    private async Task GenerateRecipeAsync(CancellationToken cancellationToken)
    {
        if (!CanGenerateRecipe())
        {
            return;
        }

        SetCurrentDraft(null);
        RecipeDraftJson = string.Empty;
        SetValidationSummaryKey(null);
        SetRetentionLines([]);
        SetRecipeStatus(UiStringKeys.CreateRecipeStatusGenerating);
        try
        {
            // The one explicit generate action: the request (plus its ADR-007 repair budget) is the only network
            // activity this page ever starts, and it goes through the already bound ChatLlm route.
            var result = await _runtime.RecipeGeneration.GenerateAsync(
                new RecipeGenerationRequest(Guid.NewGuid().ToString("N"), EffectDescription),
                cancellationToken);
            switch (result.Outcome)
            {
                case RecipeGenerationOutcome.Drafted:
                    PresentDraftedResult(result);
                    break;
                case RecipeGenerationOutcome.ValidationFailed:
                    PresentValidationFailure(result);
                    break;
                case RecipeGenerationOutcome.Cancelled:
                    SetRecipeStatus(UiStringKeys.CreateRecipeStatusGenerationCancelled);
                    break;
                default:
                    SetRecipeStatus(
                        UiStringKeys.CreateRecipeStatusGenerationUnavailableWithCode,
                        result.ChannelError);
                    break;
            }
        }
        catch (RecipeDraftStoreException exception)
        {
            SetRecipeStatus(UiStringKeys.CreateRecipeStatusDraftStorageFailedWithCode, exception.Code);
        }
        catch (ChatChannelException exception)
        {
            SetRecipeStatus(UiStringKeys.CreateRecipeStatusGenerationUnavailableWithCode, exception.Code);
        }
        catch (AiGatewayException exception)
        {
            SetRecipeStatus(UiStringKeys.CreateRecipeStatusGenerationUnavailableWithCode, exception.Code);
        }
        catch (OperationCanceledException)
        {
            SetRecipeStatus(UiStringKeys.CreateRecipeStatusGenerationCancelled);
        }
        catch
        {
            SetRecipeStatus(UiStringKeys.CreateRecipeStatusGenerationUnavailableWithCode, UnexpectedFailureCode);
        }
    }

    private void PresentDraftedResult(RecipeGenerationResult result)
    {
        // An AI draft is a new lineage root (origin ai_draft); the typed outcome reports any lineage the level-2
        // cap evicted so the page can say so instead of dropping it silently (REQ-004-33).
        var outcome = _runtime.RecipeDrafts.SaveVersion(RecipeDraftRecord.Create(result, DateTimeOffset.UtcNow));
        SetCurrentDraft(outcome.Record);
        RecipeDraftJson = PrettyPrint(outcome.Record.RecipeJson);
        SetValidationSummaryKey(UiStringKeys.CreateValidationPassed);
        SetRecipeStatus(UiStringKeys.CreateRecipeStatusDraftReady, result.RequestCount);
        PresentRetention(outcome);
    }

    /// <summary>
    /// The card click: the committed skeleton lands as a fresh pending draft through the same store and hash
    /// discipline as an AI draft. No prompt is built and no request budget is touched (REQ-004-03).
    /// </summary>
    private void ApplyPreset(PresetCardViewModel? card)
    {
        if (card is null)
        {
            return;
        }

        try
        {
            var outcome = _runtime.RecipeDrafts.SaveVersion(card.Skeleton.CreateDraftRecord(DateTimeOffset.UtcNow));
            SetCurrentDraft(outcome.Record);
            RecipeDraftJson = PrettyPrint(outcome.Record.RecipeJson);
            SetValidationSummaryKey(UiStringKeys.CreateValidationPassed);
            SetRecipeStatus(UiStringKeys.CreateRecipeStatusPresetApplied);
            PresentRetention(outcome);
        }
        catch (RecipeDraftStoreException exception)
        {
            SetRecipeStatus(UiStringKeys.CreateRecipeStatusDraftStorageFailedWithCode, exception.Code);
        }
    }

    private bool CanApplyParameterEdits() => _currentDraft is { CanonicalSha256: not null };

    private void ApplyParameterEdits()
    {
        if (_currentDraft is not { CanonicalSha256: not null } head)
        {
            return;
        }

        // The editor is a pure function over the head's JSON: type discipline, inclusive bounds, structural
        // immutability, L1 and L1.5 all happen here, before anything is persisted (REQ-004-43).
        var result = RecipeParameterEditor.Apply(head.RecipeJson, ParameterPanel.CollectEdits());
        if (!result.IsAccepted)
        {
            ParameterPanel.PresentIssues(result.Issues);
            SetRecipeStatus(UiStringKeys.CreateRecipeStatusParameterEditRejected, result.Issues.Count);
            // The retention notice belongs to the save that produced it; a refused edit saved nothing, so an
            // earlier trim/supersede line must not sit next to this rejection as if it were caused by it.
            SetRetentionLines([]);
            // Likewise the verdict box: the previous head's "passed" line would read as a verdict on the refused
            // edit, whose real verdict is the panel report. A neutral pointer replaces it (F8b3 audit B#6).
            SetValidationSummaryKey(UiStringKeys.CreateValidationEditRefusedSeePanel);
            return;
        }

        try
        {
            // The hand edit lands as a new version after the head (origin human_edit, pending confirmation); the
            // head's hash proves the user edited what was presented. The head's own record is never rewritten.
            var outcome = _runtime.RecipeDrafts.AppendVersion(
                head.DraftId,
                head.CanonicalSha256,
                RecipeParameterEditor.CreateHumanEditRevision(head, result),
                DateTimeOffset.UtcNow);
            SetCurrentDraft(outcome.Record);
            RecipeDraftJson = PrettyPrint(outcome.Record.RecipeJson);
            if (result.Issues.Count == 0)
            {
                SetValidationSummaryKey(UiStringKeys.CreateValidationPassed);
            }
            else
            {
                // L1.5 findings on the accepted document are warnings: they show, they do not block (F8a1 ruling).
                SetValidationIssues(result.Issues);
            }

            SetRecipeStatus(UiStringKeys.CreateRecipeStatusHumanEditSaved, outcome.Record.RevisionOrdinal);
            PresentRetention(outcome);
        }
        catch (RecipeDraftStoreException exception)
        {
            SetRecipeStatus(UiStringKeys.CreateRecipeStatusDraftStorageFailedWithCode, exception.Code);
        }
    }

    /// <summary>Turns the typed retention outcome into catalog lines; a fully retained save leaves the notice empty.</summary>
    private void PresentRetention(RecipeDraftSaveOutcome outcome)
    {
        var lines = new List<(string Key, object?[] Arguments)>();
        if (outcome.SupersededDraftIds.Count > 0)
        {
            lines.Add((UiStringKeys.CreateRetentionNoticeSuperseded, []));
        }

        if (outcome.TrimmedDraftIds.Count > 0)
        {
            lines.Add((UiStringKeys.CreateRetentionNoticeTrimmed, [outcome.TrimmedDraftIds.Count]));
        }

        if (outcome.EvictedLineageIds.Count > 0)
        {
            lines.Add((UiStringKeys.CreateRetentionNoticeEvicted, [outcome.EvictedLineageIds.Count, outcome.EvictedVersionCount]));
        }

        SetRetentionLines(lines);
    }

    private void RevertToSelectedVersion() => Lineage.ArmRevert();

    private void ConfirmRevert()
    {
        if (Lineage.PendingTarget is not { } target)
        {
            return;
        }

        Lineage.CancelRevert();
        // Any retention line on screen describes an earlier save; a truncation is a different event and reports its
        // own count in the status line (REQ-004-33), so the stale line must not sit next to it.
        SetRetentionLines([]);
        try
        {
            // The store recomputes the deletable set under its lock and refuses when a newer version is confirmed,
            // built or build-failed; the page never deletes anything itself (REQ-004-26).
            var outcome = _runtime.RecipeDrafts.TruncateAfter(target.DraftId);
            SetCurrentDraft(outcome.Head);
            RecipeDraftJson = PrettyPrint(outcome.Head.RecipeJson);
            PresentRetainedHeadValidation(outcome.Head);
            SetRecipeStatus(
                UiStringKeys.CreateRecipeStatusRevertedToVersion,
                outcome.Head.RevisionOrdinal,
                outcome.RemovedDraftIds.Count);
        }
        catch (RecipeDraftStoreException exception)
        {
            SetRecipeStatus(
                exception.Code == RecipeDraftStoreErrorCode.TruncationBlocked
                    ? UiStringKeys.CreateRecipeStatusRevertBlockedWithCode
                    : UiStringKeys.CreateRecipeStatusRevertFailedWithCode,
                exception.Code);
        }
    }

    /// <summary>
    /// A retained version passed L1 when it was saved: every producer of a hashed record (the generation service,
    /// the parameter editor, the preset skeletons' build-time tests) runs L1 before computing the hash; the store
    /// itself does not re-validate. Only the L1.5 warnings are re-derived here, by the same pure prevalidator the
    /// editor runs. A failed root shows its stored report.
    /// </summary>
    private void PresentRetainedHeadValidation(RecipeDraftRecord head)
    {
        if (head.CanonicalSha256 is null)
        {
            SetValidationIssues(head.Issues);
            return;
        }

        var warnings = RecipeCatalogPrevalidator.Prevalidate(head.RecipeJson);
        if (warnings.Count == 0)
        {
            SetValidationSummaryKey(UiStringKeys.CreateValidationPassed);
        }
        else
        {
            SetValidationIssues(warnings);
        }
    }

    private void OnLineagePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(LineageViewModel.SelectedVersion) or nameof(LineageViewModel.IsRevertPending))
        {
            RevertToSelectedVersionCommand.NotifyCanExecuteChanged();
            ConfirmRevertCommand.NotifyCanExecuteChanged();
            CancelRevertCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Re-lists the head's lineage from the store; every path that moves <c>_currentDraft</c> comes through here. A
    /// refused listing is shown as its stable code in the view rather than swallowed.
    /// </summary>
    private void RefreshLineage()
    {
        if (_currentDraft is null)
        {
            Lineage.Load([]);
            return;
        }

        try
        {
            Lineage.Load(_runtime.RecipeDrafts.ListLineage(_currentDraft.LineageId));
        }
        catch (RecipeDraftStoreException exception)
        {
            Lineage.LoadFailed(exception.Code);
        }
    }

    private void UseSuggestion(string? sentence)
    {
        if (!string.IsNullOrWhiteSpace(sentence))
        {
            EffectDescription = sentence;
        }
    }

    private void PresentValidationFailure(RecipeGenerationResult result)
    {
        RecipeDraftJson = result.LastOutputText ?? string.Empty;
        SetValidationIssues(result.Issues);
        var errorCodes = string.Join(", ", result.Issues
            .Where(static issue => issue.Severity == RecipeValidationSeverity.Error)
            .Select(static issue => issue.Code)
            .Distinct(StringComparer.Ordinal));
        try
        {
            // The failed final state is retained too, so the user can inspect it later (REQ-001 X3).
            var outcome = _runtime.RecipeDrafts.SaveVersion(RecipeDraftRecord.Create(result, DateTimeOffset.UtcNow));
            SetCurrentDraft(outcome.Record);
            SetRecipeStatus(UiStringKeys.CreateRecipeStatusValidationFailed, result.RequestCount, errorCodes);
            PresentRetention(outcome);
        }
        catch (RecipeDraftStoreException exception)
        {
            // The on-screen report stays authoritative, and the store's stable code is said out loud rather than
            // swallowed: the user learns the failed draft is not retained and why.
            SetRecipeStatus(
                UiStringKeys.CreateRecipeStatusValidationFailedNotRetainedWithCode,
                result.RequestCount,
                errorCodes,
                exception.Code);
        }
    }

    private bool CanConfirmRecipeDraft() =>
        _currentDraft is { Status: RecipeDraftStatus.PendingConfirmation, CanonicalSha256: not null };

    private void ConfirmRecipeDraft()
    {
        if (_currentDraft is not { CanonicalSha256: not null } draft)
        {
            return;
        }

        try
        {
            // Confirmation only flips the retained record's state; the build itself is a later milestone (F2).
            // The canonical hash binds this click to the exact draft content that was presented.
            SetCurrentDraft(_runtime.RecipeDrafts.Confirm(draft.DraftId, draft.CanonicalSha256));
            SetRecipeStatus(UiStringKeys.CreateRecipeStatusDraftConfirmed);
            // Confirmation saves no version, so the previous save's retention report has run its course.
            SetRetentionLines([]);
        }
        catch (RecipeDraftStoreException exception)
        {
            SetRecipeStatus(UiStringKeys.CreateRecipeStatusConfirmationFailedWithCode, exception.Code);
        }
    }

    protected override void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(ChatStatus));
        OnPropertyChanged(nameof(RecipeStatus));
        OnPropertyChanged(nameof(RecipeRetentionNotice));
        OnPropertyChanged(nameof(RecipeValidationSummary));
        OnPropertyChanged(nameof(CapabilityLine));
        OnPropertyChanged(nameof(ScopeNotice));
        OnPropertyChanged(nameof(SuggestionSentences));
        foreach (var card in PresetCards)
        {
            card.RefreshLocalizedText();
        }

        ParameterPanel.RefreshLocalizedText();
        Lineage.RefreshLocalizedText();
    }

    // Status lines keep their key and arguments instead of a rendered string, so a language switch re-renders them.
    private void SetChatStatus(string key, params object?[] arguments)
    {
        _chatStatusKey = key;
        _chatStatusArguments = arguments;
        OnPropertyChanged(nameof(ChatStatus));
    }

    private void SetRecipeStatus(string key, params object?[] arguments)
    {
        _recipeStatusKey = key;
        _recipeStatusArguments = arguments;
        OnPropertyChanged(nameof(RecipeStatus));
    }

    private void SetValidationSummaryKey(string? key)
    {
        _validationSummaryKey = key;
        _validationIssueList = [];
        OnPropertyChanged(nameof(RecipeValidationSummary));
    }

    /// <summary>Issue reports keep the typed list so a language switch re-renders the suggestion lines.</summary>
    private void SetValidationIssues(IReadOnlyList<RecipeValidationIssue> issues)
    {
        _validationSummaryKey = null;
        _validationIssueList = issues;
        OnPropertyChanged(nameof(RecipeValidationSummary));
    }

    private void SetRetentionLines(IReadOnlyList<(string Key, object?[] Arguments)> lines)
    {
        _retentionLines = lines;
        OnPropertyChanged(nameof(RecipeRetentionNotice));
        OnPropertyChanged(nameof(HasRetentionNotice));
    }

    private void SetCurrentDraft(RecipeDraftRecord? record)
    {
        _currentDraft = record;
        ParameterPanel.Load(record);
        RefreshLineage();
        OnPropertyChanged(nameof(DraftStatus));
        OnPropertyChanged(nameof(DraftId));
        ConfirmRecipeDraftCommand.NotifyCanExecuteChanged();
        ApplyParameterEditsCommand.NotifyCanExecuteChanged();
    }

    private static string PrettyPrint(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, IndentedJson);
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private string FormatIssues(IReadOnlyList<RecipeValidationIssue> issues) => RecipeIssueReport.Render(Localization, issues);
}
