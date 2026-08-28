using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Patch;

namespace VFXComposer.Tests.EditMode
{
    public sealed class VfxPatchTests
    {
        private const string SourceRecipe = "Assets/VFX/Recipes/fireball-2d.default.json";
        private const string TestRecipe = "Assets/VFX/Recipes/fireball-2d.s8test.json";
        private const string TestOutput = "Assets/VFX/Generated/fireball_2d_s8test";
        private const string Templates = "Assets/VFX/Templates";

        [SetUp]
        public void SetUp()
        {
            DeleteTestArtifacts();
            File.WriteAllText(Absolute(TestRecipe), File.ReadAllText(Absolute(SourceRecipe)).Replace("\"id\": \"fireball_2d\"", "\"id\": \"fireball_2d_s8test\""));
            AssetDatabase.ImportAsset(TestRecipe, ImportAssetOptions.ForceUpdate);
        }

        [TearDown]
        public void TearDown() { DeleteTestArtifacts(); }

        [Test]
        public void Validate_SupportsReplaceAddRemoveEnableAndDisable_UsingStableIdsOnly()
        {
            AssertValid("[{ 'op':'replace','path':'/stages/travel/modules/embers/parameters/rate','value':9 }]");
            AssertValid("[{ 'op':'disable','path':'/stages/travel/modules/embers' }]");
            AssertValid("[{ 'op':'enable','path':'/stages/impact' }]");
            AssertValid("[{ 'op':'remove','path':'/stages/travel/modules/embers' }]");
            AssertValid("[{ 'op':'add','path':'/stages/travel/modules/embers2','value':{ 'id':'embers2','kind':'secondary_particles','templateId':'PFT_2D_Embers','parameters':{'rate':9,'lifetime':0.55},'attachTo':'core','enabled':true } }]");
        }

        [Test]
        public void StrictPatchContract_RejectsWrapperUnknownFieldsUnknownOpsAndArrayIndexes()
        {
            AssertInvalid("{ 'operations':[] }", "E703");
            AssertInvalid("[{ 'op':'merge','path':'/stages/travel' }]", "E704");
            AssertInvalid("[{ 'op':'replace','path':'/stages/travel/modules/embers/parameters/rate','value':9,'extra':true }]", "E701");
            AssertInvalid("[{ 'op':'remove','path':'/stages/travel/modules/embers','value':null }]", "E701");
            AssertInvalid("[{ 'op':'replace','path':'/stages/1/modules/0/parameters/rate','value':9 }]", "E705");
            AssertInvalid("[{ 'op':'replace','path':'/stages/travel/modules/embers/parameters/../rate','value':9 }]", "E705");
            AssertInvalid("[{ 'op':'replace','path':'/stages/travel/modules/embers/parameters/~1rate','value':9 }]", "E705");
            AssertInvalid("[{ 'op':'replace','path':'/stages/travel/modules/embers/parameters/rate/tail','value':9 }]", "E705");
            AssertInvalid("[{ 'op':'add','path':'/stages/travel/modules/new/tail','value':{} }]", "E705");
            AssertInvalid("[{ 'op':'remove','path':'/stages/travel/modules/embers/tail' }]", "E705");
            AssertInvalid("[{ 'op':'disable','path':'/stages/travel/tail' }]", "E705");
            AssertInvalid("[]", "E702");
        }

        [Test]
        public void Validation_RejectsMissingTargetsWrongTypesRangesRequiredRemovalAndInvalidAdd()
        {
            AssertInvalid("[{ 'op':'replace','path':'/stages/missing/modules/embers/parameters/rate','value':9 }]", "E706");
            AssertInvalid("[{ 'op':'replace','path':'/stages/travel/modules/embers/parameters/rate','value':'nine' }]", "E313");
            AssertInvalid("[{ 'op':'replace','path':'/stages/travel/modules/embers/parameters/rate','value':999 }]", "E314");
            AssertInvalid("[{ 'op':'remove','path':'/stages/travel/modules/core' }]", "E708");
            AssertInvalid("[{ 'op':'add','path':'/stages/travel/modules/bad','value':{ 'id':'bad','kind':'energy_body','templateId':'NOT_A_TEMPLATE','parameters':{'scale':1},'enabled':true } }]", "E308");
            AssertInvalid("[{ 'op':'add','path':'/stages/travel/modules/bad','value':{ 'id':'bad','kind':'impact_burst','templateId':'PFT_2D_FireCore','parameters':{'scale':1},'enabled':true } }]", "E309");
        }

