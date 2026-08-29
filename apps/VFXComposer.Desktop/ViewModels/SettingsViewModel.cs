using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Desktop;

namespace VFXComposer.Desktop.ViewModels;

/// <summary>
/// Deliberate provider-profile editing surface. Ordinary presentation uses redacted summaries; raw endpoint text is
/// loaded only by <see cref="BeginSelectedProfileEdit"/> and secret text is cleared after every save attempt.
/// </summary>
public sealed class SettingsViewModel : WorkspacePageViewModel
{
    private readonly IAiDesktopRuntime _runtime;
    private string? _selectedProfileId;
    private bool _isEditingProfile;
    private string _profileId = string.Empty;
    private string _profileDisplayName = string.Empty;
    private ProviderOrigin _profileOrigin = ProviderOrigin.Custom;
    private bool _profileEnabled = true;
    private string _profileProtocolId = ProviderProtocols.OpenAiCompatibleV1;
    private string _profileOpaqueEndpoint = string.Empty;
    private int _profileTimeoutSeconds = 30;
    private string _chatCapabilityId = string.Empty;
    private string _chatModelId = string.Empty;
    private string _imageCapabilityId = string.Empty;
    private string _imageModelId = string.Empty;
    private string _secretEntry = string.Empty;
    private string _secretPresence = "No secret configured";
    private string _profileStatus = "Provider settings have not been loaded.";
    private string _chatBindingProfileId = string.Empty;
    private string _chatBindingCapabilityId = string.Empty;
    private string _chatBindingModelId = string.Empty;
    private string _imageBindingProfileId = string.Empty;
    private string _imageBindingCapabilityId = string.Empty;
    private string _imageBindingModelId = string.Empty;
    private string _chatBindingStatus = "Unbound";
    private string _imageBindingStatus = "Unbound";

    public SettingsViewModel(IAiDesktopRuntime? runtime = null)
        : base(
            "settings",
            "Settings",
            "Current-user provider profiles and explicit channel bindings.",
            "No provider profile is configured")
    {
        _runtime = runtime ?? AiDesktopRuntime.Unavailable;
        Profiles = new ObservableCollection<AiDesktopProfileSummary>();
        Origins = Enum.GetValues<ProviderOrigin>();
        BeginNewProfileCommand = new RelayCommand(BeginNewProfile);
        BeginSelectedProfileEditCommand = new RelayCommand(BeginSelectedProfileEdit, CanEditSelectedProfile);
        SaveProfileCommand = new RelayCommand(SaveProfile, () => IsEditingProfile);
        DeleteProfileCommand = new RelayCommand(DeleteSelectedProfile, CanEditSelectedProfile);
        RevokeSecretCommand = new RelayCommand(RevokeSelectedSecret, CanEditSelectedProfile);
        SaveChatBindingCommand = new RelayCommand(SaveChatBinding);
        ClearChatBindingCommand = new RelayCommand(ClearChatBinding);
        SaveImageBindingCommand = new RelayCommand(SaveImageBinding);
        ClearImageBindingCommand = new RelayCommand(ClearImageBinding);
        Reload();
    }

    public ObservableCollection<AiDesktopProfileSummary> Profiles { get; }

    public IReadOnlyList<ProviderOrigin> Origins { get; }

    public string? SelectedProfileId
    {
        get => _selectedProfileId;
        set
        {
            if (SetProperty(ref _selectedProfileId, value))
            {
                NotifySelectionChanged();
            }
        }
    }

