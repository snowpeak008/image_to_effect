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
using UnityEngine.UI;
using VFXComposer.W17W18NextCandidate;

namespace VFXComposer.Editor.NextCandidates
{
    public static class W17W18NextCandidateAuthoring
    {
        public const string CompilerVersion = "w17-w18-next-candidate-2";
        public const string CandidateStatus = "NEXT_CANDIDATE_VISUAL_PENDING";
        public const string RecipeRoot = "Assets/VFX/Recipes/W17W18NextCandidate";
        public const string GeneratedRoot = "Assets/VFX/Generated/W17W18NextCandidate";
        public const string W17PreviewScenePath = "Assets/VFX/Preview/VFXPREVIEW_GameUI_NextCandidate.unity";
        public const string W18PreviewScenePath = "Assets/VFX/Preview/VFXPREVIEW_HeroKits_NextCandidate.unity";
        public const string W17StatusRootName = "W17_NEXT_CANDIDATE_VISUAL_PENDING";
        public const string W18StatusRootName = "W18_NEXT_CANDIDATE_VISUAL_PENDING";
        public const string SharedMaterialPath = GeneratedRoot + "/Shared/W17W18_WorldCellClip.mat";

        private static readonly string[] OldW17Ids =
        {
            "button_press_fx_ui", "button_confirm_burst_ui", "card_flip_reveal_ui", "card_merge_fx_ui", "chest_open_burst_ui",
            "gacha_single_reveal_ui", "gacha_ten_sequence_ui", "reward_fly_collect_ui", "daily_check_stamp_ui", "progress_charge_fx_ui"
        };

        private static readonly string[] OldW18Ids =
        {
            "flame_blade_samurai_kit_showcase_3d", "ice_moon_mage_kit_showcase_3d",
            "mechanical_hunter_kit_showcase_3d", "ghost_curse_shrine_kit_showcase_2d"
        };

        [MenuItem("Tools/VFX Composer/Next Candidates/Build W17 W18")]
        public static void BuildAllForBatch()
        {
            BuildW17ForBatch();
            BuildW18ForBatch();
        }

        [MenuItem("Tools/VFX Composer/Next Candidates/Build W17 Game UI")]
        public static void BuildW17ForBatch()
        {
            var protectedPaths = OldW17Ids.Select(OldPrefabPath).Concat(new[] { "Assets/VFX/Preview/VFXPREVIEW_GameUI.unity" }).ToArray();
            var before = SnapshotProtectedOutputs(protectedPaths);
            EnsureFolders();
            foreach (var plan in W17W18NextCandidateCatalog.W17) BuildW17(plan);
            BuildW17Preview();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssertProtectedOutputsUnchanged(before);
        }

        [MenuItem("Tools/VFX Composer/Next Candidates/Build W18 Character Themes")]
        public static void BuildW18ForBatch()
        {
            var protectedPaths = OldW18Ids.Select(OldPrefabPath).Concat(new[] { "Assets/VFX/Preview/VFXPREVIEW_HeroKits.unity" }).ToArray();
            var before = SnapshotProtectedOutputs(protectedPaths);
            EnsureFolders();
            EnsureSharedWorldAssets();
            foreach (var plan in W17W18NextCandidateCatalog.W18) BuildW18(plan);
            BuildW18Preview();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssertProtectedOutputsUnchanged(before);
        }

        public static string W17RecipePath(string id) { return RecipeRoot + "/W17/" + id + ".default.json"; }
        public static string W18RecipePath(string id) { return RecipeRoot + "/W18/" + id + ".default.json"; }
        public static string W17PrefabPath(string id) { return GeneratedRoot + "/W17/" + id + "/VFX_" + id + ".prefab"; }
        public static string W18PrefabPath(string id) { return GeneratedRoot + "/W18/" + id + "/VFX_" + id + ".prefab"; }
        public static string ManifestPath(string workPackage, string id) { return GeneratedRoot + "/" + workPackage + "/" + id + "/NextCandidateManifest.json"; }

