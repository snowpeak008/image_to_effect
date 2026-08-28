using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using VFXComposer.Editor.Workflow;

namespace VFXComposer.Tests.EditMode
{
    // Records the witnessed repair-one outputs and prepares, but never sends, repair two.
    [Explicit("One-time Cohort J repair-one evidence recorder; rerunning would attempt to rewrite persisted evidence.")]
    public sealed class S9CohortJRepairOneRecordingTests
    {
        [Test]
        public void CohortJ_RepairOneReportsFailuresAndRepairTwoCarriesFrozenOperationAndPriorReport()
        {
            Record("J1", "/root/s9_j_j1"); Record("J3", "/root/s9_j_j3");
            Assert.That((bool)Report("J1")["succeeded"], Is.False); Assert.That(((JArray)Report("J1")["entries"]).Children<JObject>().Any(entry => (string)entry["code"] == "E720"), Is.True); Assert.That((bool)Report("J3")["succeeded"], Is.False); Assert.That(((JArray)Report("J3")["entries"]).Children<JObject>().Any(entry => (string)entry["code"] == "E702"), Is.True);
            var j1 = VfxCohortJProtocol.PrepareRepairAndPause("J1", 2); var j3 = VfxCohortJProtocol.PrepareRepairAndPause("J3", 2); AssertPreparedFeedback("J1", j1); AssertPreparedFeedback("J3", j3);
        }

        private static void Record(string key, string threadId) { VfxCohortJProtocol.RecordRepairWitnessAndReport(key, 1, threadId, File.ReadAllText(VfxCohortJProtocol.RepairEnvelopePath(key, 1))); }
        private static JObject Report(string key) { return JObject.Parse(File.ReadAllText(VfxCohortJProtocol.ReportPath(key, 1))); }
        private static void AssertPreparedFeedback(string key, PreparedJRepair prepared)
        {
            var payload = File.ReadAllText(prepared.PayloadPath); StringAssert.Contains(VfxCohortJProtocol.Normalize(File.ReadAllText(VfxCohortJProtocol.ReportPath(key, 1))), VfxCohortJProtocol.Normalize(payload)); StringAssert.Contains("FROZEN ACCEPTANCE OPERATION (authoritative complete bare array):", payload); StringAssert.Contains(VfxCohortJProtocol.FrozenAcceptanceOperation(key), payload);
            Assert.That(prepared.EnvelopeSha256, Is.EqualTo(Hash(File.ReadAllBytes(prepared.EnvelopePath)))); Assert.That(prepared.PayloadSha256, Is.EqualTo(Hash(File.ReadAllBytes(prepared.PayloadPath)))); Assert.That(prepared.TempPayloadSha256, Is.EqualTo(Hash(File.ReadAllBytes(prepared.TempPayloadPath)))); Assert.That(prepared.PriorReportSha256, Is.EqualTo(Hash(File.ReadAllBytes(prepared.PriorReportPath)))); CollectionAssert.AreEqual(File.ReadAllBytes(prepared.PayloadPath), File.ReadAllBytes(prepared.TempPayloadPath));
        }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return System.BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty); }
    }
}
