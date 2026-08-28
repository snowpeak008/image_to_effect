using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace VFXComposer.W24
{
    /// <summary>
    /// S0a's synchronous graphics recorder. It only renders the serialized authority Camera;
    /// the diagnostic effect-only buffer temporarily narrows that same Camera's culling mask.
    /// It does not advance simulation, choose frames, or make any visual quality judgement.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class W24ContinuousCaptureRecorder : MonoBehaviour
    {
        [SerializeField] private Camera authorityCamera;
        [SerializeField] private LayerMask diagnosticEffectLayers;
        private W24EvidenceStore store;
        private W24CaptureProfile profile;
        private W24CaptureSourceHashes sources;
        private string candidateId;
        private readonly List<string> frameRecords = new List<string>();
        private readonly List<string> supplementalDiagnosticRecords = new List<string>();
        // Typed raw passes are deliberately separate from the legacy supplemental list.  A
        // summary JSON has a hash too, but it must never be able to impersonate an Object-ID,
        // depth, trail-mask, or metric-result pass at the S5 boundary.
        private readonly List<string> typedRawDiagnosticRecords = new List<string>();
        private readonly List<string> metricInputRecords = new List<string>();
        private readonly List<string> metricReportRecords = new List<string>();
        private readonly Dictionary<string, string> diagnosticPassEncodings = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> typedDiagnosticPaths = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> semanticTelemetryRecords = new List<string>();
        private readonly HashSet<string> capturedSeedFrameKeys = new HashSet<string>(StringComparer.Ordinal);
        private bool active;
        private bool formalObservedFramesOnly;
        private string operatorCommandHash;
        private int latestPlayerLoopSerial;
        private int consumedPlayerLoopSerial;
        private int latestPlayerLoopFrame;
        private float latestPlayerLoopTime;
        private bool missedPlayerLoopToken;
        private bool timingSnapshotTaken;
        private int previousCaptureFramerate;
        private float previousCaptureDeltaTime;
        private int previousTargetFramerate;

        public Camera AuthorityCamera { get { return authorityCamera; } set { authorityCamera = value; } }
        public LayerMask DiagnosticEffectLayers { get { return diagnosticEffectLayers; } set { diagnosticEffectLayers = value; } }
        public bool IsActive { get { return active; } }
        /// <summary>Absolute private root of the active recorder store; exposed only for the controlled metrics CLI bridge.</summary>
        public string EvidenceRoot { get { if (!active || store == null) throw new InvalidOperationException("W24 recorder evidence root is unavailable outside active capture."); return store.Root; } }
        /// <summary>Immutable capture candidate identity available to the controlled metrics bridge.</summary>
        public string CandidateId { get { if (!active) throw new InvalidOperationException("W24 recorder has not begun."); return candidateId; } }
        /// <summary>Frozen capture-profile identity available to the controlled metrics bridge.</summary>
        public string CaptureProfileSha256 { get { if (!active || profile == null) throw new InvalidOperationException("W24 recorder has not begun."); return profile.Sha256; } }
        /// <summary>Observed after normal Update; a scene-specific driver supplies its real state name and invokes CaptureFrame.</summary>
        public event Action<int, float> AfterPlayerLoopFrame;

        /// <summary>
        /// An opaque observation made by this Recorder's real LateUpdate. It cannot be created by
        /// a fixture driver and is consumed exactly once by formal capture.
        /// </summary>
        public struct CompletedPlayerLoopToken
        {
            internal W24ContinuousCaptureRecorder Owner;
            internal int Serial;
            internal int Frame;
            internal float Time;
        }

        /// <param name="allowNonBatchModeForTest">Only automated graphics tests may set this true. Formal evidence always uses the four-argument overload and therefore requires graphics-backed batchmode.</param>
        public void Begin(string evidenceDirectory, string captureCandidateId, W24CaptureProfile captureProfile, W24CaptureSourceHashes sourceHashes, bool allowNonBatchModeForTest = false)
        {
            BeginInternal(evidenceDirectory, captureCandidateId, captureProfile, sourceHashes, null, allowNonBatchModeForTest);
        }

        /// <summary>Formal S0a entry point: all retained captures must derive from real LateUpdate observations.</summary>
        public void BeginFormal(string evidenceDirectory, string captureCandidateId, W24CaptureProfile captureProfile, W24CaptureSourceHashes sourceHashes, string canonicalOperatorCommandHash)
        {
            if (!W24CaptureProfile.IsCanonicalSha256(canonicalOperatorCommandHash)) throw new ArgumentException("Formal W24 capture requires the canonical operator-command hash.", "canonicalOperatorCommandHash");
            BeginInternal(evidenceDirectory, captureCandidateId, captureProfile, sourceHashes, canonicalOperatorCommandHash, false);
        }

        /// <summary>
        /// Test seam for exercising the formal token protocol on an interactive graphics test
        /// runner. Production evidence must use <see cref="BeginFormal"/>, which remains
        /// batch-only; this overload is deliberately named so it cannot be mistaken for a
        /// capture entry point in authoring code.
        /// </summary>
        public void BeginFormalForGraphicsTest(string evidenceDirectory, string captureCandidateId, W24CaptureProfile captureProfile, W24CaptureSourceHashes sourceHashes, string canonicalOperatorCommandHash)
        {
            if (!W24CaptureProfile.IsCanonicalSha256(canonicalOperatorCommandHash)) throw new ArgumentException("Formal W24 capture requires the canonical operator-command hash.", "canonicalOperatorCommandHash");
            BeginInternal(evidenceDirectory, captureCandidateId, captureProfile, sourceHashes, canonicalOperatorCommandHash, true);
        }

        private void BeginInternal(string evidenceDirectory, string captureCandidateId, W24CaptureProfile captureProfile, W24CaptureSourceHashes sourceHashes, string commandHash, bool allowNonBatchModeForTest)
        {
            if (active) throw new InvalidOperationException("W24 recorder is already capturing.");
            if (allowNonBatchModeForTest) RequireGraphicsDevice(); else RequireGraphicsBatchmode();
            if (authorityCamera == null) throw new InvalidOperationException("W24 recorder requires the formal scene's serialized authority Camera.");
            if (diagnosticEffectLayers.value == 0) throw new InvalidOperationException("W24 recorder requires a non-empty diagnostic effect LayerMask.");
            captureProfile.Validate(); sourceHashes.Validate();
            if (!string.Equals(captureProfile.UnityVersion, Application.unityVersion, StringComparison.Ordinal)) throw new InvalidOperationException("W24 Capture Profile Unity version does not match the active Unity process.");
            if (!string.Equals(captureProfile.GraphicsApi, SystemInfo.graphicsDeviceType.ToString(), StringComparison.Ordinal) || !string.Equals(captureProfile.GraphicsDevice, SystemInfo.graphicsDeviceName, StringComparison.Ordinal) || !string.Equals(captureProfile.GraphicsDriverVersion, SystemInfo.graphicsDeviceVersion, StringComparison.Ordinal) || !string.Equals(captureProfile.ColorSpace, QualitySettings.activeColorSpace.ToString(), StringComparison.Ordinal)) throw new InvalidOperationException("W24 Capture Profile graphics device/API/driver/color space does not match the active rendering environment.");
            if (!string.Equals(captureProfile.RenderTextureFormat, RenderTextureFormat.ARGB32.ToString(), StringComparison.Ordinal)) throw new InvalidOperationException("W24 Capture Profile RenderTexture format does not match the recorder's actual ARGB32 format.");
            if (captureProfile.Hdr != authorityCamera.allowHDR || captureProfile.Msaa != authorityCamera.allowMSAA) throw new InvalidOperationException("W24 Capture Profile HDR/MSAA does not match the serialized authority Camera.");
            if (Mathf.Max(Mathf.Abs(captureProfile.Background.r - authorityCamera.backgroundColor.r), Mathf.Abs(captureProfile.Background.g - authorityCamera.backgroundColor.g), Mathf.Abs(captureProfile.Background.b - authorityCamera.backgroundColor.b), Mathf.Abs(captureProfile.Background.a - authorityCamera.backgroundColor.a)) > .0001f) throw new InvalidOperationException("W24 Capture Profile background does not match the serialized authority Camera.");
            profile = captureProfile; sources = sourceHashes; candidateId = captureCandidateId; operatorCommandHash = commandHash;
            formalObservedFramesOnly = commandHash != null;
            store = W24EvidenceStore.Create(evidenceDirectory, candidateId, profile.Sha256);
            previousCaptureFramerate = Time.captureFramerate; previousCaptureDeltaTime = Time.captureDeltaTime; previousTargetFramerate = Application.targetFrameRate; timingSnapshotTaken = true;
            Time.captureFramerate = profile.FramesPerSecond; Time.captureDeltaTime = 1f / profile.FramesPerSecond; Application.targetFrameRate = profile.FramesPerSecond;
            frameRecords.Clear(); supplementalDiagnosticRecords.Clear(); semanticTelemetryRecords.Clear(); capturedSeedFrameKeys.Clear();
            typedRawDiagnosticRecords.Clear(); metricInputRecords.Clear(); metricReportRecords.Clear(); diagnosticPassEncodings.Clear(); typedDiagnosticPaths.Clear();
            latestPlayerLoopSerial = 0; consumedPlayerLoopSerial = 0; latestPlayerLoopFrame = -1; latestPlayerLoopTime = 0f; missedPlayerLoopToken = false;
            active = true;
        }

        /// <summary>Compatibility overload: negative Int32 values preserve their UInt32 seed bits in evidence metadata.</summary>
        public void CaptureFrame(int frameIndex, float simulationTime, string stateName, int seed)
        {
            CaptureFrame(frameIndex, simulationTime, stateName, unchecked((uint)seed));
        }

        /// <summary>Captures the exact UInt32 seed emitted by the S0a operator command.</summary>
        public void CaptureFrame(int frameIndex, float simulationTime, string stateName, uint seed)
        {
            if (formalObservedFramesOnly) throw new InvalidOperationException("Formal W24 capture must use CaptureObservedPlayerLoopFrame; direct frame metadata is forbidden.");
            CaptureFrameCore(frameIndex, simulationTime, stateName, seed);
        }

        /// <summary>Captures a retained formal frame from one, and only one, real LateUpdate token.</summary>
        public void CaptureObservedPlayerLoopFrame(CompletedPlayerLoopToken token, int logicalFrameIndex, string stateName, uint seed)
        {
            if (!formalObservedFramesOnly) throw new InvalidOperationException("Observed-player-loop capture is reserved for formal W24 evidence.");
            ConsumeObservedPlayerLoopToken(token);
            CaptureFrameCore(logicalFrameIndex, token.Time, stateName, seed);
        }

        /// <summary>
        /// Consumes a real LateUpdate observation that is intentionally not a retained image.
        /// Formal capture must acknowledge every normal PlayerLoop frame; otherwise a caller
        /// could select only convenient frames and falsely claim a continuous natural playback.
        /// </summary>
        public void AcknowledgeObservedPlayerLoopFrame(CompletedPlayerLoopToken token)
        {
            if (!formalObservedFramesOnly) throw new InvalidOperationException("Observed-player-loop acknowledgement is reserved for formal W24 evidence.");
            ConsumeObservedPlayerLoopToken(token);
        }

        private void ConsumeObservedPlayerLoopToken(CompletedPlayerLoopToken token)
        {
            ValidateObservedPlayerLoopToken(token);
            consumedPlayerLoopSerial = token.Serial;
        }

        private void ValidateObservedPlayerLoopToken(CompletedPlayerLoopToken token)
        {
            if (token.Owner != this || token.Serial <= 0 || token.Serial != latestPlayerLoopSerial || token.Serial != consumedPlayerLoopSerial + 1 || token.Frame != latestPlayerLoopFrame || Mathf.Abs(token.Time - latestPlayerLoopTime) > .0001f)
                throw new InvalidOperationException("Formal W24 capture token is absent, stale, foreign, duplicated, or out of order.");
            if (missedPlayerLoopToken) throw new InvalidOperationException("Formal W24 capture missed one or more LateUpdate observations; evidence cannot skip PlayerLoop frames.");
        }

        /// <summary>Returns the latest unconsumed LateUpdate observation for formal fixture code.</summary>
        public CompletedPlayerLoopToken ConsumeCompletedPlayerLoopToken()
        {
            if (!active || !formalObservedFramesOnly) throw new InvalidOperationException("No formal observed-player-loop capture is active.");
            if (missedPlayerLoopToken || latestPlayerLoopSerial == 0 || latestPlayerLoopSerial != consumedPlayerLoopSerial + 1)
                throw new InvalidOperationException("A fresh, unconsumed LateUpdate observation is required before formal capture.");
            return new CompletedPlayerLoopToken { Owner = this, Serial = latestPlayerLoopSerial, Frame = latestPlayerLoopFrame, Time = latestPlayerLoopTime };
        }

        private void CaptureFrameCore(int frameIndex, float simulationTime, string stateName, uint seed)
        {
            if (!active) throw new InvalidOperationException("W24 recorder has not begun.");
            if (!profile.ContainsSeed(seed)) throw new InvalidOperationException("W24 recorder seed is not one of the frozen Capture Profile seeds: " + seed);
            if (!profile.IsRetainedFrameIndex(frameIndex)) throw new InvalidOperationException("W24 recorder only retains entries listed in the frozen Capture Profile frame table: " + frameIndex);
            if (frameIndex < 0 || !W24CaptureProfile.IsFinite(simulationTime) || simulationTime < 0f || string.IsNullOrEmpty(stateName)) throw new ArgumentException("W24 frame metadata requires a retained non-negative frame, finite non-negative simulation time, and state name.");
            var frameKey = seed.ToString(CultureInfo.InvariantCulture) + ":" + frameIndex.ToString(CultureInfo.InvariantCulture);
            if (!capturedSeedFrameKeys.Add(frameKey)) throw new InvalidOperationException("W24 evidence permits one retained artifact per seed/frame pair: " + frameKey);
            var baseName = "frames/seed_" + seed.ToString(CultureInfo.InvariantCulture) + "/frame_" + frameIndex.ToString("D5", CultureInfo.InvariantCulture);
            try
            {
                var beauty = CaptureBeauty(baseName + "_beauty.png");
                var diagnostic = CaptureEffectOnly(baseName + "_effect-only.png");
                frameRecords.Add("{\"frameIndex\":" + frameIndex + ",\"simulationTime\":" + W24CaptureProfile.Number(simulationTime) + ",\"state\":\"" + W24CaptureProfile.Escape(stateName) + "\",\"seed\":" + seed + ",\"beauty\":{\"file\":\"" + beauty.File + "\",\"sha256\":\"" + beauty.Hash + "\"},\"diagnostics\":[{\"passId\":\"effect-only-rgba\",\"file\":\"" + diagnostic.File + "\",\"sha256\":\"" + diagnostic.Hash + "\",\"foregroundPixels\":" + diagnostic.ForegroundPixels + ",\"method\":\"same-serialized-camera; transparent clear; frozen effect LayerMask; RGB-or-alpha nonzero foreground\"}]}" );
            }
            catch
            {
                active = false; RestoreTimingWithoutMaskingCaptureFailure(); throw;
            }
        }

        /// <summary>
        /// Stores implementation/semantic facts separately from diagnostic images. The recorder
        /// never interprets its contents as a visual result, label, route, or S0a terminal state.
        /// </summary>
        public string WriteSemanticTelemetry(string relativePath, byte[] bytes, string description)
        {
            if (!active) throw new InvalidOperationException("W24 recorder has not begun.");
            if (string.IsNullOrEmpty(relativePath) || !relativePath.StartsWith("diagnostics/", StringComparison.Ordinal) || !relativePath.EndsWith(".json", StringComparison.Ordinal) || bytes == null || bytes.Length == 0 || string.IsNullOrEmpty(description))
                throw new ArgumentException("W24 semantic telemetry must be a non-empty JSON artifact below diagnostics/ with a description.");
            var hash = store.WriteBytes(relativePath, bytes);
            semanticTelemetryRecords.Add("{\"kind\":\"semantic-telemetry\",\"description\":\"" + W24CaptureProfile.Escape(description) + "\",\"file\":\"" + W24CaptureProfile.Escape(relativePath.Replace('\\', '/')) + "\",\"sha256\":\"" + hash + "\"}");
            return hash;
        }

        /// <summary>
        /// Records a diagnostic or telemetry artifact generated by the same formal capture driver.
        /// It is deliberately unavailable before Begin/after Complete and every byte is stored by
        /// the same write-once evidence store as Beauty and effect-only frames. This is for
        /// measurements such as a receiver-light A/B probe; it is not a path for user screenshots.
        /// </summary>
        public string WriteSupplementalDiagnostic(string relativePath, byte[] bytes, string artifactKind, string description)
        {
            if (!active) throw new InvalidOperationException("W24 recorder has not begun.");
            if (string.IsNullOrEmpty(relativePath) || !relativePath.StartsWith("diagnostics/", StringComparison.Ordinal) || string.IsNullOrEmpty(artifactKind) || string.IsNullOrEmpty(description))
                throw new ArgumentException("W24 supplemental diagnostics must use a diagnostics/ relative path with kind and description.");
            var hash = store.WriteBytes(relativePath, bytes);
            supplementalDiagnosticRecords.Add("{\"kind\":\"" + W24CaptureProfile.Escape(artifactKind) + "\",\"description\":\"" + W24CaptureProfile.Escape(description) + "\",\"file\":\"" + W24CaptureProfile.Escape(relativePath.Replace('\\', '/')) + "\",\"sha256\":\"" + hash + "\"}");
            return hash;
        }

        /// <summary>
        /// Records a supplemental diagnostic whose authority depends on a specific natural
        /// PlayerLoop observation (for example, a receiver A/B readback).  This verifies but
        /// deliberately does not consume the token: the caller must still capture or acknowledge
        /// that exact observation before another LateUpdate can occur.
        /// </summary>
        public string WriteObservedSupplementalDiagnostic(CompletedPlayerLoopToken token, int logicalFrameIndex, uint seed, string relativePath, byte[] bytes, string artifactKind, string description)
        {
            if (!formalObservedFramesOnly) throw new InvalidOperationException("Observed supplemental diagnostics are reserved for formal W24 evidence.");
            if (!profile.ContainsSeed(seed) || logicalFrameIndex < 0) throw new ArgumentException("Observed supplemental diagnostic must use a frozen seed and non-negative logical frame.");
            ValidateObservedPlayerLoopToken(token);
            if (!active) throw new InvalidOperationException("W24 recorder has not begun.");
            if (string.IsNullOrEmpty(relativePath) || !relativePath.StartsWith("diagnostics/", StringComparison.Ordinal) || string.IsNullOrEmpty(artifactKind) || string.IsNullOrEmpty(description))
                throw new ArgumentException("Observed supplemental diagnostics must use a diagnostics/ relative path with kind and description.");
            var hash = store.WriteBytes(relativePath, bytes);
            supplementalDiagnosticRecords.Add("{\"kind\":\"" + W24CaptureProfile.Escape(artifactKind) + "\",\"description\":\"" + W24CaptureProfile.Escape(description) + "\",\"file\":\"" + W24CaptureProfile.Escape(relativePath.Replace('\\', '/')) + "\",\"sha256\":\"" + hash + "\",\"observedPlayerLoop\":{\"serial\":" + token.Serial + ",\"frame\":" + token.Frame + ",\"time\":" + W24CaptureProfile.Number(token.Time) + ",\"logicalFrameIndex\":" + logicalFrameIndex + ",\"seed\":" + seed + "}}");
            return hash;
        }

        /// <summary>
        /// Stores a lossless, machine-readable raw diagnostic generated from one real
        /// LateUpdate observation.  The declaration is intentionally more specific than the
        /// legacy supplemental API: S5 later binds passId/encoding/token provenance rather than
        /// accepting an arbitrary JSON summary merely because it has a SHA-256.
        /// </summary>
        public string WriteObservedTypedDiagnostic(CompletedPlayerLoopToken token, int logicalFrameIndex, uint seed, string relativePath, byte[] bytes, string passId, string encoding, string description, string viewId = null, string derivedFrom = null)
        {
            if (!formalObservedFramesOnly) throw new InvalidOperationException("Observed typed diagnostics are reserved for formal W24 evidence.");
            if (!profile.ContainsSeed(seed) || logicalFrameIndex < 0) throw new ArgumentException("Observed typed diagnostic must use a frozen seed and non-negative logical frame.");
            ValidateObservedPlayerLoopToken(token);
            if (!active) throw new InvalidOperationException("W24 recorder has not begun.");
            ValidateTypedDiagnosticArguments(relativePath, bytes, passId, encoding, description, viewId, derivedFrom);
            var normalized = relativePath.Replace('\\', '/');
            if (!typedDiagnosticPaths.Add(normalized)) throw new InvalidOperationException("W24 typed diagnostics are write-once and may not replay a path: " + normalized);
            string declaredEncoding;
            if (diagnosticPassEncodings.TryGetValue(passId, out declaredEncoding) && !string.Equals(declaredEncoding, encoding, StringComparison.Ordinal))
                throw new InvalidOperationException("W24 diagnostic passId cannot be redeclared with a different encoding: " + passId);
            diagnosticPassEncodings[passId] = encoding;
            var hash = store.WriteBytes(normalized, bytes);
            typedRawDiagnosticRecords.Add("{\"kind\":\"diagnostic\",\"passId\":\"" + W24CaptureProfile.Escape(passId) + "\",\"encoding\":\"" + W24CaptureProfile.Escape(encoding) + "\",\"description\":\"" + W24CaptureProfile.Escape(description) + "\",\"derivedFrom\":\"" + W24CaptureProfile.Escape(derivedFrom) + "\",\"file\":\"" + W24CaptureProfile.Escape(normalized) + "\",\"sha256\":\"" + hash + "\",\"observedPlayerLoop\":{\"serial\":" + token.Serial + ",\"frame\":" + token.Frame + ",\"time\":" + W24CaptureProfile.Number(token.Time) + ",\"logicalFrameIndex\":" + logicalFrameIndex + ",\"seed\":" + seed + ",\"viewId\":\"" + W24CaptureProfile.Escape(viewId) + "\"}}");
            return hash;
        }

        /// <summary>Writes the immutable metrics input after all raw typed passes are present.</summary>
        public string WriteMetricsInput(string relativePath, byte[] bytes, string expectedToolSha256, string metricsEnvironmentSha256)
        {
            if (!active || !formalObservedFramesOnly) throw new InvalidOperationException("Formal W24 metrics input requires an active formal recorder.");
            if (!W24CaptureProfile.IsCanonicalSha256(expectedToolSha256)) throw new ArgumentException("Metrics input requires the frozen canonical metrics-tool hash.", "expectedToolSha256");
            if (!W24CaptureProfile.IsCanonicalSha256(metricsEnvironmentSha256)) throw new ArgumentException("Metrics input requires the canonical Python/dependency environment hash.", "metricsEnvironmentSha256");
            if (string.IsNullOrEmpty(relativePath) || !relativePath.StartsWith("diagnostics/", StringComparison.Ordinal) || !relativePath.EndsWith(".json", StringComparison.Ordinal) || bytes == null || bytes.Length == 0) throw new ArgumentException("Metrics input must be non-empty JSON below diagnostics/.");
            var normalized = relativePath.Replace('\\', '/');
            var hash = store.WriteBytes(normalized, bytes);
            metricInputRecords.Add("{\"kind\":\"metrics-input\",\"file\":\"" + W24CaptureProfile.Escape(normalized) + "\",\"sha256\":\"" + hash + "\",\"expectedToolSha256\":\"" + expectedToolSha256 + "\",\"metricsEnvironmentSha256\":\"" + metricsEnvironmentSha256 + "\"}");
            return hash;
        }

        /// <summary>
        /// The external CLI may only write a system-temp output.  The recorder reads the bytes
        /// and commits them here, which is the sole path by which a metrics report can enter the
        /// write-once evidence directory before the final seal.
        /// </summary>
        public string WriteMetricsReport(string relativePath, byte[] bytes, string inputRelativePath, string inputFileSha256, string analysisInputSha256, string expectedToolSha256)
        {
            if (!active || !formalObservedFramesOnly) throw new InvalidOperationException("Formal W24 metrics report requires an active formal recorder.");
            if (!W24CaptureProfile.IsCanonicalSha256(inputFileSha256) || !W24CaptureProfile.IsCanonicalSha256(analysisInputSha256) || !W24CaptureProfile.IsCanonicalSha256(expectedToolSha256)) throw new ArgumentException("Metrics report requires canonical file/input/tool hashes.");
            var input = inputRelativePath == null ? null : inputRelativePath.Replace('\\', '/');
            if (!metricInputRecords.Any(item => item.IndexOf("\"file\":\"" + W24CaptureProfile.Escape(input) + "\"", StringComparison.Ordinal) >= 0 && item.IndexOf("\"sha256\":\"" + inputFileSha256 + "\"", StringComparison.Ordinal) >= 0 && item.IndexOf("\"expectedToolSha256\":\"" + expectedToolSha256 + "\"", StringComparison.Ordinal) >= 0)) throw new InvalidOperationException("Metrics report must bind one recorder-written metrics input and its frozen tool hash.");
            if (string.IsNullOrEmpty(relativePath) || !relativePath.StartsWith("diagnostics/", StringComparison.Ordinal) || !relativePath.EndsWith(".json", StringComparison.Ordinal) || bytes == null || bytes.Length == 0) throw new ArgumentException("Metrics report must be non-empty JSON below diagnostics/.");
            var normalized = relativePath.Replace('\\', '/');
            var hash = store.WriteBytes(normalized, bytes);
            metricReportRecords.Add("{\"kind\":\"diagnostic\",\"passId\":\"metrics-report\",\"encoding\":\"json\",\"file\":\"" + W24CaptureProfile.Escape(normalized) + "\",\"sha256\":\"" + hash + "\",\"inputFile\":\"" + W24CaptureProfile.Escape(input) + "\",\"inputFileSha256\":\"" + inputFileSha256 + "\",\"analysisInputSha256\":\"" + analysisInputSha256 + "\",\"expectedToolSha256\":\"" + expectedToolSha256 + "\"}");
            return hash;
        }

        private static void ValidateTypedDiagnosticArguments(string relativePath, byte[] bytes, string passId, string encoding, string description, string viewId, string derivedFrom)
        {
            if (string.IsNullOrEmpty(relativePath) || !relativePath.StartsWith("diagnostics/", StringComparison.Ordinal) || bytes == null || bytes.Length == 0 || !ProtocolToken(passId) || !ProtocolToken(encoding) || string.IsNullOrEmpty(description)) throw new ArgumentException("Typed diagnostics require a diagnostics/ path, non-empty bytes, protocol passId/encoding, and description.");
            if (string.IsNullOrEmpty(viewId) || !ProtocolToken(viewId)) throw new ArgumentException("Typed diagnostics require a protocol viewId for seed/view provenance.");
            if (string.IsNullOrEmpty(derivedFrom) || derivedFrom.Contains("..") || derivedFrom.IndexOf('\\') >= 0 || derivedFrom.IndexOf('\r') >= 0 || derivedFrom.IndexOf('\n') >= 0) throw new ArgumentException("Typed diagnostics require a safe derivedFrom provenance reference.");
        }

        private static bool ProtocolToken(string value)
        {
            return !string.IsNullOrEmpty(value) && value.All(character => char.IsLower(character) || char.IsDigit(character) || character == '-' || character == '_' || character == '.');
        }

        public void Complete()
        {
            if (!active) throw new InvalidOperationException("W24 recorder has not begun.");
            if (formalObservedFramesOnly && (missedPlayerLoopToken || latestPlayerLoopSerial != consumedPlayerLoopSerial))
                throw new InvalidOperationException("Formal W24 capture cannot seal while one or more natural LateUpdate observations are unconsumed.");
            try
            {
                var typedPasses = string.Join(",", diagnosticPassEncodings.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => "{\"passId\":\"" + W24CaptureProfile.Escape(pair.Key) + "\",\"encoding\":\"" + W24CaptureProfile.Escape(pair.Value) + "\",\"purpose\":\"typed raw diagnostic; machine-only\"}"));
                var diagnostics = "{\"schema\":\"w24-s0a-diagnostic-pass-manifest/v1\",\"passes\":[{\"passId\":\"effect-only-rgba\",\"encoding\":\"rgba8_png\",\"purpose\":\"minimal effect-only coverage input for machine measurement; not a Beauty frame or an aesthetic conclusion\",\"camera\":\"same serialized authority Camera\",\"clear\":\"transparent black\",\"cullingMask\":" + diagnosticEffectLayers.value + ",\"format\":\"RGBA32 PNG\"}" + (typedPasses.Length == 0 ? string.Empty : "," + typedPasses) + "]}";
                var diagnosticsHash = store.WriteText("diagnostic-pass-manifest.json", diagnostics);
                var metadata = "{\"schema\":\"w24-s0a-capture-evidence/v1\",\"candidateId\":\"" + W24CaptureProfile.Escape(candidateId) + "\",\"captureModePolicy\":\"graphics-device batchmode required; -nographics prohibited; synchronized ReadPixels\",\"executedInBatchMode\":" + W24CaptureProfile.Bool(Application.isBatchMode) + ",\"frameRetentionPolicy\":\"retained-keyframes-only; CaptureFrame may only be called from the frozen retainedFrameIndices table; full-rate raw frames are not formal evidence\",\"retainedFrameIndices\":[" + string.Join(",", profile.RetainedFrameIndices) + "],\"retainedFrameIndicesSha256\":\"" + profile.RetainedFrameIndicesSha256 + "\",\"formalPlayerLoop\":{\"observedSerial\":" + latestPlayerLoopSerial + ",\"consumedSerial\":" + consumedPlayerLoopSerial + ",\"allObservedFramesConsumed\":" + W24CaptureProfile.Bool(!missedPlayerLoopToken && latestPlayerLoopSerial == consumedPlayerLoopSerial) + "},\"captureProfile\":" + profile.ToCanonicalJson() + ",\"captureProfileSha256\":\"" + profile.Sha256 + "\",\"sourceHashes\":" + sources.ToJson() + ",\"diagnosticPassManifest\":{\"file\":\"diagnostic-pass-manifest.json\",\"sha256\":\"" + diagnosticsHash + "\"},\"typedRawDiagnostics\":[" + string.Join(",", typedRawDiagnosticRecords) + "],\"metricInputs\":[" + string.Join(",", metricInputRecords) + "],\"metricReports\":[" + string.Join(",", metricReportRecords) + "],\"semanticTelemetry\":[" + string.Join(",", semanticTelemetryRecords) + "],\"supplementalDiagnostics\":[" + string.Join(",", supplementalDiagnosticRecords) + "],\"frames\":[" + string.Join(",", frameRecords) + "]}";
                var metadataHash = store.WriteText("capture-metadata.json", metadata);
                var provenance = "{\"operatorCommandHash\":" + (operatorCommandHash == null ? "null" : "\"" + W24CaptureProfile.Escape(operatorCommandHash) + "\"")
                    + ",\"captureToolSha256\":\"" + W24CaptureProfile.Escape(sources.CaptureToolSha256) + "\",\"sourceHashesSha256\":\"" + W24CaptureProfile.Escape(W24CaptureProfile.HashText(sources.ToJson()))
                    + "\",\"captureMetadataSha256\":\"" + W24CaptureProfile.Escape(metadataHash) + "\"}";
                store.Seal(provenance);
            }
            finally
            {
                active = false; RestoreTimingWithoutMaskingCaptureFailure();
            }
        }

        /// <summary>Stops observation after a failed capture while preserving the partial write-once directory for invalid-evidence investigation.</summary>
        public void Abort()
        {
            if (!active) return;
            active = false; RestoreTiming();
        }

        public static void RequireGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("W24 visual capture requires a graphics device. Invoke Unity -batchmode without -nographics.");
        }

        public static void RequireGraphicsBatchmode()
        {
            ValidateBatchmodePolicy(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, Application.isBatchMode);
        }

        /// <summary>Pure policy seam for automated verification of the formal batchmode requirement.</summary>
        public static void ValidateBatchmodePolicy(bool hasGraphicsDevice, bool isBatchMode)
        {
            if (!hasGraphicsDevice) throw new InvalidOperationException("W24 visual capture requires a graphics device. Invoke Unity -batchmode without -nographics.");
            if (!isBatchMode) throw new InvalidOperationException("W24 formal evidence requires Unity -batchmode with tools/Invoke-Unity.ps1 -UseGraphics; non-batch capture is only allowed by the automated test override.");
        }

        private void LateUpdate()
        {
            if (!active) return;
            if (formalObservedFramesOnly && latestPlayerLoopSerial != consumedPlayerLoopSerial) missedPlayerLoopToken = true;
            latestPlayerLoopSerial++;
            latestPlayerLoopFrame = Time.frameCount;
            latestPlayerLoopTime = Time.time;
            AfterPlayerLoopFrame?.Invoke(latestPlayerLoopFrame, latestPlayerLoopTime);
        }

        private void RestoreTiming()
        {
            if (!timingSnapshotTaken) return;
            Time.captureFramerate = previousCaptureFramerate; Time.captureDeltaTime = previousCaptureDeltaTime; Application.targetFrameRate = previousTargetFramerate;
            timingSnapshotTaken = false;
        }

        private void RestoreTimingWithoutMaskingCaptureFailure()
        {
            try { RestoreTiming(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private void OnDisable() { active = false; RestoreTimingWithoutMaskingCaptureFailure(); }
        private void OnDestroy() { active = false; RestoreTimingWithoutMaskingCaptureFailure(); }

        private CaptureArtifact CaptureBeauty(string relativePath)
        {
            return RenderToPng(relativePath, authorityCamera.clearFlags, authorityCamera.backgroundColor, authorityCamera.cullingMask, false);
        }

        private CaptureArtifact CaptureEffectOnly(string relativePath)
        {
            return RenderToPng(relativePath, CameraClearFlags.SolidColor, new Color(0f, 0f, 0f, 0f), diagnosticEffectLayers.value, true);
        }

        private CaptureArtifact RenderToPng(string relativePath, CameraClearFlags clearFlags, Color background, int cullingMask, bool foregroundCount)
        {
            var previousTarget = authorityCamera.targetTexture; var previousClear = authorityCamera.clearFlags; var previousBackground = authorityCamera.backgroundColor; var previousMask = authorityCamera.cullingMask; var previousActive = RenderTexture.active;
            // Formal W24 capture is intentionally fixed-exposure linear LDR ARGB32.  Do not
            // let RenderTextureReadWrite.Default silently follow a project sRGB choice while
            // the frozen profile claims a linear measurement surface.
            var target = RenderTexture.GetTemporary(profile.Width, profile.Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Texture2D image = null;
            try
            {
                authorityCamera.targetTexture = target; authorityCamera.clearFlags = clearFlags; authorityCamera.backgroundColor = background; authorityCamera.cullingMask = cullingMask; authorityCamera.Render(); RenderTexture.active = target;
                image = new Texture2D(profile.Width, profile.Height, TextureFormat.RGBA32, false, true); image.ReadPixels(new Rect(0, 0, profile.Width, profile.Height), 0, 0); image.Apply(false);
                var foreground = foregroundCount ? CountForeground(image.GetPixels32()) : 0;
                var hash = store.WriteBytes(relativePath, image.EncodeToPNG());
                return new CaptureArtifact { File = relativePath.Replace('\\', '/'), Hash = hash, ForegroundPixels = foreground };
            }
            finally
            {
                if (image != null) Destroy(image);
                authorityCamera.targetTexture = previousTarget; authorityCamera.clearFlags = previousClear; authorityCamera.backgroundColor = previousBackground; authorityCamera.cullingMask = previousMask; RenderTexture.active = previousActive; RenderTexture.ReleaseTemporary(target);
            }
        }

        private static int CountForeground(Color32[] pixels)
        {
            var count = 0;
            foreach (var pixel in pixels) if (pixel.a > 0 || pixel.r > 0 || pixel.g > 0 || pixel.b > 0) count++;
            return count;
        }

        private struct CaptureArtifact { public string File; public string Hash; public int ForegroundPixels; }
    }
}
