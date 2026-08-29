using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Batch.Core;
using VFXComposer.Cli;

namespace VFXComposer.Cli.Tests;

/// <summary>
/// The verdict half of the REQ-002 §6.5 exit-code table. The end-to-end codes are covered by the
/// runner tests; these pin the mapping itself, including the verdict the runner cannot currently
/// reach, so a latent mis-mapping cannot hide behind unreachability.
/// </summary>
[TestClass]
public sealed class CliExitCodeMappingTests
{
    [TestMethod]
    public void EachVerdictMapsToItsDocumentedCode()
    {
        Assert.AreEqual(CliExitCodes.Success, CliExitCodes.ForVerdict(BatchVerdict.AllSucceeded));
        Assert.AreEqual(
            CliExitCodes.BatchCompletedWithFailures,
            CliExitCodes.ForVerdict(BatchVerdict.CompletedWithFailures));
        Assert.AreEqual(CliExitCodes.BatchAborted, CliExitCodes.ForVerdict(BatchVerdict.Aborted));
    }

    [TestMethod]
    public void APendingVerdictNeverReportsSuccess()
    {
        Assert.AreEqual(CliExitCodes.QueueUnavailable, CliExitCodes.ForVerdict(BatchVerdict.Pending));
    }

    [TestMethod]
    public void OnlyAllSucceededMapsToZeroAcrossTheWholeVerdictVocabulary()
    {
        foreach (var verdict in Enum.GetValues<BatchVerdict>())
        {
            var expectSuccess = verdict == BatchVerdict.AllSucceeded;
            Assert.AreEqual(
                expectSuccess,
                CliExitCodes.ForVerdict(verdict) == CliExitCodes.Success,
                "Verdict " + verdict + " must not be reported as a clean run.");
        }
    }

    [TestMethod]
    public void AnUnknownVerdictValueFallsBackToAFailureCode()
    {
        Assert.AreEqual(CliExitCodes.QueueUnavailable, CliExitCodes.ForVerdict((BatchVerdict)9999));
    }
}
