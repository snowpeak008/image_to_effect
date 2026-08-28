using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Rules;
using VFXComposer;

namespace VFXComposer.Tests.EditMode
{
    public sealed class ProductionRulesTests
    {
        private const string StrictId = "production_rules_probe";
        private const string StrictFolder = "Assets/VFX/Generated/production_rules_probe";
        private const string StrictPrefab = StrictFolder + "/VFX_production_rules_probe.prefab";

        [SetUp]
        public void SetUp()
        {
            VfxProjectRules.ReloadForTests();
            DeleteStrictProbe();
        }

        [TearDown]
        public void TearDown() { DeleteStrictProbe(); }

        [Test]
        public void RulesConfig_IsMachineReadableAndDefaultsNewEffectsToStrict()
        {
            var rules = VfxProjectRules.Load();
            Assert.That(rules.SchemaVersion, Is.EqualTo(1));
            Assert.That(rules.RulesVersion, Is.EqualTo("1.0-draft"));
            Assert.That(rules.Simple.MaxGameObjects, Is.EqualTo(10));
            Assert.That(rules.Simple.MaxDepth, Is.EqualTo(2));
            Assert.That(rules.ArchetypeProfiles.Keys, Is.EquivalentTo(new[] { "projectile", "impact", "slash", "aura", "area", "beam", "trail", "shield", "spawn", "summon", "transform", "status", "environment", "screen_ui", "composite", "decal", "weapon_trail", "destruction", "lifecycle", "portal", "loot" }));
            Assert.That(VfxProjectRules.BudgetFor("area"), Is.SameAs(rules.Complex));
            Assert.That(VfxProjectRules.EnforcementFor("fireball_2d"), Is.EqualTo(VfxRulesEnforcement.LegacyAudit));
            CollectionAssert.AreEquivalent(new[]
            {
                "fireball_2d", "fireball_3d", "slash_3d_stylized",
                "fireball_2d_s7test", "fireball_2d_s8test", "fireball_3d_s10test",
                "s11_a5", "s11_a6", "s9_canonical_patch_export_base",
                "i1_river_comet", "i2_glass_spark", "i3_brazier_bead", "i4_rail_flare", "i5_aurora_seed",
                "s9_cohort_k_final_k1", "s9_cohort_k_final_k2", "s9_cohort_k_final_k3"
            }, rules.LegacyEffectIds, "Legacy audit is a closed allow-list of protected old products and isolated historical test IDs.");
            Assert.That(VfxProjectRules.EnforcementFor("cap_linear_proj_3d"), Is.EqualTo(VfxRulesEnforcement.Strict), "Capability products must not inherit historical test exemptions.");
            Assert.That(VfxProjectRules.EnforcementFor(StrictId), Is.EqualTo(VfxRulesEnforcement.Strict));
            Assert.That(VfxProjectRules.ManifestAbsolutePath(StrictId), Does.EndWith("ProjectSettings" + Path.DirectorySeparatorChar + "VFXComposer" + Path.DirectorySeparatorChar + "BuildManifests" + Path.DirectorySeparatorChar + StrictId + ".manifest.json"));
            Assert.Throws<ArgumentException>(() => VfxProjectRules.ManifestAbsolutePath("Bad/Id"));
        }

        [Test]
        public void StrictOutput_RejectsHierarchyOverBudgetAndUnsavedRecipe()
        {
            EnsureFolder(StrictFolder);
            var root = new GameObject("VFX_production_rules_probe");
            try
            {
                var parent = root.transform;
                for (var index = 0; index < 11; index++) { var child = new GameObject("Node_" + index); child.transform.SetParent(parent, false); parent = child.transform; }
                PrefabUtility.SaveAsPrefabAsset(root, StrictPrefab);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            var audit = VfxOutputAuditor.Audit(StrictId, "slash", StrictPrefab, StrictFolder);
            Assert.That(audit.Report.Contains("R8003", "/structure/gameObjects"), Is.True);
            Assert.That(audit.Report.Contains("R8004", "/structure/maxDepth"), Is.True);
            Assert.That(audit.Report.HasErrors, Is.True);
            var manifest = VfxProductionRules.EnforceAndWriteManifest(StrictId, "slash", 1, 1, "no-saved-recipe", "build", "test", StrictPrefab, StrictFolder, .2);
            Assert.That(manifest.Report.HasErrors, Is.True);
            Assert.That(File.Exists(VfxProjectRules.ManifestAbsolutePath(StrictId)), Is.False);
        }

        [Test]
        public void ReconcileCurrentOutputs_WritesExternalOwnershipManifestsWithoutChangingRuntimeFolders()
        {
            var before = Directory.GetDirectories(Absolute("Assets/VFX/Generated")).Select(Path.GetFileName).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            VfxProductionRulesMenu.ReconcileCurrentOutputs();
            var after = Directory.GetDirectories(Absolute("Assets/VFX/Generated")).Select(Path.GetFileName).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(before, after);
            foreach (var effectId in new[] { "fireball_2d", "fireball_3d", "slash_3d_stylized" })
            {
                var path = VfxProjectRules.ManifestAbsolutePath(effectId);
                Assert.That(File.Exists(path), Is.True, effectId);
                var manifest = JObject.Parse(File.ReadAllText(path));
                Assert.That((int)manifest["manifestVersion"], Is.EqualTo(1));
                Assert.That((string)manifest["rulesVersion"], Is.EqualTo("1.0-draft"));
                Assert.That((string)manifest["enforcement"], Is.EqualTo("legacy_audit"));
                Assert.That((string)manifest["effectId"], Is.EqualTo(effectId));
                Assert.That((string)manifest["archetype"], Is.EqualTo(effectId == "slash_3d_stylized" ? "slash" : "projectile"));
                Assert.That((string)manifest["runtimeEntry"]["kind"], Is.EqualTo("prefab"));
                Assert.That(((JArray)manifest["ownedOutputs"]).Count, Is.GreaterThan(0));
                Assert.That(((JArray)manifest["ownedOutputs"]).All(item => !((string)item["path"]).EndsWith("BuildManifest.json", StringComparison.Ordinal)), Is.True);
                Assert.That(((JArray)manifest["ownedOutputs"]).All(item => !string.IsNullOrEmpty((string)item["guid"]) && ((string)item["sha256"]).Length == 64), Is.True);
                Assert.That(((JArray)manifest["dependencies"]).All(item => !string.IsNullOrEmpty((string)item["path"]) && item["dependencyHash"] != null), Is.True);
                Assert.That(manifest["cost"]["localTextureBytes"], Is.Not.Null);
                Assert.That(manifest["cost"]["dependencyResidentTextureBytes"], Is.Not.Null);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>((string)manifest["runtimeEntry"]["path"]);
                Assert.That(prefab.GetComponents<MonoBehaviour>().Count(value => value is IVfxRuntimeEntry), Is.EqualTo(1));
            }
        }

        private static void DeleteStrictProbe()
        {
            if (AssetDatabase.IsValidFolder(StrictFolder)) AssetDatabase.DeleteAsset(StrictFolder);
            var manifest = VfxProjectRules.ManifestAbsolutePath(StrictId);
            if (File.Exists(manifest)) File.Delete(manifest);
            if (File.Exists(manifest + ".pending")) File.Delete(manifest + ".pending");
            AssetDatabase.Refresh();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string Absolute(string assetPath) { return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)); }
    }
}
