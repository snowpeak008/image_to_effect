using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Validation;
using VFXComposer.Editor.W24.S1;

namespace VFXComposer.Editor.W24.S5
{
    /// <summary>The only caller-controlled inputs accepted by the read-only replay boundary.</summary>
    internal sealed class W24S5CandidateEvidenceReadRequest
    {
        internal string CandidateReceiptPath;
        internal string CandidateReceiptFileHash;
        internal int EvidenceRevision;
    }

    internal sealed class W24S5CandidateEvidenceSourceSnapshot
    {
        internal string SourcePath;
        internal string SourceSha256;
        internal string SnapshotPath;
        internal string SnapshotFileHash;
    }

    /// <summary>Immutable identities established by replay. This object is not an adjudication.</summary>
    internal sealed class W24S5CandidateEvidenceSnapshot
    {
        internal string CandidateVersion;
        internal string EffectId;
        internal string CandidateId;
        internal int CandidateRevision;
        internal int ContractRevision;
        internal int EvidenceRevision;
        internal string CandidateRoot;
        internal string CandidateReceiptPath;
        internal string CandidateReceiptFileHash;
        internal string ContractPath;
        internal string ContractFileHash;
        internal string ContractHash;
        internal string PendingTracePath;
        internal string PendingTraceFileHash;
        internal string ProductionManifestPath;
        internal string ManifestSnapshotPath;
        internal string ManifestSnapshotFileHash;
        internal string BuildHash;
        internal string CaptureProfileHash;
        internal string RuntimeEntryPath;
        internal string RuntimeEntryGuid;
        internal string PreviewScenePath;
        internal string PreviewSceneFileHash;
        internal string CaptureToolBundleSnapshotPath;
        internal string CaptureToolBundleSnapshotFileHash;
        internal string CaptureToolBundleCanonicalHash;
        internal string EvidenceRoot;
        internal string EvidenceRevisionPath;
        internal string EvidenceRevisionFileHash;
        internal string CaptureMetadataPath;
        internal string CaptureMetadataFileHash;
        internal string CompletedTracePath;
        internal string CompletedTraceFileHash;
        internal string EvidenceSealPath;
        internal string EvidenceSealFileHash;
        internal string ProducerBundlePath;
        internal string ProducerBundleFileHash;
        internal readonly List<W24S5CandidateEvidenceSourceSnapshot> CaptureToolSources = new List<W24S5CandidateEvidenceSourceSnapshot>();
        internal readonly List<string> SealedEvidenceFiles = new List<string>();
    }

    /// <summary>Only INVALID or VALID_READ_ONLY can cross this boundary; no machine verdict exists here.</summary>
    internal sealed class W24S5CandidateEvidenceReadResult
    {
        internal const string InvalidStatus = "INVALID";
        internal const string ValidReadOnlyStatus = "VALID_READ_ONLY";
        internal string Status = InvalidStatus;
        internal W24S5CandidateEvidenceSnapshot Snapshot;
        internal readonly List<string> Errors = new List<string>();
        internal bool IsValidReadOnly { get { return string.Equals(Status, ValidReadOnlyStatus, StringComparison.Ordinal); } }
        internal void Invalid(string message) { Errors.Add(string.IsNullOrWhiteSpace(message) ? "Candidate/evidence replay is invalid." : message); }
    }

    /// <summary>
    /// Candidate-only replay is intentionally a separate capability from Read().  The returned
    /// authority can only be constructed by this reader after every immutable candidate input has
    /// replayed; callers cannot manufacture a Snapshot and pair it with a convenient error string.
    /// It is still read-only authority and carries no evidence or machine-gate verdict.
    /// </summary>
    internal sealed class W24S5CandidateOnlyReplayResult
    {
        internal const string InvalidStatus = "INVALID";
        internal const string ValidCandidateReadOnlyStatus = "VALID_CANDIDATE_READ_ONLY";
        internal string Status = InvalidStatus;
        internal W24S5CandidateEvidenceReader.CandidateReplayAuthority Authority;
        internal readonly List<string> Errors = new List<string>();
        internal bool IsValidCandidateReadOnly
        {
            get { return string.Equals(Status, ValidCandidateReadOnlyStatus, StringComparison.Ordinal) && Authority != null; }
        }
        internal void Invalid(string message)
        {
            Errors.Add(string.IsNullOrWhiteSpace(message) ? "Candidate replay is invalid." : message);
        }
    }

    /// <summary>
    /// Bounded, read-only replay of an immutable candidate and one E1/E2 evidence namespace.
    /// It writes nothing, issues no authority, and intentionally cannot produce an adjudication.
    /// </summary>
    internal static class W24S5CandidateEvidenceReader
    {
        internal const string ReaderVersion = "w24-s5-candidate-evidence-reader/1";
        internal const int MaxDocumentBytes = 1024 * 1024;
        internal const int MaxSourceBytes = 2 * 1024 * 1024;
        internal const int MaxOwnedAssetBytes = 128 * 1024 * 1024;
        internal const long MaxOwnedAssetTotalBytes = 1024L * 1024L * 1024L;
        internal const int MaxCandidateFiles = 512;
        internal const int MaxEvidenceFiles = 128;
        internal const int MaxDirectories = 64;
        internal const int MaxTreeDepth = 8;
        internal const long MaxCandidateBytes = 256L * 1024L * 1024L;
        internal const long MaxEvidenceBytes = 16L * 1024L * 1024L;
        internal const int MaxOwnedOutputs = 256;
        internal const int MaxSourceRecords = 32;
        internal const int MaxArtifactRecords = 512;
        internal const int MaxDependencyRecords = 256;
        internal const int MaxAuditRecords = 256;

        private const int MaxPathCharacters = 512;
        private const int MaxPathSegmentCharacters = 128;
        private const int MaxRevision = 1000000;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private sealed class PinnedFile { internal byte[] Bytes; internal string Text; internal string Hash; }
        private sealed class WalkItem { internal string AbsolutePath; internal string RelativePath; internal int Depth; }
        private static readonly object CandidateReplayAuthorityIssuer = new object();

        /// <summary>
        /// Opaque, non-constructible proof of a successful candidate-only replay.  Its immutable
        /// projections are exposed solely so the descriptor writer can pin the already-verified
        /// candidate bytes; the mutable Snapshot object never crosses this boundary.
        /// </summary>
        internal sealed class CandidateReplayAuthority
        {
            private readonly W24S5CandidateEvidenceSnapshot value;
            internal CandidateReplayAuthority(object issuer, W24S5CandidateEvidenceSnapshot snapshot)
            {
                if (!ReferenceEquals(issuer, CandidateReplayAuthorityIssuer))
                    throw new InvalidOperationException("Candidate replay authority is reader-issued only.");
                value = snapshot ?? throw new ArgumentNullException("snapshot");
            }

            internal string CandidateVersion { get { return value.CandidateVersion; } }
            internal string EffectId { get { return value.EffectId; } }
            internal string CandidateId { get { return value.CandidateId; } }
            internal int CandidateRevision { get { return value.CandidateRevision; } }
            internal int ContractRevision { get { return value.ContractRevision; } }
            internal int EvidenceRevision { get { return value.EvidenceRevision; } }
            internal string CandidateRoot { get { return value.CandidateRoot; } }
            internal string CandidateReceiptPath { get { return value.CandidateReceiptPath; } }
            internal string CandidateReceiptFileHash { get { return value.CandidateReceiptFileHash; } }
            internal string ContractPath { get { return value.ContractPath; } }
            internal string ContractFileHash { get { return value.ContractFileHash; } }
            internal string ContractHash { get { return value.ContractHash; } }
            internal string PendingTracePath { get { return value.PendingTracePath; } }
            internal string PendingTraceFileHash { get { return value.PendingTraceFileHash; } }
            internal string ManifestSnapshotPath { get { return value.ManifestSnapshotPath; } }
            internal string ManifestSnapshotFileHash { get { return value.ManifestSnapshotFileHash; } }
            internal string ProductionManifestPath { get { return value.ProductionManifestPath; } }
            internal string BuildHash { get { return value.BuildHash; } }
            internal string CaptureProfileHash { get { return value.CaptureProfileHash; } }
            internal string RuntimeEntryPath { get { return value.RuntimeEntryPath; } }
            internal string RuntimeEntryGuid { get { return value.RuntimeEntryGuid; } }
            internal string PreviewScenePath { get { return value.PreviewScenePath; } }
            internal string PreviewSceneFileHash { get { return value.PreviewSceneFileHash; } }
        }

