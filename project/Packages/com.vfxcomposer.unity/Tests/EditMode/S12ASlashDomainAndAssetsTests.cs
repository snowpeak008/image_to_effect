using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Catalog;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.SlashV2;

namespace VFXComposer.Tests.EditMode
{
    public sealed class S12ASlashDomainAndAssetsTests
    {
        private const string Recipe = "Assets/VFX/Recipes/Slash/slash-3d-stylized.default.v2.json";
        private const string ManifestRoot = "Assets/VFX/Templates/3D/SlashManifests";
        private const string Evidence = "docs/stage-notes/s12a-evidence";
        private static readonly string[] Templates = { "PFT_3D_SlashAnticipation", "PFT_3D_SlashArcSweep", "PFT_3D_SlashAfterimage", "PFT_3D_SlashSparks", "PFT_3D_SlashDissipation" };

        [Test]
        public void S12V2_ClosedContractDispatchValidatorAndBudgetRejectBadInputsWithoutThrowing()
        {
            var json = Text(Recipe); var catalog = Catalog(); var dispatch = S12RecipeDispatcher.Parse(json); Assert.That(dispatch.Report.HasErrors, Is.False); Assert.That(dispatch.SlashV2, Is.Not.Null); Assert.That(S12SlashV2Validator.Validate(json, catalog).HasErrors, Is.False); Assert.That(S12SlashBudgetCalculator.Evaluate(dispatch.SlashV2, catalog).HasErrors, Is.False);
            foreach (var phase in dispatch.SlashV2.Phases) foreach (var module in phase.Modules)
            {
                S12SlashManifest manifest; Assert.That(catalog.TryGet(module.TemplateId, out manifest), Is.True, module.TemplateId);
                foreach (var parameter in manifest.Parameters) Assert.That(module.Parameters.ContainsKey(parameter.Key), Is.True, module.Id + "/" + parameter.Key);
                foreach (var parameter in module.Parameters) Assert.That(DefaultEquivalent(parameter.Value, manifest.Parameters[parameter.Key].Default), Is.True, "Canonical recipe value must equal manifest default: " + module.Id + "/" + parameter.Key);
            }
            var pcEditor = JObject.Parse(json); pcEditor["targetProfile"] = "pc_editor"; var pcJson = pcEditor.ToString(Newtonsoft.Json.Formatting.None); Assert.That(S12SlashV2Validator.Validate(pcJson, catalog).HasErrors, Is.False, "pc_editor is an actually accepted S12 target profile."); var pcDispatch = S12RecipeDispatcher.Parse(pcJson); Assert.That(S12SlashBudgetCalculator.Evaluate(pcDispatch.SlashV2, catalog).HasErrors, Is.False, "pc_editor executes the same budget gate, not only an informational code path.");
            var v1Json = Text("Assets/VFX/Recipes/fireball-3d.default.json"); var v1Dispatch = S12RecipeDispatcher.Parse(v1Json); var v1Direct = VFXComposer.Editor.Domain.VfxDomainParser.ParseRecipe(v1Json); Assert.That(v1Dispatch.RecipeVersion, Is.EqualTo(1), "v1 remains separately dispatched."); Assert.That(v1Dispatch.Report.HasErrors, Is.EqualTo(v1Direct.Report.HasErrors)); Assert.That(v1Dispatch.V1.Id, Is.EqualTo(v1Direct.Value.Id)); Assert.That(v1Dispatch.V1.Stages.Count, Is.EqualTo(v1Direct.Value.Stages.Count));
            Assert.That(S12SlashV2Validator.Validate(json.Replace("\"timeline\"", "\"unexpected\": 1, \"timeline\""), catalog).HasErrors, Is.True, "Unknown field is rejected.");
            Assert.That(S12SlashV2Validator.Validate(json.Replace("\"archetype\": \"slash\"", "\"archetype\": \"projectile\""), catalog).HasErrors, Is.True);
            Assert.That(S12SlashV2Validator.Validate(json.Replace("\"id\": \"arc_sweep\"", "\"id\": \"../bad\""), catalog).HasErrors, Is.True, "Traversal-like module ID is rejected.");
            Assert.That(S12SlashV2Validator.Validate(json.Replace("\"startTime\": 0.12", "\"startTime\": 0.23"), catalog).HasErrors, Is.True, "Time story overlap is frozen.");
            Assert.That(S12SlashV2Validator.Validate(json.Replace("\"duration\": 0.25", "\"duration\": 0.26"), catalog).HasErrors, Is.True, "Final phase must end at timeline duration.");
            ValidationReport duplicate = null; Assert.DoesNotThrow(() => duplicate = S12SlashV2Validator.Validate(json.Replace("\"id\": \"sparks\"", "\"id\": \"afterimage\""), catalog), "Duplicate phase IDs must report, never throw."); Assert.That(duplicate.HasErrors, Is.True); Assert.That(duplicate.Contains("E1216", "/phases/afterimage/id"), Is.True);
            Assert.That(S12SlashTemplateCatalog.Load(Absolute(ManifestRoot), null).Report.HasErrors, Is.True, "Resolver bypass cannot index manifests.");
        }

