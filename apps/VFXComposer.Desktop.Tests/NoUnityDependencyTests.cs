using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Client;
using VFXComposer.Desktop.ViewModels;

namespace VFXComposer.Desktop.Tests;

[TestClass]
public sealed class NoUnityDependencyTests
{
    [TestMethod]
    public void ClientAndDesktopAssembliesHaveNoUnityReference()
    {
        var assemblies = new[]
        {
            typeof(VfxComposerClient).Assembly,
            typeof(MainWindowViewModel).Assembly,
        };

        foreach (var assembly in assemblies)
        {
            var unityReferences = assembly
                .GetReferencedAssemblies()
                .Where(reference => reference.Name?.StartsWith("Unity", StringComparison.OrdinalIgnoreCase) == true)
                .Select(reference => reference.FullName)
                .ToArray();

            Assert.AreEqual(
                0,
                unityReferences.Length,
                $"{assembly.GetName().Name} unexpectedly references: {string.Join(", ", unityReferences)}");
        }
    }
}
