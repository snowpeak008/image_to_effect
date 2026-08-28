using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace VFXComposer.Tests.PlayMode
{
    [Explicit("Graphics-backed evidence recorder; run only for a requested visual capture, never in the automatic PlayMode regression.")]
    public sealed class Impact2DVisualCaptureTests
    {
        private const string ScenePath = "Assets/VFX/Preview/VFXPREVIEW_Impact2D.unity";
        private const int Width = 768;
        private const int Height = 432;

        [UnityTest]
        public IEnumerator CaptureActualPreviewTimeline_FromSerializedCameraAndNaturalUpdate()
        {
            var operation = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single); Assert.That(operation, Is.Not.Null, "Impact preview scene must be enabled in Build Settings."); yield return operation;
            var scene = SceneManager.GetSceneByPath(ScenePath); Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
            var roots = scene.GetRootGameObjects(); var cameras = new List<Camera>(); var controllers = new List<TimedImpactVfxController>(); var drivers = new List<ImpactPreviewPlaybackDriver>();
            foreach (var root in roots) { cameras.AddRange(root.GetComponentsInChildren<Camera>(true)); controllers.AddRange(root.GetComponentsInChildren<TimedImpactVfxController>(true)); drivers.AddRange(root.GetComponentsInChildren<ImpactPreviewPlaybackDriver>(true)); }
            Assert.That(cameras.Count, Is.EqualTo(1)); Assert.That(controllers.Count, Is.EqualTo(1)); Assert.That(drivers.Count, Is.EqualTo(1));
            var camera = cameras[0]; var controller = controllers[0]; drivers[0].enabled = false; controller.ResetForPool(); yield return null;
            var directory = EvidenceDirectory(); Directory.CreateDirectory(directory); var frames = new List<string>(); FrameMetrics core = null; FrameMetrics peak = null; FrameMetrics decay = null; FrameMetrics complete = null;
            var priorCaptureRate = Time.captureFramerate; var priorDelta = Time.captureDeltaTime; var priorTarget = Application.targetFrameRate;
            try
            {
                Time.captureFramerate = 60; Time.captureDeltaTime = 1f / 60f; Application.targetFrameRate = 60;
                var empty = Capture(camera, directory, "frame_000_empty.png"); frames.Add(Frame("frame_000_empty.png", 0f, empty)); controller.Play();
                for (var frame = 1; frame <= 31; frame++)
                {
                    yield return null;
                    if (frame == 3) { core = Capture(camera, directory, "frame_003_core.png"); frames.Add(Frame("frame_003_core.png", frame / 60f, core)); }
                    if (frame == 7) { peak = Capture(camera, directory, "frame_007_peak.png"); frames.Add(Frame("frame_007_peak.png", frame / 60f, peak)); }
                    if (frame == 15) { decay = Capture(camera, directory, "frame_015_decay.png"); frames.Add(Frame("frame_015_decay.png", frame / 60f, decay)); }
                    if (frame == 29) { complete = Capture(camera, directory, "frame_029_complete.png"); frames.Add(Frame("frame_029_complete.png", frame / 60f, complete)); }
                }
                Assert.That(controller.IsAlive, Is.False);
                Assert.That(core, Is.Not.Null); Assert.That(peak, Is.Not.Null); Assert.That(decay, Is.Not.Null); Assert.That(complete, Is.Not.Null);
                Assert.That(core.WhiteClipRatio, Is.LessThan(.03f), "The short core may be bright but must not become a large white plate.");
                Assert.That(peak.WhiteClipRatio, Is.LessThan(.01f), "The combined peak must preserve ice facets instead of clipping them to white.");
                Assert.That(decay.P95Luminance, Is.LessThan(peak.P95Luminance), "The real timeline must lose energy after the peak.");
                Assert.That(peak.ForegroundPixels, Is.GreaterThan(1000), "Peak composition must remain visibly populated.");
                Assert.That(complete.ForegroundPixels, Is.EqualTo(0), "The formal completion frame must be empty for pooling.");
                File.WriteAllText(Path.Combine(directory, "metadata.json"), "{\n  \"capture\": \"PlayMode natural Update; one Play call; same serialized Preview Camera; no particle sampling, Emit, SetParticles, or replacement camera\",\n  \"scene\": \"" + ScenePath + "\",\n  \"fps\": 60,\n  \"camera\": { \"orthographicSize\": " + camera.orthographicSize.ToString("0.###", CultureInfo.InvariantCulture) + ", \"background\": [" + camera.backgroundColor.r.ToString("0.###", CultureInfo.InvariantCulture) + "," + camera.backgroundColor.g.ToString("0.###", CultureInfo.InvariantCulture) + "," + camera.backgroundColor.b.ToString("0.###", CultureInfo.InvariantCulture) + "] },\n  \"frames\": [\n    " + string.Join(",\n    ", frames) + "\n  ]\n}\n");
            }
            finally { Time.captureFramerate = priorCaptureRate; Time.captureDeltaTime = priorDelta; Application.targetFrameRate = priorTarget; controller.ResetForPool(); }
        }

        private static string EvidenceDirectory() { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "vfx-reviews", "frost_impact_2d", "evidence", "current-run")); }
        private static string Frame(string file, float time, FrameMetrics metrics) { return "{ \"file\": \"" + file + "\", \"time\": " + time.ToString("0.######", CultureInfo.InvariantCulture) + ", \"foregroundPixels\": " + metrics.ForegroundPixels + ", \"meanLuminance\": " + metrics.MeanLuminance.ToString("0.###", CultureInfo.InvariantCulture) + ", \"p95Luminance\": " + metrics.P95Luminance.ToString("0.###", CultureInfo.InvariantCulture) + ", \"whiteClipRatio\": " + metrics.WhiteClipRatio.ToString("0.######", CultureInfo.InvariantCulture) + " }"; }
        private static FrameMetrics Capture(Camera camera, string directory, string file)
        {
            var render = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32); var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = render; camera.Render(); RenderTexture.active = render; var image = new Texture2D(Width, Height, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); image.Apply(false);
                var pixels = image.GetPixels32(); var background = pixels[0]; var luminance = new List<float>(); var clipped = 0;
                foreach (var pixel in pixels)
                {
                    if (Mathf.Max(Mathf.Abs(pixel.r - background.r), Mathf.Abs(pixel.g - background.g), Mathf.Abs(pixel.b - background.b)) <= 10f) continue;
                    luminance.Add(.2126f * pixel.r + .7152f * pixel.g + .0722f * pixel.b); if (pixel.r > 245 && pixel.g > 245 && pixel.b > 245) clipped++;
                }
                luminance.Sort(); var metrics = new FrameMetrics { ForegroundPixels = luminance.Count, MeanLuminance = luminance.Count == 0 ? 0f : Sum(luminance) / luminance.Count, P95Luminance = luminance.Count == 0 ? 0f : luminance[Mathf.FloorToInt((luminance.Count - 1) * .95f)], WhiteClipRatio = luminance.Count == 0 ? 0f : (float)clipped / luminance.Count };
                File.WriteAllBytes(Path.Combine(directory, file), image.EncodeToPNG()); UnityEngine.Object.Destroy(image); return metrics;
            }
            finally { camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(render); }
        }

        private static float Sum(List<float> values) { var total = 0f; foreach (var value in values) total += value; return total; }
        private sealed class FrameMetrics { public int ForegroundPixels; public float MeanLuminance; public float P95Luminance; public float WhiteClipRatio; }
    }
}
