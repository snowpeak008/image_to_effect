using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Desktop.Localization;
using VFXComposer.Desktop.Services;
using VFXComposer.Desktop.ViewModels;

namespace VFXComposer.Desktop.Tests;

[TestClass]
public sealed class StartupDisconnectedTests
{
    [TestMethod]
    public void StartupDoesNotRequireBrokerUnityOrARegisteredProject()
    {
        var diagnostics = new InMemoryDiagnosticSink();
        var viewModel = MainWindowViewModel.CreateDisconnected(
            diagnostics,
            localization: LocalizationTestSupport.CreateEnglish());

        Assert.AreEqual(
            LocalizationTestSupport.English(UiStringKeys.MainWindowConnectionDisconnected),
            viewModel.ConnectionDisplay);
        Assert.AreEqual(
            LocalizationTestSupport.English(UiStringKeys.MainWindowProjectNone),
            viewModel.ProjectDisplay);
        Assert.AreEqual("dashboard", viewModel.CurrentPage.Key);
        Assert.IsTrue(diagnostics.Snapshot.Any(item => item.Code == "DESKTOP_READY"));
    }

    [TestMethod]
    public async Task ErrorBoundaryCapturesOnlyInMemoryDiagnosticData()
    {
        var diagnostics = new InMemoryDiagnosticSink();
        var boundary = new UiErrorBoundary(diagnostics);

        await boundary.RunAsync(
            "test-operation",
            () => throw new InvalidOperationException("sensitive detail"));

        var captured = diagnostics.Snapshot.Single();
        Assert.AreEqual("DESKTOP_UNHANDLED", captured.Code);
        Assert.AreEqual("InvalidOperationException", captured.Detail);
        Assert.IsFalse(captured.Message.Contains("sensitive detail", StringComparison.Ordinal));
    }
}
