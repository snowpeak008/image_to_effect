using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.W24.S1;

namespace VFXComposer.Editor.W24.S5
{
    /// <summary>Caller supplies observations; S5 verifies and writes the only FORMAL_EVIDENCE_BOUND trace.</summary>
    internal sealed class W24S5FormalEvidenceTransitionRequest
    {
        internal string EffectId;
        internal string CandidateReceiptPath;
        internal string CandidateReceiptFileHash;
        internal string CaptureMetadataPath;
        internal string CaptureMetadataFileHash;
        internal string CompletedTraceJson;
    }

    internal sealed class W24S5FormalEvidenceTransitionResult
    {
        internal bool Succeeded;
        internal string TracePath;
        internal string TraceFileHash;
        internal string ReceiptPath;
        internal string ReceiptFileHash;
        internal readonly List<string> Errors = new List<string>();
        internal void Error(string message) { Errors.Add(message); }
    }

    /// <summary>
    /// A candidate is immutable evidence input. Its evidence seal is a separate write-once child
    /// directory; C0/C1/C2 are reserved exclusively for immutable candidate attempts.
    /// </summary>
    internal static class W24S5EvidenceTransition
    {
        internal const string ArtifactRoot = "artifacts/vfx-evidence/";
        internal const string CandidateRoot = "docs/vfx-candidates/";
        internal const string C0 = "C0";
        internal const string EvidenceDirectory = "evidence";
        internal const int C0CandidateRevision = 0;
        internal const int FirstEvidenceRevision = 1;
        internal const string CandidateReceiptName = "candidate-receipt.json";
        internal const string CandidateContractName = "design-contract.json";
        internal const string CandidateTraceName = "implementation-trace.json";
        internal const string BootstrapManifestSnapshotName = "bootstrap-manifest.json";
        internal const string CompletedTraceName = "implementation-trace.json";
        internal const string TransitionReceiptName = "evidence-transition-receipt.json";

        internal static W24S5FormalEvidenceTransitionResult Finalize(W24S5FormalEvidenceTransitionRequest request)
        {
            var result = new W24S5FormalEvidenceTransitionResult();
            if (request == null || !EffectId(request.EffectId)) { result.Error("effectId must be stable lower_snake_case."); return result; }
            var candidateRoot = CandidateRoot + request.EffectId + "/" + C0;
            var expectedReceipt = candidateRoot + "/" + CandidateReceiptName;
            if (!Same(request.CandidateReceiptPath, expectedReceipt)) { result.Error("C0 candidate receipt path is not canonical."); return result; }

            var gate = new W24S5ProductionGateResult();
            var receiptFile = W24S5ProductionGate.ReadPersisted(gate, request.CandidateReceiptPath, request.CandidateReceiptFileHash, "candidateReceipt", "W24S5-C100", W24S5RecordScope.Formal);
            var metadataFile = ReadArtifact(gate, request.CaptureMetadataPath, request.CaptureMetadataFileHash, "captureMetadata", "W24S5-C101");
            if (gate.HasErrors || receiptFile == null || metadataFile == null) { Copy(gate, result); return result; }

            JObject receipt;
            JObject metadata;
            try { receipt = Parse(receiptFile.Text); metadata = Parse(metadataFile.Text); }
            catch (Exception e) { result.Error("Candidate receipt or capture metadata is invalid: " + e.Message); return result; }

            CandidateContext context;
            if (!TryVerifyCandidate(gate, receiptFile, receipt, request.EffectId, out context) || !TryVerifyMetadata(gate, metadataFile, metadata, context)) { Copy(gate, result); return result; }
            if (string.IsNullOrWhiteSpace(request.CompletedTraceJson)) { result.Error("Completed trace JSON is required."); return result; }

            JObject completed;
            try { completed = Parse(request.CompletedTraceJson); }
            catch (Exception e) { result.Error("Completed trace JSON is invalid: " + e.Message); return result; }
            var root = CandidateRoot + request.EffectId + "/" + C0 + "/" + EvidenceDirectory;
            var tracePath = root + "/" + CompletedTraceName;
            var transitionReceiptPath = root + "/" + TransitionReceiptName;
            var transitionReceipt = new JObject
            {
                ["transitionVersion"] = "w24-s5/evidence-transition-v1",
                ["effectId"] = request.EffectId,
                ["candidateReceiptPath"] = receiptFile.RelativePath,
                ["candidateReceiptFileHash"] = receiptFile.Hash,
                ["captureMetadataPath"] = metadataFile.RelativePath,
                ["captureMetadataFileHash"] = metadataFile.Hash,
                ["productionManifestPath"] = context.ManifestPath,
                ["productionManifestFileHash"] = context.ManifestFileHash,
                ["buildHash"] = "sha256:" + context.RawBuildHash,
                ["runtimeEntryPath"] = context.RuntimeEntryPath,
                ["runtimeEntryGuid"] = context.RuntimeEntryGuid,
                ["candidateRevision"] = C0CandidateRevision,
                ["evidenceRevision"] = FirstEvidenceRevision,
                ["completedTracePath"] = tracePath
            };
            completed["traceStatus"] = "FORMAL_EVIDENCE_BOUND";
            completed["candidateReceiptPath"] = receiptFile.RelativePath;
            completed["candidateReceiptFileHash"] = receiptFile.Hash;
            completed["captureMetadataPath"] = metadataFile.RelativePath;
            completed["captureMetadataFileHash"] = metadataFile.Hash;
            completed["evidenceTransitionReceiptPath"] = transitionReceiptPath;
            completed["effectId"] = context.EffectId;
            completed["contractRevision"] = context.Contract.ContractRevision;
            completed["contractHash"] = context.Contract.ContractHash;
            completed["buildHash"] = "sha256:" + context.RawBuildHash;
            completed["captureProfileHash"] = context.CaptureProfileHash;
            completed["runtimeEntryAssetPath"] = context.RuntimeEntryPath;
            completed["runtimeEntryGuid"] = context.RuntimeEntryGuid;
            completed["candidateRevision"] = C0CandidateRevision;
            completed["evidenceRevision"] = FirstEvidenceRevision;
            completed.Remove("evidenceTransitionReceiptFileHash");
            completed.Remove("completedTraceNormalizedSha256");
            var normalizedTraceHash = NormalizedCompletedTraceHash(completed);
            transitionReceipt["completedTraceNormalizedSha256"] = normalizedTraceHash;
            var transitionBytes = Utf8(Serialize(transitionReceipt));
            completed["evidenceTransitionReceiptFileHash"] = Hash(transitionBytes);
            completed["completedTraceNormalizedSha256"] = normalizedTraceHash;
            var completedText = Serialize(completed);

            VfxImplementationTrace completedTrace;
            var validation = VfxImplementationTraceJson.ValidateJson(completedText, context.Contract, out completedTrace);
            foreach (var issue in validation.Report.Issues) if (issue.Severity == W24GateSeverity.Error) gate.Error(issue.Code, issue.Path, issue.Message);
            if (completedTrace == null || !SamePlanShape(context.CandidateTraceJson, completed) || !AllEvidenceMapsToCapturedArtifacts(gate, completedTrace, context) || (Same(context.EffectId, "sustained_flame_3d") && !VerifySustainedFlameTraceRequirements(gate, completedTrace, context)))
            {
                if (completedTrace == null) gate.Error("W24S5-C102", "completedTrace", "Completed trace could not be parsed.");
                Copy(gate, result); return result;
            }
            if (gate.HasErrors) { Copy(gate, result); return result; }

            var traceBytes = Utf8(completedText);
            try { WriteOnceDirectory(root, new Dictionary<string, byte[]> { { CompletedTraceName, traceBytes }, { TransitionReceiptName, transitionBytes } }); }
            catch (Exception e) { result.Error("Candidate evidence seal was not written: " + e.Message); return result; }
            result.Succeeded = true; result.TracePath = tracePath; result.TraceFileHash = Hash(traceBytes); result.ReceiptPath = transitionReceiptPath; result.ReceiptFileHash = Hash(transitionBytes);
            return result;
        }

        /// <summary>Called by normal S5 admission; it verifies the immutable C0/evidence/capture chain again.</summary>
        internal static bool VerifyForFormalGate(W24S5ProductionGateResult gate, W24S5PersistedFile contractFile, W24S5PersistedFile traceFile, VfxDesignContract contract, VfxImplementationTrace trace)
        {
            if (contractFile == null || traceFile == null || contract == null || trace == null) return false;
            var candidateRoot = CandidateRoot + contract.EffectId + "/" + C0;
            var expectedContract = candidateRoot + "/" + CandidateContractName;
            var expectedCompleteTrace = CandidateRoot + contract.EffectId + "/" + C0 + "/" + EvidenceDirectory + "/" + CompletedTraceName;
            var expectedCandidateReceipt = candidateRoot + "/" + CandidateReceiptName;
            var expectedTransitionReceipt = CandidateRoot + contract.EffectId + "/" + C0 + "/" + EvidenceDirectory + "/" + TransitionReceiptName;
            if (!Same(contractFile.RelativePath, expectedContract) || !Same(traceFile.RelativePath, expectedCompleteTrace)) { gate.Error("W24S5-C200", "formalEvidence", "Formal admission must use the canonical C0 Contract and its C0/evidence completed Trace."); return false; }
            if (!Same(trace.TraceStatus, "FORMAL_EVIDENCE_BOUND") || trace.CandidateRevision != C0CandidateRevision || trace.EvidenceRevision != FirstEvidenceRevision || string.IsNullOrWhiteSpace(trace.CandidateReceiptPath) || string.IsNullOrWhiteSpace(trace.CandidateReceiptFileHash) || string.IsNullOrWhiteSpace(trace.CaptureMetadataPath) || string.IsNullOrWhiteSpace(trace.CaptureMetadataFileHash) || string.IsNullOrWhiteSpace(trace.EvidenceTransitionReceiptPath) || string.IsNullOrWhiteSpace(trace.EvidenceTransitionReceiptFileHash) || !W24Hash.IsCanonical(trace.CompletedTraceNormalizedSha256)) { gate.Error("W24S5-C201", "implementationTrace", "Completed trace lacks the exact candidate/evidence revision or gate-owned C0 receipt/capture metadata/normalized transition identity."); return false; }
            if (!Same(trace.CandidateReceiptPath, expectedCandidateReceipt) || !Same(trace.EvidenceTransitionReceiptPath, expectedTransitionReceipt)) { gate.Error("W24S5-C202", "implementationTrace", "Completed Trace must name the canonical C0 candidate and C0/evidence transition receipts."); return false; }
            var receipt = W24S5ProductionGate.ReadPersisted(gate, trace.CandidateReceiptPath, trace.CandidateReceiptFileHash, "candidateReceipt", "W24S5-C202", W24S5RecordScope.Formal);
            var transition = W24S5ProductionGate.ReadPersisted(gate, trace.EvidenceTransitionReceiptPath, trace.EvidenceTransitionReceiptFileHash, "transitionReceipt", "W24S5-C203", W24S5RecordScope.Formal);
            var metadata = ReadArtifact(gate, trace.CaptureMetadataPath, trace.CaptureMetadataFileHash, "captureMetadata", "W24S5-C204");
            if (receipt == null || transition == null || metadata == null) return false;
            try
            {
                var receiptRoot = Parse(receipt.Text); var metadataRoot = Parse(metadata.Text); var transitionRoot = Parse(transition.Text);
                CandidateContext context;
                if (!TryVerifyCandidate(gate, receipt, receiptRoot, contract.EffectId, out context) || !TryVerifyMetadata(gate, metadata, metadataRoot, context)) return false;
                if ((int?)transitionRoot["candidateRevision"] != C0CandidateRevision || (int?)transitionRoot["evidenceRevision"] != FirstEvidenceRevision || !Same((string)transitionRoot["completedTracePath"], traceFile.RelativePath) || !Same((string)transitionRoot["candidateReceiptFileHash"], receipt.Hash) || !Same((string)transitionRoot["captureMetadataFileHash"], metadata.Hash) || !Same((string)transitionRoot["completedTraceNormalizedSha256"], trace.CompletedTraceNormalizedSha256) || !Same(trace.CompletedTraceNormalizedSha256, NormalizedCompletedTraceHash(Parse(traceFile.Text)))) { gate.Error("W24S5-C205", "transitionReceipt", "Transition receipt does not bind the exact candidate/evidence revision, normalized completed Trace, C0 receipt, and capture metadata."); return false; }
                if (!Same(trace.ContractHash, context.Contract.ContractHash) || trace.ContractRevision != context.Contract.ContractRevision || !Same(trace.BuildHash, "sha256:" + context.RawBuildHash) || !Same(trace.CaptureProfileHash, context.CaptureProfileHash) || !Same(trace.RuntimeEntryAssetPath, context.RuntimeEntryPath) || !Same(trace.RuntimeEntryGuid, context.RuntimeEntryGuid)) { gate.Error("W24S5-C206", "implementationTrace", "Completed Trace identity differs from its C0 candidate."); return false; }
                return AllEvidenceMapsToCapturedArtifacts(gate, trace, context) && (!Same(context.EffectId, "sustained_flame_3d") || VerifySustainedFlameTraceRequirements(gate, trace, context)) && !gate.HasErrors;
            }
            catch (Exception e) when (e is JsonException || e is FormatException || e is IOException) { gate.Error("W24S5-C207", "formalEvidence", "Formal evidence chain cannot be verified: " + e.Message); return false; }
        }