    public bool IsEditingProfile
    {
        get => _isEditingProfile;
        private set
        {
            if (SetProperty(ref _isEditingProfile, value))
            {
                SaveProfileCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ProfileId
    {
        get => _profileId;
        set => SetProperty(ref _profileId, value ?? string.Empty);
    }

    public string ProfileDisplayName
    {
        get => _profileDisplayName;
        set => SetProperty(ref _profileDisplayName, value ?? string.Empty);
    }

    public ProviderOrigin ProfileOrigin
    {
        get => _profileOrigin;
        set => SetProperty(ref _profileOrigin, value);
    }

    public bool ProfileEnabled
    {
        get => _profileEnabled;
        set => SetProperty(ref _profileEnabled, value);
    }

    public string ProfileProtocolId
    {
        get => _profileProtocolId;
        set => SetProperty(ref _profileProtocolId, value ?? string.Empty);
    }

    /// <summary>Raw endpoint text is populated only while the deliberate profile editor is open.</summary>
    public string ProfileOpaqueEndpoint
    {
        get => _profileOpaqueEndpoint;
        set => SetProperty(ref _profileOpaqueEndpoint, value ?? string.Empty);
    }

    public int ProfileTimeoutSeconds
    {
        get => _profileTimeoutSeconds;
        set => SetProperty(ref _profileTimeoutSeconds, value);
    }

    public string ChatCapabilityId
    {
        get => _chatCapabilityId;
        set => SetProperty(ref _chatCapabilityId, value ?? string.Empty);
    }

    public string ChatModelId
    {
        get => _chatModelId;
        set => SetProperty(ref _chatModelId, value ?? string.Empty);
    }

    public string ImageCapabilityId
    {
        get => _imageCapabilityId;
        set => SetProperty(ref _imageCapabilityId, value ?? string.Empty);
    }

    public string ImageModelId
    {
        get => _imageModelId;
        set => SetProperty(ref _imageModelId, value ?? string.Empty);
    }

    /// <summary>Entry-only UI state. It is never read back from the runtime and is cleared after saving.</summary>
    public string SecretEntry
    {
        get => _secretEntry;
        set => SetProperty(ref _secretEntry, value ?? string.Empty);
    }

    public string SecretPresence
    {
        get => _secretPresence;
        private set => SetProperty(ref _secretPresence, value ?? string.Empty);
    }

    public string ProfileStatus
    {
        get => _profileStatus;
        private set => SetProperty(ref _profileStatus, value ?? string.Empty);
    }

    public string ChatBindingProfileId
    {
        get => _chatBindingProfileId;
        set => SetProperty(ref _chatBindingProfileId, value ?? string.Empty);
    }

    public string ChatBindingCapabilityId
    {
        get => _chatBindingCapabilityId;
        set => SetProperty(ref _chatBindingCapabilityId, value ?? string.Empty);
    }

    public string ChatBindingModelId
    {
        get => _chatBindingModelId;
        set => SetProperty(ref _chatBindingModelId, value ?? string.Empty);
    }

    public string ImageBindingProfileId
    {
        get => _imageBindingProfileId;
        set => SetProperty(ref _imageBindingProfileId, value ?? string.Empty);
    }

    public string ImageBindingCapabilityId
    {
        get => _imageBindingCapabilityId;
        set => SetProperty(ref _imageBindingCapabilityId, value ?? string.Empty);
    }

    public string ImageBindingModelId
    {
        get => _imageBindingModelId;
        set => SetProperty(ref _imageBindingModelId, value ?? string.Empty);
    }

    public string ChatBindingStatus
    {
        get => _chatBindingStatus;
        private set => SetProperty(ref _chatBindingStatus, value ?? string.Empty);
    }

    public string ImageBindingStatus
    {
        get => _imageBindingStatus;
        private set => SetProperty(ref _imageBindingStatus, value ?? string.Empty);
    }

    public string SecurityNotice =>
        "Secrets are entry-only. Revoke detaches the selected secret and leaves its route fail-closed until deliberate replacement. Endpoint text is shown only while editing this profile; normal summaries are redacted.";

    public IRelayCommand BeginNewProfileCommand { get; }
    public IRelayCommand BeginSelectedProfileEditCommand { get; }
    public IRelayCommand SaveProfileCommand { get; }
    public IRelayCommand DeleteProfileCommand { get; }
    public IRelayCommand RevokeSecretCommand { get; }
    public IRelayCommand SaveChatBindingCommand { get; }
    public IRelayCommand ClearChatBindingCommand { get; }
    public IRelayCommand SaveImageBindingCommand { get; }
    public IRelayCommand ClearImageBindingCommand { get; }

    public void Reload()
    {
        try
        {
            ApplySnapshot(_runtime.Settings.Load());
            ProfileStatus = Profiles.Count == 0 ? "No provider profile is configured." : "Provider settings loaded.";
        }
        catch (AiGatewayException exception)
        {
            Profiles.Clear();
            ChatBindingStatus = "Unavailable: " + exception.Code + ".";
            ImageBindingStatus = "Unavailable: " + exception.Code + ".";
            ProfileStatus = "Provider settings unavailable: " + exception.Code + ".";
        }
        catch
        {
            Profiles.Clear();
            ChatBindingStatus = "Unavailable.";
            ImageBindingStatus = "Unavailable.";
            ProfileStatus = "Provider settings unavailable.";
        }
    }

    private void BeginNewProfile()
    {
        SelectedProfileId = null;
        IsEditingProfile = true;
        ProfileId = string.Empty;
        ProfileDisplayName = string.Empty;
        ProfileOrigin = ProviderOrigin.Custom;
        ProfileEnabled = true;
        ProfileProtocolId = ProviderProtocols.OpenAiCompatibleV1;
        ProfileOpaqueEndpoint = string.Empty;
        ProfileTimeoutSeconds = 30;
        ChatCapabilityId = string.Empty;
        ChatModelId = string.Empty;
        ImageCapabilityId = string.Empty;
        ImageModelId = string.Empty;
        SecretEntry = string.Empty;
        SecretPresence = "No secret configured";
        ProfileStatus = "Editing a new provider profile.";
    }

    private bool CanEditSelectedProfile() => !string.IsNullOrWhiteSpace(SelectedProfileId);

    private void BeginSelectedProfileEdit()
    {
        if (!CanEditSelectedProfile())
        {
            return;
        }

        try
        {
            var edit = _runtime.Settings.BeginProfileEdit(SelectedProfileId!);
            var profile = edit.Profile;
            IsEditingProfile = true;
            ProfileId = profile.Id;
            ProfileDisplayName = profile.DisplayName;
            ProfileOrigin = profile.Origin;
            ProfileEnabled = profile.Enabled;
            ProfileProtocolId = profile.ProtocolId;
            ProfileOpaqueEndpoint = profile.OpaqueEndpoint;
            ProfileTimeoutSeconds = profile.TimeoutSeconds;
            var chat = profile.Capabilities.SingleOrDefault(candidate => candidate.Channel == AiChannel.ChatLlm);
            ChatCapabilityId = chat?.Id ?? string.Empty;
            ChatModelId = chat?.ModelId ?? string.Empty;
            var image = profile.Capabilities.SingleOrDefault(candidate => candidate.Channel == AiChannel.ImageGeneration);
            ImageCapabilityId = image?.Id ?? string.Empty;
            ImageModelId = image?.ModelId ?? string.Empty;
            SecretEntry = string.Empty;
            SecretPresence = edit.HasSecret ? "Secret configured" : "No secret configured";
            ProfileStatus = "Editing selected provider profile.";
        }
        catch (AiGatewayException exception)
        {
            ProfileStatus = "Profile unavailable: " + exception.Code + ".";
        }
        catch
        {
            ProfileStatus = "Profile unavailable.";
        }
    }

    private void SaveProfile()
    {
        if (!IsEditingProfile)
        {
            return;
        }

        try
        {
            var capabilities = CreateEditorCapabilities();
            var snapshot = _runtime.Settings.SaveProfile(
                new AiDesktopProfileDraft(
                    ProfileId,
                    ProfileDisplayName,
                    ProfileOrigin,
                    ProfileEnabled,
                    ProfileProtocolId,
                    ProfileOpaqueEndpoint,
                    ProfileTimeoutSeconds,
                    capabilities),
                SecretEntry);
            ApplySnapshot(snapshot);
            SelectedProfileId = ProfileId;
            SecretPresence = snapshot.Profiles.SingleOrDefault(profile =>
                string.Equals(profile.Id, ProfileId, StringComparison.Ordinal))?.HasSecret == true
                    ? "Secret configured"
                    : "No secret configured";
            ProfileStatus = "Provider profile saved.";
            IsEditingProfile = false;
            ProfileOpaqueEndpoint = string.Empty;
        }
        catch (AiGatewayException exception)
        {
            ProfileStatus = "Provider profile not saved: " + exception.Code + ".";
        }
        catch
        {
            ProfileStatus = "Provider profile not saved.";
        }
        finally
        {
            // Entry-only handling: blank after every outcome, including a failed replace.
            SecretEntry = string.Empty;
        }
    }

    private void DeleteSelectedProfile()
    {
        if (!CanEditSelectedProfile())
        {
            return;
        }

        try
        {
            var snapshot = _runtime.Settings.DeleteProfile(SelectedProfileId!);
            ApplySnapshot(snapshot);
            SelectedProfileId = null;
            IsEditingProfile = false;
            ClearEditor();
            ProfileStatus = "Provider profile deleted and its secret revoked.";
        }
        catch (AiGatewayException exception)
        {
            ProfileStatus = "Provider profile not deleted: " + exception.Code + ".";
        }
        catch
        {
            ProfileStatus = "Provider profile not deleted.";
        }
    }

    private void RevokeSelectedSecret()
    {
        if (!CanEditSelectedProfile())
        {
            return;
        }

        // Entry-only text must never survive a revoke attempt, including an unavailable configuration store.
        SecretEntry = string.Empty;
        try
        {
            var snapshot = _runtime.Settings.RevokeSecret(SelectedProfileId!);
            ApplySnapshot(snapshot);
            SecretPresence = snapshot.Profiles.SingleOrDefault(profile =>
                string.Equals(profile.Id, SelectedProfileId, StringComparison.Ordinal))?.HasSecret == true
                    ? "Secret configured"
                    : "No secret configured";
            ProfileStatus = "Secret detached. This profile is fail-closed until a new secret is saved.";
        }
        catch (AiGatewayException exception)
        {
            ProfileStatus = "Secret not revoked: " + exception.Code + ".";
        }
        catch
        {
            ProfileStatus = "Secret not revoked.";
        }
    }

    private void SaveChatBinding() => SaveBinding(
        AiChannel.ChatLlm,
        ChatBindingProfileId,
        ChatBindingCapabilityId,
        ChatBindingModelId,
        "Chat binding");

    private void ClearChatBinding() => ClearBinding(AiChannel.ChatLlm, "Chat binding");

    private void SaveImageBinding() => SaveBinding(
        AiChannel.ImageGeneration,
        ImageBindingProfileId,
        ImageBindingCapabilityId,
        ImageBindingModelId,
        "Image binding");

    private void ClearImageBinding() => ClearBinding(AiChannel.ImageGeneration, "Image binding");

    private void SaveBinding(AiChannel channel, string profileId, string capabilityId, string modelId, string label)
    {
        try
        {
            var snapshot = _runtime.Settings.SaveChannelBinding(
                new AiDesktopChannelBindingDraft(channel, profileId, capabilityId, modelId));
            ApplySnapshot(snapshot);
            ProfileStatus = label + " saved.";
        }
        catch (AiGatewayException exception)
        {
            ProfileStatus = label + " not saved: " + exception.Code + ".";
        }
        catch
        {
            ProfileStatus = label + " not saved.";
        }
    }

    private void ClearBinding(AiChannel channel, string label)
    {
        try
        {
            ApplySnapshot(_runtime.Settings.ClearChannelBinding(channel));
            ProfileStatus = label + " cleared.";
        }
        catch (AiGatewayException exception)
        {
            ProfileStatus = label + " not cleared: " + exception.Code + ".";
        }
        catch
        {
            ProfileStatus = label + " not cleared.";
        }
    }

    private IReadOnlyList<AiDesktopCapabilityDraft> CreateEditorCapabilities()
    {
        var capabilities = new List<AiDesktopCapabilityDraft>();
        AddCapabilityIfComplete(capabilities, ChatCapabilityId, ChatModelId, AiChannel.ChatLlm);
        AddCapabilityIfComplete(capabilities, ImageCapabilityId, ImageModelId, AiChannel.ImageGeneration);
        return capabilities;
    }

    private static void AddCapabilityIfComplete(
        ICollection<AiDesktopCapabilityDraft> values,
        string capabilityId,
        string modelId,
        AiChannel channel)
    {
        var hasCapability = !string.IsNullOrEmpty(capabilityId);
        var hasModel = !string.IsNullOrEmpty(modelId);
        if (hasCapability != hasModel)
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }

        if (hasCapability)
        {
            values.Add(new AiDesktopCapabilityDraft(capabilityId, channel, modelId));
        }
    }

    private void ApplySnapshot(AiDesktopSettingsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var currentSelection = SelectedProfileId;
        Profiles.Clear();
        foreach (var profile in snapshot.Profiles)
        {
            Profiles.Add(profile);
        }

        if (currentSelection is not null && !Profiles.Any(profile =>
            string.Equals(profile.Id, currentSelection, StringComparison.Ordinal)))
        {
            SelectedProfileId = null;
        }

        ApplyBinding(
            snapshot.Bindings.SingleOrDefault(binding => binding.Channel == AiChannel.ChatLlm),
            AiChannel.ChatLlm);
        ApplyBinding(
            snapshot.Bindings.SingleOrDefault(binding => binding.Channel == AiChannel.ImageGeneration),
            AiChannel.ImageGeneration);
        ChatBindingStatus = StatusText(snapshot, AiChannel.ChatLlm);
        ImageBindingStatus = StatusText(snapshot, AiChannel.ImageGeneration);
    }

    private void ApplyBinding(AiDesktopChannelBinding? binding, AiChannel channel)
    {
        var profileId = binding?.ProfileId ?? string.Empty;
        var capabilityId = binding?.CapabilityId ?? string.Empty;
        var modelId = binding?.ModelId ?? string.Empty;
        if (channel == AiChannel.ChatLlm)
        {
            ChatBindingProfileId = profileId;
            ChatBindingCapabilityId = capabilityId;
            ChatBindingModelId = modelId;
            return;
        }

        ImageBindingProfileId = profileId;
        ImageBindingCapabilityId = capabilityId;
        ImageBindingModelId = modelId;
    }

    private static string StatusText(AiDesktopSettingsSnapshot snapshot, AiChannel channel)
    {
        var status = snapshot.ChannelStatuses.SingleOrDefault(candidate => candidate.Channel == channel);
        return status is null
            ? "Unavailable"
            : status.ReasonCode is null
                ? status.State.ToString()
                : status.State + ": " + status.ReasonCode + ".";
    }

    private void ClearEditor()
    {
        ProfileId = string.Empty;
        ProfileDisplayName = string.Empty;
        ProfileOpaqueEndpoint = string.Empty;
        ChatCapabilityId = string.Empty;
        ChatModelId = string.Empty;
        ImageCapabilityId = string.Empty;
        ImageModelId = string.Empty;
        SecretEntry = string.Empty;
        SecretPresence = "No secret configured";
    }

    private void NotifySelectionChanged()
    {
        BeginSelectedProfileEditCommand.NotifyCanExecuteChanged();
        DeleteProfileCommand.NotifyCanExecuteChanged();
        RevokeSecretCommand.NotifyCanExecuteChanged();
    }
}
