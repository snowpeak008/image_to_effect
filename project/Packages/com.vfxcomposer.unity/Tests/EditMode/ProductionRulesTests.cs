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
            // T1b: ADR-009 layered particle templates put module children at depth 3 and clone one
            // material per renderer (up to 3 renderers per module, 2 modules per strict recipe).
            Assert.That(rules.Simple.MaxGameObjects, Is.EqualTo(10));
            Assert.That(rules.Simple.MaxDepth, Is.EqualTo(3));
            Assert.That(rules.ArchetypeProfiles.Keys, Is.EquivalentTo(new[] { "projectile", "impact", "slash", "aura", "area", "beam", "trail", "shield", "spawn", "summon", "transform", "status", "environment", "screen_ui", "composite", "decal", "weapon_trail", "destruction", "lifecycle", "portal", "loot" }));
            Assert.That(VfxProjectRules.BudgetFor("area"), Is.SameAs(rules.Complex));
            // ADR-010 §9: the legacy audit allow-list is empty after the old content layer cleanup.
            Assert.That(rules.LegacyEffectIds, Is.Empty, "Every legacy effect id retired with the T2c cleanup; new products are always strict.");
            Assert.That(VfxProjectRules.EnforcementFor("fireball_2d"), Is.EqualTo(VfxRulesEnforcement.Strict), "Retired legacy ids no longer receive audit exemptions.");
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
        public void LegacyContentSurfaces_AreFullyRetired()
        {
            // ADR-010 §9 delete-with-replacement audit: the old content layer must leave no residue.
            Assert.That(Directory.Exists(Absolute("Assets/VFX/Templates")), Is.False, "The v1 sprite template library is retired.");
            Assert.That(Directory.Exists(Absolute("Assets/VFX/Recipes")), Is.False, "The v1 recipe assets are retired.");
            Assert.That(Directory.Exists(Absolute("Assets/VFX/Preview")), Is.False, "The v1 preview scenes are retired.");
            var generated = Directory.GetDirectories(Absolute("Assets/VFX/Generated")).Select(Path.GetFileName).ToArray();
            Assert.That(generated, Is.SubsetOf(new[] { "2d", "3d" }), "Only the paradigm sample roots survive the cleanup.");
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
