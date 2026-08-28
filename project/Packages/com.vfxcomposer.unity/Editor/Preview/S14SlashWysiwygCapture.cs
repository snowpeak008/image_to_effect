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
using VFXComposer;
using VFXComposer.Editor.SlashV2;

namespace VFXComposer.Editor.Preview
{
    /// <summary>Explicit graphics-backed PlayMode recorder. It renders only the Preview scene's serialized MainCamera after one real PlaySlash call.</summary>
    [InitializeOnLoad]
    public static class S14SlashWysiwygCapture
    {
        private const string PendingKey = "VFXComposer.S14Capture.Pending";
        private const string DirectoryKey = "VFXComposer.S14Capture.Directory";
        private const int Width = 960;
        private const int Height = 540;
        private const string EvidenceRelative = "docs/stage-notes/s15-wysiwyg-evidence";
        private static readonly List<string> Frames = new List<string>();
        private static readonly List<string> ParticleReadbacks = new List<string>();
        // The origin is a texture-space contract, not a convenient bounds extremum.
        // Each painted action plane maps this UV to local zero.
        private static readonly List<string> AnchorReadbacks = new List<string>();
        private static Camera camera;
        private static SlashEffectController controller;
        private static string directory;
        private static int lastFrame = -1;
        private static SlashContinuousCaptureHook hook;
        private static bool recording;

        static S14SlashWysiwygCapture()
        {
            EditorApplication.playModeStateChanged += OnPlayModeState;
            // EnteredPlayMode can precede this static constructor after the domain reload.
            // SessionState survives that reload, so resume explicitly on the next editor tick.
            if (SessionState.GetBool(PendingKey, false) && EditorApplication.isPlaying) EditorApplication.delayCall += ResumeAfterDomainReload;
        }

        private static void ResumeAfterDomainReload()
        {
            if (SessionState.GetBool(PendingKey, false) && EditorApplication.isPlaying && !recording) Begin();
        }

        [MenuItem("VFX Composer/S14/Capture WYSIWYG Generated Preview (PlayMode)")]
        public static void RunBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("S14 capture requires EditMode.");
            EditorSceneManager.OpenScene(S12SlashGeneratedPreview.ScenePath, OpenSceneMode.Single);
            directory = Path.Combine(ProjectRoot(), EvidenceRelative, "run-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture));
            if (Directory.Exists(directory)) throw new InvalidOperationException("S14 evidence run directory already exists: " + directory);
            Directory.CreateDirectory(directory);
            SessionState.SetString(DirectoryKey, directory); SessionState.SetBool(PendingKey, true);
            EditorApplication.isPlaying = true;
        }

        /// <summary>Deletes only rejected recorder runs after a later verified run exists; user comparison inputs are outside the run-* allow-list.</summary>
        public static void CleanupRejectedRunsBatch()
        {
            var root = Path.Combine(ProjectRoot(), EvidenceRelative);
            var runs = Directory.Exists(root) ? Directory.GetDirectories(root, "run-*").OrderByDescending(Directory.GetLastWriteTimeUtc).ToArray() : new string[0];
            if (runs.Length == 0) return;
            var accepted = Path.GetFullPath(runs[0]);
            if (!File.Exists(Path.Combine(accepted, "metadata.json"))) throw new InvalidOperationException("Refusing S14 cleanup without a completed latest run.");
            foreach (var run in runs.Skip(1))
            {
                var full = Path.GetFullPath(run); var parent = Path.GetDirectoryName(full); var name = Path.GetFileName(full);
                if (!string.Equals(parent, Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) || !name.StartsWith("run-", StringComparison.Ordinal) || name.Length != 23) throw new InvalidOperationException("Refusing unsafe S14 cleanup target: " + full);
                Directory.Delete(full, true);
            }
        }

