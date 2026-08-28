using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Validation;
using VFXComposer.Editor.W24.S5;

namespace VFXComposer.Editor.W24.S1
{
    /// <summary>
    /// Converts a consumed, immutable S5 pre-C0 bootstrap approval into a separate write-once
    /// C0 identity candidate. The bootstrap Contract/Trace are never rewritten. The resulting
    /// Trace remains C0_CAPTURE_PENDING and deliberately contains no invented evidence or L3/L4.
    /// </summary>
    internal static class W24CandidateIdentityFreezer
    {
        internal const string CandidateId = "C0";
        internal const string CandidateContractName = "design-contract.json";
        internal const string CandidateTraceName = "implementation-trace.json";
        internal const string CandidateReceiptName = "candidate-receipt.json";
        internal const string BootstrapManifestSnapshotName = "bootstrap-manifest.json";

        internal static string FreezeC0(W24S5BootstrapReceipt bootstrap, string previewSceneAssetPath)
        {
            if (bootstrap == null) throw new InvalidOperationException("C0 identity freeze requires an immutable S5 bootstrap receipt.");
            if (string.IsNullOrWhiteSpace(previewSceneAssetPath) || !previewSceneAssetPath.StartsWith("Assets/", StringComparison.Ordinal) || previewSceneAssetPath.IndexOf("..", StringComparison.Ordinal) >= 0)
                throw new InvalidDataException("C0 Preview Scene must be a safe project Asset path.");

            var repositoryRoot = Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName;
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var bootstrapContractPath = RepositoryAbsolute(repositoryRoot, bootstrap.ContractPath);
            var bootstrapTracePath = RepositoryAbsolute(repositoryRoot, bootstrap.TracePath);
            var previewAbsolute = ProjectAbsolute(projectRoot, previewSceneAssetPath);
            var manifestAbsolute = ProjectAbsolute(projectRoot, bootstrap.ManifestPath);
            if (!Same(manifestAbsolute, VfxProjectRules.ManifestAbsolutePath(bootstrap.EffectId)))
                throw new InvalidDataException("Bootstrap receipt does not name the authoritative effect Manifest.");
            RequireFile(bootstrapContractPath, "bootstrap Contract");
            RequireFile(bootstrapTracePath, "bootstrap Trace");
            RequireFile(previewAbsolute, "formal Preview Scene");
            RequireFile(manifestAbsolute, "first formal Manifest");
            RejectReparsePoints(bootstrapContractPath, Path.Combine(repositoryRoot, "docs"));
            RejectReparsePoints(bootstrapTracePath, Path.Combine(repositoryRoot, "docs"));
            RejectReparsePoints(previewAbsolute, projectRoot);
            RejectReparsePoints(manifestAbsolute, projectRoot);

            var contractBytes = File.ReadAllBytes(bootstrapContractPath);
            var traceBytes = File.ReadAllBytes(bootstrapTracePath);
            if (!Same(Sha256Canonical(contractBytes), bootstrap.ContractFileHash) || !Same(Sha256Canonical(traceBytes), bootstrap.TraceFileHash))
                throw new InvalidDataException("Bootstrap Contract/Trace bytes changed after S5 admission.");

            VfxDesignContract parsedContract;
            var contractText = StrictUtf8(contractBytes);
            var contractReport = VfxDesignContractJson.ValidateJson(contractText, out parsedContract);
            if (contractReport.HasErrors) throw new InvalidDataException("Bootstrap Contract is no longer valid: " + Describe(contractReport));
            if (!Same(parsedContract.EffectId, bootstrap.EffectId) || parsedContract.ContractRevision != bootstrap.ContractRevision || !Same(parsedContract.ContractHash, bootstrap.ContractHash))
                throw new InvalidDataException("Bootstrap receipt identity differs from its Contract.");
            var bootstrapTrace = VfxImplementationTraceJson.FromJson(StrictUtf8(traceBytes));
            if (!Same(bootstrapTrace.TraceStatus, "PENDING_FIRST_FORMAL_BUILD_BINDING") || !Same(bootstrapTrace.BuildHash, "pending:formal-build") || !Same(bootstrapTrace.CaptureProfileHash, "pending:formal-build") || !Same(bootstrapTrace.RuntimeEntryGuid, "pending:formal-build"))
                throw new InvalidDataException("Bootstrap Trace is not the immutable pending preregistration.");

            var manifestBytes = File.ReadAllBytes(manifestAbsolute);
            var manifest = JObject.Parse(StrictUtf8(manifestBytes), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            var formal = manifest["formalProduction"] as JObject;
            var runtime = manifest["runtimeEntry"] as JObject;
            var rawBuildHash = (string)manifest["buildHash"];
            var runtimeGuid = (string)runtime?["guid"];
            if (!W24S5ProductionGate.HasExactEvidenceFreeBootstrapBinding(formal, bootstrap.ContractPath, bootstrap.ContractFileHash, bootstrap.ContractHash, bootstrap.ContractRevision, bootstrap.TracePath, bootstrap.TraceFileHash))
                throw new InvalidDataException("First formal Manifest does not retain the exact immutable bootstrap binding.");
            if (!RawHash(rawBuildHash) || !IsGuid(runtimeGuid) || !Same((string)runtime["path"], bootstrap.RuntimeEntryPath))
                throw new InvalidDataException("First formal Manifest lacks a real build hash or Runtime Entry GUID.");
            string ownedOutputError;
            if (!W24S5ProductionGate.VerifyOwnedOutputManifest(manifest, bootstrap.EffectId, bootstrap.RuntimeEntryPath, bootstrap.OwnedOutputRoot, out ownedOutputError))
                throw new InvalidDataException("First formal Manifest owned outputs drifted before C0 freeze: " + ownedOutputError);
            var ownedOutputs = ((JArray)manifest["ownedOutputs"]).DeepClone();

            var candidateRootRelative = "docs/vfx-candidates/" + bootstrap.EffectId + "/" + CandidateId;
            var candidateContractRelative = candidateRootRelative + "/" + CandidateContractName;
            var candidateTraceRelative = candidateRootRelative + "/" + CandidateTraceName;
            var candidateReceiptRelative = candidateRootRelative + "/" + CandidateReceiptName;
            var bootstrapManifestSnapshotRelative = candidateRootRelative + "/" + BootstrapManifestSnapshotName;

            var contract = JObject.Parse(contractText, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            var capture = (JObject)contract["captureProfile"];
            var extensions = (JObject)contract["extensions"];
            if (!Same((string)capture["sceneSerializedReference"], previewSceneAssetPath)) throw new InvalidDataException("Preview Scene path differs from the bootstrap Capture Profile.");
            capture["sceneHash"] = Sha256Canonical(File.ReadAllBytes(previewAbsolute));
            capture["prefabManifestHash"] = "sha256:" + rawBuildHash;
            extensions["captureBindingStatus"] = "FROZEN_PRE_C0";
            extensions["visualStatus"] = "VISUAL_PENDING";
            extensions["candidateId"] = CandidateId;
            extensions["candidateStatus"] = "C0_CAPTURE_PENDING";
            extensions["bootstrapContractPath"] = bootstrap.ContractPath;
            extensions["bootstrapContractFileHash"] = bootstrap.ContractFileHash;
            extensions["bootstrapTracePath"] = bootstrap.TracePath;
            extensions["bootstrapTraceFileHash"] = bootstrap.TraceFileHash;
            extensions["implementationTrace"] = candidateTraceRelative;
            extensions["candidateReceipt"] = candidateReceiptRelative;
            contract["contractHash"] = VfxDesignContractJson.ComputeContractHash(contract.ToString(Formatting.None));
            var candidateContractText = Serialize(contract);

            VfxDesignContract frozenContract;
            var frozenReport = VfxDesignContractJson.ValidateJson(candidateContractText, out frozenContract);
            if (frozenReport.HasErrors) throw new InvalidDataException("Frozen C0 Contract failed S1 validation: " + Describe(frozenReport));

            var trace = JObject.Parse(StrictUtf8(traceBytes), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            trace["traceStatus"] = "C0_CAPTURE_PENDING";
            trace["candidateRevision"] = 0;
            trace["evidenceRevision"] = 0;
            trace["contractRevision"] = frozenContract.ContractRevision;
            trace["contractHash"] = frozenContract.ContractHash;
            trace["buildHash"] = "sha256:" + rawBuildHash;
            trace["captureProfileHash"] = "sha256:" + RecipeCanonicalizer.ComputeSha256(capture.ToString(Formatting.None));
            trace["runtimeEntryAssetPath"] = bootstrap.RuntimeEntryPath;
            trace["runtimeEntryGuid"] = runtimeGuid;
            var candidateTraceText = Serialize(trace);
            var reparsedTrace = VfxImplementationTraceJson.FromJson(candidateTraceText);
            if (!Same(reparsedTrace.TraceStatus, "C0_CAPTURE_PENDING") || reparsedTrace.CandidateRevision != 0 || reparsedTrace.EvidenceRevision != 0 || !Same(reparsedTrace.ContractHash, frozenContract.ContractHash) || !Same(reparsedTrace.BuildHash, "sha256:" + rawBuildHash) || !Same(reparsedTrace.RuntimeEntryGuid, runtimeGuid))
                throw new InvalidDataException("Frozen C0 Trace identity round-trip failed.");

            var candidateContractBytes = Utf8(candidateContractText);
            var candidateTraceBytes = Utf8(candidateTraceText);
            var receipt = new JObject
            {
                ["candidateVersion"] = "w24-candidate/1.0",
                ["candidateId"] = CandidateId,
                ["candidateRevision"] = 0,
                ["candidateStatus"] = "C0_CAPTURE_PENDING",
                ["effectId"] = bootstrap.EffectId,
                ["bootstrapContractPath"] = bootstrap.ContractPath,
                ["bootstrapContractFileHash"] = bootstrap.ContractFileHash,
                ["bootstrapContractHash"] = bootstrap.ContractHash,
                ["bootstrapContractRevision"] = bootstrap.ContractRevision,
                ["bootstrapTracePath"] = bootstrap.TracePath,
                ["bootstrapTraceFileHash"] = bootstrap.TraceFileHash,
                ["productionManifestPath"] = bootstrap.ManifestPath,
                // The authoritative manifest evolves at C1.  This immutable C0 copy is the
                // evidence object; never make later formal updates look like C0 drift.
                ["bootstrapManifestSnapshotPath"] = bootstrapManifestSnapshotRelative,
                ["bootstrapManifestSnapshotFileHash"] = Sha256Canonical(manifestBytes),
                ["ownedOutputs"] = ownedOutputs,
                ["buildHash"] = "sha256:" + rawBuildHash,
                ["runtimeEntryPath"] = bootstrap.RuntimeEntryPath,
                ["runtimeEntryGuid"] = runtimeGuid,
                ["previewScenePath"] = previewSceneAssetPath,
                ["previewSceneHash"] = (string)capture["sceneHash"],
                ["contractPath"] = candidateContractRelative,
                ["contractFileHash"] = Sha256Canonical(candidateContractBytes),
                ["contractHash"] = frozenContract.ContractHash,
                ["tracePath"] = candidateTraceRelative,
                ["traceFileHash"] = Sha256Canonical(candidateTraceBytes),
                ["captureProfileHash"] = (string)trace["captureProfileHash"],
                ["visualStatus"] = "VISUAL_PENDING"
            };

            WriteCandidateDirectory(repositoryRoot, candidateRootRelative, candidateContractBytes, candidateTraceBytes, Utf8(Serialize(receipt)), manifestBytes);
            return candidateRootRelative;
        }

        private static void WriteCandidateDirectory(string repositoryRoot, string candidateRootRelative, byte[] contract, byte[] trace, byte[] receipt, byte[] bootstrapManifest)
        {
            var candidateRoot = RepositoryAbsolute(repositoryRoot, candidateRootRelative);
            var parent = Path.GetDirectoryName(candidateRoot);
            RejectReparsePoints(parent, Path.Combine(repositoryRoot, "docs"));
            Directory.CreateDirectory(parent);
            if (Directory.Exists(candidateRoot) || File.Exists(candidateRoot)) throw new IOException("C0 candidate is write-once and already exists: " + candidateRootRelative);
            var pending = Path.Combine(parent, ".C0.pending-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(pending);
                WriteNew(Path.Combine(pending, CandidateContractName), contract);
                WriteNew(Path.Combine(pending, CandidateTraceName), trace);
                WriteNew(Path.Combine(pending, CandidateReceiptName), receipt);
                WriteNew(Path.Combine(pending, BootstrapManifestSnapshotName), bootstrapManifest);
                Directory.Move(pending, candidateRoot);
            }
            finally
            {
                if (Directory.Exists(pending) && IsChildOf(pending, parent)) Directory.Delete(pending, true);
            }
        }

        private static string RepositoryAbsolute(string repositoryRoot, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.IndexOf('\\') >= 0 || relative.Split('/').Any(segment => string.IsNullOrEmpty(segment) || segment == "." || segment == "..")) throw new InvalidDataException("Unsafe repository-relative path: " + (relative ?? "<null>"));
            var root = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var absolute = Path.GetFullPath(Path.Combine(repositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!absolute.StartsWith(root, StringComparison.Ordinal)) throw new InvalidDataException("Repository path escapes its root: " + relative);
            return absolute;
        }

        private static string ProjectAbsolute(string projectRoot, string assetPath)
        {
            var root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var absolute = Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!absolute.StartsWith(root, StringComparison.Ordinal)) throw new InvalidDataException("Asset path escapes the Unity project: " + assetPath);
            return absolute;
        }

        private static bool IsChildOf(string path, string parent)
        {
            var root = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(root, StringComparison.Ordinal);
        }
        private static void RejectReparsePoints(string path, string boundary)
        {
            var stop = Path.GetFullPath(boundary).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            for (var current = new DirectoryInfo(Path.GetFullPath(path)); current != null; current = current.Parent)
            {
                if ((File.Exists(current.FullName) || Directory.Exists(current.FullName)) && (File.GetAttributes(current.FullName) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("C0 candidate path contains a symlink/junction/reparse point: " + current.FullName);
                if (string.Equals(current.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), stop, StringComparison.OrdinalIgnoreCase)) break;
            }
        }

        private static void RequireFile(string path, string label) { if (!File.Exists(path)) throw new FileNotFoundException("Missing " + label + ".", path); }
        private static void WriteNew(string path, byte[] bytes) { using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) stream.Write(bytes, 0, bytes.Length); }
        private static byte[] Utf8(string text) { return new UTF8Encoding(false, true).GetBytes(text); }
        private static string StrictUtf8(byte[] bytes) { return new UTF8Encoding(false, true).GetString(bytes); }
        private static string Serialize(JToken token) { return token.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n"; }
        private static string Sha256Canonical(byte[] bytes) { using (var sha = SHA256.Create()) return "sha256:" + string.Concat(sha.ComputeHash(bytes ?? Array.Empty<byte>()).Select(value => value.ToString("x2"))); }
        private static bool RawHash(string value) { return value != null && value.Length == 64 && value.All(character => (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')); }
        private static bool IsGuid(string value) { return value != null && value.Length == 32 && value.All(character => (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')); }
        private static bool Same(string a, string b) { return string.Equals(a, b, StringComparison.Ordinal); }
        private static string Describe(W24GateReport report) { return string.Join(" | ", report.Issues.Select(issue => issue.Code + " " + issue.Path + " " + issue.Message).ToArray()); }
    }
}