        internal static W24S5CandidateEvidenceReadResult Read(W24S5CandidateEvidenceReadRequest request)
        {
            var result = new W24S5CandidateEvidenceReadResult();
            try
            {
                var snapshot = ReplayCandidate(request);
                result.Snapshot = snapshot;
                ReplayEvidence(snapshot);
                result.Status = W24S5CandidateEvidenceReadResult.ValidReadOnlyStatus;
            }
            catch (Exception error) when (ExpectedInputFailure(error))
            {
                result.Invalid(error.Message);
            }
            return result;
        }

        /// <summary>
        /// Replays only the immutable candidate namespace.  This deliberately does not call
        /// Read(), ReplayEvidence(), or infer success from Read().Snapshot plus diagnostic text.
        /// Existing Read() behavior and its INVALID/VALID_READ_ONLY contract remain unchanged.
        /// </summary>
        internal static W24S5CandidateOnlyReplayResult ReplayCandidateOnly(W24S5CandidateEvidenceReadRequest request)
        {
            var result = new W24S5CandidateOnlyReplayResult();
            try
            {
                result.Authority = new CandidateReplayAuthority(CandidateReplayAuthorityIssuer, ReplayCandidate(request));
                result.Status = W24S5CandidateOnlyReplayResult.ValidCandidateReadOnlyStatus;
            }
            catch (Exception error) when (ExpectedInputFailure(error))
            {
                result.Invalid(error.Message);
            }
            return result;
        }

        private static W24S5CandidateEvidenceSnapshot ReplayCandidate(W24S5CandidateEvidenceReadRequest request)
        {
            if (request == null) throw new InvalidDataException("A candidate/evidence read request is required.");
            if (!SafeRepositoryPath(request.CandidateReceiptPath)
                || !request.CandidateReceiptPath.StartsWith("docs/vfx-candidates/", StringComparison.Ordinal)
                || !request.CandidateReceiptPath.EndsWith("/candidate-receipt.json", StringComparison.Ordinal)
                || !CanonicalHash(request.CandidateReceiptFileHash)
                || request.EvidenceRevision < 1 || request.EvidenceRevision > 2)
                throw new InvalidDataException("The request must pin one canonical candidate receipt and evidence revision E1 or E2.");

            var receiptFile = ReadRepositoryPinned(request.CandidateReceiptPath, request.CandidateReceiptFileHash, "candidate receipt", MaxDocumentBytes);
            var receipt = Parse(receiptFile.Text, "candidate receipt");
            var version = RequiredString(receipt, "candidateVersion", 64);
            var snapshot = string.Equals(version, "w24-candidate/1.0", StringComparison.Ordinal)
                ? ReplayLegacyCandidate(receipt, receiptFile, request)
                : string.Equals(version, "w24-candidate-revision/2.0", StringComparison.Ordinal)
                    ? ReplayRevisionCandidate(receipt, receiptFile, request)
                    : throw new InvalidDataException("Candidate receipt version is unsupported.");

            return snapshot;
        }

        private static W24S5CandidateEvidenceSnapshot ReplayLegacyCandidate(JObject receipt, PinnedFile receiptFile, W24S5CandidateEvidenceReadRequest request)
        {
            RequireExactly(receipt,
                "candidateVersion", "candidateId", "candidateRevision", "candidateStatus", "effectId",
                "bootstrapContractPath", "bootstrapContractFileHash", "bootstrapContractHash", "bootstrapContractRevision",
                "bootstrapTracePath", "bootstrapTraceFileHash", "productionManifestPath",
                "bootstrapManifestSnapshotPath", "bootstrapManifestSnapshotFileHash", "ownedOutputs", "buildHash",
                "runtimeEntryPath", "runtimeEntryGuid", "previewScenePath", "previewSceneHash", "contractPath",
                "contractFileHash", "contractHash", "tracePath", "traceFileHash", "captureProfileHash", "visualStatus");
            var effectId = RequiredEffectId(receipt, "effectId");
            RequireExactString(receipt, "candidateId", "C0");
            RequireExactString(receipt, "candidateStatus", "C0_CAPTURE_PENDING");
            RequireExactString(receipt, "visualStatus", "VISUAL_PENDING");
            if (RequiredInt(receipt, "candidateRevision", 0, 0) != 0) throw new InvalidDataException("Legacy candidate revision is not C0.");
            var root = "docs/vfx-candidates/" + effectId + "/C0";
            if (!Same(request.CandidateReceiptPath, root + "/candidate-receipt.json")) throw new InvalidDataException("Legacy C0 receipt path does not match its effect identity.");
            var value = CandidateCommon(receipt, receiptFile, request, root, 0, "w24-candidate/1.0",
                "bootstrapManifestSnapshotPath", "bootstrapManifestSnapshotFileHash");
            var bootstrapContractPath = RequiredPath(receipt, "bootstrapContractPath");
            var bootstrapContractFileHash = RequiredHash(receipt, "bootstrapContractFileHash");
            var bootstrapTracePath = RequiredPath(receipt, "bootstrapTracePath");
            var bootstrapTraceFileHash = RequiredHash(receipt, "bootstrapTraceFileHash");
            // FreezeC0 deliberately derives a new candidate-local Contract hash after binding
            // the real Preview/Manifest.  The predecessor bootstrap hash therefore must remain
            // distinct and is checked against formalProduction below, never against value.ContractHash.
            if (RequiredInt(receipt, "bootstrapContractRevision", 1, MaxRevision) != value.ContractRevision)
                throw new InvalidDataException("Legacy bootstrap Contract/Trace identity is malformed.");
            VerifyLegacyContractBindings(receipt, value, bootstrapContractPath, bootstrapContractFileHash, bootstrapTracePath, bootstrapTraceFileHash);
            VerifyCandidateStaticTree(value, new HashSet<string>(StringComparer.Ordinal)
            {
                request.CandidateReceiptPath, value.ContractPath, value.PendingTracePath, value.ManifestSnapshotPath
            });
            return value;
        }

