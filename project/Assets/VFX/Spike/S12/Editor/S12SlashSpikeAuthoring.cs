#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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

namespace VFXComposer.Spike.S12
{
    /// <summary>Disposable S12 render-order spike. Everything it creates lives below Assets/VFX/Spike/S12 or docs/spike-notes/s12-evidence.</summary>
    public static class S12SlashSpikeAuthoring
    {
        public const string Root = "Assets/VFX/Spike/S12";
        public const string MeshRoot = Root + "/Meshes";
        public const string MaterialRoot = Root + "/Materials";
        public const string ScenePath = Root + "/S12_SlashSpikePreview.unity";
        public const string EvidenceRelativePath = "docs/spike-notes/s12-evidence";
        private const int Width = 960;
        private const int Height = 540;
        private static readonly Color Orange = new Color(1f, .18f, .015f, .84f);
        private static readonly Color Red = new Color(.82f, .025f, .02f, .42f);
        private static readonly Color YellowWhite = new Color(1f, .92f, .38f, .98f);
        private static readonly Color Spark = new Color(1f, .52f, .06f, .94f);
        private static readonly PhaseTiming[] PhaseTimings = { new PhaseTiming("anticipation", .00f, .04f), new PhaseTiming("primary_arc", .04f, .16f), new PhaseTiming("afterimage", .12f, .18f), new PhaseTiming("sparks", .14f, .22f), new PhaseTiming("dissipation", .20f, .25f) };

        public static string EvidencePath { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", EvidenceRelativePath)); } }

        [MenuItem("VFX Composer/S12 Spike/Build Render-Order Spike and Evidence")]
        public static void BuildAll()
        {
            EnsureFolders();
            var after = CreateMaterial("S12_Afterimage", new Color(Red.r, Red.g, Red.b, .68f), 3070);
            var body = CreateMaterial("S12_ArcBody", Orange, 3100);
            var edge = CreateMaterial("S12_InnerBlade", YellowWhite, 3130);
            var anticipation = CreateMaterial("S12_Anticipation", new Color(1f, .46f, .05f, .70f), 3120);
            var sparks = CreateMaterial("S12_Sparks", Spark, 3150);
            var dissipation = CreateMaterial("S12_Dissipation", new Color(1f, .16f, .01f, .38f), 3050);
            var primary = CreateArcMesh("MESH_S12_PrimaryArc", 0f, 1f, .23f, 0f, 0f);
            var inner = CreateArcMesh("MESH_S12_InnerBlade", .02f, .98f, .062f, -.045f, .02f);
            var echoA = CreateArcMesh("MESH_S12_AfterimageA", .10f, .88f, .15f, .07f, .08f);
            var echoB = CreateArcMesh("MESH_S12_AfterimageB", .18f, .75f, .09f, .13f, .15f);
            var lead = CreateArcMesh("MESH_S12_Anticipation", 0f, .25f, .055f, -.02f, -.04f);
            var diamond = CreateDiamondMesh();
            CreateScene(primary, inner, echoA, echoB, lead, diamond, after, body, edge, anticipation, sparks, dissipation);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            CaptureEvidence();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("S12 isolated slash render-order spike and evidence created. No formal Templates, Recipes or Generated assets were touched.");
        }

        public static void BuildAllBatch() { BuildAll(); }

        public static void ValidateForTests()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(ScenePath);
            if (root == null) throw new InvalidOperationException("S12 spike preview scene is missing. Run BuildAll first.");
        }

        private static void EnsureFolders()
        {
            foreach (var folder in new[] { Root, MeshRoot, MaterialRoot, Root + "/Editor" })
                if (!AssetDatabase.IsValidFolder(folder)) Directory.CreateDirectory(Path.Combine(Application.dataPath, folder.Substring("Assets/".Length)));
            Directory.CreateDirectory(EvidencePath);
        }

