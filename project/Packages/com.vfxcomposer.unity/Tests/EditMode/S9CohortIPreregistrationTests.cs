using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using VFXComposer.Editor.Workflow;

namespace VFXComposer.Tests.EditMode
{
    [Explicit("Cohort I preregistration assertions apply only before attempt zero.")]
    public sealed class S9CohortIPreregistrationTests
    {
        [Test]
        public void CohortI_FrozenTempPairsAndShortEnvelopesAreAuditableBeforeAttemptZero()
        {
            string snapshotHash; Assert.That(VfxAiWorkflowContractSnapshot.VerifyExisting("cohort-i", out snapshotHash), Is.True);
            var manifest = JObject.Parse(File.ReadAllText(Evidence("initial-payloads.generated.json"))); Assert.That((string)manifest["contractSha256"], Is.EqualTo(snapshotHash)); Assert.That((string)manifest["tempRoot"], Is.EqualTo(VfxCohortIProtocol.TempRoot));
            foreach (var key in VfxCohortIProtocol.RecipeKeys.Concat(VfxCohortIProtocol.PatchKeys))
            {
                var payload = VfxCohortIProtocol.InitialPayloadPath(key); var temp = VfxCohortIProtocol.TempInitialPayloadPath(key); var envelope = VfxCohortIProtocol.InitialEnvelopePath(key); var entry = (JObject)manifest["initialPayloads"][key];
                CollectionAssert.AreEqual(File.ReadAllBytes(payload), File.ReadAllBytes(temp), key + " temp payload must byte-equal workspace evidence."); CollectionAssert.AreEqual(new[] { temp }, Directory.GetFiles(Path.GetDirectoryName(temp)), key + " temp directory may contain only its initial payload before dispatch."); Assert.That((string)entry["payloadSha256"], Is.EqualTo(Hash(File.ReadAllBytes(payload)))); Assert.That((string)entry["tempPayloadSha256"], Is.EqualTo(Hash(File.ReadAllBytes(temp)))); Assert.That((string)entry["envelopeSha256"], Is.EqualTo(Hash(File.ReadAllBytes(envelope))));
                var text = File.ReadAllText(envelope); StringAssert.Contains(temp, text); StringAssert.Contains((string)entry["payloadSha256"], text); StringAssert.Contains("exactly one `exec_command`", text); Assert.That(text, Does.Not.Contain("acceptance-spec"));
                Assert.That(File.Exists(VfxCohortIProtocol.AttemptPath(key, 0)), Is.False); Assert.That(File.Exists(VfxCohortIProtocol.ReportPath(key, 0)), Is.False); Assert.That(File.Exists(VfxCohortIProtocol.TransportPath(key, 0)), Is.False);
            }
        }

        [Test]
        public void CohortI_HasFiveDistinctNewSemanticCombinationsAndThreeNewStablePatches()
        {
            var i = JObject.Parse(File.ReadAllText(Evidence("acceptance-spec.json"))); var recipes = (JObject)i["recipes"]; CollectionAssert.AreEquivalent(VfxCohortIProtocol.RecipeKeys, recipes.Properties().Select(item => item.Name)); CollectionAssert.AreEquivalent(VfxCohortIProtocol.PatchKeys, ((JObject)i["patches"]).Properties().Select(item => item.Name));
            var signatures = recipes.Properties().Select(item => Signature((JObject)item.Value)).ToArray(); Assert.That(signatures.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(5));
            var allowedParameters = new[] { "core.scale", "trail.time", "trail.width", "embers.rate", "embers.lifetime", "burst.count", "burst.speed", "shockwave.endSize" }; var allowedOperators = new[] { "<", "<=", "==", ">=", ">" };
            foreach (var recipe in recipes.Properties()) foreach (var comparison in ((JObject)recipe.Value["compare"]).Properties()) { Assert.That(allowedParameters.Contains(comparison.Name), Is.True, recipe.Name + " uses an unsupported formal parameter."); Assert.That(allowedOperators.Contains((string)comparison.Value), Is.True, recipe.Name + " uses an unsupported comparison operator."); }
            var root = Directory.GetParent(VfxCohortIProtocol.EvidenceDirectory()).FullName; var prior = new[] { "cohort-g", "cohort-h" }.SelectMany(folder => ((JObject)JObject.Parse(File.ReadAllText(Path.Combine(root, folder, "acceptance-spec.json")))["recipes"]).Properties().Select(item => Signature((JObject)item.Value))).ToArray(); Assert.That(signatures.Intersect(prior, StringComparer.Ordinal), Is.Empty, "I must not reuse a G/H semantic combination.");
            var patches = (JObject)i["patches"]; StringAssert.Contains("/modules/trail/parameters/width", (string)patches["P1"]["path"]); StringAssert.Contains("/modules/trail", (string)patches["P2"]["path"]); Assert.That((string)patches["P3"]["moduleId"], Is.EqualTo("afterglow_embers"));
        }

        private static string Evidence(string file) { return Path.Combine(VfxCohortIProtocol.EvidenceDirectory(), file); }
        private static string Signature(JObject recipe) { return string.Join(",", recipe["travel"].Values<string>().OrderBy(x => x)) + "|" + string.Join(",", (recipe["forbidTravel"] ?? new JArray()).Values<string>().OrderBy(x => x)) + "|" + string.Join(",", ((JObject)recipe["compare"]).Properties().OrderBy(x => x.Name).Select(x => x.Name + "=" + x.Value)) + "|" + string.Join(",", (recipe["impact"] ?? new JArray("impact_burst", "shockwave")).Values<string>().OrderBy(x => x)); }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty); }
    }
}
