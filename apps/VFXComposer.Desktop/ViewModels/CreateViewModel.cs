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
        var record = _runtime.RecipeDrafts.Save(RecipeDraftRecord.Create(result, DateTimeOffset.UtcNow));
        SetCurrentDraft(record);
        RecipeDraftJson = PrettyPrint(record.RecipeJson);
        SetValidationSummaryKey(UiStringKeys.CreateValidationPassed);
        SetRecipeStatus(UiStringKeys.CreateRecipeStatusDraftReady, result.RequestCount);
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
            var record = _runtime.RecipeDrafts.Save(card.Skeleton.CreateDraftRecord(DateTimeOffset.UtcNow));
            SetCurrentDraft(record);
            RecipeDraftJson = PrettyPrint(record.RecipeJson);
            SetValidationSummaryKey(UiStringKeys.CreateValidationPassed);
            SetRecipeStatus(UiStringKeys.CreateRecipeStatusPresetApplied);
        }
        catch (RecipeDraftStoreException exception)
        {
            SetRecipeStatus(UiStringKeys.CreateRecipeStatusDraftStorageFailedWithCode, exception.Code);
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
        SetRecipeStatus(
            UiStringKeys.CreateRecipeStatusValidationFailed,
            result.RequestCount,
            string.Join(", ", result.Issues
                .Where(static issue => issue.Severity == RecipeValidationSeverity.Error)
                .Select(static issue => issue.Code)
                .Distinct(StringComparer.Ordinal)));
        try
        {
            // The failed final state is retained too, so the user can inspect it later (REQ-001 X3).
            SetCurrentDraft(_runtime.RecipeDrafts.Save(RecipeDraftRecord.Create(result, DateTimeOffset.UtcNow)));
        }
        catch (RecipeDraftStoreException)
        {
            // Retention is best-effort for a failed draft; the on-screen report above stays authoritative.
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
        OnPropertyChanged(nameof(RecipeValidationSummary));
        OnPropertyChanged(nameof(CapabilityLine));
        OnPropertyChanged(nameof(ScopeNotice));
        OnPropertyChanged(nameof(SuggestionSentences));
        foreach (var card in PresetCards)
        {
            card.RefreshLocalizedText();
        }
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

    private void SetCurrentDraft(RecipeDraftRecord? record)
    {
        _currentDraft = record;
        OnPropertyChanged(nameof(DraftStatus));
        OnPropertyChanged(nameof(DraftId));
        ConfirmRecipeDraftCommand.NotifyCanExecuteChanged();
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

    /// <summary>
    /// Renders each issue verbatim (stable code, JSON path, validator message) and appends the bilingual repair
    /// suggestion on its own indented line when the code maps to a suggestion key.
    /// </summary>
    private string FormatIssues(IReadOnlyList<RecipeValidationIssue> issues) =>
        string.Join(
            "\n",
            issues.Select(issue =>
                RecipeSuggestionCopy.TryGetCatalogKey(issue.Code, out var catalogKey)
                    ? issue.Code + " " + issue.Path + ": " + issue.Message + "\n    → " + Localization[catalogKey]
                    : issue.Code + " " + issue.Path + ": " + issue.Message));
}
