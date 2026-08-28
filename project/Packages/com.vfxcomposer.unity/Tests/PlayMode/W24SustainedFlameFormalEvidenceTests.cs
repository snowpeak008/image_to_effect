using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VFXComposer.W24;

namespace VFXComposer.Tests.PlayMode
{
    /// <summary>
    /// The formal S0b evidence recorder. This is intentionally Explicit: it writes candidate C0
    /// once, from the serialized preview camera and normal player-loop updates only. It is not a
    /// visual verdict and it must be invoked through Invoke-Unity.ps1 -Mode PlayMode -UseGraphics.
    /// </summary>
    [Explicit("Formal W24 S0b graphics evidence. Requires built S0b assets and writes the immutable C0 evidence directory once.")]
    public sealed class W24SustainedFlameFormalEvidenceTests
    {
        private const string ScenePath = "Assets/VFX/Preview/VFXPREVIEW_SustainedFlame.unity";
        private const string PrefabPath = "Assets/VFX/Effects/Aura/sustained_flame_3d/VFX_sustained_flame_3d.prefab";
        private const string ManifestRelativePath = "ProjectSettings/VFXComposer/BuildManifests/sustained_flame_3d.manifest.json";
        private const string RendererRelativePath = "Assets/Settings/VFXPreviewUniversalRenderer.asset";
        private const string GraphicsSettingsRelativePath = "ProjectSettings/GraphicsSettings.asset";
        private const string ToolRelativePath = "Packages/com.vfxcomposer.unity/Tests/PlayMode/W24SustainedFlameFormalEvidenceTests.cs";
        private const string ContractRelativePath = "docs/vfx-candidates/sustained_flame_3d/C0/design-contract.json";
        private const string TraceRelativePath = "docs/vfx-candidates/sustained_flame_3d/C0/implementation-trace.json";
        private const string CandidateReceiptRelativePath = "docs/vfx-candidates/sustained_flame_3d/C0/candidate-receipt.json";
        private const string CaptureToolBundleRelativePath = "docs/vfx-contracts/capture-tools/sustained-flame-capture-tool.bundle.json";
        private const string FrozenContractRelativePath = "docs/vfx-contracts/sustained_flame_3d.contract.json";
        private const string CaptureToolVersion = "w24-s0b-formal-capture/1.2.12";
        private const string CandidateId = "C0";
        private static readonly string[] CaptureToolRelativePaths =
        {
            "Packages/com.vfxcomposer.unity/Runtime/Diagnostics/W24CaptureProfile.cs",
            "Packages/com.vfxcomposer.unity/Runtime/Diagnostics/W24ContinuousCaptureRecorder.cs",
            "Packages/com.vfxcomposer.unity/Runtime/Diagnostics/W24EvidenceStore.cs",
            "Packages/com.vfxcomposer.unity/Editor/Validation/RecipeCanonicalizer.cs",
            "Packages/com.vfxcomposer.unity/Editor/W24/S1/VfxDesignContract.cs",
            "Packages/com.vfxcomposer.unity/Editor/W24/S1/VfxImplementationTrace.cs",
            "Packages/com.vfxcomposer.unity/Editor/W24/S5/W24S5EvidenceTransition.cs",
            "Packages/com.vfxcomposer.unity/Editor/W24/S5/W24S5ProductionGate.cs",
            "Packages/com.vfxcomposer.unity/Editor/W24/S5/W24S5RecorderCaptureCompletion.cs",
            ToolRelativePath
        };
        // Start (0.35 s), then 4.5 s steady: 3 x the 1.37 s Shuriken duration plus margin.
        // Exit frames are global so C0 can be compared across stop and interrupt branches.
        private static readonly int[] RetainedFrames = { 1, 21, 60, 120, 180, 240, 270, 291, 293, 321, 366 };

        private sealed class BranchPlan
        {
            public uint Seed;
            public bool Interrupt;
            public string Exit { get { return Interrupt ? "interrupt" : "stop"; } }
        }

        private sealed class BranchEvidence
        {
            public BranchPlan Plan;
            public int ObservedFrames;
            public int RetainedFrames;
            public int FirstSteadyFrame = -1;
            public bool SawRequestedExit;
            public bool SawExitCarrier;
            public int LastLitExitFrame = -1;
            public float PeakLightIntensity;
            public bool SteadyAtExitRequest;
            public SustainedEffectTelemetry Final;
            public readonly List<string> Frames = new List<string>();
            public readonly HashSet<int> CapturedFrameIndices = new HashSet<int>();

            public string ToJson()
            {
                return "{\"seed\":" + Plan.Seed.ToString(CultureInfo.InvariantCulture)
                    + ",\"exit\":\"" + Plan.Exit + "\",\"observedFrames\":" + ObservedFrames
                    + ",\"retainedFrames\":" + RetainedFrames + ",\"firstSteadyFrame\":" + FirstSteadyFrame
                    + ",\"sawRequestedExit\":" + Bool(SawRequestedExit) + ",\"sawExitCarrier\":" + Bool(SawExitCarrier)
                    + ",\"steadyAtExitRequest\":" + Bool(SteadyAtExitRequest) + ",\"lastLitExitFrame\":" + LastLitExitFrame + ",\"peakLightIntensity\":" + Number(PeakLightIntensity)
                    + ",\"frames\":[" + string.Join(",", Frames) + "],\"final\":" + TelemetryJson(Final) + "}";
            }
        }

        private sealed class RuntimeFacts
        {
            public bool LayersIndependent;
            public bool LightWithinContract;
            public bool BudgetWithinContract;
            public int ParticleSystemCount;
            public int ParticleCapacity;
            public int ParticleRendererCount;
            public int MaterialCount;
            public int LightCount;
            public string ToJson()
            {
                return "{\"layersIndependent\":" + Bool(LayersIndependent) + ",\"lightWithinContract\":" + Bool(LightWithinContract)
                    + ",\"budgetWithinContract\":" + Bool(BudgetWithinContract) + ",\"particleSystemCount\":" + ParticleSystemCount
                    + ",\"particleCapacity\":" + ParticleCapacity + ",\"particleRendererCount\":" + ParticleRendererCount
                    + ",\"materialCount\":" + MaterialCount + ",\"lightCount\":" + LightCount + "}";
            }
        }

