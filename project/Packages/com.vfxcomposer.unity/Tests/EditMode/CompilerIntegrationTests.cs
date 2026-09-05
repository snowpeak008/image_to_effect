using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Catalog;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Tests.EditMode
{
    public sealed class CompilerIntegrationTests
    {
        private const string RecipePath = "Assets/VFX/Recipes/fireball-2d.default.json";
        private const string OutputPath = "Assets/VFX/Generated/fireball_2d_s7test";

        [SetUp]
        public void SetUp() { DeleteTestOutputs(); }

        [TearDown]
        public void TearDown() { DeleteTestOutputs(); }

        [Test]
        public void FormalBindingAllowList_AppliesEveryS5Binding_AndProducesTheGoldStageStructure()
        {
            var result = new VfxCompiler().Build(Recipe());
            Assert.That(result.Succeeded, Is.True, Report(result));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath);
            var controller = prefab.GetComponent<GeneratedVfxController>();
            Assert.That(controller, Is.Not.Null);
            var serializedController = new SerializedObject(controller);
            Assert.That((serializedController.FindProperty("launchRoot").objectReferenceValue as GameObject).name, Is.EqualTo("Launch"));
            Assert.That((serializedController.FindProperty("travelRoot").objectReferenceValue as GameObject).name, Is.EqualTo("Travel"));
            Assert.That((serializedController.FindProperty("impactRoot").objectReferenceValue as GameObject).name, Is.EqualTo("Impact"));
            Assert.That(Snapshot(prefab), Is.EqualTo(ExpectedSnapshot()));
            Assert.That(prefab.GetComponentsInChildren<Renderer>(true).All(renderer => AssetDatabase.GetAssetPath(renderer.sharedMaterial).StartsWith(OutputPath + "/", StringComparison.Ordinal)), Is.True, "Generated renderers must use copied generated materials.");
            Assert.That(prefab.GetComponentsInChildren<Transform>(true).Skip(1).All(child => PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == null), Is.True, "S1-selected deep-copy strategy must not leave Nested Prefab module children.");
            AssertNoTemporaryFolders();
        }

        [Test]
        public void DisabledRecipeStage_IsSerializedIntoTheRuntimeControllerFlag()
        {
            var disabledLaunch = Recipe().Replace("\"id\": \"launch\", \"trigger\": \"on_launch\", \"duration\": 0.12, \"enabled\": true", "\"id\": \"launch\", \"trigger\": \"on_launch\", \"duration\": 0.12, \"enabled\": false");
            var result = new VfxCompiler().Build(disabledLaunch);
            Assert.That(result.Succeeded, Is.True, Report(result));
            var controller = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath).GetComponent<GeneratedVfxController>();
            var serialized = new SerializedObject(controller);
            Assert.That(serialized.FindProperty("launchRoot").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("launchEnabled").boolValue, Is.False);
            Assert.That(serialized.FindProperty("travelEnabled").boolValue, Is.True);
            Assert.That(serialized.FindProperty("impactEnabled").boolValue, Is.True);
        }

        [Test]
        public void DryRun_IsReadOnly_AndSecondBuildIsUnchangedAtTheInputAndStructureLayers()
        {
            var compiler = new VfxCompiler();
            var dryRun = compiler.DryRun(Recipe());
            Assert.That(dryRun.Items.Single().State, Is.EqualTo(VfxBuildItemState.Create));
            Assert.That(AssetDatabase.IsValidFolder(OutputPath), Is.False);
            Assert.That(compiler.Build(Recipe()).Succeeded, Is.True);
            var before = Snapshot(AssetDatabase.LoadAssetAtPath<GameObject>(VfxCompiler.PrefabPath(ParseRecipe())));
            var beforeFiles = FileHashes(OutputPath);
            var second = compiler.Build(EquivalentRecipeWithDifferentWhitespaceAndOrder());
            Assert.That(second.Succeeded, Is.True, Report(second));
            Assert.That(second.Plan.Items.Single().State, Is.EqualTo(VfxBuildItemState.Unchanged));
            Assert.That(Snapshot(AssetDatabase.LoadAssetAtPath<GameObject>(VfxCompiler.PrefabPath(ParseRecipe()))), Is.EqualTo(before));
            CollectionAssert.AreEquivalent(beforeFiles, FileHashes(OutputPath), "An unchanged build must not modify managed asset or manifest bytes after initial material serialization.");
            Assert.That(second.Plan.RecipeHash, Is.EqualTo(RecipeCanonicalizer.ComputeSha256(Recipe())));
            AssertNoTemporaryFolders();
        }

        [Test]
        public void BudgetError_BlocksBuildBeforeAnyGeneratedWrite()
        {
            var oversized = Recipe().Replace("\"duration\": 1.0", "\"duration\": 100.0");
            var result = new VfxCompiler().Build(oversized);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Plan.Report.Contains("E404", "/budget/totalDuration"), Is.True, Report(result));
            Assert.That(result.Plan.Items.Single().State, Is.EqualTo(VfxBuildItemState.Blocked));
            Assert.That(AssetDatabase.IsValidFolder(OutputPath), Is.False);
        }

        [Test]
        public void SamePathRebuild_PreservesPrefabGuid()
        {
            var compiler = new VfxCompiler();
            Assert.That(compiler.Build(Recipe()).Succeeded, Is.True);
            var path = VfxCompiler.PrefabPath(ParseRecipe());
            var guid = AssetDatabase.AssetPathToGUID(path);
            var updated = compiler.Build(Recipe().Replace("\"scale\": 1.2", "\"scale\": 1.3"));
            Assert.That(updated.Succeeded, Is.True, Report(updated));
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
        }

        [Test]
        public void FailedBinding_RollsBackGeneratedDirectoryByteForByte_WithPreciseStablePath()
        {
            Assert.That(new VfxCompiler().Build(Recipe()).Succeeded, Is.True);
            var before = FileHashes(VfxCompiler.GeneratedRoot);
            // trail.time requires a TrailRenderer, which the remade FireCore deliberately lacks, so the
            // binding still fails deterministically (the pre-T1b probe used embers.rate against a prefab
            // that had no ParticleSystem at its root; the remade template has one).
            const string wrongBindingManifest = "{ 'manifestVersion':1, 'templateId':'T_BadCore', 'templateVersion':'1', 'kind':'energy_body', 'dimension':'2d', 'assetGuid':'dd90f48c6171c074a07eee8b939a0a2', 'assetPath':'Assets/VFX/Templates/2D/Prefabs/PFT_2D_FireCore.prefab', 'tags':[], 'parameters':{'scale':{'type':'float','min':0.6,'max':2.4,'default':1.2,'binding':'trail.time'}}, 'cost':{'estimatedPeakParticles':0,'materials':1,'trails':0} }";
            const string wrongBindingRecipe = "{ 'recipeVersion':1, 'id':'fireball_2d', 'dimension':'2d', 'archetype':'projectile', 'targetProfile':'pc_editor', 'randomSeed':1, 'stages':[{'id':'travel','trigger':'on_launch','duration':1,'enabled':true,'modules':[{'id':'core','kind':'energy_body','templateId':'T_BadCore','parameters':{'scale':1.3},'enabled':true}]}], 'metadata':{'createdBy':'test','templateCatalogVersion':'1'} }";
            var catalog = TemplateCatalog.FromManifestJson(new[] { wrongBindingManifest.Replace('\'', '\"') });
            var failed = new VfxCompiler().Build(wrongBindingRecipe.Replace('\'', '\"'), catalog);
            Assert.That(failed.Succeeded, Is.False);
            Assert.That(failed.Plan.Report.Contains("E501", "/stages/travel/modules/core/parameters/scale"), Is.True, Report(failed));
            CollectionAssert.AreEquivalent(before, FileHashes(VfxCompiler.GeneratedRoot));
            AssertNoTemporaryFolders();
        }

        [Test]
        public void CommitFault_RestoresPrefabMaterialsManifestAndGuid_AfterCommitHasStarted()
        {
            var compiler = new VfxCompiler();
            Assert.That(compiler.Build(Recipe()).Succeeded, Is.True);
            var prefabPath = VfxCompiler.PrefabPath(ParseRecipe());
            var before = FileHashes(VfxCompiler.GeneratedRoot);
            var guid = AssetDatabase.AssetPathToGUID(prefabPath);
            var failed = new VfxCompiler(null, new CommitFaultHook()).Build(Recipe().Replace("\"scale\": 1.2", "\"scale\": 1.3"));
            Assert.That(failed.Succeeded, Is.False);
            Assert.That(failed.Plan.Report.Contains("E602", "/build"), Is.True, Report(failed));
            CollectionAssert.AreEquivalent(before, FileHashes(VfxCompiler.GeneratedRoot));
            Assert.That(AssetDatabase.AssetPathToGUID(prefabPath), Is.EqualTo(guid));
            AssertNoTemporaryFolders();
        }

        [Test]
        public void FirstBuildCommitFault_RemovesNewOutputFolderAndRestoresTheEmptyGeneratedBaseline()
        {
            var beforeFiles = FileHashes(VfxCompiler.GeneratedRoot);
            var beforeFolders = DirectGeneratedFolders();
            var failed = new VfxCompiler(null, new CommitFaultHook()).Build(Recipe());
            Assert.That(failed.Succeeded, Is.False);
            Assert.That(failed.Plan.Report.Contains("E602", "/build"), Is.True, Report(failed));
            CollectionAssert.AreEquivalent(beforeFiles, FileHashes(VfxCompiler.GeneratedRoot));
            CollectionAssert.AreEquivalent(beforeFolders, DirectGeneratedFolders());
            Assert.That(AssetDatabase.IsValidFolder(OutputPath), Is.False, "A first-build commit fault must not retain a new recipe output folder or its meta GUID.");
            AssertNoTemporaryFolders();
        }

        [Test]
        public void BuildManifest_RecordsCanonicalRecipeTemplatesCompilerOutputAndCost()
        {
            var result = new VfxCompiler().Build(Recipe());
            Assert.That(result.Succeeded, Is.True);
            var manifest = File.ReadAllText(VfxCompiler.ManifestPath(ParseRecipe()));
            StringAssert.Contains("\"recipeHash\": \"" + RecipeCanonicalizer.ComputeSha256(Recipe()) + "\"", manifest);
            StringAssert.Contains("\"recipeRevision\": 1", manifest);
            StringAssert.Contains("\"buildHash\": \"" + result.Plan.BuildHash + "\"", manifest);
            StringAssert.Contains("\"compilerVersion\": \"" + VfxCompiler.CompilerVersion + "\"", manifest);
            StringAssert.Contains("\"outputPrefabPath\": \"" + result.PrefabPath + "\"", manifest);
            foreach (var id in new[] { "PFT_2D_FireCore", "PFT_2D_Embers", "PFT_2D_FireImpact", "PFT_2D_FireTrail", "PFT_2D_LaunchFlash", "PFT_2D_Shockwave" }) StringAssert.Contains("\"templateId\": \"" + id + "\"", manifest);
            // D4: dependencyHash is folded into buildHash but never serialized into the committed manifest.
            StringAssert.DoesNotContain("\"dependencyHash\":", manifest);
            StringAssert.Contains("\"estimatedPeakParticles\": 129", manifest);
            StringAssert.Contains("\"materials\": 7", manifest);
            StringAssert.Contains("\"trails\": 1", manifest);
        }

        [Test]
        public void UnknownManifestBinding_IsBlockedAtTheExactModuleParameterPath()
        {
            const string manifest = "{ 'manifestVersion':1, 'templateId':'T_UnknownBinding', 'templateVersion':'1', 'kind':'energy_body', 'dimension':'2d', 'assetGuid':'01234567890123456789012345678901', 'assetPath':'Assets/VFX/Templates/Test.prefab', 'tags':[], 'parameters':{'scale':{'type':'float','min':0,'max':2,'default':1,'binding':'not.a.real.binding'}}, 'cost':{'estimatedPeakParticles':0,'materials':0,'trails':0} }";
            const string recipe = "{ 'recipeVersion':1, 'id':'unknown_binding', 'dimension':'2d', 'archetype':'projectile', 'targetProfile':'pc_editor', 'randomSeed':1, 'stages':[{'id':'travel','trigger':'on_launch','duration':1,'enabled':true,'modules':[{'id':'core','kind':'energy_body','templateId':'T_UnknownBinding','parameters':{'scale':1},'enabled':true}]}], 'metadata':{'createdBy':'test','templateCatalogVersion':'1'} }";
            var catalog = TemplateCatalog.FromManifestJson(new[] { manifest.Replace('\'', '\"') });
            var plan = new VfxCompiler().DryRun(recipe.Replace('\'', '\"'), catalog);
            Assert.That(plan.Report.Contains("E500", "/stages/travel/modules/core/parameters/scale"), Is.True, Report(plan));
            Assert.That(plan.Items.Single().State, Is.EqualTo(VfxBuildItemState.Blocked));
        }

        [Test]
        public void TemplateDependencyHashChange_ChangesBuildHashAndMarksExistingOutputForUpdate()
        {
            var dependencyHashes = new MutableDependencyHashProvider { Value = "dependency-a" };
            var compiler = new VfxCompiler(null, null, dependencyHashes);
            var first = compiler.Build(Recipe());
            Assert.That(first.Succeeded, Is.True, Report(first));
            dependencyHashes.Value = "dependency-b";
            var plan = compiler.DryRun(Recipe());
            Assert.That(plan.RecipeRevision, Is.EqualTo(1));
            Assert.That(plan.Items.Single().State, Is.EqualTo(VfxBuildItemState.Update));
            Assert.That(plan.BuildHash, Is.Not.EqualTo(first.Plan.BuildHash));
        }

        [Test]
        public void CompilerOutputPaths_AreAlwaysInsideTheGeneratedBoundary()
        {
            var recipe = ParseRecipe();
            Assert.That(VfxCompiler.OutputFolder(recipe), Does.StartWith("Assets/VFX/Generated/"));
            Assert.That(VfxCompiler.PrefabPath(recipe), Does.StartWith("Assets/VFX/Generated/"));
            Assert.That(VfxCompiler.ManifestPath(recipe), Does.StartWith("Assets/VFX/Generated/"));
        }

        // Compiler tests must never delete the retained Preview output or change its Prefab GUID.
        private static string Recipe() { return File.ReadAllText(RecipePath).Replace("\"id\": \"fireball_2d\"", "\"id\": \"fireball_2d_s7test\""); }
        private static void DeleteTestOutputs()
        {
            if (AssetDatabase.IsValidFolder(OutputPath)) AssetDatabase.DeleteAsset(OutputPath);
            var externalManifest = VfxProjectRules.ManifestAbsolutePath("fireball_2d_s7test");
            if (File.Exists(externalManifest)) File.Delete(externalManifest);
            foreach (var folder in AssetDatabase.GetSubFolders(VfxCompiler.GeneratedRoot).Where(IsKnownTemporaryFolder))
            {
                Assert.That(Path.GetDirectoryName(folder).Replace('\\', '/'), Is.EqualTo(VfxCompiler.GeneratedRoot));
                var absolute = Path.Combine(Application.dataPath, folder.Substring("Assets/".Length));
                Assert.That(Directory.GetFileSystemEntries(absolute), Is.Empty, "Only verified empty compiler temporary directories may be cleaned.");
                AssetDatabase.DeleteAsset(folder);
            }
            AssetDatabase.SaveAssets();
        }
        private static bool IsKnownTemporaryFolder(string path)
        {
            var name = Path.GetFileName(path);
            return name.StartsWith("_tmp", StringComparison.Ordinal) || name.StartsWith("TempBuild_", StringComparison.Ordinal) || name.StartsWith("vfxs6tmp_", StringComparison.Ordinal);
        }
        private static void AssertNoTemporaryFolders()
        {
            var lingering = AssetDatabase.GetSubFolders(VfxCompiler.GeneratedRoot).Where(IsKnownTemporaryFolder).ToArray();
            Assert.That(lingering, Is.Empty, "Compiler temporary directories must be removed after Build.");
        }
        private sealed class CommitFaultHook : IVfxCompilerBuildHook
        {
            public void AfterPrefabAndMaterialsSaved(string outputFolder) { throw new InvalidOperationException("intentional commit fault after Prefab/material writes"); }
        }
        private sealed class MutableDependencyHashProvider : ITemplateDependencyHashProvider
        {
            public string Value;
            public string GetDependencyHash(string assetPath) { return Value; }
        }
        private static Recipe ParseRecipe() { return VfxDomainParser.ParseRecipe(Recipe()).Value; }
        private static string EquivalentRecipeWithDifferentWhitespaceAndOrder()
        {
            return Recipe().Replace("\n", "\n    ").Replace(": ", " : ");
        }
        private static Dictionary<string, string> FileHashes(string assetFolder)
        {
            var absolute = Path.Combine(Application.dataPath, assetFolder.Substring("Assets/".Length));
            return Directory.GetFiles(absolute, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal).ToDictionary(path => path.Substring(absolute.Length).Replace('\\', '/'), Hash, StringComparer.Ordinal);
        }
        private static string[] DirectGeneratedFolders() { return AssetDatabase.GetSubFolders(VfxCompiler.GeneratedRoot).OrderBy(path => path, StringComparer.Ordinal).ToArray(); }
        private static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty); }
        private static string Report(VfxBuildResult result) { return Report(result.Plan); }
        private static string Report(VfxBuildPlan plan) { return string.Join(" | ", plan.Report.Entries.Select(entry => entry.Code + " " + entry.Path + " " + entry.Message)); }

        private static string Snapshot(GameObject root)
        {
            var lines = new List<string>();
            SnapshotTransform(root.transform, 0, lines);
            return string.Join("\n", lines);
        }
        private static void SnapshotTransform(Transform transform, int depth, List<string> lines)
        {
            var gameObject = transform.gameObject;
            var components = gameObject.GetComponents<Component>().Where(component => !(component is Transform)).Select(component => component.GetType().Name).OrderBy(name => name, StringComparer.Ordinal).ToList();
            var details = new List<string>();
            var particle = gameObject.GetComponent<ParticleSystem>();
            if (particle != null) { var main = particle.main; details.Add("lifetime=" + main.startLifetime.constant.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); var emission = particle.emission; if (emission.enabled) details.Add("rate=" + emission.rateOverTime.constant.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); if (emission.burstCount > 0) { var bursts = new ParticleSystem.Burst[emission.burstCount]; emission.GetBursts(bursts); details.Add("burst=" + bursts[0].count.constant.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); } }
            var trail = gameObject.GetComponent<TrailRenderer>(); if (trail != null) { details.Add("time=" + trail.time.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); details.Add("width=" + trail.widthMultiplier.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); }
            if (gameObject.name == "Core") details.Add("scale=" + transform.localScale.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            if (gameObject.name == "Shockwave" && particle != null) { var curve = particle.sizeOverLifetime.size.curve; details.Add("endSize=" + curve.keys[curve.length - 1].value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); }
            lines.Add(new string(' ', depth * 2) + gameObject.name + " [" + string.Join(",", components) + "]" + (details.Count == 0 ? string.Empty : " {" + string.Join(",", details) + "}"));
            for (var index = 0; index < transform.childCount; index++) SnapshotTransform(transform.GetChild(index), depth + 1, lines);
        }
        private static string ExpectedSnapshot()
        {
            return File.ReadAllText("Packages/com.vfxcomposer.unity/Tests/EditMode/TestData/fireball-2d.structure.snapshot.txt").Replace("VFX_Fireball_2D", "VFX_Fireball_2D_S7test").Replace("\r\n", "\n").TrimEnd();
        }
    }
}
