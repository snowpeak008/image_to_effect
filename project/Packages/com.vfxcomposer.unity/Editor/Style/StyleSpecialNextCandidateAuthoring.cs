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
using UnityEngine.UI;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Validation;
using VFXComposer.Editor.ValidationGallery;

namespace VFXComposer.Editor.Style
{
    public sealed class StyleSpecialNextCandidateSpec
    {
        public string RecipePath;
        public string Id;
        public string SourceId;
        public string Label;
        public string Archetype;
        public string Dimension;
        public StyleSpecialCandidateGroup Group;
        public StyleSpecialMotionProfile MotionProfile;
        public string StyleToken;
        public string SemanticCode;
        public string PairFamily;
        public string PairRole;
        public string SourceBaseId;
        public Color Primary;
        public Color Secondary;
        public Color Accent;
        public float Duration;
        public float ReleaseNormalized;
        public float SustainEndNormalized;
        public float Intensity;
        public bool Sustained;
        public uint Seed;
        public string[] MeshTokens;
    }

    public sealed class StyleSpecialNextCandidateBuildResult
    {
        public string EffectId;
        public string RecipePath;
        public string PrefabPath;
        public string RecipeHash;
        public string BuildHash;
        public bool Succeeded;
        public bool Unchanged;
        public readonly ValidationReport Report = new ValidationReport();
    }

    /// <summary>
    /// Isolated W9/W10/W16 next-candidate compiler. It reads only the frozen descriptors below,
    /// never calls the previous StyleSpecial builder and writes only suffixed Generated entries
    /// plus three suffixed Preview scenes.
    /// </summary>
    public static class StyleSpecialNextCandidateAuthoring
    {
        public const string CompilerVersion = "style-special-next-candidate-1";
        public const string CandidateStatusRootName = "STYLE_SPECIAL_NEXT_CANDIDATE_VISUAL_PENDING";
        public const string RecipeRoot = "Assets/VFX/Recipes/StyleSpecialsNextCandidate";
        public const string W9PreviewScenePath = "Assets/VFX/Preview/VFXPREVIEW_Style2D_NextCandidate.unity";
        public const string W10PreviewScenePath = "Assets/VFX/Preview/VFXPREVIEW_Style3D_NextCandidate.unity";
        public const string W16PreviewScenePath = "Assets/VFX/Preview/VFXPREVIEW_StylePack2_NextCandidate.unity";
        public const int FirstIsolatedLayer = 8;
        public const float PreviewEntryScale = 1f;
        private const string DescriptorSchema = "style-special-next-candidate/v1";
        private const string RuntimeImplementationSignature = "real-style-material-mpb-v1|explicit-lifecycle-phases-v1|bounded-mesh-topologies-v1|w16-paired-combinations-v1|exclusive-cell-cameras-v1|alternating-w10-viewpoints-v1";

        private static readonly string[] W9SourceIds =
        {
            "pixel_burst_impact_2d", "pixel_sword_slash_2d", "pixel_heal_aura_2d", "anime_smear_slash_2d", "poof_smoke_spawn_2d",
            "anime_charge_aura_2d", "ink_slash_2d", "ink_splash_impact_2d", "ink_dragon_trail_2d", "fireball_2d_pixel"
        };

        private static readonly string[] W10SourceIds =
        {
            "real_explosion_impact_3d", "smoke_plume_area_3d", "muzzle_flash_impact_3d", "holo_barrier_shield_3d", "holo_scan_area_3d",
            "glitch_blink_transform_3d", "blood_ritual_spawn_3d", "soul_drain_beam_3d", "demon_eruption_impact_3d", "prismatic_shield_3d_holo"
        };

        // Pair order is deliberate: every W16 new sample is immediately followed by the old-content style variant.
        private static readonly string[] W16SourceIds =
        {
            "poly_burst_impact_3d", "boulder_projectile_3d_lowpoly",
            "gem_lance_projectile_3d", "crystal_shield_3d_crystal",
            "candy_pop_impact_2d", "healing_bloom_aura_2d_candy",
            "nebula_orb_projectile_3d", "summoning_portal_2d_cosmic",
            "steam_vent_burst_impact_3d", "volt_shield_3d_steampunk",
            "phantom_wail_area_2d", "spectral_trail_3d_ghost"
        };

        public static IEnumerable<string> CandidateRecipePaths
        {
            get
            {
                return W9SourceIds.Select(value => RecipeRoot + "/W9/" + value + "_next_candidate.default.json")
                    .Concat(W10SourceIds.Select(value => RecipeRoot + "/W10/" + value + "_next_candidate.default.json"))
                    .Concat(W16SourceIds.Select(value => RecipeRoot + "/W16/" + value + "_next_candidate.default.json"));
            }
        }

