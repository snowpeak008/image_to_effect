using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VFXComposer;
using VFXComposer.W24;

namespace VFXComposer.Tests.PlayMode
{
    /// <summary>
    /// The formal S3 graphics-backed C0 capture producer.  These explicit tests execute only in
    /// a graphics-enabled batch Unity process after first-formal authoring has frozen C0.  They
    /// use each serialized preview MainCamera and normal player-loop frames, then hand the sealed
    /// evidence to the single S5 completion adapter.  They deliberately do not create QA or user
    /// verdicts: visualQa and user requirements remain pending after this machine-only step.
    /// </summary>
    [Explicit("Formal W24 S3 graphics evidence. Run only through Invoke-Unity.ps1 -Mode PlayMode -UseGraphics in an isolated project after the S3 C0 assets are built.")]
    public sealed class W24S3GraphicsCaptureEvidenceTests
    {
        private const string RendererRelativePath = "Assets/Settings/VFXPreviewUniversalRenderer.asset";
        private const string GraphicsRelativePath = "ProjectSettings/GraphicsSettings.asset";
        private const string BundleRelativePath = "docs/vfx-contracts/capture-tools/w24-s3-capture-tool.bundle.json";
        private const string ToolRelativePath = "Packages/com.vfxcomposer.unity/Tests/PlayMode/W24S3GraphicsCaptureEvidenceTests.cs";
        private const string CandidateId = "C0";
        private static readonly int[] RetainedFrames = { 1, 18, 48, 72, 96, 120 };
        private const int NaturalFramesPerSeed = 150; // Includes the post-stop bounded tail; only the frozen table above is retained.

        private sealed class CaptureCase
        {
            public string Id;
            public string Scene;
            public string Prefab;
            public string CompletionMethod;
            public int CanonicalSeed;
            public int RobustnessOne;
            public int RobustnessTwo;
            public int Mode; // 0 projectile; 1 bound fragments; 2 real-light receivers
        }

        private sealed class RunMeasurement
        {
            public uint Seed;
            public bool LifecycleClean;
            public int MotionSamples;
            public int PeakTrailVertices;
            public float TrailWorldSpan;
            public bool SocketBound;
            public bool SocketFollowsModel;
            public int IndependentFragmentCount;
            public int DistinctFragmentVelocityCount;
            public int EnabledLightCount;
            public bool BindingProbesPassed;
            public string BindingProbeJson;
            public readonly List<string> Frames = new List<string>();
            public string ToJson()
            {
                return "{\"seed\":" + Seed.ToString(CultureInfo.InvariantCulture) + ",\"lifecycleClean\":" + Bool(LifecycleClean) + ",\"motionSamples\":" + MotionSamples + ",\"peakTrailVertices\":" + PeakTrailVertices + ",\"trailWorldSpan\":" + Number(TrailWorldSpan) + ",\"socketBound\":" + Bool(SocketBound) + ",\"socketFollowsModel\":" + Bool(SocketFollowsModel) + ",\"independentFragmentCount\":" + IndependentFragmentCount + ",\"distinctFragmentVelocityCount\":" + DistinctFragmentVelocityCount + ",\"enabledLightCount\":" + EnabledLightCount + ",\"bindingProbesPassed\":" + Bool(BindingProbesPassed) + (string.IsNullOrEmpty(BindingProbeJson) ? string.Empty : ",\"bindingProbes\":" + BindingProbeJson) + ",\"frames\":[" + string.Join(",", Frames) + "]}";
            }
        }

        private sealed class ReceiverMeasurement
        {
            public uint Seed;
            public float OffA; public float OnA; public float OffB; public float OnB;
            public bool Passed { get { return OnA > OffA + .001f && OnB > OffB + .001f; } }
            public string ToJson()
            {
                return "{\"seed\":" + Seed.ToString(CultureInfo.InvariantCulture) + ",\"receiverA\":{\"off\":" + Number(OffA) + ",\"on\":" + Number(OnA) + ",\"delta\":" + Number(OnA - OffA) + "},\"receiverB\":{\"off\":" + Number(OffB) + ",\"on\":" + Number(OnB) + ",\"delta\":" + Number(OnB - OffB) + "},\"passed\":" + Bool(Passed) + "}";
            }
        }

        private sealed class RawDiagnostic
        {
            public string Id;
            public string Path;
            public string Hash;
            public string PassId;
            public string Encoding;
            public uint Seed;
            public int Frame;
            public string ViewId;
            public string DerivedFrom;
            public int PlayerLoopSerial;
            public int PlayerLoopFrame;
            public float PlayerLoopTime;
            public JArray ProjectedHistoryPixels;
        }

        private struct RawTokenProvenance
        {
            public int Serial;
            public int Frame;
            public float Time;
        }

        /// <summary>
        /// The recorder token is deliberately opaque outside the Runtime assembly. Formal capture
        /// observes exactly one unconsumed token per natural LateUpdate and therefore records the
        /// same serial sequence locally. S5 later compares this value, frame and time against the
        /// recorder-sealed typed raw metadata; any divergence rejects the evidence. No private
        /// token-field reflection is used.
        /// </summary>
        private sealed class RawTokenSequence
        {
            private int nextSerial;

            public RawTokenProvenance Observe()
            {
                nextSerial++;
                return new RawTokenProvenance { Serial = nextSerial, Frame = Time.frameCount, Time = Time.time };
            }
        }

        private sealed class MetricsEvidence
        {
            public string ReportHash;
            public string AnalysisInputHash;
            public readonly Dictionary<string, List<string>> ChecksByRequirement = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            public readonly HashSet<string> VerifiedCheckIds = new HashSet<string>(StringComparer.Ordinal);

            public bool PassesRequirement(string requirementId)
            {
                List<string> required;
                return !string.IsNullOrEmpty(requirementId)
                    && ChecksByRequirement.TryGetValue(requirementId, out required)
                    && required.Count > 0
                    && required.Distinct(StringComparer.Ordinal).Count() == required.Count
                    && required.All(VerifiedCheckIds.Contains);
            }
        }

        private sealed class RequiredEvidenceRow
        {
            public string EvidenceId;
            public string PassId;
            public uint Seed;
            public string ViewId;
            public int Frame;
        }

        /// <summary>Frozen Contract matrix consumed directly by the producer and copied byte-for-byte semantically into metrics input.</summary>
        private sealed class RequiredEvidencePlan
        {
            public JObject Contract;
            public JArray Matrix;
            public readonly List<RequiredEvidenceRow> Rows = new List<RequiredEvidenceRow>();

            public static RequiredEvidencePlan Read(string root, CaptureCase item, W24CaptureProfile profile)
            {
                var contractPath = Path.Combine(root, "docs", "vfx-candidates", item.Id, CandidateId, "design-contract.json");
                var contract = JObject.Parse(File.ReadAllText(contractPath));
                var matrix = contract.SelectToken("extensions.typedDiagnostics.requiredEvidenceMatrix") as JArray;
                Assert.NotNull(matrix, "Formal S3 capture requires Contract.extensions.typedDiagnostics.requiredEvidenceMatrix.");
                Assert.That(matrix.Count, Is.GreaterThan(0), "Formal S3 capture refuses an empty required evidence matrix.");
                var plan = new RequiredEvidencePlan { Contract = contract, Matrix = (JArray)matrix.DeepClone() };
                var profileSeeds = new HashSet<uint>(profile.AllSeeds().Select(value => unchecked((uint)value)));
                var seenIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var json in matrix.OfType<JObject>())
                {
                    var id = (string)json["evidenceId"]; var pass = (string)json["passId"]; var view = (string)json["viewId"];
                    var seed = (long?)json["seed"]; var frame = (long?)json["logicalFrameIndex"];
                    Assert.That(ProtocolToken(id) && ProtocolToken(pass) && ProtocolToken(view) && seed.HasValue && seed.Value >= 0 && seed.Value <= uint.MaxValue && frame.HasValue && frame.Value >= 0 && frame.Value <= int.MaxValue && seenIds.Add(id), Is.True, "Required evidence matrix has an invalid or duplicate row.");
                    var row = new RequiredEvidenceRow { EvidenceId = id, PassId = pass, Seed = (uint)seed.Value, ViewId = view, Frame = (int)frame.Value };
                    Assert.That(profileSeeds.Contains(row.Seed), Is.True, "Required evidence matrix may only name frozen capture-profile seeds.");
                    plan.Rows.Add(row);
                }
                return plan;
            }

            public IEnumerable<RequiredEvidenceRow> RowsFor(uint seed, int frame, string passId)
            {
                return Rows.Where(row => row.Seed == seed && row.Frame == frame && string.Equals(row.PassId, passId, StringComparison.Ordinal));
            }

            public RequiredEvidenceRow Require(uint seed, int frame, string passId, string viewId)
            {
                var matches = Rows.Where(row => row.Seed == seed && row.Frame == frame && string.Equals(row.PassId, passId, StringComparison.Ordinal) && string.Equals(row.ViewId, viewId, StringComparison.Ordinal)).ToArray();
                Assert.That(matches, Has.Length.EqualTo(1), "Required evidence matrix must contain exactly one " + passId + " row for seed/frame/view.");
                return matches[0];
            }

            public RequiredEvidenceRow RequireMarked(uint seed, int frame, string passId, string viewId, string idMarker)
            {
                var matches = Rows.Where(row => row.Seed == seed && row.Frame == frame && string.Equals(row.PassId, passId, StringComparison.Ordinal) && string.Equals(row.ViewId, viewId, StringComparison.Ordinal) && row.EvidenceId.IndexOf(idMarker, StringComparison.Ordinal) >= 0).ToArray();
                Assert.That(matches, Has.Length.EqualTo(1), "Required evidence matrix must contain exactly one marked " + passId + " row for seed/frame/view.");
                return matches[0];
            }

            public void AssertExactlyMatches(IEnumerable<RawDiagnostic> rawDiagnostics)
            {
                var raw = rawDiagnostics.ToArray();
                Assert.That(raw.Length, Is.EqualTo(Rows.Count), "Every frozen matrix row must produce exactly one typed raw diagnostic.");
                Assert.That(raw.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(raw.Length), "Typed raw evidence IDs must be unique.");
                foreach (var item in raw)
                {
                    var expected = Rows.SingleOrDefault(row => string.Equals(row.EvidenceId, item.Id, StringComparison.Ordinal));
                    Assert.NotNull(expected, "Typed raw evidence may not exist outside the frozen Contract matrix: " + item.Id);
                    Assert.That(item.PassId, Is.EqualTo(expected.PassId));
                    Assert.That(item.Seed, Is.EqualTo(expected.Seed));
                    Assert.That(item.Frame, Is.EqualTo(expected.Frame));
                    Assert.That(item.ViewId, Is.EqualTo(expected.ViewId));
                    Assert.That(item.PlayerLoopSerial, Is.GreaterThan(0));
                    Assert.That(item.PlayerLoopFrame, Is.GreaterThanOrEqualTo(0));
                    Assert.That(string.IsNullOrWhiteSpace(item.DerivedFrom), Is.False, "Every typed raw diagnostic must preserve derivation provenance.");
                }
                foreach (var row in Rows)
                    Assert.That(raw.Count(item => string.Equals(item.Id, row.EvidenceId, StringComparison.Ordinal)), Is.EqualTo(1), "Every frozen Contract evidence row must map back to one raw diagnostic.");
            }
        }

