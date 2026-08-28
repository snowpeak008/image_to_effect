using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.W24.S1;

namespace VFXComposer.Editor.W24.S5
{
    public enum W24S5BuildIntent { Development, Publication }
    public enum W24S5VisualStatus { VISUAL_PENDING, L3, L4, LEGACY }

    /// <summary>Public input contains persisted references, never caller-owned contract, trace, or L4 objects.</summary>
    public sealed class W24S5ProductionGateRequest
    {
        public string EffectId;
        public string ContractPath;
        public string ContractFileHash;
        public string TracePath;
        public string TraceFileHash;
        public string UserVerdictRecordPath;
        public string UserVerdictRecordHash;
        public string VisualQaRecordPath;
        public string VisualQaRecordHash;
        public string S0aStatusRecordPath;
        public string S0aStatusRecordHash;
        public string PlannedBuildHash;
        public string ExpectedRuntimeEntryPath;
        public string ExpectedManifestPath;
        public W24S5BuildIntent Intent;
        public W24S5VisualStatus VisualStatus = W24S5VisualStatus.VISUAL_PENDING;
    }

    public sealed class W24S5GateIssue { public string Code; public string Path; public string Message; public bool IsError; }

    public sealed class W24S5ProductionGateResult
    {
        private readonly List<W24S5GateIssue> issues = new List<W24S5GateIssue>();
        public IReadOnlyList<W24S5GateIssue> Issues { get { return issues; } }
        public bool HasErrors { get { return issues.Any(issue => issue.IsError); } }
        public bool CanBuild { get; internal set; }
        public bool CanPublish { get; internal set; }
        public W24S5VisualStatus EffectiveStatus { get; internal set; }
        internal W24S5FormalApproval Approval;
        internal W24S5FirstFormalBuildApproval FirstFormalApproval;
        internal void Error(string code, string path, string message) { issues.Add(new W24S5GateIssue { Code = code, Path = path, Message = message, IsError = true }); }
        internal void Warning(string code, string path, string message) { issues.Add(new W24S5GateIssue { Code = code, Path = path, Message = message, IsError = false }); }
    }

    // Deliberately fail-closed until a host-owned interactive user-signoff boundary can issue an
    // opaque authority. A repository record and claimed identity are evidence, not a signature.
    internal sealed class W24S5UserVerdictAuthority
    {
        // Reserved for a future host-owned issuer.  No constructor or repository reader can
        // create this authority in the current fail-closed build.
        internal readonly string EvidenceCorpusHash;
        internal readonly string EvidenceCorpusPath;
        internal readonly string EvidenceCorpusFileHash;
        internal readonly string DecisionRecordPath;
        internal readonly string DecisionRecordHash;
        private W24S5UserVerdictAuthority() { }
        internal static W24S5UserVerdictAuthority Verify(W24S5ProductionGateResult result, W24S5ProductionGateRequest request, VfxDesignContract contract, VfxImplementationTrace trace, W24S5PersistedFile traceFile)
        {
            result.Error("W24S5-076", "userVerdictRecord", "Automatic L4/publication is disabled until a host-owned opaque user-signoff authority is available; persisted repository records cannot grant L4.");
            return null;
        }
    }

    /// <summary>Fail-closed until gate-owned opaque Visual-QA and S0a issuers are available.</summary>
    internal sealed class W24S5VisualQaAuthority
    {
        // Reserved for a future gate-owned issuer; repository records cannot construct it.
        internal readonly string QaRecordPath, QaRecordHash, S0aStatusPath, S0aStatusHash;
        private W24S5VisualQaAuthority() { }
        internal static W24S5VisualQaAuthority Verify(W24S5ProductionGateResult result, W24S5ProductionGateRequest request, VfxDesignContract contract, VfxImplementationTrace trace, W24S5PersistedFile traceFile)
        {
            result.Error("W24S5-086", "visualQaRecord", "Automatic L3 is disabled until gate-owned opaque Visual-QA and S0a issuance authorities are available; repository JSON cannot issue either authority.");
            return null;
        }
    }

    internal sealed class W24S5PersistedFile { internal string RelativePath; internal string Hash; internal string Text; }
    internal enum W24S5RecordScope { Formal, Verdict, EvidenceCorpus, VisualQa, S0aStatus }
    internal sealed class W24S5OwnedOutputSnapshot
    {
        internal readonly bool RootExisted;
        internal readonly Dictionary<string, byte[]> Files;
        internal W24S5OwnedOutputSnapshot(bool rootExisted, Dictionary<string, byte[]> files) { RootExisted = rootExisted; Files = files; }
    }
    internal sealed class W24S5FormalApproval
    {
        internal readonly W24S5PersistedFile Contract;
        internal readonly W24S5PersistedFile Trace;
        internal readonly W24S5UserVerdictAuthority Verdict;
        internal readonly W24S5VisualQaAuthority VisualQa;
        internal readonly string RuntimeEntryPath;
        internal readonly string EffectId;
        internal readonly string PlannedBuildHash;
        internal readonly W24S5VisualStatus VisualStatus;
        internal readonly bool LegacyDevelopment;
        internal W24S5FormalApproval(object issuer, W24S5PersistedFile contractFile, W24S5PersistedFile traceFile, W24S5UserVerdictAuthority verdict, W24S5VisualQaAuthority visualQa, string effectId, string plannedBuildHash, string runtimeEntryPath, W24S5VisualStatus visualStatus = W24S5VisualStatus.VISUAL_PENDING, bool legacyDevelopment = false)
        {
            if (!legacyDevelopment && !W24S5ProductionGate.IsFormalApprovalIssuer(issuer)) throw new InvalidOperationException("Formal approvals may only be issued by the S5 gate.");
            Contract = contractFile; Trace = traceFile; Verdict = verdict; VisualQa = visualQa; EffectId = effectId; PlannedBuildHash = plannedBuildHash; RuntimeEntryPath = runtimeEntryPath; VisualStatus = visualStatus; LegacyDevelopment = legacyDevelopment;
        }
    }

    /// <summary>
    /// Internal-only pre-C0 request. This is deliberately separate from the public formal gate:
    /// it can authorize one identity-populating Development build, never evidence, QA, L3/L4,
    /// publication, or a later rebuild.
    /// </summary>
    internal sealed class W24S5FirstFormalBuildRequest
    {
        internal string EffectId;
        internal string ContractPath;
        internal string ContractFileHash;
        internal string TracePath;
        internal string TraceFileHash;
        internal string ExpectedRuntimeEntryPath;
        internal string ExpectedManifestPath;
        internal string OwnedOutputRoot;
        internal W24S5BuildIntent Intent;
        internal W24S5VisualStatus VisualStatus;
    }

    /// <summary>
    /// Opaque, gate-issued authority for exactly one bootstrap Manifest write. Its constructor,
    /// provenance, binding and state are intentionally unavailable to ordinary authoring code.
    /// </summary>
    internal sealed class W24S5FirstFormalBuildApproval
    {
        private readonly W24S5PersistedFile contract;
        private readonly W24S5PersistedFile trace;
        private readonly string effectId;
        private readonly string runtimeEntryPath;
        private readonly string manifestPath;
        private readonly string ownedOutputRoot;
        private readonly string contractHash;
        private readonly int contractRevision;
        private readonly W24S5OwnedOutputSnapshot outputSnapshot;
        private bool writeInProgress;
        private bool committed;

        internal W24S5FirstFormalBuildApproval(object issuer, W24S5PersistedFile contractFile, W24S5PersistedFile traceFile, string approvedEffectId, string approvedRuntimeEntryPath, string approvedManifestPath, string approvedOwnedOutputRoot, string approvedContractHash, int approvedContractRevision, W24S5OwnedOutputSnapshot approvedOutputSnapshot)
        {
            if (!W24S5ProductionGate.IsFirstFormalWriteIssuer(issuer)) throw new InvalidOperationException("First-formal-build approvals may only be issued by the S5 gate.");
            contract = contractFile;
            trace = traceFile;
            effectId = approvedEffectId;
            runtimeEntryPath = approvedRuntimeEntryPath;
            manifestPath = approvedManifestPath;
            ownedOutputRoot = approvedOwnedOutputRoot;
            contractHash = approvedContractHash;
            contractRevision = approvedContractRevision;
            outputSnapshot = approvedOutputSnapshot;
        }

