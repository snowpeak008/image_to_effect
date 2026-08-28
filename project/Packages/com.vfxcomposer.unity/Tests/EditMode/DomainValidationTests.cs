using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using VFXComposer.Editor.Catalog;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Tests.EditMode
{
    public sealed class DomainValidationTests
    {
        [Test]
        public void DefaultFireball_IsStructurallyAndSemanticallyValid()
        {
            var report = RecipeValidator.Validate(Fixture("valid-fireball.json"), Catalog());
            Assert.That(report.HasErrors, Is.False, Entries(report));
            var parsed = VfxDomainParser.ParseRecipe(Fixture("valid-fireball.json"));
            var budget = BudgetCalculator.Evaluate(parsed.Value, Catalog());
            Assert.That(budget.HasErrors, Is.False, Entries(budget));
            Assert.That(budget.Entries.Exists(entry => entry.Severity == ValidationSeverity.Info), Is.True);
        }

        [Test]
        public void RecipeRevision_IsBackwardCompatibleWhenAbsent_AndRejectsValuesBelowOne()
        {
            var legacy = VfxDomainParser.ParseRecipe(Fixture("valid-fireball.json"));
            Assert.That(legacy.Report.HasErrors, Is.False, Entries(legacy.Report));
            Assert.That(legacy.Value.Revision, Is.EqualTo(1), "v1 Recipes written before S6 revision support default to revision 1.");
            var invalid = Fixture("valid-fireball.json").Replace("\"recipeVersion\": 1,", "\"recipeVersion\": 1,\n  \"revision\": 0,");
            var report = RecipeValidator.Validate(invalid, Catalog());
            Assert.That(report.Contains("E316", "/revision"), Is.True, Entries(report));
            var wrongType = Fixture("valid-fireball.json").Replace("\"recipeVersion\": 1,", "\"recipeVersion\": 1,\n  \"revision\": \"one\",");
            Assert.That(VfxDomainParser.ParseRecipe(wrongType).Report.Contains("E102", "/revision"), Is.True);
        }

        [TestCase("invalid-unknown-field.json", "E100")]
        [TestCase("invalid-missing-required.json", "E101")]
        [TestCase("invalid-enum.json", "E103")]
        [TestCase("invalid-duplicate-id.json", "E303")]
        [TestCase("invalid-unknown-template.json", "E308")]
        [TestCase("invalid-parameter-range.json", "E311")]
        public void InvalidFixtures_AreRejectedWithStableErrorCodes(string file, string code)
        {
            var report = RecipeValidator.Validate(Fixture(file), Catalog());
            Assert.That(report.HasErrors, Is.True, Entries(report));
            Assert.That(report.Entries.Exists(entry => entry.Code == code), Is.True, Entries(report));
        }

        [Test]
        public void ParameterErrors_UseStableModulePaths_AndRejectTypeAndRange()
        {
            var typeError = Fixture("invalid-parameter-type.json");
            var typeReport = RecipeValidator.Validate(typeError, Catalog());
            Assert.That(typeReport.Contains("E313", "/stages/travel/modules/core/parameters/scale"), Is.True, Entries(typeReport));
            var rangeReport = RecipeValidator.Validate(Fixture("invalid-parameter-range.json"), Catalog());
            Assert.That(rangeReport.Contains("E314", "/stages/travel/modules/core/parameters/scale"), Is.True, Entries(rangeReport));
            Assert.That(rangeReport.Contains("E311", "/stages/travel/modules/core/parameters/unknown"), Is.True, Entries(rangeReport));
        }

        [Test]
        public void UnknownTemplate_ReportsOrdinalCatalogAllowList()
        {
            var report = RecipeValidator.Validate(Fixture("invalid-unknown-template.json"), Catalog());
            var entry = report.Entries.Find(value => value.Code == "E308");
            Assert.That(entry.AllowedRange, Is.EqualTo("[T_Burst, T_Core, T_Embers]"), Entries(report));
        }

        [Test]
        public void InvalidEnums_ReportActualValueAndExplicitContractOrderedAllowedRanges()
        {
            AssertEnumRange(Fixture("valid-fireball.json").Replace("\"dimension\": \"2d\"", "\"dimension\": \"sprite\""), "/dimension", "sprite", "[2d, 3d]");
            AssertEnumRange(Fixture("valid-fireball.json").Replace("\"archetype\": \"projectile\"", "\"archetype\": \"unknown_archetype\""), "/archetype", "unknown_archetype", "[projectile, impact, slash, aura, area, beam, trail, shield, spawn, transform, composite, environment, screen_ui, status, decal, weapon_trail, destruction, lifecycle, portal, loot]");
            AssertEnumRange(Fixture("valid-fireball.json").Replace("\"style\": \"stylized\"", "\"style\": \"realistic\""), "/style", "realistic", "[stylized]");
            AssertEnumRange(Fixture("valid-fireball.json").Replace("\"targetProfile\": \"mobile_medium\"", "\"targetProfile\": \"console\""), "/targetProfile", "console", "[mobile_medium, pc_editor]");
            AssertEnumRange(Fixture("valid-fireball.json").Replace("\"trigger\": \"on_launch\"", "\"trigger\": \"on_timer\""), "/stages/travel/trigger", "on_timer", "[manual, after_previous, on_launch, on_hit, on_end]");
            AssertEnumRange(Fixture("valid-fireball.json").Replace("\"kind\": \"energy_body\"", "\"kind\": \"particle\""), "/stages/travel/modules/core/kind", "particle", "[energy_body, sprite_emitter, secondary_particles, motion_trail, impact_flash, impact_burst, shockwave, sub_effect]");
            var manifest = "{\"manifestVersion\":1,\"templateId\":\"T\",\"templateVersion\":\"1\",\"kind\":\"energy_body\",\"dimension\":\"2d\",\"assetGuid\":\"g\",\"assetPath\":\"Assets/T.prefab\",\"tags\":[],\"parameters\":{\"scale\":{\"type\":\"number\",\"min\":0,\"max\":1,\"default\":0,\"binding\":\"x\"}},\"cost\":{\"estimatedPeakParticles\":0,\"materials\":0,\"trails\":0}}";
            var entry = VfxDomainParser.ParseManifest(manifest).Report.Entries.Find(value => value.Code == "E103");
            Assert.That(entry.Path, Is.EqualTo("/parameters/scale/type")); Assert.That((string)entry.ActualValue, Is.EqualTo("number")); Assert.That(entry.AllowedRange, Is.EqualTo("[float, integer, boolean, string]"));
        }

        [Test]
        public void TemplateKindDimensionAndAttachTo_AreValidated()
        {
            var json = Fixture("invalid-kind-and-attach.json");
            var report = RecipeValidator.Validate(json, Catalog());
            Assert.That(report.Contains("E309", "/stages/travel/modules/core/kind"), Is.True, Entries(report));
            Assert.That(report.Contains("E306", "/stages/travel/modules/embers/attachTo"), Is.True, Entries(report));
            var wrongDimensionCatalog = CatalogWithManifest(Manifest("T_Core", "energy_body", "3d", 10, 1, 0));
            var dimensionReport = RecipeValidator.Validate(Fixture("valid-fireball.json"), wrongDimensionCatalog);
            Assert.That(dimensionReport.Contains("E310", "/stages/travel/modules/core/templateId"), Is.True, Entries(dimensionReport));
        }

        [Test]
        public void AttachTo_CannotCrossStage()
        {
            var json = Fixture("valid-fireball.json").Replace("\"attachTo\": \"core\"", "\"attachTo\": \"launchFlash\"");
            var report = RecipeValidator.Validate(json, Catalog());
            Assert.That(report.Contains("E306", "/stages/travel/modules/embers/attachTo"), Is.True, Entries(report));
        }

        [Test]
        public void AttachTo_RejectsSelfAndSameStageCycles()
        {
            var selfRoot = JObject.Parse(Fixture("valid-fireball.json"));
            var selfModules = selfRoot["stages"].Children<JObject>().First(stage => (string)stage["id"] == "travel")["modules"].Children<JObject>().ToList();
            selfModules.First(module => (string)module["id"] == "embers")["attachTo"] = "embers";
            var selfReport = RecipeValidator.Validate(selfRoot.ToString(), Catalog());
            Assert.That(selfReport.Contains("E306", "/stages/travel/modules/embers/attachTo"), Is.True, Entries(selfReport));
            Assert.That(selfReport.Entries.FindAll(entry => entry.Code == "E306").Count, Is.EqualTo(1), Entries(selfReport));

            var root = JObject.Parse(Fixture("valid-fireball.json")); var modules = root["stages"].Children<JObject>().First(stage => (string)stage["id"] == "travel")["modules"].Children<JObject>().ToList();
            modules.First(module => (string)module["id"] == "core")["attachTo"] = "embers"; modules.First(module => (string)module["id"] == "embers")["attachTo"] = "core";
            var cycleReport = RecipeValidator.Validate(root.ToString(), Catalog());
            Assert.That(cycleReport.Contains("E306", "/stages/travel/modules/core/attachTo"), Is.True, Entries(cycleReport));
            Assert.That(cycleReport.Contains("E306", "/stages/travel/modules/embers/attachTo"), Is.True, Entries(cycleReport));
        }

        [Test]
        public void DuplicateModuleId_DoesNotThrowDuringCycleValidation()
        {
            var json = Fixture("valid-fireball.json").Replace("\"id\": \"embers\"", "\"id\": \"core\"");
            ValidationReport report = null;
            Assert.DoesNotThrow(() => report = RecipeValidator.Validate(json, Catalog()));
            Assert.That(report.Entries.Exists(entry => entry.Code == "E305"), Is.True, Entries(report));
        }

        private static void AssertEnumRange(string json, string path, string actual, string allowed)
        {
            var entry = VfxDomainParser.ParseRecipe(json).Report.Entries.Find(value => value.Code == "E103" && value.Path == path);
            Assert.That(entry, Is.Not.Null, path);
            Assert.That((string)entry.ActualValue, Is.EqualTo(actual));
            Assert.That(entry.AllowedRange, Is.EqualTo(allowed));
        }

        [Test]
        public void Catalog_RejectsDuplicateIdsAndBadGuidPathResolution()
        {
            var duplicate = TemplateCatalog.FromManifestJson(new[] { Manifest("T_Core", "energy_body", "2d", 10, 1, 0), Manifest("T_Core", "energy_body", "2d", 10, 1, 0) });
            Assert.That(duplicate.Report.Entries.Exists(entry => entry.Code == "E201"), Is.True, Entries(duplicate.Report));
            var mismatch = TemplateCatalog.FromManifestJson(new[] { Manifest("T_Core", "energy_body", "2d", 10, 1, 0) }, new FixedResolver(true, "Assets/Other.prefab"));
            Assert.That(mismatch.Report.Entries.Exists(entry => entry.Code == "E203"), Is.True, Entries(mismatch.Report));
            Assert.That(mismatch.ByTemplateId.ContainsKey("T_Core"), Is.False, "A resolver-mismatched manifest must not enter the catalog.");
            var missing = TemplateCatalog.FromManifestJson(new[] { Manifest("T_Core", "energy_body", "2d", 10, 1, 0) }, new FixedResolver(false, null));
            Assert.That(missing.Report.Entries.Exists(entry => entry.Code == "E202"), Is.True, Entries(missing.Report));
            Assert.That(missing.ByTemplateId.ContainsKey("T_Core"), Is.False, "An unresolved manifest must not enter the catalog.");
        }

        [Test]
        public void ManifestParser_RejectsUnknownRootAndParameterContractFields()
        {
            var parsed = VfxDomainParser.ParseManifest(Fixture("invalid-manifest-unknown-field.json"));
            Assert.That(parsed.Report.HasErrors, Is.True, Entries(parsed.Report));
            Assert.That(parsed.Report.Contains("E100", "/hallucinated"), Is.True, Entries(parsed.Report));
            Assert.That(parsed.Report.Contains("E100", "/parameters/scale/hallucinated"), Is.True, Entries(parsed.Report));
        }

        [Test]
        public void RecipeUnknownFields_UseStableStageAndModuleIdPaths()
        {
            var parsed = VfxDomainParser.ParseRecipe(Fixture("invalid-stable-unknown-path.json"));
            Assert.That(parsed.Report.Contains("E100", "/stages/travel/stageSurprise"), Is.True, Entries(parsed.Report));
            Assert.That(parsed.Report.Contains("E100", "/stages/travel/modules/core/moduleSurprise"), Is.True, Entries(parsed.Report));
        }

        [Test]
        public void InvalidManifests_AreReportedAndNeverIndexed()
        {
            TemplateCatalog catalog = null;
            Assert.DoesNotThrow(() => catalog = TemplateCatalog.FromManifestJson(new[] { Fixture("invalid-manifest-contract.json"), Fixture("invalid-manifest-malformed.json") }));
            Assert.That(catalog.Report.Entries.Exists(entry => entry.Code == "E204"), Is.True, Entries(catalog.Report));
            Assert.That(catalog.Report.Entries.Exists(entry => entry.Code == "E205"), Is.True, Entries(catalog.Report));
            Assert.That(catalog.Report.Entries.Exists(entry => entry.Code == "E206"), Is.True, Entries(catalog.Report));
            Assert.That(catalog.Report.Entries.Exists(entry => entry.Code == "E207"), Is.True, Entries(catalog.Report));
            Assert.That(catalog.Report.Entries.Exists(entry => entry.Code == "E208"), Is.True, Entries(catalog.Report));
            Assert.That(catalog.Report.Entries.Exists(entry => entry.Code == "E209"), Is.True, Entries(catalog.Report));
            Assert.That(catalog.Report.Entries.Exists(entry => entry.Code == "E104"), Is.True, Entries(catalog.Report));
            Assert.That(catalog.ByTemplateId.ContainsKey("T_Bad"), Is.False);
        }

        [Test]
        public void BooleanAndStringManifestParameters_UseV1UnboundedContract()
        {
            var booleanWithBounds = "{\"manifestVersion\":1,\"templateId\":\"T_Bool\",\"templateVersion\":\"1\",\"kind\":\"energy_body\",\"dimension\":\"2d\",\"assetGuid\":\"guid-bool\",\"assetPath\":\"Assets/VFX/Templates/T_Bool.prefab\",\"tags\":[],\"parameters\":{\"enabled\":{\"type\":\"boolean\",\"min\":0,\"default\":true,\"binding\":\"test.enabled\"}},\"cost\":{\"estimatedPeakParticles\":0,\"materials\":0,\"trails\":0}}";
            var stringWrongDefault = "{\"manifestVersion\":1,\"templateId\":\"T_String\",\"templateVersion\":\"1\",\"kind\":\"energy_body\",\"dimension\":\"2d\",\"assetGuid\":\"guid-string\",\"assetPath\":\"Assets/VFX/Templates/T_String.prefab\",\"tags\":[],\"parameters\":{\"label\":{\"type\":\"string\",\"default\":1,\"binding\":\"test.label\"}},\"cost\":{\"estimatedPeakParticles\":0,\"materials\":0,\"trails\":0}}";
            var catalog = TemplateCatalog.FromManifestJson(new[] { booleanWithBounds, stringWrongDefault });
            Assert.That(catalog.Report.Entries.Exists(entry => entry.Code == "E210"), Is.True, Entries(catalog.Report));
            Assert.That(catalog.ByTemplateId.Count, Is.EqualTo(0));
        }

        [Test]
        public void NonFiniteRecipeNumbers_AreRejectedByParserAndValidator()
        {
            var nonFiniteDuration = RecipeValidator.Validate(Fixture("invalid-nonfinite-duration.json"), Catalog());
            Assert.That(nonFiniteDuration.Entries.Exists(entry => entry.Code == "E105"), Is.True, Entries(nonFiniteDuration));
            var nonFiniteParameter = RecipeValidator.Validate(Fixture("invalid-nonfinite-parameter.json"), Catalog());
            Assert.That(nonFiniteParameter.Contains("E315", "/stages/travel/modules/core/parameters/scale"), Is.True, Entries(nonFiniteParameter));
        }

        [Test]
        public void CatalogDirectoryScan_SortsFilesBeforeDuplicateDetection()
        {
            var directory = Path.Combine(Path.GetTempPath(), "vfxcomposer-s4-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "z.json"), Manifest("T_Core", "energy_body", "2d", 0, 1, 0, "scale", "float", "0.5", "3", "1"));
                File.WriteAllText(Path.Combine(directory, "a.json"), Manifest("T_Core", "energy_body", "2d", 0, 1, 0, "scale", "float", "0.5", "3", "1"));
                var catalog = TemplateCatalog.LoadFromDirectory(directory);
                Assert.That(catalog.Report.Contains("E201", "/catalog/z.json/templateId"), Is.True, Entries(catalog.Report));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void CanonicalHash_IgnoresWhitespaceAndObjectOrder_ButPreservesSemantics()
        {
            var first = "{\"b\":1.0,\"a\":[{\"z\":-0.0,\"x\":true}]}";
            var equivalent = " { \"a\" : [ { \"x\" : true, \"z\" : 0 } ], \"b\" : 1 } ";
            var changed = "{\"a\":[{\"x\":false,\"z\":0}],\"b\":1}";
            Assert.That(RecipeCanonicalizer.ComputeSha256(first), Is.EqualTo(RecipeCanonicalizer.ComputeSha256(equivalent)));
            Assert.That(RecipeCanonicalizer.ComputeSha256(first), Is.Not.EqualTo(RecipeCanonicalizer.ComputeSha256(changed)));
        }

        [Test]
        public void Budget_ReportsErrorsWarningsAndInfo()
        {
            var parsed = VfxDomainParser.ParseRecipe(Fixture("valid-fireball.json"));
            var catalog = Catalog();
            var errors = BudgetCalculator.Evaluate(parsed.Value, catalog, new BudgetProfile { Id = "strict", MaxPeakParticles = 1, MaxMaterials = 1, MaxTrails = 0, MaxTotalDuration = 0.1 });
            Assert.That(errors.HasErrors, Is.True, Entries(errors));
            Assert.That(errors.Entries.Exists(entry => entry.Code == "E401"), Is.True, Entries(errors));
            var warning = BudgetCalculator.Evaluate(parsed.Value, catalog, new BudgetProfile { Id = "warning", MaxPeakParticles = 60, MaxMaterials = 8, MaxTrails = 3, MaxTotalDuration = 10 });
            Assert.That(warning.Entries.Exists(entry => entry.Code == "W401" && entry.Severity == ValidationSeverity.Warning), Is.True, Entries(warning));
            Assert.That(warning.Entries.Exists(entry => entry.Code == "I400" && entry.Severity == ValidationSeverity.Info), Is.True, Entries(warning));
        }

        private static TemplateCatalog Catalog()
        {
            return TemplateCatalog.FromManifestJson(new[]
            {
                Manifest("T_Core", "energy_body", "2d", 0, 1, 0, "scale", "float", "0.5", "3", "1.2"),
                Manifest("T_Embers", "secondary_particles", "2d", 24, 1, 0, "rate", "float", "0", "100", "18", "lifetime", "float", "0.1", "3", "0.55"),
                Manifest("T_Burst", "impact_burst", "2d", 24, 1, 0, "count", "integer", "1", "100", "24", "speed", "float", "0.1", "10", "3.5")
            });
        }

        private static TemplateCatalog CatalogWithManifest(string replacement)
        {
            return TemplateCatalog.FromManifestJson(new[]
            {
                replacement,
                Manifest("T_Embers", "secondary_particles", "2d", 24, 1, 0, "rate", "float", "0", "100", "18", "lifetime", "float", "0.1", "3", "0.55"),
                Manifest("T_Burst", "impact_burst", "2d", 24, 1, 0, "count", "integer", "1", "100", "24", "speed", "float", "0.1", "10", "3.5")
            });
        }

        private static string Manifest(string templateId, string kind, string dimension, int particles, int materials, int trails, params string[] parameters)
        {
            var declaration = new System.Text.StringBuilder();
            for (var index = 0; index < parameters.Length; index += 5)
            {
                if (index > 0) declaration.Append(',');
                declaration.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "\"{0}\":{{\"type\":\"{1}\",\"min\":{2},\"max\":{3},\"default\":{4},\"binding\":\"test.{0}\"}}", parameters[index], parameters[index + 1], parameters[index + 2], parameters[index + 3], parameters[index + 4]);
            }
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{{\"manifestVersion\":1,\"templateId\":\"{0}\",\"templateVersion\":\"1.0.0\",\"kind\":\"{1}\",\"dimension\":\"{2}\",\"assetGuid\":\"guid-{0}\",\"assetPath\":\"Assets/VFX/Templates/{0}.prefab\",\"tags\":[\"fire\"],\"parameters\":{{{3}}},\"cost\":{{\"estimatedPeakParticles\":{4},\"materials\":{5},\"trails\":{6}}}}}", templateId, kind, dimension, declaration, particles, materials, trails);
        }

        private static string Fixture(string name)
        {
            return File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "Packages", "com.vfxcomposer.unity", "Tests", "EditMode", "TestData", name));
        }

        private static string Entries(ValidationReport report)
        {
            return string.Join("; ", report.Entries.ConvertAll(entry => entry.Code + " " + entry.Path + " " + entry.Message).ToArray());
        }

        private sealed class FixedResolver : IAssetReferenceResolver
        {
            private readonly bool found;
            private readonly string path;
            public FixedResolver(bool found, string path) { this.found = found; this.path = path; }
            public AssetReferenceResolution Resolve(string assetGuid) { return new AssetReferenceResolution { Found = found, AssetPath = path }; }
        }
    }
}
