using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Style;
using VFXComposer.Editor.Validation;
using VFXComposer.W11W13NextCandidate;

namespace VFXComposer.Editor.NextCandidates
{
    public sealed class W11W13NextBuildResult
    {
        public bool Succeeded;
        public bool Unchanged;
        public string PrefabPath;
        public string BuildHash;
        public string Error;
    }

    public sealed class W11W13CompositePeak
    {
        public int Particles;
        public int ParticleSystems;
        public int Renderers;
        public int Materials;
    }

    /// <summary>
    /// Dedicated builder for the W11 environment, W12 hit-feedback and W13 ultimate next candidates.
    /// It has disjoint ids/output/scenes and never calls the rejected builders.
    /// </summary>
    public static class W11W13NextCandidateAuthoring
    {
        public const string CompilerVersion = "w11-w13-next-candidate-1";
        public const string OutputRoot = "Assets/VFX/Generated/W11W13NextCandidate";
        public const string SharedRoot = "Assets/VFX/Shared/W11W13NextCandidate";
        public const string ShaderPath = SharedRoot + "/Shaders/W11W13NextCandidateLayeredUnlit.shader";
        public const string MaterialRoot = SharedRoot + "/Materials";
        public const string W11PreviewPath = "Assets/VFX/Preview/VFXPREVIEW_W11_ENVIRONMENT_NEXT_CANDIDATE.unity";
        public const string W12PreviewPath = "Assets/VFX/Preview/VFXPREVIEW_W12_HIT_FEEDBACK_NEXT_CANDIDATE.unity";
        public const string W13PreviewPath = "Assets/VFX/Preview/VFXPREVIEW_W13_ULTIMATE_NEXT_CANDIDATE.unity";

        [MenuItem("Tools/VFX Composer/Content/Build W11 W12 W13 Next Candidates (Visual Pending)")]
        public static void BuildAllMenu()
        {
            BuildAll();
            Debug.Log("W11/W12/W13 next-candidate source and Preview scenes are current. Status remains NEXT_CANDIDATE_VISUAL_PENDING; no visual verdict or L level is implied.");
        }

        /// <summary>Batch-safe executeMethod. The caller owns Unity process lifetime and exit handling.</summary>
        public static void BuildAllForBatch() { BuildAll(); }