        [UnityTest]
        public IEnumerator Capture_C0_ActualLifecycleAndLightDiagnostics_FromOneSerializedCamera()
        {
            W24ContinuousCaptureRecorder.RequireGraphicsBatchmode();
            RequireFormalInputs();
            var operation = LoadFormalSceneAsset(ScenePath);
            Assert.NotNull(operation, "S0b preview scene must be enabled for the formal graphics capture.");
            yield return operation;

            var scene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
            var cameras = Find<Camera>(scene);
            var controllers = Find<SustainedEffectController>(scene);
            var drivers = Find<SustainedEffectPreviewDriver>(scene);
            var receivers = Find<Renderer>(scene).Where(value => value.name == "Preview_LightReceiverMarker").ToArray();
            Assert.That(cameras, Has.Length.EqualTo(1), "Formal capture permits exactly one serialized MainCamera.");
            Assert.That(controllers, Has.Length.EqualTo(1), "Formal preview must contain exactly one Runtime Entry controller.");
            Assert.That(drivers, Has.Length.EqualTo(1), "Formal preview must contain exactly one preview-only lifecycle driver.");
            Assert.That(receivers, Has.Length.EqualTo(1), "Formal preview requires one separate receiver marker for the real-light A/B probe.");

            var camera = cameras[0];
            var controller = controllers[0];
            var previewDriver = drivers[0];
            var receiver = receivers[0];
            Assert.That(camera.name, Is.EqualTo("MainCamera"));
            previewDriver.enabled = false;
            controller.ResetForPool();
            yield return null;

            var recorder = camera.gameObject.AddComponent<W24ContinuousCaptureRecorder>();
            recorder.AuthorityCamera = camera;
            recorder.DiagnosticEffectLayers = 1 << 1; // Built S0b Runtime Entry uses TransparentFX, receiver remains Default.
            var root = ProjectRoot();
            var profile = Profile(camera, root);
            var sources = Sources(root);
            var evidenceRoot = Path.Combine(root, "artifacts", "vfx-evidence", "sustained_flame_3d", CandidateId);
            if (Directory.Exists(evidenceRoot) && Directory.EnumerateFileSystemEntries(evidenceRoot).Any())
                Assert.Ignore("Formal S0b C0 evidence already exists and is write-once: " + evidenceRoot);

            var plans = new[]
            {
                new BranchPlan { Seed = unchecked((uint)profile.CanonicalSeed), Interrupt = false },
                new BranchPlan { Seed = unchecked((uint)profile.RobustnessSeeds[0]), Interrupt = false },
                new BranchPlan { Seed = unchecked((uint)profile.RobustnessSeeds[1]), Interrupt = true }
            };
            var branches = new List<BranchEvidence>();
            ReceiverLightDiagnostic lightDiagnostic = default(ReceiverLightDiagnostic);
            var hasLightDiagnostic = false;
            var captureSealed = false;
            var finalizationCompleted = false;
            try
            {
                var command = FrozenOperatorCommand(profile, plans);
                var commandHash = HashText(command);
                VerifyFrozenCaptureProfile(root, profile, camera);
                recorder.BeginFormal(evidenceRoot, CandidateId, profile, sources, commandHash);
                Assert.That(recorder.WriteSupplementalDiagnostic("diagnostics/operator-command.json", Encoding.UTF8.GetBytes(command), "formal-capture-command", "Frozen S0b C0 operator command. It declares all three seeds, the retained frame table, and the stop/interrupt mapping before normal PlayerLoop capture begins."), Is.EqualTo(commandHash));

                // Prime the formal observer through one complete natural frame. UnityTest can
                // start after the current LateUpdate, in which case an immediate EndOfFrame yield
                // has no fresh token. This frame is deliberately outside every branch plan.
                yield return null;
                recorder.AcknowledgeObservedPlayerLoopFrame(recorder.ConsumeCompletedPlayerLoopToken());

                var runtimeFacts = CaptureRuntimeFacts(controller);
                foreach (var plan in plans)
                    yield return RunBranch(camera, controller, recorder, plan, receiver, branches, diagnostic => { lightDiagnostic = diagnostic; hasLightDiagnostic = true; });

                VerifyBranchCoverage(profile, plans, branches);
                Assert.That(hasLightDiagnostic, Is.True, "Canonical steady-state receiver A/B must be captured inside the tokenized natural PlayerLoop sequence.");
                Assert.That(lightDiagnostic.OnLinearLuminance, Is.GreaterThan(lightDiagnostic.OffLinearLuminance + .001f), "REQ-LIGHT-RECEIVER: the actual point light must measurably increase the isolated receiver's linear luminance.");
                Assert.That(lightDiagnostic.OffArtifactHash, Does.StartWith("sha256:"), "The receiver-off artifact must already be sealed through the observed PlayerLoop token.");
                Assert.That(lightDiagnostic.OnArtifactHash, Does.StartWith("sha256:"), "The receiver-on artifact must already be sealed through the observed PlayerLoop token.");
                var receiverAbHash = lightDiagnostic.SummaryArtifactHash;
                Assert.That(receiverAbHash, Does.StartWith("sha256:"), "The receiver A/B summary must already be sealed through the same observed PlayerLoop token.");
                var telemetryHash = recorder.WriteSemanticTelemetry("diagnostics/semantic-telemetry.json", Encoding.UTF8.GetBytes("{\"schema\":\"w24-s0b-semantic-telemetry/v2\",\"captureCompleteness\":\"complete\",\"runtimeFacts\":" + runtimeFacts.ToJson() + ",\"branches\":[" + string.Join(",", branches.Select(value => value.ToJson()).ToArray()) + "]}"), "Controller and Runtime Entry readback measured after every tokenized natural PlayerLoop frame for canonical and both robustness seeds.");
                var diagnosticHash = recorder.WriteSupplementalDiagnostic("diagnostics/capture-diagnostic-summary.json", Encoding.UTF8.GetBytes(DiagnosticSummary(branches, lightDiagnostic)), "capture-diagnostic-summary", "Retained Beauty/effect-only diagnostics from the serialized authority camera. Beauty is LDR display evidence; receiver A/B is a separate linear measurement.");
                var completedTrace = CompletedMachineTrace(root, telemetryHash, receiverAbHash, diagnosticHash, branches, runtimeFacts, lightDiagnostic);
                recorder.WriteSupplementalDiagnostic("diagnostics/machine-gate-trace.json", Encoding.UTF8.GetBytes(completedTrace), "machine-gate-trace", "Machine gate result derived from this sealed S0b capture; Visual QA and user authority remain explicitly pending.");
                recorder.Complete();
                captureSealed = true;
                FinalizeC0EvidenceThroughEditorGate();
                finalizationCompleted = true;
            }
            finally
            {
                // Unity iterator methods cannot yield from a try/catch body (CS1626).  Preserve
                // the write-once machine failure marker from the finally path instead; NUnit
                // still receives and reports the original exception after cleanup completes.
                if (!finalizationCompleted) WriteAttemptStatus(root, captureSealed ? "FINALIZATION_FAILED" : "FAILED_OR_INCOMPLETE", new InvalidOperationException("Formal S0b capture exited before the C0 evidence transition completed."));
                if (recorder != null && recorder.IsActive) recorder.Abort();
                controller.ResetForPool();
                UnityEngine.Object.Destroy(recorder);
            }
        }

        private static AsyncOperation LoadFormalSceneAsset(string scenePath)
        {
            // Formal capture runs in Editor PlayMode against a contract-pinned scene asset. It
            // must not depend on, or mutate, the Player Build Settings merely to load evidence.
            var type = Type.GetType("UnityEditor.SceneManagement.EditorSceneManager, UnityEditor");
            if (type == null) throw new InvalidOperationException("EditorSceneManager is unavailable for formal PlayMode capture.");
            var method = type.GetMethod("LoadSceneAsyncInPlayMode", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(string), typeof(LoadSceneParameters) }, null);
            if (method == null) throw new InvalidOperationException("EditorSceneManager.LoadSceneAsyncInPlayMode is unavailable.");
            var operation = method.Invoke(null, new object[] { scenePath, new LoadSceneParameters(LoadSceneMode.Single) }) as AsyncOperation;
            if (operation == null) throw new InvalidOperationException("Formal scene asset load did not return an AsyncOperation: " + scenePath);
            return operation;
        }