        [Test]
        public void RequiredTravelEnergyBodyProtection_IsSemanticNotTiedToCanonicalCoreId()
        {
            var renamed = File.ReadAllText(Absolute(TestRecipe)).Replace("\"id\": \"core\"", "\"id\": \"body_alpha\"").Replace("\"attachTo\": \"core\"", "\"attachTo\": \"body_alpha\"");
            var result = new VfxPatchService().Validate(renamed, "[{ 'op':'remove','path':'/stages/travel/modules/body_alpha' }]".Replace('\'', '\"'), 1);
            Assert.That(result.Report.Entries.Any(entry => entry.Code == "E708"), Is.True, Describe(result));
        }

        [Test]
        public void Apply_IsRevisionGuarded_RecordsHistoryAndReportsOnlyEmbersAsUpdate()
        {
            var compiler = new VfxCompiler();
            Assert.That(compiler.Build(File.ReadAllText(Absolute(TestRecipe))).Succeeded, Is.True);
            var snapshotBefore = PrefabSnapshot(AssetDatabase.LoadAssetAtPath<GameObject>(VfxCompiler.PrefabPath(VfxDomainParser.ParseRecipe(File.ReadAllText(Absolute(TestRecipe))).Value)));
            var templatesBefore = Hashes(Templates);
            var service = new VfxPatchService();
            var conflict = service.ApplyToAsset(TestRecipe, HalfPatch(), 2);
            Assert.That(conflict.Report.Contains("E707", "/revision"), Is.True);
            var applied = service.ApplyToAsset(TestRecipe, HalfPatch(), 1);
            Assert.That(applied.IsValid, Is.True, Describe(applied));
            Assert.That(applied.AfterRevision, Is.EqualTo(2));
            Assert.That(applied.AffectedItems.Single(item => item.ModuleId == "embers").State, Is.EqualTo(VfxPatchImpactState.Update));
            Assert.That(applied.AffectedItems.Where(item => item.ModuleId != "embers").All(item => item.State == VfxPatchImpactState.Unchanged), Is.True, Describe(applied));
            Assert.That(File.ReadAllText(Absolute(TestRecipe)), Does.Contain("\"revision\": 2"));
            Assert.That(File.ReadAllText(Absolute(TestRecipe + VfxPatchService.HistorySuffix)), Does.Contain("\"beforeRevision\": 1"));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VfxCompiler.PrefabPath(VfxDomainParser.ParseRecipe(File.ReadAllText(Absolute(TestRecipe))).Value));
            var embers = prefab.transform.Find("Travel/Core/Embers").GetComponent<ParticleSystem>();
            Assert.That(embers.emission.rateOverTime.constant, Is.EqualTo(9).Within(0.001));
            var snapshotAfter = PrefabSnapshot(prefab);
            Assert.That(snapshotAfter.Replace("rate=9", "rate=18"), Is.EqualTo(snapshotBefore), "A6 structure snapshot diff must be limited to Embers rate.");
            CollectionAssert.AreEquivalent(templatesBefore, Hashes(Templates), "A5: Patch must not alter any Template asset bytes.");
            var replay = service.ApplyToAsset(TestRecipe, HalfPatch(), 1);
            Assert.That(replay.Report.Contains("E707", "/revision"), Is.True, "Old Patch replay must reject without another revision increment.");
        }

