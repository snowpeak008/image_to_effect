using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VFXComposer.Jobs.Tests;

[TestClass]
public sealed class JobQueueDiagnosticsTests
{
    [TestMethod]
    public void EveryCatalogMessageIsSingleLinePathFreeAndBounded()
    {
        foreach (var definition in JobQueueDiagnosticCatalog.All.Values)
        {
            Assert.IsTrue(definition.Message.Length is > 0 and <= 512, definition.Code);
            Assert.IsFalse(definition.Message.Contains('\n'), definition.Code);
            Assert.IsFalse(definition.Message.Contains('\\'), definition.Code);
            Assert.IsFalse(definition.Message.Contains(":/", StringComparison.Ordinal), definition.Code);
            Assert.IsTrue(definition.Code.StartsWith("VFXJ", StringComparison.Ordinal), definition.Code);
        }
    }

    [TestMethod]
    public void UnknownCodesAreRejectedEverywhere()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => JobQueueDiagnosticCatalog.Require("VFXJ9999"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new JobQueueException("VFXJ9999"));
    }

    [TestMethod]
    public void ExceptionMessageIsAlwaysTheFixedCatalogMessage()
    {
        var exception = new JobQueueException(JobQueueDiagnosticCodes.QueueFull);

        Assert.AreEqual(JobQueueDiagnosticCodes.QueueFull, exception.Code);
        Assert.AreEqual(
            JobQueueDiagnosticCatalog.Require(JobQueueDiagnosticCodes.QueueFull).Message,
            exception.Message);
    }

    [TestMethod]
    public void TheJobsAssemblyHasNoNetworkFacingReferences()
    {
        var references = typeof(JobStore).Assembly.GetReferencedAssemblies();

        foreach (var reference in references)
        {
            Assert.IsFalse(
                reference.Name!.StartsWith("System.Net", StringComparison.Ordinal),
                $"The queue must not gain a network surface ({reference.Name}).");
        }
    }
}