        private static IEnumerator RunBranch(Camera camera, SustainedEffectController controller, W24ContinuousCaptureRecorder recorder, BranchPlan plan, Renderer receiver, List<BranchEvidence> branches, Action<ReceiverLightDiagnostic> receiverDiagnostic)
        {
            controller.ResetForPool();
            var evidence = new BranchEvidence { Plan = plan };
            var logicalFrame = 0;
            var completed = false;
            var branchStartedOnFrameBoundary = false;
            Exception observationFailure = null;
            Action<int, float> observer = (playerLoopFrame, playerLoopTime) =>
            {
                if (completed || observationFailure != null) return;
                try
                {
                    var token = recorder.ConsumeCompletedPlayerLoopToken();
                    // RunBranch is entered by a coroutine and can therefore subscribe after
                    // this frame's Update has already executed. Consume that boundary token,
                    // then start the branch in LateUpdate so logical frame 1 is guaranteed to
                    // follow one complete natural controller Update on every host/batch mode.
                    if (!branchStartedOnFrameBoundary)
                    {
                        recorder.AcknowledgeObservedPlayerLoopFrame(token);
                        controller.ResetForPool();
                        controller.PlayWithSeed(plan.Seed);
                        branchStartedOnFrameBoundary = true;
                        return;
                    }
                    logicalFrame++;
                    var sample = controller.ReadTelemetry();
                    evidence.ObservedFrames++;
                    evidence.PeakLightIntensity = Mathf.Max(evidence.PeakLightIntensity, GetControlledLight(controller).intensity);
                    if (sample.State == SustainedEffectState.Steady && evidence.FirstSteadyFrame < 0) evidence.FirstSteadyFrame = logicalFrame;
                    if (logicalFrame > 291)
                    {
                        var exitState = plan.Interrupt ? SustainedEffectState.Interrupted : SustainedEffectState.Stopping;
                        if (sample.State == exitState) evidence.SawRequestedExit = true;
                        if (sample.EnabledLightCount > 0) evidence.LastLitExitFrame = logicalFrame;
                        evidence.SawExitCarrier |= ExitCarrierIsExclusive(controller, plan.Interrupt);
                    }
                    RecordTelemetry(evidence, sample, logicalFrame);
                    if (plan.Seed == unchecked((uint)24001) && logicalFrame == 180)
                    {
                        Assert.That(sample.State, Is.EqualTo(SustainedEffectState.Steady));
                        var diagnostic = CaptureReceiverLightDiagnostic(camera, controller, receiver);
                        diagnostic.OffArtifactHash = recorder.WriteObservedSupplementalDiagnostic(token, logicalFrame, plan.Seed, "diagnostics/receiver-light-off.png", diagnostic.OffPng, "receiver-light-off", "Receiver-only linear diagnostic; all Runtime Entry renderers hidden and the actual Light disabled at this natural PlayerLoop observation.");
                        diagnostic.OnArtifactHash = recorder.WriteObservedSupplementalDiagnostic(token, logicalFrame, plan.Seed, "diagnostics/receiver-light-on.png", diagnostic.OnPng, "receiver-light-on", "Receiver-only linear diagnostic; all Runtime Entry renderers hidden and the actual Light enabled at this same natural PlayerLoop observation.");
                        diagnostic.SummaryArtifactHash = recorder.WriteObservedSupplementalDiagnostic(token, logicalFrame, plan.Seed, "diagnostics/receiver-light-ab.json", Encoding.UTF8.GetBytes(diagnostic.ToJson()), "receiver-linear-luminance-ab", "Matched receiver-only probe bound to the same seed, logical frame, LateUpdate serial, and PlayerLoop time; only UnityEngine.Light.enabled changes between A/B.");
                        receiverDiagnostic(diagnostic);
                    }
                    if (RetainedFrames.Contains(logicalFrame)) { recorder.CaptureObservedPlayerLoopFrame(token, logicalFrame, StateToken(sample.State), plan.Seed); evidence.RetainedFrames++; evidence.CapturedFrameIndices.Add(logicalFrame); }
                    else recorder.AcknowledgeObservedPlayerLoopFrame(token);

                    if (logicalFrame == 291)
                    {
                        Assert.That(evidence.FirstSteadyFrame, Is.GreaterThan(0).And.LessThanOrEqualTo(21), "REQ-LIFECYCLE-START: starting must reach steady by the frozen 0.35-second deadline.");
                        Assert.That(controller.State, Is.EqualTo(SustainedEffectState.Steady), "REQ-LIFECYCLE-STEADY: the effect must remain in its explicit steady state for at least three 1.37-second cycles.");
                        Assert.That(controller.ReadTelemetry().EnabledLightCount, Is.EqualTo(1), "REQ-LIGHT-REAL: one actual controlled Light is active during steady combustion.");
                        evidence.SteadyAtExitRequest = controller.State == SustainedEffectState.Steady;
                        if (plan.Interrupt) controller.Interrupt(); else controller.Stop(VfxStopMode.AllowTail);
                    }
                    if (logicalFrame == 366) completed = true;
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

            evidence.Final = controller.ReadTelemetry();
            var exit = plan.Interrupt ? SustainedEffectState.Interrupted : SustainedEffectState.Stopping;
            Assert.That(evidence.SawRequestedExit, Is.True, "The requested " + exit + " branch must be observed, not replaced by the other exit.");
            Assert.That(evidence.SawExitCarrier, Is.True, "The requested " + exit + " must activate only its own semantic exit carrier.");
            Assert.That(evidence.Final.State, Is.EqualTo(SustainedEffectState.Idle), "REQ-LIFECYCLE cleanup must return to idle after the bounded exit.");
            Assert.That(evidence.Final.CleanupComplete, Is.True);
            Assert.That(evidence.Final.EnabledLightCount, Is.EqualTo(0), "Real light must be disabled after cleanup.");
            var maximumLightFade = plan.Interrupt ? .35f : .8f;
            Assert.That(evidence.LastLitExitFrame, Is.GreaterThanOrEqualTo(292), "REQ-LIGHT-REAL: the requested exit begins from an active controlled light, rather than replacing the light layer with a hard pre-exit disable.");
            Assert.That((evidence.LastLitExitFrame - 291) / 60f, Is.LessThanOrEqualTo(maximumLightFade + (1f / 60f)), "REQ-LIFECYCLE-" + plan.Exit.ToUpperInvariant() + ": controlled light must finish its declared branch fade on time.");
            branches.Add(evidence);
        }

        private static void RecordTelemetry(BranchEvidence evidence, SustainedEffectTelemetry sample, int frame)
        {
            evidence.Frames.Add("{\"frameIndex\":" + frame + ",\"state\":\"" + StateToken(sample.State) + "\",\"sample\":" + TelemetryJson(sample) + "}");
        }

        private static Light GetControlledLight(SustainedEffectController controller)
        {
            var lights = controller.GetComponentsInChildren<Light>(true);
            Assert.That(lights, Has.Length.EqualTo(1), "S0b permits exactly one controlled UnityEngine.Light.");
            return lights[0];
        }

        private static bool ExitCarrierIsExclusive(SustainedEffectController controller, bool interrupt)
        {
            var transforms = controller.GetComponentsInChildren<Transform>(true);
            var stop = transforms.Single(value => value.name == "StopTail").gameObject;
            var burst = transforms.Single(value => value.name == "InterruptBurst").gameObject;
            return interrupt ? burst.activeInHierarchy && !stop.activeInHierarchy : stop.activeInHierarchy && !burst.activeInHierarchy;
        }

        private static RuntimeFacts CaptureRuntimeFacts(SustainedEffectController controller)
        {
            var particles = controller.GetComponentsInChildren<ParticleSystem>(true);
            var renderers = controller.GetComponentsInChildren<ParticleSystemRenderer>(true);
            var light = GetControlledLight(controller);
            var expectedNames = new HashSet<string>(new[] { "Ignition", "CoreFlame", "OuterFlame", "Smoke", "Embers", "StopTail", "InterruptBurst" }, StringComparer.Ordinal);
            var actualNames = new HashSet<string>(particles.Select(value => value.name), StringComparer.Ordinal);
            var allSeedsExplicit = particles.All(value => !value.useAutoRandomSeed);
            var uniqueSeeds = particles.Select(value => value.randomSeed).Distinct().Count() == particles.Length;
            var noRuntimeTextures = renderers.All(value => value.sharedMaterial != null && value.sharedMaterial.GetTexturePropertyNames().All(name => value.sharedMaterial.GetTexture(name) == null));
            var facts = new RuntimeFacts
            {
                ParticleSystemCount = particles.Length,
                ParticleCapacity = particles.Sum(value => value.main.maxParticles),
                ParticleRendererCount = renderers.Length,
                MaterialCount = renderers.Select(value => value.sharedMaterial).Distinct().Count(),
                LightCount = controller.GetComponentsInChildren<Light>(true).Length,
                LayersIndependent = particles.Length == 7 && renderers.Length == 7 && expectedNames.SetEquals(actualNames) && allSeedsExplicit && uniqueSeeds,
                LightWithinContract = light.type == LightType.Point && light.shadows == LightShadows.None && light.range <= 2.45f && light.intensity <= 1.25f,
                BudgetWithinContract = particles.Sum(value => value.main.maxParticles) <= 144 && renderers.Length == 7 && renderers.Select(value => value.sharedMaterial).Distinct().Count() <= 4 && controller.GetComponentsInChildren<Light>(true).Length == 1 && noRuntimeTextures
            };
            Assert.That(facts.LayersIndependent, Is.True, "REQ-LAYERS-INDEPENDENT requires seven independently addressable seeded Shuriken carriers.");
            Assert.That(facts.LightWithinContract, Is.True, "REQ-LIGHT-REAL requires one unshadowed bounded point light.");
            Assert.That(facts.BudgetWithinContract, Is.True, "REQ-BUDGET-S0B requires the structural capacity/renderer/material/light/texture limits.");
            return facts;
        }

        private static void VerifyBranchCoverage(W24CaptureProfile profile, BranchPlan[] plans, List<BranchEvidence> branches)
        {
            Assert.That(branches, Has.Count.EqualTo(3), "Formal C0 must close all three frozen seed branches.");
            Assert.That(branches.Select(value => value.Plan.Seed), Is.EquivalentTo(profile.AllSeeds().Select(value => unchecked((uint)value))), "Formal C0 must use the complete frozen three-seed set.");
            for (var index = 0; index < plans.Length; index++)
            {
                var branch = branches.Single(value => value.Plan.Seed == plans[index].Seed);
                Assert.That(branch.Plan.Exit, Is.EqualTo(plans[index].Exit), "The frozen operator command selects the exit branch per seed.");
                Assert.That(branch.ObservedFrames, Is.EqualTo(366), "Every natural PlayerLoop frame through cleanup must be tokenized and acknowledged.");
                Assert.That(branch.RetainedFrames, Is.EqualTo(RetainedFrames.Length), "Every seed retains the full frozen frame table.");
                Assert.That(branch.CapturedFrameIndices.SetEquals(RetainedFrames), Is.True, "Every seed must retain exactly the frozen frame indices.");
                Assert.That(branch.FirstSteadyFrame, Is.GreaterThan(0).And.LessThanOrEqualTo(21));
                Assert.That(branch.SteadyAtExitRequest, Is.True);
                Assert.That(branch.SawRequestedExit && branch.SawExitCarrier, Is.True);
                Assert.That(branch.Final.State, Is.EqualTo(SustainedEffectState.Idle));
                Assert.That(branch.Final.CleanupComplete && branch.Final.EnabledLightCount == 0, Is.True);
            }
            Assert.That(branches.Count(value => value.Plan.Exit == "stop"), Is.EqualTo(2));
            Assert.That(branches.Count(value => value.Plan.Exit == "interrupt"), Is.EqualTo(1));
        }

        private static string FrozenOperatorCommand(W24CaptureProfile profile, BranchPlan[] plans)
        {
            var command = new JObject
            {
                ["schema"] = "w24-s0b-formal-operator-command/v1",
                ["effectId"] = "sustained_flame_3d",
                ["candidateId"] = CandidateId,
                ["captureToolVersion"] = CaptureToolVersion,
                ["captureProfileSha256"] = profile.Sha256,
                ["seeds"] = new JArray(profile.AllSeeds().Select(value => new JValue(unchecked((uint)value)))),
                ["retainedFrameIndices"] = new JArray(RetainedFrames),
                ["branches"] = new JArray(plans.Select(value => new JObject
                {
                    ["seed"] = value.Seed,
                    ["exit"] = value.Exit,
                    ["steadyFramesBeforeExit"] = 291,
                    ["cleanupThroughFrame"] = 366
                }))
            };
            return CanonicalJson(command);
        }

        private static void VerifyFrozenCaptureProfile(string root, W24CaptureProfile profile, Camera camera)
        {
            Assert.That(profile.ProfileVersion, Is.EqualTo("w24-s0b-capture-profile/v1"));
            Assert.That(profile.UrpVersion, Is.EqualTo("14.0.12"));
            Assert.That(profile.Width, Is.EqualTo(960));
            Assert.That(profile.Height, Is.EqualTo(540));
            Assert.That(profile.FramesPerSecond, Is.EqualTo(60));
            Assert.That(profile.ColorSpace, Is.EqualTo("Linear"));
            Assert.That(profile.Hdr, Is.False, "Beauty is intentionally LDR display evidence; HDR capture is not part of frozen C0.");
            Assert.That(profile.Msaa, Is.False, "MSAA is disabled for the frozen ARGB32 readback contract.");
            Assert.That(profile.RenderTextureFormat, Is.EqualTo(RenderTextureFormat.ARGB32.ToString()));
            Assert.That(profile.Bloom, Is.False);
            Assert.That(profile.ToneMapping, Is.EqualTo("None"));
            Assert.That(profile.CanonicalSeed, Is.EqualTo(24001));
            Assert.That(profile.RobustnessSeeds, Is.EquivalentTo(new[] { 24011, 24021 }));
            Assert.That(profile.RetainedFrameIndices, Is.EquivalentTo(RetainedFrames));
            var contract = JObject.Parse(File.ReadAllText(Path.Combine(root, FrozenContractRelativePath)));
            var frozen = (JObject)contract["captureProfile"];
            Assert.That(profile.UnityVersion, Is.EqualTo((string)frozen["unityVersion"]));
            Assert.That(profile.UrpVersion, Is.EqualTo((string)frozen["urpVersion"]));
            Assert.That(profile.GraphicsApi, Is.EqualTo((string)frozen["graphicsApi"]));
            Assert.That(profile.GraphicsDevice + " / " + profile.GraphicsDriverVersion, Is.EqualTo((string)frozen["graphicsDeviceDriver"]));
            Assert.That(profile.SerializedCameraReference, Is.EqualTo((string)frozen["cameraSerializedReference"]));
            Assert.That(profile.ScenePath, Is.EqualTo((string)frozen["sceneSerializedReference"]));
            Assert.That(profile.RendererAssetReference, Is.EqualTo((string)frozen["rendererAssetSerializedReference"]));
            Assert.That(profile.VolumeReference, Does.StartWith("ProjectSettings/GraphicsSettings.asset"));
            Assert.That(profile.Width, Is.EqualTo((int)frozen["resolution"]["width"]));
            Assert.That(profile.Height, Is.EqualTo((int)frozen["resolution"]["height"]));
            Assert.That(profile.FramesPerSecond, Is.EqualTo((int)frozen["fps"]));
            Assert.That(profile.RenderTextureFormat, Is.EqualTo((string)frozen["renderTextureFormat"]));
            Assert.That(profile.ColorSpace, Is.EqualTo((string)frozen["colorSpace"]));
            Assert.That(profile.Hdr, Is.EqualTo((bool)frozen["hdr"]));
            Assert.That(profile.Msaa, Is.EqualTo((int)frozen["msaaSamples"] > 1));
            Assert.That(profile.Bloom, Is.EqualTo((bool)frozen["bloom"]["enabled"]));
            Assert.That(profile.ToneMapping, Is.EqualTo((string)frozen["toneMapping"]));
            Assert.That(profile.CanonicalSeed, Is.EqualTo((int)frozen["canonicalSeed"]));
            Assert.That(profile.RobustnessSeeds, Is.EquivalentTo(((JArray)frozen["robustnessSeeds"]).Select(value => (int)value)));
            Assert.That(camera.fieldOfView, Is.EqualTo((float)frozen["cameraFovDegrees"]).Within(.001f));
            var pose = (JObject)frozen["cameraPose"];
            Assert.That(camera.transform.position.x, Is.EqualTo((float)pose["position"][0]).Within(.001f));
            Assert.That(camera.transform.position.y, Is.EqualTo((float)pose["position"][1]).Within(.001f));
            Assert.That(camera.transform.position.z, Is.EqualTo((float)pose["position"][2]).Within(.001f));
            Assert.That(camera.transform.eulerAngles.x, Is.EqualTo((float)pose["orientationEulerDegrees"][0]).Within(.01f));
            Assert.That(camera.transform.eulerAngles.y, Is.EqualTo((float)pose["orientationEulerDegrees"][1]).Within(.01f));
            Assert.That(camera.transform.eulerAngles.z, Is.EqualTo((float)pose["orientationEulerDegrees"][2]).Within(.01f));
            Assert.That(profile.Background.r, Is.EqualTo(.035f).Within(.0001f));
            Assert.That(profile.Background.g, Is.EqualTo(.04f).Within(.0001f));
            Assert.That(profile.Background.b, Is.EqualTo(.055f).Within(.0001f));
            Assert.That(profile.Background.a, Is.EqualTo(1f).Within(.0001f));
            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(root, "project", "Packages", "manifest.json")));
            var lockFile = JObject.Parse(File.ReadAllText(Path.Combine(root, "project", "Packages", "packages-lock.json")));
            Assert.That((string)manifest["dependencies"]["com.unity.render-pipelines.universal"], Is.EqualTo(profile.UrpVersion), "URP manifest version drift invalidates the frozen capture contract.");
            Assert.That((string)lockFile["dependencies"]["com.unity.render-pipelines.universal"]["version"], Is.EqualTo(profile.UrpVersion), "URP package-lock version drift invalidates the frozen capture contract.");
        }

