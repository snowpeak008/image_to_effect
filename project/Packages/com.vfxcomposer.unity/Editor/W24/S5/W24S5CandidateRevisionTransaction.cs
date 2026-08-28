using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// <summary>
    /// Persisted inputs for a normal immutable-candidate retry.  The request deliberately has no
    /// candidate id, candidate revision, Contract JSON, Trace JSON, visual verdict, or maturity
    /// field.  S5 derives those identities and requires a separate opaque failure authority.
    /// </summary>
    internal sealed class W24S5CandidateRevisionRequest
    {
        internal string EffectId;
        internal string PreviousCandidateReceiptPath;
        internal string PreviousCandidateReceiptFileHash;
        internal string ProductionManifestPath;
        internal string ProductionManifestFileHash;
        internal string OwnedOutputRoot;
        internal string RuntimeEntryPath;
        internal string PreviewScenePath;
        internal string CaptureToolBundlePath;
        internal string CaptureToolBundleFileHash;
    }

    internal sealed class W24S5CandidateRevisionResult
    {
        internal bool Succeeded;
        internal string CandidateId;
        internal int CandidateRevision = -1;
        internal string CandidateRoot;
        internal string CandidateReceiptPath;
        internal string CandidateReceiptFileHash;
        internal readonly List<string> Errors = new List<string>();
        internal W24S5CandidateRevisionApproval Approval;
        internal void Error(string message) { Errors.Add(message); }
    }

    /// <summary>
    /// Opaque proof that a gate, rather than a request enum/string, authorized consumption of the
    /// previous candidate.  Production issuance intentionally remains unavailable until a sealed
    /// MACHINE_FAIL receipt can be replayed.  Visual-failure issuance additionally remains blocked
    /// on the independent Visual-QA issuer.
    /// </summary>
    internal sealed class W24S5CandidateFailureAuthority
    {
        private readonly string effectId;
        private readonly string candidateReceiptPath;
        private readonly string candidateReceiptFileHash;
        private readonly int contractRevision;
        private readonly int candidateRevision;
        private readonly string route;
        private readonly string issuerVersion;
        private readonly bool testOnly;

        private W24S5CandidateFailureAuthority(
            object issuer,
            string approvedEffectId,
            string approvedCandidateReceiptPath,
            string approvedCandidateReceiptFileHash,
            int approvedContractRevision,
            int approvedCandidateRevision,
            string approvedRoute,
            string approvedIssuerVersion,
            bool isTestOnly)
        {
            if (!W24S5CandidateRevisionTransaction.IsFailureAuthorityIssuer(issuer))
                throw new InvalidOperationException("Candidate failure authorities may only be issued by the S5 candidate gate.");
            effectId = approvedEffectId;
            candidateReceiptPath = approvedCandidateReceiptPath;
            candidateReceiptFileHash = approvedCandidateReceiptFileHash;
            contractRevision = approvedContractRevision;
            candidateRevision = approvedCandidateRevision;
            route = approvedRoute;
            issuerVersion = approvedIssuerVersion;
            testOnly = isTestOnly;
        }

        internal bool Matches(CandidateSnapshot candidate)
        {
            return candidate != null
                && Same(effectId, candidate.EffectId)
                && Same(candidateReceiptPath, candidate.ReceiptPath)
                && Same(candidateReceiptFileHash, candidate.ReceiptFileHash)
                && contractRevision == candidate.ContractRevision
                && candidateRevision == candidate.CandidateRevision
                && Same(route, "MACHINE_FAIL")
                && testOnly;
        }

        internal JObject ToReceiptJson()
        {
            return new JObject
            {
                ["route"] = route,
                ["issuerVersion"] = issuerVersion,
                ["productionIssuerStatus"] = "FAILURE_ISSUER_PENDING",
                ["failureReceiptPath"] = JValue.CreateNull(),
                ["failureReceiptFileHash"] = JValue.CreateNull(),
                ["testOnly"] = testOnly
            };
        }

        internal bool AllowsTestOnlyInfrastructure { get { return testOnly; } }

        private static bool Same(string a, string b) { return string.Equals(a, b, StringComparison.Ordinal); }

#if UNITY_INCLUDE_TESTS
        internal static W24S5CandidateFailureAuthority IssueMachineFailureForTests(
            string effectId,
            string candidateReceiptPath,
            string candidateReceiptFileHash,
            int contractRevision,
            int candidateRevision)
        {
            return new W24S5CandidateFailureAuthority(
                W24S5CandidateRevisionTransaction.FailureAuthorityIssuerForTests,
                effectId,
                candidateReceiptPath,
                candidateReceiptFileHash,
                contractRevision,
                candidateRevision,
                "MACHINE_FAIL",
                "w24-s5-test-machine-failure/1",
                true);
        }
#endif
    }

    /// <summary>One-use, gate-issued approval.  Commit always replays every persisted input.</summary>
    internal sealed class W24S5CandidateRevisionApproval
    {
        private readonly W24S5CandidateRevisionRequest request;
        private readonly W24S5CandidateFailureAuthority failureAuthority;
        private readonly string candidateId;
        private readonly int candidateRevision;
        private readonly string preparedTreeHash;
        private bool consumed;

        internal W24S5CandidateRevisionApproval(
            object issuer,
            W24S5CandidateRevisionRequest approvedRequest,
            W24S5CandidateFailureAuthority approvedFailureAuthority,
            string approvedCandidateId,
            int approvedCandidateRevision,
            string approvedTreeHash)
        {
            if (!W24S5CandidateRevisionTransaction.IsApprovalIssuer(issuer))
                throw new InvalidOperationException("Candidate revision approvals may only be issued by the S5 candidate gate.");
            request = W24S5CandidateRevisionTransaction.Copy(approvedRequest);
            failureAuthority = approvedFailureAuthority;
            candidateId = approvedCandidateId;
            candidateRevision = approvedCandidateRevision;
            preparedTreeHash = approvedTreeHash;
        }

        internal bool TryConsume(
            object issuer,
            out W24S5CandidateRevisionRequest approvedRequest,
            out W24S5CandidateFailureAuthority approvedFailureAuthority,
            out string approvedCandidateId,
            out int approvedCandidateRevision,
            out string approvedTreeHash,
            out string error)
        {
            approvedRequest = null;
            approvedFailureAuthority = null;
            approvedCandidateId = null;
            approvedCandidateRevision = -1;
            approvedTreeHash = null;
            error = null;
            if (!W24S5CandidateRevisionTransaction.IsCommitIssuer(issuer)) { error = "Candidate revision commit issuer is invalid."; return false; }
            if (consumed) { error = "Candidate revision approval is already consumed."; return false; }
            consumed = true;
            approvedRequest = W24S5CandidateRevisionTransaction.Copy(request);
            approvedFailureAuthority = failureAuthority;
            approvedCandidateId = candidateId;
            approvedCandidateRevision = candidateRevision;
            approvedTreeHash = preparedTreeHash;
            return true;
        }
    }

    internal sealed class CandidateSnapshot
    {
        internal string EffectId;
        internal string CandidateId;
        internal int CandidateRevision;
        internal int ContractRevision;
        internal string ReceiptPath;
        internal string ReceiptFileHash;
        internal string CandidateRoot;
        internal string ContractPath;
        internal string ContractFileHash;
        internal string TracePath;
        internal string TraceFileHash;
        internal string ManifestSnapshotPath;
        internal string ManifestSnapshotFileHash;
        internal string ProductionManifestPath;
        internal string OwnedOutputRoot;
        internal string RuntimeEntryPath;
        internal string RuntimeEntryGuid;
        internal string PreviewScenePath;
        internal string PreviewSceneHash;
        internal string BuildHash;
        internal string CaptureProfileHash;
        internal string DesignSemanticHash;
        internal string PreviousReceiptPath;
        internal string PreviousReceiptFileHash;
        internal string CaptureToolBundleInputPath;
        internal string CaptureToolBundleInputFileHash;
        internal string CaptureToolBundleSnapshotPath;
        internal bool TestOnlyInfrastructure;
        internal JObject Receipt;
        internal JObject ContractJson;
        internal JObject TraceJson;
        internal JObject ManifestJson;
        internal VfxDesignContract Contract;
        internal VfxImplementationTrace Trace;
        internal HashSet<string> OwnedOutputPaths;
    }

    /// <summary>
    /// Gate-owned C0 -> C1 -> C2 candidate-directory transaction.  It never edits an earlier
    /// candidate, evidence directory, Contract, Trace, bundle, Manifest snapshot, or owned output.
    /// C1/C2 use contract-revision namespaces and physically disjoint asset roots.
    /// </summary>
    internal static class W24S5CandidateRevisionTransaction
    {
        internal const string CandidateRootPrefix = "docs/vfx-candidates/";
        internal const string CandidateContractName = "design-contract.json";
        internal const string CandidateTraceName = "implementation-trace.json";
        internal const string CandidateReceiptName = "candidate-receipt.json";
        internal const string ManifestSnapshotName = "production-manifest.json";
        internal const string CaptureToolBundleSnapshotName = "capture-tool.bundle.json";
        internal const string EvidenceDirectoryName = "evidence";
        internal const string ProductionFailureIssuerStatus = "FAILURE_ISSUER_PENDING";
        private const string RepositoryCommitLockName = ".w24-s5-candidate-revision.commit.lock";

        private static readonly object FailureAuthorityIssuer = new object();
        private static readonly object ApprovalIssuer = new object();
        private static readonly object CommitIssuer = new object();

#if UNITY_INCLUDE_TESTS
        internal static Action<string, string, string> BeforeCandidatePublishForTests;
        internal static Func<string, bool> TreatPathAsReparsePointForTests;
        internal static object FailureAuthorityIssuerForTests { get { return FailureAuthorityIssuer; } }
        internal static string RepositoryCommitLockPathForTests { get { return RepositoryCommitLockPath(); } }

        internal static IDisposable AcquireRepositoryCommitLockForTests()
        {
            return AcquireRepositoryCommitLock();
        }

        internal static void ResetCommitHooksForTests()
        {
            BeforeCandidatePublishForTests = null;
            TreatPathAsReparsePointForTests = null;
        }
#endif
        internal static bool IsFailureAuthorityIssuer(object issuer) { return ReferenceEquals(issuer, FailureAuthorityIssuer); }
        internal static bool IsApprovalIssuer(object issuer) { return ReferenceEquals(issuer, ApprovalIssuer); }
        internal static bool IsCommitIssuer(object issuer) { return ReferenceEquals(issuer, CommitIssuer); }

        internal static W24S5CandidateRevisionResult Evaluate(W24S5CandidateRevisionRequest request)
        {
            var result = new W24S5CandidateRevisionResult();
            result.Error("A gate-issued MACHINE_FAIL authority is required; production failure-receipt replay is not implemented yet.");
            return result;
        }

        internal static W24S5CandidateRevisionResult Evaluate(W24S5CandidateRevisionRequest request, W24S5CandidateFailureAuthority failureAuthority)
        {
            var result = new W24S5CandidateRevisionResult();
            PreparedCandidate prepared;
            if (!TryPrepare(request, failureAuthority, null, -1, result, out prepared)) return result;
            result.CandidateId = prepared.CandidateId;
            result.CandidateRevision = prepared.CandidateRevision;
            result.CandidateRoot = prepared.CandidateRoot;
            result.CandidateReceiptPath = prepared.CandidateRoot + "/" + CandidateReceiptName;
            result.Approval = new W24S5CandidateRevisionApproval(ApprovalIssuer, request, failureAuthority, prepared.CandidateId, prepared.CandidateRevision, prepared.TreeHash);
            result.Succeeded = true;
            return result;
        }

        internal static W24S5CandidateRevisionResult Commit(W24S5CandidateRevisionApproval approval)
        {
            var result = new W24S5CandidateRevisionResult();
            if (approval == null) { result.Error("A gate-issued candidate revision approval is required."); return result; }
            W24S5CandidateRevisionRequest request;
            W24S5CandidateFailureAuthority failureAuthority;
            string candidateId;
            int candidateRevision;
            string expectedTreeHash;
            string consumeError;
            if (!approval.TryConsume(CommitIssuer, out request, out failureAuthority, out candidateId, out candidateRevision, out expectedTreeHash, out consumeError))
            {
                result.Error(consumeError);
                return result;
            }

            RepositoryCommitLock repositoryLock;
            try { repositoryLock = AcquireRepositoryCommitLock(); }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException)
            {
                result.Error("Candidate repository commit lock is unavailable or unsafe: " + error.Message);
                return result;
            }

            try
            {
                PreparedCandidate prepared;
                if (!TryPrepare(request, failureAuthority, candidateId, candidateRevision, result, out prepared)) return result;
                if (!Same(prepared.TreeHash, expectedTreeHash))
                {
                    result.Error("Candidate inputs or derived bytes drifted after approval.");
                    return result;
                }

                try { WriteOnceCandidate(prepared); }
                catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException)
                {
                    result.Error("Candidate transaction was not committed: " + error.Message);
                    return result;
                }
                result.Succeeded = true;
                result.CandidateId = prepared.CandidateId;
                result.CandidateRevision = prepared.CandidateRevision;
                result.CandidateRoot = prepared.CandidateRoot;
                result.CandidateReceiptPath = prepared.CandidateRoot + "/" + CandidateReceiptName;
                result.CandidateReceiptFileHash = W24S5Hash.Sha256Bytes(prepared.Files[CandidateReceiptName]);
                return result;
            }
            finally
            {
                try { repositoryLock.Dispose(); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                    result.Succeeded = false;
                    result.Error("Candidate repository commit lock could not be released; inspect the write-once target before retry: " + error.Message);
                }
            }
        }

        internal static W24S5CandidateRevisionRequest Copy(W24S5CandidateRevisionRequest source)
        {
            if (source == null) return null;
            return new W24S5CandidateRevisionRequest
            {
                EffectId = source.EffectId,
                PreviousCandidateReceiptPath = source.PreviousCandidateReceiptPath,
                PreviousCandidateReceiptFileHash = source.PreviousCandidateReceiptFileHash,
                ProductionManifestPath = source.ProductionManifestPath,
                ProductionManifestFileHash = source.ProductionManifestFileHash,
                OwnedOutputRoot = source.OwnedOutputRoot,
                RuntimeEntryPath = source.RuntimeEntryPath,
                PreviewScenePath = source.PreviewScenePath,
                CaptureToolBundlePath = source.CaptureToolBundlePath,
                CaptureToolBundleFileHash = source.CaptureToolBundleFileHash
            };
        }

        private sealed class PreparedCandidate
        {
            internal string CandidateId;
            internal int CandidateRevision;
            internal string CandidateRoot;
            internal string TreeHash;
            internal readonly Dictionary<string, byte[]> Files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        }

        private sealed class RepositoryCommitLock : IDisposable
        {
            private readonly string path;
            private FileStream stream;

            internal RepositoryCommitLock(string lockPath, FileStream lockStream)
            {
                path = lockPath;
                stream = lockStream;
            }

            public void Dispose()
            {
                var held = stream;
                if (held == null) return;
                stream = null;
                try { held.Dispose(); }
                finally { File.Delete(path); }
            }
        }

        private sealed class BundleSnapshot
        {
            internal string ToolVersion;
            internal string CanonicalHash;
            internal byte[] BundleBytes;
            internal readonly List<BundleSourceSnapshot> Sources = new List<BundleSourceSnapshot>();
        }

        private sealed class BundleSourceSnapshot
        {
            internal string OriginalPath;
            internal string OriginalHash;
            internal string CandidateLocalPath;
            internal byte[] Bytes;
        }

        private static bool TryPrepare(
            W24S5CandidateRevisionRequest request,
            W24S5CandidateFailureAuthority failureAuthority,
            string expectedCandidateId,
            int expectedCandidateRevision,
            W24S5CandidateRevisionResult result,
            out PreparedCandidate prepared)
        {
            prepared = null;
            if (request == null || !EffectId(request.EffectId)) { result.Error("effectId must be stable lower_snake_case."); return false; }
            CandidateSnapshot previous;
            List<CandidateSnapshot> chain;
            if (!TryLoadChain(request.EffectId, request.PreviousCandidateReceiptPath, request.PreviousCandidateReceiptFileHash, result, out previous, out chain)) return false;
            if (failureAuthority == null || !failureAuthority.Matches(previous))
            {
                result.Error("A gate-issued failure authority bound to the exact previous candidate receipt is required. Caller-declared MACHINE_FAIL/VISUAL_FAIL values have no authority.");
                return false;
            }
            if (chain.Any(item => item.TestOnlyInfrastructure) && !failureAuthority.AllowsTestOnlyInfrastructure) { result.Error("A production failure issuer may not consume TEST_ONLY_TRANSACTION_INFRASTRUCTURE candidates."); return false; }
            if (previous.CandidateRevision >= 2) { result.Error("C2 is exhausted; the workflow aggregator must emit NEEDS_USER_DECISION and may not create C3."); return false; }

            var revision = previous.CandidateRevision + 1;
            var candidateId = "C" + revision.ToString();
            if (expectedCandidateRevision >= 0 && (revision != expectedCandidateRevision || !Same(candidateId, expectedCandidateId)))
            {
                result.Error("Gate-derived candidate revision changed after approval.");
                return false;
            }
            var revisionNamespace = "R" + previous.ContractRevision.ToString();
            var candidateRoot = CandidateRootPrefix + request.EffectId + "/" + revisionNamespace + "/" + candidateId;
            var candidateReceiptPath = candidateRoot + "/" + CandidateReceiptName;
            var expectedAssetRoot = "Assets/VFX/Candidates/" + revisionNamespace + "/" + candidateId + "/" + request.EffectId;
            var expectedBundleInput = "docs/vfx-contracts/capture-tools/" + request.EffectId + "." + revisionNamespace + "." + candidateId + ".bundle.json";
            if (!Same(request.OwnedOutputRoot, expectedAssetRoot)) { result.Error("Candidate owned-output root must be the gate-derived revision-owned root: " + expectedAssetRoot); return false; }
            if (!SafeAsset(request.RuntimeEntryPath) || !Under(request.RuntimeEntryPath, expectedAssetRoot) || !request.RuntimeEntryPath.EndsWith(".prefab", StringComparison.Ordinal)) { result.Error("Candidate Runtime Entry must be a Prefab under the revision-owned root."); return false; }
            if (!SafeAsset(request.PreviewScenePath) || !Under(request.PreviewScenePath, expectedAssetRoot) || !request.PreviewScenePath.EndsWith(".unity", StringComparison.Ordinal)) { result.Error("Candidate Preview Scene must be under the revision-owned root."); return false; }
            if (!CameraBoundToPreview(previous.Contract.CaptureProfile.CameraSerializedReference, previous.PreviewScenePath)) { result.Error("The predecessor cameraSerializedReference must be an exact child reference of its frozen Preview Scene before it can be remapped."); return false; }
            if (!Same(request.ProductionManifestPath, previous.ProductionManifestPath)) { result.Error("The authoritative live Manifest path must remain fixed across candidates."); return false; }
            if (!Same(request.CaptureToolBundlePath, expectedBundleInput)) { result.Error("Capture-tool bundle input must use the gate-derived versioned path: " + expectedBundleInput); return false; }
            if (Directory.Exists(RepositoryAbsolute(candidateRoot)) || File.Exists(RepositoryAbsolute(candidateRoot))) { result.Error(candidateId + " is write-once and already exists."); return false; }
            var later = CandidateRootPrefix + request.EffectId + "/" + revisionNamespace + "/C2";
            if (revision == 1 && (Directory.Exists(RepositoryAbsolute(later)) || File.Exists(RepositoryAbsolute(later)))) { result.Error("Candidate chain contains C2 without the required C1 predecessor."); return false; }
            var legacyCollision = CandidateRootPrefix + request.EffectId + "/" + candidateId;
            if (Directory.Exists(RepositoryAbsolute(legacyCollision)) || File.Exists(RepositoryAbsolute(legacyCollision))) { result.Error("A non-namespaced candidate collision exists: " + legacyCollision); return false; }

            W24S5PersistedFile manifestFile;
            if (!TryReadFormal(request.ProductionManifestPath, request.ProductionManifestFileHash, "production Manifest", result, out manifestFile)) return false;
            JObject manifest;
            try { manifest = Parse(manifestFile.Text); }
            catch (Exception error) { result.Error("Production Manifest JSON is invalid: " + error.Message); return false; }
            var rawBuildHash = (string)manifest["buildHash"];
            var runtime = manifest["runtimeEntry"] as JObject;
            string ownedError = null;
            if ((int?)manifest["manifestVersion"] != 1 || !Same((string)manifest["effectId"], request.EffectId) || !RawHash(rawBuildHash) || runtime == null || !Same((string)runtime["path"], request.RuntimeEntryPath) || !W24S5ProductionGate.VerifyOwnedOutputManifest(manifest, request.EffectId, request.RuntimeEntryPath, expectedAssetRoot, out ownedError))
            {
                result.Error("Candidate Manifest/owned outputs are invalid: " + ownedError);
                return false;
            }
            var owned = manifest["ownedOutputs"] as JArray;
            var newPaths = new HashSet<string>((owned ?? new JArray()).OfType<JObject>().Select(value => (string)value["path"]), StringComparer.Ordinal);
            if (!newPaths.Contains(request.PreviewScenePath) || !newPaths.Contains(request.RuntimeEntryPath)) { result.Error("Candidate Manifest must own both its Runtime Entry and Preview Scene."); return false; }
            if (chain.Any(item => item.OwnedOutputPaths.Overlaps(newPaths) || RootsOverlap(item.OwnedOutputRoot, expectedAssetRoot))) { result.Error("Candidate owned outputs/root must be disjoint from every earlier candidate."); return false; }

            var previewAbsolute = ProjectAbsolute(request.PreviewScenePath);
            if (!File.Exists(previewAbsolute) || HasReparsePointAtOrAbove(previewAbsolute, ProjectRoot())) { result.Error("Candidate Preview Scene is missing or reparse-backed."); return false; }
            var previewHash = W24S5Hash.Sha256Bytes(File.ReadAllBytes(previewAbsolute));

            BundleSnapshot bundle;
            if (!TrySnapshotBundle(request, result, out bundle)) return false;
            if (chain.Any(item => Same(request.CaptureToolBundlePath, item.CaptureToolBundleInputPath) || Same(request.CaptureToolBundlePath, item.CaptureToolBundleSnapshotPath))) { result.Error("A retry must use a new versioned capture-tool bundle path; no earlier input or snapshot path may be reused."); return false; }

            var contract = BuildContract(previous, candidateId, candidateRoot, candidateReceiptPath, request, manifest, rawBuildHash, previewHash, bundle);
            VfxDesignContract parsedContract;
            var contractText = Serialize(contract);
            var contractReport = VfxDesignContractJson.ValidateJson(contractText, out parsedContract);
            if (contractReport.HasErrors) { result.Error("Derived candidate Contract failed S1 validation: " + Describe(contractReport)); return false; }
            var semanticHash = DesignSemanticHash(contract);
            if (!Same(semanticHash, previous.DesignSemanticHash)) { result.Error("Normal candidate retry changed the frozen design-semantic identity or contractRevision."); return false; }

            var trace = BuildTrace(previous, parsedContract, contract, candidateId, revision, candidateRoot, request, rawBuildHash, (string)runtime["guid"]);
            var traceText = Serialize(trace);
            VfxImplementationTrace parsedTrace;
            try { parsedTrace = VfxImplementationTraceJson.FromJson(traceText); }
            catch (Exception error) { result.Error("Derived candidate Trace failed strict S1 parsing: " + error.Message); return false; }
            if (!ValidPendingTrace(parsedTrace, parsedContract, candidateId, revision, newPaths)) { result.Error("Derived candidate Trace is not a complete evidence-free pending plan whose object mappings resolve to exact Manifest-owned outputs."); return false; }

            var contractBytes = Utf8(contractText);
            var traceBytes = Utf8(traceText);
            var manifestBytes = manifestFileBytes(manifestFile);
            var bundleSnapshotPath = candidateRoot + "/" + CaptureToolBundleSnapshotName;
            var receipt = new JObject
            {
                ["candidateVersion"] = "w24-candidate-revision/2.0",
                ["candidateId"] = candidateId,
                ["candidateRevision"] = revision,
                ["contractRevisionNamespace"] = revisionNamespace,
                ["candidateStatus"] = candidateId + "_CAPTURE_PENDING",
                ["infrastructureStatus"] = "TEST_ONLY_TRANSACTION_INFRASTRUCTURE",
                ["effectId"] = request.EffectId,
                ["previousCandidateReceiptPath"] = previous.ReceiptPath,
                ["previousCandidateReceiptFileHash"] = previous.ReceiptFileHash,
                ["advanceAuthority"] = failureAuthority.ToReceiptJson(),
                ["productionManifestPath"] = request.ProductionManifestPath,
                ["productionManifestInputFileHash"] = manifestFile.Hash,
                ["productionManifestSnapshotPath"] = candidateRoot + "/" + ManifestSnapshotName,
                ["productionManifestSnapshotFileHash"] = W24S5Hash.Sha256Bytes(manifestBytes),
                ["ownedOutputRoot"] = expectedAssetRoot,
                ["ownedOutputs"] = owned.DeepClone(),
                ["buildHash"] = "sha256:" + rawBuildHash,
                ["runtimeEntryPath"] = request.RuntimeEntryPath,
                ["runtimeEntryGuid"] = (string)runtime["guid"],
                ["previewScenePath"] = request.PreviewScenePath,
                ["previewSceneHash"] = previewHash,
                ["contractPath"] = candidateRoot + "/" + CandidateContractName,
                ["contractFileHash"] = W24S5Hash.Sha256Bytes(contractBytes),
                ["contractHash"] = parsedContract.ContractHash,
                ["contractRevision"] = parsedContract.ContractRevision,
                ["designSemanticHash"] = semanticHash,
                ["tracePath"] = candidateRoot + "/" + CandidateTraceName,
                ["traceFileHash"] = W24S5Hash.Sha256Bytes(traceBytes),
                ["captureProfileHash"] = (string)trace["captureProfileHash"],
                ["captureToolBundleInputPath"] = request.CaptureToolBundlePath,
                ["captureToolBundleInputFileHash"] = request.CaptureToolBundleFileHash,
                ["captureToolBundleSnapshotPath"] = bundleSnapshotPath,
                ["captureToolBundleSnapshotFileHash"] = W24S5Hash.Sha256Bytes(bundle.BundleBytes),
                ["captureToolBundleCanonicalHash"] = bundle.CanonicalHash,
                ["captureToolSourceSnapshots"] = new JArray(bundle.Sources.Select(source => new JObject
                {
                    ["sourcePath"] = source.OriginalPath,
                    ["sourceSha256"] = source.OriginalHash,
                    ["snapshotPath"] = candidateRoot + "/" + source.CandidateLocalPath,
                    ["snapshotFileHash"] = W24S5Hash.Sha256Bytes(source.Bytes)
                })),
                ["evidenceRoot"] = candidateRoot + "/" + EvidenceDirectoryName,
                ["evidenceRevision"] = 0,
                ["visualStatus"] = "VISUAL_PENDING",
                ["visualQaRecordPath"] = JValue.CreateNull(),
                ["visualQaRecordFileHash"] = JValue.CreateNull(),
                ["userVerdictRecordPath"] = JValue.CreateNull(),
                ["userVerdictRecordFileHash"] = JValue.CreateNull(),
                ["maturityLevel"] = "L2_MAXIMUM_PENDING"
            };

            prepared = new PreparedCandidate { CandidateId = candidateId, CandidateRevision = revision, CandidateRoot = candidateRoot };
            prepared.Files.Add(CandidateContractName, contractBytes);
            prepared.Files.Add(CandidateTraceName, traceBytes);
            prepared.Files.Add(ManifestSnapshotName, manifestBytes);
            prepared.Files.Add(CaptureToolBundleSnapshotName, bundle.BundleBytes);
            foreach (var source in bundle.Sources) prepared.Files.Add(source.CandidateLocalPath, source.Bytes);
            prepared.Files.Add(CandidateReceiptName, Utf8(Serialize(receipt)));
            prepared.TreeHash = TreeHash(prepared.Files);
            return true;
        }

        private static JObject BuildContract(
            CandidateSnapshot previous,
            string candidateId,
            string candidateRoot,
            string receiptPath,
            W24S5CandidateRevisionRequest request,
            JObject manifest,
            string rawBuildHash,
            string previewHash,
            BundleSnapshot bundle)
        {
            var contract = (JObject)previous.ContractJson.DeepClone();
            RemapPreviewBindings(contract, previous.PreviewScenePath, request.PreviewScenePath);
            var capture = (JObject)contract["captureProfile"];
            var extensions = (JObject)contract["extensions"];
            capture["sceneSerializedReference"] = request.PreviewScenePath;
            capture["sceneHash"] = previewHash;
            capture["prefabManifestSerializedReference"] = candidateRoot + "/" + ManifestSnapshotName + "#buildHash";
            capture["prefabManifestHash"] = "sha256:" + rawBuildHash;
            capture["captureToolVersion"] = bundle.ToolVersion;
            capture["captureToolHash"] = bundle.CanonicalHash;
            extensions["captureBindingStatus"] = "FROZEN_PRE_" + candidateId;
            extensions["visualStatus"] = "VISUAL_PENDING";
            extensions["candidateId"] = candidateId;
            extensions["candidateStatus"] = candidateId + "_CAPTURE_PENDING";
            extensions["candidateReceipt"] = receiptPath;
            extensions["previousCandidateReceipt"] = previous.ReceiptPath;
            extensions["previousCandidateReceiptFileHash"] = previous.ReceiptFileHash;
            extensions["runtimeEntry"] = request.RuntimeEntryPath;
            extensions["previewScene"] = request.PreviewScenePath;
            extensions["manifest"] = candidateRoot + "/" + ManifestSnapshotName;
            extensions["implementationTrace"] = candidateRoot + "/" + CandidateTraceName;
            extensions["captureToolBundle"] = candidateRoot + "/" + CaptureToolBundleSnapshotName;
            contract["contractHash"] = VfxDesignContractJson.ComputeContractHash(contract.ToString(Formatting.None));
            return contract;
        }

        private static JObject BuildTrace(
            CandidateSnapshot previous,
            VfxDesignContract contract,
            JObject contractJson,
            string candidateId,
            int candidateRevision,
            string candidateRoot,
            W24S5CandidateRevisionRequest request,
            string rawBuildHash,
            string runtimeGuid)
        {
            var trace = (JObject)previous.TraceJson.DeepClone();
            trace["traceStatus"] = candidateId + "_CAPTURE_PENDING";
            trace["effectId"] = request.EffectId;
            trace["contractRevision"] = contract.ContractRevision;
            trace["contractHash"] = contract.ContractHash;
            trace["buildHash"] = "sha256:" + rawBuildHash;
            trace["captureProfileHash"] = "sha256:" + RecipeCanonicalizer.ComputeSha256(((JObject)contractJson["captureProfile"]).ToString(Formatting.None));
            trace["runtimeEntryAssetPath"] = request.RuntimeEntryPath;
            trace["runtimeEntryGuid"] = runtimeGuid;
            trace["candidateRevision"] = candidateRevision;
            trace["evidenceRevision"] = 0;
            foreach (var name in new[] { "candidateReceiptPath", "candidateReceiptFileHash", "captureMetadataPath", "captureMetadataFileHash", "evidenceTransitionReceiptPath", "evidenceTransitionReceiptFileHash", "completedTraceNormalizedSha256" }) trace.Remove(name);
            foreach (var item in ((JArray)trace["requirementTraces"] ?? new JArray()).OfType<JObject>())
            {
                item.Remove("authorityEvidence");
                item.Remove("crossEvidence");
                foreach (var mapped in ((JArray)item["objects"] ?? new JArray()).OfType<JObject>())
                {
                    var oldAsset = (string)mapped["assetPath"];
                    string replacement = null;
                    if (Same(oldAsset, previous.PreviewScenePath)) replacement = request.PreviewScenePath;
                    else if (Same(oldAsset, previous.RuntimeEntryPath)) replacement = request.RuntimeEntryPath;
                    else if (Under(oldAsset, previous.OwnedOutputRoot)) replacement = request.OwnedOutputRoot + oldAsset.Substring(previous.OwnedOutputRoot.Length);
                    if (replacement != null) mapped["assetPath"] = replacement;
                    var instance = (string)mapped["componentInstanceId"];
                    if (!string.IsNullOrEmpty(instance))
                    {
                        instance = instance.Replace(previous.RuntimeEntryPath, request.RuntimeEntryPath);
                        instance = instance.Replace(previous.PreviewScenePath, request.PreviewScenePath);
                        instance = instance.Replace(previous.OwnedOutputRoot, request.OwnedOutputRoot);
                        mapped["componentInstanceId"] = instance;
                    }
                }
            }
            return trace;
        }

        private static bool ValidPendingTrace(VfxImplementationTrace trace, VfxDesignContract contract, string candidateId, int revision, HashSet<string> ownedPaths)
        {
            if (trace == null || contract == null || !Same(trace.TraceStatus, candidateId + "_CAPTURE_PENDING") || trace.CandidateRevision != revision || trace.EvidenceRevision != 0 || !Same(trace.EffectId, contract.EffectId) || trace.ContractRevision != contract.ContractRevision || !Same(trace.ContractHash, contract.ContractHash)) return false;
            var expected = new HashSet<string>((contract.Requirements ?? Array.Empty<VfxDesignRequirement>()).Select(value => value.DesignRequirementId), StringComparer.Ordinal);
            var actual = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in trace.RequirementTraces ?? Array.Empty<VfxRequirementTrace>())
            {
                if (item == null || !actual.Add(item.DesignRequirementId) || (item.AuthorityEvidence != null && item.AuthorityEvidence.Length != 0) || (item.CrossEvidence != null && item.CrossEvidence.Length != 0)) return false;
                var requirement = (contract.Requirements ?? Array.Empty<VfxDesignRequirement>()).SingleOrDefault(value => Same(value.DesignRequirementId, item.DesignRequirementId));
                if (requirement == null || !Same(requirement.EvidenceAuthority, item.EvidenceAuthority)) return false;
                if ((item.Objects ?? Array.Empty<VfxTraceObject>()).Any(value => value == null || !SafeAsset(value.AssetPath) || ownedPaths == null || !ownedPaths.Contains(value.AssetPath))) return false;
            }
            return expected.SetEquals(actual);
        }

        private static bool TryLoadChain(string effectId, string receiptPath, string receiptFileHash, W24S5CandidateRevisionResult result, out CandidateSnapshot latest, out List<CandidateSnapshot> chain)
        {
            latest = null;
            chain = new List<CandidateSnapshot>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var currentPath = receiptPath;
            var currentHash = receiptFileHash;
            while (true)
            {
                if (!seen.Add(currentPath ?? string.Empty)) { result.Error("Candidate receipt chain contains a cycle."); return false; }
                CandidateSnapshot current;
                if (!TryLoadCandidate(effectId, currentPath, currentHash, result, out current)) return false;
                chain.Add(current);
                if (latest == null) latest = current;
                if (current.CandidateRevision == 0) break;
                if (string.IsNullOrEmpty(current.PreviousReceiptPath) || !W24Hash.IsCanonical(current.PreviousReceiptFileHash)) { result.Error("Candidate receipt lacks its immutable predecessor binding."); return false; }
                currentPath = current.PreviousReceiptPath;
                currentHash = current.PreviousReceiptFileHash;
            }
            chain.Reverse();
            for (var index = 0; index < chain.Count; index++)
            {
                if (chain[index].CandidateRevision != index || chain[index].ContractRevision != latest.ContractRevision || !Same(chain[index].DesignSemanticHash, latest.DesignSemanticHash)) { result.Error("Candidate chain has a gap or changed frozen design-semantic identity/contractRevision."); return false; }
                if (index > 0 && (!Same(chain[index].PreviousReceiptPath, chain[index - 1].ReceiptPath) || !Same(chain[index].PreviousReceiptFileHash, chain[index - 1].ReceiptFileHash))) { result.Error("Candidate receipt predecessor hash/path is inconsistent."); return false; }
                if (index > 0 && ContainsPreviewBinding(chain[index].ContractJson, chain[index - 1].PreviewScenePath)) { result.Error("Candidate Contract retained a stale binding to its predecessor Preview Scene."); return false; }
            }
            return true;
        }

        private static bool TryLoadCandidate(string effectId, string receiptPath, string receiptFileHash, W24S5CandidateRevisionResult result, out CandidateSnapshot snapshot)
        {
            snapshot = null;
            W24S5PersistedFile receiptFile;
            if (!TryReadFormal(receiptPath, receiptFileHash, "candidate receipt", result, out receiptFile)) return false;
            JObject receipt;
            try { receipt = Parse(receiptFile.Text); }
            catch (Exception error) { result.Error("Candidate receipt JSON is invalid: " + error.Message); return false; }
            var candidateId = (string)receipt["candidateId"];
            var revision = (int?)receipt["candidateRevision"];
            if (!revision.HasValue || revision.Value < 0 || revision.Value > 2 || !Same(candidateId, "C" + revision.Value.ToString()) || !Same((string)receipt["effectId"], effectId) || !Same((string)receipt["candidateStatus"], candidateId + "_CAPTURE_PENDING") || !Same((string)receipt["visualStatus"], "VISUAL_PENDING")) { result.Error("Candidate receipt identity/status is invalid."); return false; }
            if (revision.Value == 0 ? !Same((string)receipt["candidateVersion"], "w24-candidate/1.0") : !Same((string)receipt["candidateVersion"], "w24-candidate-revision/2.0")) { result.Error("Candidate receipt schema/version is invalid for its revision."); return false; }

            var root = revision.Value == 0
                ? CandidateRootPrefix + effectId + "/C0"
                : CandidateRootPrefix + effectId + "/" + (string)receipt["contractRevisionNamespace"] + "/" + candidateId;
            if (!Same(receiptPath, root + "/" + CandidateReceiptName)) { result.Error("Candidate receipt path is not canonical for its id/revision namespace."); return false; }
            var contractPath = (string)receipt["contractPath"];
            var tracePath = (string)receipt["tracePath"];
            var manifestPath = revision.Value == 0 ? (string)receipt["bootstrapManifestSnapshotPath"] : (string)receipt["productionManifestSnapshotPath"];
            var manifestHash = revision.Value == 0 ? (string)receipt["bootstrapManifestSnapshotFileHash"] : (string)receipt["productionManifestSnapshotFileHash"];
            if (!Same(contractPath, root + "/" + CandidateContractName) || !Same(tracePath, root + "/" + CandidateTraceName) || !Same(manifestPath, root + "/" + (revision.Value == 0 ? "bootstrap-manifest.json" : ManifestSnapshotName))) { result.Error("Candidate receipt names a noncanonical Contract/Trace/Manifest snapshot path."); return false; }
            W24S5PersistedFile contractFile;
            W24S5PersistedFile traceFile;
            W24S5PersistedFile manifestFile;
            if (!TryReadFormal(contractPath, (string)receipt["contractFileHash"], "candidate Contract", result, out contractFile) || !TryReadFormal(tracePath, (string)receipt["traceFileHash"], "candidate Trace", result, out traceFile) || !TryReadFormal(manifestPath, manifestHash, "candidate Manifest snapshot", result, out manifestFile)) return false;

            JObject contractJson;
            JObject traceJson;
            JObject manifestJson;
            VfxDesignContract contract;
            VfxImplementationTrace trace;
            try
            {
                contractJson = Parse(contractFile.Text);
                traceJson = Parse(traceFile.Text);
                manifestJson = Parse(manifestFile.Text);
                var contractReport = VfxDesignContractJson.ValidateJson(contractFile.Text, out contract);
                if (contractReport.HasErrors) { result.Error("Candidate Contract failed S1 validation: " + Describe(contractReport)); return false; }
                trace = VfxImplementationTraceJson.FromJson(traceFile.Text);
            }
            catch (Exception error) { result.Error("Candidate Contract/Trace/Manifest cannot be parsed: " + error.Message); return false; }
            var ownedPaths = new HashSet<string>(((JArray)receipt["ownedOutputs"] ?? new JArray()).OfType<JObject>().Select(value => (string)value["path"]), StringComparer.Ordinal);
            var computedCaptureProfileHash = "sha256:" + RecipeCanonicalizer.ComputeSha256(((JObject)contractJson["captureProfile"]).ToString(Formatting.None));
            if (!Same(contract.EffectId, effectId) || !Same(contract.ContractHash, (string)receipt["contractHash"]) || !Same(trace.TraceStatus, candidateId + "_CAPTURE_PENDING") || trace.CandidateRevision != revision.Value || trace.EvidenceRevision != 0 || !Same(trace.ContractHash, contract.ContractHash) || trace.ContractRevision != contract.ContractRevision || !Same(trace.BuildHash, (string)receipt["buildHash"]) || !Same(trace.CaptureProfileHash, (string)receipt["captureProfileHash"]) || !Same(trace.CaptureProfileHash, computedCaptureProfileHash) || !Same(trace.RuntimeEntryAssetPath, (string)receipt["runtimeEntryPath"]) || !Same(trace.RuntimeEntryGuid, (string)receipt["runtimeEntryGuid"]) || !ValidPendingTrace(trace, contract, candidateId, revision.Value, ownedPaths)) { result.Error("Candidate Contract/Trace identity or evidence-free owned-output plan differs from its receipt."); return false; }
            if (revision.Value > 0 && (!Same((string)receipt["contractRevisionNamespace"], "R" + contract.ContractRevision.ToString()) || (int?)receipt["contractRevision"] != contract.ContractRevision || !Same((string)receipt["evidenceRoot"], root + "/" + EvidenceDirectoryName) || (int?)receipt["evidenceRevision"] != 0)) { result.Error("Candidate revision namespace/evidence binding is invalid."); return false; }
            var expectedFiles = new HashSet<string>(StringComparer.Ordinal)
            {
                contractPath,
                tracePath,
                manifestPath,
                receiptPath
            };
            if (revision.Value > 0)
            {
                var bundlePath = (string)receipt["captureToolBundleSnapshotPath"];
                var bundleHash = (string)receipt["captureToolBundleSnapshotFileHash"];
                var bundleInputPath = (string)receipt["captureToolBundleInputPath"];
                var bundleInputHash = (string)receipt["captureToolBundleInputFileHash"];
                byte[] bundleBytes;
                if (!Same(bundlePath, root + "/" + CaptureToolBundleSnapshotName) || !Same((string)receipt["productionManifestInputFileHash"], manifestHash) || !TryReadFormalBytes(bundlePath, bundleHash, "candidate capture-tool bundle snapshot", result, out bundleBytes)) return false;
                byte[] bundleInputBytes;
                var expectedBundleInput = "docs/vfx-contracts/capture-tools/" + effectId + ".R" + contract.ContractRevision.ToString() + "." + candidateId + ".bundle.json";
                if (!Same(bundleInputPath, expectedBundleInput) || !Same(bundleInputHash, bundleHash) || !TryReadFormalBytes(bundleInputPath, bundleInputHash, "candidate capture-tool bundle input", result, out bundleInputBytes) || !bundleInputBytes.SequenceEqual(bundleBytes)) { result.Error("Candidate capture-tool input path/hash/bytes drifted from its immutable snapshot."); return false; }
                JObject bundle;
                try { bundle = Parse(new UTF8Encoding(false, true).GetString(bundleBytes)); }
                catch (Exception error) { result.Error("Candidate capture-tool bundle snapshot is invalid: " + error.Message); return false; }
                var canonicalBundleHash = W24S5Hash.Sha256Bytes(Utf8(CanonicalJson(bundle)));
                if (!Same(canonicalBundleHash, (string)receipt["captureToolBundleCanonicalHash"])
                    || !Same(canonicalBundleHash, contract.CaptureProfile.CaptureToolHash)
                    || !Same((string)bundle["toolVersion"], contract.CaptureProfile.CaptureToolVersion)
                    || !Same((string)contractJson.SelectToken("extensions.captureToolBundle"), bundlePath)
                    || !Same(contract.CaptureProfile.PrefabManifestSerializedReference, manifestPath + "#buildHash")
                    || !Same(contract.CaptureProfile.PrefabManifestHash, (string)receipt["buildHash"]))
                {
                    result.Error("Candidate capture-tool bundle and Manifest snapshot identities are inconsistent with the frozen Contract.");
                    return false;
                }
                expectedFiles.Add(bundlePath);
                var declaredSources = (bundle["sources"] as JArray ?? new JArray()).OfType<JObject>().ToArray();
                var sourceReceipts = (receipt["captureToolSourceSnapshots"] as JArray ?? new JArray()).OfType<JObject>().ToArray();
                if (declaredSources.Length == 0 || declaredSources.Length != sourceReceipts.Length) { result.Error("Candidate receipt must snapshot every and only the versioned capture-tool bundle sources."); return false; }
                for (var index = 0; index < declaredSources.Length; index++)
                {
                    var source = declaredSources[index];
                    var sourceReceipt = sourceReceipts[index];
                    var snapshotPath = (string)sourceReceipt["snapshotPath"];
                    var snapshotHash = (string)sourceReceipt["snapshotFileHash"];
                    byte[] sourceBytes;
                    if (!Same((string)sourceReceipt["sourcePath"], (string)source["path"]) || !Same((string)sourceReceipt["sourceSha256"], (string)source["sha256"]) || !Same(snapshotPath, root + "/capture-tool-sources/" + index.ToString("D4") + ".source") || !TryReadFormalBytes(snapshotPath, snapshotHash, "candidate capture-tool source snapshot", result, out sourceBytes) || !Same(snapshotHash, (string)sourceReceipt["sourceSha256"])) { result.Error("Candidate capture-tool source snapshot path/hash differs from the frozen bundle."); return false; }
                    expectedFiles.Add(snapshotPath);
                }
                var authority = receipt["advanceAuthority"] as JObject;
                if (authority == null || !Same((string)authority["route"], "MACHINE_FAIL") || !Same((string)authority["issuerVersion"], "w24-s5-test-machine-failure/1") || !Same((string)authority["productionIssuerStatus"], ProductionFailureIssuerStatus) || !IsJsonNull(authority["failureReceiptPath"]) || !IsJsonNull(authority["failureReceiptFileHash"]) || (bool?)authority["testOnly"] != true || !Same((string)receipt["infrastructureStatus"], "TEST_ONLY_TRANSACTION_INFRASTRUCTURE") || !IsJsonNull(receipt["visualQaRecordPath"]) || !IsJsonNull(receipt["visualQaRecordFileHash"]) || !IsJsonNull(receipt["userVerdictRecordPath"]) || !IsJsonNull(receipt["userVerdictRecordFileHash"]) || !Same((string)receipt["maturityLevel"], "L2_MAXIMUM_PENDING")) { result.Error("Candidate receipt attempted to synthesize unavailable failure/Visual-QA/user/L3/L4 authority."); return false; }
            }

            var rawBuild = (string)manifestJson["buildHash"];
            var runtime = manifestJson["runtimeEntry"] as JObject;
            var runtimePath = (string)receipt["runtimeEntryPath"];
            var ownedRoot = revision.Value == 0 ? ParentAsset(runtimePath) : (string)receipt["ownedOutputRoot"];
            string ownedError = null;
            if ((int?)manifestJson["manifestVersion"] != 1 || !RawHash(rawBuild) || !Same((string)manifestJson["effectId"], effectId) || runtime == null || !Same((string)receipt["buildHash"], "sha256:" + rawBuild) || !Same(trace.BuildHash, "sha256:" + rawBuild) || !Same((string)runtime["path"], runtimePath) || !Same((string)runtime["guid"], (string)receipt["runtimeEntryGuid"]) || !JToken.DeepEquals(receipt["ownedOutputs"], manifestJson["ownedOutputs"]) || !W24S5ProductionGate.VerifyOwnedOutputManifest(manifestJson, effectId, runtimePath, ownedRoot, out ownedError)) { result.Error("Candidate Manifest/owned-output replay failed: " + ownedError); return false; }

            var previewPath = (string)receipt["previewScenePath"];
            var previewHash = (string)receipt["previewSceneHash"];
            var previewAbsolute = ProjectAbsolute(previewPath);
            if (!SafeAsset(previewPath) || !File.Exists(previewAbsolute) || HasReparsePointAtOrAbove(previewAbsolute, ProjectRoot()) || !Same(W24S5Hash.Sha256Bytes(File.ReadAllBytes(previewAbsolute)), previewHash) || !Same(contract.CaptureProfile.SceneSerializedReference, previewPath) || !Same(contract.CaptureProfile.SceneHash, previewHash) || (revision.Value > 0 && !CameraBoundToPreview(contract.CaptureProfile.CameraSerializedReference, previewPath))) { result.Error("Candidate Preview Scene/camera bytes and paths drifted from its receipt and Contract."); return false; }

            var semanticHash = DesignSemanticHash(contractJson);
            if (revision.Value > 0 && !Same((string)receipt["designSemanticHash"], semanticHash)) { result.Error("Candidate design-semantic hash differs from its Contract."); return false; }
            snapshot = new CandidateSnapshot
            {
                EffectId = effectId,
                CandidateId = candidateId,
                CandidateRevision = revision.Value,
                ContractRevision = contract.ContractRevision,
                ReceiptPath = receiptPath,
                ReceiptFileHash = receiptFile.Hash,
                CandidateRoot = root,
                ContractPath = contractPath,
                ContractFileHash = contractFile.Hash,
                TracePath = tracePath,
                TraceFileHash = traceFile.Hash,
                ManifestSnapshotPath = manifestPath,
                ManifestSnapshotFileHash = manifestFile.Hash,
                ProductionManifestPath = (string)receipt["productionManifestPath"],
                OwnedOutputRoot = ownedRoot,
                RuntimeEntryPath = runtimePath,
                RuntimeEntryGuid = (string)receipt["runtimeEntryGuid"],
                PreviewScenePath = previewPath,
                PreviewSceneHash = previewHash,
                BuildHash = (string)receipt["buildHash"],
                CaptureProfileHash = (string)receipt["captureProfileHash"],
                DesignSemanticHash = semanticHash,
                PreviousReceiptPath = (string)receipt["previousCandidateReceiptPath"],
                PreviousReceiptFileHash = (string)receipt["previousCandidateReceiptFileHash"],
                CaptureToolBundleInputPath = revision.Value == 0 ? (string)contractJson.SelectToken("extensions.captureToolBundle") : (string)receipt["captureToolBundleInputPath"],
                CaptureToolBundleInputFileHash = revision.Value == 0 ? null : (string)receipt["captureToolBundleInputFileHash"],
                CaptureToolBundleSnapshotPath = revision.Value == 0 ? null : (string)receipt["captureToolBundleSnapshotPath"],
                TestOnlyInfrastructure = revision.Value > 0,
                Receipt = receipt,
                ContractJson = contractJson,
                TraceJson = traceJson,
                ManifestJson = manifestJson,
                Contract = contract,
                Trace = trace,
                OwnedOutputPaths = ownedPaths
            };
            if (!CandidateFileSetMatches(root, expectedFiles)) { result.Error("Candidate directory contains missing, extra, or path-drifted files outside its separately write-once evidence/terminal subtrees."); snapshot = null; return false; }
            return true;
        }

        private static bool TrySnapshotBundle(W24S5CandidateRevisionRequest request, W24S5CandidateRevisionResult result, out BundleSnapshot snapshot)
        {
            snapshot = null;
            if (!SafeRepositoryPath(request.CaptureToolBundlePath) || !request.CaptureToolBundlePath.StartsWith("docs/vfx-contracts/capture-tools/", StringComparison.Ordinal) || !request.CaptureToolBundlePath.EndsWith(".bundle.json", StringComparison.Ordinal)) { result.Error("Capture-tool bundle path must be a safe versioned docs/vfx-contracts/capture-tools file."); return false; }
            W24S5PersistedFile file;
            if (!TryReadFormal(request.CaptureToolBundlePath, request.CaptureToolBundleFileHash, "capture-tool bundle", result, out file)) return false;
            JObject bundle;
            try { bundle = Parse(file.Text); }
            catch (Exception error) { result.Error("Capture-tool bundle JSON is invalid: " + error.Message); return false; }
            var sources = (bundle["sources"] as JArray ?? new JArray()).OfType<JObject>().ToArray();
            var toolVersion = (string)bundle["toolVersion"];
            if (string.IsNullOrWhiteSpace((string)bundle["bundleVersion"]) || string.IsNullOrWhiteSpace(toolVersion) || sources.Length == 0) { result.Error("Capture-tool bundle must name its schema/tool version and at least one source."); return false; }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            snapshot = new BundleSnapshot { ToolVersion = toolVersion, CanonicalHash = W24S5Hash.Sha256Bytes(Utf8(CanonicalJson(bundle))), BundleBytes = manifestFileBytes(file) };
            for (var index = 0; index < sources.Length; index++)
            {
                var path = (string)sources[index]["path"];
                var hash = (string)sources[index]["sha256"];
                if (!SafeRepositoryPath(path) || !W24Hash.IsCanonical(hash) || !seen.Add(path)) { result.Error("Capture-tool bundle contains an unsafe, duplicate, or unhashed source."); return false; }
                var absolute = RepositoryAbsolute(path);
                if (!File.Exists(absolute) || HasReparsePointAtOrAbove(absolute, RepositoryRoot())) { result.Error("Capture-tool source is missing or reparse-backed: " + path); return false; }
                var bytes = File.ReadAllBytes(absolute);
                if (!Same(W24S5Hash.Sha256Bytes(bytes), hash)) { result.Error("Capture-tool source bytes drifted from the versioned bundle: " + path); return false; }
                snapshot.Sources.Add(new BundleSourceSnapshot { OriginalPath = path, OriginalHash = hash, CandidateLocalPath = "capture-tool-sources/" + index.ToString("D4") + ".source", Bytes = bytes });
            }
            return true;
        }

        private static void WriteOnceCandidate(PreparedCandidate prepared)
        {
            var target = RepositoryAbsolute(prepared.CandidateRoot);
            var parent = Path.GetDirectoryName(target);
            var candidateBoundary = RepositoryAbsolute(CandidateRootPrefix.TrimEnd('/'));
            RejectReparsePoints(parent, candidateBoundary);
            Directory.CreateDirectory(parent);
            RejectReparsePoints(parent, candidateBoundary);
            if (Directory.Exists(target) || File.Exists(target)) throw new IOException("Candidate is write-once and already exists: " + prepared.CandidateRoot);
            var pending = Path.Combine(parent, "." + prepared.CandidateId + ".pending-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(pending);
                foreach (var pair in prepared.Files.OrderBy(value => value.Key, StringComparer.Ordinal))
                {
                    var output = Path.GetFullPath(Path.Combine(pending, pair.Key.Replace('/', Path.DirectorySeparatorChar)));
                    var prefix = Path.GetFullPath(pending).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    if (!output.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidDataException("Candidate local path escapes pending root.");
                    Directory.CreateDirectory(Path.GetDirectoryName(output));
                    using (var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None)) stream.Write(pair.Value, 0, pair.Value.Length);
                }
#if UNITY_INCLUDE_TESTS
                var beforePublish = BeforeCandidatePublishForTests;
                if (beforePublish != null) beforePublish(pending, target, parent);
#endif
                RejectReparsePoints(parent, candidateBoundary);
                RejectReparsePoints(pending, candidateBoundary);
                if (Directory.Exists(target) || File.Exists(target)) throw new IOException("Candidate is write-once and already exists: " + prepared.CandidateRoot);
                Directory.Move(pending, target);
            }
            finally
            {
                if (Directory.Exists(pending) && UnderAbsolute(pending, parent)) Directory.Delete(pending, true);
            }
        }

        private static string DesignSemanticHash(JObject contract)
        {
            var value = (JObject)contract.DeepClone();
            value.Remove("contractHash");
            var capture = value["captureProfile"] as JObject;
            var previewPath = (string)capture?["sceneSerializedReference"];
            if (!string.IsNullOrEmpty(previewPath)) RemapPreviewBindings(value, previewPath, "$W24_CANDIDATE_PREVIEW");
            if (capture != null) foreach (var name in new[] { "cameraSerializedReference", "sceneSerializedReference", "sceneHash", "prefabManifestSerializedReference", "prefabManifestHash", "captureToolVersion", "captureToolHash" }) capture.Remove(name);
            var extensions = value["extensions"] as JObject;
            if (extensions != null) foreach (var name in new[] { "captureBindingStatus", "visualStatus", "candidateId", "candidateStatus", "candidateReceipt", "previousCandidateReceipt", "previousCandidateReceiptFileHash", "runtimeEntry", "previewScene", "manifest", "implementationTrace", "captureToolBundle", "bootstrapContractPath", "bootstrapContractFileHash", "bootstrapTracePath", "bootstrapTraceFileHash" }) extensions.Remove(name);
            return W24S5Hash.Sha256Bytes(Utf8(CanonicalJson(value)));
        }

        private static void RemapPreviewBindings(JToken token, string oldPreviewPath, string newPreviewPath)
        {
            if (token == null || string.IsNullOrEmpty(oldPreviewPath) || string.IsNullOrEmpty(newPreviewPath)) return;
            var value = token as JValue;
            if (value != null && value.Type == JTokenType.String)
            {
                var text = (string)value;
                if (Same(text, oldPreviewPath)) value.Value = newPreviewPath;
                else if (!string.IsNullOrEmpty(text) && text.StartsWith(oldPreviewPath + "#", StringComparison.Ordinal)) value.Value = newPreviewPath + text.Substring(oldPreviewPath.Length);
                return;
            }
            var container = token as JContainer;
            if (container != null) foreach (var child in container.Children().ToArray()) RemapPreviewBindings(child, oldPreviewPath, newPreviewPath);
        }

        private static bool ContainsPreviewBinding(JToken token, string previewPath)
        {
            if (token == null || string.IsNullOrEmpty(previewPath)) return false;
            var value = token as JValue;
            if (value != null && value.Type == JTokenType.String)
            {
                var text = (string)value;
                return Same(text, previewPath) || (!string.IsNullOrEmpty(text) && text.StartsWith(previewPath + "#", StringComparison.Ordinal));
            }
            var container = token as JContainer;
            return container != null && container.Children().Any(child => ContainsPreviewBinding(child, previewPath));
        }

        private static string TreeHash(Dictionary<string, byte[]> files)
        {
            var builder = new StringBuilder();
            foreach (var pair in files.OrderBy(value => value.Key, StringComparer.Ordinal)) builder.Append(W24S5Hash.Sha256Bytes(pair.Value)).Append("  ").Append(pair.Key).Append('\n');
            return W24S5Hash.Sha256Bytes(Utf8(builder.ToString()));
        }

        private static bool TryReadFormal(string relativePath, string expectedHash, string label, W24S5CandidateRevisionResult result, out W24S5PersistedFile file)
        {
            file = null;
            var gate = new W24S5ProductionGateResult();
            file = W24S5ProductionGate.ReadPersisted(gate, relativePath, expectedHash, label, "W24S5-CR001", W24S5RecordScope.Formal);
            foreach (var issue in gate.Issues.Where(value => value.IsError)) result.Error(issue.Code + " " + issue.Path + ": " + issue.Message);
            return file != null && !gate.HasErrors;
        }

        private static bool TryReadFormalBytes(string relativePath, string expectedHash, string label, W24S5CandidateRevisionResult result, out byte[] bytes)
        {
            bytes = null;
            string absolute;
            if (!SafeRepositoryPath(relativePath) || !W24Hash.IsCanonical(expectedHash) || !W24S5ProductionGate.TryResolvePersistedPath(relativePath, W24S5RecordScope.Formal, out absolute) || !File.Exists(absolute) || HasReparsePointAtOrAbove(absolute, RepositoryRoot()))
            {
                result.Error(label + " path/hash is invalid, missing, or reparse-backed.");
                return false;
            }
            bytes = File.ReadAllBytes(absolute);
            if (!Same(W24S5Hash.Sha256Bytes(bytes), expectedHash))
            {
                result.Error(label + " bytes differ from the receipt hash.");
                bytes = null;
                return false;
            }
            return true;
        }

        private static bool CandidateFileSetMatches(string candidateRoot, HashSet<string> expected)
        {
            var absolute = RepositoryAbsolute(candidateRoot);
            if (!Directory.Exists(absolute)) return false;
            var actual = new HashSet<string>(Directory.GetFiles(absolute, "*", SearchOption.AllDirectories)
                .Select(path => RepositoryRelative(path))
                .Where(path => path != null
                    && !path.StartsWith(candidateRoot + "/" + EvidenceDirectoryName + "/", StringComparison.Ordinal)
                    && !path.StartsWith(candidateRoot + "/terminal/", StringComparison.Ordinal)), StringComparer.Ordinal);
            return actual.SetEquals(expected);
        }

        private static byte[] manifestFileBytes(W24S5PersistedFile file)
        {
            string absolute;
            if (!W24S5ProductionGate.TryResolvePersistedPath(file.RelativePath, W24S5RecordScope.Formal, out absolute)) throw new InvalidDataException("Persisted file path became unsafe.");
            var bytes = File.ReadAllBytes(absolute);
            if (!Same(W24S5Hash.Sha256Bytes(bytes), file.Hash)) throw new InvalidDataException("Persisted file bytes drifted after their pinned read: " + file.RelativePath);
            return bytes;
        }

        private static JObject Parse(string text)
        {
            return W24StrictJsonText.ParseObject(text, "W24 S5 candidate revision JSON");
        }

        private static string CanonicalJson(JToken value)
        {
            if (value is JObject obj)
            {
                var sorted = new JObject();
                foreach (var property in obj.Properties().OrderBy(item => item.Name, StringComparer.Ordinal)) sorted.Add(property.Name, JToken.Parse(CanonicalJson(property.Value)));
                return sorted.ToString(Formatting.None);
            }
            if (value is JArray array) return new JArray(array.Select(item => JToken.Parse(CanonicalJson(item)))).ToString(Formatting.None);
            return value.ToString(Formatting.None);
        }

        private static string Describe(W24GateReport report) { return string.Join(" | ", report.Issues.Select(issue => issue.Code + " " + issue.Path + " " + issue.Message).ToArray()); }
        private static string Serialize(JToken token) { return token.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n"; }
        private static byte[] Utf8(string text) { return new UTF8Encoding(false, true).GetBytes(text); }
        private static bool Same(string a, string b) { return string.Equals(a, b, StringComparison.Ordinal); }
        private static bool IsJsonNull(JToken value) { return value != null && value.Type == JTokenType.Null; }
        private static bool RawHash(string value) { return value != null && value.Length == 64 && value.All(character => (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')); }
        private static bool EffectId(string value) { return !string.IsNullOrEmpty(value) && Regex.IsMatch(value, "^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$"); }
        private static bool SafeAsset(string value) { return SafeRepositoryPath(value) && value.StartsWith("Assets/", StringComparison.Ordinal); }
        private static bool SafeRepositoryPath(string value) { return !string.IsNullOrWhiteSpace(value) && !Path.IsPathRooted(value) && value.IndexOf('\\') < 0 && value.Split('/').All(part => !string.IsNullOrEmpty(part) && part != "." && part != ".."); }
        private static bool Under(string path, string root) { return !string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(root) && path.StartsWith(root.TrimEnd('/') + "/", StringComparison.Ordinal); }
        private static bool CameraBoundToPreview(string cameraReference, string previewPath) { return !string.IsNullOrEmpty(previewPath) && !string.IsNullOrEmpty(cameraReference) && cameraReference.StartsWith(previewPath + "#", StringComparison.Ordinal) && cameraReference.Length > previewPath.Length + 1; }
        private static bool RootsOverlap(string a, string b) { return Same(a, b) || Under(a, b) || Under(b, a); }
        private static string ParentAsset(string path) { return string.IsNullOrEmpty(path) ? null : Path.GetDirectoryName(path).Replace('\\', '/'); }
        private static string RepositoryRoot() { return Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName; }
        private static string ProjectRoot() { return Directory.GetParent(Application.dataPath).FullName; }
        private static string RepositoryAbsolute(string relative) { return Path.GetFullPath(Path.Combine(RepositoryRoot(), relative.Replace('/', Path.DirectorySeparatorChar))); }
        private static string ProjectAbsolute(string relative) { return Path.GetFullPath(Path.Combine(ProjectRoot(), relative.Replace('/', Path.DirectorySeparatorChar))); }
        private static string RepositoryRelative(string absolute) { var root = Path.GetFullPath(RepositoryRoot()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar; var full = Path.GetFullPath(absolute); return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full.Substring(root.Length).Replace('\\', '/') : null; }
        private static bool UnderAbsolute(string path, string root) { var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar; return Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase); }

        private static string RepositoryCommitLockPath()
        {
            return RepositoryAbsolute(CandidateRootPrefix + RepositoryCommitLockName);
        }

        private static RepositoryCommitLock AcquireRepositoryCommitLock()
        {
            var path = RepositoryCommitLockPath();
            var directory = Path.GetDirectoryName(path);
            var repositoryBoundary = RepositoryRoot();
            RejectReparsePoints(directory, repositoryBoundary);
            Directory.CreateDirectory(directory);
            RejectReparsePoints(directory, repositoryBoundary);
            var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            return new RepositoryCommitLock(path, stream);
        }

        private static void RejectReparsePoints(string path, string boundary)
        {
            var stop = Path.GetFullPath(boundary).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            for (var current = new DirectoryInfo(Path.GetFullPath(path)); current != null; current = current.Parent)
            {
                var isReparsePoint = (File.Exists(current.FullName) || Directory.Exists(current.FullName)) && (File.GetAttributes(current.FullName) & FileAttributes.ReparsePoint) != 0;
#if UNITY_INCLUDE_TESTS
                var testProbe = TreatPathAsReparsePointForTests;
                if (testProbe != null && testProbe(current.FullName)) isReparsePoint = true;
#endif
                if (isReparsePoint) throw new InvalidDataException("Candidate path contains a symlink/junction/reparse point: " + current.FullName);
                if (Same(current.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), stop)) break;
            }
        }

        private static bool HasReparsePointAtOrAbove(string path, string boundary)
        {
            try { RejectReparsePoints(path, boundary); return false; }
            catch (IOException) { return true; }
            catch (UnauthorizedAccessException) { return true; }
            catch (InvalidDataException) { return true; }
        }
    }
}
