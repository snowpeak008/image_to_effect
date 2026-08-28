using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VFXComposer;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.SlashV2;
using VFXComposer.Editor.Preview;

namespace VFXComposer.Tests.EditMode
{
    public sealed class S12BSlashCompilerTests
    {
        private const string RecipePath = "Assets/VFX/Recipes/Slash/slash-3d-stylized.default.v2.json";

        [Test]
        public void S12B_V2CompilerBuildsAuditablePrefabWithSharedGeneratedMaterialsAndIdempotence()
        {
            var text = Text(RecipePath); var compiler = new S12SlashCompiler(); var before = Snapshot(S12SlashCompiler.OutputFolderPath); var validate = compiler.Validate(text); var dry = compiler.DryRun(text); Assert.That(validate.IsBlocked, Is.False, Describe(validate)); Assert.That(dry.IsBlocked, Is.False, Describe(dry)); CollectionAssert.AreEquivalent(before, Snapshot(S12SlashCompiler.OutputFolderPath), "Validate/DryRun must not write.");
            var first = compiler.Build(text); Assert.That(first.Succeeded, Is.True, Describe(first.Plan)); var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(S12SlashCompiler.OutputPrefabPath); Assert.That(prefab, Is.Not.Null); var controller = prefab.GetComponent<SlashEffectController>(); Assert.That(controller, Is.Not.Null); CollectionAssert.AreEqual(new[] { "anticipation", "primary_arc", "afterimage", "sparks", "dissipation" }, controller.Phases.Select(phase => phase.PhaseId).ToArray()); foreach (var phase in controller.Phases) { Assert.That(phase.Root, Is.Not.Null); Assert.That(phase.Root.transform.parent, Is.EqualTo(prefab.transform)); Assert.That(phase.Root.name, Is.EqualTo(char.ToUpperInvariant(phase.PhaseId[0]) + phase.PhaseId.Substring(1))); Assert.That(phase.Root.transform.childCount, Is.EqualTo(1)); }
            var renderers = prefab.GetComponentsInChildren<Renderer>(true); var particles = prefab.GetComponentsInChildren<ParticleSystem>(true); var materials = renderers.SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null).Select(AssetDatabase.GetAssetPath).Distinct(StringComparer.Ordinal).ToArray(); Assert.That(renderers.Length, Is.LessThanOrEqualTo(7)); Assert.That(particles.Length, Is.LessThanOrEqualTo(4)); Assert.That(particles.Sum(particle => particle.main.maxParticles), Is.LessThanOrEqualTo(48)); Assert.That(materials.Length, Is.LessThanOrEqualTo(5)); Assert.That(materials.All(path => path.StartsWith(S12SlashCompiler.OutputFolderPath + "/", StringComparison.Ordinal)), Is.True); Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(component => component == null), Is.False, "Generated prefab may not serialize a missing MonoBehaviour."); var manifest = JObject.Parse(File.ReadAllText(Absolute(S12SlashCompiler.ManifestPath))); CollectionAssert.AreEquivalent(materials, ((JArray)manifest["outputMaterialPaths"]).Values<string>().ToArray(), "Build manifest material set must be read back from final Prefab."); Assert.That(prefab.GetComponentInChildren<SlashAfterimageAlpha>(true), Is.Not.Null, "Afterimage alpha must be serialized for runtime reload.");
            var guid = AssetDatabase.AssetPathToGUID(S12SlashCompiler.OutputPrefabPath); var hash = Snapshot(S12SlashCompiler.OutputFolderPath); var second = compiler.Build(text); Assert.That(second.Succeeded, Is.True); Assert.That(second.Plan.Items.Single().State, Is.EqualTo(VfxBuildItemState.Unchanged)); Assert.That(AssetDatabase.AssetPathToGUID(S12SlashCompiler.OutputPrefabPath), Is.EqualTo(guid)); CollectionAssert.AreEquivalent(hash, Snapshot(S12SlashCompiler.OutputFolderPath));
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(S12SlashGeneratedPreview.ScenePath) != null) AssetDatabase.DeleteAsset(S12SlashGeneratedPreview.ScenePath); AssetDatabase.Refresh(); S12SlashGeneratedPreview.BuildSceneForBatch(); Assert.That(File.Exists(S12SlashGeneratedPreview.ScenePath), Is.True, "Clean-path formal preview must be created additively from the generated v2 prefab."); var preview = EditorSceneManager.OpenScene(S12SlashGeneratedPreview.ScenePath, OpenSceneMode.Additive); try { var previewController = preview.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SlashEffectController>(true)).Single(); Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(previewController.gameObject)), Is.EqualTo(S12SlashCompiler.OutputPrefabPath)); Assert.That(preview.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SlashPreviewPlaybackDriver>(true)).Any(), Is.True); } finally { EditorSceneManager.CloseScene(preview, true); }
        }

        [Test]
        public void S12B_TransactionFailureRestoresNoOutputAndExistingOutputBytesGuidsAndNoResidue()
        {
            var text = Text(RecipePath); try { DeleteOutput(); var failing = new S12SlashCompiler(null, new ThrowAfterSave(), null); var initial = failing.Build(text); Assert.That(initial.Succeeded, Is.False); Assert.That(AssetDatabase.IsValidFolder(S12SlashCompiler.OutputFolderPath), Is.False); AssertResidueFree();
                var built = new S12SlashCompiler().Build(text); Assert.That(built.Succeeded, Is.True, Describe(built.Plan)); var bytes = Snapshot(S12SlashCompiler.OutputFolderPath); var prefabGuid = AssetDatabase.AssetPathToGUID(S12SlashCompiler.OutputPrefabPath); var changed = JObject.Parse(text); changed["revision"] = 2; var failedExisting = failing.Build(changed.ToString()); Assert.That(failedExisting.Succeeded, Is.False); CollectionAssert.AreEquivalent(bytes, Snapshot(S12SlashCompiler.OutputFolderPath)); Assert.That(AssetDatabase.AssetPathToGUID(S12SlashCompiler.OutputPrefabPath), Is.EqualTo(prefabGuid)); AssertResidueFree(); }
            finally { Assert.That(new S12SlashCompiler().Build(text).Succeeded, Is.True, "Finally restore canonical output after transaction injection."); }
        }

        [Test]
        public void S12B_DispatchesV1ToExistingCompilerAndRejectsCompilerVersionMismatchWithoutWrite()
        {
            var v1 = Text("Assets/VFX/Recipes/fireball-3d.default.json"); var dispatcher = new S12CompilerDispatcher(); Assert.That(dispatcher.DryRun(v1).IsBlocked, Is.False); var output = Snapshot(S12SlashCompiler.OutputFolderPath); var rejected = new S12SlashCompiler().DryRun(v1); Assert.That(rejected.IsBlocked, Is.True); CollectionAssert.AreEquivalent(output, Snapshot(S12SlashCompiler.OutputFolderPath));
        }

        [Test]
        public void S12B_DependencyHashAndNonDefaultParametersPersistAcrossGeneratedPrefabReload()
        {
            var canonical = Text(RecipePath); try
            {
                var hashes = new MutableHashes("dep-a"); var compiler = new S12SlashCompiler(null, null, hashes); Assert.That(compiler.Build(canonical).Succeeded, Is.True); var stable = compiler.DryRun(canonical); Assert.That(stable.Items.Single().State, Is.EqualTo(VfxBuildItemState.Unchanged)); hashes.Value = "dep-b"; var invalidated = compiler.DryRun(canonical); Assert.That(invalidated.Items.Single().State, Is.EqualTo(VfxBuildItemState.Update)); Assert.That(invalidated.BuildHash, Is.Not.EqualTo(stable.BuildHash), "Dependency hash alone must invalidate v2 output.");
                var custom = JObject.Parse(canonical); var phases = (JArray)custom["phases"]; ((JObject)((JArray)phases.Single(phase => (string)phase["id"] == "primary_arc")["modules"])[0])["parameters"]["duration"] = .22; ((JObject)((JArray)phases.Single(phase => (string)phase["id"] == "afterimage")["modules"])[0])["parameters"]["count"] = 1; ((JObject)((JArray)phases.Single(phase => (string)phase["id"] == "afterimage")["modules"])[0])["parameters"]["alpha"] = .45; Assert.That(new S12SlashCompiler().Build(custom.ToString()).Succeeded, Is.True);
                var loaded = PrefabUtility.LoadPrefabContents(S12SlashCompiler.OutputPrefabPath); try { var runner = loaded.transform.Find("Primary_arc/Arc_sweep/PrimarySweepRunner").GetComponent<ParticleSystem>(); Assert.That(runner.main.startLifetime.constant, Is.EqualTo(.22f).Within(.0001f)); Assert.That(loaded.transform.Find("Afterimage/Arc_afterimage/EchoB").gameObject.activeSelf, Is.False); var alpha = loaded.GetComponentInChildren<SlashAfterimageAlpha>(true); Assert.That(alpha.Alpha, Is.EqualTo(.45f).Within(.0001f)); loaded.SetActive(true); alpha.Alpha = alpha.Alpha; var block = new MaterialPropertyBlock(); alpha.GetComponentInChildren<Renderer>(true).GetPropertyBlock(block); Assert.That(block.GetColor("_BaseColor").a, Is.EqualTo(.45f).Within(.0001f), "Serialized runtime alpha must apply through MPB after reload."); } finally { PrefabUtility.UnloadPrefabContents(loaded); }
            }
            finally { Assert.That(new S12SlashCompiler().Build(canonical).Succeeded, Is.True, "Finally restore canonical formal output."); }
        }

        [Test]
        public void S12B_FrozenV1ValuesAndFinalGeneratedDirectorySetRemainExact()
        {
            Assert.That(Hash(File.ReadAllBytes(Absolute("Assets/VFX/Recipes/fireball-2d.default.json"))), Is.EqualTo("53C308EBD4C5DCED06A65618A71ECAB27955F160A174EB7CB91CDB4CBBEEDB88")); Assert.That(Hash(File.ReadAllBytes(Absolute("Assets/VFX/Recipes/fireball-3d.default.json"))), Is.EqualTo("1311E824313C3043EC6F75B4A086BB8A7D96FCE9408117D929D3C82E25B60AF2")); Assert.That(AssetDatabase.AssetPathToGUID("Assets/VFX/Generated/fireball_2d/VFX_Fireball_2D.prefab"), Is.EqualTo("edfdb8327c7bd234c94f0f4338c35816")); Assert.That(AssetDatabase.AssetPathToGUID("Assets/VFX/Generated/fireball_3d/VFX_Fireball_3D.prefab"), Is.EqualTo("27d60143a7650dd4fb850abed3ca178b")); Assert.That(GateHash("Assets/VFX/Generated/fireball_2d"), Is.EqualTo("B86A5932C8CC20644E0A7B2FB6FB2F2C51B4EB2BF6842F867FC0A8AD31EC1240")); Assert.That(GateHash("Assets/VFX/Generated/fireball_3d"), Is.EqualTo("4B8FC85CCF7E8EF9D2489E3706EF1413238D231FFB91831E754E0D886AA20FCD")); CollectionAssert.IsSubsetOf(new[] { "fireball_2d", "fireball_3d", "slash_3d_stylized", "frost_impact_2d" }, Directory.GetDirectories(Absolute(S12SlashCompiler.GeneratedRoot)).Select(Path.GetFileName).ToArray(), "Protected outputs remain exact; later approved batches may add new output folders.");
        }

        [Test]
        public void S12B_LegacySamplerEvidenceIsRejectedAsCurrentRuntimeVisualProof()
        {
            Assert.That(S12SlashGeneratedEvidence.VerifyExisting(), Is.False, "S12B used its own camera and SampleForPreview; it remains rejected as current Game/runtime visual proof."); var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "stage-notes", "s12b-evidence")); var metadata = JObject.Parse(File.ReadAllText(Path.Combine(root, "metadata.json"))); Assert.That((string)metadata["capture"], Does.Contain("internal deterministic controller sample")); var evidenceRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "stage-notes", "s15-wysiwyg-evidence")); var authority = Directory.GetDirectories(evidenceRoot, "run-*").Select(path => Path.Combine(path, "metadata.json")).Single(File.Exists); Assert.That(File.Exists(authority), Is.True, "The current serialized-MainCamera continuous capture must replace the legacy sampler as review input."); var current = JObject.Parse(File.ReadAllText(authority)); Assert.That((string)current["scene"], Is.EqualTo(S12SlashGeneratedPreview.ScenePath)); Assert.That((int)current["fps"], Is.EqualTo(60)); Assert.That(((JArray)current["frames"]).Count, Is.EqualTo(28)); Assert.That(((JArray)current["frames"])[0]["time"].Value<float>(), Is.EqualTo(0f)); Assert.That(((JArray)current["frames"])[27]["time"].Value<float>(), Is.EqualTo(.45f).Within(.0001f)); var readbacks = (JArray)current["liveParticleReadback"]; Assert.That(readbacks, Is.Not.Null, "Authority metadata must record same-frame live particle facts for audit, not infer them from pixels."); Assert.That(readbacks.Count, Is.EqualTo(28)); var anchors = (JArray)current["anchorReadback"]; Assert.That(anchors, Is.Not.Null); Assert.That(anchors.Count, Is.EqualTo(28)); Assert.That(anchors.Max(item => item["maxDistancePx"].Value<float>()), Is.EqualTo(0f).Within(.0001f), "All Slash layers must retain the corrected common origin anchor.");
        }

        private sealed class ThrowAfterSave : IS12SlashBuildHook { public void AfterPrefabAndMaterialsSaved(string outputFolder) { throw new InvalidOperationException("S12B injected failure after prefab/material write."); } }
        private sealed class MutableHashes : ITemplateDependencyHashProvider { public string Value; public MutableHashes(string value) { Value = value; } public string GetDependencyHash(string assetPath) { return Value; } }
        private static void DeleteOutput() { if (AssetDatabase.IsValidFolder(S12SlashCompiler.OutputFolderPath)) AssetDatabase.DeleteAsset(S12SlashCompiler.OutputFolderPath); AssetDatabase.Refresh(); }
        private static void AssertResidueFree() { var root = Absolute(S12SlashCompiler.GeneratedRoot); Assert.That(Directory.GetDirectories(root).Select(Path.GetFileName).Where(name => name.StartsWith("s12btmp_", StringComparison.Ordinal)).ToArray(), Is.Empty); Assert.That(Directory.GetFiles(root, "*.pending", SearchOption.AllDirectories), Is.Empty); Assert.That(Directory.GetDirectories(Path.GetTempPath(), "vfxcomposer_s12b_*").ToArray(), Is.Empty); }
        private static string[] Snapshot(string assetFolder) { var path = Absolute(assetFolder); return !Directory.Exists(path) ? new string[0] : Directory.GetFiles(path, "*", SearchOption.AllDirectories).OrderBy(item => item, StringComparer.Ordinal).Select(item => item.Substring(path.Length).Replace('\\', '/') + " " + Hash(File.ReadAllBytes(item))).ToArray(); }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("X2"))); }
        private static string GateHash(string assetPath) { var directory = Path.GetFullPath(Absolute(assetPath)); var lines = Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Select(Path.GetFullPath).OrderBy(path => path, StringComparer.InvariantCulture).Select(path => path.Substring(directory.Length).Replace('\\', '/') + " " + Hash(File.ReadAllBytes(path))); return Hash(Encoding.UTF8.GetBytes(string.Join("\n", lines))); }
        private static int WarmPixels(string path) { var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false); try { texture.LoadImage(File.ReadAllBytes(path)); return texture.GetPixels32().Count(color => color.r > 100 && color.r > color.g + 20 && color.r > color.b + 10); } finally { UnityEngine.Object.DestroyImmediate(texture); } }
        private static void AssertParticleGate(JToken frame, string name, int min, int max) { var particle = ((JArray)frame["particles"]).Single(value => (string)value["name"] == name); Assert.That((int)particle["particleCount"], Is.InRange(min, max)); Assert.That((int)particle["distinctPositions"], Is.GreaterThanOrEqualTo(min)); Assert.That((float)((JArray)particle["boundsSize"])[0], Is.GreaterThan(.2f)); }
        private static int Clusters(string path) { var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false); try { texture.LoadImage(File.ReadAllBytes(path)); var pixels = texture.GetPixels32(); var used = new bool[pixels.Length]; var count = 0; for (var index = 0; index < pixels.Length; index++) { if (used[index] || !Warm(pixels[index])) continue; var area = 0; var queue = new Queue<int>(); queue.Enqueue(index); used[index] = true; while (queue.Count > 0) { var current = queue.Dequeue(); area++; var x = current % texture.width; var y = current / texture.width; foreach (var next in new[] { current - 1, current + 1, current - texture.width, current + texture.width }) if (next >= 0 && next < pixels.Length && !used[next] && ((next / texture.width == y) || (next % texture.width == x)) && Warm(pixels[next])) { used[next] = true; queue.Enqueue(next); } } if (area >= 6 && area <= 500) count++; } return count; } finally { UnityEngine.Object.DestroyImmediate(texture); } }
        private static bool Warm(Color32 color) { return color.r > 100 && color.r > color.g + 20 && color.r > color.b + 10; }
        private static string Text(string assetPath) { return File.ReadAllText(Absolute(assetPath)); }
        private static string Absolute(string path) { return Path.Combine(Application.dataPath, path.Substring("Assets/".Length)); }
        private static string Describe(VfxBuildPlan plan) { return string.Join(" | ", plan.Report.Entries.Select(entry => entry.Code + " " + entry.Path)); }
    }
}