        [Test]
        public void FailedPatch_LeavesRecipeHistoryGeneratedAndTemplateHashesUntouched()
        {
            Assert.That(new VfxPatchService().ApplyToAsset(TestRecipe, HalfPatch(), 1).IsValid, Is.True);
            var recipeBefore = Hashes("Assets/VFX/Recipes");
            var generatedBefore = Hashes(TestOutput);
            var templatesBefore = Hashes(Templates);
            var failed = new VfxPatchService().ApplyToAsset(TestRecipe, "[{ 'op':'replace','path':'/stages/travel/modules/embers/parameters/rate','value':999 }]".Replace('\'', '\"'), 2);
            Assert.That(failed.IsValid, Is.False);
            CollectionAssert.AreEquivalent(recipeBefore, Hashes("Assets/VFX/Recipes"));
            CollectionAssert.AreEquivalent(generatedBefore, Hashes(TestOutput));
            CollectionAssert.AreEquivalent(templatesBefore, Hashes(Templates));
        }

        [Test]
        public void StageDisable_ProducesAnExplicitStageUpdateRatherThanAllUnchanged()
        {
            var result = new VfxPatchService().Validate(File.ReadAllText(Absolute(TestRecipe)), "[{ 'op':'disable','path':'/stages/travel' }]".Replace('\'', '\"'), 1);
            Assert.That(result.IsValid, Is.True, Describe(result));
            Assert.That(result.AffectedItems.Single(item => item.IsStage && item.StageId == "travel").State, Is.EqualTo(VfxPatchImpactState.Update));
        }

        [Test]
        public void PostPatchValidation_AttributesTheExactOperationOrLeavesItExplicitlyUnattributed()
        {
            var multi = new VfxPatchService().Validate(File.ReadAllText(Absolute(TestRecipe)), "[{ 'op':'replace','path':'/stages/travel/modules/embers/parameters/rate','value':999 },{ 'op':'replace','path':'/stages/travel/modules/embers/parameters/lifetime','value':.55 }]".Replace('\'', '\"'), 1);
            Assert.That(multi.Report.Contains("E314", "/stages/travel/modules/embers/parameters/rate"), Is.True, Describe(multi));
            Assert.That(multi.FailedOperationIndex, Is.EqualTo(0), "The range error belongs to op 0, not the later valid op.");

            var badAdd = new VfxPatchService().Validate(File.ReadAllText(Absolute(TestRecipe)), "[{ 'op':'add','path':'/stages/travel/modules/bad','value':{ 'id':'bad','kind':'energy_body','templateId':'NOT_A_TEMPLATE','parameters':{'scale':1},'enabled':true } }]".Replace('\'', '\"'), 1);
            Assert.That(badAdd.Report.Contains("E308", "/stages/travel/modules/bad/templateId"), Is.True, Describe(badAdd));
            Assert.That(badAdd.FailedOperationIndex, Is.EqualTo(0), "A deep validation path beneath an added module belongs to add.");

            var budgetPatch = "[" + AddEmbersOperation("embers2") + "," + AddEmbersOperation("embers3") + "," + AddEmbersOperation("embers4") + "]";
            var budget = new VfxPatchService().Validate(File.ReadAllText(Absolute(TestRecipe)), budgetPatch.Replace('\'', '\"'), 1);
            Assert.That(budget.Report.Contains("E401", "/budget/estimatedPeakParticles"), Is.True, Describe(budget));
            Assert.That(budget.FailedOperationIndex, Is.Null);
            Assert.That(budget.IsPostPatchValidationFailure, Is.True);
        }

        [Test]
        public void SnapshotAndRollbackFailures_AreReportedWithoutEscapingThePatchApi()
        {
            var captureFailed = new VfxPatchService(null, null, new ThrowingSnapshotProvider()).ApplyToAsset(TestRecipe, HalfPatch(), 1);
            Assert.That(captureFailed.Report.Contains("E710", "/transaction/snapshot"), Is.True, Describe(captureFailed));

            var rollbackFailed = new VfxPatchService(null, new ThrowAfterRecipeWrittenHook(), new RestoreFailingSnapshotProvider()).ApplyToAsset(TestRecipe, HalfPatch(), 1);
            Assert.That(rollbackFailed.IsValid, Is.False);
            Assert.That(rollbackFailed.Report.Contains("E711", "/transaction/rollback/generated"), Is.True, Describe(rollbackFailed));
            StringAssert.Contains("manual recovery is required", Describe(rollbackFailed).ToLowerInvariant());
        }