        private static Material CreateMaterial(string name, Color color, int queue)
        {
            var path = MaterialRoot + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No usable URP/Standard shader was found for S12 spike material.");
            if (material == null) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); }
            else material.shader = shader;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 1f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.One);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            // URP Particle Unlit owns its transparent base queue; the explicit offset is the reviewed ordering mechanism.
            if (material.HasProperty("_QueueOffset")) material.SetFloat("_QueueOffset", queue - 3000);
            material.renderQueue = queue;
            EditorUtility.SetDirty(material);
            return material;
        }

        // A shallow extrusion and curved Z offset make this a real 3D ribbon, rather than a single camera-facing card.
        private static Mesh CreateArcMesh(string name, float start, float end, float width, float sideOffset, float zOffset)
        {
            var path = MeshRoot + "/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) AssetDatabase.DeleteAsset(path);
            const int segments = 24;
            const float thickness = .042f;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (var i = 0; i <= segments; i++)
            {
                var t = Mathf.Lerp(start, end, i / (float)segments);
                var localT = i / (float)segments;
                var center = ArcPoint(t) + ArcNormal(t) * sideOffset + Vector3.forward * zOffset;
                var normal = ArcNormal(t);
                var taperedWidth = width * (.18f + .82f * Mathf.Sin(localT * Mathf.PI));
                var left = center - normal * taperedWidth * .5f;
                var right = center + normal * taperedWidth * .5f;
                vertices.Add(left + Vector3.back * thickness * .5f); vertices.Add(right + Vector3.back * thickness * .5f);
                vertices.Add(left + Vector3.forward * thickness * .5f); vertices.Add(right + Vector3.forward * thickness * .5f);
                var u = i / (float)segments;
                uvs.Add(new Vector2(u, 0)); uvs.Add(new Vector2(u, 1)); uvs.Add(new Vector2(u, 0)); uvs.Add(new Vector2(u, 1));
            }
            var triangles = new List<int>();
            for (var i = 0; i < segments; i++)
            {
                var a = i * 4; var b = (i + 1) * 4;
                AddQuad(triangles, a, b, a + 1, b + 1);       // rear
                AddQuad(triangles, a + 2, a + 3, b + 2, b + 3); // front
                AddQuad(triangles, a, a + 2, b, b + 2);         // left rim
                AddQuad(triangles, a + 1, b + 1, a + 3, b + 3); // right rim
            }
            mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0, true); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void AddQuad(List<int> values, int a, int b, int c, int d)
        {
            values.Add(a); values.Add(b); values.Add(c); values.Add(c); values.Add(b); values.Add(d);
        }

        private static Vector3 ArcPoint(float t)
        {
            // A rising scimitar: its leading tip keeps climbing toward upper-right instead of ending in a uniform upside-down U.
            return new Vector3(Mathf.Lerp(-1.28f, 1.30f, t) + .10f * Mathf.Sin(t * Mathf.PI), -.88f + 2.20f * t + .42f * Mathf.Sin(t * Mathf.PI), .18f * Mathf.Sin(t * Mathf.PI));
        }

        private static Mesh CreateDiamondMesh()
        {
            var path = MeshRoot + "/MESH_S12_DiamondSpark.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) AssetDatabase.DeleteAsset(path);
            mesh = new Mesh { name = "MESH_S12_DiamondSpark" };
            mesh.vertices = new[] { new Vector3(0f, .07f, 0f), new Vector3(.045f, 0f, 0f), new Vector3(0f, -.07f, 0f), new Vector3(-.045f, 0f, 0f), new Vector3(0f, .07f, .016f), new Vector3(.045f, 0f, .016f), new Vector3(0f, -.07f, .016f), new Vector3(-.045f, 0f, .016f) };
            mesh.triangles = new[] { 0, 1, 3, 3, 1, 2, 4, 7, 5, 5, 7, 6 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }

        private static Vector3 ArcNormal(float t)
        {
            const float epsilon = .002f;
            var tangent = (ArcPoint(Mathf.Min(1f, t + epsilon)) - ArcPoint(Mathf.Max(0f, t - epsilon))).normalized;
            return Vector3.Cross(Vector3.forward, tangent).normalized;
        }

        private static void CreateScene(Mesh primary, Mesh inner, Mesh echoA, Mesh echoB, Mesh lead, Mesh diamond, Material after, Material body, Material edge, Material anticipation, Material sparks, Material dissipation)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraGo = new GameObject("S12_SpikeCamera"); cameraGo.tag = "MainCamera"; var camera = cameraGo.AddComponent<Camera>(); camera.orthographic = false; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f, .17f, .19f); camera.allowHDR = false; camera.allowMSAA = false;
            var lightGo = new GameObject("S12_ReviewLight"); var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = .25f; light.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
            var root = new GameObject("S12_SlashSpikeRoot");
            CreateScaleReference(diamond, root.transform, CreateMaterial("S12_ScaleReference", new Color(.55f, .61f, .67f, .78f), 3020));
            var anticipationRoot = CreatePhase(root.transform, "anticipation", .00f, .04f);
            AddMesh(anticipationRoot.transform, "AnticipationGlint", lead, anticipation);
            var primaryRoot = CreatePhase(root.transform, "primary_arc", .04f, .16f);
            AddMesh(primaryRoot.transform, "PrimaryArcBody", primary, body);
            AddMesh(primaryRoot.transform, "YellowWhiteInnerBlade", inner, edge);
            var afterRoot = CreatePhase(root.transform, "afterimage", .12f, .18f);
            AddMesh(afterRoot.transform, "RedAfterimageA", echoA, after);
            AddMesh(afterRoot.transform, "RedAfterimageB", echoB, after);
            var sparkRoot = CreatePhase(root.transform, "sparks", .14f, .22f);
            CreateParticles(sparkRoot.transform, "SeparatedDiamondSparks", sparks, true);
            AddStaticSparks(sparkRoot.transform, diamond, sparks, new[] { new Vector3(-.22f, -.24f, .04f), new Vector3(.08f, .24f, .08f), new Vector3(.38f, .56f, .12f), new Vector3(.70f, .20f, -.02f), new Vector3(.98f, .72f, .10f), new Vector3(-.47f, .48f, .04f), new Vector3(1.16f, -.02f, .04f), new Vector3(.30f, 1.05f, -.04f) }, 1f);
            var dissRoot = CreatePhase(root.transform, "dissipation", .20f, .25f);
            CreateParticles(dissRoot.transform, "DissipationMotes", dissipation, false);
            AddStaticSparks(dissRoot.transform, diamond, dissipation, new[] { new Vector3(-.28f, .14f, .06f), new Vector3(.18f, .42f, .08f), new Vector3(.62f, .23f, .04f), new Vector3(.92f, .55f, .07f), new Vector3(1.15f, .26f, -.02f) }, .62f);
            SetFrame(root, "combined");
            ApplyPose(camera, Pose("front"));
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static GameObject CreatePhase(Transform parent, string id, float start, float duration)
        {
            var phase = new GameObject(id); phase.transform.SetParent(parent, false);
            return phase;
        }

        private static void AddMesh(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static ParticleSystem CreateParticles(Transform parent, string name, Material material, bool sparks)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>(); var main = ps.main; main.loop = false; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.Local; main.maxParticles = sparks ? 24 : 12; main.startLifetime = sparks ? .25f : .2f; main.startSize = sparks ? .095f : .075f; main.startColor = sparks ? Spark : new Color(1f, .18f, .02f, .5f); main.startSpeed = 0f;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Billboard; renderer.sharedMaterial = material; renderer.sortMode = ParticleSystemSortMode.OldestInFront; renderer.localBounds = new Bounds(new Vector3(.35f, .38f, .06f), new Vector3(3.1f, 2.4f, .8f));
            return ps;
        }

        private static void AddStaticSparks(Transform parent, Mesh diamond, Material material, IEnumerable<Vector3> offsets, float scale)
        {
            var index = 0;
            foreach (var offset in offsets)
            {
                var go = new GameObject("Spark_" + index++); go.transform.SetParent(parent, false); go.transform.localPosition = offset; go.transform.localRotation = Quaternion.Euler(0f, 0f, 45f); go.transform.localScale = Vector3.one * scale; go.AddComponent<MeshFilter>().sharedMesh = diamond; go.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
        }

        private static void CreateScaleReference(Mesh diamond, Transform parent, Material material)
        {
            var marker = new GameObject("ScaleReference_1_8m"); marker.transform.SetParent(parent, false); marker.transform.localPosition = new Vector3(-1.82f, .02f, .22f);
            AddScaleBar(marker.transform, "Height_1_8m", new Vector3(0f, .9f, 0f), new Vector3(.035f, 1.8f, .035f), material);
            AddScaleBar(marker.transform, "BaseTick", new Vector3(0f, 0f, 0f), new Vector3(.32f, .025f, .025f), material);
            AddScaleBar(marker.transform, "TopTick", new Vector3(0f, 1.8f, 0f), new Vector3(.32f, .025f, .025f), material);
            var head = new GameObject("ReferenceHead"); head.transform.SetParent(marker.transform, false); head.transform.localPosition = new Vector3(0f, 1.63f, 0f); head.transform.localScale = Vector3.one * .40f; head.AddComponent<MeshFilter>().sharedMesh = diamond; head.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void AddScaleBar(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = material;
        }

        public static void ApplyTime(GameObject root, float time)
        {
            foreach (var timing in PhaseTimings)
            {
                var phase = root.transform.Find(timing.Id); if (phase == null) throw new InvalidOperationException("Missing S12 spike phase: " + timing.Id);
                var active = time >= timing.StartTime && time <= timing.StartTime + timing.Duration + .0001f;
                phase.gameObject.SetActive(true);
                foreach (var renderer in phase.GetComponentsInChildren<Renderer>(true)) renderer.enabled = active;
                var sparks = phase.GetComponentInChildren<ParticleSystem>(true);
                if (sparks == null || !active) continue;
                sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); sparks.Clear(true); sparks.Play(true);
                if (timing.Id == "sparks") EmitSparks(sparks); else EmitDissipation(sparks);
                SimulateEmittedParticles(sparks);
            }
        }

        private static void EmitSparks(ParticleSystem ps)
        {
            var offsets = new[] { new Vector3(-.15f, -.1f, .12f), new Vector3(.10f, .35f, .06f), new Vector3(.42f, .68f, .18f), new Vector3(.74f, .25f, -.04f), new Vector3(.98f, .82f, .12f), new Vector3(-.46f, .52f, .02f), new Vector3(1.12f, -.15f, .08f), new Vector3(.28f, 1.12f, -.02f) };
            for (var index = 0; index < offsets.Length; index++)
            {
                var emit = new ParticleSystem.EmitParams { position = offsets[index], velocity = Vector3.zero, startSize = index % 3 == 0 ? .13f : .075f, startColor = Spark, startLifetime = .23f, rotation = Mathf.PI * .25f };
                ps.Emit(emit, 1);
            }
        }

        private static void EmitDissipation(ParticleSystem ps)
        {
            foreach (var offset in new[] { new Vector3(-.32f, .22f, .08f), new Vector3(.12f, .46f, .02f), new Vector3(.42f, .13f, .1f), new Vector3(.75f, .62f, -.03f), new Vector3(1.05f, .32f, .08f) })
            {
                var emit = new ParticleSystem.EmitParams { position = offset, velocity = Vector3.zero, startSize = .07f, startColor = new Color(1f, .18f, .02f, .45f), startLifetime = .2f };
                ps.Emit(emit, 1);
            }
        }

        private static void CaptureEvidence()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().Single(item => item.name == "S12_SlashSpikeRoot");
            var camera = scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<Camera>(true)).Single(item => item.name == "S12_SpikeCamera");
            var views = new List<ViewRecord>();
            var definitions = new[] { new ViewDefinition("front", "dark", new Color(.035f, .04f, .055f)), new ViewDefinition("side", "neutral", new Color(.16f, .17f, .19f)), new ViewDefinition("oblique_top", "bright", new Color(.70f, .72f, .74f)), new ViewDefinition("close", "dark", new Color(.035f, .04f, .055f)), new ViewDefinition("game_distance", "neutral", new Color(.16f, .17f, .19f)) };
            SetFrame(root, "combined");
            foreach (var view in definitions)
            {
                var pose = Pose(view.Name); camera.backgroundColor = view.Background; var record = Capture(camera, pose, view.Name + ".png"); record.Background = view.BackgroundName; views.Add(record);
            }
            var frames = new[] { new TimeDefinition("anticipation", .02f), new TimeDefinition("primary", .16f), new TimeDefinition("afterimage", .24f), new TimeDefinition("dissipation", .38f) };
            var times = new List<TimeRecord>();
            foreach (var frame in frames)
            {
                SetFrame(root, frame.Name); camera.backgroundColor = new Color(.16f, .17f, .19f); var record = Capture(camera, Pose("front"), "time_" + frame.Name + ".png"); times.Add(new TimeRecord { Name = frame.Name, Time = frame.Time, Hash = record.Hash, File = record.File });
            }
            SetFrame(root, "combined"); camera.backgroundColor = new Color(.16f, .17f, .19f); ApplyPose(camera, Pose("front")); EditorSceneManager.SaveScene(scene, ScenePath);
            WriteMetadata(root, views, times);
        }

        // Capture has an explicit renderer-state table rather than relying on an Editor update tick after SetActive.
        // It proves that every visual phase can render alone; combined is used only for the five perspective reviews.
        private static void SetFrame(GameObject root, string frame)
        {
            var enabled = frame == "combined" ? new[] { "primary_arc", "afterimage", "sparks" } : frame == "primary" ? new[] { "primary_arc" } : frame == "afterimage" ? new[] { "afterimage", "sparks" } : new[] { frame };
            foreach (var id in new[] { "anticipation", "primary_arc", "afterimage", "sparks", "dissipation" })
            {
                var phase = root.transform.Find(id); if (phase == null) throw new InvalidOperationException("Missing S12 spike phase: " + id);
                var active = enabled.Contains(id); foreach (var renderer in phase.GetComponentsInChildren<Renderer>(true)) renderer.enabled = active;
                var particles = phase.GetComponentInChildren<ParticleSystem>(true);
                if (particles != null) { particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); particles.Clear(true); if (active) { particles.Play(true); if (id == "sparks") EmitSparks(particles); else EmitDissipation(particles); SimulateEmittedParticles(particles); } }
            }
            EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll();
        }

        // Emit state is not serialized into the preview scene. Advance the live system explicitly so Camera.Render,
        // renderer bounds, and EditMode tests observe actual particles rather than a static stand-in.
        private static void SimulateEmittedParticles(ParticleSystem particles)
        {
            particles.Simulate(.01f, true, false, true);
            if (particles.particleCount == 0) throw new InvalidOperationException("S12 spike particle emission did not produce a live particle state: " + particles.name);
            var live = new ParticleSystem.Particle[particles.particleCount];
            var count = particles.GetParticles(live);
            var localBounds = new Bounds(live[0].position, Vector3.zero);
            for (var index = 0; index < count; index++)
            {
                var radius = Mathf.Max(.02f, live[index].GetCurrentSize(particles) * .5f);
                localBounds.Encapsulate(live[index].position + Vector3.one * radius);
                localBounds.Encapsulate(live[index].position - Vector3.one * radius);
            }
            particles.GetComponent<ParticleSystemRenderer>().localBounds = localBounds;
        }

        private static S12SlashSpikePose Pose(string name)
        {
            var target = new Vector3(0f, .38f, 0f);
            switch (name)
            {
                case "front": return new S12SlashSpikePose { Position = new Vector3(0f, 2.4f, -7.6f), Target = target, FieldOfView = 60f };
                // Deliberately near the +X axis: |z| / |x| = 7.3%, so this is a true side-depth witness rather than a 45-degree oblique.
                case "side": return new S12SlashSpikePose { Position = new Vector3(8.2f, 2.8f, -.6f), Target = target, FieldOfView = 60f };
                case "oblique_top": return new S12SlashSpikePose { Position = new Vector3(4.8f, 6.6f, -7.2f), Target = target, FieldOfView = 60f };
                case "close": return new S12SlashSpikePose { Position = new Vector3(-.4f, 1.55f, -4.35f), Target = target, FieldOfView = 60f };
                case "game_distance": return new S12SlashSpikePose { Position = new Vector3(0f, 3.2f, -12f), Target = target, FieldOfView = 60f };
                default: throw new ArgumentOutOfRangeException("name", name, "Unsupported S12 spike view.");
            }
        }

        public static void ApplyPose(Camera camera, S12SlashSpikePose pose)
        {
            camera.orthographic = false; camera.fieldOfView = pose.FieldOfView; camera.transform.position = pose.Position; camera.transform.LookAt(pose.Target);
        }

        private static ViewRecord Capture(Camera camera, S12SlashSpikePose pose, string file)
        {
            ApplyPose(camera, pose);
            var render = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32); camera.targetTexture = render; camera.Render(); var previous = RenderTexture.active; RenderTexture.active = render;
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false); texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); texture.Apply(); var path = Path.Combine(EvidencePath, file); File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture); RenderTexture.active = previous; camera.targetTexture = null; render.Release(); UnityEngine.Object.DestroyImmediate(render);
            return new ViewRecord { File = file, Position = pose.Position, Target = pose.Target, FieldOfView = pose.FieldOfView, Hash = Hash(path) };
        }

        private static void WriteMetadata(GameObject root, List<ViewRecord> views, List<TimeRecord> times)
        {
            var lines = new List<string> { "{", "  \"capture\": \"hidden-graphics-device batch Camera.Render; no -nographics; Bloom disabled (camera HDR false)\",", "  \"targetReference\": \"docs/slash/reference/slash-visual-target-v1.png (reference only; no texture or flipbook imported)\",", "  \"views\": [" };
            for (var i = 0; i < views.Count; i++) { var v = views[i]; lines.Add("    { \"file\": \"" + v.File + "\", \"background\": \"" + v.Background + "\", \"position\": " + Vec(v.Position) + ", \"target\": " + Vec(v.Target) + ", \"fov\": " + Float(v.FieldOfView) + ", \"sha256\": \"" + v.Hash + "\" }" + (i == views.Count - 1 ? string.Empty : ",")); }
            lines.Add("  ],"); lines.Add("  \"timelineFrames\": [");
            for (var i = 0; i < times.Count; i++) { var t = times[i]; lines.Add("    { \"phase\": \"" + t.Name + "\", \"time\": " + Float(t.Time) + ", \"file\": \"" + t.File + "\", \"sha256\": \"" + t.Hash + "\" }" + (i == times.Count - 1 ? string.Empty : ",")); }
            lines.Add("  ],"); lines.Add("  \"meshes\": [");
            var meshes = AssetDatabase.FindAssets("t:Mesh", new[] { MeshRoot }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            for (var i = 0; i < meshes.Length; i++) { var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshes[i]); lines.Add("    { \"path\": \"" + meshes[i] + "\", \"vertices\": " + mesh.vertexCount + ", \"triangles\": " + mesh.triangles.Length / 3 + ", \"bounds\": " + Bounds(mesh.bounds) + " }" + (i == meshes.Length - 1 ? string.Empty : ",")); }
            lines.Add("  ],"); lines.Add("  \"materials\": [");
            var materials = AssetDatabase.FindAssets("t:Material", new[] { MaterialRoot }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            for (var i = 0; i < materials.Length; i++) { var material = AssetDatabase.LoadAssetAtPath<Material>(materials[i]); var alpha = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor").a : material.HasProperty("_Color") ? material.GetColor("_Color").a : 1f; var queueOffset = material.HasProperty("_QueueOffset") ? Mathf.RoundToInt(material.GetFloat("_QueueOffset")) : 0; lines.Add("    { \"path\": \"" + materials[i] + "\", \"shader\": \"" + material.shader.name + "\", \"renderQueue\": " + material.renderQueue + ", \"queueOffset\": " + queueOffset + ", \"alpha\": " + Float(alpha) + " }" + (i == materials.Length - 1 ? string.Empty : ",")); }
            var particleSamples = SampleParticleBounds(root);
            lines.Add("  ],"); lines.Add("  \"rendererBounds\": [");
            var renderers = root.GetComponentsInChildren<Renderer>(true).OrderBy(value => Hierarchy(value.transform), StringComparer.Ordinal).ToArray();
            for (var i = 0; i < renderers.Length; i++)
            {
                var queueOffset = renderers[i].sharedMaterial.HasProperty("_QueueOffset") ? Mathf.RoundToInt(renderers[i].sharedMaterial.GetFloat("_QueueOffset")) : 0;
                var particleRenderer = renderers[i] as ParticleSystemRenderer;
                var rendererBounds = particleRenderer == null ? renderers[i].bounds : particleSamples[particleRenderer].Bounds;
                var boundsSpace = particleRenderer == null ? "world" : "local";
                var sample = particleRenderer == null ? string.Empty : ", \"phaseSampleTime\": " + Float(particleSamples[particleRenderer].SampleTime);
                lines.Add("    { \"name\": \"" + Hierarchy(renderers[i].transform) + "\", \"bounds\": " + Bounds(rendererBounds) + ", \"boundsSpace\": \"" + boundsSpace + "\"" + sample + ", \"material\": \"" + renderers[i].sharedMaterial.name + "\", \"renderQueue\": " + renderers[i].sharedMaterial.renderQueue + ", \"queueOffset\": " + queueOffset + " }" + (i == renderers.Length - 1 ? string.Empty : ","));
            }
            lines.Add("  ]"); lines.Add("}"); File.WriteAllText(Path.Combine(EvidencePath, "metadata.json"), string.Join("\n", lines) + "\n", new UTF8Encoding(false));
        }

        private static Dictionary<ParticleSystemRenderer, ParticleBoundsSample> SampleParticleBounds(GameObject root)
        {
            var samples = new Dictionary<ParticleSystemRenderer, ParticleBoundsSample>();
            foreach (var item in new[] { new ParticleSampleDefinition("sparks", .16f), new ParticleSampleDefinition("dissipation", .24f) })
            {
                ApplyTime(root, item.Time);
                var particleRenderer = root.transform.Find(item.Phase + "/" + (item.Phase == "sparks" ? "SeparatedDiamondSparks" : "DissipationMotes")).GetComponent<ParticleSystemRenderer>();
                var particles = particleRenderer.GetComponent<ParticleSystem>();
                if (particles.particleCount == 0 || particleRenderer.localBounds.size.sqrMagnitude < .01f) throw new InvalidOperationException("S12 spike sampled particle bounds are empty for " + item.Phase);
                samples.Add(particleRenderer, new ParticleBoundsSample { Bounds = particleRenderer.localBounds, SampleTime = item.Time });
            }
            SetFrame(root, "combined");
            return samples;
        }

        private static string Vec(Vector3 value) { return "[" + Float(value.x) + ", " + Float(value.y) + ", " + Float(value.z) + "]"; }
        private static string Bounds(Bounds value) { return "{ \"center\": " + Vec(value.center) + ", \"size\": " + Vec(value.size) + " }"; }
        private static string Float(float value) { return value.ToString("0.######", CultureInfo.InvariantCulture); }
        private static string Hierarchy(Transform transform) { var names = new List<string>(); while (transform != null) { names.Add(transform.name); transform = transform.parent; } names.Reverse(); return string.Join("/", names.ToArray()); }
        public static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty); }

        private struct ViewDefinition { public string Name; public string BackgroundName; public Color Background; public ViewDefinition(string name, string backgroundName, Color background) { Name = name; BackgroundName = backgroundName; Background = background; } }
        private struct TimeDefinition { public string Name; public float Time; public TimeDefinition(string name, float time) { Name = name; Time = time; } }
        private struct ParticleSampleDefinition { public string Phase; public float Time; public ParticleSampleDefinition(string phase, float time) { Phase = phase; Time = time; } }
        private struct ParticleBoundsSample { public Bounds Bounds; public float SampleTime; }
        private struct PhaseTiming { public string Id; public float StartTime; public float Duration; public PhaseTiming(string id, float startTime, float duration) { Id = id; StartTime = startTime; Duration = duration; } }
        private sealed class ViewRecord { public string File; public string Background; public Vector3 Position; public Vector3 Target; public float FieldOfView; public string Hash; }
        private sealed class TimeRecord { public string Name; public float Time; public string File; public string Hash; }
    }

    public struct S12SlashSpikePose { public Vector3 Position; public Vector3 Target; public float FieldOfView; }
}
#endif
