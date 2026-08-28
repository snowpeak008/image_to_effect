using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using VFXComposer.W24;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class W24ContinuousCaptureRecorderTests
    {
        [UnityTest]
        public IEnumerator Recorder_WritesBeautyMinimalDiagnosticAndFrozenEvidenceMetadata_FromItsSerializedCamera()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("W24 graphics capture test requires tools/Invoke-Unity.ps1 -Mode PlayMode -UseGraphics.");
            var root = TemporaryDirectory();
            var cameraObject = new GameObject("SerializedMainCamera");
            var effect = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var priorCaptureRate = Time.captureFramerate; var priorCaptureDelta = Time.captureDeltaTime; var priorTargetRate = Application.targetFrameRate;
            try
            {
                cameraObject.transform.position = new Vector3(0f, 0f, -4f);
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.allowHDR = false; camera.allowMSAA = false; camera.orthographic = true; camera.orthographicSize = 1.5f;
                effect.layer = 8; effect.transform.position = Vector3.zero; effect.transform.localScale = Vector3.one;
                var recorder = cameraObject.AddComponent<W24ContinuousCaptureRecorder>();
                recorder.AuthorityCamera = camera; recorder.DiagnosticEffectLayers = 1 << 8;
                var profile = Profile(); var sources = Sources();
                recorder.Begin(root, "C0", profile, sources, true);
                var observedPlayerLoopFrames = 0;
                recorder.AfterPlayerLoopFrame += (frame, time) => observedPlayerLoopFrames++;
                yield return null; // Allows the scene to have completed one normal player-loop frame before the recorder observes it.
                Assert.That(observedPlayerLoopFrames, Is.GreaterThan(0));
                Assert.That(Time.captureFramerate, Is.EqualTo(profile.FramesPerSecond));
                Assert.Throws<ArgumentException>(() => recorder.CaptureFrame(1, float.NaN, "steady", profile.CanonicalSeed));
                recorder.CaptureFrame(1, 1f / 60f, "steady", profile.CanonicalSeed);
                recorder.CaptureFrame(1, 1f / 60f, "steady", profile.RobustnessSeeds[0]);
                recorder.CaptureFrame(1, 1f / 60f, "steady", profile.RobustnessSeeds[1]);
                var supplementalHash = recorder.WriteSupplementalDiagnostic("diagnostics/receiver-light-ab.json", System.Text.Encoding.UTF8.GetBytes("{\"delta\":0.1}"), "receiver-linear-luminance-ab", "Test-only matched A/B receiver measurement.");
                StringAssert.StartsWith("sha256:", supplementalHash);
                var telemetryHash = recorder.WriteSemanticTelemetry("diagnostics/semantic-telemetry.json", System.Text.Encoding.UTF8.GetBytes("{\"state\":\"steady\"}"), "Test-only natural player-loop semantic facts.");
                StringAssert.StartsWith("sha256:", telemetryHash);
                Assert.Throws<ArgumentException>(() => recorder.WriteSupplementalDiagnostic("receiver-light-ab.json", new byte[] { 1 }, "kind", "description"));
                Assert.Throws<ArgumentException>(() => recorder.WriteSemanticTelemetry("diagnostics/semantic-telemetry.txt", new byte[] { 1 }, "wrong extension"));
                Assert.Throws<InvalidOperationException>(() => recorder.CaptureFrame(1, 1f / 60f, "steady", profile.CanonicalSeed));
                recorder.Complete();
                Assert.That(Time.captureFramerate, Is.EqualTo(priorCaptureRate));
                Assert.That(Time.captureDeltaTime, Is.EqualTo(priorCaptureDelta));
                Assert.That(Application.targetFrameRate, Is.EqualTo(priorTargetRate));

                var metadataPath = Path.Combine(root, "capture-metadata.json");
                var diagnosticPath = Path.Combine(root, "diagnostic-pass-manifest.json");
                Assert.That(File.Exists(metadataPath), Is.True);
                Assert.That(File.Exists(diagnosticPath), Is.True);
                var metadata = File.ReadAllText(metadataPath);
                StringAssert.Contains("graphics-device batchmode required; -nographics prohibited", metadata);
                StringAssert.Contains("\"executedInBatchMode\":", metadata);
                StringAssert.Contains("effect-only-rgba", metadata);
                StringAssert.Contains("captureProfileSha256", metadata);
                StringAssert.Contains("\"scene\"", metadata);
                StringAssert.Contains("\"buildHash\"", metadata);
                StringAssert.Contains("frameRetentionPolicy", metadata);
                StringAssert.Contains("retainedFrameIndicesSha256", metadata);
                StringAssert.Contains("supplementalDiagnostics", metadata);
                StringAssert.Contains("semanticTelemetry", metadata);
                StringAssert.Contains("semantic-telemetry.json", metadata);
                StringAssert.Contains("receiver-light-ab.json", metadata);
                StringAssert.Contains("\"seed\":101", metadata);
                StringAssert.Contains("\"seed\":202", metadata);
                StringAssert.Contains("\"seed\":303", metadata);
                Assert.That(Directory.GetFiles(root, "*_beauty.png", SearchOption.AllDirectories), Has.Length.EqualTo(3));
                Assert.That(Directory.GetFiles(root, "*_effect-only.png", SearchOption.AllDirectories), Has.Length.EqualTo(3));
                Assert.Throws<InvalidOperationException>(() => recorder.CaptureFrame(2, 2f / 60f, "steady", profile.CanonicalSeed));
            }
            finally
            {
                if (effect != null) UnityEngine.Object.Destroy(effect);
                if (cameraObject != null) UnityEngine.Object.Destroy(cameraObject);
                DeleteTemporaryDirectory(root);
            }
        }

        [UnityTest]
        public IEnumerator Recorder_RestoresTimingAfterCompleteFailureAndDisable()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("W24 graphics capture test requires tools/Invoke-Unity.ps1 -Mode PlayMode -UseGraphics.");
            var root = TemporaryDirectory(); var disabledRoot = TemporaryDirectory(); var abortedRoot = TemporaryDirectory(); var destroyedRoot = TemporaryDirectory(); var cameraObject = new GameObject("SerializedMainCamera");
            var priorCaptureRate = Time.captureFramerate; var priorCaptureDelta = Time.captureDeltaTime; var priorTargetRate = Application.targetFrameRate;
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.allowHDR = false; camera.allowMSAA = false;
                var recorder = cameraObject.AddComponent<W24ContinuousCaptureRecorder>(); recorder.AuthorityCamera = camera; recorder.DiagnosticEffectLayers = 1 << 8;
                recorder.Begin(root, "C0", Profile(), Sources(), true);
                File.WriteAllText(Path.Combine(root, "diagnostic-pass-manifest.json"), "test-forced-write-once-collision");
                var completionFailure = Assert.Throws<InvalidOperationException>(() => recorder.Complete());
                StringAssert.Contains("already exists", completionFailure.Message, "The original write-once failure must not be replaced by timing restoration.");
                Assert.That(Time.captureFramerate, Is.EqualTo(priorCaptureRate));
                Assert.That(Time.captureDeltaTime, Is.EqualTo(priorCaptureDelta));
                Assert.That(Application.targetFrameRate, Is.EqualTo(priorTargetRate));

                recorder.enabled = true;
                recorder.Begin(disabledRoot, "C1", Profile(), Sources(), true);
                recorder.enabled = false;
                Assert.That(Time.captureFramerate, Is.EqualTo(priorCaptureRate));
                Assert.That(Time.captureDeltaTime, Is.EqualTo(priorCaptureDelta));
                Assert.That(Application.targetFrameRate, Is.EqualTo(priorTargetRate));

                recorder.enabled = true;
                recorder.Begin(abortedRoot, "C2", Profile(), Sources(), true);
                recorder.Abort();
                Assert.That(Time.captureFramerate, Is.EqualTo(priorCaptureRate));
                Assert.That(Time.captureDeltaTime, Is.EqualTo(priorCaptureDelta));
                Assert.That(Application.targetFrameRate, Is.EqualTo(priorTargetRate));

                recorder.Begin(destroyedRoot, "C3", Profile(), Sources(), true);
                UnityEngine.Object.Destroy(recorder);
                // WaitForEndOfFrame is not guaranteed to resume in Editor batchmode after the
                // only Camera component is destroyed.  A normal frame is sufficient to execute
                // the deferred Destroy and its OnDestroy timing restoration.
                yield return null;
                Assert.That(Time.captureFramerate, Is.EqualTo(priorCaptureRate));
                Assert.That(Time.captureDeltaTime, Is.EqualTo(priorCaptureDelta));
                Assert.That(Application.targetFrameRate, Is.EqualTo(priorTargetRate));
            }
            finally
            {
                Time.captureFramerate = priorCaptureRate; Time.captureDeltaTime = priorCaptureDelta; Application.targetFrameRate = priorTargetRate;
                if (cameraObject != null) UnityEngine.Object.Destroy(cameraObject);
                DeleteTemporaryDirectory(root); DeleteTemporaryDirectory(disabledRoot); DeleteTemporaryDirectory(abortedRoot); DeleteTemporaryDirectory(destroyedRoot);
            }
        }

        [UnityTest]
        public IEnumerator Recorder_FormalObservedTokens_RejectDuplicateAndSkippedPlayerLoopFrames()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("W24 graphics capture test requires tools/Invoke-Unity.ps1 -Mode PlayMode -UseGraphics.");
            var root = TemporaryDirectory();
            var secondRoot = TemporaryDirectory();
            var cameraObject = new GameObject("FormalTokenCamera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.allowHDR = false; camera.allowMSAA = false;
                var recorder = cameraObject.AddComponent<W24ContinuousCaptureRecorder>(); recorder.AuthorityCamera = camera; recorder.DiagnosticEffectLayers = 1 << 8;
                // The test-only Begin overload preserves graphics-device execution but bypasses
                // batchmode.  BeginFormal's token protocol is selected by its command hash.
                recorder.BeginFormalForGraphicsTest(root, "C0", Profile(), Sources(), Hash('9'));
                // The UnityTest coroutine can begin after this frame's LateUpdate.  Prime with
                // one complete normal frame; subsequent EndOfFrame yields then each cross a real
                // LateUpdate and expose exactly one recorder token.
                yield return null;
                var token = recorder.ConsumeCompletedPlayerLoopToken();
                recorder.AcknowledgeObservedPlayerLoopFrame(token);
                Assert.Throws<InvalidOperationException>(() => recorder.AcknowledgeObservedPlayerLoopFrame(token), "A token may be consumed exactly once.");

                yield return null;
                // Deliberately leave this real LateUpdate observation unconsumed.  The next one
                // must mark the formal run invalid rather than silently dropping a frame.
                recorder.ConsumeCompletedPlayerLoopToken();
                yield return null;
                Assert.Throws<InvalidOperationException>(() => recorder.ConsumeCompletedPlayerLoopToken(), "A formal lifecycle cannot skip a PlayerLoop token.");
                recorder.Abort();

                // Sealing is independently fail-closed: even if no later frame exposes the
                // skip, Complete must reject an unacknowledged natural LateUpdate observation.
                recorder.BeginFormalForGraphicsTest(secondRoot, "C4", Profile(), Sources(), Hash('8'));
                // A fresh formal session has no relationship to the previous frame phase.  Give
                // it one complete normal PlayerLoop so its own LateUpdate token exists, then
                // deliberately leave that token unconsumed for the Complete() guard below.
                yield return null;
                Assert.Throws<InvalidOperationException>(() => recorder.Complete(), "Formal evidence may not seal with an unconsumed LateUpdate token.");
                recorder.Abort();
            }
            finally
            {
                if (cameraObject != null) UnityEngine.Object.Destroy(cameraObject);
                DeleteTemporaryDirectory(root);
                DeleteTemporaryDirectory(secondRoot);
            }
        }

        private static W24CaptureProfile Profile()
        {
            return new W24CaptureProfile
            {
                UnityVersion = Application.unityVersion,
                UrpVersion = "14.0-test",
                GraphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                GraphicsDevice = SystemInfo.graphicsDeviceName,
                GraphicsDriverVersion = SystemInfo.graphicsDeviceVersion,
                RenderTextureFormat = RenderTextureFormat.ARGB32.ToString(),
                RendererAssetReference = "Assets/VFX/W24/Tests/Renderer.asset",
                RendererAssetSha256 = Hash('f'),
                VolumeReference = "Assets/VFX/W24/Tests/Volume.asset",
                VolumeSha256 = Hash('a'),
                ColorSpace = QualitySettings.activeColorSpace.ToString(),
                ScenePath = "Assets/VFX/W24/Tests/SerializedCaptureScene.unity",
                SerializedCameraReference = "SerializedCaptureScene/MainCamera",
                Width = 96,
                Height = 64,
                FramesPerSecond = 60,
                Background = Color.black,
                CanonicalSeed = 101,
                RobustnessSeeds = new[] { 202, 303 },
                RetainedFrameIndices = new[] { 1 }
            };
        }

        private static W24CaptureSourceHashes Sources()
        {
            return new W24CaptureSourceHashes
            {
                SceneSha256 = Hash('b'),
                SceneSourcePath = "Assets/VFX/W24/Tests/SerializedCaptureScene.unity",
                PrefabGuid = "00000000000000000000000000000000",
                PrefabSourcePath = "Assets/VFX/W24/Tests/Effect.prefab",
                PrefabSha256 = Hash('c'),
                ManifestSourcePath = "Assets/VFX/W24/Tests/BuildManifest.json",
                ManifestSha256 = Hash('d'),
                BuildHash = Hash('e'),
                CaptureToolSourcePath = "Packages/com.vfxcomposer.unity/Runtime/Diagnostics/W24ContinuousCaptureRecorder.cs",
                CaptureToolVersion = "w24-test-tool/v1",
                CaptureToolSha256 = Hash('f')
            };
        }

        private static string TemporaryDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "vfxcomposer-w24-playmode-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path); return path;
        }

        private static void DeleteTemporaryDirectory(string path)
        {
            if (!Directory.Exists(path)) return;
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(path, true);
        }

        private static string Hash(char character) { return "sha256:" + new string(character, 64); }
    }
}