        private sealed class CandidateContext
        {
            internal string EffectId; internal string RawBuildHash; internal string RuntimeEntryPath; internal string RuntimeEntryGuid; internal string ManifestPath; internal string ManifestFileHash; internal string CaptureProfileHash; internal string RecorderCaptureProfileHash;
            internal VfxDesignContract Contract; internal JObject CandidateTraceJson; internal JToken OwnedOutputs; internal List<Artifact> Artifacts = new List<Artifact>();
            internal readonly Dictionary<string, MetricReport> MetricReports = new Dictionary<string, MetricReport>(StringComparer.Ordinal);
        }
        private sealed class Artifact { internal string Path; internal string Hash; internal string Kind; internal string PassId; internal string Encoding; }
        private sealed class MetricReport
        {
            internal string InputHash;
            internal string ToolHash;
            internal readonly HashSet<string> PassingChecks = new HashSet<string>(StringComparer.Ordinal);
            internal readonly Dictionary<string, string> CheckKinds = new Dictionary<string, string>(StringComparer.Ordinal);
            internal readonly Dictionary<string, HashSet<string>> RequirementChecks = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        }

        private static bool TryVerifyCandidate(W24S5ProductionGateResult gate, W24S5PersistedFile receiptFile, JObject receipt, string effectId, out CandidateContext context)
        {
            context = null;
            var root = CandidateRoot + effectId + "/" + C0;
            var contractPath = root + "/" + CandidateContractName;
            var tracePath = root + "/" + CandidateTraceName;
            var snapshotPath = root + "/" + BootstrapManifestSnapshotName;
            if (!Same((string)receipt["candidateVersion"], "w24-candidate/1.0") || !Same((string)receipt["candidateId"], C0) || (int?)receipt["candidateRevision"] != C0CandidateRevision || !Same((string)receipt["candidateStatus"], "C0_CAPTURE_PENDING") || !Same((string)receipt["effectId"], effectId) || !Same((string)receipt["contractPath"], contractPath) || !Same((string)receipt["tracePath"], tracePath) || !Same((string)receipt["bootstrapManifestSnapshotPath"], snapshotPath)) { gate.Error("W24S5-C110", "candidateReceipt", "C0 receipt identity is invalid."); return false; }
            var contractFile = W24S5ProductionGate.ReadPersisted(gate, contractPath, (string)receipt["contractFileHash"], "candidateContract", "W24S5-C111", W24S5RecordScope.Formal);
            var traceFile = W24S5ProductionGate.ReadPersisted(gate, tracePath, (string)receipt["traceFileHash"], "candidateTrace", "W24S5-C112", W24S5RecordScope.Formal);
            var manifestFile = W24S5ProductionGate.ReadPersisted(gate, snapshotPath, (string)receipt["bootstrapManifestSnapshotFileHash"], "bootstrapManifestSnapshot", "W24S5-C113", W24S5RecordScope.Formal);
            var bootstrapContractFile = W24S5ProductionGate.ReadPersisted(gate, (string)receipt["bootstrapContractPath"], (string)receipt["bootstrapContractFileHash"], "bootstrapContract", "W24S5-C109", W24S5RecordScope.Formal);
            var bootstrapTraceFile = W24S5ProductionGate.ReadPersisted(gate, (string)receipt["bootstrapTracePath"], (string)receipt["bootstrapTraceFileHash"], "bootstrapTrace", "W24S5-C108", W24S5RecordScope.Formal);
            if (contractFile == null || traceFile == null || manifestFile == null || bootstrapContractFile == null || bootstrapTraceFile == null) return false;
            VfxDesignContract contract; var contractValidation = VfxDesignContractJson.ValidateJson(contractFile.Text, out contract);
            foreach (var issue in contractValidation.Issues) if (issue.Severity == W24GateSeverity.Error) gate.Error(issue.Code, issue.Path, issue.Message);
            VfxDesignContract bootstrapContract; var bootstrapContractValidation = VfxDesignContractJson.ValidateJson(bootstrapContractFile.Text, out bootstrapContract);
            foreach (var issue in bootstrapContractValidation.Issues) if (issue.Severity == W24GateSeverity.Error) gate.Error(issue.Code, issue.Path, issue.Message);
            VfxImplementationTrace pending;
            try { pending = VfxImplementationTraceJson.FromJson(traceFile.Text); } catch (Exception e) { gate.Error("W24S5-C114", "candidateTrace", e.Message); return false; }
            VfxImplementationTrace bootstrapTrace;
            try { bootstrapTrace = VfxImplementationTraceJson.FromJson(bootstrapTraceFile.Text); } catch (Exception e) { gate.Error("W24S5-C107", "bootstrapTrace", e.Message); return false; }
            JObject manifest;
            try { manifest = Parse(manifestFile.Text); } catch (Exception e) { gate.Error("W24S5-C115", "productionManifest", e.Message); return false; }
            if (bootstrapContract == null || !Same(bootstrapContract.EffectId, effectId) || !Same((string)receipt["bootstrapContractHash"], bootstrapContract.ContractHash) || (int?)receipt["bootstrapContractRevision"] != bootstrapContract.ContractRevision || !Same(bootstrapTrace.TraceStatus, "PENDING_FIRST_FORMAL_BUILD_BINDING") || !Same(bootstrapTrace.BuildHash, "pending:formal-build") || !Same(bootstrapTrace.CaptureProfileHash, "pending:formal-build") || !Same(bootstrapTrace.RuntimeEntryGuid, "pending:formal-build")) { gate.Error("W24S5-C106", "candidateReceipt.bootstrap", "C0 receipt does not bind the immutable S5 preregistration Contract and pending Trace."); return false; }
            if (contract == null || !Same(contract.EffectId, effectId) || !Same((string)contract.Extensions["candidateStatus"], "C0_CAPTURE_PENDING") || !Same((string)contract.Extensions["candidateReceipt"], receiptFile.RelativePath) || !Same(pending.TraceStatus, "C0_CAPTURE_PENDING") || pending.CandidateRevision != C0CandidateRevision || pending.EvidenceRevision != 0 || !Same(pending.ContractHash, contract.ContractHash)) { gate.Error("W24S5-C116", "candidate", "C0 Contract/Trace are not the immutable pending candidate."); return false; }
            var rawBuild = (string)manifest["buildHash"]; var runtime = manifest["runtimeEntry"] as JObject;
            string ownedError = null;
            var ownedRoot = Path.GetDirectoryName((string)runtime?["path"]) == null ? null : Path.GetDirectoryName((string)runtime["path"]).Replace('\\', '/');
            if (!RawHash(rawBuild) || runtime == null || !W24S5ProductionGate.VerifyOwnedOutputManifest(manifest, effectId, (string)runtime["path"], ownedRoot, out ownedError)) { gate.Error("W24S5-C117", "productionManifest", "C0 Manifest/owned-output identity is invalid: " + ownedError); return false; }
            var formal = manifest["formalProduction"] as JObject;
            if (!W24S5ProductionGate.HasExactEvidenceFreeBootstrapBinding(formal, bootstrapContractFile.RelativePath, bootstrapContractFile.Hash, bootstrapContract.ContractHash, bootstrapContract.ContractRevision, bootstrapTraceFile.RelativePath, bootstrapTraceFile.Hash)) { gate.Error("W24S5-C105", "productionManifest.formalProduction", "C0 candidate is not rooted in the immutable evidence-free first-formal-build binding."); return false; }
            if (!JToken.DeepEquals(receipt["ownedOutputs"], manifest["ownedOutputs"]) || !Same((string)receipt["buildHash"], "sha256:" + rawBuild) || !Same((string)receipt["runtimeEntryPath"], (string)runtime["path"]) || !Same((string)receipt["runtimeEntryGuid"], (string)runtime["guid"])) { gate.Error("W24S5-C118", "candidateReceipt", "C0 receipt does not bind the exact Manifest owned-output identity."); return false; }
            // The immutable snapshot above proves C0.  The authoritative manifest may later
            // evolve from PRE_C0 to normal formalProduction, but live output bytes/meta may not.
            string livePath;
            if (!W24S5ProductionGate.TryResolvePersistedPath((string)receipt["productionManifestPath"], W24S5RecordScope.Formal, out livePath) || !File.Exists(livePath)) { gate.Error("W24S5-C125", "productionManifest", "Current authoritative Manifest is missing."); return false; }
            JObject live;
            try { live = Parse(File.ReadAllText(livePath)); } catch (Exception e) { gate.Error("W24S5-C125", "productionManifest", "Current authoritative Manifest is invalid: " + e.Message); return false; }
            string liveOwnedError = null;
            if (!W24S5ProductionGate.VerifyOwnedOutputManifest(live, effectId, (string)runtime["path"], ownedRoot, out liveOwnedError) || !JToken.DeepEquals(receipt["ownedOutputs"], live["ownedOutputs"])) { gate.Error("W24S5-C126", "productionManifest", "Current owned outputs drifted from C0 identity: " + liveOwnedError); return false; }
            var preview = (string)receipt["previewScenePath"]; var previewHash = (string)receipt["previewSceneHash"];
            if (!SafeAsset(preview) || !File.Exists(ProjectAbsolute(preview)) || !Same(Hash(File.ReadAllBytes(ProjectAbsolute(preview))), previewHash) || !Same(contract.CaptureProfile.SceneSerializedReference, preview) || !Same(contract.CaptureProfile.SceneHash, previewHash)) { gate.Error("W24S5-C119", "candidateReceipt.previewScene", "C0 Preview Scene bytes do not match the candidate Contract."); return false; }
            context = new CandidateContext { EffectId = effectId, RawBuildHash = rawBuild, RuntimeEntryPath = (string)runtime["path"], RuntimeEntryGuid = (string)runtime["guid"], ManifestPath = (string)receipt["productionManifestPath"], ManifestFileHash = manifestFile.Hash, Contract = contract, CandidateTraceJson = Parse(traceFile.Text), OwnedOutputs = receipt["ownedOutputs"].DeepClone(), CaptureProfileHash = "sha256:" + VFXComposer.Editor.Validation.RecipeCanonicalizer.ComputeSha256(((JObject)Parse(contractFile.Text)["captureProfile"]).ToString(Formatting.None)) };
            return !gate.HasErrors;
        }

