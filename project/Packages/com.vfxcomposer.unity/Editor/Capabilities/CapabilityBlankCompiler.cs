using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Validation;
using VFXComposer.Editor.ValidationGallery;

namespace VFXComposer.Editor.Capabilities
{
    /// <summary>Builds neutral, player-safe Runtime Entries for behavior acceptance.</summary>
    public static class CapabilityBlankCompiler
    {
        public const string CompilerVersion = "capability-blank-2-carrier-showcase";
        public const string BeamCompilerVersion = "capability-blank-3-beam-visual-execution";
        public const string TimingAreaCompilerVersion = "capability-blank-4-timing-area-visual-execution";
        public const string LegacySharedCompilerVersion = "capability-blank-1";
        public const string RecipeRoot = "Assets/VFX/Recipes/Capability";
        public const string SharedRoot = "Assets/VFX/Shared/Capability";
        public const string AdditiveMaterialPath = SharedRoot + "/MAT_CapabilityBlank_Additive.mat";

        private static readonly string[] ProjectileIds =
        {
            "cap_linear_proj_3d", "cap_accel_proj_3d", "cap_parabola_proj_3d", "cap_homing_proj_3d",
            "cap_wave_proj_2d", "cap_boomerang_proj_3d", "cap_bounce_proj_3d", "cap_orbit_proj_3d",
            "cap_pierce_proj_3d", "cap_split_proj_2d", "cap_chainhop_proj_2d", "cap_volley_proj_2d"
        };
        private static readonly string[] BeamIds =
        {
            "cap_hitscan_beam_3d", "cap_sustained_beam_3d", "cap_sweep_beam_3d", "cap_charge_beam_3d",
            "cap_reflect_beam_3d", "cap_occlude_beam_3d", "cap_converge_beam_3d", "cap_arclink_beam_2d"
        };
        private static readonly string[] TimingAreaIds =
        {
            "cap_telegraph_impact_3d", "cap_delayfuse_impact_2d", "cap_tickpulse_area_2d", "cap_charge_release_2d", "cap_channel_3d",
            "cap_chainseq_impact_2d", "cap_expand_area_3d", "cap_implode_area_3d", "cap_movingzone_area_3d", "cap_growth_area_2d"
        };
        private static readonly string[] SupportIds = { "cap_hexflash_impact_2d", "cap_residue_trail_3d" };

        [MenuItem("Tools/VFX Composer/Capabilities/Build W-C1 Projectile Blanks")]
        public static void BuildProjectileBlanksMenu()
        {
            BuildProjectileBlanks();
            Debug.Log("W-C1: built 12 projectile capability Runtime Entries. Visual sign-off remains pending user review.");
        }

