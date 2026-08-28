using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using VFXComposer.Editor.Workflow;

namespace VFXComposer.Tests.EditMode
{
    /// <summary>These tests intentionally validate a frozen, approval-pending cohort with no model output.</summary>
    public sealed class S9CohortHPreregistrationTests
    {
        [Test]
        public void CohortH_FrozenInitialPayloadsAreCompleteHashedAndAwaitAttemptZero()
        {
            string snapshotHash;
            Assert.That(VfxAiWorkflowContractSnapshot.VerifyExisting("cohort-h", out snapshotHash), Is.True, "H needs its one-time verified contract snapshot.");
            var keys = VfxCohortHProtocol.RecipeKeys.Concat(VfxCohortHProtocol.PatchKeys).ToArray();
            var routeManifest = JObject.Parse(File.ReadAllText(Evidence("transport-manifest.json")));
            var initialManifest = JObject.Parse(File.ReadAllText(Evidence("initial-payloads.generated.json")));
            Assert.That((string)initialManifest["contractSha256"], Is.EqualTo(snapshotHash));
            CollectionAssert.AreEquivalent(keys, ((JObject)routeManifest["questions"]).Properties().Select(property => property.Name));
            CollectionAssert.AreEquivalent(keys, ((JObject)initialManifest["initialPayloads"]).Properties().Select(property => property.Name));
            var snapshot = Normalize(File.ReadAllText(Evidence("contract-snapshot.md")));
            foreach (var key in keys)
            {
                var route = (JObject)routeManifest["questions"][key];
                Assert.That((string)route["model"], Is.EqualTo("gpt-5.6-terra")); Assert.That((string)route["reasoningEffort"], Is.EqualTo("high")); Assert.That((string)route["forkTurns"], Is.EqualTo("none"));
                var payloadPath = VfxCohortHProtocol.InitialPayloadPath(key); var payload = Normalize(File.ReadAllText(payloadPath));
                StringAssert.Contains("ISOLATION AND OUTPUT REQUIREMENTS:\nYou are an isolated authoring agent. Do not use tools or workspace.", payload); StringAssert.Contains(snapshot, payload, key + " initial payload must include the whole frozen contract."); StringAssert.Contains("ORIGINAL PREREGISTERED REQUIREMENT:\n" + PromptFor(key), payload);
                Assert.That((string)initialManifest["initialPayloads"][key]["payloadSha256"], Is.EqualTo(Hash(File.ReadAllBytes(payloadPath))));
                Assert.That(File.Exists(VfxCohortHProtocol.AttemptPath(key, 0)), Is.False, key + " must still have attempt=0 absent before approval.");
                Assert.That(File.Exists(VfxCohortHProtocol.ReportPath(key, 0)), Is.False); Assert.That(File.Exists(VfxCohortHProtocol.TransportPath(key, 0)), Is.False);
            }
        }

