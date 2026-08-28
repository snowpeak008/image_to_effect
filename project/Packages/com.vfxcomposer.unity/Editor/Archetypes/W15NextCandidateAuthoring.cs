using System;
using System.Collections.Generic;
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
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Style;
using VFXComposer.Editor.Validation;
using VFXComposer.W15NextCandidate;

namespace VFXComposer.Editor.Archetypes
{
    /// <summary>Parallel W15-only authoring path.  It never calls or overwrites the rejected W15 builder.</summary>
    public static class W15NextCandidateAuthoring
    {
        public const string CompilerVersion = "w15-next-candidate-1";
        public const string RecipeRoot = "Assets/VFX/Recipes/W15NextCandidate";
        public const string OutputRoot = "Assets/VFX/Generated/W15NextCandidate";
        public const string PreviewScenePath = "Assets/VFX/Preview/VFXPREVIEW_W15_NEXT_CANDIDATE.unity";
        public const string SharedRoot = "Assets/VFX/Shared/W15NextCandidate";
        public const string EffectShaderPath = SharedRoot + "/Shaders/W15NextCandidateLayeredUnlit.shader";
        public const string CharacterShaderPath = SharedRoot + "/Shaders/W15NextCandidateCharacterDissolve.shader";
        public const string MaterialRoot = SharedRoot + "/Materials";
        public static readonly Vector3 PreviewCellSize = new Vector3(3.8f, 2.55f, 2.4f);

        public static readonly Definition[] Definitions =
        {
            D("w15nc_scorch_decal_3d","scorch_decal_3d","Scorch Decal Next Candidate","decal","3d","dark",W15NextArchetype.Decal,W15NextVariant.ScorchDecal,C("#8A240D","#FF6A17","#FFD38A"),3.2,P("size",1.15,"lifetime",3.2,"stack_limit",4)),
            D("w15nc_frost_decal_3d","frost_decal_3d","Frost Decal Next Candidate","decal","3d","neon",W15NextArchetype.Decal,W15NextVariant.FrostDecal,C("#176A9A","#55DFFF","#E9FFFF"),3.6,P("size",1.1,"lifetime",3.6,"stack_limit",4)),
            D("w15nc_katana_trail_weapon_3d","katana_trail_weapon_3d","Katana Trail Next Candidate","weapon_trail","3d","semireal",W15NextArchetype.WeaponTrail,W15NextVariant.KatanaTrail,C("#571319","#E14727","#FFF1C7"),2.4,P("speed_threshold",1.55,"history_points",12,"fade_time",.15)),
            D("w15nc_energy_whip_trail_2d","energy_whip_trail_2d","Energy Whip Trail Next Candidate","weapon_trail","2d","neon",W15NextArchetype.WeaponTrail,W15NextVariant.EnergyWhipTrail,C("#5D118F","#E24DFF","#FFF2FF"),2.4,P("speed_threshold",1.25,"history_points",16,"fade_time",.18)),
            D("w15nc_crate_break_destruction_3d","crate_break_destruction_3d","Crate Break Next Candidate","destruction","3d","cartoon",W15NextArchetype.Destruction,W15NextVariant.CrateBreak,C("#5A2C18","#C77833","#FFE0A0"),1.55,P("piece_count",10,"explode_force",2.6,"debris_lifetime",1.55)),
            D("w15nc_crystal_shatter_destruction_3d","crystal_shatter_destruction_3d","Crystal Shatter Next Candidate","destruction","3d","holo",W15NextArchetype.Destruction,W15NextVariant.CrystalShatter,C("#135C85","#42CEFF","#E8FFFF"),1.9,P("piece_count",12,"explode_force",2.1,"debris_lifetime",1.9)),
            D("w15nc_death_dissolve_lifecycle_3d","death_dissolve_lifecycle_3d","Death Dissolve Next Candidate","lifecycle","3d","dark",W15NextArchetype.LifeCycle,W15NextVariant.DeathDissolve,C("#18231D","#668953","#FF7B2E"),1.4,P("duration",1.4,"direction","up","edge_color","#FF7B2E")),
            D("w15nc_hero_entrance_lifecycle_3d","hero_entrance_lifecycle_3d","Hero Entrance Next Candidate","lifecycle","3d","semireal",W15NextArchetype.LifeCycle,W15NextVariant.HeroEntrance,C("#8F5E11","#FFD04A","#FFFFFF"),1.25,P("duration",1.25,"direction","down","edge_color","#FFF0A0")),
            D("w15nc_twin_portal_3d","twin_portal_3d","Twin Portal Next Candidate","portal","3d","holo",W15NextArchetype.Portal,W15NextVariant.TwinPortal,C("#28116F","#9A5BFF","#72F5FF"),2.8,P("pair_id","w15_next_twin_pair","portal_radius",1.0,"swirl_speed",2.8)),
            D("w15nc_loot_beam_pickup_3d","loot_beam_pickup_3d","Loot Beam Next Candidate","loot","3d","cartoon",W15NextArchetype.Loot,W15NextVariant.LootBeam,C("#6A4008","#FFD14A","#FFFFFF"),2.8,P("rarity",3,"pickup_speed",4.8,"beam_height",2.4))
        };

        public static IEnumerable<string> RecipePaths { get { return Definitions.Select(value => RecipePath(value.Id)); } }

        [MenuItem("Tools/VFX Composer/Archetypes/Build W15 Next Candidate (Visual Pending)")]
        public static void BuildAllMenu()
        {
            BuildAll();
            Debug.Log("W15 next candidate source/build/Preview is current. Status remains W15_NEXT_CANDIDATE_VISUAL_PENDING; no L3/L4 or user visual verdict is implied.");
        }

        public static void BuildAllFromCommandLine() { BuildAll(); }