        private static void BuildW17(W17NextCandidatePlan plan)
        {
            var recipePath = W17RecipePath(plan.Id);
            var recipe = ReadAndValidateRecipe(recipePath, plan.Id, "w17-ui-next-candidate/v1", "W17");
            var buildHash = Hash(CompilerVersion + "|W17|" + Canonical(recipe));
            var prefabPath = W17PrefabPath(plan.Id);
            var manifestPath = ManifestPath("W17", plan.Id);
            if (IsUnchanged(manifestPath, prefabPath, buildHash)) return;

            EnsureAssetFolder(Path.GetDirectoryName(prefabPath).Replace('\\', '/'));
            var root = new GameObject("VFX_" + plan.Id, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            try
            {
                root.layer = LayerMask.NameToLayer("UI");
                var rootRect = root.GetComponent<RectTransform>();
                rootRect.sizeDelta = new Vector2(320f, 184f);
                rootRect.localScale = Vector3.one * .006f;
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.pixelPerfect = false;
                canvas.sortingOrder = 20;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.dynamicPixelsPerUnit = 1f;
                scaler.referencePixelsPerUnit = 100f;
                root.GetComponent<GraphicRaycaster>().enabled = false;

                var viewport = CreateRect("HardClipViewport", rootRect, Vector2.zero, new Vector2(304f, 168f));
                var clip = viewport.gameObject.AddComponent<RectMask2D>();
                var effectRoot = CreateRect("RealUiEffectRoot", viewport, Vector2.zero, viewport.sizeDelta);
                var graphics = CreateW17Visuals(plan, effectRoot);
                var controller = root.AddComponent<W17UiInteractionController>();
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("effectId").stringValue = plan.Id;
                serialized.FindProperty("kind").enumValueIndex = (int)plan.Kind;
                serialized.FindProperty("duration").floatValue = plan.Duration;
                serialized.FindProperty("seed").longValue = StableSeed(plan.Id);
                serialized.FindProperty("primary").colorValue = ColorOf(plan.Primary);
                serialized.FindProperty("secondary").colorValue = ColorOf(plan.Secondary);
                serialized.FindProperty("accent").colorValue = ColorOf(plan.Accent);
                serialized.FindProperty("canvas").objectReferenceValue = canvas;
                serialized.FindProperty("canvasRoot").objectReferenceValue = rootRect;
                serialized.FindProperty("hardClip").objectReferenceValue = clip;
                serialized.FindProperty("effectRoot").objectReferenceValue = effectRoot;
                serialized.FindProperty("rarity").intValue = plan.Rarity;
                serialized.FindProperty("itemCount").intValue = plan.ItemCount;
                serialized.FindProperty("mergeSourceCount").intValue = plan.Kind == W17UiEffectKind.CardMerge ? 3 : 2;
                serialized.FindProperty("rewardStagger").floatValue = .055f;
                serialized.FindProperty("rewardArcHeight").floatValue = 62f;
                WriteObjectArray(serialized.FindProperty("graphics"), graphics.Cast<UnityEngine.Object>().ToArray());
                WriteObjectArray(serialized.FindProperty("carriers"), graphics.Select(value => (UnityEngine.Object)value.rectTransform).ToArray());
                serialized.ApplyModifiedPropertiesWithoutUndo();
                controller.ResetForPool();

                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null) throw new InvalidOperationException("Could not save " + prefabPath);
                WriteManifest(manifestPath, new JObject
                {
                    ["schema"] = "vfx-next-candidate-manifest/v1",
                    ["compilerVersion"] = CompilerVersion,
                    ["buildHash"] = buildHash,
                    ["workPackage"] = "W17",
                    ["candidateStatus"] = CandidateStatus,
                    ["userVisualVerdict"] = JValue.CreateNull(),
                    ["id"] = plan.Id,
                    ["recipe"] = recipePath,
                    ["runtimeEntry"] = prefabPath,
                    ["runtimeType"] = typeof(W17UiInteractionController).FullName,
                    ["hardClip"] = "RectMask2D",
                    ["uiElements"] = graphics.Count,
                    ["uiElementLimit"] = plan.Kind == W17UiEffectKind.GachaSingle || plan.Kind == W17UiEffectKind.GachaTen ? W17UiInteractionController.GachaUiElementBudget : W17UiInteractionController.NormalUiElementBudget,
                    ["particleSystems"] = 0,
                    ["rewardPoolCapacity"] = plan.Kind == W17UiEffectKind.RewardFly ? W17UiInteractionController.RewardPoolCapacity : 0,
                    ["oldCandidateUntouched"] = true
                });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildW18(W18NextCandidatePlan plan)
        {
            var recipePath = W18RecipePath(plan.Id);
            var recipe = ReadAndValidateRecipe(recipePath, plan.Id, "w18-theme-next-candidate/v1", "W18");
            var buildHash = Hash(CompilerVersion + "|W18|" + Canonical(recipe));
            var prefabPath = W18PrefabPath(plan.Id);
            var manifestPath = ManifestPath("W18", plan.Id);
            if (IsUnchanged(manifestPath, prefabPath, buildHash)) return;

            EnsureAssetFolder(Path.GetDirectoryName(prefabPath).Replace('\\', '/'));
            var material = AssetDatabase.LoadAssetAtPath<Material>(SharedMaterialPath);
            if (material == null) throw new InvalidOperationException("Missing shared clip material: " + SharedMaterialPath);
            var root = new GameObject("VFX_" + plan.Id);
            try
            {
                root.SetActive(false);
                var controller = root.AddComponent<W18CharacterThemeController>();
                var renderers = new List<Renderer>();
                var carriers = new List<Transform>();
                var hand = new List<Transform>();
                var weapon = new List<Transform>();
                var chest = new List<Transform>();
                var feet = new List<Transform>();

                var body = AddMeshCarrier(root.transform, "VisibleCharacterBody", W18CarrierMesh.Body, new Vector3(0f, .03f, .18f), new Vector3(.44f, 1.08f, 1f), material);
                renderers.Add(body.GetComponent<MeshRenderer>());
                foreach (var carrierPlan in plan.Carriers)
                {
                    var carrier = AddMeshCarrier(root.transform, carrierPlan.Role, carrierPlan.Mesh, carrierPlan.Position, carrierPlan.Scale, material);
                    carriers.Add(carrier);
                    renderers.Add(carrier.GetComponent<MeshRenderer>());
                    AddToSlot(carrierPlan.Slot, carrier, hand, weapon, chest, feet);
                }
                var lineObject = new GameObject("ThemeLine");
                lineObject.transform.SetParent(root.transform, false);
                var line = lineObject.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.startWidth = .035f;
                line.endWidth = .012f;
                line.numCornerVertices = 2;
                line.numCapVertices = 2;
                line.positionCount = 0;
                line.enabled = false;
                carriers.Add(line.transform);
                renderers.Add(line);

                var serialized = new SerializedObject(controller);
                serialized.FindProperty("kitId").stringValue = plan.Id;
                serialized.FindProperty("theme").enumValueIndex = (int)plan.Theme;
                serialized.FindProperty("paletteReference").stringValue = plan.PaletteReference;
                serialized.FindProperty("shapeLanguage").stringValue = plan.ShapeLanguage;
                serialized.FindProperty("cycleDuration").floatValue = 8f;
                serialized.FindProperty("seed").longValue = StableSeed(plan.Id);
                serialized.FindProperty("primary").colorValue = ColorOf(plan.Primary);
                serialized.FindProperty("secondary").colorValue = ColorOf(plan.Secondary);
                serialized.FindProperty("accent").colorValue = ColorOf(plan.Accent);
                serialized.FindProperty("bodyRenderer").objectReferenceValue = body.GetComponent<MeshRenderer>();
                WriteObjectArray(serialized.FindProperty("ownedRenderers"), renderers.Cast<UnityEngine.Object>().ToArray());
                WriteObjectArray(serialized.FindProperty("visualCarriers"), carriers.Cast<UnityEngine.Object>().ToArray());
                WriteObjectArray(serialized.FindProperty("handCarriers"), hand.Cast<UnityEngine.Object>().ToArray());
                WriteObjectArray(serialized.FindProperty("weaponCarriers"), weapon.Cast<UnityEngine.Object>().ToArray());
                WriteObjectArray(serialized.FindProperty("chestCarriers"), chest.Cast<UnityEngine.Object>().ToArray());
                WriteObjectArray(serialized.FindProperty("feetCarriers"), feet.Cast<UnityEngine.Object>().ToArray());
                WriteObjectArray(serialized.FindProperty("lines"), new UnityEngine.Object[] { line });
                WriteObjectArray(serialized.FindProperty("particles"), new UnityEngine.Object[0]);
                serialized.FindProperty("previewHardClip").boolValue = false;
                serialized.FindProperty("worldClipRect").vector4Value = new Vector4(-1.48f, -1.04f, 1.48f, 1.04f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(true);
                controller.ResetForPool();

                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null) throw new InvalidOperationException("Could not save " + prefabPath);
                var budget = controller.ReadBudget();
                WriteManifest(manifestPath, new JObject
                {
                    ["schema"] = "vfx-next-candidate-manifest/v1",
                    ["compilerVersion"] = CompilerVersion,
                    ["buildHash"] = buildHash,
                    ["workPackage"] = "W18",
                    ["candidateStatus"] = CandidateStatus,
                    ["userVisualVerdict"] = JValue.CreateNull(),
                    ["id"] = plan.Id,
                    ["recipe"] = recipePath,
                    ["runtimeEntry"] = prefabPath,
                    ["runtimeType"] = typeof(W18CharacterThemeController).FullName,
                    ["paletteReference"] = plan.PaletteReference,
                    ["shapeLanguage"] = plan.ShapeLanguage,
                    ["hardClipShader"] = W18CharacterThemeController.RequiredClipShader,
                    ["budget"] = new JObject
                    {
                        ["renderers"] = budget.Renderers,
                        ["materials"] = budget.Materials,
                        ["particleSystems"] = budget.ParticleSystems,
                        ["particleCapacity"] = budget.ParticleCapacity,
                        ["limits"] = new JObject
                        {
                            ["renderers"] = W18CharacterThemeController.MaxRendererBudget,
                            ["materials"] = W18CharacterThemeController.MaxMaterialBudget,
                            ["particleSystems"] = W18CharacterThemeController.MaxParticleSystemBudget,
                            ["particleCapacity"] = W18CharacterThemeController.MaxParticleCapacity
                        }
                    },
                    ["oldCandidateUntouched"] = true
                });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static List<Graphic> CreateW17Visuals(W17NextCandidatePlan plan, RectTransform parent)
        {
            var roles = new List<string>();
            switch (plan.Kind)
            {
                case W17UiEffectKind.ButtonPress:
                    roles.AddRange(new[] { "ButtonSurface", "Ripple", "EdgeSweep", "Star_0", "Star_1" });
                    break;
                case W17UiEffectKind.ButtonConfirm:
                    roles.AddRange(new[] { "ButtonSurface", "ConfirmRing", "EdgeSweep" });
                    for (var index = 0; index < 8; index++) roles.Add("Ray_" + index);
                    break;
                case W17UiEffectKind.CardFlip:
                    roles.AddRange(new[] { "CardBody", "RevealFlash" });
                    for (var index = 0; index < 5; index++) roles.Add("RarityBurst_" + index);
                    break;
                case W17UiEffectKind.CardMerge:
                    for (var index = 0; index < 3; index++) roles.Add("MergeSource_" + index);
                    roles.AddRange(new[] { "ResultColumn", "ResultCard" });
                    break;
                case W17UiEffectKind.ChestOpen:
                    roles.AddRange(new[] { "ChestBase", "ChestLid", "ChestLeak", "ChestBurst" });
                    for (var index = 0; index < 5; index++) roles.Add("Tease_" + index);
                    break;
                case W17UiEffectKind.GachaSingle:
                    roles.Add("GachaOrb");
                    for (var index = 0; index < 6; index++) roles.Add("Crack_" + index);
                    for (var index = 0; index < 12; index++) roles.Add("RarityBurst_" + index);
                    roles.AddRange(new[] { "RevealFlash", "FullscreenGrace" });
                    break;
                case W17UiEffectKind.GachaTen:
                    for (var index = 0; index < 10; index++) roles.Add("TenCard_" + index);
                    roles.Add("HighestPulse");
                    break;
                case W17UiEffectKind.RewardFly:
                    for (var index = 0; index < W17UiInteractionController.RewardPoolCapacity; index++) roles.Add("RewardItem_" + index);
                    roles.Add("EndpointPulse");
                    break;
                case W17UiEffectKind.DailyStamp:
                    roles.AddRange(new[] { "StampBody", "InkRing", "CheckStroke" });
                    break;
                default:
                    roles.AddRange(new[] { "ProgressTrack", "ProgressFill", "ProgressGlint", "FullPulse" });
                    break;
            }

            var graphics = new List<Graphic>();
            foreach (var role in roles)
            {
                var rect = CreateRect(role, parent, Vector2.zero, DefaultUiSize(role));
                var image = rect.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                image.color = Color.white;
                image.material = null;
                image.enabled = false;
                graphics.Add(image);
            }
            return graphics;
        }

        private static Vector2 DefaultUiSize(string role)
        {
            if (role == "ButtonSurface") return new Vector2(140f, 70f);
            if (role == "ChestBase") return new Vector2(104f, 62f);
            if (role == "ChestLid") return new Vector2(96f, 34f);
            if (role == "CardBody" || role == "ResultCard") return new Vector2(72f, 104f);
            if (role == "ProgressTrack" || role == "ProgressFill") return new Vector2(244f, 26f);
            if (role.StartsWith("TenCard_", StringComparison.Ordinal)) return new Vector2(40f, 54f);
            if (role.StartsWith("RewardItem_", StringComparison.Ordinal)) return Vector2.one * 16f;
            return Vector2.one * 24f;
        }

        private static void BuildW17Preview()
        {
            var hash = PreviewBuildHash("W17", W17W18NextCandidateCatalog.W17.Select(value => ManifestPath("W17", value.Id)));
            var marker = GeneratedRoot + "/W17Preview.hash.txt";
            if (File.Exists(Absolute(W17PreviewScenePath)) && ReadText(marker) == hash) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateReviewCamera("W17GameUiNextCandidateCamera", 3.35f);
            new GameObject(W17StatusRootName);
            var entries = new List<W17UiInteractionController>();
            var labels = new List<string>();
            var plans = W17W18NextCandidateCatalog.W17.ToList();
            plans.Add(W17W18NextCandidateCatalog.W17[0]);
            plans.Add(W17W18NextCandidateCatalog.W17[0]);
            for (var index = 0; index < plans.Count; index++)
            {
                var plan = plans[index];
                var column = index % 4;
                var row = index / 4;
                var cell = new GameObject("W17Cell_" + (index + 1).ToString("00", CultureInfo.InvariantCulture) + "_" + plan.Id);
                cell.transform.position = new Vector3((column - 1.5f) * 2.25f, (1f - row) * 1.62f, 0f);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(W17PrefabPath(plan.Id));
                if (prefab == null) throw new InvalidOperationException("Missing W17 prefab " + plan.Id);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetParent(cell.transform, false);
                var entry = instance.GetComponent<W17UiInteractionController>();
                if (index == 10) entry.SetButtonRectSize(new Vector2(92f, 44f));
                else if (index == 11) entry.SetButtonRectSize(new Vector2(220f, 92f));
                if (entry.Kind == W17UiEffectKind.RewardFly) entry.SetRewardRoute(new Vector2(-118f, -50f), new Vector2(118f, 48f), 12, 72f, .05f);
                if (entry.Kind == W17UiEffectKind.GachaSingle) entry.SetRarity(5);
                entries.Add(entry);
                var viewport = new Rect((column + .035f) / 4f, (2 - row + .15f) / 3f, .93f / 4f, .75f / 3f);
                var metadata = cell.AddComponent<W17W18NextCandidateCell>();
                var serialized = new SerializedObject(metadata);
                serialized.FindProperty("family").enumValueIndex = (int)W17W18PreviewFamily.W17Ui;
                serialized.FindProperty("cellIndex").intValue = index;
                serialized.FindProperty("candidateId").stringValue = plan.Id + (index >= 10 ? ".button_size_" + (index - 9) : string.Empty);
                serialized.FindProperty("normalizedViewport").rectValue = viewport;
                serialized.FindProperty("worldClipRect").rectValue = new Rect(cell.transform.position.x - .96f, cell.transform.position.y - .52f, 1.92f, 1.04f);
                serialized.FindProperty("uiEntry").objectReferenceValue = entry;
                serialized.FindProperty("canvasClip").objectReferenceValue = entry.GetComponentInChildren<RectMask2D>(true);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                labels.Add(index >= 10 ? "button_press / " + entry.ButtonRectSize.x.ToString("0", CultureInfo.InvariantCulture) + "x" + entry.ButtonRectSize.y.ToString("0", CultureInfo.InvariantCulture) : plan.Id.Replace("_next_candidate", string.Empty));
            }
            CreateOverlay("W17Overlay", "W17 GAME UI • NEXT_CANDIDATE_VISUAL_PENDING", labels, 4, 3);
            var driverObject = new GameObject("W17NextCandidatePreviewDriver");
            var driver = driverObject.AddComponent<W17W18NextCandidatePreviewDriver>();
            var driverSerialized = new SerializedObject(driver);
            driverSerialized.FindProperty("compilerVersion").stringValue = CompilerVersion;
            WriteObjectArray(driverSerialized.FindProperty("uiEntries"), entries.Cast<UnityEngine.Object>().ToArray());
            WriteObjectArray(driverSerialized.FindProperty("themeEntries"), new UnityEngine.Object[0]);
            driverSerialized.FindProperty("playDuration").floatValue = 4.2f;
            driverSerialized.FindProperty("cleanGap").floatValue = .38f;
            driverSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, W17PreviewScenePath);
            WriteText(marker, hash);
        }

        private static void BuildW18Preview()
        {
            var hash = PreviewBuildHash("W18", W17W18NextCandidateCatalog.W18.Select(value => ManifestPath("W18", value.Id)));
            var marker = GeneratedRoot + "/W18Preview.hash.txt";
            if (File.Exists(Absolute(W18PreviewScenePath)) && ReadText(marker) == hash) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateReviewCamera("W18HeroKitsNextCandidateCamera", 2.65f);
            new GameObject(W18StatusRootName);
            var entries = new List<W18CharacterThemeController>();
            var labels = new List<string>();
            for (var index = 0; index < W17W18NextCandidateCatalog.W18.Length; index++)
            {
                var plan = W17W18NextCandidateCatalog.W18[index];
                var column = index % 2;
                var row = index / 2;
                var center = new Vector3((column - .5f) * 3.12f, (.5f - row) * 2.34f, 0f);
                var cell = new GameObject("W18Cell_" + (index + 1).ToString("00", CultureInfo.InvariantCulture) + "_" + plan.Id);
                cell.transform.position = center;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(W18PrefabPath(plan.Id));
                if (prefab == null) throw new InvalidOperationException("Missing W18 prefab " + plan.Id);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetParent(cell.transform, false);
                var entry = instance.GetComponent<W18CharacterThemeController>();
                var clipRect = new Rect(center.x - 1.45f, center.y - 1.02f, 2.9f, 2.04f);
                var entrySerialized = new SerializedObject(entry);
                entrySerialized.FindProperty("previewHardClip").boolValue = true;
                entrySerialized.FindProperty("worldClipRect").vector4Value = new Vector4(clipRect.xMin, clipRect.yMin, clipRect.xMax, clipRect.yMax);
                entrySerialized.ApplyModifiedPropertiesWithoutUndo();
                entry.ConfigurePreviewClip(clipRect);
                entries.Add(entry);
                var metadata = cell.AddComponent<W17W18NextCandidateCell>();
                var serialized = new SerializedObject(metadata);
                serialized.FindProperty("family").enumValueIndex = (int)W17W18PreviewFamily.W18Theme;
                serialized.FindProperty("cellIndex").intValue = index;
                serialized.FindProperty("candidateId").stringValue = plan.Id;
                serialized.FindProperty("normalizedViewport").rectValue = new Rect((column + .04f) / 2f, (1 - row + .13f) / 2f, .92f / 2f, .76f / 2f);
                serialized.FindProperty("worldClipRect").rectValue = clipRect;
                serialized.FindProperty("themeEntry").objectReferenceValue = entry;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                labels.Add(plan.Theme + "\n" + plan.ShapeLanguage);
            }
            CreateOverlay("W18Overlay", "W18 CHARACTER THEMES • NEXT_CANDIDATE_VISUAL_PENDING", labels, 2, 2);
            var driverObject = new GameObject("W18NextCandidatePreviewDriver");
            var driver = driverObject.AddComponent<W17W18NextCandidatePreviewDriver>();
            var driverSerialized = new SerializedObject(driver);
            driverSerialized.FindProperty("compilerVersion").stringValue = CompilerVersion;
            WriteObjectArray(driverSerialized.FindProperty("uiEntries"), new UnityEngine.Object[0]);
            WriteObjectArray(driverSerialized.FindProperty("themeEntries"), entries.Cast<UnityEngine.Object>().ToArray());
            driverSerialized.FindProperty("playDuration").floatValue = 8.15f;
            driverSerialized.FindProperty("cleanGap").floatValue = .42f;
            driverSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, W18PreviewScenePath);
            WriteText(marker, hash);
        }

        private static void EnsureSharedWorldAssets()
        {
            EnsureAssetFolder(GeneratedRoot + "/Shared/Meshes");
            var shader = Shader.Find(W18CharacterThemeController.RequiredClipShader);
            if (shader == null) throw new InvalidOperationException("Shader was not imported: " + W18CharacterThemeController.RequiredClipShader);
            if (AssetDatabase.LoadAssetAtPath<Material>(SharedMaterialPath) == null)
            {
                var material = new Material(shader) { name = "W17W18_WorldCellClip" };
                material.SetColor("_Color", Color.white);
                material.SetFloat("_UseClip", 0f);
                AssetDatabase.CreateAsset(material, SharedMaterialPath);
            }
            foreach (W18CarrierMesh value in Enum.GetValues(typeof(W18CarrierMesh))) EnsureMesh(value);
            AssetDatabase.SaveAssets();
        }

        private static Transform AddMeshCarrier(Transform parent, string role, W18CarrierMesh meshKind, Vector3 position, Vector3 scale, Material material)
        {
            var child = new GameObject(role);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            child.transform.localScale = scale;
            var filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(meshKind));
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.enabled = false;
            return child.transform;
        }

        private static void AddToSlot(W18CarrierSlot slot, Transform value, List<Transform> hand, List<Transform> weapon, List<Transform> chest, List<Transform> feet)
        {
            if (slot == W18CarrierSlot.Hand) hand.Add(value);
            else if (slot == W18CarrierSlot.Weapon) weapon.Add(value);
            else if (slot == W18CarrierSlot.Chest) chest.Add(value);
            else if (slot == W18CarrierSlot.Feet) feet.Add(value);
        }

        private static void EnsureMesh(W18CarrierMesh kind)
        {
            var path = MeshPath(kind);
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) return;
            Mesh mesh;
            switch (kind)
            {
                case W18CarrierMesh.Body: mesh = PolygonMesh("CharacterBody", new[] { new Vector2(-.42f, -.5f), new Vector2(-.35f, .15f), new Vector2(-.18f, .42f), new Vector2(0f, .55f), new Vector2(.18f, .42f), new Vector2(.35f, .15f), new Vector2(.42f, -.5f), new Vector2(0f, -.62f) }); break;
                case W18CarrierMesh.Crescent: mesh = ArcRibbonMesh("SharpCrescent", 16, 210f, .34f); break;
                case W18CarrierMesh.Diamond: mesh = PolygonMesh("Diamond", RadialPoints(4, .5f, 45f)); break;
                case W18CarrierMesh.Hexagon: mesh = PolygonMesh("Hexagon", RadialPoints(6, .5f, 30f)); break;
                case W18CarrierMesh.Ring: mesh = RingMesh("Ring", 24, .5f, .34f); break;
                case W18CarrierMesh.Gear: mesh = PolygonMesh("Gear", AlternatingRadialPoints(12, .5f, .38f)); break;
                case W18CarrierMesh.Ribbon: mesh = PolygonMesh("Ribbon", new[] { new Vector2(-.55f, -.18f), new Vector2(-.2f, .2f), new Vector2(.15f, -.08f), new Vector2(.55f, .18f), new Vector2(.2f, -.2f), new Vector2(-.15f, .08f) }); break;
                case W18CarrierMesh.Star: mesh = PolygonMesh("Star", AlternatingRadialPoints(8, .5f, .18f)); break;
                case W18CarrierMesh.TalismanArray: mesh = TalismanArrayMesh(); break;
                default: mesh = GhostProcessionMesh(); break;
            }
            mesh.name = kind.ToString();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            AssetDatabase.CreateAsset(mesh, path);
        }

