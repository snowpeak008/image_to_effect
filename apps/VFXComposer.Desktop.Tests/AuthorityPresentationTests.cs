using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Desktop.Localization;
using VFXComposer.Desktop.ViewModels;

namespace VFXComposer.Desktop.Tests;

[TestClass]
public sealed class AuthorityPresentationTests
{
    [TestMethod]
    public void ReviewKeepsMachineVisualUserL3AndL4StatusesDistinct()
    {
        var localization = LocalizationTestSupport.CreateEnglish();
        var review = new ReviewViewModel(localization);

        Assert.AreEqual(LocalizationTestSupport.English(UiStringKeys.ReviewMachineStatus), review.MachineStatus);
        Assert.AreEqual(LocalizationTestSupport.English(UiStringKeys.ReviewVisualStatus), review.VisualStatus);
        Assert.AreEqual(
            LocalizationTestSupport.English(UiStringKeys.ReviewUserVerdictStatus),
            review.UserVerdictStatus);
        Assert.AreEqual(LocalizationTestSupport.English(UiStringKeys.ReviewL3Status), review.L3Status);
        Assert.AreEqual(LocalizationTestSupport.English(UiStringKeys.ReviewL4Status), review.L4Status);
        Assert.AreEqual(
            LocalizationTestSupport.English(UiStringKeys.ReviewAuthorityNotice),
            review.AuthorityNotice);

        localization.SetLanguage(UiLanguage.ChineseSimplified);

        // The four domains stay separate lines in every language, and the visual status keeps its protocol word.
        Assert.AreEqual(
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.ReviewMachineStatus),
            review.MachineStatus);
        Assert.AreEqual(
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.ReviewUserVerdictStatus),
            review.UserVerdictStatus);
        Assert.AreEqual(LocalizationTestSupport.ChineseSimplified(UiStringKeys.ReviewL3Status), review.L3Status);
        Assert.AreEqual(LocalizationTestSupport.ChineseSimplified(UiStringKeys.ReviewL4Status), review.L4Status);
        Assert.AreEqual(
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.ReviewAuthorityNotice),
            review.AuthorityNotice);
        StringAssert.Contains(review.VisualStatus, "VISUAL_PENDING");
    }

    [TestMethod]
    public void ReviewStatusPropertiesAreReadOnlyAndExposeNoElevationCommand()
    {
        var type = typeof(ReviewViewModel);
        var statusProperties = new[]
        {
            nameof(ReviewViewModel.MachineStatus),
            nameof(ReviewViewModel.VisualStatus),
            nameof(ReviewViewModel.UserVerdictStatus),
            nameof(ReviewViewModel.L3Status),
            nameof(ReviewViewModel.L4Status),
        };

        foreach (var name in statusProperties)
        {
            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property);
            Assert.IsFalse(property.CanWrite);
        }

        var prohibitedWords = new[] { "Grant", "Approve", "Sign", "Issue", "Elevate" };
        var publicMethodNames = type
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToArray();

        foreach (var word in prohibitedWords)
        {
            Assert.IsFalse(publicMethodNames.Any(name => name.Contains(word, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