        [Test]
        public void S12ManifestContract_RejectsMalformedInputsAndNeverIndexesThem()
        {
            var source = JObject.Parse(Text(ManifestRoot + "/PFT_3D_SlashArcSweep.slash.manifest.json"));
            AssertReject(Mutate(source, value => value["unexpected"] = true)); AssertReject(Mutate(source, value => value["slashManifestVersion"] = 3)); AssertReject(Mutate(source, value => value["phaseKind"] = "sparks")); AssertReject(Mutate(source, value => value["assetPath"] = "Assets/VFX/Templates/3D/Slash/Prefabs/../escape.prefab")); AssertReject(Mutate(source, value => value["cost"]["estimatedPeakParticles"] = -1)); AssertReject(Mutate(source, value => value["materialGuids"] = new JArray((string)value["materialGuids"][0], (string)value["materialGuids"][0]))); AssertReject(Mutate(source, value => value["materialGuids"] = new JArray("00000000000000000000000000000000", (string)value["materialGuids"][1]))); AssertReject(Mutate(source, value => value["parameters"]["width"]["binding"] = "3d.slash.sparks.count")); AssertReject(Mutate(source, value => ((JObject)value["parameters"]).Property("width").Remove())); AssertReject(Mutate(source, value => value["parameters"]["extra"] = value["parameters"]["width"].DeepClone())); AssertReject(Mutate(source, value => value["parameters"]["width"]["min"] = .5)); AssertReject(Mutate(source, value => value["parameters"]["width"]["max"] = new JValue(double.NaN)));
            AssertReject(source, new FixedResolver { Resolution = new AssetReferenceResolution { Found = false } }); AssertReject(source, new FixedResolver { Resolution = new AssetReferenceResolution { Found = true, AssetPath = "Assets/VFX/Templates/3D/Slash/Prefabs/other.prefab" } }); AssertReject(source, new FixedResolver { Throw = true });
        }

