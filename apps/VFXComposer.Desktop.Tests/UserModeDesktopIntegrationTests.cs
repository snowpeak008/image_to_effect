using VFXComposer.Client;
using VFXComposer.Desktop.Localization;
using VFXComposer.Desktop.Services;
using VFXComposer.Desktop.ViewModels;

namespace VFXComposer.Desktop.Tests;

[TestClass]
public sealed class UserModeDesktopIntegrationTests
{
    [TestMethod]
    public async Task DesktopPresentsOnlyConnectionAndRecoveryStateFromCurrentUserSession()
    {
        var session = new ScriptedSession();
        var diagnostics = new InMemoryDiagnosticSink();
        var connected = LocalizationTestSupport.English(UiStringKeys.MainWindowConnectionConnected);
        var disconnected = LocalizationTestSupport.English(UiStringKeys.MainWindowConnectionDisconnected);
        var noProject = LocalizationTestSupport.English(UiStringKeys.MainWindowProjectNone);
        await using var viewModel = MainWindowViewModel.CreateUserMode(
            session,
            new ScriptedSelectionDialog(null),
            new ImmediateDispatcher(),
            diagnostics,
            localization: LocalizationTestSupport.CreateEnglish());

        Assert.AreEqual(disconnected, viewModel.ConnectionDisplay);
        Assert.AreEqual(noProject, viewModel.ProjectDisplay);

        await viewModel.ConnectCommand.ExecuteAsync(null);

        Assert.AreEqual(UserModeDesktopSessionState.ConnectedNoProject, session.State);
        Assert.AreEqual(connected, viewModel.ConnectionDisplay);
        Assert.AreEqual(noProject, viewModel.ProjectDisplay);
        Assert.IsTrue(viewModel.SelectProjectCommand.CanExecute(null));

        session.EnterRecovery();

        Assert.AreEqual(disconnected, viewModel.ConnectionDisplay);
        Assert.IsTrue(viewModel.RecoverCommand.CanExecute(null));
        Assert.IsFalse(viewModel.ReadProjectCommand.CanExecute(null));

        await viewModel.RecoverCommand.ExecuteAsync(null);

        Assert.AreEqual(UserModeDesktopSessionState.ConnectedNoProject, session.State);
        Assert.AreEqual(connected, viewModel.ConnectionDisplay);
        Assert.AreEqual(1, session.RestartCount);
        Assert.IsFalse(diagnostics.Snapshot.Any(item => item.Detail?.Contains("\\", StringComparison.Ordinal) == true));
    }

    private sealed class ScriptedSession : IUserModeDesktopSession
    {
        public UserModeDesktopSessionState State { get; private set; } = UserModeDesktopSessionState.Disconnected;
        public long Generation { get; private set; }
        public UserModeDesktopReadPresentation? LastRead { get; private set; }
        public int RestartCount { get; private set; }
        public event EventHandler? StateChanged;

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Generation++;
            SetState(UserModeDesktopSessionState.ConnectedNoProject);
            return ValueTask.CompletedTask;
        }

        public ValueTask SelectAsync(string selection, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetState(UserModeDesktopSessionState.Selected);
            return ValueTask.CompletedTask;
        }

        public ValueTask<UserModeDesktopReadPresentation> ReadAsync(
            string documentKind = "LIBRARY_INDEX",
            string documentId = "project",
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(LastRead ?? new UserModeDesktopReadPresentation(
                false, documentKind, documentId, 0, null, "VFXP0008"));

        public ValueTask RestartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RestartCount++;
            Generation++;
            SetState(UserModeDesktopSessionState.ConnectedNoProject);
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            SetState(UserModeDesktopSessionState.Disconnected);
            return ValueTask.CompletedTask;
        }

        public void EnterRecovery() => SetState(UserModeDesktopSessionState.RecoveryRequired);

        private void SetState(UserModeDesktopSessionState state)
        {
            State = state;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class ScriptedSelectionDialog(string? selection) : IProjectSelectionDialog
    {
        public ValueTask<string?> SelectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(selection);
        }
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public void Post(Action action) => action();
    }
}
