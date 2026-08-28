using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VFXComposer.Editor.W24;
using VFXComposer.Editor.W24.S0a;
using VFXComposer.W24;

namespace VFXComposer.Tests.PlayMode.W24S0aFormal
{
    /// <summary>
    /// This is the only S0a formal capture entry point. It deliberately consumes a fixed
    /// operator-only cohort name, never a caller path, label, answer ledger, or blind payload.
    /// It writes candidate evidence only; it does not run Visual QA, freeze labels, calculate
    /// metrics, or emit an S0a terminal status.
    /// </summary>
    public static class W24S0aFormalCalibrationCaptureAuthority
    {
        private const string RendererRelativePath = W24S0aFormalCaptureProtocol.RendererAssetReference;
        private const string GraphicsSettingsRelativePath = "ProjectSettings/GraphicsSettings.asset";

        public static IEnumerator CaptureReduced66()
        {
            Assert.That(Application.isPlaying, Is.True, "Formal S0a capture must execute inside real PlayMode.");
            return CaptureCohort(W24S0aCalibrationCohort.Reduced);
        }

        public static IEnumerator CaptureFull110()
        {
            if (!Directory.Exists(W24S0aOperatorCommandSet.GetCommandDirectory(W24S0aCalibrationCohort.Full)))
                Assert.Ignore("The future full 110-sample operator cohort has not been generated; no substitute input is accepted.");
            Assert.That(Application.isPlaying, Is.True, "Formal S0a capture must execute inside real PlayMode.");
            return CaptureCohort(W24S0aCalibrationCohort.Full);
        }

