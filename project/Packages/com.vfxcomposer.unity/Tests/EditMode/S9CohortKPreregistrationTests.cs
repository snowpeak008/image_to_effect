using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using VFXComposer.Editor.Workflow;

namespace VFXComposer.Tests.EditMode
{
    [Explicit("Cohort K preregistration assertions apply only before dispatch and attempt zero.")]
    public sealed class S9CohortKPreregistrationTests
    {
        [Test]
        public void CohortK_FreezeCreatesThreeCompactSourceDerivedPreDispatchPairsOnly()
        {
            VfxCohortKProtocol.Freeze(); VfxCohortKProtocol.VerifyPreDispatch();
            var manifest = JObject.Parse(File.ReadAllText(E("initial-payloads.generated.json"))); Assert.That((string)manifest["protocol"], Is.EqualTo("cohort-k-patch-only")); Assert.That((int)manifest["runtimeEvidence"], Is.Zero);
            CollectionAssert.AreEquivalent(VfxCohortKProtocol.PatchKeys, ((JObject)manifest["initialPayloads"]).Properties().Select(x => x.Name));
            foreach (var key in VfxCohortKProtocol.PatchKeys)
            {
                var payload = VfxCohortKProtocol.InitialPayloadPath(key); var temp = VfxCohortKProtocol.TempInitialPayloadPath(key); var bytes = File.ReadAllBytes(payload); var text = File.ReadAllText(payload); var entry = (JObject)manifest["initialPayloads"][key];
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(temp)); Assert.That(bytes.Length, Is.LessThan(VfxCohortKProtocol.MaximumPayloadBytes)); Assert.That(text.IndexOf("TASK\n", System.StringComparison.Ordinal), Is.EqualTo(0)); Assert.That(text.IndexOf("TASK\n", System.StringComparison.Ordinal), Is.LessThan(512));
                Assert.That((string)entry["payloadSha256"], Is.EqualTo(Hash(bytes))); Assert.That((string)entry["tempPayloadSha256"], Is.EqualTo(Hash(File.ReadAllBytes(temp)))); Assert.That((int)entry["payloadBytes"], Is.EqualTo(bytes.Length)); CollectionAssert.AreEqual(new[] { temp }, Directory.GetFiles(Path.GetDirectoryName(temp)));
                var expected = key == "K2" ? new[] { "TASK", "PATCH_OPERATION_SYNTAX", "PATH_RULES", "RECIPE_CONTEXT", "OUTPUT=bare array" } : new[] { "TASK", "PATCH_OPERATION_SYNTAX", "PATH_RULES", "RECIPE_CONTEXT", "CATALOG_FACTS", "OUTPUT=bare array" }; CollectionAssert.AreEquivalent(expected, text.Split('\n').Where(x => expected.Contains(x) || x.StartsWith("OUTPUT=", System.StringComparison.Ordinal)).Distinct());
                StringAssert.Contains("replace /stages/{stageId}/modules/{moduleId}/parameters/{parameter}; disable /stages/{stageId}/modules/{moduleId}; add /stages/{stageId}/modules/{newModuleId}, value is a complete module object and value.id equals newModuleId.", text); StringAssert.Contains("Never use array indexes, wrappers, Markdown, prose, or fences.", text);
                Assert.That(text, Does.Not.Contain("canonical-patches.generated").And.Not.Contain("patch-authoring.md").And.Not.Contain("recipe-v1.schema").And.Not.Contain("template-parameters.generated").And.Not.Contain("linger_embers").And.Not.Contain("sample_embers"));
                Assert.That(File.Exists(VfxCohortKProtocol.AttemptPath(key, 0)) || File.Exists(VfxCohortKProtocol.ReportPath(key, 0)) || File.Exists(VfxCohortKProtocol.TransportPath(key, 0)) || File.Exists(VfxCohortKProtocol.FinalPath(key)), Is.False, key + " must have runtime=0.");
            }
            var generated = Directory.GetDirectories(Path.Combine(Application.dataPath, "VFX", "Generated")).Select(Path.GetFileName).ToArray(); CollectionAssert.AreEquivalent(new[] { "fireball_2d" }, generated);
        }

