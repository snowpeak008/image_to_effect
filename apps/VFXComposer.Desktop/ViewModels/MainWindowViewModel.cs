using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VFXComposer.AI.Contracts.Desktop;
using VFXComposer.Client;
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
    private string _connectionDisplay;
    private string _projectDisplay;
    private string _sessionDisplay = "Disconnected";
    private string _readDisplay = "No read result";

    public MainWindowViewModel(
        VfxComposerClient client,
        IInMemoryDiagnosticSink diagnostics,
        IUiErrorBoundary errorBoundary,
        IAiDesktopRuntime? aiRuntime = null,
        IJobQueueClient? jobQueue = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _errorBoundary = errorBoundary ?? throw new ArgumentNullException(nameof(errorBoundary));
        _aiRuntime = aiRuntime ?? AiDesktopRuntime.Unavailable;
        _createPage = new CreateViewModel(_aiRuntime);
        _previewPage = new PreviewViewModel(_aiRuntime);
        _settingsPage = new SettingsViewModel(_aiRuntime);

        NavigationItems = new ReadOnlyObservableCollection<NavigationItemViewModel>(
            new ObservableCollection<NavigationItemViewModel>(
            [
                new(new DashboardViewModel()),
                new(new LibraryViewModel()),
                new(_createPage),
                new(_previewPage),
                new(new PatchViewModel()),
                new(new ReviewViewModel()),
                new(new JobsViewModel(jobQueue)),
                new(_settingsPage),
            ]));

        _selectedNavigationItem = NavigationItems[0];
        _connectionDisplay = client.CurrentState.ConnectionDisplay;
        _projectDisplay = client.CurrentState.ProjectDisplay;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ConnectCommand = new AsyncRelayCommand(ConnectAsync, () => _session is not null);
        SelectProjectCommand = new AsyncRelayCommand(SelectProjectAsync, () =>
            _session?.State is UserModeDesktopSessionState.ConnectedNoProject or UserModeDesktopSessionState.Selected);
        ReadProjectCommand = new AsyncRelayCommand(ReadProjectAsync, () =>
            _session?.State == UserModeDesktopSessionState.Selected);
        RecoverCommand = new AsyncRelayCommand(RecoverAsync, () =>
            _session?.State == UserModeDesktopSessionState.RecoveryRequired);

        _diagnostics.Record(
            "DESKTOP_READY",
            "Desktop shell started without a Broker or Unity dependency.");
    }

    public string ProductName => "VFX Composer";

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

    public string ConnectionDisplay
    {
        get => _connectionDisplay;
        private set => SetProperty(ref _connectionDisplay, value);
    }

    public string ProjectDisplay
    {
        get => _projectDisplay;
        private set => SetProperty(ref _projectDisplay, value);
    }

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

    public string ReadDisplay
    {
        get => _readDisplay;
        private set => SetProperty(ref _readDisplay, value);
    }

    public IReadOnlyList<UiDiagnostic> Diagnostics => _diagnostics.Snapshot;

    public CreateViewModel CreatePage => _createPage;

    public PreviewViewModel PreviewPage => _previewPage;

    public SettingsViewModel SettingsPage => _settingsPage;

    public static MainWindowViewModel CreateDisconnected(
        IInMemoryDiagnosticSink? diagnostics = null,
        IUiErrorBoundary? errorBoundary = null,
        IAiDesktopRuntime? aiRuntime = null,
        IJobQueueClient? jobQueue = null)
    {
        diagnostics ??= new InMemoryDiagnosticSink();
        errorBoundary ??= new UiErrorBoundary(diagnostics);

        return new MainWindowViewModel(
            VfxComposerClient.CreateDisconnected(),
            diagnostics,
            errorBoundary,
            aiRuntime,
            jobQueue);
    }

    public static MainWindowViewModel CreateUserMode(
        IUserModeDesktopSession session,
        IProjectSelectionDialog selectionDialog,
        IUiDispatcher dispatcher,
        IInMemoryDiagnosticSink? diagnostics = null,
        IUiErrorBoundary? errorBoundary = null,
        IAiDesktopRuntime? aiRuntime = null,
        IJobQueueClient? jobQueue = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(selectionDialog);
        ArgumentNullException.ThrowIfNull(dispatcher);
        diagnostics ??= new InMemoryDiagnosticSink();
        errorBoundary ??= new UiErrorBoundary(diagnostics);
        var result = new MainWindowViewModel(
            VfxComposerClient.CreateDisconnected(), diagnostics, errorBoundary, aiRuntime, jobQueue)
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
                ConnectionDisplay = state.ConnectionDisplay;
                ProjectDisplay = state.ProjectDisplay;
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
                ReadDisplay = result.Accepted
                    ? $"Read {result.ByteLength} bytes"
                    : $"Read rejected: {result.DiagnosticCode ?? "U4FS001"}";
            });
    }

    private async Task RecoverAsync()
    {
        if (_session is null)
        {
            return;
        }

        ReadDisplay = "No read result";
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
        SessionDisplay = _session?.State.ToString() ?? "Disconnected";
        ConnectionDisplay = _session?.State is
            UserModeDesktopSessionState.ConnectedNoProject or
            UserModeDesktopSessionState.Selecting or
            UserModeDesktopSessionState.Selected or
            UserModeDesktopSessionState.Reading
                ? "Connected"
                : "Disconnected";
        ProjectDisplay = _session?.State is UserModeDesktopSessionState.Selected or UserModeDesktopSessionState.Reading
            ? "Selected project"
            : "No registered project";
        SelectProjectCommand.NotifyCanExecuteChanged();
        ReadProjectCommand.NotifyCanExecuteChanged();
        RecoverCommand.NotifyCanExecuteChanged();
    }
}
