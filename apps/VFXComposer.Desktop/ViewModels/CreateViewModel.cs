using CommunityToolkit.Mvvm.Input;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Chat;
using VFXComposer.AI.Contracts.Desktop;

namespace VFXComposer.Desktop.ViewModels;

public sealed class CreateViewModel : WorkspacePageViewModel
{
    private readonly IAiDesktopRuntime _runtime;
    private string _recipeName = string.Empty;
    private string _draftNotes = string.Empty;
    private string _chatPrompt = string.Empty;
    private string _chatResponse = string.Empty;
    private string _chatStatus = "Chat is not configured.";

    public CreateViewModel(IAiDesktopRuntime? runtime = null)
        : base(
            "create",
            "Create",
            "Local transient recipe drafts and an explicit ChatLlm prompt.",
            "Drafts stay in memory and cannot write an external workspace.")
    {
        _runtime = runtime ?? AiDesktopRuntime.Unavailable;
        SendChatCommand = new AsyncRelayCommand(SendChatAsync, CanSendChat);
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

    public IAsyncRelayCommand SendChatCommand { get; }

    private bool CanSendChat() => !string.IsNullOrWhiteSpace(ChatPrompt);

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
            ChatStatus = "Chat unavailable.";
        }
    }
}