        private static bool TryVerifyMetadata(W24S5ProductionGateResult gate, W24S5PersistedFile metadataFile, JObject metadata, CandidateContext context)
        {
            if (!Same((string)metadata["schemaVersion"], "w24-capture-metadata-v1") || !Same((string)metadata["effectId"], context.EffectId) || !Same((string)metadata["productionManifestPath"], context.ManifestPath) || !Same((string)metadata["productionManifestFileHash"], context.ManifestFileHash) || !Same((string)metadata["buildHash"], "sha256:" + context.RawBuildHash) || !Same((string)metadata["runtimeEntryPath"], context.RuntimeEntryPath) || !Same((string)metadata["runtimeEntryGuid"], context.RuntimeEntryGuid)) { gate.Error("W24S5-C120", "captureMetadata", "Capture metadata does not bind the exact C0 Manifest/runtime identity."); return false; }
            var recorderCaptureProfile = metadata["recorderCaptureProfile"] as JObject; var recorderCaptureProfileHash = (string)metadata["recorderCaptureProfileSha256"];
            if (recorderCaptureProfile == null || !W24Hash.IsCanonical(recorderCaptureProfileHash) || !Same(recorderCaptureProfileHash, Hash(Utf8(recorderCaptureProfile.ToString(Formatting.None))))) { gate.Error("W24S5-C120", "captureMetadata.recorderCaptureProfile", "Formal metadata must preserve the exact W24 field-order recorder profile serialization and hash."); return false; }
            context.RecorderCaptureProfileHash = recorderCaptureProfileHash;
            if (!JToken.DeepEquals(metadata["ownedOutputs"], context.OwnedOutputs)) { gate.Error("W24S5-C121", "captureMetadata.ownedOutputs", "Capture metadata lacks the exact immutable C0 owned-output snapshot."); return false; }
            var artifacts = metadata["artifacts"] as JArray;
            if (artifacts == null || artifacts.Count == 0) { gate.Error("W24S5-C122", "captureMetadata.artifacts", "At least one hashed capture artifact is required."); return false; }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in artifacts.OfType<JObject>())
            {
                var path = (string)item["path"]; var hash = (string)item["sha256"]; var kind = (string)item["kind"];
                var file = ReadArtifact(gate, path, hash, "captureArtifact", "W24S5-C123");
                if (file == null || string.IsNullOrWhiteSpace(kind) || !seen.Add(path)) { gate.Error("W24S5-C124", "captureMetadata.artifacts", "Capture artifacts must have unique safe paths, hashes, and kinds."); continue; }
                context.Artifacts.Add(new Artifact { Path = path, Hash = hash, Kind = kind, PassId = (string)item["passId"], Encoding = (string)item["encoding"] });
            }
            if (!VerifyTypedMetricsDag(gate, metadata, context)) return false;
            if (Same(context.EffectId, "sustained_flame_3d") && !VerifySustainedFlameFormalMetadata(gate, metadata, context.Contract)) return false;
            return !gate.HasErrors;
        }

        // S0b predates the typed render-metrics DAG, but it is still a formal machine-evidence
        // capture. Keep its replay proof strict and separate: empty typed arrays remain valid
        // only while its Contract has no typed declaration; lifecycle, light A/B, and token facts are verified
        // from the sealed projection rather than from a caller-supplied `passed` flag.
        internal static bool VerifySustainedFlameFormalMetadata(W24S5ProductionGateResult gate, JObject metadata, VfxDesignContract contract)
        {
            const string effectId = "sustained_flame_3d";
            const int exitRequestFrame = 291;
            var requiredFrames = new HashSet<long> { 1, 21, 60, 120, 180, 240, 270, 291, 293, 321, 366 };
            if (gate == null || metadata == null || contract == null || !Same(contract.EffectId, effectId) || contract.CaptureProfile == null || contract.Lifecycle == null || contract.Cleanup == null || contract.Budget == null) return S0bError(gate, "W24S5-C165", "s0bFormalEvidence", "S0b replay requires its exact Contract capture, lifecycle, cleanup, and budget fields.");
            var proof = metadata["s0bFormalEvidence"] as JObject;
            if (proof == null || !Same((string)proof["schema"], "w24-s0b-formal-evidence-projection/v1")) return S0bError(gate, "W24S5-C166", "s0bFormalEvidence", "S0b formal metadata must retain its structured replay projection.");
            var profile = proof["captureProfile"] as JObject;
            var expectedSeeds = new HashSet<long>(new[] { (long)contract.CaptureProfile.CanonicalSeed }.Concat((contract.CaptureProfile.RobustnessSeeds ?? Array.Empty<uint>()).Select(value => (long)value)));
            if (expectedSeeds.Count != 3 || (int?)profile?["fps"] != contract.CaptureProfile.Fps || !SetEquals(profile?["canonicalSeed"] as JValue, profile?["robustnessSeeds"] as JArray, expectedSeeds) || !SetEquals(profile?["retainedFrameIndices"] as JArray, requiredFrames)) return S0bError(gate, "W24S5-C167", "s0bFormalEvidence.captureProfile", "S0b replay profile must preserve the exact Contract seeds, FPS, and retained-frame matrix.");
            var playerLoop = proof["formalPlayerLoop"] as JObject;
            if ((long?)playerLoop?["observedSerial"] <= 0 || (long?)playerLoop?["observedSerial"] != (long?)playerLoop?["consumedSerial"] || (bool?)playerLoop?["allObservedFramesConsumed"] != true) return S0bError(gate, "W24S5-C168", "s0bFormalEvidence.formalPlayerLoop", "S0b final metadata must prove all observed PlayerLoop tokens were consumed.");

            var frames = (proof["frames"] as JArray ?? new JArray()).OfType<JObject>().ToArray();
            if (frames.Length != requiredFrames.Count * expectedSeeds.Count || frames.Any(item => !expectedSeeds.Contains((long?)item["seed"] ?? -1L) || !requiredFrames.Contains((long?)item["frameIndex"] ?? -1L)) || frames.Select(item => ((long?)item["seed"] ?? -1L).ToString() + ":" + ((long?)item["frameIndex"] ?? -1L).ToString()).Distinct(StringComparer.Ordinal).Count() != frames.Length) return S0bError(gate, "W24S5-C169", "s0bFormalEvidence.frames", "S0b replay must retain exactly one sealed Beauty/effect-only frame pair for every seed/frame matrix cell.");

            JObject commandRecord;
            if (!SingleRecord(proof["supplementalDiagnostics"] as JArray, "formal-capture-command", out commandRecord) || !Same((string)commandRecord["file"], "diagnostics/operator-command.json")) return S0bError(gate, "W24S5-C170", "s0bFormalEvidence.supplementalDiagnostics", "S0b replay requires the sealed frozen operator command record.");
            var commandArtifact = FindArtifact(metadata, effectId, (string)commandRecord["file"], "diagnostic");
            var provenance = proof["recorderProvenance"] as JObject;
            if (commandArtifact == null || !Same((string)commandArtifact["sha256"], (string)commandRecord["sha256"]) || !Same((string)provenance?["operatorCommandHash"], (string)commandRecord["sha256"])) return S0bError(gate, "W24S5-C171", "s0bFormalEvidence.recorderProvenance", "S0b final seal must bind the exact operator-command artifact.");
            var command = ReadS0bJson(gate, commandArtifact, "W24S5-C171", "operatorCommand");
            if (command == null || !Same((string)command["schema"], "w24-s0b-formal-operator-command/v1") || !Same((string)command["effectId"], effectId) || !Same((string)command["candidateId"], C0) || !SetEquals(command["seeds"] as JArray, expectedSeeds) || !SetEquals(command["retainedFrameIndices"] as JArray, requiredFrames) || !S0bCommandBranches(command["branches"] as JArray, expectedSeeds)) return S0bError(gate, "W24S5-C172", "operatorCommand", "S0b operator command must retain the exact seed/frame/stop-stop-interrupt plan.");

            JObject telemetryRecord;
            if (!SingleRecord(proof["semanticTelemetry"] as JArray, "semantic-telemetry", out telemetryRecord) || !Same((string)telemetryRecord["file"], "diagnostics/semantic-telemetry.json")) return S0bError(gate, "W24S5-C173", "s0bFormalEvidence.semanticTelemetry", "S0b replay requires one sealed semantic telemetry record.");
            var telemetryArtifact = FindArtifact(metadata, effectId, (string)telemetryRecord["file"], "telemetry");
            if (telemetryArtifact == null || !Same((string)telemetryArtifact["sha256"], (string)telemetryRecord["sha256"])) return S0bError(gate, "W24S5-C173", "s0bFormalEvidence.semanticTelemetry", "S0b semantic telemetry must resolve to its sealed telemetry artifact.");
            var telemetry = ReadS0bJson(gate, telemetryArtifact, "W24S5-C173", "semanticTelemetry");
            if (telemetry == null || !Same((string)telemetry["schema"], "w24-s0b-semantic-telemetry/v2") || !Same((string)telemetry["captureCompleteness"], "complete") || !S0bRuntimeFacts(telemetry["runtimeFacts"] as JObject, contract) || !S0bBranches(telemetry["branches"] as JArray, contract, expectedSeeds, exitRequestFrame)) return S0bError(gate, "W24S5-C178", "semanticTelemetry", "S0b lifecycle, branch cleanup, light-fade, or runtime-budget facts do not satisfy the frozen Contract.");

            JObject off; JObject on; JObject summary;
            if (!SingleRecord(proof["supplementalDiagnostics"] as JArray, "receiver-light-off", out off) || !SingleRecord(proof["supplementalDiagnostics"] as JArray, "receiver-light-on", out on) || !SingleRecord(proof["supplementalDiagnostics"] as JArray, "receiver-linear-luminance-ab", out summary) || !Same((string)off["file"], "diagnostics/receiver-light-off.png") || !Same((string)on["file"], "diagnostics/receiver-light-on.png") || !Same((string)summary["file"], "diagnostics/receiver-light-ab.json") || !SameObservedToken(off["observedPlayerLoop"] as JObject, on["observedPlayerLoop"] as JObject) || !SameObservedToken(off["observedPlayerLoop"] as JObject, summary["observedPlayerLoop"] as JObject) || !ExpectedReceiverToken(off["observedPlayerLoop"] as JObject)) return S0bError(gate, "W24S5-C174", "s0bFormalEvidence.receiverLight", "Receiver off/on/summary must share one sealed seed=24001 logical-frame=180 PlayerLoop token.");
            var offArtifact = FindArtifact(metadata, effectId, (string)off["file"], "diagnostic");
            var onArtifact = FindArtifact(metadata, effectId, (string)on["file"], "diagnostic");
            var summaryArtifact = FindArtifact(metadata, effectId, (string)summary["file"], "diagnostic");
            if (offArtifact == null || onArtifact == null || summaryArtifact == null || !Same((string)offArtifact["sha256"], (string)off["sha256"]) || !Same((string)onArtifact["sha256"], (string)on["sha256"]) || !Same((string)summaryArtifact["sha256"], (string)summary["sha256"])) return S0bError(gate, "W24S5-C174", "s0bFormalEvidence.receiverLight", "Every receiver A/B token-bound record must resolve to a sealed artifact.");
            var receiver = ReadS0bJson(gate, summaryArtifact, "W24S5-C174", "receiverLightSummary");
            var offLuminance = (double?)receiver?["offLinearLuminance"];
            var onLuminance = (double?)receiver?["onLinearLuminance"];
            if (receiver == null || !Same((string)receiver["schema"], "w24-s0b-receiver-light-ab/v2") || !Same((string)receiver["onlyChangedBetweenSamples"], "UnityEngine.Light.enabled") || !offLuminance.HasValue || !onLuminance.HasValue || onLuminance.Value <= offLuminance.Value + .001d) return S0bError(gate, "W24S5-C174", "receiverLightSummary", "Receiver A/B must retain a positive linear-luminance delta caused only by the real Light.");
            return !gate.HasErrors;
        }

