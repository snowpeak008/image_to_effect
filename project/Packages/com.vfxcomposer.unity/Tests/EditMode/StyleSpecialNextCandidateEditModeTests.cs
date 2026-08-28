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
    public sealed class StyleSpecialNextCandidateEditModeTests
    {
        [Test]
        public void FrozenDescriptors_AreExactDistinctAndCarrySixW16NewVariantPairs()
        {
            var specs = StyleSpecialNextCandidateAuthoring.LoadFrozenSpecs();
            Assert.That(specs.Length, Is.EqualTo(32));
            Assert.That(specs.Count(value => value.Group == StyleSpecialCandidateGroup.W9Style2D), Is.EqualTo(10));
            Assert.That(specs.Count(value => value.Group == StyleSpecialCandidateGroup.W10Style3D), Is.EqualTo(10));
            Assert.That(specs.Count(value => value.Group == StyleSpecialCandidateGroup.W16StylePack2), Is.EqualTo(12));
            Assert.That(specs.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(32));
            Assert.That(specs.Select(value => value.SemanticCode).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(32), "Observable semantics may not be duplicated labels.");
            CollectionAssert.AreEquivalent(new[] { "pixel", "cartoon", "inkwash" }, specs.Where(value => value.Group == StyleSpecialCandidateGroup.W9Style2D).Select(value => value.StyleToken).Distinct());
            CollectionAssert.AreEquivalent(new[] { "semireal", "holo", "dark" }, specs.Where(value => value.Group == StyleSpecialCandidateGroup.W10Style3D).Select(value => value.StyleToken).Distinct());
            CollectionAssert.AreEquivalent(new[] { "lowpoly", "crystal", "candy", "cosmic", "steampunk", "ghost" }, specs.Where(value => value.Group == StyleSpecialCandidateGroup.W16StylePack2).Select(value => value.StyleToken).Distinct());
            var pairs = specs.Where(value => value.Group == StyleSpecialCandidateGroup.W16StylePack2).GroupBy(value => value.PairFamily, StringComparer.Ordinal).ToArray();
            Assert.That(pairs.Length, Is.EqualTo(6));
            foreach (var pair in pairs)
            {
                Assert.That(pair.Count(), Is.EqualTo(2), pair.Key);
                CollectionAssert.AreEquivalent(new[] { "new", "variant" }, pair.Select(value => value.PairRole));
                Assert.That(pair.All(value => !string.IsNullOrEmpty(value.SourceBaseId)), Is.True, pair.Key);
            }
        }

        [Test]
        public void EveryDescriptor_ResolvesARealSharedMaterialAndThreeToSixBoundedCarriers()
        {
            var specs = StyleSpecialNextCandidateAuthoring.LoadFrozenSpecs();
            foreach (var spec in specs)
            {
                var materialPath = StyleSpecialNextCandidateAuthoring.MaterialPathFor(spec.StyleToken);
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                Assert.That(material, Is.Not.Null, spec.Id + " material");
                Assert.That(material.shader, Is.Not.Null, spec.Id + " shader");
                Assert.That(spec.MeshTokens.Length, Is.InRange(3, 6), spec.Id);
                foreach (var token in spec.MeshTokens)
                {
                    var path = StyleSpecialNextCandidateAuthoring.MeshPathFor(token);
                    if (token == "Line") Assert.That(path, Is.Empty, spec.Id);
                    else Assert.That(AssetDatabase.LoadAssetAtPath<Mesh>(path), Is.Not.Null, spec.Id + " / " + token);
                }
            }
        }

        [Test]
        public void CandidateBuild_IsStrictIdempotentBudgetedAndDoesNotRewriteRejectedOutputs()
        {
            var protectedIds = StyleSpecialCatalog.All.Select(value => value.Id).ToArray();
            var before = SnapshotProtectedOutputs(protectedIds);
            var first = StyleSpecialNextCandidateAuthoring.BuildCandidateEntries();
            Assert.That(first.Length, Is.EqualTo(32));
            Assert.That(first.All(value => value.Succeeded), Is.True, FailureText(first));
            var second = StyleSpecialNextCandidateAuthoring.BuildCandidateEntries();
            Assert.That(second.All(value => value.Succeeded && value.Unchanged), Is.True, FailureText(second));
            CollectionAssert.AreEquivalent(before, SnapshotProtectedOutputs(protectedIds), "The old rejected Prefabs/manifests are write-protected by scope.");
            foreach (var result in second)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath);
                Assert.That(prefab, Is.Not.Null, result.EffectId);
                var entry = prefab.GetComponent<StyleSpecialNextCandidateRuntimeEntry>();
                Assert.That(entry, Is.Not.Null, result.EffectId);
                Assert.That(prefab.GetComponents<MonoBehaviour>().Count(value => value is IVfxRuntimeEntry), Is.EqualTo(1), result.EffectId);
                var budget = entry.ReadBudget();
                Assert.That(budget.GameObjects, Is.LessThanOrEqualTo(StyleSpecialNextCandidateRuntimeEntry.MaxGameObjectsBudget), result.EffectId);
                Assert.That(budget.Renderers, Is.LessThanOrEqualTo(StyleSpecialNextCandidateRuntimeEntry.MaxRenderersBudget), result.EffectId);
                Assert.That(budget.ParticleSystems, Is.EqualTo(StyleSpecialNextCandidateRuntimeEntry.MaxParticleSystemsBudget), result.EffectId);
                Assert.That(budget.Materials, Is.LessThanOrEqualTo(StyleSpecialNextCandidateRuntimeEntry.MaxMaterialsBudget), result.EffectId);
                var manifest = JObject.Parse(File.ReadAllText(VfxProjectRules.ManifestAbsolutePath(result.EffectId)));
                Assert.That((string)manifest["compilerVersion"], Is.EqualTo(StyleSpecialNextCandidateAuthoring.CompilerVersion), result.EffectId);
                Assert.That((string)manifest["buildHash"], Is.EqualTo(result.BuildHash), result.EffectId);
                Assert.That((int)manifest["cost"]["localTextureBytes"], Is.EqualTo(0), result.EffectId);
            }
        }

        [Test]
        public void BuiltW10TimingAndW16CombinationContracts_AreSerializedIntoObservableRuntimeEntries()
        {
            StyleSpecialNextCandidateAuthoring.BuildCandidateEntries();
            var specs = StyleSpecialNextCandidateAuthoring.LoadFrozenSpecs();
            foreach (var spec in specs.Where(value => value.Group == StyleSpecialCandidateGroup.W10Style3D))
            {
                var entry = LoadEntry(spec.Id);
                Assert.That(entry.Duration, Is.EqualTo(spec.Duration).Within(.0001f), spec.Id);
                Assert.That(entry.ReleaseNormalized, Is.EqualTo(spec.ReleaseNormalized).Within(.0001f), spec.Id);
                Assert.That(entry.MotionProfile, Is.EqualTo(spec.MotionProfile), spec.Id);
                Assert.That(entry.VisualSignature, Does.Contain(spec.SemanticCode), spec.Id);
            }
            foreach (var family in specs.Where(value => value.Group == StyleSpecialCandidateGroup.W16StylePack2).GroupBy(value => value.PairFamily, StringComparer.Ordinal))
            {
                var entries = family.Select(value => LoadEntry(value.Id)).ToArray();
                Assert.That(entries.Length, Is.EqualTo(2));
                Assert.That(entries.Select(value => value.CombinationSignature).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(2), family.Key);
                Assert.That(entries.Select(value => value.StyleToken).Distinct(StringComparer.Ordinal).Single(), Is.EqualTo(family.Key), family.Key);
                CollectionAssert.AreEquivalent(new[] { "new", "variant" }, entries.Select(value => value.PairRole));
            }
        }

        private static StyleSpecialNextCandidateRuntimeEntry LoadEntry(string id)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/" + id + "/VFX_" + id + ".prefab");
            Assert.That(prefab, Is.Not.Null, id);
            return prefab.GetComponent<StyleSpecialNextCandidateRuntimeEntry>();
        }

        private static Dictionary<string, string> SnapshotProtectedOutputs(IEnumerable<string> ids)
        {
            var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                var prefab = Absolute("Assets/VFX/Generated/" + id + "/VFX_" + id + ".prefab");
                var manifest = VfxProjectRules.ManifestAbsolutePath(id);
                if (File.Exists(prefab)) snapshot[prefab] = Sha256(prefab);
                if (File.Exists(manifest)) snapshot[manifest] = Sha256(manifest);
            }
            return snapshot;
        }

        private static string FailureText(IEnumerable<StyleSpecialNextCandidateBuildResult> results)
        {
            return string.Join(" | ", results.Where(value => !value.Succeeded).Select(value => value.EffectId + ": " + string.Join("; ", value.Report.Entries.Select(item => item.Code + " " + item.Path + " " + item.Message))).ToArray());
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
