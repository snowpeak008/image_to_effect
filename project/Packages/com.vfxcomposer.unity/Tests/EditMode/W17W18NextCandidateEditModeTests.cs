using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VFXComposer.Editor.NextCandidates;
using VFXComposer.W17W18NextCandidate;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W17W18NextCandidateEditModeTests
    {
        [Test]
        public void SourceRecipes_AreFourteenUniqueNullVerdictCandidatesThatPreserveRejectedOutputs()
        {
            var entries = new List<Tuple<string, string, string>>();
            entries.AddRange(W17W18NextCandidateCatalog.W17.Select(value => Tuple.Create(W17W18NextCandidateAuthoring.W17RecipePath(value.Id), value.Id, "w17-ui-next-candidate/v1")));
            entries.AddRange(W17W18NextCandidateCatalog.W18.Select(value => Tuple.Create(W17W18NextCandidateAuthoring.W18RecipePath(value.Id), value.Id, "w18-theme-next-candidate/v1")));
            Assert.That(entries.Count, Is.EqualTo(14));
            Assert.That(entries.Select(value => value.Item2).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(14));
            foreach (var entry in entries)
            {
                var root = JObject.Parse(File.ReadAllText(Absolute(entry.Item1)));
                Assert.That((string)root["schema"], Is.EqualTo(entry.Item3), entry.Item1);
                Assert.That((string)root["id"], Is.EqualTo(entry.Item2));
                Assert.That((string)root["candidateStatus"], Is.EqualTo(W17W18NextCandidateAuthoring.CandidateStatus));
                Assert.That(root["userVisualVerdict"].Type, Is.EqualTo(JTokenType.Null));
                Assert.That((bool)root["preserveRejectedCandidate"], Is.True);
                Assert.That(entry.Item2.EndsWith("_next_candidate", StringComparison.Ordinal), Is.True);
            }
        }

        [Test]
        public void BuildAll_ProducesDedicatedRuntimeEntriesRealCarriersStrictBudgetsAndNoParticleUi()
        {
            W17W18NextCandidateAuthoring.BuildAllForBatch();
            foreach (var plan in W17W18NextCandidateCatalog.W17)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(W17W18NextCandidateAuthoring.W17PrefabPath(plan.Id));
                Assert.That(prefab, Is.Not.Null, plan.Id);
                var entry = prefab.GetComponent<W17UiInteractionController>();
                Assert.That(entry, Is.Not.Null);
                Assert.That(prefab.GetComponents<MonoBehaviour>().Any(value => value != null && value.GetType().Name == "PlannedContentVfxController"), Is.False, "The next candidate is isolated from the rejected generic carrier.");
                Assert.That(entry.HasHardClip, Is.True);
                var budget = entry.ReadBudget();
                var limit = plan.Kind == W17UiEffectKind.GachaSingle || plan.Kind == W17UiEffectKind.GachaTen ? W17UiInteractionController.GachaUiElementBudget : W17UiInteractionController.NormalUiElementBudget;
                Assert.That(budget.Graphics, Is.LessThanOrEqualTo(limit), plan.Id);
                Assert.That(budget.ParticleSystems, Is.Zero, plan.Id);
                Assert.That(prefab.GetComponentsInChildren<Graphic>(true).Length, Is.GreaterThan(0), plan.Id + " must own real UI geometry.");
                var manifest = JObject.Parse(File.ReadAllText(Absolute(W17W18NextCandidateAuthoring.ManifestPath("W17", plan.Id))));
                Assert.That((string)manifest["candidateStatus"], Is.EqualTo(W17W18NextCandidateAuthoring.CandidateStatus));
                Assert.That(manifest["userVisualVerdict"].Type, Is.EqualTo(JTokenType.Null));
                Assert.That((string)manifest["hardClip"], Is.EqualTo("RectMask2D"));
            }
            foreach (var plan in W17W18NextCandidateCatalog.W18)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(W17W18NextCandidateAuthoring.W18PrefabPath(plan.Id));
                Assert.That(prefab, Is.Not.Null, plan.Id);
                var entry = prefab.GetComponent<W18CharacterThemeController>();
                Assert.That(entry, Is.Not.Null);
                Assert.That(entry.PaletteReference, Is.EqualTo(plan.PaletteReference));
                Assert.That(entry.ShapeLanguage, Is.EqualTo(plan.ShapeLanguage));
                Assert.That(entry.UsesHardClipShader(), Is.True, plan.Id);
                Assert.That(entry.PreviewHardClip, Is.False, plan.Id + " production Prefab must not world-clip before a Preview cell supplies its rect.");
                var budget = entry.ReadBudget();
                Assert.That(budget.Renderers, Is.LessThanOrEqualTo(W18CharacterThemeController.MaxRendererBudget), plan.Id);
                Assert.That(budget.Materials, Is.LessThanOrEqualTo(W18CharacterThemeController.MaxMaterialBudget), plan.Id);
                Assert.That(budget.ParticleSystems, Is.LessThanOrEqualTo(W18CharacterThemeController.MaxParticleSystemBudget), plan.Id);
                Assert.That(budget.ParticleCapacity, Is.LessThanOrEqualTo(W18CharacterThemeController.MaxParticleCapacity), plan.Id);
                Assert.That(prefab.GetComponentsInChildren<MeshFilter>(true).Any(value => value.sharedMesh != null && value.sharedMesh.vertexCount >= 4), Is.True, plan.Id + " must own visible topology.");
            }
        }

        [Test]
        public void Rebuild_IsByteStableForCandidateManifestsAndKeepsOldScenePrefabHashes()
        {
            W17W18NextCandidateAuthoring.BuildAllForBatch();
            var candidatePaths = W17W18NextCandidateCatalog.W17.Select(value => W17W18NextCandidateAuthoring.ManifestPath("W17", value.Id))
                .Concat(W17W18NextCandidateCatalog.W18.Select(value => W17W18NextCandidateAuthoring.ManifestPath("W18", value.Id)))
                .Concat(new[] { W17W18NextCandidateAuthoring.W17PreviewScenePath, W17W18NextCandidateAuthoring.W18PreviewScenePath }).ToArray();
            var candidateBefore = candidatePaths.ToDictionary(value => value, value => Sha256(Absolute(value)), StringComparer.Ordinal);
            var protectedPaths = new[]
            {
                "Assets/VFX/Preview/VFXPREVIEW_GameUI.unity",
                "Assets/VFX/Preview/VFXPREVIEW_HeroKits.unity",
                "Assets/VFX/Generated/button_press_fx_ui/VFX_button_press_fx_ui.prefab",
                "Assets/VFX/Generated/flame_blade_samurai_kit_showcase_3d/VFX_flame_blade_samurai_kit_showcase_3d.prefab"
            }.Where(value => File.Exists(Absolute(value))).ToArray();
            var protectedBefore = protectedPaths.ToDictionary(value => value, value => AssetDatabase.AssetPathToGUID(value) + "|" + Sha256(Absolute(value)), StringComparer.Ordinal);
            W17W18NextCandidateAuthoring.BuildAllForBatch();
            foreach (var item in candidateBefore) Assert.That(Sha256(Absolute(item.Key)), Is.EqualTo(item.Value), item.Key);
            foreach (var item in protectedBefore) Assert.That(AssetDatabase.AssetPathToGUID(item.Key) + "|" + Sha256(Absolute(item.Key)), Is.EqualTo(item.Value), item.Key);
        }

        [Test]
        public void VisualPlans_EncodeOriginalInteractionAndThemeSemanticsInsteadOfStatusOnly()
        {
            W17W18NextCandidateAuthoring.BuildAllForBatch();
            var button = AssetDatabase.LoadAssetAtPath<GameObject>(W17W18NextCandidateAuthoring.W17PrefabPath("button_press_fx_ui_next_candidate")).GetComponent<W17UiInteractionController>();
            CollectionAssert.IsSubsetOf(new[] { "ButtonSurface", "Ripple", "EdgeSweep", "Star_0", "Star_1" }, button.GetComponentsInChildren<RectTransform>(true).Select(value => value.name).ToArray());
            var reward = AssetDatabase.LoadAssetAtPath<GameObject>(W17W18NextCandidateAuthoring.W17PrefabPath("reward_fly_collect_ui_next_candidate")).GetComponent<W17UiInteractionController>();
            Assert.That(reward.ReadBudget().PooledRewards, Is.EqualTo(12));
            var ghost = AssetDatabase.LoadAssetAtPath<GameObject>(W17W18NextCandidateAuthoring.W18PrefabPath("ghost_curse_shrine_kit_next_candidate")).GetComponent<W18CharacterThemeController>();
            var talisman = ghost.FindCarrier("TalismanArray").GetComponent<MeshFilter>().sharedMesh;
            Assert.That(talisman.triangles.Length / 3, Is.EqualTo(16), "Eight independent talisman quads are one bounded draw carrier.");
            var procession = ghost.FindCarrier("HundredGhosts").GetComponent<MeshFilter>().sharedMesh;
            Assert.That(procession.triangles.Length / 6, Is.EqualTo(12), "The ultimate owns twelve visible ghost silhouettes, each encoded as one quad.");
            Assert.That(ghost.GetComponentsInChildren<Transform>(true).Any(value => value.name.Contains(W17W18NextCandidateAuthoring.CandidateStatus)), Is.False, "Status text is not embedded as the effect carrier.");
        }

        private static string Sha256(string path)
        {
            using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
        }

        private static string Absolute(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
