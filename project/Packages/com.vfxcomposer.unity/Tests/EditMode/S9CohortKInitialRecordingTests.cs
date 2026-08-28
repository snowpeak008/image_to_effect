using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using VFXComposer.Editor.Workflow;

namespace VFXComposer.Tests.EditMode
{
    // Post-dispatch recorder only: persists witnessed initial outputs, their real reports, and only K3 repair preparation.
    [Explicit("One-time Cohort K initial evidence recorder; rerunning would attempt to rewrite persisted evidence.")]
    public sealed class S9CohortKInitialRecordingTests
    {
        [Test]
        public void CohortK_InitialOutputsPersistRealOutcomesAndPrepareOnlyK3RepairOne()
        {
            Record("K1", "/root/s9_k_k1"); Record("K2", "/root/s9_k_k2"); Record("K3", "/root/s9_k_k3");
            Assert.That((bool)Report("K1")["succeeded"], Is.True); Assert.That((bool)Report("K2")["succeeded"], Is.True); Assert.That((bool)Report("K3")["succeeded"], Is.False); Assert.That(((JArray)Report("K3")["entries"]).Children<JObject>().Any(x => (string)x["code"] == "E100"), Is.True, "K3 must retain its actual attachedTo unknown-field failure.");
            CollectionAssert.AreEqual(File.ReadAllBytes(VfxCohortKProtocol.AttemptPath("K1", 0)), File.ReadAllBytes(VfxCohortKProtocol.FinalPath("K1"))); CollectionAssert.AreEqual(File.ReadAllBytes(VfxCohortKProtocol.AttemptPath("K2", 0)), File.ReadAllBytes(VfxCohortKProtocol.FinalPath("K2")));
            var repair = VfxCohortKProtocol.PrepareRepairAndPause("K3", 1); Assert.That(File.Exists(repair.EnvelopePath) && File.Exists(repair.PayloadPath) && File.Exists(repair.TempPayloadPath) && File.Exists(VfxCohortKProtocol.PreparedPath("K3", 1)), Is.True); Assert.That(File.Exists(VfxCohortKProtocol.RepairEnvelopePath("K1", 1)) || File.Exists(VfxCohortKProtocol.RepairEnvelopePath("K2", 1)), Is.False, "Successful K1/K2 must not receive repairs.");
        }

        private static void Record(string key, string threadId) { VfxCohortKProtocol.RecordInitialWitnessAndReport(key, threadId, File.ReadAllText(VfxCohortKProtocol.InitialEnvelopePath(key))); }
        private static JObject Report(string key) { return JObject.Parse(File.ReadAllText(VfxCohortKProtocol.ReportPath(key, 0))); }
    }
}