        private static string DiagnosticSummary(List<BranchEvidence> branches, ReceiverLightDiagnostic light)
        {
            return "{\"schema\":\"w24-s0b-capture-diagnostic-summary/v2\",\"beautyEncoding\":\"LDR ARGB32 serialized authority camera\",\"effectOnlyEncoding\":\"RGBA effect-only diagnostic from the same serialized authority camera\",\"receiverEncoding\":\"linear ARGB32 receiver-only A/B measurement\",\"branches\":[" + string.Join(",", branches.Select(value => value.ToJson()).ToArray()) + "],\"receiverLight\":" + light.ToJson() + "}";
        }

        private static void WriteAttemptStatus(string root, string status, Exception error)
        {
            try
            {
                var candidateReceiptHash = HashFile(Path.Combine(root, CandidateReceiptRelativePath.Replace('/', Path.DirectorySeparatorChar)));
                var path = Path.Combine(root, "artifacts", "vfx-evidence", "sustained_flame_3d", CandidateId + ".capture-attempt-status." + candidateReceiptHash.Substring("sha256:".Length) + ".json");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    writer.Write("{\"schema\":\"w24-s0b-capture-attempt-status/v2\",\"effectId\":\"sustained_flame_3d\",\"candidateId\":\"C0\",\"candidateReceiptFileHash\":\"" + candidateReceiptHash + "\",\"status\":\"" + Escape(status) + "\",\"errorType\":\"" + Escape(error.GetType().FullName) + "\",\"errorMessage\":\"" + Escape(error.Message) + "\"}");
            }
            catch (IOException) { /* preserve the first write-once machine-readable failure. */ }
        }