        private static readonly CaptureCase Projectile = new CaptureCase
        {
            Id = "w24_moving_projectile_trail", Scene = "Assets/VFX/Preview/W24S3/VFXPREVIEW_MovingProjectileTrail.unity",
            Prefab = "Assets/VFX/Generated/w24_moving_projectile_trail/VFX_w24_moving_projectile_trail.prefab",
            CompletionMethod = "FinalizeS3MovingProjectileC0Capture", CanonicalSeed = 24101, RobustnessOne = 24111, RobustnessTwo = 24121, Mode = 0
        };
        private static readonly CaptureCase Binding = new CaptureCase
        {
            Id = "w24_weapon_socket_fragments", Scene = "Assets/VFX/Preview/W24S3/VFXPREVIEW_ModelSocketFragments.unity",
            Prefab = "Assets/VFX/Generated/w24_weapon_socket_fragments/VFX_w24_weapon_socket_fragments.prefab",
            CompletionMethod = "FinalizeS3WeaponSocketFragmentsC0Capture", CanonicalSeed = 24201, RobustnessOne = 24211, RobustnessTwo = 24221, Mode = 1
        };
        private static readonly CaptureCase Lighting = new CaptureCase
        {
            Id = "w24_real_light_receivers", Scene = "Assets/VFX/Preview/W24S3/VFXPREVIEW_RealLightReceivers.unity",
            Prefab = "Assets/VFX/Generated/w24_real_light_receivers/VFX_w24_real_light_receivers.prefab",
            CompletionMethod = "FinalizeS3RealLightReceiversC0Capture", CanonicalSeed = 24301, RobustnessOne = 24311, RobustnessTwo = 24321, Mode = 2
        };