        /// <summary>Batch-safe W13-only executeMethod. It does not build or write W11/W12 candidates.</summary>
        public static void BuildW13ForBatch()
        {
            var definitions = W11W13NextCandidatePlan.Group("W13").ToArray();
            VerifyRecipes(definitions);
            EnsureSharedAssets(new[] { "W13" }, false);
            BuildDefinitions(definitions);
            BuildPreview("W13", W13PreviewPath, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void BuildAll()
        {
            VerifyRecipes();
            EnsureSharedAssets();
            BuildDefinitions(W11W13NextCandidatePlan.Definitions);
            BuildPreview("W11", W11PreviewPath, true);
            BuildPreview("W12", W12PreviewPath, false);
            BuildPreview("W13", W13PreviewPath, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void VerifyRecipes()
        {
            VerifyRecipes(W11W13NextCandidatePlan.Definitions);
        }

        private static void VerifyRecipes(IEnumerable<W11W13NextDefinition> definitions)
        {
            foreach (var definition in definitions)
            {
                var path = W11W13NextCandidatePlan.RecipePath(definition);
                var absolute = Absolute(path);
                if (!File.Exists(absolute)) throw new FileNotFoundException("W11/W12/W13 next-candidate Recipe is missing.", absolute);
                W11W13NextCandidatePlan.ValidateRecipe(JObject.Parse(File.ReadAllText(absolute)), definition);
            }
        }

        private static void BuildDefinitions(IEnumerable<W11W13NextDefinition> definitions)
        {
            foreach (var definition in definitions)
            {
                var result = BuildAsset(definition);
                if (!result.Succeeded) throw new InvalidOperationException(definition.Id + ": " + result.Error);
            }
        }

        public static W11W13NextBuildResult BuildAsset(W11W13NextDefinition definition)
        {
            var result = new W11W13NextBuildResult();
            try
            {
                if (definition == null) throw new ArgumentNullException("definition");
                var recipePath = W11W13NextCandidatePlan.RecipePath(definition);
                var json = File.ReadAllText(Absolute(recipePath));
                W11W13NextCandidatePlan.ValidateRecipe(JObject.Parse(json), definition);
                var recipeHash = RecipeCanonicalizer.ComputeSha256(json);
                var buildHash = Hash(recipeHash + "|" + CompilerVersion + "|" + CompilerSignature() + "|" + DependencySignature(definition) + "|" + Application.unityVersion);
                var outputFolder = OutputFolder(definition.Id);
                var prefabPath = PrefabPath(definition.Id);
                result.PrefabPath = prefabPath; result.BuildHash = buildHash;
                if (string.Equals(ReadManifestHash(definition.Id), buildHash, StringComparison.Ordinal) && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                {
                    result.Succeeded = true; result.Unchanged = true; return result;
                }
                EnsureFolder(outputFolder);
                var root = new GameObject("VFX_" + definition.Id);
                try
                {
                    BuildRuntime(root, definition);
                    ValidateLocalBudget(root, definition);
                    if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null) throw new InvalidOperationException("Could not save " + prefabPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
                AssetDatabase.SaveAssets();
                var audit = VfxProductionRules.EnforceAndWriteManifest(definition.Id, definition.Archetype, 1, 1, recipeHash, buildHash, CompilerVersion, prefabPath, outputFolder, definition.Duration, recipePath);
                if (audit.Report.HasErrors) throw new InvalidOperationException(string.Join(" | ", audit.Report.Entries.Select(value => value.Code + " " + value.Path + " " + value.Message)));
                result.Succeeded = true;
            }
            catch (Exception exception) { result.Error = exception.Message; }
            return result;
        }

        public static string OutputFolder(string id) { return OutputRoot + "/" + id; }
        public static string PrefabPath(string id) { return OutputFolder(id) + "/VFX_" + id + ".prefab"; }

        public static string DependencyPrefabPath(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Dependency id is required.", "id");
            var manifestPath = VfxProjectRules.ManifestAbsolutePath(id);
            if (!File.Exists(manifestPath)) throw new FileNotFoundException("Composite dependency manifest is missing.", manifestPath);
            var manifest = JObject.Parse(File.ReadAllText(manifestPath));
            if (!string.Equals((string)manifest["effectId"], id, StringComparison.Ordinal)) throw new InvalidDataException("Composite dependency manifest identity mismatch: " + id);
            var runtime = manifest["runtimeEntry"] as JObject;
            var path = runtime == null ? null : (string)runtime["path"];
            var expectedRoot = "Assets/VFX/Generated/" + id + "/";
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith(expectedRoot, StringComparison.Ordinal) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Composite dependency runtime Prefab path is invalid: " + id);
            var actualGuid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrWhiteSpace(actualGuid)) throw new FileNotFoundException("Composite dependency runtime Prefab is missing.", path);
            var declaredGuid = runtime == null ? null : (string)runtime["guid"];
            if (!string.IsNullOrWhiteSpace(declaredGuid) && !string.Equals(declaredGuid, actualGuid, StringComparison.Ordinal))
                throw new InvalidDataException("Composite dependency runtime Prefab GUID mismatch: " + id);
            return path;
        }
        public static string PreviewPath(string group) { return group == "W11" ? W11PreviewPath : group == "W12" ? W12PreviewPath : W13PreviewPath; }

        private static void BuildRuntime(GameObject root, W11W13NextDefinition definition)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(definition.Group));
            if (material == null) throw new InvalidOperationException("Missing next-candidate material for " + definition.Group);
            var renderers = new List<Renderer>();
            var particles = new List<ParticleSystem>();
            var lines = new List<LineRenderer>();
            var animated = new List<Transform>();
            var flowMotes = new List<Transform>();
            Transform primary = null, secondary = null, result = null;
            GameObject[] stageRoots = new GameObject[0];
            GameObject[] sourcePrefabs = new GameObject[0];

            if (definition.Family == W11W13NextFamily.Environment) BuildEnvironment(root, definition, material, renderers, particles, lines, animated, ref primary, ref secondary, ref result, flowMotes);
            else if (definition.Family == W11W13NextFamily.HitFeedback) BuildHitFeedback(root, definition, material, renderers, particles, lines, animated, ref primary, ref secondary, ref result, flowMotes);
            else BuildUltimate(root, definition, material, renderers, particles, lines, out stageRoots, out sourcePrefabs);

            var controller = root.AddComponent<W11W13NextCandidateController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("candidateId").stringValue = definition.Id;
            serialized.FindProperty("family").enumValueIndex = (int)definition.Family;
            serialized.FindProperty("variant").enumValueIndex = (int)definition.Variant;
            serialized.FindProperty("duration").floatValue = definition.Duration;
            serialized.FindProperty("seed").longValue = StableSeed(definition.Id);
            serialized.FindProperty("primary").colorValue = definition.Primary;
            serialized.FindProperty("secondary").colorValue = definition.Secondary;
            serialized.FindProperty("accent").colorValue = definition.Accent;
            SetObjects(serialized.FindProperty("ownedRenderers"), renderers.Cast<UnityEngine.Object>().ToArray());
            SetObjects(serialized.FindProperty("particles"), particles.Cast<UnityEngine.Object>().ToArray());
            SetObjects(serialized.FindProperty("lines"), lines.Cast<UnityEngine.Object>().ToArray());
            SetObjects(serialized.FindProperty("animatedTransforms"), animated.Cast<UnityEngine.Object>().ToArray());
            serialized.FindProperty("primaryBody").objectReferenceValue = primary;
            serialized.FindProperty("secondaryBody").objectReferenceValue = secondary;
            serialized.FindProperty("resultBody").objectReferenceValue = result;
            SetObjects(serialized.FindProperty("flowMotes"), flowMotes.Cast<UnityEngine.Object>().ToArray());
            SetObjects(serialized.FindProperty("stageRoots"), stageRoots.Cast<UnityEngine.Object>().ToArray());
            SetObjects(serialized.FindProperty("sourcePrefabs"), sourcePrefabs.Cast<UnityEngine.Object>().ToArray());
            WriteStructArray(serialized.FindProperty("timeline"), W11W13NextCandidatePlan.Timeline(definition));
            WriteStructArray(serialized.FindProperty("cameraHints"), W11W13NextCandidatePlan.CameraHints(definition));
            WriteStructArray(serialized.FindProperty("gates"), W11W13NextCandidatePlan.Gates(definition));
            serialized.ApplyModifiedPropertiesWithoutUndo();

            foreach (var renderer in renderers) if (renderer != null) renderer.enabled = false;
            foreach (var particle in particles) if (particle != null) particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var stageRoot in stageRoots) if (stageRoot != null) stageRoot.SetActive(false);
        }

        private static void BuildEnvironment(GameObject root, W11W13NextDefinition definition, Material material, List<Renderer> renderers, List<ParticleSystem> particles, List<LineRenderer> lines, List<Transform> animated, ref Transform primary, ref Transform secondary, ref Transform result, List<Transform> flowMotes)
        {
            switch (definition.Variant)
            {
                case W11W13NextVariant.Rain:
                    primary = Particle(root.transform,"NearRainStreaks",material,48,32,true,new Vector3(4.8f,2.4f,2f),new Vector3(0,-8f,0),renderers,particles,true).transform;
                    Particle(root.transform,"MidRainCurtain",material,42,22,true,new Vector3(5.8f,2.8f,2.5f),new Vector3(0,-6.2f,0),renderers,particles,true);
                    secondary = Particle(root.transform,"GroundSplashRipples",material,32,18,true,new Vector3(4.5f,.15f,2.1f),Vector3.zero,renderers,particles,false).transform;
                    Particle(root.transform,"FarMist",material,24,8,true,new Vector3(6f,.7f,2.7f),new Vector3(.1f,.05f,0),renderers,particles,false);
                    result = Mesh(root.transform,"FarAtmosphereDepth",VfxStyleSharedLibrary.RibbonPath,material,new Vector3(0,.1f,.7f),new Vector3(5.4f,.55f,1f),Quaternion.Euler(78,0,0),renderers,animated).transform;
                    break;
                case W11W13NextVariant.Sandstorm:
                    primary = Mesh(root.transform,"CrosswindSandVeil",VfxStyleSharedLibrary.RibbonPath,material,new Vector3(0,.55f,.3f),new Vector3(5.6f,1.6f,1),Quaternion.Euler(0,0,-7),renderers,animated).transform;
                    secondary = Mesh(root.transform,"RollingDustKnots",VfxStyleSharedLibrary.BurstPath,material,new Vector3(-.4f,.2f,0),new Vector3(2.8f,1.15f,1),Quaternion.Euler(72,0,0),renderers,animated).transform;
                    Particle(root.transform,"GroundSkimSand",material,46,28,true,new Vector3(5.5f,.25f,2.2f),new Vector3(5.2f,.15f,0),renderers,particles,false);
                    Particle(root.transform,"SuspendedSand",material,44,21,true,new Vector3(5f,1.8f,2f),new Vector3(3.4f,.05f,0),renderers,particles,false);
                    Particle(root.transform,"OccasionalGrit",material,20,4,true,new Vector3(4f,1.2f,1.6f),new Vector3(6f,.2f,0),renderers,particles,false);
                    break;
                case W11W13NextVariant.MistFog:
                    primary = Mesh(root.transform,"LowFogBandA",VfxStyleSharedLibrary.RibbonPath,material,new Vector3(0,.08f,.35f),new Vector3(4.8f,.6f,1),Quaternion.Euler(84,0,2),renderers,animated).transform;
                    secondary = Mesh(root.transform,"LowFogBandB",VfxStyleSharedLibrary.RibbonPath,material,new Vector3(.25f,.26f,-.2f),new Vector3(4.1f,.55f,1),Quaternion.Euler(80,0,-4),renderers,animated).transform;
                    result = Mesh(root.transform,"TornFogEdge",VfxStyleSharedLibrary.BurstPath,material,new Vector3(-.5f,.18f,.6f),new Vector3(2.3f,.7f,1),Quaternion.Euler(74,0,11),renderers,animated).transform;
                    Particle(root.transform,"BreathingDepthLayer",material,32,7,true,new Vector3(4.8f,.7f,2.2f),new Vector3(.18f,.03f,0),renderers,particles,false);
                    break;
                case W11W13NextVariant.FallingLeaves:
                    primary = Particle(root.transform,"NearTumblingLeaves",material,52,22,true,new Vector3(4.5f,2.5f,2f),new Vector3(.7f,-.8f,.15f),renderers,particles,false).transform;
                    secondary = Particle(root.transform,"MidSwayLeaves",material,38,13,true,new Vector3(5f,2.8f,2.3f),new Vector3(-.2f,-.45f,.1f),renderers,particles,false).transform;
                    result = Mesh(root.transform,"GroundSlideTail",VfxStyleSharedLibrary.RibbonPath,material,new Vector3(0,-.9f,.3f),new Vector3(3.2f,.18f,1),Quaternion.Euler(82,0,0),renderers,animated).transform;
                    animated.Add(primary); animated.Add(secondary);
                    break;
                case W11W13NextVariant.Fireflies:
                    primary = Particle(root.transform,"WanderingGlowPoints",material,38,12,true,new Vector3(4f,2f,2f),new Vector3(.12f,.08f,.05f),renderers,particles,false).transform;
                    Particle(root.transform,"NearLensMotes",material,20,4,true,new Vector3(3f,1.5f,1.6f),new Vector3(-.08f,.12f,.03f),renderers,particles,false);
                    secondary = Primitive(root.transform,"PairedOrbitMoteA",PrimitiveType.Sphere,material,new Vector3(-.4f,.2f,0),Vector3.one*.11f,Quaternion.identity,renderers,animated).transform;
                    result = Primitive(root.transform,"PairedOrbitMoteB",PrimitiveType.Sphere,material,new Vector3(.4f,.2f,.1f),Vector3.one*.08f,Quaternion.identity,renderers,animated).transform;
                    break;
                case W11W13NextVariant.AmbientDust:
                    primary = Particle(root.transform,"AmbientFineDust",material,48,8,true,new Vector3(4.4f,2.4f,2f),new Vector3(.04f,.08f,.02f),renderers,particles,false).transform;
                    secondary = Particle(root.transform,"LightBandDust",material,30,13,true,new Vector3(1.5f,2.5f,1f),new Vector3(.02f,.12f,.01f),renderers,particles,false).transform;
                    result = Mesh(root.transform,"VisibleLightShaft",VfxStyleSharedLibrary.RibbonPath,material,new Vector3(.55f,.2f,.5f),new Vector3(1.2f,3.2f,1),Quaternion.Euler(0,0,-18),renderers,animated).transform;
                    break;
                default:
                    primary = Line(root.transform,"CurvedWaterCurtain",material,10,.62f,Curve(new Vector3(-1.5f,1.8f,0),new Vector3(1.5f,-1.1f,.35f),10,.4f),renderers,lines).transform;
                    Line(root.transform,"WhiteWaterStrandA",material,8,.11f,Curve(new Vector3(-.55f,1.85f,-.08f),new Vector3(-.25f,-1f,.28f),8,.13f),renderers,lines);
                    Line(root.transform,"WhiteWaterStrandB",material,8,.09f,Curve(new Vector3(.7f,1.75f,.05f),new Vector3(.48f,-.95f,.4f),8,-.12f),renderers,lines);
                    secondary = Particle(root.transform,"ImpactMistVolume",material,44,18,true,new Vector3(2.5f,.45f,1.3f),new Vector3(.1f,.4f,.12f),renderers,particles,false).transform;
                    Particle(root.transform,"SplashPearls",material,28,9,true,new Vector3(2.2f,.25f,1f),new Vector3(.2f,1.2f,.1f),renderers,particles,false);
                    result = Line(root.transform,"DownstreamFoam",material,12,.18f,Curve(new Vector3(-1.7f,-1.05f,.45f),new Vector3(1.7f,-1.05f,.85f),12,.22f),renderers,lines).transform;
                    animated.Add(primary); animated.Add(secondary); animated.Add(result);
                    break;
            }
        }

        private static void BuildHitFeedback(GameObject root, W11W13NextDefinition definition, Material material, List<Renderer> renderers, List<ParticleSystem> particles, List<LineRenderer> lines, List<Transform> animated, ref Transform primary, ref Transform secondary, ref Transform result, List<Transform> flowMotes)
        {
            switch (definition.Variant)
            {
                case W11W13NextVariant.HitFlash:
                    primary = Particle(root.transform,"DirectionalHitSparks",material,8,0,false,new Vector3(.4f,.4f,.4f),new Vector3(2.5f,.6f,0),renderers,particles,false,5).transform;
                    secondary = Mesh(root.transform,"EdgeFlashCarrier",VfxStyleSharedLibrary.BurstPath,material,Vector3.zero,Vector3.one*.72f,Quaternion.identity,renderers,animated).transform;
                    break;
                case W11W13NextVariant.CriticalStrike:
                    primary = Mesh(root.transform,"RadialHardCracks",VfxStyleSharedLibrary.ShardPath,material,Vector3.zero,new Vector3(1.35f,1.35f,1),Quaternion.identity,renderers,animated).transform;
                    secondary = Mesh(root.transform,"FourPointStar",VfxStyleSharedLibrary.BurstPath,material,new Vector3(0,0,-.02f),Vector3.one*1.05f,Quaternion.Euler(0,0,45),renderers,animated).transform;
                    result = Mesh(root.transform,"TiltedImpactRing",VfxStyleSharedLibrary.RingPath,material,Vector3.zero,Vector3.one*.88f,Quaternion.Euler(18,12,-18),renderers,animated).transform;
                    Particle(root.transform,"GoldDebris",material,28,0,false,new Vector3(.4f,.4f,.3f),new Vector3(0,2.2f,0),renderers,particles,false,18);
                    break;
                case W11W13NextVariant.ParrySpark:
                    primary = Particle(root.transform,"CollisionSparkFan",material,30,0,false,new Vector3(.1f,.1f,.1f),new Vector3(3f,1.2f,.3f),renderers,particles,false,14,true).transform;
                    secondary = Mesh(root.transform,"ContactFlashRing",VfxStyleSharedLibrary.RingPath,material,Vector3.zero,Vector3.one*.55f,Quaternion.Euler(65,0,0),renderers,animated).transform;
                    Particle(root.transform,"FallingMetalTails",material,10,0,false,new Vector3(.2f,.2f,.2f),new Vector3(1.5f,.7f,.4f),renderers,particles,false,8);
                    break;
                case W11W13NextVariant.KnockupLauncher:
                    primary = Primitive(root.transform,"VerticalAirColumn",PrimitiveType.Cylinder,material,new Vector3(0,.45f,0),new Vector3(.42f,.65f,.42f),Quaternion.identity,renderers,animated).transform;
                    secondary = Mesh(root.transform,"GroundLaunchRing",VfxStyleSharedLibrary.RingPath,material,new Vector3(0,-.2f,0),Vector3.one*.8f,Quaternion.Euler(90,0,0),renderers,animated).transform;
                    Line(root.transform,"RisingCoreLines",material,6,.065f,new[]{new Vector3(-.12f,-.15f,0),new Vector3(-.08f,.2f,0),new Vector3(-.04f,.55f,0),new Vector3(.02f,.9f,0),new Vector3(.08f,1.25f,0),new Vector3(.13f,1.55f,0)},renderers,lines);
                    result = Particle(root.transform,"ThrownDebris",material,26,0,false,new Vector3(.55f,.2f,.55f),new Vector3(0,3.1f,0),renderers,particles,false,18).transform;
                    break;
                case W11W13NextVariant.ComboSurge:
                    for (var index=0;index<5;index++)
                    {
                        var ring=Mesh(root.transform,"StackRing_"+(index+1).ToString("00"),VfxStyleSharedLibrary.RingPath,material,new Vector3(0,-.38f+index*.18f,0),Vector3.one*(.62f+index*.11f),Quaternion.Euler(90,0,index*14),renderers,animated);
                        if(index==0)primary=ring.transform;if(index==4)secondary=ring.transform;
                    }
                    Mesh(root.transform,"LevelUpFootPulse",VfxStyleSharedLibrary.BurstPath,material,new Vector3(0,-.44f,.02f),Vector3.one*.55f,Quaternion.Euler(90,0,0),renderers,animated);
                    result=Particle(root.transform,"RisingComboMotes",material,26,14,true,new Vector3(.7f,1.3f,.7f),new Vector3(0,1.2f,0),renderers,particles,false).transform;
                    break;
                case W11W13NextVariant.ElementalReaction:
                    primary=Primitive(root.transform,"ApproachEnergyA",PrimitiveType.Sphere,material,new Vector3(-1.25f,0,0),Vector3.one*.38f,Quaternion.identity,renderers,animated).transform;
                    secondary=Primitive(root.transform,"ApproachEnergyB",PrimitiveType.Sphere,material,new Vector3(1.25f,0,0),Vector3.one*.38f,Quaternion.identity,renderers,animated).transform;
                    result=Primitive(root.transform,"FusionResultBody",PrimitiveType.Sphere,material,Vector3.zero,Vector3.one*.72f,Quaternion.identity,renderers,animated).transform;
                    Line(root.transform,"DualColorSpiral",material,18,.08f,Helix(18,.72f,.1f),renderers,lines);
                    Particle(root.transform,"OpposedFragments",material,24,0,false,new Vector3(.3f,.3f,.3f),new Vector3(1.8f,.5f,0),renderers,particles,false,18);
                    break;
                default:
                    primary=Primitive(root.transform,"CasterIntake",PrimitiveType.Sphere,material,new Vector3(-.95f,0,0),Vector3.one*.18f,Quaternion.identity,renderers,animated).transform;
                    secondary=Primitive(root.transform,"TargetMist",PrimitiveType.Sphere,material,new Vector3(.95f,0,0),Vector3.one*.24f,Quaternion.identity,renderers,animated).transform;
                    result=Line(root.transform,"SaggingDynamicLink",material,20,.085f,Curve(new Vector3(-.95f,0,0),new Vector3(.95f,0,0),20,-.42f),renderers,lines).transform;
                    Particle(root.transform,"TargetMistParticles",material,20,9,true,new Vector3(.4f,.4f,.4f),new Vector3(-.3f,.15f,0),renderers,particles,false);
                    flowMotes.Add(Primitive(root.transform,"ReverseFlowMoteA",PrimitiveType.Sphere,material,new Vector3(.3f,-.1f,0),Vector3.one*.065f,Quaternion.identity,renderers,null).transform);
                    flowMotes.Add(Primitive(root.transform,"ReverseFlowMoteB",PrimitiveType.Sphere,material,new Vector3(-.3f,-.1f,0),Vector3.one*.05f,Quaternion.identity,renderers,null).transform);
                    break;
            }
        }

        private static void BuildUltimate(GameObject root, W11W13NextDefinition definition, Material material, List<Renderer> renderers, List<ParticleSystem> particles, List<LineRenderer> lines, out GameObject[] stageRoots, out GameObject[] sourcePrefabs)
        {
            stageRoots = new[] { Child(root.transform,"IntroStage"), Child(root.transform,"PrimaryStage"), Child(root.transform,"ReleaseStage"), Child(root.transform,"TailStage") };
            var intro=stageRoots[0].transform;var main=stageRoots[1].transform;var release=stageRoots[2].transform;var tail=stageRoots[3].transform;
            switch(definition.Variant)
            {
                case W11W13NextVariant.DragonBreath:
                    Primitive(intro,"ChargeContinuityBody",PrimitiveType.Sphere,material,new Vector3(-.7f,.3f,0),Vector3.one*.38f,Quaternion.identity,renderers,null);
                    Primitive(main,"DragonHeadSilhouette",PrimitiveType.Capsule,material,new Vector3(-.3f,.25f,0),new Vector3(.55f,.38f,.42f),Quaternion.Euler(0,0,68),renderers,null);
                    Line(main,"SweepingBreathVolume",material,12,.5f,Curve(new Vector3(0,.25f,0),new Vector3(2.5f,.15f,.2f),12,.25f),renderers,lines);
                    Mesh(release,"FireNovaRelease",VfxStyleSharedLibrary.BurstPath,material,new Vector3(1.7f,.1f,0),Vector3.one*1.3f,Quaternion.Euler(76,0,0),renderers,null);
                    Mesh(tail,"AfterburnField",VfxStyleSharedLibrary.RibbonPath,material,new Vector3(1.2f,-.35f,.4f),new Vector3(2.2f,.35f,1),Quaternion.Euler(76,0,0),renderers,null);
                    break;
                case W11W13NextVariant.MeteorShower:
                    Mesh(intro,"SkyWarningField",VfxStyleSharedLibrary.RingPath,material,new Vector3(0,-.65f,.5f),Vector3.one*1.7f,Quaternion.Euler(90,0,0),renderers,null);
                    for(var i=0;i<6;i++)Primitive(main,"MeteorBody_"+(i+1).ToString("00"),PrimitiveType.Sphere,material,new Vector3((i-2.5f)*.38f,2.4f,(i%2)*.32f),Vector3.one*(.18f+i*.015f),Quaternion.identity,renderers,null);
                    Mesh(release,"ImpactSequence",VfxStyleSharedLibrary.BurstPath,material,new Vector3(0,-.7f,.2f),new Vector3(2.4f,1f,1),Quaternion.Euler(72,0,0),renderers,null);
                    Mesh(tail,"ClosingDustFront",VfxStyleSharedLibrary.RibbonPath,material,new Vector3(0,-.55f,.5f),new Vector3(3.2f,.65f,1),Quaternion.Euler(76,0,0),renderers,null);
                    break;
                case W11W13NextVariant.FrozenDomain:
                    Mesh(intro,"ExpandingIceBoundaryA",VfxStyleSharedLibrary.RingPath,material,Vector3.zero,Vector3.one*.9f,Quaternion.Euler(90,0,0),renderers,null);
                    Mesh(intro,"ExpandingIceBoundaryB",VfxStyleSharedLibrary.RingPath,material,Vector3.zero,Vector3.one*1.35f,Quaternion.Euler(90,0,18),renderers,null);
                    Mesh(main,"PersistentFrozenDomain",VfxStyleSharedLibrary.RibbonPath,material,new Vector3(0,-.45f,.25f),new Vector3(3.1f,1.1f,1),Quaternion.Euler(80,0,0),renderers,null);
                    for(var i=0;i<5;i++)Mesh(release,"IndependentIceSpike_"+(i+1).ToString("00"),VfxStyleSharedLibrary.ConePath,material,new Vector3((i-2)*.48f,-.2f,(i%2)*.28f),new Vector3(.25f,.8f,.25f),Quaternion.Euler(-90,0,i*27),renderers,null);
                    Mesh(tail,"DomainShatterRelease",VfxStyleSharedLibrary.BurstPath,material,Vector3.zero,Vector3.one*2f,Quaternion.Euler(70,0,0),renderers,null);
                    break;
                case W11W13NextVariant.JudgementRay:
                    for(var i=0;i<3;i++)Mesh(intro,"LayeredRune_"+(i+1).ToString("00"),VfxStyleSharedLibrary.RingPath,material,new Vector3(0,-.35f+i*.03f,0),Vector3.one*(.7f+i*.32f),Quaternion.Euler(90,0,i*29),renderers,null);
                    Primitive(main,"ContinuousFocusCore",PrimitiveType.Sphere,material,new Vector3(0,1.4f,0),Vector3.one*.34f,Quaternion.identity,renderers,null);
                    Primitive(main,"VolumetricJudgementColumn",PrimitiveType.Cylinder,material,new Vector3(0,.25f,0),new Vector3(.38f,1.7f,.38f),Quaternion.identity,renderers,null);
                    Mesh(release,"JudgementGroundBurst",VfxStyleSharedLibrary.BurstPath,material,new Vector3(0,-.55f,0),Vector3.one*1.8f,Quaternion.Euler(90,0,0),renderers,null);
                    Particle(tail,"AshFeatherTail",material,24,6,true,new Vector3(1.4f,1.2f,1.2f),new Vector3(.1f,-.35f,.05f),renderers,particles,false);
                    break;
                case W11W13NextVariant.DemonGate:
                    Mesh(intro,"BloodRitualFloor",VfxStyleSharedLibrary.RingPath,material,new Vector3(0,-.7f,.2f),Vector3.one*1.25f,Quaternion.Euler(90,0,0),renderers,null);
                    Primitive(main,"DeepGateLeft",PrimitiveType.Cube,material,new Vector3(-.75f,.3f,.2f),new Vector3(.18f,1.25f,.25f),Quaternion.identity,renderers,null);
                    Primitive(main,"DeepGateRight",PrimitiveType.Cube,material,new Vector3(.75f,.3f,.2f),new Vector3(.18f,1.25f,.25f),Quaternion.identity,renderers,null);
                    Primitive(main,"DeepGateArch",PrimitiveType.Cube,material,new Vector3(0,1.42f,.2f),new Vector3(.9f,.18f,.25f),Quaternion.identity,renderers,null);
                    Primitive(release,"BreakingDemonHand",PrimitiveType.Capsule,material,new Vector3(0,.35f,-.15f),new Vector3(.38f,.9f,.38f),Quaternion.Euler(0,0,-18),renderers,null);
                    Mesh(tail,"ThreatWaveTail",VfxStyleSharedLibrary.RingPath,material,new Vector3(0,.25f,.1f),Vector3.one*1.85f,Quaternion.Euler(0,0,0),renderers,null);
                    break;
                default:
                    Primitive(intro,"DrawStanceContinuity",PrimitiveType.Sphere,material,new Vector3(-.55f,.1f,0),Vector3.one*.28f,Quaternion.identity,renderers,null);
                    for(var i=0;i<8;i++){var anchor=Child(main,"SpatialSlash_"+(i+1).ToString("00"));anchor.transform.localPosition=new Vector3(Mathf.Cos(i*Mathf.PI/4)*.55f,Mathf.Sin(i*Mathf.PI/4)*.55f,(i%2)*.08f);anchor.transform.localRotation=Quaternion.Euler(0,0,i*45);}
                    Mesh(release,"TempestVolume",VfxStyleSharedLibrary.RingPath,material,Vector3.zero,Vector3.one*1.7f,Quaternion.Euler(65,0,0),renderers,null);
                    Mesh(tail,"SheatheFlashTail",VfxStyleSharedLibrary.BurstPath,material,new Vector3(.55f,.1f,0),Vector3.one*.8f,Quaternion.identity,renderers,null);
                    break;
            }
            sourcePrefabs = definition.Dependencies.Select(id =>
            {
                var path=DependencyPrefabPath(id);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(prefab==null)throw new InvalidOperationException(definition.Id+" requires existing dependency "+path);
                return prefab;
            }).ToArray();
        }

        private static void BuildPreview(string group, string path, bool sequential)
        {
            EnsureFolder("Assets/VFX/Preview");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var cameraObject=new GameObject(group+"NextCandidateReviewCamera");
            var camera=cameraObject.AddComponent<Camera>();camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.012f,.016f,.024f);camera.allowHDR=false;camera.allowMSAA=false;
            camera.orthographic=group=="W12";camera.orthographicSize=4.7f;camera.transform.position=group=="W11"?new Vector3(0,1.8f,-6.6f):group=="W13"?new Vector3(0,1.65f,-7.4f):new Vector3(0,.35f,-11f);camera.transform.LookAt(group=="W11"?new Vector3(0,.2f,0):group=="W13"?new Vector3(0,.25f,0):Vector3.zero);
            var lightObject=new GameObject("PreviewKeyLight");var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=.55f;light.transform.rotation=Quaternion.Euler(52,-32,0);
            var definitions=W11W13NextCandidatePlan.Group(group).ToArray();
            var entries=new List<W11W13NextCandidateController>();var targets=new List<Renderer>();
            for(var i=0;i<definitions.Length;i++)
            {
                var definition=definitions[i];var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(definition.Id));if(prefab==null)throw new InvalidOperationException("Missing preview prefab "+definition.Id);
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);instance.name="Review_"+(i+1).ToString("00")+"_"+definition.Id;
                if(group=="W12")instance.transform.position=new Vector3((i%3-1)*3.05f,2.25f-(i/3)*2.25f,0);else instance.transform.position=Vector3.zero;
                instance.transform.localScale=group=="W12"?Vector3.one*.72f:Vector3.one;
                entries.Add(instance.GetComponent<W11W13NextCandidateController>());
                Renderer targetRenderer=null;
                if(group=="W12")
                {
                    var target=GameObject.CreatePrimitive(PrimitiveType.Capsule);target.name="ExternalTarget_"+(i+1).ToString("00");target.transform.position=instance.transform.position+new Vector3(0,0,.55f);target.transform.localScale=new Vector3(.32f,.45f,.32f);targetRenderer=target.GetComponent<Renderer>();targetRenderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(group));
                    var boundary=Line(target.transform,"CellBoundary",AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(group)),5,.022f,new[]{new Vector3(-1.35f,-.95f,.3f),new Vector3(1.35f,-.95f,.3f),new Vector3(1.35f,.95f,.3f),new Vector3(-1.35f,.95f,.3f),new Vector3(-1.35f,-.95f,.3f)},new List<Renderer>(),new List<LineRenderer>());boundary.enabled=true;
                }
                targets.Add(targetRenderer);
                AddLabel(scene,definition.SourceId,group=="W12"?instance.transform.position+new Vector3(0,-1f,0):new Vector3(0,-1.65f,0),sequential?instance.transform:null);
                instance.SetActive(!sequential||i==0);
            }
            if(group=="W11")
            {
                var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="EnvironmentDepthReceiver";ground.transform.position=new Vector3(0,-1.1f,.6f);ground.transform.localScale=new Vector3(5.5f,.08f,3.8f);ground.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(PreviewSurfaceMaterialPath);
                var depth=GameObject.CreatePrimitive(PrimitiveType.Cube);depth.name="EnvironmentDepthWall";depth.transform.position=new Vector3(0,.2f,1.8f);depth.transform.localScale=new Vector3(5.5f,2.5f,.08f);depth.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(PreviewSurfaceMaterialPath);
            }
            var driverObject=new GameObject("W11W13NextCandidatePreviewDriver");var driver=driverObject.AddComponent<W11W13NextCandidatePreviewDriver>();var serialized=new SerializedObject(driver);
            SetObjects(serialized.FindProperty("entries"),entries.Cast<UnityEngine.Object>().ToArray());SetObjects(serialized.FindProperty("hitTargets"),targets.Cast<UnityEngine.Object>().ToArray());serialized.FindProperty("reviewCamera").objectReferenceValue=camera;serialized.FindProperty("sequential").boolValue=sequential;serialized.FindProperty("replaySeconds").floatValue=group=="W12"?2.1f:4f;serialized.FindProperty("selectionSeconds").floatValue=group=="W13"?9f:7f;serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene,path);
        }

        private static void AddLabel(Scene scene,string text,Vector3 position,Transform parent)
        {
            var label=new GameObject("PreviewLabel_"+text);SceneManager.MoveGameObjectToScene(label,scene);if(parent!=null){label.transform.SetParent(parent,false);label.transform.localPosition=position;}else label.transform.position=position;var mesh=label.AddComponent<TextMesh>();mesh.text=text;mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;mesh.fontSize=42;mesh.characterSize=.026f;mesh.color=new Color(.65f,.75f,.86f);
        }

        private static void EnsureSharedAssets()
        {
            EnsureSharedAssets(new[] { "W11", "W12", "W13" }, true);
        }

        private static void EnsureSharedAssets(IEnumerable<string> groups, bool includePreviewSurface)
        {
            RequireMesh(VfxStyleSharedLibrary.RingPath);
            RequireMesh(VfxStyleSharedLibrary.BurstPath);
            RequireMesh(VfxStyleSharedLibrary.RibbonPath);
            RequireMesh(VfxStyleSharedLibrary.ConePath);
            RequireMesh(VfxStyleSharedLibrary.ShardPath);
            EnsureFolder(SharedRoot);EnsureFolder(SharedRoot+"/Shaders");EnsureFolder(MaterialRoot);AssetDatabase.ImportAsset(ShaderPath,ImportAssetOptions.ForceSynchronousImport);
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);if(shader==null)throw new InvalidOperationException("W11/W12/W13 next-candidate shader failed to import.");
            foreach(var group in groups)EnsureMaterial(MaterialPath(group),shader,Color.white);
            if(includePreviewSurface){var previewShader=Shader.Find("Universal Render Pipeline/Unlit");if(previewShader==null)throw new InvalidOperationException("URP Unlit is required for preview receivers.");EnsureMaterial(PreviewSurfaceMaterialPath,previewShader,new Color(.055f,.075f,.105f));}
            AssetDatabase.SaveAssets();
        }

        private static void RequireMesh(string path)
        {
            if(AssetDatabase.LoadAssetAtPath<Mesh>(path)==null)throw new InvalidOperationException("Required shared mesh is missing; next-candidate authoring will not regenerate old shared assets: "+path);
        }

        private static void EnsureMaterial(string path,Shader shader,Color color)
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null){material=new Material(shader){name=Path.GetFileNameWithoutExtension(path)};AssetDatabase.CreateAsset(material,path);}material.shader=shader;if(material.HasProperty("_PrimaryColor"))material.SetColor("_PrimaryColor",color);if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);if(material.HasProperty("_GlobalAlpha"))material.SetFloat("_GlobalAlpha",1f);EditorUtility.SetDirty(material);
        }

        private static void ValidateLocalBudget(GameObject root,W11W13NextDefinition definition)
        {
            var renderers=root.GetComponentsInChildren<Renderer>(true);var particles=root.GetComponentsInChildren<ParticleSystem>(true);var capacity=particles.Sum(value=>value.main.maxParticles);
            if(renderers.Length>definition.RendererBudget)throw new InvalidOperationException(definition.Id+" local renderer budget "+renderers.Length+" > "+definition.RendererBudget);
            if(capacity>definition.ParticleBudget)throw new InvalidOperationException(definition.Id+" particle capacity "+capacity+" > "+definition.ParticleBudget);
            if(particles.Length>5&&definition.Family==W11W13NextFamily.Environment)throw new InvalidOperationException(definition.Id+" exceeds five environment Particle Systems.");
            if(root.GetComponentsInChildren<Rigidbody>(true).Length!=0)throw new InvalidOperationException(definition.Id+" must not depend on nondeterministic rigidbodies.");
            if(root.GetComponentsInChildren<W11W13NextCandidatePreviewDriver>(true).Length!=0)throw new InvalidOperationException(definition.Id+" production prefab contains a Preview driver.");
            if(definition.Family==W11W13NextFamily.Ultimate)
            {
                var peak=ComputeCompositePeak(root,definition);
                if(peak.Particles>200||peak.ParticleSystems>10||peak.Renderers>14||peak.Materials>10)throw new InvalidOperationException(definition.Id+" composite peak exceeds 200 particles / 10 PS / 10 materials / 14 renderers: "+peak.Particles+" / "+peak.ParticleSystems+" / "+peak.Materials+" / "+peak.Renderers);
            }
        }

        public static W11W13CompositePeak ComputeCompositePeak(GameObject root,W11W13NextDefinition definition)
        {
            if(root==null)throw new ArgumentNullException("root");if(definition==null||definition.Family!=W11W13NextFamily.Ultimate)throw new ArgumentException("Composite definition is required.","definition");
            var timeline=W11W13NextCandidatePlan.Timeline(definition).OrderBy(value=>value.Time).ThenBy(value=>value.Play?1:0).ToArray();
            var samples=timeline.Select(value=>value.Time).Concat(timeline.Select(value=>Mathf.Min(definition.Duration,value.Time+.001f))).Concat(new[]{0f,definition.Duration*.14f,definition.Duration*.34f,definition.Duration*.55f,definition.Duration*.74f,definition.Duration*.75f,definition.Duration*.93f,definition.Duration}).Distinct().OrderBy(value=>value).ToArray();
            var sourceCosts=definition.Dependencies.Select(ReadDependencyCost).ToArray();var peak=new W11W13CompositePeak();
            var stageRoots=new[]{root.transform.Find("IntroStage"),root.transform.Find("PrimaryStage"),root.transform.Find("ReleaseStage"),root.transform.Find("TailStage")};
            foreach(var sample in samples)
            {
                var active=new bool[definition.Dependencies.Length];
                foreach(var cue in timeline){if(cue.Time>sample+.00001f)break;if(cue.SourceIndex>=0&&cue.SourceIndex<active.Length)active[cue.SourceIndex]=cue.Play;}
                var phase=Mathf.Clamp01(sample/Mathf.Max(.05f,definition.Duration));var stageActive=new[]{phase<.34f,phase>=.14f&&phase<.75f,phase>=.55f&&phase<.93f,phase>=.74f};
                var particles=0;var systems=0;var renderers=0;var materials=0;
                for(var index=0;index<active.Length;index++)if(active[index]){particles+=sourceCosts[index].Particles;systems+=sourceCosts[index].ParticleSystems;renderers+=sourceCosts[index].Renderers;materials+=sourceCosts[index].Materials;}
                for(var index=0;index<stageRoots.Length;index++)if(stageActive[index]&&stageRoots[index]!=null){particles+=stageRoots[index].GetComponentsInChildren<ParticleSystem>(true).Sum(value=>value.main.maxParticles);systems+=stageRoots[index].GetComponentsInChildren<ParticleSystem>(true).Length;renderers+=stageRoots[index].GetComponentsInChildren<Renderer>(true).Length;}
                if(renderers>0)materials+=1;
                peak.Particles=Math.Max(peak.Particles,particles);peak.ParticleSystems=Math.Max(peak.ParticleSystems,systems);peak.Renderers=Math.Max(peak.Renderers,renderers);peak.Materials=Math.Max(peak.Materials,materials);
            }
            return peak;
        }

        private static W11W13CompositePeak ReadDependencyCost(string id)
        {
            var path=VfxProjectRules.ManifestAbsolutePath(id);if(!File.Exists(path))throw new FileNotFoundException("Composite dependency manifest is missing.",path);var cost=JObject.Parse(File.ReadAllText(path))["cost"] as JObject;if(cost==null)throw new InvalidDataException("Composite dependency cost is missing: "+id);
            return new W11W13CompositePeak{Particles=(int?)cost["particles"]??0,ParticleSystems=(int?)cost["particleSystems"]??0,Renderers=(int?)cost["renderers"]??0,Materials=(int?)cost["materials"]??0};
        }

        private static GameObject Child(Transform parent,string name){var value=new GameObject(name);value.transform.SetParent(parent,false);return value;}
        private static MeshRenderer Mesh(Transform parent,string name,string meshPath,Material material,Vector3 position,Vector3 scale,Quaternion rotation,List<Renderer> renderers,List<Transform> animated)
        {var go=Child(parent,name);go.transform.localPosition=position;go.transform.localScale=scale;go.transform.localRotation=rotation;var filter=go.AddComponent<MeshFilter>();filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);if(filter.sharedMesh==null)throw new InvalidOperationException("Missing shared mesh "+meshPath);var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.enabled=false;if(renderers!=null)renderers.Add(renderer);if(animated!=null)animated.Add(go.transform);return renderer;}
        private static MeshRenderer Primitive(Transform parent,string name,PrimitiveType type,Material material,Vector3 position,Vector3 scale,Quaternion rotation,List<Renderer> renderers,List<Transform> animated)
        {var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.transform.localRotation=rotation;var collider=go.GetComponent<Collider>();if(collider!=null)UnityEngine.Object.DestroyImmediate(collider);var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.enabled=false;if(renderers!=null)renderers.Add(renderer);if(animated!=null)animated.Add(go.transform);return renderer;}
        private static LineRenderer Line(Transform parent,string name,Material material,int count,float width,Vector3[] positions,List<Renderer> renderers,List<LineRenderer> lines)
        {var go=Child(parent,name);var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=false;line.positionCount=count;line.widthMultiplier=width;line.numCapVertices=4;line.numCornerVertices=3;line.sharedMaterial=material;line.enabled=false;for(var i=0;i<count&&i<positions.Length;i++)line.SetPosition(i,positions[i]);if(renderers!=null)renderers.Add(line);if(lines!=null)lines.Add(line);return line;}
        private static ParticleSystem Particle(Transform parent,string name,Material material,int capacity,float rate,bool loop,Vector3 shapeScale,Vector3 velocity,List<Renderer> renderers,List<ParticleSystem> particles,bool stretched,int burst=0,bool collision=false)
        {var go=Child(parent,name);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;main.playOnAwake=false;main.loop=loop;main.duration=loop?2f:.5f;main.maxParticles=capacity;main.startLifetime=loop?new ParticleSystem.MinMaxCurve(.65f,1.7f):new ParticleSystem.MinMaxCurve(.12f,.48f);main.startSpeed=0f;main.startSize=stretched?new ParticleSystem.MinMaxCurve(.018f,.038f):new ParticleSystem.MinMaxCurve(.035f,.13f);var emission=ps.emission;emission.rateOverTime=loop?rate:0f;if(!loop&&burst>0)emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)Math.Min(capacity,burst))});var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=shapeScale;var vol=ps.velocityOverLifetime;vol.enabled=true;vol.space=ParticleSystemSimulationSpace.Local;vol.x=velocity.x;vol.y=velocity.y;vol.z=velocity.z;if(collision){var module=ps.collision;module.enabled=true;module.type=ParticleSystemCollisionType.World;module.mode=ParticleSystemCollisionMode.Collision3D;module.bounce=.62f;module.dampen=.14f;module.lifetimeLoss=.2f;module.maxCollisionShapes=4;}var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;if(stretched){renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=2.8f;renderer.velocityScale=.35f;}else{renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=AssetDatabase.LoadAssetAtPath<Mesh>(VfxStyleSharedLibrary.ShardPath);}renderer.enabled=false;if(renderers!=null)renderers.Add(renderer);if(particles!=null)particles.Add(ps);return ps;}
        private static Vector3[] Curve(Vector3 from,Vector3 to,int count,float bend){var result=new Vector3[count];for(var i=0;i<count;i++){var u=i/(float)(count-1);result[i]=Vector3.Lerp(from,to,u)+Vector3.forward*Mathf.Sin(u*Mathf.PI)*bend;}return result;}
        private static Vector3[] Helix(int count,float radius,float depth){var result=new Vector3[count];for(var i=0;i<count;i++){var u=i/(float)(count-1);var angle=u*Mathf.PI*4;result[i]=new Vector3(Mathf.Cos(angle)*radius*(1-u*.55f),Mathf.Sin(angle)*radius*(1-u*.55f),(u-.5f)*depth);}return result;}
        private static string MaterialPath(string group){return MaterialRoot+"/MAT_"+group+"_NextCandidate.mat";}
        private const string PreviewSurfaceMaterialPath=MaterialRoot+"/MAT_PreviewSurface.mat";
        private static void SetObjects(SerializedProperty property,UnityEngine.Object[] values){property.arraySize=values.Length;for(var i=0;i<values.Length;i++)property.GetArrayElementAtIndex(i).objectReferenceValue=values[i];}
        private static void WriteStructArray(SerializedProperty property,W11W13TimelineCue[] values){property.arraySize=values.Length;for(var i=0;i<values.Length;i++){var item=property.GetArrayElementAtIndex(i);item.FindPropertyRelative("Time").floatValue=values[i].Time;item.FindPropertyRelative("SourceIndex").intValue=values[i].SourceIndex;item.FindPropertyRelative("Play").boolValue=values[i].Play;item.FindPropertyRelative("LocalPosition").vector3Value=values[i].LocalPosition;item.FindPropertyRelative("LocalEuler").vector3Value=values[i].LocalEuler;item.FindPropertyRelative("Scale").floatValue=values[i].Scale;item.FindPropertyRelative("EventId").stringValue=values[i].EventId??string.Empty;}}
        private static void WriteStructArray(SerializedProperty property,W11W13CameraHint[] values){property.arraySize=values.Length;for(var i=0;i<values.Length;i++){var item=property.GetArrayElementAtIndex(i);item.FindPropertyRelative("Time").floatValue=values[i].Time;item.FindPropertyRelative("Type").stringValue=values[i].Type;item.FindPropertyRelative("Strength").floatValue=values[i].Strength;}}
        private static void WriteStructArray(SerializedProperty property,W11W13StageGate[] values){property.arraySize=values.Length;for(var i=0;i<values.Length;i++){var item=property.GetArrayElementAtIndex(i);item.FindPropertyRelative("Time").floatValue=values[i].Time;item.FindPropertyRelative("EventId").stringValue=values[i].EventId;}}
        private static void EnsureFolder(string assetPath){if(AssetDatabase.IsValidFolder(assetPath))return;var parent=Path.GetDirectoryName(assetPath).Replace('\\','/');if(!AssetDatabase.IsValidFolder(parent))EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(assetPath));}
        private static string Absolute(string assetPath){return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,assetPath.Replace('/',Path.DirectorySeparatorChar)));}
        private static string ReadManifestHash(string id){var path=VfxProjectRules.ManifestAbsolutePath(id);if(!File.Exists(path))return null;try{return(string)JObject.Parse(File.ReadAllText(path))["buildHash"];}catch{return null;}}
        private static string CompilerSignature(){return FileHash(Absolute(ShaderPath))+"|"+FileHash(Absolute("Packages/com.vfxcomposer.unity/Runtime/W11W13NextCandidate/W11W13NextCandidateController.cs"))+"|"+FileHash(Absolute("Packages/com.vfxcomposer.unity/Editor/W11W13NextCandidate/W11W13NextCandidatePlan.cs"))+"|"+FileHash(Absolute("Packages/com.vfxcomposer.unity/Editor/W11W13NextCandidate/W11W13NextCandidateAuthoring.cs"));}
        private static string DependencySignature(W11W13NextDefinition definition){return string.Join("|",(definition.Dependencies??new string[0]).Select(id=>id+":"+FileHash(VfxProjectRules.ManifestAbsolutePath(id))));}
        private static string FileHash(string path){using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())return string.Concat(sha.ComputeHash(stream).Select(value=>value.ToString("x2",CultureInfo.InvariantCulture)));}
        private static string Hash(string value){using(var sha=SHA256.Create())return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(item=>item.ToString("x2",CultureInfo.InvariantCulture)));}
        private static uint StableSeed(string value){unchecked{uint hash=2166136261;foreach(var character in value){hash^=character;hash*=16777619;}return hash==0?1:hash;}}
    }
}
