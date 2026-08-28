using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using VFXComposer.Editor.Workflow;

namespace VFXComposer.Tests.EditMode
{
    // This post-dispatch recorder persists the witnessed initial outputs; it never dispatches or follows up.
    [Explicit("One-time Cohort J initial evidence recorder; rerunning would attempt to rewrite persisted evidence.")]
    public sealed class S9CohortJInitialRecordingTests
    {
        [Test]
        public void CohortJ_InitialOutputsRecordFrozenWitnessesSemanticReportsAndOnlyNeededRepairs()
        {
            Record("J1", "/root/s9_j_j1"); Record("J2", "/root/s9_j_j2"); Record("J3", "/root/s9_j_j3");
            var j1 = Report("J1"); Assert.That((bool)j1["succeeded"], Is.False); var mismatch = ((JArray)j1["entries"]).Children<JObject>().Single(entry => (string)entry["code"] == "E720"); Assert.That((string)mismatch["path"], Is.EqualTo("/stages/launch/modules/launchFlash/parameters/size")); Assert.That(mismatch["actualValue"].Type, Is.EqualTo(JTokenType.Array)); StringAssert.Contains("/stages/launch/modules/launchFlash/parameters/size", (string)mismatch["allowedRange"]);
            Assert.That((bool)Report("J2")["succeeded"], Is.True, "J2 is the only correct initial Patch."); Assert.That((bool)Report("J3")["succeeded"], Is.False, "J3 must retain the real Patch parse/shape failure.");
            var repair1 = VfxCohortJProtocol.PrepareRepairAndPause("J1", 1); var repair3 = VfxCohortJProtocol.PrepareRepairAndPause("J3", 1); Assert.That(File.Exists(repair1.EnvelopePath) && File.Exists(repair1.PayloadPath) && File.Exists(repair3.EnvelopePath) && File.Exists(repair3.PayloadPath), Is.True); Assert.That(File.Exists(VfxCohortJProtocol.RepairEnvelopePath("J2", 1)), Is.False, "A succeeded Patch must not get a repair envelope.");
        }

        private static void Record(string key, string threadId) { VfxCohortJProtocol.RecordInitialWitnessAndReport(key, threadId, File.ReadAllText(VfxCohortJProtocol.InitialEnvelopePath(key))); }
        private static JObject Report(string key) { return JObject.Parse(File.ReadAllText(VfxCohortJProtocol.ReportPath(key, 0))); }
    }
}
