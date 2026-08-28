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

namespace VFXComposer.Authoring
{
    /// <summary>S12A formal inputs only: five reusable templates plus Gold Sample. It never writes Generated assets.</summary>
    public static class S12SlashFormalAuthoring
    {
        public const string Root = "Assets/VFX/Templates/3D/Slash";
        public const string Prefabs = Root + "/Prefabs";
        public const string Meshes = Root + "/Meshes";
        public const string Materials = Root + "/Materials";
        public const string Textures = Root + "/Textures";
        public const string Manifests = "Assets/VFX/Templates/3D/SlashManifests";
        public const string GoldScene = "Assets/VFX/Preview/S12_SlashGoldSample.unity";
        public const string EvidenceRelative = "docs/stage-notes/s12a-evidence";
        private const int Width = 960;
        private const int Height = 540;
        private static readonly Color Orange = new Color(1f, .20f, .018f, .86f);
        private static readonly Color Blade = new Color(1f, .93f, .42f, .98f);
        private static readonly Color Red = new Color(.78f, .025f, .015f, .35f);
        private static readonly Color Spark = new Color(1f, .52f, .06f, .94f);
        public static string EvidencePath { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", EvidenceRelative)); } }

        [MenuItem("VFX Composer/S12A/Build Formal Slash Templates and Gold Sample")]
        public static void BuildAll()
        {
            BuildFormalAssets(); CreateGoldSampleAndEvidence(); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("S12A formal slash inputs and Gold Sample created. Generated/Compiler/Runtime remain untouched.");
        }

        /// <summary>S14's non-rendering formal asset refresh. It deliberately does not recreate legacy Gold/SetFrame evidence.</summary>
        public static void BuildAllS14Batch()
        {
            BuildFormalAssets();
            Debug.Log("S14 formal Slash templates refreshed without Gold evidence capture.");
        }

        /// <summary>S15 formal refresh: local generated brush textures, shader, and action-plane meshes only.</summary>
        public static void BuildAllS15Batch()
        {
            BuildFormalAssets();
            Debug.Log("S15 formal Slash templates refreshed without legacy Gold evidence capture.");
        }

        private static void BuildFormalAssets()
        {
            EnsureFolders(); RemoveRejectedStaticWitnessAssets();
            var mainPaint = S15Texture("S15_FieryCrescent_Main_v1.png", true, false);
            var breakupNoise = S15Texture("S15_SlashBreakupNoise_v1.png", false, true);
            var sparkAtlas = S15Texture("S15_SparkAtlas_v1.png", true, false);
            Material("S12_SlashAnticipation", new Color(1f, .38f, .04f, .80f), 3095);
            var primary = PaintedMaterial("S12_SlashPrimary", mainPaint, breakupNoise, 3100);
            var blade = Material("S12_SlashBlade", Blade, 3130, true);
            var after = Material("S12_SlashAfterimage", new Color(1f, .22f, .025f, .78f), 3070);
            var spark = SparkMaterial(sparkAtlas);
            Material("S12_SlashScaleReference", new Color(.32f, .35f, .39f, .80f), 3005);
            var lead = IgnitionBrush();
            var ignitionStar = IgnitionStar();
            var paintedCrescent = CurvedActionPlane("MESH_S15_PaintedCrescentActionPlane", 4.5f, 3.05f);
            var afterPlane = CurvedActionPlane("MESH_S15_PaintedAfterimagePlane", 3.95f, 2.68f);
            var residuePlane = CurvedActionPlane("MESH_S15_PaintedResiduePlane", 2.75f, 1.88f);
            var sparkSpawn = SparkSpawnFlakes();
            var diamond = Diamond(); var sparkQuad = SparkQuad();
            SaveAnticipation(lead, ignitionStar, after, spark); SaveArc(paintedCrescent, diamond, primary, blade); SaveAfterimage(afterPlane, primary); SaveParticles("PFT_3D_SlashSparks", spark, sparkQuad, sparkSpawn, 24, 8, .18f, 2.2f, true); SaveParticles("PFT_3D_SlashDissipation", spark, sparkQuad, null, 12, 2, .22f, .35f, false, residuePlane, primary);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); WriteManifests(); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }

        public static void BuildAllBatch() { BuildAll(); }

        private static void EnsureFolders()
        {
            foreach (var folder in new[] { Root, Prefabs, Meshes, Materials, Manifests, "Assets/VFX/Preview" }) if (!AssetDatabase.IsValidFolder(folder)) Directory.CreateDirectory(Path.Combine(Application.dataPath, folder.Substring("Assets/".Length)));
            Directory.CreateDirectory(EvidencePath);
        }
        private static void RemoveRejectedStaticWitnessAssets()
        {
            foreach (var asset in new[] { Meshes + "/MESH_S12A_SparkWitnesses.asset", Meshes + "/MESH_S12A_DissipationWitnesses.asset" }) if (AssetDatabase.LoadAssetAtPath<Mesh>(asset) != null) AssetDatabase.DeleteAsset(asset);
        }