        private static bool VerifySustainedFlameTraceRequirements(W24S5ProductionGateResult gate, VfxImplementationTrace trace, CandidateContext context)
        {
            var telemetry = FindArtifact(context, "diagnostics/semantic-telemetry.json", "telemetry");
            var receiver = FindArtifact(context, "diagnostics/receiver-light-ab.json", "diagnostic");
            if (telemetry == null || receiver == null) return S0bError(gate, "W24S5-C175", "implementationTrace", "S0b completed Trace requires sealed semantic telemetry and receiver A/B artifacts.");
            foreach (var requirement in trace.RequirementTraces ?? Array.Empty<VfxRequirementTrace>())
            {
                var evidence = requirement.AuthorityEvidence ?? Array.Empty<VfxTraceEvidence>();
                if (requirement.EvidenceAuthority == "visualQa" || requirement.EvidenceAuthority == "user")
                {
                    if (evidence.Length != 1 || evidence[0].Passed || !Same(evidence[0].Kind, requirement.EvidenceAuthority) || string.IsNullOrEmpty(evidence[0].Reference) || !evidence[0].Reference.StartsWith("pending:", StringComparison.Ordinal)) S0bError(gate, "W24S5-C176", "implementationTrace." + requirement.DesignRequirementId, "S0b formal capture must preserve pending Visual QA/user authority.");
                    continue;
                }
                var expected = Same(requirement.DesignRequirementId, "REQ-LIGHT-RECEIVER") ? receiver : telemetry;
                if (evidence.Length != 1 || !evidence[0].Passed || !Same(evidence[0].Reference, expected.Path) || !Same(evidence[0].Sha256, expected.Hash) || !Same(evidence[0].Kind, expected.Kind)) S0bError(gate, "W24S5-C177", "implementationTrace." + requirement.DesignRequirementId, "S0b machine requirement must resolve to its independently replay-verified sealed telemetry or receiver artifact.");
            }
            return !gate.HasErrors;
        }

        private static bool S0bRuntimeFacts(JObject facts, VfxDesignContract contract)
        {
            var particleCarriers = (contract.Layers ?? Array.Empty<VfxLayer>()).Count(item => Same(item.Carrier, "Shuriken ParticleSystem"));
            return facts != null && (bool?)facts["layersIndependent"] == true && (bool?)facts["lightWithinContract"] == true && (bool?)facts["budgetWithinContract"] == true && (int?)facts["particleSystemCount"] == particleCarriers && (int?)facts["particleCapacity"] > 0 && (int?)facts["particleCapacity"] <= contract.Budget.ParticlePeak && (int?)facts["particleRendererCount"] == contract.Budget.RendererCount && (int?)facts["materialCount"] > 0 && (int?)facts["materialCount"] <= contract.Budget.MaterialCount && (int?)facts["lightCount"] == contract.Budget.LightCount;
        }

        private static bool S0bBranches(JArray branches, VfxDesignContract contract, HashSet<long> expectedSeeds, int exitRequestFrame)
        {
            if (branches == null || branches.Count != expectedSeeds.Count) return false;
            var seen = new HashSet<long>();
            foreach (var branch in branches.OfType<JObject>())
            {
                var seed = (long?)branch["seed"];
                var interrupt = seed == 24021;
                var deadline = interrupt ? contract.Lifecycle.Interrupt.DeadlineSeconds : contract.Lifecycle.Stop.DeadlineSeconds;
                var frames = (branch["frames"] as JArray ?? new JArray()).OfType<JObject>().ToArray();
                var final = branch["final"] as JObject;
                var firstSteady = (int?)branch["firstSteadyFrame"];
                var lastLit = (int?)branch["lastLitExitFrame"];
                var recordedFrameIndices = new HashSet<int>(frames.Select(item => (int?)item["frameIndex"] ?? -1));
                var computedFirstSteady = frames.Where(item => Same((string)item["state"], "steady")).Select(item => (int?)item["frameIndex"]).Where(item => item.HasValue).Select(item => item.Value).DefaultIfEmpty(-1).First();
                var computedLastLit = frames.Where(item => (int?)item["frameIndex"] >= exitRequestFrame + 1 && (int?)item.SelectToken("sample.enabledLightCount") > 0).Select(item => (int?)item["frameIndex"]).Where(item => item.HasValue).Select(item => item.Value).DefaultIfEmpty(-1).Last();
                if (!seed.HasValue || !expectedSeeds.Contains(seed.Value) || !seen.Add(seed.Value) || !Same((string)branch["exit"], interrupt ? "interrupt" : "stop") || (int?)branch["observedFrames"] != 366 || frames.Length != 366 || recordedFrameIndices.Count != 366 || !recordedFrameIndices.SetEquals(Enumerable.Range(1, 366)) || (int?)branch["retainedFrames"] != 11 || firstSteady.GetValueOrDefault(-1) != computedFirstSteady || firstSteady.GetValueOrDefault(-1) <= 0 || firstSteady.Value > (int)Math.Ceiling(contract.Lifecycle.Start.DeadlineSeconds * contract.CaptureProfile.Fps) || (bool?)branch["steadyAtExitRequest"] != true || !frames.Any(item => (int?)item["frameIndex"] == exitRequestFrame && Same((string)item["state"], "steady")) || !frames.Any(item => Same((string)item["state"], interrupt ? "interrupted" : "stopping")) || (bool?)branch["sawRequestedExit"] != true || (bool?)branch["sawExitCarrier"] != true || lastLit.GetValueOrDefault(-1) != computedLastLit || lastLit.GetValueOrDefault(-1) < exitRequestFrame + 1 || (lastLit.Value - exitRequestFrame) / (double)contract.CaptureProfile.Fps > deadline + 1d / contract.CaptureProfile.Fps || final == null || !Same((string)final["state"], "idle") || (bool?)final["cleanupComplete"] != true || (int?)final["enabledLightCount"] != 0 || (366 - exitRequestFrame) / (double)contract.CaptureProfile.Fps > contract.Cleanup.CleanupDeadline) return false;
            }
            return seen.SetEquals(expectedSeeds);
        }

        private static bool S0bCommandBranches(JArray branches, HashSet<long> expectedSeeds)
        {
            var expected = new Dictionary<long, string> { { 24001, "stop" }, { 24011, "stop" }, { 24021, "interrupt" } };
            return branches != null && branches.Count == expectedSeeds.Count && branches.OfType<JObject>().All(item => expected.TryGetValue((long?)item["seed"] ?? -1L, out var exit) && Same((string)item["exit"], exit) && (int?)item["steadyFramesBeforeExit"] == 291 && (int?)item["cleanupThroughFrame"] == 366) && branches.OfType<JObject>().Select(item => (long?)item["seed"] ?? -1L).Distinct().Count() == expectedSeeds.Count;
        }

        private static bool ExpectedReceiverToken(JObject token) { return token != null && (long?)token["serial"] > 0 && (long?)token["frame"] >= 0 && (long?)token["logicalFrameIndex"] == 180 && (long?)token["seed"] == 24001 && token["time"] != null; }
        private static bool SameObservedToken(JObject a, JObject b) { return a != null && b != null && (long?)a["serial"] == (long?)b["serial"] && (long?)a["frame"] == (long?)b["frame"] && JToken.DeepEquals(a["time"], b["time"]) && (long?)a["logicalFrameIndex"] == (long?)b["logicalFrameIndex"] && (long?)a["seed"] == (long?)b["seed"]; }
        private static bool SingleRecord(JArray records, string kind, out JObject record) { var matches = (records ?? new JArray()).OfType<JObject>().Where(item => Same((string)item["kind"], kind)).ToArray(); record = matches.Length == 1 ? matches[0] : null; return record != null; }
        private static bool SetEquals(JArray values, HashSet<long> expected) { return values != null && new HashSet<long>(values.Select(item => (long)item)).SetEquals(expected) && values.Count == expected.Count; }
        private static bool SetEquals(JValue canonical, JArray robustness, HashSet<long> expected) { return canonical != null && SetEquals(new JArray(new[] { canonical }.Concat((robustness ?? new JArray()).Children())), expected); }
        private static JObject FindArtifact(JObject metadata, string effectId, string local, string kind) { var path = ArtifactRoot + effectId + "/C0/" + local; return (metadata["artifacts"] as JArray ?? new JArray()).OfType<JObject>().SingleOrDefault(item => Same((string)item["path"], path) && Same((string)item["kind"], kind)); }
        private static Artifact FindArtifact(CandidateContext context, string local, string kind) { return context.Artifacts.SingleOrDefault(item => Same(CaptureLocal(item.Path, context.EffectId), local) && Same(item.Kind, kind)); }
        private static JObject ReadS0bJson(W24S5ProductionGateResult gate, JObject artifact, string code, string field) { if (artifact == null) return null; var file = ReadArtifact(gate, (string)artifact["path"], (string)artifact["sha256"], field, code); if (file == null || file.Text == null) return null; try { return Parse(file.Text); } catch (Exception e) { gate.Error(code, field, "S0b sealed JSON is invalid: " + e.Message); return null; } }
        private static bool S0bError(W24S5ProductionGateResult gate, string code, string path, string message) { if (gate != null) gate.Error(code, path, message); return false; }

