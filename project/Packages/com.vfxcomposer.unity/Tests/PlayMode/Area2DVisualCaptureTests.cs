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
    public sealed class Area2DVisualCaptureTests
    {
        private const string ScenePath = "Assets/VFX/Preview/VFXPREVIEW_Area2D.unity";
        private const int Width = 768;
        private const int Height = 432;

        [UnityTest]
        public IEnumerator CaptureActualInfernoArea_FromSerializedCameraAndNaturalUpdate()
        {
            var operation = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single); Assert.That(operation, Is.Not.Null, "Area preview scene must be enabled in Build Settings."); yield return operation;
            var scene = SceneManager.GetSceneByPath(ScenePath); Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
            var cameras = new List<Camera>(); var controllers = new List<InfernoAreaVfxController>(); var drivers = new List<AreaPreviewPlaybackDriver>();
            foreach (var root in scene.GetRootGameObjects()) { cameras.AddRange(root.GetComponentsInChildren<Camera>(true)); controllers.AddRange(root.GetComponentsInChildren<InfernoAreaVfxController>(true)); drivers.AddRange(root.GetComponentsInChildren<AreaPreviewPlaybackDriver>(true)); }
            Assert.That(cameras.Count, Is.EqualTo(1)); Assert.That(controllers.Count, Is.EqualTo(1)); Assert.That(drivers.Count, Is.EqualTo(1));
            var camera = cameras[0]; var controller = controllers[0]; drivers[0].enabled = false; controller.ResetForPool(); yield return null;
            var directory = EvidenceDirectory(); Directory.CreateDirectory(directory); foreach (var old in Directory.GetFiles(directory, "frame_*.png")) File.Delete(old);
            var frames = new List<string>(); FrameMetrics ignition = null; FrameMetrics established = null; FrameMetrics pulse = null; FrameMetrics stopping = null; FrameMetrics complete = null;
            var priorCaptureRate = Time.captureFramerate; var priorDelta = Time.captureDeltaTime; var priorTarget = Application.targetFrameRate;
            try
            {
                Time.captureFramerate = 60; Time.captureDeltaTime = 1f / 60f; Application.targetFrameRate = 60;
                var empty = Capture(camera, directory, "frame_000_empty.png"); frames.Add(Frame("frame_000_empty.png", 0f, empty)); controller.Play();
                for (var frame = 1; frame <= 120; frame++)
                {
                    yield return null;
                    if (frame == 9) { ignition = Capture(camera, directory, "frame_009_ignition.png"); frames.Add(Frame("frame_009_ignition.png", frame / 60f, ignition)); }
                    if (frame == 42) { established = Capture(camera, directory, "frame_042_established.png"); frames.Add(Frame("frame_042_established.png", frame / 60f, established)); }
                    if (frame == 86) { pulse = Capture(camera, directory, "frame_086_tick_pulse.png"); frames.Add(Frame("frame_086_tick_pulse.png", frame / 60f, pulse)); }
                }
                controller.Stop(VfxStopMode.AllowTail);
                for (var frame = 1; frame <= 27; frame++)
                {
                    yield return null;
                    if (frame == 10) { stopping = Capture(camera, directory, "frame_130_stopping.png"); frames.Add(Frame("frame_130_stopping.png", 2f + frame / 60f, stopping)); }
                    if (frame == 27) { complete = Capture(camera, directory, "frame_147_complete.png"); frames.Add(Frame("frame_147_complete.png", 2f + frame / 60f, complete)); }
                }
                Assert.That(ignition, Is.Not.Null); Assert.That(established, Is.Not.Null); Assert.That(pulse, Is.Not.Null); Assert.That(stopping, Is.Not.Null); Assert.That(complete, Is.Not.Null);
                Assert.That(ignition.ForegroundPixels, Is.GreaterThan(800), "Ignition must establish more than a placeholder spark."); Assert.That(established.ForegroundPixels, Is.GreaterThan(8000), "The sustained field must have broad layered screen presence."); Assert.That(established.WhiteClipRatio, Is.LessThan(.04f), "The active field must not collapse into a white plate."); Assert.That(pulse.ForegroundPixels, Is.GreaterThan(established.ForegroundPixels * .65f), "The tick pulse must preserve the established fire field."); Assert.That(stopping.MeanLuminance, Is.LessThan(established.MeanLuminance), "AllowTail must visibly lose heat before pooling."); Assert.That(complete.ForegroundPixels, Is.EqualTo(0), "Completion frame must be empty after the stop tail."); Assert.That(controller.IsAlive, Is.False);
                File.WriteAllText(Path.Combine(directory, "metadata.json"), "{\n  \"capture\": \"PlayMode natural Update; one serialized Preview Camera; one Play, one AllowTail Stop; no Emit, SetParticles, sampling, or replacement camera\",\n  \"scene\": \"" + ScenePath + "\",\n  \"fps\": 60,\n  \"camera\": { \"orthographicSize\": " + camera.orthographicSize.ToString("0.###", CultureInfo.InvariantCulture) + ", \"background\": [" + camera.backgroundColor.r.ToString("0.###", CultureInfo.InvariantCulture) + "," + camera.backgroundColor.g.ToString("0.###", CultureInfo.InvariantCulture) + "," + camera.backgroundColor.b.ToString("0.###", CultureInfo.InvariantCulture) + "] },\n  \"runtimePng\": { \"path\": \"Assets/VFX/Shared/Fire/Textures/T_Fire_MaskAtlas_A_v1.png\", \"maxSourceBytes\": 65536 },\n  \"frames\": [\n    " + string.Join(",\n    ", frames) + "\n  ]\n}\n");
            }
            finally { Time.captureFramerate = priorCaptureRate; Time.captureDeltaTime = priorDelta; Application.targetFrameRate = priorTarget; controller.ResetForPool(); }
        }

        private static string EvidenceDirectory() { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "vfx-reviews", "inferno_vortex_area_2d", "evidence", "current-run")); }
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