        private static ReceiverLightDiagnostic CaptureReceiverLightDiagnostic(Camera camera, SustainedEffectController controller, Renderer receiver)
        {
            var light = GetControlledLight(controller);
            Assert.That(light.type, Is.EqualTo(LightType.Point));
            Assert.That(light.shadows, Is.EqualTo(LightShadows.None));
            Assert.That(light.range, Is.LessThanOrEqualTo(2.45f));
            Assert.That(light.intensity, Is.GreaterThan(0f).And.LessThanOrEqualTo(1.25f));
            var particleRenderers = controller.GetComponentsInChildren<ParticleSystemRenderer>(true);
            var allEffectRenderers = controller.GetComponentsInChildren<Renderer>(true);
            Assert.That(particleRenderers, Has.Length.EqualTo(7), "Receiver A/B must disable every effect renderer, not merely a chosen visual subset.");
            Assert.That(allEffectRenderers, Is.EquivalentTo(particleRenderers), "S0b has no hidden mesh/sprite renderer that could contaminate receiver A/B.");
            Assert.That(receiver.transform.IsChildOf(controller.transform), Is.False, "Receiver must be a separate scene object, never a child of the Runtime Entry.");
            Assert.That(particleRenderers.All(value => value.gameObject.layer != receiver.gameObject.layer), Is.True, "Receiver must stay outside the effect-only diagnostic mask.");
            var priorEnabled = particleRenderers.Select(value => value.enabled).ToArray();
            var priorReceiverEnabled = receiver.enabled;
            var priorLightEnabled = light.enabled;
            var priorIntensity = light.intensity;
            try
            {
                for (var index = 0; index < particleRenderers.Length; index++) particleRenderers[index].enabled = false;
                receiver.enabled = true;
                light.enabled = false;
                var off = SampleLinearReceiver(camera, receiver);
                light.enabled = true;
                light.intensity = Mathf.Max(.001f, priorIntensity);
                var on = SampleLinearReceiver(camera, receiver);
                return new ReceiverLightDiagnostic { OffLinearLuminance = off.Luminance, OnLinearLuminance = on.Luminance, OffPng = off.Png, OnPng = on.Png, LightType = light.type.ToString(), LightRange = light.range, LightIntensity = light.intensity, ShadowMode = light.shadows.ToString(), RendererIsolation = "all-7-particle-renderers-disabled; receiver-outside-runtime-entry-and-effect-layer" };
            }
            finally
            {
                light.enabled = priorLightEnabled;
                light.intensity = priorIntensity;
                receiver.enabled = priorReceiverEnabled;
                for (var index = 0; index < particleRenderers.Length; index++) particleRenderers[index].enabled = priorEnabled[index];
            }
        }

        private static ReceiverProbe SampleLinearReceiver(Camera camera, Renderer receiver)
        {
            var texture = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var priorTarget = camera.targetTexture;
            var priorActive = RenderTexture.active;
            Texture2D image = null;
            try
            {
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                image = new Texture2D(960, 540, TextureFormat.RGBA32, false, true);
                image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                image.Apply(false);
                var screen = camera.WorldToScreenPoint(receiver.bounds.center);
                Assert.That(screen.z, Is.GreaterThan(0f), "Receiver marker must be in front of the authority camera.");
                var centerX = Mathf.Clamp(Mathf.RoundToInt(screen.x), 8, 951);
                var centerY = Mathf.Clamp(Mathf.RoundToInt(screen.y), 8, 531);
                var total = 0f;
                var samples = 0;
                for (var y = centerY - 7; y <= centerY + 7; y++)
                    for (var x = centerX - 7; x <= centerX + 7; x++)
                    {
                        var pixel = image.GetPixel(x, y);
                        total += .2126f * pixel.r + .7152f * pixel.g + .0722f * pixel.b;
                        samples++;
                    }
                return new ReceiverProbe { Luminance = total / Mathf.Max(1, samples), Png = image.EncodeToPNG() };
            }
            finally
            {
                if (image != null) UnityEngine.Object.Destroy(image);
                camera.targetTexture = priorTarget;
                RenderTexture.active = priorActive;
                RenderTexture.ReleaseTemporary(texture);
            }
        }

