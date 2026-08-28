using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using VFXComposer.Editor.Workflow;

namespace VFXComposer.Tests.EditMode
{
    // Post-dispatch recorder only: persists the witnessed K3 repair-one output; it never sends repair two.
    [Explicit("One-time Cohort K repair-one evidence recorder; rerunning would attempt to rewrite persisted evidence.")]
    public sealed class S9CohortKRepairOneRecordingTests
    {
        [Test]
        public void CohortK_K3RepairOnePersistsItsRealSuccessAndFinalBytes()
        {
            VfxCohortKProtocol.RecordRepairWitnessAndReport("K3", 1, "/root/s9_k_k3", File.ReadAllText(VfxCohortKProtocol.RepairEnvelopePath("K3", 1)));
            var report = JObject.Parse(File.ReadAllText(VfxCohortKProtocol.ReportPath("K3", 1))); Assert.That((bool)report["succeeded"], Is.True); CollectionAssert.AreEqual(File.ReadAllBytes(VfxCohortKProtocol.AttemptPath("K3", 1)), File.ReadAllBytes(VfxCohortKProtocol.FinalPath("K3"))); Assert.That(File.Exists(VfxCohortKProtocol.RepairEnvelopePath("K3", 2)), Is.False, "A successful K3 repair must not prepare repair two.");
        }
    }
}