        private static W24S5CandidateEvidenceSnapshot ReplayRevisionCandidate(JObject receipt, PinnedFile receiptFile, W24S5CandidateEvidenceReadRequest request)
        {
            RequireExactly(receipt,
                "candidateVersion", "candidateId", "candidateRevision", "contractRevisionNamespace", "candidateStatus",
                "infrastructureStatus", "effectId", "previousCandidateReceiptPath", "previousCandidateReceiptFileHash",
                "advanceAuthority", "productionManifestPath", "productionManifestInputFileHash",
                "productionManifestSnapshotPath", "productionManifestSnapshotFileHash", "ownedOutputRoot", "ownedOutputs",
                "buildHash", "runtimeEntryPath", "runtimeEntryGuid", "previewScenePath", "previewSceneHash", "contractPath",
                "contractFileHash", "contractHash", "contractRevision", "designSemanticHash", "tracePath", "traceFileHash",
                "captureProfileHash", "captureToolBundleInputPath", "captureToolBundleInputFileHash",
                "captureToolBundleSnapshotPath", "captureToolBundleSnapshotFileHash", "captureToolBundleCanonicalHash",
                "captureToolSourceSnapshots", "evidenceRoot", "evidenceRevision", "visualStatus", "visualQaRecordPath",
                "visualQaRecordFileHash", "userVerdictRecordPath", "userVerdictRecordFileHash", "maturityLevel");
            var effectId = RequiredEffectId(receipt, "effectId");
            var revision = RequiredInt(receipt, "candidateRevision", 1, 2);
            var candidateId = "C" + revision.ToString(CultureInfo.InvariantCulture);
            RequireExactString(receipt, "candidateId", candidateId);
            RequireExactString(receipt, "candidateStatus", candidateId + "_CAPTURE_PENDING");
            RequireExactString(receipt, "visualStatus", "VISUAL_PENDING");
            RequireExactString(receipt, "infrastructureStatus", "TEST_ONLY_TRANSACTION_INFRASTRUCTURE");
            RequireExactString(receipt, "maturityLevel", "L2_MAXIMUM_PENDING");
            RequireJsonNull(receipt, "visualQaRecordPath"); RequireJsonNull(receipt, "visualQaRecordFileHash");
            RequireJsonNull(receipt, "userVerdictRecordPath"); RequireJsonNull(receipt, "userVerdictRecordFileHash");
            var contractRevision = RequiredInt(receipt, "contractRevision", 1, MaxRevision);
            var revisionNamespace = "R" + contractRevision.ToString(CultureInfo.InvariantCulture);
            RequireExactString(receipt, "contractRevisionNamespace", revisionNamespace);
            var root = "docs/vfx-candidates/" + effectId + "/" + revisionNamespace + "/" + candidateId;
            if (!Same(request.CandidateReceiptPath, root + "/candidate-receipt.json")) throw new InvalidDataException("C1/C2 receipt path does not match its contract-revision namespace.");
            if (RequiredInt(receipt, "evidenceRevision", 0, 0) != 0 || !Same(RequiredPath(receipt, "evidenceRoot"), root + "/evidence")) throw new InvalidDataException("Pending candidate evidence root/revision is invalid.");
            VerifyPendingAuthorityMarker(RequiredObject(receipt, "advanceAuthority"));
            var value = CandidateCommon(receipt, receiptFile, request, root, revision, "w24-candidate-revision/2.0",
                "productionManifestSnapshotPath", "productionManifestSnapshotFileHash");
            if (value.ContractRevision != contractRevision) throw new InvalidDataException("Receipt and Contract revisions differ.");

            var bundlePath = RequiredPath(receipt, "captureToolBundleSnapshotPath");
            if (!Same(bundlePath, root + "/capture-tool.bundle.json")) throw new InvalidDataException("Capture-tool bundle snapshot path is not candidate-local and canonical.");
            var bundleFile = ReadRepositoryPinned(bundlePath, RequiredHash(receipt, "captureToolBundleSnapshotFileHash"), "capture-tool bundle snapshot", MaxDocumentBytes);
            var bundle = Parse(bundleFile.Text, "capture-tool bundle snapshot");
            RequireExactly(bundle, "bundleVersion", "toolVersion", "sources");
            RequiredString(bundle, "bundleVersion", 128); var toolVersion = RequiredString(bundle, "toolVersion", 128);
            var canonicalBundleHash = Hash(StrictUtf8.GetBytes(CanonicalJson(bundle)));
            if (!Same(canonicalBundleHash, RequiredHash(receipt, "captureToolBundleCanonicalHash"))) throw new InvalidDataException("Capture-tool bundle canonical hash differs from the receipt.");
            var contractFile = ReadRepositoryPinned(value.ContractPath, value.ContractFileHash, "candidate Contract replay", MaxDocumentBytes);
            var contractJson = Parse(contractFile.Text, "candidate Contract replay");
            if (!Same((string)contractJson.SelectToken("captureProfile.captureToolVersion"), toolVersion)
                || !Same((string)contractJson.SelectToken("captureProfile.captureToolHash"), canonicalBundleHash)
                || !Same((string)contractJson.SelectToken("extensions.captureToolBundle"), bundlePath))
                throw new InvalidDataException("Candidate Contract does not bind the immutable capture-tool bundle.");
            var inputPath = RequiredPath(receipt, "captureToolBundleInputPath");
            if (!Same(inputPath, "docs/vfx-contracts/capture-tools/" + effectId + "." + revisionNamespace + "." + candidateId + ".bundle.json")
                || !Same(RequiredHash(receipt, "captureToolBundleInputFileHash"), bundleFile.Hash))
                throw new InvalidDataException("Capture-tool input identity differs from the immutable bundle snapshot.");

            var declared = RequiredArray(bundle, "sources", 1, MaxSourceRecords);
            var sourceReceipts = RequiredArray(receipt, "captureToolSourceSnapshots", 1, MaxSourceRecords);
            if (declared.Count != sourceReceipts.Count) throw new InvalidDataException("Capture-tool source registry and candidate snapshots differ.");
            var expectedFiles = new HashSet<string>(StringComparer.Ordinal)
            {
                request.CandidateReceiptPath, value.ContractPath, value.PendingTracePath, value.ManifestSnapshotPath, bundlePath
            };
            var originalPaths = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < declared.Count; index++)
            {
                var source = RequiredArrayObject(declared[index], "capture-tool source");
                RequireExactly(source, "path", "sha256");
                var sourceReceipt = RequiredArrayObject(sourceReceipts[index], "capture-tool source snapshot receipt");
                RequireExactly(sourceReceipt, "sourcePath", "sourceSha256", "snapshotPath", "snapshotFileHash");
                var originalPath = RequiredPath(source, "path");
                var originalHash = RequiredHash(source, "sha256");
                var snapshotPath = RequiredPath(sourceReceipt, "snapshotPath");
                var expectedPath = root + "/capture-tool-sources/" + index.ToString("D4", CultureInfo.InvariantCulture) + ".source";
                if (!originalPaths.Add(originalPath)
                    || !Same(RequiredPath(sourceReceipt, "sourcePath"), originalPath)
                    || !Same(RequiredHash(sourceReceipt, "sourceSha256"), originalHash)
                    || !Same(snapshotPath, expectedPath))
                    throw new InvalidDataException("Capture-tool source snapshot registry is not exact and ordered.");
                var snapshotFile = ReadRepositoryPinned(snapshotPath, RequiredHash(sourceReceipt, "snapshotFileHash"), "capture-tool source snapshot", MaxSourceBytes);
                if (!Same(snapshotFile.Hash, originalHash)) throw new InvalidDataException("Capture-tool source snapshot bytes differ from the frozen bundle source hash.");
                value.CaptureToolSources.Add(new W24S5CandidateEvidenceSourceSnapshot
                {
                    SourcePath = originalPath, SourceSha256 = originalHash, SnapshotPath = snapshotPath, SnapshotFileHash = snapshotFile.Hash
                });
                expectedFiles.Add(snapshotPath);
            }
            value.CaptureToolBundleSnapshotPath = bundlePath;
            value.CaptureToolBundleSnapshotFileHash = bundleFile.Hash;
            value.CaptureToolBundleCanonicalHash = canonicalBundleHash;
            VerifyCandidateStaticTree(value, expectedFiles);
            return value;
        }