        private static string MeshPath(W18CarrierMesh kind) { return GeneratedRoot + "/Shared/Meshes/W18_" + kind + ".asset"; }

        private static Mesh PolygonMesh(string name, Vector2[] points)
        {
            var vertices = new List<Vector3> { Vector3.zero };
            vertices.AddRange(points.Select(value => new Vector3(value.x, value.y, 0f)));
            var triangles = new List<int>();
            for (var index = 0; index < points.Length; index++) { triangles.Add(0); triangles.Add(index + 1); triangles.Add((index + 1) % points.Length + 1); }
            var mesh = new Mesh { name = name, vertices = vertices.ToArray(), triangles = triangles.ToArray() };
            mesh.uv = vertices.Select(value => new Vector2(value.x + .5f, value.y + .5f)).ToArray();
            return mesh;
        }

        private static Mesh RingMesh(string name, int segments, float outer, float inner)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (var index = 0; index < segments; index++)
            {
                var angle = index / (float)segments * Mathf.PI * 2f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertices.Add(direction * outer); vertices.Add(direction * inner);
                uv.Add(new Vector2(index / (float)segments, 1f)); uv.Add(new Vector2(index / (float)segments, 0f));
            }
            for (var index = 0; index < segments; index++)
            {
                var next = (index + 1) % segments;
                triangles.Add(index * 2); triangles.Add(next * 2); triangles.Add(index * 2 + 1);
                triangles.Add(next * 2); triangles.Add(next * 2 + 1); triangles.Add(index * 2 + 1);
            }
            return new Mesh { name = name, vertices = vertices.ToArray(), uv = uv.ToArray(), triangles = triangles.ToArray() };
        }

