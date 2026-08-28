using VFXComposer.Protocol.Diagnostics;
using VFXComposer.Protocol.Jobs;
using VFXComposer.Protocol.Projects;

namespace VFXComposer.Protocol.Tests;

[TestClass]
public sealed class JobAndConnectionModelTests
{
    [TestMethod]
    public void ProjectConnectionStateStartsWithFailClosedDisconnectedValue()
    {
        var values = Enum.GetValues<ProjectConnectionState>();
        Assert.AreEqual(ProjectConnectionState.Disconnected, values[0]);
        CollectionAssert.AllItemsAreUnique(values.Cast<object>().ToArray());
        CollectionAssert.Contains(values, ProjectConnectionState.ConnectedNoRegisteredProject);
        CollectionAssert.Contains(values, ProjectConnectionState.ConnectedRegisteredProject);
    }

    [TestMethod]
    public void JobModelsCarryCorrelationOnlyAndStableStatus()
    {
        var identity = new JobIdentity("request-01", "job-01", "idem-01");
        var status = new JobStatus(
            identity,
            JobStatusStates.Disconnected,
            DateTimeOffset.UnixEpoch,
            StableDiagnosticCatalog.Create(StableDiagnosticCodes.Disconnected));

        Assert.AreEqual("request-01", status.Identity.RequestId);
        Assert.AreEqual(JobStatusStates.Disconnected, status.State);
        Assert.IsNotNull(status.Diagnostic);
        Assert.ThrowsExactly<ArgumentException>(() =>
            new JobIdentity("request-01", "job-01", "C:/project"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new JobStatus(identity, "PROMOTED", DateTimeOffset.UnixEpoch, null));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new JobStatus(identity, JobStatusStates.Queued, DateTimeOffset.Now, null));
    }
}