        internal bool TryBeginWrite(object issuer, out W24S5PersistedFile contractFile, out W24S5PersistedFile traceFile, out string approvedEffectId, out string approvedRuntimeEntryPath, out string approvedManifestPath, out string approvedOwnedOutputRoot, out W24S5OwnedOutputSnapshot approvedOutputSnapshot, out VfxFormalProductionBinding binding, out string error)
        {
            contractFile = null; traceFile = null; approvedEffectId = null; approvedRuntimeEntryPath = null; approvedManifestPath = null; approvedOwnedOutputRoot = null; approvedOutputSnapshot = null; binding = null; error = null;
            if (!W24S5ProductionGate.IsFirstFormalWriteIssuer(issuer)) { error = "First-formal-build authority is not gate-issued."; return false; }
            if (committed || writeInProgress) { error = "First-formal-build authority is already consumed or being committed."; return false; }
            writeInProgress = true;
            contractFile = contract;
            traceFile = trace;
            approvedEffectId = effectId;
            approvedRuntimeEntryPath = runtimeEntryPath;
            approvedManifestPath = manifestPath;
            approvedOwnedOutputRoot = ownedOutputRoot;
            approvedOutputSnapshot = outputSnapshot;
            binding = new VfxFormalProductionBinding
            {
                ContractPath = contract.RelativePath,
                ContractFileHash = contract.Hash,
                ContractHash = contractHash,
                ContractRevision = contractRevision,
                TracePath = trace.RelativePath,
                TraceFileHash = trace.Hash,
                VisualStatus = W24S5VisualStatus.VISUAL_PENDING.ToString(),
                AdmissionPhase = "PRE_C0_FIRST_FORMAL_BUILD"
            };
            return true;
        }

        internal bool TryCompleteWrite(object issuer, out string error)
        {
            error = null;
            if (!W24S5ProductionGate.IsFirstFormalWriteIssuer(issuer) || !writeInProgress || committed) { error = "First-formal-build authority was not in a gate-owned commit."; return false; }
            committed = true;
            writeInProgress = false;
            return true;
        }

        internal void AbortWrite(object issuer)
        {
            if (W24S5ProductionGate.IsFirstFormalWriteIssuer(issuer) && !committed) writeInProgress = false;
        }

        internal bool TryCreateBootstrapReceipt(object issuer, out W24S5BootstrapReceipt receipt, out string error)
        {
            receipt = null;
            error = null;
            if (!W24S5ProductionGate.IsFirstFormalWriteIssuer(issuer) || !committed) { error = "Bootstrap receipt is available only after the gate-owned first-formal commit succeeds."; return false; }
            receipt = new W24S5BootstrapReceipt(issuer, effectId, runtimeEntryPath, manifestPath, ownedOutputRoot, contract.RelativePath, contract.Hash, contractHash, contractRevision, trace.RelativePath, trace.Hash);
            return true;
        }
    }

    /// <summary>Immutable provenance handed to the write-once C0 candidate freezer after commit.</summary>
    internal sealed class W24S5BootstrapReceipt
    {
        internal readonly string EffectId;
        internal readonly string RuntimeEntryPath;
        internal readonly string ManifestPath;
        internal readonly string OwnedOutputRoot;
        internal readonly string ContractPath;
        internal readonly string ContractFileHash;
        internal readonly string ContractHash;
        internal readonly int ContractRevision;
        internal readonly string TracePath;
        internal readonly string TraceFileHash;

        internal W24S5BootstrapReceipt(object issuer, string effectId, string runtimeEntryPath, string manifestPath, string ownedOutputRoot, string contractPath, string contractFileHash, string contractHash, int contractRevision, string tracePath, string traceFileHash)
        {
            if (!W24S5ProductionGate.IsFirstFormalWriteIssuer(issuer)) throw new InvalidOperationException("Bootstrap receipts may only be issued by the S5 gate.");
            EffectId = effectId;
            RuntimeEntryPath = runtimeEntryPath;
            ManifestPath = manifestPath;
            OwnedOutputRoot = ownedOutputRoot;
            ContractPath = contractPath;
            ContractFileHash = contractFileHash;
            ContractHash = contractHash;
            ContractRevision = contractRevision;
            TracePath = tracePath;
            TraceFileHash = traceFileHash;
        }
    }

    public static class W24S5ProductionGate
    {
        public const string ManifestRoot = "ProjectSettings/VFXComposer/BuildManifests/";
        private const string DocsRoot = "docs/";
        private const string VerdictRoot = "docs/vfx-verdicts/";
        private const string EvidenceCorpusRoot = "docs/vfx-evidence-corpora/";
        private const string GeneratedOutputRoot = "Assets/VFX/Generated/";
        private static readonly object FirstFormalWriteIssuer = new object();
        private static readonly object FormalApprovalIssuer = new object();
        private static readonly object FormalWriteIssuer = new object();

        // This comparison intentionally reveals no constructible authority to other editor code.
        internal static bool IsFirstFormalWriteIssuer(object issuer) { return ReferenceEquals(issuer, FirstFormalWriteIssuer); }
        internal static bool IsFormalApprovalIssuer(object issuer) { return ReferenceEquals(issuer, FormalApprovalIssuer); }
        internal static bool IsFormalWriteIssuer(object issuer) { return ReferenceEquals(issuer, FormalWriteIssuer); }

