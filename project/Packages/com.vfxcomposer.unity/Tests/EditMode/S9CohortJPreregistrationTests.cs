using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using VFXComposer.Editor.Workflow;

namespace VFXComposer.Tests.EditMode
{
    [Explicit("Cohort J preregistration assertions apply only before dispatch and attempt zero.")]
    public sealed class S9CohortJPreregistrationTests
    {
        [Test]
        public void CohortJ_FreezeProducesOnlyAuditablePatchOnlyPreDispatchInputs()
        {
            VfxCohortJProtocol.Freeze();
            var manifest = JObject.Parse(File.ReadAllText(E("initial-payloads.generated.json")));
            Assert.That((string)manifest["protocol"], Is.EqualTo("cohort-j-patch-only")); Assert.That((string)manifest["tempRoot"], Is.EqualTo(VfxCohortJProtocol.TempRoot));
            var entries = (JObject)manifest["initialPayloads"]; CollectionAssert.AreEquivalent(VfxCohortJProtocol.PatchKeys, entries.Properties().Select(item => item.Name));
            foreach (var key in VfxCohortJProtocol.PatchKeys)
            {
                var entry = (JObject)entries[key]; var payload = VfxCohortJProtocol.InitialPayloadPath(key); var temp = VfxCohortJProtocol.TempInitialPayloadPath(key); var envelope = VfxCohortJProtocol.InitialEnvelopePath(key);
                CollectionAssert.AreEqual(File.ReadAllBytes(payload), File.ReadAllBytes(temp), key + " temp payload must byte-equal workspace evidence.");
                CollectionAssert.AreEqual(new[] { temp }, Directory.GetFiles(Path.GetDirectoryName(temp)), key + " temp directory may contain only its initial payload before dispatch.");
                Assert.That((string)entry["payloadSha256"], Is.EqualTo(Hash(File.ReadAllBytes(payload)))); Assert.That((string)entry["tempPayloadSha256"], Is.EqualTo(Hash(File.ReadAllBytes(temp)))); Assert.That((string)entry["envelopeSha256"], Is.EqualTo(Hash(File.ReadAllBytes(envelope))));
                var text = File.ReadAllText(envelope); StringAssert.Contains(temp, text); StringAssert.Contains((string)entry["payloadSha256"], text); StringAssert.Contains("exactly one `exec_command`", text); Assert.That(text.EndsWith("\n", System.StringComparison.Ordinal), Is.True, "The frozen envelope must retain its terminal LF for exact-byte witness comparison.");
                var bundle = File.ReadAllText(payload); StringAssert.Contains("patch-authoring.md", bundle); StringAssert.Contains("canonical-patches.generated.md", bundle); Assert.That(bundle, Does.Not.Contain("recipe-authoring.md")); Assert.That(bundle, Does.Not.Contain("recipe-v1.schema.json"));
                Assert.That(File.Exists(VfxCohortJProtocol.AttemptPath(key, 0)), Is.False); Assert.That(File.Exists(VfxCohortJProtocol.ReportPath(key, 0)), Is.False); Assert.That(File.Exists(VfxCohortJProtocol.FinalPath(key)), Is.False); Assert.That(File.Exists(VfxCohortJProtocol.TransportPath(key, 0)), Is.False);
            }
        }

        [Test]
        public void CohortJ_HasExactlyThreeNewPatchContracts()
        {
            var patches = (JObject)JObject.Parse(File.ReadAllText(E("acceptance-spec.json")))["patches"]; CollectionAssert.AreEquivalent(VfxCohortJProtocol.PatchKeys, patches.Properties().Select(item => item.Name));
            var j1 = (JObject)patches["J1"]; Assert.That((string)j1["operation"], Is.EqualTo("replace")); Assert.That((string)j1["path"], Is.EqualTo("/stages/launch/modules/launchFlash/parameters/size")); Assert.That((double)j1["value"], Is.EqualTo(1.4));
            var j2 = (JObject)patches["J2"]; Assert.That((string)j2["operation"], Is.EqualTo("disable")); Assert.That((string)j2["path"], Is.EqualTo("/stages/impact/modules/shockwave")); Assert.That((bool)j2["enabled"], Is.False);
            var j3 = (JObject)patches["J3"]; var module = (JObject)j3["module"]; Assert.That((string)j3["operation"], Is.EqualTo("add")); Assert.That((string)j3["path"], Is.EqualTo("/stages/travel/modules/linger_embers")); Assert.That((string)module["id"], Is.EqualTo("linger_embers")); Assert.That((string)module["kind"], Is.EqualTo("secondary_particles")); Assert.That((string)module["templateId"], Is.EqualTo("PFT_2D_Embers")); Assert.That((double)module["parameters"]["rate"], Is.EqualTo(8)); Assert.That((double)module["parameters"]["lifetime"], Is.EqualTo(.9)); Assert.That((string)module["attachTo"], Is.EqualTo("core")); Assert.That((bool)module["enabled"], Is.True);
            var i = (JObject)JObject.Parse(File.ReadAllText(Path.Combine(Directory.GetParent(VfxCohortJProtocol.EvidenceDirectory()).FullName, "cohort-i", "acceptance-spec.json")))["patches"];
            Assert.That((string)j1["path"], Is.Not.EqualTo((string)i["P1"]["path"])); Assert.That((string)j2["path"], Is.Not.EqualTo((string)i["P2"]["path"])); Assert.That((string)j3["path"], Is.Not.EqualTo("/stages/travel/modules/" + (string)i["P3"]["moduleId"]));
            AssertNoGeneratedExampleLeaksAcceptanceAnswer(j1, j2, j3);
        }

