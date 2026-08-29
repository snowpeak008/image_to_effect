using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Catalog;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Tests.EditMode
{
    /// <summary>
    /// Restricted build entry point coverage: one real strict build that produces all three write-surface
    /// members, plus the ADR-007 §5 refusal paths. Every refusal case asserts zero writes, which is the
    /// property that makes the closed write surface meaningful.
    /// </summary>
    public sealed class VfxRecipeBuildEntrypointTests
    {
        private const string EffectId = "f2_build_probe";

        private string stagingRoot;

        [SetUp]
        public void SetUp()
        {
            stagingRoot = Path.Combine(Path.GetTempPath(), "vfxcomposer_f2_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(stagingRoot);
            DeleteWriteSurfaceMembers();
        }

        [TearDown]
        public void TearDown()
        {
            DeleteWriteSurfaceMembers();
            if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true);
        }

        [Test]
        public void AConfirmedRecipeBuildsThePrefabOwnershipManifestAndProvenanceRecipe()
        {
            var recipeJson = MinimalRecipe(EffectId);
            var outcome = VfxRecipeBuildEntrypoint.Execute(Request(recipeJson));

            Assert.That(outcome.Succeeded, Is.True, Describe(outcome));
            Assert.That(outcome.FailureCode, Is.Null);
            Assert.That(outcome.EffectId, Is.EqualTo(EffectId));
            Assert.That(outcome.DryRunState, Is.EqualTo("Create"));
            Assert.That(outcome.CompilerVersion, Is.EqualTo(VfxCompiler.CompilerVersion));
            Assert.That(outcome.DeclaredTemplateCatalogVersion, Is.EqualTo("1.0.0"));
            Assert.That(outcome.CatalogIdentityHash, Does.Match("^[0-9a-f]{64}$"));

            // Member 1: the Prefab and its in-root build manifest.
            Assert.That(outcome.PrefabPath, Is.EqualTo("Assets/VFX/Generated/" + EffectId + "/VFX_" + EffectId + ".prefab"));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(outcome.PrefabPath), Is.Not.Null);
            var buildManifest = ReadJson(outcome.BuildManifestPath);
            Assert.That((string)buildManifest["recipeHash"], Is.EqualTo(outcome.RecipeHash));
            Assert.That((string)buildManifest["buildHash"], Is.EqualTo(outcome.BuildHash));

            // Member 2: the authoritative ownership manifest single point.
            Assert.That(outcome.OwnershipManifestPath, Is.EqualTo("ProjectSettings/VFXComposer/BuildManifests/" + EffectId + ".manifest.json"));
            var ownership = ReadJson(outcome.OwnershipManifestPath);
            Assert.That((string)ownership["enforcement"], Is.EqualTo("strict"), "The probe id must exercise strict enforcement, not legacy audit.");
            Assert.That((string)ownership["recipeHash"], Is.EqualTo(outcome.RecipeHash));
            Assert.That((string)ownership["buildHash"], Is.EqualTo(outcome.BuildHash));
            Assert.That((string)ownership["sourceRecipePath"], Is.EqualTo(outcome.ProvenanceRecipePath), "Strict provenance must resolve to the recipe this build landed.");

            // Member 3: the build provenance recipe, canonical and hash-identical to the built input.
            Assert.That(outcome.ProvenanceRecipePath, Is.EqualTo("Assets/VFX/Recipes/" + EffectId + ".json"));
            var provenance = File.ReadAllText(Absolute(outcome.ProvenanceRecipePath));
            Assert.That(provenance, Is.EqualTo(RecipeCanonicalizer.Canonicalize(recipeJson)));
            Assert.That(RecipeCanonicalizer.ComputeSha256(provenance), Is.EqualTo(outcome.RecipeHash));

            // Idempotency (REQ-001-19): the same confirmed draft rebuilds as Unchanged with stable bytes.
            var prefabGuid = AssetDatabase.AssetPathToGUID(outcome.PrefabPath);
            var ownershipBytes = File.ReadAllBytes(Absolute(outcome.OwnershipManifestPath));
            var second = VfxRecipeBuildEntrypoint.Execute(Request(recipeJson));
            Assert.That(second.Succeeded, Is.True, Describe(second));
            Assert.That(second.DryRunState, Is.EqualTo("Unchanged"));
            Assert.That(second.BuildHash, Is.EqualTo(outcome.BuildHash));
            Assert.That(AssetDatabase.AssetPathToGUID(outcome.PrefabPath), Is.EqualTo(prefabGuid));
            CollectionAssert.AreEqual(ownershipBytes, File.ReadAllBytes(Absolute(outcome.OwnershipManifestPath)));
            AssertNoTemporaryResidue();
        }

        [Test]
        public void AStagedRecipeThatDoesNotMatchTheConfirmedHashIsRefusedWithZeroWrites()
        {
            var request = Request(MinimalRecipe(EffectId));
            request.ExpectedCanonicalSha256 = new string('0', 64);

            var outcome = VfxRecipeBuildEntrypoint.Execute(request);

            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.FailureCode, Is.EqualTo(VfxRecipeBuildCodes.ConfirmationHashMismatch));
            AssertNoWriteSurfaceMembers();
        }

        [Test]
        public void AnInProjectRecipeInputIsRefusedBecauseBuildInputsAreStagedOutsideTheProject()
        {
            var request = Request(MinimalRecipe(EffectId));
            request.RecipePath = Absolute("Assets/VFX/Recipes/fireball-2d.default.json");

            var outcome = VfxRecipeBuildEntrypoint.Execute(request);

            Assert.That(outcome.FailureCode, Is.EqualTo(VfxRecipeBuildCodes.RecipeInputRejected));
            AssertNoWriteSurfaceMembers();
        }

        [Test]
        public void AMissingStagedRecipeIsRefusedBeforeAnythingElseHappens()
        {
            var request = Request(MinimalRecipe(EffectId));
            request.RecipePath = Path.Combine(stagingRoot, "absent.json");

            Assert.That(VfxRecipeBuildEntrypoint.Execute(request).FailureCode, Is.EqualTo(VfxRecipeBuildCodes.RecipeInputRejected));
            AssertNoWriteSurfaceMembers();
        }

        [TestCase("con")]
        [TestCase("nul")]
        [TestCase("com1")]
        [TestCase("lpt9")]
        [TestCase("aux")]
        public void AReservedWindowsDeviceNameIsRefusedBeforeTheCompilerRuns(string reserved)
        {
            var outcome = VfxRecipeBuildEntrypoint.Execute(Request(MinimalRecipe(reserved)));

            Assert.That(outcome.FailureCode, Is.EqualTo(VfxRecipeBuildCodes.EffectIdRejected), Describe(outcome));
            Assert.That(File.Exists(Absolute("Assets/VFX/Recipes/" + reserved + ".json")), Is.False);
            Assert.That(AssetDatabase.IsValidFolder("Assets/VFX/Generated/" + reserved), Is.False);
            Assert.That(File.Exists(Absolute("ProjectSettings/VFXComposer/BuildManifests/" + reserved + ".manifest.json")), Is.False);
        }

        [TestCase("Fireball2D", TestName = "UppercaseIsRefused")]
        [TestCase("fireball-2d", TestName = "HyphenIsRefused")]
        [TestCase("_leading", TestName = "LeadingUnderscoreIsRefused")]
        [TestCase("trailing_", TestName = "TrailingUnderscoreIsRefused")]
        [TestCase("double__underscore", TestName = "DoubledUnderscoreIsRefused")]
        [TestCase("../escape", TestName = "TraversalIsRefused")]
        [TestCase("nested/id", TestName = "SeparatorIsRefused")]
        [TestCase("C:/absolute", TestName = "AbsolutePathIsRefused")]
        [TestCase("back\\slash", TestName = "BackslashIsRefused")]
        [TestCase("con", TestName = "ReservedDeviceNameIsRefused")]
        public void UnsafeEffectIdsAreRefusedByTheExecutorLayerGuard(string effectId)
        {
            string error;
            Assert.That(VfxRecipeBuildWriteSurface.IsAcceptedEffectId(effectId, out error), Is.False, effectId);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void AnOverlongEffectIdIsRefused()
        {
            string error;
            var accepted = new string('a', VfxRecipeBuildWriteSurface.MaximumEffectIdLength);
            Assert.That(VfxRecipeBuildWriteSurface.IsAcceptedEffectId(accepted, out error), Is.True, error);
            Assert.That(VfxRecipeBuildWriteSurface.IsAcceptedEffectId(accepted + "a", out error), Is.False);
        }

        [Test]
        public void TheClosedWriteSurfaceRefusesEveryTargetOutsideItsThreeMembers()
        {
            Assert.That(VfxRecipeBuildWriteSurface.IsInsideWriteSurface("Assets/VFX/Generated/x/VFX_x.prefab"), Is.True);
            Assert.That(VfxRecipeBuildWriteSurface.IsInsideWriteSurface("Assets/VFX/Generated/x/BuildManifest.json"), Is.True);
            Assert.That(VfxRecipeBuildWriteSurface.IsInsideWriteSurface("ProjectSettings/VFXComposer/BuildManifests/x.manifest.json"), Is.True);
            Assert.That(VfxRecipeBuildWriteSurface.IsInsideWriteSurface("Assets/VFX/Recipes/x.json"), Is.True);

            foreach (var refused in new[]
            {
                "Assets/VFX/Shared/Styles/Materials/MAT_Style_cartoon.mat",
                "Assets/VFX/Templates/2D/Manifests/PFT_2D_FireCore.manifest.json",
                "Assets/VFX/Recipes/Projectile/x.json",
                "Assets/VFX/Recipes/x.txt",
                "Assets/VFX/Recipes/con.json",
                "ProjectSettings/VFXComposer/VfxProjectRules.json",
                "ProjectSettings/EditorBuildSettings.asset",
                "ProjectSettings/VFXComposer/BuildManifests/x.json",
                "Packages/com.vfxcomposer.unity/Editor/Build/VfxCompiler.cs",
                "Assets/VFX/Generated/../Shared/x.mat",
                "../outside.json",
                "C:/temp/x.json",
                "Assets\\VFX\\Generated\\x\\y.prefab",
                "",
            })
            {
                Assert.That(VfxRecipeBuildWriteSurface.IsInsideWriteSurface(refused), Is.False, refused);
            }
        }

        [Test]
        public void AnUnknownTemplateIsRefusedByAuthoritativeValidationWithZeroWrites()
        {
            var recipe = JObject.Parse(MinimalRecipe(EffectId));
            var travel = ((JArray)recipe["stages"]).Children<JObject>().Single(stage => (string)stage["id"] == "travel");
            ((JObject)((JArray)travel["modules"])[0])["templateId"] = "not_registered";

            var outcome = VfxRecipeBuildEntrypoint.Execute(Request(recipe.ToString()));

            Assert.That(outcome.FailureCode, Is.EqualTo(VfxRecipeBuildCodes.AuthoritativeValidationFailed), Describe(outcome));
            Assert.That(outcome.Issues.Any(issue => issue.Code == "E308"), Is.True, Describe(outcome));
            AssertNoWriteSurfaceMembers();
        }

        [Test]
        public void AnUnparseableRecipeIsRefusedWithZeroWrites()
        {
            var recipe = JObject.Parse(MinimalRecipe(EffectId));
            recipe["hallucinatedField"] = true;

            var outcome = VfxRecipeBuildEntrypoint.Execute(Request(recipe.ToString()));

            Assert.That(outcome.FailureCode, Is.EqualTo(VfxRecipeBuildCodes.RecipeUnparseable), Describe(outcome));
            AssertNoWriteSurfaceMembers();
        }

        [Test]
        public void OrphanCompilerTemporaryDirectoriesAndPendingResidueAreSweptWithoutTouchingRealOutputs()
        {
            var orphan = "Assets/VFX/Generated/vfxs6tmp_f2probe";
            var keep = "Assets/VFX/Generated/f2_build_probe_keep";
            AssetDatabase.CreateFolder(VfxRecipeBuildWriteSurface.GeneratedRoot, "vfxs6tmp_f2probe");
            AssetDatabase.CreateFolder(VfxRecipeBuildWriteSurface.GeneratedRoot, "f2_build_probe_keep");
            var pending = Absolute(keep + "/BuildManifest.json.pending");
            File.WriteAllText(pending, "{}", new UTF8Encoding(false));
            try
            {
                var cleaned = new List<string>();
                string error;
                Assert.That(VfxRecipeBuildWriteSurface.TrySweepResidue(EffectId, cleaned, out error), Is.True, error);
                Assert.That(AssetDatabase.IsValidFolder(orphan), Is.False, "A known compiler temporary must be swept.");
                Assert.That(AssetDatabase.IsValidFolder(keep), Is.True, "A non-temporary generated folder must never be swept.");
                Assert.That(File.Exists(pending), Is.False, "Pending residue under the generated root must be swept.");
                Assert.That(cleaned, Does.Contain(orphan));
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(orphan)) AssetDatabase.DeleteAsset(orphan);
                if (AssetDatabase.IsValidFolder(keep)) AssetDatabase.DeleteAsset(keep);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void AnUnknownRequestSchemaOrFieldIsRefusedWithoutExecutingAnything()
        {
            var path = Path.Combine(stagingRoot, "request.json");
            string code;
            string error;

            File.WriteAllText(path, "{\"schemaVersion\":\"vfxcomposer.recipe-build-request/2\"}", new UTF8Encoding(false));
            Assert.That(VfxRecipeBuildJson.TryReadRequest(path, out code, out error), Is.Null);
            Assert.That(code, Is.EqualTo(VfxRecipeBuildCodes.RequestInvalid));

            File.WriteAllText(
                path,
                "{\"schemaVersion\":\"" + VfxRecipeBuildRequest.SchemaVersion + "\",\"draftId\":\"d\",\"recipePath\":\"x\"," +
                "\"expectedCanonicalSha256\":\"" + new string('a', 64) + "\",\"surprise\":1}",
                new UTF8Encoding(false));
            Assert.That(VfxRecipeBuildJson.TryReadRequest(path, out code, out error), Is.Null);
            Assert.That(code, Is.EqualTo(VfxRecipeBuildCodes.RequestInvalid));

            File.WriteAllText(
                path,
                "{\"schemaVersion\":\"" + VfxRecipeBuildRequest.SchemaVersion + "\",\"draftId\":\"d\",\"recipePath\":\"x\"," +
                "\"expectedCanonicalSha256\":\"NOTAHASH\"}",
                new UTF8Encoding(false));
            Assert.That(VfxRecipeBuildJson.TryReadRequest(path, out code, out error), Is.Null);
            Assert.That(code, Is.EqualTo(VfxRecipeBuildCodes.RequestInvalid));

            Assert.That(VfxRecipeBuildJson.TryReadRequest(Path.Combine(stagingRoot, "absent.json"), out code, out error), Is.Null);
            Assert.That(code, Is.EqualTo(VfxRecipeBuildCodes.RequestUnreadable));
        }

        [Test]
        public void TheCatalogIdentityHashTracksTemplateIdentityAndNotRecipeContent()
        {
            var formal = VfxRecipeBuildEntrypoint.ComputeCatalogIdentityHash(VfxCompiler.LoadFormalCatalog());
            Assert.That(formal, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(VfxRecipeBuildEntrypoint.ComputeCatalogIdentityHash(VfxCompiler.LoadFormalCatalog()), Is.EqualTo(formal));

            const string manifest = "{\"manifestVersion\":1,\"templateId\":\"T_Probe\",\"templateVersion\":\"{0}\",\"kind\":\"energy_body\"," +
                "\"dimension\":\"2d\",\"assetGuid\":\"00000000000000000000000000000001\"," +
                "\"assetPath\":\"Assets/VFX/Templates/2D/Prefabs/T_Probe.prefab\",\"tags\":[\"probe\"]," +
                "\"parameters\":{\"scale\":{\"type\":\"float\",\"min\":0.1,\"max\":4,\"default\":1,\"binding\":\"core.scale\"}}," +
                "\"cost\":{\"estimatedPeakParticles\":0,\"materials\":1,\"trails\":0}}";
            var baseline = TemplateCatalog.FromManifestJson(new[] { manifest.Replace("{0}", "1.0.0") });
            Assert.That(baseline.Report.HasErrors, Is.False, DescribeReport(baseline.Report));
            Assert.That(baseline.ByTemplateId.Count, Is.EqualTo(1));

            var first = VfxRecipeBuildEntrypoint.ComputeCatalogIdentityHash(baseline);
            var bumped = VfxRecipeBuildEntrypoint.ComputeCatalogIdentityHash(
                TemplateCatalog.FromManifestJson(new[] { manifest.Replace("{0}", "1.0.1") }));
            Assert.That(bumped, Is.Not.EqualTo(first), "A template version bump must move the catalog identity.");
        }

        private VfxRecipeBuildRequest Request(string recipeJson)
        {
            var path = Path.Combine(stagingRoot, "staged-recipe.json");
            File.WriteAllText(path, recipeJson, new UTF8Encoding(false));
            return new VfxRecipeBuildRequest
            {
                DraftId = "draft-f2probe",
                RecipePath = path,
                ExpectedCanonicalSha256 = RecipeCanonicalizer.ComputeSha256(recipeJson),
                DeclaredTemplateCatalogVersion = "1.0.0"
            };
        }

        /// <summary>
        /// The smallest formal projectile a strict build can actually commit. The generated Prefab must
        /// carry all three stage roots (the Runtime controller wiring check demands launch/travel/impact),
        /// while the strict structure budget allows at most two cloned local materials — one per
        /// renderer-bearing module. A single module and no attachTo chain therefore keeps depth at the
        /// module level and stays well inside every budget.
        /// </summary>
        private static string MinimalRecipe(string effectId)
        {
            return new JObject
            {
                ["recipeVersion"] = 1,
                ["revision"] = 1,
                ["id"] = effectId,
                ["name"] = "F2 Build Probe",
                ["dimension"] = "2d",
                ["archetype"] = "projectile",
                ["targetProfile"] = "mobile_medium",
                ["randomSeed"] = 20260829,
                ["stages"] = new JArray
                {
                    Stage("launch", "on_launch", 0.1, new JArray()),
                    Stage("travel", "after_previous", 1.0, new JArray
                    {
                        new JObject
                        {
                            ["id"] = "core",
                            ["kind"] = "energy_body",
                            ["templateId"] = "PFT_2D_FireCore",
                            ["parameters"] = new JObject { ["scale"] = 1.2 },
                            ["enabled"] = true
                        }
                    }),
                    Stage("impact", "on_hit", 0.2, new JArray())
                },
                ["metadata"] = new JObject { ["createdBy"] = "f2-editmode-test", ["templateCatalogVersion"] = "1.0.0" }
            }.ToString();
        }

        private static JObject Stage(string id, string trigger, double duration, JArray modules)
        {
            return new JObject
            {
                ["id"] = id,
                ["trigger"] = trigger,
                ["duration"] = duration,
                ["enabled"] = true,
                ["modules"] = modules
            };
        }

        private static string Absolute(string projectRelativePath)
        {
            return Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static JObject ReadJson(string projectRelativePath)
        {
            var absolute = Absolute(projectRelativePath);
            Assert.That(File.Exists(absolute), Is.True, "Expected write-surface member is missing: " + projectRelativePath);
            return JObject.Parse(File.ReadAllText(absolute));
        }

        private static string DescribeReport(VFXComposer.Editor.Domain.ValidationReport report)
        {
            return string.Join(
                " | ",
                report.Entries.Select(entry => entry.Code + " " + entry.Path + " " + entry.Message).ToArray());
        }

        private static string Describe(VfxRecipeBuildOutcome outcome)
        {
            var builder = new StringBuilder("code=").Append(outcome.FailureCode ?? "<none>");
            foreach (var issue in outcome.Issues)
            {
                builder.Append(" | ").Append(issue.Code).Append(' ').Append(issue.Path).Append(' ').Append(issue.Message);
            }

            return builder.ToString();
        }

        private static void AssertNoWriteSurfaceMembers()
        {
            Assert.That(AssetDatabase.IsValidFolder("Assets/VFX/Generated/" + EffectId), Is.False, "A refused build must not create the generated asset root.");
            Assert.That(File.Exists(Absolute("Assets/VFX/Recipes/" + EffectId + ".json")), Is.False, "A refused build must not land build provenance.");
            Assert.That(File.Exists(Absolute("ProjectSettings/VFXComposer/BuildManifests/" + EffectId + ".manifest.json")), Is.False, "A refused build must not write the ownership manifest.");
        }

        private static void AssertNoTemporaryResidue()
        {
            var lingering = AssetDatabase.GetSubFolders(VfxRecipeBuildWriteSurface.GeneratedRoot)
                .Where(folder => VfxRecipeBuildWriteSurface.IsKnownTemporaryDirectory(Path.GetFileName(folder)))
                .ToArray();
            Assert.That(lingering, Is.Empty);
        }

        private static void DeleteWriteSurfaceMembers()
        {
            var generated = "Assets/VFX/Generated/" + EffectId;
            if (AssetDatabase.IsValidFolder(generated)) AssetDatabase.DeleteAsset(generated);
            foreach (var id in new[] { EffectId, "con", "nul", "com1", "lpt9", "aux" })
            {
                var provenance = "Assets/VFX/Recipes/" + id + ".json";
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(provenance) != null) AssetDatabase.DeleteAsset(provenance);
                var absolute = Absolute(provenance);
                if (File.Exists(absolute)) File.Delete(absolute);
                if (File.Exists(absolute + ".meta")) File.Delete(absolute + ".meta");
                var ownership = Absolute("ProjectSettings/VFXComposer/BuildManifests/" + id + ".manifest.json");
                if (File.Exists(ownership)) File.Delete(ownership);
            }

            AssetDatabase.Refresh();
        }
    }
}
