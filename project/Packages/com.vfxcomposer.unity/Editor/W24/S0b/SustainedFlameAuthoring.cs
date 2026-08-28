using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Validation;
using VFXComposer.Editor.W24.S1;
using VFXComposer.Editor.W24.S5;

namespace VFXComposer.Editor.W24
{
    public static class SustainedFlameAuthoring
    {
        public const string EffectId = "sustained_flame_3d";
        public const string CompilerVersion = "w24-s0b-sustained-flame/1.1.1";
        public const string CaptureToolVersion = "w24-s0b-formal-capture/1.2.12";
        public const string ContractPath = "docs/vfx-contracts/sustained_flame_3d.contract.json";
        public const string TracePath = "docs/vfx-traces/sustained_flame_3d.implementation-trace.json";
        public const string ManifestPath = "ProjectSettings/VFXComposer/BuildManifests/sustained_flame_3d.manifest.json";
        public const string RecipePath = "Assets/VFX/Recipes/Aura/sustained_flame_3d.default.json";
        public const string OutputFolder = "Assets/VFX/Effects/Aura/sustained_flame_3d";
        public const string PrefabPath = OutputFolder + "/VFX_sustained_flame_3d.prefab";
        public const string PreviewScenePath = "Assets/VFX/Preview/VFXPREVIEW_SustainedFlame.unity";
        public const string ShaderPath = "Assets/VFX/Shared/Shaders/SHD_URP_FlameParticle.shader";
        // First formal builds must never change a shared asset.  These generated materials are
        // owned by this Runtime Entry and therefore participate in its output Manifest/rollback.
        // Blend state, not visual layer, defines the local Material boundary. Particle start
        // colours and independent simulation modules provide the per-layer variation. This keeps
        // the seven carriers independent while respecting the project-wide two-Material budget.
        public const string AdditiveMaterialPath = OutputFolder + "/Materials/MAT_Flame_Additive.mat";
        public const string AlphaMaterialPath = OutputFolder + "/Materials/MAT_Flame_Alpha.mat";
        // Preview-only receiver material is a read-only shared dependency. Keeping it outside
        // the Runtime Entry owned root prevents an unreachable preview helper from entering the
        // formal Manifest and keeps the runtime-local material budget at four.
        public const string ReceiverMaterialPath = "Assets/VFX/Templates/3D/Materials/VFX3D_LightReceiver.mat";

