using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VFXComposer.Editor.W24.S5
{
    /// <summary>
    /// The only adapter from a sealed graphics-recorder directory to the S5 C0 evidence seal.
    /// It copies no image bytes and makes no visual/QA/user decision: it verifies the recorder's
    /// hash index, writes a deterministic formal metadata projection once, then delegates the
    /// identity and completed-trace checks to the gate-owned transition.
    /// </summary>
    public static class W24S5RecorderCaptureCompletion
    {
        private const string ArtifactRoot = "artifacts/vfx-evidence/";
        private const string CandidateRoot = "docs/vfx-candidates/";
        private const string FormalMetadataName = "formal-capture-metadata.json";

        internal static W24S5FormalEvidenceTransitionResult Finalize(string effectId, string completedTraceJson)
        {
            var result = new W24S5FormalEvidenceTransitionResult();
            if (!EffectId(effectId)) { result.Error("effectId must be stable lower_snake_case."); return result; }
            if (string.IsNullOrWhiteSpace(completedTraceJson)) { result.Error("A machine-produced completed Trace is required; capture alone cannot claim a verdict."); return result; }

            var metadataPath = ArtifactRoot + effectId + "/C0/bound/" + FormalMetadataName;
            byte[] metadataBytes;
            try { metadataBytes = BuildFormalMetadata(effectId); }
            catch (Exception e)
            {
                result.Error("Sealed recorder evidence could not be bound: " + e.Message);
                return result;
            }

            var metadataAbsolute = RepositoryAbsolute(metadataPath);
            var metadataDirectory = Path.GetDirectoryName(metadataAbsolute);
            var wroteMetadata = false;
            try
            {
                RejectReparsePoints(metadataDirectory, RepositoryRoot());
                Directory.CreateDirectory(metadataDirectory);
                using (var stream = new FileStream(metadataAbsolute, FileMode.CreateNew, FileAccess.Write, FileShare.None)) stream.Write(metadataBytes, 0, metadataBytes.Length);
                wroteMetadata = true;
                var receiptPath = CandidateRoot + effectId + "/C0/candidate-receipt.json";
                var transition = W24S5ProductionGate.FinalizeC0Evidence(new W24S5FormalEvidenceTransitionRequest
                {
                    EffectId = effectId,
                    CandidateReceiptPath = receiptPath,
                    CandidateReceiptFileHash = HashFile(RepositoryAbsolute(receiptPath)),
                    CaptureMetadataPath = metadataPath,
                    CaptureMetadataFileHash = Hash(metadataBytes),
                    CompletedTraceJson = completedTraceJson
                });
                if (transition.Succeeded) return transition;

                // The adapter owns this one derived file.  A rejected replay/trace must not
                // leave a fake "completed" metadata object beside genuine capture evidence.
                DeleteOwnedMetadata(metadataAbsolute, metadataDirectory);
                return transition;
            }
            catch (Exception e)
            {
                if (wroteMetadata) DeleteOwnedMetadata(metadataAbsolute, metadataDirectory);
                result.Error("Formal C0 capture completion failed without committing an evidence seal: " + e.Message);
                return result;
            }
        }

        // Zero-argument batch/CI entry points (`-executeMethod`), deliberately not GUI menu
        // actions. The capture job writes its machine Trace before sealing; these methods then
        // perform only the post-capture C0 evidence binding, never QA or user signing.
        // Unity ignores an -executeMethod return value; returning the sealed Trace path lets
        // non-GUI callers verify exactly what was bound while failure remains an exception.
        public static string FinalizeSustainedFlameC0Capture()
        {
            VerifySustainedFlameFormalCaptureShape();
            return FinalizeFromMachineTrace("sustained_flame_3d");
        }
        public static string FinalizeS3MovingProjectileC0Capture() { return FinalizeFromMachineTrace("w24_moving_projectile_trail"); }
        public static string FinalizeS3WeaponSocketFragmentsC0Capture() { return FinalizeFromMachineTrace("w24_weapon_socket_fragments"); }
        public static string FinalizeS3RealLightReceiversC0Capture() { return FinalizeFromMachineTrace("w24_real_light_receivers"); }

        private static string FinalizeFromMachineTrace(string effectId)
        {
            var tracePath = ArtifactRoot + effectId + "/C0/diagnostics/machine-gate-trace.json";
            var trace = ReadJson(tracePath).ToString(Formatting.None);
            var result = Finalize(effectId, trace);
            if (!result.Succeeded) throw new InvalidOperationException("Formal C0 evidence binding was rejected: " + string.Join(" | ", result.Errors));
            return result.TracePath;
        }

        // S0b's formal fixture is stricter than generic capture binding: it has one frozen
        // command, three seed/exit branches, and an exact retained-frame matrix.  This preflight
        // makes the public S5 entry point fail closed even when invoked outside the PlayMode test.
        private static void VerifySustainedFlameFormalCaptureShape()
        {
            const string effectId = "sustained_flame_3d";
            var root = ArtifactRoot + effectId + "/C0/";
            var candidateReceiptPath = CandidateRoot + effectId + "/C0/candidate-receipt.json";
            var candidateReceiptHash = HashFile(RepositoryAbsolute(candidateReceiptPath));
            var statusPath = ArtifactRoot + effectId + "/C0.capture-attempt-status." + candidateReceiptHash.Substring("sha256:".Length) + ".json";
            if (File.Exists(RepositoryAbsolute(statusPath))) throw new InvalidDataException("S0b capture has a machine-readable incomplete/finalization failure marker.");
            var metadata = ReadJson(root + "capture-metadata.json");
            var seal = ReadJson(root + "evidence-seal.json");
            var command = ReadJson(root + "diagnostics/operator-command.json");
            var telemetry = ReadJson(root + "diagnostics/semantic-telemetry.json");
            var trace = ReadJson(root + "diagnostics/machine-gate-trace.json");
            var requiredFrames = new HashSet<int> { 1, 21, 60, 120, 180, 240, 270, 291, 293, 321, 366 };
            var requiredSeeds = new HashSet<long> { 24001, 24011, 24021 };
            var frames = (metadata["frames"] as JArray ?? new JArray()).OfType<JObject>().ToArray();
            if (frames.Length != requiredFrames.Count * requiredSeeds.Count || frames.Any(item => !requiredSeeds.Contains((long?)item["seed"] ?? -1L) || !requiredFrames.Contains((int?)item["frameIndex"] ?? -1)) || frames.Select(item => ((long)item["seed"]).ToString() + ":" + ((int)item["frameIndex"]).ToString()).Distinct(StringComparer.Ordinal).Count() != frames.Length)
                throw new InvalidDataException("S0b formal capture must contain exactly the frozen 3 x retained-frame matrix.");
            var provenance = seal["provenance"] as JObject;
            if (!Same((string)provenance?["operatorCommandHash"], HashFile(RepositoryAbsolute(root + "diagnostics/operator-command.json")))) throw new InvalidDataException("S0b seal does not bind the exact frozen operator command.");
            if (!Same((string)command["schema"], "w24-s0b-formal-operator-command/v1") || !Same((string)command["effectId"], effectId) || !Same((string)command["candidateId"], "C0") || ((JArray)command["seeds"] ?? new JArray()).Select(value => (long)value).OrderBy(value => value).SequenceEqual(requiredSeeds.OrderBy(value => value)) == false || !((JArray)command["retainedFrameIndices"] ?? new JArray()).Select(value => (int)value).OrderBy(value => value).SequenceEqual(requiredFrames.OrderBy(value => value)))
                throw new InvalidDataException("S0b frozen operator command differs from the required seed/frame contract.");
            var branches = (telemetry["branches"] as JArray ?? new JArray()).OfType<JObject>().ToArray();
            var expectedExits = new Dictionary<long, string> { { 24001, "stop" }, { 24011, "stop" }, { 24021, "interrupt" } };
            if (!Same((string)telemetry["captureCompleteness"], "complete") || branches.Length != 3 || branches.Any(item => !SustainedFlameBranchComplete(item, requiredFrames.Count)) || branches.Select(item => (long?)item["seed"] ?? -1L).Distinct().Count() != 3 || branches.Any(item => !expectedExits.TryGetValue((long?)item["seed"] ?? -1L, out var expectedExit) || !Same((string)item["exit"], expectedExit)))
                throw new InvalidDataException("S0b semantic telemetry does not prove all stop/interrupt cleanup branches.");

            // Verify the same rich projection which is persisted for the later formal-gate
            // replay.  This makes the public completion command reject provenance or semantic
            // omissions before it can write a transition receipt.
            var candidateContractPath = CandidateRoot + effectId + "/C0/design-contract.json";
            VFXComposer.Editor.W24.S1.VfxDesignContract candidateContract;
            var contractReport = VFXComposer.Editor.W24.S1.VfxDesignContractJson.ValidateJson(File.ReadAllText(RepositoryAbsolute(candidateContractPath), new UTF8Encoding(false, true)), out candidateContract);
            if (contractReport.HasErrors || candidateContract == null) throw new InvalidDataException("S0b candidate Contract cannot be used to revalidate formal capture evidence.");
            var replayGate = new W24S5ProductionGateResult();
            var projection = JObject.Parse(Encoding.UTF8.GetString(BuildFormalMetadata(effectId)), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (!W24S5EvidenceTransition.VerifySustainedFlameFormalMetadata(replayGate, projection, candidateContract) || replayGate.HasErrors)
                throw new InvalidDataException("S0b formal evidence projection is incomplete: " + string.Join(" | ", replayGate.Issues.Where(item => item.IsError).Select(item => item.Code + " " + item.Message)));
            foreach (var requirement in (trace["requirementTraces"] as JArray ?? new JArray()).OfType<JObject>())
            {
                var authority = (string)requirement["evidenceAuthority"];
                var evidence = (requirement["authorityEvidence"] as JArray ?? new JArray()).OfType<JObject>().SingleOrDefault();
                if (authority == "visualQa" || authority == "user")
                {
                    if (evidence == null || (bool?)evidence["passed"] != false || !((string)evidence["reference"] ?? string.Empty).StartsWith("pending:", StringComparison.Ordinal)) throw new InvalidDataException("S0b finalization must preserve pending Visual QA/user authority.");
                }
                else if (evidence == null || (bool?)evidence["passed"] != true) throw new InvalidDataException("S0b finalization rejects unmeasured or failed machine requirement evidence.");
            }
        }

        private static bool SustainedFlameBranchComplete(JObject branch, int retainedFrameCount)
        {
            var final = branch["final"] as JObject;
            return (int?)branch["observedFrames"] == 366 && (int?)branch["retainedFrames"] == retainedFrameCount && (bool?)branch["sawRequestedExit"] == true && (bool?)branch["sawExitCarrier"] == true && final != null && Same((string)final["state"], "idle") && (bool?)final["cleanupComplete"] == true && (int?)final["enabledLightCount"] == 0;
        }

        private static byte[] BuildFormalMetadata(string effectId)
        {
            var captureRoot = ArtifactRoot + effectId + "/C0";
            var rawMetadataPath = captureRoot + "/capture-metadata.json";
            var sealPath = captureRoot + "/evidence-seal.json";
            var receiptPath = CandidateRoot + effectId + "/C0/candidate-receipt.json";
            var snapshotPath = CandidateRoot + effectId + "/C0/bootstrap-manifest.json";
            var contractPath = CandidateRoot + effectId + "/C0/design-contract.json";
            var rawMetadata = ReadJson(rawMetadataPath);
            var seal = ReadJson(sealPath);
            var receipt = ReadJson(receiptPath);
            var snapshot = ReadJson(snapshotPath);
            var contract = ReadJson(contractPath);
            var rawMetadataHash = HashFile(RepositoryAbsolute(rawMetadataPath));

            if (!Same((string)rawMetadata["schema"], "w24-s0a-capture-evidence/v1") || !Same((string)rawMetadata["candidateId"], "C0") || rawMetadata["executedInBatchMode"] == null || !(bool)rawMetadata["executedInBatchMode"])
                throw new InvalidDataException("Recorder metadata is not a completed graphics-backed C0 capture.");
            if (!Same((string)seal["schema"], "w24-s0a-final-evidence-seal/v1") || !Same((string)seal["candidateId"], "C0") || !Same((string)seal["captureProfileSha256"], (string)rawMetadata["captureProfileSha256"]))
                throw new InvalidDataException("Recorder final seal does not bind the C0 metadata/profile.");
            var recorderCaptureProfile = rawMetadata["captureProfile"] as JObject;
            var recorderCaptureProfileHash = (string)rawMetadata["captureProfileSha256"];
            // W24CaptureProfile.Sha256 intentionally hashes its frozen field-order serialization,
            // not the Contract canonicalizer's recursively sorted object form.
            var recomputedRecorderCaptureProfileHash = recorderCaptureProfile == null ? null : Hash(new UTF8Encoding(false, true).GetBytes(recorderCaptureProfile.ToString(Formatting.None)));
            if (!Canonical(recorderCaptureProfileHash) || !Same(recorderCaptureProfileHash, recomputedRecorderCaptureProfileHash))
                throw new InvalidDataException("Recorder capture-profile hash does not match its sealed canonical profile bytes.");
            var provenance = seal["provenance"] as JObject;
            if (!Same((string)provenance?["captureMetadataSha256"], rawMetadataHash)) throw new InvalidDataException("Recorder final seal does not bind the exact metadata bytes.");

            var sealedFiles = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in (seal["artifacts"] as JArray ?? new JArray()).OfType<JObject>())
            {
                var file = (string)item["file"]; var hash = (string)item["sha256"];
                if (!SafeLocal(file) || !Canonical(hash) || sealedFiles.ContainsKey(file)) throw new InvalidDataException("Recorder final seal has an unsafe or duplicate artifact.");
                sealedFiles.Add(file, hash);
                if (!Same(HashFile(RepositoryAbsolute(captureRoot + "/" + file)), hash)) throw new InvalidDataException("Recorder artifact bytes drifted after sealing: " + file);
            }
            string sealedMetadataHash;
            if (!Same(sealedFiles.TryGetValue("capture-metadata.json", out sealedMetadataHash) ? sealedMetadataHash : null, rawMetadataHash)) throw new InvalidDataException("Recorder metadata is absent from its final seal.");

            var manifest = snapshot;
            var runtime = manifest["runtimeEntry"] as JObject;
            if (!Same((string)receipt["effectId"], effectId) || !Same((string)receipt["candidateId"], "C0") || !Same((string)receipt["bootstrapManifestSnapshotFileHash"], HashFile(RepositoryAbsolute(snapshotPath))) || runtime == null)
                throw new InvalidDataException("C0 receipt/snapshot identity is invalid.");
            var source = rawMetadata["sourceHashes"] as JObject;
            var sourceManifest = source == null ? null : source["manifest"] as JObject;
            var sourcePrefab = source == null ? null : source["prefab"] as JObject;
            var sourceScene = source == null ? null : source["scene"] as JObject;
            var sourceTool = source == null ? null : source["captureTool"] as JObject;
            var capture = contract["captureProfile"] as JObject;
            var runtimeOwnedOutput = (receipt["ownedOutputs"] as JArray ?? new JArray()).OfType<JObject>().FirstOrDefault(item => Same((string)item["path"], (string)runtime["path"]));
            if (sourceManifest == null || !SamePath((string)sourceManifest["path"], ProjectAbsolute((string)receipt["productionManifestPath"])) || !Same((string)sourceManifest["sha256"], HashFile(RepositoryAbsolute(snapshotPath))) || !Same((string)sourceManifest["buildHash"], "sha256:" + (string)manifest["buildHash"]))
                throw new InvalidDataException("Recorder manifest/build identity differs from frozen C0.");
            if (sourcePrefab == null || runtimeOwnedOutput == null || !SamePath((string)sourcePrefab["path"], ProjectAbsolute((string)runtime["path"])) || !Same((string)sourcePrefab["guid"], (string)runtime["guid"]) || !Same((string)sourcePrefab["sha256"], "sha256:" + (string)runtimeOwnedOutput["sha256"])) throw new InvalidDataException("Recorder Runtime Entry/owned-output identity differs from frozen C0.");
            if (sourceScene == null || capture == null || !SamePath((string)sourceScene["path"], ProjectAbsolute((string)capture["sceneSerializedReference"])) || !Same((string)sourceScene["sha256"], (string)capture["sceneHash"])) throw new InvalidDataException("Recorder scene identity differs from frozen C0 Contract.");
            if (sourceTool == null || !Same((string)sourceTool["version"], (string)capture["captureToolVersion"]) || !Same((string)sourceTool["sha256"], (string)capture["captureToolHash"])) throw new InvalidDataException("Recorder capture tool differs from frozen C0 Contract.");

            var artifacts = new List<JObject>();
            AddArtifact(artifacts, sealedFiles, captureRoot, "capture-metadata.json", rawMetadataHash, "capture-metadata");
            var diagnosticManifest = rawMetadata["diagnosticPassManifest"] as JObject;
            AddArtifact(artifacts, sealedFiles, captureRoot, (string)diagnosticManifest?["file"], (string)diagnosticManifest?["sha256"], "diagnostic-pass-manifest");
            foreach (var frame in (rawMetadata["frames"] as JArray ?? new JArray()).OfType<JObject>())
            {
                var beauty = frame["beauty"] as JObject;
                AddArtifact(artifacts, sealedFiles, captureRoot, (string)beauty?["file"], (string)beauty?["sha256"], "beauty");
                foreach (var diagnostic in (frame["diagnostics"] as JArray ?? new JArray()).OfType<JObject>()) AddArtifact(artifacts, sealedFiles, captureRoot, (string)diagnostic["file"], (string)diagnostic["sha256"], "diagnostic");
            }
            foreach (var item in (rawMetadata["semanticTelemetry"] as JArray ?? new JArray()).OfType<JObject>()) AddArtifact(artifacts, sealedFiles, captureRoot, (string)item["file"], (string)item["sha256"], "telemetry");
            foreach (var item in (rawMetadata["supplementalDiagnostics"] as JArray ?? new JArray()).OfType<JObject>()) AddArtifact(artifacts, sealedFiles, captureRoot, (string)item["file"], (string)item["sha256"], ArtifactKind((string)item["kind"]));
            foreach (var item in (rawMetadata["typedRawDiagnostics"] as JArray ?? new JArray()).OfType<JObject>()) AddArtifact(artifacts, sealedFiles, captureRoot, (string)item["file"], (string)item["sha256"], "diagnostic", (string)item["passId"], (string)item["encoding"]);
            foreach (var item in (rawMetadata["metricInputs"] as JArray ?? new JArray()).OfType<JObject>()) AddArtifact(artifacts, sealedFiles, captureRoot, (string)item["file"], (string)item["sha256"], "metrics-input");
            foreach (var item in (rawMetadata["metricReports"] as JArray ?? new JArray()).OfType<JObject>()) AddArtifact(artifacts, sealedFiles, captureRoot, (string)item["file"], (string)item["sha256"], "diagnostic", (string)item["passId"], (string)item["encoding"]);
            if (artifacts.Count == 0) throw new InvalidDataException("Recorder metadata declares no capture artifacts.");

            var formal = new JObject
            {
                ["schemaVersion"] = "w24-capture-metadata-v1", ["effectId"] = effectId,
                ["productionManifestPath"] = (string)receipt["productionManifestPath"], ["productionManifestFileHash"] = HashFile(RepositoryAbsolute(snapshotPath)),
                ["buildHash"] = "sha256:" + (string)manifest["buildHash"], ["runtimeEntryPath"] = (string)runtime["path"], ["runtimeEntryGuid"] = (string)runtime["guid"],
                ["ownedOutputs"] = receipt["ownedOutputs"]?.DeepClone(), ["artifacts"] = new JArray(artifacts),
                ["diagnosticPassManifest"] = rawMetadata["diagnosticPassManifest"]?.DeepClone(),
                ["recorderCaptureProfile"] = recorderCaptureProfile.DeepClone(), ["recorderCaptureProfileSha256"] = recorderCaptureProfileHash,
                ["typedRawDiagnostics"] = rawMetadata["typedRawDiagnostics"]?.DeepClone() ?? new JArray(),
                ["metricInputs"] = rawMetadata["metricInputs"]?.DeepClone() ?? new JArray(),
                ["metricReports"] = rawMetadata["metricReports"]?.DeepClone() ?? new JArray()
            };
            // S0b has a deliberately stricter replay contract than the generic recorder:
            // retain the sealed semantic and observed-diagnostic records verbatim so S5 can
            // revalidate lifecycle facts and receiver A/B token provenance after transition.
            // Do not fold these into typedRawDiagnostics: S0b intentionally has no typed DAG.
            if (Same(effectId, "sustained_flame_3d"))
            {
                formal["s0bFormalEvidence"] = new JObject
                {
                    ["schema"] = "w24-s0b-formal-evidence-projection/v1",
                    ["captureProfile"] = rawMetadata["captureProfile"]?.DeepClone(),
                    ["formalPlayerLoop"] = rawMetadata["formalPlayerLoop"]?.DeepClone(),
                    ["frames"] = rawMetadata["frames"]?.DeepClone() ?? new JArray(),
                    ["semanticTelemetry"] = rawMetadata["semanticTelemetry"]?.DeepClone() ?? new JArray(),
                    ["supplementalDiagnostics"] = rawMetadata["supplementalDiagnostics"]?.DeepClone() ?? new JArray(),
                    ["recorderProvenance"] = seal["provenance"]?.DeepClone()
                };
            }
            return new UTF8Encoding(false, true).GetBytes(formal.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n");
        }

        private static void AddArtifact(List<JObject> target, Dictionary<string, string> sealedFiles, string root, string file, string hash, string kind, string passId = null, string encoding = null)
        {
            string sealedHash;
            if (!SafeLocal(file) || string.IsNullOrWhiteSpace(kind) || !Canonical(hash) || !Same(sealedFiles.TryGetValue(file, out sealedHash) ? sealedHash : null, hash)) throw new InvalidDataException("Recorder metadata refers to an artifact not present in the final seal.");
            var path = root + "/" + file;
            if (target.Any(item => Same((string)item["path"], path))) throw new InvalidDataException("Recorder metadata repeats a capture artifact: " + file);
            var artifact = new JObject { ["path"] = path, ["sha256"] = hash, ["kind"] = kind };
            if (passId != null) artifact["passId"] = passId;
            if (encoding != null) artifact["encoding"] = encoding;
            target.Add(artifact);
        }
        private static string ArtifactKind(string recorderKind) { return !string.IsNullOrEmpty(recorderKind) && recorderKind.IndexOf("telemetry", StringComparison.OrdinalIgnoreCase) >= 0 ? "telemetry" : "diagnostic"; }

        private static JObject ReadJson(string path)
        {
            var absolute = RepositoryAbsolute(path);
            if (!File.Exists(absolute)) throw new FileNotFoundException("Required formal capture input is missing.", absolute);
            return JObject.Parse(File.ReadAllText(absolute, new UTF8Encoding(false, true)), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        }
        private static void DeleteOwnedMetadata(string path, string directory)
        {
            try { if (File.Exists(path)) File.Delete(path); if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory); }
            catch { /* Preserve the original gate failure; a failed cleanup is never a success. */ }
        }
        private static bool SafeLocal(string path) { return !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) && path.IndexOf('\\') < 0 && path.Split('/').All(part => !string.IsNullOrEmpty(part) && part != "." && part != ".."); }
        private static bool EffectId(string value) { return !string.IsNullOrEmpty(value) && char.IsLower(value[0]) && value.All(c => char.IsLower(c) || char.IsDigit(c) || c == '_') && !value.Contains("__") && value[value.Length - 1] != '_'; }
        private static bool Canonical(string hash) { return hash != null && hash.Length == 71 && hash.StartsWith("sha256:", StringComparison.Ordinal) && hash.Skip(7).All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')); }
        private static bool Same(string a, string b) { return string.Equals(a, b, StringComparison.Ordinal); }
        private static string RepositoryRoot() { return Directory.GetParent(Directory.GetParent(UnityEngine.Application.dataPath).FullName).FullName; }
        private static string RepositoryAbsolute(string path)
        {
            if (!SafeLocal(path)) throw new InvalidDataException("Unsafe repository-relative path.");
            var root = RepositoryRoot(); var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var absolute = Path.GetFullPath(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
            if (!absolute.StartsWith(prefix, StringComparison.Ordinal) || HasReparsePoint(absolute, root)) throw new InvalidDataException("Repository path escapes its formal evidence root.");
            return absolute;
        }
        private static string ProjectAbsolute(string path)
        {
            if (!SafeLocal(path)) throw new InvalidDataException("Unsafe project-relative source path.");
            var root = Path.Combine(RepositoryRoot(), "project"); var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var absolute = Path.GetFullPath(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
            if (!absolute.StartsWith(prefix, StringComparison.Ordinal) || HasReparsePoint(absolute, root)) throw new InvalidDataException("Project source path escapes the Unity project.");
            return absolute;
        }
        private static void RejectReparsePoints(string path, string boundary) { if (HasReparsePoint(path, boundary)) throw new InvalidDataException("Formal evidence path contains a reparse point."); }
        private static bool HasReparsePoint(string path, string boundary)
        {
            var stop = Path.GetFullPath(boundary).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            for (var current = new DirectoryInfo(Path.GetFullPath(path)); current != null; current = current.Parent)
            {
                if ((File.Exists(current.FullName) || Directory.Exists(current.FullName)) && (File.GetAttributes(current.FullName) & FileAttributes.ReparsePoint) != 0) return true;
                if (Same(current.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), stop)) break;
            }
            return false;
        }
        private static bool SamePath(string actual, string expected) { try { return !string.IsNullOrWhiteSpace(actual) && Same(Path.GetFullPath(actual), expected); } catch (Exception) { return false; } }
        private static string HashFile(string path) { using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return "sha256:" + string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2"))); }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return "sha256:" + string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2"))); }
    }
}
