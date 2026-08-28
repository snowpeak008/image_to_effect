using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Archetypes;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Validation;
using VFXComposer.W15NextCandidate;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W15NextCandidateTests
    {
        [Test]
        public void ParallelRecipes_ParseStrictlyAndBuildNeverMutatesRejectedW15Sources()
        {
            var oldGeneratedAndEvidence = W15NewArchetypeAuthoring.Definitions.SelectMany(definition => new[]
            {
                "Assets/VFX/Recipes/Patches/" + definition.Id + ".semantic.patch.json",
                "Assets/VFX/Recipes/Patches/" + definition.Id + ".semantic.patch.json.meta",
                "Assets/VFX/Generated/" + definition.Id + ".meta",
                "Assets/VFX/Generated/" + definition.Id + "/VFX_" + definition.Id + ".prefab",
                "Assets/VFX/Generated/" + definition.Id + "/VFX_" + definition.Id + ".prefab.meta",
                VfxProjectRules.RelativeManifestRoot + "/" + definition.Id + ".manifest.json"
            });
            var protectedPaths = W15NewArchetypeAuthoring.RecipePaths.SelectMany(path => new[] { path, path + ".meta" }).Concat(oldGeneratedAndEvidence).Concat(new[]
            {
                W15NewArchetypeAuthoring.PreviewScenePath,
                W15NewArchetypeAuthoring.PreviewScenePath + ".meta",
                "Packages/com.vfxcomposer.unity/Editor/Archetypes/W15NewArchetypeAuthoring.cs",
                "Packages/com.vfxcomposer.unity/Runtime/Components/NewArchetypePreviewDriver.cs",
                "Packages/com.vfxcomposer.unity/Tests/EditMode/W15NewArchetypeTests.cs",
                "Packages/com.vfxcomposer.unity/Tests/PlayMode/W15NewArchetypeRuntimeTests.cs"
            }).ToArray();
            var before = protectedPaths.ToDictionary(path => path, HashAsset, StringComparer.Ordinal);
            W15NextCandidateAuthoring.BuildAll();
            foreach (var pair in before) Assert.That(HashAsset(pair.Key), Is.EqualTo(pair.Value), "Rejected W15 source/evidence candidate changed: " + pair.Key);

            var catalog = VfxCompiler.LoadFormalCatalog();
            Assert.That(W15NextCandidateAuthoring.Definitions.Length, Is.EqualTo(10));
            CollectionAssert.AreEquivalent(new[] { "decal", "weapon_trail", "destruction", "lifecycle", "portal", "loot" }, W15NextCandidateAuthoring.Definitions.Select(value => value.Archetype).Distinct().ToArray());
            foreach (var definition in W15NextCandidateAuthoring.Definitions)
            {
                Assert.That(definition.Id, Does.StartWith("w15nc_"));
                Assert.That(definition.Id, Is.Not.EqualTo(definition.OriginalId));
                var path = W15NextCandidateAuthoring.RecipeRoot + "/" + definition.Id + ".default.json";
                var json = File.ReadAllText(Absolute(path));
                var parsed = VfxDomainParser.ParseRecipe(json);
                Assert.That(parsed.Report.HasErrors, Is.False, path);
                Assert.That(RecipeValidator.Validate(json, catalog).HasErrors, Is.False, path);
                Assert.That(parsed.Value.ArchetypeParameters.Count, Is.EqualTo(3), path);
                Assert.That((string)JObject.Parse(json).SelectToken("metadata.createdBy"), Is.EqualTo("w15-next-candidate-authoring"));
            }
        }

        [Test]
        public void DedicatedCompiler_IsIdempotentAndBuildsConcreteCarrierBudgets()
        {
            W15NextCandidateAuthoring.BuildAll();
            var first = W15NextCandidateAuthoring.Definitions.Select(PathInfo).ToArray();
            W15NextCandidateAuthoring.BuildAll();
            CollectionAssert.AreEqual(first, W15NextCandidateAuthoring.Definitions.Select(PathInfo).ToArray());

            foreach (var definition in W15NextCandidateAuthoring.Definitions)
            {
                var path = PrefabPath(definition.Id);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                var controller = prefab.GetComponent<W15NextCandidateController>();
                Assert.That(controller, Is.Not.Null, definition.Id);
                Assert.That(controller.Archetype, Is.EqualTo(definition.RuntimeArchetype));
                Assert.That(prefab.GetComponents<MonoBehaviour>().Count(value => value is IVfxRuntimeEntry), Is.EqualTo(1));
                Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(prefab.GetComponentInChildren<VFXComposer.W15NextCandidate.NewArchetypePreviewDriver>(true), Is.Null);
                AssertBudget(prefab, definition.RuntimeArchetype);
                AssertCarrier(prefab, definition.RuntimeArchetype);

                var manifest = JObject.Parse(File.ReadAllText(VfxProjectRules.ManifestAbsolutePath(definition.Id)));
                Assert.That((string)manifest["enforcement"], Is.EqualTo("strict"));
                Assert.That((string)manifest["compilerVersion"], Is.EqualTo(W15NextCandidateAuthoring.CompilerVersion));
                Assert.That((string)manifest.SelectToken("runtimeEntry.path"), Is.EqualTo(path));
                Assert.That((int)manifest.SelectToken("cost.localTextureBytes"), Is.EqualTo(0));
                Assert.That(((JArray)manifest["ownedOutputs"]).Count, Is.EqualTo(1));
                Assert.That(((JArray)manifest["dependencies"]).Any(value => ((string)value["path"]).StartsWith(W15NextCandidateAuthoring.SharedRoot + "/", StringComparison.Ordinal)), Is.True);
            }
        }

        [Test]
        public void RuntimePrefabs_ArePoolIdleAndExposeBothStopPathsWithoutPreviewHelpers()
        {
            W15NextCandidateAuthoring.BuildAll();
            foreach (var definition in W15NextCandidateAuthoring.Definitions)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(definition.Id));
                Assert.That(prefab.GetComponentsInChildren<Renderer>(true).All(value => !value.enabled), Is.True, definition.Id);
                Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true).All(value => !value.isPlaying && value.particleCount == 0), Is.True, definition.Id);
                var source = File.ReadAllText(Absolute("Packages/com.vfxcomposer.unity/Runtime/W15/W15NextCandidateController.cs"));
                Assert.That(source, Does.Contain("VfxStopMode.Immediate"));
                Assert.That(source, Does.Contain("VfxStopMode.AllowTail"));
            }
        }

        private static void AssertCarrier(GameObject prefab, W15NextArchetype archetype)
        {
            if (archetype == W15NextArchetype.Decal)
            {
                Assert.That(prefab.transform.Find("SurfaceBody"), Is.Not.Null);
                Assert.That(prefab.transform.Find("DirectionalEdgeCracks"), Is.Not.Null);
                Assert.That(prefab.transform.Find("SurfaceResidue"), Is.Not.Null);
                Assert.That(prefab.transform.Find("DirectionalEdgeCracks").localPosition.z, Is.GreaterThan(prefab.transform.Find("SurfaceBody").localPosition.z));
            }
            else if (archetype == W15NextArchetype.WeaponTrail)
            {
                Assert.That(prefab.transform.Find("SweptBladeRibbon").GetComponent<MeshFilter>(), Is.Not.Null);
                Assert.That(prefab.transform.Find("LiveBladeRootTip").GetComponent<LineRenderer>(), Is.Not.Null);
            }
            else if (archetype == W15NextArchetype.Destruction)
            {
                Assert.That(prefab.transform.Find("IndependentFragmentField").GetComponent<MeshFilter>(), Is.Not.Null);
                Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true).Single(), Is.Not.Null);
                Assert.That(prefab.transform.Cast<Transform>().Any(value => value.name.StartsWith("Intact", StringComparison.Ordinal)), Is.True);
            }
            else if (archetype == W15NextArchetype.LifeCycle)
            {
                Assert.That(prefab.transform.Find("BoundBodyDissolveEdge"), Is.Not.Null);
                Assert.That(prefab.transform.Cast<Transform>().Any(value => value.name == "BodyAshMotes" || value.name == "BodyAssemblyMotes"), Is.True);
            }
            else if (archetype == W15NextArchetype.Portal)
            {
                Assert.That(prefab.transform.Find("EntryIntakeFunnel"), Is.Not.Null);
                Assert.That(prefab.transform.Find("ExitEjectionBurst"), Is.Not.Null);
                Assert.That(prefab.transform.Find("DirectionalPortalFlow").GetComponent<LineRenderer>(), Is.Not.Null);
            }
            else
            {
                Assert.That(prefab.transform.Find("WorldLootToken"), Is.Not.Null);
                Assert.That(prefab.transform.Find("RarityBeam"), Is.Not.Null);
                Assert.That(prefab.transform.Find("RarityGeometryAndLayers").GetComponent<MeshFilter>(), Is.Not.Null);
                Assert.That(prefab.transform.Find("RarityCadenceSparkles").GetComponent<ParticleSystem>(), Is.Not.Null);
                Assert.That(prefab.transform.Find("CurvedPickupArc").GetComponent<LineRenderer>(), Is.Not.Null);
            }
        }

        private static void AssertBudget(GameObject prefab, W15NextArchetype archetype)
        {
            var rendererLimit = archetype == W15NextArchetype.Decal || archetype == W15NextArchetype.WeaponTrail || archetype == W15NextArchetype.Destruction ? 3 : archetype == W15NextArchetype.LifeCycle ? 2 : 5;
            var particleLimit = archetype == W15NextArchetype.Destruction ? 56 : archetype == W15NextArchetype.Portal ? 0 : 24;
            Assert.That(prefab.GetComponentsInChildren<Renderer>(true).Length, Is.LessThanOrEqualTo(rendererLimit), archetype + " renderer budget");
            Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true).Sum(value => value.main.maxParticles), Is.LessThanOrEqualTo(particleLimit), archetype + " particle budget");
            Assert.That(prefab.GetComponentsInChildren<Transform>(true).Length, Is.LessThanOrEqualTo(archetype == W15NextArchetype.Destruction || archetype == W15NextArchetype.Portal ? 16 : 10));
        }

        private static string PathInfo(W15NextCandidateAuthoring.Definition definition)
        {
            var prefab = PrefabPath(definition.Id);
            var manifest = JObject.Parse(File.ReadAllText(VfxProjectRules.ManifestAbsolutePath(definition.Id)));
            return AssetDatabase.AssetPathToGUID(prefab) + "|" + (string)manifest["buildHash"];
        }

        private static string PrefabPath(string id) { return W15NextCandidateAuthoring.OutputRoot + "/" + id + "/VFX_" + id + ".prefab"; }
        private static string HashAsset(string path)
        {
            var absolute = Absolute(path); if (!File.Exists(absolute)) return "missing";
            using (var stream = File.OpenRead(absolute)) using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
        }
        private static string Absolute(string assetPath) { return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath.Replace('/', Path.DirectorySeparatorChar))); }
    }
}