        private static Material Material(string name, Color color, int queue, bool reveal = false)
        {
            var path = Materials + "/" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path); var shader = reveal ? Shader.Find("Universal Render Pipeline/VFXComposer Slash Reveal Unlit") : Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit"); if (shader == null) throw new InvalidOperationException("S12A requires a URP unlit shader.");
            if (material == null) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); } else material.shader = shader;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color); if (material.HasProperty("_Color")) material.SetColor("_Color", color); if (material.HasProperty("_Reveal")) material.SetFloat("_Reveal", 0f); if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f); if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 1f); if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.One); if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f); if (material.HasProperty("_QueueOffset")) material.SetFloat("_QueueOffset", queue - 3000); material.renderQueue = queue; EditorUtility.SetDirty(material); return material;
        }

        private static Texture2D S15Texture(string file, bool alpha, bool repeat)
        {
            var path = Textures + "/" + file; var importer = AssetImporter.GetAtPath(path) as TextureImporter; if (importer == null) throw new InvalidOperationException("Missing S15 texture input: " + path);
            importer.textureType = TextureImporterType.Default; importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None; importer.alphaIsTransparency = alpha; importer.mipmapEnabled = false; importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear; importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path); if (texture == null) throw new InvalidOperationException("S15 texture import did not yield Texture2D: " + path); return texture;
        }

        private static Material PaintedMaterial(string name, Texture2D main, Texture2D noise, int queue)
        {
            var path = Materials + "/" + name + ".mat"; var shader = Shader.Find("Universal Render Pipeline/VFXComposer S15 Painted Crescent Unlit"); if (shader == null) throw new InvalidOperationException("S15 painted-crescent shader is unavailable."); var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); } else material.shader = shader;
            material.SetTexture("_MainTex", main); material.SetTexture("_BreakupNoise", noise); material.SetColor("_BaseColor", Color.white); material.SetFloat("_Reveal", 0f); material.SetFloat("_Dissolve", 0f); material.SetFloat("_NoiseScale", 2.15f); material.SetFloat("_Emission", 1.1f); material.renderQueue = queue; EditorUtility.SetDirty(material); return material;
        }

        private static Material SparkMaterial(Texture2D atlas)
        {
            var path = Materials + "/S12_SlashSpark.mat"; var shader = Shader.Find("Universal Render Pipeline/VFXComposer S15 Spark Atlas Unlit"); if (shader == null) throw new InvalidOperationException("S15 spark-atlas shader is unavailable."); var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader) { name = "S12_SlashSpark" }; AssetDatabase.CreateAsset(material, path); } else material.shader = shader;
            material.SetTexture("_BaseMap", atlas); material.SetColor("_BaseColor", Color.white); material.renderQueue = 3150; EditorUtility.SetDirty(material); return material;
        }

        // Fresh S12A mesh construction; it shares only the approved visual mathematics, not any Spike asset or path.
        private static Mesh Ribbon(string name, float start, float end, float width, float side, float z)
        {
            var path = Meshes + "/" + name + ".asset"; var previous = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (previous != null) AssetDatabase.DeleteAsset(path);
            const int segments = 28; const float depth = .055f; var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (var index = 0; index <= segments; index++) { var progress = index / (float)segments; var t = Mathf.Lerp(start, end, progress); var center = Arc(t) + Normal(t) * side + Vector3.forward * z; var half = width * (.18f + .82f * Mathf.Sin(progress * Mathf.PI)) * .5f; var left = center - Normal(t) * half; var right = center + Normal(t) * half; vertices.Add(left + Vector3.back * depth); vertices.Add(right + Vector3.back * depth); vertices.Add(left + Vector3.forward * depth); vertices.Add(right + Vector3.forward * depth); uv.Add(new Vector2(progress, 0)); uv.Add(new Vector2(progress, 1)); uv.Add(new Vector2(progress, 0)); uv.Add(new Vector2(progress, 1)); }
            for (var index = 0; index < segments; index++) { var a = index * 4; var b = a + 4; Quad(triangles, a, b, a + 1, b + 1); Quad(triangles, a + 2, a + 3, b + 2, b + 3); Quad(triangles, a, a + 2, b, b + 2); Quad(triangles, a + 1, b + 1, a + 3, b + 3); }
            var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0, true); mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        // A subtly curved action plane, not a screen card: its 32x20 surface follows the action
        // plane and samples a local RGBA VFX paint texture with transparent surroundings.
        private static Mesh CurvedActionPlane(string name, float width, float height)
        {
            var path = Meshes + "/" + name + ".asset"; var previous = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (previous != null) AssetDatabase.DeleteAsset(path);
            const int columns = 32; const int rows = 20; var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>(); var anchor = SlashOriginAnchor.MainTextureUv; var anchorCurve = .075f * Mathf.Sin((anchor.x - .12f) * Mathf.PI) * (1f - Mathf.Abs(anchor.y - .5f) * .65f); var anchorWave = .055f * Mathf.Sin((anchor.x + anchor.y) * Mathf.PI);
            for (var row = 0; row <= rows; row++) for (var column = 0; column <= columns; column++)
            {
                var u = column / (float)columns; var v = row / (float)rows; var x = (u - anchor.x) * width; var y = (v - anchor.y) * height; var curve = .075f * Mathf.Sin((u - .12f) * Mathf.PI) * (1f - Mathf.Abs(v - .5f) * .65f);
                vertices.Add(new Vector3(x, y + .055f * Mathf.Sin((u + v) * Mathf.PI) - anchorWave, curve - anchorCurve)); uv.Add(new Vector2(u, v));
            }
            for (var row = 0; row < rows; row++) for (var column = 0; column < columns; column++) { var a = row * (columns + 1) + column; var b = a + columns + 1; Quad(triangles, a, b, a + 1, b + 1); }
            var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0, true); mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        private static Mesh RaggedRibbon(string name, float start, float end, float width, float side, float z, float seed)
        {
            var path = Meshes + "/" + name + ".asset"; var previous = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (previous != null) AssetDatabase.DeleteAsset(path);
            const int segments = 28; var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (var index = 0; index <= segments; index++) { var progress = index / (float)segments; var t = Mathf.Lerp(start, end, progress); var center = Arc(t) + Normal(t) * side + Vector3.forward * z; var pulse = Mathf.Clamp01(Mathf.Sin(progress * Mathf.PI)); var teeth = Mathf.Max(0f, Mathf.Sin(progress * 19f + seed) * .42f + Mathf.Sin(progress * 37f + seed * 1.9f) * .22f); var inner = width * (.14f + .52f * pulse) * .5f; var outer = width * (.20f + .96f * pulse + teeth) * .5f; vertices.Add(center - Normal(t) * inner); vertices.Add(center + Normal(t) * outer); uv.Add(new Vector2(progress, 0)); uv.Add(new Vector2(progress, 1)); }
            for (var index = 0; index < segments; index++) { var a = index * 2; Quad(triangles, a, a + 2, a + 1, a + 3); }
            var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0, true); mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        private static Mesh Tongues(string name, params float[] locations)
        {
            var path = Meshes + "/" + name + ".asset"; var previous = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (previous != null) AssetDatabase.DeleteAsset(path); var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (var index = 0; index < locations.Length; index++) { var t = locations[index]; var center = Arc(t) + Normal(t) * .21f + Vector3.forward * .09f; var tangent = (Arc(Mathf.Min(1f, t + .01f)) - Arc(Mathf.Max(0f, t - .01f))).normalized; var normal = Normal(t); var width = .055f + index * .012f; var tip = center + normal * (.16f + index * .045f) + tangent * (.05f + index * .035f); vertices.Add(center - tangent * width); vertices.Add(center + tangent * width); vertices.Add(tip); triangles.Add(index * 3); triangles.Add(index * 3 + 1); triangles.Add(index * 3 + 2); }
            var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0, true); mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        private static Vector3 Arc(float t) { return new Vector3(Mathf.Lerp(-1.25f, 1.30f, t) + .09f * Mathf.Sin(Mathf.PI * t), -.88f + 2.18f * t + .46f * Mathf.Sin(Mathf.PI * t), .22f * Mathf.Sin(Mathf.PI * t)); }
        private static Vector3 Normal(float t) { var epsilon = .002f; return Vector3.Cross(Vector3.forward, (Arc(Mathf.Min(1f, t + epsilon)) - Arc(Mathf.Max(0f, t - epsilon))).normalized).normalized; }
        private static void Quad(List<int> target, int a, int b, int c, int d) { target.Add(a); target.Add(b); target.Add(c); target.Add(c); target.Add(b); target.Add(d); }
        private static Mesh Diamond()
        {
            var path = Meshes + "/MESH_S12A_DiamondSpark.asset"; var previous = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (previous != null) AssetDatabase.DeleteAsset(path); var mesh = new Mesh { name = "MESH_S12A_DiamondSpark" }; mesh.vertices = new[] { new Vector3(0f, .075f, 0f), new Vector3(.05f, 0f, 0f), new Vector3(0f, -.075f, 0f), new Vector3(-.05f, 0f, 0f), new Vector3(0f, .075f, .022f), new Vector3(.05f, 0f, .022f), new Vector3(0f, -.075f, .022f), new Vector3(-.05f, 0f, .022f) }; mesh.triangles = new[] { 0, 1, 3, 3, 1, 2, 4, 7, 5, 5, 7, 6 }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        private static Mesh SparkQuad()
        {
            var path = Meshes + "/MESH_S15_SparkAtlasQuad.asset"; var previous = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (previous != null) AssetDatabase.DeleteAsset(path); var mesh = new Mesh { name = "MESH_S15_SparkAtlasQuad" };
            mesh.vertices = new[] { new Vector3(-.07f, -.07f, 0f), new Vector3(.07f, -.07f, 0f), new Vector3(.07f, .07f, 0f), new Vector3(-.07f, .07f, 0f) }; mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) }; mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        private static Mesh IgnitionBrush()
        {
            var path = Meshes + "/MESH_S15_IgnitionBrush.asset"; var previous = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (previous != null) AssetDatabase.DeleteAsset(path);
            var mesh = new Mesh { name = "MESH_S15_IgnitionBrush" }; mesh.vertices = new[] { new Vector3(-.025f, -.035f, -.04f), new Vector3(.025f, .035f, -.04f), new Vector3(.34f, .29f, -.04f), new Vector3(.19f, .08f, -.04f) }; mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(.62f, 0f) }; mesh.triangles = new[] { 0, 1, 3, 1, 2, 3 }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        private static Mesh SparkSpawnFlakes()
        {
            var path = Meshes + "/MESH_S15_SparkOuterEdgeSpawn.asset"; var previous = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (previous != null) AssetDatabase.DeleteAsset(path); var vertices = new List<Vector3>(); var triangles = new List<int>();
            // Five very small emission islands sit just outside the painted crescent's terminal/outer edge.
            foreach (var point in new[] { new Vector3(.55f, 1.20f, -.18f), new Vector3(.98f, .93f, -.18f), new Vector3(1.34f, .54f, -.18f), new Vector3(1.55f, .14f, -.18f), new Vector3(.18f, 1.36f, -.18f) })
            {
                var index = vertices.Count; vertices.Add(point + new Vector3(-.035f, -.02f, 0f)); vertices.Add(point + new Vector3(.04f, -.01f, 0f)); vertices.Add(point + new Vector3(0f, .045f, 0f)); triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
            }
            var mesh = new Mesh { name = "MESH_S15_SparkOuterEdgeSpawn" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0, true); mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        private static Mesh IgnitionStar()
        {
            var path = Meshes + "/MESH_S15_IgnitionAtlasStar.asset"; var previous = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (previous != null) AssetDatabase.DeleteAsset(path); var mesh = new Mesh { name = "MESH_S15_IgnitionAtlasStar" };
            var origin = new Vector3(.015f, .015f, -.055f); mesh.vertices = new[] { origin + new Vector3(-.18f, -.18f, 0f), origin + new Vector3(.18f, -.18f, 0f), origin + new Vector3(.18f, .18f, 0f), origin + new Vector3(-.18f, .18f, 0f) };
            // Select exactly one atlas quadrant for one compact ignition star, rather than spreading the atlas across a ribbon.
            mesh.uv = new[] { new Vector2(0f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, 1f), new Vector2(0f, 1f) }; mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }

        private static Mesh Layered(string name, params Mesh[] layers)
        {
            var path = Meshes + "/" + name + ".asset"; var previous = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (previous != null) AssetDatabase.DeleteAsset(path);
            var mesh = new Mesh { name = name }; mesh.CombineMeshes(layers.Select(layer => new CombineInstance { mesh = layer }).ToArray(), false, false); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }

        private static void SaveAnticipation(Mesh brush, Mesh star, Material brushMaterial, Material starMaterial)
        {
            // One compact lower-left ignition: a local atlas star physically touches a short warm brush.
            // Two submeshes share one renderer so the static renderer budget remains truthful.
            var root = new GameObject("PFT_3D_SlashAnticipation"); root.AddComponent<MeshFilter>().sharedMesh = Layered("MESH_S15_IgnitionCombined", brush, star); root.AddComponent<MeshRenderer>().sharedMaterials = new[] { brushMaterial, starMaterial }; Save(root, "PFT_3D_SlashAnticipation");
        }
        private static void SaveArc(Mesh paintedCrescent, Mesh diamond, Material primary, Material blade)
        {
            var root = new GameObject("PFT_3D_SlashArcSweep"); root.AddComponent<SlashArcSweepReveal>(); var width = new GameObject("RibbonWidthControl"); width.transform.SetParent(root.transform, false); var painted = new GameObject("PaintedCrescentActionPlane"); painted.transform.SetParent(width.transform, false); painted.AddComponent<MeshFilter>().sharedMesh = paintedCrescent; painted.AddComponent<MeshRenderer>().sharedMaterial = primary;
            // This small moving tip glint is a visible primary-layer feature. Its lifetime is the direct, reviewed primary-duration binding—not a hidden clock.
            var runner = new GameObject("PrimarySweepRunner"); runner.transform.SetParent(root.transform, false); var ps = runner.AddComponent<ParticleSystem>(); var main = ps.main; main.loop = false; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.Local; main.maxParticles = 1; main.startLifetime = .16f; main.startSize = .075f; main.startColor = Blade; var emission = ps.emission; emission.enabled = false; var shape = ps.shape; shape.enabled = false; var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = diamond; renderer.sharedMaterial = blade; Burst(ps, 1);
            Save(root, "PFT_3D_SlashArcSweep");
        }
        private static void SaveAfterimage(Mesh plane, Material material)
        {
            var root = new GameObject("PFT_3D_SlashAfterimage"); var painted = new GameObject("PaintedAfterimage"); painted.transform.SetParent(root.transform, false); painted.transform.localPosition = Vector3.zero; painted.AddComponent<MeshFilter>().sharedMesh = plane; painted.AddComponent<MeshRenderer>().sharedMaterial = material; painted.AddComponent<SlashPaintedLayerFade>().Configure(new Color(1f, .25f, .045f, .42f), 12f, .42f, 1f);
            // Retained binding target: count still controls a second semantic echo without reintroducing a visible narrow ribbon.
            var echoB = new GameObject("EchoB"); echoB.transform.SetParent(root.transform, false); Save(root, "PFT_3D_SlashAfterimage");
        }
        private static void SaveParticles(string name, Material material, Mesh mesh, Mesh spawnMesh, int maxParticles, int count, float lifetime, float speed, bool bright, Mesh residuePlane = null, Material residueMaterial = null)
        {
            var root = new GameObject(name); var ps = root.AddComponent<ParticleSystem>(); var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.Local; main.maxParticles = maxParticles; main.startLifetime = lifetime; main.startSize = bright ? new ParticleSystem.MinMaxCurve(.55f, .82f) : .18f; main.startSpeed = speed; main.startColor = bright ? Color.white : new Color(1f, .16f, .01f, .38f);
            var emission = ps.emission; emission.enabled = !bright; if (!bright) emission.rateOverTime = 12f;
            var shape = ps.shape; shape.enabled = true; shape.shapeType = spawnMesh == null ? ParticleSystemShapeType.Box : ParticleSystemShapeType.Mesh;
            if (spawnMesh != null) shape.mesh = spawnMesh; else { shape.position = new Vector3(.72f, .62f, -.18f); shape.scale = new Vector3(.7f, .45f, .10f); }
            shape.randomDirectionAmount = .28f; var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local; velocity.x = bright ? .55f : .20f; velocity.y = bright ? .68f : .25f;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, .12f)));
            var color = ps.colorOverLifetime; color.enabled = true; var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(bright ? new Color(1f, .88f, .24f) : new Color(.95f, .22f, .04f), 0f), new GradientColorKey(bright ? new Color(1f, .34f, .02f) : new Color(.5f, .04f, .01f), 1f) }, new[] { new GradientAlphaKey(bright ? 1f : .34f, 0f), new GradientAlphaKey(0f, 1f) }); color.color = new ParticleSystem.MinMaxGradient(gradient);
            // Unity's TextureSheetAnimation start frame is normalized 0..1, not a tile index.
            // 0..0.75 selects the four 2x2 atlas cells without out-of-range clamping.
            var sheet = ps.textureSheetAnimation; sheet.enabled = true; sheet.mode = ParticleSystemAnimationMode.Grid; sheet.numTilesX = 2; sheet.numTilesY = 2; sheet.animation = ParticleSystemAnimationType.WholeSheet; sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, .75f);
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = mesh; renderer.sharedMaterial = material; renderer.sortMode = ParticleSystemSortMode.YoungestInFront; renderer.localBounds = new Bounds(bright ? new Vector3(1.15f, 1.05f, -.16f) : new Vector3(.85f, .75f, -.18f), bright ? new Vector3(2.6f, 2.4f, .75f) : new Vector3(1.1f, .85f, .55f)); Burst(ps, count);
            if (residuePlane != null && residueMaterial != null) { var residue = new GameObject("PaintedResidue"); residue.transform.SetParent(root.transform, false); residue.transform.localPosition = Vector3.zero; residue.transform.localScale = new Vector3(.62f, .62f, 1f); residue.AddComponent<MeshFilter>().sharedMesh = residuePlane; residue.AddComponent<MeshRenderer>().sharedMaterial = residueMaterial; residue.AddComponent<SlashPaintedLayerFade>().Configure(new Color(.95f, .18f, .025f, .17f), 10f, .25f, .93f); }
            Save(root, name);
        }
        private static void AddMesh(Transform parent, string name, Mesh mesh, Material material) { var child = new GameObject(name); child.transform.SetParent(parent, false); child.AddComponent<MeshFilter>().sharedMesh = mesh; child.AddComponent<MeshRenderer>().sharedMaterial = material; }
        private static void Save(GameObject root, string name) { var path = Prefabs + "/" + name + ".prefab"; PrefabUtility.SaveAsPrefabAsset(root, path); UnityEngine.Object.DestroyImmediate(root); }
        private static void Burst(ParticleSystem ps, int count) { var curve = new ParticleSystem.MinMaxCurve { mode = ParticleSystemCurveMode.Constant, constant = count }; ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, curve) }); }

        private static void WriteManifests()
        {
            Manifest("PFT_3D_SlashAnticipation", "anticipation", "anticipation_glint", new string[0], new[] { "S12_SlashAfterimage", "S12_SlashSpark" }, 0, 0, 2, 1);
            Manifest("PFT_3D_SlashArcSweep", "primary_arc", "arc_sweep", new[] { Param("scale", "float", .8, 1, 1.3, "3d.slash.arc.scale"), Param("width", "float", .16, .24, .34, "3d.slash.arc.width"), Param("duration", "float", .12, .16, .22, "3d.slash.arc.duration") }, new[] { "S12_SlashPrimary", "S12_SlashBlade" }, 1, 1, 2, 2);
            Manifest("PFT_3D_SlashAfterimage", "afterimage", "arc_afterimage", new[] { Param("count", "integer", 1, 2, 2, "3d.slash.afterimage.count"), Param("alpha", "float", .18, .32, .45, "3d.slash.afterimage.alpha") }, new[] { "S12_SlashPrimary" }, 0, 0, 1, 1);
            Manifest("PFT_3D_SlashSparks", "sparks", "slash_sparks", new[] { Param("count", "integer", 4, 8, 24, "3d.slash.sparks.count"), Param("speed", "float", 1.2, 2.2, 3.6, "3d.slash.sparks.speed"), Param("lifetime", "float", .1, .18, .28, "3d.slash.sparks.lifetime") }, new[] { "S12_SlashSpark" }, 24, 1, 1, 1);
            Manifest("PFT_3D_SlashDissipation", "dissipation", "slash_dissipation", new[] { Param("lifetime", "float", .12, .22, .28, "3d.slash.dissipation.lifetime") }, new[] { "S12_SlashSpark", "S12_SlashPrimary" }, 12, 1, 2, 2);
        }
        private static string Param(string name, string type, double min, double value, double max, string binding) { return "\"" + name + "\": { \"type\": \"" + type + "\", \"min\": " + Number(min) + ", \"default\": " + Number(value) + ", \"max\": " + Number(max) + ", \"binding\": \"" + binding + "\" }"; }
        private static void Manifest(string id, string phase, string module, string[] parameters, string[] materialNames, int particles, int systems, int materials, int renderers)
        {
            var path = Prefabs + "/" + id + ".prefab"; var guid = AssetDatabase.AssetPathToGUID(path); var materialGuids = materialNames.Select(name => AssetDatabase.AssetPathToGUID(Materials + "/" + name + ".mat")).ToArray(); if (string.IsNullOrEmpty(guid) || materialGuids.Any(string.IsNullOrEmpty)) throw new InvalidOperationException("S12A formal asset GUID missing."); var json = "{\n  \"slashManifestVersion\": 2,\n  \"templateId\": \"" + id + "\",\n  \"templateVersion\": \"1.0.0\",\n  \"phaseKind\": \"" + phase + "\",\n  \"moduleKind\": \"" + module + "\",\n  \"dimension\": \"3d\",\n  \"assetGuid\": \"" + guid + "\",\n  \"assetPath\": \"" + path + "\",\n  \"tags\": [\"slash\", \"s12a\", \"bloom-off\"],\n  \"materialGuids\": [\"" + string.Join("\", \"", materialGuids) + "\"],\n  \"parameters\": { " + string.Join(", ", parameters) + " },\n  \"cost\": { \"estimatedPeakParticles\": " + particles + ", \"particleSystems\": " + systems + ", \"materials\": " + materials + ", \"transparentRenderers\": " + renderers + " }\n}\n"; File.WriteAllText(Path.Combine(Application.dataPath, "VFX", "Templates", "3D", "SlashManifests", id + ".slash.manifest.json"), json, new UTF8Encoding(false));
        }
        private static string Number(double value) { return value.ToString("0.###", CultureInfo.InvariantCulture); }

        private static void CreateGoldSampleAndEvidence()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); var cameraGo = new GameObject("S12A_GoldCamera"); var camera = cameraGo.AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.allowHDR = false; camera.allowMSAA = false;
            var root = new GameObject("S12A_SlashGoldRoot"); AddScale(root.transform); var phases = new Dictionary<string, GameObject>(); foreach (var id in new[] { "anticipation", "primary_arc", "afterimage", "sparks", "dissipation" }) { var phase = new GameObject(id); phase.transform.SetParent(root.transform, false); phases.Add(id, phase); }
            Instance("PFT_3D_SlashAnticipation", phases["anticipation"]); Instance("PFT_3D_SlashArcSweep", phases["primary_arc"]); Instance("PFT_3D_SlashAfterimage", phases["afterimage"]); Instance("PFT_3D_SlashSparks", phases["sparks"]); Instance("PFT_3D_SlashDissipation", phases["dissipation"]);
            SetFrame(root, "combined"); Pose(camera, "front"); EditorSceneManager.SaveScene(scene, GoldScene); CaptureEvidence(root, camera); EditorSceneManager.SaveScene(scene, GoldScene);
        }
        private static void Instance(string prefab, GameObject phase) { var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + prefab + ".prefab"); var instance = PrefabUtility.InstantiatePrefab(asset) as GameObject; instance.name = prefab; instance.transform.SetParent(phase.transform, false); }
        private static void AddScale(Transform parent) { var root = new GameObject("ScaleReference_1_8m"); root.transform.SetParent(parent, false); root.transform.localPosition = new Vector3(-1.85f, 0f, .22f); var bar = GameObject.CreatePrimitive(PrimitiveType.Cube); bar.transform.SetParent(root.transform, false); bar.transform.localPosition = new Vector3(0f, .9f, 0f); bar.transform.localScale = new Vector3(.025f, 1.8f, .025f); UnityEngine.Object.DestroyImmediate(bar.GetComponent<Collider>()); bar.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/S12_SlashScaleReference.mat"); }
        private static void SetFrame(GameObject root, string frame)
        {
            // At the real .16 s primary sample, both deliberate overlap phases have started; capturing them makes the timing evidence truthful rather than showing an impossible isolated primary.
            var enabled = frame == "combined" || frame == "primary" ? new[] { "primary_arc", "afterimage", "sparks" } : frame == "afterimage" ? new[] { "afterimage", "sparks" } : new[] { frame };
            foreach (var id in new[] { "anticipation", "primary_arc", "afterimage", "sparks", "dissipation" }) { var phase = root.transform.Find(id); var active = enabled.Contains(id); foreach (var renderer in phase.GetComponentsInChildren<Renderer>(true)) renderer.enabled = active; var particle = phase.GetComponentInChildren<ParticleSystem>(true); if (particle != null) { particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); particle.Clear(true); if (active) { particle.Play(true); if (id == "sparks") Emit(particle, true); else if (id == "dissipation") Emit(particle, false); else particle.Emit(new ParticleSystem.EmitParams { position = new Vector3(1.12f, 1.05f, .05f), velocity = Vector3.zero, startLifetime = .16f, startSize = .70f, startColor = Blade }, 1); particle.Simulate(.01f, true, false, true); LiveBounds(particle); } } }
        }
        private static void Emit(ParticleSystem particle, bool sparks) { var offsets = sparks ? new[] { new Vector3(-.34f, -.34f, -.22f), new Vector3(.03f, .13f, -.20f), new Vector3(.42f, .72f, -.24f), new Vector3(.79f, .10f, -.18f), new Vector3(1.18f, .82f, -.23f), new Vector3(-.56f, .54f, -.19f), new Vector3(1.42f, -.18f, -.20f), new Vector3(.24f, 1.26f, -.22f) } : new[] { new Vector3(-.32f, .12f, -.16f), new Vector3(.17f, .51f, -.17f), new Vector3(.68f, .20f, -.15f), new Vector3(1.08f, .67f, -.16f), new Vector3(1.38f, .27f, -.15f) }; foreach (var offset in offsets) particle.Emit(new ParticleSystem.EmitParams { position = offset, velocity = Vector3.zero, startLifetime = sparks ? .18f : .20f, startSize = sparks ? 1.2f : .8f, startColor = sparks ? Spark : new Color(1f, .16f, .01f, .60f) }, 1); }
        private static void LiveBounds(ParticleSystem particle) { if (particle.particleCount == 0) throw new InvalidOperationException("S12A live particle emission failed: " + particle.name); var particles = new ParticleSystem.Particle[particle.particleCount]; var count = particle.GetParticles(particles); var bounds = new Bounds(particles[0].position, Vector3.zero); for (var index = 0; index < count; index++) { var radius = Mathf.Max(.02f, particles[index].GetCurrentSize(particle) * .5f); bounds.Encapsulate(particles[index].position + Vector3.one * radius); bounds.Encapsulate(particles[index].position - Vector3.one * radius); } particle.GetComponent<ParticleSystemRenderer>().localBounds = bounds; }
        private static void Pose(Camera camera, string name) { var target = new Vector3(0f, .38f, 0f); Vector3 position; switch (name) { case "front": position = new Vector3(0f, 2.4f, -7.6f); break; case "side": position = new Vector3(8.2f, 2.8f, -.6f); break; case "oblique_top": position = new Vector3(4.8f, 6.6f, -7.2f); break; case "close": position = new Vector3(-.4f, 1.55f, -4.35f); break; case "game_distance": position = new Vector3(0f, 3.2f, -12f); break; default: throw new ArgumentOutOfRangeException("name"); } camera.fieldOfView = 60f; camera.transform.position = position; camera.transform.LookAt(target); }
        private static void CaptureEvidence(GameObject root, Camera camera)
        {
            var views = new List<string>(); var definitions = new[] { new View("front", "dark", new Color(.035f, .04f, .055f)), new View("side", "neutral", new Color(.16f, .17f, .19f)), new View("oblique_top", "bright", new Color(.70f, .72f, .74f)), new View("close", "dark", new Color(.035f, .04f, .055f)), new View("game_distance", "neutral", new Color(.16f, .17f, .19f)) }; SetFrame(root, "combined"); foreach (var view in definitions) { camera.backgroundColor = view.Color; Pose(camera, view.Name); Capture(camera, view.Name + ".png"); views.Add("{ \"file\": \"" + view.Name + ".png\", \"background\": \"" + view.Background + "\", \"position\": " + Vec(camera.transform.position) + ", \"target\": [0, 0.38, 0], \"fov\": 60, \"sha256\": \"" + Hash(Path.Combine(EvidencePath, view.Name + ".png")) + "\" }"); }
            var times = new List<string>(); var particles = new List<string>(); foreach (var frame in new[] { new Frame("anticipation", .02f), new Frame("primary", .16f), new Frame("afterimage", .24f), new Frame("dissipation", .38f), new Frame("complete", .451f) }) { SetFrame(root, frame.Name == "complete" ? "none" : frame.Name); camera.backgroundColor = new Color(.16f, .17f, .19f); Pose(camera, "front"); var file = "time_" + frame.Name + ".png"; Capture(camera, file); times.Add("{ \"phase\": \"" + frame.Name + "\", \"time\": " + Number(frame.Time) + ", \"file\": \"" + file + "\", \"sha256\": \"" + Hash(Path.Combine(EvidencePath, file)) + "\" }"); if (frame.Name == "afterimage") particles.Add(ParticleFacts(root.transform.Find("sparks").GetComponentInChildren<ParticleSystem>(true), "sparks", frame.Time)); if (frame.Name == "dissipation") particles.Add(ParticleFacts(root.transform.Find("dissipation").GetComponentInChildren<ParticleSystem>(true), "dissipation", frame.Time)); }
            SetFrame(root, "combined"); var json = "{\n  \"capture\": \"hidden-graphics-device batch Camera.Render; no -nographics; Bloom disabled (camera HDR false)\",\n  \"unityVersion\": \"" + Application.unityVersion + "\",\n  \"scaleReferenceMeters\": 1.8,\n  \"views\": [" + string.Join(",", views) + "],\n  \"timelineFrames\": [" + string.Join(",", times) + "],\n  \"particleSamples\": [" + string.Join(",", particles) + "]\n}\n"; File.WriteAllText(Path.Combine(EvidencePath, "metadata.json"), json, new UTF8Encoding(false));
        }
        private static void Capture(Camera camera, string file) { var render = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32); camera.targetTexture = render; camera.Render(); var prior = RenderTexture.active; RenderTexture.active = render; var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false); texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); texture.Apply(); File.WriteAllBytes(Path.Combine(EvidencePath, file), texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture); RenderTexture.active = prior; camera.targetTexture = null; render.Release(); UnityEngine.Object.DestroyImmediate(render); }
        private static string ParticleFacts(ParticleSystem particle, string phase, float time) { var values = new ParticleSystem.Particle[particle.particleCount]; var count = particle.GetParticles(values); var distinct = values.Take(count).Select(value => value.position.ToString("F4")).Distinct(StringComparer.Ordinal).Count(); var bounds = particle.GetComponent<ParticleSystemRenderer>().localBounds; return "{ \"phase\": \"" + phase + "\", \"sampleTime\": " + Number(time) + ", \"particleCount\": " + count + ", \"distinctPositions\": " + distinct + ", \"localBoundsSize\": " + Vec(bounds.size) + " }"; }
        private static string Vec(Vector3 value) { return "[" + Number(value.x) + ", " + Number(value.y) + ", " + Number(value.z) + "]"; }
        public static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty); }
        private struct View { public string Name; public string Background; public Color Color; public View(string name, string background, Color color) { Name = name; Background = background; Color = color; } }
        private struct Frame { public string Name; public float Time; public Frame(string name, float time) { Name = name; Time = time; } }
    }
}
#endif