        [MenuItem("Tools/VFX Composer/W24/Build Sustained Flame Baseline")]
        public static void BuildAndOpen()
        {
            BuildAssetsAndPreview();
            EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        [MenuItem("Tools/VFX Composer/W24/Open Sustained Flame Preview")]
        public static void OpenPreview()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewScenePath) == null)
                BuildAssetsAndPreview();
            EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
        }

        public static void BuildAssetsAndPreview()
        {
            // The only authority for the first identity-populating build is the internal S5
            // pre-C0 gate. It consumes the persisted pending Contract/Trace and can grant only
            // Development + VISUAL_PENDING; no output is touched before this succeeds.
            var firstFormalApproval = ApproveFirstFormalBuild();
            // Do not let an admitted build leak half-created Prefabs/scenes/Manifest/candidates.
            // The transaction includes .meta bytes so both first-build and restore preserve GUIDs.
            using (var transaction = W24FirstFormalBuildTransaction.Begin(
                ToAbsolute(OutputFolder), ToAbsolute(RecipePath), ToAbsolute(PreviewScenePath),
                ToAbsolute(ManifestPath), RepositoryAbsolute("docs/vfx-candidates/" + EffectId + "/C0")))
            {
                EnsureFolders();
                // A clean first-formal build must create every owned input it hashes.  Earlier
                // builds accidentally depended on a Recipe left behind by an older workspace,
                // which made a fresh shadow build fail only after Prefab/Preview authoring.  Keep
                // the Recipe inside the same rollback transaction as all derived outputs.
                WriteRecipe();
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
                if (shader == null) throw new InvalidOperationException("Missing sustained flame Shader dependency: " + ShaderPath);

                var additive = UpsertFlameMaterial(AdditiveMaterialPath, shader, new Color(1.55f, .23f, .015f, .92f), new Color(2.4f, 1.25f, .16f, 1f), 2.35f, 7.5f, .18f, 1.8f, BlendMode.One);
                var alpha = UpsertFlameMaterial(AlphaMaterialPath, shader, new Color(1.0f, .18f, .04f, .72f), new Color(1.45f, .5f, .1f, .82f), 1.25f, 8.2f, .32f, 1.05f, BlendMode.OneMinusSrcAlpha);
                var prefab = BuildRuntimeEntry(additive, alpha);
                BuildPreviewScene(prefab, EnsurePreviewForwardRenderer());

                // Every asset written above is owned by this transaction. Import only those
                // targets; do not globally SaveAssets/Refresh and accidentally persist another
                // editor user's dirty shared asset.
                transaction.ImportOwnedAssets();
                W24FirstFormalBuildTransaction.ThrowIfFaultInjected("s0b.before-bootstrap-receipt");
                WriteProductionManifest(firstFormalApproval);
                // This pure postcondition check remains inside the transaction: a C0 identity
                // mismatch must roll back owned outputs instead of committing a bad formal build.
                W24FormalBatchAuthoringEntrypoints.VerifyFormalOutput(EffectId, PrefabPath, PreviewScenePath, OutputFolder);
                W24FirstFormalBuildTransaction.ThrowIfFaultInjected("s0b.after-c0-freeze");
                transaction.ImportOwnedAssets();
                transaction.Commit();
            }
        }

        private static GameObject BuildRuntimeEntry(Material additive, Material alpha)
        {
            var root = new GameObject("VFX_" + EffectId);
            try
            {
                root.layer = 1;
                var start = CreateParticleObject(root.transform, "Ignition", additive, 16, false, 24002u, 42);
                ConfigureBurst(start, 12, .24f, new ParticleSystem.MinMaxCurve(.24f, .55f), new ParticleSystem.MinMaxCurve(.15f, .55f), 24f, new Color(1f, .52f, .08f, .95f));

                var steady = new GameObject("Steady");
                steady.layer = 1;
                steady.transform.SetParent(root.transform, false);
                var coreFlame = CreateParticleObject(steady.transform, "CoreFlame", additive, 32, true, 24011u, 44);
                ConfigureSustainedFlame(coreFlame, 22f, .38f, .62f, .22f, .48f, .18f, 12f, new Color(1f, .82f, .2f, .96f));
                var outerFlame = CreateParticleObject(steady.transform, "OuterFlame", alpha, 28, true, 24021u, 42);
                ConfigureSustainedFlame(outerFlame, 15f, .72f, 1.05f, .4f, .76f, .24f, 18f, new Color(1f, .21f, .035f, .76f));
                var smokeLayer = CreateParticleObject(steady.transform, "Smoke", alpha, 18, true, 24031u, 38);
                ConfigureSmoke(smokeLayer);
                var embers = CreateParticleObject(steady.transform, "Embers", additive, 16, true, 24041u, 48);
                ConfigureEmbers(embers);

                var stop = CreateParticleObject(root.transform, "StopTail", alpha, 14, false, 24051u, 39);
                ConfigureBurst(stop, 8, .82f, new ParticleSystem.MinMaxCurve(.2f, .42f), new ParticleSystem.MinMaxCurve(.4f, .82f), 30f, new Color(.28f, .22f, .18f, .38f));
                var interrupt = CreateParticleObject(root.transform, "InterruptBurst", additive, 20, false, 24061u, 49);
                ConfigureBurst(interrupt, 16, .3f, new ParticleSystem.MinMaxCurve(.05f, .11f), new ParticleSystem.MinMaxCurve(.65f, 1.35f), 58f, new Color(1f, .5f, .08f, .95f));

                var lightObject = new GameObject("FlameLight");
                lightObject.layer = 1;
                lightObject.transform.SetParent(root.transform, false);
                lightObject.transform.localPosition = new Vector3(0f, .48f, -.08f);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, .29f, .055f);
                light.range = 2.45f;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
                light.enabled = false;

                var controller = root.AddComponent<SustainedEffectController>();
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("startRoot").objectReferenceValue = start.gameObject;
                serialized.FindProperty("steadyRoot").objectReferenceValue = steady;
                serialized.FindProperty("stopRoot").objectReferenceValue = stop.gameObject;
                serialized.FindProperty("interruptRoot").objectReferenceValue = interrupt.gameObject;
                var lights = serialized.FindProperty("controlledLights");
                lights.arraySize = 1;
                lights.GetArrayElementAtIndex(0).objectReferenceValue = light;
                serialized.FindProperty("steadyLightIntensity").floatValue = 1.25f;
                serialized.FindProperty("startDuration").floatValue = .35f;
                serialized.FindProperty("stopDuration").floatValue = .8f;
                serialized.FindProperty("interruptDuration").floatValue = .35f;
                serialized.FindProperty("cleanupDeadline").floatValue = 1.25f;
                serialized.FindProperty("canonicalSeed").longValue = 24001L;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                start.gameObject.SetActive(false);
                steady.SetActive(false);
                stop.gameObject.SetActive(false);
                interrupt.gameObject.SetActive(false);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (saved == null) throw new InvalidOperationException("Could not save sustained flame Runtime Entry: " + PrefabPath);
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static ParticleSystem CreateParticleObject(Transform parent, string name, Material material, int maxParticles, bool loop, uint seed, int sortingOrder)
        {
            var gameObject = new GameObject(name);
            gameObject.layer = 1;
            gameObject.transform.SetParent(parent, false);
            var particle = gameObject.AddComponent<ParticleSystem>();
            particle.useAutoRandomSeed = false;
            particle.randomSeed = seed;
            var main = particle.main;
            main.playOnAwake = false;
            main.loop = loop;
            main.duration = loop ? 1.37f : 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = maxParticles;
            main.gravityModifier = 0f;
            var renderer = gameObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            renderer.localBounds = new Bounds(new Vector3(0f, .72f, 0f), new Vector3(2.2f, 2.8f, 1.4f));
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return particle;
        }

        private static void ConfigureSustainedFlame(ParticleSystem particle, float rate, float lifetimeMin, float lifetimeMax, float sizeMin, float sizeMax, float speedMin, float coneAngle, Color startColor)
        {
            var main = particle.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMin + .34f);
            main.startColor = new ParticleSystem.MinMaxGradient(startColor);
            main.startRotation = new ParticleSystem.MinMaxCurve(-.22f, .22f);
            var emission = particle.emission;
            emission.rateOverTime = rate;
            var shape = particle.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = .16f;
            shape.angle = coneAngle;
            shape.length = .08f;
            var noise = particle.noise;
            noise.enabled = true;
            noise.strength = .1f;
            noise.frequency = .55f;
            noise.scrollSpeed = .28f;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            ApplyFadeAndShrink(particle, .96f, 0f, 1f, .16f);
        }

        private static void ConfigureSmoke(ParticleSystem particle)
        {
            var main = particle.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.15f, 1.65f);
            main.startSize = new ParticleSystem.MinMaxCurve(.38f, .68f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.22f, .42f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.24f, .22f, .2f, .24f));
            main.startRotation = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
            var emission = particle.emission;
            emission.rateOverTime = 5f;
            var shape = particle.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = .18f;
            shape.angle = 24f;
            var noise = particle.noise;
            noise.enabled = true;
            noise.strength = .22f;
            noise.frequency = .38f;
            noise.scrollSpeed = .16f;
            ApplyFadeAndShrink(particle, .22f, 0f, .72f, 1.35f);
        }

        private static void ConfigureEmbers(ParticleSystem particle)
        {
            var main = particle.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.72f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(.035f, .075f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.44f, .9f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .52f, .08f, .94f));
            var emission = particle.emission;
            emission.rateOverTime = 4f;
            var shape = particle.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = .14f;
            shape.angle = 38f;
            var velocity = particle.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-.18f, .18f);
            velocity.y = new ParticleSystem.MinMaxCurve(.08f, .34f);
            ApplyFadeAndShrink(particle, .94f, 0f, 1f, .05f);
        }

        private static void ConfigureBurst(ParticleSystem particle, short count, float lifetime, ParticleSystem.MinMaxCurve size, ParticleSystem.MinMaxCurve speed, float angle, Color color)
        {
            var main = particle.main;
            main.startLifetime = lifetime;
            main.startSize = size;
            main.startSpeed = speed;
            main.startColor = new ParticleSystem.MinMaxGradient(color);
            var emission = particle.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
            var shape = particle.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = .12f;
            shape.angle = angle;
            ApplyFadeAndShrink(particle, color.a, 0f, 1f, .08f);
        }

        private static void ApplyFadeAndShrink(ParticleSystem particle, float alpha, float endAlpha, float startSize, float endSize)
        {
            var color = particle.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, .1f), new GradientAlphaKey(alpha * .72f, .68f), new GradientAlphaKey(endAlpha, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);
            var size = particle.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, startSize), new Keyframe(.72f, Mathf.Lerp(startSize, endSize, .45f)), new Keyframe(1f, endSize)));
        }

        private static Material UpsertFlameMaterial(string path, Shader shader, Color baseColor, Color tipColor, float emission, float noiseScale, float noiseStrength, float scrollSpeed, BlendMode destinationBlend)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_TipColor", tipColor);
            material.SetFloat("_Emission", emission);
            material.SetFloat("_NoiseScale", noiseScale);
            material.SetFloat("_NoiseStrength", noiseStrength);
            material.SetFloat("_EdgeSoftness", .14f);
            material.SetFloat("_ScrollSpeed", scrollSpeed);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)destinationBlend);
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        private static void BuildPreviewScene(GameObject prefab, int previewRendererIndex)
        {
            var previousActive = SceneManager.GetActiveScene();
            var needsBatchRunner = !previousActive.IsValid() || !previousActive.isLoaded || string.IsNullOrEmpty(previousActive.path);
            if (needsBatchRunner && !Application.isBatchMode)
                throw new InvalidOperationException("Sustained-flame preview authoring requires a saved active scene outside batch mode; the current scene was left unchanged.");

            var batchRunnerPath = string.Empty;
            var scene = default(Scene);
            try
            {
            if (needsBatchRunner)
            {
                // An Editor Preview Scene cannot be the active scene in Unity 2022.3.  Persist a
                // normal empty batch runner just long enough to make Additive authoring legal,
                // then delete both the asset and its meta in the finally block below.
                var runner = previousActive;
                if (!runner.IsValid() || !runner.isLoaded || runner.GetRootGameObjects().Length != 0)
                    runner = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                batchRunnerPath = "Assets/__W24S0bBatchRunner_" + Guid.NewGuid().ToString("N") + ".unity";
                if (!EditorSceneManager.SaveScene(runner, batchRunnerPath))
                    throw new InvalidOperationException("Could not save the temporary sustained-flame batch runner scene.");
            }

            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            if (SceneManager.GetActiveScene() != scene && !SceneManager.SetActiveScene(scene))
                throw new InvalidOperationException("Could not activate the sustained-flame authoring scene.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "Preview_RuntimeEntry_sustained_flame_3d";
            instance.transform.position = Vector3.zero;
            var controller = instance.GetComponent<SustainedEffectController>();

            var driverObject = new GameObject("Preview_SustainedLifecycleDriver");
            var driver = driverObject.AddComponent<SustainedEffectPreviewDriver>();
            var driverSerialized = new SerializedObject(driver);
            driverSerialized.FindProperty("controller").objectReferenceValue = controller;
            driverSerialized.FindProperty("steadySeconds").floatValue = 4.5f;
            driverSerialized.FindProperty("idleSeconds").floatValue = .9f;
            driverSerialized.FindProperty("loop").boolValue = true;
            driverSerialized.ApplyModifiedPropertiesWithoutUndo();

            var cameraObject = new GameObject("MainCamera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .04f, .055f, 1f);
            camera.fieldOfView = 38f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 50f;
            camera.transform.position = new Vector3(0f, 1.12f, -4.35f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, .62f, 0f) - camera.transform.position, Vector3.up);
            SetCameraRenderer(camera, previewRendererIndex);

            var receiverMaterial = LoadReceiverMaterial();
            var receiver = GameObject.CreatePrimitive(PrimitiveType.Cube);
            receiver.name = "Preview_LightReceiver";
            receiver.transform.position = new Vector3(0f, -.13f, .05f);
            receiver.transform.localScale = new Vector3(3.8f, .12f, 2.6f);
            receiver.GetComponent<Renderer>().sharedMaterial = receiverMaterial;
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Preview_LightReceiverMarker";
            marker.transform.position = new Vector3(.15f, .42f, .12f);
            marker.transform.localScale = new Vector3(.18f, .18f, .18f);
            var markerRenderer = marker.GetComponent<Renderer>();
            markerRenderer.sharedMaterial = receiverMaterial;
            // This probe is formal diagnostic infrastructure, not part of the user-facing
            // Preview composition. The capture enables it for both matched A/B renders and
            // restores this serialized hidden state afterwards.
            markerRenderer.enabled = false;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.018f, .021f, .028f);
            EditorSceneManager.SaveScene(scene, PreviewScenePath);
            }
            finally
            {
                if (string.IsNullOrEmpty(batchRunnerPath))
                {
                    if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
                    if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                }
                else
                {
                    try
                    {
                        if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    }
                    finally
                    {
                        var absoluteRunnerPath = ToAbsolute(batchRunnerPath);
                        if ((File.Exists(absoluteRunnerPath) || File.Exists(absoluteRunnerPath + ".meta")) && !AssetDatabase.DeleteAsset(batchRunnerPath))
                            throw new InvalidOperationException("Could not delete the temporary sustained-flame batch runner scene.");
                        if (File.Exists(absoluteRunnerPath) || File.Exists(absoluteRunnerPath + ".meta"))
                            throw new InvalidOperationException("The temporary sustained-flame batch runner scene was not fully deleted.");
                    }
                }
            }
        }

        private static int EnsurePreviewForwardRenderer()
        {
            // Shared renderer provisioning is a separate isolated infrastructure transaction;
            // an effect build may only read and select the already-registered renderer.
            return W24PreviewRendererInfrastructure.RequireRendererIndex();
        }

        private static void SetCameraRenderer(Camera camera, int rendererIndex)
        {
            var additionalCameraDataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (additionalCameraDataType == null)
                throw new InvalidOperationException("URP UniversalAdditionalCameraData type is unavailable.");
            var additionalCameraData = camera.gameObject.GetComponent(additionalCameraDataType) ?? camera.gameObject.AddComponent(additionalCameraDataType);
            var setRenderer = additionalCameraDataType.GetMethod("SetRenderer", new[] { typeof(int) });
            if (setRenderer == null)
                throw new InvalidOperationException("URP camera SetRenderer API is unavailable.");
            setRenderer.Invoke(additionalCameraData, new object[] { rendererIndex });

            var serialized = new SerializedObject(additionalCameraData);
            var rendererProperty = serialized.FindProperty("m_RendererIndex");
            if (rendererProperty == null || rendererProperty.intValue != rendererIndex)
                throw new InvalidOperationException("Preview camera did not retain the requested Forward Renderer index.");
        }

        private static Material LoadReceiverMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(ReceiverMaterialPath);
            if (material == null) throw new InvalidOperationException("Missing read-only sustained-flame receiver material dependency: " + ReceiverMaterialPath);
            if (material.shader == null || !string.Equals(material.shader.name, "Universal Render Pipeline/Lit", StringComparison.Ordinal))
                throw new InvalidOperationException("Sustained-flame receiver dependency must use URP/Lit so the real Point Light A/B diagnostic cannot silently render an unlit false negative.");
            return material;
        }

        private static W24S5FirstFormalBuildApproval ApproveFirstFormalBuild()
        {
            var contractPath = RepositoryAbsolute(ContractPath);
            var tracePath = RepositoryAbsolute(TracePath);
            if (!File.Exists(contractPath)) throw new FileNotFoundException("Missing W24 baseline design contract.", contractPath);
            if (!File.Exists(tracePath)) throw new FileNotFoundException("Missing W24 baseline Implementation Trace preregistration.", tracePath);
            var result = W24S5ProductionGate.EvaluateFirstFormalBuild(new W24S5FirstFormalBuildRequest
            {
                EffectId = EffectId,
                ContractPath = ContractPath,
                ContractFileHash = "sha256:" + HashFile(contractPath),
                TracePath = TracePath,
                TraceFileHash = "sha256:" + HashFile(tracePath),
                ExpectedRuntimeEntryPath = PrefabPath,
                ExpectedManifestPath = ManifestPath,
                OwnedOutputRoot = OutputFolder,
                Intent = W24S5BuildIntent.Development,
                VisualStatus = W24S5VisualStatus.VISUAL_PENDING
            });
            if (result.HasErrors || result.FirstFormalApproval == null)
                throw new InvalidOperationException("Sustained flame pre-C0 production gate rejected the build: " + string.Join(" | ", result.Issues.Select(issue => issue.Code + " " + issue.Path + " " + issue.Message).ToArray()));
            return result.FirstFormalApproval;
        }

        private static void WriteProductionManifest(W24S5FirstFormalBuildApproval approval)
        {
            var recipeText = File.ReadAllText(ToAbsolute(RecipePath));
            var recipeHash = RecipeCanonicalizer.ComputeSha256(recipeText);
            var dependencyIdentity = string.Join("\n", new[] { ShaderPath, AdditiveMaterialPath, AlphaMaterialPath }
                .Select(path => path + ":" + AssetDatabase.GetAssetDependencyHash(path).ToString()));
            // Build identity is deliberately contract/evidence independent. The S5 binding
            // records immutable Contract/Trace file identities beside this buildHash, avoiding
            // the capture-contract-manifest hash cycle.
            var buildHash = HashText(CompilerVersion + "\n" + recipeHash + "\n" + dependencyIdentity);
            var audit = W24S5ProductionGate.CommitFirstFormalBuild(approval, "aura", 1, 1, recipeHash, buildHash, CompilerVersion, 5.7, RecipePath);
            if (audit.Report.HasErrors)
                throw new InvalidOperationException("Sustained flame production audit failed: " + string.Join(" | ", audit.Report.Entries.Select(entry => entry.Code + " " + entry.Path + " " + entry.Message).ToArray()));
            W24S5BootstrapReceipt bootstrap;
            string receiptError;
            if (!W24S5ProductionGate.TryGetBootstrapReceipt(approval, out bootstrap, out receiptError))
                throw new InvalidOperationException("Sustained flame bootstrap receipt was not issued after the gate-owned commit: " + receiptError);
            // The immutable preregistration remains the PRE_C0 receipt. C0 is a distinct,
            // write-once candidate record with real build/scene/GUID identities but no evidence.
            W24CandidateIdentityFreezer.FreezeC0(bootstrap, PreviewScenePath);
        }

        private static void WriteRecipe()
        {
            // Explicit LF separators and UTF-8 without BOM make Recipe bytes stable across the
            // Windows authoring host and any future CI host.  This is the owned source document;
            // Runtime assets are still generated by the normal authoring path above.
            var lines = new[]
            {
                "{",
                "  \"recipeVersion\": 1,",
                "  \"revision\": 1,",
                "  \"id\": \"sustained_flame_3d\",",
                "  \"name\": \"Sustained Flame 3D\",",
                "  \"dimension\": \"3d\",",
                "  \"archetype\": \"aura\",",
                "  \"style\": {",
                "    \"token\": \"semireal\",",
                "    \"palette\": {",
                "      \"primary\": \"#FF4A08\",",
                "      \"secondary\": \"#FFB21A\",",
                "      \"accent\": \"#FFF1A0\"",
                "    },",
                "    \"outline\": 0.0,",
                "    \"shading_steps\": 5,",
                "    \"noise_scale\": 2.4,",
                "    \"glow_strength\": 1.35",
                "  },",
                "  \"behavior\": {",
                "    \"hit\": {\"type\":\"single\"},",
                "    \"emission\":{\"type\":\"single\"},",
                "    \"timing\":{\"type\":\"sustained\"}",
                "  },",
                "  \"content\": {",
                "    \"family\":\"fire\",",
                "    \"parameters\":{",
                "      \"start_duration\":0.35,",
                "      \"steady_minimum\":4.5,",
                "      \"stop_duration\":0.8,",
                "      \"interrupt_duration\":0.35,",
                "      \"cleanup_deadline\":1.25,",
                "      \"light_intensity\":1.25,",
                "      \"core_rate\":22,",
                "      \"outer_rate\":15,",
                "      \"smoke_rate\":5,",
                "      \"ember_rate\":4",
                "    }",
                "  },",
                "  \"targetProfile\":\"mobile_medium\",",
                "  \"randomSeed\":24001,",
                "  \"stages\":[{",
                "    \"id\":\"main\",\"trigger\":\"manual\",\"duration\":5.7,\"enabled\":true,",
                "    \"modules\":[",
                "      {\"id\":\"core\",\"kind\":\"energy_body\",\"templateId\":\"PFT_3D_FireCore\",\"parameters\":{\"scale\":0.72},\"enabled\":true},",
                "      {\"id\":\"outer_flame\",\"kind\":\"motion_trail\",\"templateId\":\"PFT_3D_FireTrail\",\"parameters\":{\"time\":0.9,\"width\":0.62},\"attachTo\":\"core\",\"enabled\":true},",
                "      {\"id\":\"embers\",\"kind\":\"secondary_particles\",\"templateId\":\"PFT_3D_Embers\",\"parameters\":{\"rate\":4,\"lifetime\":1.1},\"attachTo\":\"core\",\"enabled\":true}",
                "    ]",
                "  }],",
                "  \"metadata\":{",
                "    \"createdBy\":\"w24-s0b-vertical-slice\",",
                "    \"templateCatalogVersion\":\"1.0.0\",",
                "    \"designContract\":\"docs/vfx-contracts/sustained_flame_3d.contract.json\"",
                "  }",
                "}"
            };
            File.WriteAllText(ToAbsolute(RecipePath), string.Join("\n", lines) + "\n", new UTF8Encoding(false));
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/VFX/Effects");
            EnsureFolder("Assets/VFX/Effects/Aura");
            EnsureFolder("Assets/VFX/Recipes");
            EnsureFolder("Assets/VFX/Recipes/Aura");
            EnsureFolder(OutputFolder);
            EnsureFolder(OutputFolder + "/Materials");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = path.Substring(0, path.LastIndexOf('/'));
            var name = path.Substring(path.LastIndexOf('/') + 1);
            EnsureFolder(parent);
            var guid = AssetDatabase.CreateFolder(parent, name);
            if (string.IsNullOrEmpty(guid) || !string.Equals(AssetDatabase.GUIDToAssetPath(guid), path, StringComparison.Ordinal))
                throw new InvalidOperationException("Could not create exact VFX folder: " + path);
        }

        private static string RepositoryAbsolute(string relativePath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var repositoryRoot = Directory.GetParent(projectRoot).FullName;
            return Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ToAbsolute(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static string HashText(string text)
        {
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }
}