        [Test]
        public void CohortJ_RepairProtocolStaticallyRejectsAReportThatAlreadySucceeded()
        {
            var source = File.ReadAllText(Path.Combine(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(VfxCohortJProtocol.EvidenceDirectory()).FullName).FullName).FullName).FullName, "project", "Packages", "com.vfxcomposer.unity", "Editor", "Workflow", "VfxCohortJProtocol.cs"));
            StringAssert.Contains("Patch already succeeded; a repair must not be prepared.", source); StringAssert.Contains("prior[\"succeeded\"]", source);
        }

        private static void AssertNoGeneratedExampleLeaksAcceptanceAnswer(JObject j1, JObject j2, JObject j3)
        {
            var examples = Regex.Matches(File.ReadAllText(Path.Combine(Directory.GetParent(VfxCohortJProtocol.EvidenceDirectory()).Parent.FullName, "canonical-patches.generated.md")), "```json\\n(?<patch>[\\s\\S]*?)\\n```", RegexOptions.CultureInvariant).Cast<Match>().Select(match => JArray.Parse(match.Groups["patch"].Value)).ToArray();
            var expected = new[] { new JArray(new JObject { ["op"] = (string)j1["operation"], ["path"] = (string)j1["path"], ["value"] = j1["value"] }), new JArray(new JObject { ["op"] = (string)j2["operation"], ["path"] = (string)j2["path"] }), new JArray(new JObject { ["op"] = (string)j3["operation"], ["path"] = (string)j3["path"], ["value"] = j3["module"] }) };
            foreach (var target in expected) Assert.That(examples.Select(Canonical).Contains(Canonical(target)), Is.False, "A generated example must not canonicalize to a J acceptance operation.");
            var replace = examples.Select(array => (JObject)array.Single()).Where(operation => (string)operation["op"] == "replace"); Assert.That(replace.Any(operation => (string)operation["path"] == (string)j1["path"] && JToken.DeepEquals(operation["value"], j1["value"])), Is.False, "J1 path/value must not appear in a generated replace example.");
            var disable = examples.Select(array => (JObject)array.Single()).Where(operation => (string)operation["op"] == "disable"); Assert.That(disable.Any(operation => (string)operation["path"] == (string)j2["path"]), Is.False, "J2 path must not appear in a generated disable example.");
            var add = examples.Select(array => (JObject)array.Single()).Where(operation => (string)operation["op"] == "add"); var j3Module = (JObject)j3["module"]; Assert.That(add.Any(operation => (string)operation["path"] == (string)j3["path"] || (string)operation["value"]["id"] == (string)j3Module["id"] || JToken.DeepEquals(operation["value"]["parameters"], j3Module["parameters"])), Is.False, "J3 path, module ID, and parameter bundle must not appear in a generated add example.");
        }

        private static string Canonical(JToken token)
        {
            if (token is JObject) return "{" + string.Join(",", ((JObject)token).Properties().OrderBy(property => property.Name, System.StringComparer.Ordinal).Select(property => Newtonsoft.Json.JsonConvert.ToString(property.Name) + ":" + Canonical(property.Value)).ToArray()) + "}";
            if (token is JArray) return "[" + string.Join(",", ((JArray)token).Select(Canonical).ToArray()) + "]";
            return token.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static string E(string file) { return Path.Combine(VfxCohortJProtocol.EvidenceDirectory(), file); }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return System.BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty); }
    }
}