        [MenuItem("Tools/VFX Composer/Style/Build W9 W10 W16 Next Candidates and Open W9")]
        public static void BuildAndOpenMenu()
        {
            BuildAllForBatch();
            EditorSceneManager.OpenScene(W9PreviewScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Tools/VFX Composer/Style/Build W9 W10 W16 Next Candidates (Batch Safe)")]
        public static void BuildAllForBatch()
        {
            var results = BuildCandidateEntries();
            var failed = results.Where(value => !value.Succeeded).ToArray();
            if (failed.Length > 0) throw new InvalidOperationException(string.Join(" | ", failed.Select(value => value.EffectId + ": " + Describe(value.Report)).ToArray()));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var specs = LoadFrozenSpecs();
            BuildPreviewIfRequired(StyleSpecialCandidateGroup.W9Style2D, specs, results);
            BuildPreviewIfRequired(StyleSpecialCandidateGroup.W10Style3D, specs, results);
            BuildPreviewIfRequired(StyleSpecialCandidateGroup.W16StylePack2, specs, results);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static StyleSpecialNextCandidateBuildResult[] BuildCandidateEntries()
        {
            var specs = LoadFrozenSpecs();
            var results = new List<StyleSpecialNextCandidateBuildResult>();
            for (var index = 0; index < specs.Length; index++) results.Add(BuildCandidateEntry(specs[index]));
            return results.ToArray();
        }

        public static StyleSpecialNextCandidateSpec[] LoadFrozenSpecs()
        {
            var reports = new List<ValidationReport>();
            var specs = new List<StyleSpecialNextCandidateSpec>();
            foreach (var path in CandidateRecipePaths)
            {
                var report = new ValidationReport();
                reports.Add(report);
                specs.Add(LoadSpec(path, report));
            }
            var failures = reports.Where(value => value.HasErrors).ToArray();
            if (failures.Length > 0) throw new InvalidOperationException(string.Join(" | ", failures.Select(Describe).ToArray()));
            ValidateFrozenSet(specs.ToArray());
            return specs.ToArray();
        }

        public static string PreviewScenePath(StyleSpecialCandidateGroup group)
        {
            if (group == StyleSpecialCandidateGroup.W9Style2D) return W9PreviewScenePath;
            if (group == StyleSpecialCandidateGroup.W10Style3D) return W10PreviewScenePath;
            return W16PreviewScenePath;
        }

        public static int GridColumns(StyleSpecialCandidateGroup group)
        {
            return group == StyleSpecialCandidateGroup.W16StylePack2 ? 4 : 5;
        }

        public static int GridRows(StyleSpecialCandidateGroup group)
        {
            return group == StyleSpecialCandidateGroup.W16StylePack2 ? 3 : 2;
        }

        public static Rect FullCellViewport(StyleSpecialCandidateGroup group, int index)
        {
            var columns = GridColumns(group);
            var rows = GridRows(group);
            var column = index % columns;
            var row = index / columns;
            const float left = .012f;
            const float right = .012f;
            const float bottom = .025f;
            const float top = .072f;
            const float xGap = .006f;
            const float yGap = .009f;
            var width = (1f - left - right - xGap * (columns - 1)) / columns;
            var height = (1f - bottom - top - yGap * (rows - 1)) / rows;
            var x = left + column * (width + xGap);
            var y = 1f - top - (row + 1) * height - row * yGap;
            return new Rect(x, y, width, height);
        }

        public static Rect EffectViewport(StyleSpecialCandidateGroup group, int index)
        {
            var full = FullCellViewport(group, index);
            var labelHeight = full.height * .22f;
            return new Rect(full.x + full.width * .025f, full.y + labelHeight + full.height * .018f, full.width * .95f, full.height - labelHeight - full.height * .042f);
        }

        public static Rect LabelViewport(StyleSpecialCandidateGroup group, int index)
        {
            var full = FullCellViewport(group, index);
            return new Rect(full.x, full.y, full.width, full.height * .2f);
        }

        public static string MaterialPathFor(string token)
        {
            return VfxStyleSharedLibrary.MaterialPath(token);
        }

        public static string MeshPathFor(string token)
        {
            if (token == "Quad") return VfxStyleSharedLibrary.QuadPath;
            if (token == "Ring") return VfxStyleSharedLibrary.RingPath;
            if (token == "Ribbon") return VfxStyleSharedLibrary.RibbonPath;
            if (token == "Burst") return VfxStyleSharedLibrary.BurstPath;
            if (token == "Cone") return VfxStyleSharedLibrary.ConePath;
            if (token == "Shard") return VfxStyleSharedLibrary.ShardPath;
            if (token == "FacetA") return VfxStyleSharedLibrary.FacetPaths[0];
            if (token == "FacetB") return VfxStyleSharedLibrary.FacetPaths[1];
            if (token == "FacetC") return VfxStyleSharedLibrary.FacetPaths[2];
            if (token == "GearA") return VfxStyleSharedLibrary.GearPaths[0];
            if (token == "GearB") return VfxStyleSharedLibrary.GearPaths[1];
            if (token == "GearC") return VfxStyleSharedLibrary.GearPaths[2];
            if (token == "Line") return string.Empty;
            throw new InvalidOperationException("Unknown StyleSpecial next-candidate mesh token: " + token);
        }

        private static StyleSpecialNextCandidateBuildResult BuildCandidateEntry(StyleSpecialNextCandidateSpec spec)
        {
            var result = new StyleSpecialNextCandidateBuildResult { EffectId = spec.Id, RecipePath = spec.RecipePath };
            var materialPath = MaterialPathFor(spec.StyleToken);
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                result.Report.Add("ESSN100", ValidationSeverity.Error, "/dependencies/material", "Missing pre-existing shared style Material: " + materialPath);
                return result;
            }
            for (var index = 0; index < spec.MeshTokens.Length; index++)
            {
                var meshPath = MeshPathFor(spec.MeshTokens[index]);
                if (meshPath.Length > 0 && AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) == null)
                {
                    result.Report.Add("ESSN101", ValidationSeverity.Error, "/dependencies/meshes/" + index, "Missing pre-existing shared Mesh: " + meshPath);
                    return result;
                }
            }

            var recipeHash = HashFile(Absolute(spec.RecipePath));
            var dependencySignature = DependencySignature(spec);
            var buildHash = Hash(recipeHash + "|" + CompilerVersion + "|" + RuntimeImplementationSignature + "|" + dependencySignature + "|" + Application.unityVersion);
            var outputFolder = "Assets/VFX/Generated/" + spec.Id;
            var prefabPath = outputFolder + "/VFX_" + spec.Id + ".prefab";
            result.PrefabPath = prefabPath;
            result.RecipeHash = recipeHash;
            result.BuildHash = buildHash;
            if (string.Equals(ReadManifestBuildHash(spec.Id), buildHash, StringComparison.Ordinal) && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                result.Succeeded = true;
                result.Unchanged = true;
                return result;
            }

            ValidationGalleryCompiler.EnsureFolder(outputFolder);
            var root = new GameObject("VFX_" + spec.Id);
            try
            {
                BuildRuntimePrefab(root, spec, material);
                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null) throw new InvalidOperationException("Could not save next-candidate Prefab: " + prefabPath);
            }
            catch (Exception exception)
            {
                result.Report.Add("ESSN102", ValidationSeverity.Error, "/build", exception.Message);
                return result;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            var audit = VfxProductionRules.EnforceAndWriteManifest(spec.Id, spec.Archetype, 1, 1, recipeHash, buildHash, CompilerVersion, prefabPath, outputFolder, spec.Duration, spec.RecipePath);
            result.Report.AddRange(audit.Report);
            result.Succeeded = !result.Report.HasErrors;
            return result;
        }

        private static void BuildRuntimePrefab(GameObject root, StyleSpecialNextCandidateSpec spec, Material material)
        {
            var renderers = new List<Renderer>();
            var carriers = new List<Transform>();
            LineRenderer semanticLine = null;
            for (var index = 0; index < spec.MeshTokens.Length; index++)
            {
                var token = spec.MeshTokens[index];
                var carrier = new GameObject("SemanticCarrier_" + (index + 1).ToString("00", CultureInfo.InvariantCulture) + "_" + token);
                carrier.transform.SetParent(root.transform, false);
                carrier.transform.localPosition = CarrierPosition(spec, index);
                carrier.transform.localRotation = CarrierRotation(spec, index);
                if (token == "Line")
                {
                    semanticLine = carrier.AddComponent<LineRenderer>();
                    semanticLine.useWorldSpace = false;
                    semanticLine.sharedMaterial = material;
                    semanticLine.positionCount = 9;
                    semanticLine.startWidth = .055f;
                    semanticLine.endWidth = .025f;
                    semanticLine.numCapVertices = 2;
                    semanticLine.numCornerVertices = 2;
                    semanticLine.enabled = false;
                    renderers.Add(semanticLine);
                }
                else
                {
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPathFor(token));
                    carrier.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = carrier.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.sortingOrder = 20 + index;
                    renderer.enabled = false;
                    SetNormalizedCarrierSize(carrier.transform, mesh, CarrierTargetSize(spec, index));
                    renderers.Add(renderer);
                }
                carriers.Add(carrier.transform);
            }

            var entry = root.AddComponent<StyleSpecialNextCandidateRuntimeEntry>();
            var serialized = new SerializedObject(entry);
            serialized.FindProperty("effectId").stringValue = spec.Id;
            serialized.FindProperty("group").enumValueIndex = (int)spec.Group;
            serialized.FindProperty("motionProfile").enumValueIndex = (int)spec.MotionProfile;
            serialized.FindProperty("styleToken").stringValue = spec.StyleToken;
            serialized.FindProperty("semanticCode").stringValue = spec.SemanticCode;
            serialized.FindProperty("pairFamily").stringValue = spec.PairFamily;
            serialized.FindProperty("pairRole").stringValue = spec.PairRole;
            serialized.FindProperty("sourceBaseId").stringValue = spec.SourceBaseId;
            serialized.FindProperty("visualSignature").stringValue = VisualSignature(spec, material);
            serialized.FindProperty("duration").floatValue = spec.Duration;
            serialized.FindProperty("releaseNormalized").floatValue = spec.ReleaseNormalized;
            serialized.FindProperty("sustainEndNormalized").floatValue = spec.SustainEndNormalized;
            serialized.FindProperty("sustained").boolValue = spec.Sustained;
            serialized.FindProperty("seed").longValue = spec.Seed;
            serialized.FindProperty("primary").colorValue = spec.Primary;
            serialized.FindProperty("secondary").colorValue = spec.Secondary;
            serialized.FindProperty("accent").colorValue = spec.Accent;
            serialized.FindProperty("baseIntensity").floatValue = spec.Intensity;
            serialized.FindProperty("declaredLocalBounds").boundsValue = StyleSpecialNextCandidateRuntimeEntry.UniformLocalEnvelope;
            SetObjectArray(serialized.FindProperty("visualRenderers"), renderers.Cast<UnityEngine.Object>().ToArray());
            SetObjectArray(serialized.FindProperty("animatedCarriers"), carriers.Cast<UnityEngine.Object>().ToArray());
            serialized.FindProperty("semanticLine").objectReferenceValue = semanticLine;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            entry.ResetForPool();
        }