        [Test]
        public void BuildFailure_RestoresExistingGeneratedRecipeHistoryAndDirectoryBytes()
        {
            Assert.That(new VfxCompiler().Build(File.ReadAllText(Absolute(TestRecipe))).Succeeded, Is.True);
            var beforeGenerated = Hashes(VfxCompiler.GeneratedRoot);
            var beforeRecipes = Hashes("Assets/VFX/Recipes");
            var beforeFolders = DirectGeneratedFolders();
            var prefabPath = VfxCompiler.PrefabPath(VfxDomainParser.ParseRecipe(File.ReadAllText(Absolute(TestRecipe))).Value);
            var guid = AssetDatabase.AssetPathToGUID(prefabPath);
            var service = new VfxPatchService(() => new VfxCompiler(null, new BuildCommitFaultHook()), null);
            var failed = service.ApplyToAsset(TestRecipe, HalfPatch(), 1);
            Assert.That(failed.IsValid, Is.False);
            CollectionAssert.AreEquivalent(beforeGenerated, Hashes(VfxCompiler.GeneratedRoot));
            CollectionAssert.AreEquivalent(beforeRecipes, Hashes("Assets/VFX/Recipes"));
            CollectionAssert.AreEquivalent(beforeFolders, DirectGeneratedFolders());
            Assert.That(AssetDatabase.AssetPathToGUID(prefabPath), Is.EqualTo(guid));
            AssertNoPatchResidue();
        }

        [Test]
        public void PostBuildTextFault_RestoresExistingAndFirstBuildGeneratedByteForByte()
        {
            Assert.That(new VfxCompiler().Build(File.ReadAllText(Absolute(TestRecipe))).Succeeded, Is.True);
            var beforeGenerated = Hashes(VfxCompiler.GeneratedRoot);
            var beforeRecipes = Hashes("Assets/VFX/Recipes");
            var beforeFolders = DirectGeneratedFolders();
            var prefabPath = VfxCompiler.PrefabPath(VfxDomainParser.ParseRecipe(File.ReadAllText(Absolute(TestRecipe))).Value);
            var guid = AssetDatabase.AssetPathToGUID(prefabPath);
            var failedExisting = new VfxPatchService(null, new ThrowAfterRecipeWrittenHook()).ApplyToAsset(TestRecipe, HalfPatch(), 1);
            Assert.That(failedExisting.IsValid, Is.False);
            CollectionAssert.AreEquivalent(beforeGenerated, Hashes(VfxCompiler.GeneratedRoot));
            CollectionAssert.AreEquivalent(beforeRecipes, Hashes("Assets/VFX/Recipes"));
            CollectionAssert.AreEquivalent(beforeFolders, DirectGeneratedFolders());
            Assert.That(AssetDatabase.AssetPathToGUID(prefabPath), Is.EqualTo(guid));
            AssertNoPatchResidue();

            AssetDatabase.DeleteAsset(TestOutput);
            AssetDatabase.SaveAssets();
            beforeGenerated = Hashes(VfxCompiler.GeneratedRoot);
            beforeFolders = DirectGeneratedFolders();
            var failedFirst = new VfxPatchService(null, new ThrowAfterRecipeWrittenHook()).ApplyToAsset(TestRecipe, HalfPatch(), 1);
            Assert.That(failedFirst.IsValid, Is.False);
            CollectionAssert.AreEquivalent(beforeGenerated, Hashes(VfxCompiler.GeneratedRoot));
            CollectionAssert.AreEquivalent(beforeFolders, DirectGeneratedFolders());
            Assert.That(AssetDatabase.IsValidFolder(TestOutput), Is.False);
            AssertNoPatchResidue();
        }