        [UnityTest, Timeout(600000)]
        public IEnumerator Capture_MovingProjectileTrail_C0_UsesSerializedCameraAndNaturalFrames()
        {
            yield return Capture(Projectile);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Capture_WeaponSocketFragments_C0_UsesSerializedCameraAndNaturalFrames()
        {
            yield return Capture(Binding);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Capture_RealLightReceivers_C0_UsesSerializedCameraAndNaturalFrames()
        {
            yield return Capture(Lighting);
        }

        private static IEnumerator Capture(CaptureCase item)
        {
            W24ContinuousCaptureRecorder.RequireGraphicsBatchmode();
            RequireFormalInputs(item);
            var load = LoadFormalSceneAsset(item.Scene);
            Assert.NotNull(load, "Formal S3 preview Scene must be enabled for graphics capture.");
            yield return load;

            var scene = SceneManager.GetSceneByPath(item.Scene);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
            var camera = Find<Camera>(scene).SingleOrDefault();
            var entry = Find<W24S3RuntimeEntry>(scene).SingleOrDefault();
            var driver = Find<W24S3PreviewDriver>(scene).SingleOrDefault();
            Assert.NotNull(camera, "Formal S3 capture requires exactly one serialized MainCamera.");
            Assert.That(camera.name, Is.EqualTo("MainCamera"));
            Assert.NotNull(entry, "Formal S3 preview requires exactly one Runtime Entry.");
            Assert.NotNull(driver, "Formal S3 preview requires exactly one scene-only natural playback driver.");
            Assert.That(driver.enabled, Is.True, "Formal capture must run the serialized Preview Driver, not a substitute manual timeline.");

            var root = RepositoryRoot();
            var evidenceRoot = Path.Combine(root, "artifacts", "vfx-evidence", item.Id, CandidateId);
            if (Directory.Exists(evidenceRoot) && Directory.EnumerateFileSystemEntries(evidenceRoot).Any())
                Assert.Ignore("Formal S3 C0 evidence is write-once and already exists: " + evidenceRoot);

            var recorder = camera.gameObject.AddComponent<W24ContinuousCaptureRecorder>();
            recorder.AuthorityCamera = camera;
            recorder.DiagnosticEffectLayers = 1 << 1; // W24S3BaselineAuthoring freezes Runtime Entry content to TransparentFX.
            var profile = Profile(item, camera, root);
            var requiredEvidence = RequiredEvidencePlan.Read(root, item, profile);
            var sources = Sources(item, root, profile, camera);
            var measurements = new List<RunMeasurement>();
            var receiverMeasurements = new List<ReceiverMeasurement>();
            var rawDiagnostics = new List<RawDiagnostic>();
            var rawTokenSequence = new RawTokenSequence();
            string telemetryHash = null;
            try
            {
                // The formal lifecycle is bound to the frozen C0 receipt before any PlayerLoop
                // observation is accepted.  The persisted command artifact and the seal use the
                // identical canonical hash, so a later caller cannot relabel this capture.
                var command = CaptureCommandJson(root, item, profile);
                var commandHash = HashText(command);
                recorder.BeginFormal(evidenceRoot, CandidateId, profile, sources, commandHash);
                Assert.That(recorder.WriteSupplementalDiagnostic("diagnostics/operator-command.json", Encoding.UTF8.GetBytes(command), "formal-capture-command", "Frozen C0 receipt-bound formal capture command; this is provenance, not a QA verdict."), Is.EqualTo(commandHash));

                // Establish one complete, unmeasured PlayerLoop before seed 1.  UnityTest can
                // begin after LateUpdate; priming prevents the first EndOfFrame from being
                // mistaken for a completed observed frame while preserving natural playback.
                yield return null;
                recorder.AcknowledgeObservedPlayerLoopFrame(recorder.ConsumeCompletedPlayerLoopToken());
                rawTokenSequence.Observe();
                foreach (var seed in profile.AllSeeds())
                    yield return RunNaturalLifecycle(item, scene, entry, driver, recorder, unchecked((uint)seed), requiredEvidence, measurements, rawDiagnostics, rawTokenSequence);

                VerifyMeasurements(item, measurements);
                if (item.Mode == 2)
                {
                    yield return WriteReceiverDiagnostics(recorder, camera, entry, driver, scene, profile, requiredEvidence, receiverMeasurements, rawDiagnostics, rawTokenSequence);
                }
                requiredEvidence.AssertExactlyMatches(rawDiagnostics);
                var metrics = WriteMetricsEvidence(root, item, recorder, profile, requiredEvidence, rawDiagnostics);
                telemetryHash = recorder.WriteSemanticTelemetry("diagnostics/semantic-telemetry.json", Encoding.UTF8.GetBytes(SemanticTelemetry(item, measurements, receiverMeasurements)), "Runtime Entry/module readback, receiver-linear-luminance A/B cross-checks, and required negative binding probes measured for all frozen seeds after normal PlayerLoop frames.");
                recorder.WriteSupplementalDiagnostic("diagnostics/capture-diagnostic-summary.json", Encoding.UTF8.GetBytes(DiagnosticSummary(item, measurements)), "capture-diagnostic-summary", "Supplemental operator summary only; requirement authority is the sealed typed metrics report and semantic telemetry.");
                recorder.WriteSupplementalDiagnostic("diagnostics/machine-gate-trace.json", Encoding.UTF8.GetBytes(CompletedMachineTrace(root, item, telemetryHash, measurements, receiverMeasurements, metrics)), "machine-gate-trace", "Machine-only trace assembled from sealed typed raw diagnostics, measured metrics, and Runtime Entry telemetry; Visual QA and user authority remain pending.");
                recorder.Complete();
                FinalizeC0EvidenceThroughEditorGate(item);
            }
            finally
            {
                if (recorder != null && recorder.IsActive) recorder.Abort();
                entry.ResetForPool();
                UnityEngine.Object.Destroy(recorder);
            }
        }

        private static AsyncOperation LoadFormalSceneAsset(string scenePath)
        {
            // Contract-pinned Preview scenes are evidence inputs, not Player Build content.
            // Load them directly in Editor PlayMode without changing EditorBuildSettings.
            var type = Type.GetType("UnityEditor.SceneManagement.EditorSceneManager, UnityEditor");
            if (type == null) throw new InvalidOperationException("EditorSceneManager is unavailable for formal PlayMode capture.");
            var method = type.GetMethod("LoadSceneAsyncInPlayMode", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(string), typeof(LoadSceneParameters) }, null);
            if (method == null) throw new InvalidOperationException("EditorSceneManager.LoadSceneAsyncInPlayMode is unavailable.");
            var operation = method.Invoke(null, new object[] { scenePath, new LoadSceneParameters(LoadSceneMode.Single) }) as AsyncOperation;
            if (operation == null) throw new InvalidOperationException("Formal scene asset load did not return an AsyncOperation: " + scenePath);
            return operation;
        }

        private static IEnumerator RunNaturalLifecycle(CaptureCase item, Scene scene, W24S3RuntimeEntry entry, W24S3PreviewDriver driver, W24ContinuousCaptureRecorder recorder, uint seed, RequiredEvidencePlan requiredEvidence, List<RunMeasurement> measurements, List<RawDiagnostic> rawDiagnostics, RawTokenSequence rawTokenSequence)
        {
            var measured = new RunMeasurement { Seed = seed };
            var initialAttachmentLocal = Vector3.zero;
            var initialSocketWorld = Vector3.zero;
            var initialVisualWorld = Vector3.zero;
            var attachmentLocalCaptured = false;
            var logicalFrame = 0;
            var startedOnObservedBoundary = false;
            var completed = false;
            Exception observationFailure = null;
            Action<int, float> observer = null;
            observer = (playerLoopFrame, playerLoopTime) =>
            {
                try
                {
                    var token = recorder.ConsumeCompletedPlayerLoopToken();
                    var rawToken = rawTokenSequence.Observe();
                    if (!startedOnObservedBoundary)
                    {
                        recorder.AcknowledgeObservedPlayerLoopFrame(token);
                        driver.RestartForFormalCapture(seed);
                        Assert.That(entry.IsAlive, Is.True, "Formal S3 seed must activate through Runtime Entry.Play on a natural observed frame boundary.");
                        startedOnObservedBoundary = true;
                        return;
                    }

                    logicalFrame++;
                    var sample = entry.ReadSemanticTelemetry();
                    if (item.Mode == 0 && requiredEvidence.RowsFor(seed, logicalFrame, "trail-only-mask").Any())
                        WriteTrailMaskDiagnostic(recorder, token, rawToken, entry, requiredEvidence.Require(seed, logicalFrame, "trail-only-mask", "projectile_front_main"), rawDiagnostics);
                    if (item.Mode == 1 && requiredEvidence.RowsFor(seed, logicalFrame, "fragment-id").Any())
                        WriteBindingObjectIdDepthDiagnostics(recorder, token, rawToken, scene, entry, requiredEvidence, seed, logicalFrame, rawDiagnostics);
                    if (logicalFrame == 1 || RetainedFrames.Contains(logicalFrame))
                        measured.Frames.Add("{\"frameIndex\":" + logicalFrame + ",\"playerLoopFrame\":" + playerLoopFrame + ",\"simulationTime\":" + Number(playerLoopTime) + ",\"state\":\"" + sample.State.ToString().ToLowerInvariant() + "\",\"alive\":" + Bool(entry.IsAlive) + ",\"lastEvent\":\"" + Escape(sample.LastEventId) + "\",\"cleanupComplete\":" + Bool(sample.CleanupComplete) + "}");
                    if (RetainedFrames.Contains(logicalFrame)) recorder.CaptureObservedPlayerLoopFrame(token, logicalFrame, sample.State.ToString().ToLowerInvariant(), seed);
                    else recorder.AcknowledgeObservedPlayerLoopFrame(token);

                    if (item.Mode == 0)
                    {
                        var motion = entry.GetComponent<W24MovingEmitterTrailProtocol>();
                        measured.MotionSamples = Mathf.Max(measured.MotionSamples, motion == null ? 0 : motion.SampleCount);
                        var trail = entry.GetComponentInChildren<TrailRenderer>(true);
                        if (trail != null)
                        {
                            measured.PeakTrailVertices = Mathf.Max(measured.PeakTrailVertices, trail.positionCount);
                            if (trail.positionCount > 1)
                            {
                                var points = new Vector3[trail.positionCount]; trail.GetPositions(points);
                                measured.TrailWorldSpan = Mathf.Max(measured.TrailWorldSpan, points.Max(point => point.x) - points.Min(point => point.x));
                            }
                        }
                    }
                    else if (item.Mode == 1)
                    {
                        var model = Find<Transform>(scene).Single(value => value.name == "PreviewTestModel_MeshRenderer");
                        var socket = Find<Transform>(scene).Single(value => value.name == "weapon_socket");
                        // A successful socket binding intentionally reparents the visual out of the
                        // Runtime Entry hierarchy and under the model socket.  Resolve it from the
                        // authority scene so the measurement observes the post-bind topology.
                        var visual = Find<Transform>(scene).Single(value => value.name == "SocketVisualRoot");
                        var binding = entry.GetComponent<W24ModelBindingAdapter>();
                        measured.SocketBound |= binding != null && binding.Result.IsBound && visual.parent == socket && socket.IsChildOf(model);
                        if (!attachmentLocalCaptured && visual.parent == socket)
                        {
                            initialAttachmentLocal = visual.localPosition; initialSocketWorld = socket.position; initialVisualWorld = visual.position; attachmentLocalCaptured = true;
                        }
                        if (attachmentLocalCaptured && visual.parent == socket)
                        {
                            var preservedLocalAlignment = Vector3.Distance(initialAttachmentLocal, visual.localPosition) < .0001f;
                            var socketMoved = Vector3.Distance(initialSocketWorld, socket.position) > .0001f;
                            var visualMovedWithSocket = Vector3.Distance(initialVisualWorld, visual.position) > .0001f;
                            measured.SocketFollowsModel |= preservedLocalAlignment && socketMoved && visualMovedWithSocket;
                        }
                        var fragments = entry.GetComponentInChildren<W24FragmentMotionSystem>(true);
                        if (fragments != null && fragments.States.Count > 0)
                        {
                            measured.IndependentFragmentCount = Mathf.Max(measured.IndependentFragmentCount, fragments.States.Count);
                            measured.DistinctFragmentVelocityCount = Mathf.Max(measured.DistinctFragmentVelocityCount, fragments.States.Select(state => state.Velocity).Distinct().Count());
                        }
                    }
                    else
                        measured.EnabledLightCount = Mathf.Max(measured.EnabledLightCount, entry.GetComponentsInChildren<Light>(true).Count(light => light.enabled));

                    if (logicalFrame == NaturalFramesPerSeed) completed = true;
                }
                catch (Exception exception)
                {
                    observationFailure = exception;
                }
            };
            recorder.AfterPlayerLoopFrame += observer;
            try
            {
                while (!completed && observationFailure == null) yield return null;
            }
            finally
            {
                recorder.AfterPlayerLoopFrame -= observer;
            }
            if (observationFailure != null) throw observationFailure;
            Assert.That(startedOnObservedBoundary && logicalFrame == NaturalFramesPerSeed, Is.True, "Every S3 seed must consume one boundary token and the complete natural PlayerLoop frame span.");
            measured.LifecycleClean = !entry.IsAlive && entry.ReadSemanticTelemetry().CleanupComplete;
            if (item.Mode == 1)
            {
                var probes = entry.ReadMissingBindingProbeReport();
                Assert.That(probes.Passed, Is.True, "Each seed must retain the exact four fail-closed missing-binding probe outcomes.");
                measured.BindingProbesPassed = probes.Passed;
                measured.BindingProbeJson = probes.ToJson();
            }
            Assert.That(measured.LifecycleClean, Is.True, "S3 lifecycle must have reached its explicit bounded cleanup before the last frozen retained frame.");
            measurements.Add(measured);
        }

        private static void WriteTrailMaskDiagnostic(W24ContinuousCaptureRecorder recorder, W24ContinuousCaptureRecorder.CompletedPlayerLoopToken token, RawTokenProvenance rawToken, W24S3RuntimeEntry entry, RequiredEvidenceRow row, List<RawDiagnostic> raw)
        {
            var trail = entry.GetComponentInChildren<TrailRenderer>(true);
            Assert.NotNull(trail, "Projectile typed diagnostics require the authored TrailRenderer as their sole rendered subject.");
            var history = entry.ReadEmitterHistory();
            Assert.That(history.Samples, Is.Not.Null.And.Length.GreaterThanOrEqualTo(2), "Trail corridor authority requires accepted world emitter history, never TrailRenderer vertex readback.");
            var result = W24TrailMaskDiagnosticCapture.Capture(recorder.AuthorityCamera, trail, history.Samples.Select(value => value.Position), 960, 540);
            var path = "diagnostics/seed_" + row.Seed.ToString(CultureInfo.InvariantCulture) + "/frame_" + row.Frame.ToString("D5", CultureInfo.InvariantCulture) + "_trail-mask.npy";
            AddRaw(recorder, token, rawToken, row, raw, path, result.BinaryMaskNpy, "R8 |u1 trail-only mask rendered from the authored TrailRenderer; projected corridor is supplied only from ReadEmitterHistory world samples.", "w24s3runtimeentry.reademitterhistory", new JArray(result.ProjectedEmitterHistoryPixels.Select(value => new JArray(value.x, value.y)).ToArray()));
        }

        private static void WriteBindingObjectIdDepthDiagnostics(W24ContinuousCaptureRecorder recorder, W24ContinuousCaptureRecorder.CompletedPlayerLoopToken token, RawTokenProvenance rawToken, Scene scene, W24S3RuntimeEntry entry, RequiredEvidencePlan requiredEvidence, uint seed, int frame, List<RawDiagnostic> raw)
        {
            var registrations = Find<W24DiagnosticObjectRegistration>(scene).OrderBy(value => value.ObjectId).ToArray();
            Assert.That(registrations.Select(value => value.ObjectId), Is.EqualTo(new uint[] { 10u, 101u, 201u, 202u, 203u }), "Binding typed diagnostics require exactly the frozen stable Object-ID registrations.");
            var objectPlan = requiredEvidence.Contract.SelectToken("extensions.typedDiagnostics.objectIdDepth") as JObject;
            var fragmentPlan = requiredEvidence.Contract.SelectToken("extensions.typedDiagnostics.fragmentTracks") as JObject;
            Assert.NotNull(objectPlan, "Binding capture requires the frozen Object-ID/depth plan.");
            Assert.NotNull(fragmentPlan, "Binding capture requires the frozen fragment-ID cross-evidence plan.");
            var requiredIds = ((JArray)objectPlan["requiredObjectIds"]).OfType<JObject>().Select(value => unchecked((uint)(long)value["id"])).ToArray();
            var fragmentIds = ((JArray)fragmentPlan["fragmentIds"]).Values<uint>().ToArray();
            var frontViewId = (string)fragmentPlan["frontViewId"];
            var camera = recorder.AuthorityCamera;
            var views = new[]
            {
                W24ObjectIdDepthDiagnosticCapture.W24DiagnosticView.FromPose(camera, "binding_front_main", camera.transform.position, camera.transform.rotation),
                W24ObjectIdDepthDiagnosticCapture.W24DiagnosticView.FromPose(camera, "binding_oblique", new Vector3(3.2f, 1.8f, -4.2f), Quaternion.LookRotation(new Vector3(0f, 1f, 0f) - new Vector3(3.2f, 1.8f, -4.2f)))
            };
            foreach (var view in views)
            {
                var needsObject = requiredEvidence.RowsFor(seed, frame, "object-id").Any(row => string.Equals(row.ViewId, view.ViewId, StringComparison.Ordinal));
                var needsDepth = requiredEvidence.RowsFor(seed, frame, "depth-linear").Any(row => string.Equals(row.ViewId, view.ViewId, StringComparison.Ordinal));
                var needsFragment = string.Equals(view.ViewId, frontViewId, StringComparison.Ordinal) && requiredEvidence.RowsFor(seed, frame, "fragment-id").Any();
                if (!needsObject && !needsDepth && !needsFragment) continue;
                var result = W24ObjectIdDepthDiagnosticCapture.Capture(camera, registrations, view, 960, 540);
                var idsToValidate = needsObject || needsDepth ? requiredIds : fragmentIds;
                foreach (var id in idsToValidate)
                    Assert.That(result.ObjectIds.Select((value, index) => new { value, index }).Any(value => value.value == id && result.LinearDepth[value.index] >= .0001f), Is.True, "Every required Object-ID needs a finite >= 0.0001 linear-depth pixel in its frozen diagnostic view.");
                var prefix = "diagnostics/seed_" + seed.ToString(CultureInfo.InvariantCulture) + "/frame_" + frame.ToString("D5", CultureInfo.InvariantCulture) + "_" + view.ViewId;
                RawDiagnostic objectRaw = null;
                if (needsObject)
                {
                    var row = requiredEvidence.Require(seed, frame, "object-id", view.ViewId);
                    objectRaw = AddRaw(recorder, token, rawToken, row, raw, prefix + "_object-id.npy", result.ObjectIdNpy, "R32_UInt Object-ID raw diagnostic for the frozen registered binding entities.", "w24diagnosticobjectregistration");
                }
                if (needsFragment)
                {
                    var row = requiredEvidence.Require(seed, frame, "fragment-id", view.ViewId);
                    var derivedFrom = objectRaw == null ? "w24diagnosticobjectregistration" : objectRaw.Path;
                    AddRaw(recorder, token, rawToken, row, raw, prefix + "_fragment-id.npy", result.ObjectIdNpy, "R32_UInt fragment-ID raw diagnostic for independent fragment trajectory cross-evidence.", derivedFrom);
                }
                if (needsDepth)
                {
                    Assert.NotNull(objectRaw, "Every depth-linear matrix row must share its view/frame with a frozen object-id raw diagnostic.");
                    var row = requiredEvidence.Require(seed, frame, "depth-linear", view.ViewId);
                    AddRaw(recorder, token, rawToken, row, raw, prefix + "_linear-depth.npy", result.LinearDepthNpy, "R32_SFloat finite linear-depth raw diagnostic for the same frozen Object-ID view.", objectRaw.Path);
                }
            }
            var probes = entry.ReadMissingBindingProbeReport();
            Assert.That(probes.Passed, Is.True, "All missing socket/renderer/mesh/bone probes must report their exact fault without an anchor or renderer fallback.");
        }

        private static IEnumerator WriteReceiverDiagnostics(W24ContinuousCaptureRecorder recorder, Camera camera, W24S3RuntimeEntry entry, W24S3PreviewDriver driver, Scene scene, W24CaptureProfile profile, RequiredEvidencePlan requiredEvidence, List<ReceiverMeasurement> measurements, List<RawDiagnostic> raw, RawTokenSequence rawTokenSequence)
        {
            var receivers = Find<Renderer>(scene).Where(value => value.name == "Receiver_A_LinearProbe" || value.name == "Receiver_B_LinearProbe").OrderBy(value => value.name, StringComparer.Ordinal).ToArray();
            var lights = entry.GetComponentsInChildren<Light>(true);
            Assert.That(receivers, Has.Length.EqualTo(2), "Real-light baseline requires two separate serialized receiver probes.");
            Assert.That(lights, Has.Length.EqualTo(2), "Real-light baseline requires two actual UnityEngine.Light components.");
            // Receiver diagnostics must not accidentally sample the effect body.  Disable every
            // Renderer owned by the Runtime Entry (particles, meshes and trails), not merely
            // ParticleSystemRenderer, while leaving the two scene-only receiver probes intact.
            var effectRenderers = entry.GetComponentsInChildren<Renderer>(true);
            Assert.That(effectRenderers.All(value => !receivers.Contains(value)), Is.True, "Receiver probes must remain scene-only and outside the Runtime Entry hierarchy.");
            var priorEffectRenderer = effectRenderers.Select(value => value.enabled).ToArray();
            var priorLight = lights.Select(value => value.enabled).ToArray();
            try
            {
                foreach (var profileSeed in profile.AllSeeds())
                {
                    var seed = unchecked((uint)profileSeed);
                    var logicalFrame = 0;
                    var startedOnObservedBoundary = false;
                    var completed = false;
                    Exception observationFailure = null;
                    Action<int, float> observer = null;
                    observer = (playerLoopFrame, playerLoopTime) =>
                    {
                        try
                        {
                            var token = recorder.ConsumeCompletedPlayerLoopToken();
                            var rawToken = rawTokenSequence.Observe();
                            if (!startedOnObservedBoundary)
                            {
                                recorder.AcknowledgeObservedPlayerLoopFrame(token);
                                driver.RestartForFormalCapture(seed);
                                Assert.That(entry.IsAlive, Is.True, "Receiver diagnostic seed must activate through the serialized driver on a natural observed frame boundary.");
                                startedOnObservedBoundary = true;
                                return;
                            }

                            logicalFrame++;
                            if (requiredEvidence.RowsFor(seed, logicalFrame, "effect-mask").Any())
                            {
                                var maskSubjects = effectRenderers.Where(value => value.enabled && value.gameObject.activeInHierarchy && value.sharedMaterials != null && value.sharedMaterials.Length > 0).ToArray();
                                Assert.That(maskSubjects, Is.Not.Empty, "Receiver authority needs an explicit active Runtime Entry renderer set for its effect-mask cross-input.");
                                var effectMask = W24RendererMaskDiagnosticCapture.Capture(camera, maskSubjects, 960, 540);
                                var effectRow = requiredEvidence.Require(seed, logicalFrame, "effect-mask", "light_main");
                                var pathPrefix = "diagnostics/seed_" + seed.ToString(CultureInfo.InvariantCulture) + "/frame_" + logicalFrame.ToString("D5", CultureInfo.InvariantCulture);
                                var effectRaw = AddRaw(recorder, token, rawToken, effectRow, raw, pathPrefix + "_effect-mask.npy", effectMask.BinaryMaskNpy, "Explicit Runtime Entry renderer effect-mask captured before receiver A/B hiding.", "runtime-entry-renderer-set");
                                for (var index = 0; index < effectRenderers.Length; index++) effectRenderers[index].enabled = false;
                                Assert.That(effectRenderers.All(value => !value.enabled), Is.True, "Receiver A/B may only measure scene receivers; all Runtime Entry renderers must be hidden.");
                                foreach (var light in lights) light.enabled = false;
                                var receiverRegistrations = new List<W24DiagnosticObjectRegistration>();
                                try
                                {
                                    receiverRegistrations.Add(receivers[0].gameObject.AddComponent<W24DiagnosticObjectRegistration>()); receiverRegistrations[0].Configure(receivers[0], 11u, "receiver_a", true);
                                    receiverRegistrations.Add(receivers[1].gameObject.AddComponent<W24DiagnosticObjectRegistration>()); receiverRegistrations[1].Configure(receivers[1], 12u, "receiver_b", true);
                                    var ids = W24ObjectIdDepthDiagnosticCapture.Capture(camera, receiverRegistrations, W24ObjectIdDepthDiagnosticCapture.W24DiagnosticView.FromPose(camera, "light_main", camera.transform.position, camera.transform.rotation), 960, 540);
                                    var idsRow = requiredEvidence.Require(seed, logicalFrame, "receiver-id", "light_main");
                                    var idsRaw = AddRaw(recorder, token, rawToken, idsRow, raw, pathPrefix + "_receiver-id.npy", ids.ObjectIdNpy, "R32_UInt receiver ID map for the two explicit receiver probes.", "receiver-probe-registration");
                                    var off = W24LinearLdrDiagnosticCapture.Capture(camera, 960, 540);
                                    var offRow = requiredEvidence.RequireMarked(seed, logicalFrame, "receiver-linear-ldr", "light_main", "-off-");
                                    var offRaw = AddRaw(recorder, token, rawToken, offRow, raw, pathPrefix + "_receiver-off-linear-ldr.npy", off.LinearRgbNpy, "Fixed-exposure linear LDR float32 Receiver A/B off raw capture with Runtime Entry renderers hidden.", idsRaw.Path);
                                    foreach (var light in lights) light.enabled = true;
                                    var on = W24LinearLdrDiagnosticCapture.Capture(camera, 960, 540);
                                    var onRow = requiredEvidence.RequireMarked(seed, logicalFrame, "receiver-linear-ldr", "light_main", "-on-");
                                    AddRaw(recorder, token, rawToken, onRow, raw, pathPrefix + "_receiver-on-linear-ldr.npy", on.LinearRgbNpy, "Fixed-exposure linear LDR float32 Receiver A/B on raw capture; only actual UnityEngine.Light.enabled differs from off.", offRaw.Path);
                                    Assert.NotNull(effectRaw, "Receiver luminance metrics must bind the independent pre-hide effect mask.");
                                    var result = new ReceiverMeasurement { Seed = seed, OffA = SampleReceiverLuminance(camera, receivers[0], off), OnA = SampleReceiverLuminance(camera, receivers[0], on), OffB = SampleReceiverLuminance(camera, receivers[1], off), OnB = SampleReceiverLuminance(camera, receivers[1], on) };
                                    Assert.That(result.Passed, Is.True, "Both receiver probes must respond to actual Light only for every frozen seed.");
                                    measurements.Add(result);
                                }
                                finally { foreach (var registration in receiverRegistrations) if (registration != null) UnityEngine.Object.Destroy(registration); }
                                for (var index = 0; index < effectRenderers.Length; index++) effectRenderers[index].enabled = priorEffectRenderer[index];
                            }
                            recorder.AcknowledgeObservedPlayerLoopFrame(token);
                            if (logicalFrame == 30) completed = true;
                        }
                        catch (Exception exception)
                        {
                            observationFailure = exception;
                        }
                    };
                    recorder.AfterPlayerLoopFrame += observer;
                    try
                    {
                        while (!completed && observationFailure == null) yield return null;
                    }
                    finally
                    {
                        recorder.AfterPlayerLoopFrame -= observer;
                    }
                    if (observationFailure != null) throw observationFailure;
                    Assert.That(startedOnObservedBoundary && logicalFrame == 30, Is.True, "Every receiver seed must consume one boundary token and its complete 30-frame natural PlayerLoop span.");
                }
                Assert.That(measurements, Has.Count.EqualTo(profile.AllSeeds().Count()), "Receiver typed raw A/B diagnostics must exist for every frozen seed.");
            }
            finally
            {
                for (var index = 0; index < effectRenderers.Length; index++) effectRenderers[index].enabled = priorEffectRenderer[index];
                for (var index = 0; index < lights.Length; index++) lights[index].enabled = priorLight[index];
                entry.ResetForPool();
            }
        }

        private static RawDiagnostic AddRaw(W24ContinuousCaptureRecorder recorder, W24ContinuousCaptureRecorder.CompletedPlayerLoopToken token, RawTokenProvenance rawToken, RequiredEvidenceRow row, List<RawDiagnostic> raw, string path, byte[] bytes, string description, string derivedFrom, JArray projectedHistoryPixels = null)
        {
            Assert.NotNull(row, "Every typed raw diagnostic must originate from a frozen required-evidence matrix row.");
            Assert.That(ProtocolToken(row.EvidenceId) && ProtocolToken(row.PassId) && ProtocolToken(row.ViewId) && !string.IsNullOrWhiteSpace(derivedFrom), Is.True, "Typed raw diagnostics require contract-bound ID/pass/view and non-empty derivedFrom provenance.");
            var frame = row.Frame; var seed = row.Seed;
            var encoding = EncodingForPass(row.PassId);
            var hash = recorder.WriteObservedTypedDiagnostic(token, frame, seed, path, bytes, row.PassId, encoding, description, row.ViewId, derivedFrom);
            var item = new RawDiagnostic { Id = row.EvidenceId, Path = path, Hash = hash, PassId = row.PassId, Encoding = encoding, Seed = seed, Frame = frame, ViewId = row.ViewId, DerivedFrom = derivedFrom, PlayerLoopSerial = rawToken.Serial, PlayerLoopFrame = rawToken.Frame, PlayerLoopTime = rawToken.Time, ProjectedHistoryPixels = projectedHistoryPixels };
            raw.Add(item);
            return item;
        }

        private static float SampleReceiverLuminance(Camera camera, Renderer receiver, W24LinearLdrDiagnosticCapture.Result image)
        {
            // WorldToScreenPoint uses the current Game-view/backbuffer size after the
            // diagnostic capture restores camera.targetTexture.  Batch Game views are not
            // guaranteed to be 960x540, so those coordinates can sample unrelated pixels.
            // Viewport coordinates are resolution-independent and are projected into the
            // frozen diagnostic surface explicitly.
            var viewport = camera.WorldToViewportPoint(receiver.bounds.center); Assert.That(viewport.z, Is.GreaterThan(0f));
            Assert.That(viewport.x, Is.InRange(0f, 1f)); Assert.That(viewport.y, Is.InRange(0f, 1f));
            var centerX = Mathf.Clamp(Mathf.RoundToInt(viewport.x * image.Width), 8, image.Width - 9); var centerY = Mathf.Clamp(Mathf.RoundToInt(viewport.y * image.Height), 8, image.Height - 9);
            var total = 0f; var samples = 0; var rgb = image.LinearRgb;
            for (var y = centerY - 7; y <= centerY + 7; y++) for (var x = centerX - 7; x <= centerX + 7; x++) { var index = (y * image.Width + x) * 3; total += .2126f * rgb[index] + .7152f * rgb[index + 1] + .0722f * rgb[index + 2]; samples++; }
            return total / Mathf.Max(1, samples);
        }

        private static MetricsEvidence WriteMetricsEvidence(string root, CaptureCase item, W24ContinuousCaptureRecorder recorder, W24CaptureProfile profile, RequiredEvidencePlan requiredEvidence, List<RawDiagnostic> raw)
        {
            Assert.That(raw, Is.Not.Empty, "Every S3 diagnostic authority must have typed raw NPY inputs before metrics can run.");
            requiredEvidence.AssertExactlyMatches(raw);
            var contract = requiredEvidence.Contract;
            var typed = contract.SelectToken("extensions.typedDiagnostics") as JObject;
            var toolPlan = typed == null ? null : typed["metricsTool"] as JObject;
            Assert.NotNull(toolPlan, "Every S3 typed diagnostics Contract must freeze the exact metrics-tool bytes.");
            var toolPath = Path.Combine(root, "tools", "vfx", "metrics", "render_metrics.py");
            var expectedToolHash = (string)toolPlan["sha256"];
            Assert.That((string)toolPlan["path"], Is.EqualTo("tools/vfx/metrics/render_metrics.py"));
            Assert.That(expectedToolHash, Is.EqualTo(HashFile(toolPath)), "Metrics tool bytes differ from the Contract-frozen expected tool hash.");
            var contractRevision = (int?)contract["contractRevision"];
            var contractHash = (string)contract["contractHash"];
            var contractCaptureProfile = contract["captureProfile"] as JObject;
            Assert.NotNull(contractCaptureProfile, "Metrics input requires the complete frozen Contract capture profile.");
            var contractCaptureProfileHash = HashText(CanonicalJson(contractCaptureProfile));
            var captureToolHash = (string)contract.SelectToken("captureProfile.captureToolHash");
            var captureToolBundlePath = (string)contract.SelectToken("extensions.captureToolBundle");
            Assert.That(contractRevision.HasValue && contractRevision.Value > 0 && CanonicalHash(contractHash) && CanonicalHash(captureToolHash) && CanonicalHash(expectedToolHash) && string.Equals(captureToolBundlePath, BundleRelativePath, StringComparison.Ordinal), Is.True, "Metrics input requires a frozen Contract revision/hash, canonical capture bundle path/hash, and metrics-tool hash.");
            var frozenEnvironment = typed["metricsEnvironment"] as JObject;
            Assert.NotNull(frozenEnvironment, "Every S3 typed diagnostics Contract must freeze the Python/NumPy/Pillow environment identity.");
            var pythonExecutable = RequiredMetricsPythonExecutable();
            var observedEnvironment = InvokeMetricsBridge("ProbeMetricsEnvironmentForInput", pythonExecutable) as JObject;
            Assert.NotNull(observedEnvironment, "The controlled metrics bridge must return a detached frozen environment identity.");
            Assert.That(CanonicalJson(observedEnvironment), Is.EqualTo(CanonicalJson(frozenEnvironment)), "The current metrics Python executable or dependency bytes differ from the Contract-frozen environment.");
            var copiedMatrix = (JArray)requiredEvidence.Matrix.DeepClone();
            var input = new JObject
            {
                ["schema"] = "w24-render-metrics-input/v1", ["effectId"] = item.Id, ["candidateId"] = CandidateId,
                ["contractRevision"] = contractRevision.Value, ["contractSha256"] = contractHash, ["captureProfileSha256"] = contractCaptureProfileHash, ["recorderCaptureProfileSha256"] = profile.Sha256,
                ["captureToolBundlePath"] = captureToolBundlePath, ["captureToolBundleSha256"] = captureToolHash, ["expectedToolSha256"] = expectedToolHash,
                ["metricsEnvironment"] = (JObject)frozenEnvironment.DeepClone(),
                ["requiredEvidenceMatrix"] = copiedMatrix, ["requiredEvidenceMatrixSha256"] = HashText(CanonicalJson(copiedMatrix)),
                ["evidence"] = new JArray(raw.OrderBy(value => value.Id, StringComparer.Ordinal).Select(MetricRegistryEntry)),
                ["checks"] = new JArray()
            };
            var output = new MetricsEvidence();
            var checks = (JArray)input["checks"];
            if (item.Mode == 0)
            {
                var corridor = typed["trailCorridor"] as JObject;
                var thresholds = corridor == null ? null : corridor["thresholds"] as JObject;
                var metricPlan = corridor == null ? null : corridor["metricPlan"] as JObject;
                Assert.NotNull(thresholds); Assert.NotNull(metricPlan);
                foreach (var itemRaw in raw.Where(value => value.PassId == "trail-only-mask").OrderBy(value => value.Seed).ThenBy(value => value.Frame))
                {
                    Assert.NotNull(itemRaw.ProjectedHistoryPixels, "Trail metrics require the world-history projection captured with the raw mask.");
                    var checkId = MetricCheckId(metricPlan, itemRaw.Seed, itemRaw.Frame, null, null);
                    checks.Add(new JObject { ["id"] = checkId, ["kind"] = "trail", ["trail"] = itemRaw.Id, ["historyProjectedPx"] = itemRaw.ProjectedHistoryPixels, ["radiusPx"] = (double)thresholds["corridorRadiusPixels"], ["maxMeanNearestDistancePx"] = (double)thresholds["maximumMeanNearestHistoryDistancePixels"], ["minCorridorCoverage"] = (double)thresholds["corridorCoverageMinimum"] });
                    AddMetricCheck(output, (string)corridor["requirementId"], checkId);
                }
            }
            else if (item.Mode == 1)
            {
                var objectPlan = typed["objectIdDepth"] as JObject;
                var multiviewPlan = objectPlan == null ? null : objectPlan["metricPlan"] as JObject;
                var thresholds = objectPlan == null ? null : objectPlan["thresholds"] as JObject;
                var fragmentPlan = typed["fragmentTracks"] as JObject;
                var fragmentMetricPlan = fragmentPlan == null ? null : fragmentPlan["metricPlan"] as JObject;
                Assert.NotNull(objectPlan); Assert.NotNull(multiviewPlan); Assert.NotNull(thresholds); Assert.NotNull(fragmentPlan); Assert.NotNull(fragmentMetricPlan);
                var frame = (int)objectPlan["metricPlan"]["logicalFrame"];
                var requiredIds = ((JArray)objectPlan["requiredObjectIds"]).OfType<JObject>().Select(value => unchecked((uint)(long)value["id"])).ToArray();
                var parallaxRequiredIds = new HashSet<uint>(((JArray)objectPlan["parallaxRequiredObjectIds"]).Values<uint>());
                Assert.That(parallaxRequiredIds, Is.Not.Empty.And.SubsetOf(requiredIds), "The frozen Contract must explicitly identify which off-centre attachment/fragment IDs require centroid parallax.");
                var views = ((JArray)objectPlan["frozenViews"]).OfType<JObject>().Select(value => (string)value["viewId"]).ToArray();
                Assert.That(views, Has.Length.EqualTo(2), "Frozen multiview metrics require front and oblique views.");
                foreach (var seed in profile.AllSeeds().Select(value => unchecked((uint)value)))
                foreach (var id in requiredIds)
                {
                    var checkId = MetricCheckId(multiviewPlan, seed, frame, id, null);
                    checks.Add(new JObject
                    {
                        ["id"] = checkId, ["kind"] = "multiview_3d", ["objectId"] = id, ["carrier"] = "mesh",
                        ["minDepthSpan"] = 0.0, ["minParallaxPx"] = (double)thresholds["minimumCentroidParallaxPixelsAcrossViews"], ["requireParallax"] = parallaxRequiredIds.Contains(id),
                        ["views"] = new JArray(BindingMetricView(raw, requiredEvidence, seed, frame, views[0]), BindingMetricView(raw, requiredEvidence, seed, frame, views[1]))
                    });
                    AddMetricCheck(output, (string)objectPlan["requirementId"], checkId);
                }
                var fragmentFrames = ((JArray)fragmentPlan["frames"]).Values<int>().ToArray();
                var fragmentIds = ((JArray)fragmentPlan["fragmentIds"]).Values<uint>().ToArray();
                var fragmentThresholds = (JObject)fragmentPlan["thresholds"];
                var fragmentRequirement = (string)fragmentPlan["requirementId"];
                var frontViewId = (string)fragmentPlan["frontViewId"];
                foreach (var seed in profile.AllSeeds().Select(value => unchecked((uint)value)))
                {
                    var checkId = MetricCheckId(fragmentMetricPlan, seed, null, null, null);
                    var frames = new JArray(fragmentFrames.Select(value => requiredEvidence.Require(seed, value, "fragment-id", frontViewId).EvidenceId));
                    checks.Add(new JObject { ["id"] = checkId, ["kind"] = "fragment_tracks", ["frames"] = frames, ["fragmentIds"] = new JArray(fragmentIds), ["maxTrajectoryCorrelation"] = (double)fragmentThresholds["maxTrajectoryCorrelation"], ["minPairwiseDistanceVariationRatio"] = (double)fragmentThresholds["minPairwiseDistanceVariationRatio"], ["rejectSingleRigidBody"] = (bool)fragmentThresholds["rejectSingleRigidBody"] });
                    AddMetricCheck(output, fragmentRequirement, checkId);
                }
            }
            else
            {
                var receiverPlan = typed["receiverLuminanceLdr"] as JObject;
                var metricPlan = receiverPlan == null ? null : receiverPlan["metricPlan"] as JObject;
                var thresholds = receiverPlan == null ? null : receiverPlan["thresholds"] as JObject;
                var frame = receiverPlan == null ? -1 : (int)receiverPlan["seedConsumptionPlan"]["logicalFrame"];
                Assert.NotNull(receiverPlan); Assert.NotNull(metricPlan); Assert.NotNull(thresholds); Assert.That(frame, Is.GreaterThanOrEqualTo(0));
                var requirementIds = ((JArray)receiverPlan["requirementIds"]).Values<string>().ToArray();
                Assert.That(requirementIds, Is.Not.Empty, "The frozen receiver metric plan must name every Contract requirement that consumes its checks.");
                var requirementMappings = ((JArray)receiverPlan["perRequirementCheckMapping"]).OfType<JObject>().ToArray();
                Assert.That(requirementMappings.Select(value => (string)value["requirementId"]), Is.EquivalentTo(requirementIds), "The frozen per-requirement check mapping must cover the exact declared requirement set.");
                foreach (var seed in profile.AllSeeds().Select(value => unchecked((uint)value)))
                foreach (var receiver in ((JArray)receiverPlan["receiverIds"]).OfType<JObject>())
                {
                    var receiverId = (int)receiver["id"]; var name = ((string)receiver["role"]).Replace("receiver_", string.Empty);
                    var checkId = MetricCheckId(metricPlan, seed, frame, null, name);
                    var effect = requiredEvidence.Require(seed, frame, "effect-mask", "light_main");
                    var ids = requiredEvidence.Require(seed, frame, "receiver-id", "light_main");
                    var off = requiredEvidence.RequireMarked(seed, frame, "receiver-linear-ldr", "light_main", "-off-");
                    var on = requiredEvidence.RequireMarked(seed, frame, "receiver-linear-ldr", "light_main", "-on-");
                    checks.Add(new JObject { ["id"] = checkId, ["kind"] = "receiver_luminance_ldr", ["on"] = on.EvidenceId, ["off"] = off.EvidenceId, ["receiverIds"] = ids.EvidenceId, ["effectMask"] = effect.EvidenceId, ["receiverId"] = receiverId, ["minLinearLuminanceDelta"] = (double)thresholds["minimumLinearLuminanceDelta"] });
                    foreach (var mapping in requirementMappings)
                    {
                        if (!((JArray)mapping["receiverIds"]).Values<int>().Contains(receiverId)) continue;
                        AddMetricCheck(output, (string)mapping["requirementId"], checkId);
                    }
                }
            }
            Assert.That(checks.Count, Is.GreaterThan(0), "No S3 Contract may write a metrics input without frozen metric checks.");
            var inputPath = "diagnostics/metrics-input.json";
            var inputHash = InvokeMetricsBridge("WriteInput", recorder, inputPath, input, contractRevision.Value, contractHash, contractCaptureProfileHash, expectedToolHash) as string;
            Assert.That(inputHash, Is.Not.Null.And.Not.Empty, "Metrics bridge must recorder-write the immutable input.");
            output.AnalysisInputHash = HashText(CanonicalJson(input));
            output.ReportHash = InvokeMetricsBridge("RunAndWriteReport", recorder, inputPath, inputHash, "diagnostics/metrics-report.json", pythonExecutable, toolPath, expectedToolHash) as string;
            Assert.That(output.ReportHash, Is.Not.Null.And.Not.Empty, "Metrics bridge must recorder-write a passing report or fail closed.");
            VerifyMetricsReport(recorder, input, output);
            return output;
        }

        private static string RequiredMetricsPythonExecutable()
        {
            var configured = Environment.GetEnvironmentVariable("W24_METRICS_PYTHON");
            Assert.That(string.IsNullOrWhiteSpace(configured), Is.False, "Formal S3 metrics require W24_METRICS_PYTHON to name the Contract-frozen absolute Python executable.");
            Assert.That(Path.IsPathRooted(configured), Is.True, "W24_METRICS_PYTHON must be an absolute executable path; PATH resolution is forbidden for formal evidence.");
            var absolute = Path.GetFullPath(configured);
            Assert.That(File.Exists(absolute), Is.True, "W24_METRICS_PYTHON must resolve to an existing executable file.");
            return absolute;
        }

        private static object InvokeMetricsBridge(string methodName, params object[] arguments)
        {
            var type = Type.GetType("VFXComposer.Editor.W24.S5.W24MetricsEvidenceDag, VFXComposer.Editor", true);
            var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
            Assert.NotNull(method, "Formal S3 producer requires the controlled W24 metrics bridge.");
            try { return method.Invoke(null, arguments); }
            catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
        }

        private static JObject MetricRegistryEntry(RawDiagnostic value)
        {
            return new JObject
            {
                ["id"] = value.Id, ["path"] = value.Path, ["sha256"] = value.Hash, ["kind"] = "diagnostic", ["passId"] = value.PassId, ["encoding"] = value.Encoding,
                ["seed"] = value.Seed, ["logicalFrameIndex"] = value.Frame, ["playerLoopSerial"] = value.PlayerLoopSerial, ["playerLoopFrame"] = value.PlayerLoopFrame,
                ["playerLoopTime"] = JToken.Parse(Number(value.PlayerLoopTime)), ["viewId"] = value.ViewId, ["derivedFrom"] = value.DerivedFrom
            };
        }

        private static void VerifyMetricsReport(W24ContinuousCaptureRecorder recorder, JObject input, MetricsEvidence output)
        {
            var reportPath = Path.Combine(recorder.EvidenceRoot, "diagnostics", "metrics-report.json");
            Assert.That(File.Exists(reportPath), Is.True, "The controlled bridge must recorder-write a formal metrics report.");
            var report = JObject.Parse(File.ReadAllText(reportPath));
            var expected = ((JArray)input["checks"]).OfType<JObject>().ToDictionary(value => (string)value["id"], value => (string)value["kind"], StringComparer.Ordinal);
            var observed = (report["checks"] as JArray ?? new JArray()).OfType<JObject>().ToArray();
            Assert.That((string)report["route"], Is.EqualTo("MEASURED"));
            Assert.That((bool?)report["machineGatesPassed"], Is.True);
            Assert.That(observed.Length, Is.EqualTo(expected.Count), "Metrics report must contain every and only the frozen input checks.");
            foreach (var check in observed)
            {
                var id = (string)check["id"]; string kind;
                Assert.That(expected.TryGetValue(id ?? string.Empty, out kind), Is.True, "Metrics report emitted an unfrozen check ID.");
                Assert.That((string)check["kind"], Is.EqualTo(kind));
                Assert.That((bool?)check["pass"], Is.True, "Any failed metrics check must fail formal capture before trace emission.");
                Assert.That(output.VerifiedCheckIds.Add(id), Is.True, "Metrics report may not duplicate a check ID.");
            }
            Assert.That(output.VerifiedCheckIds.SetEquals(expected.Keys), Is.True, "Metrics report must verify the exact frozen input check set.");
        }

        private static JObject BindingMetricView(List<RawDiagnostic> raw, RequiredEvidencePlan requiredEvidence, uint seed, int frame, string viewId)
        {
            var ids = requiredEvidence.Require(seed, frame, "object-id", viewId);
            var depth = requiredEvidence.Require(seed, frame, "depth-linear", viewId);
            return new JObject { ["objectIds"] = FindRaw(raw, ids.EvidenceId).Id, ["depth"] = FindRaw(raw, depth.EvidenceId).Id };
        }

        private static RawDiagnostic FindRaw(List<RawDiagnostic> raw, string id)
        {
            var value = raw.SingleOrDefault(item => item.Id == id);
            Assert.NotNull(value, "Frozen metrics input is missing raw typed diagnostic " + id + ".");
            return value;
        }

        private static void AddMetricCheck(MetricsEvidence metrics, string requirementId, string checkId)
        {
            Assert.That(string.IsNullOrEmpty(requirementId), Is.False, "Every frozen metric check must bind a Contract requirement.");
            List<string> values;
            if (!metrics.ChecksByRequirement.TryGetValue(requirementId, out values)) { values = new List<string>(); metrics.ChecksByRequirement.Add(requirementId, values); }
            Assert.That(values.Contains(checkId), Is.False, "A requirement cannot claim the same frozen metrics check twice.");
            values.Add(checkId);
        }

        private static string MetricCheckId(JObject metricPlan, uint seed, int? frame, uint? objectId, string receiver)
        {
            Assert.NotNull(metricPlan, "Frozen metric plans are required for every typed diagnostic check.");
            var value = (string)metricPlan["checkIdPattern"];
            Assert.That(string.IsNullOrEmpty(value), Is.False, "Metric check ID pattern is required.");
            value = value.Replace("{seed}", seed.ToString(CultureInfo.InvariantCulture));
            if (frame.HasValue) value = value.Replace("{logicalFrame}", frame.Value.ToString(CultureInfo.InvariantCulture));
            if (objectId.HasValue) value = value.Replace("{objectId}", objectId.Value.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(receiver)) value = value.Replace("{receiver}", receiver);
            Assert.That(ProtocolToken(value) && value.IndexOf('{') < 0 && value.IndexOf('}') < 0, Is.True, "Metric check ID pattern has an unresolved or invalid placeholder.");
            return value;
        }

        private static string EncodingForPass(string passId)
        {
            switch (passId)
            {
                case "trail-only-mask":
                case "effect-mask": return "mask_binary";
                case "object-id":
                case "fragment-id":
                case "receiver-id": return "id_uint";
                case "depth-linear": return "linear_float";
                case "receiver-linear-ldr": return "linear_ldr";
                default: Assert.Fail("Required evidence matrix names an unsupported typed diagnostic pass: " + passId); return string.Empty;
            }
        }

        private static bool ProtocolToken(string value)
        {
            return !string.IsNullOrEmpty(value) && value.All(character => char.IsLower(character) || char.IsDigit(character) || character == '-' || character == '_' || character == '.');
        }

        private static bool CanonicalHash(string value)
        {
            return value != null && value.Length == 71 && value.StartsWith("sha256:", StringComparison.Ordinal) && value.Skip(7).All(character => (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'));
        }

        private static string SemanticTelemetry(CaptureCase item, List<RunMeasurement> measurements, List<ReceiverMeasurement> receiverMeasurements)
        {
            var receivers = receiverMeasurements == null || receiverMeasurements.Count == 0 ? string.Empty : ",\"receiverLinearLuminance\":[" + string.Join(",", receiverMeasurements.Select(value => value.ToJson()).ToArray()) + "]";
            return "{\"schema\":\"w24-s3-semantic-telemetry/v4\",\"effectId\":\"" + item.Id + "\",\"runs\":[" + string.Join(",", measurements.Select(value => value.ToJson()).ToArray()) + "]" + receivers + "}";
        }

        private static W24CaptureProfile Profile(CaptureCase item, Camera camera, string root)
        {
            return new W24CaptureProfile
            {
                ProfileVersion = "w24-s3-formal-capture-profile/v1", UnityVersion = Application.unityVersion, UrpVersion = InstalledUrpVersion(root),
                GraphicsApi = SystemInfo.graphicsDeviceType.ToString(), GraphicsDevice = SystemInfo.graphicsDeviceName, GraphicsDriverVersion = SystemInfo.graphicsDeviceVersion,
                RenderTextureFormat = RenderTextureFormat.ARGB32.ToString(), RendererAssetReference = RendererRelativePath, RendererAssetSha256 = HashFile(Path.Combine(root, "project", RendererRelativePath)),
                VolumeReference = GraphicsRelativePath + " (no per-scene Volume; bloom/tone mapping disabled)", VolumeSha256 = HashFile(Path.Combine(root, "project", GraphicsRelativePath)),
                ScenePath = item.Scene, SerializedCameraReference = item.Scene + "#MainCamera", Width = 960, Height = 540, FramesPerSecond = 60,
                Background = camera.backgroundColor, ColorSpace = QualitySettings.activeColorSpace.ToString(), Hdr = camera.allowHDR, Msaa = camera.allowMSAA, Bloom = false, ToneMapping = "None",
                CanonicalSeed = item.CanonicalSeed, RobustnessSeeds = new[] { item.RobustnessOne, item.RobustnessTwo }, RetainedFrameIndices = RetainedFrames
            };
        }

        private static W24CaptureSourceHashes Sources(CaptureCase item, string root, W24CaptureProfile profile, Camera camera)
        {
            var project = Path.Combine(root, "project");
            var bundlePath = Path.Combine(root, BundleRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var bundle = JObject.Parse(File.ReadAllText(bundlePath));
            foreach (var source in (JArray)bundle["sources"])
            {
                var sourcePath = Path.Combine(root, ((string)source["path"]).Replace('/', Path.DirectorySeparatorChar));
                Assert.That((string)source["sha256"], Is.EqualTo(HashFile(sourcePath)), "Capture tool source drifted after C0 registration.");
            }
            var toolHash = HashText(CanonicalJson(bundle));
            var contract = JObject.Parse(File.ReadAllText(Path.Combine(root, "docs", "vfx-candidates", item.Id, CandidateId, "design-contract.json")));
            var frozenCapture = (JObject)contract["captureProfile"];
            Assert.That((string)frozenCapture["captureToolHash"], Is.EqualTo(toolHash));
            Assert.That((string)frozenCapture["captureToolVersion"], Is.EqualTo("w24-s3-capture/3.5"));
            var manifestPath = Path.Combine(project, "ProjectSettings", "VFXComposer", "BuildManifests", item.Id + ".manifest.json");
            var manifest = JObject.Parse(File.ReadAllText(manifestPath));
            var sources = new W24CaptureSourceHashes
            {
                SceneSourcePath = Path.Combine(project, item.Scene.Replace('/', Path.DirectorySeparatorChar)), SceneSha256 = HashFile(Path.Combine(project, item.Scene.Replace('/', Path.DirectorySeparatorChar))),
                PrefabSourcePath = Path.Combine(project, item.Prefab.Replace('/', Path.DirectorySeparatorChar)), PrefabGuid = ReadGuid(Path.Combine(project, item.Prefab.Replace('/', Path.DirectorySeparatorChar)) + ".meta"), PrefabSha256 = HashFile(Path.Combine(project, item.Prefab.Replace('/', Path.DirectorySeparatorChar))),
                ManifestSourcePath = manifestPath, ManifestSha256 = HashFile(manifestPath), BuildHash = "sha256:" + (string)manifest["buildHash"],
                CaptureToolSourcePath = bundlePath, CaptureToolVersion = "w24-s3-capture/3.5", CaptureToolSha256 = toolHash
            };
            VerifyFrozenCaptureProfile(item, profile, camera, frozenCapture, sources);
            return sources;
        }

        private static string InstalledUrpVersion(string root)
        {
            var lockFile = JObject.Parse(File.ReadAllText(Path.Combine(root, "project", "Packages", "packages-lock.json")));
            var version = (string)lockFile["dependencies"]?["com.unity.render-pipelines.universal"]?["version"];
            Assert.That(version, Is.Not.Null.And.Not.Empty, "Formal capture must read its URP version from the project's frozen package lock, not a source literal.");
            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(root, "project", "Packages", "manifest.json")));
            Assert.That((string)manifest["dependencies"]?["com.unity.render-pipelines.universal"], Is.EqualTo(version), "URP manifest/package-lock drift invalidates the formal capture profile.");
            return version;
        }

        private static void VerifyFrozenCaptureProfile(CaptureCase item, W24CaptureProfile actual, Camera camera, JObject frozen, W24CaptureSourceHashes sources)
        {
            var resolution = frozen["resolution"] as JObject;
            Assert.NotNull(resolution, "Frozen C0 contract needs an explicit capture resolution.");
            Assert.That((string)frozen["unityVersion"], Is.EqualTo(actual.UnityVersion));
            Assert.That((string)frozen["urpVersion"], Is.EqualTo(actual.UrpVersion));
            Assert.That((string)frozen["graphicsApi"], Is.EqualTo(actual.GraphicsApi));
            Assert.That((string)frozen["graphicsDeviceDriver"], Is.EqualTo(actual.GraphicsDevice + " / " + actual.GraphicsDriverVersion));
            Assert.That((string)frozen["sceneSerializedReference"], Is.EqualTo(item.Scene));
            Assert.That((string)frozen["cameraSerializedReference"], Is.EqualTo(item.Scene + "#MainCamera"));
            Assert.That((int?)resolution["width"], Is.EqualTo(actual.Width));
            Assert.That((int?)resolution["height"], Is.EqualTo(actual.Height));
            Assert.That((int?)frozen["fps"], Is.EqualTo(actual.FramesPerSecond));
            Assert.That((string)frozen["colorSpace"], Is.EqualTo(actual.ColorSpace));
            Assert.That((bool?)frozen["hdr"], Is.EqualTo(actual.Hdr));
            Assert.That((int?)frozen["msaaSamples"], Is.EqualTo(actual.Msaa ? 2 : 1), "Camera.allowMSAA=false is a single-sample capture, not zero samples.");
            Assert.That((string)frozen["renderTextureFormat"], Is.EqualTo(actual.RenderTextureFormat));
            Assert.That((string)frozen["background"], Is.EqualTo("dark neutral"));
            Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That((bool?)((JObject)frozen["bloom"])["enabled"], Is.EqualTo(actual.Bloom));
            Assert.That((string)frozen["toneMapping"], Is.EqualTo(actual.ToneMapping));
            Assert.That((int?)frozen["canonicalSeed"], Is.EqualTo(actual.CanonicalSeed));
            Assert.That(((JArray)frozen["robustnessSeeds"]).Values<int>().ToArray(), Is.EqualTo(actual.RobustnessSeeds));
            Assert.That(sources.SceneSha256, Is.EqualTo((string)frozen["sceneHash"]), "The actual serialized preview scene bytes must equal frozen C0 capture identity.");
            Assert.That(sources.BuildHash, Is.EqualTo((string)frozen["prefabManifestHash"]), "The actual manifest build hash must equal frozen C0 capture identity.");
            Assert.That(actual.RendererAssetReference, Is.EqualTo((string)frozen["rendererAssetSerializedReference"]));
            Assert.That(actual.VolumeReference, Is.EqualTo((string)frozen["volumeSerializedReference"]));
            Assert.That(camera.fieldOfView, Is.EqualTo((float)frozen["cameraFovDegrees"]).Within(.001f));
            var pose = (JObject)frozen["cameraPose"];
            Assert.That(camera.transform.position.x, Is.EqualTo((float)pose["position"][0]).Within(.001f));
            Assert.That(camera.transform.position.y, Is.EqualTo((float)pose["position"][1]).Within(.001f));
            Assert.That(camera.transform.position.z, Is.EqualTo((float)pose["position"][2]).Within(.001f));
            Assert.That(camera.transform.eulerAngles.x, Is.EqualTo((float)pose["orientationEulerDegrees"][0]).Within(.1f));
            Assert.That(camera.transform.eulerAngles.y, Is.EqualTo((float)pose["orientationEulerDegrees"][1]).Within(.1f));
            Assert.That(camera.transform.eulerAngles.z, Is.EqualTo((float)pose["orientationEulerDegrees"][2]).Within(.1f));
        }

        private static void VerifyMeasurements(CaptureCase item, List<RunMeasurement> values)
        {
            Assert.That(values, Has.Count.EqualTo(3));
            Assert.That(values.All(value => value.LifecycleClean), Is.True, "Every frozen seed must complete cleanup through the serialized Preview Driver.");
            if (item.Mode == 0)
            {
                Assert.That(values.All(value => value.MotionSamples >= 3 && value.PeakTrailVertices > 1 && value.TrailWorldSpan > .5f), Is.True, "Projectile telemetry/diagnostic measurements must prove real emitter history, trail vertices and a non-static world corridor.");
            }
            else if (item.Mode == 1)
            {
                Assert.That(values.All(value => value.SocketBound && value.SocketFollowsModel && value.IndependentFragmentCount >= 3 && value.DistinctFragmentVelocityCount >= 2), Is.True, "Binding measurements must prove socket attachment and independently integrated fragments.");
            }
            else
            {
                Assert.That(values.All(value => value.EnabledLightCount == 2), Is.True, "Real-light telemetry must observe the two enabled UnityEngine.Light components.");
            }
        }

        private static string CompletedMachineTrace(string root, CaptureCase item, string telemetryHash, List<RunMeasurement> measurements, List<ReceiverMeasurement> receiverMeasurements, MetricsEvidence metrics)
        {
            var tracePath = Path.Combine(root, "docs", "vfx-candidates", item.Id, CandidateId, "implementation-trace.json");
            var trace = JObject.Parse(File.ReadAllText(tracePath));
            var evidenceRoot = "artifacts/vfx-evidence/" + item.Id + "/C0/diagnostics/";
            foreach (var requirement in ((JArray)trace["requirementTraces"]).OfType<JObject>())
            {
                var authority = (string)requirement["evidenceAuthority"];
                var pending = authority == "visualQa" || authority == "user";
                var requirementId = (string)requirement["designRequirementId"];
                var telemetryPass = TelemetryRequirementPassed(item, requirementId, measurements, receiverMeasurements);
                var measuredPass = RequirementPassed(item, requirementId, measurements, receiverMeasurements, metrics);
                var authorityKind = pending ? authority : authority == "diagnostic" ? "diagnostic" : "telemetry";
                if (authority == "diagnostic")
                {
                    requirement["authorityEvidence"] = MetricTraceEvidence(evidenceRoot, requirementId, metrics, "Sealed typed raw diagnostic metric check for every frozen seed/view; generic summaries are supplemental only.");
                    requirement["crossEvidence"] = new JArray(new JObject { ["kind"] = "telemetry", ["reference"] = evidenceRoot + "semantic-telemetry.json", ["sha256"] = telemetryHash, ["passed"] = telemetryPass, ["detail"] = item.Mode == 2 ? "Independent C# receiver-linear-luminance A/B and actual Light-component telemetry cross-observation; it does not replace Python typed diagnostic authority." : "Independent Runtime Entry/module readback cross-observation; it does not replace typed diagnostic authority." });
                }
                else
                {
                    // Telemetry and pending visual/user requirements cross-reference a sealed
                    // Beauty artifact from the same authority camera. A passed generic
                    // diagnostic image is deliberately forbidden here: typed-DAG contracts
                    // reserve diagnostic pass claims for exact frozen metrics-report checks.
                    requirement["authorityEvidence"] = new JArray(new JObject { ["kind"] = authorityKind, ["reference"] = pending ? "pending:" + authority : evidenceRoot + "semantic-telemetry.json", ["sha256"] = pending ? HashText("pending:" + authority) : telemetryHash, ["passed"] = pending ? false : measuredPass, ["detail"] = pending ? "Pending independent authority; graphics capture does not fabricate a Visual QA or user verdict." : "Passed only after the matching Runtime Entry semantic telemetry was asserted for all frozen seeds." });
                    if (item.Mode == 2 && (requirementId == "REQ-D-REAL-LIGHTS" || requirementId == "REQ-D-CLEANUP" || requirementId == "REQ-D-VISUAL"))
                        requirement["crossEvidence"] = MetricTraceEvidence(evidenceRoot, requirementId, metrics, "Independent typed receiver-linear-luminance A/B metric cross-check proves physical receiver response and rejects additive-only fake light.");
                    else if (requirementId == "REQ-C-FRAGMENT-INDEPENDENCE")
                        requirement["crossEvidence"] = MetricTraceEvidence(evidenceRoot, requirementId, metrics, "Fragment-ID trajectory metric is diagnostic cross-evidence only; telemetry remains the authority.");
                    else
                    {
                        var cross = RetainedBeautyArtifact(root, item, unchecked((uint)item.CanonicalSeed), 72);
                        requirement["crossEvidence"] = new JArray(new JObject { ["kind"] = "beauty", ["reference"] = cross.Reference, ["sha256"] = cross.Hash, ["passed"] = pending || measuredPass, ["detail"] = "Independent retained Beauty camera observation; it is a cross-check only and does not replace the requirement authority." });
                    }
                }
            }
            return trace.ToString(Formatting.None);
        }

        private static JArray MetricTraceEvidence(string evidenceRoot, string requirementId, MetricsEvidence metrics, string detail)
        {
            List<string> checkIds;
            Assert.That(metrics.ChecksByRequirement.TryGetValue(requirementId, out checkIds) && checkIds.Count > 0 && metrics.PassesRequirement(requirementId), Is.True, "Every typed diagnostic trace binding must derive from the exact verified metrics check set.");
            return new JArray(checkIds.Select(checkId => new JObject { ["kind"] = "diagnostic", ["reference"] = evidenceRoot + "metrics-report.json", ["sha256"] = metrics.ReportHash, ["passed"] = metrics.VerifiedCheckIds.Contains(checkId), ["detail"] = detail, ["passId"] = "metrics-report", ["encoding"] = "json", ["metricCheckId"] = checkId, ["analysisInputSha256"] = metrics.AnalysisInputHash }));
        }

        private static bool RequirementPassed(CaptureCase item, string requirementId, List<RunMeasurement> values, List<ReceiverMeasurement> receivers, MetricsEvidence metrics)
        {
            if (requirementId == null) return false;
            if (requirementId == "REQ-B-TRAIL-CORRIDOR" || requirementId == "REQ-C-MULTIVIEW") return metrics.PassesRequirement(requirementId);
            if (requirementId == "REQ-C-FRAGMENT-INDEPENDENCE") return TelemetryRequirementPassed(item, requirementId, values, receivers) && metrics.PassesRequirement(requirementId);
            if (requirementId == "REQ-D-RECEIVER-A" || requirementId == "REQ-D-RECEIVER-B") return TelemetryRequirementPassed(item, requirementId, values, receivers) && metrics.PassesRequirement(requirementId);
            return TelemetryRequirementPassed(item, requirementId, values, receivers);
        }

        private static bool TelemetryRequirementPassed(CaptureCase item, string requirementId, List<RunMeasurement> values, List<ReceiverMeasurement> receivers)
        {
            if (requirementId == null) return false;
            if (item.Mode == 0)
            {
                if (requirementId == "REQ-B-MOTION") return values.All(value => value.MotionSamples >= 3);
                if (requirementId == "REQ-B-WORLD-TRAIL") return values.All(value => value.PeakTrailVertices > 1);
                if (requirementId == "REQ-B-TRAIL-CORRIDOR") return values.All(value => value.MotionSamples >= 3 && value.PeakTrailVertices > 1 && value.TrailWorldSpan > .5f);
                if (requirementId == "REQ-B-POOL-CLEAR") return values.All(value => value.LifecycleClean);
            }
            if (item.Mode == 1)
            {
                if (requirementId == "REQ-C-SOCKET") return values.All(value => value.SocketBound);
                if (requirementId == "REQ-C-MODEL-MOTION") return values.All(value => value.SocketFollowsModel);
                if (requirementId == "REQ-C-FRAGMENT-INDEPENDENCE") return values.All(value => value.IndependentFragmentCount >= 3 && value.DistinctFragmentVelocityCount >= 2);
                if (requirementId == "REQ-C-MULTIVIEW") return values.All(value => value.SocketBound && value.SocketFollowsModel && value.BindingProbesPassed);
                if (requirementId == "REQ-C-MISSING") return values.All(value => value.BindingProbesPassed && !string.IsNullOrEmpty(value.BindingProbeJson));
            }
            if (item.Mode == 2)
            {
                if (requirementId == "REQ-D-REAL-LIGHTS") return values.All(value => value.EnabledLightCount == 2);
                if (requirementId == "REQ-D-RECEIVER-A" || requirementId == "REQ-D-RECEIVER-B") return receivers.Count == 3 && receivers.All(value => value.Passed);
                if (requirementId == "REQ-D-CLEANUP") return values.All(value => value.LifecycleClean);
            }
            return false;
        }

        private struct RetainedArtifact { public string Reference; public string Hash; }

        private static RetainedArtifact RetainedBeautyArtifact(string root, CaptureCase item, uint seed, int frameIndex)
        {
            Assert.That(RetainedFrames.Contains(frameIndex), Is.True, "Cross-observation must use a frozen retained frame.");
            var local = "frames/seed_" + seed.ToString(CultureInfo.InvariantCulture) + "/frame_" + frameIndex.ToString("D5", CultureInfo.InvariantCulture) + "_beauty.png";
            var absolute = Path.Combine(root, "artifacts", "vfx-evidence", item.Id, CandidateId, local.Replace('/', Path.DirectorySeparatorChar));
            Assert.That(File.Exists(absolute), Is.True, "Cross-observation must reference an actual recorder-produced Beauty frame.");
            return new RetainedArtifact { Reference = "artifacts/vfx-evidence/" + item.Id + "/C0/" + local, Hash = HashFile(absolute) };
        }

        private static void FinalizeC0EvidenceThroughEditorGate(CaptureCase item)
        {
            var type = Type.GetType("VFXComposer.Editor.W24.S5.W24S5RecorderCaptureCompletion, VFXComposer.Editor", true);
            var method = type.GetMethod(item.CompletionMethod, BindingFlags.Static | BindingFlags.Public);
            Assert.NotNull(method, "S5 must expose the canonical S3 post-capture command.");
            try { Assert.That(method.Invoke(null, null) as string, Is.EqualTo("docs/vfx-candidates/" + item.Id + "/C0/evidence/implementation-trace.json")); }
            catch (TargetInvocationException e) { Assert.Fail("Formal S3 C0 evidence binding failed: " + e.InnerException); }
        }

        private static void RequireFormalInputs(CaptureCase item)
        {
            var root = RepositoryRoot();
            var required = new[]
            {
                Path.Combine(root, "project", item.Scene.Replace('/', Path.DirectorySeparatorChar)), Path.Combine(root, "project", item.Prefab.Replace('/', Path.DirectorySeparatorChar)),
                Path.Combine(root, "project", "ProjectSettings", "VFXComposer", "BuildManifests", item.Id + ".manifest.json"), Path.Combine(root, "project", RendererRelativePath), Path.Combine(root, "project", GraphicsRelativePath),
                Path.Combine(root, "project", ToolRelativePath.Replace('/', Path.DirectorySeparatorChar)), Path.Combine(root, BundleRelativePath.Replace('/', Path.DirectorySeparatorChar)),
                Path.Combine(root, "tools", "vfx", "metrics", "render_metrics.py"),
                Path.Combine(root, "docs", "vfx-candidates", item.Id, CandidateId, "design-contract.json"), Path.Combine(root, "docs", "vfx-candidates", item.Id, CandidateId, "implementation-trace.json"), Path.Combine(root, "docs", "vfx-candidates", item.Id, CandidateId, "candidate-receipt.json")
            };
            var missing = required.Where(path => !File.Exists(path)).ToArray();
            if (missing.Length > 0) Assert.Ignore("S3 formal capture precondition is not built yet. Missing: " + string.Join("; ", missing));
        }

        private static T[] Find<T>(Scene scene) where T : Component { return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray(); }
        private static string RepositoryRoot() { return Directory.GetParent(Application.dataPath).Parent.FullName; }
        private static string ReadGuid(string path) { var line = File.ReadLines(path).First(value => value.StartsWith("guid: ", StringComparison.Ordinal)); return line.Substring(6).Trim(); }
        private static string HashFile(string path) { using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return "sha256:" + string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2", CultureInfo.InvariantCulture))); }
        private static string HashText(string text) { using (var sha = SHA256.Create()) return "sha256:" + string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(value => value.ToString("x2", CultureInfo.InvariantCulture))); }
        private static string CanonicalJson(JToken token) { if (token is JObject obj) { var sorted = new JObject(); foreach (var property in obj.Properties().OrderBy(value => value.Name, StringComparer.Ordinal)) sorted.Add(property.Name, JToken.Parse(CanonicalJson(property.Value))); return sorted.ToString(Formatting.None); } if (token is JArray array) return new JArray(array.Select(value => JToken.Parse(CanonicalJson(value)))).ToString(Formatting.None); return token.ToString(Formatting.None); }
        private static string Escape(string value) { return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\""); }
        private static string Number(float value) { return value.ToString("0.######", CultureInfo.InvariantCulture); }
        private static string Bool(bool value) { return value ? "true" : "false"; }
        private static string ModeName(int mode) { return mode == 0 ? "moving-projectile" : mode == 1 ? "socket-fragments" : "real-light"; }
        private static string CaptureCommandJson(string root, CaptureCase item, W24CaptureProfile profile)
        {
            var receiptPath = Path.Combine(root, "docs", "vfx-candidates", item.Id, CandidateId, "candidate-receipt.json");
            var command = new JObject
            {
                ["schema"] = "w24-s3-formal-capture-command/v1",
                ["effectId"] = item.Id,
                ["candidateId"] = CandidateId,
                ["frozenCandidateReceiptPath"] = "docs/vfx-candidates/" + item.Id + "/C0/candidate-receipt.json",
                ["frozenCandidateReceiptSha256"] = HashFile(receiptPath),
                ["serializedPreviewScene"] = item.Scene,
                ["serializedRuntimeEntry"] = item.Prefab,
                ["captureProfileSha256"] = profile.Sha256,
                ["seeds"] = new JArray(profile.AllSeeds().Select(value => new JValue(unchecked((uint)value)))),
                ["retainedFrameIndices"] = new JArray(RetainedFrames),
                ["producer"] = "W24S3GraphicsCaptureEvidenceTests"
            };
            return CanonicalJson(command);
        }

        private static string DiagnosticSummary(CaptureCase item, List<RunMeasurement> measurements)
        {
            return "{\"schema\":\"w24-s3-capture-diagnostic-summary/v2\",\"effectId\":\"" + item.Id + "\",\"captureOutput\":\"fixed-exposure linear LDR RGBA32\",\"runs\":[" + string.Join(",", measurements.Select(value => value.ToJson()).ToArray()) + "],\"retainedFrames\":[" + string.Join(",", RetainedFrames) + "],\"diagnosticPolicy\":\"same serialized MainCamera; effect-only TransparentFX mask\"}";
        }
    }
}
