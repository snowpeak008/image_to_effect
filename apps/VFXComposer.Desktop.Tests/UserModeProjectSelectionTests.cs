using VFXComposer.Client;
using VFXComposer.Desktop.Localization;
using VFXComposer.Desktop.Services;
using VFXComposer.Desktop.ViewModels;

namespace VFXComposer.Desktop.Tests;

[TestClass]
public sealed class UserModeProjectSelectionTests
{
    [TestMethod]
    public async Task SelectedRootIsForwardedOnlyToSessionAndNeverPresentedByDesktop()
    {
        const string selectedRoot = "C:\\private-project\\do-not-display";
        var session = new CapturingSession();
        var dialog = new CapturingDialog(selectedRoot);
        await using var viewModel = MainWindowViewModel.CreateUserMode(
            session,
            dialog,
            new ImmediateDispatcher(),
            localization: LocalizationTestSupport.CreateEnglish());

        await viewModel.ConnectCommand.ExecuteAsync(null);
        await viewModel.SelectProjectCommand.ExecuteAsync(null);

        Assert.AreEqual(selectedRoot, session.LastSelection);
        Assert.AreEqual(
            LocalizationTestSupport.English(UiStringKeys.MainWindowProjectSelected),
            viewModel.ProjectDisplay);
        Assert.IsFalse(viewModel.ProjectDisplay.Contains(selectedRoot, StringComparison.Ordinal));
        Assert.IsFalse(viewModel.SessionDisplay.Contains(selectedRoot, StringComparison.Ordinal));
        Assert.IsFalse(viewModel.ReadDisplay.Contains(selectedRoot, StringComparison.Ordinal));
        Assert.IsFalse(viewModel.Diagnostics.Any(item =>
            item.Message.Contains(selectedRoot, StringComparison.Ordinal) ||
            item.Detail?.Contains(selectedRoot, StringComparison.Ordinal) == true));
    }

    private sealed class CapturingSession : IUserModeDesktopSession
    {
        public UserModeDesktopSessionState State { get; private set; } = UserModeDesktopSessionState.Disconnected;
        public long Generation { get; private set; }
        public UserModeDesktopReadPresentation? LastRead => null;
        public string? LastSelection { get; private set; }
        public event EventHandler? StateChanged;

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
        {
            Generation++;
            SetState(UserModeDesktopSessionState.ConnectedNoProject);
            return ValueTask.CompletedTask;
        }

        public ValueTask SelectAsync(string selection, CancellationToken cancellationToken = default)
        {
            LastSelection = selection;
            SetState(UserModeDesktopSessionState.Selected);
            return ValueTask.CompletedTask;
        }

        public ValueTask<UserModeDesktopReadPresentation> ReadAsync(
            string documentKind = "LIBRARY_INDEX",
            string documentId = "project",
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<UserModeDesktopReadPresentation>(new InvalidOperationException());

        public ValueTask RestartAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private void SetState(UserModeDesktopSessionState state)
        {
            State = state;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class CapturingDialog(string selection) : IProjectSelectionDialog
    {
        public ValueTask<string?> SelectAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<string?>(selection);
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public void Post(Action action) => action();
    }
}