        private static W24S5CandidateEvidenceSnapshot CandidateCommon(
            JObject receipt, PinnedFile receiptFile, W24S5CandidateEvidenceReadRequest request, string root, int revision,
            string version, string manifestPathField, string manifestHashField)
        {
            var candidateId = "C" + revision.ToString(CultureInfo.InvariantCulture);
            var effectId = RequiredEffectId(receipt, "effectId");
            var contractPath = RequiredPath(receipt, "contractPath");
            var tracePath = RequiredPath(receipt, "tracePath");
            var manifestPath = RequiredPath(receipt, manifestPathField);
            var expectedManifestName = revision == 0 ? "bootstrap-manifest.json" : "production-manifest.json";
            if (!Same(contractPath, root + "/design-contract.json") || !Same(tracePath, root + "/implementation-trace.json") || !Same(manifestPath, root + "/" + expectedManifestName))
                throw new InvalidDataException("Candidate Contract/Trace/Manifest paths are not exact candidate-local paths.");
            var contractFile = ReadRepositoryPinned(contractPath, RequiredHash(receipt, "contractFileHash"), "candidate Contract", MaxDocumentBytes);
            var traceFile = ReadRepositoryPinned(tracePath, RequiredHash(receipt, "traceFileHash"), "candidate pending Trace", MaxDocumentBytes);
            var manifestFile = ReadRepositoryPinned(manifestPath, RequiredHash(receipt, manifestHashField), "candidate Manifest snapshot", MaxDocumentBytes);
            var contractJson = Parse(contractFile.Text, "candidate Contract");
            var traceJson = Parse(traceFile.Text, "candidate pending Trace");
            var manifestJson = Parse(manifestFile.Text, "candidate Manifest snapshot");
            VfxDesignContract contract;
            var contractReport = VfxDesignContractJson.ValidateJson(contractFile.Text, out contract);
            if (contractReport.HasErrors) throw new InvalidDataException("Candidate Contract failed S1 validation: " + Describe(contractReport));
            var trace = VfxImplementationTraceJson.FromJson(traceFile.Text);
            var contractHash = RequiredHash(receipt, "contractHash");
            var buildHash = RequiredHash(receipt, "buildHash");
            var captureProfileHash = RequiredHash(receipt, "captureProfileHash");
            var runtimePath = RequiredAssetPath(receipt, "runtimeEntryPath");
            var runtimeGuid = RequiredGuid(receipt, "runtimeEntryGuid");
            var computedCaptureHash = "sha256:" + RecipeCanonicalizer.ComputeSha256(RequiredObject(contractJson, "captureProfile").ToString(Formatting.None));
            var ownedPaths = new HashSet<string>(RequiredArray(receipt, "ownedOutputs", 1, MaxOwnedOutputs)
                .Select(item => RequiredAssetPath(RequiredArrayObject(item, "owned output receipt"), "path")), StringComparer.Ordinal);
            if (!Same(contract.EffectId, effectId) || !Same(contract.ContractHash, contractHash)
                || !Same(trace.TraceStatus, candidateId + "_CAPTURE_PENDING") || trace.CandidateRevision != revision || trace.EvidenceRevision != 0
                || trace.ContractRevision != contract.ContractRevision || !Same(trace.ContractHash, contractHash)
                || !Same(trace.BuildHash, buildHash) || !Same(trace.CaptureProfileHash, captureProfileHash)
                || !Same(captureProfileHash, computedCaptureHash) || !Same(trace.RuntimeEntryAssetPath, runtimePath)
                || !Same(trace.RuntimeEntryGuid, runtimeGuid) || !ValidPendingTrace(trace, contract, candidateId, revision, ownedPaths))
                throw new InvalidDataException("Candidate Contract/pending Trace identity or evidence-free owned-output plan differs from the receipt.");
            VerifyManifestAndOwnedOutputs(receipt, manifestJson, effectId, runtimePath, runtimeGuid, buildHash, revision);
            var previewPath = RequiredAssetPath(receipt, "previewScenePath");
            var previewHash = RequiredHash(receipt, "previewSceneHash");
            ReadProjectPinned(previewPath, previewHash, "candidate Preview Scene", MaxOwnedAssetBytes);
            if (!Same(contract.CaptureProfile.SceneSerializedReference, previewPath) || !Same(contract.CaptureProfile.SceneHash, previewHash)
                || !CameraBoundToPreview(contract.CaptureProfile.CameraSerializedReference, previewPath))
                throw new InvalidDataException("Candidate Preview Scene/camera binding differs from the Contract and receipt.");
            return new W24S5CandidateEvidenceSnapshot
            {
                CandidateVersion = version, EffectId = effectId, CandidateId = candidateId, CandidateRevision = revision,
                ContractRevision = contract.ContractRevision, EvidenceRevision = request.EvidenceRevision, CandidateRoot = root,
                CandidateReceiptPath = request.CandidateReceiptPath, CandidateReceiptFileHash = receiptFile.Hash,
                ContractPath = contractPath, ContractFileHash = contractFile.Hash, ContractHash = contractHash,
                PendingTracePath = tracePath, PendingTraceFileHash = traceFile.Hash, ManifestSnapshotPath = manifestPath,
                ManifestSnapshotFileHash = manifestFile.Hash, BuildHash = buildHash, CaptureProfileHash = captureProfileHash,
                ProductionManifestPath = RequiredPath(receipt, "productionManifestPath"), RuntimeEntryPath = runtimePath,
                RuntimeEntryGuid = runtimeGuid, PreviewScenePath = previewPath, PreviewSceneFileHash = previewHash,
                EvidenceRoot = revision == 0 ? root + "/evidence" : root + "/evidence/E" + request.EvidenceRevision.ToString(CultureInfo.InvariantCulture)
            };
        }

        private static void VerifyManifestAndOwnedOutputs(JObject receipt, JObject manifest, string effectId, string runtimePath, string runtimeGuid, string buildHash, int revision)
        {
            if (revision == 0) VerifyLegacyBootstrapManifest(receipt, manifest);
            else RequireExactly(manifest, "manifestVersion", "effectId", "buildHash", "runtimeEntry", "ownedOutputs");
            if (RequiredInt(manifest, "manifestVersion", 1, 1) != 1 || !Same(RequiredEffectId(manifest, "effectId"), effectId)) throw new InvalidDataException("Manifest version/effect identity is invalid.");
            var rawBuild = RequiredRawHash(manifest, "buildHash");
            if (!Same(buildHash, "sha256:" + rawBuild)) throw new InvalidDataException("Manifest build identity differs from the receipt.");
            var runtime = RequiredObject(manifest, "runtimeEntry");
            RequireExactly(runtime, "kind", "path", "guid");
            RequiredString(runtime, "kind", 64);
            if (!Same(RequiredAssetPath(runtime, "path"), runtimePath) || !Same(RequiredGuid(runtime, "guid"), runtimeGuid)) throw new InvalidDataException("Manifest Runtime Entry differs from the receipt.");
            var manifestOwned = RequiredArray(manifest, "ownedOutputs", 1, MaxOwnedOutputs);
            if (!JToken.DeepEquals(receipt["ownedOutputs"], manifestOwned)) throw new InvalidDataException("Receipt and Manifest owned-output registries differ.");
            var ownedRoot = revision == 0 ? Parent(runtimePath) : RequiredAssetPath(receipt, "ownedOutputRoot");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            long total = 0;
            foreach (var token in manifestOwned)
            {
                var item = RequiredArrayObject(token, "Manifest owned output");
                RequireExactly(item, "path", "guid", "assetType", "sha256");
                var path = RequiredAssetPath(item, "path");
                var guid = RequiredGuid(item, "guid");
                RequiredString(item, "assetType", 128);
                var rawHash = RequiredRawHash(item, "sha256");
                if (!seen.Add(path) || (!Same(path, ownedRoot) && !Under(path, ownedRoot))) throw new InvalidDataException("Manifest contains a duplicate or out-of-root owned output.");
                var file = ReadProjectPinned(path, "sha256:" + rawHash, "Manifest-owned output", MaxOwnedAssetBytes);
                total = checked(total + file.Bytes.LongLength);
                if (total > MaxOwnedAssetTotalBytes) throw new InvalidDataException("Manifest-owned output bytes exceed the replay bound.");
                var meta = ReadProjectUnpinned(path + ".meta", "Manifest-owned output meta", MaxDocumentBytes);
                var match = Regex.Match(meta.Text, "(?m)^guid: ([0-9a-f]{32})$");
                if (!match.Success || !Same(match.Groups[1].Value, guid)) throw new InvalidDataException("Manifest-owned output meta GUID differs from the Manifest.");
            }
            if (!seen.Contains(runtimePath)) throw new InvalidDataException("Manifest does not own its Runtime Entry.");
            VerifyOwnedOutputRootExact(ownedRoot, seen);
        }

