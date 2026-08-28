using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using VFXComposer.Editor.Workflow;

namespace VFXComposer.Tests.EditMode
{
    // Terminal recorder only: attempt two is the maximum and this test never prepares another repair.
    [Explicit("One-time Cohort J terminal evidence recorder; historical terminal output must not be replayed.")]
    public sealed class S9CohortJTerminalRecordingTests
    {
        [Test]
        public void CohortJ_TerminalRepairOutputsPersistWithTheirActualOutcomes()
        {
            Record("J1", "/root/s9_j_j1"); Record("J3", "/root/s9_j_j3");
            Assert.That((bool)Report("J1")["succeeded"], Is.True); Assert.That((bool)Report("J3")["succeeded"], Is.False); Assert.That(((JArray)Report("J3")["entries"]).Children<JObject>().Any(entry => (string)entry["code"] == "E702"), Is.True);
            CollectionAssert.AreEqual(File.ReadAllBytes(VfxCohortJProtocol.AttemptPath("J1", 2)), File.ReadAllBytes(VfxCohortJProtocol.FinalPath("J1"))); CollectionAssert.AreEqual(File.ReadAllBytes(VfxCohortJProtocol.AttemptPath("J3", 2)), File.ReadAllBytes(VfxCohortJProtocol.FinalPath("J3"))); Assert.That(File.Exists(VfxCohortJProtocol.AttemptPath("J1", 3)) || File.Exists(VfxCohortJProtocol.AttemptPath("J3", 3)), Is.False);
        }

        private static void Record(string key, string threadId) { VfxCohortJProtocol.RecordRepairWitnessAndReport(key, 2, threadId, File.ReadAllText(VfxCohortJProtocol.RepairEnvelopePath(key, 2))); }
        private static JObject Report(string key) { return JObject.Parse(File.ReadAllText(VfxCohortJProtocol.ReportPath(key, 2))); }
    }
}