        private static Mesh ArcRibbonMesh(string name, int segments, float degrees, float thickness)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (var index = 0; index <= segments; index++)
            {
                var u = index / (float)segments;
                var angle = (-degrees * .5f + degrees * u) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertices.Add(direction * .52f); vertices.Add(direction * (.52f - thickness));
                uv.Add(new Vector2(u, 1f)); uv.Add(new Vector2(u, 0f));
                if (index == segments) continue;
                var baseIndex = index * 2;
                triangles.Add(baseIndex); triangles.Add(baseIndex + 2); triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2); triangles.Add(baseIndex + 3); triangles.Add(baseIndex + 1);
            }
            return new Mesh { name = name, vertices = vertices.ToArray(), uv = uv.ToArray(), triangles = triangles.ToArray() };
        }

        private static Mesh TalismanArrayMesh()
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (var index = 0; index < 8; index++)
            {
                var angle = index * 45f * Mathf.Deg2Rad;
                var center = new Vector2(Mathf.Cos(angle) * .58f, Mathf.Sin(angle) * .38f);
                var tangent = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
                var radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var start = vertices.Count;
                vertices.Add(center - tangent * .065f - radial * .14f);
                vertices.Add(center + tangent * .065f - radial * .14f);
                vertices.Add(center + tangent * .065f + radial * .14f);
                vertices.Add(center - tangent * .065f + radial * .14f);
                uv.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            return new Mesh { name = "EightTalismanArray", vertices = vertices.ToArray(), uv = uv.ToArray(), triangles = triangles.ToArray() };
        }

        private static Mesh GhostProcessionMesh()
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (var index = 0; index < 12; index++)
            {
                var center = new Vector2(-.88f + index * .16f, Mathf.Sin(index * .9f) * .26f);
                var size = .07f + (index % 3) * .012f;
                var start = vertices.Count;
                vertices.Add(center + Vector2.up * size * 1.5f);
                vertices.Add(center + Vector2.right * size);
                vertices.Add(center - Vector2.up * size * 1.5f);
                vertices.Add(center - Vector2.right * size);
                uv.AddRange(new[] { Vector2.up, Vector2.right, Vector2.zero, Vector2.left });
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            return new Mesh { name = "TwelveGhostProcession", vertices = vertices.ToArray(), uv = uv.ToArray(), triangles = triangles.ToArray() };
        }

        private static Vector2[] RadialPoints(int count, float radius, float offsetDegrees)
        {
            return Enumerable.Range(0, count).Select(index =>
            {
                var angle = (offsetDegrees + index * 360f / count) * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }).ToArray();
        }

        private static Vector2[] AlternatingRadialPoints(int count, float outer, float inner)
        {
            return Enumerable.Range(0, count * 2).Select(index =>
            {
                var angle = index * Mathf.PI / count;
                var radius = (index & 1) == 0 ? outer : inner;
                return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }).ToArray();
        }

        private static void CreateReviewCamera(string name, float orthographicSize)
        {
            var cameraObject = new GameObject(name);
            var camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.012f, .016f, .026f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.transform.position = new Vector3(0f, 0f, -12f);
            camera.allowHDR = false;
            camera.allowMSAA = false;
        }

        private static void CreateOverlay(string name, string title, IList<string> labels, int columns, int rows)
        {
            var canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var titleText = AddOverlayText(canvasObject.transform, "Title", title, 24, TextAnchor.UpperCenter);
            titleText.rectTransform.anchorMin = new Vector2(.05f, .94f);
            titleText.rectTransform.anchorMax = new Vector2(.95f, .995f);
            titleText.rectTransform.offsetMin = titleText.rectTransform.offsetMax = Vector2.zero;
            for (var index = 0; index < labels.Count; index++)
            {
                var column = index % columns;
                var row = index / columns;
                var text = AddOverlayText(canvasObject.transform, "Label_" + index.ToString("00", CultureInfo.InvariantCulture), labels[index], columns <= 2 ? 20 : 14, TextAnchor.LowerCenter);
                var xMin = column / (float)columns + .012f;
                var xMax = (column + 1f) / columns - .012f;
                var yMin = columns <= 2 ? .05f + (rows - row - 1f) * .43f : .141f + (rows - row - 1f) * .242f;
                var yMax = yMin + .035f;
                text.rectTransform.anchorMin = new Vector2(xMin, yMin);
                text.rectTransform.anchorMax = new Vector2(xMax, yMax);
                text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            }
        }

        private static Text AddOverlayText(Transform parent, string name, string value, int fontSize, TextAnchor alignment)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            child.transform.SetParent(parent, false);
            var text = child.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.color = new Color(.84f, .9f, .96f, .92f);
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string name, RectTransform parent, Vector2 position, Vector2 size)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            child.layer = parent.gameObject.layer;
            var rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static JObject ReadAndValidateRecipe(string path, string expectedId, string schema, string workPackage)
        {
            var absolute = Absolute(path);
            if (!File.Exists(absolute)) throw new FileNotFoundException("Missing next-candidate Recipe", absolute);
            var root = JObject.Parse(File.ReadAllText(absolute));
            if ((string)root["schema"] != schema) throw new InvalidOperationException(path + " schema must be " + schema);
            if ((string)root["id"] != expectedId) throw new InvalidOperationException(path + " id mismatch");
            if ((string)root["workPackage"] != workPackage) throw new InvalidOperationException(path + " workPackage mismatch");
            if ((string)root["candidateStatus"] != CandidateStatus) throw new InvalidOperationException(path + " candidateStatus mismatch");
            if (root["userVisualVerdict"] == null || root["userVisualVerdict"].Type != JTokenType.Null) throw new InvalidOperationException(path + " userVisualVerdict must remain null");
            if ((bool?)root["preserveRejectedCandidate"] != true) throw new InvalidOperationException(path + " must preserve the rejected candidate");
            return root;
        }

        private static bool IsUnchanged(string manifestPath, string prefabPath, string buildHash)
        {
            var manifestAbsolute = Absolute(manifestPath);
            if (!File.Exists(manifestAbsolute) || AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) return false;
            try { return (string)JObject.Parse(File.ReadAllText(manifestAbsolute))["buildHash"] == buildHash; }
            catch { return false; }
        }

        private static void WriteManifest(string path, JObject value)
        {
            WriteText(path, value.ToString(Formatting.Indented));
        }

        private static void WriteText(string assetPath, string value)
        {
            var absolute = Absolute(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            var normalized = value.Replace("\r\n", "\n");
            if (!normalized.EndsWith("\n", StringComparison.Ordinal)) normalized += "\n";
            if (File.Exists(absolute) && File.ReadAllText(absolute) == normalized) return;
            File.WriteAllText(absolute, normalized, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static string ReadText(string assetPath)
        {
            var absolute = Absolute(assetPath);
            return File.Exists(absolute) ? File.ReadAllText(absolute).Trim() : null;
        }

        private static void EnsureFolders()
        {
            EnsureAssetFolder(GeneratedRoot + "/W17");
            EnsureAssetFolder(GeneratedRoot + "/W18");
            EnsureAssetFolder("Assets/VFX/Preview");
        }

        private static void EnsureAssetFolder(string folder)
        {
            var normalized = folder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized)) return;
            var parent = normalized.Substring(0, normalized.LastIndexOf('/'));
            var name = normalized.Substring(normalized.LastIndexOf('/') + 1);
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void WriteObjectArray(SerializedProperty property, UnityEngine.Object[] values)
        {
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++) property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }

        private static Color ColorOf(string html)
        {
            Color value;
            return ColorUtility.TryParseHtmlString(html, out value) ? value : Color.white;
        }

        private static long StableSeed(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (var character in value) { hash ^= character; hash *= 16777619; }
                return hash;
            }
        }

        private static string PreviewBuildHash(string family, IEnumerable<string> manifests)
        {
            var values = manifests.Select(path => path + ":" + (File.Exists(Absolute(path)) ? Hash(File.ReadAllText(Absolute(path))) : "missing"));
            return Hash(CompilerVersion + "|preview|" + family + "|" + string.Join("|", values.ToArray()));
        }

        private static string Canonical(JToken value) { return value.ToString(Formatting.None); }

        private static string Hash(string value)
        {
            using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static string OldPrefabPath(string id) { return "Assets/VFX/Generated/" + id + "/VFX_" + id + ".prefab"; }

        private static Dictionary<string, string> SnapshotProtectedOutputs(IEnumerable<string> assetPaths)
        {
            var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var assetPath in assetPaths)
            {
                var absolute = Absolute(assetPath);
                if (!File.Exists(absolute)) continue;
                snapshot[assetPath] = AssetDatabase.AssetPathToGUID(assetPath) + "|" + Hash(File.ReadAllText(absolute));
            }
            return snapshot;
        }

        private static void AssertProtectedOutputsUnchanged(Dictionary<string, string> before)
        {
            foreach (var item in before)
            {
                var absolute = Absolute(item.Key);
                if (!File.Exists(absolute)) throw new InvalidOperationException("Protected old candidate was removed: " + item.Key);
                var after = AssetDatabase.AssetPathToGUID(item.Key) + "|" + Hash(File.ReadAllText(absolute));
                if (after != item.Value) throw new InvalidOperationException("Protected old candidate changed: " + item.Key);
            }
        }

        private static string Absolute(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