        private static void AssertValid(string patch) { Assert.That(new VfxPatchService().Validate(File.ReadAllText(Absolute(TestRecipe)), patch.Replace('\'', '\"'), 1).IsValid, Is.True, patch); }
        private static void AssertInvalid(string patch, string code) { var result = new VfxPatchService().Validate(File.ReadAllText(Absolute(TestRecipe)), patch.Replace('\'', '\"'), 1); Assert.That(result.Report.Entries.Any(entry => entry.Code == code), Is.True, Describe(result)); }
        private static string HalfPatch() { return "[{ 'op':'replace','path':'/stages/travel/modules/embers/parameters/rate','value':9 }]".Replace('\'', '\"'); }
        private static string AddEmbersOperation(string id) { return "{ 'op':'add','path':'/stages/travel/modules/" + id + "','value':{ 'id':'" + id + "','kind':'secondary_particles','templateId':'PFT_2D_Embers','parameters':{'rate':9,'lifetime':.55},'attachTo':'core','enabled':true } }"; }
        private static string Describe(VfxPatchResult result) { return string.Join(" | ", result.Report.Entries.Select(entry => entry.Code + " " + entry.Path + " " + entry.Message)); }
        private static string Absolute(string assetPath) { return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)); }
        private static Dictionary<string, string> Hashes(string assetFolder)
        {
            var absolute = Absolute(assetFolder);
            if (!Directory.Exists(absolute)) return new Dictionary<string, string>();
            return Directory.GetFiles(absolute, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal).ToDictionary(path => path.Substring(absolute.Length).Replace('\\', '/'), Hash, StringComparer.Ordinal);
        }
        private static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty); }
        private static string PrefabSnapshot(GameObject root)
        {
            var lines = new List<string>();
            Snapshot(root.transform, 0, lines);
            return string.Join("\n", lines);
        }
        private static void Snapshot(Transform transform, int depth, List<string> lines)
        {
            var particle = transform.GetComponent<ParticleSystem>();
            var suffix = particle == null ? string.Empty : " rate=" + particle.emission.rateOverTime.constant.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            lines.Add(new string(' ', depth * 2) + transform.name + suffix);
            for (var index = 0; index < transform.childCount; index++) Snapshot(transform.GetChild(index), depth + 1, lines);
        }
        private static void DeleteTestArtifacts()
        {
            if (AssetDatabase.IsValidFolder(TestOutput)) AssetDatabase.DeleteAsset(TestOutput);
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TestRecipe) != null) AssetDatabase.DeleteAsset(TestRecipe);
            var history = TestRecipe + VfxPatchService.HistorySuffix;
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(history) != null) AssetDatabase.DeleteAsset(history);
            foreach (var stray in new[] { Absolute(TestRecipe), Absolute(history), Absolute(TestRecipe) + ".pending", Absolute(history) + ".pending" }) if (File.Exists(stray)) File.Delete(stray);
            AssetDatabase.Refresh();
        }
        private static string[] DirectGeneratedFolders() { return AssetDatabase.GetSubFolders(VfxCompiler.GeneratedRoot).OrderBy(path => path, StringComparer.Ordinal).ToArray(); }
        private static void AssertNoPatchResidue()
        {
            Assert.That(Directory.GetFiles(Application.dataPath, "*.pending", SearchOption.AllDirectories), Is.Empty);
            Assert.That(AssetDatabase.GetSubFolders(VfxCompiler.GeneratedRoot).Where(path => Path.GetFileName(path).StartsWith("vfxs8", StringComparison.Ordinal)), Is.Empty);
        }
        private sealed class BuildCommitFaultHook : IVfxCompilerBuildHook
        {
            public void AfterPrefabAndMaterialsSaved(string outputFolder) { throw new InvalidOperationException("S8 test build fault"); }
        }
        private sealed class ThrowAfterRecipeWrittenHook : IVfxPatchTransactionHook
        {
            public void AfterBuildBeforeTextCommit() { }
            public void AfterRecipeWrittenBeforeHistoryWritten() { throw new InvalidOperationException("S8 test text fault after Recipe write"); }
        }
        private sealed class ThrowingSnapshotProvider : IVfxPatchSnapshotProvider
        {
            public IVfxPatchGeneratedSnapshot Capture(string assetFolder) { throw new IOException("intentional snapshot capture failure"); }
        }
        private sealed class RestoreFailingSnapshotProvider : IVfxPatchSnapshotProvider
        {
            public IVfxPatchGeneratedSnapshot Capture(string assetFolder) { return new RestoreFailingSnapshot(); }
        }
        private sealed class RestoreFailingSnapshot : IVfxPatchGeneratedSnapshot
        {
            public void Restore() { throw new IOException("intentional Generated restore failure"); }
            public void Dispose() { }
        }
    }
}
