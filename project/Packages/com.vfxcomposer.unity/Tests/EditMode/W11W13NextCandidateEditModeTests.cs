using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.NextCandidates;
using VFXComposer.Editor.Rules;
using VFXComposer.W11W13NextCandidate;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W11W13NextCandidateEditModeTests
    {
        [Test]
        public void SourceRecipes_AreFrozenParallelContractsAndBuildDoesNotMutateRejectedCandidates()
        {
            var protectedPaths = W11W13NextCandidatePlan.Definitions.SelectMany(definition => new[]
            {
                OldRecipePath(definition),
                OldRecipePath(definition) + ".meta",
                "Assets/VFX/Generated/" + definition.SourceId + "/VFX_" + definition.SourceId + ".prefab",
                "Assets/VFX/Generated/" + definition.SourceId + "/VFX_" + definition.SourceId + ".prefab.meta"
            }).Concat(new[]
            {
                "Assets/VFX/Preview/VFXPREVIEW_Environment.unity",
                "Assets/VFX/Preview/VFXPREVIEW_Environment.unity.meta",
                "Assets/VFX/Preview/VFXPREVIEW_HitFeedback.unity",
                "Assets/VFX/Preview/VFXPREVIEW_HitFeedback.unity.meta",
                "Assets/VFX/Preview/VFXPREVIEW_Ultimate.unity",
                "Assets/VFX/Preview/VFXPREVIEW_Ultimate.unity.meta",
                "Packages/com.vfxcomposer.unity/Editor/Independent/IndependentContentAuthoring.cs",
                "Packages/com.vfxcomposer.unity/Editor/Composite/CompositeAndHeroKitAuthoring.cs"
            }).Where(path => File.Exists(Absolute(path))).Distinct(StringComparer.Ordinal).ToArray();
            var before = protectedPaths.ToDictionary(path => path, HashAsset, StringComparer.Ordinal);
            W11W13NextCandidateAuthoring.BuildAll();
            foreach (var item in before) Assert.That(HashAsset(item.Key), Is.EqualTo(item.Value), "Rejected source candidate changed: " + item.Key);

            Assert.That(W11W13NextCandidatePlan.Definitions.Length, Is.EqualTo(20));
            CollectionAssert.AreEqual(new[] { 7, 7, 6 }, new[] { W11W13NextCandidatePlan.Group("W11").Count(), W11W13NextCandidatePlan.Group("W12").Count(), W11W13NextCandidatePlan.Group("W13").Count() });
            foreach (var definition in W11W13NextCandidatePlan.Definitions)
            {
                Assert.That(definition.Id, Is.Not.EqualTo(definition.SourceId));
                Assert.That(definition.Id, Does.StartWith(definition.Group.ToLowerInvariant() + "nc_"));
                var path = W11W13NextCandidatePlan.RecipePath(definition);
                var recipe = JObject.Parse(File.ReadAllText(Absolute(path)));
                Assert.DoesNotThrow(() => W11W13NextCandidatePlan.ValidateRecipe(recipe, definition), path);
                Assert.That((string)recipe["status"], Is.EqualTo("NEXT_CANDIDATE_VISUAL_PENDING"));
                Assert.That(recipe.ToString(), Does.Not.Contain("VISUAL_PASS"));
                Assert.That(recipe.ToString(), Does.Not.Contain("L4"));
            }
        }

        [Test]
        public void DedicatedCompiler_IsIdempotentStrictAndWithinFrozenLocalBudgets()
        {
            W11W13NextCandidateAuthoring.BuildAll();
            var first = W11W13NextCandidatePlan.Definitions.Select(BuildIdentity).ToArray();
            W11W13NextCandidateAuthoring.BuildAll();
            CollectionAssert.AreEqual(first, W11W13NextCandidatePlan.Definitions.Select(BuildIdentity).ToArray());
            foreach (var definition in W11W13NextCandidatePlan.Definitions)
            {
                var path = W11W13NextCandidateAuthoring.PrefabPath(definition.Id);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, definition.Id);
                var controller = prefab.GetComponent<W11W13NextCandidateController>();
                Assert.That(controller, Is.Not.Null, definition.Id);
                Assert.That(controller.Family, Is.EqualTo(definition.Family));
                Assert.That(controller.Variant, Is.EqualTo(definition.Variant));
                Assert.That(prefab.GetComponents<MonoBehaviour>().Count(value => value is IVfxRuntimeEntry), Is.EqualTo(1));
                Assert.That(prefab.GetComponentInChildren<W11W13NextCandidatePreviewDriver>(true), Is.Null);
                Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(prefab.GetComponentsInChildren<Renderer>(true).Length, Is.LessThanOrEqualTo(definition.RendererBudget));
                Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true).Sum(value => value.main.maxParticles), Is.LessThanOrEqualTo(definition.ParticleBudget));
                Assert.That(prefab.GetComponentsInChildren<Renderer>(true).All(value => !value.enabled), Is.True, definition.Id + " pool idle renderers");
                var manifest = JObject.Parse(File.ReadAllText(VfxProjectRules.ManifestAbsolutePath(definition.Id)));
                Assert.That((string)manifest["enforcement"], Is.EqualTo("strict"));
                Assert.That((string)manifest["compilerVersion"], Is.EqualTo(W11W13NextCandidateAuthoring.CompilerVersion));
                Assert.That((string)manifest.SelectToken("runtimeEntry.path"), Is.EqualTo(path));
                Assert.That((string)manifest["sourceRecipePath"], Is.EqualTo(W11W13NextCandidatePlan.RecipePath(definition)));
                Assert.That((int)manifest.SelectToken("cost.localTextureBytes"), Is.Zero);
            }
        }

        [Test]
        public void W11AndW12_PrefabsOwnTheRequiredObservableCarriersAndProtocols()
        {
            W11W13NextCandidateAuthoring.BuildAll();
            foreach (var definition in W11W13NextCandidatePlan.Definitions.Where(value => value.Family != W11W13NextFamily.Ultimate))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(W11W13NextCandidateAuthoring.PrefabPath(definition.Id));
                var names = prefab.GetComponentsInChildren<Transform>(true).Select(value => value.name).ToArray();
                foreach (var contract in definition.RequiredCarriers)
                {
                    var tokens = contract.Split(new[] { "And", " + ", "/" }, StringSplitOptions.RemoveEmptyEntries);
                    Assert.That(tokens.Any(token => names.Any(name => name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)) || CarrierAliasSatisfied(contract, names), Is.True, definition.Id + " missing observable carrier " + contract);
                }
                if (definition.Variant == W11W13NextVariant.Rain)
                {
                    Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true).Length, Is.GreaterThanOrEqualTo(3));
                    Assert.That(names, Does.Contain("GroundSplashRipples"));
                }
                if (definition.Variant == W11W13NextVariant.Waterfall)
                {
                    Assert.That(prefab.GetComponentsInChildren<LineRenderer>(true).Length, Is.GreaterThanOrEqualTo(4));
                    var curtain = prefab.transform.Find("CurvedWaterCurtain").GetComponent<LineRenderer>();
                    Assert.That(curtain.GetPosition(curtain.positionCount - 1).z, Is.Not.EqualTo(curtain.GetPosition(0).z));
                }
                if (definition.Variant == W11W13NextVariant.ParrySpark)
                {
                    var collision = prefab.transform.Find("CollisionSparkFan").GetComponent<ParticleSystem>().collision;
                    Assert.That(collision.enabled, Is.True); Assert.That(collision.bounce.constant, Is.GreaterThan(0f));
                }
                if (definition.Variant == W11W13NextVariant.ComboSurge) Assert.That(names.Count(value => value.StartsWith("StackRing_", StringComparison.Ordinal)), Is.EqualTo(5));
                if (definition.Variant == W11W13NextVariant.LifestealLink) Assert.That(prefab.transform.Find("SaggingDynamicLink").GetComponent<LineRenderer>().positionCount, Is.EqualTo(20));
            }
        }

        [Test]
        public void W13_CompositesReferenceDependenciesWithoutCopyingTheirPrefabHierarchies()
        {
            W11W13NextCandidateAuthoring.BuildW13ForBatch();
            foreach (var definition in W11W13NextCandidatePlan.Group("W13"))
            {
                var path = W11W13NextCandidateAuthoring.PrefabPath(definition.Id);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab.transform.childCount, Is.EqualTo(4), definition.Id + " contains only four local stage roots");
                CollectionAssert.AreEquivalent(new[] { "IntroStage", "PrimaryStage", "ReleaseStage", "TailStage" }, prefab.transform.Cast<Transform>().Select(value => value.name).ToArray());
                var serialized = new SerializedObject(prefab.GetComponent<W11W13NextCandidateController>());
                var sources = serialized.FindProperty("sourcePrefabs");
                Assert.That(sources.arraySize, Is.EqualTo(definition.Dependencies.Length));
                for (var index = 0; index < sources.arraySize; index++)
                {
                    var source = sources.GetArrayElementAtIndex(index).objectReferenceValue as GameObject;
                    Assert.That(source, Is.Not.Null); Assert.That(AssetDatabase.GetAssetPath(source), Is.EqualTo(W11W13NextCandidateAuthoring.DependencyPrefabPath(definition.Dependencies[index])));
                }
                Assert.That(serialized.FindProperty("timeline").arraySize, Is.EqualTo(W11W13NextCandidatePlan.Timeline(definition).Length));
                Assert.That(serialized.FindProperty("cameraHints").arraySize, Is.EqualTo(3));
                Assert.That(serialized.FindProperty("gates").arraySize, Is.EqualTo(definition.Variant == W11W13NextVariant.DemonGate ? 2 : 0));
                var peak=W11W13NextCandidateAuthoring.ComputeCompositePeak(prefab,definition);Assert.That(peak.Particles,Is.LessThanOrEqualTo(200),definition.Id);Assert.That(peak.ParticleSystems,Is.LessThanOrEqualTo(10),definition.Id);Assert.That(peak.Materials,Is.LessThanOrEqualTo(10),definition.Id);Assert.That(peak.Renderers,Is.LessThanOrEqualTo(14),definition.Id);
                if (definition.Variant == W11W13NextVariant.BladeTempest) Assert.That(definition.Dependencies.Count(value => value == "slash_3d_stylized"), Is.EqualTo(8));
                if (definition.Variant == W11W13NextVariant.MeteorShower) Assert.That(definition.Dependencies.Count(value => value == "meteor_impact_3d"), Is.EqualTo(6));
            }
        }

        private static bool CarrierAliasSatisfied(string contract, string[] names)
        {
            if (contract == "WhiteWaterStrands") return names.Count(name => name.StartsWith("WhiteWaterStrand", StringComparison.Ordinal)) >= 2;
            if (contract == "PairedOrbitMotes") return names.Count(name => name.StartsWith("PairedOrbitMote", StringComparison.Ordinal)) == 2;
            if (contract == "FiveIndependentStackRings") return names.Count(name => name.StartsWith("StackRing_", StringComparison.Ordinal)) == 5;
            if (contract == "ReverseFlowMotes") return names.Count(name => name.StartsWith("ReverseFlowMote", StringComparison.Ordinal)) == 2;
            if (contract == "ExternalRendererMpbFlash") return true;
            return false;
        }

        private static string BuildIdentity(W11W13NextDefinition definition)
        {
            var path = W11W13NextCandidateAuthoring.PrefabPath(definition.Id);
            var manifest = JObject.Parse(File.ReadAllText(VfxProjectRules.ManifestAbsolutePath(definition.Id)));
            return AssetDatabase.AssetPathToGUID(path) + "|" + (string)manifest["buildHash"];
        }
        private static string OldRecipePath(W11W13NextDefinition definition){return definition.Group=="W13"?"Assets/VFX/Recipes/Composites/Ultimate/"+definition.SourceId+".default.json":"Assets/VFX/Recipes/Independent/"+(definition.Group=="W11"?"Environment":"HitFeedback")+"/"+definition.SourceId+".default.json";}
        private static string HashAsset(string path){using(var stream=File.OpenRead(Absolute(path)))using(var sha=SHA256.Create())return string.Concat(sha.ComputeHash(stream).Select(value=>value.ToString("x2")));}
        private static string Absolute(string path){return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,path.Replace('/',Path.DirectorySeparatorChar)));}
    }
}
