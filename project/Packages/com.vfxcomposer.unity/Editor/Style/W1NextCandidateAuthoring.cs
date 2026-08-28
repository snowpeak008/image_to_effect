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
    public sealed class W1NextCandidateBuildResult
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

    public sealed class W1NextCandidateSpec
    {
        public string RecipePath;
        public string Id;
        public string Label;
        public W1NextCandidateKind Kind;
        public string Archetype;
        public string Dimension;
        public string StyleToken;
        public W1StyleTimingProfile TimingProfile;
        public Color Primary;
        public Color Secondary;
        public Color Accent;
        public float Duration;
        public uint Seed;
    }

    /// <summary>
    /// Dedicated W1 next-candidate compiler. It consumes only StyleGallery descriptors and never
    /// invokes the capability or element-family batch builders.
    /// </summary>
    public static class W1NextCandidateAuthoring
    {
        public const string CompilerVersion = "w1-style-next-candidate-1";
        public const string CandidateStatusRootName = "W1_NEXT_CANDIDATE_VISUAL_PENDING";
        public const string RecipeRoot = "Assets/VFX/Recipes/StyleGallery";
        public const string PreviewScenePath = W1NextCandidatePreviewDriver.ScenePath;
        public const int TokenCellCount = 8;
        public const int CapabilityCellCount = 3;
        public const int GridColumns = 4;
        public const int GridRows = 3;
        public const int FirstIsolatedLayer = 8;
        public const float PreviewEntryScale = 1f;
        private const string DescriptorSchema = "w1-style-next-candidate/v1";
        private const string RuntimeImplementationSignature = "fixed-envelope-v3|token-carriers-v2|fan-wave-batch-v2|charge-occlude-line-v2|telegraph-nova-burst-v6|viewport-clip-v1";

        private static readonly string[] RecipeFileNames =
        {
            "style_orb_stylized_2d.default.json",
            "style_orb_cartoon_2d.default.json",
            "style_orb_pixel_2d.default.json",
            "style_orb_inkwash_2d.default.json",
            "style_orb_semireal_3d.default.json",
            "style_orb_holo_3d.default.json",
            "style_orb_dark_3d.default.json",
            "style_orb_neon_2d.default.json",
            "fan_wave_cartoon_showcase_2d.default.json",
            "charge_occlude_holo_showcase_3d.default.json",
            "telegraph_nova_holy_showcase_3d.default.json"
        };

        public static IEnumerable<string> CandidateRecipePaths
        {
            get { return RecipeFileNames.Select(value => RecipeRoot + "/" + value); }
        }

        [MenuItem("Tools/VFX Composer/Style/Build W1 Next Candidate and Preview")]
        public static void BuildAndOpenMenu()
        {
            BuildAllForBatch();
            EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
            Debug.Log("W1 next candidate is built as VISUAL_PENDING. No user visual conclusion was written.");
        }

        [MenuItem("Tools/VFX Composer/Style/Build W1 Next Candidate (Batch Safe)")]
        public static void BuildAllForBatch()
        {
            var results = BuildCandidateEntries();
            var failures = results.Where(value => !value.Succeeded).ToArray();
            if (failures.Length > 0)
            {
                throw new InvalidOperationException(string.Join(" | ", failures.SelectMany(value => value.Report.Entries.Select(entry => value.EffectId + ": " + entry.Code + " " + entry.Path + " " + entry.Message)).ToArray()));
            }
            var signature = Hash(CompilerVersion + "|" + string.Join("|", results.Select(value => value.EffectId + ":" + value.BuildHash).ToArray()));
            if (!PreviewIsCurrent(signature)) BuildPreviewScene(results.Select(value => LoadSpec(value.RecipePath, value.Report)).ToArray(), signature);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static W1NextCandidateBuildResult[] BuildCandidateEntries()
        {
            // Fail before touching Assets/VFX/Generated if the frozen eleven-entry wall drifts.
            LoadFrozenSpecs();
            return CandidateRecipePaths.Select(BuildCandidateEntry).ToArray();
        }

        public static W1NextCandidateSpec[] LoadFrozenSpecs()
        {
            var specs = CandidateRecipePaths.Select(path =>
            {
                var report = new ValidationReport();
                var spec = LoadSpec(path, report);
                if (report.HasErrors) throw new InvalidOperationException(path + ": " + string.Join(" | ", report.Entries.Select(value => value.Code + " " + value.Path + " " + value.Message).ToArray()));
                return spec;
            }).ToArray();
            ValidateFrozenSet(specs);
            return specs;
        }

        public static Rect FullCellViewport(int index)
        {
            const float left = .025f;
            const float right = .025f;
            const float bottom = .035f;
            const float top = .105f;
            const float columnGap = .012f;
            const float rowGap = .018f;
            var width = (1f - left - right - columnGap * (GridColumns - 1)) / GridColumns;
            var height = (1f - bottom - top - rowGap * (GridRows - 1)) / GridRows;
            var column = index % GridColumns;
            var row = index / GridColumns;
            var x = left + column * (width + columnGap);
            var y = 1f - top - (row + 1) * height - row * rowGap;
            return new Rect(x, y, width, height);
        }

        public static Rect LabelViewport(int index)
        {
            var full = FullCellViewport(index);
            var height = Mathf.Min(.052f, full.height * .22f);
            return new Rect(full.x, full.y, full.width, height);
        }

        public static Rect EffectViewport(int index)
        {
            var full = FullCellViewport(index);
            var label = LabelViewport(index);
            const float inset = .004f;
            return new Rect(full.x + inset, label.yMax + inset, full.width - inset * 2f, full.yMax - label.yMax - inset * 2f);
        }

        private static W1NextCandidateBuildResult BuildCandidateEntry(string recipePath)
        {
            var result = new W1NextCandidateBuildResult { RecipePath = recipePath };
            var spec = LoadSpec(recipePath, result.Report);
            if (spec == null || result.Report.HasErrors) return result;
            result.EffectId = spec.Id;
            var json = File.ReadAllText(Absolute(recipePath));
            var recipeHash = RecipeCanonicalizer.ComputeSha256(json);
            result.RecipeHash = recipeHash;
            var dependencySignature = DependencySignature(spec);
            var buildHash = Hash(recipeHash + "|" + CompilerVersion + "|" + RuntimeImplementationSignature + "|" + dependencySignature + "|" + Application.unityVersion);
            result.BuildHash = buildHash;
            var outputFolder = "Assets/VFX/Generated/" + spec.Id;
            var prefabPath = outputFolder + "/VFX_" + spec.Id + ".prefab";
            result.PrefabPath = prefabPath;
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
                BuildRuntimePrefab(root, spec);
                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null) throw new InvalidOperationException("Could not save W1 next-candidate Runtime Entry: " + prefabPath);
            }
            catch (Exception exception)
            {
                result.Report.Add("EW1NC006", ValidationSeverity.Error, "/build", exception.Message);
                return result;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            var audit = VfxProductionRules.EnforceAndWriteManifest(spec.Id, spec.Archetype, 1, 1, recipeHash, buildHash, CompilerVersion, prefabPath, outputFolder, spec.Duration, recipePath);
            result.Report.AddRange(audit.Report);
            result.Succeeded = !result.Report.HasErrors;
            return result;
        }

        private static W1NextCandidateSpec LoadSpec(string recipePath, ValidationReport report)
        {
            if (!File.Exists(Absolute(recipePath)))
            {
                report.Add("EW1NC001", ValidationSeverity.Error, "/recipe", "W1 next-candidate descriptor is missing.", new JValue(recipePath));
                return null;
            }
            JObject json;
            try { json = JObject.Parse(File.ReadAllText(Absolute(recipePath))); }
            catch (Exception exception)
            {
                report.Add("EW1NC002", ValidationSeverity.Error, "/recipe", exception.Message);
                return null;
            }

            var allowed = new HashSet<string>(new[] { "schema", "recipeVersion", "revision", "id", "displayName", "entryKind", "archetype", "dimension", "styleToken", "timingProfile", "palette", "duration", "seed" }, StringComparer.Ordinal);
            foreach (var property in json.Properties()) if (!allowed.Contains(property.Name)) report.Add("EW1NC003", ValidationSeverity.Error, "/" + property.Name, "Unknown W1 next-candidate descriptor field.");
            var schema = (string)json["schema"];
            var id = (string)json["id"];
            var entryKind = (string)json["entryKind"];
            var styleToken = (string)json["styleToken"];
            var timing = (string)json["timingProfile"];
            var archetype = (string)json["archetype"];
            var dimension = (string)json["dimension"];
            var displayName = (string)json["displayName"];
            var duration = json["duration"] == null ? 0f : (float)json["duration"];
            var seed = json["seed"] == null ? 0u : (uint)json["seed"];
            if (schema != DescriptorSchema || (int?)json["recipeVersion"] != 1 || (int?)json["revision"] != 1) report.Add("EW1NC004", ValidationSeverity.Error, "/schema", "Descriptor schema/version/revision must be frozen at w1-style-next-candidate/v1, 1, 1.");
            if (string.IsNullOrEmpty(id) || id.Any(character => !(character == '_' || character >= 'a' && character <= 'z' || character >= '0' && character <= '9'))) report.Add("EW1NC004", ValidationSeverity.Error, "/id", "Effect id must be lower_snake_case.");
            if (!string.Equals(Path.GetFileName(recipePath), id + ".default.json", StringComparison.Ordinal)) report.Add("EW1NC004", ValidationSeverity.Error, "/id", "Descriptor filename must be <id>.default.json.");
            if (string.IsNullOrWhiteSpace(displayName)) report.Add("EW1NC004", ValidationSeverity.Error, "/displayName", "Display name is required.");
            if (archetype != "projectile" && archetype != "beam" && archetype != "impact") report.Add("EW1NC004", ValidationSeverity.Error, "/archetype", "W1 comparison entries use only projectile, beam, or impact archetypes.");
            if (duration < 1.8f || duration > 2.2f) report.Add("EW1NC004", ValidationSeverity.Error, "/duration", "All comparison entries must share the bounded 1.8-2.2 second playback window.");
            if (seed == 0u) report.Add("EW1NC004", ValidationSeverity.Error, "/seed", "A non-zero deterministic seed is required.");
            VfxStyleDefinition definition;
            if (!VfxStyleRegistry.TryGet(styleToken, out definition) || !InitialStyleTokens.Contains(styleToken)) report.Add("EW1NC004", ValidationSeverity.Error, "/styleToken", "Only one of the eight W1 style tokens is allowed.");
            if (dimension != "2d" && dimension != "3d") report.Add("EW1NC004", ValidationSeverity.Error, "/dimension", "Dimension must be 2d or 3d.");
            if (definition != null && (dimension == "2d" && !definition.Supports2D || dimension == "3d" && !definition.Supports3D)) report.Add("EW1NC004", ValidationSeverity.Error, "/dimension", "Style token does not support the selected dimension.");
            W1NextCandidateKind kind;
            W1StyleTimingProfile timingProfile;
            if (!TryParseKind(entryKind, out kind)) report.Add("EW1NC004", ValidationSeverity.Error, "/entryKind", "Unknown W1 next-candidate entry kind.");
            if (!TryParseTiming(timing, out timingProfile)) report.Add("EW1NC004", ValidationSeverity.Error, "/timingProfile", "Unknown W1 next-candidate timing profile.");
            var primary = Color.white;
            var secondary = Color.white;
            var accent = Color.white;
            var palette = json["palette"] as JObject;
            if (palette == null || !TryColor((string)palette["primary"], out primary) || !TryColor((string)palette["secondary"], out secondary) || !TryColor((string)palette["accent"], out accent)) report.Add("EW1NC005", ValidationSeverity.Error, "/palette", "Palette requires parseable primary, secondary, and accent colors.");
            if (report.HasErrors) return null;
            return new W1NextCandidateSpec
            {
                RecipePath = recipePath,
                Id = id,
                Label = displayName,
                Kind = kind,
                Archetype = archetype,
                Dimension = dimension,
                StyleToken = styleToken,
                TimingProfile = timingProfile,
                Primary = primary,
                Secondary = secondary,
                Accent = accent,
                Duration = duration,
                Seed = seed
            };
        }

        private static void BuildRuntimePrefab(GameObject root, W1NextCandidateSpec spec)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(VfxStyleSharedLibrary.MaterialPath(spec.StyleToken));
            if (material == null || material.shader == null) throw new InvalidOperationException("Missing registered W1 style material: " + spec.StyleToken);
            var renderers = new List<Renderer>();
            var animated = new List<Transform>();
            var styleCarriers = new List<Transform>();
            var fanCarriers = new List<Transform>();
            LineRenderer beamLine = null;
            Transform chargeGlyph = null;
            MeshRenderer telegraph = null;
            MeshRenderer nova = null;
            MeshRenderer novaMotes = null;

            if (spec.Kind == W1NextCandidateKind.StyleToken)
            {
                var meshPaths = StyleMeshes(spec.StyleToken);
                // The largest layer may rotate through every angle. These normalized sizes keep
                // its full diagonal inside the shared 1.56 x 1.08 envelope instead of merely
                // fitting the axis-aligned frame at authoring time.
                var targetSizes = new[] { new Vector2(.46f, .42f), new Vector2(.78f, .58f), new Vector2(.48f, .38f) };
                for (var index = 0; index < meshPaths.Length; index++)
                {
                    var layer = CreateMeshLayer(root.transform, "StyleCarrier_" + (index + 1).ToString("00", CultureInfo.InvariantCulture), meshPaths[index], material, targetSizes[index], index * .02f, 20 + index);
                    renderers.Add(layer.GetComponent<Renderer>());
                    animated.Add(layer);
                    styleCarriers.Add(layer);
                }
            }
            else if (spec.Kind == W1NextCandidateKind.FanWave)
            {
                for (var index = 0; index < 5; index++)
                {
                    var carrier = CreateMeshLayer(root.transform, "FanProjectile_" + (index + 1).ToString("00", CultureInfo.InvariantCulture), VfxStyleSharedLibrary.ShardPath, material, new Vector2(.12f, .18f), index * .008f, 20 + index);
                    renderers.Add(carrier.GetComponent<Renderer>());
                    animated.Add(carrier);
                    fanCarriers.Add(carrier);
                }
            }
            else if (spec.Kind == W1NextCandidateKind.ChargeOcclude)
            {
                var lineObject = new GameObject("OccludedBeamCarrier");
                lineObject.transform.SetParent(root.transform, false);
                beamLine = lineObject.AddComponent<LineRenderer>();
                beamLine.useWorldSpace = false;
                beamLine.alignment = LineAlignment.View;
                beamLine.textureMode = LineTextureMode.Tile;
                beamLine.positionCount = 9;
                beamLine.numCapVertices = 4;
                beamLine.sharedMaterial = material;
                beamLine.sortingOrder = 22;
                beamLine.enabled = false;
                for (var index = 0; index < beamLine.positionCount; index++) beamLine.SetPosition(index, Vector3.Lerp(new Vector3(-.54f, 0f, 0f), new Vector3(.27f, 0f, 0f), index / 8f));
                renderers.Add(beamLine);
                animated.Add(lineObject.transform);
                chargeGlyph = CreateMeshLayer(root.transform, "ChargeGlyphCarrier", VfxStyleSharedLibrary.RingPath, material, new Vector2(.24f, .24f), .02f, 21);
                renderers.Add(chargeGlyph.GetComponent<Renderer>());
                animated.Add(chargeGlyph);
            }
            else
            {
                var telegraphTransform = CreateMeshLayer(root.transform, "TelegraphCarrier", VfxStyleSharedLibrary.RingPath, material, new Vector2(.82f, .62f), 0f, 20);
                telegraph = telegraphTransform.GetComponent<MeshRenderer>();
                var novaTransform = CreateMeshLayer(root.transform, "NovaRingCarrier", VfxStyleSharedLibrary.RingPath, material, new Vector2(.1f, .1f), .02f, 21);
                nova = novaTransform.GetComponent<MeshRenderer>();
                var moteTransform = CreateMeshLayer(root.transform, "TwelveMoteBurstCarrier", VfxStyleSharedLibrary.BurstPath, material, new Vector2(.1f, .1f), .04f, 22);
                novaMotes = moteTransform.GetComponent<MeshRenderer>();
                renderers.Add(telegraph);
                renderers.Add(nova);
                renderers.Add(novaMotes);
                animated.Add(telegraphTransform);
                animated.Add(novaTransform);
                animated.Add(moteTransform);
            }

            for (var index = 0; index < renderers.Count; index++) renderers[index].enabled = false;
            var controller = root.AddComponent<W1NextCandidateRuntimeEntry>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("kind").enumValueIndex = (int)spec.Kind;
            serialized.FindProperty("timingProfile").enumValueIndex = (int)spec.TimingProfile;
            serialized.FindProperty("styleToken").stringValue = spec.StyleToken;
            serialized.FindProperty("visualSignature").stringValue = VisualSignature(spec, material, renderers);
            serialized.FindProperty("duration").floatValue = spec.Duration;
            serialized.FindProperty("seed").longValue = spec.Seed;
            serialized.FindProperty("primary").colorValue = spec.Primary;
            serialized.FindProperty("secondary").colorValue = spec.Secondary;
            serialized.FindProperty("accent").colorValue = spec.Accent;
            serialized.FindProperty("baseIntensity").floatValue = BaseIntensity(spec.StyleToken, spec.Kind);
            serialized.FindProperty("declaredLocalBounds").boundsValue = W1NextCandidateRuntimeEntry.UniformLocalEnvelope;
            SetObjectArray(serialized.FindProperty("visualRenderers"), renderers.Cast<UnityEngine.Object>().ToArray());
            SetObjectArray(serialized.FindProperty("animatedTransforms"), animated.Cast<UnityEngine.Object>().ToArray());
            SetObjectArray(serialized.FindProperty("styleCarriers"), styleCarriers.Cast<UnityEngine.Object>().ToArray());
            SetObjectArray(serialized.FindProperty("fanCarriers"), fanCarriers.Cast<UnityEngine.Object>().ToArray());
            serialized.FindProperty("beamLine").objectReferenceValue = beamLine;
            serialized.FindProperty("chargeGlyph").objectReferenceValue = chargeGlyph;
            serialized.FindProperty("telegraphRenderer").objectReferenceValue = telegraph;
            serialized.FindProperty("novaRenderer").objectReferenceValue = nova;
            serialized.FindProperty("novaMoteRenderer").objectReferenceValue = novaMotes;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform CreateMeshLayer(Transform parent, string name, string meshPath, Material material, Vector2 targetSize, float z, int sortingOrder)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null) throw new InvalidOperationException("Missing W1 shared carrier mesh: " + meshPath);
            var value = new GameObject(name);
            value.transform.SetParent(parent, false);
            value.transform.localPosition = new Vector3(0f, 0f, z);
            var size = mesh.bounds.size;
            value.transform.localScale = new Vector3(targetSize.x / Mathf.Max(.001f, size.x), targetSize.y / Mathf.Max(.001f, size.y), 1f);
            value.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = value.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            renderer.enabled = false;
            return value.transform;
        }

        private static void BuildPreviewScene(W1NextCandidateSpec[] specs, string candidateSignature)
        {
            if (specs.Length != TokenCellCount + CapabilityCellCount) throw new InvalidOperationException("W1 next-candidate Preview requires exactly eleven Runtime Entries.");
            ValidationGalleryCompiler.EnsureFolder("Assets/VFX/Preview");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateBackgroundCamera(scene);
            var canvas = CreateOverlayCanvas(scene);
            CreateStatusHeader(scene, canvas.transform);
            var entries = new List<W1NextCandidateRuntimeEntry>();
            for (var index = 0; index < specs.Length; index++)
            {
                var spec = specs[index];
                var cellRoot = new GameObject("Cell_" + (index + 1).ToString("00", CultureInfo.InvariantCulture) + "_" + spec.Id);
                SceneManager.MoveGameObjectToScene(cellRoot, scene);
                var prefabPath = "Assets/VFX/Generated/" + spec.Id + "/VFX_" + spec.Id + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null) throw new InvalidOperationException("Missing W1 next-candidate Runtime Entry: " + prefabPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = "Entry_" + spec.Id;
                instance.transform.SetParent(cellRoot.transform, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one * PreviewEntryScale;
                var layer = FirstIsolatedLayer + index;
                ApplyLayerRecursively(instance, layer);
                var entry = instance.GetComponent<W1NextCandidateRuntimeEntry>();
                if (entry == null) throw new InvalidOperationException(prefabPath + " has no W1 next-candidate Runtime Entry.");
                entries.Add(entry);
                var effectViewport = EffectViewport(index);
                var cameraObject = new GameObject("CellCamera_" + (index + 1).ToString("00", CultureInfo.InvariantCulture));
                cameraObject.transform.SetParent(cellRoot.transform, false);
                cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = .62f;
                camera.rect = effectViewport;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = CellBackground(spec.StyleToken);
                camera.cullingMask = 1 << layer;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.depth = index;
                CreateLabelBand(canvas.transform, index, (index + 1).ToString("00", CultureInfo.InvariantCulture) + "  " + spec.Label.ToUpperInvariant());
                var cell = cellRoot.AddComponent<W1NextCandidateCell>();
                var serialized = new SerializedObject(cell);
                serialized.FindProperty("cellIndex").intValue = index + 1;
                serialized.FindProperty("label").stringValue = spec.Label;
                serialized.FindProperty("styleToken").stringValue = spec.StyleToken;
                serialized.FindProperty("fullViewport").rectValue = FullCellViewport(index);
                serialized.FindProperty("effectViewport").rectValue = effectViewport;
                serialized.FindProperty("labelViewport").rectValue = LabelViewport(index);
                serialized.FindProperty("isolatedLayer").intValue = layer;
                serialized.FindProperty("effectCamera").objectReferenceValue = camera;
                serialized.FindProperty("runtimeEntry").objectReferenceValue = entry;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            CreateLegendCell(canvas.transform);
            var driverObject = new GameObject("W1NextCandidatePreviewDriver");
            SceneManager.MoveGameObjectToScene(driverObject, scene);
            var driver = driverObject.AddComponent<W1NextCandidatePreviewDriver>();
            var driverSerialized = new SerializedObject(driver);
            SetObjectArray(driverSerialized.FindProperty("runtimeEntries"), entries.Cast<UnityEngine.Object>().ToArray());
            driverSerialized.FindProperty("playDuration").floatValue = 2.05f;
            driverSerialized.FindProperty("cleanGap").floatValue = .3f;
            driverSerialized.FindProperty("compilerVersion").stringValue = CompilerVersion;
            driverSerialized.FindProperty("candidateSignature").stringValue = candidateSignature;
            driverSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, PreviewScenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static bool PreviewIsCurrent(string candidateSignature)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewScenePath) == null) return false;
            var scene = EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
            try
            {
                var driver = scene.GetRootGameObjects().Select(value => value.GetComponent<W1NextCandidatePreviewDriver>()).FirstOrDefault(value => value != null);
                return driver != null && driver.CompilerVersion == CompilerVersion && driver.CandidateSignature == candidateSignature && driver.ConfiguredEntryCount == TokenCellCount + CapabilityCellCount;
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static Camera CreateBackgroundCamera(Scene scene)
        {
            var value = new GameObject("VFXPREVIEW_W1_NextCandidate_MainCamera");
            SceneManager.MoveGameObjectToScene(value, scene);
            value.tag = "MainCamera";
            value.transform.position = new Vector3(0f, 0f, -10f);
            var camera = value.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.012f, .016f, .027f, 1f);
            camera.cullingMask = 0;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.depth = -100f;
            return camera;
        }

        private static Canvas CreateOverlayCanvas(Scene scene)
        {
            var value = new GameObject("W1NextCandidateOverlayCanvas");
            SceneManager.MoveGameObjectToScene(value, scene);
            value.layer = 5;
            var canvas = value.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = value.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = .5f;
            return canvas;
        }

        private static void CreateStatusHeader(Scene scene, Transform canvas)
        {
            var statusRoot = new GameObject(CandidateStatusRootName);
            SceneManager.MoveGameObjectToScene(statusRoot, scene);
            var header = new GameObject("W1NextCandidateStatusHeader", typeof(RectTransform));
            header.transform.SetParent(canvas, false);
            SetRect(header.GetComponent<RectTransform>(), new Rect(.025f, .91f, .95f, .075f));
            var text = header.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = "W1 NEXT CANDIDATE — VISUAL SIGN-OFF PENDING\n8 TOKEN MATERIAL / CARRIER / TIMING COMPARISON  •  3 TRACE-BACKED CAPABILITY + SKIN SAMPLES";
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 24;
            text.color = new Color(.84f, .9f, 1f, 1f);
        }

        private static void CreateLabelBand(Transform canvas, int index, string label)
        {
            var panel = CreatePanel(canvas, "CellLabelSafeBand_" + (index + 1).ToString("00", CultureInfo.InvariantCulture), LabelViewport(index), new Color(.025f, .035f, .055f, .98f));
            var textObject = new GameObject("Label", typeof(RectTransform));
            textObject.transform.SetParent(panel.transform, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(5f, 1f);
            rect.offsetMax = new Vector2(-5f, -1f);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 19;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = 21;
            text.color = new Color(.78f, .84f, .94f, 1f);
        }

        private static void CreateLegendCell(Transform canvas)
        {
            var full = FullCellViewport(11);
            var panel = CreatePanel(canvas, "Cell_12_BoundsLegend", full, new Color(.028f, .038f, .058f, .98f));
            var textObject = new GameObject("LegendText", typeof(RectTransform));
            textObject.transform.SetParent(panel.transform, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8f, 8f);
            rect.offsetMax = new Vector2(-8f, -8f);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = "FIXED CELL CONTRACT\n\nROOT SCALE 1.00\nLOCAL ENVELOPE 1.56 × 1.08\nHARD VIEWPORT CLIP\nSEPARATE LABEL SAFE BAND\n\nREPLAY → CLEAN GAP → REPLAY";
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 21;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = 23;
            text.color = new Color(.58f, .76f, .91f, 1f);
        }

        private static GameObject CreatePanel(Transform parent, string name, Rect normalizedRect, Color color)
        {
            var value = new GameObject(name, typeof(RectTransform));
            value.transform.SetParent(parent, false);
            SetRect(value.GetComponent<RectTransform>(), normalizedRect);
            var image = value.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return value;
        }

        private static void SetRect(RectTransform transformValue, Rect normalizedRect)
        {
            transformValue.anchorMin = normalizedRect.min;
            transformValue.anchorMax = normalizedRect.max;
            transformValue.offsetMin = Vector2.zero;
            transformValue.offsetMax = Vector2.zero;
        }

        private static string[] StyleMeshes(string token)
        {
            if (token == "stylized") return new[] { VfxStyleSharedLibrary.RibbonPath, VfxStyleSharedLibrary.RingPath, VfxStyleSharedLibrary.BurstPath };
            if (token == "cartoon") return new[] { VfxStyleSharedLibrary.BurstPath, VfxStyleSharedLibrary.RingPath, VfxStyleSharedLibrary.QuadPath };
            if (token == "pixel") return new[] { VfxStyleSharedLibrary.QuadPath, VfxStyleSharedLibrary.ShardPath, VfxStyleSharedLibrary.BurstPath };
            if (token == "inkwash") return new[] { VfxStyleSharedLibrary.RibbonPath, VfxStyleSharedLibrary.QuadPath, VfxStyleSharedLibrary.RingPath };
            if (token == "semireal") return new[] { VfxStyleSharedLibrary.QuadPath, VfxStyleSharedLibrary.RingPath, VfxStyleSharedLibrary.BurstPath };
            if (token == "holo") return new[] { VfxStyleSharedLibrary.RingPath, VfxStyleSharedLibrary.QuadPath, VfxStyleSharedLibrary.BurstPath };
            if (token == "dark") return new[] { VfxStyleSharedLibrary.RingPath, VfxStyleSharedLibrary.BurstPath, VfxStyleSharedLibrary.ShardPath };
            return new[] { VfxStyleSharedLibrary.RingPath, VfxStyleSharedLibrary.RibbonPath, VfxStyleSharedLibrary.BurstPath };
        }

        private static string VisualSignature(W1NextCandidateSpec spec, Material material, List<Renderer> renderers)
        {
            var meshes = renderers.Select(value => value.GetComponent<MeshFilter>()).Where(value => value != null && value.sharedMesh != null).Select(value => value.sharedMesh.name).ToArray();
            return material.shader.name +
                   "|mode=" + MaterialFloat(material, "_StyleMode") +
                   "|outline=" + MaterialFloat(material, "_Outline") +
                   "|steps=" + MaterialFloat(material, "_ShadingSteps") +
                   "|noise=" + MaterialFloat(material, "_NoiseScale") +
                   "|blend=" + MaterialFloat(material, "_DstBlend") +
                   "|meshes=" + string.Join(",", meshes) +
                   "|timing=" + spec.TimingProfile;
        }

        private static string MaterialFloat(Material material, string property)
        {
            return material.GetFloat(property).ToString("R", CultureInfo.InvariantCulture);
        }

        private static float BaseIntensity(string token, W1NextCandidateKind kind)
        {
            if (kind == W1NextCandidateKind.TelegraphNova) return 1.18f;
            if (token == "dark") return 1.24f;
            if (token == "inkwash") return .92f;
            if (token == "pixel") return 1.05f;
            if (token == "neon" || token == "holo") return 1.16f;
            return 1.08f;
        }

        private static string DependencySignature(W1NextCandidateSpec spec)
        {
            var paths = StyleMeshes(spec.StyleToken).Concat(new[] { VfxStyleSharedLibrary.MaterialPath(spec.StyleToken), VfxStyleSharedLibrary.RingPath, VfxStyleSharedLibrary.BurstPath, VfxStyleSharedLibrary.ShardPath }).Distinct().OrderBy(value => value, StringComparer.Ordinal).ToArray();
            foreach (var path in paths) if (AssetDatabase.LoadMainAssetAtPath(path) == null) throw new InvalidOperationException("Missing W1 next-candidate dependency: " + path);
            return string.Join("|", paths.Select(value => value + ":" + AssetDatabase.GetAssetDependencyHash(value)).ToArray());
        }

        private static Color CellBackground(string styleToken)
        {
            var definition = InitialStyleTokens.IndexOf(styleToken);
            var tint = Mathf.Max(0, definition) / 32f;
            return new Color(.018f + tint * .03f, .023f + tint * .02f, .038f + tint * .035f, 1f);
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

        private static bool TryParseKind(string value, out W1NextCandidateKind kind)
        {
            if (value == "style_token") { kind = W1NextCandidateKind.StyleToken; return true; }
            if (value == "fan_wave") { kind = W1NextCandidateKind.FanWave; return true; }
            if (value == "charge_occlude") { kind = W1NextCandidateKind.ChargeOcclude; return true; }
            if (value == "telegraph_nova") { kind = W1NextCandidateKind.TelegraphNova; return true; }
            kind = W1NextCandidateKind.StyleToken;
            return false;
        }

        private static bool TryParseTiming(string value, out W1StyleTimingProfile timing)
        {
            var map = new Dictionary<string, W1StyleTimingProfile>(StringComparer.Ordinal)
            {
                { "painted_sweep", W1StyleTimingProfile.PaintedSweep },
                { "cel_bounce", W1StyleTimingProfile.CelBounce },
                { "pixel_step", W1StyleTimingProfile.PixelStep },
                { "ink_bleed", W1StyleTimingProfile.InkBleed },
                { "soft_turbulence", W1StyleTimingProfile.SoftTurbulence },
                { "holo_scan", W1StyleTimingProfile.HoloScan },
                { "ritual_pulse", W1StyleTimingProfile.RitualPulse },
                { "neon_beat", W1StyleTimingProfile.NeonBeat },
                { "fan_wave", W1StyleTimingProfile.FanWave },
                { "charge_occlude", W1StyleTimingProfile.ChargeOcclude },
                { "telegraph_nova", W1StyleTimingProfile.TelegraphNova }
            };
            return map.TryGetValue(value ?? string.Empty, out timing);
        }

        private static bool TryColor(string value, out Color color)
        {
            return ColorUtility.TryParseHtmlString(value, out color);
        }

        private static void ValidateFrozenSet(W1NextCandidateSpec[] specs)
        {
            if (specs.Length != TokenCellCount + CapabilityCellCount) throw new InvalidOperationException("W1 next candidate must contain exactly eight token entries and three capability + skin entries.");
            if (specs.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != specs.Length) throw new InvalidOperationException("W1 next-candidate effect ids must be unique.");
            if (specs.Select(value => value.Seed).Distinct().Count() != specs.Length) throw new InvalidOperationException("W1 next-candidate deterministic seeds must be unique.");
            if (specs.Any(value => !Mathf.Approximately(value.Duration, 2f))) throw new InvalidOperationException("Every W1 next-candidate entry must use the same two-second playback window.");
            for (var index = 0; index < TokenCellCount; index++)
            {
                var spec = specs[index];
                if (spec.Kind != W1NextCandidateKind.StyleToken || spec.StyleToken != InitialStyleTokens[index] || spec.TimingProfile != InitialTimingProfiles[index])
                    throw new InvalidOperationException("The first eight W1 cells must preserve the frozen style-token and timing order.");
            }
            AssertCapabilityContract(specs[8], W1NextCandidateKind.FanWave, "projectile", "2d", "cartoon", W1StyleTimingProfile.FanWave);
            AssertCapabilityContract(specs[9], W1NextCandidateKind.ChargeOcclude, "beam", "3d", "holo", W1StyleTimingProfile.ChargeOcclude);
            AssertCapabilityContract(specs[10], W1NextCandidateKind.TelegraphNova, "impact", "3d", "stylized", W1StyleTimingProfile.TelegraphNova);
        }

        private static void AssertCapabilityContract(W1NextCandidateSpec spec, W1NextCandidateKind kind, string archetype, string dimension, string styleToken, W1StyleTimingProfile timing)
        {
            if (spec.Kind != kind || spec.Archetype != archetype || spec.Dimension != dimension || spec.StyleToken != styleToken || spec.TimingProfile != timing)
                throw new InvalidOperationException("W1 capability + skin descriptor drifted: " + spec.Id);
        }

        private static string ReadManifestBuildHash(string effectId)
        {
            var path = VfxProjectRules.ManifestAbsolutePath(effectId);
            if (!File.Exists(path)) return null;
            try { return (string)JObject.Parse(File.ReadAllText(path))["buildHash"]; }
            catch { return null; }
        }

        private static string Hash(string value)
        {
            using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static string Absolute(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static readonly List<string> InitialStyleTokens = new List<string> { "stylized", "cartoon", "pixel", "inkwash", "semireal", "holo", "dark", "neon" };
        private static readonly W1StyleTimingProfile[] InitialTimingProfiles =
        {
            W1StyleTimingProfile.PaintedSweep,
            W1StyleTimingProfile.CelBounce,
            W1StyleTimingProfile.PixelStep,
            W1StyleTimingProfile.InkBleed,
            W1StyleTimingProfile.SoftTurbulence,
            W1StyleTimingProfile.HoloScan,
            W1StyleTimingProfile.RitualPulse,
            W1StyleTimingProfile.NeonBeat
        };
    }
}