        // The recorder deliberately writes raw passes, metrics input, and the CLI report before
        // final metadata/sealing.  This verifier closes that DAG without asking the Python tool
        // to consume self-referential capture metadata.
        private static bool VerifyTypedMetricsDag(W24S5ProductionGateResult gate, JObject metadata, CandidateContext context)
        {
            var raws = (metadata["typedRawDiagnostics"] as JArray ?? new JArray()).OfType<JObject>().ToArray();
            var inputs = (metadata["metricInputs"] as JArray ?? new JArray()).OfType<JObject>().ToArray();
            var reports = (metadata["metricReports"] as JArray ?? new JArray()).OfType<JObject>().ToArray();
            if (raws.Length == 0 && inputs.Length == 0 && reports.Length == 0) return ValidateTypedMetricsPresence(gate, context.Contract, raws.Length, inputs.Length, reports.Length);
            if (!ValidateTypedMetricsPresence(gate, context.Contract, raws.Length, inputs.Length, reports.Length)) return false;
            var passEncodings = ReadDeclaredDiagnosticPasses(gate, context);
            var rawByLocal = new Dictionary<string, JObject>(StringComparer.Ordinal);
            foreach (var raw in raws)
            {
                var local = (string)raw["file"]; var hash = (string)raw["sha256"]; var pass = (string)raw["passId"]; var encoding = (string)raw["encoding"]; var observed = raw["observedPlayerLoop"] as JObject;
                if (!SafeLocalDiagnosticFile(local) || !W24Hash.IsCanonical(hash) || !ProtocolToken(pass) || !ProtocolToken(encoding) || !passEncodings.TryGetValue(pass, out var declaredEncoding) || !Same(declaredEncoding, encoding) || observed == null || (long?)observed["serial"] <= 0 || (long?)observed["frame"] < 0 || (long?)observed["logicalFrameIndex"] < 0 || (long?)observed["seed"] < 0 || !ProtocolToken((string)observed["viewId"]) || string.IsNullOrWhiteSpace((string)raw["derivedFrom"]) || rawByLocal.ContainsKey(local)) { gate.Error("W24S5-C141", "typedRawDiagnostics", "Typed raw diagnostics require a declared unique diagnostics/ path/hash/pass/encoding and complete PlayerLoop seed/view/frame/derived-from provenance."); continue; }
                var artifact = context.Artifacts.SingleOrDefault(value => Same(CaptureLocal(value.Path, context.EffectId), local));
                if (artifact == null || !Same(artifact.Hash, hash) || !Same(artifact.Kind, "diagnostic") || !Same(artifact.PassId, pass) || !Same(artifact.Encoding, encoding)) gate.Error("W24S5-C142", "typedRawDiagnostics", "Typed raw diagnostic does not exactly resolve to formal metadata artifact pass/encoding/hash.");
                rawByLocal.Add(local, raw);
            }
            var inputByLocal = new Dictionary<string, JObject>(StringComparer.Ordinal);
            foreach (var inputRecord in inputs)
            {
                var local = (string)inputRecord["file"]; var inputHash = (string)inputRecord["sha256"]; var expectedTool = (string)inputRecord["expectedToolSha256"]; var environmentHash = (string)inputRecord["metricsEnvironmentSha256"];
                if (!SafeLocalDiagnosticFile(local) || !W24Hash.IsCanonical(inputHash) || !W24Hash.IsCanonical(expectedTool) || !W24Hash.IsCanonical(environmentHash) || inputByLocal.ContainsKey(local)) { gate.Error("W24S5-C143", "metricInputs", "Metrics input records require unique diagnostics/ path and canonical input/tool/environment hashes."); continue; }
                var artifact = context.Artifacts.SingleOrDefault(value => Same(CaptureLocal(value.Path, context.EffectId), local));
                if (artifact == null || !Same(artifact.Hash, inputHash) || !Same(artifact.Kind, "metrics-input")) { gate.Error("W24S5-C144", "metricInputs", "Metrics input is absent from the sealed formal artifact set."); continue; }
                inputByLocal.Add(local, inputRecord);
            }
            foreach (var reportRecord in reports)
            {
                var local = (string)reportRecord["file"]; var reportHash = (string)reportRecord["sha256"]; var inputLocal = (string)reportRecord["inputFile"]; var inputFileHash = (string)reportRecord["inputFileSha256"]; var analysisInputHash = (string)reportRecord["analysisInputSha256"]; var expectedTool = (string)reportRecord["expectedToolSha256"];
                if (!SafeLocalDiagnosticFile(local) || !W24Hash.IsCanonical(reportHash) || !W24Hash.IsCanonical(inputFileHash) || !W24Hash.IsCanonical(analysisInputHash) || !W24Hash.IsCanonical(expectedTool) || !Same((string)reportRecord["passId"], "metrics-report") || !Same((string)reportRecord["encoding"], "json") || !inputByLocal.TryGetValue(inputLocal ?? string.Empty, out var inputRecord) || !Same((string)inputRecord["sha256"], inputFileHash) || !Same((string)inputRecord["expectedToolSha256"], expectedTool)) { gate.Error("W24S5-C145", "metricReports", "Metrics report must bind exactly one sealed recorder-written input and frozen tool hash."); continue; }
                var artifact = context.Artifacts.SingleOrDefault(value => Same(CaptureLocal(value.Path, context.EffectId), local));
                if (artifact == null || !Same(artifact.Hash, reportHash) || !Same(artifact.Kind, "diagnostic") || !Same(artifact.PassId, "metrics-report") || !Same(artifact.Encoding, "json")) { gate.Error("W24S5-C146", "metricReports", "Metrics report cannot resolve to its sealed typed formal artifact."); continue; }
                var inputArtifact = context.Artifacts.Single(value => Same(CaptureLocal(value.Path, context.EffectId), inputLocal));
                VerifyMetricsInputAndReport(gate, context, rawByLocal, inputArtifact, expectedTool, (string)inputRecord["metricsEnvironmentSha256"], analysisInputHash, artifact, out var parsed);
                if (parsed != null)
                {
                    if (context.MetricReports.ContainsKey(artifact.Path)) gate.Error("W24S5-C156", "metricReports", "Metrics report artifact may not be replayed.");
                    else context.MetricReports.Add(artifact.Path, parsed);
                }
            }
            return !gate.HasErrors;
        }

        private static bool ContractDeclaresTypedMetrics(VfxDesignContract contract)
        {
            return contract != null && DeclaresTypedMetrics(contract.Extensions);
        }

        private static bool ValidateTypedMetricsPresence(W24S5ProductionGateResult gate, VfxDesignContract contract, int rawCount, int inputCount, int reportCount)
        {
            if (rawCount == 0 && inputCount == 0 && reportCount == 0)
            {
                if (!ContractDeclaresTypedMetrics(contract)) return true; // Genuine pre-typed S0a/S0b compatibility only.
                gate.Error("W24S5-C140", "typedMetrics", "A Contract declaring typedDiagnostics, requiredEvidenceMatrix, or metricPlan requires raw diagnostics, a recorder-written input, and a recorder-written report."); return false;
            }
            if (rawCount == 0 || inputCount == 0 || reportCount == 0) { gate.Error("W24S5-C140", "typedMetrics", "Typed metrics evidence requires raw diagnostics, a recorder-written input, and a recorder-written report."); return false; }
            return true;
        }

        private static bool DeclaresTypedMetrics(JToken token)
        {
            var objectValue = token as JObject;
            if (objectValue == null) return false;
            foreach (var property in objectValue.Properties())
            {
                if (Same(property.Name, "typedDiagnostics") || Same(property.Name, "requiredEvidenceMatrix") || Same(property.Name, "metricPlan")) return true;
                if (DeclaresTypedMetrics(property.Value)) return true;
            }
            return false;
        }

        private static Dictionary<string, string> ReadDeclaredDiagnosticPasses(W24S5ProductionGateResult gate, CandidateContext context)
        {
            var output = new Dictionary<string, string>(StringComparer.Ordinal);
            var artifact = context.Artifacts.SingleOrDefault(value => Same(value.Kind, "diagnostic-pass-manifest"));
            if (artifact == null) { gate.Error("W24S5-C157", "diagnosticPassManifest", "Typed metrics requires the sealed diagnostic pass manifest."); return output; }
            try
            {
                var root = Parse(File.ReadAllText(RepositoryAbsolute(artifact.Path)));
                foreach (var pass in (root["passes"] as JArray ?? new JArray()).OfType<JObject>())
                {
                    var id = (string)pass["passId"]; var encoding = (string)pass["encoding"];
                    if (!ProtocolToken(id) || !ProtocolToken(encoding) || output.ContainsKey(id)) { gate.Error("W24S5-C158", "diagnosticPassManifest", "Diagnostic pass manifest has missing or duplicate pass/encoding declarations."); }
                    else output.Add(id, encoding);
                }
            }
            catch (Exception e) { gate.Error("W24S5-C159", "diagnosticPassManifest", "Diagnostic pass manifest is unreadable: " + e.Message); }
            return output;
        }