        public static void BuildAll()
        {
            EnsureRecipes();
            EnsureSharedAssets();
            foreach (var definition in Definitions)
            {
                var result = BuildAsset(definition);
                if (!result.Succeeded) throw new InvalidOperationException(definition.Id + ": " + Describe(result.Report));
            }
            BuildPreview();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void EnsureRecipes()
        {
            EnsureFolder(RecipeRoot);
            foreach (var definition in Definitions) WriteIfChanged(RecipePath(definition.Id), Recipe(definition).ToString(Formatting.Indented) + "\n");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        public static W15NextBuildResult BuildAsset(Definition definition)
        {
            var result = new W15NextBuildResult();
            if (definition == null) { result.Report.Add("E15NC000", ValidationSeverity.Error, "/definition", "Definition is missing."); return result; }
            var recipePath = RecipePath(definition.Id);
            var absolute = Absolute(recipePath);
            if (!File.Exists(absolute)) { result.Report.Add("E15NC001", ValidationSeverity.Error, "/recipe", "W15 next-candidate Recipe is missing.", new JValue(recipePath)); return result; }
            var json = File.ReadAllText(absolute);
            result.Report.AddRange(RecipeValidator.Validate(json, VfxCompiler.LoadFormalCatalog()));
            var parsed = VfxDomainParser.ParseRecipe(json);
            result.Report.AddRange(parsed.Report);
            if (result.Report.HasErrors || parsed.Value == null) return result;
            var recipe = parsed.Value;
            if (!string.Equals(recipe.Id, definition.Id, StringComparison.Ordinal) || ArchetypeToken(recipe.Archetype) != definition.Archetype)
            {
                result.Report.Add("E15NC002", ValidationSeverity.Error, "/id", "Recipe identity/archetype does not match its frozen W15 next-candidate definition.");
                return result;
            }
            var recipeHash = RecipeCanonicalizer.ComputeSha256(json);
            var buildHash = Hash(recipeHash + "|" + CompilerVersion + "|" + CompilerSignature() + "|" + Application.unityVersion);
            var folder = OutputFolder(definition.Id);
            var prefabPath = PrefabPath(definition.Id);
            result.PrefabPath = prefabPath; result.RecipeHash = recipeHash; result.BuildHash = buildHash;
            if (string.Equals(ReadManifestBuildHash(definition.Id), buildHash, StringComparison.Ordinal) && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                result.Succeeded = true; result.Unchanged = true; return result;
            }
            EnsureFolder(folder);
            var root = new GameObject("VFX_" + definition.Id);
            try
            {
                BuildRuntime(root, recipe, definition);
                ValidateCarrierBudget(root, definition, result.Report);
                if (result.Report.HasErrors) return result;
                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null) throw new InvalidOperationException("Could not save " + prefabPath);
            }
            catch (Exception exception)
            {
                result.Report.Add("E15NC003", ValidationSeverity.Error, "/build", exception.Message);
                return result;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            var audit = VfxProductionRules.EnforceAndWriteManifest(definition.Id, definition.Archetype, recipe.RecipeVersion, recipe.Revision, recipeHash, buildHash, CompilerVersion, prefabPath, folder, definition.Duration, recipePath);
            result.Report.AddRange(audit.Report);
            result.Succeeded = !result.Report.HasErrors;
            return result;
        }

        private static void EnsureSharedAssets()
        {
            EnsureFolder(MaterialRoot);
            AssetDatabase.ImportAsset(EffectShaderPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(CharacterShaderPath, ImportAssetOptions.ForceSynchronousImport);
            var effectShader = AssetDatabase.LoadAssetAtPath<Shader>(EffectShaderPath);
            var characterShader = AssetDatabase.LoadAssetAtPath<Shader>(CharacterShaderPath);
            if (effectShader == null || characterShader == null) throw new InvalidOperationException("W15 next-candidate shaders are missing or failed to import.");
            foreach (W15NextArchetype archetype in Enum.GetValues(typeof(W15NextArchetype))) EnsureMaterial(EffectMaterialPath(archetype), effectShader, Color.white);
            EnsureMaterial(CharacterMaterialPath, characterShader, new Color(.11f, .18f, .28f));
            var previewShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (previewShader == null) throw new InvalidOperationException("Universal Render Pipeline/Unlit is required for W15 preview surface carriers.");
            EnsureMaterial(SurfaceMaterialPath, previewShader, new Color(.075f, .095f, .13f));
            RequireMesh(VfxStyleSharedLibrary.QuadPath); RequireMesh(VfxStyleSharedLibrary.RingPath); RequireMesh(VfxStyleSharedLibrary.BurstPath); RequireMesh(VfxStyleSharedLibrary.ConePath); RequireMesh(VfxStyleSharedLibrary.ShardPath);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureMaterial(string path, Shader shader, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) }; AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            if (material.HasProperty("_PrimaryColor")) material.SetColor("_PrimaryColor", color);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_GlobalAlpha")) material.SetFloat("_GlobalAlpha", 1f);
            EditorUtility.SetDirty(material);
        }

        private static void BuildRuntime(GameObject root, Recipe recipe, Definition definition)
        {
            var effectMaterial = AssetDatabase.LoadAssetAtPath<Material>(EffectMaterialPath(definition.RuntimeArchetype));
            if (effectMaterial == null) throw new InvalidOperationException("Missing W15 shared effect material for " + definition.RuntimeArchetype);
            var renderers = new List<Renderer>();
            var particles = new List<ParticleSystem>();
            var controller = root.AddComponent<W15NextCandidateController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("archetype").enumValueIndex = (int)definition.RuntimeArchetype;
            serialized.FindProperty("variant").enumValueIndex = (int)definition.Variant;
            serialized.FindProperty("duration").floatValue = (float)definition.Duration;
            serialized.FindProperty("seed").longValue = recipe.RandomSeed;
            serialized.FindProperty("primary").colorValue = Palette(recipe, "primary", Color.white);
            serialized.FindProperty("secondary").colorValue = Palette(recipe, "secondary", Color.cyan);
            serialized.FindProperty("accent").colorValue = Palette(recipe, "accent", Color.white);

            switch (definition.RuntimeArchetype)
            {
                case W15NextArchetype.Decal: BuildDecal(root, recipe, effectMaterial, serialized, renderers, particles); break;
                case W15NextArchetype.WeaponTrail: BuildWeaponTrail(root, recipe, effectMaterial, serialized, renderers); break;
                case W15NextArchetype.Destruction: BuildDestruction(root, recipe, definition, effectMaterial, serialized, renderers, particles); break;
                case W15NextArchetype.LifeCycle: BuildLifeCycle(root, recipe, definition, effectMaterial, serialized, renderers, particles); break;
                case W15NextArchetype.Portal: BuildPortal(root, recipe, effectMaterial, serialized, renderers); break;
                default: BuildLoot(root, recipe, effectMaterial, serialized, renderers, particles); break;
            }
            SetObjects(serialized.FindProperty("ownedRenderers"), renderers.Cast<UnityEngine.Object>().ToArray());
            SetObjects(serialized.FindProperty("particles"), particles.Cast<UnityEngine.Object>().ToArray());
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (var renderer in renderers) if (renderer != null) renderer.enabled = false;
        }

        private static void BuildDecal(GameObject root, Recipe recipe, Material material, SerializedObject serialized, List<Renderer> renderers, List<ParticleSystem> particles)
        {
            var size = Number(recipe, "size", 1f);
            var body = MeshNode(root.transform, "SurfaceBody", VfxStyleSharedLibrary.RingPath, material, Vector3.one * size, new Vector3(0, 0, 0), Quaternion.identity, renderers);
            var edge = MeshNode(root.transform, "DirectionalEdgeCracks", VfxStyleSharedLibrary.BurstPath, material, Vector3.one * size * .76f, new Vector3(0, 0, .0012f), Quaternion.Euler(0, 0, 17), renderers);
            var residue = ParticleNode(root.transform, "SurfaceResidue", material, 12, false, .08f, .24f, .02f, renderers, particles);
            residue.transform.localPosition = new Vector3(0, 0, .0022f);
            var layers = new[] { body.transform, edge.transform, residue.transform };
            SetObjects(serialized.FindProperty("decalLayers"), layers.Cast<UnityEngine.Object>().ToArray());
            serialized.FindProperty("surfaceBias").floatValue = .006f;
            serialized.FindProperty("stackLimit").intValue = Integer(recipe, "stack_limit", 4);
        }

        private static void BuildWeaponTrail(GameObject root, Recipe recipe, Material material, SerializedObject serialized, List<Renderer> renderers)
        {
            var ribbon = DynamicMeshNode(root.transform, "SweptBladeRibbon", material, renderers);
            var endpoint = LineNode(root.transform, "LiveBladeRootTip", material, false, .045f, renderers);
            endpoint.numCapVertices = 3;
            serialized.FindProperty("weaponRibbonFilter").objectReferenceValue = ribbon.Filter;
            serialized.FindProperty("weaponRibbonRenderer").objectReferenceValue = ribbon.Renderer;
            serialized.FindProperty("weaponEndpointLine").objectReferenceValue = endpoint;
            serialized.FindProperty("historyPoints").intValue = Integer(recipe, "history_points", 12);
            serialized.FindProperty("speedThreshold").floatValue = Number(recipe, "speed_threshold", 1.5f);
            serialized.FindProperty("fadeTime").floatValue = Number(recipe, "fade_time", .15f);
        }

        private static void BuildDestruction(GameObject root, Recipe recipe, Definition definition, Material material, SerializedObject serialized, List<Renderer> renderers, List<ParticleSystem> particles)
        {
            Renderer intact;
            if (definition.Variant == W15NextVariant.CrystalShatter)
                intact = MeshNode(root.transform, "IntactCrystal", VfxStyleSharedLibrary.ShardPath, material, new Vector3(.55f, .72f, .55f), Vector3.up * .12f, Quaternion.Euler(0, 0, -8), renderers);
            else intact = PrimitiveNode(root.transform, "IntactCrate", PrimitiveType.Cube, material, new Vector3(.72f, .72f, .48f), Vector3.up * .05f, Quaternion.identity, renderers);
            var fragments = DynamicMeshNode(root.transform, "IndependentFragmentField", material, renderers);
            var dust = ParticleNode(root.transform, definition.Variant == W15NextVariant.CrystalShatter ? "SuspendedCrystalDust" : "ImpactDust", material, 24, false, .05f, definition.Variant == W15NextVariant.CrystalShatter ? .75f : .48f, definition.Variant == W15NextVariant.CrystalShatter ? .16f : .42f, renderers, particles);
            serialized.FindProperty("destructionIntact").objectReferenceValue = intact.transform;
            serialized.FindProperty("destructionIntactRenderer").objectReferenceValue = intact;
            serialized.FindProperty("destructionFragmentsFilter").objectReferenceValue = fragments.Filter;
            serialized.FindProperty("destructionFragmentsRenderer").objectReferenceValue = fragments.Renderer;
            serialized.FindProperty("destructionDust").objectReferenceValue = dust;
            serialized.FindProperty("pieceCount").intValue = Integer(recipe, "piece_count", 10);
            serialized.FindProperty("explodeForce").floatValue = Number(recipe, "explode_force", 2.6f);
            serialized.FindProperty("debrisLifetime").floatValue = Number(recipe, "debris_lifetime", 1.5f);
        }

        private static void BuildLifeCycle(GameObject root, Recipe recipe, Definition definition, Material material, SerializedObject serialized, List<Renderer> renderers, List<ParticleSystem> particles)
        {
            var edge = MeshNode(root.transform, "BoundBodyDissolveEdge", VfxStyleSharedLibrary.RingPath, material, new Vector3(.62f, .28f, .62f), Vector3.up * .02f, Quaternion.Euler(72, 0, 0), renderers);
            var ash = ParticleNode(root.transform, definition.Variant == W15NextVariant.HeroEntrance ? "BodyAssemblyMotes" : "BodyAshMotes", material, 20, true, .035f, .55f, definition.Variant == W15NextVariant.HeroEntrance ? -.22f : .32f, renderers, particles);
            serialized.FindProperty("lifecycleEdgeRenderer").objectReferenceValue = edge;
            serialized.FindProperty("lifecycleParticles").objectReferenceValue = ash;
            serialized.FindProperty("lifecycleDirection").stringValue = Text(recipe, "direction", "up");
            serialized.FindProperty("inverseEntrance").boolValue = definition.Variant == W15NextVariant.HeroEntrance;
        }

        private static void BuildPortal(GameObject root, Recipe recipe, Material material, SerializedObject serialized, List<Renderer> renderers)
        {
            var ring = MeshNode(root.transform, "PairedPortalRing", VfxStyleSharedLibrary.RingPath, material, Vector3.one * .82f, Vector3.zero, Quaternion.identity, renderers);
            var interior = MeshNode(root.transform, "PortalInterior", VfxStyleSharedLibrary.QuadPath, material, Vector3.one * .58f, new Vector3(0, 0, .012f), Quaternion.identity, renderers);
            var funnel = MeshNode(root.transform, "EntryIntakeFunnel", VfxStyleSharedLibrary.ConePath, material, new Vector3(.7f, .84f, .7f), new Vector3(0, 0, .025f), Quaternion.Euler(0, 0, 90), renderers);
            var burst = MeshNode(root.transform, "ExitEjectionBurst", VfxStyleSharedLibrary.BurstPath, material, Vector3.one * .68f, new Vector3(0, 0, .032f), Quaternion.identity, renderers);
            var flow = LineNode(root.transform, "DirectionalPortalFlow", material, false, .042f, renderers);
            flow.numCapVertices = 2;
            serialized.FindProperty("portalRing").objectReferenceValue = ring.transform;
            serialized.FindProperty("portalRingRenderer").objectReferenceValue = ring;
            serialized.FindProperty("portalInterior").objectReferenceValue = interior.transform;
            serialized.FindProperty("portalInteriorRenderer").objectReferenceValue = interior;
            serialized.FindProperty("portalEntryFunnel").objectReferenceValue = funnel.transform;
            serialized.FindProperty("portalEntryFunnelRenderer").objectReferenceValue = funnel;
            serialized.FindProperty("portalExitBurst").objectReferenceValue = burst.transform;
            serialized.FindProperty("portalExitBurstRenderer").objectReferenceValue = burst;
            serialized.FindProperty("portalFlowLine").objectReferenceValue = flow;
            serialized.FindProperty("pairId").stringValue = Text(recipe, "pair_id", "w15_next_twin_pair");
            serialized.FindProperty("portalRadius").floatValue = Number(recipe, "portal_radius", 1f);
            serialized.FindProperty("swirlSpeed").floatValue = Number(recipe, "swirl_speed", 2.8f);
        }

        private static void BuildLoot(GameObject root, Recipe recipe, Material material, SerializedObject serialized, List<Renderer> renderers, List<ParticleSystem> particles)
        {
            var baseRing = MeshNode(root.transform, "WorldLootToken", VfxStyleSharedLibrary.RingPath, material, new Vector3(.45f, .24f, .45f), Vector3.zero, Quaternion.Euler(68, 0, 0), renderers);
            var beam = MeshNode(root.transform, "RarityBeam", VfxStyleSharedLibrary.ConePath, material, Vector3.one, new Vector3(0, .02f, .01f), Quaternion.identity, renderers);
            var crown = DynamicMeshNode(root.transform, "RarityGeometryAndLayers", material, renderers);
            var sparkles = ParticleNode(root.transform, "RarityCadenceSparkles", material, 20, true, .035f, .42f, .18f, renderers, particles);
            var arc = LineNode(root.transform, "CurvedPickupArc", material, true, .032f, renderers);
            arc.numCapVertices = 3;
            serialized.FindProperty("lootBase").objectReferenceValue = baseRing.transform;
            serialized.FindProperty("lootBaseRenderer").objectReferenceValue = baseRing;
            serialized.FindProperty("lootBeam").objectReferenceValue = beam.transform;
            serialized.FindProperty("lootBeamRenderer").objectReferenceValue = beam;
            serialized.FindProperty("lootCrownFilter").objectReferenceValue = crown.Filter;
            serialized.FindProperty("lootCrownRenderer").objectReferenceValue = crown.Renderer;
            serialized.FindProperty("lootSparkles").objectReferenceValue = sparkles;
            serialized.FindProperty("lootPickupArc").objectReferenceValue = arc;
            serialized.FindProperty("rarity").intValue = Integer(recipe, "rarity", 3);
            serialized.FindProperty("pickupSpeed").floatValue = Number(recipe, "pickup_speed", 4.8f);
            serialized.FindProperty("beamHeight").floatValue = Number(recipe, "beam_height", 2.4f);
        }

        private static void ValidateCarrierBudget(GameObject root, Definition definition, ValidationReport report)
        {
            var rendererLimit = definition.RuntimeArchetype == W15NextArchetype.Decal || definition.RuntimeArchetype == W15NextArchetype.WeaponTrail || definition.RuntimeArchetype == W15NextArchetype.Destruction ? 3 : definition.RuntimeArchetype == W15NextArchetype.LifeCycle ? 2 : 5;
            var particleLimit = definition.RuntimeArchetype == W15NextArchetype.Destruction ? 56 : definition.RuntimeArchetype == W15NextArchetype.Portal ? 0 : 24;
            var transformLimit = definition.RuntimeArchetype == W15NextArchetype.Destruction || definition.RuntimeArchetype == W15NextArchetype.Portal ? 16 : 10;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
            var transformCount = root.GetComponentsInChildren<Transform>(true).Length;
            var capacity = particleSystems.Sum(value => value.main.maxParticles);
            if (renderers.Length > rendererLimit) report.Add("E15NC010", ValidationSeverity.Error, "/budget/renderers", "W15 carrier exceeds its archetype renderer budget.", new JValue(renderers.Length), "<= " + rendererLimit);
            if (capacity > particleLimit) report.Add("E15NC011", ValidationSeverity.Error, "/budget/particles", "W15 carrier exceeds its archetype particle budget.", new JValue(capacity), "<= " + particleLimit);
            if (transformCount > transformLimit) report.Add("E15NC015", ValidationSeverity.Error, "/budget/transforms", "W15 carrier exceeds its archetype transform budget.", new JValue(transformCount), "<= " + transformLimit);
            if (root.GetComponentsInChildren<Rigidbody>(true).Length != 0) report.Add("E15NC012", ValidationSeverity.Error, "/carrier/destruction", "W15 deterministic destruction must not contain Rigidbody/Physics carriers.");
            if (root.GetComponents<MonoBehaviour>().Count(value => value is IVfxRuntimeEntry) != 1) report.Add("E15NC013", ValidationSeverity.Error, "/runtimeEntry", "W15 next-candidate prefab root must have exactly one runtime entry.");
            if (root.GetComponentInChildren<VFXComposer.W15NextCandidate.NewArchetypePreviewDriver>(true) != null) report.Add("E15NC014", ValidationSeverity.Error, "/runtimeEntry", "Preview driver leaked into a production prefab.");
        }

        private static void BuildPreview()
        {
            EnsureFolder("Assets/VFX/Preview");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var entries = new List<W15NextCandidateController>();
            var decalEntries = new List<W15NextCandidateController>();
            var decalAnchors = new List<Transform>();
            var fastWeapons = new List<W15NextCandidateController>();
            var slowWeapons = new List<W15NextCandidateController>();
            var lootEntries = new List<W15NextCandidateController>();
            W15NextCandidateController deathEntry = null, entranceEntry = null, portalEntry = null, portalExit = null;
            Renderer[] deathCharacter = new Renderer[0], entranceCharacter = new Renderer[0];
            Transform lootTarget = null;
            var surfaceMaterial = AssetDatabase.LoadAssetAtPath<Material>(SurfaceMaterialPath);
            var dissolveMaterial = AssetDatabase.LoadAssetAtPath<Material>(CharacterMaterialPath);
            var borderMaterial = AssetDatabase.LoadAssetAtPath<Material>(EffectMaterialPath(W15NextArchetype.Decal));

            for (var index = 0; index < Definitions.Length; index++)
            {
                var definition = Definitions[index];
                var row = index / 3; var column = index % 3;
                var holder = new GameObject("W15_NEXT_Cell_" + (index + 1).ToString("00", CultureInfo.InvariantCulture) + "_" + definition.OriginalId);
                holder.transform.position = new Vector3((column - 1) * 4.2f, 4.35f - row * 2.9f, 0f);
                var bounds = holder.AddComponent<BoxCollider>(); bounds.isTrigger = true; bounds.size = PreviewCellSize;
                AddCellBorder(holder.transform, borderMaterial);
                AddLabel(holder.transform, (index + 1) + "  " + definition.Archetype.ToUpperInvariant(), new Vector3(0, -1.08f, -.05f), .032f);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(definition.Id));
                if (prefab == null) throw new InvalidOperationException("Missing W15 next-candidate prefab: " + PrefabPath(definition.Id));
                switch (definition.RuntimeArchetype)
                {
                    case W15NextArchetype.Decal:
                        BuildDecalCell(prefab, holder.transform, definition, surfaceMaterial, entries, decalEntries, decalAnchors);
                        break;
                    case W15NextArchetype.WeaponTrail:
                        var fast = InstantiateEntry(prefab, holder.transform, "FAST_REAL_SWING", new Vector3(-.72f, .02f, 0), .62f);
                        var slow = InstantiateEntry(prefab, holder.transform, "SLOW_BELOW_THRESHOLD", new Vector3(.72f, .02f, 0), .62f);
                        AddLabel(holder.transform, "FAST", new Vector3(-.72f, -.72f, 0), .025f); AddLabel(holder.transform, "SLOW / FADE", new Vector3(.72f, -.72f, 0), .025f);
                        entries.Add(fast); entries.Add(slow); fastWeapons.Add(fast); slowWeapons.Add(slow);
                        break;
                    case W15NextArchetype.LifeCycle:
                        var life = InstantiateEntry(prefab, holder.transform, "BOUND_EFFECT", Vector3.zero, .72f);
                        var character = BuildCharacter(holder.transform, definition.Variant == W15NextVariant.DeathDissolve ? "BoundCharacter_Death" : "BoundCharacter_Entrance", dissolveMaterial);
                        entries.Add(life);
                        if (definition.Variant == W15NextVariant.DeathDissolve) { deathEntry = life; deathCharacter = character; }
                        else { entranceEntry = life; entranceCharacter = character; }
                        break;
                    case W15NextArchetype.Portal:
                        portalEntry = InstantiateEntry(prefab, holder.transform, "ENTRY_INTAKE", new Vector3(-.72f, .08f, 0), .58f);
                        portalExit = InstantiateEntry(prefab, holder.transform, "EXIT_EJECTION", new Vector3(.72f, .08f, 0), .58f);
                        portalEntry.ConfigurePortal("w15_next_twin_pair", PortalEndpointRole.Entry); portalExit.ConfigurePortal("w15_next_twin_pair", PortalEndpointRole.Exit);
                        EditorUtility.SetDirty(portalEntry); EditorUtility.SetDirty(portalExit);
                        AddLabel(holder.transform, "ENTRY 0.00s", new Vector3(-.72f, -.7f, 0), .023f); AddLabel(holder.transform, "EXIT +0.35s", new Vector3(.72f, -.7f, 0), .023f);
                        entries.Add(portalEntry); entries.Add(portalExit);
                        break;
                    case W15NextArchetype.Loot:
                        for (var rarity = 1; rarity <= 5; rarity++)
                        {
                            var loot = InstantiateEntry(prefab, holder.transform, "RARITY_" + rarity, new Vector3((rarity - 3) * .64f, -.28f, 0), .34f);
                            loot.ConfigureRarity(rarity); EditorUtility.SetDirty(loot); entries.Add(loot); lootEntries.Add(loot);
                            AddLabel(holder.transform, "T" + rarity, new Vector3((rarity - 3) * .64f, -.83f, 0), .021f);
                        }
                        var targetObject = new GameObject("SharedWorldPickupEndpoint"); targetObject.transform.SetParent(holder.transform, false); targetObject.transform.localPosition = new Vector3(0, .84f, 0); lootTarget = targetObject.transform;
                        break;
                    default:
                        var previewScale = definition.RuntimeArchetype == W15NextArchetype.Destruction ? .46f : .68f;
                        var entry = InstantiateEntry(prefab, holder.transform, "RUNTIME", new Vector3(0, -.05f, 0), previewScale); entries.Add(entry);
                        break;
                }
            }

            var cameraObject = new GameObject("W15NextCandidateReviewCamera"); cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 6.15f; camera.transform.position = new Vector3(0, .25f, -18f); camera.transform.rotation = Quaternion.identity; camera.nearClipPlane = .05f; camera.farClipPlane = 40f; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.012f, .018f, .03f); camera.allowHDR = false; camera.allowMSAA = false;
            var driverObject = new GameObject("W15NextCandidatePreviewDriver");
            var driver = driverObject.AddComponent<VFXComposer.W15NextCandidate.NewArchetypePreviewDriver>();
            var serialized = new SerializedObject(driver);
            SetObjects(serialized.FindProperty("entries"), entries.Cast<UnityEngine.Object>().ToArray());
            SetObjects(serialized.FindProperty("decalEntries"), decalEntries.Cast<UnityEngine.Object>().ToArray());
            SetObjects(serialized.FindProperty("decalAnchors"), decalAnchors.Cast<UnityEngine.Object>().ToArray());
            SetObjects(serialized.FindProperty("fastWeaponEntries"), fastWeapons.Cast<UnityEngine.Object>().ToArray());
            SetObjects(serialized.FindProperty("slowWeaponEntries"), slowWeapons.Cast<UnityEngine.Object>().ToArray());
            serialized.FindProperty("deathEntry").objectReferenceValue = deathEntry; SetObjects(serialized.FindProperty("deathCharacter"), deathCharacter.Cast<UnityEngine.Object>().ToArray());
            serialized.FindProperty("entranceEntry").objectReferenceValue = entranceEntry; SetObjects(serialized.FindProperty("entranceCharacter"), entranceCharacter.Cast<UnityEngine.Object>().ToArray());
            serialized.FindProperty("portalEntry").objectReferenceValue = portalEntry; serialized.FindProperty("portalExit").objectReferenceValue = portalExit;
            SetObjects(serialized.FindProperty("lootEntries"), lootEntries.Cast<UnityEngine.Object>().ToArray()); serialized.FindProperty("lootPickupTarget").objectReferenceValue = lootTarget;
            serialized.FindProperty("cycleDuration").floatValue = 6f; serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, PreviewScenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static void BuildDecalCell(GameObject prefab, Transform holder, Definition definition, Material surfaceMaterial, List<W15NextCandidateController> entries, List<W15NextCandidateController> decalEntries, List<Transform> anchors)
        {
            var positions = new[] { new Vector3(-1.05f, -.22f, .1f), new Vector3(0, .02f, .18f), new Vector3(1.05f, -.1f, .12f) };
            var normals = new[] { Vector3.up, Vector3.back, new Vector3(0, .7071068f, -.7071068f) };
            var tangents = new[] { Vector3.forward, Vector3.up, new Vector3(0, .7071068f, .7071068f) };
            var names = new[] { "GROUND", "WALL", "SLOPE_45" };
            for (var index = 0; index < 3; index++)
            {
                var support = PrimitiveNode(holder, "SurfaceCarrier_" + names[index], PrimitiveType.Cube, surfaceMaterial, index == 1 ? new Vector3(.72f, .72f, .06f) : new Vector3(.72f, .06f, .62f), positions[index], index == 2 ? Quaternion.Euler(-45, 0, 0) : Quaternion.identity, new List<Renderer>());
                var anchorObject = new GameObject("DecalAnchor_" + names[index]); anchorObject.transform.SetParent(holder, false);
                anchorObject.transform.localPosition = positions[index] + normals[index] * (index == 1 ? .04f : .055f);
                anchorObject.transform.localRotation = Quaternion.LookRotation(normals[index], tangents[index]);
                var instance = InstantiateEntry(prefab, holder, definition.Variant + "_" + names[index], anchorObject.transform.localPosition, .34f);
                entries.Add(instance); decalEntries.Add(instance); anchors.Add(anchorObject.transform);
                AddLabel(holder, names[index], positions[index] + new Vector3(0, -.58f, 0), .019f);
                support.sortingOrder = -20;
            }
        }

        private static Renderer[] BuildCharacter(Transform holder, string name, Material material)
        {
            var root = new GameObject(name); root.transform.SetParent(holder, false); root.transform.localPosition = new Vector3(0, -.22f, .08f);
            var renderers = new List<Renderer>();
            PrimitiveNode(root.transform, "Torso", PrimitiveType.Capsule, material, new Vector3(.32f, .42f, .2f), new Vector3(0, .35f, 0), Quaternion.identity, renderers);
            PrimitiveNode(root.transform, "Head", PrimitiveType.Sphere, material, Vector3.one * .25f, new Vector3(0, .88f, 0), Quaternion.identity, renderers);
            PrimitiveNode(root.transform, "Arm_L", PrimitiveType.Capsule, material, new Vector3(.11f, .34f, .11f), new Vector3(-.35f, .42f, 0), Quaternion.Euler(0, 0, -18), renderers);
            PrimitiveNode(root.transform, "Arm_R", PrimitiveType.Capsule, material, new Vector3(.11f, .34f, .11f), new Vector3(.35f, .42f, 0), Quaternion.Euler(0, 0, 18), renderers);
            PrimitiveNode(root.transform, "Leg_L", PrimitiveType.Capsule, material, new Vector3(.13f, .38f, .13f), new Vector3(-.16f, -.28f, 0), Quaternion.identity, renderers);
            PrimitiveNode(root.transform, "Leg_R", PrimitiveType.Capsule, material, new Vector3(.13f, .38f, .13f), new Vector3(.16f, -.28f, 0), Quaternion.identity, renderers);
            return renderers.ToArray();
        }

        private static W15NextCandidateController InstantiateEntry(GameObject prefab, Transform parent, string name, Vector3 localPosition, float scale)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name; instance.transform.localPosition = localPosition; instance.transform.localRotation = Quaternion.identity; instance.transform.localScale = Vector3.one * scale;
            var entry = instance.GetComponent<W15NextCandidateController>();
            if (entry == null) throw new InvalidOperationException(prefab.name + " has no W15NextCandidateController.");
            return entry;
        }