        private static W24CaptureProfile Profile(Camera camera, string root)
        {
            return new W24CaptureProfile
            {
                ProfileVersion = "w24-s0b-capture-profile/v1",
                UnityVersion = Application.unityVersion,
                UrpVersion = "14.0.12",
                GraphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                GraphicsDevice = SystemInfo.graphicsDeviceName,
                GraphicsDriverVersion = SystemInfo.graphicsDeviceVersion,
                RenderTextureFormat = RenderTextureFormat.ARGB32.ToString(),
                RendererAssetReference = RendererRelativePath,
                RendererAssetSha256 = HashFile(Path.Combine(root, "project", RendererRelativePath)),
                VolumeReference = GraphicsSettingsRelativePath + " (no per-scene Volume; bloom/tone mapping disabled)",
                VolumeSha256 = HashFile(Path.Combine(root, "project", GraphicsSettingsRelativePath)),
                ScenePath = ScenePath,
                SerializedCameraReference = ScenePath + "#MainCamera",
                Width = 960,
                Height = 540,
                FramesPerSecond = 60,
                Background = camera.backgroundColor,
                ColorSpace = QualitySettings.activeColorSpace.ToString(),
                Hdr = camera.allowHDR,
                Msaa = camera.allowMSAA,
                Bloom = false,
                ToneMapping = "None",
                CanonicalSeed = 24001,
                RobustnessSeeds = new[] { 24011, 24021 },
                RetainedFrameIndices = RetainedFrames
            };
        }

