#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace VFXComposer.Authoring
{
    /// <summary>One-shot S5 authoring and evidence capture. This stays outside formal templates and is safe to rerun.</summary>
    public static class S5TemplateAuthoring
    {
        private const string Root = "Assets/VFX/Templates/2D";
        private const string Textures = Root + "/Textures";
        private const string Materials = Root + "/Materials";
        private const string Prefabs = Root + "/Prefabs";
        private const string Manifests = Root + "/Manifests";
        private const string Preview = "Assets/VFX/Preview/S5_2D_FireballGoldSample.unity";
        private static readonly Color DeepOrange = new Color(1f, .28f, .035f, 1f);
        private static readonly Color Gold = new Color(1f, .68f, .10f, 1f);
        private static readonly Color WarmWhite = new Color(1f, .93f, .62f, 1f);

        [MenuItem("VFX Composer/S5/Build 2D Template Library and Gold Sample")]
        public static void BuildAll()
        {
            EnsureFolders();
            ConfigureTextures();
            var transparent = CreateMaterial("VFX2D_SpriteTransparent", false, Sprite("VFX2D_FireCore.png").texture);
            var emberMaterial = CreateMaterial("VFX2D_Additive_Ember", true, Sprite("VFX2D_Ember.png").texture, true);
            var impactMaterial = CreateMaterial("VFX2D_Additive_Impact", true, Sprite("VFX2D_ImpactStreak.png").texture, true);
            var trailMaterial = CreateMaterial("VFX2D_Additive_Trail", true, Sprite("VFX2D_FireTrail.png").texture, true);
            var launchMaterial = CreateMaterial("VFX2D_Additive_Launch", true, Sprite("VFX2D_LaunchFlash.png").texture, true);
            var shockwaveMaterial = CreateMaterial("VFX2D_Additive_Shockwave", true, Sprite("VFX2D_ShockwaveRing.png").texture, true);
            CreateCore(transparent);
            CreateEmbers(emberMaterial);
            CreateImpact(impactMaterial);
            CreateTrail(trailMaterial);
            CreateLaunch(launchMaterial);
            CreateShockwave(shockwaveMaterial);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            WriteManifests();
            AssetDatabase.Refresh();
            CreateGoldSample();
            CaptureEvidence();
            SetSourceReadability(false);
            AssetDatabase.SaveAssets();
            Debug.Log("S5 formal template library, manifests, Gold Sample and evidence were created.");
        }

        private static void EnsureFolders()
        {
            foreach (var folder in new[] { Root, Materials, Prefabs, Manifests, "Assets/VFX/Preview" })
            {
                if (!AssetDatabase.IsValidFolder(folder)) Directory.CreateDirectory(Path.Combine(Application.dataPath, folder.Substring("Assets/".Length)));
            }
        }

        private static void ConfigureTextures()
        {
            var settings = new Dictionary<string, int>
            {
                { "VFX2D_FireCore.png", 1024 }, { "VFX2D_Ember.png", 512 }, { "VFX2D_ImpactStreak.png", 1024 },
                { "VFX2D_LaunchFlash.png", 512 }, { "VFX2D_ShockwaveRing.png", 1024 }, { "VFX2D_FireTrail.png", 512 }
            };
            foreach (var item in settings)
            {
                var path = Textures + "/" + item.Key;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing S5 source texture: " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 512f;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = item.Value;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.isReadable = true; // Required briefly to compute a non-destructive UV crop for padded source art.
                importer.SaveAndReimport();
            }
        }

        private static void SetSourceReadability(bool readable)
        {
            foreach (var file in Directory.GetFiles(Path.Combine(Application.dataPath, "VFX", "Templates", "2D", "Textures"), "*.png"))
            {
                var assetPath = "Assets/VFX/Templates/2D/Textures/" + Path.GetFileName(file);
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer != null && importer.isReadable != readable) { importer.isReadable = readable; importer.SaveAndReimport(); }
            }
        }

        private static Material CreateMaterial(string name, bool additive, Texture texture = null, bool cropToOpaqueBounds = false)
        {
            var path = Materials + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", additive ? 1f : 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)(additive ? BlendMode.One : BlendMode.SrcAlpha));
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (texture != null)
            {
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
                if (cropToOpaqueBounds) ApplyOpaqueUvCrop(material, texture as Texture2D);
            }
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ApplyOpaqueUvCrop(Material material, Texture2D texture)
        {
            if (texture == null) return;
            var pixels = texture.GetPixels(); var minX = texture.width; var minY = texture.height; var maxX = -1; var maxY = -1;
            for (var y = 0; y < texture.height; y++) for (var x = 0; x < texture.width; x++) if (pixels[y * texture.width + x].a > .02f)
            { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }
            if (maxX < minX || maxY < minY) return;
            const int margin = 3; minX = Mathf.Max(0, minX - margin); minY = Mathf.Max(0, minY - margin); maxX = Mathf.Min(texture.width - 1, maxX + margin); maxY = Mathf.Min(texture.height - 1, maxY + margin);
            var scale = new Vector2((maxX - minX + 1f) / texture.width, (maxY - minY + 1f) / texture.height);
            var offset = new Vector2(minX / (float)texture.width, minY / (float)texture.height);
            if (material.HasProperty("_BaseMap")) { material.SetTextureScale("_BaseMap", scale); material.SetTextureOffset("_BaseMap", offset); }
            if (material.HasProperty("_MainTex")) { material.SetTextureScale("_MainTex", scale); material.SetTextureOffset("_MainTex", offset); }
        }

        private static Sprite Sprite(string textureName) { return AssetDatabase.LoadAssetAtPath<Sprite>(Textures + "/" + textureName); }

        private static void SavePrefab(GameObject root, string file)
        {
            var path = Prefabs + "/" + file + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreateCore(Material material)
        {
            var root = new GameObject("PFT_2D_FireCore");
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = Sprite("VFX2D_FireCore.png"); renderer.sharedMaterial = material; renderer.color = Color.white; renderer.sortingOrder = 20;
            root.transform.localScale = Vector3.one * 1.2f;
            SavePrefab(root, "PFT_2D_FireCore");
        }

        private static ParticleSystem CreateParticleRoot(string name, Material material, Sprite texture, int sortingOrder)
        {
            var root = new GameObject(name);
            var ps = root.AddComponent<ParticleSystem>();
            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard; renderer.sharedMaterial = material; renderer.sortingOrder = sortingOrder;
            renderer.sortingLayerName = "Default";
            var textureSheet = ps.textureSheetAnimation; textureSheet.enabled = false;
            return ps;
        }

        private static void CreateEmbers(Material material)
        {
            var ps = CreateParticleRoot("PFT_2D_Embers", material, Sprite("VFX2D_Ember.png"), 12);
            var main = ps.main; main.loop = true; main.duration = 1f; main.startLifetime = .55f; main.startSpeed = 1.1f; main.startSize = .25f; main.startColor = Color.white; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 48; main.playOnAwake = true;
            var emission = ps.emission; emission.rateOverTime = 18f;
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .38f;
            var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World; velocity.y = -.35f;
            var color = ps.colorOverLifetime; color.enabled = true; color.color = Fade(Color.white);
            SavePrefab(ps.gameObject, "PFT_2D_Embers");
        }

        private static void CreateImpact(Material material)
        {
            var ps = CreateParticleRoot("PFT_2D_FireImpact", material, Sprite("VFX2D_ImpactStreak.png"), 30);
            var main = ps.main; main.loop = false; main.duration = .28f; main.startLifetime = .24f; main.startSpeed = 3.5f; main.startSize = .18f; main.startColor = Color.white; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 48; main.playOnAwake = true;
            var emission = ps.emission; emission.rateOverTime = 0f; emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .05f;
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.velocityScale = .35f; renderer.lengthScale = .65f;
            var color = ps.colorOverLifetime; color.enabled = true; color.color = Fade(Color.white);
            SavePrefab(ps.gameObject, "PFT_2D_FireImpact");
        }

        private static void CreateTrail(Material material)
        {
            var root = new GameObject("PFT_2D_FireTrail");
            var trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material; trail.time = .22f; trail.minVertexDistance = .01f; trail.widthMultiplier = .42f; trail.textureMode = LineTextureMode.Stretch; trail.alignment = LineAlignment.View; trail.numCapVertices = 2; trail.numCornerVertices = 2; trail.sortingOrder = 10;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(.65f, .46f), new Keyframe(1f, 0f));
            trail.colorGradient = Gradient(Gold, new Color(DeepOrange.r, DeepOrange.g, DeepOrange.b, 0f));
            SavePrefab(root, "PFT_2D_FireTrail");
        }

        private static void CreateLaunch(Material material)
        {
            var ps = CreateParticleRoot("PFT_2D_LaunchFlash", material, Sprite("VFX2D_LaunchFlash.png"), 15);
            var main = ps.main; main.loop = false; main.duration = .16f; main.startLifetime = .12f; main.startSpeed = 0f; main.startSize = 1f; main.startColor = Color.white; main.maxParticles = 1; main.playOnAwake = true;
            var emission = ps.emission; emission.rateOverTime = 0f; emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shape = ps.shape; shape.enabled = false;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .35f), new Keyframe(.2f, 1f), new Keyframe(1f, 1.45f)));
            var color = ps.colorOverLifetime; color.enabled = true; color.color = Fade(Color.white);
            SavePrefab(ps.gameObject, "PFT_2D_LaunchFlash");
        }

        private static void CreateShockwave(Material material)
        {
            var ps = CreateParticleRoot("PFT_2D_Shockwave", material, Sprite("VFX2D_ShockwaveRing.png"), 32);
            var main = ps.main; main.loop = false; main.duration = .32f; main.startLifetime = .28f; main.startSpeed = 0f; main.startSize = 1.4f; main.startColor = Color.white; main.maxParticles = 1; main.playOnAwake = true;
            var emission = ps.emission; emission.rateOverTime = 0f; emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shape = ps.shape; shape.enabled = false;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .35f), new Keyframe(.3f, 1.1f), new Keyframe(1f, 2.8f)));
            var color = ps.colorOverLifetime; color.enabled = true; color.color = Fade(Color.white);
            SavePrefab(ps.gameObject, "PFT_2D_Shockwave");
        }

        private static ParticleSystem.MinMaxGradient Fade(Color color) { return new ParticleSystem.MinMaxGradient(Gradient(color, new Color(color.r, color.g, color.b, 0f))); }
        private static Gradient Gradient(Color first, Color last)
        {
            var result = new Gradient();
            result.SetKeys(new[] { new GradientColorKey(first, 0f), new GradientColorKey(last, 1f) }, new[] { new GradientAlphaKey(first.a, 0f), new GradientAlphaKey(last.a, 1f) });
            return result;
        }

        private static void WriteManifests()
        {
            WriteManifest("PFT_2D_FireCore", "energy_body", new [] { P("scale", "float", ".6", "2.4", "1.2", "core.scale") }, 0, 1, 0);
            WriteManifest("PFT_2D_Embers", "secondary_particles", new [] { P("rate", "float", "4", "36", "18", "embers.rate"), P("lifetime", "float", ".25", "1.1", ".55", "embers.lifetime") }, 40, 1, 0);
            WriteManifest("PFT_2D_FireImpact", "impact_burst", new [] { P("count", "integer", "8", "40", "24", "impact.count"), P("speed", "float", "1.5", "6", "3.5", "impact.speed") }, 40, 1, 0);
            WriteManifest("PFT_2D_FireTrail", "motion_trail", new [] { P("time", "float", ".08", ".4", ".22", "trail.time"), P("width", "float", ".12", ".55", ".42", "trail.width") }, 0, 1, 1);
            WriteManifest("PFT_2D_LaunchFlash", "impact_flash", new [] { P("lifetime", "float", ".06", ".22", ".12", "launch.lifetime"), P("size", "float", ".45", "1.8", "1", "launch.size") }, 1, 1, 0);
            WriteManifest("PFT_2D_Shockwave", "shockwave", new [] { P("lifetime", "float", ".12", ".5", ".28", "shockwave.lifetime"), P("endSize", "float", "1.2", "4", "2.8", "shockwave.endSize") }, 1, 1, 0);
        }

        private static string P(string name, string type, string min, string max, string value, string binding) { return string.Format("\"{0}\": {{ \"type\": \"{1}\", \"min\": {2}, \"max\": {3}, \"default\": {4}, \"binding\": \"{5}\" }}", name, type, min, max, value, binding); }
        private static void WriteManifest(string id, string kind, string[] parameters, int particles, int materials, int trails)
        {
            var prefabPath = Prefabs + "/" + id + ".prefab";
            var guid = AssetDatabase.AssetPathToGUID(prefabPath);
            var json = "{\n  \"manifestVersion\": 1,\n  \"templateId\": \"" + id + "\",\n  \"templateVersion\": \"1.0.0\",\n  \"kind\": \"" + kind + "\",\n  \"dimension\": \"2d\",\n  \"assetGuid\": \"" + guid + "\",\n  \"assetPath\": \"" + prefabPath + "\",\n  \"tags\": [\"fire\", \"stylized\", \"2d\"],\n  \"parameters\": { " + string.Join(", ", parameters) + " },\n  \"cost\": { \"estimatedPeakParticles\": " + particles + ", \"materials\": " + materials + ", \"trails\": " + trails + " }\n}";
            File.WriteAllText(Path.Combine(Application.dataPath, Manifests.Substring("Assets/".Length), id + ".manifest.json"), json);
        }

        private static GameObject Load(string name) { return PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + name + ".prefab")) as GameObject; }
        private static void CreateGoldSample()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraGo = new GameObject("GoldSample_OrthographicCamera"); var camera = cameraGo.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 4.5f; camera.transform.position = new Vector3(0, 0, -10); camera.backgroundColor = new Color(.075f, .085f, .105f); camera.clearFlags = CameraClearFlags.SolidColor;
            MakeReferenceLine();
            var launch = Load("PFT_2D_LaunchFlash"); launch.name = "LaunchFlash"; launch.transform.position = new Vector3(-4.5f, 0f, 0f); launch.GetComponent<ParticleSystem>().Simulate(.04f, true, true, true);
            var launchCore = Load("PFT_2D_FireCore"); launchCore.name = "Launch_Core"; launchCore.transform.position = new Vector3(-4.5f, 0f, 0f); launchCore.transform.localScale = Vector3.one * .42f;
            var core = Load("PFT_2D_FireCore"); core.name = "Travel_Core"; core.transform.position = new Vector3(-.6f, .4f, 0f);
            var trail = Load("PFT_2D_FireTrail"); trail.name = "Travel_Trail"; trail.transform.position = new Vector3(-.6f, .4f, 0f); BuildTrailGeometry(trail.GetComponent<TrailRenderer>());
            var embers = Load("PFT_2D_Embers"); embers.name = "Travel_Embers"; embers.transform.position = new Vector3(-.6f, .4f, 0f); embers.GetComponent<ParticleSystem>().Simulate(.18f, true, true, true);
            var impactFlash = Load("PFT_2D_LaunchFlash"); impactFlash.name = "Impact_Flash"; impactFlash.transform.position = new Vector3(3.3f, .15f, 0f); impactFlash.transform.localScale = Vector3.one * 1.25f; impactFlash.GetComponent<ParticleSystem>().Simulate(.035f, true, true, true);
            var impact = Load("PFT_2D_FireImpact"); impact.name = "Impact_Burst"; impact.transform.position = new Vector3(3.3f, .15f, 0f); impact.GetComponent<ParticleSystem>().Simulate(.06f, true, true, true);
            var shock = Load("PFT_2D_Shockwave"); shock.name = "Impact_Shockwave"; shock.transform.position = new Vector3(3.3f, .15f, 0f); shock.GetComponent<ParticleSystem>().Simulate(.08f, true, true, true);
            EditorSceneManager.SaveScene(scene, Preview);
        }

        private static void MakeReferenceLine()
        {
            var root = new GameObject("SizeReference_OneUnit");
            var line = root.AddComponent<LineRenderer>(); line.positionCount = 2; line.SetPositions(new[] { new Vector3(-.5f, -3.2f, 0), new Vector3(.5f, -3.2f, 0) }); line.widthMultiplier = .018f; line.sharedMaterial = CreateMaterial("VFX2D_SpriteTransparent", false); line.startColor = line.endColor = new Color(.65f, .7f, .75f, .65f);
        }

        private static void BuildTrailGeometry(TrailRenderer trail)
        {
            trail.emitting = true; trail.Clear();
            var origin = trail.transform.position;
            trail.AddPositions(new[] { origin + new Vector3(-2.4f, 0, 0), origin + new Vector3(-1.8f, 0, 0), origin + new Vector3(-1.2f, 0, 0), origin + new Vector3(-.6f, 0, 0), origin });
            trail.emitting = false;
        }

        private static void CaptureEvidence()
        {
            var evidence = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "s5-evidence")); Directory.CreateDirectory(evidence);
            CaptureStage(evidence, "travel.png", 2.6f, BuildTravelCapture);
            CaptureSilhouetteCheck(evidence);
            CaptureImpactFrame(evidence, "impact_early.png", .025f, .025f, .025f);
            CaptureImpactFrame(evidence, "impact_mid.png", .065f, .06f, .1f);
            CaptureImpactFrame(evidence, "impact.png", .14f, .12f, .18f);
            CaptureStage(evidence, "launch.png", 1.6f, () =>
            {
                var flash = Load("PFT_2D_LaunchFlash"); flash.GetComponent<ParticleSystem>().Simulate(.04f, true, true, true);
                var core = Load("PFT_2D_FireCore"); core.transform.localScale = Vector3.one * .42f; core.transform.position = Vector3.zero;
            });
        }

        private static void BuildTravelCapture()
        {
            var trail = Load("PFT_2D_FireTrail"); BuildTrailGeometry(trail.GetComponent<TrailRenderer>());
            var core = Load("PFT_2D_FireCore"); core.transform.position = Vector3.zero;
            var embers = Load("PFT_2D_Embers"); embers.transform.position = Vector3.zero; PlaceCaptureEmbers(embers.GetComponent<ParticleSystem>());
        }

        private static void CaptureImpactFrame(string directory, string filename, float flashTime, float burstTime, float shockwaveTime)
        {
            CaptureStage(directory, filename, 2.4f, () =>
            {
                var flash = Load("PFT_2D_LaunchFlash"); flash.name = "ImpactFlash"; flash.GetComponent<ParticleSystem>().Simulate(flashTime, true, true, true);
                var burst = Load("PFT_2D_FireImpact"); burst.name = "ImpactBurst"; burst.GetComponent<ParticleSystem>().Simulate(burstTime, true, true, true);
                var shockwave = Load("PFT_2D_Shockwave"); shockwave.name = "Shockwave"; shockwave.GetComponent<ParticleSystem>().Simulate(shockwaveTime, true, true, true);
            });
        }

        private static void PlaceCaptureEmbers(ParticleSystem embers)
        {
            embers.Simulate(.34f, true, true, true);
            if (embers.particleCount < 6) embers.Emit(6);
            var particles = new ParticleSystem.Particle[embers.main.maxParticles];
            var count = embers.GetParticles(particles);
            for (var index = 0; index < count; index++)
            {
                var particle = particles[index];
                particle.position = new Vector3(-.65f - index * .3f, index % 2 == 0 ? .52f : -.44f, 0f);
                particle.startColor = Color.white; particle.startSize = .25f; particle.remainingLifetime = .42f; particle.startLifetime = .55f;
                particles[index] = particle;
            }
            embers.SetParticles(particles, count);
        }

        private static void CaptureStage(string directory, string filename, float size, Action buildStage)
        {
            var texture = RenderStage(size, new Color(.075f, .085f, .105f), buildStage);
            File.WriteAllBytes(Path.Combine(directory, filename), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static void CaptureSilhouetteCheck(string directory)
        {
            var backgrounds = new[] { new Color(.005f, .005f, .005f), new Color(.30f, .32f, .35f), new Color(.88f, .88f, .84f) };
            var sizes = new[] { 2.6f, 5.2f };
            var cells = new Color[sizes.Length * backgrounds.Length][];
            for (var row = 0; row < sizes.Length; row++) for (var column = 0; column < backgrounds.Length; column++)
            {
                var cell = RenderStage(sizes[row], backgrounds[column], BuildTravelCapture);
                cells[row * backgrounds.Length + column] = cell.GetPixels();
                UnityEngine.Object.DestroyImmediate(cell);
            }
            var composite = new Texture2D(768 * 3, 512 * 2, TextureFormat.RGBA32, false);
            for (var row = 0; row < sizes.Length; row++) for (var column = 0; column < backgrounds.Length; column++) composite.SetPixels(column * 768, (sizes.Length - 1 - row) * 512, 768, 512, cells[row * backgrounds.Length + column]);
            composite.Apply(); File.WriteAllBytes(Path.Combine(directory, "silhouette-check.png"), composite.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(composite);
        }

        private static Texture2D RenderStage(float size, Color background, Action buildStage)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraGo = new GameObject("EvidenceCamera"); var camera = cameraGo.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = size; camera.transform.position = new Vector3(0f, 0f, -10f); camera.backgroundColor = background; camera.clearFlags = CameraClearFlags.SolidColor;
            buildStage();
            camera.targetTexture = new RenderTexture(768, 512, 24, RenderTextureFormat.ARGB32); camera.Render();
            var active = RenderTexture.active; RenderTexture.active = camera.targetTexture; var texture = new Texture2D(768, 512, TextureFormat.RGBA32, false); texture.ReadPixels(new Rect(0, 0, 768, 512), 0, 0); texture.Apply();
            RenderTexture.active = active; var target = camera.targetTexture; camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target);
            return texture;
        }
    }
}
#endif