        private static void AddCellBorder(Transform parent, Material material)
        {
            var go = new GameObject("FixedCellBounds"); go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.loop = true; line.positionCount = 4; line.widthMultiplier = .012f; line.sharedMaterial = material; line.sortingOrder = -50;
            var x = PreviewCellSize.x * .5f; var y = PreviewCellSize.y * .5f;
            line.SetPositions(new[] { new Vector3(-x, -y, .25f), new Vector3(-x, y, .25f), new Vector3(x, y, .25f), new Vector3(x, -y, .25f) });
        }

        private static Renderer MeshNode(Transform parent, string name, string meshPath, Material material, Vector3 scale, Vector3 position, Quaternion rotation, List<Renderer> renderers)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localScale = scale; go.transform.localPosition = position; go.transform.localRotation = rotation;
            go.AddComponent<MeshFilter>().sharedMesh = RequireMesh(meshPath); var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.enabled = false; renderer.sortingOrder = 20 + renderers.Count; renderers.Add(renderer); return renderer;
        }

        private static DynamicNode DynamicMeshNode(Transform parent, string name, Material material, List<Renderer> renderers)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); var filter = go.AddComponent<MeshFilter>(); var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.enabled = false; renderer.sortingOrder = 20 + renderers.Count; renderers.Add(renderer); return new DynamicNode(filter, renderer);
        }

        private static Renderer PrimitiveNode(Transform parent, string name, PrimitiveType type, Material material, Vector3 scale, Vector3 position, Quaternion rotation, List<Renderer> renderers)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false); go.transform.localScale = scale; go.transform.localPosition = position; go.transform.localRotation = rotation;
            var collider = go.GetComponent<Collider>(); if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material; renderer.enabled = true; renderer.sortingOrder = 20 + renderers.Count; renderers.Add(renderer); return renderer;
        }

        private static LineRenderer LineNode(Transform parent, string name, Material material, bool worldSpace, float width, List<Renderer> renderers)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = material; line.useWorldSpace = worldSpace; line.positionCount = 0; line.widthMultiplier = width; line.enabled = false; line.alignment = LineAlignment.View; line.sortingOrder = 20 + renderers.Count; renderers.Add(line); return line;
        }

        private static ParticleSystem ParticleNode(Transform parent, string name, Material material, int maxParticles, bool loop, float size, float lifetime, float speed, List<Renderer> renderers, List<ParticleSystem> particles)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); var particle = go.AddComponent<ParticleSystem>(); particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particle.main; main.playOnAwake = false; main.loop = loop; main.duration = 1f; main.startLifetime = lifetime; main.startSpeed = speed; main.startSize = new ParticleSystem.MinMaxCurve(size * .65f, size * 1.35f); main.maxParticles = maxParticles; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = particle.emission; emission.rateOverTime = loop ? Mathf.Min(14, maxParticles) : 0;
            var shape = particle.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .35f;
            var color = particle.colorOverLifetime; color.enabled = true; color.color = new ParticleSystem.MinMaxGradient(new Gradient { colorKeys = new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, alphaKeys = new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) } });
            var renderer = go.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = RequireMesh(VfxStyleSharedLibrary.ShardPath); renderer.sharedMaterial = material; renderer.enabled = false; renderer.sortingOrder = 20 + renderers.Count;
            renderers.Add(renderer); particles.Add(particle); return particle;
        }

        private static void AddLabel(Transform parent, string text, Vector3 position, float characterSize)
        {
            var go = new GameObject("Label_" + text.Replace(' ', '_').Replace('/', '_')); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var label = go.AddComponent<TextMesh>(); label.text = text; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.fontSize = 34; label.characterSize = characterSize; label.color = new Color(.68f, .77f, .9f);
        }

        private static JObject Recipe(Definition definition)
        {
            return new JObject
            {
                ["recipeVersion"] = 1, ["revision"] = 1, ["id"] = definition.Id, ["name"] = definition.Name,
                ["dimension"] = definition.Dimension, ["archetype"] = definition.Archetype,
                ["style"] = new JObject { ["token"] = definition.Style, ["palette"] = definition.Palette, ["glow_strength"] = definition.Style == "dark" ? .78 : 1.25 },
                ["archetypeParameters"] = definition.Parameters, ["targetProfile"] = "mobile_medium", ["randomSeed"] = StableSeed(definition.Id),
                ["stages"] = new JArray(new JObject { ["id"] = "active", ["trigger"] = "manual", ["duration"] = definition.Duration, ["enabled"] = true, ["modules"] = Modules(definition) }),
                ["metadata"] = new JObject { ["createdBy"] = "w15-next-candidate-authoring", ["templateCatalogVersion"] = "formal-1" }
            };
        }

        private static JArray Modules(Definition definition)
        {
            var prefix = definition.Dimension == "2d" ? "PFT_2D_" : "PFT_3D_";
            var modules = new JArray(new JObject { ["id"] = "core", ["kind"] = "energy_body", ["templateId"] = prefix + "FireCore", ["parameters"] = new JObject { ["scale"] = 1.0 }, ["enabled"] = true });
            if (definition.RuntimeArchetype == W15NextArchetype.WeaponTrail || definition.RuntimeArchetype == W15NextArchetype.Portal)
                modules.Add(new JObject { ["id"] = "flow", ["kind"] = "motion_trail", ["templateId"] = prefix + "FireTrail", ["parameters"] = new JObject { ["time"] = .22, ["width"] = .3 }, ["attachTo"] = "core", ["enabled"] = true });
            else if (definition.RuntimeArchetype == W15NextArchetype.Destruction)
                modules.Add(new JObject { ["id"] = "fragments", ["kind"] = "impact_burst", ["templateId"] = prefix + "FireImpact", ["parameters"] = new JObject { ["count"] = Integer(definition.Parameters, "piece_count", 10), ["speed"] = Number(definition.Parameters, "explode_force", 2.5f) }, ["attachTo"] = "core", ["enabled"] = true });
            else
                modules.Add(new JObject { ["id"] = "secondary", ["kind"] = "secondary_particles", ["templateId"] = prefix + "Embers", ["parameters"] = new JObject { ["rate"] = 8.0, ["lifetime"] = .45 }, ["attachTo"] = "core", ["enabled"] = true });
            return modules;
        }

        private static string CompilerSignature()
        {
            var paths = new[]
            {
                EffectShaderPath, CharacterShaderPath, "Packages/com.vfxcomposer.unity/Runtime/W15/W15NextCandidateController.cs",
                VfxStyleSharedLibrary.QuadPath, VfxStyleSharedLibrary.RingPath, VfxStyleSharedLibrary.BurstPath, VfxStyleSharedLibrary.ConePath, VfxStyleSharedLibrary.ShardPath
            };
            return string.Join("|", paths.Select(path => path + ":" + AssetDatabase.GetAssetDependencyHash(path)).ToArray());
        }

        private static Mesh RequireMesh(string path)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (mesh == null) throw new InvalidOperationException("Required existing shared mesh is missing: " + path); return mesh;
        }

        private static string RecipePath(string id) { return RecipeRoot + "/" + id + ".default.json"; }
        private static string OutputFolder(string id) { return OutputRoot + "/" + id; }
        private static string PrefabPath(string id) { return OutputFolder(id) + "/VFX_" + id + ".prefab"; }
        private static string EffectMaterialPath(W15NextArchetype archetype) { return MaterialRoot + "/MAT_W15NC_" + archetype + ".mat"; }
        public const string CharacterMaterialPath = MaterialRoot + "/MAT_W15NC_CharacterDissolve.mat";
        public const string SurfaceMaterialPath = MaterialRoot + "/MAT_W15NC_PreviewSurface.mat";

        private static Color Palette(Recipe recipe, string key, Color fallback) { string value; Color color; return recipe.Style != null && recipe.Style.Palette.TryGetValue(key, out value) && ColorUtility.TryParseHtmlString(value, out color) ? color : fallback; }
        private static float Number(Recipe recipe, string key, float fallback) { JToken token; return recipe.ArchetypeParameters.TryGetValue(key, out token) ? (float)token : fallback; }
        private static int Integer(Recipe recipe, string key, int fallback) { JToken token; return recipe.ArchetypeParameters.TryGetValue(key, out token) ? (int)token : fallback; }
        private static string Text(Recipe recipe, string key, string fallback) { JToken token; return recipe.ArchetypeParameters.TryGetValue(key, out token) ? (string)token : fallback; }
        private static float Number(JObject values, string key, float fallback) { JToken token; return values.TryGetValue(key, out token) ? (float)token : fallback; }
        private static int Integer(JObject values, string key, int fallback) { JToken token; return values.TryGetValue(key, out token) ? (int)token : fallback; }
        private static string ArchetypeToken(RecipeArchetype value) { if (value == RecipeArchetype.WeaponTrail) return "weapon_trail"; if (value == RecipeArchetype.LifeCycle) return "lifecycle"; return value.ToString().ToLowerInvariant(); }
        private static string Describe(ValidationReport report) { return string.Join(" | ", report.Entries.Select(value => value.Code + " " + value.Path + " " + value.Message).ToArray()); }
        private static string ReadManifestBuildHash(string id) { var path = VfxProjectRules.ManifestAbsolutePath(id); if (!File.Exists(path)) return null; try { return (string)JObject.Parse(File.ReadAllText(path))["buildHash"]; } catch { return null; } }
        private static void SetObjects(SerializedProperty property, UnityEngine.Object[] values) { property.arraySize = values.Length; for (var index = 0; index < values.Length; index++) property.GetArrayElementAtIndex(index).objectReferenceValue = values[index]; }
        private static string Absolute(string assetPath) { return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath.Replace('/', Path.DirectorySeparatorChar))); }
        private static void WriteIfChanged(string path, string content) { var absolute = Absolute(path); Directory.CreateDirectory(Path.GetDirectoryName(absolute)); if (File.Exists(absolute) && string.Equals(File.ReadAllText(absolute), content, StringComparison.Ordinal)) return; File.WriteAllText(absolute, content, new UTF8Encoding(false)); }
        private static void EnsureFolder(string path) { if (AssetDatabase.IsValidFolder(path)) return; var parent = Path.GetDirectoryName(path).Replace('\\', '/'); if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); }
        private static string Hash(string value) { using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(item => item.ToString("x2", CultureInfo.InvariantCulture))); }
        private static uint StableSeed(string value) { unchecked { var hash = 2166136261u; for (var index = 0; index < value.Length; index++) { hash ^= value[index]; hash *= 16777619u; } return hash; } }
        private static JObject C(string primary, string secondary, string accent) { return new JObject { ["primary"] = primary, ["secondary"] = secondary, ["accent"] = accent }; }
        private static JObject P(params object[] values) { var result = new JObject(); for (var index = 0; index < values.Length; index += 2) result[(string)values[index]] = JToken.FromObject(values[index + 1]); return result; }
        private static Definition D(string id, string originalId, string name, string archetype, string dimension, string style, W15NextArchetype runtimeArchetype, W15NextVariant variant, JObject palette, double duration, JObject parameters) { return new Definition(id, originalId, name, archetype, dimension, style, runtimeArchetype, variant, palette, duration, parameters); }

        private sealed class DynamicNode
        {
            public readonly MeshFilter Filter; public readonly MeshRenderer Renderer;
            public DynamicNode(MeshFilter filter, MeshRenderer renderer) { Filter = filter; Renderer = renderer; }
        }

        public sealed class Definition
        {
            public readonly string Id, OriginalId, Name, Archetype, Dimension, Style;
            public readonly W15NextArchetype RuntimeArchetype; public readonly W15NextVariant Variant;
            public readonly JObject Palette, Parameters; public readonly double Duration;
            public Definition(string id, string originalId, string name, string archetype, string dimension, string style, W15NextArchetype runtimeArchetype, W15NextVariant variant, JObject palette, double duration, JObject parameters)
            { Id = id; OriginalId = originalId; Name = name; Archetype = archetype; Dimension = dimension; Style = style; RuntimeArchetype = runtimeArchetype; Variant = variant; Palette = palette; Duration = duration; Parameters = parameters; }
        }
    }

    public sealed class W15NextBuildResult
    {
        public bool Succeeded;
        public bool Unchanged;
        public string PrefabPath;
        public string RecipeHash;
        public string BuildHash;
        public ValidationReport Report = new ValidationReport();
    }
}