        private static IEnumerator CaptureCohort(W24S0aCalibrationCohort cohort)
        {
            W24ContinuousCaptureRecorder.RequireGraphicsBatchmode();
            RequireFormalInputs();
            var commands = W24S0aOperatorCommandSet.LoadCohort(cohort);
            Assert.That(commands.Commands.Count, Is.EqualTo(W24S0aOperatorCommandSet.ExpectedSampleCount(cohort)));
            if (W24S0aBatchCaptureRecovery.GetState(commands) == W24S0aBatchCaptureState.Complete)
                Assert.Ignore("The named S0a cohort already has sealed write-once capture output. It is intentionally not recaptured.");

            // This authority assembly is Editor-only by design, so its exact Preview scene does
            // not become a Player Build Settings dependency. Load the asset path through the
            // Editor PlayMode API while retaining real Update/LateUpdate execution.
            var operation = EditorSceneManager.LoadSceneAsyncInPlayMode(
                SustainedFlameAuthoring.PreviewScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            Assert.NotNull(operation, "The authority sustained-flame preview scene must be loadable from its exact project asset path.");
            yield return operation;
            var scene = SceneManager.GetSceneByPath(SustainedFlameAuthoring.PreviewScenePath);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
            var cameras = Find<Camera>(scene);
            var sceneControllers = Find<SustainedEffectController>(scene);
            Assert.That(cameras, Has.Length.EqualTo(1), "S0a must use exactly the serialized MainCamera from the authority preview scene.");
            Assert.That(cameras[0].name, Is.EqualTo("MainCamera"));
            Assert.That(sceneControllers, Has.Length.EqualTo(1), "The authority preview scene must contain exactly one source Runtime Entry.");

            var authorityCamera = cameras[0];
            var sourceEntry = sceneControllers[0];
            var wasSourceActive = sourceEntry.gameObject.activeSelf;
            var sourceSnapshot = W24S0aFixtureSession.SnapshotOfficialSources();
            sourceEntry.gameObject.SetActive(false); // runtime-only isolation; no asset/scene bytes are changed.
            try
            {
                foreach (var command in commands.Commands)
                    yield return CaptureOne(command, scene, authorityCamera, sourceSnapshot);
                Assert.That(
                    W24S0aBatchCaptureRecovery.GetState(commands),
                    Is.EqualTo(W24S0aBatchCaptureState.Complete),
                    "A formal S0a cohort is not complete until every write-once candidate passes full recovery validation.");
            }
            finally
            {
                if (sourceEntry != null) sourceEntry.gameObject.SetActive(wasSourceActive);
                W24S0aFixtureSession.VerifyOfficialSourcesUnchanged(sourceSnapshot);
            }
        }

        private static IEnumerator CaptureOne(W24S0aOperatorCommand command, Scene scene, Camera authorityCamera, IDictionary<string, string> sourceSnapshot)
        {
            W24S0aFixtureSession session = null;
            W24ContinuousCaptureRecorder recorder = null;
            try
            {
                session = W24S0aFixtureSession.Create(command.SourcePath, scene);
                Assert.That(session.FixedSeed, Is.EqualTo(command.FixedSeed));
                recorder = authorityCamera.gameObject.AddComponent<W24ContinuousCaptureRecorder>();
                recorder.AuthorityCamera = authorityCamera;
                recorder.DiagnosticEffectLayers = 1 << 1; // sustained-flame Runtime Entry layer; authority camera remains unchanged.
                var profile = Profile(authorityCamera, command.FixedSeed);
                var sources = Sources(ProjectRoot());
                session.BeginActualCapture(recorder, profile, sources);

                // UnityTest may enter after the current frame's LateUpdate.  Consume one
                // unmeasured priming frame before the declared seed timelines so frame 1 is
                // observed after a genuine LateUpdate in graphics-backed batchmode.
                yield return null;
                recorder.AcknowledgeObservedPlayerLoopFrame(recorder.ConsumeCompletedPlayerLoopToken());

                var telemetry = new List<string>();
                for (var seedOrdinal = 0; seedOrdinal < 3; seedOrdinal++)
                {
                    var seed = session.StartNextProfileSeed();
                    for (var frame = 1; frame <= 180; frame++)
                    {
                        // In Editor batchmode WaitForEndOfFrame may never resume. A normal frame
                        // yield crosses a genuine LateUpdate; the recorder token below proves that
                        // exact PlayerLoop observation and rejects skipped or duplicate frames.
                        yield return null;
                        RecordNaturalFrame(session, telemetry, frame, seed);
                    }
                    session.StopCurrentProfileSeed();
                    for (var frame = 181; frame <= 360; frame++)
                    {
                        yield return null;
                        RecordNaturalFrame(session, telemetry, frame, seed);
                    }
                }

                session.WriteActualSemanticTelemetry("diagnostics/semantic-telemetry.json", Encoding.UTF8.GetBytes("{\"schema\":\"w24-s0a-semantic-telemetry/v1\",\"sampleId\":\"" + command.SampleId + "\",\"fixedSeed\":" + command.FixedSeed.ToString(CultureInfo.InvariantCulture) + ",\"frames\":[" + string.Join(",", telemetry) + "]}"), "Natural PlayerLoop state and component telemetry for this single operator-command clone.");
                session.CompleteActualCapture(); // invalid-evidence controls derive their copy only after this seal.
                Assert.That(W24S0aFixtureSession.VerifyOfficialSourcesUnchanged(sourceSnapshot), Is.True);
            }
            finally
            {
                if (recorder != null && recorder.IsActive) recorder.Abort();
                if (recorder != null) UnityEngine.Object.Destroy(recorder);
                if (session != null) session.Dispose();
            }
            // Destroy is intentionally deferred to a genuine frame boundary so the next clone
            // never races the previous recorder on the serialized authority camera.
            yield return null;
        }

        private static void RecordNaturalFrame(W24S0aFixtureSession session, List<string> telemetry, int frame, uint seed)
        {
            var sample = session.Controller.ReadTelemetry();
            session.ObserveCompletedPlayerLoopFrame();
            if (W24S0aFormalCaptureProtocol.RetainedFrames.Contains(frame))
            {
                telemetry.Add("{\"frameIndex\":" + frame + ",\"state\":\"" + StateToken(sample.State) + "\",\"seed\":" + seed.ToString(CultureInfo.InvariantCulture) + ",\"liveParticleCount\":" + sample.LiveParticleCount + ",\"enabledRendererCount\":" + sample.EnabledRendererCount + ",\"enabledLightCount\":" + sample.EnabledLightCount + ",\"transitionSerial\":" + sample.TransitionSerial + ",\"cleanupComplete\":" + (sample.CleanupComplete ? "true" : "false") + "}");
            }
        }

        private static W24CaptureProfile Profile(Camera camera, uint fixedSeed)
        {
            var root = ProjectRoot();
            var robustness = W24S0aFormalCaptureProtocol.DeriveRobustnessSeeds(fixedSeed);
            return new W24CaptureProfile
            {
                ProfileVersion = "w24-s0a-formal-calibration-capture-profile/v1",
                UnityVersion = Application.unityVersion,
                UrpVersion = "14.0.12",
                GraphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                GraphicsDevice = SystemInfo.graphicsDeviceName,
                GraphicsDriverVersion = SystemInfo.graphicsDeviceVersion,
                RenderTextureFormat = RenderTextureFormat.ARGB32.ToString(),
                RendererAssetReference = RendererRelativePath,
                RendererAssetSha256 = HashFile(Path.Combine(root, "project", RendererRelativePath)),
                VolumeReference = W24S0aFormalCaptureProtocol.VolumeReference,
                VolumeSha256 = HashFile(Path.Combine(root, "project", GraphicsSettingsRelativePath)),
                ScenePath = SustainedFlameAuthoring.PreviewScenePath,
                SerializedCameraReference = SustainedFlameAuthoring.PreviewScenePath + "#MainCamera",
                Width = 960,
                Height = 540,
                FramesPerSecond = 60,
                Background = camera.backgroundColor,
                ColorSpace = QualitySettings.activeColorSpace.ToString(),
                Hdr = camera.allowHDR,
                Msaa = camera.allowMSAA,
                Bloom = false,
                ToneMapping = "None",
                CanonicalSeed = unchecked((int)fixedSeed),
                RobustnessSeeds = new[] { unchecked((int)robustness[0]), unchecked((int)robustness[1]) },
                RetainedFrameIndices = W24S0aFormalCaptureProtocol.RetainedFrames
            };
        }

        private static W24CaptureSourceHashes Sources(string root)
        {
            var project = Path.Combine(root, "project");
            var manifest = Path.Combine(project, SustainedFlameAuthoring.ManifestPath);
            var build = Regex.Match(File.ReadAllText(manifest), "\\\"buildHash\\\"\\s*:\\s*\\\"(?<hash>[0-9a-fA-F]{64})\\\"");
            Assert.That(build.Success, Is.True, "The sustained-flame BuildManifest must expose a 64-character buildHash.");
            return new W24CaptureSourceHashes
            {
                SceneSourcePath = Path.Combine(project, SustainedFlameAuthoring.PreviewScenePath),
                SceneSha256 = HashFile(Path.Combine(project, SustainedFlameAuthoring.PreviewScenePath)),
                PrefabSourcePath = Path.Combine(project, SustainedFlameAuthoring.PrefabPath),
                PrefabGuid = ReadGuid(Path.Combine(project, SustainedFlameAuthoring.PrefabPath) + ".meta"),
                PrefabSha256 = HashFile(Path.Combine(project, SustainedFlameAuthoring.PrefabPath)),
                ManifestSourcePath = manifest,
                ManifestSha256 = HashFile(manifest),
                BuildHash = "sha256:" + build.Groups["hash"].Value.ToLowerInvariant(),
                CaptureToolSourcePath = W24S0aFormalCaptureProtocol.CaptureToolIdentityPath,
                CaptureToolVersion = W24S0aFormalCaptureProtocol.CaptureToolVersion,
                CaptureToolSha256 = W24S0aFormalCaptureProtocol.CaptureToolSha256()
            };
        }


        private static void RequireFormalInputs()
        {
            var root = ProjectRoot();
            var required = new[]
            {
                Path.Combine(root, "project", SustainedFlameAuthoring.PreviewScenePath),
                Path.Combine(root, "project", SustainedFlameAuthoring.PrefabPath),
                Path.Combine(root, "project", SustainedFlameAuthoring.ManifestPath),
                Path.Combine(root, "project", RendererRelativePath),
                Path.Combine(root, "project", GraphicsSettingsRelativePath)
            }.Concat(W24S0aFormalCaptureProtocol.CaptureToolRelativePaths.Select(path => Path.Combine(root, "project", path))).ToArray();
            var missing = required.Where(path => !File.Exists(path)).ToArray();
            if (missing.Length > 0) Assert.Ignore("S0a formal capture precondition is not built yet. Missing: " + string.Join("; ", missing));
        }

        private static T[] Find<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        }

        private static string StateToken(SustainedEffectState state) { return state.ToString().ToLowerInvariant(); }
        private static string ProjectRoot() { return Directory.GetParent(Application.dataPath).Parent.FullName; }
        private static string ReadGuid(string metaPath)
        {
            var match = Regex.Match(File.ReadAllText(metaPath), "^guid:\\s*(?<guid>[0-9a-f]{32})", RegexOptions.Multiline);
            Assert.That(match.Success, Is.True, "The sustained-flame Prefab meta must contain a lowercase GUID.");
            return match.Groups["guid"].Value;
        }
        private static string HashFile(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return "sha256:" + string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }
}
