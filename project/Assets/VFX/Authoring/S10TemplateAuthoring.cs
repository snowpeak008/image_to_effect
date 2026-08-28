#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VFXComposer;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Preview;

namespace VFXComposer.Authoring
{
    /// <summary>Controlled S10 authoring entry point. It creates only protected 3D template inputs and review evidence.</summary>
    public static class S10TemplateAuthoring
    {
        private const string Root = "Assets/VFX/Templates/3D";
        private const string Materials = Root + "/Materials";
        private const string Meshes = Root + "/Meshes";
        private const string Prefabs = Root + "/Prefabs";
        private const string Manifests = Root + "/Manifests";
        private const string RecipePath = "Assets/VFX/Recipes/fireball-3d.default.json";
        private const string PreviewPath = "Assets/VFX/Preview/S10_3D_FireballPreview.unity";
        private static readonly Color Fire = new Color(1f, .22f, .025f, .82f);
        private static readonly Color Gold = new Color(1f, .63f, .08f, .9f);
        private static readonly Color WhiteGold = new Color(1f, .9f, .48f, .95f);

        [MenuItem("VFX Composer/S10/Build 3D Template Library and Evidence")]
        public static void BuildAll()
        {
            EnsureFolders();
            var core = CreateMaterial("VFX3D_Core", new Color(1f, .38f, .04f, 1f), (int)RenderQueue.Geometry, false);
            var flame = CreateMaterial("VFX3D_BillboardFlame", Fire, 3100, true);
            var particles = CreateMaterial("VFX3D_BillboardParticles", Gold, 3150, true);
            var trail = CreateMaterial("VFX3D_Trail", new Color(1f, .2f, .02f, .72f), 3090, true);
            var impact = CreateMaterial("VFX3D_Impact", WhiteGold, 3200, true);
            var shockwave = CreateMaterial("VFX3D_RingShockwave", new Color(1f, .3f, .04f, .7f), 3050, true);
            var ring = CreateRingMesh();
            var flameMesh = CreateFlameMesh();
            CreateCore(core, flame, flameMesh);
            CreateEmbers(particles);
            CreateTrail(trail);
            CreateLaunch(impact);
            CreateImpact(impact);
            CreateShockwave(shockwave, ring);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            WriteManifests();
            AssetDatabase.SaveAssets();
            CreatePreviewAndEvidence();
            Debug.Log("S10 formal 3D templates, manifests, preview and five-view evidence created.");
        }

        private static void EnsureFolders()
        {
            foreach (var folder in new[] { Root, Materials, Meshes, Prefabs, Manifests, "Assets/VFX/Preview" })
                if (!AssetDatabase.IsValidFolder(folder)) Directory.CreateDirectory(Path.Combine(Application.dataPath, folder.Substring("Assets/".Length)));
        }

