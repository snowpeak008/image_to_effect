using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VFXComposer.Editor.Elements;
using VFXComposer.Editor.Patch;
using VFXComposer.Editor.Rules;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W3W5ElementNextCandidateTests
    {
        private static readonly string[] Families = { "fire", "frost", "lightning" };

        [Test]
        public void TwentyTwoPlanCohort_StaysW3W5OnlyAndBindsEveryContentParameterToPhysicalWork()
        {
            var entries = CandidateEntries().ToArray();
            Assert.That(entries.Length, Is.EqualTo(22));
            var shapes = new HashSet<string>(StringComparer.Ordinal);
            var topologies = new HashSet<string>(StringComparer.Ordinal);

            foreach (var entry in entries)
            {
                var result = ElementNextCandidateCompiler.PlanAsset(RecipePath(entry));
                Assert.That(result.Succeeded, Is.True, entry.Id + ": " + Describe(result));
                var plan = result.Plan;
                Assert.That(plan.EffectId, Is.EqualTo(entry.Id));
                Assert.That(plan.Bindings.Count, Is.EqualTo(plan.Parameters.Count), entry.Id);
                Assert.That(plan.Bindings.All(value => !string.IsNullOrEmpty(value.Carrier)), Is.True, entry.Id);
                CollectionAssert.AreEquivalent(plan.Parameters.Keys, plan.Bindings.Select(value => value.Parameter), entry.Id);
                Assert.That(plan.ParticleBudget, Is.LessThanOrEqualTo(ElementNextCandidateVisualExecutor.AbsoluteMaxParticleCapacity), entry.Id);
                Assert.That(plan.RendererBudget, Is.LessThanOrEqualTo(ElementNextCandidateVisualExecutor.AbsoluteMaxRendererCount), entry.Id);
                Assert.That(plan.MaxLocalExtent, Is.GreaterThan(0f), entry.Id);
                Assert.That(shapes.Add(plan.ShapeToken), Is.True, "Each effect owns an explicit carrier shape: " + entry.Id);
                Assert.That(topologies.Add(plan.TopologySignature), Is.True, entry.Id);
            }

            var water = ElementFamilyCatalog.All.Single(value => value.Id == "water_jet_beam_3d");
            var routed = ElementNextCandidateCompiler.PlanAsset(ElementNextCandidateW6W8Authoring.RecipePath(water));
            Assert.That(routed.Succeeded, Is.True, Describe(routed));
            Assert.That(routed.Plan.CompilerVersion, Is.EqualTo(ElementNextCandidatePlanCompiler.CompilerVersionW6W8));
            Assert.That(ElementNextCandidateCompiler.PrefabPath(water.Id), Does.StartWith(ElementNextCandidateCompiler.GeneratedRootW6W8 + "/"));
            Assert.That(CandidateEntries().Any(value => value.Id == water.Id), Is.False, "The W3-W5 authoring filter must stay at its original twenty-two entries.");
        }

        [Test]
        public void SemanticPatches_ChangeFireFrostAndLightningPhysicalPlans()
        {
            AssertPatchChangesCarrier("flame_slash_2d", "arc_width", .9f, "PrimaryFlameCrescent.width");
            AssertPatchChangesCarrier("ice_spike_spawn_3d", "height", 2.1f, "PrimaryCrystalSpikes.height");
            AssertPatchChangesCarrier("thunder_strike_impact_3d", "fork_count", 3, "ArcBranchBatch.topology");
        }

        [Test]
        public void ExtremeValidContent_ExpandsCompiledBoundsBeforePreviewScaling()
        {
            AssertExtremeExtent("chain_blast_impact_2d", new Dictionary<string, object> { ["per_blast_scale"] = 3f }, 3.9f);
            AssertExtremeExtent("flash_freeze_transform_3d", new Dictionary<string, object> { ["shatter_scale"] = 5f }, 5.1f);
            AssertExtremeExtent("ball_lightning_projectile_3d", new Dictionary<string, object> { ["discharge_range"] = 20f }, 10.8f);
            AssertExtremeExtent("blizzard_area_3d", new Dictionary<string, object> { ["radius"] = 3f, ["fog_height"] = 5f }, 6.7f);
        }

        [Test]
        public void DedicatedBuild_IsIdempotentBudgetedAndDoesNotRewriteRejectedOrOtherWorkstreamOutputs()
        {
            var protectedPaths = ProtectedPaths().Distinct(StringComparer.Ordinal).ToArray();
            var before = protectedPaths.ToDictionary(value => value, HashAsset, StringComparer.Ordinal);

            ElementNextCandidateAuthoring.BuildW3W5ForBatch();
            var first = CandidateEntries().ToDictionary(
                value => value.Id,
                value => AssetDatabase.AssetPathToGUID(ElementNextCandidateCompiler.PrefabPath(value.Id)) + "|" + ManifestHash(value.Id),
                StringComparer.Ordinal);
            var second = Families.SelectMany(ElementNextCandidateAuthoring.BuildEntries).ToArray();
            Assert.That(second.Length, Is.EqualTo(22));
            Assert.That(second.All(value => value.Succeeded && value.Unchanged), Is.True, string.Join(" | ", second.Select(Describe).ToArray()));
            CollectionAssert.AreEquivalent(first, CandidateEntries().ToDictionary(
                value => value.Id,
                value => AssetDatabase.AssetPathToGUID(ElementNextCandidateCompiler.PrefabPath(value.Id)) + "|" + ManifestHash(value.Id),
                StringComparer.Ordinal));

            foreach (var pair in before)
                Assert.That(HashAsset(pair.Key), Is.EqualTo(pair.Value), "W3-W5-only build changed a protected old/other-workstream path: " + pair.Key);

            var primaryMeshPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in CandidateEntries())
            {
                var prefabPath = ElementNextCandidateCompiler.PrefabPath(entry.Id);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.That(prefab, Is.Not.Null, prefabPath);
                var executor = prefab.GetComponent<ElementNextCandidateVisualExecutor>();
                Assert.That(executor, Is.Not.Null, entry.Id);
                Assert.That(executor.CompilerVersion, Is.EqualTo(ElementNextCandidatePlanCompiler.CompilerVersion));
                Assert.That(executor.VisualStatus, Is.EqualTo(ElementNextCandidatePlanCompiler.VisualStatus));
                Assert.That(prefab.GetComponents<MonoBehaviour>().Count(value => value is IVfxRuntimeEntry), Is.EqualTo(1), entry.Id);
                Assert.That(prefab.GetComponent<StyledVfxController>(), Is.Null, "The old shared executor must not carry the next candidate.");
                Assert.That(prefab.GetComponentInChildren<ElementNextCandidatePreviewDriver>(true), Is.Null, entry.Id);
                Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty, entry.Id);
                Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true).Length, Is.EqualTo(1), entry.Id);
                Assert.That(prefab.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(executor.OwnedRendererCount), entry.Id);
                Assert.That(executor.OwnedRendererCount, Is.LessThanOrEqualTo(executor.RendererBudget), entry.Id);
                Assert.That(executor.BudgetWithinLimits, Is.True, entry.Id);
                Assert.That(executor.ParameterBindingCount, Is.GreaterThan(0), entry.Id);

                var primary = prefab.GetComponentsInChildren<MeshFilter>(true).First(value => value.name.Contains("Primary"));
                var primaryPath = AssetDatabase.GetAssetPath(primary.sharedMesh);
                Assert.That(primaryPath, Does.StartWith(ElementNextCandidateCompiler.OutputFolder(entry.Id) + "/Meshes/"), entry.Id);
                Assert.That(primaryMeshPaths.Add(primaryPath), Is.True, "No fixed body mesh may be shared across entries: " + entry.Id);

                var lines = prefab.GetComponentsInChildren<LineRenderer>(true);
                Assert.That(lines.Length, Is.EqualTo(ElementNextCandidateCompiler.PlanAsset(RecipePath(entry)).Plan.ArcCarrierCount), entry.Id);
                var manifest = JObject.Parse(File.ReadAllText(Absolute(ElementNextCandidateCompiler.ManifestPath(entry.Id))));
                Assert.That((string)manifest["compilerVersion"], Is.EqualTo(ElementNextCandidatePlanCompiler.CompilerVersion));
                Assert.That((string)manifest["visualStatus"], Is.EqualTo("VISUAL_PENDING"));
                Assert.That((bool)manifest["oldRejectedCandidateModified"], Is.False);
                Assert.That(manifest["userVisualVerdict"].Type, Is.EqualTo(JTokenType.Null));
                Assert.That((bool)manifest["machineEvidenceIsVisualAcceptance"], Is.False);
                Assert.That(((JArray)manifest["parameterBindings"]).Count, Is.EqualTo(executor.ParameterBindingCount));
            }

            var body = AssetDatabase.LoadAssetAtPath<Material>(ElementNextCandidateCompiler.BodyMaterialPath);
            var atmosphere = AssetDatabase.LoadAssetAtPath<Material>(ElementNextCandidateCompiler.AtmosphereMaterialPath);
            var highlight = AssetDatabase.LoadAssetAtPath<Material>(ElementNextCandidateCompiler.HighlightMaterialPath);
            Assert.That(body.GetFloat("_DstBlend"), Is.EqualTo((float)BlendMode.OneMinusSrcAlpha));
            Assert.That(atmosphere.GetFloat("_DstBlend"), Is.EqualTo((float)BlendMode.OneMinusSrcAlpha));
            Assert.That(highlight.GetFloat("_DstBlend"), Is.EqualTo((float)BlendMode.One));
        }

        [Test]
        public void ThreePreviewScenes_HavePendingRootsFixedNonOverlappingCellsAndNoMachineAcceptance()
        {
            ElementNextCandidateAuthoring.BuildW3W5ForBatch();
            foreach (var family in Families)
            {
                var path = ElementNextCandidateAuthoring.ScenePath(family);
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, path);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                var statusName = ElementNextCandidateAuthoring.CandidateStatusRoot(family);
                var status = roots.SingleOrDefault(value => value.name == statusName);
                Assert.That(status, Is.Not.Null, family);
                Assert.That(status.GetComponent<TextMesh>().text, Does.Contain("VISUAL SIGN-OFF PENDING"));
                Assert.That(status.GetComponent<TextMesh>().text, Does.Not.Contain("ACCEPTED"));

                var expected = family == "fire" ? 8 : 7;
                var cells = roots.Where(value => value.name.StartsWith("Cell_", StringComparison.Ordinal)).Select(value => value.GetComponent<ElementNextCandidateCell>()).ToArray();
                Assert.That(cells.Length, Is.EqualTo(expected), family);
                Assert.That(cells.All(value => value != null && value.EffectAndLabelAreDisjoint && value.ScaledEnvelopeFitsEffectBounds), Is.True, family);
                Assert.That(cells.Select(value => value.CellIndex).OrderBy(value => value), Is.EqualTo(Enumerable.Range(1, expected)));
                AssertCellsDoNotOverlap(cells);
                Assert.That(roots.SelectMany(value => value.GetComponentsInChildren<ElementNextCandidateVisualExecutor>(true)).Count(), Is.EqualTo(expected), family);
                Assert.That(roots.SelectMany(value => value.GetComponentsInChildren<Camera>(true)).Count(), Is.EqualTo(1), family);
                var driver = roots.SelectMany(value => value.GetComponentsInChildren<ElementNextCandidatePreviewDriver>(true)).Single();
                Assert.That(driver.EntryCount, Is.EqualTo(expected), family);
                Assert.That(roots.SelectMany(value => value.GetComponentsInChildren<ElementNextCandidateVisualExecutor>(true)).All(value => value.GetComponent<ElementNextCandidatePreviewDriver>() == null), Is.True);

                var receipt = JObject.Parse(File.ReadAllText(Absolute(ElementNextCandidateCompiler.CandidateRoot + "/Preview/" + family + "-preview.json")));
                Assert.That((string)receipt["visualStatus"], Is.EqualTo("VISUAL_PENDING"));
                Assert.That((string)receipt["candidateStatusRoot"], Is.EqualTo(statusName));
                Assert.That((bool)receipt["machineEvidenceIsVisualAcceptance"], Is.False);
            }
        }

        private static void AssertPatchChangesCarrier(string effectId, string parameter, object value, string expectedCarrier)
        {
            var entry = ElementFamilyCatalog.All.Single(item => item.Id == effectId);
            var path = RecipePath(entry);
            var source = File.ReadAllText(Absolute(path));
            var before = ElementNextCandidatePlanCompiler.PlanJson(path, source);
            var patch = new JArray(new JObject
            {
                ["op"] = "set_content_param",
                ["path"] = "/content/parameters/" + parameter,
                ["value"] = JToken.FromObject(value)
            }).ToString();
            var validated = new VfxPatchService().Validate(source, patch, 1);
            Assert.That(validated.IsValid, Is.True, effectId + ": " + string.Join(" | ", validated.Report.Entries.Select(item => item.Code + " " + item.Path + " " + item.Message).ToArray()));
            var after = ElementNextCandidatePlanCompiler.PlanJson(path, validated.PatchedRecipeJson);
            Assert.That(before.Succeeded && after.Succeeded, Is.True, effectId);
            Assert.That(after.Plan.TopologySignature, Is.Not.EqualTo(before.Plan.TopologySignature), effectId);
            Assert.That(after.Plan.BuildHash, Is.Not.EqualTo(before.Plan.BuildHash), effectId);
            Assert.That(after.Plan.CarrierFor(parameter), Is.EqualTo(expectedCarrier));
            Assert.That(after.Plan.Number(parameter, -1f), Is.EqualTo(Convert.ToSingle(value)).Within(.0001f));
        }

        private static void AssertExtremeExtent(string effectId, IDictionary<string, object> values, float minimum)
        {
            var entry = ElementFamilyCatalog.All.Single(item => item.Id == effectId);
            var path = RecipePath(entry);
            var recipe = JObject.Parse(File.ReadAllText(Absolute(path)));
            foreach (var pair in values) recipe["content"]["parameters"][pair.Key] = JToken.FromObject(pair.Value);
            var planned = ElementNextCandidatePlanCompiler.PlanJson(path, recipe.ToString());
            Assert.That(planned.Succeeded, Is.True, effectId + ": " + Describe(planned));
            Assert.That(planned.Plan.MaxLocalExtent, Is.GreaterThanOrEqualTo(minimum), effectId);
            var scale = ElementNextCandidateAuthoring.DisplayScale(planned.Plan);
            Assert.That(planned.Plan.MaxLocalExtent * scale, Is.LessThanOrEqualTo(ElementNextCandidateAuthoring.CellEffectHalfExtent + .0001f), effectId);
        }

        private static void AssertCellsDoNotOverlap(ElementNextCandidateCell[] cells)
        {
            for (var left = 0; left < cells.Length; left++)
            for (var right = left + 1; right < cells.Length; right++)
            {
                var a = WorldRect(cells[left]);
                var b = WorldRect(cells[right]);
                Assert.That(a.Overlaps(b), Is.False, cells[left].EffectId + " overlaps " + cells[right].EffectId);
            }
        }

        private static Rect WorldRect(ElementNextCandidateCell cell)
        {
            var local = cell.FullBounds;
            var position = cell.transform.position;
            return new Rect(local.x + position.x, local.y + position.y, local.width, local.height);
        }

        private static IEnumerable<ElementContentEntry> CandidateEntries()
        {
            return ElementFamilyCatalog.All.Where(value => Families.Contains(value.Family));
        }

        private static IEnumerable<string> ProtectedPaths()
        {
            foreach (var entry in CandidateEntries())
            {
                yield return RecipePath(entry);
                yield return RecipePath(entry) + ".meta";
                yield return ElementFamilyAuthoring.PatchRoot + "/" + entry.Id + ".semantic.patch.json";
                yield return ElementFamilyAuthoring.PatchRoot + "/" + entry.Id + ".semantic.patch.json.meta";
                yield return "Assets/VFX/Generated/" + entry.Id + "/VFX_" + entry.Id + ".prefab";
                yield return "Assets/VFX/Generated/" + entry.Id + "/VFX_" + entry.Id + ".prefab.meta";
                yield return VfxProjectRules.RelativeManifestRoot + "/" + entry.Id + ".manifest.json";
            }
            foreach (var family in Families)
            {
                yield return ElementFamilyAuthoring.PreviewPath(family);
                yield return ElementFamilyAuthoring.PreviewPath(family) + ".meta";
            }
            foreach (var id in new[] { "cap_linear_proj_3d", "cap_hitscan_beam_3d", "cap_hexflash_impact_2d", "style_orb_stylized_2d", "w15nc_scorch_decal_3d", "water_jet_beam_3d" })
            {
                yield return "Assets/VFX/Generated/" + id + "/VFX_" + id + ".prefab";
                yield return VfxProjectRules.RelativeManifestRoot + "/" + id + ".manifest.json";
            }
        }

        private static string RecipePath(ElementContentEntry entry) { return ElementNextCandidateAuthoring.RecipePath(entry); }
        private static string ManifestHash(string id) { return (string)JObject.Parse(File.ReadAllText(Absolute(ElementNextCandidateCompiler.ManifestPath(id))))["buildHash"]; }
        private static string Describe(ElementNextCandidatePlanResult result) { return string.Join(" | ", result.Report.Entries.Select(value => value.Code + " " + value.Path + " " + value.Message).ToArray()); }
        private static string Describe(ElementNextCandidateBuildResult result) { return string.Join(" | ", result.Report.Entries.Select(value => value.Code + " " + value.Path + " " + value.Message).ToArray()); }

        private static string HashAsset(string assetPath)
        {
            var absolute = Absolute(assetPath);
            if (!File.Exists(absolute)) return "missing";
            using (var stream = File.OpenRead(absolute))
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")).ToArray());
        }

        private static string Absolute(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