        /// <summary>Detects an effect that has entered W24 authority. Ordinary compiler/writer paths must never overwrite it.</summary>
        internal static bool IsW24ProtectedEffect(string effectId)
        {
            if (!IsEffectId(effectId)) return false;
            try
            {
                var manifestPath = VfxProjectRules.ManifestAbsolutePath(effectId);
                if (File.Exists(manifestPath))
                {
                    var manifest = JObject.Parse(File.ReadAllText(manifestPath), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (manifest["formalProduction"] is JObject) return true;
                }
                var projectRoot = Directory.GetParent(Application.dataPath).FullName;
                var repositoryRoot = Directory.GetParent(projectRoot).FullName;
                if (Directory.Exists(Path.Combine(repositoryRoot, "docs", "vfx-candidates", effectId))) return true;
                var contractsRoot = Path.Combine(repositoryRoot, "docs", "vfx-contracts");
                if (!Directory.Exists(contractsRoot)) return false;
                foreach (var file in Directory.GetFiles(contractsRoot, "*.json", SearchOption.AllDirectories))
                {
                    try
                    {
                        var root = JObject.Parse(File.ReadAllText(file), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                        if (Same((string)root["effectId"], effectId) && ((string)root["contractVersion"] ?? string.Empty).StartsWith("w24", StringComparison.OrdinalIgnoreCase)) return true;
                    }
                    catch (JsonException) { }
                }
            }
            // This predicate protects public compiler/writer entry points.  If discovery is
            // incomplete or unreadable, conservatively treat the effect as W24-protected.
            catch (Exception) { return true; }
            return false;
        }

        public static W24S5ProductionGateResult Evaluate(W24S5ProductionGateRequest request)
        {
            var result = new W24S5ProductionGateResult { EffectiveStatus = W24S5VisualStatus.VISUAL_PENDING };
            if (request == null) { result.Error("W24S5-001", "$", "A production-gate request is required."); return Finish(result); }
            if (!IsEffectId(request.EffectId)) result.Error("W24S5-002", "effectId", "effectId must be stable lower_snake_case.");
            if (!Same(request.ExpectedManifestPath, ManifestRoot + request.EffectId + ".manifest.json")) result.Error("W24S5-003", "expectedManifestPath", "Formal manifest path must be the authoritative effect manifest path.");
            if (TryValidateAuthoritativeLegacy(result, request)) return result;
            ValidateExistingFormalManifest(result, request);
            if (request.VisualStatus == W24S5VisualStatus.LEGACY) result.Error("W24S5-004", "visualStatus", "LEGACY is derived only from an existing verified legacy manifest, never from a request.");

            var contractFile = ReadPersisted(result, request.ContractPath, request.ContractFileHash, "designContract", "W24S5-010", W24S5RecordScope.Formal);
            var traceFile = ReadPersisted(result, request.TracePath, request.TraceFileHash, "implementationTrace", "W24S5-020", W24S5RecordScope.Formal);
            VfxDesignContract contract = null;
            VfxImplementationTrace trace = null;
            if (contractFile != null)
            {
                var contractReport = VfxDesignContractJson.ValidateJson(contractFile.Text, out contract);
                CopyS1Issues(result, contractReport, "designContract");
                if (contract != null && !Same(contract.EffectId, request.EffectId)) result.Error("W24S5-013", "designContract.effectId", "Contract effectId must equal the planned effect.");
                if (contract != null && !Same(contract.ContractHash, VfxDesignContractJson.ComputeContractHash(contractFile.Text))) result.Error("W24S5-015", "designContract.contractHash", "Contract hash must be the authoritative S1 canonical hash.");
            }
            if (traceFile != null && contract != null)
            {
                var traceValidation = VfxImplementationTraceJson.ValidateJson(traceFile.Text, contract, out trace);
                CopyS1Issues(result, traceValidation.Report, "implementationTrace");
            }
            if (contract != null && trace != null)
            {
                if (!Same(trace.TraceStatus, "FORMAL_EVIDENCE_BOUND")) result.Error("W24S5-029", "implementationTrace.traceStatus", "Ordinary formal exact-plan admission requires FORMAL_EVIDENCE_BOUND; PENDING_FIRST_FORMAL_BUILD_BINDING and C0_CAPTURE_PENDING remain bootstrap/candidate-only states.");
                if (!Same(trace.BuildHash, request.PlannedBuildHash)) result.Error("W24S5-027", "implementationTrace.buildHash", "Trace must bind the exact gated compiler build hash.");
                if (!Same(trace.RuntimeEntryAssetPath, request.ExpectedRuntimeEntryPath)) result.Error("W24S5-028", "implementationTrace.runtimeEntryAssetPath", "Trace must bind the planned Runtime Entry path.");
                if (Same(trace.TraceStatus, "FORMAL_EVIDENCE_BOUND")) W24S5EvidenceTransition.VerifyForFormalGate(result, contractFile, traceFile, contract, trace);
            }
            var visualQa = request.VisualStatus == W24S5VisualStatus.L3 ? W24S5VisualQaAuthority.Verify(result, request, contract, trace, traceFile) : null;
            var verdict = request.VisualStatus == W24S5VisualStatus.L4 ? W24S5UserVerdictAuthority.Verify(result, request, contract, trace, traceFile) : null;
            ValidateStatus(result, request, verdict, visualQa);
            result.EffectiveStatus = request.VisualStatus == W24S5VisualStatus.L4 && verdict == null || request.VisualStatus == W24S5VisualStatus.L3 && visualQa == null ? W24S5VisualStatus.VISUAL_PENDING : request.VisualStatus;
            result.CanBuild = !result.HasErrors;
            result.CanPublish = !result.HasErrors && request.Intent == W24S5BuildIntent.Publication && result.EffectiveStatus == W24S5VisualStatus.L4;
            if (result.CanBuild && contract != null && trace != null)
                result.Approval = new W24S5FormalApproval(FormalApprovalIssuer, contractFile, traceFile, verdict, visualQa, request.EffectId, request.PlannedBuildHash, request.ExpectedRuntimeEntryPath, result.EffectiveStatus);
            return result;
        }

        /// <summary>
        /// Internal pre-C0 admission for a first identity-populating build. It intentionally does
        /// not call the normal trace validator: that validator correctly requires evidence and
        /// real asset identities, neither of which exists before the first formal build. Instead
        /// this method accepts only the documented pending sentinels and validates the exact
        /// requirement/state/layer topology without manufacturing evidence.
        /// </summary>
        internal static W24S5ProductionGateResult EvaluateFirstFormalBuild(W24S5FirstFormalBuildRequest request)
        {
            var result = new W24S5ProductionGateResult { EffectiveStatus = W24S5VisualStatus.VISUAL_PENDING };
            if (request == null) { result.Error("W24S5-PRE001", "$", "A first-formal-build request is required."); return Finish(result); }
            if (request.Intent != W24S5BuildIntent.Development) result.Error("W24S5-PRE002", "intent", "Pre-C0 admission permits Development only.");
            if (request.VisualStatus != W24S5VisualStatus.VISUAL_PENDING) result.Error("W24S5-PRE003", "visualStatus", "Pre-C0 admission permits VISUAL_PENDING only; it cannot grant L3 or L4.");
            if (!IsEffectId(request.EffectId)) result.Error("W24S5-PRE004", "effectId", "effectId must be stable lower_snake_case.");
            if (!Same(request.ExpectedManifestPath, ManifestRoot + request.EffectId + ".manifest.json")) result.Error("W24S5-PRE005", "expectedManifestPath", "First formal build must target the authoritative effect manifest.");
            if (!IsExactEffectOwnedOutputRoot(request.OwnedOutputRoot, request.EffectId) || !Same(NormalizeAssetPath(request.ExpectedRuntimeEntryPath == null ? null : Path.GetDirectoryName(request.ExpectedRuntimeEntryPath)), NormalizeAssetPath(request.OwnedOutputRoot)) || !IsUnderOwnedOutputRoot(request.ExpectedRuntimeEntryPath, request.OwnedOutputRoot) || !request.ExpectedRuntimeEntryPath.EndsWith(".prefab", StringComparison.Ordinal)) result.Error("W24S5-PRE006", "ownedOutputRoot", "Runtime Entry must be a prefab directly under a safe effect-specific owned-output root.");

            var manifestAbsolute = IsEffectId(request.EffectId) ? VfxProjectRules.ManifestAbsolutePath(request.EffectId) : null;
            if (!string.IsNullOrEmpty(manifestAbsolute) && File.Exists(manifestAbsolute)) result.Error("W24S5-PRE007", "existingManifest", "First-formal-build admission is single-use; an authoritative manifest already exists.");

            var contractFile = ReadPersisted(result, request.ContractPath, request.ContractFileHash, "designContract", "W24S5-PRE010", W24S5RecordScope.Formal);
            var traceFile = ReadPersisted(result, request.TracePath, request.TraceFileHash, "implementationTrace", "W24S5-PRE020", W24S5RecordScope.Formal);
            VfxDesignContract contract = null;
            VfxImplementationTrace trace = null;
            if (contractFile != null)
            {
                CopyS1Issues(result, VfxDesignContractJson.ValidateJson(contractFile.Text, out contract), "designContract");
                ValidateFirstFormalContract(result, request, contract);
            }
            if (traceFile != null)
            {
                try { trace = VfxImplementationTraceJson.FromJson(traceFile.Text); }
                catch (Exception e) when (e is JsonException || e is FormatException || e is OverflowException) { result.Error("W24S5-PRE020", "implementationTrace", "Invalid strict lowerCamel trace JSON: " + e.Message); }
            }
            if (contract != null && trace != null) ValidateFirstFormalTrace(result, request, contract, trace);

            result.CanBuild = !result.HasErrors;
            result.CanPublish = false;
            if (result.CanBuild)
            {
                W24S5OwnedOutputSnapshot outputSnapshot;
                try { outputSnapshot = CaptureOwnedOutputSnapshot(NormalizeAssetPath(request.OwnedOutputRoot)); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { result.Error("W24S5-PRE008", "ownedOutputRoot", "Could not snapshot the owned-output root for rollback: " + e.Message); result.CanBuild = false; return result; }
                result.FirstFormalApproval = new W24S5FirstFormalBuildApproval(FirstFormalWriteIssuer, contractFile, traceFile, request.EffectId, request.ExpectedRuntimeEntryPath, request.ExpectedManifestPath, NormalizeAssetPath(request.OwnedOutputRoot), contract.ContractHash, contract.ContractRevision, outputSnapshot);
            }
            return result;
        }

        /// <summary>
        /// Gate-owned bootstrap transaction. It derives the Runtime Entry, owned root and binding
        /// only from an opaque approval; authoring cannot manufacture or modify a PRE_C0 binding.
        /// This writes a bootstrap receipt only, never a visual/capture/publication verdict.
        /// </summary>
        internal static VfxOutputAuditResult CommitFirstFormalBuild(W24S5FirstFormalBuildApproval approval, string archetype, int recipeVersion, int recipeRevision, string recipeHash, string buildHash, string compilerVersion, double duration, string sourceRecipePathOverride = null)
        {
            W24S5PersistedFile contractFile;
            W24S5PersistedFile traceFile;
            string effectId;
            string runtimeEntryPath;
            string manifestRelativePath;
            string ownedOutputRoot;
            W24S5OwnedOutputSnapshot outputSnapshot;
            VfxFormalProductionBinding binding;
            string error = null;
            if (approval == null || !approval.TryBeginWrite(FirstFormalWriteIssuer, out contractFile, out traceFile, out effectId, out runtimeEntryPath, out manifestRelativePath, out ownedOutputRoot, out outputSnapshot, out binding, out error)) return BootstrapError("E24S5PRE040", error ?? "First-formal-build approval is missing or not gate-issued.");
            if (!FileMatches(contractFile, W24S5RecordScope.Formal) || !FileMatches(traceFile, W24S5RecordScope.Formal)) { approval.AbortWrite(FirstFormalWriteIssuer); return BootstrapFailure(effectId, ownedOutputRoot, outputSnapshot, null, "E24S5PRE041", "Contract or preregistration Trace changed after gate admission."); }
            if (!IsRawHash(buildHash)) { approval.AbortWrite(FirstFormalWriteIssuer); return BootstrapFailure(effectId, ownedOutputRoot, outputSnapshot, null, "E24S5PRE042", "First-formal-build manifest requires a real lowercase raw buildHash."); }
            if (!Same(manifestRelativePath, ManifestRoot + effectId + ".manifest.json")) { approval.AbortWrite(FirstFormalWriteIssuer); return BootstrapFailure(effectId, ownedOutputRoot, outputSnapshot, null, "E24S5PRE043", "Bootstrap approval does not target the authoritative manifest path."); }
            var manifestPath = VfxProjectRules.ManifestAbsolutePath(effectId);
            if (File.Exists(manifestPath)) { approval.AbortWrite(FirstFormalWriteIssuer); return BootstrapFailure(effectId, ownedOutputRoot, outputSnapshot, null, "E24S5PRE044", "Bootstrap receipt cannot overwrite an existing authoritative manifest."); }

            var priorManifest = VfxProductionRules.CaptureManifest(effectId);
            VfxOutputAuditResult audit = null;
            try
            {
                audit = VfxProductionRules.EnforceAndWriteBootstrapManifest(FirstFormalWriteIssuer, effectId, archetype, recipeVersion, recipeRevision, recipeHash, buildHash, compilerVersion, runtimeEntryPath, ownedOutputRoot, duration, sourceRecipePathOverride, binding);
                if (audit.Report.HasErrors)
                {
                    approval.AbortWrite(FirstFormalWriteIssuer);
                    AppendRollbackFailure(audit, effectId, ownedOutputRoot, outputSnapshot, priorManifest);
                    return audit;
                }
                if (!VerifyFirstFormalBuildCommit(contractFile, traceFile, effectId, runtimeEntryPath, manifestRelativePath, ownedOutputRoot, binding, out error))
                {
                    approval.AbortWrite(FirstFormalWriteIssuer);
                    audit.Report.Add("E24S5PRE045", ValidationSeverity.Error, "/formalProduction", error);
                    AppendRollbackFailure(audit, effectId, ownedOutputRoot, outputSnapshot, priorManifest);
                    return audit;
                }
                if (!approval.TryCompleteWrite(FirstFormalWriteIssuer, out error))
                {
                    approval.AbortWrite(FirstFormalWriteIssuer);
                    audit.Report.Add("E24S5PRE046", ValidationSeverity.Error, "/formalProduction", error);
                    AppendRollbackFailure(audit, effectId, ownedOutputRoot, outputSnapshot, priorManifest);
                    return audit;
                }
                return audit;
            }
            catch (Exception e)
            {
                approval.AbortWrite(FirstFormalWriteIssuer);
                if (audit == null) audit = new VfxOutputAuditResult();
                audit.Report.Add("E24S5PRE047", ValidationSeverity.Error, "/formalProduction", "Bootstrap receipt transaction failed; rollback of the prior manifest and owned outputs was attempted: " + e.Message);
                AppendRollbackFailure(audit, effectId, ownedOutputRoot, outputSnapshot, priorManifest);
                return audit;
            }
        }

        /// <summary>
        /// Exposes immutable bootstrap provenance only after the gate-owned receipt transaction
        /// succeeds. It is the sole input permitted to the write-once C0 candidate freezer.
        /// </summary>
        internal static bool TryGetBootstrapReceipt(W24S5FirstFormalBuildApproval approval, out W24S5BootstrapReceipt receipt, out string error)
        {
            if (approval == null) { receipt = null; error = "First-formal-build approval is required."; return false; }
            return approval.TryCreateBootstrapReceipt(FirstFormalWriteIssuer, out receipt, out error);
        }

        /// <summary>Gate-owned C0 evidence seal; it never mutates C0 or consumes candidate C1.</summary>
        internal static W24S5FormalEvidenceTransitionResult FinalizeC0Evidence(W24S5FormalEvidenceTransitionRequest request)
        {
            return W24S5EvidenceTransition.Finalize(request);
        }

        private static bool VerifyFirstFormalBuildCommit(W24S5PersistedFile contractFile, W24S5PersistedFile traceFile, string effectId, string runtimeEntryPath, string manifestRelativePath, string ownedOutputRoot, VfxFormalProductionBinding expectedBinding, out string error)
        {
            error = null;
            if (!FileMatches(contractFile, W24S5RecordScope.Formal) || !FileMatches(traceFile, W24S5RecordScope.Formal)) { error = "Contract or preregistration Trace changed before bootstrap receipt verification."; return false; }
            if (!Same(manifestRelativePath, ManifestRoot + effectId + ".manifest.json")) { error = "Bootstrap receipt manifest path differs from the approved authoritative path."; return false; }
            var manifestPath = VfxProjectRules.ManifestAbsolutePath(effectId);
            if (!File.Exists(manifestPath)) { error = "First formal build did not write its authoritative manifest."; return false; }
            try
            {
                var manifest = JObject.Parse(File.ReadAllText(manifestPath), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (!Same((string)manifest["enforcement"], "strict") || !Same((string)manifest["effectId"], effectId)) { error = "First formal manifest is not the expected strict effect manifest."; return false; }
                if (!IsRawHash((string)manifest["buildHash"])) { error = "First formal manifest must contain a real raw buildHash."; return false; }
                if (!VerifyOwnedOutputManifest(manifest, effectId, runtimeEntryPath, ownedOutputRoot, out error)) return false;
                var binding = manifest["formalProduction"] as JObject;
                if (!HasExactEvidenceFreeBootstrapBinding(binding, expectedBinding.ContractPath, expectedBinding.ContractFileHash, expectedBinding.ContractHash, expectedBinding.ContractRevision, expectedBinding.TracePath, expectedBinding.TraceFileHash)) { error = "First formal manifest lacks the exact evidence-free bootstrap receipt binding."; return false; }
                return true;
            }
            catch (Exception e) when (e is JsonException || e is IOException) { error = "First formal manifest cannot be verified: " + e.Message; return false; }
        }

        /// <summary>
        /// Strictly recognizes the one immutable, evidence-free bootstrap binding.  Production
        /// manifests deliberately serialize absent evidence as explicit JSON nulls so that a
        /// receipt has a stable schema.  A Newtonsoft <see cref="JToken"/> for JSON null is not
        /// a C# null reference; checking only <c>token != null</c> would reject that valid,
        /// gate-owned receipt.  Requiring explicit null tokens also rejects omitted fields and
        /// any attempt to smuggle C0/L3/L4 authority into the bootstrap manifest.
        /// </summary>
        internal static bool HasExactEvidenceFreeBootstrapBinding(JObject binding, string contractPath, string contractFileHash, string contractHash, int contractRevision, string tracePath, string traceFileHash)
        {
            return binding != null
                && Same((string)binding["admissionPhase"], "PRE_C0_FIRST_FORMAL_BUILD")
                && Same((string)binding["contractPath"], contractPath)
                && Same((string)binding["contractFileHash"], contractFileHash)
                && Same((string)binding["contractHash"], contractHash)
                && (int?)binding["contractRevision"] == contractRevision
                && Same((string)binding["tracePath"], tracePath)
                && Same((string)binding["traceFileHash"], traceFileHash)
                && Same((string)binding["visualStatus"], "VISUAL_PENDING")
                && IsExplicitJsonNull(binding, "evidenceCorpusPath")
                && IsExplicitJsonNull(binding, "evidenceCorpusHash")
                && IsExplicitJsonNull(binding, "userVerdictRecordPath")
                && IsExplicitJsonNull(binding, "userVerdictRecordHash")
                && IsExplicitJsonNull(binding, "visualQaRecordPath")
                && IsExplicitJsonNull(binding, "visualQaRecordHash")
                && IsExplicitJsonNull(binding, "s0aStatusRecordPath")
                && IsExplicitJsonNull(binding, "s0aStatusRecordHash");
        }

        private static bool IsExplicitJsonNull(JObject value, string property)
        {
            var token = value[property];
            return token != null && token.Type == JTokenType.Null;
        }

        internal static bool IsApprovalCurrent(W24S5FormalApproval approval, out string error)
        {
            error = null;
            if (approval == null) { error = "No S5 formal approval was attached to this build plan."; return false; }
            if (approval.LegacyDevelopment) return true;
            if (!FileMatches(approval.Contract, W24S5RecordScope.Formal) || !FileMatches(approval.Trace, W24S5RecordScope.Formal)) { error = "The persisted contract or trace changed after gating."; return false; }
            if (approval.Verdict != null && (!FileMatches(new W24S5PersistedFile { RelativePath = approval.Verdict.DecisionRecordPath, Hash = approval.Verdict.DecisionRecordHash }, W24S5RecordScope.Verdict) || !FileMatches(new W24S5PersistedFile { RelativePath = approval.Verdict.EvidenceCorpusPath, Hash = approval.Verdict.EvidenceCorpusFileHash }, W24S5RecordScope.EvidenceCorpus))) { error = "The persisted user verdict or its evidence corpus changed after gating."; return false; }
            if (approval.VisualQa != null && (!FileMatches(new W24S5PersistedFile { RelativePath = approval.VisualQa.QaRecordPath, Hash = approval.VisualQa.QaRecordHash }, W24S5RecordScope.VisualQa) || !FileMatches(new W24S5PersistedFile { RelativePath = approval.VisualQa.S0aStatusPath, Hash = approval.VisualQa.S0aStatusHash }, W24S5RecordScope.S0aStatus))) { error = "The persisted Visual QA or S0a status changed after gating."; return false; }
            try
            {
                VfxDesignContract contract;
                var contractReport = VfxDesignContractJson.ValidateJson(approval.Contract.Text, out contract);
                VfxImplementationTrace trace;
                var traceReport = VfxImplementationTraceJson.ValidateJson(approval.Trace.Text, contract, out trace);
                if (contractReport.HasErrors || traceReport.Report.HasErrors) { error = "The persisted formal Contract or Trace is no longer valid."; return false; }
                var current = new W24S5ProductionGateResult();
                if (!W24S5EvidenceTransition.VerifyForFormalGate(current, approval.Contract, approval.Trace, contract, trace) || current.HasErrors) { error = "The formal C0/evidence/capture chain changed after gating."; return false; }
            }
            catch (Exception e) when (e is JsonException || e is FormatException || e is IOException) { error = "The formal evidence chain cannot be revalidated: " + e.Message; return false; }
            return true;
        }

        /// <summary>
        /// The only normal formal-manifest commit. All persisted authority is re-read immediately
        /// before the write and the serialized binding is reconstructed inside S5; callers never
        /// receive a writable binding object.
        /// </summary>
        internal static VfxOutputAuditResult CommitFormalManifest(W24S5FormalApproval approval, string effectId, string archetype, int recipeVersion, int recipeRevision, string recipeHash, string buildHash, string compilerVersion, string runtimeEntryPath, string outputFolder, double duration, string sourceRecipePathOverride = null)
        {
            string error = null;
            if (approval == null || approval.LegacyDevelopment || !Same(approval.EffectId, effectId) || !Same(approval.PlannedBuildHash, buildHash) || !Same(approval.RuntimeEntryPath, runtimeEntryPath) || !IsApprovalCurrent(approval, out error))
                return BootstrapError("E24S5-092", string.IsNullOrEmpty(error) ? "Formal manifest commit does not match a current S5 approval." : error);

            var contract = VfxDesignContract.FromJson(approval.Contract.Text);
            var binding = new VfxFormalProductionBinding
            {
                ContractPath = approval.Contract.RelativePath,
                ContractFileHash = approval.Contract.Hash,
                ContractHash = contract.ContractHash,
                ContractRevision = contract.ContractRevision,
                TracePath = approval.Trace.RelativePath,
                TraceFileHash = approval.Trace.Hash,
                VisualStatus = approval.VisualStatus.ToString(),
                EvidenceCorpusPath = approval.Verdict == null ? null : approval.Verdict.EvidenceCorpusPath,
                EvidenceCorpusHash = approval.Verdict == null ? null : approval.Verdict.EvidenceCorpusHash,
                UserVerdictRecordPath = approval.Verdict == null ? null : approval.Verdict.DecisionRecordPath,
                UserVerdictRecordHash = approval.Verdict == null ? null : approval.Verdict.DecisionRecordHash,
                VisualQaRecordPath = approval.VisualQa == null ? null : approval.VisualQa.QaRecordPath,
                VisualQaRecordHash = approval.VisualQa == null ? null : approval.VisualQa.QaRecordHash,
                S0aStatusRecordPath = approval.VisualQa == null ? null : approval.VisualQa.S0aStatusPath,
                S0aStatusRecordHash = approval.VisualQa == null ? null : approval.VisualQa.S0aStatusHash
            };
            return VfxProductionRules.EnforceAndWriteFormalManifest(FormalWriteIssuer, effectId, archetype, recipeVersion, recipeRevision, recipeHash, buildHash, compilerVersion, runtimeEntryPath, outputFolder, duration, sourceRecipePathOverride, binding);
        }

        private static void ValidateExistingFormalManifest(W24S5ProductionGateResult result, W24S5ProductionGateRequest request)
        {
            if (!IsEffectId(request.EffectId) || string.IsNullOrEmpty(request.ExpectedRuntimeEntryPath)) return;
            var path = VfxProjectRules.ManifestAbsolutePath(request.EffectId);
            if (!File.Exists(path)) return;
            try
            {
                var manifest = JObject.Parse(File.ReadAllText(path), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (!Same((string)manifest["enforcement"], "strict") || !Same((string)manifest["effectId"], request.EffectId)) { result.Error("W24S5-053", "existingManifest", "Formal update requires the existing authoritative strict manifest for the same effect."); return; }
                string error;
                if (!VerifyOwnedOutputManifest(manifest, request.EffectId, request.ExpectedRuntimeEntryPath, NormalizeAssetPath(Path.GetDirectoryName(request.ExpectedRuntimeEntryPath)), out error)) result.Error("W24S5-052", "existingManifest", "Existing formal manifest does not own the exact Runtime Entry/output root: " + error);
            }
            catch (Exception e) when (e is JsonException || e is IOException) { result.Error("W24S5-050", "existingManifest", "Existing formal manifest cannot be parsed or verified: " + e.Message); }
        }

        private static bool TryValidateAuthoritativeLegacy(W24S5ProductionGateResult result, W24S5ProductionGateRequest request)
        {
            var path = VfxProjectRules.ManifestAbsolutePath(request.EffectId);
            if (!File.Exists(path)) return false;
            try
            {
                var manifest = JObject.Parse(File.ReadAllText(path), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                var claimsLegacy = Same((string)manifest["enforcement"], "legacy_audit");
                if (!claimsLegacy) return false;
                string legacyError = null;
                var legacy = VfxProjectRules.EnforcementFor(request.EffectId) == VfxRulesEnforcement.LegacyAudit && VerifyLegacyManifest(manifest, request, out legacyError);
                if (!legacy) { result.Error("W24S5-005", "existingManifest", "Legacy authority cannot be verified: " + legacyError); return true; }
                result.Warning("W24S5-LEGACY", "existingManifest", "Legacy authority was derived from the existing verified legacy manifest; it remains development-only and never publishable.");
                result.EffectiveStatus = W24S5VisualStatus.LEGACY;
                if (request.Intent != W24S5BuildIntent.Development) result.Error("W24S5-006", "intent", "Verified legacy entries are never publishable.");
                result.CanBuild = request.Intent == W24S5BuildIntent.Development && !result.HasErrors;
                result.CanPublish = false;
                if (result.CanBuild) result.Approval = new W24S5FormalApproval(null, null, null, null, null, request.EffectId, null, request.ExpectedRuntimeEntryPath, W24S5VisualStatus.LEGACY, true);
                return true;
            }
            catch (Exception e) when (e is JsonException || e is IOException || e is InvalidDataException) { result.Error("W24S5-005", "existingManifest", "Authoritative manifest cannot be verified: " + e.Message); return true; }
        }

        private static bool VerifyLegacyManifest(JObject manifest, W24S5ProductionGateRequest request, out string error)
        {
            error = null;
            if ((int?)manifest["manifestVersion"] != 1 || !Same((string)manifest["effectId"], request.EffectId)) { error = "Manifest identity or schema is invalid."; return false; }
            return VerifyOwnedOutputManifest(manifest, request.EffectId, request.ExpectedRuntimeEntryPath, GeneratedOutputRoot + request.EffectId, out error);
        }

        internal static bool VerifyOwnedOutputManifest(JObject manifest, string effectId, string runtimeEntryPath, string ownedOutputRoot, out string error)
        {
            error = null;
            if (!IsSafeOwnedOutputRoot(ownedOutputRoot) || !IsUnderOwnedOutputRoot(runtimeEntryPath, ownedOutputRoot) || !runtimeEntryPath.EndsWith(".prefab", StringComparison.Ordinal)) { error = "Owned-output root or Runtime Entry path is invalid."; return false; }
            var runtime = manifest["runtimeEntry"] as JObject;
            var owned = manifest["ownedOutputs"] as JArray;
            if (runtime == null || owned == null || owned.Count == 0 || !Same((string)runtime["kind"], "prefab") || !Same((string)runtime["path"], runtimeEntryPath)) { error = "Runtime entry is incomplete or outside the allowed owned-output root."; return false; }
            var declared = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in owned.OfType<JObject>())
            {
                var outputPath = (string)record["path"];
                var outputGuid = (string)record["guid"];
                var outputHash = (string)record["sha256"];
                if (!IsUnderOwnedOutputRoot(outputPath, ownedOutputRoot) || !IsRawHash(outputHash) || !IsGuid(outputGuid) || string.IsNullOrWhiteSpace((string)record["assetType"]) || !declared.Add(outputPath)) { error = "Owned outputs contain an invalid, duplicate, or out-of-root record."; return false; }
                var absolute = VfxProjectRules.ProjectAbsolute(outputPath);
                var metaPath = absolute + ".meta";
                var projectRoot = Directory.GetParent(Application.dataPath).FullName;
                if (HasReparsePointAtOrAbove(absolute, projectRoot) || HasReparsePointAtOrAbove(metaPath, projectRoot)) { error = "Owned output contains a symlink/junction/reparse point: " + outputPath; return false; }
                if (!File.Exists(absolute) || !File.Exists(metaPath) || !SameRawHash(Sha256Raw(File.ReadAllBytes(absolute)), outputHash) || !Same(MetaGuid(metaPath), outputGuid)) { error = "Owned output bytes or .meta GUID do not match the manifest: " + outputPath; return false; }
            }
            if (declared.Count != owned.Count || !declared.Contains(runtimeEntryPath) || !Same((string)runtime["guid"], owned.OfType<JObject>().Where(item => Same((string)item["path"], runtimeEntryPath)).Select(item => (string)item["guid"]).FirstOrDefault())) { error = "Runtime entry is not exactly represented by the owned outputs."; return false; }
            var outputRoot = VfxProjectRules.ProjectAbsolute(ownedOutputRoot);
            if (HasReparsePointAtOrAbove(outputRoot, Directory.GetParent(Application.dataPath).FullName)) { error = "Owned-output root contains a symlink/junction/reparse point."; return false; }
            var actual = Directory.Exists(outputRoot) ? new HashSet<string>(Directory.GetFiles(outputRoot, "*", SearchOption.AllDirectories).Where(value => !value.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) && !Same(Path.GetFileName(value), "BuildManifest.json")).Select(ToProjectRelative), StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
            if (!actual.SetEquals(declared)) { error = "Owned outputs do not exactly enumerate the allowed output root."; return false; }
            return true;
        }

        private static void ValidateStatus(W24S5ProductionGateResult result, W24S5ProductionGateRequest request, W24S5UserVerdictAuthority verdict, W24S5VisualQaAuthority visualQa)
        {
            if (request.VisualStatus == W24S5VisualStatus.VISUAL_PENDING) result.Warning("W24S5-VISUAL-PENDING", "visualStatus", "The entry may be developed but cannot be commercial, visually passed, or production ready before user L4 signoff.");
            if (request.VisualStatus == W24S5VisualStatus.L3 && request.Intent == W24S5BuildIntent.Publication) result.Error("W24S5-060", "visualStatus", "L3 remains a production candidate; publication requires an exact persisted user L4 verdict.");
            if (request.VisualStatus == W24S5VisualStatus.VISUAL_PENDING && request.Intent == W24S5BuildIntent.Publication) result.Error("W24S5-062", "visualStatus", "VISUAL_PENDING permits continued development only; publication requires exact user L4 signoff.");
            if (request.VisualStatus == W24S5VisualStatus.L4 && verdict == null) result.Error("W24S5-073", "visualStatus", "L4 cannot be granted by public request data; a verified internal user-verdict authority is required.");
            if (request.VisualStatus == W24S5VisualStatus.L3 && visualQa == null) result.Error("W24S5-085", "visualStatus", "L3 cannot be granted by public request data; a qualified S0a status and independent persisted Visual QA pass are required.");
            if (request.VisualStatus != W24S5VisualStatus.VISUAL_PENDING && request.VisualStatus != W24S5VisualStatus.L3 && request.VisualStatus != W24S5VisualStatus.L4 && request.VisualStatus != W24S5VisualStatus.LEGACY) result.Error("W24S5-061", "visualStatus", "Unknown visual status.");
        }

        internal static W24S5PersistedFile ReadPersisted(W24S5ProductionGateResult result, string relativePath, string expectedHash, string field, string code, W24S5RecordScope scope)
        {
            string absolute;
            if (!TryResolvePersistedPath(relativePath, scope, out absolute)) { result.Error(code, field + "Path", "Formal records must use a safe approved docs/ or ProjectSettings/ path."); return null; }
            if (!W24Hash.IsCanonical(expectedHash)) { result.Error(code, field + "FileHash", "A canonical SHA-256 hash of persisted file bytes is required."); return null; }
            if (!File.Exists(absolute)) { result.Error(code, field + "Path", "Persisted formal record is missing."); return null; }
            byte[] bytes;
            try { bytes = File.ReadAllBytes(absolute); } catch (IOException e) { result.Error(code, field + "Path", "Could not read persisted record: " + e.Message); return null; }
            var hash = W24S5Hash.Sha256Bytes(bytes);
            if (!Same(hash, expectedHash)) { result.Error(code, field + "FileHash", "Persisted record bytes do not match the supplied hash."); return null; }
            try { return new W24S5PersistedFile { RelativePath = relativePath, Hash = hash, Text = new UTF8Encoding(false, true).GetString(bytes) }; }
            catch (DecoderFallbackException e) { result.Error(code, field, "Persisted record is not strict UTF-8: " + e.Message); return null; }
        }

        private static void CopyS1Issues(W24S5ProductionGateResult result, W24GateReport report, string prefix) { foreach (var issue in report.Issues) { if (issue.Severity == W24GateSeverity.Error) result.Error(issue.Code, prefix + "." + issue.Path, issue.Message); else result.Warning(issue.Code, prefix + "." + issue.Path, issue.Message); } }
        private static W24S5ProductionGateResult Finish(W24S5ProductionGateResult result) { result.CanBuild = false; result.CanPublish = false; return result; }
        private static VfxOutputAuditResult BootstrapError(string code, string message) { var audit = new VfxOutputAuditResult(); audit.Report.Add(code, ValidationSeverity.Error, "/formalProduction", message); return audit; }
        private static VfxOutputAuditResult BootstrapFailure(string effectId, string ownedOutputRoot, W24S5OwnedOutputSnapshot outputSnapshot, string priorManifest, string code, string message) { var audit = BootstrapError(code, message); AppendRollbackFailure(audit, effectId, ownedOutputRoot, outputSnapshot, priorManifest); return audit; }
        private static void AppendRollbackFailure(VfxOutputAuditResult audit, string effectId, string ownedOutputRoot, W24S5OwnedOutputSnapshot outputSnapshot, string priorManifest)
        {
            var failures = new List<string>();
            // Rollback is best-effort, but its own failure must never hide the original transaction
            // error. InvalidDataException and other non-IO failures are security-relevant too.
            try { RestoreOwnedOutputSnapshot(ownedOutputRoot, outputSnapshot); } catch (Exception e) { failures.Add("owned outputs (" + e.GetType().FullName + "): " + e.Message); }
            try { VfxProductionRules.RestoreManifest(effectId, priorManifest); } catch (Exception e) { failures.Add("manifest (" + e.GetType().FullName + "): " + e.Message); }
            if (failures.Count != 0) audit.Report.Add("E24S5PRE048", ValidationSeverity.Error, "/formalProduction", "Bootstrap rollback could not fully restore the prior state: " + string.Join(" | ", failures.ToArray()));
        }
        private static W24S5OwnedOutputSnapshot CaptureOwnedOutputSnapshot(string ownedOutputRoot)
        {
            if (!IsSafeOwnedOutputRoot(ownedOutputRoot)) throw new InvalidDataException("Unsafe owned-output root.");
            var root = VfxProjectRules.ProjectAbsolute(ownedOutputRoot);
            RejectOwnedOutputReparsePoints(root);
            var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            if (!Directory.Exists(root)) return new W24S5OwnedOutputSnapshot(false, files);
            var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (var absolute in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                var full = Path.GetFullPath(absolute);
                RejectOwnedOutputReparsePoints(full);
                if (!full.StartsWith(rootFull, StringComparison.Ordinal)) throw new InvalidDataException("Owned-output snapshot path escaped its root.");
                var relative = full.Substring(rootFull.Length).Replace('\\', '/');
                if (!IsSafeOwnedRelativePath(relative) || files.ContainsKey(relative)) throw new InvalidDataException("Owned-output snapshot contains an invalid or duplicate file path.");
                files.Add(relative, File.ReadAllBytes(full));
            }
            return new W24S5OwnedOutputSnapshot(true, files);
        }
        private static void RestoreOwnedOutputSnapshot(string ownedOutputRoot, W24S5OwnedOutputSnapshot snapshot)
        {
            if (snapshot == null) return;
            if (!IsSafeOwnedOutputRoot(ownedOutputRoot)) throw new InvalidDataException("Unsafe owned-output root during rollback.");
            var root = VfxProjectRules.ProjectAbsolute(ownedOutputRoot);
            RejectOwnedOutputReparsePoints(root);
            var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (Directory.Exists(root))
            {
                foreach (var absolute in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    var full = Path.GetFullPath(absolute);
                    RejectOwnedOutputReparsePoints(full);
                    if (!full.StartsWith(rootFull, StringComparison.Ordinal)) throw new InvalidDataException("Owned-output rollback path escaped its root.");
                    var relative = full.Substring(rootFull.Length).Replace('\\', '/');
                    if (!IsSafeOwnedRelativePath(relative)) throw new InvalidDataException("Owned-output rollback encountered an invalid file path.");
                    if (!snapshot.Files.ContainsKey(relative)) { RejectOwnedOutputReparsePoints(full); File.Delete(full); }
                }
            }
            foreach (var item in snapshot.Files)
            {
                if (!IsSafeOwnedRelativePath(item.Key)) throw new InvalidDataException("Owned-output snapshot contains an invalid file path.");
                var absolute = Path.GetFullPath(Path.Combine(root, item.Key.Replace('/', Path.DirectorySeparatorChar)));
                if (!absolute.StartsWith(rootFull, StringComparison.Ordinal)) throw new InvalidDataException("Owned-output snapshot restore path escaped its root.");
                RejectOwnedOutputReparsePoints(Path.GetDirectoryName(absolute));
                Directory.CreateDirectory(Path.GetDirectoryName(absolute));
                if (!File.Exists(absolute) || !File.ReadAllBytes(absolute).SequenceEqual(item.Value)) { RejectOwnedOutputReparsePoints(absolute); File.WriteAllBytes(absolute, item.Value); }
            }
            if (!snapshot.RootExisted && Directory.Exists(root))
            {
                foreach (var directory in Directory.GetDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length))
                {
                    RejectOwnedOutputReparsePoints(directory);
                    if (!Directory.EnumerateFileSystemEntries(directory).Any()) { RejectOwnedOutputReparsePoints(directory); Directory.Delete(directory, false); }
                }
                RejectOwnedOutputReparsePoints(root);
                if (!Directory.EnumerateFileSystemEntries(root).Any()) { RejectOwnedOutputReparsePoints(root); Directory.Delete(root, false); }
            }
            AssetDatabase.Refresh();
        }
        private static bool IsSafeOwnedRelativePath(string relative) { return !string.IsNullOrEmpty(relative) && relative.IndexOf('\\') < 0 && relative.Split('/').All(segment => !string.IsNullOrEmpty(segment) && segment != "." && segment != ".."); }
        private static void RejectOwnedOutputReparsePoints(string path)
        {
            var boundary = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Assets", "VFX");
            if (HasReparsePointAtOrAbove(path, boundary)) throw new InvalidDataException("Owned-output snapshot path contains a symlink/junction/reparse point: " + path);
        }
        internal static bool TryResolvePersistedPath(string relativePath, W24S5RecordScope scope, out string absolute)
        {
            absolute = null;
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || relativePath.IndexOf('\\') >= 0 || relativePath.IndexOf('\0') >= 0 || relativePath.Split('/').Any(segment => string.IsNullOrEmpty(segment) || segment == "." || segment == "..")) return false;
            var allowed = scope == W24S5RecordScope.Verdict ? relativePath.StartsWith(VerdictRoot, StringComparison.Ordinal) : scope == W24S5RecordScope.EvidenceCorpus ? relativePath.StartsWith(EvidenceCorpusRoot, StringComparison.Ordinal) : scope == W24S5RecordScope.VisualQa ? relativePath.StartsWith("docs/vfx-qa/", StringComparison.Ordinal) : scope == W24S5RecordScope.S0aStatus ? relativePath.StartsWith("docs/vfx-calibration/", StringComparison.Ordinal) : relativePath.StartsWith(DocsRoot, StringComparison.Ordinal) || relativePath.StartsWith("ProjectSettings/", StringComparison.Ordinal);
            if (!allowed) return false;
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var root = relativePath.StartsWith(DocsRoot, StringComparison.Ordinal) ? Directory.GetParent(projectRoot).FullName : projectRoot;
            var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!candidate.StartsWith(rootFull, StringComparison.Ordinal)) return false;
            if (HasReparsePointAtOrAbove(candidate, root)) return false;
            absolute = candidate;
            return true;
        }
        // Canonical strings are insufficient on Windows: a junction can redirect an otherwise
        // in-root segment after lexical validation.  Reject both a file link and every existing
        // parent up to the scope root before any formal evidence is read or written.
        private static bool HasReparsePointAtOrAbove(string path, string boundary)
        {
            try
            {
                var stop = Path.GetFullPath(boundary).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                for (var current = new DirectoryInfo(Path.GetFullPath(path)); current != null; current = current.Parent)
                {
                    if ((File.Exists(current.FullName) || Directory.Exists(current.FullName)) && (File.GetAttributes(current.FullName) & FileAttributes.ReparsePoint) != 0) return true;
                    if (string.Equals(current.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), stop, StringComparison.OrdinalIgnoreCase)) break;
                }
                return false;
            }
            catch (IOException) { return true; }
            catch (UnauthorizedAccessException) { return true; }
        }
        private static bool FileMatches(W24S5PersistedFile file, W24S5RecordScope scope) { string path; if (file == null || !TryResolvePersistedPath(file.RelativePath, scope, out path)) return false; try { return File.Exists(path) && Same(W24S5Hash.Sha256Bytes(File.ReadAllBytes(path)), file.Hash); } catch (IOException) { return false; } }
        private static string NormalizeAssetPath(string path) { return string.IsNullOrEmpty(path) ? null : path.Replace('\\', '/').TrimEnd('/'); }
        private static bool IsSafeOwnedOutputRoot(string path)
        {
            path = NormalizeAssetPath(path);
            return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/VFX/", StringComparison.Ordinal) && !Path.IsPathRooted(path) && path.IndexOf('\\') < 0 && path.Split('/').All(segment => !string.IsNullOrEmpty(segment) && segment != "." && segment != "..");
        }
        private static bool IsExactEffectOwnedOutputRoot(string path, string effectId) { path = NormalizeAssetPath(path); return IsSafeOwnedOutputRoot(path) && IsEffectId(effectId) && Same(path.Substring(path.LastIndexOf('/') + 1), effectId); }
        private static bool IsUnderOwnedOutputRoot(string path, string root)
        {
            path = NormalizeAssetPath(path); root = NormalizeAssetPath(root);
            return IsSafeOwnedOutputRoot(root) && !string.IsNullOrEmpty(path) && path.StartsWith(root + "/", StringComparison.Ordinal) && !path.Split('/').Any(segment => string.IsNullOrEmpty(segment) || segment == "." || segment == "..");
        }
        private static bool IsLegacyOutputPath(string effectId, string path) { return !string.IsNullOrWhiteSpace(path) && path.IndexOf('\\') < 0 && !Path.IsPathRooted(path) && path.Split('/').All(segment => !string.IsNullOrEmpty(segment) && segment != "." && segment != "..") && path.StartsWith(GeneratedOutputRoot + effectId + "/", StringComparison.Ordinal); }
        private static bool IsRawHash(string value) { return value != null && value.Length == 64 && value.All(character => (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')); }
        private static bool SameRawHash(string actual, string expected) { return IsRawHash(actual) && IsRawHash(expected) && Same(actual, expected); }
        private static bool IsGuid(string value) { return value != null && value.Length == 32 && value.All(character => (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')); }
        private static string MetaGuid(string metaPath) { var match = Regex.Match(File.ReadAllText(metaPath), "^guid:\\s*([0-9a-f]{32})\\s*$", RegexOptions.Multiline); return match.Success ? match.Groups[1].Value : null; }
        private static string Sha256Raw(byte[] bytes) { using (var sha = System.Security.Cryptography.SHA256.Create()) return string.Concat(sha.ComputeHash(bytes ?? Array.Empty<byte>()).Select(value => value.ToString("x2"))); }
        private static string ToProjectRelative(string absolute) { var projectRoot = Directory.GetParent(Application.dataPath).FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar; var full = Path.GetFullPath(absolute); return full.StartsWith(projectRoot, StringComparison.Ordinal) ? full.Substring(projectRoot.Length).Replace('\\', '/') : null; }
        private static void ValidateFirstFormalContract(W24S5ProductionGateResult result, W24S5FirstFormalBuildRequest request, VfxDesignContract contract)
        {
            if (contract == null) return;
            if (!Same(contract.EffectId, request.EffectId)) result.Error("W24S5-PRE011", "designContract.effectId", "Contract effectId must equal the first-build effect.");
            if (!Same(contract.ContractHash, VfxDesignContractJson.ComputeContractHash(contract))) result.Error("W24S5-PRE012", "designContract.contractHash", "Contract hash must be the authoritative S1 canonical hash.");
            var extensions = contract.Extensions;
            if (extensions == null || !Same((string)extensions["captureBindingStatus"], "PENDING_FIRST_FORMAL_BUILD")) result.Error("W24S5-PRE013", "designContract.extensions.captureBindingStatus", "Pre-C0 contract must declare PENDING_FIRST_FORMAL_BUILD.");
            if (extensions == null || !Same((string)extensions["visualStatus"], "VISUAL_PENDING")) result.Error("W24S5-PRE014", "designContract.extensions.visualStatus", "Pre-C0 contract must remain VISUAL_PENDING.");
            if (extensions == null || !Same((string)extensions["runtimeEntry"], request.ExpectedRuntimeEntryPath)) result.Error("W24S5-PRE015", "designContract.extensions.runtimeEntry", "Contract must authorize this exact planned Runtime Entry.");
            var capture = contract.CaptureProfile;
            if (capture == null || !Same(capture.SceneHash, "pending:formal-build") || !Same(capture.PrefabManifestHash, "pending:formal-build") || !Same(capture.PrefabManifestSerializedReference, request.ExpectedManifestPath + "#buildHash")) result.Error("W24S5-PRE016", "designContract.captureProfile", "Pre-C0 capture/build identities must use pending:formal-build and the authoritative manifest reference.");
            if (extensions == null || !Same((string)extensions["implementationTrace"], request.TracePath)) result.Error("W24S5-PRE017", "designContract.extensions.implementationTrace", "Contract must bind this exact persisted preregistration Trace path.");
            var declaredManifest = extensions == null ? null : (string)extensions["manifest"];
            if (!string.IsNullOrEmpty(declaredManifest) && !Same(declaredManifest, request.ExpectedManifestPath)) result.Error("W24S5-PRE018", "designContract.extensions.manifest", "When present, contract manifest metadata must name the authoritative manifest path.");
        }

        private static void ValidateFirstFormalTrace(W24S5ProductionGateResult result, W24S5FirstFormalBuildRequest request, VfxDesignContract contract, VfxImplementationTrace trace)
        {
            if (string.IsNullOrWhiteSpace(trace.TraceVersion)) result.Error("W24S5-PRE019", "implementationTrace.traceVersion", "Pre-C0 trace must have an explicit version.");
            if (!Same(trace.TraceStatus, "PENDING_FIRST_FORMAL_BUILD_BINDING")) result.Error("W24S5-PRE021", "implementationTrace.traceStatus", "Pre-C0 trace must declare PENDING_FIRST_FORMAL_BUILD_BINDING.");
            if (!Same(trace.EffectId, request.EffectId) || trace.ContractRevision != contract.ContractRevision || !Same(trace.ContractHash, contract.ContractHash)) result.Error("W24S5-PRE022", "implementationTrace", "Pre-C0 trace must bind the exact effect, contract revision, and contract hash.");
            if (!Same(trace.BuildHash, "pending:formal-build") || !Same(trace.CaptureProfileHash, "pending:formal-build") || !Same(trace.RuntimeEntryGuid, "pending:formal-build")) result.Error("W24S5-PRE023", "implementationTrace", "Pre-C0 build, capture, and Runtime Entry GUID identities must all be pending:formal-build.");
            if (!Same(trace.RuntimeEntryAssetPath, request.ExpectedRuntimeEntryPath)) result.Error("W24S5-PRE024", "implementationTrace.runtimeEntryAssetPath", "Trace must bind the exact planned Runtime Entry path.");

            var requirements = new Dictionary<string, VfxDesignRequirement>(StringComparer.Ordinal);
            foreach (var requirement in contract.Requirements ?? Array.Empty<VfxDesignRequirement>()) if (requirement != null && !string.IsNullOrEmpty(requirement.DesignRequirementId) && !requirements.ContainsKey(requirement.DesignRequirementId)) requirements.Add(requirement.DesignRequirementId, requirement);
            var states = new HashSet<string>((contract.SemanticStateMachine == null ? Array.Empty<VfxSemanticState>() : contract.SemanticStateMachine.States ?? Array.Empty<VfxSemanticState>()).Where(state => state != null).Select(state => state.StateId), StringComparer.Ordinal);
            var layers = new HashSet<string>((contract.Layers ?? Array.Empty<VfxLayer>()).Where(layer => layer != null).Select(layer => layer.LayerId), StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var mappedLayers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in trace.RequirementTraces ?? Array.Empty<VfxRequirementTrace>())
            {
                if (item == null || string.IsNullOrEmpty(item.DesignRequirementId) || !seen.Add(item.DesignRequirementId)) { result.Error("W24S5-PRE025", "implementationTrace.requirementTraces", "Trace requirement IDs must be non-empty and unique."); continue; }
                VfxDesignRequirement requirement;
                if (!requirements.TryGetValue(item.DesignRequirementId, out requirement)) { result.Error("W24S5-PRE026", "implementationTrace.requirementTraces", "Trace cannot add a requirement absent from the contract."); continue; }
                if (!Same(item.EvidenceAuthority, requirement.EvidenceAuthority)) result.Error("W24S5-PRE027", "implementationTrace.requirementTraces." + item.DesignRequirementId + ".evidenceAuthority", "Trace authority must equal its contract requirement authority.");
                if ((item.AuthorityEvidence != null && item.AuthorityEvidence.Length != 0) || (item.CrossEvidence != null && item.CrossEvidence.Length != 0)) result.Error("W24S5-PRE028", "implementationTrace.requirementTraces." + item.DesignRequirementId, "Pre-C0 trace may not claim authority or cross evidence before capture.");
                var traceStates = item.StateIds ?? Array.Empty<string>();
                if (requirement.Type == "behavioral" && traceStates.Length == 0) result.Error("W24S5-PRE029", "implementationTrace.requirementTraces." + item.DesignRequirementId + ".stateIds", "Behavioral preregistration requires a contract state mapping.");
                if (traceStates.Any(state => string.IsNullOrEmpty(state) || !states.Contains(state))) result.Error("W24S5-PRE030", "implementationTrace.requirementTraces." + item.DesignRequirementId + ".stateIds", "Trace states must exist in the contract state machine.");
                var traceLayers = item.LayerIds ?? Array.Empty<string>();
                var visual = requirement.Type == "visual-measurable" || requirement.Type == "visual-semantic";
                if (visual && traceLayers.Length == 0) result.Error("W24S5-PRE031", "implementationTrace.requirementTraces." + item.DesignRequirementId + ".layerIds", "Visual preregistration requires a contract layer mapping.");
                foreach (var layer in traceLayers)
                {
                    if (string.IsNullOrEmpty(layer) || !layers.Contains(layer)) result.Error("W24S5-PRE032", "implementationTrace.requirementTraces." + item.DesignRequirementId + ".layerIds", "Trace layers must exist in the contract.");
                    else mappedLayers.Add(layer);
                }
                var objects = item.Objects ?? Array.Empty<VfxTraceObject>();
                if (requirement.EvidenceAuthority == "telemetry" && objects.Length == 0) result.Error("W24S5-PRE033", "implementationTrace.requirementTraces." + item.DesignRequirementId + ".objects", "Telemetry preregistration requires planned component mappings.");
                foreach (var mapped in objects)
                {
                    if (mapped == null || string.IsNullOrWhiteSpace(mapped.HierarchyPath) || mapped.HierarchyPath.IndexOf("..", StringComparison.Ordinal) >= 0 || string.IsNullOrWhiteSpace(mapped.ComponentType)) result.Error("W24S5-PRE034", "implementationTrace.requirementTraces." + item.DesignRequirementId + ".objects", "Planned object mappings require a non-traversing hierarchy path and component type.");
                    if (mapped != null && !string.IsNullOrEmpty(mapped.AssetPath) && !IsUnderOwnedOutputRoot(mapped.AssetPath, request.OwnedOutputRoot)) result.Error("W24S5-PRE035", "implementationTrace.requirementTraces." + item.DesignRequirementId + ".objects.assetPath", "Planned object paths must stay under the owned-output root.");
                }
            }
            foreach (var requirement in requirements.Keys) if (!seen.Contains(requirement)) result.Error("W24S5-PRE036", "implementationTrace.requirementTraces", "Every contract requirement needs one preregistration trace.");
            foreach (var layer in layers) if (!mappedLayers.Contains(layer)) result.Error("W24S5-PRE037", "implementationTrace.requirementTraces", "Every contract layer needs a reverse preregistration trace mapping: " + layer + ".");
        }

        private static bool IsEffectId(string value) { return !string.IsNullOrEmpty(value) && char.IsLower(value[0]) && value.All(character => char.IsLower(character) || char.IsDigit(character) || character == '_') && !value.Contains("__") && value[value.Length - 1] != '_'; }
        private static bool Same(string a, string b) { return string.Equals(a, b, StringComparison.Ordinal); }
    }

    internal static class W24S5Hash
    {
        internal static string Sha256Bytes(byte[] bytes)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create()) return "sha256:" + string.Concat(sha.ComputeHash(bytes ?? Array.Empty<byte>()).Select(value => value.ToString("x2")));
        }
    }
}