        public static void BuildProjectileBlanks()
        {
            EnsureShared();
            var catalog = VfxCompiler.LoadFormalCatalog();
            foreach (var id in SupportIds) BuildOne(id, catalog);
            foreach (var id in ProjectileIds) BuildOne(id, catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/VFX Composer/Capabilities/Build W-C2 Beam Blanks")]
        public static void BuildBeamBlanksMenu()
        {
            BuildBeamBlanks();
            Debug.Log("W-C2: built 8 beam capability Runtime Entries. Visual sign-off remains pending user review.");
        }

        public static void BuildBeamBlanks()
        {
            EnsureShared();
            var catalog = VfxCompiler.LoadFormalCatalog();
            foreach (var id in BeamIds) BuildOne(id, catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/VFX Composer/Capabilities/Build W-C3 Timing Area Blanks")]
        public static void BuildTimingAreaBlanksMenu()
        {
            BuildTimingAreaBlanks();
            Debug.Log("W-C3: built 10 timing/area capability Runtime Entries. Visual sign-off remains pending user review.");
        }

        public static void BuildTimingAreaBlanks()
        {
            EnsureShared();
            var catalog = VfxCompiler.LoadFormalCatalog();
            foreach (var id in SupportIds) BuildOne(id, catalog);
            foreach (var id in TimingAreaIds) BuildOne(id, catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BuildOne(string id, Catalog.TemplateCatalog catalog)
        {
            var recipePath = RecipeRoot + "/" + id + ".default.json";
            var json = File.ReadAllText(Absolute(recipePath));
            var report = RecipeValidator.Validate(json, catalog);
            if (report.HasErrors) throw new InvalidOperationException(id + ": " + string.Join(" | ", report.Entries.Select(value => value.Code + " " + value.Path + " " + value.Message).ToArray()));
            var parsed = VfxDomainParser.ParseRecipe(json).Value;
            ValidateShowcaseBinding(parsed);
            var recipeHash = RecipeCanonicalizer.ComputeSha256(json);
            var compilerVersion = ProjectileIds.Contains(id, StringComparer.Ordinal) ? CompilerVersion
                : BeamIds.Contains(id, StringComparer.Ordinal) ? BeamCompilerVersion
                : TimingAreaIds.Contains(id, StringComparer.Ordinal) ? TimingAreaCompilerVersion
                : LegacySharedCompilerVersion;
            var timingSlotIdentity = TimingAreaIds.Contains(id, StringComparer.Ordinal) ? TimingSlotDependencyIdentity(ResolveTimingSlotId(parsed.Behavior), catalog) : string.Empty;
            var buildHash = Hash(recipeHash + "|" + compilerVersion + "|" + AssetDatabase.GetAssetDependencyHash(AdditiveMaterialPath) + "|" + timingSlotIdentity + "|" + Application.unityVersion);
            var folder = "Assets/VFX/Generated/" + id;
            ValidationGalleryCompiler.EnsureFolder(folder);
            var prefabPath = folder + "/VFX_" + id + ".prefab";
            var root = new GameObject("VFX_" + id);
            try
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AdditiveMaterialPath);
                var sphere = BuiltinSphere();
                var ring = AssetDatabase.LoadAssetAtPath<Mesh>(CoverageGalleryBCompiler.RingPath);
                var core = new GameObject("CapabilityCore");
                core.transform.SetParent(root.transform, false);
                core.transform.localScale = Vector3.one * .16f;
                core.AddComponent<MeshFilter>().sharedMesh = sphere;
                var coreRenderer = core.AddComponent<MeshRenderer>();
                coreRenderer.sharedMaterial = material;
                coreRenderer.enabled = false;
                TrailRenderer trail = null;
                LineRenderer beam = null;
                LineRenderer[] auxiliaryBeamLines = new LineRenderer[0];
                Transform areaBoundary = null;
                Renderer areaRenderer = null;
                if (parsed.Archetype == RecipeArchetype.Beam)
                {
                    beam = AddBeamLine(root, material);
                    var isConverge = BehaviorType(parsed.Behavior == null ? null : parsed.Behavior.Emission) == "converge";
                    var isReflect = BehaviorType(parsed.Behavior == null ? null : parsed.Behavior.Hit) == "reflect";
                    var lineCount = isConverge
                        ? Mathf.Clamp(Mathf.RoundToInt(Number(parsed.Behavior.Emission, "source_count", 4f)), 2, 5)
                        : isReflect ? Mathf.Clamp(Mathf.RoundToInt(Number(parsed.Behavior.Hit, "max_segments", 3f)), 1, BeamCapabilityVisualExecutor.MaxReflectSegments)
                        : 1;
                    auxiliaryBeamLines = new LineRenderer[Mathf.Max(0, lineCount - 1)];
                    for (var lineIndex = 0; lineIndex < auxiliaryBeamLines.Length; lineIndex++)
                    {
                        var lineObject = new GameObject((isConverge ? "CapabilityConvergeLine_" : "CapabilityReflectSegment_") + (lineIndex + 2).ToString("00", CultureInfo.InvariantCulture));
                        lineObject.transform.SetParent(root.transform, false);
                        auxiliaryBeamLines[lineIndex] = AddBeamLine(lineObject, material);
                    }
                }
                else if (parsed.Archetype == RecipeArchetype.Projectile || parsed.Archetype == RecipeArchetype.Trail)
                {
                    trail = core.AddComponent<TrailRenderer>();
                    trail.sharedMaterial = material;
                    trail.time = .32f;
                    trail.minVertexDistance = .025f;
                    trail.widthMultiplier = .16f;
                    trail.widthCurve = new AnimationCurve(new Keyframe(0, 1f), new Keyframe(1, 0f));
                    trail.colorGradient = NeutralGradient();
                    trail.alignment = LineAlignment.View;
                    trail.emitting = false;
                    trail.enabled = false;
                }
                else
                {
                    var area = new GameObject("CapabilityBoundary");
                    area.transform.SetParent(root.transform, false);
                    area.transform.localScale = Vector3.zero;
                    area.AddComponent<MeshFilter>().sharedMesh = ring;
                    var renderer = area.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.enabled = false;
                    areaBoundary = area.transform;
                    areaRenderer = renderer;
                }

                var marker = new GameObject("CapabilityEventMarker");
                marker.transform.SetParent(root.transform, false);
                marker.transform.localScale = Vector3.one * .34f;
                Renderer markerRenderer = null;
                var omitEndpointRenderer = parsed.Archetype == RecipeArchetype.Beam && BehaviorType(parsed.Behavior == null ? null : parsed.Behavior.Hit) == "reflect";
                if (!omitEndpointRenderer)
                {
                    marker.AddComponent<MeshFilter>().sharedMesh = ring;
                    markerRenderer = marker.AddComponent<MeshRenderer>();
                    markerRenderer.sharedMaterial = material;
                    markerRenderer.enabled = false;
                }

                ParticleSystem carrierParticles = null;
                ParticleSystemRenderer carrierRenderer = null;
                if (NeedsCarrierSystem(parsed))
                {
                    var carriers = new GameObject("CapabilityCarriers");
                    carriers.transform.SetParent(root.transform, false);
                    carrierParticles = carriers.AddComponent<ParticleSystem>();
                    var main = carrierParticles.main;
                    main.playOnAwake = false;
                    main.loop = false;
                    main.duration = 60f;
                    main.startLifetime = 60f;
                    main.startSpeed = 0f;
                    main.startSize3D = true;
                    main.maxParticles = 24;
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                    var emission = carrierParticles.emission;
                    emission.enabled = false;
                    var shape = carrierParticles.shape;
                    shape.enabled = false;
                    carrierParticles.useAutoRandomSeed = false;
                    carrierParticles.randomSeed = parsed.RandomSeed == 0 ? 1u : parsed.RandomSeed;
                    carrierRenderer = carriers.GetComponent<ParticleSystemRenderer>();
                    carrierRenderer.renderMode = ParticleSystemRenderMode.Mesh;
                    carrierRenderer.mesh = sphere;
                    carrierRenderer.sharedMaterial = material;
                    carrierRenderer.enabled = false;
                    carrierParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }

                ParticleSystem beamMarkerParticles = null;
                ParticleSystemRenderer beamMarkerRenderer = null;
                if (parsed.Archetype == RecipeArchetype.Beam && BehaviorType(parsed.Behavior == null ? null : parsed.Behavior.Hit) == "reflect")
                {
                    var markers = new GameObject("CapabilityBounceMarkers");
                    markers.transform.SetParent(root.transform, false);
                    beamMarkerParticles = markers.AddComponent<ParticleSystem>();
                    var main = beamMarkerParticles.main;
                    main.playOnAwake = false;
                    main.loop = false;
                    main.duration = 60f;
                    main.startLifetime = 60f;
                    main.startSpeed = 0f;
                    main.maxParticles = BeamCapabilityVisualExecutor.MaxMarkerParticles;
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                    var emission = beamMarkerParticles.emission;
                    emission.enabled = false;
                    var shape = beamMarkerParticles.shape;
                    shape.enabled = false;
                    beamMarkerParticles.useAutoRandomSeed = false;
                    beamMarkerParticles.randomSeed = parsed.RandomSeed == 0 ? 1u : parsed.RandomSeed;
                    beamMarkerRenderer = markers.GetComponent<ParticleSystemRenderer>();
                    beamMarkerRenderer.renderMode = ParticleSystemRenderMode.Mesh;
                    beamMarkerRenderer.mesh = sphere;
                    beamMarkerRenderer.sharedMaterial = material;
                    beamMarkerRenderer.enabled = false;
                    beamMarkerParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }

                BeamCapabilityVisualExecutor beamVisual = null;
                if (parsed.Archetype == RecipeArchetype.Beam)
                {
                    beamVisual = root.AddComponent<BeamCapabilityVisualExecutor>();
                    ConfigureBeamVisual(beamVisual, parsed, beam, auxiliaryBeamLines, core.transform, coreRenderer, marker.transform, markerRenderer, beamMarkerParticles, beamMarkerRenderer);
                }

                TimingAreaCapabilityVisualExecutor timingAreaVisual = null;
                if (TimingAreaIds.Contains(id, StringComparer.Ordinal))
                {
                    coreRenderer.sortingOrder = 4;
                    if (areaRenderer != null) areaRenderer.sortingOrder = 1;
                    if (markerRenderer != null) markerRenderer.sortingOrder = 8;
                    var detailObject = new GameObject("CapabilityDetailLine");
                    detailObject.transform.SetParent(root.transform, false);
                    var detailLine = AddDetailLine(detailObject, material);
                    detailLine.sortingOrder = 6;

                    var behavior = parsed.Behavior ?? new RecipeBehaviorContract();
                    var slotId = ResolveTimingSlotId(behavior);
                    var slotObject = new GameObject("ResolvedVisualSlotBatch_" + (string.IsNullOrEmpty(slotId) ? "neutral" : slotId));
                    slotObject.transform.SetParent(root.transform, false);
                    var slotParticles = slotObject.AddComponent<ParticleSystem>();
                    var slotMain = slotParticles.main;
                    slotMain.playOnAwake = false;
                    slotMain.loop = false;
                    slotMain.duration = 60f;
                    slotMain.startLifetime = 60f;
                    slotMain.startSpeed = 0f;
                    slotMain.maxParticles = TimingAreaCapabilityVisualExecutor.MaxParticleCapacity;
                    slotMain.simulationSpace = ParticleSystemSimulationSpace.Local;
                    var slotEmission = slotParticles.emission;
                    slotEmission.enabled = false;
                    var slotShape = slotParticles.shape;
                    slotShape.enabled = false;
                    slotParticles.useAutoRandomSeed = false;
                    slotParticles.randomSeed = parsed.RandomSeed == 0 ? 1u : parsed.RandomSeed;
                    var slotRenderer = slotObject.GetComponent<ParticleSystemRenderer>();
                    var resolvedSlot = ResolveSlotVisual(slotId, catalog, sphere, material);
                    slotRenderer.renderMode = resolvedSlot.RenderMode;
                    slotRenderer.mesh = resolvedSlot.Mesh;
                    slotRenderer.sharedMaterial = resolvedSlot.Material;
                    slotRenderer.sortingOrder = 10;
                    slotRenderer.enabled = false;
                    slotParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                    timingAreaVisual = root.AddComponent<TimingAreaCapabilityVisualExecutor>();
                    ConfigureTimingAreaVisual(timingAreaVisual, parsed, slotId, core.transform, coreRenderer, areaBoundary, areaRenderer, marker.transform, markerRenderer, detailLine, slotParticles, slotRenderer);
                }

                var controller = root.AddComponent<CapabilityBlankVfxController>();
                Configure(controller, parsed, core.transform, coreRenderer, trail, beam, areaBoundary, areaRenderer, marker.transform, markerRenderer, carrierParticles, carrierRenderer, beamVisual, timingAreaVisual);
                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null) throw new InvalidOperationException("Could not save " + prefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }

            AssetDatabase.SaveAssets();
            var duration = parsed.Stages.Where(value => value.Enabled).Sum(value => value.Duration);
            var audit = VfxProductionRules.EnforceAndWriteManifest(parsed.Id, parsed.Archetype.ToString().ToLowerInvariant(), parsed.RecipeVersion, parsed.Revision, recipeHash, buildHash, compilerVersion, prefabPath, folder, duration);
            if (audit.Report.HasErrors) throw new InvalidOperationException(id + " output audit: " + string.Join(" | ", audit.Report.Entries.Select(value => value.Code + " " + value.Path + " " + value.Message).ToArray()));
        }

        private static void Configure(CapabilityBlankVfxController controller, Recipe recipe, Transform core, Renderer coreRenderer, TrailRenderer trail, LineRenderer beam, Transform areaBoundary, Renderer areaRenderer, Transform marker, Renderer markerRenderer, ParticleSystem carrierParticles, ParticleSystemRenderer carrierRenderer, BeamCapabilityVisualExecutor beamVisual, TimingAreaCapabilityVisualExecutor timingAreaVisual)
        {
            var behavior = recipe.Behavior ?? new RecipeBehaviorContract();
            var serialized = new SerializedObject(controller);
            SetText(serialized, "motionType", behavior.Motion == null ? (recipe.Archetype == RecipeArchetype.Projectile || recipe.Archetype == RecipeArchetype.Trail ? "linear" : "stationary") : behavior.Motion.Type);
            SetText(serialized, "hitType", behavior.Hit == null ? "single" : behavior.Hit.Type);
            SetText(serialized, "emissionType", behavior.Emission == null ? "single" : behavior.Emission.Type);
            SetText(serialized, "timingType", behavior.Timing == null ? "instant" : behavior.Timing.Type);
            SetParameters(serialized, "motion", behavior.Motion);
            SetParameters(serialized, "hit", behavior.Hit);
            SetParameters(serialized, "emission", behavior.Emission);
            SetParameters(serialized, "timing", behavior.Timing);
            serialized.FindProperty("duration").floatValue = Mathf.Max(.1f, (float)recipe.Stages.Where(value => value.Enabled).Sum(value => value.Duration));
            serialized.FindProperty("seed").longValue = recipe.RandomSeed;
            serialized.FindProperty("core").objectReferenceValue = core;
            serialized.FindProperty("coreRenderer").objectReferenceValue = coreRenderer;
            serialized.FindProperty("directionTrail").objectReferenceValue = trail;
            serialized.FindProperty("beamLine").objectReferenceValue = beam;
            serialized.FindProperty("areaBoundary").objectReferenceValue = areaBoundary;
            serialized.FindProperty("areaRenderer").objectReferenceValue = areaRenderer;
            serialized.FindProperty("eventMarker").objectReferenceValue = marker;
            serialized.FindProperty("eventRenderer").objectReferenceValue = markerRenderer;
            serialized.FindProperty("carrierParticles").objectReferenceValue = carrierParticles;
            serialized.FindProperty("carrierRenderer").objectReferenceValue = carrierRenderer;
            serialized.FindProperty("beamVisual").objectReferenceValue = beamVisual;
            serialized.FindProperty("timingAreaVisual").objectReferenceValue = timingAreaVisual;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureTimingAreaVisual(TimingAreaCapabilityVisualExecutor executor, Recipe recipe, string slotId, Transform core, Renderer coreRenderer, Transform boundary, Renderer boundaryRenderer, Transform eventMarker, Renderer eventRenderer, LineRenderer detailLine, ParticleSystem slotParticles, ParticleSystemRenderer slotRenderer)
        {
            var behavior = recipe.Behavior ?? new RecipeBehaviorContract();
            var motion = behavior.Motion;
            var timing = behavior.Timing;
            var timingType = BehaviorType(timing);
            var motionType = BehaviorType(motion);
            var mode = timingType == "telegraph" || timingType == "delay_fuse" || timingType == "tick_pulse" || timingType == "charge_release" || timingType == "channel_interrupt" || timingType == "chain_sequence"
                ? timingType : motionType;
            var serialized = new SerializedObject(executor);
            SetText(serialized, "visualMode", mode);
            SetText(serialized, "telegraphShape", Text(timing, "shape", "circle"));
            SetText(serialized, "fillStyle", Text(timing, "fill_style", "center_fill"));
            SetText(serialized, "configuredSlotId", slotId);
            serialized.FindProperty("slotBindingResolved").boolValue = true;
            serialized.FindProperty("core").objectReferenceValue = core;
            serialized.FindProperty("coreRenderer").objectReferenceValue = coreRenderer;
            serialized.FindProperty("boundary").objectReferenceValue = boundary;
            serialized.FindProperty("boundaryRenderer").objectReferenceValue = boundaryRenderer;
            serialized.FindProperty("eventMarker").objectReferenceValue = eventMarker;
            serialized.FindProperty("eventRenderer").objectReferenceValue = eventRenderer;
            serialized.FindProperty("detailLine").objectReferenceValue = detailLine;
            serialized.FindProperty("slotParticles").objectReferenceValue = slotParticles;
            serialized.FindProperty("slotParticleRenderer").objectReferenceValue = slotRenderer;
            serialized.FindProperty("maxRadius").floatValue = Mathf.Max(.01f, Number(motion, "max_radius", 4f));
            serialized.FindProperty("edgeThickness").floatValue = Mathf.Max(.001f, Number(motion, "edge_thickness", .2f));
            serialized.FindProperty("startRadius").floatValue = Mathf.Max(.01f, Number(motion, "start_radius", 4f));
            serialized.FindProperty("growthStageCount").intValue = Mathf.Clamp(Mathf.RoundToInt(Number(motion, "stage_count", 3f)), 2, 3);
            serialized.FindProperty("growthBaseRadius").floatValue = Mathf.Max(.01f, Number(motion, "base_radius", 1f));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureBeamVisual(BeamCapabilityVisualExecutor executor, Recipe recipe, LineRenderer primaryLine, LineRenderer[] auxiliaryLines, Transform sourceMarker, Renderer sourceRenderer, Transform endpointMarker, Renderer endpointRenderer, ParticleSystem markerParticles, ParticleSystemRenderer markerRenderer)
        {
            var behavior = recipe.Behavior ?? new RecipeBehaviorContract();
            var motion = behavior.Motion;
            var hit = behavior.Hit;
            var emission = behavior.Emission;
            var timing = behavior.Timing;
            var mode = BehaviorType(timing) == "hitscan" ? "hitscan"
                : BehaviorType(motion) == "sweep" ? "sweep"
                : BehaviorType(timing) == "charge_scale" ? "charge_scale"
                : BehaviorType(hit) == "reflect" ? "reflect"
                : BehaviorType(hit) == "occlude" ? "occlude"
                : BehaviorType(emission) == "converge" ? "converge"
                : BehaviorType(hit) == "arc_link" ? "arc_link"
                : "sustained";
            var serialized = new SerializedObject(executor);
            SetText(serialized, "visualMode", mode);
            serialized.FindProperty("primaryLine").objectReferenceValue = primaryLine;
            var lines = serialized.FindProperty("auxiliaryLines");
            lines.arraySize = auxiliaryLines == null ? 0 : auxiliaryLines.Length;
            for (var i = 0; i < lines.arraySize; i++) lines.GetArrayElementAtIndex(i).objectReferenceValue = auxiliaryLines[i];
            serialized.FindProperty("sourceMarker").objectReferenceValue = sourceMarker;
            serialized.FindProperty("sourceRenderer").objectReferenceValue = sourceRenderer;
            serialized.FindProperty("endpointMarker").objectReferenceValue = endpointMarker;
            serialized.FindProperty("endpointRenderer").objectReferenceValue = endpointRenderer;
            serialized.FindProperty("markerParticles").objectReferenceValue = markerParticles;
            serialized.FindProperty("markerParticleRenderer").objectReferenceValue = markerRenderer;
            serialized.FindProperty("baseWidth").floatValue = .08f;
            serialized.FindProperty("tilingPerMeter").floatValue = 1f;
            serialized.FindProperty("hitscanLinger").floatValue = Mathf.Clamp(Number(timing, "linger", .15f), .1f, .2f);
            serialized.FindProperty("sweepSpeedMax").floatValue = Mathf.Max(0f, Number(motion, "sweep_speed_max", 90f));
            serialized.FindProperty("sweepInertia").floatValue = Mathf.Max(0f, Number(motion, "inertia", .12f));
            serialized.FindProperty("reflectSegmentLimit").intValue = Mathf.Clamp(Mathf.RoundToInt(Number(hit, "max_segments", 3f)), 1, BeamCapabilityVisualExecutor.MaxReflectSegments);
            serialized.FindProperty("reflectDamping").floatValue = Mathf.Clamp01(Number(hit, "damping_per_bounce", .2f));
            serialized.FindProperty("convergeSourceCount").intValue = Mathf.Clamp(Mathf.RoundToInt(Number(emission, "source_count", 4f)), 2, 5);
            serialized.FindProperty("focusGrowth").floatValue = Mathf.Max(0f, Number(emission, "focus_growth", 1.5f));
            serialized.FindProperty("arcHopCount").intValue = Mathf.Clamp(Mathf.RoundToInt(Number(hit, "hop_count", 4f)), 1, BeamCapabilityVisualExecutor.MaxArcHops);
            serialized.FindProperty("arcSag").floatValue = Mathf.Max(0f, Number(hit, "sag", .3f));
            serialized.FindProperty("arcJitter").floatValue = Mathf.Max(0f, Number(hit, "jitter", .12f));
            serialized.FindProperty("seed").longValue = recipe.RandomSeed;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool NeedsCarrierSystem(Recipe recipe)
        {
            var behavior = recipe.Behavior;
            var hit = behavior == null || behavior.Hit == null ? string.Empty : behavior.Hit.Type;
            var emission = behavior == null || behavior.Emission == null ? string.Empty : behavior.Emission.Type;
            return hit == "split" || emission == "fan" || emission == "burst_stagger" || emission == "ring" || emission == "volley_showcase";
        }

        private static LineRenderer AddBeamLine(GameObject target, Material material)
        {
            var line = target.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Tile;
            line.numCapVertices = 5;
            line.widthMultiplier = .08f;
            line.sharedMaterial = material;
            line.enabled = false;
            return line;
        }

        private static LineRenderer AddDetailLine(GameObject target, Material material)
        {
            var line = target.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Tile;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.widthMultiplier = .04f;
            line.sharedMaterial = material;
            line.enabled = false;
            return line;
        }

        private static string ResolveTimingSlotId(RecipeBehaviorContract behavior)
        {
            var timing = behavior == null ? null : behavior.Timing;
            var motion = behavior == null ? null : behavior.Motion;
            var timingType = BehaviorType(timing);
            var explicitTiming = Text(timing, "impact_slot", string.Empty);
            if (string.IsNullOrEmpty(explicitTiming)) explicitTiming = Text(timing, "tick_visual_slot", string.Empty);
            if (!string.IsNullOrEmpty(explicitTiming)) return explicitTiming;
            var residue = Text(motion, "residue_slot", string.Empty);
            if (!string.IsNullOrEmpty(residue)) return residue;
            if (timingType == "delay_fuse" || timingType == "charge_release") return "cap_hexflash_impact_2d";
            var motionType = BehaviorType(motion);
            return motionType == "implode" ? "cap_hexflash_impact_2d" : string.Empty;
        }

        private static ResolvedSlotVisual ResolveSlotVisual(string slotId, Catalog.TemplateCatalog catalog, Mesh neutralFallback, Material neutralMaterial)
        {
            if (string.IsNullOrEmpty(slotId)) return new ResolvedSlotVisual { RenderMode = ParticleSystemRenderMode.Mesh, Mesh = neutralFallback, Material = neutralMaterial };
            var supportPath = RecipeRoot + "/" + slotId + ".default.json";
            if (!File.Exists(Absolute(supportPath))) throw new InvalidOperationException("Resolved W-C3 visual slot recipe is missing: " + supportPath);
            var parsed = VfxDomainParser.ParseRecipe(File.ReadAllText(Absolute(supportPath)));
            if (parsed.Report.HasErrors || parsed.Value == null) throw new InvalidOperationException("Resolved W-C3 visual slot recipe is invalid: " + supportPath);
            var module = parsed.Value.Stages.Where(value => value.Enabled).SelectMany(value => value.Modules).FirstOrDefault(value => value.Enabled);
            if (module == null || string.IsNullOrEmpty(module.TemplateId)) throw new InvalidOperationException("Resolved W-C3 visual slot has no enabled template carrier: " + supportPath);
            TemplateManifest manifest;
            if (!catalog.ByTemplateId.TryGetValue(module.TemplateId, out manifest)) throw new InvalidOperationException("Resolved W-C3 visual slot template is absent from the formal catalog: " + module.TemplateId);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(manifest.AssetPath);
            var particleRenderer = prefab == null ? null : prefab.GetComponentInChildren<ParticleSystemRenderer>(true);
            if (particleRenderer != null && particleRenderer.sharedMaterial != null)
                return new ResolvedSlotVisual { RenderMode = particleRenderer.renderMode, Mesh = particleRenderer.mesh, Material = particleRenderer.sharedMaterial };
            var meshRenderer = prefab == null ? null : prefab.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(value => value.sharedMaterial != null && value.GetComponent<MeshFilter>() != null && value.GetComponent<MeshFilter>().sharedMesh != null);
            if (meshRenderer != null)
                return new ResolvedSlotVisual { RenderMode = ParticleSystemRenderMode.Mesh, Mesh = meshRenderer.GetComponent<MeshFilter>().sharedMesh, Material = meshRenderer.sharedMaterial };
            throw new InvalidOperationException("Resolved W-C3 visual slot template has no reusable particle or mesh renderer/material carrier: " + manifest.AssetPath);
        }

        private static string TimingSlotDependencyIdentity(string slotId, Catalog.TemplateCatalog catalog)
        {
            if (string.IsNullOrEmpty(slotId)) return "neutral-slot";
            var supportPath = RecipeRoot + "/" + slotId + ".default.json";
            var absolute = Absolute(supportPath);
            if (!File.Exists(absolute)) throw new InvalidOperationException("Resolved W-C3 visual slot recipe is missing: " + supportPath);
            var json = File.ReadAllText(absolute);
            var parsed = VfxDomainParser.ParseRecipe(json);
            if (parsed.Report.HasErrors || parsed.Value == null) throw new InvalidOperationException("Resolved W-C3 visual slot recipe is invalid: " + supportPath);
            var module = parsed.Value.Stages.Where(value => value.Enabled).SelectMany(value => value.Modules).FirstOrDefault(value => value.Enabled);
            if (module == null || string.IsNullOrEmpty(module.TemplateId)) throw new InvalidOperationException("Resolved W-C3 visual slot has no enabled template carrier: " + supportPath);
            TemplateManifest manifest;
            if (!catalog.ByTemplateId.TryGetValue(module.TemplateId, out manifest)) throw new InvalidOperationException("Resolved W-C3 visual slot template is absent from the formal catalog: " + module.TemplateId);
            return RecipeCanonicalizer.ComputeSha256(json) + "|" + module.TemplateId + "|" + AssetDatabase.GetAssetDependencyHash(manifest.AssetPath);
        }

        private sealed class ResolvedSlotVisual
        {
            public ParticleSystemRenderMode RenderMode;
            public Mesh Mesh;
            public Material Material;
        }

        private static string BehaviorType(RecipeCapabilityBlock block)
        {
            return block == null ? string.Empty : block.Type ?? string.Empty;
        }

        private static float Number(RecipeCapabilityBlock block, string key, float fallback)
        {
            if (block == null || block.Parameters == null) return fallback;
            JToken token;
            if (!block.Parameters.TryGetValue(key, out token) || token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)) return fallback;
            return (float)token;
        }

        private static string Text(RecipeCapabilityBlock block, string key, string fallback)
        {
            if (block == null || block.Parameters == null) return fallback;
            JToken token;
            if (!block.Parameters.TryGetValue(key, out token) || token == null || token.Type != JTokenType.String) return fallback;
            return (string)token ?? fallback;
        }

        private static void ValidateShowcaseBinding(Recipe recipe)
        {
            if (recipe.Behavior == null || recipe.Behavior.Emission == null || recipe.Behavior.Emission.Type != "volley_showcase") return;
            var emission = recipe.Behavior.Emission;
            JToken token;
            if (!emission.Parameters.TryGetValue("phase_duration", out token)) throw new InvalidOperationException(recipe.Id + ": volley_showcase requires phase_duration.");
            var phaseDuration = (double)token;
            var expectedIds = new[] { "showcase_fan", "showcase_burst_stagger", "showcase_ring" };
            var expectedTriggers = new[] { StageTrigger.OnLaunch, StageTrigger.AfterPrevious, StageTrigger.AfterPrevious };
            var stages = recipe.Stages.Where(value => value.Enabled).ToArray();
            if (stages.Length != expectedIds.Length) throw new InvalidOperationException(recipe.Id + ": volley_showcase must bind exactly three enabled Recipe stages.");
            for (var i = 0; i < expectedIds.Length; i++)
            {
                if (stages[i].Id != expectedIds[i] || stages[i].Trigger != expectedTriggers[i] || Math.Abs(stages[i].Duration - phaseDuration) > .000001d)
                    throw new InvalidOperationException(recipe.Id + ": volley_showcase stage " + i + " must bind " + expectedIds[i] + "/" + expectedTriggers[i] + "/" + phaseDuration.ToString(CultureInfo.InvariantCulture) + "s.");
            }
        }

        private static void SetParameters(SerializedObject serialized, string prefix, RecipeCapabilityBlock block)
        {
            var values = block == null ? new KeyValuePair<string, JToken>[0] : block.Parameters.Where(value => value.Value.Type == JTokenType.Integer || value.Value.Type == JTokenType.Float || value.Value.Type == JTokenType.Boolean).OrderBy(value => value.Key, StringComparer.Ordinal).ToArray();
            var keys = serialized.FindProperty(prefix + "Keys");
            var numbers = serialized.FindProperty(prefix + "Values");
            keys.arraySize = values.Length;
            numbers.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                keys.GetArrayElementAtIndex(i).stringValue = values[i].Key;
                numbers.GetArrayElementAtIndex(i).floatValue = values[i].Value.Type == JTokenType.Boolean ? ((bool)values[i].Value ? 1f : 0f) : (float)values[i].Value;
            }
        }

        private static void SetText(SerializedObject serialized, string name, string value) { serialized.FindProperty(name).stringValue = value ?? string.Empty; }

        private static void EnsureShared()
        {
            CoverageGalleryBCompiler.EnsureShared();
            ValidationGalleryCompiler.EnsureFolder(SharedRoot);
            var shader = Shader.Find(CoverageGalleryBCompiler.ShaderName);
            if (shader == null) throw new InvalidOperationException("Missing shared capability shader: " + CoverageGalleryBCompiler.ShaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(AdditiveMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "MAT_CapabilityBlank_Additive" };
                AssetDatabase.CreateAsset(material, AdditiveMaterialPath);
            }
            material.shader = shader;
            material.SetColor("_PrimaryColor", new Color32(0x8F, 0xA3, 0xB8, 0xFF));
            material.SetColor("_SecondaryColor", new Color32(0xDD, 0xE6, 0xF0, 0xFF));
            material.SetFloat("_Intensity", 1f);
            material.SetFloat("_GlobalAlpha", 1f);
            material.renderQueue = 3030;
            EditorUtility.SetDirty(material);
        }

        private static Mesh BuiltinSphere()
        {
            var temporary = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try { return temporary.GetComponent<MeshFilter>().sharedMesh; }
            finally { UnityEngine.Object.DestroyImmediate(temporary); }
        }

        private static Gradient NeutralGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color32(0xDD, 0xE6, 0xF0, 0xFF), 0), new GradientColorKey(new Color32(0x8F, 0xA3, 0xB8, 0xFF), 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            return gradient;
        }

        private static string Absolute(string assetPath) { return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath.Replace('/', Path.DirectorySeparatorChar))); }
        private static string Hash(string text) { using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(value => value.ToString("x2", CultureInfo.InvariantCulture))); }
    }
}
