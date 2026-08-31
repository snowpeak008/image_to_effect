using System.Text.Json;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Chat;
using VFXComposer.AI.Contracts.Desktop;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.ViewModels;

public sealed class CreateViewModel : WorkspacePageViewModel
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    private readonly IAiDesktopRuntime _runtime;
    private string _recipeName = string.Empty;
    private string _draftNotes = string.Empty;
    private string _chatPrompt = string.Empty;
    private string _chatResponse = string.Empty;
    private string _chatStatus = "Chat is not configured.";
    private string _effectDescription = string.Empty;
    private string _recipeStatus = "Describe an effect, then generate a draft.";
    private string _recipeDraftJson = string.Empty;
    private string _recipeValidationSummary = string.Empty;
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

    public string ChatStatus
    {
        get => _chatStatus;
        private set => SetProperty(ref _chatStatus, value ?? string.Empty);
    }

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

    public string RecipeStatus
    {
        get => _recipeStatus;
        private set => SetProperty(ref _recipeStatus, value ?? string.Empty);
    }

    /// <summary>The retained draft JSON (indented for reading; confirmation binds to the canonical hash).</summary>
    public string RecipeDraftJson
    {
        get => _recipeDraftJson;
        private set => SetProperty(ref _recipeDraftJson, value ?? string.Empty);
    }

    public string RecipeValidationSummary
    {
        get => _recipeValidationSummary;
        private set => SetProperty(ref _recipeValidationSummary, value ?? string.Empty);
    }

    /// <summary>The lifecycle state of the currently displayed draft, if one is retained.</summary>
    public RecipeDraftStatus? DraftStatus => _currentDraft?.Status;

    public string? DraftId => _currentDraft?.DraftId;

    public IAsyncRelayCommand SendChatCommand { get; }

    public IAsyncRelayCommand GenerateRecipeCommand { get; }

    public ICommand CancelGenerateRecipeCommand { get; }

    public IRelayCommand ConfirmRecipeDraftCommand { get; }

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
            ChatStatus = "Chat completed.";
        }
        catch (ChatChannelException exception)
        {
            ChatStatus = "Chat unavailable: " + exception.Code + ".";
        }
        catch (AiGatewayException exception)
        {
            ChatStatus = "Chat unavailable: " + exception.Code + ".";
        }
        catch (OperationCanceledException)
        {
            ChatStatus = "Chat cancelled.";
        }
        catch
        {
            // The typed catches above carry the failure's own stable code; an unexpected failure has
            // none, so it settles under this fixed code rather than an untraceable bare message.
            ChatStatus = "Chat unavailable: " + UnexpectedFailureCode + ".";
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
        RecipeValidationSummary = string.Empty;
        RecipeStatus = "Generating recipe draft...";
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
                    RecipeStatus = "Generation cancelled.";
                    break;
                default:
                    RecipeStatus = "Generation unavailable: " + result.ChannelError + ".";
                    break;
            }
        }
        catch (RecipeDraftStoreException exception)
        {
            RecipeStatus = "Draft storage failed: " + exception.Code + ".";
        }
        catch (ChatChannelException exception)
        {
            RecipeStatus = "Generation unavailable: " + exception.Code + ".";
        }
        catch (AiGatewayException exception)
        {
            RecipeStatus = "Generation unavailable: " + exception.Code + ".";
        }
        catch (OperationCanceledException)
        {
            RecipeStatus = "Generation cancelled.";
        }
        catch
        {
            RecipeStatus = "Generation unavailable: " + UnexpectedFailureCode + ".";
        }
    }

    private void PresentDraftedResult(RecipeGenerationResult result)
    {
        var record = _runtime.RecipeDrafts.Save(RecipeDraftRecord.Create(result, DateTimeOffset.UtcNow));
        SetCurrentDraft(record);
        RecipeDraftJson = PrettyPrint(record.RecipeJson);
        RecipeValidationSummary = "L1 validation passed.";
        RecipeStatus = "Draft ready after " + result.RequestCount + " request(s) - confirm to queue it for build.";
    }

    private void PresentValidationFailure(RecipeGenerationResult result)
    {
        RecipeDraftJson = result.LastOutputText ?? string.Empty;
        RecipeValidationSummary = FormatIssues(result.Issues);
        RecipeStatus = "Validation failed after " + result.RequestCount + " request(s): "
            + string.Join(", ", result.Issues
                .Where(static issue => issue.Severity == RecipeValidationSeverity.Error)
                .Select(static issue => issue.Code)
                .Distinct(StringComparer.Ordinal))
            + ".";
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
            RecipeStatus = "Draft confirmed - awaiting build.";
        }
        catch (RecipeDraftStoreException exception)
        {
            RecipeStatus = "Confirmation failed: " + exception.Code + ".";
        }
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

    private static string FormatIssues(IReadOnlyList<RecipeValidationIssue> issues) =>
        string.Join(
            "\n",
            issues.Select(static issue => issue.Code + " " + issue.Path + ": " + issue.Message));
}