        private static void VerifyOwnedOutputRootExact(string ownedRoot, HashSet<string> declared)
        {
            if (!SafeAssetPath(ownedRoot)) throw new InvalidDataException("Owned-output root is unsafe.");
            var absoluteRoot = ProjectAbsolute(ownedRoot);
            EnsureDirectory(absoluteRoot, "owned-output root", ProjectRoot());
            var actual = new HashSet<string>(StringComparer.Ordinal);
            var metas = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<WalkItem>();
            pending.Push(new WalkItem { AbsolutePath = absoluteRoot, RelativePath = ownedRoot, Depth = 0 });
            var directories = 1; var files = 0; long bytes = 0;
            while (pending.Count != 0)
            {
                var current = pending.Pop();
                EnsureDirectory(current.AbsolutePath, "owned-output directory", ProjectRoot());
                foreach (var entry in Directory.EnumerateFileSystemEntries(current.AbsolutePath))
                {
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Owned-output root contains a reparse-backed entry.");
                    var relative = ProjectRelative(entry);
                    if (relative == null) throw new InvalidDataException("Owned-output entry escaped the Unity project.");
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        if (current.Depth + 1 > MaxTreeDepth || ++directories > MaxDirectories) throw new InvalidDataException("Owned-output root exceeds its directory/depth bound.");
                        pending.Push(new WalkItem { AbsolutePath = entry, RelativePath = relative, Depth = current.Depth + 1 });
                        continue;
                    }
                    if (++files > MaxCandidateFiles) throw new InvalidDataException("Owned-output root exceeds its file-count bound.");
                    long length; using (var stream = File.OpenRead(entry)) length = stream.Length;
                    bytes = checked(bytes + length);
                    if (bytes > MaxOwnedAssetTotalBytes) throw new InvalidDataException("Owned-output root exceeds its byte bound.");
                    if (relative.EndsWith(".meta", StringComparison.Ordinal)) metas.Add(relative);
                    else if (!actual.Add(relative)) throw new InvalidDataException("Owned-output root repeats an asset path.");
                }
            }
            if (!actual.SetEquals(declared)) throw new InvalidDataException("Manifest owned outputs do not exactly enumerate the owned-output root.");
            foreach (var meta in metas)
            {
                var target = meta.Substring(0, meta.Length - ".meta".Length);
                if (!declared.Contains(target) && !Directory.Exists(ProjectAbsolute(target)))
                    throw new InvalidDataException("Owned-output root contains an orphan or undeclared meta file.");
            }
        }

        private static void VerifyLegacyBootstrapManifest(JObject receipt, JObject manifest)
        {
            RequireExactly(manifest,
                "manifestVersion", "rulesVersion", "enforcement", "effectId", "archetype", "recipeVersion",
                "recipeRevision", "recipeHash", "buildHash", "compilerVersion", "unityVersion", "sourceRecipePath",
                "runtimeEntry", "ownedOutputs", "dependencies", "cost", "audit", "formalProduction", "generatedAtUtc");
            RequireExactString(manifest, "enforcement", "strict");
            RequiredString(manifest, "rulesVersion", 128);
            RequiredString(manifest, "archetype", 128);
            RequiredInt(manifest, "recipeVersion", 1, MaxRevision);
            RequiredInt(manifest, "recipeRevision", 1, MaxRevision);
            RequiredRawHash(manifest, "recipeHash");
            RequiredString(manifest, "compilerVersion", 256);
            RequiredString(manifest, "unityVersion", 128);
            var recipePath = RequiredAssetPath(manifest, "sourceRecipePath");
            if (!recipePath.StartsWith("Assets/VFX/Recipes/", StringComparison.Ordinal)) throw new InvalidDataException("Legacy bootstrap Manifest source Recipe is outside the formal Recipe root.");
            RequiredString(manifest, "generatedAtUtc", 128);

            var dependencies = RequiredArray(manifest, "dependencies", 0, MaxDependencyRecords);
            foreach (var token in dependencies)
            {
                var dependency = RequiredArrayObject(token, "Manifest dependency");
                // D4: dependencyHash was removed from the dependency record — machine-local and never
                // compared. The formal reader now requires exactly the portable identity fields.
                RequireExactly(dependency, "path", "guid", "assetType", "version");
                RequiredPath(dependency, "path");
                RequiredGuid(dependency, "guid");
                RequiredString(dependency, "assetType", 128);
                RequireNullOrBoundedString(dependency, "version", 128);
            }

            var cost = RequiredObject(manifest, "cost");
            RequireExactly(cost, "particles", "particleSystems", "renderers", "materials", "trails", "duration",
                "localTextureBytes", "dependencyResidentTextureBytes", "gameObjects", "maxDepth");
            foreach (var field in new[] { "particles", "particleSystems", "renderers", "materials", "trails", "localTextureBytes", "dependencyResidentTextureBytes", "gameObjects", "maxDepth" })
                RequiredNonNegativeNumber(cost, field, true);
            RequiredNonNegativeNumber(cost, "duration", false);

            var audit = RequiredArray(manifest, "audit", 0, MaxAuditRecords);
            foreach (var token in audit)
            {
                var item = RequiredArrayObject(token, "Manifest audit entry");
                RequireExactly(item, "code", "severity", "path", "message");
                RequiredString(item, "code", 128);
                RequiredString(item, "severity", 32);
                RequiredString(item, "path", MaxPathCharacters);
                RequiredString(item, "message", 4096);
            }

            var formal = RequiredObject(manifest, "formalProduction");
            RequireExactly(formal, "contractPath", "contractFileHash", "contractHash", "contractRevision", "tracePath",
                "traceFileHash", "visualStatus", "evidenceCorpusPath", "evidenceCorpusHash", "userVerdictRecordPath",
                "userVerdictRecordHash", "visualQaRecordPath", "visualQaRecordHash", "s0aStatusRecordPath",
                "s0aStatusRecordHash", "admissionPhase");
            if (!W24S5ProductionGate.HasExactEvidenceFreeBootstrapBinding(formal,
                    RequiredPath(receipt, "bootstrapContractPath"), RequiredHash(receipt, "bootstrapContractFileHash"),
                    RequiredHash(receipt, "bootstrapContractHash"), RequiredInt(receipt, "bootstrapContractRevision", 1, MaxRevision),
                    RequiredPath(receipt, "bootstrapTracePath"), RequiredHash(receipt, "bootstrapTraceFileHash")))
                throw new InvalidDataException("Legacy bootstrap Manifest does not retain the exact evidence-free first-formal binding.");
        }

        private static void VerifyLegacyContractBindings(
            JObject receipt, W24S5CandidateEvidenceSnapshot value, string bootstrapContractPath,
            string bootstrapContractFileHash, string bootstrapTracePath, string bootstrapTraceFileHash)
        {
            if (!Same(bootstrapContractPath, "docs/vfx-contracts/" + value.EffectId + ".contract.json")
                || !Same(bootstrapTracePath, "docs/vfx-traces/" + value.EffectId + ".implementation-trace.json"))
                throw new InvalidDataException("Legacy bootstrap Contract/Trace paths are not the authoritative effect paths.");
            var bootstrapContractFile = ReadRepositoryPinned(bootstrapContractPath, bootstrapContractFileHash, "legacy bootstrap Contract", MaxDocumentBytes);
            VfxDesignContract bootstrapContract;
            var bootstrapContractReport = VfxDesignContractJson.ValidateJson(bootstrapContractFile.Text, out bootstrapContract);
            if (bootstrapContractReport.HasErrors
                || !Same(bootstrapContract.EffectId, value.EffectId)
                || bootstrapContract.ContractRevision != value.ContractRevision
                || !Same(bootstrapContract.ContractHash, RequiredHash(receipt, "bootstrapContractHash")))
                throw new InvalidDataException("Legacy bootstrap Contract bytes do not match the receipt identity: " + Describe(bootstrapContractReport));
            var bootstrapTraceFile = ReadRepositoryPinned(bootstrapTracePath, bootstrapTraceFileHash, "legacy bootstrap Trace", MaxDocumentBytes);
            var bootstrapTrace = VfxImplementationTraceJson.FromJson(bootstrapTraceFile.Text);
            var ownedPaths = new HashSet<string>(RequiredArray(receipt, "ownedOutputs", 1, MaxOwnedOutputs)
                .Select(item => RequiredAssetPath(RequiredArrayObject(item, "legacy owned output receipt"), "path")), StringComparer.Ordinal);
            if (!ValidBootstrapPreregistration(bootstrapTrace, bootstrapContract, value.RuntimeEntryPath, ownedPaths))
                throw new InvalidDataException("Legacy bootstrap Trace is not the exact evidence-free first-formal preregistration.");

            var contractFile = ReadRepositoryPinned(value.ContractPath, value.ContractFileHash, "legacy candidate Contract binding replay", MaxDocumentBytes);
            var contract = Parse(contractFile.Text, "legacy candidate Contract binding replay");
            var extensions = RequiredObject(contract, "extensions");
            if (!Same((string)extensions["visualStatus"], "VISUAL_PENDING")
                || !Same((string)extensions["captureBindingStatus"], "FROZEN_PRE_C0")
                || !Same((string)extensions["runtimeEntry"], value.RuntimeEntryPath)
                || !Same((string)extensions["previewScene"], value.PreviewScenePath)
                || !Same((string)extensions["implementationTrace"], value.PendingTracePath)
                || !Same((string)extensions["candidateId"], "C0")
                || !Same((string)extensions["candidateStatus"], "C0_CAPTURE_PENDING")
                || !Same((string)extensions["candidateReceipt"], value.CandidateReceiptPath)
                || !Same((string)extensions["bootstrapContractPath"], bootstrapContractPath)
                || !Same((string)extensions["bootstrapContractFileHash"], bootstrapContractFileHash)
                || !Same((string)extensions["bootstrapTracePath"], bootstrapTracePath)
                || !Same((string)extensions["bootstrapTraceFileHash"], bootstrapTraceFileHash)
                || !Same((string)contract.SelectToken("captureProfile.prefabManifestHash"), value.BuildHash))
                throw new InvalidDataException("Legacy candidate Contract does not retain its exact C0/bootstrap/Manifest bindings.");
            if (!Same(RequiredPath(receipt, "productionManifestPath"), "ProjectSettings/VFXComposer/BuildManifests/" + value.EffectId + ".manifest.json"))
                throw new InvalidDataException("Legacy candidate production Manifest path is not the authoritative effect path.");
        }

        private static bool ValidBootstrapPreregistration(VfxImplementationTrace trace, VfxDesignContract contract, string runtimePath, HashSet<string> ownedPaths)
        {
            if (trace == null || contract == null
                || !Same(trace.TraceStatus, "PENDING_FIRST_FORMAL_BUILD_BINDING")
                || !Same(trace.EffectId, contract.EffectId)
                || trace.ContractRevision != contract.ContractRevision
                || !Same(trace.ContractHash, contract.ContractHash)
                || !Same(trace.BuildHash, "pending:formal-build")
                || !Same(trace.CaptureProfileHash, "pending:formal-build")
                || !Same(trace.RuntimeEntryGuid, "pending:formal-build")
                || !Same(trace.RuntimeEntryAssetPath, runtimePath)) return false;
            var expected = new HashSet<string>((contract.Requirements ?? Array.Empty<VfxDesignRequirement>()).Select(item => item.DesignRequirementId), StringComparer.Ordinal);
            var actual = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in trace.RequirementTraces ?? Array.Empty<VfxRequirementTrace>())
            {
                if (item == null || !actual.Add(item.DesignRequirementId)
                    || (item.AuthorityEvidence != null && item.AuthorityEvidence.Length != 0)
                    || (item.CrossEvidence != null && item.CrossEvidence.Length != 0)) return false;
                var requirement = (contract.Requirements ?? Array.Empty<VfxDesignRequirement>()).SingleOrDefault(candidate => Same(candidate.DesignRequirementId, item.DesignRequirementId));
                if (requirement == null || !Same(requirement.EvidenceAuthority, item.EvidenceAuthority)) return false;
                if ((item.Objects ?? Array.Empty<VfxTraceObject>()).Any(mapped => mapped == null || !SafeAssetPath(mapped.AssetPath) || !ownedPaths.Contains(mapped.AssetPath))) return false;
            }
            return expected.SetEquals(actual);
        }

        private static void ReplayEvidence(W24S5CandidateEvidenceSnapshot value)
        {
            if (value.CandidateRevision == 0)
            {
                if (value.EvidenceRevision == 2) throw new InvalidDataException("The repository defines no legacy C0 E2 namespace, predecessor pin, or immutable E2 descriptor.");
                throw new InvalidDataException("Legacy C0 candidate bytes were replayed, but its raw recorder tree has no immutable E1 revision descriptor binding candidate receipt, raw seal, and capture-tool bytes. C0/evidence is a success-only completed-Trace transition and is intentionally not accepted by this pre-verdict reader.");
            }
            throw new InvalidDataException("C1/C2 candidate snapshots were replayed, but the repository has no committed C1/C2 E1/E2 transition schema or writer; evidence remains invalid rather than accepting the synthetic machine-scaffold envelope.");
        }

        private static void VerifyCandidateStaticTree(W24S5CandidateEvidenceSnapshot value, HashSet<string> expected)
        {
            var excluded = new HashSet<string>(StringComparer.Ordinal) { value.CandidateRoot + "/evidence", value.CandidateRoot + "/terminal" };
            var actual = EnumerateBoundedTree(value.CandidateRoot, MaxCandidateFiles, MaxDirectories, MaxTreeDepth, MaxCandidateBytes, excluded);
            if (!actual.SetEquals(expected)) throw new InvalidDataException("Candidate static namespace contains a missing, extra, or path-drifted file.");
        }

        private static HashSet<string> EnumerateBoundedTree(string relativeRoot, int maxFiles, int maxDirectories, int maxDepth, long maxBytes, HashSet<string> excludedRoots)
        {
            if (!SafeRepositoryPath(relativeRoot)) throw new InvalidDataException("Tree root path is unsafe.");
            var root = RepositoryAbsolute(relativeRoot);
            EnsureDirectory(root, "bounded tree root", RepositoryRoot());
            var output = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<WalkItem>();
            pending.Push(new WalkItem { AbsolutePath = root, RelativePath = relativeRoot, Depth = 0 });
            var directories = 1; long bytes = 0;
            while (pending.Count != 0)
            {
                var current = pending.Pop(); EnsureDirectory(current.AbsolutePath, "bounded tree directory", RepositoryRoot());
                foreach (var entry in Directory.EnumerateFileSystemEntries(current.AbsolutePath))
                {
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Bounded tree contains a reparse-backed entry.");
                    var relative = RepositoryRelative(entry);
                    if (relative == null) throw new InvalidDataException("Bounded tree entry escaped the repository.");
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        if (excludedRoots != null && excludedRoots.Contains(relative)) continue;
                        if (current.Depth + 1 > maxDepth || ++directories > maxDirectories) throw new InvalidDataException("Bounded tree exceeds its directory/depth limit.");
                        pending.Push(new WalkItem { AbsolutePath = entry, RelativePath = relative, Depth = current.Depth + 1 });
                    }
                    else
                    {
                        if (output.Count >= maxFiles) throw new InvalidDataException("Bounded tree exceeds its file-count limit.");
                        long length; using (var stream = File.OpenRead(entry)) length = stream.Length;
                        bytes = checked(bytes + length);
                        if (bytes > maxBytes || !output.Add(relative)) throw new InvalidDataException("Bounded tree exceeds its byte limit or repeats a path.");
                    }
                }
            }
            return output;
        }

        private static bool ValidPendingTrace(VfxImplementationTrace trace, VfxDesignContract contract, string candidateId, int revision, HashSet<string> ownedPaths)
        {
            if (trace == null || contract == null || !Same(trace.TraceStatus, candidateId + "_CAPTURE_PENDING") || trace.CandidateRevision != revision || trace.EvidenceRevision != 0 || !Same(trace.EffectId, contract.EffectId) || trace.ContractRevision != contract.ContractRevision || !Same(trace.ContractHash, contract.ContractHash)) return false;
            var expected = new HashSet<string>((contract.Requirements ?? Array.Empty<VfxDesignRequirement>()).Select(item => item.DesignRequirementId), StringComparer.Ordinal);
            var actual = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in trace.RequirementTraces ?? Array.Empty<VfxRequirementTrace>())
            {
                if (item == null || !actual.Add(item.DesignRequirementId) || (item.AuthorityEvidence != null && item.AuthorityEvidence.Length != 0) || (item.CrossEvidence != null && item.CrossEvidence.Length != 0)) return false;
                var requirement = (contract.Requirements ?? Array.Empty<VfxDesignRequirement>()).SingleOrDefault(candidate => Same(candidate.DesignRequirementId, item.DesignRequirementId));
                if (requirement == null || !Same(requirement.EvidenceAuthority, item.EvidenceAuthority)) return false;
                if ((item.Objects ?? Array.Empty<VfxTraceObject>()).Any(mapped => mapped == null || !SafeAssetPath(mapped.AssetPath) || !ownedPaths.Contains(mapped.AssetPath))) return false;
            }
            return expected.SetEquals(actual);
        }

        private static void VerifyPendingAuthorityMarker(JObject authority)
        {
            RequireExactly(authority, "route", "issuerVersion", "productionIssuerStatus", "failureReceiptPath", "failureReceiptFileHash", "testOnly");
            RequireExactString(authority, "route", "MACHINE_FAIL");
            RequireExactString(authority, "issuerVersion", "w24-s5-test-machine-failure/1");
            RequireExactString(authority, "productionIssuerStatus", "FAILURE_ISSUER_PENDING");
            RequireJsonNull(authority, "failureReceiptPath"); RequireJsonNull(authority, "failureReceiptFileHash");
            if ((bool?)authority["testOnly"] != true) throw new InvalidDataException("Pending candidate authority marker is not explicitly test-only.");
        }

        private static void VerifyIdentity(JObject document, W24S5CandidateEvidenceSnapshot value, bool evidence, string label)
        {
            if (!Same(RequiredEffectId(document, "effectId"), value.EffectId)
                || !Same(RequiredString(document, "candidateId", 8), value.CandidateId)
                || RequiredInt(document, "candidateRevision", 0, 2) != value.CandidateRevision
                || RequiredInt(document, "contractRevision", 1, MaxRevision) != value.ContractRevision
                || !Same(RequiredHash(document, "contractHash"), value.ContractHash)
                || !Same(RequiredHash(document, "buildHash"), value.BuildHash)
                || !Same(RequiredHash(document, "captureProfileHash"), value.CaptureProfileHash)
                || !Same(RequiredAssetPath(document, "runtimeEntryPath"), value.RuntimeEntryPath)
                || !Same(RequiredGuid(document, "runtimeEntryGuid"), value.RuntimeEntryGuid))
                throw new InvalidDataException(label + " identity differs from the replayed candidate.");
            if (evidence && (RequiredInt(document, "evidenceRevision", 1, 2) != value.EvidenceRevision
                || !Same(RequiredPath(document, "candidateReceiptPath"), value.CandidateReceiptPath)
                || !Same(RequiredHash(document, "candidateReceiptFileHash"), value.CandidateReceiptFileHash)))
                throw new InvalidDataException(label + " does not bind the requested E revision and exact candidate receipt bytes.");
        }

        private static string[] IdentityFields(bool evidence)
        {
            var fields = new List<string> { "effectId", "candidateId", "candidateRevision", "contractRevision", "contractHash", "buildHash", "captureProfileHash", "runtimeEntryPath", "runtimeEntryGuid" };
            if (evidence) fields.AddRange(new[] { "evidenceRevision", "candidateReceiptPath", "candidateReceiptFileHash" });
            return fields.ToArray();
        }

        private static PinnedFile ReadRepositoryPinned(string path, string expectedHash, string label, int maximumBytes)
        {
            if (!CanonicalHash(expectedHash)) throw new InvalidDataException(label + " hash is not canonical.");
            var file = ReadRepositoryUnpinned(path, label, maximumBytes);
            if (!Same(file.Hash, expectedHash)) throw new InvalidDataException(label + " bytes differ from their immutable pin.");
            return file;
        }

        private static PinnedFile ReadRepositoryUnpinned(string path, string label, int maximumBytes)
        {
            if (!SafeRepositoryPath(path)) throw new InvalidDataException(label + " path is unsafe.");
            return ReadAbsolute(RepositoryAbsolute(path), RepositoryRoot(), label, maximumBytes);
        }

        private static PinnedFile ReadProjectPinned(string path, string expectedHash, string label, int maximumBytes)
        {
            if (!SafeAssetPath(path) || !CanonicalHash(expectedHash)) throw new InvalidDataException(label + " path/hash is unsafe.");
            var file = ReadAbsoluteBytes(ProjectAbsolute(path), ProjectRoot(), label, maximumBytes);
            if (!Same(file.Hash, expectedHash)) throw new InvalidDataException(label + " bytes differ from their immutable pin.");
            return file;
        }

        private static PinnedFile ReadProjectUnpinned(string path, string label, int maximumBytes)
        {
            if (!SafeAssetPath(path)) throw new InvalidDataException(label + " path is unsafe.");
            return ReadAbsolute(ProjectAbsolute(path), ProjectRoot(), label, maximumBytes);
        }

        private static PinnedFile ReadAbsolute(string absolute, string boundary, string label, int maximumBytes)
        {
            var file = ReadAbsoluteBytes(absolute, boundary, label, maximumBytes);
            try { file.Text = StrictUtf8.GetString(file.Bytes); }
            catch (DecoderFallbackException error) { throw new InvalidDataException(label + " is not strict UTF-8.", error); }
            return file;
        }

        private static PinnedFile ReadAbsoluteBytes(string absolute, string boundary, string label, int maximumBytes)
        {
            EnsureRegularFile(absolute, label, boundary);
            byte[] bytes;
            using (var stream = new FileStream(absolute, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length < 0 || stream.Length > maximumBytes) throw new InvalidDataException(label + " exceeds its byte bound.");
                bytes = new byte[(int)stream.Length]; var offset = 0;
                while (offset < bytes.Length)
                {
                    var read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read < 1) throw new InvalidDataException(label + " changed length while being read.");
                    offset += read;
                }
                if (stream.ReadByte() != -1) throw new InvalidDataException(label + " grew while being read.");
            }
            EnsureRegularFile(absolute, label, boundary);
            return new PinnedFile { Bytes = bytes, Hash = Hash(bytes) };
        }

        private static void EnsureRegularFile(string absolute, string label, string boundary)
        {
            if (!File.Exists(absolute)) throw new InvalidDataException(label + " is missing.");
            EnsureNoReparseAtOrAbove(absolute, boundary);
            var attributes = File.GetAttributes(absolute);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) throw new InvalidDataException(label + " is not a regular non-reparse file.");
        }

        private static void EnsureDirectory(string absolute, string label, string boundary)
        {
            if (!Directory.Exists(absolute)) throw new InvalidDataException(label + " is missing.");
            EnsureNoReparseAtOrAbove(absolute, boundary);
            var attributes = File.GetAttributes(absolute);
            if ((attributes & FileAttributes.Directory) == 0 || (attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException(label + " is not a regular non-reparse directory.");
        }

        private static void EnsureNoReparseAtOrAbove(string path, string boundary)
        {
            var stop = Path.GetFullPath(boundary).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var current = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            while (true)
            {
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Repository/project input is reparse-backed.");
                if (string.Equals(current, stop, StringComparison.OrdinalIgnoreCase)) return;
                var parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Input escaped its checked filesystem boundary.");
                current = parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        private static JObject Parse(string text, string label) { return W24StrictJsonText.ParseObject(text, "W24 S5 reader " + label); }
        private static void VerifyTypedSelfHash(JObject document, string label, string hashField = "selfHash")
        {
            var clone = (JObject)document.DeepClone(); var claimed = RequiredHash(clone, hashField); clone.Remove(hashField);
            var encodingField = string.Equals(hashField, "selfHash", StringComparison.Ordinal) ? "selfHashEncoding" : "typedBundleHashEncoding";
            RequireExactString(clone, encodingField, W24TypedBinaryCanonicalEncoding.EncodingName);
            if (!W24TypedBinaryCanonicalEncoding.Verify(claimed, NormalizeTypedNumbers(clone))) throw new InvalidDataException(label + " typed self-hash is invalid.");
        }

        private static JToken NormalizeTypedNumbers(JToken token)
        {
            var obj = token as JObject;
            if (obj != null) { var copy = new JObject(); foreach (var property in obj.Properties()) copy.Add(property.Name, NormalizeTypedNumbers(property.Value)); return copy; }
            var array = token as JArray;
            if (array != null) return new JArray(array.Select(NormalizeTypedNumbers));
            if (token.Type == JTokenType.Float) return new JValue(Convert.ToDouble(((JValue)token).Value, CultureInfo.InvariantCulture));
            return token.DeepClone();
        }

        private static string CanonicalJson(JToken value)
        {
            var obj = value as JObject;
            if (obj != null) { var sorted = new JObject(); foreach (var property in obj.Properties().OrderBy(item => item.Name, StringComparer.Ordinal)) sorted.Add(property.Name, JToken.Parse(CanonicalJson(property.Value))); return sorted.ToString(Formatting.None); }
            var array = value as JArray;
            if (array != null) return new JArray(array.Select(item => JToken.Parse(CanonicalJson(item)))).ToString(Formatting.None);
            return value.ToString(Formatting.None);
        }

        private static void RequireExactly(JObject value, params string[] fields)
        {
            var expected = new HashSet<string>(fields, StringComparer.Ordinal);
            var actual = new HashSet<string>(value.Properties().Select(property => property.Name), StringComparer.Ordinal);
            if (!expected.SetEquals(actual)) throw new InvalidDataException("JSON object field set is not exact; expected " + string.Join(",", fields) + ".");
        }

        private static JObject RequiredObject(JObject value, string field) { var token = value[field]; if (token == null || token.Type != JTokenType.Object) throw new InvalidDataException(field + " must be an object."); return (JObject)token; }
        private static JObject RequiredArrayObject(JToken value, string label) { if (value == null || value.Type != JTokenType.Object) throw new InvalidDataException(label + " must be an object."); return (JObject)value; }
        private static JArray RequiredArray(JObject value, string field, int minimum, int maximum) { var token = value[field]; if (token == null || token.Type != JTokenType.Array) throw new InvalidDataException(field + " must be an array."); var array = (JArray)token; if (array.Count < minimum || array.Count > maximum) throw new InvalidDataException(field + " count exceeds its bound."); return array; }
        private static string RequiredString(JObject value, string field, int maximum) { var token = value[field]; if (token == null || token.Type != JTokenType.String) throw new InvalidDataException(field + " must be a string."); var text = (string)token; if (string.IsNullOrEmpty(text) || text.Length > maximum || text.Any(char.IsControl)) throw new InvalidDataException(field + " is outside its text bound."); return text; }
        private static void RequireExactString(JObject value, string field, string expected) { if (!Same(RequiredString(value, field, 128), expected)) throw new InvalidDataException(field + " has an unsupported value."); }
        private static string RequiredEffectId(JObject value, string field) { var text = RequiredString(value, field, 64); if (!Regex.IsMatch(text, "^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$")) throw new InvalidDataException(field + " is not lower_snake_case."); return text; }
        private static string RequiredPath(JObject value, string field) { var text = RequiredString(value, field, MaxPathCharacters); if (!SafeRepositoryPath(text)) throw new InvalidDataException(field + " is not a safe repository-relative path."); return text; }
        private static string RequiredAssetPath(JObject value, string field) { var text = RequiredPath(value, field); if (!text.StartsWith("Assets/", StringComparison.Ordinal)) throw new InvalidDataException(field + " is not a Unity asset path."); return text; }
        private static string RequiredEvidencePath(JObject value, string field, string root) { var text = RequiredPath(value, field); if (!Under(text, root)) throw new InvalidDataException(field + " is outside the requested E namespace."); return text; }
        private static string RequiredHash(JObject value, string field) { var text = RequiredString(value, field, 71); if (!CanonicalHash(text)) throw new InvalidDataException(field + " is not a canonical SHA-256."); return text; }
        private static string RequiredRawHash(JObject value, string field) { var text = RequiredString(value, field, 64); if (!RawHash(text)) throw new InvalidDataException(field + " is not a raw lowercase SHA-256."); return text; }
        private static string RequiredGuid(JObject value, string field) { var text = RequiredString(value, field, 32); if (text.Length != 32 || text.Any(character => !LowerHex(character))) throw new InvalidDataException(field + " is not a lowercase Unity GUID."); return text; }
        private static string RequiredLowerHex(JObject value, string field, int length) { var text = RequiredString(value, field, length); if (text.Length != length || text.Any(character => !LowerHex(character))) throw new InvalidDataException(field + " is not the expected lowercase hexadecimal token."); return text; }
        private static int RequiredInt(JObject value, string field, int minimum, int maximum) { var token = value[field] as JValue; if (token == null || token.Type != JTokenType.Integer || !(token.Value is long) && !(token.Value is int) && !(token.Value is short) && !(token.Value is byte)) throw new InvalidDataException(field + " must be an integer."); var number = Convert.ToInt64(token.Value, CultureInfo.InvariantCulture); if (number < minimum || number > maximum) throw new InvalidDataException(field + " is outside its bound."); return (int)number; }
        private static void RequiredNonNegativeNumber(JObject value, string field, bool integer)
        {
            var token = value[field] as JValue;
            if (token == null || integer && token.Type != JTokenType.Integer || !integer && token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
                throw new InvalidDataException(field + " must be a numeric value of the expected kind.");
            var number = Convert.ToDouble(token.Value, CultureInfo.InvariantCulture);
            if (double.IsNaN(number) || double.IsInfinity(number) || number < 0 || number > 1000000000000d)
                throw new InvalidDataException(field + " is outside its finite non-negative bound.");
        }
        private static void RequireNullOrBoundedString(JObject value, string field, int maximum)
        {
            var token = value[field];
            if (token == null) throw new InvalidDataException(field + " is required.");
            if (token.Type == JTokenType.Null) return;
            RequiredString(value, field, maximum);
        }
        private static void RequireJsonNull(JObject value, string field) { var token = value[field]; if (token == null || token.Type != JTokenType.Null) throw new InvalidDataException(field + " must be explicit JSON null."); }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return "sha256:" + string.Concat(sha.ComputeHash(bytes).Select(item => item.ToString("x2", CultureInfo.InvariantCulture))); }
        private static bool CanonicalHash(string value) { return value != null && value.Length == 71 && value.StartsWith("sha256:", StringComparison.Ordinal) && value.Substring(7).All(LowerHex); }
        private static bool RawHash(string value) { return value != null && value.Length == 64 && value.All(LowerHex); }
        private static bool LowerHex(char value) { return value >= '0' && value <= '9' || value >= 'a' && value <= 'f'; }
        private static bool Same(string left, string right) { return string.Equals(left, right, StringComparison.Ordinal); }
        private static bool Under(string path, string root) { return !string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(root) && path.StartsWith(root.TrimEnd('/') + "/", StringComparison.Ordinal); }
        private static string Parent(string path) { return string.IsNullOrEmpty(path) ? null : Path.GetDirectoryName(path).Replace('\\', '/'); }
        private static bool CameraBoundToPreview(string camera, string preview) { return !string.IsNullOrEmpty(camera) && camera.StartsWith(preview + "#", StringComparison.Ordinal) && camera.Length > preview.Length + 1; }
        private static bool SafeAssetPath(string value) { return SafeRepositoryPath(value) && value.StartsWith("Assets/", StringComparison.Ordinal); }
        private static bool SafeRepositoryPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > MaxPathCharacters || Path.IsPathRooted(value) || value.IndexOf('\\') >= 0 || value.IndexOf(':') >= 0) return false;
            var parts = value.Split('/');
            return parts.Length > 0 && parts.All(part => part.Length > 0 && part.Length <= MaxPathSegmentCharacters && part != "." && part != "..");
        }
        private static string ProjectRoot() { return Directory.GetParent(Application.dataPath).FullName; }
        private static string RepositoryRoot() { return Directory.GetParent(ProjectRoot()).FullName; }
        private static string ProjectAbsolute(string relative) { return CheckedAbsolute(ProjectRoot(), relative); }
        private static string RepositoryAbsolute(string relative) { return CheckedAbsolute(RepositoryRoot(), relative); }
        private static string CheckedAbsolute(string root, string relative) { var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar; var absolute = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))); if (!absolute.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Path escaped its filesystem boundary."); return absolute; }
        private static string RepositoryRelative(string absolute) { var prefix = Path.GetFullPath(RepositoryRoot()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar; var full = Path.GetFullPath(absolute); return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? full.Substring(prefix.Length).Replace('\\', '/') : null; }
        private static string ProjectRelative(string absolute) { var prefix = Path.GetFullPath(ProjectRoot()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar; var full = Path.GetFullPath(absolute); return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? full.Substring(prefix.Length).Replace('\\', '/') : null; }
        private static string Describe(W24GateReport report) { return string.Join(" | ", report.Issues.Select(issue => issue.Code + " " + issue.Path + " " + issue.Message).ToArray()); }
        private static bool ExpectedInputFailure(Exception error) { return error is InvalidDataException || error is IOException || error is UnauthorizedAccessException || error is SecurityException || error is JsonException || error is FormatException || error is OverflowException || error is ArgumentException || error is CryptographicException || error is DecoderFallbackException; }
    }
}
