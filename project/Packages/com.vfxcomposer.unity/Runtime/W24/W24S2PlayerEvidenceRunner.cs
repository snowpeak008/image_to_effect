using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace VFXComposer.W24
{
    /// <summary>Shared, Player-safe wire contract for the dedicated S2 runtime probe.</summary>
    public static class W24S2PlayerEvidenceProtocol
    {
        public const string ActivationArgument = "-w24S2Evidence";
        public const string ForceProbeFailureArgument = "-w24S2ForceProbeFailure";
        public const string LegacyResultArgument = "-w24S2ResultPath";
        public const string ResultDirectoryName = "evidence";
        public const string ResultFileName = "player-result.json";
        public const string ResultRelativePath = ResultDirectoryName + "/" + ResultFileName;
        public const string ResultSchema = "w24-s2-player-runtime-evidence/v1";
        public const int SuccessExitCode = 0;
        public const int ProbeFailureExitCode = 24;
        public const int InvalidCommandExitCode = 25;
        // These are per-module-stage limits. A headless Player can execute tens of thousands
        // of real Update frames per second, so a suite-global frame budget is not a duration.
        public const int ProbeFrameLimit = 1000000;
        public const float ProbeRealtimeLimitSeconds = 10f;
    }

    /// <summary>
    /// Command-line-only Windows Player probe for the W24 S2 runtime foundation. It is inert
    /// in the Editor and unless its dedicated activation flag is present. The result location
    /// is never supplied by the caller: it is a frozen relative path under the executable's
    /// build root. This is runtime/protocol evidence only and cannot issue a visual/L3/L4 verdict.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class W24S2PlayerEvidenceRunner : MonoBehaviour
    {
        [Serializable]
        private sealed class ModuleResult
        {
            public string moduleId;
            public bool passed;
            public string detail;
        }

        [Serializable]
        private sealed class PlayerResult
        {
            public string schema;
            public bool passed;
            public int exitCode;
            public string unityVersion;
            public string runtimePlatform;
            public string graphicsDevice;
            public bool batchMode;
            public ModuleResult[] modules;
            public string failure;
        }

        private sealed class ProbeExecution
        {
            public readonly List<GameObject> Owned = new List<GameObject>();
            public readonly List<ModuleResult> Results = new List<ModuleResult>();
            public readonly List<W24SemanticTelemetry> Telemetry = new List<W24SemanticTelemetry>();
            public string ActiveStageId { get; private set; }

            public ProbeExecution()
            {
                ActiveStageId = "activation";
            }

            public void BeginStage(string stageId)
            {
                if (string.IsNullOrEmpty(stageId)) throw new ArgumentException("S2 probe stage id is required.", "stageId");
                ActiveStageId = stageId;
            }

            public GameObject Own(string name)
            {
                var value = new GameObject(name);
                Owned.Add(value);
                return value;
            }

            public void Add(string moduleId, bool passed, string detail)
            {
                Results.Add(Result(moduleId, passed, detail));
            }

            public void Cleanup()
            {
                Exception firstFailure = null;
                for (var index = Owned.Count - 1; index >= 0; index--)
                {
                    try
                    {
                        var value = Owned[index];
                        if (value == null) continue;
                        value.SetActive(false);
                        Destroy(value);
                    }
                    catch (Exception exception)
                    {
                        if (firstFailure == null) firstFailure = exception;
                    }
                }
                Owned.Clear();
                if (firstFailure != null) throw new InvalidOperationException("S2 Player probe cleanup failed.", firstFailure);
            }
        }

        private sealed class ProbeDriver : IDisposable
        {
            private readonly IEnumerator probe;
            private readonly ProbeExecution execution;
            private string deadlineStage;
            private int firstFrame;
            private float firstRealtime;

            public ProbeDriver(IEnumerator probe, ProbeExecution execution)
            {
                this.probe = probe;
                this.execution = execution;
                deadlineStage = execution.ActiveStageId;
                firstFrame = Time.frameCount;
                firstRealtime = Time.realtimeSinceStartup;
            }

            public object Current { get; private set; }
            public Exception Failure { get; private set; }

            public bool MoveNext()
            {
                if (Failure != null) return false;
                try
                {
                    ResetDeadlineForStageTransition();
                    EnsureWithinProbeDeadline(deadlineStage, firstFrame, firstRealtime);
                    var moved = probe.MoveNext();
                    // ExecuteProbe marks the new stage inside MoveNext. Capture that same frame
                    // as its deadline origin instead of waiting for the next coroutine drive.
                    ResetDeadlineForStageTransition();
                    if (!moved) return false;
                    Current = probe.Current;
                    return true;
                }
                catch (Exception exception)
                {
                    Failure = exception;
                    return false;
                }
            }

            private void ResetDeadlineForStageTransition()
            {
                var activeStage = execution.ActiveStageId;
                if (string.Equals(deadlineStage, activeStage, StringComparison.Ordinal)) return;
                deadlineStage = activeStage;
                firstFrame = Time.frameCount;
                firstRealtime = Time.realtimeSinceStartup;
            }

            public void Dispose()
            {
                try { W24S2PlayerEvidenceRunner.Dispose(probe); }
                catch (Exception exception)
                {
                    if (Failure == null) Failure = exception;
                    else Debug.LogError("W24 S2 secondary iterator cleanup failure: " + exception);
                }
            }
        }

        private IEnumerator Start()
        {
            if (Application.isEditor) yield break;

            // Activation itself crosses a real PlayerLoop. A synchronous Start-only result is
            // never accepted as Player runtime evidence.
            yield return null;

            var arguments = Environment.GetCommandLineArgs();
            if (CountArgument(arguments, W24S2PlayerEvidenceProtocol.ActivationArgument) == 0) yield break;

            bool forceProbeFailure;
            var commandError = ParseCommand(arguments, out forceProbeFailure);
            if (commandError != null)
            {
                Debug.LogError("W24 S2 Player evidence command rejected: " + commandError);
                Application.Quit(W24S2PlayerEvidenceProtocol.InvalidCommandExitCode);
                yield break;
            }

            string resultPath;
            try
            {
                resultPath = ResolveFixedResultPath();
            }
            catch (Exception exception)
            {
                Debug.LogError("W24 S2 Player evidence fixed result path was rejected: " + exception);
                Application.Quit(W24S2PlayerEvidenceProtocol.ProbeFailureExitCode);
                yield break;
            }

            var execution = new ProbeExecution();
            Exception probeFailure = null;
            string probeFailureStage = null;
            if (forceProbeFailure)
            {
                execution.BeginStage("forced_probe_failure");
                probeFailure = new InvalidOperationException("Injected S2 probe failure for the dedicated Development Player evidence test.");
                probeFailureStage = execution.ActiveStageId;
            }
            else
            {
                var driver = new ProbeDriver(ExecuteProbe(execution), execution);
                try
                {
                    while (driver.MoveNext()) yield return driver.Current;
                }
                finally { driver.Dispose(); }
                probeFailure = driver.Failure;
                if (probeFailure != null) probeFailureStage = execution.ActiveStageId;
            }

            // Destruction never occurs in the continuation that activated or last exercised a
            // module. This also gives exception paths a real PlayerLoop cleanup boundary.
            yield return null;
            try
            {
                execution.Cleanup();
            }
            catch (Exception cleanupFailure)
            {
                if (probeFailure == null)
                {
                    probeFailure = cleanupFailure;
                    probeFailureStage = "cleanup";
                }
                else Debug.LogError("W24 S2 secondary object cleanup failure: " + cleanupFailure);
            }

            var modules = execution.Results.ToArray();
            var passed = probeFailure == null && modules.Length == 6;
            for (var index = 0; index < modules.Length; index++) passed &= modules[index].passed;
            var result = NewResult(
                passed,
                passed ? W24S2PlayerEvidenceProtocol.SuccessExitCode : W24S2PlayerEvidenceProtocol.ProbeFailureExitCode,
                modules,
                probeFailure == null ? (passed ? string.Empty : "one or more S2 runtime modules failed") : DescribeProbeFailure(probeFailureStage, probeFailure));

            try
            {
                WriteResultOnce(resultPath, JsonUtility.ToJson(result, true));
            }
            catch (Exception exception)
            {
                Debug.LogError("W24 S2 Player evidence result could not be committed write-once: " + exception);
                Application.Quit(W24S2PlayerEvidenceProtocol.ProbeFailureExitCode);
                yield break;
            }

            yield return null;
            Application.Quit(result.exitCode);
        }

        private static IEnumerator ExecuteProbe(ProbeExecution execution)
        {
            execution.BeginStage("moving_emitter_trail");
            var stage = ProbeMovingEmitter(execution);
            try { while (stage.MoveNext()) yield return stage.Current; }
            finally { Dispose(stage); }

            execution.BeginStage("model_binding");
            stage = ProbeModelBinding(execution);
            try { while (stage.MoveNext()) yield return stage.Current; }
            finally { Dispose(stage); }

            execution.BeginStage("fragment_motion");
            stage = ProbeFragmentMotion(execution);
            try { while (stage.MoveNext()) yield return stage.Current; }
            finally { Dispose(stage); }

            execution.BeginStage("real_lighting");
            stage = ProbeRealLighting(execution);
            try { while (stage.MoveNext()) yield return stage.Current; }
            finally { Dispose(stage); }

            execution.BeginStage("semantic_state_machine");
            stage = ProbeSemanticTimeline(execution);
            try { while (stage.MoveNext()) yield return stage.Current; }
            finally { Dispose(stage); }

            execution.BeginStage("semantic_telemetry");
            stage = ProbeCommonTelemetry(execution);
            try { while (stage.MoveNext()) yield return stage.Current; }
            finally { Dispose(stage); }
        }

        private static IEnumerator ProbeMovingEmitter(ProbeExecution execution)
        {
            var firstFrame = Time.frameCount;
            var source = execution.Own("S2 Motion Source");
            var host = execution.Own("S2 Moving Emitter");
            var trail = host.AddComponent<TrailRenderer>();
            trail.time = 1f;
            trail.minVertexDistance = .001f;
            var module = host.AddComponent<W24MovingEmitterTrailProtocol>();
            module.SetMotionSource(source.transform);
            module.SetTrails(new[] { trail });
            module.Play(1101u);

            source.transform.position = Vector3.right;
            yield return null;
            var afterFirst = module.ReadEmitterHistory();
            source.transform.position = Vector3.right * 2f;
            yield return null;
            var afterSecond = module.ReadEmitterHistory();
            yield return null;
            RequireFrameAdvance(firstFrame, "moving_emitter_trail");

            var history = module.ReadEmitterHistory();
            var telemetry = module.ReadSemanticTelemetry();
            execution.Telemetry.Add(telemetry);
            var updateSampled = afterFirst.Count == 1 && afterSecond.Count == 2 && history.Count == 2 &&
                                history.Samples[0].Position == Vector3.right && history.Samples[1].Position == Vector3.right * 2f &&
                                history.Samples[0].Time > 0f && history.Samples[1].Time > history.Samples[0].Time;
            var passed = updateSampled && history.Seed == 1101u && module.SampleCount == 2 && !module.IsMoving && !trail.emitting &&
                         telemetry.Module == "moving_emitter_trail" && telemetry.State == W24SemanticState.Continuous;
            module.ResetForPool();
            passed &= module.ReadEmitterHistory().IsCleared && module.ReadSemanticTelemetry().CleanupComplete;
            execution.Add("moving_emitter_trail", passed, "Update sampled two moving world-space heads across three PlayerLoop frames; the stationary frame added none; reset cleared history");
        }

        private static IEnumerator ProbeModelBinding(ProbeExecution execution)
        {
            var firstFrame = Time.frameCount;
            var model = execution.Own("S2 Model");
            var host = execution.Own("S2 Binding Adapter");
            var module = host.AddComponent<W24ModelBindingAdapter>();
            var bound = module.Bind(model.transform);
            yield return null;
            RequireFrameAdvance(firstFrame, "model_binding");

            var telemetry = module.ReadSemanticTelemetry();
            var negative = W24BindingDiagnosticProbes.Run(model.transform);
            execution.Telemetry.Add(telemetry);
            var passed = bound && module.Result.IsBound && module.Result.Anchor == model.transform &&
                         telemetry.Module == "model_binding" && telemetry.State == W24SemanticState.Continuous && negative.Passed;
            module.ResetForPool();
            passed &= module.ReadSemanticTelemetry().CleanupComplete;
            execution.Add("model_binding", passed, "binding remained attached across a real PlayerLoop frame; all four no-fallback fault probes passed; reset detached it");
        }

        private static IEnumerator ProbeFragmentMotion(ProbeExecution execution)
        {
            var firstFrame = Time.frameCount;
            var root = execution.Own("S2 Fragments");
            var first = execution.Own("Fragment A");
            var second = execution.Own("Fragment B");
            first.transform.SetParent(root.transform, false);
            second.transform.SetParent(root.transform, false);
            second.transform.localPosition = Vector3.right;
            var module = root.AddComponent<W24FragmentMotionSystem>();
            module.SetFragments(new[] { first.transform, second.transform });
            module.Play(2202u);
            var beforeFirst = first.transform.localPosition;
            var beforeSecond = second.transform.localPosition;

            var observedMotion = false;
            while (module.ReadSemanticTelemetry().LastEventId != "fragment_complete")
            {
                yield return null;
                var firstDelta = first.transform.localPosition - beforeFirst;
                var secondDelta = second.transform.localPosition - beforeSecond;
                observedMotion |= firstDelta.sqrMagnitude > .000001f && secondDelta.sqrMagnitude > .000001f && firstDelta != secondDelta;
            }
            RequireFrameAdvance(firstFrame, "fragment_motion");

            var telemetry = module.ReadSemanticTelemetry();
            execution.Telemetry.Add(telemetry);
            var passed = observedMotion && telemetry.Module == "fragment_motion" && telemetry.Seed == 2202u &&
                         telemetry.LastEventId == "fragment_complete" && telemetry.CleanupComplete;
            module.ResetForPool();
            passed &= !first.activeSelf && !second.activeSelf;
            execution.Add("fragment_motion", passed, "two seed-derived fragments advanced only through Update across real frames and reached bounded completion independently");
        }

        private static IEnumerator ProbeRealLighting(ProbeExecution execution)
        {
            var firstFrame = Time.frameCount;
            var root = execution.Own("S2 Lights");
            var old = execution.Own("S2 Old Light").AddComponent<Light>();
            var replacementA = execution.Own("S2 Replacement Light A").AddComponent<Light>();
            var replacementB = execution.Own("S2 Replacement Light B").AddComponent<Light>();
            old.transform.SetParent(root.transform, false);
            replacementA.transform.SetParent(root.transform, false);
            replacementB.transform.SetParent(root.transform, false);
            var module = root.AddComponent<W24RealLightingModule>();

            module.Configure3DLights(new[] { null, old, old }, 2);
            module.SetLights(true, 99f);
            yield return null;
            var duplicateCountedOnce = old.enabled && module.ReadSemanticTelemetry().ActiveItemCount == 1;

            module.Configure3DLights(new[] { replacementA, null, replacementA, replacementB }, 2);
            var cleared = module.ReadSemanticTelemetry();
            var oldDisabledOnReplace = !old.enabled && !replacementA.enabled && !replacementB.enabled &&
                                       cleared.State == W24SemanticState.Idle && cleared.ActiveItemCount == 0 && cleared.CleanupComplete;
            module.SetLights(true, 99f);
            yield return null;
            RequireFrameAdvance(firstFrame, "real_lighting");

            var telemetry = module.ReadSemanticTelemetry();
            execution.Telemetry.Add(telemetry);
            var passed = duplicateCountedOnce && oldDisabledOnReplace && !old.enabled && replacementA.enabled && replacementB.enabled &&
                         replacementA.intensity <= 2.0001f && replacementB.intensity <= 2.0001f &&
                         telemetry.Module == "real_lighting" && telemetry.State == W24SemanticState.Continuous && telemetry.ActiveItemCount == 2;
            module.ResetForPool();
            passed &= !old.enabled && !replacementA.enabled && !replacementB.enabled && module.ReadSemanticTelemetry().CleanupComplete;
            execution.Add("real_lighting", passed, "null and duplicate slots were de-duplicated by Light identity; replacement immediately disabled old and new sets; replacement lights crossed a frame within budget");
        }

        private static IEnumerator ProbeSemanticTimeline(ProbeExecution execution)
        {
            var firstFrame = Time.frameCount;
            var host = execution.Own("S2 Semantic Timeline");
            var module = host.AddComponent<W24SemanticTimeline>();
            module.Send(W24TimelineCommand.Continuous);
            yield return null;
            var continuous = module.ReadSemanticTelemetry();
            module.Send(W24TimelineCommand.Interrupt);
            yield return null;
            var interrupted = module.ReadSemanticTelemetry();

            module.ResetForPool();
            module.Send(W24TimelineCommand.Impulse);
            while (module.State != W24SemanticState.Completed) yield return null;
            RequireFrameAdvance(firstFrame, "semantic_state_machine");

            var completed = module.ReadSemanticTelemetry();
            execution.Telemetry.Add(completed);
            var passed = continuous.State == W24SemanticState.Continuous && interrupted.State == W24SemanticState.Interrupted &&
                         interrupted.LastEventId == "interrupted" && completed.State == W24SemanticState.Completed && completed.LastEventId == "completed" && completed.Elapsed > 0f;
            execution.Add("semantic_state_machine", passed, "continuous and interrupt each crossed a PlayerLoop frame; impulse completion was reached only by real Update frames");
        }

        private static IEnumerator ProbeCommonTelemetry(ProbeExecution execution)
        {
            var firstFrame = Time.frameCount;
            yield return null;
            RequireFrameAdvance(firstFrame, "semantic_telemetry");

            var expected = new[] { "moving_emitter_trail", "model_binding", "fragment_motion", "real_lighting", "semantic_timeline" };
            var passed = execution.Telemetry.Count == expected.Length;
            for (var index = 0; passed && index < expected.Length; index++)
            {
                var snapshot = execution.Telemetry[index];
                passed &= string.Equals(snapshot.Module, expected[index], StringComparison.Ordinal);
                passed &= snapshot.EventSerial > 0;
                passed &= snapshot.State != W24SemanticState.Faulted;
                passed &= string.IsNullOrEmpty(snapshot.FaultCode) || snapshot.FaultCode == W24BindingFault.None.ToString();
            }
            execution.Add("semantic_telemetry", passed, "the five stable module/state/seed/event/cleanup/fault snapshots were consumed in frozen order after another PlayerLoop frame");
        }

        private static void EnsureWithinProbeDeadline(string stage, int firstFrame, float firstRealtime)
        {
            var elapsedFrames = Time.frameCount - firstFrame;
            var elapsedRealtime = Time.realtimeSinceStartup - firstRealtime;
            if (elapsedFrames > W24S2PlayerEvidenceProtocol.ProbeFrameLimit)
                throw new TimeoutException("S2 Player probe stage '" + stage + "' exceeded " + W24S2PlayerEvidenceProtocol.ProbeFrameLimit +
                                           " PlayerLoop frames (elapsedRealtimeSeconds=" + elapsedRealtime.ToString("R", CultureInfo.InvariantCulture) + ").");
            if (elapsedRealtime > W24S2PlayerEvidenceProtocol.ProbeRealtimeLimitSeconds)
                throw new TimeoutException("S2 Player probe stage '" + stage + "' exceeded " + W24S2PlayerEvidenceProtocol.ProbeRealtimeLimitSeconds.ToString("R", CultureInfo.InvariantCulture) +
                                           " realtime seconds (elapsedFrames=" + elapsedFrames.ToString(CultureInfo.InvariantCulture) + ").");
        }

        private static string DescribeProbeFailure(string stage, Exception failure)
        {
            return "stage=" + (string.IsNullOrEmpty(stage) ? "unknown" : stage) + "\n" + failure;
        }

        private static void RequireFrameAdvance(int firstFrame, string stage)
        {
            if (Time.frameCount <= firstFrame) throw new InvalidOperationException(stage + " did not cross a real PlayerLoop frame.");
        }

        private static void Dispose(IEnumerator iterator)
        {
            var disposable = iterator as IDisposable;
            if (disposable != null) disposable.Dispose();
        }

        private static ModuleResult Result(string moduleId, bool passed, string detail)
        {
            return new ModuleResult { moduleId = moduleId, passed = passed, detail = detail };
        }

        private static PlayerResult NewResult(bool passed, int exitCode, ModuleResult[] modules, string failure)
        {
            return new PlayerResult
            {
                schema = W24S2PlayerEvidenceProtocol.ResultSchema,
                passed = passed,
                exitCode = exitCode,
                unityVersion = Application.unityVersion,
                runtimePlatform = Application.platform.ToString(),
                graphicsDevice = SystemInfo.graphicsDeviceType.ToString(),
                batchMode = Application.isBatchMode,
                modules = modules ?? new ModuleResult[0],
                failure = failure ?? string.Empty
            };
        }

        private static int CountArgument(string[] arguments, string argument)
        {
            var count = 0;
            if (arguments != null) for (var index = 0; index < arguments.Length; index++)
                if (string.Equals(arguments[index], argument, StringComparison.Ordinal)) count++;
            return count;
        }

        private static string ParseCommand(string[] arguments, out bool forceProbeFailure)
        {
            forceProbeFailure = false;
            if (CountArgument(arguments, W24S2PlayerEvidenceProtocol.ActivationArgument) != 1)
                return W24S2PlayerEvidenceProtocol.ActivationArgument + " must occur exactly once";
            if (CountArgument(arguments, W24S2PlayerEvidenceProtocol.LegacyResultArgument) != 0)
                return W24S2PlayerEvidenceProtocol.LegacyResultArgument + " is forbidden; the result path is fixed under the executable build root";

            var forced = CountArgument(arguments, W24S2PlayerEvidenceProtocol.ForceProbeFailureArgument);
            if (forced > 1) return W24S2PlayerEvidenceProtocol.ForceProbeFailureArgument + " may occur at most once";
            if (forced == 1 && !Debug.isDebugBuild) return "S2 failure injection is restricted to a Development Player";
            for (var index = 0; arguments != null && index < arguments.Length; index++)
            {
                if (!string.Equals(arguments[index], W24S2PlayerEvidenceProtocol.ActivationArgument, StringComparison.Ordinal) &&
                    !string.Equals(arguments[index], W24S2PlayerEvidenceProtocol.ForceProbeFailureArgument, StringComparison.Ordinal)) continue;
                if (index + 1 < arguments.Length && !arguments[index + 1].StartsWith("-", StringComparison.Ordinal))
                    return arguments[index] + " is a valueless flag";
            }
            forceProbeFailure = forced == 1;
            return null;
        }

        private static string ResolveFixedResultPath()
        {
            var dataPath = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            EnsureNoReparsePoints(dataPath, false);
            var parent = Directory.GetParent(dataPath);
            if (parent == null) throw new InvalidOperationException("Player data path has no executable build root.");
            var buildRoot = Path.GetFullPath(parent.FullName).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            EnsureNoReparsePoints(buildRoot, false);
            var result = Path.GetFullPath(Path.Combine(buildRoot, W24S2PlayerEvidenceProtocol.ResultDirectoryName, W24S2PlayerEvidenceProtocol.ResultFileName));
            var expected = buildRoot + Path.DirectorySeparatorChar + W24S2PlayerEvidenceProtocol.ResultDirectoryName + Path.DirectorySeparatorChar + W24S2PlayerEvidenceProtocol.ResultFileName;
            if (!string.Equals(result, expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Fixed result path canonicalization changed its frozen path segments.");
            EnsureNoReparsePoints(result, true);
            return result;
        }

        private static void WriteResultOnce(string path, string json)
        {
            var absolute = Path.GetFullPath(path);
            var parent = Path.GetDirectoryName(absolute);
            if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException("Result path has no parent directory.");
            EnsureNoReparsePoints(parent, true);
            Directory.CreateDirectory(parent);
            EnsureNoReparsePoints(parent, false);
            EnsureExistingEvidenceEntriesAreNotReparsePoints(parent, absolute);

            if (File.Exists(absolute) || Directory.Exists(absolute)) throw new IOException("Write-once final result already exists.");
            var pending = absolute + "." + Guid.NewGuid().ToString("N") + ".pending";
            if (!string.Equals(Path.GetPathRoot(absolute), Path.GetPathRoot(pending), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Pending and final result paths must be on the same volume.");

            var ownsPending = false;
            try
            {
                var bytes = new UTF8Encoding(false, true).GetBytes((json ?? string.Empty) + "\n");
                using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    ownsPending = true;
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                EnsureNoReparsePoints(pending, false);
                if (File.Exists(absolute) || Directory.Exists(absolute)) throw new IOException("Write-once final result appeared before commit.");
                File.Move(pending, absolute);
                ownsPending = false;
                EnsureNoReparsePoints(absolute, false);
            }
            catch
            {
                if (ownsPending && File.Exists(pending))
                {
                    EnsureNoReparsePoints(pending, false);
                    File.Delete(pending);
                }
                throw;
            }
        }

        private static void EnsureExistingEvidenceEntriesAreNotReparsePoints(string parent, string final)
        {
            if (File.Exists(final) || Directory.Exists(final)) EnsureNoReparsePoints(final, false);
            var pattern = Path.GetFileName(final) + "*.pending";
            foreach (var pending in Directory.GetFileSystemEntries(parent, pattern, SearchOption.TopDirectoryOnly))
                EnsureNoReparsePoints(pending, false);
        }

        private static void EnsureNoReparsePoints(string path, bool allowMissingTail)
        {
            var absolute = Path.GetFullPath(path);
            var root = Path.GetPathRoot(absolute);
            if (string.IsNullOrEmpty(root)) throw new InvalidOperationException("Path has no rooted volume: " + absolute);
            var current = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Reparse-point volume root is forbidden: " + current);
            var relative = absolute.Substring(root.Length);
            var segments = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            var missing = false;
            for (var index = 0; index < segments.Length; index++)
            {
                current = Path.Combine(current, segments[index]);
                var exists = Directory.Exists(current) || File.Exists(current);
                if (!exists)
                {
                    missing = true;
                    continue;
                }
                if (missing) throw new IOException("An existing path component followed a missing component: " + current);
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Reparse-point path component is forbidden: " + current);
            }
            if (!allowMissingTail && missing) throw new DirectoryNotFoundException("Required path does not exist: " + absolute);
        }
    }
}
