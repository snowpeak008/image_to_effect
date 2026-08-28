using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Style;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W1NextCandidateEditModeTests
    {
        private static readonly string[] InitialTokens = { "stylized", "cartoon", "pixel", "inkwash", "semireal", "holo", "dark", "neon" };

        [Test]
        public void FrozenDescriptors_ContainEightTokensAndThreeExactCapabilitySkinContracts()
        {
            var specs = W1NextCandidateAuthoring.LoadFrozenSpecs();
            Assert.That(specs.Length, Is.EqualTo(11));
            var tokenSpecs = specs.Where(value => value.Kind == W1NextCandidateKind.StyleToken).ToArray();
            CollectionAssert.AreEquivalent(InitialTokens, tokenSpecs.Select(value => value.StyleToken));
            Assert.That(tokenSpecs.Select(value => value.TimingProfile).Distinct().Count(), Is.EqualTo(8));
            Assert.That(specs.All(value => value.Duration == 2f), Is.True, "The comparison wall uses one playback duration.");
            Assert.That(specs.Count(value => value.Kind == W1NextCandidateKind.FanWave && value.StyleToken == "cartoon" && value.Archetype == "projectile"), Is.EqualTo(1));
            Assert.That(specs.Count(value => value.Kind == W1NextCandidateKind.ChargeOcclude && value.StyleToken == "holo" && value.Archetype == "beam"), Is.EqualTo(1));
            Assert.That(specs.Count(value => value.Kind == W1NextCandidateKind.TelegraphNova && value.Archetype == "impact"), Is.EqualTo(1));
            Assert.That(W1NextCandidateAuthoring.CandidateRecipePaths.All(value => value.StartsWith(W1NextCandidateAuthoring.RecipeRoot + "/", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void EightTokenEntries_UseDistinctRealMaterialMeshAndTimingSignaturesAtOneEnvelope()
        {
            W1NextCandidateAuthoring.BuildAllForBatch();
            var specs = W1NextCandidateAuthoring.LoadFrozenSpecs().Where(value => value.Kind == W1NextCandidateKind.StyleToken).ToArray();
            var actualSignatures = new HashSet<string>(StringComparer.Ordinal);
            foreach (var spec in specs)
            {
                var prefab = LoadPrefab(spec.Id);
                var entry = prefab.GetComponent<W1NextCandidateRuntimeEntry>();
                Assert.That(entry, Is.Not.Null, spec.Id);
                Assert.That(entry.StyleToken, Is.EqualTo(spec.StyleToken));
                Assert.That(entry.DeclaredLocalBounds.center, Is.EqualTo(W1NextCandidateRuntimeEntry.UniformLocalEnvelope.center));
                Assert.That(entry.DeclaredLocalBounds.size, Is.EqualTo(W1NextCandidateRuntimeEntry.UniformLocalEnvelope.size));
                Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
                var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(renderers.Length, Is.EqualTo(3));
                Assert.That(renderers.All(value => !value.enabled), Is.True, "Idle invisibility must be serialized.");
                var material = renderers.Select(value => value.sharedMaterial).Distinct().Single();
                var meshes = renderers.Select(value => value.GetComponent<MeshFilter>().sharedMesh.name).ToArray();
                var actual = ActualSignature(material, meshes, entry.TimingProfile);
                actualSignatures.Add(actual);
                Assert.That(entry.VisualSignature, Is.EqualTo(actual));
            }
            Assert.That(actualSignatures.Count, Is.EqualTo(8), "Token differentiation must be carried by actual shader/mesh/timing state, not labels alone.");
            Assert.That(specs.Select(value => LoadPrefab(value.Id).GetComponentsInChildren<MeshRenderer>(true).First().sharedMaterial.shader.name).Distinct().Count(), Is.GreaterThanOrEqualTo(5));
            Assert.That(specs.Select(value => LoadPrefab(value.Id).GetComponentsInChildren<MeshRenderer>(true).First().sharedMaterial.GetFloat("_StyleMode")).Distinct().Count(), Is.EqualTo(8));
            var dark = LoadPrefab("style_orb_dark_3d").GetComponent<W1NextCandidateRuntimeEntry>();
            var stylized = LoadPrefab("style_orb_stylized_2d").GetComponent<W1NextCandidateRuntimeEntry>();
            Assert.That(SerializedFloat(dark, "baseIntensity"), Is.GreaterThan(SerializedFloat(stylized, "baseIntensity")), "Dark is deliberately given a stronger visible energy floor than the old weak sample.");
        }

        [Test]
        public void CandidateBuild_IsStrictBudgetedIdempotentAndDoesNotRewriteCapabilityOrElementOutputs()
        {
            var protectedIds = new[] { "cap_linear_proj_3d", "cap_hitscan_beam_3d", "cap_telegraph_impact_3d", "flame_slash_2d" };
            var protectedBefore = SnapshotProtectedOutputs(protectedIds);
            var first = W1NextCandidateAuthoring.BuildCandidateEntries();
            Assert.That(first.All(value => value.Succeeded), Is.True, FailureText(first));
            var identity = first.ToDictionary(value => value.EffectId, value => AssetDatabase.AssetPathToGUID(value.PrefabPath) + "|" + value.BuildHash, StringComparer.Ordinal);
            var second = W1NextCandidateAuthoring.BuildCandidateEntries();
            Assert.That(second.All(value => value.Succeeded && value.Unchanged), Is.True, FailureText(second));
            CollectionAssert.AreEquivalent(identity, second.ToDictionary(value => value.EffectId, value => AssetDatabase.AssetPathToGUID(value.PrefabPath) + "|" + value.BuildHash, StringComparer.Ordinal));
            CollectionAssert.AreEquivalent(protectedBefore, SnapshotProtectedOutputs(protectedIds), "The W1-only compiler must not rebuild W-C1/W-C2/W-C3 or W3+ outputs.");

            foreach (var result in second)
            {
                var prefab = LoadPrefab(result.EffectId);
                var entry = prefab.GetComponent<W1NextCandidateRuntimeEntry>();
                Assert.That(prefab.GetComponents<MonoBehaviour>().Count(value => value is IVfxRuntimeEntry), Is.EqualTo(1));
                var budget = entry.ReadBudget();
                Assert.That(budget.GameObjects, Is.LessThanOrEqualTo(W1NextCandidateRuntimeEntry.MaxGameObjectsBudget));
                Assert.That(budget.Renderers, Is.LessThanOrEqualTo(W1NextCandidateRuntimeEntry.MaxRenderersBudget));
                Assert.That(budget.ParticleSystems, Is.EqualTo(W1NextCandidateRuntimeEntry.MaxParticleSystemsBudget));
                Assert.That(budget.Materials, Is.LessThanOrEqualTo(W1NextCandidateRuntimeEntry.MaxMaterialsBudget));
                var manifest = JObject.Parse(File.ReadAllText(VfxProjectRules.ManifestAbsolutePath(result.EffectId)));
                Assert.That((string)manifest["compilerVersion"], Is.EqualTo(W1NextCandidateAuthoring.CompilerVersion));
                Assert.That((string)manifest["enforcement"], Is.EqualTo("strict"));
                Assert.That((int)manifest["cost"]["localTextureBytes"], Is.EqualTo(0));
                Assert.That((int)manifest["cost"]["gameObjects"], Is.LessThanOrEqualTo(W1NextCandidateRuntimeEntry.MaxGameObjectsBudget));
                Assert.That(((JArray)manifest["ownedOutputs"]).Any(value => ((string)value["path"]).EndsWith(".mat", StringComparison.OrdinalIgnoreCase)), Is.False);
            }
        }

        private static GameObject LoadPrefab(string id)
        {
            var path = "Assets/VFX/Generated/" + id + "/VFX_" + id + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            return prefab;
        }

        private static float SerializedFloat(UnityEngine.Object target, string property)
        {
            return new SerializedObject(target).FindProperty(property).floatValue;
        }

        private static string ActualSignature(Material material, string[] meshes, W1StyleTimingProfile timing)
        {
            return material.shader.name +
                   "|mode=" + material.GetFloat("_StyleMode").ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                   "|outline=" + material.GetFloat("_Outline").ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                   "|steps=" + material.GetFloat("_ShadingSteps").ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                   "|noise=" + material.GetFloat("_NoiseScale").ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                   "|blend=" + material.GetFloat("_DstBlend").ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                   "|meshes=" + string.Join(",", meshes) +
                   "|timing=" + timing;
        }

        private static Dictionary<string, string> SnapshotProtectedOutputs(IEnumerable<string> ids)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                var prefab = "Assets/VFX/Generated/" + id + "/VFX_" + id + ".prefab";
                var prefabAbsolute = Absolute(prefab);
                if (File.Exists(prefabAbsolute)) values["prefab:" + id] = AssetDatabase.AssetPathToGUID(prefab) + "|" + Sha256(prefabAbsolute);
                var manifest = VfxProjectRules.ManifestAbsolutePath(id);
                if (File.Exists(manifest)) values["manifest:" + id] = Sha256(manifest);
            }
            return values;
        }

        private static string FailureText(IEnumerable<W1NextCandidateBuildResult> results)
        {
            return string.Join(" | ", results.SelectMany(result => result.Report.Entries.Select(value => result.EffectId + ":" + value.Code + " " + value.Path + " " + value.Message)).ToArray());
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
