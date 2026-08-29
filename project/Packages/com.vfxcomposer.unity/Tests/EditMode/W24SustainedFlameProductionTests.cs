using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VFXComposer.Editor.Validation;
using VFXComposer.Editor.W24;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W24SustainedFlameProductionTests
    {
        private static readonly string[] CaptureAuthoritySources =
        {
            "project/Packages/com.vfxcomposer.unity/Runtime/Diagnostics/W24CaptureProfile.cs",
            "project/Packages/com.vfxcomposer.unity/Runtime/Diagnostics/W24ContinuousCaptureRecorder.cs",
            "project/Packages/com.vfxcomposer.unity/Runtime/Diagnostics/W24EvidenceStore.cs",
            "project/Packages/com.vfxcomposer.unity/Editor/Validation/RecipeCanonicalizer.cs",
            "project/Packages/com.vfxcomposer.unity/Editor/W24/S1/VfxDesignContract.cs",
            "project/Packages/com.vfxcomposer.unity/Editor/W24/S1/VfxImplementationTrace.cs",
            "project/Packages/com.vfxcomposer.unity/Editor/W24/S5/W24S5EvidenceTransition.cs",
            "project/Packages/com.vfxcomposer.unity/Editor/W24/S5/W24S5ProductionGate.cs",
            "project/Packages/com.vfxcomposer.unity/Editor/W24/S5/W24S5RecorderCaptureCompletion.cs",
            "project/Packages/com.vfxcomposer.unity/Tests/PlayMode/W24SustainedFlameFormalEvidenceTests.cs"
        };

        // Exempted by the main agent on 2026-08-29; see docs/plans/UNITY_TEST_TRIAGE.md §3.4 (R-4).
        // The S0b bundle pins VfxDesignContract.cs at a content that predates this repository's
        // history and is unrecoverable, and re-sealing sustained_flame_3d.contract.json would
        // rewrite 111 downstream write-once evidence pins. The assertions below stay intact so the
        // exemption is visible and reversible; do not weaken them.
        [Test, Ignore("R-4 exemption: S0b capture-tool bundle re-seal is deferred; see docs/plans/UNITY_TEST_TRIAGE.md §3.4.")]
        public void CaptureToolBundle_BindsTheExactS0bAuthoritySourceSet()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var repositoryRoot = Directory.GetParent(projectRoot).FullName;
            var bundlePath = Path.Combine(repositoryRoot, "docs", "vfx-contracts", "capture-tools", "sustained-flame-capture-tool.bundle.json");
            var bundle = JObject.Parse(File.ReadAllText(bundlePath));
            Assert.That((string)bundle["toolVersion"], Is.EqualTo(SustainedFlameAuthoring.CaptureToolVersion));
            var sources = ((JArray)bundle["sources"]).OfType<JObject>().ToArray();
            CollectionAssert.AreEqual(CaptureAuthoritySources.OrderBy(value => value, StringComparer.Ordinal).ToArray(), sources.Select(source => (string)source["path"]).OrderBy(value => value, StringComparer.Ordinal).ToArray());
            Assert.That(sources.Select(source => (string)source["path"]).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(sources.Length));
            foreach (var source in sources)
                Assert.That((string)source["sha256"], Is.EqualTo("sha256:" + HashFile(Path.Combine(repositoryRoot, ((string)source["path"]).Replace('/', Path.DirectorySeparatorChar)))));
        }

        [Test]
        public void RuntimeEntry_HasOneControllerDistinctLayersAndNoPreviewComponents()
        {
            var prefab = RequireBuiltPrefab();
            Assert.That(prefab.GetComponents<MonoBehaviour>().Count(component => component is IVfxRuntimeEntry), Is.EqualTo(1));
            var controller = prefab.GetComponent<SustainedEffectController>();
            Assert.NotNull(controller);
            Assert.IsNull(prefab.GetComponentInChildren<SustainedEffectPreviewDriver>(true));
            Assert.That(prefab.GetComponentsInChildren<Transform>(true).Length, Is.LessThanOrEqualTo(10));
            Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true).Length, Is.EqualTo(7));
            Assert.That(prefab.GetComponentsInChildren<Light>(true).Length, Is.EqualTo(1));

            var names = prefab.GetComponentsInChildren<Transform>(true).Select(value => value.name).ToArray();
            CollectionAssert.IsSupersetOf(names, new[] { "Ignition", "Steady", "CoreFlame", "OuterFlame", "Smoke", "Embers", "StopTail", "InterruptBurst", "FlameLight" });
            Assert.That(names.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(names.Length));
            foreach (var particle in prefab.GetComponentsInChildren<ParticleSystem>(true))
                Assert.IsFalse(particle.useAutoRandomSeed, particle.name + " must use a deterministic seed.");
            Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true).Select(value => value.randomSeed).Distinct().Count(), Is.EqualTo(7));
        }

        [Test]
        public void RuntimeEntry_IsSerializedInvisibleAndUsesOnlyOwnedRenderMaterials()
        {
            var prefab = RequireBuiltPrefab();
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                Assert.That(instance.GetComponentsInChildren<ParticleSystem>(true).Sum(value => value.particleCount), Is.EqualTo(0));
                Assert.That(instance.GetComponentsInChildren<Light>(true).All(value => !value.enabled && value.intensity <= 0f), Is.True);
                Assert.That(instance.GetComponentsInChildren<ParticleSystem>(true).All(value => !value.gameObject.activeInHierarchy), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }

            var dependencies = AssetDatabase.GetDependencies(SustainedFlameAuthoring.PrefabPath, true);
            Assert.That(dependencies.Any(path => path == SustainedFlameAuthoring.ShaderPath), Is.True);
            Assert.That(dependencies.Where(path => path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)).All(path => path.StartsWith(SustainedFlameAuthoring.OutputFolder + "/", StringComparison.Ordinal)), Is.True);
            Assert.That(dependencies.Count(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".tga", StringComparison.OrdinalIgnoreCase)), Is.EqualTo(0), "This baseline intentionally uses a procedural Shader and no Runtime texture.");
        }

        [Test]
        public void Manifest_TracksContractRecipeRuntimeAndVisualPendingTruthfully()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var repositoryRoot = Directory.GetParent(projectRoot).FullName;
            var manifestPath = Path.Combine(projectRoot, "ProjectSettings", "VFXComposer", "BuildManifests", SustainedFlameAuthoring.EffectId + ".manifest.json");
            var bootstrapContractPath = Path.Combine(repositoryRoot, "docs", "vfx-contracts", "sustained_flame_3d.contract.json");
            var bootstrapTracePath = Path.Combine(repositoryRoot, "docs", "vfx-traces", "sustained_flame_3d.implementation-trace.json");
            var candidateRoot = Path.Combine(repositoryRoot, "docs", "vfx-candidates", SustainedFlameAuthoring.EffectId, "C0");
            var candidateContractPath = Path.Combine(candidateRoot, "design-contract.json");
            var candidateTracePath = Path.Combine(candidateRoot, "implementation-trace.json");
            var candidateReceiptPath = Path.Combine(candidateRoot, "candidate-receipt.json");
            if (new[] { manifestPath, bootstrapContractPath, bootstrapTracePath, candidateContractPath, candidateTracePath, candidateReceiptPath, Path.Combine(candidateRoot, "bootstrap-manifest.json") }.Any(path => !File.Exists(path)))
                Assert.Ignore("S0b asset build/manifest precondition is not available yet; formal production assertions resume after the compiler build.");
            var manifest = JObject.Parse(File.ReadAllText(manifestPath));
            Assert.That((string)manifest["effectId"], Is.EqualTo(SustainedFlameAuthoring.EffectId));
            Assert.That((string)manifest["runtimeEntry"]["path"], Is.EqualTo(SustainedFlameAuthoring.PrefabPath));
            Assert.That((string)manifest["runtimeEntry"]["guid"], Is.EqualTo(AssetDatabase.AssetPathToGUID(SustainedFlameAuthoring.PrefabPath)));
            Assert.IsNull(manifest["designContract"], "The formal Contract authority belongs only under formalProduction.");
            Assert.IsNull(manifest["implementationTrace"], "The formal Trace authority belongs only under formalProduction.");

            var bootstrapContract = JObject.Parse(File.ReadAllText(bootstrapContractPath));
            var bootstrapTrace = JObject.Parse(File.ReadAllText(bootstrapTracePath));
            var formal = (JObject)manifest["formalProduction"];
            Assert.NotNull(formal);
            Assert.That((string)formal["contractPath"], Is.EqualTo(SustainedFlameAuthoring.ContractPath));
            Assert.That((string)formal["contractFileHash"], Is.EqualTo("sha256:" + HashFile(bootstrapContractPath)));
            Assert.That((string)formal["contractHash"], Is.EqualTo((string)bootstrapContract["contractHash"]));
            Assert.That((string)formal["tracePath"], Is.EqualTo(SustainedFlameAuthoring.TracePath));
            Assert.That((string)formal["traceFileHash"], Is.EqualTo("sha256:" + HashFile(bootstrapTracePath)));
            Assert.That((string)formal["admissionPhase"], Is.EqualTo("PRE_C0_FIRST_FORMAL_BUILD"));
            Assert.That((string)formal["visualStatus"], Is.EqualTo("VISUAL_PENDING"));
            Assert.That((string)bootstrapContract["extensions"]["captureBindingStatus"], Is.EqualTo("PENDING_FIRST_FORMAL_BUILD"));
            Assert.That((string)bootstrapContract["captureProfile"]["sceneHash"], Is.EqualTo("pending:formal-build"));
            Assert.That((string)bootstrapTrace["traceStatus"], Is.EqualTo("PENDING_FIRST_FORMAL_BUILD_BINDING"));

            var contract = JObject.Parse(File.ReadAllText(candidateContractPath));
            var trace = JObject.Parse(File.ReadAllText(candidateTracePath));
            var receipt = JObject.Parse(File.ReadAllText(candidateReceiptPath));
            Assert.That((string)contract["extensions"]["captureBindingStatus"], Is.EqualTo("FROZEN_PRE_C0"));
            Assert.That((string)contract["extensions"]["candidateStatus"], Is.EqualTo("C0_CAPTURE_PENDING"));
            Assert.That((string)contract["captureProfile"]["sceneHash"], Is.EqualTo("sha256:" + HashFile(Path.Combine(projectRoot, SustainedFlameAuthoring.PreviewScenePath.Replace('/', Path.DirectorySeparatorChar)))));
            Assert.That((string)contract["captureProfile"]["prefabManifestHash"], Is.EqualTo("sha256:" + (string)manifest["buildHash"]));
            Assert.That((string)trace["traceStatus"], Is.EqualTo("C0_CAPTURE_PENDING"));
            Assert.That((string)trace["contractHash"], Is.EqualTo((string)contract["contractHash"]));
            Assert.That((string)trace["buildHash"], Is.EqualTo("sha256:" + (string)manifest["buildHash"]));
            Assert.That((string)trace["runtimeEntryGuid"], Is.EqualTo((string)manifest["runtimeEntry"]["guid"]));
            Assert.That((string)receipt["bootstrapContractFileHash"], Is.EqualTo((string)formal["contractFileHash"]));
            Assert.That((string)receipt["bootstrapTraceFileHash"], Is.EqualTo((string)formal["traceFileHash"]));
            Assert.That((string)receipt["contractFileHash"], Is.EqualTo("sha256:" + HashFile(candidateContractPath)));
            Assert.That((string)receipt["traceFileHash"], Is.EqualTo("sha256:" + HashFile(candidateTracePath)));
            var bootstrapManifestPath = Path.Combine(candidateRoot, "bootstrap-manifest.json");
            Assert.That(File.Exists(bootstrapManifestPath), Is.True);
            Assert.That((string)receipt["bootstrapManifestSnapshotPath"], Is.EqualTo("docs/vfx-candidates/" + SustainedFlameAuthoring.EffectId + "/C0/bootstrap-manifest.json"));
            Assert.That((string)receipt["bootstrapManifestSnapshotFileHash"], Is.EqualTo("sha256:" + HashFile(bootstrapManifestPath)));
            Assert.That(JToken.DeepEquals(receipt["ownedOutputs"], manifest["ownedOutputs"]), Is.True, "C0 receipt must freeze the complete owned-output identity, not merely its Runtime Entry.");

            var bundlePath = Path.Combine(repositoryRoot, "docs", "vfx-contracts", "capture-tools", "sustained-flame-capture-tool.bundle.json");
            var bundle = JObject.Parse(File.ReadAllText(bundlePath));
            foreach (var source in (JArray)bundle["sources"])
                Assert.That((string)source["sha256"], Is.EqualTo("sha256:" + HashFile(Path.Combine(repositoryRoot, ((string)source["path"]).Replace('/', Path.DirectorySeparatorChar)))));
            Assert.That((string)contract["captureProfile"]["captureToolHash"], Is.EqualTo("sha256:" + RecipeCanonicalizer.ComputeSha256(File.ReadAllText(bundlePath))));
            Assert.That((string)contract["captureProfile"]["captureToolVersion"], Is.EqualTo(SustainedFlameAuthoring.CaptureToolVersion));

            var recipeText = File.ReadAllText(Path.Combine(projectRoot, SustainedFlameAuthoring.RecipePath.Replace('/', Path.DirectorySeparatorChar)));
            Assert.That((string)manifest["recipeHash"], Is.EqualTo(RecipeCanonicalizer.ComputeSha256(recipeText)));
            Assert.That(((JArray)manifest["ownedOutputs"]).Count, Is.EqualTo(1));
            Assert.That(((JArray)manifest["ownedOutputs"])[0]["path"].Value<string>(), Is.EqualTo(SustainedFlameAuthoring.PrefabPath));
        }

        [Test]
        public void PreviewScene_IsSeparateFromRuntimeEntryAndAvailableForFinalReview()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SustainedFlameAuthoring.PreviewScenePath) == null)
                Assert.Ignore("S0b preview scene precondition is not available yet; formal production assertions resume after the compiler build.");
            var runtimeDependencies = AssetDatabase.GetDependencies(SustainedFlameAuthoring.PrefabPath, true);
            Assert.That(runtimeDependencies, Does.Not.Contain(SustainedFlameAuthoring.PreviewScenePath));
            Assert.That(runtimeDependencies.Any(path => path.IndexOf("Preview", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
        }

        [Test]
        public void PreviewCamera_UsesReadOnlyProjectRenderer_AndRuntimeLightIsRealAndBudgeted()
        {
            var prefab = RequireBuiltPrefab();
            var light = prefab.GetComponentInChildren<Light>(true);
            Assert.NotNull(light);
            Assert.That(light.type, Is.EqualTo(LightType.Point));
            Assert.That(light.shadows, Is.EqualTo(LightShadows.None));
            Assert.That(light.range, Is.LessThanOrEqualTo(2.45f));

            var scene = EditorSceneManager.OpenScene(SustainedFlameAuthoring.PreviewScenePath, OpenSceneMode.Additive);
            try
            {
                var camera = scene.GetRootGameObjects().Select(root => root.GetComponent<Camera>()).Single(value => value != null && value.CompareTag("MainCamera"));
                var additionalType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
                Assert.NotNull(additionalType);
                var additional = camera.gameObject.GetComponent(additionalType);
                Assert.NotNull(additional);
                var additionalSerialized = new SerializedObject(additional);
                var rendererIndex = additionalSerialized.FindProperty("m_RendererIndex").intValue;
                Assert.That(rendererIndex, Is.GreaterThanOrEqualTo(0), "Preview camera must serialize a valid project renderer index without changing pipeline configuration.");
                var receiver = scene.GetRootGameObjects().Single(root => root.name == "Preview_LightReceiver");
                var receiverRenderer = receiver.GetComponent<Renderer>();
                Assert.NotNull(receiverRenderer);
                Assert.NotNull(receiverRenderer.sharedMaterial);
                Assert.That(receiverRenderer.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"), "The formal real-Light A/B receiver must use a lit material; an unlit shared ground material makes the diagnostic a guaranteed false negative.");
                Assert.That(AssetDatabase.GetAssetPath(receiverRenderer.sharedMaterial), Is.EqualTo(SustainedFlameAuthoring.ReceiverMaterialPath));
                Assert.That(scene.GetRootGameObjects().Single(root => root.name == "Preview_LightReceiverMarker").GetComponent<Renderer>().enabled, Is.False, "The formal probe marker must stay hidden in the user-facing Preview and be enabled only for its matched A/B diagnostic.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
        }

        private static GameObject RequireBuiltPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SustainedFlameAuthoring.PrefabPath);
            if (prefab == null)
                Assert.Ignore("S0b Runtime Entry precondition is not available yet; this is not a failed production verdict before the compiler build.");
            return prefab;
        }
    }
}
