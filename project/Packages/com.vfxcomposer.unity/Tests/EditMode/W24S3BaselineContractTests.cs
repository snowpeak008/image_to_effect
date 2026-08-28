using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Validation;
using VFXComposer.Editor.W24.S1;
using VFXComposer.Editor.W24.S3;
using VFXComposer.Editor.W24.S5;
using VFXComposer.W24;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W24S3BaselineContractTests
    {
        private static readonly string[] ContractFiles =
        {
            "w24_moving_projectile_trail.contract.json",
            "w24_weapon_socket_fragments.contract.json",
            "w24_real_light_receivers.contract.json"
        };

        [Test]
        public void EveryS3Baseline_DeclaresAllTenContractSegments_AndStaysVisualPending()
        {
            var root = RepositoryRoot();
            foreach (var file in ContractFiles)
            {
                var json = JObject.Parse(File.ReadAllText(Path.Combine(root, "docs", "vfx-contracts", file)));
                foreach (var segment in new[] { "contractVersion", "effectId", "contractRevision", "contractHash", "lifecycle", "references", "spatialContract", "captureProfile", "semanticStateMachine", "layers", "allowedSubstitutions", "forbiddenSubstitutions", "cleanup", "budget", "requirements" })
                    Assert.NotNull(json[segment], file + " is missing " + segment);
                Assert.IsNull(json["implementationTrace"], file + " must reference an independent Trace authority rather than embed one at the contract top level.");
                Assert.That((string)json["extensions"]["visualStatus"], Is.EqualTo("VISUAL_PENDING"));
                var traceRelative = (string)json["extensions"]["implementationTrace"];
                Assert.That(traceRelative, Does.StartWith("docs/vfx-traces/"));
                Assert.That(File.Exists(Path.Combine(root, traceRelative.Replace('/', Path.DirectorySeparatorChar))), Is.True, traceRelative);
                Assert.That(((JArray)json["requirements"]).Any(requirement => (string)requirement["evidenceAuthority"] == "user"), Is.True);
                Assert.That(((JArray)json["requirements"]).All(requirement => (string)requirement["evidenceAuthority"] != "calibrationLabels"), Is.True);
                VfxDesignContract parsed;
                var report = VfxDesignContractJson.ValidateJson(json.ToString(Newtonsoft.Json.Formatting.None), out parsed);
                Assert.That(report.HasErrors, Is.False, file + ": " + string.Join(" | ", report.Issues.Select(issue => issue.Code + " " + issue.Path + " " + issue.Message)));
            }
        }

        [Test]
        public void TypedDiagnosticPlans_FreezeHistoryViewsIdsThresholdsAndSeedConsumption()
        {
            var projectile = JObject.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "vfx-contracts", ContractFiles[0])));
            var trail = (JObject)projectile.SelectToken("extensions.typedDiagnostics.trailCorridor");
            Assert.That((string)trail["requirementId"], Is.EqualTo("REQ-B-TRAIL-CORRIDOR"));
            Assert.That((string)trail.SelectToken("artifact.graphicsFormat"), Is.EqualTo("R8_UNorm"));
            Assert.That((string)trail.SelectToken("artifact.npyDtype"), Is.EqualTo("|u1"));
            Assert.That((bool)trail.SelectToken("artifact.beautyFallbackAllowed"), Is.False);
            Assert.That((string)trail["historySource"], Does.Contain("W24MovingEmitterTrailProtocol"));
            Assert.That((string)trail["historySource"], Does.Contain("forbidden"));
            Assert.That((double)trail.SelectToken("thresholds.corridorCoverageMinimum"), Is.GreaterThan(0));
            Assert.That(((JArray)trail.SelectToken("seedConsumptionPlan.orderedSeeds")).Values<uint>(), Is.EqualTo(new uint[] { 24101u, 24111u, 24121u }));

            var binding = JObject.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "vfx-contracts", ContractFiles[1])));
            var diagnostic = (JObject)binding.SelectToken("extensions.typedDiagnostics.objectIdDepth");
            Assert.That(((JArray)diagnostic["frozenViews"]).Select(item => (string)item["viewId"]), Is.EqualTo(new[] { "binding_front_main", "binding_oblique" }));
            Assert.That(((JArray)diagnostic["requiredObjectIds"]).Select(item => (uint)item["id"]), Is.EqualTo(new uint[] { 10u, 101u, 201u, 202u, 203u }));
            Assert.That((int)diagnostic.SelectToken("formalDiagnosticLayer.index"), Is.EqualTo(W24S3BaselineAuthoring.FormalDiagnosticLayer));
            Assert.That((bool)diagnostic.SelectToken("formalDiagnosticLayer.beautyCameraExcluded"), Is.True);
            Assert.That((bool)diagnostic.SelectToken("missingBindingProbePlan.anchorOrRendererFallbackAllowed"), Is.False);
            Assert.That((string)diagnostic.SelectToken("missingBindingProbePlan.expectedFaults.missing_bone"), Is.EqualTo("MissingBone"));
            Assert.That(((JArray)diagnostic.SelectToken("seedConsumptionPlan.orderedSeeds")).Values<uint>(), Is.EqualTo(new uint[] { 24201u, 24211u, 24221u }));
            Assert.That((string)binding.SelectToken("extensions.visualStatus"), Is.EqualTo("VISUAL_PENDING"));
        }

        [Test]
        public void TypedDiagnosticRequiredEvidenceMatrices_AreExactAndRejectMissingPassViewAndReceiverOffRows()
        {
            var root = RepositoryRoot();
            var projectile = JObject.Parse(File.ReadAllText(Path.Combine(root, "docs", "vfx-contracts", ContractFiles[0])));
            var binding = JObject.Parse(File.ReadAllText(Path.Combine(root, "docs", "vfx-contracts", ContractFiles[1])));
            var lighting = JObject.Parse(File.ReadAllText(Path.Combine(root, "docs", "vfx-contracts", ContractFiles[2])));
            Assert.That((int)projectile["contractRevision"], Is.GreaterThanOrEqualTo(7));
            Assert.That((int)binding["contractRevision"], Is.GreaterThanOrEqualTo(7));
            Assert.That((int)lighting["contractRevision"], Is.GreaterThanOrEqualTo(6));
            var b = (JArray)projectile.SelectToken("extensions.typedDiagnostics.requiredEvidenceMatrix");
            var c = (JArray)binding.SelectToken("extensions.typedDiagnostics.requiredEvidenceMatrix");
            var d = (JArray)lighting.SelectToken("extensions.typedDiagnostics.requiredEvidenceMatrix");
            Assert.That(ProjectileMatrixComplete(b), Is.True, "B must freeze exactly 3 seeds × 3 trail frames = 9 typed raw rows.");
            Assert.That(BindingMatrixComplete(c), Is.True, "C must freeze exactly 3 seeds × (3 fragment-ID + 2 view × object-ID/depth) = 21 typed raw rows.");
            Assert.That(ReceiverMatrixComplete(d), Is.True, "D must freeze exactly 3 seeds × effect-mask/receiver-ID/off/on = 12 typed raw rows.");
            var environments = new[]
            {
                projectile.SelectToken("extensions.typedDiagnostics.metricsEnvironment") as JObject,
                binding.SelectToken("extensions.typedDiagnostics.metricsEnvironment") as JObject,
                lighting.SelectToken("extensions.typedDiagnostics.metricsEnvironment") as JObject
            };
            Assert.That(environments.All(MetricsEnvironmentComplete), Is.True, "Every S3 Contract must freeze the replayable Python/NumPy/Pillow probe object accepted by the metrics bridge.");
            var tamperedEnvironment = (JObject)environments[0].DeepClone();
            tamperedEnvironment["pillowVersion"] = "tampered";
            Assert.That(MetricsEnvironmentComplete(tamperedEnvironment), Is.False, "A changed dependency identity must not self-declare a passing metrics environment.");
            var missingObliqueObject = (JArray)c.DeepClone();
            missingObliqueObject.Remove(missingObliqueObject.OfType<JObject>().Single(row => (string)row["evidenceId"] == "c-object-id-24201-oblique-f72"));
            Assert.That(BindingMatrixComplete(missingObliqueObject), Is.False, "A whole required multiview pass row must fail closed rather than silently weaken C.");
            var missingPass = (JArray)c.DeepClone();
            missingPass.OfType<JObject>().Single(row => (string)row["evidenceId"] == "c-object-id-24201-front-f72")["passId"] = JValue.CreateNull();
            Assert.That(BindingMatrixComplete(missingPass), Is.False, "A row without its frozen pass must fail closed rather than infer Object-ID from its path.");
            var missingView = (JArray)c.DeepClone();
            missingView.OfType<JObject>().Single(row => (string)row["evidenceId"] == "c-object-id-24201-front-f72")["viewId"] = JValue.CreateNull();
            Assert.That(BindingMatrixComplete(missingView), Is.False, "A row without its frozen view must fail closed rather than infer a camera from frame identity.");
            var missingReceiverOff = (JArray)d.DeepClone();
            missingReceiverOff.Remove(missingReceiverOff.OfType<JObject>().Single(row => (string)row["evidenceId"] == "d-receiver-off-24301-f24"));
            Assert.That(ReceiverMatrixComplete(missingReceiverOff), Is.False, "A missing receiver-off row must fail closed rather than compare on/on or omit a seed.");
            var fragmentPlan = (JObject)binding.SelectToken("extensions.typedDiagnostics.fragmentTracks");
            var objectPlan = (JObject)binding.SelectToken("extensions.typedDiagnostics.objectIdDepth");
            Assert.That(((JArray)objectPlan["parallaxRequiredObjectIds"]).Values<uint>(), Is.EqualTo(new uint[] { 101u, 201u, 202u, 203u }), "The centred bound-model carrier is depth-verified; only the off-centre socket and fragments require centroid parallax.");
            Assert.That(((JArray)fragmentPlan["fragmentIds"]).Values<uint>(), Is.EqualTo(new uint[] { 201u, 202u, 203u }));
            Assert.That(((JArray)fragmentPlan["frames"]).Values<int>(), Is.EqualTo(new[] { 54, 63, 72 }));
            Assert.That((bool)fragmentPlan.SelectToken("thresholds.rejectSingleRigidBody"), Is.True);
            var receiverMappings = ((JArray)lighting.SelectToken("extensions.typedDiagnostics.receiverLuminanceLdr.perRequirementCheckMapping")).OfType<JObject>().ToArray();
            Assert.That(receiverMappings.Select(value => (string)value["requirementId"]), Is.EquivalentTo(new[] { "REQ-D-REAL-LIGHTS", "REQ-D-RECEIVER-A", "REQ-D-RECEIVER-B", "REQ-D-CLEANUP", "REQ-D-VISUAL" }));
            Assert.That(((JArray)receiverMappings.Single(value => (string)value["requirementId"] == "REQ-D-RECEIVER-A")["receiverIds"]).Values<int>(), Is.EqualTo(new[] { 11 }));
            Assert.That(((JArray)receiverMappings.Single(value => (string)value["requirementId"] == "REQ-D-RECEIVER-B")["receiverIds"]).Values<int>(), Is.EqualTo(new[] { 12 }));
            Assert.That(receiverMappings.Where(value => (string)value["requirementId"] != "REQ-D-RECEIVER-A" && (string)value["requirementId"] != "REQ-D-RECEIVER-B").All(value => ((JArray)value["receiverIds"]).Values<int>().SequenceEqual(new[] { 11, 12 })), Is.True, "Physical-light, cleanup and visual anti-fake cross-evidence must consume both receiver checks.");
            var metricsTests = File.ReadAllText(Path.Combine(root, "tools", "vfx", "tests", "test_render_metrics.py"));
            Assert.That(metricsTests, Does.Contain("rigid_result = fragment_tracks"), "The metrics test suite must retain a rigid-group negative fixture.");
            Assert.That(metricsTests, Does.Contain("self.assertFalse(rigid_result[\"pass\"])"), "A shared rigid trajectory must be rejected, never reported as fragment independence.");
        }

        [Test]
        public void BindingAuthoring_UsesStableRequiredDiagnosticIdsAndBeautyExcludedLayer()
        {
            var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "project", "Packages", "com.vfxcomposer.unity", "Editor", "W24", "S3", "W24S3BaselineAuthoring.cs"));
            Assert.That(W24S3BaselineAuthoring.BindingModelObjectId, Is.EqualTo(10u));
            Assert.That(W24S3BaselineAuthoring.BindingSocketObjectId, Is.EqualTo(101u));
            Assert.That(W24S3BaselineAuthoring.BindingFragmentFirstObjectId, Is.EqualTo(201u));
            Assert.That(source, Does.Contain("ConfigureDiagnostic(modelRenderer, BindingModelObjectId, \"bound_model\")"));
            Assert.That(source, Does.Contain("ConfigureDiagnostic(socketRenderer, BindingSocketObjectId, \"weapon_socket_marker\")"));
            Assert.That(source, Does.Contain("ConfigureDiagnostic(fragmentRenderer, BindingFragmentFirstObjectId + (uint)index"));
            Assert.That(source, Does.Contain("camera.cullingMask &= ~(1 << FormalDiagnosticLayer)"));
            Assert.That(source, Does.Contain(".Configure(renderer, objectId, semanticRole, true)"));
        }

        [Test]
        public void S3Authoring_SerializesEachFrozenCanonicalSeedAndWorldSpaceTrailInvariant()
        {
            var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "project", "Packages", "com.vfxcomposer.unity", "Editor", "W24", "S3", "W24S3BaselineAuthoring.cs"));
            Assert.That(W24S3BaselineAuthoring.ProjectileCanonicalSeed, Is.EqualTo(24101u));
            Assert.That(W24S3BaselineAuthoring.BindingCanonicalSeed, Is.EqualTo(24201u));
            Assert.That(W24S3BaselineAuthoring.LightingCanonicalSeed, Is.EqualTo(24301u));
            Assert.That(source, Does.Contain("false, ProjectileCanonicalSeed)"));
            Assert.That(source, Does.Contain("true, BindingCanonicalSeed)"));
            Assert.That(source, Does.Contain("false, LightingCanonicalSeed)"));
            Assert.That(source, Does.Contain("SetUInt(entry, \"canonicalSeed\", canonicalSeed)"));
            Assert.That(source, Does.Contain("SetBool(motion, \"requireWorldSpaceHistory\", true)"));
        }

        [Test]
        public void PreviewAuthoring_UntitledSceneHandling_IsBatchOnlyAndCleansItsRunner()
        {
            var sourcePath = Path.Combine(RepositoryRoot(), "project", "Packages", "com.vfxcomposer.unity", "Editor", "W24", "S3", "W24S3BaselineAuthoring.cs");
            var source = File.ReadAllText(sourcePath);
            Assert.That(source, Does.Contain("if (needsBatchRunner && !Application.isBatchMode)"), "GUI authoring must fail closed rather than save an Untitled user scene.");
            Assert.That(source, Does.Contain("Guid.NewGuid().ToString(\"N\")"), "The batch runner path must be unique.");
            Assert.That(source, Does.Contain("EditorSceneManager.SaveScene(runner, batchRunnerPath)"), "Batch mode needs a saved runner before additive scene creation.");
            Assert.That(source, Does.Contain("EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive)"));
            Assert.That(source, Does.Contain("EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)"), "Batch cleanup must restore an empty runner scene.");
            Assert.That(source, Does.Contain("AssetDatabase.DeleteAsset(batchRunnerPath)"), "Batch cleanup must delete the temporary scene and meta asset.");
            Assert.That(source, Does.Not.Contain("NewPreviewScene("));
            Assert.That(source, Does.Not.Contain("AssetDatabase.SaveAssets("));
        }

        [Test]
        public void ProductionManifests_UseExternalOwnership_IsolatedOutputs_AndEffectOwnedMaterial()
        {
            var cases = new[]
            {
                new { Id=W24S3BaselineAuthoring.ProjectileId, Folder=W24S3BaselineAuthoring.ProjectileOutputFolder, Prefab=W24S3BaselineAuthoring.ProjectilePrefab, Contract="docs/vfx-contracts/w24_moving_projectile_trail.contract.json", Trace="docs/vfx-traces/w24_moving_projectile_trail.implementation-trace.json", Preview=W24S3BaselineAuthoring.ProjectilePreview },
                new { Id=W24S3BaselineAuthoring.BindingId, Folder=W24S3BaselineAuthoring.BindingOutputFolder, Prefab=W24S3BaselineAuthoring.BindingPrefab, Contract="docs/vfx-contracts/w24_weapon_socket_fragments.contract.json", Trace="docs/vfx-traces/w24_weapon_socket_fragments.implementation-trace.json", Preview=W24S3BaselineAuthoring.BindingPreview },
                new { Id=W24S3BaselineAuthoring.LightingId, Folder=W24S3BaselineAuthoring.LightingOutputFolder, Prefab=W24S3BaselineAuthoring.LightingPrefab, Contract="docs/vfx-contracts/w24_real_light_receivers.contract.json", Trace="docs/vfx-traces/w24_real_light_receivers.implementation-trace.json", Preview=W24S3BaselineAuthoring.LightingPreview }
            };
            if (cases.Any(item => !File.Exists(VfxProjectRules.ManifestAbsolutePath(item.Id)) || !File.Exists(CandidateAbsolute(item.Id, "design-contract.json")) || !File.Exists(CandidateAbsolute(item.Id, "implementation-trace.json")) || !File.Exists(CandidateAbsolute(item.Id, "candidate-receipt.json")))) Assert.Ignore("Build W24 S3 baselines before manifest/candidate assertions.");
            foreach (var item in cases)
            {
                var manifestPath = VfxProjectRules.ManifestAbsolutePath(item.Id);
                Assert.That(manifestPath, Does.EndWith(item.Id + ".manifest.json"));
                var manifest = JObject.Parse(File.ReadAllText(manifestPath));
                Assert.That((string)manifest["runtimeEntry"]["path"], Is.EqualTo(item.Prefab));
                Assert.That((string)manifest["enforcement"], Is.EqualTo("strict"));
                Assert.IsNull(manifest["designContract"], "S3 must not append an ad-hoc contract block outside formalProduction.");
                Assert.IsNull(manifest["implementationTrace"], "S3 must not append an ad-hoc trace block outside formalProduction.");
                var formal = (JObject)manifest["formalProduction"];
                Assert.NotNull(formal);
                Assert.That((string)formal["contractPath"], Is.EqualTo(item.Contract));
                Assert.That((string)formal["contractFileHash"], Is.EqualTo("sha256:" + FileHash(Path.Combine(RepositoryRoot(), item.Contract.Replace('/', Path.DirectorySeparatorChar)))));
                Assert.That((string)formal["tracePath"], Is.EqualTo(item.Trace));
                Assert.That((string)formal["traceFileHash"], Is.EqualTo("sha256:" + FileHash(Path.Combine(RepositoryRoot(), item.Trace.Replace('/', Path.DirectorySeparatorChar)))));
                Assert.That((string)formal["visualStatus"], Is.EqualTo("VISUAL_PENDING"));
                Assert.That((string)formal["admissionPhase"], Is.EqualTo("PRE_C0_FIRST_FORMAL_BUILD"));
                Assert.IsNull(manifest["previewScene"], "Preview metadata must not be mistaken for a production-owned or Player dependency.");
                var owned = ((JArray)manifest["ownedOutputs"]).Select(record => (string)record["path"]).ToArray();
                Assert.That(owned, Does.Contain(item.Prefab));
                Assert.That(owned.All(path => path == item.Folder || path.StartsWith(item.Folder + "/", StringComparison.Ordinal)), Is.True);
                Assert.That(owned, Does.Not.Contain(item.Preview));
                var dependencies = ((JArray)manifest["dependencies"]).Select(record => (string)record["path"]).ToArray();
                Assert.That(owned, Does.Contain(W24S3BaselineAuthoring.MaterialPath(item.Id)));
                Assert.That(dependencies, Does.Not.Contain(W24S3BaselineAuthoring.MaterialPath(item.Id)));
                if (item.Id == W24S3BaselineAuthoring.LightingId)
                {
                    Assert.That(owned, Does.Contain(W24S3BaselineAuthoring.LightingEmissiveCoreMaterial), "D must own its independent emissive source-body material in addition to the neutral receiver material.");
                    Assert.That(dependencies, Does.Not.Contain(W24S3BaselineAuthoring.LightingEmissiveCoreMaterial));
                }
                Assert.That(dependencies, Does.Not.Contain(item.Preview));
                foreach (var record in (JArray)manifest["ownedOutputs"])
                {
                    Assert.That((string)record["guid"], Has.Length.EqualTo(32));
                    Assert.That((string)record["assetType"], Is.Not.Empty);
                    Assert.That((string)record["sha256"], Has.Length.EqualTo(64));
                }

                var bootstrapContractPath = Path.Combine(RepositoryRoot(), item.Contract.Replace('/', Path.DirectorySeparatorChar));
                var bootstrapTracePath = Path.Combine(RepositoryRoot(), item.Trace.Replace('/', Path.DirectorySeparatorChar));
                var bootstrapContract = JObject.Parse(File.ReadAllText(bootstrapContractPath));
                var bootstrapTrace = JObject.Parse(File.ReadAllText(bootstrapTracePath));
                Assert.That((string)bootstrapContract["extensions"]["captureBindingStatus"], Is.EqualTo("PENDING_FIRST_FORMAL_BUILD"));
                Assert.That((string)bootstrapContract["captureProfile"]["sceneHash"], Is.EqualTo("pending:formal-build"));
                Assert.That((string)bootstrapTrace["traceStatus"], Is.EqualTo("PENDING_FIRST_FORMAL_BUILD_BINDING"));

                var candidateContractPath = CandidateAbsolute(item.Id, "design-contract.json");
                var candidateTracePath = CandidateAbsolute(item.Id, "implementation-trace.json");
                var candidateReceiptPath = CandidateAbsolute(item.Id, "candidate-receipt.json");
                var candidateContractText = File.ReadAllText(candidateContractPath);
                var candidateContract = JObject.Parse(candidateContractText);
                var candidateTrace = JObject.Parse(File.ReadAllText(candidateTracePath));
                var candidateReceipt = JObject.Parse(File.ReadAllText(candidateReceiptPath));
                VfxDesignContract parsedCandidate;
                var candidateValidation = VfxDesignContractJson.ValidateJson(candidateContractText, out parsedCandidate);
                Assert.That(candidateValidation.HasErrors, Is.False, item.Id + ": " + string.Join(" | ", candidateValidation.Issues.Select(issue => issue.Code + " " + issue.Path + " " + issue.Message)));
                Assert.That((string)candidateContract["extensions"]["captureBindingStatus"], Is.EqualTo("FROZEN_PRE_C0"));
                Assert.That((string)candidateContract["extensions"]["candidateStatus"], Is.EqualTo("C0_CAPTURE_PENDING"));
                Assert.That((string)candidateContract["captureProfile"]["sceneSerializedReference"], Is.EqualTo(item.Preview));
                Assert.That((string)candidateContract["captureProfile"]["sceneHash"], Is.EqualTo("sha256:" + FileHash(AssetAbsolute(item.Preview))));
                Assert.That((string)candidateContract["captureProfile"]["prefabManifestHash"], Is.EqualTo("sha256:" + (string)manifest["buildHash"]));
                Assert.That((string)candidateTrace["traceStatus"], Is.EqualTo("C0_CAPTURE_PENDING"));
                Assert.That((string)candidateTrace["contractHash"], Is.EqualTo(parsedCandidate.ContractHash));
                Assert.That((string)candidateTrace["buildHash"], Is.EqualTo("sha256:" + (string)manifest["buildHash"]));
                Assert.That((string)candidateTrace["runtimeEntryAssetPath"], Is.EqualTo(item.Prefab));
                Assert.That((string)candidateTrace["runtimeEntryGuid"], Is.EqualTo((string)manifest["runtimeEntry"]["guid"]));
                Assert.That(((JArray)candidateTrace["requirementTraces"]).All(requirement =>
                    !((requirement["authorityEvidence"] as JArray)?.Any() ?? false) &&
                    !((requirement["crossEvidence"] as JArray)?.Any() ?? false)),
                    Is.True, "C0_CAPTURE_PENDING must not invent evidence; omitted and empty arrays are both evidence-free.");
                Assert.That((string)candidateReceipt["bootstrapContractFileHash"], Is.EqualTo((string)formal["contractFileHash"]));
                Assert.That((string)candidateReceipt["bootstrapTraceFileHash"], Is.EqualTo((string)formal["traceFileHash"]));
                var bootstrapManifestPath = CandidateAbsolute(item.Id, "bootstrap-manifest.json");
                Assert.That(File.Exists(bootstrapManifestPath), Is.True);
                Assert.That((string)candidateReceipt["bootstrapManifestSnapshotPath"], Is.EqualTo("docs/vfx-candidates/" + item.Id + "/C0/bootstrap-manifest.json"));
                Assert.That((string)candidateReceipt["bootstrapManifestSnapshotFileHash"], Is.EqualTo("sha256:" + FileHash(bootstrapManifestPath)));
                Assert.That(JToken.DeepEquals(candidateReceipt["ownedOutputs"], manifest["ownedOutputs"]), Is.True, item.Id + ": C0 receipt must freeze all owned output identities.");
                Assert.That((string)candidateReceipt["contractFileHash"], Is.EqualTo("sha256:" + FileHash(candidateContractPath)));
                Assert.That((string)candidateReceipt["traceFileHash"], Is.EqualTo("sha256:" + FileHash(candidateTracePath)));
            }
        }

        [Test, Explicit("One-time formal authoring integration mutates the project; run only in an isolated shadow project.")]
        public void FirstFormalBuild_IsSingleUse_AndASecondPreC0AttemptCannotChangeBytes()
        {
            if (!File.Exists(VfxProjectRules.ManifestAbsolutePath(W24S3BaselineAuthoring.ProjectileId))) W24S3BaselineAuthoring.BuildAll();
            var paths = new[]
            {
                AssetAbsolute(W24S3BaselineAuthoring.ProjectilePrefab), AssetAbsolute(W24S3BaselineAuthoring.BindingPrefab), AssetAbsolute(W24S3BaselineAuthoring.LightingPrefab),
                AssetAbsolute(W24S3BaselineAuthoring.ProjectileRecipe), AssetAbsolute(W24S3BaselineAuthoring.BindingRecipe), AssetAbsolute(W24S3BaselineAuthoring.LightingRecipe),
                VfxProjectRules.ManifestAbsolutePath(W24S3BaselineAuthoring.ProjectileId), VfxProjectRules.ManifestAbsolutePath(W24S3BaselineAuthoring.BindingId), VfxProjectRules.ManifestAbsolutePath(W24S3BaselineAuthoring.LightingId),
                CandidateAbsolute(W24S3BaselineAuthoring.ProjectileId, "design-contract.json"), CandidateAbsolute(W24S3BaselineAuthoring.ProjectileId, "implementation-trace.json"), CandidateAbsolute(W24S3BaselineAuthoring.ProjectileId, "candidate-receipt.json"),
                CandidateAbsolute(W24S3BaselineAuthoring.BindingId, "design-contract.json"), CandidateAbsolute(W24S3BaselineAuthoring.BindingId, "implementation-trace.json"), CandidateAbsolute(W24S3BaselineAuthoring.BindingId, "candidate-receipt.json"),
                CandidateAbsolute(W24S3BaselineAuthoring.LightingId, "design-contract.json"), CandidateAbsolute(W24S3BaselineAuthoring.LightingId, "implementation-trace.json"), CandidateAbsolute(W24S3BaselineAuthoring.LightingId, "candidate-receipt.json")
            };
            var first = paths.ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);
            var failure = Assert.Throws<InvalidOperationException>(() => W24S3BaselineAuthoring.BuildAll());
            StringAssert.Contains("pre-C0 production gate rejected", failure.Message);
            foreach (var path in paths) Assert.That(File.ReadAllBytes(path), Is.EqualTo(first[path]), path + " changed during a rejected second pre-C0 attempt.");
        }

        [Test]
        public void S3Contracts_ExpressTheThreeForbiddenCarrierSubstitutions()
        {
            var text = ContractFiles.Select(file => File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "vfx-contracts", file))).ToArray();
            StringAssert.Contains("fixed line", text[0]);
            StringAssert.Contains("whole image", text[1]);
            StringAssert.Contains("additive glow", text[2]);
        }

        [Test]
        public void CaptureToolBundle_IsARealReproducibleIdentitySharedByAllThreeContracts()
        {
            var root = RepositoryRoot();
            var bundlePath = Path.Combine(root, "docs", "vfx-contracts", "capture-tools", "w24-s3-capture-tool.bundle.json");
            var bundleText = File.ReadAllText(bundlePath); var bundle = JObject.Parse(bundleText);
            foreach (var source in (JArray)bundle["sources"])
            {
                var sourcePath = Path.Combine(root, ((string)source["path"]).Replace('/', Path.DirectorySeparatorChar));
                Assert.That("sha256:" + FileHash(sourcePath), Is.EqualTo((string)source["sha256"]), sourcePath);
            }
            var bundleHash = "sha256:" + RecipeCanonicalizer.ComputeSha256(bundleText);
            foreach (var file in ContractFiles)
            {
                var contract = JObject.Parse(File.ReadAllText(Path.Combine(root, "docs", "vfx-contracts", file)));
                Assert.That((string)contract["captureProfile"]["captureToolHash"], Is.EqualTo(bundleHash), file);
            }
        }

        [Test]
        public void S3RecorderCompletion_FailsClosedWithoutACapturedAndSealedC0()
        {
            var cases = new[]
            {
                new { EffectId = W24S3BaselineAuthoring.ProjectileId, Command = "FinalizeS3MovingProjectileC0Capture" },
                new { EffectId = W24S3BaselineAuthoring.BindingId, Command = "FinalizeS3WeaponSocketFragmentsC0Capture" },
                new { EffectId = W24S3BaselineAuthoring.LightingId, Command = "FinalizeS3RealLightReceiversC0Capture" }
            };
            foreach (var item in cases)
            {
                Assert.NotNull(typeof(W24S5RecorderCaptureCompletion).GetMethod(item.Command, BindingFlags.Public | BindingFlags.Static), item.EffectId + " must expose a batch/CI post-capture command.");
                var rawCapture = Path.Combine(RepositoryRoot(), "artifacts", "vfx-evidence", item.EffectId, "C0", "capture-metadata.json");
                if (File.Exists(rawCapture)) Assert.Ignore("Formal S3 capture already exists; do not replay or overwrite write-once evidence.");
                var formalMetadata = Path.Combine(RepositoryRoot(), "artifacts", "vfx-evidence", item.EffectId, "C0", "bound", "formal-capture-metadata.json");
                var result = W24S5RecorderCaptureCompletion.Finalize(item.EffectId, "{ }");
                Assert.That(result.Succeeded, Is.False, item.EffectId + " must not enter the evidence seal without a graphics capture.");
                Assert.That(File.Exists(formalMetadata), Is.False, item.EffectId + " must not leave derived formal metadata after a rejected capture completion.");
            }
        }

        [Test]
        public void S3FormalGraphicsCaptureProducer_IsExplicitNaturalPlayerLoopAndCannotClaimQaOrUserVerdicts()
        {
            var sourcePath = Path.Combine(RepositoryRoot(), "project", "Packages", "com.vfxcomposer.unity", "Tests", "PlayMode", "W24S3GraphicsCaptureEvidenceTests.cs");
            var source = File.ReadAllText(sourcePath);
            Assert.That(source, Does.Contain("[Explicit("), "Formal graphics capture must never become normal regression work.");
            Assert.That(source, Does.Contain("W24ContinuousCaptureRecorder.RequireGraphicsBatchmode()"), "Formal capture must reject -nographics and non-batch runs.");
            Assert.That(source, Does.Not.Contain("new WaitForEndOfFrame()"), "Editor batchmode may never resume WaitForEndOfFrame; formal capture must use the recorder's real LateUpdate observation instead.");
            Assert.That(source, Does.Contain("yield return null"), "The UnityTest must yield natural PlayerLoop frames without synthetic time or manual stepping.");
            Assert.That(source, Does.Contain("recorder.DiagnosticEffectLayers = 1 << 1"), "effect-only diagnostics must use the frozen Runtime Entry layer, not the receiver/default layer.");
            Assert.That(source, Does.Contain("recorder.BeginFormal("), "Formal capture must bind a non-null receipt-bound command hash before observing frames.");
            Assert.That(source, Does.Contain("recorder.ConsumeCompletedPlayerLoopToken()"), "Every formal observation must originate from the recorder's completed real LateUpdate token.");
            Assert.That(source, Does.Contain("CaptureObservedPlayerLoopFrame(token"), "Retained Beauty/diagnostic frames must derive from the canonical recorder's real LateUpdate token.");
            Assert.That(source, Does.Contain("AcknowledgeObservedPlayerLoopFrame(token)"), "Every non-retained natural frame must be acknowledged so formal evidence cannot cherry-pick a timeline.");
            Assert.That(source, Does.Contain("driver.RestartForFormalCapture(seed)"), "The serialized formal driver may restart only after an observed LateUpdate boundary token has been consumed and acknowledged.");
            Assert.That(source, Does.Contain("recorder.AfterPlayerLoopFrame += observer"), "Formal lifecycle observation must be driven by the recorder's post-LateUpdate callback.");
            Assert.That(source, Does.Contain("WriteObservedTypedDiagnostic(token, frame, seed"), "All formal raw diagnostics must bind the same natural LateUpdate token that is later acknowledged.");
            Assert.That(source, Does.Contain("W24TrailMaskDiagnosticCapture.Capture"), "Projectile authority must use the typed R8 trail-mask capture.");
            Assert.That(source, Does.Contain("entry.ReadEmitterHistory()"), "Trail corridors must use accepted world emitter history rather than TrailRenderer vertex readback.");
            Assert.That(source, Does.Contain("W24ObjectIdDepthDiagnosticCapture.Capture"), "Binding and receiver identity inputs must use typed Object-ID/depth capture.");
            Assert.That(source, Does.Contain("W24LinearLdrDiagnosticCapture.Capture"), "Receiver A/B authority must use float linear-LDR raw NPY instead of PNG authority.");
            Assert.That(source, Does.Contain("W24MetricsEvidenceDag"), "Formal raw diagnostics must enter the controlled recorder-owned metrics DAG.");
            Assert.That(source, Does.Contain("metricCheckId"), "Completed traces must bind individual sealed metrics checks, not a generic summary.");
            Assert.That(source, Does.Contain("RequiredEvidencePlan.Read"), "The producer must consume the Contract-frozen raw evidence matrix rather than guess a capture schedule.");
            Assert.That(source, Does.Contain("requiredEvidenceMatrixSha256"), "Metrics input must preserve the exact hash-bound Contract matrix.");
            Assert.That(source, Does.Contain("contractRevision"), "Metrics input must bind its explicit Contract revision.");
            Assert.That(source, Does.Contain("captureToolBundlePath"), "Metrics input must bind the canonical frozen bundle path as well as its hash.");
            Assert.That(source, Does.Contain("ProbeMetricsEnvironmentForInput"), "The producer must get environment identity through the public controlled bridge API.");
            Assert.That(source, Does.Contain("RequiredMetricsPythonExecutable"), "Formal metrics must require an explicit absolute Python executable.");
            Assert.That(source, Does.Not.Contain("W24_METRICS_PYTHON\") ?? \"python\""), "Formal evidence must never fall back to PATH-resolved python.");
            Assert.That(source, Does.Contain("playerLoopSerial"), "Metrics registry must mirror raw LateUpdate token provenance.");
            Assert.That(source, Does.Contain("RawTokenSequence"), "Raw diagnostics must retain explicit serial/frame/time provenance for the recorder-sealed LateUpdate token.");
            Assert.That(source, Does.Not.Contain("token.Serial"), "The producer must not reflect inaccessible token fields; raw provenance is captured through the controlled sequence.");
            Assert.That(source, Does.Contain("row.ViewId, derivedFrom"), "Every formal typed raw write must supply a non-empty view and derived-from provenance.");
            Assert.That(source, Does.Contain("!string.IsNullOrWhiteSpace(derivedFrom)"), "Producer-side raw creation must reject missing derivedFrom before recorder write.");
            Assert.That(source, Does.Contain("RetainedBeautyArtifact"), "Non-metric telemetry cross-evidence must reference sealed Beauty rather than generic passed diagnostics.");
            Assert.That(source, Does.Not.Contain("RetainedEffectOnlyArtifact"), "A generic effect-only diagnostic cannot bypass the frozen requirement-to-check mapping.");
            Assert.That(source, Does.Contain("InvokeMetricsBridge(\"WriteInput\", recorder, inputPath, input, contractRevision.Value, contractHash, contractCaptureProfileHash, expectedToolHash)"), "Metrics input must use the final seven-parameter bridge contract, including the frozen Contract capture-profile identity.");
            Assert.That(source, Does.Not.Contain("InvokeMetricsBridge(\"WriteInput\", recorder, inputPath, input, contractRevision.Value, contractHash, expectedToolHash)"), "The obsolete six-parameter WriteInput call must be rejected.");
            Assert.That(source, Does.Not.Contain("InvokeMetricsBridge(\"WriteInput\", recorder, inputPath, input, expectedToolHash)"), "The obsolete four-argument WriteInput call must be rejected.");
            Assert.That(source, Does.Not.Contain("REQ-B-TRAIL-CORRIDOR\") return true"), "Diagnostic requirement pass may not be a literal true.");
            Assert.That(source, Does.Not.Contain("REQ-C-MULTIVIEW\") return true"), "Diagnostic requirement pass may not be a literal true.");
            var recorderSource = File.ReadAllText(Path.Combine(RepositoryRoot(), "project", "Packages", "com.vfxcomposer.unity", "Runtime", "Diagnostics", "W24ContinuousCaptureRecorder.cs"));
            Assert.That(recorderSource, Does.Contain("string.IsNullOrEmpty(derivedFrom)"), "Missing derivedFrom must be rejected by the canonical typed recorder API.");
            Assert.That(source, Does.Contain("frozenCandidateReceiptSha256"), "The recorder provenance command must bind the frozen C0 receipt.");
            Assert.That(source, Does.Contain("FinalizeS3MovingProjectileC0Capture"));
            Assert.That(source, Does.Contain("FinalizeS3WeaponSocketFragmentsC0Capture"));
            Assert.That(source, Does.Contain("FinalizeS3RealLightReceiversC0Capture"));
            Assert.That(source, Does.Contain("Pending independent authority; graphics capture does not fabricate a Visual QA or user verdict."));
        }

        [Test]
        public void S3Authoring_IsolatesRuntimeEntryOnDiagnosticLayerWithoutMovingPreviewReceivers()
        {
            var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "project", "Packages", "com.vfxcomposer.unity", "Editor", "W24", "S3", "W24S3BaselineAuthoring.cs"));
            Assert.That(source, Does.Contain("SetLayerRecursively(root, 1)"));
            Assert.That(source, Does.Contain("Receiver probes stay on Default"));
            Assert.That(source, Does.Contain("model itself on Default"));
        }

        [Test]
        public void BuiltRuntimeEntries_HaveExactlyOnePlayerSafeEntry_AndNoPreviewDriver()
        {
            var prefabs = new[] { W24S3BaselineAuthoring.ProjectilePrefab, W24S3BaselineAuthoring.BindingPrefab, W24S3BaselineAuthoring.LightingPrefab };
            if (prefabs.Any(path => AssetDatabase.LoadAssetAtPath<GameObject>(path) == null))
                Assert.Ignore("Run VFX Composer/W24/S3/Build three remaining baselines before asset assertions.");
            foreach (var path in prefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab.GetComponents<MonoBehaviour>().Count(item => item is IVfxRuntimeEntry), Is.EqualTo(1), path);
                Assert.IsNull(prefab.GetComponentInChildren<VfxPreviewSequenceDriver>(true), path + " must not carry a Preview driver into the Runtime Entry.");
                Assert.IsNull(prefab.GetComponentInChildren<W24S3PreviewDriver>(true), path + " must not carry the S3 Preview driver into the Runtime Entry.");
                Assert.NotNull(prefab.GetComponent<W24S3RuntimeEntry>(), path + " must use the Player-safe S3 bridge.");
            }
        }

        [Test]
        public void ProjectileRuntimeEntry_UsesTheRealMotionTrailProtocol()
        {
            var prefab = RequirePrefab(W24S3BaselineAuthoring.ProjectilePrefab);
            var protocol = prefab.GetComponent<W24MovingEmitterTrailProtocol>();
            Assert.NotNull(protocol);
            Assert.That(protocol.UsesWorldSpaceHistory, Is.True);
            var trail = prefab.GetComponentInChildren<TrailRenderer>(true);
            Assert.NotNull(trail);
            Assert.That(trail.name, Is.EqualTo("WorldSpaceTrail"));
        }

        [Test]
        public void BindingRuntimeEntry_UsesSocketAdapterAndIndependentFragmentTransforms()
        {
            var prefab = RequirePrefab(W24S3BaselineAuthoring.BindingPrefab);
            Assert.NotNull(prefab.GetComponent<W24ModelBindingAdapter>());
            var fragments = prefab.GetComponentsInChildren<Transform>(true).Where(item => item.name.StartsWith("IndependentFragment_", StringComparison.Ordinal)).ToArray();
            Assert.That(fragments.Length, Is.EqualTo(3));
            Assert.That(fragments.Select(item => item.GetInstanceID()).Distinct().Count(), Is.EqualTo(3));
            Assert.NotNull(prefab.GetComponentInChildren<W24FragmentMotionSystem>(true));
        }

        [Test]
        public void LightingRuntimeEntry_UsesTwoUnshadowedRealLights_AndFormalReceiverScene()
        {
            var prefab = RequirePrefab(W24S3BaselineAuthoring.LightingPrefab);
            Assert.NotNull(prefab.GetComponent<W24RealLightingModule>());
            var lights = prefab.GetComponentsInChildren<Light>(true);
            Assert.That(lights.Length, Is.EqualTo(2));
            Assert.That(lights.All(light => light.type == LightType.Point && light.shadows == LightShadows.None), Is.True);
            var core = prefab.GetComponentsInChildren<MeshRenderer>(true).Single(renderer => renderer.name == "PhysicalLightCoreMesh");
            Assert.IsNull(core.GetComponent<Collider>(), "The declared source body is a visual carrier and must not alter gameplay collision.");
            var entry = prefab.GetComponent<W24S3RuntimeEntry>(); var serializedEntry = new SerializedObject(entry);
            Assert.IsNull(serializedEntry.FindProperty("selfBrightRenderer"), "The Runtime Entry must not rely on a non-persistent MPB emission bridge.");
            Assert.That(AssetDatabase.GetAssetPath(core.sharedMaterial), Is.EqualTo(W24S3BaselineAuthoring.LightingEmissiveCoreMaterial));
            Assert.That(core.sharedMaterial.IsKeywordEnabled("_EMISSION"), Is.True, "The effect-owned source-body material must persist its URP Lit emission variant.");
            Assert.That(core.sharedMaterial.GetColor("_EmissionColor").maxColorComponent, Is.GreaterThan(1f), "The source-body material must serialize a non-black HDR emission so URP import cannot truthfully strip _EMISSION.");
            Assert.That(prefab.GetComponentsInChildren<Renderer>(true).Length, Is.LessThanOrEqualTo(5), "The required source body must stay inside the frozen Runtime Entry renderer budget.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(W24S3BaselineAuthoring.LightingPreview) == null)
                Assert.Ignore("S3 Preview scenes have not been built yet.");
            var scene = EditorSceneManager.OpenScene(W24S3BaselineAuthoring.LightingPreview, OpenSceneMode.Additive);
            try
            {
                var receivers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Renderer>(true)).Where(item => item.name == "Receiver_A_LinearProbe" || item.name == "Receiver_B_LinearProbe").ToArray();
                Assert.That(receivers.Length, Is.EqualTo(2));
                foreach (var receiver in receivers)
                {
                    Assert.That(AssetDatabase.GetAssetPath(receiver.sharedMaterial), Is.EqualTo(W24S3BaselineAuthoring.LightingMaterial));
                    Assert.That(receiver.sharedMaterial, Is.Not.SameAs(core.sharedMaterial), receiver.name + " must not share the effect-owned emissive source-body material.");
                    Assert.That(receiver.sharedMaterial.GetColor("_EmissionColor").maxColorComponent, Is.EqualTo(0f), receiver.name + " must remain a neutral physical-light probe.");
                    Assert.That(receiver.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"), receiver.name + " must remain a physically light-responsive receiver rather than an Unlit substitute.");
                    var properties = new MaterialPropertyBlock(); receiver.GetPropertyBlock(properties);
                    Assert.That(properties.isEmpty, Is.True, receiver.name + " must not carry any source-body property override.");
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void LightingMaterialAuthoring_PersistsSeparateTruthfulCoreAndNeutralReceiverVariants()
        {
            var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "project", "Packages", "com.vfxcomposer.unity", "Editor", "W24", "S3", "W24S3BaselineAuthoring.cs"));
            var createIndex = source.IndexOf("AssetDatabase.CreateAsset(new Material(shader)", StringComparison.Ordinal);
            var emissionColorIndex = source.IndexOf("material.SetColor(\"_EmissionColor\", emissionColor)", StringComparison.Ordinal);
            var enableIndex = source.IndexOf("material.EnableKeyword(\"_EMISSION\")", StringComparison.Ordinal);
            var saveIndex = source.IndexOf("AssetDatabase.SaveAssetIfDirty(material)", StringComparison.Ordinal);
            var reimportIndex = source.IndexOf("AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate)", StringComparison.Ordinal);
            Assert.That(createIndex, Is.GreaterThanOrEqualTo(0), "The owned URP Lit material must complete its first import before applying final keyword state.");
            Assert.That(emissionColorIndex, Is.GreaterThan(createIndex));
            Assert.That(enableIndex, Is.GreaterThan(emissionColorIndex), "The non-black serialized emission property must justify the variant before _EMISSION is enabled.");
            Assert.That(saveIndex, Is.GreaterThan(enableIndex), "The post-import _EMISSION variant must be persisted after it is enabled.");
            Assert.That(reimportIndex, Is.GreaterThan(saveIndex), "Authoring must validate the imported bytes rather than only the transient Material instance.");
            Assert.That(source, Does.Contain("!material.IsKeywordEnabled(\"_EMISSION\") || material.GetColor(\"_EmissionColor\").maxColorComponent <= 1f"), "Authoring must fail closed if the reimported effect-owned material loses either the keyword or its non-black emission.");
            Assert.That(source, Does.Contain("!emissionEnabled && material.GetColor(\"_EmissionColor\").maxColorComponent != 0f"), "Authoring must fail closed if a neutral receiver material acquires non-black serialized emission.");
            Assert.That(source, Does.Contain("MaterialGlobalIlluminationFlags.EmissiveIsBlack"), "Authoring should give URP the compatible neutral GI hint even though importer-owned keywords are not an acceptance invariant.");
            var buildAllStart = source.IndexOf("public static void BuildAll()", StringComparison.Ordinal);
            var buildAllEnd = source.IndexOf("private static void BuildProjectile", buildAllStart, StringComparison.Ordinal);
            var buildAll = source.Substring(buildAllStart, buildAllEnd - buildAllStart);
            var finalImportIndex = buildAll.LastIndexOf("transaction.ImportOwnedAssets();", StringComparison.Ordinal);
            var finalValidationIndex = buildAll.LastIndexOf("ValidatePersistedMaterialContracts();", StringComparison.Ordinal);
            var finalVerifyIndex = buildAll.LastIndexOf("VFXComposer.Editor.W24.W24FormalBatchAuthoringEntrypoints.VerifyFormalOutput", StringComparison.Ordinal);
            var commitIndex = buildAll.LastIndexOf("transaction.Commit();", StringComparison.Ordinal);
            Assert.That(finalImportIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(finalValidationIndex, Is.GreaterThan(finalImportIndex), "The final exact owned-folder import must be followed by read-only material validation.");
            Assert.That(finalVerifyIndex, Is.GreaterThan(finalValidationIndex), "Frozen output identities must be revalidated after the last material check.");
            Assert.That(commitIndex, Is.GreaterThan(finalVerifyIndex), "Commit must remain the final operation after all persistence checks.");
        }

        [Test]
        public void LightingContractAndTrace_DeclareTheSelfBrightSourceBodyWithoutReceiverContamination()
        {
            var root = RepositoryRoot();
            var contract = JObject.Parse(File.ReadAllText(Path.Combine(root, "docs", "vfx-contracts", "w24_real_light_receivers.contract.json")));
            var source = ((JArray)contract["layers"]).OfType<JObject>().Single(layer => (string)layer["layerId"] == "source_body");
            Assert.That((bool)source["required"], Is.True);
            Assert.That((string)source["carrier"], Does.Contain("MeshRenderer"));
            Assert.That((string)source["materialModel"], Does.Contain("effect-owned independent emissive material"));
            Assert.That((string)source["materialModel"], Does.Not.Contain("MaterialPropertyBlock"));
            Assert.That((string)source["attachment"], Is.EqualTo("Runtime Entry/SustainedFlame/PhysicalLightCoreMesh"));
            Assert.That((int)source.SelectToken("budgetCost.renderers"), Is.EqualTo(1));
            Assert.That((int)contract.SelectToken("budget.rendererCount"), Is.EqualTo(5));
            Assert.That((int)contract.SelectToken("budget.materialCount"), Is.EqualTo(2));
            var trace = JObject.Parse(File.ReadAllText(Path.Combine(root, "docs", "vfx-traces", "w24_real_light_receivers.implementation-trace.json")));
            var visual = ((JArray)trace["requirementTraces"]).OfType<JObject>().Single(item => (string)item["designRequirementId"] == "REQ-D-VISUAL");
            Assert.That(((JArray)visual["layerIds"]).Values<string>(), Does.Contain("source_body"));
            Assert.That(((JArray)visual["objects"]).OfType<JObject>().Any(item =>
                (string)item["hierarchyPath"] == "/VFX_w24_real_light_receivers/SustainedFlame/PhysicalLightCoreMesh"
                && (string)item["componentType"] == "MeshRenderer"), Is.True, "REQ-D-VISUAL must trace the exact runtime source-body renderer rather than infer it from the real Light components.");
            Assert.That(((JArray)visual["objects"]).OfType<JObject>().Any(item =>
                (string)item["assetPath"] == W24S3BaselineAuthoring.LightingEmissiveCoreMaterial
                && (string)item["componentType"] == "Material"
                && (string)item["propertyPath"] == "_EmissionColor"
                && (string)item["shaderName"] == "Universal Render Pipeline/Lit"), Is.True, "REQ-D-VISUAL must trace the exact independent emissive material and property, not only its Renderer.");
        }

        private static GameObject RequirePrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) Assert.Ignore("Run VFX Composer/W24/S3/Build three remaining baselines before asset assertions.");
            return prefab;
        }

        private static bool ProjectileMatrixComplete(JArray matrix)
        {
            var seeds = new uint[] { 24101u, 24111u, 24121u }; var frames = new[] { 18, 48, 72 };
            return matrix != null && matrix.Count == 9 && seeds.All(seed => frames.All(frame => MatrixHas(matrix, "b-trail-" + seed + "-f" + frame, "trail-only-mask", seed, "projectile_front_main", frame)));
        }

        private static bool BindingMatrixComplete(JArray matrix)
        {
            var seeds = new uint[] { 24201u, 24211u, 24221u };
            if (matrix == null || matrix.Count != 21) return false;
            foreach (var seed in seeds)
            {
                if (!MatrixHas(matrix, "c-fragment-" + seed + "-f54", "fragment-id", seed, "binding_front_main", 54)
                    || !MatrixHas(matrix, "c-fragment-" + seed + "-f63", "fragment-id", seed, "binding_front_main", 63)
                    || !MatrixHas(matrix, "c-fragment-" + seed + "-f72", "fragment-id", seed, "binding_front_main", 72)
                    || !MatrixHas(matrix, "c-object-id-" + seed + "-front-f72", "object-id", seed, "binding_front_main", 72)
                    || !MatrixHas(matrix, "c-depth-" + seed + "-front-f72", "depth-linear", seed, "binding_front_main", 72)
                    || !MatrixHas(matrix, "c-object-id-" + seed + "-oblique-f72", "object-id", seed, "binding_oblique", 72)
                    || !MatrixHas(matrix, "c-depth-" + seed + "-oblique-f72", "depth-linear", seed, "binding_oblique", 72)) return false;
            }
            return matrix.OfType<JObject>().Select(row => (string)row["evidenceId"]).Distinct().Count() == matrix.Count;
        }

        private static bool ReceiverMatrixComplete(JArray matrix)
        {
            var seeds = new uint[] { 24301u, 24311u, 24321u };
            return matrix != null && matrix.Count == 12 && seeds.All(seed =>
                MatrixHas(matrix, "d-effect-mask-" + seed + "-f24", "effect-mask", seed, "light_main", 24)
                && MatrixHas(matrix, "d-receiver-id-" + seed + "-f24", "receiver-id", seed, "light_main", 24)
                && MatrixHas(matrix, "d-receiver-off-" + seed + "-f24", "receiver-linear-ldr", seed, "light_main", 24)
                && MatrixHas(matrix, "d-receiver-on-" + seed + "-f24", "receiver-linear-ldr", seed, "light_main", 24));
        }

        private static bool MetricsEnvironmentComplete(JObject environment)
        {
            if (environment == null) return false;
            var body = (JObject)environment.DeepClone();
            var selfHash = (string)body["environmentSha256"];
            body.Remove("environmentSha256");
            return string.Equals((string)body["pythonExecutablePath"], "C:/Program Files/Python312/python.exe", System.StringComparison.Ordinal)
                && string.Equals((string)body["pythonExecutableSha256"], "sha256:fd5c46d73d29ba21b04c844bbaf9096066136526911230645a2a040d23fb612b", System.StringComparison.Ordinal)
                && string.Equals((string)body["pythonVersion"], "Python 3.12.4", System.StringComparison.Ordinal)
                && string.Equals((string)body["numpyVersion"], "2.4.5", System.StringComparison.Ordinal)
                && string.Equals((string)body["pillowVersion"], "12.2.0", System.StringComparison.Ordinal)
                && string.Equals(selfHash, "sha256:" + RecipeCanonicalizer.ComputeSha256(body.ToString(Newtonsoft.Json.Formatting.None)), System.StringComparison.Ordinal);
        }

        private static bool MatrixHas(JArray matrix, string evidenceId, string passId, uint seed, string viewId, int frame)
        {
            return matrix.OfType<JObject>().Count(row => string.Equals((string)row["evidenceId"], evidenceId, System.StringComparison.Ordinal)
                && string.Equals((string)row["passId"], passId, System.StringComparison.Ordinal)
                && (uint?)row["seed"] == seed && string.Equals((string)row["viewId"], viewId, System.StringComparison.Ordinal)
                && (int?)row["logicalFrameIndex"] == frame) == 1;
        }
        private static string CandidateAbsolute(string effectId, string fileName) { return Path.Combine(RepositoryRoot(), "docs", "vfx-candidates", effectId, "C0", fileName); }
        private static string AssetAbsolute(string path) { return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path.Replace('/', Path.DirectorySeparatorChar))); }
        private static string FileHash(string path) { using (var stream=File.OpenRead(path)) using(var sha=SHA256.Create()) return string.Concat(sha.ComputeHash(stream).Select(value=>value.ToString("x2"))); }
        private static string RepositoryRoot() { return Directory.GetParent(Application.dataPath).Parent.FullName; }
    }
}
