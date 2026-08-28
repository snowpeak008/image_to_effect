using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using VFXComposer;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Catalog;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Patch;
using VFXComposer.Editor.SlashV2;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Tests.EditMode
{
    // These are release gates, not historical evidence. They intentionally run in every full EditMode suite.
    public sealed class S11ReleaseAcceptanceTests
    {
        private const string Templates = "Assets/VFX/Templates";
        private static readonly string[] FormalRecipes = { "Assets/VFX/Recipes/fireball-2d.default.json", "Assets/VFX/Recipes/fireball-3d.default.json" };

        [Test]
        public void A1_Default2DFireball_BuildsFormalPrefabWithAllStages()
        {
            var recipe = Read(FormalRecipes[0]);
            var result = new VfxCompiler().Build(recipe);
            Assert.That(result.Succeeded, Is.True, Describe(result.Plan));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            foreach (var stage in new[] { "Launch", "Travel", "Impact" }) Assert.That(prefab.transform.Find(stage), Is.Not.Null, stage);
            Assert.That(prefab.GetComponent<GeneratedVfxController>(), Is.Not.Null);
        }

        [Test]
        public void A2_Repeated2DAnd3DBuilds_AreCanonicalIdempotentAndKeepPrefabGuids()
        {
            var compiler = new VfxCompiler();
            foreach (var path in FormalRecipes)
            {
                var recipe = Read(path); var parsed = VfxDomainParser.ParseRecipe(recipe).Value;
                Assert.That(compiler.Build(recipe).Succeeded, Is.True);
                var prefabPath = VfxCompiler.PrefabPath(parsed); var guid = AssetDatabase.AssetPathToGUID(prefabPath);
                var firstBytes = Hashes(VfxCompiler.OutputFolder(parsed));
                var second = compiler.Build(recipe);
                Assert.That(second.Succeeded, Is.True, Describe(second.Plan));
                Assert.That(second.Plan.Items.Single().State, Is.EqualTo(VfxBuildItemState.Unchanged));
                Assert.That(AssetDatabase.AssetPathToGUID(prefabPath), Is.EqualTo(guid));
                CollectionAssert.AreEquivalent(firstBytes, Hashes(VfxCompiler.OutputFolder(parsed)), "Repeated canonical Build must not rewrite files or produce a meaningless output diff.");
            }
        }

        [Test]
        public void A3_A4_InvalidTemplateAndOutOfRangeParameter_AreBlockedWithoutGeneratedWrites()
        {
            var before = Hashes(VfxCompiler.GeneratedRoot);
            var source = Read(FormalRecipes[0]);
            var unknown = source.Replace("PFT_2D_Embers", "PFT_DOES_NOT_EXIST");
            var outOfRange = source.Replace("\"rate\": 18", "\"rate\": 999");
            var compiler = new VfxCompiler();
            var unknownResult = compiler.Build(unknown);
            var outOfRangeResult = compiler.Build(outOfRange);
            Assert.That(unknownResult.Succeeded, Is.False);
            Assert.That(unknownResult.Plan.Report.Contains("E308", "/stages/travel/modules/embers/templateId"), Is.True);
            Assert.That(outOfRangeResult.Succeeded, Is.False);
            Assert.That(outOfRangeResult.Plan.Report.Contains("E314", "/stages/travel/modules/embers/parameters/rate"), Is.True);
            CollectionAssert.AreEquivalent(before, Hashes(VfxCompiler.GeneratedRoot));
        }

        [Test]
        public void A5_TemplateProtection_BuildAndPatchFailuresLeaveTemplateBytesUntouched()
        {
            const string testRecipe = "Assets/VFX/Recipes/s11_a5.json";
            const string testOutput = "Assets/VFX/Generated/s11_a5";
            var before = Hashes(Templates);
            var source = Read(FormalRecipes[0]);
            var invalid = source.Replace("PFT_2D_Embers", "PFT_DOES_NOT_EXIST");
            Assert.That(new VfxCompiler().Build(invalid).Succeeded, Is.False);
            try
            {
                DeleteAsset(testRecipe); DeleteAsset(testRecipe + VfxPatchService.HistorySuffix); DeleteAsset(testOutput);
                File.WriteAllText(Absolute(testRecipe), source.Replace("\"id\": \"fireball_2d\"", "\"id\": \"s11_a5\""));
                AssetDatabase.ImportAsset(testRecipe, ImportAssetOptions.ForceUpdate);
                var initialBuild = new VfxCompiler().Build(Read(testRecipe));
                Assert.That(initialBuild.Succeeded, Is.True, Describe(initialBuild.Plan));
                var patch = "[{\"op\":\"replace\",\"path\":\"/stages/travel/modules/embers/parameters/rate\",\"value\":999}]";
                var failed = new VfxPatchService().ApplyToAsset(testRecipe, patch, 1);
                Assert.That(failed.IsValid, Is.False);
                Assert.That(failed.Report.Contains("E314", "/stages/travel/modules/embers/parameters/rate"), Is.True);
                var successful = new VfxPatchService().ApplyToAsset(testRecipe, "[{\"op\":\"replace\",\"path\":\"/stages/travel/modules/embers/parameters/rate\",\"value\":9}]", 1);
                Assert.That(successful.IsValid, Is.True, Describe(successful.Report));
                CollectionAssert.AreEquivalent(before, Hashes(Templates));
            }
            finally { DeleteAsset(testOutput); DeleteAsset(testRecipe); DeleteAsset(testRecipe + VfxPatchService.HistorySuffix); AssetDatabase.SaveAssets(); }
        }

        [Test]
        public void A6_EmbersHalfPatch_UpdatesRevisionHistoryAndOnlyTargetModuleImpact()
        {
            const string testRecipe = "Assets/VFX/Recipes/s11_a6.json";
            const string testOutput = "Assets/VFX/Generated/s11_a6";
            try
            {
                DeleteAsset(testRecipe); DeleteAsset(testRecipe + VfxPatchService.HistorySuffix); DeleteAsset(testOutput);
                File.WriteAllText(Absolute(testRecipe), Read(FormalRecipes[0]).Replace("\"id\": \"fireball_2d\"", "\"id\": \"s11_a6\""));
                AssetDatabase.ImportAsset(testRecipe, ImportAssetOptions.ForceUpdate);
                Assert.That(new VfxCompiler().Build(Read(testRecipe)).Succeeded, Is.True);
                var beforeRecipe = JObject.Parse(Read(testRecipe));
                var beforeGenerated = GeneratedSnapshot(testOutput);
                var patch = "[{\"op\":\"replace\",\"path\":\"/stages/travel/modules/embers/parameters/rate\",\"value\":9}]";
                var result = new VfxPatchService().ApplyToAsset(testRecipe, patch, 1);
                Assert.That(result.IsValid, Is.True, Describe(result.Report));
                Assert.That(result.AfterRevision, Is.EqualTo(2));
                Assert.That(result.AffectedItems.Single(item => item.ModuleId == "embers").State, Is.EqualTo(VfxPatchImpactState.Update));
                Assert.That(result.AffectedItems.Where(item => item.ModuleId != "embers").All(item => item.State == VfxPatchImpactState.Unchanged), Is.True);
                var afterRecipe = JObject.Parse(Read(testRecipe));
                afterRecipe["revision"] = 1;
                afterRecipe.SelectToken("$.stages[1].modules[2].parameters.rate").Replace(18);
                Assert.That(JToken.DeepEquals(beforeRecipe, afterRecipe), Is.True, "Recipe snapshot may differ only in revision and Embers rate.");
                var history = JArray.Parse(Read(testRecipe + VfxPatchService.HistorySuffix));
                Assert.That(history.Count, Is.EqualTo(1));
                Assert.That((int)history[0]["beforeRevision"], Is.EqualTo(1));
                Assert.That((int)history[0]["afterRevision"], Is.EqualTo(2));
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/s11_a6/VFX_S11_A6.prefab");
                Assert.That(prefab.transform.Find("Travel/Core/Embers").GetComponent<ParticleSystem>().emission.rateOverTime.constant, Is.EqualTo(9).Within(.001));
                var afterGenerated = GeneratedSnapshot(testOutput);
                Assert.That(afterGenerated.Replace("rate=9", "rate=18"), Is.EqualTo(beforeGenerated), "Generated hierarchy/parameters may differ only at Embers rate.");
            }
            finally { DeleteAsset(testOutput); DeleteAsset(testRecipe); DeleteAsset(testRecipe + VfxPatchService.HistorySuffix); AssetDatabase.SaveAssets(); }
        }

        [Test]
        public void A7_RuntimeAndFixedPreviews_ArePresentWithoutEditorAssemblyInRuntime()
        {
            Assert.That(File.Exists(Absolute("Assets/VFX/Preview/S7_2D_FireballPreview.unity")), Is.True);
            Assert.That(File.Exists(Absolute("Assets/VFX/Preview/S10_3D_FireballPreview.unity")), Is.True);
            Assert.That(File.Exists(Absolute("Assets/VFX/Preview/S12_SlashGeneratedPreview.unity")), Is.True);
            Assert.That(File.Exists(Absolute("Assets/VFX/Preview/S12_AI_ValidatedSlash/S12_AI_ValidatedSlashPreview.unity")), Is.True);
            var runtimeRoot = Path.Combine(Application.dataPath, "..", "Packages", "com.vfxcomposer.unity", "Runtime");
            Assert.That(Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText).All(text => !text.Contains("UnityEditor")), Is.True);
            foreach (var recipePath in FormalRecipes) Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(VfxCompiler.PrefabPath(VfxDomainParser.ParseRecipe(Read(recipePath)).Value)).GetComponent<GeneratedVfxController>(), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(S12SlashCompiler.OutputPrefabPath).GetComponent<SlashEffectController>(), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Preview/S12_AI_ValidatedSlash/VFX_Slash_3D_Stylized_AI_Validated.prefab").GetComponent<SlashEffectController>(), Is.Not.Null);
        }

        [Test]
        public void A8_ThreeDUsesSharedSemanticsAndReportsUnsupportedDimensionMismatch()
        {
            var twoD = VfxDomainParser.ParseRecipe(Read(FormalRecipes[0])).Value;
            var threeD = VfxDomainParser.ParseRecipe(Read(FormalRecipes[1])).Value;
            CollectionAssert.AreEqual(twoD.Stages.Select(stage => stage.Id + ":" + stage.Trigger + ":" + stage.Duration + ":" + stage.Enabled), threeD.Stages.Select(stage => stage.Id + ":" + stage.Trigger + ":" + stage.Duration + ":" + stage.Enabled));
            CollectionAssert.AreEqual(twoD.Stages.SelectMany(stage => stage.Modules).Select(module => module.Id + ":" + module.Kind + ":" + (module.AttachTo ?? string.Empty)), threeD.Stages.SelectMany(stage => stage.Modules).Select(module => module.Id + ":" + module.Kind + ":" + (module.AttachTo ?? string.Empty)));
            var invalid = Read(FormalRecipes[1]).Replace("PFT_3D_Embers", "PFT_2D_Embers");
            Assert.That(new VfxCompiler().DryRun(invalid).Report.Contains("E310", "/stages/travel/modules/embers/templateId"), Is.True);
        }

        [Test]
        public void StaticPerformancePreflightReport_MatchesLiveRecipesCatalogAndGeneratedManifests()
        {
            var report = File.ReadAllText(DocumentationPath("release/STATIC_PERFORMANCE_PREFLIGHT.md"));
            var catalog = VfxCompiler.LoadFormalCatalog();
            foreach (var recipePath in FormalRecipes)
            {
                var recipe = VfxDomainParser.ParseRecipe(Read(recipePath)).Value;
                var budget = BudgetCalculator.Evaluate(recipe, catalog);
                Assert.That(budget.HasErrors, Is.False, Describe(budget));
                var manifest = JObject.Parse(Read(VfxCompiler.ManifestPath(recipe)));
                var cost = manifest["cost"];
                var row = recipe.Id + " | " + (int)cost["estimatedPeakParticles"] + " | " + (int)cost["materials"] + " | " + (int)cost["trails"] + " | " + ((double)cost["totalDuration"]).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                StringAssert.Contains(row, report);
            }
            foreach (var target in new[] { TargetProfile.MobileMedium, TargetProfile.PcEditor })
            {
                var profile = BudgetProfiles.For(target);
                var outcomes = new List<string>();
                foreach (var recipePath in FormalRecipes)
                {
                    var recipe = VfxDomainParser.ParseRecipe(Read(recipePath)).Value;
                    var budget = BudgetCalculator.Evaluate(recipe, catalog, profile);
                    Assert.That(budget.HasErrors, Is.False, profile.Id + ": " + Describe(budget));
                    var warnings = budget.Entries.Where(entry => entry.Severity == ValidationSeverity.Warning).Select(entry => entry.Code).OrderBy(code => code, StringComparer.Ordinal).ToArray();
                    outcomes.Add(recipe.Id + ": " + (warnings.Length == 0 ? "pass" : string.Join(",", warnings)));
                    if (target == TargetProfile.MobileMedium) CollectionAssert.AreEqual(new[] { "W402" }, warnings, recipe.Id + " mobile_medium warning contract");
                    else Assert.That(warnings, Is.Empty, recipe.Id + " pc_editor warning contract");
                }
                var profileRow = profile.Id + " | " + profile.MaxPeakParticles + " | " + profile.MaxMaterials + " | " + profile.MaxTrails + " | " + profile.MaxTotalDuration.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " | " + string.Join("; ", outcomes);
                StringAssert.Contains(profileRow, report);
            }
            StringAssert.Contains("静态预检，非真机认证", report);
            StringAssert.Contains("mobile_medium", report); StringAssert.Contains("pc_editor", report);
        }

        [Test]
        public void ReleaseVersionIdentifiers_AreConsistent()
        {
            var package = JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "..", "Packages", "com.vfxcomposer.unity", "package.json")));
            var version = (string)package["version"];
            Assert.That(version, Is.EqualTo("0.1.0"));
            Assert.That(VFXComposerRuntimeMarker.PackageVersion, Is.EqualTo(version));
            Assert.That(VfxCompiler.CompilerVersion, Is.EqualTo(version));
            foreach (var recipePath in FormalRecipes)
            {
                var recipe = VfxDomainParser.ParseRecipe(Read(recipePath)).Value;
                Assert.That((string)JObject.Parse(Read(VfxCompiler.ManifestPath(recipe)))["compilerVersion"], Is.EqualTo(version), recipe.Id);
            }
            StringAssert.Contains("## " + version, File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "CHANGELOG.md"))));
        }

        [Test]
        public void ErrorCodeAudit_EditorSourceAndDocumentationRemainBidirectionallyInSync()
        {
            var package = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages", "com.vfxcomposer.unity"));
            var sourceRoots = new[] { Path.Combine(package, "Editor"), Path.Combine(package, "Runtime") };
            var sourceCodes = new HashSet<string>(sourceRoots.SelectMany(root => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)).SelectMany(path => Regex.Matches(File.ReadAllText(path), "\\b[EI]\\d{3,4}\\b").Cast<Match>().Select(match => match.Value)), StringComparer.Ordinal);
            foreach (var code in new[] { "E401", "E402", "E403", "E404" }) sourceCodes.Add("W" + code.Substring(1));
            var documentation = File.ReadAllText(DocumentationPath("release/ERROR_CODES.md"));
            var documentedCodes = new HashSet<string>(Regex.Matches(documentation, "(?m)^\\|\\s*([EIW]\\d{3,4})\\s*\\|").Cast<Match>().Select(match => match.Groups[1].Value), StringComparer.Ordinal);
            CollectionAssert.AreEquivalent(sourceCodes, documentedCodes);
            Assert.That(sourceCodes, Does.Contain("E1200").And.Contain("I1230"));
            foreach (var code in sourceCodes) StringAssert.Contains("| " + code + " |", documentation);
            StringAssert.Contains("actualValue", documentation); StringAssert.Contains("allowedRange", documentation);
        }

        [Test]
        public void A7_PlayerBuildPreflight_SerializesFormalS12AndCompositePreviewScenesToAnExternalTemporaryBuild()
        {
            var folder = Path.Combine(Path.GetTempPath(), "vfxcomposer_s11_player_" + Guid.NewGuid().ToString("N"));
            var executable = Path.Combine(folder, "VfxComposerReleaseProbe.exe");
            try
            {
                Directory.CreateDirectory(folder);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/VFX/Preview/S7_2D_FireballPreview.unity", "Assets/VFX/Preview/S10_3D_FireballPreview.unity", "Assets/VFX/Preview/S12_SlashGeneratedPreview.unity", "Assets/VFX/Preview/S12_AI_ValidatedSlash/S12_AI_ValidatedSlashPreview.unity", "Assets/VFX/Preview/VFXPREVIEW_Ultimate.unity", "Assets/VFX/Preview/VFXPREVIEW_HeroKits.unity" },
                    locationPathName = executable,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                });
                Assert.That(report.summary.result, Is.EqualTo(BuildResult.Succeeded), report.summary.totalErrors + " build errors; see Unity build log.");
                Assert.That(File.Exists(executable), Is.True);
            }
            finally
            {
                if (Directory.Exists(folder) && Path.GetFileName(folder).StartsWith("vfxcomposer_s11_player_", StringComparison.Ordinal)) Directory.Delete(folder, true);
            }
        }

        [Test]
        public void A7_EditorOnlyAuthoringAudit_RejectsUnguardedEditorDependenciesOutsideEditorFolders()
        {
            var assets = Application.dataPath;
            var offenders = Directory.GetFiles(assets, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Replace('\\', '/').Contains("/Editor/"))
                .Where(path => { var source = File.ReadAllText(path); return (source.Contains("UnityEditor") || source.Contains("VFXComposer.Editor")) && !source.TrimStart().StartsWith("#if UNITY_EDITOR", StringComparison.Ordinal); })
                .ToArray();
            Assert.That(offenders, Is.Empty, "Assets scripts with Editor dependencies must be whole-file UNITY_EDITOR guarded or live below an Editor folder.");
        }

        [Test]
        public void A7_PlayerSceneList_ContainsFormalAndS12EvidencePreviewScenes()
        {
            CollectionAssert.IsSubsetOf(new[] { "Assets/VFX/Preview/S7_2D_FireballPreview.unity", "Assets/VFX/Preview/S10_3D_FireballPreview.unity", "Assets/VFX/Preview/S12_SlashGeneratedPreview.unity", "Assets/VFX/Preview/S12_AI_ValidatedSlash/S12_AI_ValidatedSlashPreview.unity" }, EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray());
        }

        [Test]
        public void S12_StaticSlashBudgetReport_MatchesTheLiveV2BudgetCalculator()
        {
            var recipe = File.ReadAllText(Absolute("Assets/VFX/Recipes/Slash/slash-3d-stylized.default.v2.json")); var catalog = S12SlashCompiler.LoadFormalCatalog(); var parsed = S12RecipeDispatcher.Parse(recipe).SlashV2; var budget = S12SlashBudgetCalculator.Evaluate(parsed, catalog); Assert.That(budget.HasErrors, Is.False, Describe(budget));
            var modules = parsed.Phases.Where(phase => phase.Enabled).SelectMany(phase => phase.Modules.Where(module => module.Enabled)).ToArray(); var manifests = modules.Select(module => catalog.ByTemplateId[module.TemplateId]).ToArray(); var particles = manifests.Sum(manifest => manifest.Cost.EstimatedPeakParticles); var systems = manifests.Sum(manifest => manifest.Cost.ParticleSystems); var materials = manifests.SelectMany(manifest => manifest.MaterialGuids).Distinct().Count(); var renderers = manifests.Sum(manifest => manifest.Cost.TransparentRenderers);
            var report = File.ReadAllText(DocumentationPath("release/S12_SLASH_STATIC_BUDGET.md")); var row = parsed.Id + " | " + particles + " | " + systems + " | " + materials + " | " + renderers + " | " + parsed.Timeline.Duration.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " | 48 | 4 | 5 | 7"; StringAssert.Contains(row, report); StringAssert.Contains("静态预检，非真机认证", report);
        }

        private static string DocumentationPath(string relative) { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", relative)); }
        private static string Absolute(string assetPath) { return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)); }
        private static string Read(string assetPath) { return File.ReadAllText(Absolute(assetPath)); }
        private static void DeleteAsset(string path) { if (AssetDatabase.LoadMainAssetAtPath(path) != null || AssetDatabase.IsValidFolder(path)) AssetDatabase.DeleteAsset(path); else if (File.Exists(Absolute(path))) File.Delete(Absolute(path)); }
        private static Dictionary<string, string> Hashes(string assetFolder)
        {
            var root = Absolute(assetFolder); if (!Directory.Exists(root)) return new Dictionary<string, string>();
            return Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal).ToDictionary(path => path.Substring(root.Length).Replace('\\', '/'), Hash, StringComparer.Ordinal);
        }
        private static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty); }
        private static string GeneratedSnapshot(string folder)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/VFX_S11_A6.prefab");
            var lines = new List<string>(); Snapshot(prefab.transform, lines); return string.Join("\n", lines);
        }
        private static void Snapshot(Transform transform, List<string> lines)
        {
            var particle = transform.GetComponent<ParticleSystem>();
            var values = transform.name + " active=" + transform.gameObject.activeSelf + " scale=" + transform.localScale.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            if (particle != null) { var main = particle.main; var emission = particle.emission; values += " lifetime=" + main.startLifetime.constant.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " rate=" + emission.rateOverTime.constant.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }
            var trail = transform.GetComponent<TrailRenderer>(); if (trail != null) values += " trail=" + trail.time.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ":" + trail.widthMultiplier.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            lines.Add(values); for (var index = 0; index < transform.childCount; index++) Snapshot(transform.GetChild(index), lines);
        }
        private static string Describe(VfxBuildPlan plan) { return Describe(plan.Report); }
        private static string Describe(ValidationReport report) { return string.Join(" | ", report.Entries.Select(entry => entry.Code + " " + entry.Path + " " + entry.Message)); }
    }
}