        private static void BuildPreviewIfRequired(StyleSpecialCandidateGroup group, StyleSpecialNextCandidateSpec[] allSpecs, StyleSpecialNextCandidateBuildResult[] allResults)
        {
            var specs = allSpecs.Where(value => value.Group == group).ToArray();
            var ids = new HashSet<string>(specs.Select(value => value.Id), StringComparer.Ordinal);
            var signature = Hash(CompilerVersion + "|" + RuntimeImplementationSignature + "|" + string.Join("|", allResults.Where(value => ids.Contains(value.EffectId)).Select(value => value.BuildHash).ToArray()));
            if (!PreviewIsCurrent(group, signature, specs.Length)) BuildPreviewScene(group, specs, signature);
        }

        private static void BuildPreviewScene(StyleSpecialCandidateGroup group, StyleSpecialNextCandidateSpec[] specs, string candidateSignature)
        {
            var expected = group == StyleSpecialCandidateGroup.W16StylePack2 ? 12 : 10;
            if (specs.Length != expected) throw new InvalidOperationException(group + " next-candidate Preview requires exactly " + expected + " Runtime Entries.");
            ValidationGalleryCompiler.EnsureFolder("Assets/VFX/Preview");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateBackgroundCamera(scene);
            var canvas = CreateOverlayCanvas(scene);
            CreateHeader(canvas.transform, group);
            var runtimeEntries = new List<StyleSpecialNextCandidateRuntimeEntry>();
            for (var index = 0; index < specs.Length; index++)
            {
                var spec = specs[index];
                var cellObject = new GameObject("Cell_" + (index + 1).ToString("00", CultureInfo.InvariantCulture) + "_" + spec.Id);
                SceneManager.MoveGameObjectToScene(cellObject, scene);
                var cell = cellObject.AddComponent<StyleSpecialNextCandidateCell>();
                var layer = FirstIsolatedLayer + index;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/" + spec.Id + "/VFX_" + spec.Id + ".prefab");
                if (prefab == null) throw new InvalidOperationException("Missing next-candidate Prefab for Preview: " + spec.Id);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = "RuntimeEntry_" + spec.Id;
                instance.transform.SetParent(cellObject.transform, false);
                instance.transform.localScale = Vector3.one * PreviewEntryScale;
                if (group == StyleSpecialCandidateGroup.W10Style3D || (group == StyleSpecialCandidateGroup.W16StylePack2 && spec.Dimension == "3d")) instance.transform.localRotation = Quaternion.Euler(17f, -18f, 0f);
                ApplyLayerRecursively(instance, layer);
                var entry = instance.GetComponent<StyleSpecialNextCandidateRuntimeEntry>();
                runtimeEntries.Add(entry);

                var cameraObject = new GameObject("EffectCamera_" + (index + 1).ToString("00", CultureInfo.InvariantCulture));
                cameraObject.transform.SetParent(cellObject.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = CellBackground(spec.StyleToken);
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.depth = 10 + index;
                camera.rect = EffectViewport(group, index);
                camera.cullingMask = 1 << layer;
                camera.orthographic = true;
                camera.orthographicSize = .69f;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 12f;
                camera.transform.position = new Vector3(0f, .02f, -4f);

                var serializedCell = new SerializedObject(cell);
                serializedCell.FindProperty("cellIndex").intValue = index + 1;
                serializedCell.FindProperty("group").enumValueIndex = (int)group;
                serializedCell.FindProperty("label").stringValue = spec.Label;
                serializedCell.FindProperty("pairFamily").stringValue = spec.PairFamily;
                serializedCell.FindProperty("pairRole").stringValue = spec.PairRole;
                serializedCell.FindProperty("fullViewport").rectValue = FullCellViewport(group, index);
                serializedCell.FindProperty("effectViewport").rectValue = EffectViewport(group, index);
                serializedCell.FindProperty("labelViewport").rectValue = LabelViewport(group, index);
                serializedCell.FindProperty("isolatedLayer").intValue = layer;
                serializedCell.FindProperty("effectCamera").objectReferenceValue = camera;
                serializedCell.FindProperty("runtimeEntry").objectReferenceValue = entry;
                serializedCell.ApplyModifiedPropertiesWithoutUndo();
                CreateLabelBand(canvas.transform, group, index, spec);
            }

            var driverObject = new GameObject("StyleSpecialNextCandidatePreviewDriver_" + group);
            SceneManager.MoveGameObjectToScene(driverObject, scene);
            var driver = driverObject.AddComponent<StyleSpecialNextCandidatePreviewDriver>();
            var serializedDriver = new SerializedObject(driver);
            serializedDriver.FindProperty("group").enumValueIndex = (int)group;
            SetObjectArray(serializedDriver.FindProperty("runtimeEntries"), runtimeEntries.Cast<UnityEngine.Object>().ToArray());
            serializedDriver.FindProperty("playDuration").floatValue = group == StyleSpecialCandidateGroup.W10Style3D ? 2.55f : 2.35f;
            serializedDriver.FindProperty("cleanGap").floatValue = .28f;
            serializedDriver.FindProperty("compilerVersion").stringValue = CompilerVersion;
            serializedDriver.FindProperty("candidateSignature").stringValue = candidateSignature;
            serializedDriver.ApplyModifiedPropertiesWithoutUndo();
            var status = new GameObject(CandidateStatusRootName);
            SceneManager.MoveGameObjectToScene(status, scene);
            EditorSceneManager.SaveScene(scene, PreviewScenePath(group));
        }

        private static bool PreviewIsCurrent(StyleSpecialCandidateGroup group, string signature, int expectedCount)
        {
            var path = PreviewScenePath(group);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) return false;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            try
            {
                var driver = scene.GetRootGameObjects().Select(value => value.GetComponent<StyleSpecialNextCandidatePreviewDriver>()).FirstOrDefault(value => value != null);
                return driver != null && driver.Group == group && driver.CompilerVersion == CompilerVersion && driver.CandidateSignature == signature && driver.ConfiguredEntryCount == expectedCount;
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static StyleSpecialNextCandidateSpec LoadSpec(string recipePath, ValidationReport report)
        {
            var spec = new StyleSpecialNextCandidateSpec { RecipePath = recipePath, MeshTokens = new string[0] };
            if (!File.Exists(Absolute(recipePath)))
            {
                report.Add("ESSN001", ValidationSeverity.Error, recipePath, "Frozen next-candidate descriptor is missing.");
                return spec;
            }
            JObject json;
            try { json = JObject.Parse(File.ReadAllText(Absolute(recipePath))); }
            catch (Exception exception)
            {
                report.Add("ESSN002", ValidationSeverity.Error, recipePath, exception.Message);
                return spec;
            }
            if ((string)json["schema"] != DescriptorSchema) report.Add("ESSN003", ValidationSeverity.Error, "/schema", "Expected " + DescriptorSchema + ".");
            spec.Id = (string)json["id"] ?? string.Empty;
            spec.SourceId = (string)json["sourceId"] ?? string.Empty;
            spec.Label = (string)json["label"] ?? string.Empty;
            spec.Archetype = (string)json["archetype"] ?? string.Empty;
            spec.Dimension = (string)json["dimension"] ?? string.Empty;
            spec.StyleToken = (string)json["style"] ?? string.Empty;
            spec.SemanticCode = (string)json["semantic"] ?? string.Empty;
            spec.PairFamily = (string)json["pairFamily"] ?? string.Empty;
            spec.PairRole = (string)json["pairRole"] ?? string.Empty;
            spec.SourceBaseId = (string)json["sourceBaseId"] ?? string.Empty;
            spec.Duration = (float?)json["duration"] ?? 0f;
            spec.ReleaseNormalized = (float?)json["releaseNormalized"] ?? -1f;
            spec.SustainEndNormalized = (float?)json["sustainEndNormalized"] ?? -1f;
            spec.Intensity = (float?)json["intensity"] ?? 0f;
            spec.Sustained = (bool?)json["sustained"] ?? false;
            spec.Seed = (uint?)json["seed"] ?? 0u;
            spec.MeshTokens = json["meshes"] == null ? new string[0] : json["meshes"].Values<string>().ToArray();
            if (!TryParseGroup((string)json["group"], out spec.Group)) report.Add("ESSN004", ValidationSeverity.Error, "/group", "Unknown candidate group.");
            if (!TryParseProfile((string)json["profile"], out spec.MotionProfile)) report.Add("ESSN005", ValidationSeverity.Error, "/profile", "Unknown motion profile.");
            var palette = json["palette"] as JObject;
            if (palette == null || !TryColor((string)palette["primary"], out spec.Primary) || !TryColor((string)palette["secondary"], out spec.Secondary) || !TryColor((string)palette["accent"], out spec.Accent)) report.Add("ESSN006", ValidationSeverity.Error, "/palette", "Palette must contain three #RRGGBB colors.");
            if (!spec.Id.EndsWith("_next_candidate", StringComparison.Ordinal) || spec.Id != spec.SourceId + "_next_candidate") report.Add("ESSN007", ValidationSeverity.Error, "/id", "Candidate id must be sourceId + _next_candidate.");
            if (spec.Label.Length < 3 || spec.SemanticCode.Length < 8) report.Add("ESSN008", ValidationSeverity.Error, "/semantic", "Label and observable semantic code are required.");
            if (spec.Dimension != "2d" && spec.Dimension != "3d") report.Add("ESSN009", ValidationSeverity.Error, "/dimension", "Dimension must be 2d or 3d.");
            if (spec.Duration < .12f || spec.Duration > 3f || spec.ReleaseNormalized < 0f || spec.ReleaseNormalized > .8f || spec.SustainEndNormalized <= spec.ReleaseNormalized || spec.SustainEndNormalized > .95f) report.Add("ESSN010", ValidationSeverity.Error, "/timing", "Duration/release/sustain bounds are invalid.");
            if (spec.Intensity < .1f || spec.Intensity > 2f) report.Add("ESSN011", ValidationSeverity.Error, "/intensity", "Intensity must be in [0.1,2].");
            if (spec.MeshTokens.Length < 3 || spec.MeshTokens.Length > 6) report.Add("ESSN012", ValidationSeverity.Error, "/meshes", "Three through six bounded visual carriers are required.");
            for (var index = 0; index < spec.MeshTokens.Length; index++)
            {
                try { MeshPathFor(spec.MeshTokens[index]); }
                catch (Exception exception) { report.Add("ESSN013", ValidationSeverity.Error, "/meshes/" + index, exception.Message); }
            }
            return spec;
        }

        private static void ValidateFrozenSet(StyleSpecialNextCandidateSpec[] specs)
        {
            if (specs.Length != 32) throw new InvalidOperationException("StyleSpecial next-candidate descriptor set must contain exactly 32 entries.");
            if (specs.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != 32) throw new InvalidOperationException("StyleSpecial next-candidate ids must be unique.");
            AssertGroup(specs, StyleSpecialCandidateGroup.W9Style2D, W9SourceIds, new[] { "pixel", "cartoon", "inkwash" });
            AssertGroup(specs, StyleSpecialCandidateGroup.W10Style3D, W10SourceIds, new[] { "semireal", "holo", "dark" });
            AssertGroup(specs, StyleSpecialCandidateGroup.W16StylePack2, W16SourceIds, new[] { "lowpoly", "crystal", "candy", "cosmic", "steampunk", "ghost" });
            var w16 = specs.Where(value => value.Group == StyleSpecialCandidateGroup.W16StylePack2).ToArray();
            var families = w16.GroupBy(value => value.PairFamily, StringComparer.Ordinal).ToArray();
            if (families.Length != 6 || families.Any(value => string.IsNullOrEmpty(value.Key) || value.Count() != 2 || value.Select(item => item.PairRole).OrderBy(item => item, StringComparer.Ordinal).SequenceEqual(new[] { "new", "variant" }) == false)) throw new InvalidOperationException("W16 requires six exact new/variant pair contracts.");
            if (w16.Any(value => string.IsNullOrEmpty(value.SourceBaseId))) throw new InvalidOperationException("Every W16 comparison side must identify its composed source base.");
            if (specs.Where(value => value.Group != StyleSpecialCandidateGroup.W16StylePack2).Any(value => !string.IsNullOrEmpty(value.PairFamily) || !string.IsNullOrEmpty(value.PairRole) || !string.IsNullOrEmpty(value.SourceBaseId))) throw new InvalidOperationException("Only W16 may declare pair contracts.");
        }

        private static void AssertGroup(StyleSpecialNextCandidateSpec[] specs, StyleSpecialCandidateGroup group, string[] expectedSources, string[] styles)
        {
            var values = specs.Where(value => value.Group == group).ToArray();
            CollectionAssertEquivalent(expectedSources, values.Select(value => value.SourceId).ToArray(), group + " sources");
            if (values.Any(value => !styles.Contains(value.StyleToken))) throw new InvalidOperationException(group + " contains a style outside its frozen set.");
        }

        private static void CollectionAssertEquivalent(string[] expected, string[] actual, string label)
        {
            var left = expected.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var right = actual.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (!left.SequenceEqual(right)) throw new InvalidOperationException(label + " do not match the frozen plan.");
        }

        private static Vector3 CarrierPosition(StyleSpecialNextCandidateSpec spec, int index)
        {
            if (spec.MotionProfile == StyleSpecialMotionProfile.SustainedPlume) return new Vector3((index - 2f) * .08f, -.32f + index * .11f, index * .015f);
            if (spec.MotionProfile == StyleSpecialMotionProfile.MuzzleFlash) return new Vector3(index * -.1f, 0f, index * .015f);
            if (spec.MotionProfile == StyleSpecialMotionProfile.SoulDrain) return Vector3.zero;
            if (spec.MotionProfile == StyleSpecialMotionProfile.DemonEruption) return new Vector3((index - 2f) * .08f, -.2f + index * .035f, index * .015f);
            if (spec.MotionProfile == StyleSpecialMotionProfile.LanceFlight) return new Vector3(-index * .09f, (index & 1) == 0 ? .03f : -.03f, index * .015f);
            if (spec.MotionProfile == StyleSpecialMotionProfile.CandyBounce || spec.MotionProfile == StyleSpecialMotionProfile.FacetBurst)
            {
                var angle = index * 2.399963f;
                return new Vector3(Mathf.Cos(angle) * .16f, Mathf.Sin(angle) * .13f, index * .015f);
            }
            if (spec.MotionProfile == StyleSpecialMotionProfile.GhostPulse) return new Vector3((index - 2f) * .14f, ((index + 1) % 3 - 1) * .09f, index * .015f);
            return new Vector3((index % 3 - 1) * .09f, (index / 3) * .065f - .035f, index * .015f);
        }

        private static Quaternion CarrierRotation(StyleSpecialNextCandidateSpec spec, int index)
        {
            var z = index * 37f + (spec.MotionProfile == StyleSpecialMotionProfile.InkBleed ? -24f : 0f);
            return Quaternion.Euler(0f, 0f, z);
        }

        private static Vector2 CarrierTargetSize(StyleSpecialNextCandidateSpec spec, int index)
        {
            var token = spec.MeshTokens[index];
            if (spec.MotionProfile == StyleSpecialMotionProfile.MuzzleFlash) return new Vector2(.72f - index * .13f, .42f - index * .06f);
            if (spec.MotionProfile == StyleSpecialMotionProfile.SustainedPlume) return new Vector2(.34f - index * .025f, .32f + index * .025f);
            if (spec.MotionProfile == StyleSpecialMotionProfile.LanceFlight) return token == "Ribbon" ? new Vector2(.52f, .12f) : new Vector2(.28f, .18f);
            if (spec.MotionProfile == StyleSpecialMotionProfile.HoloBarrier) return index == 0 ? new Vector2(.8f, .62f) : new Vector2(.56f - index * .06f, .44f - index * .04f);
            if (spec.MotionProfile == StyleSpecialMotionProfile.RitualSummon) return index < 2 ? new Vector2(.72f - index * .16f, .52f - index * .12f) : new Vector2(.25f, .25f);
            if (spec.MotionProfile == StyleSpecialMotionProfile.InkBleed && token == "Ribbon") return new Vector2(.68f - index * .08f, .25f - index * .02f);
            return new Vector2(Mathf.Max(.18f, .42f - index * .035f), Mathf.Max(.16f, .38f - index * .03f));
        }

        private static void SetNormalizedCarrierSize(Transform carrier, Mesh mesh, Vector2 targetSize)
        {
            var size = mesh.bounds.size;
            carrier.localScale = new Vector3(targetSize.x / Mathf.Max(.0001f, size.x), targetSize.y / Mathf.Max(.0001f, size.y), Mathf.Min(targetSize.x, targetSize.y) / Mathf.Max(.0001f, size.z));
        }

        private static string VisualSignature(StyleSpecialNextCandidateSpec spec, Material material)
        {
            var shader = material.shader == null ? "null" : material.shader.name;
            return shader + "|" + MaterialFloat(material, "_StyleMode") + "|" + MaterialFloat(material, "_Outline") + "|" + MaterialFloat(material, "_NoiseScale") + "|" + spec.MotionProfile + "|" + spec.SemanticCode + "|" + string.Join(",", spec.MeshTokens);
        }

        private static string MaterialFloat(Material material, string property)
        {
            return material.HasProperty(property) ? material.GetFloat(property).ToString("F4", CultureInfo.InvariantCulture) : "na";
        }

        private static string DependencySignature(StyleSpecialNextCandidateSpec spec)
        {
            var paths = new List<string> { MaterialPathFor(spec.StyleToken) };
            paths.AddRange(spec.MeshTokens.Where(value => value != "Line").Select(MeshPathFor));
            return string.Join("|", paths.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).Select(value => value + "=" + HashFile(Absolute(value))).ToArray());
        }

        private static void CreateBackgroundCamera(Scene scene)
        {
            var cameraObject = new GameObject("ReviewBackgroundCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.009f, .013f, .022f, 1f);
            camera.cullingMask = 0;
            camera.depth = -20f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
        }

        private static Canvas CreateOverlayCanvas(Scene scene)
        {
            var objectValue = new GameObject("ReviewOverlayCanvas");
            SceneManager.MoveGameObjectToScene(objectValue, scene);
            var canvas = objectValue.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            objectValue.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            objectValue.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
            objectValue.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void CreateHeader(Transform canvas, StyleSpecialCandidateGroup group)
        {
            var panel = CreatePanel(canvas, "CandidateStatusHeader", new Rect(.012f, .944f, .976f, .046f), new Color(.02f, .035f, .06f, .98f));
            var text = CreatePanelText(panel.transform, "HeaderText");
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 19;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(.66f, .86f, 1f, 1f);
            text.text = group + "  ·  NEXT CANDIDATE  ·  REAL MATERIAL / OBSERVABLE PHASES / HARD CELL CLIP  ·  VISUAL PENDING";
        }

        private static void CreateLabelBand(Transform canvas, StyleSpecialCandidateGroup group, int index, StyleSpecialNextCandidateSpec spec)
        {
            var panel = CreatePanel(canvas, "CellLabelSafeBand_" + (index + 1).ToString("00", CultureInfo.InvariantCulture), LabelViewport(group, index), new Color(.018f, .029f, .05f, .98f));
            var text = CreatePanelText(panel.transform, "LabelText");
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = group == StyleSpecialCandidateGroup.W16StylePack2 ? 12 : 13;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(.78f, .86f, .96f, 1f);
            text.text = (index + 1).ToString("00", CultureInfo.InvariantCulture) + "  " + spec.Label.ToUpperInvariant() + (string.IsNullOrEmpty(spec.PairRole) ? string.Empty : "  [" + spec.PairRole.ToUpperInvariant() + "]");
        }

        private static GameObject CreatePanel(Transform parent, string name, Rect normalizedRect, Color color)
        {
            var objectValue = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            objectValue.transform.SetParent(parent, false);
            var rect = objectValue.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(normalizedRect.xMin, normalizedRect.yMin);
            rect.anchorMax = new Vector2(normalizedRect.xMax, normalizedRect.yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            objectValue.GetComponent<Image>().color = color;
            return objectValue;
        }

        private static Text CreatePanelText(Transform parent, string name)
        {
            var objectValue = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            objectValue.transform.SetParent(parent, false);
            var rect = objectValue.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return objectValue.GetComponent<Text>();
        }

        private static Color CellBackground(string token)
        {
            if (token == "inkwash") return new Color(.14f, .135f, .125f, 1f);
            if (token == "dark") return new Color(.026f, .012f, .034f, 1f);
            if (token == "cosmic") return new Color(.016f, .012f, .052f, 1f);
            if (token == "candy") return new Color(.08f, .045f, .075f, 1f);
            return new Color(.014f, .021f, .035f, 1f);
        }

        private static void ApplyLayerRecursively(GameObject root, int layer)
        {
            foreach (var transformValue in root.GetComponentsInChildren<Transform>(true)) transformValue.gameObject.layer = layer;
        }

        private static void SetObjectArray(SerializedProperty property, UnityEngine.Object[] values)
        {
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++) property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }

        private static bool TryParseGroup(string value, out StyleSpecialCandidateGroup group)
        {
            if (value == "w9") { group = StyleSpecialCandidateGroup.W9Style2D; return true; }
            if (value == "w10") { group = StyleSpecialCandidateGroup.W10Style3D; return true; }
            if (value == "w16") { group = StyleSpecialCandidateGroup.W16StylePack2; return true; }
            group = StyleSpecialCandidateGroup.W9Style2D;
            return false;
        }

        private static bool TryParseProfile(string value, out StyleSpecialMotionProfile profile)
        {
            return Enum.TryParse(value, false, out profile);
        }

        private static bool TryColor(string value, out Color color)
        {
            return ColorUtility.TryParseHtmlString(value ?? string.Empty, out color);
        }

        private static string ReadManifestBuildHash(string effectId)
        {
            var path = VfxProjectRules.ManifestAbsolutePath(effectId);
            if (!File.Exists(path)) return string.Empty;
            try { return (string)JObject.Parse(File.ReadAllText(path))["buildHash"] ?? string.Empty; }
            catch { return string.Empty; }
        }

        private static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static string Hash(string value)
        {
            using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static string Absolute(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string Describe(ValidationReport report)
        {
            return string.Join(" | ", report.Entries.Select(value => value.Code + " " + value.Path + " " + value.Message).ToArray());
        }
    }
}