        [Test]
        public void S12FormalTemplates_HaveActualPrefabCostsStableGuidsAndExplicitBindings()
        {
            var catalog = Catalog(); var allMaterials = new HashSet<string>(StringComparer.Ordinal); var totalRenderers = 0; var totalParticles = 0;
            foreach (var id in Templates)
            {
                S12SlashManifest manifest; Assert.That(catalog.TryGet(id, out manifest), Is.True, id); Assert.That(AssetDatabase.GUIDToAssetPath(manifest.AssetGuid), Is.EqualTo(manifest.AssetPath)); var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(manifest.AssetPath); Assert.That(prefab, Is.Not.Null);
                var renderers = prefab.GetComponentsInChildren<Renderer>(true).Where(renderer => renderer.sharedMaterials.Any(material => material != null && material.renderQueue >= 3000)).ToArray(); var particles = prefab.GetComponentsInChildren<ParticleSystem>(true); Assert.That(renderers.Length, Is.EqualTo(manifest.Cost.TransparentRenderers), id + " Renderer cost must equal actual prefab renderers."); Assert.That(particles.Length, Is.EqualTo(manifest.Cost.ParticleSystems), id + " ParticleSystem cost must equal actual prefab systems."); Assert.That(particles.Sum(particle => particle.main.maxParticles), Is.EqualTo(manifest.Cost.EstimatedPeakParticles), id + " peak particle cost must equal actual maxParticles envelope.");
                var materialPaths = renderers.SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null).Select(AssetDatabase.GetAssetPath).Distinct(StringComparer.Ordinal).ToArray(); var materialGuids = materialPaths.Select(AssetDatabase.AssetPathToGUID).ToArray(); Assert.That(materialPaths.Length, Is.EqualTo(manifest.Cost.Materials), id + " material cost must be real, not a slot placeholder."); CollectionAssert.AreEquivalent(manifest.MaterialGuids, materialGuids, id + " Manifest must name the exact actual material GUID set."); foreach (var material in materialPaths) allMaterials.Add(material); totalRenderers += renderers.Length; totalParticles += particles.Length;
                Assert.That(prefab.GetComponentsInChildren<TrailRenderer>(true), Is.Empty); Assert.That(prefab.GetComponentsInChildren<Light>(true), Is.Empty); Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty); Assert.That(particles.All(particle => !particle.subEmitters.enabled)); Assert.That(renderers.SelectMany(renderer => renderer.sharedMaterials).All(material => material != null && material.shader != null && material.shader.name.Contains("Universal Render Pipeline") && material.renderQueue >= 3000), Is.True);
                foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true)) ValidateMeshTopology(manifest.AssetPath, filter.sharedMesh);
                foreach (var texture in AssetDatabase.GetDependencies(manifest.AssetPath, true).Select(AssetDatabase.LoadAssetAtPath<Texture2D>).Where(texture => texture != null)) Assert.That(Mathf.Max(texture.width, texture.height), Is.LessThanOrEqualTo(1024));
                Assert.That(AssetDatabase.GetDependencies(manifest.AssetPath, true).Where(path => path.StartsWith("Assets/", StringComparison.Ordinal)).All(path => path.StartsWith("Assets/VFX/Templates/3D/Slash/", StringComparison.Ordinal)), Is.True, id + " dependencies stay inside formal slash inputs.");
            }
            Assert.That(totalRenderers, Is.LessThanOrEqualTo(7)); Assert.That(totalParticles, Is.LessThanOrEqualTo(4)); Assert.That(allMaterials.Count, Is.LessThanOrEqualTo(5));
        }

        [Test]
        public void S12Bindings_ApplyAndReadBackEveryManifestMinDefaultMaxIncludingVisibleDurationRunner()
        {
            var catalog = Catalog(); var registry = S12SlashBindingRegistry.CreateFormal();
            foreach (var manifest in catalog.ByTemplateId.Values) foreach (var declaration in manifest.Parameters) foreach (var value in new[] { declaration.Value.Min, declaration.Value.Default, declaration.Value.Max })
            {
                var instance = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(manifest.AssetPath)) as GameObject; try { registry.Apply(declaration.Value.Binding, instance, value); Readback(manifest.TemplateId, declaration.Key, instance, value); } finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
            var arc = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(catalog.ByTemplateId["PFT_3D_SlashArcSweep"].AssetPath)) as GameObject; try { var runner = arc.transform.Find("PrimarySweepRunner").GetComponent<ParticleSystem>(); registry.Apply("3d.slash.arc.duration", arc, new JValue(.12f)); runner.Emit(1); runner.Simulate(.15f, true, false, true); Assert.That(runner.particleCount, Is.EqualTo(0), "Visible runner expires at min duration."); runner.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); registry.Apply("3d.slash.arc.duration", arc, new JValue(.22f)); runner.Emit(1); runner.Simulate(.15f, true, false, true); Assert.That(runner.particleCount, Is.GreaterThan(0), "Visible runner survives at max duration."); } finally { UnityEngine.Object.DestroyImmediate(arc); }
        }

        [Test]
        public void S12GoldEvidence_HasTrueSideSixtyFovLiveParticleClustersAndCompletionClear()
        {
            var root = Absolute(Evidence); var metadata = JObject.Parse(File.ReadAllText(Path.Combine(root, "metadata.json"))); var views = (JArray)metadata["views"]; Assert.That(views.Count, Is.EqualTo(5)); Assert.That(views.Select(view => (string)view["sha256"]).Distinct().Count(), Is.EqualTo(5)); CollectionAssert.AreEquivalent(new[] { "dark", "neutral", "bright" }, views.Select(view => (string)view["background"]).Distinct().ToArray()); foreach (var view in views) { Assert.That((float)view["fov"], Is.InRange(55f, 65f)); Assert.That(WarmPixels(Path.Combine(root, (string)view["file"])), Is.GreaterThan(40), "Bloom-off visual must remain readable on each recorded background."); } var side = views.Single(view => (string)view["file"] == "side.png"); var position = (JArray)side["position"]; var target = (JArray)side["target"]; var x = Mathf.Abs((float)position[0] - (float)target[0]); var z = Mathf.Abs((float)position[2] - (float)target[2]); Assert.That(x, Is.GreaterThan(4f)); Assert.That(z, Is.LessThanOrEqualTo(x * .15f));
            var frames = (JArray)metadata["timelineFrames"]; Assert.That(frames.Select(frame => (string)frame["sha256"]).Distinct().Count(), Is.EqualTo(5)); var primaryWarm = WarmPixels(Path.Combine(root, "time_primary.png")); var afterWarm = WarmPixels(Path.Combine(root, "time_afterimage.png")); var dissWarm = WarmPixels(Path.Combine(root, "time_dissipation.png")); Assert.That(primaryWarm, Is.GreaterThan(afterWarm), "Primary arc must dominate subordinate afterimage."); Assert.That(afterWarm, Is.GreaterThan(dissWarm), "Afterimage must outlast and dominate sparse dissipation."); Assert.That(dissWarm, Is.GreaterThan(0)); var samples = (JArray)metadata["particleSamples"]; var spark = samples.Single(sample => (string)sample["phase"] == "sparks"); var dissipation = samples.Single(sample => (string)sample["phase"] == "dissipation"); Assert.That((int)spark["particleCount"], Is.GreaterThanOrEqualTo(8)); Assert.That((int)spark["distinctPositions"], Is.GreaterThanOrEqualTo(5)); Assert.That((float)((JArray)spark["localBoundsSize"])[0], Is.GreaterThan(1.4f)); Assert.That((int)dissipation["particleCount"], Is.InRange(3, 6)); Assert.That((int)dissipation["distinctPositions"], Is.InRange(3, 6)); Assert.That(Clusters(Path.Combine(root, "time_afterimage.png")), Is.GreaterThanOrEqualTo(5), "Afterimage frame needs five visibly separated warm ParticleSystem sparks."); Assert.That(Clusters(Path.Combine(root, "time_dissipation.png")), Is.InRange(3, 6), "Dissipation must show three to six sparse motes."); Assert.That(WarmPixels(Path.Combine(root, "time_complete.png")), Is.EqualTo(0), "0.451 s completion must have no warm VFX residue.");
        }

        [Test]
        public void S12V1Protection_FrozenRecipesGuidsGeneratedManifestsAndDirectorySetRemainExact()
        {
            Assert.That(Sha256File(Absolute("Assets/VFX/Recipes/fireball-2d.default.json")), Is.EqualTo("53C308EBD4C5DCED06A65618A71ECAB27955F160A174EB7CB91CDB4CBBEEDB88")); Assert.That(Sha256File(Absolute("Assets/VFX/Recipes/fireball-3d.default.json")), Is.EqualTo("1311E824313C3043EC6F75B4A086BB8A7D96FCE9408117D929D3C82E25B60AF2")); Assert.That(AssetDatabase.AssetPathToGUID("Assets/VFX/Generated/fireball_2d/VFX_Fireball_2D.prefab"), Is.EqualTo("edfdb8327c7bd234c94f0f4338c35816")); Assert.That(AssetDatabase.AssetPathToGUID("Assets/VFX/Generated/fireball_3d/VFX_Fireball_3D.prefab"), Is.EqualTo("27d60143a7650dd4fb850abed3ca178b"));
            var generated = Absolute("Assets/VFX/Generated"); CollectionAssert.IsSubsetOf(new[] { "fireball_2d", "fireball_3d", "slash_3d_stylized", "frost_impact_2d" }, Directory.GetDirectories(generated).Select(Path.GetFileName).ToArray(), "Protected formal outputs must remain present while later approved batches may add new outputs."); Assert.That(AssetDatabase.AssetPathToGUID("Assets/VFX/Generated/slash_3d_stylized/VFX_Slash_3D_Stylized.prefab"), Is.EqualTo("0dc223c2ffbe2c14aa24f424440f1cd2")); Assert.That(File.Exists(Path.Combine(generated, "slash_3d_stylized", "BuildManifest.json")), Is.True); Assert.That(Directory.GetFiles(Path.Combine(generated, "fireball_2d"), "*", SearchOption.AllDirectories).Length, Is.EqualTo(18)); Assert.That(Directory.GetFiles(Path.Combine(generated, "fireball_3d"), "*", SearchOption.AllDirectories).Length, Is.EqualTo(20)); Assert.That(DirectoryManifestHash(Path.Combine(generated, "fireball_2d")), Is.EqualTo("B86A5932C8CC20644E0A7B2FB6FB2F2C51B4EB2BF6842F867FC0A8AD31EC1240")); Assert.That(DirectoryManifestHash(Path.Combine(generated, "fireball_3d")), Is.EqualTo("4B8FC85CCF7E8EF9D2489E3706EF1413238D231FFB91831E754E0D886AA20FCD"));
        }

        private static S12SlashTemplateCatalog Catalog() { var catalog = S12SlashTemplateCatalog.Load(Absolute(ManifestRoot), new UnityAssetReferenceResolver()); Assert.That(catalog.Report.HasErrors, Is.False, Describe(catalog.Report)); return catalog; }
        private static JObject Mutate(JObject source, Action<JObject> mutation) { var clone = (JObject)source.DeepClone(); mutation(clone); return clone; }
        private static void AssertReject(JObject json, IAssetReferenceResolver resolver = null)
        {
            var directory = Path.Combine(Path.GetTempPath(), "vfxcomposer_s12a_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); try { File.WriteAllText(Path.Combine(directory, "bad.slash.manifest.json"), json.ToString()); var catalog = S12SlashTemplateCatalog.Load(directory, resolver ?? new UnityAssetReferenceResolver()); Assert.That(catalog.Report.HasErrors, Is.True); Assert.That(catalog.ByTemplateId, Is.Empty); } finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
        private sealed class FixedResolver : IAssetReferenceResolver { public AssetReferenceResolution Resolution; public bool Throw; public AssetReferenceResolution Resolve(string assetGuid) { if (Throw) throw new InvalidOperationException("expected resolver failure"); return Resolution; } }
        private static void ValidateMeshTopology(string assetPath, Mesh mesh)
        {
            Assert.That(mesh, Is.Not.Null, assetPath + " has a missing MeshFilter mesh."); Assert.That(mesh.bounds.size.sqrMagnitude, Is.GreaterThan(.0001f), assetPath + "/" + mesh.name + " bounds must be substantive."); var vertices = mesh.vertices; var triangles = mesh.triangles; var relativeThreshold = Mathf.Max(0.000000000001f, mesh.bounds.size.sqrMagnitude * mesh.bounds.size.sqrMagnitude * .000000001f);
            foreach (var vertex in vertices) Assert.That(float.IsNaN(vertex.x) || float.IsInfinity(vertex.x) || float.IsNaN(vertex.y) || float.IsInfinity(vertex.y) || float.IsNaN(vertex.z) || float.IsInfinity(vertex.z), Is.False, assetPath + "/" + mesh.name + " contains a non-finite vertex.");
            for (var index = 0; index < triangles.Length; index += 3)
            {
                var a = triangles[index]; var b = triangles[index + 1]; var c = triangles[index + 2]; Assert.That(a, Is.Not.EqualTo(b), assetPath + "/" + mesh.name + " triangle " + (index / 3) + " repeats index a/b."); Assert.That(a, Is.Not.EqualTo(c), assetPath + "/" + mesh.name + " triangle " + (index / 3) + " repeats index a/c."); Assert.That(b, Is.Not.EqualTo(c), assetPath + "/" + mesh.name + " triangle " + (index / 3) + " repeats index b/c."); var crossSq = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).sqrMagnitude; Assert.That(crossSq, Is.GreaterThan(relativeThreshold), assetPath + "/" + mesh.name + " triangle " + (index / 3) + " is degenerate: crossSq=" + crossSq + ", threshold=" + relativeThreshold + ", indices=" + a + "," + b + "," + c + ".");
            }
        }
        private static bool DefaultEquivalent(JToken actual, JToken expected)
        {
            if (actual == null || expected == null) return actual == expected;
            if ((actual.Type == JTokenType.Integer || actual.Type == JTokenType.Float) && (expected.Type == JTokenType.Integer || expected.Type == JTokenType.Float)) return Math.Abs(actual.Value<double>() - expected.Value<double>()) < .000001;
            return JToken.DeepEquals(actual, expected);
        }
        private static string DirectoryManifestHash(string directory)
        {
            // Reproduce the Gate F PowerShell Sort-Object manifest algorithm: invariant-culture ordering, '/' relative paths, upper-case byte SHA, LF separator, and no trailing LF.
            directory = Path.GetFullPath(directory); var lines = Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Select(Path.GetFullPath).OrderBy(path => path, StringComparer.InvariantCulture).Select(path => path.Substring(directory.Length).Replace('\\', '/') + " " + Sha256File(path)).ToArray(); return Sha256Text(string.Join("\n", lines));
        }
        private static string Sha256File(string path) { using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(File.ReadAllBytes(path)).Select(valueByte => valueByte.ToString("X2"))); }
        private static string Sha256Text(string value) { using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(valueByte => valueByte.ToString("X2"))); }
        private static void Readback(string template, string parameter, GameObject target, JToken value)
        {
            var number = value.Value<float>(); if (template == "PFT_3D_SlashArcSweep") { if (parameter == "scale") Assert.That(target.transform.localScale.x, Is.EqualTo(number).Within(.0001f)); else if (parameter == "width") Assert.That(target.transform.Find("RibbonWidthControl").localScale.x, Is.EqualTo(number / .24f).Within(.0001f)); else Assert.That(target.transform.Find("PrimarySweepRunner").GetComponent<ParticleSystem>().main.startLifetime.constant, Is.EqualTo(number).Within(.0001f)); return; }
            if (template == "PFT_3D_SlashAfterimage") { if (parameter == "count") Assert.That(target.transform.Find("EchoB").gameObject.activeSelf, Is.EqualTo(number >= 2)); else { var block = new MaterialPropertyBlock(); target.GetComponentInChildren<Renderer>().GetPropertyBlock(block); Assert.That(block.GetColor("_BaseColor").a, Is.EqualTo(number).Within(.0001f)); } return; }
            var particle = target.GetComponent<ParticleSystem>(); if (parameter == "count") { var bursts = new ParticleSystem.Burst[particle.emission.burstCount]; particle.emission.GetBursts(bursts); Assert.That(bursts[0].count.constant, Is.EqualTo(number).Within(.0001f)); } else if (parameter == "speed") Assert.That(particle.main.startSpeed.constant, Is.EqualTo(number).Within(.0001f)); else Assert.That(particle.main.startLifetime.constant, Is.EqualTo(number).Within(.0001f));
        }
        private static int WarmPixels(string path) { var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false); try { Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True); return texture.GetPixels32().Count(color => color.r > 100 && color.r > color.g + 20 && color.r > color.b + 10); } finally { UnityEngine.Object.DestroyImmediate(texture); } }
        private static int Clusters(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false); try { texture.LoadImage(File.ReadAllBytes(path)); var pixels = texture.GetPixels32(); var width = texture.width; var height = texture.height; var used = new bool[pixels.Length]; var clusters = 0; for (var i = 0; i < pixels.Length; i++) { if (used[i] || !Warm(pixels[i])) continue; var count = 0; var queue = new Queue<int>(); queue.Enqueue(i); used[i] = true; while (queue.Count > 0) { var current = queue.Dequeue(); count++; var x = current % width; var y = current / width; foreach (var next in new[] { current - 1, current + 1, current - width, current + width }) if (next >= 0 && next < pixels.Length && !used[next] && ((next / width == y) || (next % width == x)) && Warm(pixels[next])) { used[next] = true; queue.Enqueue(next); } } if (count >= 6 && count <= 300) clusters++; } return clusters; } finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
        private static bool Warm(Color32 color) { return color.r > 100 && color.r > color.g + 20 && color.r > color.b + 10; }
        private static string Absolute(string assetOrDocPath) { return assetOrDocPath.StartsWith("Assets/", StringComparison.Ordinal) ? Path.Combine(Application.dataPath, assetOrDocPath.Substring("Assets/".Length)) : Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", assetOrDocPath)); }
        private static string Text(string assetPath) { return File.ReadAllText(Absolute(assetPath)); }
        private static string Describe(VFXComposer.Editor.Domain.ValidationReport report) { return string.Join(" | ", report.Entries.Select(entry => entry.Code + " " + entry.Path)); }
    }
}
