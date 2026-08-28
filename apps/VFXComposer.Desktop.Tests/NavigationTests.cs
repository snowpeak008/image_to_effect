using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Desktop.ViewModels;

namespace VFXComposer.Desktop.Tests;

[TestClass]
public sealed class NavigationTests
{
    private static readonly string[] ExpectedKeys =
    [
        "dashboard",
        "library",
        "create",
        "preview",
        "patch",
        "review",
        "jobs",
        "settings",
    ];

    [TestMethod]
    public void ShellContainsExactlyTheEightApprovedInformationAreas()
    {
        var viewModel = MainWindowViewModel.CreateDisconnected();

        CollectionAssert.AreEqual(
            ExpectedKeys,
            viewModel.NavigationItems.Select(item => item.Key).ToArray());
    }

    [TestMethod]
    public void EveryApprovedAreaCanBeSelectedWithoutExternalState()
    {
        var viewModel = MainWindowViewModel.CreateDisconnected();

        foreach (var key in ExpectedKeys)
        {
            Assert.IsTrue(viewModel.TryNavigate(key));
            Assert.AreEqual(key, viewModel.CurrentPage.Key);
        }

        Assert.IsFalse(viewModel.TryNavigate("unknown"));
        Assert.AreEqual("settings", viewModel.CurrentPage.Key);
    }
}