        [Test]
        public void CohortH_RepairPayloadIsLFCompleteReportOnlyAndWriteOnce()
        {
            var report = "{\r\n  \"succeeded\": false,\r  \"entries\": []\r\n}\r\n";
            var prompt = VfxCohortHProtocol.BuildRepairPayload(true, report);
            StringAssert.Contains("same agent/thread", prompt); StringAssert.Contains("complete previous machine report", prompt.ToLowerInvariant());
            StringAssert.Contains("{\n  \"succeeded\": false,\n  \"entries\": []\n}\n", prompt);
            Assert.That(prompt, Does.Not.Contain("FROZEN CONTRACT SNAPSHOT:")); Assert.That(prompt, Does.Not.Contain("ORIGINAL PREREGISTERED REQUIREMENT:"));
            var path = Path.Combine(Path.GetTempPath(), "vfx-cohort-h-write-once-" + Guid.NewGuid().ToString("N") + ".md");
            try
            {
                VfxCohortHProtocol.WriteOnce(path, "a\r\nb\r\n"); Assert.DoesNotThrow(() => VfxCohortHProtocol.WriteOnce(path, "a\nb\n"));
                Assert.Throws<InvalidOperationException>(() => VfxCohortHProtocol.WriteOnce(path, "different\n"));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Test]
        public void CohortH_SpecHasExactlyFiveDistinctSupportedRecipeCombinationsAndThreeStablePatches()
        {
            var spec = JObject.Parse(File.ReadAllText(Evidence("acceptance-spec.json"))); var recipes = (JObject)spec["recipes"]; var patches = (JObject)spec["patches"];
            CollectionAssert.AreEquivalent(VfxCohortHProtocol.RecipeKeys, recipes.Properties().Select(property => property.Name)); CollectionAssert.AreEquivalent(VfxCohortHProtocol.PatchKeys, patches.Properties().Select(property => property.Name));
            var identities = recipes.Properties().Select(property => SemanticSignature((JObject)property.Value)).ToArray();
            Assert.That(identities.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(5), "H recipes must be meaningfully distinct, not G rewordings.");
            var allowed = new[] { "core.scale", "trail.time", "trail.width", "embers.rate", "embers.lifetime", "burst.count", "burst.speed", "shockwave.endSize" };
            var operators = new[] { "<", "<=", "==", ">=", ">" };
            foreach (var recipe in recipes.Properties())
            {
                var comparisons = ((JObject)recipe.Value["compare"]).Properties().ToArray(); Assert.That(comparisons.All(property => allowed.Contains(property.Name)), Is.True, recipe.Name + " may use only actual formal parameters.");
                Assert.That(comparisons.All(property => operators.Contains((string)property.Value)), Is.True, recipe.Name + " has an unsupported semantic comparison.");
            }
            var cohortG = JObject.Parse(File.ReadAllText(Path.Combine(Directory.GetParent(VfxCohortHProtocol.EvidenceDirectory()).FullName, "cohort-g", "acceptance-spec.json")));
            var gSignatures = ((JObject)cohortG["recipes"]).Properties().Select(property => SemanticSignature((JObject)property.Value)).ToArray();
            Assert.That(identities.Intersect(gSignatures, StringComparer.Ordinal), Is.Empty, "H may not reuse any Cohort G semantic combination.");
            StringAssert.Contains("/stages/travel/modules/embers/parameters/rate", (string)patches["P1"]["path"]); StringAssert.Contains("/stages/travel/modules/embers", (string)patches["P2"]["path"]); Assert.That((string)patches["P3"]["attachTo"], Is.EqualTo("core"));
        }

        private static string Evidence(string file) { return Path.Combine(VfxCohortHProtocol.EvidenceDirectory(), file); }
        private static string PromptFor(string key)
        {
            var text = File.ReadAllText(Evidence("prompts.md")); var marker = "## " + key + "\n\n"; var start = text.IndexOf(marker, StringComparison.Ordinal) + marker.Length; var end = text.IndexOf("\n## ", start, StringComparison.Ordinal); return (end < 0 ? text.Substring(start) : text.Substring(start, end - start)).TrimEnd();
        }
        private static string Normalize(string text) { return text.Replace("\r\n", "\n").Replace("\r", "\n"); }
        private static string SemanticSignature(JObject recipe)
        {
            var travel = string.Join(",", recipe["travel"].Values<string>().OrderBy(value => value, StringComparer.Ordinal)); var forbidden = string.Join(",", (recipe["forbidTravel"] ?? new JArray()).Values<string>().OrderBy(value => value, StringComparer.Ordinal)); var comparisons = string.Join(",", ((JObject)recipe["compare"]).Properties().OrderBy(property => property.Name, StringComparer.Ordinal).Select(property => property.Name + "=" + property.Value)); var impact = string.Join(",", (recipe["impact"] ?? new JArray("impact_burst", "shockwave")).Values<string>().OrderBy(value => value, StringComparer.Ordinal)); return travel + "|" + forbidden + "|" + comparisons + "|" + impact;
        }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty); }
    }
}