        [Test]
        public void CohortK_HasExactlyThreeNewNonOverlappingContractsAndSourceFacts()
        {
            var patches = (JObject)JObject.Parse(File.ReadAllText(E("acceptance-spec.json")))["patches"]; CollectionAssert.AreEquivalent(VfxCohortKProtocol.PatchKeys, patches.Properties().Select(x => x.Name));
            var k1 = (JObject)patches["K1"]; Assert.That((string)k1["operation"], Is.EqualTo("replace")); Assert.That((string)k1["path"], Is.EqualTo("/stages/impact/modules/burst/parameters/speed")); Assert.That((double)k1["value"], Is.EqualTo(4.4));
            var k2 = (JObject)patches["K2"]; Assert.That((string)k2["operation"], Is.EqualTo("disable")); Assert.That((string)k2["path"], Is.EqualTo("/stages/launch/modules/launchFlash")); Assert.That((bool)k2["enabled"], Is.False);
            var k3 = (JObject)patches["K3"]; var module = (JObject)k3["module"]; Assert.That((string)k3["path"], Is.EqualTo("/stages/travel/modules/sparkle_embers")); Assert.That((string)module["kind"], Is.EqualTo("secondary_particles")); Assert.That((string)module["templateId"], Is.EqualTo("PFT_2D_Embers")); Assert.That((double)module["parameters"]["rate"], Is.EqualTo(12)); Assert.That((double)module["parameters"]["lifetime"], Is.EqualTo(.65)); Assert.That((string)module["attachTo"], Is.EqualTo("core")); Assert.That((bool)module["enabled"], Is.True);
            var j = (JObject)JObject.Parse(File.ReadAllText(Path.Combine(Directory.GetParent(VfxCohortKProtocol.EvidenceDirectory()).FullName, "cohort-j", "acceptance-spec.json")))["patches"]; var i = (JObject)JObject.Parse(File.ReadAllText(Path.Combine(Directory.GetParent(VfxCohortKProtocol.EvidenceDirectory()).FullName, "cohort-i", "acceptance-spec.json")))["patches"];
            Assert.That((string)k1["path"], Is.Not.EqualTo((string)j["J1"]["path"]).And.Not.EqualTo((string)i["P1"]["path"])); Assert.That((string)k2["path"], Is.Not.EqualTo((string)j["J2"]["path"]).And.Not.EqualTo((string)i["P2"]["path"])); Assert.That((string)module["id"], Is.Not.EqualTo((string)j["J3"]["module"]["id"]).And.Not.EqualTo((string)i["P3"]["moduleId"]));
            var canonical = JObject.Parse(File.ReadAllText(Path.Combine(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(VfxCohortKProtocol.EvidenceDirectory()).FullName).FullName).FullName).FullName, "docs", "ai-workflow", "canonical-recipe.generated.json"))); var impact = canonical["stages"].Children<JObject>().Single(x => (string)x["id"] == "impact"); Assert.That((double)impact["modules"].Children<JObject>().Single(x => (string)x["id"] == "burst")["parameters"]["speed"], Is.EqualTo(3.5)); StringAssert.Contains("\"type\":\"float\",\"min\":1.5,\"max\":6", File.ReadAllText(VfxCohortKProtocol.InitialPayloadPath("K1")).Replace(" ", string.Empty)); StringAssert.Contains("\"type\":\"float\",\"min\":4,\"max\":36", File.ReadAllText(VfxCohortKProtocol.InitialPayloadPath("K3")).Replace(" ", string.Empty));
        }

        [Test]
        public void CohortK_RepairIsPrebuiltToCarryFullPriorReportAndAuthoritativeOperationAndRejectSuccess()
        {
            var source = File.ReadAllText(Path.Combine(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(VfxCohortKProtocol.EvidenceDirectory()).FullName).FullName).FullName).FullName, "project", "Packages", "com.vfxcomposer.unity", "Editor", "Workflow", "VfxCohortKProtocol.cs")); StringAssert.Contains("PRIOR_REPORT", source); StringAssert.Contains("AUTHORITATIVE_OPERATION", source); StringAssert.Contains("Patch already succeeded or report is invalid; a repair must not be prepared.", source); StringAssert.Contains("repairAttempt > 2", source);
        }

        private static string E(string file) { return Path.Combine(VfxCohortKProtocol.EvidenceDirectory(), file); }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return System.BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty); }
    }
}