        private static void VerifyMetricsInputAndReport(W24S5ProductionGateResult gate, CandidateContext context, Dictionary<string, JObject> rawByLocal, Artifact input, string expectedTool, string claimedEnvironmentHash, string claimedAnalysisInputHash, Artifact report, out MetricReport parsed)
        {
            parsed = null;
            JObject inputRoot, reportRoot;
            try { inputRoot = Parse(File.ReadAllText(RepositoryAbsolute(input.Path))); reportRoot = Parse(File.ReadAllText(RepositoryAbsolute(report.Path))); }
            catch (Exception e) { gate.Error("W24S5-C147", "typedMetrics", "Metrics input/report JSON is invalid: " + e.Message); return; }
            var contractBundlePath = context.Contract.Extensions == null ? null : (string)context.Contract.Extensions["captureToolBundle"];
            if (!Same((string)inputRoot["schema"], "w24-render-metrics-input/v1") || !Same((string)inputRoot["effectId"], context.EffectId) || !Same((string)inputRoot["candidateId"], C0) || (int?)inputRoot["contractRevision"] != context.Contract.ContractRevision || !Same((string)inputRoot["contractSha256"], context.Contract.ContractHash) || !Same((string)inputRoot["captureProfileSha256"], context.CaptureProfileHash) || !Same((string)inputRoot["recorderCaptureProfileSha256"], context.RecorderCaptureProfileHash) || !Same((string)inputRoot["expectedToolSha256"], expectedTool) || !Same((string)inputRoot["captureToolBundlePath"], contractBundlePath) || !Same((string)inputRoot["captureToolBundleSha256"], context.Contract.CaptureProfile.CaptureToolHash)) { gate.Error("W24S5-C148", "metricInput", "Metrics input does not separately bind the exact effect/candidate/Contract revision/hash/Contract profile/recorder profile/tool-bundle identity."); return; }
            if (!VerifyMetricsToolBundle(gate, context, contractBundlePath, expectedTool)) return;
            if (!VerifyMetricsEnvironment(gate, context, inputRoot["metricsEnvironment"] as JObject, claimedEnvironmentHash)) return;
            var registry = (inputRoot["evidence"] as JArray ?? new JArray()).OfType<JObject>().ToArray();
            var ids = new HashSet<string>(StringComparer.Ordinal); var locals = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in registry)
            {
                var id = (string)item["id"]; var local = (string)item["path"]; var hash = (string)item["sha256"]; var pass = (string)item["passId"]; var encoding = (string)item["encoding"];
                JObject raw; var foundRaw = rawByLocal.TryGetValue(local ?? string.Empty, out raw); var observed = foundRaw ? raw["observedPlayerLoop"] as JObject : null;
                if (string.IsNullOrEmpty(id) || !ids.Add(id) || !SafeLocalDiagnosticFile(local) || !locals.Add(local) || !foundRaw || !Same((string)raw["sha256"], hash) || !Same((string)raw["passId"], pass) || !Same((string)raw["encoding"], encoding) || observed == null || (long?)item["seed"] != (long?)observed["seed"] || (long?)item["logicalFrameIndex"] != (long?)observed["logicalFrameIndex"] || (long?)item["playerLoopSerial"] != (long?)observed["serial"] || (long?)item["playerLoopFrame"] != (long?)observed["frame"] || !JToken.DeepEquals(item["playerLoopTime"], observed["time"]) || !Same((string)item["viewId"], (string)observed["viewId"]) || !Same((string)item["derivedFrom"], (string)raw["derivedFrom"])) { gate.Error("W24S5-C149", "metricInput.evidence", "Metrics registry must bijectively identify sealed typed raw diagnostics and their seed/view provenance."); }
            }
            if (ids.Count != registry.Length || locals.Count != registry.Length) return;
            if (locals.Count != rawByLocal.Count || rawByLocal.Keys.Any(value => !locals.Contains(value))) gate.Error("W24S5-C150", "metricInput.evidence", "Metrics input cannot omit a sealed typed raw diagnostic (including a failed seed/view).");
            var checks = (inputRoot["checks"] as JArray ?? new JArray()).OfType<JObject>().ToArray();
            if (checks.Length == 0 || checks.Any(value => !ProtocolToken((string)value["id"]))) gate.Error("W24S5-C151", "metricInput.checks", "Metrics input requires unique protocol-named checks.");
            var checkIds = new HashSet<string>(checks.Select(value => (string)value["id"]), StringComparer.Ordinal);
            if (checkIds.Count != checks.Length) { gate.Error("W24S5-C151", "metricInput.checks", "Metrics input checks may not repeat IDs."); return; }
            var consumedEvidence = new HashSet<string>(StringComparer.Ordinal);
            foreach (var check in checks) CollectMetricEvidenceReferences(check, consumedEvidence);
            if (!ids.All(consumedEvidence.Contains)) gate.Error("W24S5-C152", "metricInput.checks", "Every typed raw registry item must be consumed by a frozen metrics check.");
            var inputCanonical = Hash(Utf8(CanonicalJson(inputRoot)));
            var matrix = inputRoot["requiredEvidenceMatrix"] as JArray;
            if (matrix == null || matrix.Count == 0 || !Same((string)inputRoot["requiredEvidenceMatrixSha256"], Hash(Utf8(CanonicalJson(matrix)))) || !VerifyRequiredEvidenceMatrix(gate, context, rawByLocal, registry, matrix)) return;
            if (!Same(inputCanonical, claimedAnalysisInputHash) || !Same((string)reportRoot["inputSha256"], inputCanonical) || !Same((string)reportRoot["toolSha256"], expectedTool) || !Same((string)reportRoot["route"], "MEASURED") || (bool?)reportRoot["machineGatesPassed"] != true) { gate.Error("W24S5-C153", "metricReport", "Metrics report input/tool identity, route, or gate result is invalid."); return; }
            var cloned = (JObject)reportRoot.DeepClone(); var claimedSeal = (string)cloned["sealedReportHash"]; cloned.Remove("sealedReportHash");
            if (!Same((string)cloned["sealedReportEncoding"], W24TypedBinaryCanonicalEncoding.EncodingName) || !W24TypedBinaryCanonicalEncoding.Verify(claimedSeal, cloned)) { gate.Error("W24S5-C154", "metricReport.sealedReportHash", "Metrics report must carry the required typed encoding and matching typed self-seal."); return; }
            var inputKinds = checks.ToDictionary(value => (string)value["id"], value => (string)value["kind"], StringComparer.Ordinal);
            var resultChecks = (reportRoot["checks"] as JArray ?? new JArray()).OfType<JObject>().ToArray(); var resultIds = new HashSet<string>(resultChecks.Select(value => (string)value["id"]), StringComparer.Ordinal);
            if (resultIds.Count != resultChecks.Length || !resultIds.SetEquals(checkIds) || resultChecks.Any(value => (bool?)value["pass"] != true || !inputKinds.TryGetValue((string)value["id"], out var kind) || !Same(kind, (string)value["kind"]))) { gate.Error("W24S5-C155", "metricReport.checks", "Metrics report must contain exactly the frozen passing checks with the same IDs and kinds."); return; }
            var requirementChecks = BuildFrozenRequirementChecks(gate, context, checks);
            if (gate.HasErrors) return;
            parsed = new MetricReport { InputHash = inputCanonical, ToolHash = expectedTool };
            foreach (var checkId in resultIds) parsed.PassingChecks.Add(checkId);
            foreach (var pair in inputKinds) parsed.CheckKinds.Add(pair.Key, pair.Value);
            foreach (var pair in requirementChecks) parsed.RequirementChecks.Add(pair.Key, pair.Value);
        }

        private static void CollectMetricEvidenceReferences(JObject check, HashSet<string> output)
        {
            var kind = (string)check["kind"];
            switch (kind)
            {
                case "mask_steady": case "autocorrelation": case "fragment_tracks": CollectStringValues(check["frames"], output); break;
                case "trail": CollectStringValues(check["trail"], output); CollectStringValues(check["previous"], output); CollectStringValues(check["headNewSpace"], output); break;
                case "receiver_luminance": case "receiver_luminance_ldr": CollectStringValues(check["on"], output); CollectStringValues(check["off"], output); CollectStringValues(check["receiverIds"], output); CollectStringValues(check["effectMask"], output); break;
                case "multiview_3d": CollectStringValues(check["views"], output); break;
                case "cleanup": case "transition": CollectStringValues(check["baselineLayers"], output); CollectStringValues(check["afterLayers"], output); CollectStringValues(check["beforeLayers"], output); CollectStringValues(check["anchorsBefore"], output); CollectStringValues(check["anchorsAfter"], output); break;
                default: break;
            }
        }

        private static bool VerifyMetricsEnvironment(W24S5ProductionGateResult gate, CandidateContext context, JObject environment, string claimedEnvironmentHash)
        {
            var frozen = context.Contract.Extensions == null ? null : context.Contract.Extensions.SelectToken("typedDiagnostics.metricsEnvironment") as JObject;
            if (environment == null || frozen == null || !Same(CanonicalJson(environment), CanonicalJson(frozen))) { gate.Error("W24S5-C178", "metricInput.metricsEnvironment", "Metrics input must exactly equal the Contract-frozen Python/NumPy/Pillow environment identity."); return false; }
            var body = (JObject)environment.DeepClone(); var embedded = (string)body["environmentSha256"]; body.Remove("environmentSha256");
            if (!W24Hash.IsCanonical(embedded) || !Same(embedded, claimedEnvironmentHash) || !Same(embedded, Hash(Utf8(CanonicalJson(body))))) { gate.Error("W24S5-C179", "metricInput.metricsEnvironment", "Metrics environment canonical self-hash or recorder metadata binding is invalid."); return false; }
            try
            {
                var observed = W24MetricsEvidenceDag.ProbeMetricsEnvironmentForInput((string)environment["pythonExecutablePath"]);
                if (!Same(CanonicalJson(observed), CanonicalJson(environment))) { gate.Error("W24S5-C180", "metricInput.metricsEnvironment", "Current Python executable bytes/version or NumPy/Pillow versions differ from the frozen environment."); return false; }
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is InvalidOperationException || e is ArgumentException)
            {
                gate.Error("W24S5-C180", "metricInput.metricsEnvironment", "Metrics environment cannot be independently replayed: " + e.Message); return false;
            }
            return true;
        }

        private static Dictionary<string, HashSet<string>> BuildFrozenRequirementChecks(W24S5ProductionGateResult gate, CandidateContext context, JObject[] checks)
        {
            var output = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var typed = context.Contract.Extensions == null ? null : context.Contract.Extensions["typedDiagnostics"] as JObject;
            var assigned = new HashSet<string>(StringComparer.Ordinal);
            if (typed == null) { gate.Error("W24S5-C181", "typedDiagnostics", "Frozen metric checks require the Contract typedDiagnostics mapping."); return output; }
            foreach (var property in typed.Properties())
            {
                var block = property.Value as JObject; var plan = block == null ? null : block["metricPlan"] as JObject; var kind = plan == null ? null : (string)plan["kind"];
                if (!ProtocolToken(kind)) continue;
                var matching = checks.Where(check => Same((string)check["kind"], kind)).ToArray();
                if (matching.Length == 0) { gate.Error("W24S5-C182", "metricInput.checks", "Contract metric plan has no frozen input check: " + property.Name); continue; }
                var requirementIds = new List<string>(); var single = (string)block["requirementId"];
                if (!string.IsNullOrEmpty(single)) requirementIds.Add(single);
                requirementIds.AddRange((block["requirementIds"] as JArray ?? new JArray()).Values<string>().Where(value => !string.IsNullOrEmpty(value)));
                requirementIds = requirementIds.Distinct(StringComparer.Ordinal).ToList();
                if (requirementIds.Count == 1)
                {
                    foreach (var check in matching) AssignFrozenRequirementCheck(gate, output, assigned, requirementIds[0], (string)check["id"]);
                    continue;
                }
                var receiverIds = (block["receiverIds"] as JArray ?? new JArray()).OfType<JObject>().ToArray();
                var explicitMappings = (block["perRequirementCheckMapping"] as JArray ?? new JArray()).OfType<JObject>().ToArray();
                if (explicitMappings.Length > 0)
                {
                    var declared = new HashSet<string>(requirementIds, StringComparer.Ordinal);
                    var mapped = new HashSet<string>(StringComparer.Ordinal);
                    var declaredReceivers = new HashSet<int>(receiverIds.Select(value => (int?)value["id"]).Where(value => value.HasValue).Select(value => value.Value));
                    foreach (var mapping in explicitMappings)
                    {
                        var requirementId = (string)mapping["requirementId"];
                        var mappedReceivers = (mapping["receiverIds"] as JArray ?? new JArray()).Values<int>().ToArray();
                        if (string.IsNullOrEmpty(requirementId) || !declared.Contains(requirementId) || !mapped.Add(requirementId)
                            || mappedReceivers.Length == 0 || mappedReceivers.Distinct().Count() != mappedReceivers.Length
                            || mappedReceivers.Any(value => !declaredReceivers.Contains(value)))
                        {
                            gate.Error("W24S5-C183", "typedDiagnostics." + property.Name, "Explicit per-requirement metric mapping must cover each declared requirement once with known receiver IDs.");
                            continue;
                        }
                        var acceptedReceivers = new HashSet<int>(mappedReceivers);
                        var perRequirement = matching.Where(check => ((int?)check["receiverId"]).HasValue && acceptedReceivers.Contains((int)check["receiverId"])).ToArray();
                        if (perRequirement.Length == 0) { gate.Error("W24S5-C184", "metricInput.checks", "No frozen check resolves Contract receiver requirement " + requirementId + "."); continue; }
                        foreach (var check in perRequirement) AssignFrozenRequirementCheck(gate, output, assigned, requirementId, (string)check["id"]);
                    }
                    if (!declared.SetEquals(mapped)) gate.Error("W24S5-C183", "typedDiagnostics." + property.Name, "Explicit per-requirement metric mapping must cover the exact declared requirement set.");
                    continue;
                }
                if (requirementIds.Count == 0 || receiverIds.Length != requirementIds.Count)
                {
                    gate.Error("W24S5-C183", "typedDiagnostics." + property.Name, "A multi-requirement metric plan needs an unambiguous Contract-frozen per-requirement check mapping.");
                    continue;
                }
                for (var index = 0; index < requirementIds.Count; index++)
                {
                    var receiverId = (int?)receiverIds[index]["id"];
                    var perRequirement = receiverId.HasValue ? matching.Where(check => (int?)check["receiverId"] == receiverId).ToArray() : Array.Empty<JObject>();
                    if (perRequirement.Length == 0) { gate.Error("W24S5-C184", "metricInput.checks", "No frozen check resolves Contract receiver requirement " + requirementIds[index] + "."); continue; }
                    foreach (var check in perRequirement) AssignFrozenRequirementCheck(gate, output, assigned, requirementIds[index], (string)check["id"]);
                }
            }
            var checkIds = new HashSet<string>(checks.Select(check => (string)check["id"]), StringComparer.Ordinal);
            if (!checkIds.SetEquals(assigned)) gate.Error("W24S5-C185", "metricInput.checks", "Every frozen input check must map to at least one explicitly frozen Contract requirement metric plan.");
            return output;
        }

        private static void AssignFrozenRequirementCheck(W24S5ProductionGateResult gate, Dictionary<string, HashSet<string>> output, HashSet<string> assigned, string requirementId, string checkId)
        {
            if (string.IsNullOrEmpty(requirementId) || !ProtocolToken(checkId)) { gate.Error("W24S5-C186", "metricInput.checks", "A frozen requirement/check mapping is invalid."); return; }
            if (!output.TryGetValue(requirementId, out var ids)) output.Add(requirementId, ids = new HashSet<string>(StringComparer.Ordinal));
            if (!ids.Add(checkId)) { gate.Error("W24S5-C186", "metricInput.checks", "A frozen requirement cannot claim the same check more than once."); return; }
            assigned.Add(checkId);
        }

        private static bool VerifyMetricsToolBundle(W24S5ProductionGateResult gate, CandidateContext context, string relative, string expectedTool)
        {
            if (!SafeToolBundlePath(relative)) { gate.Error("W24S5-C165", "captureToolBundle", "Contract capture-tool bundle path is unsafe or noncanonical."); return false; }
            var absolute = RepositoryAbsolute(relative); var root = RepositoryRoot();
            if (!File.Exists(absolute) || HasReparsePointAtOrAbove(absolute, root)) { gate.Error("W24S5-C166", "captureToolBundle", "Capture-tool bundle is missing or reparse-backed."); return false; }
            JObject bundle;
            try { bundle = Parse(File.ReadAllText(absolute, new UTF8Encoding(false, true))); }
            catch (Exception e) { gate.Error("W24S5-C167", "captureToolBundle", "Capture-tool bundle is invalid: " + e.Message); return false; }
            if (!Same(Hash(Utf8(CanonicalJson(bundle))), context.Contract.CaptureProfile.CaptureToolHash)) { gate.Error("W24S5-C168", "captureToolBundle", "Bundle canonical hash differs from Contract captureToolHash."); return false; }
            var sources = (bundle["sources"] as JArray ?? new JArray()).OfType<JObject>().ToArray(); var seen = new HashSet<string>(StringComparer.Ordinal); var metricsCount = 0;
            foreach (var source in sources)
            {
                var path = (string)source["path"]; var claimed = (string)source["sha256"];
                if (!SafeRepositorySource(path) || !W24Hash.IsCanonical(claimed) || !seen.Add(path)) { gate.Error("W24S5-C169", "captureToolBundle.sources", "Bundle contains an unsafe, duplicate, or unhashed source."); return false; }
                var sourceAbsolute = RepositoryAbsolute(path);
                if (!File.Exists(sourceAbsolute) || HasReparsePointAtOrAbove(sourceAbsolute, root) || !Same(Hash(File.ReadAllBytes(sourceAbsolute)), claimed)) { gate.Error("W24S5-C170", "captureToolBundle.sources", "Bundle source bytes drifted: " + path); return false; }
                if (Same(path, "tools/vfx/metrics/render_metrics.py")) { metricsCount++; if (!Same(claimed, expectedTool)) { gate.Error("W24S5-C171", "captureToolBundle.sources", "expectedTool is not the metrics source frozen in the bundle."); return false; } }
            }
            var declaredMetrics = context.Contract.Extensions.SelectToken("typedDiagnostics.metricsTool") as JObject;
            if (metricsCount != 1 || declaredMetrics == null || !Same((string)declaredMetrics["path"], "tools/vfx/metrics/render_metrics.py") || !Same((string)declaredMetrics["sha256"], expectedTool)) { gate.Error("W24S5-C172", "typedDiagnostics.metricsTool", "Contract and bundle must uniquely bind the same metrics tool source/hash."); return false; }
            return true;
        }
        private static bool SafeToolBundlePath(string value) { return SafeRepositorySource(value) && value.StartsWith("docs/vfx-contracts/capture-tools/", StringComparison.Ordinal) && value.EndsWith(".bundle.json", StringComparison.Ordinal); }
        private static bool SafeRepositorySource(string value) { return !string.IsNullOrWhiteSpace(value) && !Path.IsPathRooted(value) && value.IndexOf('\\') < 0 && value.Split('/').All(part => !string.IsNullOrEmpty(part) && part != "." && part != ".."); }
        private static bool VerifyRequiredEvidenceMatrix(W24S5ProductionGateResult gate, CandidateContext context, Dictionary<string, JObject> rawByLocal, JObject[] registry, JArray matrix)
        {
            var contractMatrix = context.Contract.Extensions == null ? null : context.Contract.Extensions.SelectToken("typedDiagnostics.requiredEvidenceMatrix") as JArray;
            if (contractMatrix == null || contractMatrix.Count == 0 || !Same(CanonicalJson(matrix), CanonicalJson(contractMatrix))) { gate.Error("W24S5-C160", "requiredEvidenceMatrix", "Metrics matrix must exactly equal the Contract's frozen typedDiagnostics.requiredEvidenceMatrix."); return false; }
            var rows = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in matrix.OfType<JObject>())
            {
                var evidenceId = (string)row["evidenceId"]; var pass = (string)row["passId"]; var seed = (long?)row["seed"]; var view = (string)row["viewId"]; var frame = (long?)row["logicalFrameIndex"];
                if (!ProtocolToken(evidenceId) || !ProtocolToken(pass) || !seed.HasValue || !ProtocolToken(view) || !frame.HasValue || frame.Value < 0 || !rows.Add(evidenceId)) { gate.Error("W24S5-C161", "requiredEvidenceMatrix", "Required matrix has an invalid or duplicate evidenceId row."); return false; }
            }
            var registryById = registry.ToDictionary(item => (string)item["id"], item => item, StringComparer.Ordinal);
            if (!rows.SetEquals(registryById.Keys)) { gate.Error("W24S5-C162", "requiredEvidenceMatrix", "Required matrix and input evidence registry must have an exact evidenceId set."); return false; }
            foreach (var row in matrix.OfType<JObject>())
            {
                var entry = registryById[(string)row["evidenceId"]]; var local = (string)entry["path"]; JObject raw;
                if (!rawByLocal.TryGetValue(local, out raw)) { gate.Error("W24S5-C163", "requiredEvidenceMatrix", "Matrix evidenceId has no sealed typed raw diagnostic."); return false; }
                var observed = raw["observedPlayerLoop"] as JObject;
                if (observed == null || !Same((string)row["passId"], (string)raw["passId"]) || (long?)row["seed"] != (long?)observed["seed"] || !Same((string)row["viewId"], (string)observed["viewId"]) || (long?)row["logicalFrameIndex"] != (long?)observed["logicalFrameIndex"]) { gate.Error("W24S5-C164", "requiredEvidenceMatrix", "Matrix row does not exactly resolve to its sealed typed raw pass/seed/view/frame."); return false; }
            }
            return true;
        }
        private static void CollectStringValues(JToken token, HashSet<string> output) { if (token is JValue value && value.Type == JTokenType.String) output.Add((string)value); else if (token is JContainer container) foreach (var child in container.Children()) CollectStringValues(child, output); }
        private static bool SafeLocalCaptureFile(string value) { return !string.IsNullOrWhiteSpace(value) && !Path.IsPathRooted(value) && value.IndexOf('\\') < 0 && value.Split('/').All(part => !string.IsNullOrEmpty(part) && part != "." && part != ".."); }
        private static bool SafeLocalDiagnosticFile(string value) { return SafeLocalCaptureFile(value) && value.StartsWith("diagnostics/", StringComparison.Ordinal); }
        private static bool ProtocolToken(string value) { return !string.IsNullOrEmpty(value) && value.All(character => char.IsLower(character) || char.IsDigit(character) || character == '-' || character == '_' || character == '.'); }
        private static string CaptureLocal(string formalPath, string effectId)
        {
            var prefix = ArtifactRoot + effectId + "/C0/";
            return formalPath != null && formalPath.StartsWith(prefix, StringComparison.Ordinal) ? formalPath.Substring(prefix.Length) : null;
        }
        private static string CanonicalJson(JToken value)
        {
            if (value is JObject obj) { var sorted = new JObject(); foreach (var property in obj.Properties().OrderBy(item => item.Name, StringComparer.Ordinal)) sorted.Add(property.Name, JToken.Parse(CanonicalJson(property.Value))); return sorted.ToString(Formatting.None); }
            if (value is JArray array) return new JArray(array.Select(item => JToken.Parse(CanonicalJson(item)))).ToString(Formatting.None);
            return value.ToString(Formatting.None);
        }

        private static bool SamePlanShape(JObject candidate, JObject completed)
        {
            var a = (JObject)candidate.DeepClone(); var b = (JObject)completed.DeepClone();
            foreach (var root in new[] { a, b }) { root.Remove("traceStatus"); root.Remove("candidateRevision"); root.Remove("evidenceRevision"); root.Remove("candidateReceiptPath"); root.Remove("candidateReceiptFileHash"); root.Remove("captureMetadataPath"); root.Remove("captureMetadataFileHash"); root.Remove("evidenceTransitionReceiptPath"); root.Remove("evidenceTransitionReceiptFileHash"); root.Remove("completedTraceNormalizedSha256"); foreach (var item in ((JArray)root["requirementTraces"] ?? new JArray()).OfType<JObject>()) { item.Remove("authorityEvidence"); item.Remove("crossEvidence"); } }
            return JToken.DeepEquals(a, b);
        }
        private static string NormalizedCompletedTraceHash(JObject trace)
        {
            var normalized = (JObject)trace.DeepClone();
            normalized.Remove("evidenceTransitionReceiptFileHash");
            normalized.Remove("completedTraceNormalizedSha256");
            return Hash(Utf8(Serialize(normalized)));
        }

        private static bool AllEvidenceMapsToCapturedArtifacts(W24S5ProductionGateResult gate, VfxImplementationTrace trace, CandidateContext context)
        {
            var artifacts = context.Artifacts;
            VerifyTypedDiagnosticRequirementCoverage(gate, trace, context);
            foreach (var requirement in trace.RequirementTraces ?? Array.Empty<VfxRequirementTrace>())
            foreach (var evidence in (requirement.AuthorityEvidence ?? Array.Empty<VfxTraceEvidence>()).Concat(requirement.CrossEvidence ?? Array.Empty<VfxTraceEvidence>()))
            {
                if (evidence == null) { gate.Error("W24S5-C130", "implementationTrace.evidence", "Every evidence item must be present."); continue; }
                // Capture sealing establishes machine capture facts only.  A false/pending visual
                // QA or user item is deliberately not an artifact failure and cannot be promoted
                // here; those authorities are unavailable in this fail-closed build.
                if (!evidence.Passed && (Same(evidence.Kind, "visualQa") || Same(evidence.Kind, "user"))) continue;
                var artifact = artifacts.FirstOrDefault(item => Same(item.Path, evidence.Reference) && Same(item.Hash, evidence.Sha256) && Same(item.Kind, evidence.Kind));
                if (!evidence.Passed || artifact == null) { gate.Error("W24S5-C130", "implementationTrace.evidence", "Every non-pending authority/cross evidence item must pass and resolve to a hashed captured artifact of the same kind."); continue; }
                if (!string.IsNullOrEmpty(evidence.PassId) && (!Same(artifact.PassId, evidence.PassId) || !Same(artifact.Encoding, evidence.Encoding))) gate.Error("W24S5-C131", "implementationTrace.evidence", "Typed trace evidence passId/encoding does not match the sealed capture artifact.");
                if (!string.IsNullOrEmpty(evidence.MetricCheckId))
                {
                    MetricReport report;
                    if (!Same(evidence.PassId, "metrics-report") || !Same(evidence.Encoding, "json") || !context.MetricReports.TryGetValue(artifact.Path, out report) || !Same(evidence.AnalysisInputSha256, report.InputHash) || !report.PassingChecks.Contains(evidence.MetricCheckId)) gate.Error("W24S5-C132", "implementationTrace.evidence", "Metric trace evidence does not bind a sealed report, exact input hash, and passing frozen check.");
                }
            }
            return !gate.HasErrors;
        }

        private static void VerifyTypedDiagnosticRequirementCoverage(W24S5ProductionGateResult gate, VfxImplementationTrace trace, CandidateContext context)
        {
            var typed = context.Contract.Extensions == null ? null : context.Contract.Extensions["typedDiagnostics"] as JObject;
            var matrix = typed == null ? null : typed["requiredEvidenceMatrix"] as JArray;
            if (matrix == null || matrix.Count == 0) return; // S0a/S0b generic diagnostic compatibility.
            var requirements = (context.Contract.Requirements ?? Array.Empty<VfxDesignRequirement>()).ToDictionary(value => value.DesignRequirementId, value => value, StringComparer.Ordinal);
            var traces = (trace.RequirementTraces ?? Array.Empty<VfxRequirementTrace>()).Where(value => value != null && !string.IsNullOrEmpty(value.DesignRequirementId)).GroupBy(value => value.DesignRequirementId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var declaredKinds = VfxImplementationTraceValidator.TypedDiagnosticRequirementKinds(context.Contract);
            foreach (var requirement in requirements.Values)
            {
                if (!traces.TryGetValue(requirement.DesignRequirementId, out var requirementTrace)) continue;
                var expected = new HashSet<string>(StringComparer.Ordinal);
                foreach (var reportPair in context.MetricReports)
                {
                    if (!reportPair.Value.RequirementChecks.TryGetValue(requirement.DesignRequirementId, out var ids)) continue;
                    foreach (var id in ids) expected.Add(reportPair.Key + "\n" + id);
                }
                var authority = requirementTrace.AuthorityEvidence ?? Array.Empty<VfxTraceEvidence>(); var cross = requirementTrace.CrossEvidence ?? Array.Empty<VfxTraceEvidence>();
                var passedDiagnostics = authority.Concat(cross).Where(value => value != null && value.Passed && Same(value.Kind, "diagnostic")).ToArray();
                if (expected.Count == 0)
                {
                    if (passedDiagnostics.Length > 0) gate.Error("W24S5-C187", "implementationTrace." + requirement.DesignRequirementId, "A requirement with no Contract-frozen metric plan cannot use passed diagnostic evidence; generic summaries are supplemental only.");
                    if (Same(requirement.EvidenceAuthority, "diagnostic")) gate.Error("W24S5-C188", "implementationTrace." + requirement.DesignRequirementId, "Typed matrix Contract omitted the diagnostic authority requirement-to-metric-plan/check binding.");
                    continue;
                }
                if (!declaredKinds.TryGetValue(requirement.DesignRequirementId, out var kinds) || kinds.Count == 0) { gate.Error("W24S5-C188", "implementationTrace." + requirement.DesignRequirementId, "Sealed checks have no matching Contract requirement-to-metric-plan declaration."); continue; }
                var evidenceSet = Same(requirement.EvidenceAuthority, "diagnostic") ? authority : passedDiagnostics;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var evidence in evidenceSet)
                {
                    if (evidence == null || !Same(evidence.Kind, "diagnostic") || !Same(evidence.PassId, "metrics-report") || !Same(evidence.Encoding, "json") || !ProtocolToken(evidence.MetricCheckId) || !W24Hash.IsCanonical(evidence.AnalysisInputSha256))
                    {
                        gate.Error("W24S5-C189", "implementationTrace." + requirement.DesignRequirementId, "Every passed diagnostic authority/cross item must name a sealed metrics-report/json check and analysis-input hash.");
                        continue;
                    }
                    var artifact = context.Artifacts.FirstOrDefault(item => Same(item.Path, evidence.Reference) && Same(item.Hash, evidence.Sha256) && Same(item.Kind, "diagnostic") && Same(item.PassId, "metrics-report") && Same(item.Encoding, "json"));
                    if (artifact == null || !context.MetricReports.TryGetValue(artifact.Path, out var report) || !Same(evidence.AnalysisInputSha256, report.InputHash) || !report.PassingChecks.Contains(evidence.MetricCheckId) || !report.RequirementChecks.TryGetValue(requirement.DesignRequirementId, out var allowed) || !allowed.Contains(evidence.MetricCheckId))
                    {
                        gate.Error("W24S5-C190", "implementationTrace." + requirement.DesignRequirementId, "Diagnostic metricCheckId is not a passing frozen check assigned to this exact Contract requirement.");
                        continue;
                    }
                    if (!seen.Add(artifact.Path + "\n" + evidence.MetricCheckId)) gate.Error("W24S5-C191", "implementationTrace." + requirement.DesignRequirementId, "A frozen requirement check may be referenced exactly once.");
                }
                if (!seen.SetEquals(expected) || evidenceSet.Length != expected.Count) gate.Error("W24S5-C192", "implementationTrace." + requirement.DesignRequirementId, "Diagnostic authority/cross evidence must consume every and only the Contract-bound frozen metric checks exactly once.");
            }
        }

        private static W24S5PersistedFile ReadArtifact(W24S5ProductionGateResult gate, string path, string hash, string field, string code)
        {
            string absolute;
            if (!SafeArtifact(path, out absolute) || !W24Hash.IsCanonical(hash) || !File.Exists(absolute)) { gate.Error(code, field, "Capture artifact path/hash is invalid or missing."); return null; }
            var bytes = File.ReadAllBytes(absolute); var actual = Hash(bytes);
            if (!Same(actual, hash)) { gate.Error(code, field, "Capture artifact bytes do not match their recorded SHA-256."); return null; }
            try { return new W24S5PersistedFile { RelativePath = path, Hash = actual, Text = new UTF8Encoding(false, true).GetString(bytes) }; }
            catch (DecoderFallbackException) { return new W24S5PersistedFile { RelativePath = path, Hash = actual, Text = null }; }
        }

        private static bool SafeArtifact(string path, out string absolute)
        {
            absolute = null;
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.IndexOf('\\') >= 0 || !path.StartsWith(ArtifactRoot, StringComparison.Ordinal) || path.Split('/').Any(part => string.IsNullOrEmpty(part) || part == "." || part == "..")) return false;
            var root = RepositoryRoot(); var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            absolute = Path.GetFullPath(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
            return absolute.StartsWith(prefix, StringComparison.Ordinal) && !HasReparsePointAtOrAbove(absolute, root);
        }
        private static void WriteOnceDirectory(string relative, Dictionary<string, byte[]> files)
        {
            var root = RepositoryRoot(); var target = RepositoryAbsolute(relative); var parent = Path.GetDirectoryName(target);
            RejectReparsePoints(parent, Path.Combine(root, "docs"));
            Directory.CreateDirectory(parent); if (Directory.Exists(target) || File.Exists(target)) throw new IOException("Evidence transition is write-once and already exists: " + relative);
            var pending = Path.Combine(parent, ".evidence.pending-" + Guid.NewGuid().ToString("N"));
            try { Directory.CreateDirectory(pending); foreach (var pair in files) using (var stream = new FileStream(Path.Combine(pending, pair.Key), FileMode.CreateNew, FileAccess.Write, FileShare.None)) stream.Write(pair.Value, 0, pair.Value.Length); Directory.Move(pending, target); }
            finally { if (Directory.Exists(pending)) Directory.Delete(pending, true); }
        }
        private static string W24S5ProductionGatePath(string relative) { string ignored; if (!W24S5ProductionGate.TryResolvePersistedPath(relative, W24S5RecordScope.Formal, out ignored)) throw new InvalidDataException("Unsafe formal path."); return ignored; }
        private static string RepositoryRoot() { return Directory.GetParent(Directory.GetParent(UnityEngine.Application.dataPath).FullName).FullName; }
        private static string RepositoryAbsolute(string path) { return Path.GetFullPath(Path.Combine(RepositoryRoot(), path.Replace('/', Path.DirectorySeparatorChar))); }
        private static void RejectReparsePoints(string path, string boundary)
        {
            var stop = Path.GetFullPath(boundary).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            for (var current = new DirectoryInfo(Path.GetFullPath(path)); current != null; current = current.Parent)
            {
                if ((File.Exists(current.FullName) || Directory.Exists(current.FullName)) && (File.GetAttributes(current.FullName) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Evidence transition path contains a symlink/junction/reparse point: " + current.FullName);
                if (string.Equals(current.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), stop, StringComparison.OrdinalIgnoreCase)) break;
            }
        }
        private static bool HasReparsePointAtOrAbove(string path, string boundary)
        {
            try { RejectReparsePoints(path, boundary); return false; }
            catch (IOException) { return true; }
            catch (UnauthorizedAccessException) { return true; }
            catch (InvalidDataException) { return true; }
        }
        private static string ProjectAbsolute(string path) { return Path.GetFullPath(Path.Combine(Directory.GetParent(UnityEngine.Application.dataPath).FullName, path.Replace('/', Path.DirectorySeparatorChar))); }
        private static bool SafeAsset(string path) { return !string.IsNullOrWhiteSpace(path) && path.StartsWith("Assets/", StringComparison.Ordinal) && path.IndexOf("..", StringComparison.Ordinal) < 0 && path.IndexOf('\\') < 0; }
        private static JObject Parse(string text)
        {
            W24StrictJsonTextPreflight.Validate(text);
            using (var source = new StringReader(text ?? throw new ArgumentNullException("text")))
            using (var reader = new JsonTextReader(source) { FloatParseHandling = FloatParseHandling.Double, DateParseHandling = DateParseHandling.None })
            {
                var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                RejectNonFinite(root); RejectLoneSurrogates(root); return root;
            }
        }
        private static void RejectNonFinite(JToken token)
        {
            if (token is JValue value && token.Type == JTokenType.Float)
            {
                if (!(value.Value is double number) || double.IsNaN(number) || double.IsInfinity(number)) throw new InvalidDataException("JSON floating values must be finite binary64 doubles.");
            }
            foreach (var child in token.Children()) RejectNonFinite(child);
        }
        private static void RejectLoneSurrogates(JToken token)
        {
            var objectValue = token as JObject;
            if (objectValue != null) foreach (var property in objectValue.Properties()) ValidateStrictUtf8(property.Name);
            var value = token as JValue;
            if (value != null && token.Type == JTokenType.String) ValidateStrictUtf8((string)value);
            foreach (var child in token.Children()) RejectLoneSurrogates(child);
        }
        private static void ValidateStrictUtf8(string value)
        {
            try { new UTF8Encoding(false, true).GetBytes(value); }
            catch (EncoderFallbackException error) { throw new InvalidDataException("JSON string/property name contains a lone surrogate.", error); }
        }
        private static string Serialize(JToken token) { return token.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n"; }
        private static byte[] Utf8(string text) { return new UTF8Encoding(false, true).GetBytes(text); }
        private static string Hash(byte[] bytes) { return W24S5Hash.Sha256Bytes(bytes); }
        private static bool RawHash(string value) { return value != null && value.Length == 64 && value.All(character => (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')); }
        private static bool EffectId(string value) { return !string.IsNullOrEmpty(value) && char.IsLower(value[0]) && value.All(character => char.IsLower(character) || char.IsDigit(character) || character == '_') && !value.Contains("__") && value[value.Length - 1] != '_'; }
        private static bool Same(string a, string b) { return string.Equals(a, b, StringComparison.Ordinal); }
        private static void Copy(W24S5ProductionGateResult source, W24S5FormalEvidenceTransitionResult target) { foreach (var item in source.Issues.Where(item => item.IsError)) target.Error(item.Code + " " + item.Path + ": " + item.Message); }
    }

    /// <summary>
    /// Validates Unicode escapes before Json.NET can replace an unpaired surrogate
    /// with U+FFFD. General JSON grammar remains owned by JsonTextReader.
    /// </summary>
    internal static class W24StrictJsonTextPreflight
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal static void Validate(string text)
        {
            if (text == null) throw new ArgumentNullException("text");
            try { StrictUtf8.GetBytes(text); }
            catch (EncoderFallbackException error) { throw new InvalidDataException("JSON text contains a raw lone surrogate.", error); }

            var inString = false;
            for (var index = 0; index < text.Length; index++)
            {
                var character = text[index];
                if (!inString)
                {
                    if (character == '"') inString = true;
                    continue;
                }
                if (character == '"') { inString = false; continue; }
                if (character != '\\') continue;
                if (++index >= text.Length) break;
                if (text[index] != 'u') continue;

                var codeUnit = ParseEscapedCodeUnit(text, index + 1);
                index += 4;
                if (codeUnit >= 0xdc00 && codeUnit <= 0xdfff)
                    throw new InvalidDataException("JSON string contains an escaped lone low surrogate.");
                if (codeUnit < 0xd800 || codeUnit > 0xdbff) continue;

                var secondSlash = index + 1;
                if (secondSlash + 5 >= text.Length || text[secondSlash] != '\\' || text[secondSlash + 1] != 'u')
                    throw new InvalidDataException("JSON string contains an escaped lone high surrogate.");
                var low = ParseEscapedCodeUnit(text, secondSlash + 2);
                if (low < 0xdc00 || low > 0xdfff)
                    throw new InvalidDataException("JSON string contains an escaped high surrogate without a low-surrogate pair.");
                index = secondSlash + 5;
            }
        }

        private static int ParseEscapedCodeUnit(string text, int start)
        {
            if (start < 0 || start + 4 > text.Length) throw new InvalidDataException("JSON string contains an incomplete Unicode escape.");
            var value = 0;
            for (var offset = 0; offset < 4; offset++)
            {
                var character = text[start + offset];
                int digit;
                if (character >= '0' && character <= '9') digit = character - '0';
                else if (character >= 'a' && character <= 'f') digit = character - 'a' + 10;
                else if (character >= 'A' && character <= 'F') digit = character - 'A' + 10;
                else throw new InvalidDataException("JSON string contains an invalid Unicode escape.");
                value = (value << 4) | digit;
            }
            return value;
        }
    }
}
