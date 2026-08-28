using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VFXComposer.Client;
using VFXComposer.Desktop.Services;

namespace VFXComposer.Desktop.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly VfxComposerClient _client;
    private readonly IInMemoryDiagnosticSink _diagnostics;
    private readonly IUiErrorBoundary _errorBoundary;
    private NavigationItemViewModel _selectedNavigationItem;
    private string _connectionDisplay;
    private string _projectDisplay;

    public MainWindowViewModel(
        VfxComposerClient client,
        IInMemoryDiagnosticSink diagnostics,
        IUiErrorBoundary errorBoundary)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _errorBoundary = errorBoundary ?? throw new ArgumentNullException(nameof(errorBoundary));

        NavigationItems = new ReadOnlyObservableCollection<NavigationItemViewModel>(
            new ObservableCollection<NavigationItemViewModel>(
            [
                new(new DashboardViewModel()),
                new(new LibraryViewModel()),
                new(new CreateViewModel()),
                new(new PreviewViewModel()),
                new(new PatchViewModel()),
                new(new ReviewViewModel()),
                new(new JobsViewModel()),
                new(new SettingsViewModel()),
            ]));

        _selectedNavigationItem = NavigationItems[0];
        _connectionDisplay = client.CurrentState.ConnectionDisplay;
        _projectDisplay = client.CurrentState.ProjectDisplay;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);

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

    public IReadOnlyList<UiDiagnostic> Diagnostics => _diagnostics.Snapshot;

    public static MainWindowViewModel CreateDisconnected(
        IInMemoryDiagnosticSink? diagnostics = null,
        IUiErrorBoundary? errorBoundary = null)
    {
        diagnostics ??= new InMemoryDiagnosticSink();
        errorBoundary ??= new UiErrorBoundary(diagnostics);

        return new MainWindowViewModel(
            VfxComposerClient.CreateDisconnected(),
            diagnostics,
            errorBoundary);
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
}
