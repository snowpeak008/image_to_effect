using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Validation;
using VFXComposer.Editor.W24.S1;
using VFXComposer.Editor.W24.S5;
using VFXComposer.W24;

namespace VFXComposer.Editor.W24.S3
{
    /// <summary>
    /// Authoring-only builder for W24 S3. It deliberately produces three isolated Runtime Entries
    /// and three scene-only preview rigs; it never modifies S0b/S1/S2 assets or code.
    /// </summary>
    public static class W24S3BaselineAuthoring
    {
        public const string Root = "Assets/VFX/Generated";
        public const string CompilerVersion = "w24-s3-baseline/2.7";
        public const int FormalDiagnosticLayer = 30;
        public const uint BindingModelObjectId = 10u;
        public const uint BindingSocketObjectId = 101u;
        public const uint BindingFragmentFirstObjectId = 201u;
        public const uint ProjectileCanonicalSeed = 24101u;
        public const uint BindingCanonicalSeed = 24201u;
        public const uint LightingCanonicalSeed = 24301u;
        public const string ProjectileId = "w24_moving_projectile_trail";
        public const string BindingId = "w24_weapon_socket_fragments";
        public const string LightingId = "w24_real_light_receivers";
        public const string ProjectileOutputFolder = Root + "/" + ProjectileId;
        public const string BindingOutputFolder = Root + "/" + BindingId;
        public const string LightingOutputFolder = Root + "/" + LightingId;
        public const string ProjectilePrefab = ProjectileOutputFolder + "/VFX_" + ProjectileId + ".prefab";
        public const string BindingPrefab = BindingOutputFolder + "/VFX_" + BindingId + ".prefab";
        public const string LightingPrefab = LightingOutputFolder + "/VFX_" + LightingId + ".prefab";
        public const string ProjectilePreview = "Assets/VFX/Preview/W24S3/VFXPREVIEW_MovingProjectileTrail.unity";
        public const string BindingPreview = "Assets/VFX/Preview/W24S3/VFXPREVIEW_ModelSocketFragments.unity";
        public const string LightingPreview = "Assets/VFX/Preview/W24S3/VFXPREVIEW_RealLightReceivers.unity";
        public const string ProjectileManifest = "ProjectSettings/VFXComposer/BuildManifests/" + ProjectileId + ".manifest.json";
        public const string BindingManifest = "ProjectSettings/VFXComposer/BuildManifests/" + BindingId + ".manifest.json";
        public const string LightingManifest = "ProjectSettings/VFXComposer/BuildManifests/" + LightingId + ".manifest.json";
        public const string ProjectileRecipe = "Assets/VFX/Recipes/Projectile/" + ProjectileId + ".w24s3.json";
        public const string BindingRecipe = "Assets/VFX/Recipes/Trail/" + BindingId + ".w24s3.json";
        public const string LightingRecipe = "Assets/VFX/Recipes/Impact/" + LightingId + ".w24s3.json";
        // Every first-build material belongs to exactly one effect output root.  This is more
        // verbose than a shared material, but lets a failed C0 admission restore all touched
        // bytes/GUIDs without ever writing a shared no-ownership dependency.
        public const string ProjectileMaterial = ProjectileOutputFolder + "/MAT_W24S3_ProjectileLit.mat";
        public const string BindingMaterial = BindingOutputFolder + "/MAT_W24S3_BindingLit.mat";
        public const string LightingMaterial = LightingOutputFolder + "/MAT_W24S3_LightingReceiverNeutral.mat";
        public const string LightingEmissiveCoreMaterial = LightingOutputFolder + "/MAT_W24S3_LightingCoreEmissive.mat";

