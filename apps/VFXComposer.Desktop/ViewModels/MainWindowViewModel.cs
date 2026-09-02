using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VFXComposer.AI.Contracts.Desktop;
using VFXComposer.Client;
using VFXComposer.Desktop.Localization;
using VFXComposer.Desktop.Services;
using VFXComposer.Jobs;

namespace VFXComposer.Desktop.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IAsyncDisposable
{
    private readonly VfxComposerClient _client;
    private IUserModeDesktopSession? _session;
    private IProjectSelectionDialog? _selectionDialog;
    private IUiDispatcher? _dispatcher;
    private readonly IInMemoryDiagnosticSink _diagnostics;
    private readonly IUiErrorBoundary _errorBoundary;
    private readonly IAiDesktopRuntime _aiRuntime;
    private readonly CreateViewModel _createPage;
    private readonly PreviewViewModel _previewPage;
    private readonly SettingsViewModel _settingsPage;
    private NavigationItemViewModel _selectedNavigationItem;
    private string _connectionKey;
    private string _projectKey;
    // Session state names are protocol words, not prose: they stay verbatim in every language.
    private string _sessionDisplay = UserModeDesktopSessionState.Disconnected.ToString();
    private string _readKey = UiStringKeys.MainWindowReadNone;
    private object?[] _readArguments = [];

    public MainWindowViewModel(
        VfxComposerClient client,
        IInMemoryDiagnosticSink diagnostics,
        IUiErrorBoundary errorBoundary,
        IAiDesktopRuntime? aiRuntime = null,
        IJobQueueClient? jobQueue = null,
        LocalizationService? localization = null,
        GenerationModeService? generationModes = null,
        IBuildHostLauncher? buildHostLauncher = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _errorBoundary = errorBoundary ?? throw new ArgumentNullException(nameof(errorBoundary));
        _aiRuntime = aiRuntime ?? AiDesktopRuntime.Unavailable;
        // A shell built without a stored preference (tests, design time) starts in the default language and mode;
        // the composition root passes the services that carry the persisted choices.
        Localization = localization ?? new LocalizationService();
        var modes = generationModes ?? new GenerationModeService();
        _createPage = new CreateViewModel(Localization, _aiRuntime, modes, buildHostLauncher);
        _previewPage = new PreviewViewModel(Localization, _aiRuntime);
        _settingsPage = new SettingsViewModel(Localization, _aiRuntime, modes);

        NavigationItems = new ReadOnlyObservableCollection<NavigationItemViewModel>(
            new ObservableCollection<NavigationItemViewModel>(
            [
                new(new DashboardViewModel(Localization)),
                new(new LibraryViewModel(Localization)),
                new(_createPage),
                new(_previewPage),
                new(new PatchViewModel(Localization)),
                new(new ReviewViewModel(Localization)),
                new(new JobsViewModel(Localization, jobQueue)),
                new(_settingsPage),
            ]));

        _selectedNavigationItem = NavigationItems[0];
        _connectionKey = ConnectionKeyFor(client.CurrentState.IsConnected);
        _projectKey = client.CurrentState.HasRegisteredProject
            ? UiStringKeys.MainWindowProjectRegistered
            : UiStringKeys.MainWindowProjectNone;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ConnectCommand = new AsyncRelayCommand(ConnectAsync, () => _session is not null);
        SelectProjectCommand = new AsyncRelayCommand(SelectProjectAsync, () =>
            _session?.State is UserModeDesktopSessionState.ConnectedNoProject or UserModeDesktopSessionState.Selected);
        ReadProjectCommand = new AsyncRelayCommand(ReadProjectAsync, () =>
            _session?.State == UserModeDesktopSessionState.Selected);
        RecoverCommand = new AsyncRelayCommand(RecoverAsync, () =>
            _session?.State == UserModeDesktopSessionState.RecoveryRequired);
        Localization.LanguageChanged += OnLanguageChanged;

        _diagnostics.Record(
            "DESKTOP_READY",
            "Desktop shell started without a Broker or Unity dependency.");
    }

    /// <summary>Bound by the window chrome through the string indexer, e.g. <c>{Binding Localization[AppProductName]}</c>.</summary>
    public LocalizationService Localization { get; }

