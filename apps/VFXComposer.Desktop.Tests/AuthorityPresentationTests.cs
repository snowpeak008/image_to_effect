using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Desktop.ViewModels;

namespace VFXComposer.Desktop.Tests;

[TestClass]
public sealed class AuthorityPresentationTests
{
    [TestMethod]
    public void ReviewKeepsMachineVisualUserL3AndL4StatusesDistinct()
    {
        var review = new ReviewViewModel(LocalizationTestSupport.CreateEnglish());

        Assert.AreEqual("Machine: Not evaluated", review.MachineStatus);
        Assert.AreEqual("Visual: VISUAL_PENDING", review.VisualStatus);
        Assert.AreEqual("User verdict: Not signed", review.UserVerdictStatus);
        Assert.AreEqual("L3: Not granted", review.L3Status);
        Assert.AreEqual("L4: Not granted", review.L4Status);
        Assert.IsTrue(review.AuthorityNotice.Contains("not an authority grant", StringComparison.Ordinal));
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