        [MenuItem("VFX Composer/W24/S3/Build three remaining baselines")]
        public static void BuildAll()
        {
            // Admission happens before the authoring transaction.  Once it succeeds, either all
            // three first-build identities exist or every owned file (including .meta GUIDs) is
            // restored to the exact prior state.
            var projectileApproval = ApproveFirstFormalBuild(ProjectileId, ProjectilePrefab, ProjectileOutputFolder, ProjectileManifest, "docs/vfx-contracts/w24_moving_projectile_trail.contract.json", "docs/vfx-traces/w24_moving_projectile_trail.implementation-trace.json");
            var bindingApproval = ApproveFirstFormalBuild(BindingId, BindingPrefab, BindingOutputFolder, BindingManifest, "docs/vfx-contracts/w24_weapon_socket_fragments.contract.json", "docs/vfx-traces/w24_weapon_socket_fragments.implementation-trace.json");
            var lightingApproval = ApproveFirstFormalBuild(LightingId, LightingPrefab, LightingOutputFolder, LightingManifest, "docs/vfx-contracts/w24_real_light_receivers.contract.json", "docs/vfx-traces/w24_real_light_receivers.implementation-trace.json");
            using (var transaction = W24FirstFormalBuildTransaction.Begin(
                ToAbsolute(ProjectileOutputFolder), ToAbsolute(BindingOutputFolder), ToAbsolute(LightingOutputFolder),
                ToAbsolute(ProjectilePreview), ToAbsolute(BindingPreview), ToAbsolute(LightingPreview),
                ToAbsolute(ProjectileRecipe), ToAbsolute(BindingRecipe), ToAbsolute(LightingRecipe),
                ToAbsolute(ProjectileManifest), ToAbsolute(BindingManifest), ToAbsolute(LightingManifest),
                RepositoryAbsolute("docs/vfx-candidates/" + ProjectileId + "/C0"),
                RepositoryAbsolute("docs/vfx-candidates/" + BindingId + "/C0"),
                RepositoryAbsolute("docs/vfx-candidates/" + LightingId + "/C0")))
            {
                EnsureFolder("Assets/VFX");
                EnsureFolder("Assets/VFX/Recipes"); EnsureFolder("Assets/VFX/Recipes/Projectile"); EnsureFolder("Assets/VFX/Recipes/Trail"); EnsureFolder("Assets/VFX/Recipes/Impact");
                EnsureFolder(Root);
                EnsureFolder(ProjectileOutputFolder); EnsureFolder(BindingOutputFolder); EnsureFolder(LightingOutputFolder);
                EnsureFolder("Assets/VFX/Preview"); EnsureFolder("Assets/VFX/Preview/W24S3");

                WriteRecipe(ProjectileRecipe, ProjectileId, "projectile", "one_shot", 1.82, "#7EDBFF", "#EAFBFF");
                WriteRecipe(BindingRecipe, BindingId, "weapon_trail", "event_driven", 1.7, "#8C63FF", "#E6D7FF");
                WriteRecipe(LightingRecipe, LightingId, "impact", "event_driven", 1.5, "#FF6A20", "#FFD08A");
                AssetDatabase.ImportAsset(ProjectileRecipe, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(BindingRecipe, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(LightingRecipe, ImportAssetOptions.ForceSynchronousImport);

                var projectileMaterial = UpsertNeutralLitMaterial(ProjectileMaterial);
                var bindingMaterial = UpsertNeutralLitMaterial(BindingMaterial);
                var lightingMaterial = UpsertNeutralLitMaterial(LightingMaterial);
                var lightingCoreMaterial = UpsertEmissiveCoreLitMaterial(LightingEmissiveCoreMaterial);
                // Materials are saved individually when authored. Do not flush unrelated dirty
                // assets from the editor through a global AssetDatabase.SaveAssets call.
                BuildProjectile(projectileMaterial); BuildBinding(bindingMaterial); BuildLighting(lightingMaterial, lightingCoreMaterial);
                BuildProjectilePreview(projectileMaterial); BuildBindingPreview(bindingMaterial); BuildLightingPreview(lightingMaterial);
                transaction.ImportOwnedAssets();
                // Importing the complete owned output folders may invoke URP's material
                // validation after the individual assets were first saved. Reassert the exact
                // persisted contracts now, before any Manifest/build/C0 identity is computed.
                FinalizePersistedMaterialContracts();
                W24FirstFormalBuildTransaction.ThrowIfFaultInjected("s3.before-bootstrap-receipts");
                WriteProductionManifest(ProjectileId, "projectile", ProjectileRecipe, ProjectilePrefab, ProjectileOutputFolder, ProjectilePreview, 1.82, projectileApproval);
                WriteProductionManifest(BindingId, "weapon_trail", BindingRecipe, BindingPrefab, BindingOutputFolder, BindingPreview, 1.7, bindingApproval);
                WriteProductionManifest(LightingId, "impact", LightingRecipe, LightingPrefab, LightingOutputFolder, LightingPreview, 1.5, lightingApproval);
                // Validate all three frozen C0 identities before Commit so a bad binding follows
                // W24FirstFormalBuildTransaction rollback rather than becoming a formal output.
                VFXComposer.Editor.W24.W24FormalBatchAuthoringEntrypoints.VerifyFormalOutput(ProjectileId, ProjectilePrefab, ProjectilePreview, ProjectileOutputFolder);
                VFXComposer.Editor.W24.W24FormalBatchAuthoringEntrypoints.VerifyFormalOutput(BindingId, BindingPrefab, BindingPreview, BindingOutputFolder);
                VFXComposer.Editor.W24.W24FormalBatchAuthoringEntrypoints.VerifyFormalOutput(LightingId, LightingPrefab, LightingPreview, LightingOutputFolder);
                W24FirstFormalBuildTransaction.ThrowIfFaultInjected("s3.after-c0-freezes");
                transaction.ImportOwnedAssets();
                // The transaction's last directory import is deliberately after C0 freeze.
                // It must be read-only with respect to all frozen material identities: fail and
                // roll back the whole first build if a render-pipeline validator changes them.
                ValidatePersistedMaterialContracts();
                VFXComposer.Editor.W24.W24FormalBatchAuthoringEntrypoints.VerifyFormalOutput(ProjectileId, ProjectilePrefab, ProjectilePreview, ProjectileOutputFolder);
                VFXComposer.Editor.W24.W24FormalBatchAuthoringEntrypoints.VerifyFormalOutput(BindingId, BindingPrefab, BindingPreview, BindingOutputFolder);
                VFXComposer.Editor.W24.W24FormalBatchAuthoringEntrypoints.VerifyFormalOutput(LightingId, LightingPrefab, LightingPreview, LightingOutputFolder);
                transaction.Commit();
            }
        }

        private static void BuildProjectile(Material material)
        {
            var root = new GameObject("VFX_" + ProjectileId);
            try
            {
                var launch = Child(root, "Launch"); var travel = Child(root, "Travel"); var impact = Child(root, "ImpactResidue");
                AddParticle(launch, material, 10); AddParticle(travel, material, 18); AddParticle(impact, material, 14);
                var trailObject = Child(travel, "WorldSpaceTrail"); var trail = trailObject.AddComponent<TrailRenderer>();
                trail.time = .42f; trail.minVertexDistance = .015f; trail.widthMultiplier = .09f; trail.sharedMaterial = material;
                trail.emitting = false; trailObject.SetActive(true);
                var motion = root.AddComponent<W24MovingEmitterTrailProtocol>();
                // Unity 2022.3 TrailRenderer is intrinsically world-space and exposes no
                // LineRenderer-style useWorldSpace property. Serialize the equivalent formal
                // invariant on its motion protocol and fail closed if it is ever disabled.
                SetObject(motion, "motionSource", root.transform); SetArray(motion, "trails", new UnityEngine.Object[] { trail }); SetBool(motion, "requireWorldSpaceHistory", true);
                var entry = root.AddComponent<W24S3RuntimeEntry>(); var timeline = root.AddComponent<W24SemanticTimeline>();
                AssignS3Entry(entry, launch, travel, impact, motion, null, null, null, timeline, null, false, ProjectileCanonicalSeed);
                SetLayerRecursively(root, 1); // TransparentFX: the formal effect-only diagnostic mask.
                PrefabUtility.SaveAsPrefabAsset(root, ProjectilePrefab);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void BuildBinding(Material material)
        {
            var root = new GameObject("VFX_" + BindingId);
            try
            {
                var launch = Child(root, "Attach"); var travel = Child(root, "SocketTrail"); var impact = Child(root, "DetachCleanup");
                AddParticle(launch, material, 8); AddParticle(travel, material, 12); AddParticle(impact, material, 8);
                var visual = Child(root, "SocketVisualRoot");
                var binding = root.AddComponent<W24ModelBindingAdapter>();
                SetObject(binding, "visualRoot", visual.transform);
                var bindingSerialized = new SerializedObject(binding);
                bindingSerialized.FindProperty("request.Target").enumValueIndex = (int)W24BindingTarget.Socket;
                bindingSerialized.FindProperty("request.TargetName").stringValue = "weapon_socket";
                bindingSerialized.ApplyModifiedPropertiesWithoutUndo();
                var fragments = new Transform[3];
                for (var index = 0; index < fragments.Length; index++)
                {
                    var fragment = GameObject.CreatePrimitive(PrimitiveType.Cube); fragment.name = "IndependentFragment_" + index;
                    fragment.transform.SetParent(visual.transform, false); fragment.transform.localPosition = new Vector3((index - 1) * .14f, .06f * index, 0f); fragment.transform.localScale = Vector3.one * .08f;
                    var fragmentRenderer = fragment.GetComponent<MeshRenderer>(); fragmentRenderer.sharedMaterial = material;
                    ConfigureDiagnostic(fragmentRenderer, BindingFragmentFirstObjectId + (uint)index, "independent_fragment_" + index);
                    fragments[index] = fragment.transform;
                }
                var fragmentMotion = visual.AddComponent<W24FragmentMotionSystem>(); SetArray(fragmentMotion, "fragments", fragments);
                foreach (var fragment in fragments) fragment.gameObject.SetActive(false);
                var entry = root.AddComponent<W24S3RuntimeEntry>(); var timeline = root.AddComponent<W24SemanticTimeline>();
                AssignS3Entry(entry, launch, travel, impact, null, binding, visual.transform, fragmentMotion, timeline, null, true, BindingCanonicalSeed);
                SetLayerRecursively(root, 1); // Keep the model itself on Default so attachment is independently observable.
                PrefabUtility.SaveAsPrefabAsset(root, BindingPrefab);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void BuildLighting(Material neutralMaterial, Material emissiveCoreMaterial)
        {
            var root = new GameObject("VFX_" + LightingId);
            try
            {
                var launch = Child(root, "MuzzleFlash"); var travel = Child(root, "SustainedFlame"); var impact = Child(root, "LightStopTail");
                AddParticle(launch, neutralMaterial, 8); AddParticle(travel, neutralMaterial, 12); AddParticle(impact, neutralMaterial, 12);
                // The physical-light baseline needs a real, drawable effect carrier in addition
                // to ParticleSystemRenderers.  This small mesh is part of the Runtime Entry (not
                // a diagnostic proxy), so Beauty and the typed effect-mask both prove that the
                // light source body is actually present while the receiver probes remain scene-only.
                var lightCore = GameObject.CreatePrimitive(PrimitiveType.Sphere); lightCore.name = "PhysicalLightCoreMesh";
                lightCore.transform.SetParent(travel.transform, false); lightCore.transform.localScale = Vector3.one * .24f;
                UnityEngine.Object.DestroyImmediate(lightCore.GetComponent<Collider>());
                var lightCoreRenderer = lightCore.GetComponent<MeshRenderer>(); lightCoreRenderer.sharedMaterial = emissiveCoreMaterial;
                var muzzle = MakeLight(root, "MuzzleFlashLight", new Color(1f, .55f, .18f), 2.4f, 3.1f);
                var sustained = MakeLight(root, "SustainedFireLight", new Color(1f, .28f, .08f), 1.2f, 2.4f);
                var lighting = root.AddComponent<W24RealLightingModule>();
                SetArray(lighting, "lights3D", new UnityEngine.Object[] { muzzle, sustained }); SetInt(lighting, "maximum3DLights", 2); SetFloat(lighting, "maximumIntensity", 2.4f);
                lighting.ResetForPool();
                var entry = root.AddComponent<W24S3RuntimeEntry>(); var timeline = root.AddComponent<W24SemanticTimeline>();
                AssignS3Entry(entry, launch, travel, impact, null, null, null, null, timeline, lighting, false, LightingCanonicalSeed);
                SetLayerRecursively(root, 1); // Receiver probes stay on Default and cannot contaminate effect-only captures.
                PrefabUtility.SaveAsPrefabAsset(root, LightingPrefab);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void BuildProjectilePreview(Material material)
        {
            WithScene(ProjectilePreview, scene =>
            {
                var instance = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefab), scene) as GameObject;
                instance.name = "PreviewRuntimeEntry_" + ProjectileId;
                var driver = instance.AddComponent<W24S3PreviewDriver>(); SetObject(driver, "entry", instance.GetComponent<W24S3RuntimeEntry>()); SetInt(driver, "mode", 0);
                AddCamera(new Vector3(0f, 1.3f, -6.3f), new Vector3(0f, .8f, 0f));
                AddReceiver("ProjectileImpactReceiver", new Vector3(2.4f, .2f, 0f), new Vector3(.45f, .45f, .45f), material);
            });
        }

        private static void BuildBindingPreview(Material material)
        {
            WithScene(BindingPreview, scene =>
            {
                var model = GameObject.CreatePrimitive(PrimitiveType.Capsule); model.name = "PreviewTestModel_MeshRenderer"; model.transform.position = new Vector3(0f, 1f, 0f);
                var modelRenderer = model.GetComponent<MeshRenderer>(); modelRenderer.sharedMaterial = material;
                ConfigureDiagnostic(modelRenderer, BindingModelObjectId, "bound_model");
                var socket = Child(model, "weapon_socket"); socket.transform.localPosition = new Vector3(.62f, .35f, -.08f);
                var socketMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere); socketMarker.name = "FormalDiagnosticSocketMarker"; socketMarker.transform.SetParent(socket.transform, false); socketMarker.transform.localScale = Vector3.one * .08f; socketMarker.layer = FormalDiagnosticLayer;
                UnityEngine.Object.DestroyImmediate(socketMarker.GetComponent<Collider>());
                var socketRenderer = socketMarker.GetComponent<MeshRenderer>(); socketRenderer.sharedMaterial = material; socketRenderer.enabled = true;
                ConfigureDiagnostic(socketRenderer, BindingSocketObjectId, "weapon_socket_marker");
                var instance = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BindingPrefab), scene) as GameObject;
                instance.name = "PreviewRuntimeEntry_" + BindingId;
                var entry = instance.GetComponent<W24S3RuntimeEntry>(); entry.ConfigureModelRoot(model.transform);
                var driver = instance.AddComponent<W24S3PreviewDriver>(); SetObject(driver, "entry", entry); SetObject(driver, "modelRoot", model.transform); SetInt(driver, "mode", 1);
                AddCamera(new Vector3(0f, 1.4f, -5.4f), new Vector3(0f, 1f, 0f));
            });
        }

        private static void BuildLightingPreview(Material material)
        {
            WithScene(LightingPreview, scene =>
            {
                var instance = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LightingPrefab), scene) as GameObject;
                instance.name = "PreviewRuntimeEntry_" + LightingId;
                var driver = instance.AddComponent<W24S3PreviewDriver>(); SetObject(driver, "entry", instance.GetComponent<W24S3RuntimeEntry>()); SetInt(driver, "mode", 2);
                AddReceiver("Receiver_A_LinearProbe", new Vector3(-1.05f, .35f, .35f), new Vector3(.8f, .7f, .18f), material);
                AddReceiver("Receiver_B_LinearProbe", new Vector3(1.12f, .62f, .45f), new Vector3(.66f, 1.24f, .18f), material);
                AddCamera(new Vector3(0f, 1.5f, -5.5f), new Vector3(0f, .72f, 0f));
            });
        }

        private static void WithScene(string path, Action<Scene> build)
        {
            var previous = SceneManager.GetActiveScene();
            var needsBatchRunner = !previous.IsValid() || !previous.isLoaded || string.IsNullOrEmpty(previous.path);
            // Unity cannot create an additive scene while its only active scene is Untitled.
            // Do not save or replace an interactive user's scene: callers must start from a
            // saved scene outside batch mode.
            if (needsBatchRunner && !Application.isBatchMode)
                throw new InvalidOperationException("W24 S3 preview authoring requires a saved active scene outside batch mode; the current scene was left unchanged.");

            var batchRunnerPath = string.Empty;
            var scene = default(Scene);
            try
            {
                if (needsBatchRunner)
                {
                    // Batch mode has no user scene to preserve.  Persist its empty Untitled
                    // runner at a unique asset path only long enough to make Additive legal.
                    var runner = previous;
                    // EditMode test runners can place transient roots in the initial Untitled
                    // scene. They are not user content in batch mode; replace that scene with a
                    // clean runner instead of rejecting an otherwise valid isolated build.
                    if (!runner.IsValid() || !runner.isLoaded || runner.GetRootGameObjects().Length != 0)
                        runner = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    batchRunnerPath = "Assets/__W24S3BatchRunner_" + Guid.NewGuid().ToString("N") + ".unity";
                    if (!EditorSceneManager.SaveScene(runner, batchRunnerPath))
                        throw new InvalidOperationException("Could not save the temporary W24 S3 batch runner scene.");
                }

                // This is always a normal scene (never a Preview Scene), so it is safe to
                // activate in batch mode and remains isolated from the caller's GUI scene.
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                // NewScene can make the new scene active immediately; SetActiveScene returns
                // false when no transition is needed on some Unity 2022.3 test-runner paths.
                if (SceneManager.GetActiveScene() != scene && !SceneManager.SetActiveScene(scene))
                    throw new InvalidOperationException("Could not activate the W24 S3 authoring scene.");
                build(scene);
                RenderSettings.ambientLight = new Color(.025f, .025f, .03f, 1f);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Could not save the W24 S3 preview scene: " + path);
            }
            finally
            {
                if (string.IsNullOrEmpty(batchRunnerPath))
                {
                    // Restore before closing the temporary scene so the user's original active
                    // scene remains active in the GUI.
                    if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                    if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                }
                else
                {
                    try
                    {
                        if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                        // Replace the saved temporary runner with the empty Untitled runner
                        // that batch EditMode started with before deleting its asset.
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    }
                    finally
                    {
                        // DeleteAsset removes the scene and its .meta together; verify neither
                        // remains so a failed batch cannot leave a unique runner artifact.
                        var absoluteRunnerPath = ToAbsolute(batchRunnerPath);
                        if ((File.Exists(absoluteRunnerPath) || File.Exists(absoluteRunnerPath + ".meta")) && !AssetDatabase.DeleteAsset(batchRunnerPath))
                            throw new InvalidOperationException("Could not delete the temporary W24 S3 batch runner scene.");
                        if (File.Exists(absoluteRunnerPath) || File.Exists(absoluteRunnerPath + ".meta"))
                            throw new InvalidOperationException("The temporary W24 S3 batch runner scene was not fully deleted.");
                    }
                }
            }
        }

        private static void WriteRecipe(string recipePath, string effectId, string archetype, string lifecycle, double duration, string primaryColor, string secondaryColor)
        {
            var recipe = new JObject
            {
                ["recipeVersion"] = 1,
                ["revision"] = 1,
                ["id"] = effectId,
                ["archetype"] = archetype,
                ["dimension"] = "3d",
                ["lifecycle"] = lifecycle,
                ["duration"] = duration,
                ["primaryColor"] = primaryColor,
                ["secondaryColor"] = secondaryColor
            };
            WriteDeterministic(ToAbsolute(recipePath), recipe.ToString(Formatting.Indented) + "\n");
        }

        private static W24S5FirstFormalBuildApproval ApproveFirstFormalBuild(string effectId, string prefabPath, string outputFolder, string manifestPath, string contractPath, string tracePath)
        {
            var contractAbsolute = RepositoryAbsolute(contractPath);
            var traceAbsolute = RepositoryAbsolute(tracePath);
            if (!File.Exists(contractAbsolute)) throw new FileNotFoundException("Missing W24 S3 design contract.", contractAbsolute);
            if (!File.Exists(traceAbsolute)) throw new FileNotFoundException("Missing independent W24 S3 Implementation Trace preregistration.", traceAbsolute);
            var result = W24S5ProductionGate.EvaluateFirstFormalBuild(new W24S5FirstFormalBuildRequest
            {
                EffectId = effectId,
                ContractPath = contractPath,
                ContractFileHash = "sha256:" + HashFile(contractAbsolute),
                TracePath = tracePath,
                TraceFileHash = "sha256:" + HashFile(traceAbsolute),
                ExpectedRuntimeEntryPath = prefabPath,
                ExpectedManifestPath = manifestPath,
                OwnedOutputRoot = outputFolder,
                Intent = W24S5BuildIntent.Development,
                VisualStatus = W24S5VisualStatus.VISUAL_PENDING
            });
            if (result.HasErrors || result.FirstFormalApproval == null)
                throw new InvalidOperationException("W24 S3 pre-C0 production gate rejected " + effectId + ": " + string.Join(" | ", result.Issues.Select(issue => issue.Code + " " + issue.Path + " " + issue.Message).ToArray()));
            return result.FirstFormalApproval;
        }

        private static void WriteProductionManifest(string effectId, string archetype, string recipePath, string prefabPath, string outputFolder, string previewScenePath, double duration, W24S5FirstFormalBuildApproval approval)
        {
            var recipeHash = RecipeCanonicalizer.ComputeSha256(File.ReadAllText(ToAbsolute(recipePath)));
            var buildHash = BuildHash(effectId, recipeHash);
            var audit = W24S5ProductionGate.CommitFirstFormalBuild(approval, archetype, 1, 1, recipeHash, buildHash, CompilerVersion, duration, recipePath);
            if (audit.Report.HasErrors)
                throw new InvalidOperationException("W24 S3 production rules rejected " + effectId + ": " + string.Join(" | ", audit.Report.Entries.Select(entry => entry.Code + " " + entry.Path + " " + entry.Message).ToArray()));
            W24S5BootstrapReceipt bootstrap;
            string receiptError;
            if (!W24S5ProductionGate.TryGetBootstrapReceipt(approval, out bootstrap, out receiptError))
                throw new InvalidOperationException("W24 S3 bootstrap receipt was not issued after the gate-owned commit for " + effectId + ": " + receiptError);
            W24CandidateIdentityFreezer.FreezeC0(bootstrap, previewScenePath);
        }

        private static string BuildHash(string effectId, string recipeHash)
        {
            // Contract/evidence identities are deliberately excluded: the Capture Profile binds
            // this buildHash, while the Manifest binds contractHash separately, so no hash cycle exists.
            var common = new[]
            {
                MaterialPath(effectId),
                "Packages/com.vfxcomposer.unity/Editor/W24/S3/W24S3BaselineAuthoring.cs",
                "Packages/com.vfxcomposer.unity/Runtime/W24/W24S3RuntimeEntry.cs",
                "Packages/com.vfxcomposer.unity/Runtime/W24/W24SemanticTimeline.cs"
            };
            var specific = effectId == ProjectileId
                ? new[] { "Packages/com.vfxcomposer.unity/Runtime/W24/W24MovingEmitterTrailProtocol.cs" }
                : effectId == BindingId
                    ? new[] { "Packages/com.vfxcomposer.unity/Runtime/W24/W24ModelBindingAdapter.cs", "Packages/com.vfxcomposer.unity/Runtime/W24/W24FragmentMotionSystem.cs", "Packages/com.vfxcomposer.unity/Runtime/Diagnostics/W24DiagnosticObjectRegistration.cs" }
                    : new[] { LightingEmissiveCoreMaterial, "Packages/com.vfxcomposer.unity/Runtime/W24/W24RealLightingModule.cs" };
            var signature = new StringBuilder().Append(recipeHash).Append('|').Append(CompilerVersion).Append('|').Append(Application.unityVersion);
            foreach (var path in common.Concat(specific).OrderBy(path => path, StringComparer.Ordinal))
            {
                var identity = path.StartsWith("Assets/", StringComparison.Ordinal)
                    ? AssetDatabase.GetAssetDependencyHash(path).ToString()
                    : HashFile(ToAbsolute(path));
                signature.Append('|').Append(path).Append('|').Append(identity);
            }
            return HashText(signature.ToString());
        }

        private static ParticleSystem AddParticle(GameObject parent, Material material, int maxParticles)
        {
            var particle = parent.AddComponent<ParticleSystem>(); var main = particle.main; main.maxParticles = maxParticles; main.loop = true; main.playOnAwake = false; main.useUnscaledTime = false;
            particle.useAutoRandomSeed = false; particle.randomSeed = (uint)(parent.name.GetHashCode() & 0x7fffffff);
            particle.GetComponent<ParticleSystemRenderer>().sharedMaterial = material; return particle;
        }
        private static Light MakeLight(GameObject root, string name, Color colour, float intensity, float range)
        {
            var light = Child(root, name).AddComponent<Light>(); light.type = LightType.Point; light.color = colour; light.intensity = 0f; light.range = range; light.shadows = LightShadows.None; light.enabled = false; return light;
        }
        private static void AddCamera(Vector3 position, Vector3 lookAt)
        {
            // Formal S3 evidence reads ARGB32 through a linear RenderTexture.  Keep the
            // serialized authority camera LDR too: HDR would be a misleading profile label
            // without an ARGBHalf/HDR readback pipeline.
            var camera = new GameObject("MainCamera").AddComponent<Camera>(); camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.02f, .024f, .032f, 1f); camera.allowHDR = false; camera.allowMSAA = false; camera.cullingMask &= ~(1 << FormalDiagnosticLayer); camera.transform.position = position; camera.transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
            W24PreviewRendererInfrastructure.ApplyToCamera(camera);
        }
        private static void AddReceiver(string name, Vector3 position, Vector3 scale, Material material)
        {
            var receiver = GameObject.CreatePrimitive(PrimitiveType.Cube); receiver.name = name; receiver.transform.position = position; receiver.transform.localScale = scale; receiver.GetComponent<Renderer>().sharedMaterial = material;
        }
        private static GameObject Child(GameObject parent, string name) { var child = new GameObject(name); child.transform.SetParent(parent.transform, false); return child; }
        private static void ConfigureDiagnostic(MeshRenderer renderer, uint objectId, string semanticRole)
        {
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));
            renderer.gameObject.AddComponent<W24DiagnosticObjectRegistration>().Configure(renderer, objectId, semanticRole, true);
        }
        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
        }
        private static void AssignS3Entry(W24S3RuntimeEntry entry, GameObject launch, GameObject active, GameObject impact, W24MovingEmitterTrailProtocol motion, W24ModelBindingAdapter binding, Transform bindingVisual, W24FragmentMotionSystem fragments, W24SemanticTimeline timeline, W24RealLightingModule lighting, bool requiresBinding, uint canonicalSeed)
        {
            SetObject(entry, "launchRoot", launch); SetObject(entry, "activeRoot", active); SetObject(entry, "impactRoot", impact);
            SetObject(entry, "movingTrail", motion); SetObject(entry, "modelBinding", binding); SetObject(entry, "bindingVisualRoot", bindingVisual); SetObject(entry, "fragments", fragments);
            SetObject(entry, "timeline", timeline); SetObject(entry, "lighting", lighting); SetBool(entry, "requiresModelBinding", requiresBinding); SetUInt(entry, "canonicalSeed", canonicalSeed);
        }
        internal static string MaterialPath(string effectId)
        {
            if (effectId == ProjectileId) return ProjectileMaterial;
            if (effectId == BindingId) return BindingMaterial;
            if (effectId == LightingId) return LightingMaterial;
            throw new ArgumentOutOfRangeException(nameof(effectId), effectId, "Unknown W24 S3 effect id.");
        }

        private static Material UpsertNeutralLitMaterial(string path)
        {
            return UpsertLitMaterial(path, new Color(.38f, .48f, .62f, 1f), Color.black, false);
        }

        private static Material UpsertEmissiveCoreLitMaterial(string path)
        {
            // The emission is serialized as a non-black property on a material used only by the
            // Runtime Entry source body. URP's material importer therefore has a truthful reason
            // to retain _EMISSION; neutral receiver probes never reference this asset.
            return UpsertLitMaterial(path, new Color(.16f, .035f, .008f, 1f), new Color(2.1f, .42f, .08f, 1f), true);
        }

        private static Material UpsertLitMaterial(string path, Color baseColor, Color emissionColor, bool emissionEnabled)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit"); if (shader == null) throw new InvalidOperationException("URP Lit is required for the S3 physical receiver baselines.");
            // Create/import first, then persist properties and force one more synchronous import.
            // Validation occurs on the reloaded asset, not on the transient in-memory Material.
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                AssetDatabase.CreateAsset(new Material(shader) { name = Path.GetFileNameWithoutExtension(path) }, path);
                material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) throw new InvalidOperationException("Could not create the owned S3 material: " + path);
            }
            material.shader = shader; material.SetColor("_BaseColor", baseColor); material.SetFloat("_Metallic", 0f); material.SetFloat("_Smoothness", .18f);
            material.SetColor("_EmissionColor", emissionColor);
            // Give URP the semantically compatible GI hint, but do not claim its importer-owned
            // keyword list as a neutral-material functional invariant.  The stable neutral
            // contract is the serialized black emission value plus asset isolation from the
            // non-black source body; actual receiver response is proven independently by A/B.
            material.globalIlluminationFlags = emissionEnabled
                ? MaterialGlobalIlluminationFlags.RealtimeEmissive
                : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            if (emissionEnabled) material.EnableKeyword("_EMISSION");
            else material.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) throw new InvalidOperationException("The persisted S3 material could not be reloaded: " + path);
            if (emissionEnabled && (!material.IsKeywordEnabled("_EMISSION") || material.GetColor("_EmissionColor").maxColorComponent <= 1f))
                throw new InvalidOperationException("The effect-owned S3 emissive-core material did not retain its non-black URP Lit _EMISSION variant after reimport.");
            if (!emissionEnabled && material.GetColor("_EmissionColor").maxColorComponent != 0f)
                throw new InvalidOperationException("A neutral S3 material acquired non-black serialized emission during persistence: " + path);
            return material;
        }

        private static void FinalizePersistedMaterialContracts()
        {
            UpsertNeutralLitMaterial(ProjectileMaterial);
            UpsertNeutralLitMaterial(BindingMaterial);
            UpsertNeutralLitMaterial(LightingMaterial);
            UpsertEmissiveCoreLitMaterial(LightingEmissiveCoreMaterial);
            ValidatePersistedMaterialContracts();
        }

        private static void ValidatePersistedMaterialContracts()
        {
            ValidatePersistedMaterial(ProjectileMaterial, false);
            ValidatePersistedMaterial(BindingMaterial, false);
            ValidatePersistedMaterial(LightingMaterial, false);
            ValidatePersistedMaterial(LightingEmissiveCoreMaterial, true);
            var neutralReceiver = AssetDatabase.LoadAssetAtPath<Material>(LightingMaterial);
            var emissiveCore = AssetDatabase.LoadAssetAtPath<Material>(LightingEmissiveCoreMaterial);
            if (neutralReceiver == null || emissiveCore == null || ReferenceEquals(neutralReceiver, emissiveCore) ||
                string.Equals(AssetDatabase.GetAssetPath(neutralReceiver), AssetDatabase.GetAssetPath(emissiveCore), StringComparison.Ordinal))
                throw new InvalidOperationException("The D receiver and source body must remain two independently owned material assets.");
        }

        private static void ValidatePersistedMaterial(string path, bool emissionEnabled)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.shader == null || !string.Equals(material.shader.name, "Universal Render Pipeline/Lit", StringComparison.Ordinal))
                throw new InvalidOperationException("The persisted S3 material is missing or no longer uses URP Lit: " + path);
            var emission = material.GetColor("_EmissionColor").maxColorComponent;
            if (emissionEnabled)
            {
                if (!material.IsKeywordEnabled("_EMISSION") || emission <= 1f)
                    throw new InvalidOperationException("The final imported S3 emissive-core material contract is invalid: " + path);
            }
            else if (emission != 0f)
                throw new InvalidOperationException("The final imported S3 neutral material contract is invalid: " + path);
        }
        private static void SetObject(UnityEngine.Object target, string property, UnityEngine.Object value) { var serialized = new SerializedObject(target); serialized.FindProperty(property).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
        private static void SetArray(UnityEngine.Object target, string property, UnityEngine.Object[] values) { var serialized = new SerializedObject(target); var list = serialized.FindProperty(property); list.arraySize = values.Length; for (var i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; serialized.ApplyModifiedPropertiesWithoutUndo(); }
        private static void SetInt(UnityEngine.Object target, string property, int value) { var serialized = new SerializedObject(target); serialized.FindProperty(property).intValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
        private static void SetUInt(UnityEngine.Object target, string property, uint value) { var serialized = new SerializedObject(target); serialized.FindProperty(property).longValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
        private static void SetFloat(UnityEngine.Object target, string property, float value) { var serialized = new SerializedObject(target); serialized.FindProperty(property).floatValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
        private static void SetBool(UnityEngine.Object target, string property, bool value) { var serialized = new SerializedObject(target); serialized.FindProperty(property).boolValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
        private static void EnsureFolder(string path) { if (AssetDatabase.IsValidFolder(path)) return; var slash = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, slash)); AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1)); }
        private static string ToAbsolute(string assetPath) { return Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath.Replace('/', Path.DirectorySeparatorChar)); }
        private static string RepositoryAbsolute(string relativePath) { return Path.Combine(Directory.GetParent(Application.dataPath).Parent.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar)); }
        private static string HashText(string value) { using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)).Select(item => item.ToString("x2", CultureInfo.InvariantCulture))); }
        private static string HashFile(string absolutePath) { using (var stream = File.OpenRead(absolutePath)) using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(stream).Select(item => item.ToString("x2", CultureInfo.InvariantCulture))); }
        private static void WriteDeterministic(string absolutePath, string content)
        {
            var normalized = (content ?? string.Empty).Replace("\r\n", "\n");
            if (File.Exists(absolutePath) && string.Equals(File.ReadAllText(absolutePath), normalized, StringComparison.Ordinal)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllText(absolutePath, normalized, new UTF8Encoding(false));
        }
    }
}