    public string ProductName => Localization[UiStringKeys.AppProductName];

    public ReadOnlyObservableCollection<NavigationItemViewModel> NavigationItems { get; }

    public NavigationItemViewModel SelectedNavigationItem
    {
        get => _selectedNavigationItem;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetProperty(ref _selectedNavigationItem, value))
            {
                OnPropertyChanged(nameof(CurrentPage));
            }
        }
    }

    public WorkspacePageViewModel CurrentPage => SelectedNavigationItem.Page;

    public string ConnectionDisplay => Localization[_connectionKey];

    public string ProjectDisplay => Localization[_projectKey];

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand ConnectCommand { get; }
    public IAsyncRelayCommand SelectProjectCommand { get; }
    public IAsyncRelayCommand ReadProjectCommand { get; }
    public IAsyncRelayCommand RecoverCommand { get; }

    public string SessionDisplay
    {
        get => _sessionDisplay;
        private set => SetProperty(ref _sessionDisplay, value);
    }

    public string ReadDisplay => _readArguments.Length == 0
        ? Localization[_readKey]
        : Localization.Format(_readKey, _readArguments);

    public IReadOnlyList<UiDiagnostic> Diagnostics => _diagnostics.Snapshot;

    public CreateViewModel CreatePage => _createPage;

    public PreviewViewModel PreviewPage => _previewPage;

    public SettingsViewModel SettingsPage => _settingsPage;

    public static MainWindowViewModel CreateDisconnected(
        IInMemoryDiagnosticSink? diagnostics = null,
        IUiErrorBoundary? errorBoundary = null,
        IAiDesktopRuntime? aiRuntime = null,
        IJobQueueClient? jobQueue = null,
        LocalizationService? localization = null,
        GenerationModeService? generationModes = null)
    {
        diagnostics ??= new InMemoryDiagnosticSink();
        errorBoundary ??= new UiErrorBoundary(diagnostics);

        return new MainWindowViewModel(
            VfxComposerClient.CreateDisconnected(),
            diagnostics,
            errorBoundary,
            aiRuntime,
            jobQueue,
            localization,
            generationModes);
    }

    public static MainWindowViewModel CreateUserMode(
        IUserModeDesktopSession session,
        IProjectSelectionDialog selectionDialog,
        IUiDispatcher dispatcher,
        IInMemoryDiagnosticSink? diagnostics = null,
        IUiErrorBoundary? errorBoundary = null,
        IAiDesktopRuntime? aiRuntime = null,
        IJobQueueClient? jobQueue = null,
        LocalizationService? localization = null,
        GenerationModeService? generationModes = null,
        IBuildHostLauncher? buildHostLauncher = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(selectionDialog);
        ArgumentNullException.ThrowIfNull(dispatcher);
        diagnostics ??= new InMemoryDiagnosticSink();
        errorBoundary ??= new UiErrorBoundary(diagnostics);
        var result = new MainWindowViewModel(
            VfxComposerClient.CreateDisconnected(),
            diagnostics,
            errorBoundary,
            aiRuntime,
            jobQueue,
            localization,
            generationModes,
            buildHostLauncher)
        {
            _session = session,
            _selectionDialog = selectionDialog,
            _dispatcher = dispatcher,
        };
        session.StateChanged += result.OnSessionStateChanged;
        result.RefreshSessionPresentation();
        return result;
    }

    public bool TryNavigate(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var target = NavigationItems.FirstOrDefault(
            item => string.Equals(item.Key, key, StringComparison.Ordinal));
        if (target is null)
        {
            return false;
        }

        SelectedNavigationItem = target;
        return true;
    }

    private async Task RefreshAsync()
    {
        await _errorBoundary.RunAsync(
            "refresh-connection-state",
            async () =>
            {
                var state = await _client.RefreshStateAsync(RequestCorrelation.CreateNew());
                SetConnectionKey(ConnectionKeyFor(state.IsConnected));
                SetProjectKey(state.HasRegisteredProject
                    ? UiStringKeys.MainWindowProjectRegistered
                    : UiStringKeys.MainWindowProjectNone);
            });
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_session is not null)
            {
                _session.StateChanged -= OnSessionStateChanged;
                await _session.DisposeAsync();
            }
        }
        finally
        {
            _previewPage.Dispose();
            await _aiRuntime.DisposeAsync();
        }
    }

    private async Task ConnectAsync()
    {
        if (_session is null)
        {
            return;
        }

        await RunSessionOperationAsync("connect-user-mode", token => _session.ConnectAsync(token));
    }

    private async Task SelectProjectAsync()
    {
        if (_session is null || _selectionDialog is null)
        {
            return;
        }

        string? selection = await _selectionDialog.SelectAsync();
        if (selection is null)
        {
            return;
        }

        try
        {
            await RunSessionOperationAsync(
                "select-user-project",
                token => _session.SelectAsync(selection, token));
        }
        finally
        {
            selection = null;
        }
    }

    private async Task ReadProjectAsync()
    {
        if (_session is null)
        {
            return;
        }

        await RunSessionOperationAsync(
            "read-user-project",
            async token =>
            {
                var result = await _session.ReadAsync(cancellationToken: token);
                if (result.Accepted)
                {
                    SetReadKey(UiStringKeys.MainWindowReadAccepted, result.ByteLength);
                }
                else
                {
                    SetReadKey(UiStringKeys.MainWindowReadRejected, result.DiagnosticCode ?? "U4FS001");
                }
            });
    }

    private async Task RecoverAsync()
    {
        if (_session is null)
        {
            return;
        }

        SetReadKey(UiStringKeys.MainWindowReadNone);
        await RunSessionOperationAsync("restart-user-mode", token => _session.RestartAsync(token));
    }

    private async Task RunSessionOperationAsync(
        string operation,
        Func<CancellationToken, ValueTask> action)
    {
        await _errorBoundary.RunAsync(operation, () => action(CancellationToken.None));
        RefreshSessionPresentation();
    }

    private void OnSessionStateChanged(object? sender, EventArgs eventArgs) =>
        _dispatcher?.Post(RefreshSessionPresentation);

    private void RefreshSessionPresentation()
    {
        SessionDisplay = _session?.State.ToString() ?? UserModeDesktopSessionState.Disconnected.ToString();
        SetConnectionKey(ConnectionKeyFor(_session?.State is
            UserModeDesktopSessionState.ConnectedNoProject or
            UserModeDesktopSessionState.Selecting or
            UserModeDesktopSessionState.Selected or
            UserModeDesktopSessionState.Reading));
        SetProjectKey(_session?.State is UserModeDesktopSessionState.Selected or UserModeDesktopSessionState.Reading
            ? UiStringKeys.MainWindowProjectSelected
            : UiStringKeys.MainWindowProjectNone);
        SelectProjectCommand.NotifyCanExecuteChanged();
        ReadProjectCommand.NotifyCanExecuteChanged();
        RecoverCommand.NotifyCanExecuteChanged();
    }

    private static string ConnectionKeyFor(bool connected) => connected
        ? UiStringKeys.MainWindowConnectionConnected
        : UiStringKeys.MainWindowConnectionDisconnected;

    private void SetConnectionKey(string key)
    {
        if (string.Equals(_connectionKey, key, StringComparison.Ordinal))
        {
            return;
        }

        _connectionKey = key;
        OnPropertyChanged(nameof(ConnectionDisplay));
    }

    private void SetProjectKey(string key)
    {
        if (string.Equals(_projectKey, key, StringComparison.Ordinal))
        {
            return;
        }

        _projectKey = key;
        OnPropertyChanged(nameof(ProjectDisplay));
    }

    // Read outcomes keep their key and arguments instead of a rendered string, so a language switch re-renders them.
    private void SetReadKey(string key, params object?[] arguments)
    {
        _readKey = key;
        _readArguments = arguments;
        OnPropertyChanged(nameof(ReadDisplay));
    }

    private void OnLanguageChanged(object? sender, EventArgs eventArgs)
    {
        OnPropertyChanged(nameof(ProductName));
        OnPropertyChanged(nameof(ConnectionDisplay));
        OnPropertyChanged(nameof(ProjectDisplay));
        OnPropertyChanged(nameof(ReadDisplay));
    }
}