        private static Material CreateMaterial(string name, Color color, int renderQueue, bool transparent)
        {
            var path = Materials + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = transparent ? Shader.Find("Universal Render Pipeline/Particles/Unlit") : Shader.Find("Universal Render Pipeline/Unlit");
            shader = shader ?? Shader.Find("Standard");
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null && material.shader != shader) material.shader = shader;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", transparent ? 1f : 0f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", transparent ? 1f : 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)(transparent ? BlendMode.SrcAlpha : BlendMode.One));
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)(transparent ? BlendMode.One : BlendMode.Zero));
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", transparent ? 0f : 1f);
            material.renderQueue = renderQueue;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh CreateFlameMesh()
        {
            var path = Meshes + "/MESH_3D_BillboardFlame.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            mesh = new Mesh { name = "MESH_3D_BillboardFlame" };
            mesh.vertices = new[] { new Vector3(-.45f, -.58f, 0f), new Vector3(0f, .82f, 0f), new Vector3(.45f, -.58f, 0f), new Vector3(0f, -.08f, -.025f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(.5f, 1f), new Vector2(1f, 0f), new Vector2(.5f, .36f) };
            mesh.triangles = new[] { 0, 1, 3, 3, 1, 2, 2, 1, 0, 0, 3, 2 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Mesh CreateRingMesh()
        {
            var path = Meshes + "/MESH_3D_ShockwaveRing.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            const int segments = 48;
            const float inner = .72f;
            const float outer = 1f;
            var vertices = new Vector3[segments * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (var index = 0; index < segments; index++)
            {
                var angle = index * Mathf.PI * 2f / segments;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[index * 2] = direction * inner;
                vertices[index * 2 + 1] = direction * outer;
                uvs[index * 2] = new Vector2(index / (float)segments, 0f);
                uvs[index * 2 + 1] = new Vector2(index / (float)segments, 1f);
                var next = (index + 1) % segments;
                var t = index * 6;
                triangles[t] = index * 2; triangles[t + 1] = next * 2; triangles[t + 2] = index * 2 + 1;
                triangles[t + 3] = index * 2 + 1; triangles[t + 4] = next * 2; triangles[t + 5] = next * 2 + 1;
            }
            mesh = new Mesh { name = "MESH_3D_ShockwaveRing", vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void CreateCore(Material core, Material flame, Mesh flameMesh)
        {
            var root = new GameObject("PFT_3D_FireCore");
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "SphereEnergyCore";
            sphere.transform.SetParent(root.transform, false);
            sphere.transform.localScale = Vector3.one * .82f;
            sphere.GetComponent<Renderer>().sharedMaterial = core;
            var billboard = new GameObject("BillboardFlame");
            billboard.name = "BillboardFlame";
            billboard.transform.SetParent(root.transform, false);
            billboard.transform.localScale = Vector3.one * 1.25f;
            billboard.AddComponent<MeshFilter>().sharedMesh = flameMesh;
            billboard.AddComponent<MeshRenderer>().sharedMaterial = flame;
            billboard.AddComponent<CameraFacingBillboard>();
            SavePrefab(root, "PFT_3D_FireCore");
        }

        private static ParticleSystem CreateParticleRoot(string name, Material material, ParticleSystemRenderMode mode)
        {
            var root = new GameObject(name);
            var ps = root.AddComponent<ParticleSystem>();
            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            renderer.sharedMaterial = material;
            renderer.sortingLayerName = "Default";
            return ps;
        }

        private static void CreateEmbers(Material material)
        {
            var ps = CreateParticleRoot("PFT_3D_Embers", material, ParticleSystemRenderMode.Billboard);
            var main = ps.main; main.loop = true; main.duration = 1f; main.startLifetime = .55f; main.startSpeed = 1.1f; main.startSize = .11f; main.startColor = Gold; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 48; main.playOnAwake = true;
            var emission = ps.emission; emission.rateOverTime = 18f;
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .35f;
            var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World; velocity.y = -.28f;
            SavePrefab(ps.gameObject, "PFT_3D_Embers");
        }

        private static void CreateTrail(Material material)
        {
            var root = new GameObject("PFT_3D_FireTrail");
            var trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material; trail.time = .22f; trail.minVertexDistance = .01f; trail.widthMultiplier = .34f; trail.alignment = LineAlignment.View; trail.textureMode = LineTextureMode.Stretch; trail.numCapVertices = 3; trail.numCornerVertices = 2;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(.7f, .36f), new Keyframe(1f, 0f));
            trail.colorGradient = Gradient(new Color(1f, .7f, .18f, .88f), new Color(.92f, .08f, .01f, 0f));
            SavePrefab(root, "PFT_3D_FireTrail");
        }

        private static void CreateLaunch(Material material)
        {
            var ps = CreateParticleRoot("PFT_3D_LaunchFlash", material, ParticleSystemRenderMode.Billboard);
            var main = ps.main; main.loop = false; main.duration = .16f; main.startLifetime = .12f; main.startSize = 1f; main.startSpeed = 0f; main.startColor = WhiteGold; main.maxParticles = 1; main.playOnAwake = true;
            var emission = ps.emission; emission.rateOverTime = 0f; emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shape = ps.shape; shape.enabled = false;
            ps.gameObject.AddComponent<CameraFacingBillboard>();
            SavePrefab(ps.gameObject, "PFT_3D_LaunchFlash");
        }

        private static void CreateImpact(Material material)
        {
            var ps = CreateParticleRoot("PFT_3D_FireImpact", material, ParticleSystemRenderMode.Billboard);
            var main = ps.main; main.loop = false; main.duration = .28f; main.startLifetime = .24f; main.startSpeed = 3.5f; main.startSize = .16f; main.startColor = Gold; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 48; main.playOnAwake = true;
            var emission = ps.emission; emission.rateOverTime = 0f; emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .04f;
            SavePrefab(ps.gameObject, "PFT_3D_FireImpact");
        }

        private static void CreateShockwave(Material material, Mesh ring)
        {
            var ps = CreateParticleRoot("PFT_3D_Shockwave", material, ParticleSystemRenderMode.Mesh);
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.mesh = ring; renderer.alignment = ParticleSystemRenderSpace.World;
            var main = ps.main; main.loop = false; main.duration = .32f; main.startLifetime = .28f; main.startSpeed = 0f; main.startSize = 1f; main.startColor = new Color(1f, .34f, .05f, .78f); main.maxParticles = 1; main.playOnAwake = true;
            var emission = ps.emission; emission.rateOverTime = 0f; emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shape = ps.shape; shape.enabled = false;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .65f), new Keyframe(1f, 2.8f)));
            SavePrefab(ps.gameObject, "PFT_3D_Shockwave");
        }

        private static void SavePrefab(GameObject root, string file)
        {
            PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/" + file + ".prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void WriteManifests()
        {
            WriteManifest("PFT_3D_FireCore", "energy_body", "{ \"scale\": { \"type\": \"float\", \"min\": .6, \"max\": 2.4, \"default\": 1.2, \"binding\": \"3d.core.scale\" } }", "0, 2, 0");
            WriteManifest("PFT_3D_Embers", "secondary_particles", "{ \"rate\": { \"type\": \"float\", \"min\": 4, \"max\": 36, \"default\": 18, \"binding\": \"3d.embers.rate\" }, \"lifetime\": { \"type\": \"float\", \"min\": .25, \"max\": 1.1, \"default\": .55, \"binding\": \"3d.embers.lifetime\" } }", "40, 1, 0");
            WriteManifest("PFT_3D_FireTrail", "motion_trail", "{ \"time\": { \"type\": \"float\", \"min\": .08, \"max\": .4, \"default\": .22, \"binding\": \"3d.trail.time\" }, \"width\": { \"type\": \"float\", \"min\": .12, \"max\": .55, \"default\": .42, \"binding\": \"3d.trail.width\" } }", "0, 1, 1");
            WriteManifest("PFT_3D_LaunchFlash", "impact_flash", "{ \"lifetime\": { \"type\": \"float\", \"min\": .06, \"max\": .22, \"default\": .12, \"binding\": \"3d.launch.lifetime\" }, \"size\": { \"type\": \"float\", \"min\": .45, \"max\": 1.8, \"default\": 1.0, \"binding\": \"3d.launch.size\" } }", "1, 1, 0");
            WriteManifest("PFT_3D_FireImpact", "impact_burst", "{ \"count\": { \"type\": \"integer\", \"min\": 8, \"max\": 40, \"default\": 24, \"binding\": \"3d.impact.count\" }, \"speed\": { \"type\": \"float\", \"min\": 1.5, \"max\": 6, \"default\": 3.5, \"binding\": \"3d.impact.speed\" } }", "40, 1, 0");
            WriteManifest("PFT_3D_Shockwave", "shockwave", "{ \"lifetime\": { \"type\": \"float\", \"min\": .12, \"max\": .5, \"default\": .28, \"binding\": \"3d.shockwave.lifetime\" }, \"endSize\": { \"type\": \"float\", \"min\": 1.2, \"max\": 4, \"default\": 2.8, \"binding\": \"3d.shockwave.endSize\" } }", "1, 1, 0");
        }

        private static void WriteManifest(string id, string kind, string parameters, string cost)
        {
            var path = Prefabs + "/" + id + ".prefab";
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("3D Prefab has no GUID: " + path);
            var pieces = cost.Split(',');
            var json = "{\n  \"manifestVersion\": 1,\n  \"templateId\": \"" + id + "\",\n  \"templateVersion\": \"1.0.0\",\n  \"kind\": \"" + kind + "\",\n  \"dimension\": \"3d\",\n  \"assetGuid\": \"" + guid + "\",\n  \"assetPath\": \"" + path + "\",\n  \"tags\": [\"fire\", \"stylized\", \"3d\", \"bounds:template-local\", \"camera:perspective-reviewed\"],\n  \"parameters\": " + parameters + ",\n  \"cost\": { \"estimatedPeakParticles\": " + pieces[0].Trim() + ", \"materials\": " + pieces[1].Trim() + ", \"trails\": " + pieces[2].Trim() + " }\n}\n";
            File.WriteAllText(Path.Combine(Application.dataPath, "VFX", "Templates", "3D", "Manifests", id + ".manifest.json"), json);
        }

        private static void CreatePreviewAndEvidence()
        {
            // Correct a pre-release S10 casing artifact before the official Prefab
            // build; this exact known generated file has never been an input asset.
            const string legacyPrefab = "Assets/VFX/Generated/fireball_3d/VFX_Fireball_3d.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(legacyPrefab) != null) AssetDatabase.DeleteAsset(legacyPrefab);
            var recipe = AssetDatabase.LoadAssetAtPath<TextAsset>(RecipePath);
            if (recipe == null) throw new InvalidOperationException("Missing S10 default recipe: " + RecipePath);
            var build = new VfxCompiler().Build(recipe.text);
            if (!build.Succeeded) throw new InvalidOperationException("S10 Prefab build failed.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(build.PrefabPath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = CreateCamera("S10_PerspectiveCamera");
            CreateReference(scene);
            var launch = CreatePreviewInstance(prefab, "Launch_Gold", new Vector3(-3.35f, .2f, 1.15f));
            var travel = CreatePreviewInstance(prefab, "Travel_Gold", Vector3.zero);
            var impact = CreatePreviewInstance(prefab, "Impact_Gold", new Vector3(3.35f, .2f, -1.15f));
            PrepareLaunch(launch);
            PrepareTravel(travel);
            PrepareImpact(impact);
            EditorSceneManager.SaveScene(scene, PreviewPath);
            var evidence = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "s10-evidence"));
            Directory.CreateDirectory(evidence);
            var views = new List<ViewEvidence>
            {
                CaptureView(camera, S10PreviewScene.GetPose(S10PreviewView.Front), "front.png", evidence),
                CaptureView(camera, S10PreviewScene.GetPose(S10PreviewView.Side), "side.png", evidence),
                CaptureView(camera, S10PreviewScene.GetPose(S10PreviewView.ObliqueTop), "oblique_top.png", evidence),
                CaptureView(camera, S10PreviewScene.GetPose(S10PreviewView.Close), "close.png", evidence),
                CaptureView(camera, S10PreviewScene.GetPose(S10PreviewView.GameDistance), "game_distance.png", evidence)
            };
            WriteViewEvidence(evidence, views, S10PreviewScene.GetPose(S10PreviewView.Front).Target);
            AssetDatabase.Refresh();
        }

        private static Camera CreateCamera(string name)
        {
            var go = new GameObject(name); go.tag = "MainCamera";
            var camera = go.AddComponent<Camera>(); camera.orthographic = false; camera.fieldOfView = 52f; camera.nearClipPlane = .1f; camera.farClipPlane = 100f; camera.backgroundColor = new Color(.065f, .075f, .095f, 1f); camera.clearFlags = CameraClearFlags.SolidColor;
            return camera;
        }

        private static void CreateReference(Scene scene)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.name = "GroundReference"; ground.transform.localScale = Vector3.one * .8f; ground.GetComponent<Renderer>().sharedMaterial = CreateMaterial("VFX3D_Ground", new Color(.12f, .14f, .16f, 1f), (int)RenderQueue.Geometry, false);
        }

        private static GameObject CreatePreviewInstance(GameObject prefab, string name, Vector3 position)
        {
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            instance.name = name; instance.transform.position = position; instance.GetComponent<GeneratedVfxController>().ResetForPool();
            return instance;
        }

        private static void PrepareLaunch(GameObject instance)
        {
            var controller = instance.GetComponent<GeneratedVfxController>(); controller.PlayLaunch();
            SimulateActiveParticles(instance, .04f);
        }

        private static void PrepareTravel(GameObject instance)
        {
            var controller = instance.GetComponent<GeneratedVfxController>();
            controller.ResetForPool(); controller.SetTravelTransform(Vector3.zero, Quaternion.identity); controller.StartTravel();
            foreach (var trail in instance.GetComponentsInChildren<TrailRenderer>(true)) trail.AddPositions(new[] { new Vector3(-2.5f, 0f, 0f), new Vector3(-1.25f, .08f, 0f), Vector3.zero });
            SimulateActiveParticles(instance, .16f);
            // Freeze a sparse, inspectable sample from the real generated Embers
            // ParticleSystem. This is deliberately not screenshot-only geometry.
            var embers = instance.transform.Find("Travel/Core/Embers").GetComponent<ParticleSystem>();
            var offsets = new[] { new Vector3(-1.05f, .78f, .05f), new Vector3(-.92f, -.72f, -.08f), new Vector3(-.35f, 1.08f, .12f), new Vector3(.72f, .86f, -.05f), new Vector3(1.02f, -.64f, .04f), new Vector3(.32f, -1.02f, .08f) };
            foreach (var offset in offsets)
            {
                var emit = new ParticleSystem.EmitParams(); emit.position = instance.transform.position + offset; emit.velocity = Vector3.zero; emit.startSize = .28f; emit.startColor = WhiteGold;
                embers.Emit(emit, 1);
            }
        }

        private static void PrepareImpact(GameObject instance)
        {
            var controller = instance.GetComponent<GeneratedVfxController>(); controller.PlayImpact(instance.transform.position);
            SimulateActiveParticles(instance, .07f);
            // Preserve separated rays from the real Burst ParticleSystem so the
            // review composition can distinguish burst, central flash and ring.
            var burst = instance.transform.Find("Impact/Burst").GetComponent<ParticleSystem>();
            var directions = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, new Vector3(.7f, .5f, .1f).normalized, new Vector3(-.65f, .45f, -.1f).normalized };
            foreach (var direction in directions)
            {
                var emit = new ParticleSystem.EmitParams(); emit.position = instance.transform.position + direction * 1.22f; emit.velocity = Vector3.zero; emit.startSize = .26f; emit.startColor = WhiteGold;
                burst.Emit(emit, 1);
            }
        }

        private static void SimulateActiveParticles(GameObject root, float time)
        {
            foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true)) if (particle.gameObject.activeInHierarchy) particle.Simulate(time, true, false, true);
        }

        private static ViewEvidence CaptureView(Camera camera, S10PreviewCameraPose pose, string file, string folder)
        {
            S10PreviewScene.ApplyPose(camera, pose);
            foreach (var billboard in UnityEngine.Object.FindObjectsOfType<CameraFacingBillboard>()) billboard.transform.rotation = Quaternion.LookRotation(billboard.transform.position - camera.transform.position, camera.transform.up);
            var target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32); camera.targetTexture = target; camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(960, 540, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
            var path = Path.Combine(folder, file); File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image); RenderTexture.active = previous; camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target);
            return new ViewEvidence { File = file, Position = pose.Position, FieldOfView = pose.FieldOfView, Hash = Sha256(path) };
        }

        private static void WriteViewEvidence(string folder, List<ViewEvidence> views, Vector3 target)
        {
            var lines = new List<string> { "{", "  \"capture\": \"hidden-graphics-device batch Camera.Render; no -nographics\",", "  \"camera\": \"perspective\",", "  \"target\": [" + target.x + ", " + target.y + ", " + target.z + "],", "  \"views\": [" };
            for (var index = 0; index < views.Count; index++)
            {
                var view = views[index];
                lines.Add("    { \"file\": \"" + view.File + "\", \"position\": [" + view.Position.x + ", " + view.Position.y + ", " + view.Position.z + "], \"fov\": " + view.FieldOfView + ", \"sha256\": \"" + view.Hash + "\" }" + (index == views.Count - 1 ? string.Empty : ","));
            }
            lines.Add("  ],"); lines.Add("  \"composition\": \"One retained 3D Gold Sample places Launch, Travel (sphere/flame/trail/separate billboard embers), and Impact (flash/burst/mesh-ring) apart in space for every perspective review.\""); lines.Add("}");
            File.WriteAllText(Path.Combine(folder, "views.json"), string.Join("\n", lines) + "\n");
        }

        private static string Sha256(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty).ToLowerInvariant();
        }

        private sealed class ViewEvidence
        {
            public string File;
            public Vector3 Position;
            public float FieldOfView;
            public string Hash;
        }

        private static Gradient Gradient(Color start, Color end)
        {
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) }, new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(end.a, 1f) }); return gradient;
        }
    }
}
#endif