        private static W24CaptureSourceHashes Sources(string root)
        {
            var project = Path.Combine(root, "project");
            var manifest = Path.Combine(project, ManifestRelativePath);
            var build = Regex.Match(File.ReadAllText(manifest), "\\\"buildHash\\\"\\s*:\\s*\\\"(?<hash>[0-9a-fA-F]{64})\\\"");
            Assert.That(build.Success, Is.True, "S0b BuildManifest must expose a 64-character buildHash.");
            var scene = Path.Combine(project, ScenePath);
            var prefab = Path.Combine(project, PrefabPath);
            var contractPath = Path.Combine(root, ContractRelativePath);
            var tracePath = Path.Combine(root, TraceRelativePath);
            var receiptPath = Path.Combine(root, CandidateReceiptRelativePath);
            var bundlePath = Path.Combine(root, CaptureToolBundleRelativePath);
            var contract = JObject.Parse(File.ReadAllText(contractPath));
            var trace = JObject.Parse(File.ReadAllText(tracePath));
            var receipt = JObject.Parse(File.ReadAllText(receiptPath));
            var bundleText = File.ReadAllText(bundlePath);
            var bundle = JObject.Parse(bundleText);
            Assert.That((string)bundle["toolVersion"], Is.EqualTo(CaptureToolVersion));
            var registeredSources = ((JArray)bundle["sources"]).OfType<JObject>().ToArray();
            var expectedSourcePaths = CaptureToolRelativePaths.Select(path => "project/" + path).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var registeredSourcePaths = registeredSources.Select(source => (string)source["path"]).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expectedSourcePaths, registeredSourcePaths, "The S0b source manifest must be bidirectionally exact; an unregistered authority source or an unrelated source invalidates the capture-tool identity.");
            Assert.That(registeredSourcePaths.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(registeredSourcePaths.Length), "Capture-tool source paths must be unique.");
            foreach (var source in registeredSources)
                Assert.That((string)source["sha256"], Is.EqualTo(HashFile(Path.Combine(root, ((string)source["path"]).Replace('/', Path.DirectorySeparatorChar)))), "Capture-tool source drifted after C0 registration.");
            var captureToolHash = HashText(CanonicalJson(bundle));
            Assert.That((string)contract["captureProfile"]["captureToolHash"], Is.EqualTo(captureToolHash), "Candidate Contract must bind the exact reproducible capture-tool bundle.");
            Assert.That((string)contract["captureProfile"]["captureToolVersion"], Is.EqualTo(CaptureToolVersion));
            Assert.That((string)contract["extensions"]["captureBindingStatus"], Is.EqualTo("FROZEN_PRE_C0"));
            Assert.That((string)contract["extensions"]["candidateStatus"], Is.EqualTo("C0_CAPTURE_PENDING"));
            Assert.That((string)contract["captureProfile"]["sceneHash"], Is.EqualTo(HashFile(scene)));
            Assert.That((string)contract["captureProfile"]["prefabManifestHash"], Is.EqualTo("sha256:" + build.Groups["hash"].Value.ToLowerInvariant()));
            Assert.That((string)trace["traceStatus"], Is.EqualTo("C0_CAPTURE_PENDING"));
            Assert.That((string)trace["contractHash"], Is.EqualTo((string)contract["contractHash"]));
            Assert.That((string)trace["buildHash"], Is.EqualTo("sha256:" + build.Groups["hash"].Value.ToLowerInvariant()));
            Assert.That((string)trace["runtimeEntryGuid"], Is.EqualTo(ReadGuid(prefab + ".meta")));
            Assert.That((string)receipt["contractFileHash"], Is.EqualTo(HashFile(contractPath)));
            Assert.That((string)receipt["traceFileHash"], Is.EqualTo(HashFile(tracePath)));
            var bootstrapManifest = Path.Combine(root, "docs", "vfx-candidates", "sustained_flame_3d", "C0", "bootstrap-manifest.json");
            Assert.That(File.Exists(bootstrapManifest), Is.True);
            Assert.That((string)receipt["bootstrapManifestSnapshotPath"], Is.EqualTo("docs/vfx-candidates/sustained_flame_3d/C0/bootstrap-manifest.json"));
            Assert.That((string)receipt["bootstrapManifestSnapshotFileHash"], Is.EqualTo(HashFile(bootstrapManifest)));
            Assert.That((string)receipt["captureProfileHash"], Is.EqualTo((string)trace["captureProfileHash"]));
            return new W24CaptureSourceHashes
            {
                SceneSourcePath = scene,
                SceneSha256 = HashFile(scene),
                PrefabSourcePath = prefab,
                PrefabGuid = ReadGuid(prefab + ".meta"),
                PrefabSha256 = HashFile(prefab),
                ManifestSourcePath = manifest,
                ManifestSha256 = HashFile(manifest),
                BuildHash = "sha256:" + build.Groups["hash"].Value.ToLowerInvariant(),
                CaptureToolSourcePath = string.Join(";", CaptureToolRelativePaths),
                CaptureToolVersion = CaptureToolVersion,
                CaptureToolSha256 = captureToolHash
            };
        }

        private static T[] Find<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        }

        private static string StateToken(SustainedEffectState state) { return state.ToString().ToLowerInvariant(); }
        private static string TelemetryJson(SustainedEffectTelemetry sample)
        {
            return "{\"state\":\"" + StateToken(sample.State) + "\",\"stateElapsed\":" + Number(sample.StateElapsed) + ",\"lifetimeElapsed\":" + Number(sample.LifetimeElapsed) + ",\"seed\":" + sample.Seed + ",\"liveParticleCount\":" + sample.LiveParticleCount + ",\"emittingParticleSystemCount\":" + sample.EmittingParticleSystemCount + ",\"enabledRendererCount\":" + sample.EnabledRendererCount + ",\"enabledLightCount\":" + sample.EnabledLightCount + ",\"transitionSerial\":" + sample.TransitionSerial + ",\"cleanupComplete\":" + (sample.CleanupComplete ? "true" : "false") + "}";
        }
        private static string Number(float value) { return value.ToString("0.######", CultureInfo.InvariantCulture); }
        private static string Bool(bool value) { return value ? "true" : "false"; }
        private static string Escape(string value) { return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n"); }
        private static string ReadGuid(string metaPath)
        {
            var match = Regex.Match(File.ReadAllText(metaPath), "^guid:\\s*(?<guid>[0-9a-f]{32})", RegexOptions.Multiline);
            Assert.That(match.Success, Is.True, "Prefab meta must contain a lowercase GUID.");
            return match.Groups["guid"].Value;
        }
        private static void RequireFormalInputs()
        {
            var root = ProjectRoot();
            var required = new[] { Path.Combine(root, "project", ScenePath), Path.Combine(root, "project", PrefabPath), Path.Combine(root, "project", ManifestRelativePath), Path.Combine(root, "project", RendererRelativePath), Path.Combine(root, "project", GraphicsSettingsRelativePath), Path.Combine(root, "project", "Packages", "manifest.json"), Path.Combine(root, "project", "Packages", "packages-lock.json"), Path.Combine(root, "project", ToolRelativePath), Path.Combine(root, FrozenContractRelativePath), Path.Combine(root, ContractRelativePath), Path.Combine(root, TraceRelativePath), Path.Combine(root, CandidateReceiptRelativePath), Path.Combine(root, CaptureToolBundleRelativePath) };
            var missing = required.Where(path => !File.Exists(path)).ToArray();
            if (missing.Length > 0) Assert.Ignore("S0b formal capture precondition is not built yet. Missing: " + string.Join("; ", missing));
        }
        private static string ProjectRoot() { return Directory.GetParent(Application.dataPath).Parent.FullName; }
        private static string HashFile(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return "sha256:" + string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }
        private static string HashText(string value)
        {
            using (var sha = SHA256.Create())
                return "sha256:" + string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
        }
        private static string CanonicalJson(JToken token)
        {
            if (token is JObject obj)
            {
                var sorted = new JObject();
                foreach (var property in obj.Properties().OrderBy(value => value.Name, StringComparer.Ordinal)) sorted.Add(property.Name, JToken.Parse(CanonicalJson(property.Value)));
                return sorted.ToString(Formatting.None);
            }
            if (token is JArray array) return new JArray(array.Select(value => JToken.Parse(CanonicalJson(value)))).ToString(Formatting.None);
            return token.ToString(Formatting.None);
        }

        // This deliberately uses reflection so the Player-safe PlayMode test assembly does not
        // reference Editor code.  The actual call is still the single S5 gate-owned adapter;
        // it runs only after the recorder's final seal exists.  No Visual QA/user evidence is
        // created here, so the resulting C0 evidence revision is evidence-bound, never L3/L4.
        private static void FinalizeC0EvidenceThroughEditorGate()
        {
            var type = Type.GetType("VFXComposer.Editor.W24.S5.W24S5RecorderCaptureCompletion, VFXComposer.Editor", true);
            var method = type.GetMethod("FinalizeSustainedFlameC0Capture", BindingFlags.Static | BindingFlags.Public);
            Assert.NotNull(method, "S5 must expose the batch/CI post-capture completion command.");
            string returnedTracePath = null;
            try { returnedTracePath = method.Invoke(null, null) as string; }
            catch (TargetInvocationException e) { Assert.Fail("Formal C0 evidence binding failed: " + e.InnerException); }
            var tracePath = "docs/vfx-candidates/sustained_flame_3d/C0/evidence/implementation-trace.json";
            Assert.That(returnedTracePath, Is.EqualTo(tracePath), "The post-capture caller must report the exact C0 evidence seal path.");
            var sealedTrace = JObject.Parse(File.ReadAllText(Path.Combine(ProjectRoot(), tracePath.Replace('/', Path.DirectorySeparatorChar))));
            foreach (var requirement in ((JArray)sealedTrace["requirementTraces"]).OfType<JObject>().Where(item => (string)item["evidenceAuthority"] == "visualQa" || (string)item["evidenceAuthority"] == "user"))
            {
                var authority = ((JArray)requirement["authorityEvidence"]).OfType<JObject>().Single();
                Assert.That((bool)authority["passed"], Is.False, "Capture must not fabricate a Visual QA or user pass.");
                Assert.That((string)authority["reference"], Does.StartWith("pending:"), "Pending QA/user authority must not be substituted with a capture artifact.");
            }
        }

        private static string CompletedMachineTrace(string root, string telemetryHash, string receiverAbHash, string diagnosticHash, List<BranchEvidence> branches, RuntimeFacts facts, ReceiverLightDiagnostic light)
        {
            var tracePath = Path.Combine(root, TraceRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var trace = JObject.Parse(File.ReadAllText(tracePath));
            var frozenContract = JObject.Parse(File.ReadAllText(Path.Combine(root, FrozenContractRelativePath.Replace('/', Path.DirectorySeparatorChar))));
            var lightSensitiveRequirements = new HashSet<string>(((JArray)frozenContract["requirements"]).OfType<JObject>()
                .Where(item => ((string)item["statement"] ?? string.Empty).IndexOf("light", StringComparison.OrdinalIgnoreCase) >= 0
                    || ((string)item["statement"] ?? string.Empty).IndexOf("illumination", StringComparison.OrdinalIgnoreCase) >= 0
                    || ((string)item["statement"] ?? string.Empty).IndexOf("光照", StringComparison.Ordinal) >= 0)
                .Select(item => (string)item["designRequirementId"]), StringComparer.Ordinal);
            var evidenceRoot = "artifacts/vfx-evidence/sustained_flame_3d/C0/diagnostics/";
            var beautyReference = "artifacts/vfx-evidence/sustained_flame_3d/C0/frames/seed_24001/frame_00180_beauty.png";
            var beautyHash = HashFile(Path.Combine(root, beautyReference.Replace('/', Path.DirectorySeparatorChar)));
            foreach (var requirement in ((JArray)trace["requirementTraces"]).OfType<JObject>())
            {
                var requirementId = (string)requirement["designRequirementId"];
                var authority = (string)requirement["evidenceAuthority"];
                var objects = requirement["objects"] as JArray ?? new JArray();
                foreach (var item in objects.OfType<JObject>())
                {
                    var hierarchy = (string)item["hierarchyPath"] ?? "/";
                    var component = (string)item["componentType"] ?? "Unknown";
                    var assetPath = hierarchy.StartsWith("/Preview_", StringComparison.Ordinal) ? ScenePath : PrefabPath;
                    Assert.That((string)item["assetPath"], Is.EqualTo(assetPath), "Every implementation object identity must be pre-registered before C0 capture.");
                    Assert.That(component, Is.Not.EqualTo("UnityEngine.Light"), "Trace component identities must use the frozen runtime component token before C0 capture.");
                    Assert.That((string)item["componentInstanceId"], Is.EqualTo(assetPath + "#" + hierarchy + "#" + component), "Every implementation component instance must be pre-registered before C0 capture.");
                }
                if (lightSensitiveRequirements.Contains(requirementId))
                    Assert.That(objects.OfType<JObject>().Any(item => string.Equals((string)item["componentType"], "Light", StringComparison.Ordinal)), Is.True, "Light-sensitive requirements must pre-register the real Light object before C0 capture.");
                if (authority == "visualQa" || authority == "user")
                {
                    requirement["authorityEvidence"] = new JArray(new JObject { ["kind"] = authority, ["reference"] = "pending:" + authority, ["sha256"] = HashText("pending:" + authority), ["passed"] = false, ["detail"] = "No Visual QA or user verdict is asserted by formal capture." });
                    requirement["crossEvidence"] = new JArray(new JObject { ["kind"] = "beauty", ["reference"] = beautyReference, ["sha256"] = beautyHash, ["passed"] = true, ["detail"] = "A real retained Beauty frame is available to the independent authority, but is not a substitute for its Visual QA/user decision." });
                    continue;
                }

                var passed = RequirementPassed(requirementId, branches, facts, light);
                Assert.That(passed, Is.True, "Formal S0b machine requirement failed: " + requirementId);
                var isDiagnostic = authority == "diagnostic";
                var authorityReference = isDiagnostic ? evidenceRoot + "receiver-light-ab.json" : evidenceRoot + "semantic-telemetry.json";
                var authorityHash = isDiagnostic ? receiverAbHash : telemetryHash;
                var authorityDetail = RequirementDetail(requirementId);
                requirement["authorityEvidence"] = new JArray(new JObject { ["kind"] = authority, ["reference"] = authorityReference, ["sha256"] = authorityHash, ["passed"] = true, ["detail"] = authorityDetail });
                var cross = new JArray(new JObject { ["kind"] = isDiagnostic ? "telemetry" : "beauty", ["reference"] = isDiagnostic ? evidenceRoot + "semantic-telemetry.json" : beautyReference, ["sha256"] = isDiagnostic ? telemetryHash : beautyHash, ["passed"] = true, ["detail"] = isDiagnostic ? "Controller telemetry cross-checks the positive receiver-linear-luminance A/B diagnostic but is not a Visual QA or user decision." : "A retained Beauty frame from the authority camera is independent image evidence, not a reserialized telemetry summary or effect-only diagnostic pass." });
                if (lightSensitiveRequirements.Contains(requirementId) && !isDiagnostic)
                    cross.Add(new JObject { ["kind"] = "diagnostic", ["reference"] = evidenceRoot + "receiver-light-ab.json", ["sha256"] = receiverAbHash, ["passed"] = true, ["detail"] = "Independent receiver-linear-luminance A/B proves that the serialized UnityEngine.Light changes a separate Lit receiver while effect renderers remain hidden." });
                requirement["crossEvidence"] = cross;
            }
            return trace.ToString(Formatting.None);
        }

        private static bool RequirementPassed(string requirementId, List<BranchEvidence> branches, RuntimeFacts facts, ReceiverLightDiagnostic light)
        {
            var all = branches.Count == 3 && branches.All(value => value.ObservedFrames == 366 && value.RetainedFrames == RetainedFrames.Length && value.CapturedFrameIndices.SetEquals(RetainedFrames));
            var start = all && branches.All(value => value.FirstSteadyFrame > 0 && value.FirstSteadyFrame <= 21 && value.SteadyAtExitRequest);
            var stop = branches.Where(value => !value.Plan.Interrupt).ToArray();
            var interrupt = branches.Where(value => value.Plan.Interrupt).ToArray();
            switch (requirementId)
            {
                case "REQ-LIFECYCLE-START": return start;
                case "REQ-LIFECYCLE-STEADY": return start && branches.All(value => value.Frames.Any(frame => frame.Contains("\"frameIndex\":291,\"state\":\"steady\"")));
                case "REQ-LIFECYCLE-STOP": return stop.Length == 2 && stop.All(value => value.SawRequestedExit && value.SawExitCarrier && value.Final.State == SustainedEffectState.Idle && value.Final.CleanupComplete && value.Final.EnabledLightCount == 0 && value.LastLitExitFrame >= 292 && (value.LastLitExitFrame - 291) / 60f <= .8f + 1f / 60f);
                case "REQ-LIFECYCLE-INTERRUPT": return interrupt.Length == 1 && interrupt.All(value => value.SawRequestedExit && value.SawExitCarrier && value.Final.State == SustainedEffectState.Idle && value.Final.CleanupComplete && value.Final.EnabledLightCount == 0 && value.LastLitExitFrame >= 292 && (value.LastLitExitFrame - 291) / 60f <= .35f + 1f / 60f);
                case "REQ-LAYERS-INDEPENDENT": return facts.LayersIndependent;
                case "REQ-LIGHT-REAL": return facts.LightWithinContract && branches.All(value => value.PeakLightIntensity > 0f && value.PeakLightIntensity <= 1.25f);
                case "REQ-LIGHT-RECEIVER": return light.LightType == LightType.Point.ToString() && light.ShadowMode == LightShadows.None.ToString() && light.LightRange <= 2.45f && light.LightIntensity > 0f && light.LightIntensity <= 1.25f && light.OnLinearLuminance > light.OffLinearLuminance + .001f;
                case "REQ-BUDGET-S0B": return facts.BudgetWithinContract;
                default: throw new ArgumentOutOfRangeException("requirementId", requirementId, "S0b machine trace contains an unknown non-visual requirement.");
            }
        }

        private static string RequirementDetail(string requirementId)
        {
            switch (requirementId)
            {
                case "REQ-LIFECYCLE-START": return "Measured first steady frame for all three frozen seeds; each must be <= 21 at 60 FPS.";
                case "REQ-LIFECYCLE-STEADY": return "Measured steady telemetry at frame 291 for all three frozen seeds before the commanded exit.";
                case "REQ-LIFECYCLE-STOP": return "Measured both frozen stop branches, their exclusive StopTail carrier, bounded light fade, and idle cleanup.";
                case "REQ-LIFECYCLE-INTERRUPT": return "Measured the frozen interrupt branch, its exclusive InterruptBurst carrier, bounded light fade, and idle cleanup.";
                case "REQ-LAYERS-INDEPENDENT": return "Read back seven named deterministic Shuriken carriers and seven independent particle renderers.";
                case "REQ-LIGHT-REAL": return "Read back one bounded unshadowed Point light and observed positive runtime intensity across all seeds.";
                case "REQ-LIGHT-RECEIVER": return "Matched receiver-only linear A/B measures a positive luminance delta while all effect renderers are disabled.";
                case "REQ-BUDGET-S0B": return "Read back capacity, renderer, material, light, and runtime-texture structural limits from the Runtime Entry.";
                default: throw new ArgumentOutOfRangeException("requirementId", requirementId, null);
            }
        }

        private struct ReceiverLightDiagnostic
        {
            public float OffLinearLuminance;
            public float OnLinearLuminance;
            public string LightType;
            public float LightRange;
            public float LightIntensity;
            public string ShadowMode;
            public string RendererIsolation;
            public byte[] OffPng;
            public byte[] OnPng;
            public string OffArtifactHash;
            public string OnArtifactHash;
            public string SummaryArtifactHash;
            public string ToJson()
            {
                return "{\"schema\":\"w24-s0b-receiver-light-ab/v2\",\"receiver\":\"Preview_LightReceiverMarker\",\"measurement\":\"linear-luminance; 15x15 screen-space probe; all particle renderers hidden in both samples\",\"onlyChangedBetweenSamples\":\"UnityEngine.Light.enabled\",\"rendererIsolation\":\"" + Escape(RendererIsolation) + "\",\"offLinearLuminance\":" + Number(OffLinearLuminance) + ",\"onLinearLuminance\":" + Number(OnLinearLuminance) + ",\"delta\":" + Number(OnLinearLuminance - OffLinearLuminance) + ",\"light\":{\"type\":\"" + LightType + "\",\"range\":" + Number(LightRange) + ",\"intensity\":" + Number(LightIntensity) + ",\"shadows\":\"" + ShadowMode + "\"}}";
            }
        }

        private struct ReceiverProbe
        {
            public float Luminance;
            public byte[] Png;
        }
    }
}