        private static void OnPlayModeState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false)) Begin();
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(PendingKey, false)) { SessionState.EraseBool(PendingKey); SessionState.EraseString(DirectoryKey); EditorApplication.Exit(0); }
        }

        private static void Begin()
        {
            directory = SessionState.GetString(DirectoryKey, string.Empty);
            var scene = SceneManager.GetSceneByPath(S12SlashGeneratedPreview.ScenePath);
            if (!scene.IsValid() || string.IsNullOrEmpty(directory)) throw new InvalidOperationException("S14 capture did not enter the saved Generated Preview scene.");
            var roots = scene.GetRootGameObjects();
            var cameras = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Where(item => item.CompareTag("MainCamera")).ToArray();
            var controllers = roots.SelectMany(root => root.GetComponentsInChildren<SlashEffectController>(true)).ToArray();
            var drivers = roots.SelectMany(root => root.GetComponentsInChildren<SlashPreviewPlaybackDriver>(true)).ToArray();
            if (cameras.Length != 1 || controllers.Length != 1 || drivers.Length != 1) throw new InvalidOperationException("S14 Preview must contain exactly one serialized MainCamera, generated controller, and driver.");
            camera = cameras.Single(); controller = controllers.Single(); drivers.Single().enabled = false;
            if (camera.clearFlags != CameraClearFlags.SolidColor || camera.allowHDR || camera.allowMSAA) throw new InvalidOperationException("S14 serialized MainCamera is not the required solid-color, HDR-off, MSAA-off authority.");
            controller.ResetForPool(); controller.enabled = true; controller.PlaySlash(controller.transform.position, controller.transform.rotation); Time.captureFramerate = 60; Time.captureDeltaTime = 1f / 60f; Application.targetFrameRate = 60;
            Frames.Clear(); ParticleReadbacks.Clear(); AnchorReadbacks.Clear(); lastFrame = -1; recording = true; CaptureOne("frame_0000", 0f); CaptureParticleReadback(0f); CaptureAnchorReadback(0f); hook = camera.gameObject.AddComponent<SlashContinuousCaptureHook>(); hook.Controller = controller; SlashContinuousCaptureHook.AfterFrame += CapturePlayerLoopFrame;
        }

        private static void CapturePlayerLoopFrame(int frame, float elapsed)
        {
            if (!recording || frame == lastFrame) return;
            lastFrame = frame; var timestamp = Mathf.Min(elapsed, .45f); CaptureOne("frame_" + Frames.Count.ToString("D4", CultureInfo.InvariantCulture), timestamp); CaptureParticleReadback(timestamp); CaptureAnchorReadback(timestamp);
            if (!controller.IsPlaying && elapsed >= .45f) Finish();
        }

        private static void Finish()
        {
            recording = false; SlashContinuousCaptureHook.AfterFrame -= CapturePlayerLoopFrame; Time.captureFramerate = 0; Time.captureDeltaTime = 0f; Application.targetFrameRate = -1; if (hook != null) UnityEngine.Object.Destroy(hook);
            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "VFX", "Generated", "slash_3d_stylized", "BuildManifest.json")));
            var cameraJson = "{ \"name\": \"" + camera.name + "\", \"clearFlags\": \"SolidColor\", \"background\": [" + Number(camera.backgroundColor.r) + "," + Number(camera.backgroundColor.g) + "," + Number(camera.backgroundColor.b) + "," + Number(camera.backgroundColor.a) + "], \"fov\": " + Number(camera.fieldOfView) + ", \"hdr\": false, \"msaa\": false, \"cullingMask\": " + camera.cullingMask + ", \"position\": [" + Number(camera.transform.position.x) + "," + Number(camera.transform.position.y) + "," + Number(camera.transform.position.z) + "], \"rotation\": [" + Number(camera.transform.rotation.x) + "," + Number(camera.transform.rotation.y) + "," + Number(camera.transform.rotation.z) + "," + Number(camera.transform.rotation.w) + "] }";
            var json = "{\n  \"capture\": \"explicit PlayMode actual player-loop capture; serialized Preview MainCamera only; one PlaySlash call; controller remained enabled and normal Update advanced at Time.captureFramerate=60; LateUpdate hook observed completed frames only; no SampleForPreview, StepForContinuousCapture, Emit, SetParticles, phase toggles, or evidence camera\",\n  \"scene\": \"" + S12SlashGeneratedPreview.ScenePath + "\",\n  \"sourcePrefabPath\": \"" + S12SlashCompiler.OutputPrefabPath + "\",\n  \"sourcePrefabGuid\": \"" + AssetDatabase.AssetPathToGUID(S12SlashCompiler.OutputPrefabPath) + "\",\n  \"recipeHash\": \"" + (string)manifest["recipeHash"] + "\",\n  \"buildHash\": \"" + (string)manifest["buildHash"] + "\",\n  \"fps\": 60,\n  \"anchorTextureUv\": [" + Number(SlashOriginAnchor.MainTextureUv.x) + "," + Number(SlashOriginAnchor.MainTextureUv.y) + "],\n  \"camera\": " + cameraJson + ",\n  \"frames\": [\n    " + string.Join(",\n    ", Frames) + "\n  ],\n  \"liveParticleReadback\": [\n    " + string.Join(",\n    ", ParticleReadbacks) + "\n  ],\n  \"anchorReadback\": [\n    " + string.Join(",\n    ", AnchorReadbacks) + "\n  ]\n}\n";
            File.WriteAllText(Path.Combine(directory, "metadata.json"), json, new UTF8Encoding(false));
            Debug.Log("S14 WYSIWYG PlayMode capture written once: " + directory);
            EditorApplication.isPlaying = false;
        }

        private static void CaptureOne(string name, float elapsed)
        {
            var file = name + ".png"; var path = Path.Combine(directory, file); if (File.Exists(path)) throw new InvalidOperationException("S14 evidence is write-once: " + path);
            var render = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32); var prior = RenderTexture.active;
            try { camera.targetTexture = render; camera.Render(); RenderTexture.active = render; var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false); texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); texture.Apply(false); File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.Destroy(texture); }
            finally { camera.targetTexture = null; RenderTexture.active = prior; RenderTexture.ReleaseTemporary(render); }
            Frames.Add("{ \"index\": " + (Frames.Count) + ", \"time\": " + Number(elapsed) + ", \"file\": \"" + file + "\", \"sha256\": \"" + Hash(path) + "\" }");
        }

        // Read from the same completed player-loop frame as the PNG. This exposes auditable
        // particle facts without turning a non-rendering test into a visual acceptance claim.
        private static void CaptureParticleReadback(float elapsed)
        {
            var systems = controller.GetComponentsInChildren<ParticleSystem>(true);
            var sparks = systems.Single(system => system.name == "Slash_sparks");
            var dissipation = systems.Single(system => system.name == "Slash_dissipation");
            var spark = ParticleStats(sparks); var diss = ParticleStats(dissipation);
            ParticleReadbacks.Add("{ \"time\": " + Number(elapsed) + ", \"sparkLiveCount\": " + spark.Count + ", \"sparkProjectedAreaPx\": " + Number(spark.Area) + ", \"sparkMaxProjectedSizePx\": " + Number(spark.MaxSize) + ", \"sparkMeanProjectedSizePx\": " + Number(spark.MeanSize) + ", \"sparkMeanAlpha\": " + Number(spark.MeanAlpha) + ", \"dissipationLiveCount\": " + diss.Count + ", \"dissipationProjectedAreaPx\": " + Number(diss.Area) + ", \"dissipationMeanAlpha\": " + Number(diss.MeanAlpha) + " }");
        }

        // The action-plane mesh maps SlashOriginAnchor.MainTextureUv to local zero.  The
        // projection record makes drift between painted layers independently auditable.
        private static void CaptureAnchorReadback(float elapsed)
        {
            var primary = controller.GetComponentsInChildren<Transform>(true).Single(item => item.name == "PaintedCrescentActionPlane");
            var afterimage = controller.GetComponentsInChildren<Transform>(true).Single(item => item.name == "PaintedAfterimage");
            var residue = controller.GetComponentsInChildren<Transform>(true).Single(item => item.name == "PaintedResidue");
            var main = camera.WorldToScreenPoint(primary.TransformPoint(Vector3.zero));
            var after = camera.WorldToScreenPoint(afterimage.TransformPoint(Vector3.zero));
            var dissipation = camera.WorldToScreenPoint(residue.TransformPoint(Vector3.zero));
            var maxDistance = Mathf.Max(Vector2.Distance(main, after), Vector2.Distance(main, dissipation), Vector2.Distance(after, dissipation));
            AnchorReadbacks.Add("{ \"time\": " + Number(elapsed) + ", \"primary\": [" + Number(main.x) + "," + Number(main.y) + "], \"afterimage\": [" + Number(after.x) + "," + Number(after.y) + "], \"residue\": [" + Number(dissipation.x) + "," + Number(dissipation.y) + "], \"maxDistancePx\": " + Number(maxDistance) + " }");
        }

        private static ParticleFrameStats ParticleStats(ParticleSystem system)
        {
            var count = system.particleCount; if (count == 0) return new ParticleFrameStats();
            var particles = new ParticleSystem.Particle[count]; var read = system.GetParticles(particles); var meshHeight = system.GetComponent<ParticleSystemRenderer>().mesh.bounds.size.y; var area = 0f; var maxSize = 0f; var sizeTotal = 0f; var alphaTotal = 0f;
            for (var index = 0; index < read; index++)
            {
                var particle = particles[index]; var world = system.transform.TransformPoint(particle.position); var center = camera.WorldToScreenPoint(world); if (center.z <= 0f) continue;
                var size = particle.GetCurrentSize(system) * meshHeight; var edge = camera.WorldToScreenPoint(world + camera.transform.up * (size * .5f)); var diameter = Mathf.Abs(edge.y - center.y); var radius = diameter * .5f;
                area += Mathf.PI * radius * radius; maxSize = Mathf.Max(maxSize, diameter); sizeTotal += diameter; alphaTotal += particle.GetCurrentColor(system).a / 255f;
            }
            return new ParticleFrameStats { Count = read, Area = area, MaxSize = maxSize, MeanSize = read == 0 ? 0f : sizeTotal / read, MeanAlpha = read == 0 ? 0f : alphaTotal / read };
        }

        private struct ParticleFrameStats { public int Count; public float Area; public float MaxSize; public float MeanSize; public float MeanAlpha; }

        private static string ProjectRoot() { return Directory.GetParent(Application.dataPath).Parent.FullName; }
        private static string Number(float value) { return value.ToString("0.######", CultureInfo.InvariantCulture); }
        private static string Hash(string path) { using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(File.ReadAllBytes(path)).Select(value => value.ToString("X2", CultureInfo.InvariantCulture))); }
    }
}
