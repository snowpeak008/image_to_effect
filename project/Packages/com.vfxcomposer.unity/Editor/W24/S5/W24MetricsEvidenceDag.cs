using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VFXComposer.W24;

namespace VFXComposer.Editor.W24.S5
{
    /// <summary>
    /// Controlled bridge for the non-cyclic W24 numeric-metrics DAG.  Python receives an
    /// immutable recorder-written input and writes only a private temp file; the recorder is
    /// still the sole writer to formal evidence storage.  This helper deliberately performs no
    /// visual judgement and is not an alternate capture API.
    /// </summary>
    public static class W24MetricsEvidenceDag
    {
        public static string WriteInput(W24ContinuousCaptureRecorder recorder, string evidenceRelativePath, JObject input, int expectedContractRevision, string expectedContractSha256, string expectedContractCaptureProfileSha256, string expectedToolSha256)
        {
            if (recorder == null || input == null) throw new ArgumentNullException(recorder == null ? "recorder" : "input");
            if (!CanonicalHash(expectedContractSha256) || !CanonicalHash(expectedContractCaptureProfileSha256) || !CanonicalHash(expectedToolSha256)) throw new ArgumentException("Expected contract/capture-profile/tool hashes must be canonical.");
            var clone = (JObject)input.DeepClone();
            if (!string.Equals((string)clone["schema"], "w24-render-metrics-input/v1", StringComparison.Ordinal)) throw new InvalidDataException("Metrics input schema must be w24-render-metrics-input/v1.");
            if (clone["captureMetadata"] != null) throw new InvalidDataException("Metrics DAG input must not depend on final capture metadata.");
            if (expectedContractRevision < 1 || !string.Equals((string)clone["candidateId"], recorder.CandidateId, StringComparison.Ordinal) || !string.Equals((string)clone["captureProfileSha256"], expectedContractCaptureProfileSha256, StringComparison.Ordinal) || !string.Equals((string)clone["recorderCaptureProfileSha256"], recorder.CaptureProfileSha256, StringComparison.Ordinal) || (int?)clone["contractRevision"] != expectedContractRevision || !string.Equals((string)clone["contractSha256"], expectedContractSha256, StringComparison.Ordinal)) throw new InvalidDataException("Metrics input must separately bind the active candidate, Contract capture profile, recorder-observed capture profile, and frozen Contract revision/hash.");
            clone["expectedToolSha256"] = expectedToolSha256;
            ValidateInputShape(clone);
            VerifyToolBundle((string)clone["captureToolBundlePath"], (string)clone["captureToolBundleSha256"], expectedToolSha256, null);
            return recorder.WriteMetricsInput(evidenceRelativePath, Utf8(CanonicalJson(clone)), expectedToolSha256, (string)clone.SelectToken("metricsEnvironment.environmentSha256"));
        }

        public static string RunAndWriteReport(W24ContinuousCaptureRecorder recorder, string evidenceRelativeInputPath, string inputFileSha256, string evidenceRelativeReportPath, string pythonExecutable, string metricsToolPath, string expectedToolSha256)
        {
            if (recorder == null) throw new ArgumentNullException("recorder");
            if (!CanonicalHash(inputFileSha256) || !CanonicalHash(expectedToolSha256)) throw new ArgumentException("Metrics input/tool hash must be canonical.");
            if (string.IsNullOrWhiteSpace(pythonExecutable) || string.IsNullOrWhiteSpace(metricsToolPath) || !File.Exists(Path.GetFullPath(pythonExecutable)) || !File.Exists(metricsToolPath)) throw new FileNotFoundException("Controlled Python executable or metrics tool is unavailable.");
            var inputPath = Path.Combine(recorder.EvidenceRoot, evidenceRelativeInputPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(inputPath) || !string.Equals(W24EvidenceStore.HashFile(inputPath), inputFileSha256, StringComparison.Ordinal)) throw new InvalidDataException("Recorder-written metrics input is missing or changed.");
            var inputBytes = File.ReadAllBytes(inputPath);
            var input = ParseStrict(new UTF8Encoding(false, true).GetString(inputBytes));
            var observedEnvironment = ProbeMetricsEnvironmentForInput(pythonExecutable);
            if (!string.Equals(CanonicalJson(observedEnvironment), CanonicalJson(input["metricsEnvironment"]), StringComparison.Ordinal)) throw new InvalidDataException("Observed Python/NumPy/Pillow environment differs from the frozen metrics input.");
            var frozenToolPath = VerifyToolBundle((string)input["captureToolBundlePath"], (string)input["captureToolBundleSha256"], expectedToolSha256, metricsToolPath);
            var analysisInputHash = Hash(CanonicalJson(input));
            var tempRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "w24-metrics-" + Guid.NewGuid().ToString("N")));
            var tempInput = Path.Combine(tempRoot, "metrics-input.json");
            var temp = Path.Combine(tempRoot, "metrics-report.json");
            try
            {
                Directory.CreateDirectory(tempRoot);
                File.WriteAllBytes(tempInput, inputBytes);
                File.SetAttributes(tempInput, File.GetAttributes(tempInput) | FileAttributes.ReadOnly);
                CopySealedEvidenceToTemp(recorder.EvidenceRoot, tempRoot, input);
                var info = new ProcessStartInfo { FileName = pythonExecutable, WorkingDirectory = tempRoot, Arguments = Quote(frozenToolPath) + " " + Quote(tempInput) + " --output " + Quote(temp), UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
                using (var process = Process.Start(info))
                {
                    if (process == null) throw new InvalidOperationException("Metrics CLI process could not be started.");
                    var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(120000))
                    {
                        try { process.Kill(); process.WaitForExit(); } catch (InvalidOperationException) { }
                        throw new TimeoutException("Metrics CLI exceeded the 120-second private-child timeout.");
                    }
                    Task.WaitAll(stdout, stderr);
                    if (process.ExitCode != 0) throw new InvalidOperationException("Metrics CLI failed: " + stderr.Result);
                }
                if (!File.Exists(temp)) throw new InvalidDataException("Metrics CLI did not produce its private temporary report.");
                if (!string.Equals(W24EvidenceStore.HashFile(inputPath), inputFileSha256, StringComparison.Ordinal)) throw new InvalidDataException("Metrics input changed while the external tool ran.");
                VerifyPrivateTempInputs(tempRoot, tempInput, inputBytes, input);
                var bytes = File.ReadAllBytes(temp); var report = ParseStrict(new UTF8Encoding(false, true).GetString(bytes));
                ValidateReport(report, input, analysisInputHash, expectedToolSha256);
                return recorder.WriteMetricsReport(evidenceRelativeReportPath, bytes, evidenceRelativeInputPath, inputFileSha256, analysisInputHash, expectedToolSha256);
            }
            finally { DeletePrivateTempTree(tempRoot); }
        }

        private static void ValidateInputShape(JObject input)
        {
            var entries = (input["evidence"] as JArray ?? new JArray()).OfType<JObject>().ToArray();
            var checks = (input["checks"] as JArray ?? new JArray()).OfType<JObject>().ToArray();
            var matrix = input["requiredEvidenceMatrix"] as JArray;
            var environment = input["metricsEnvironment"] as JObject; var environmentBody = environment == null ? null : (JObject)environment.DeepClone(); var environmentHash = environmentBody == null ? null : (string)environmentBody["environmentSha256"]; if (environmentBody != null) environmentBody.Remove("environmentSha256");
            if (entries.Length == 0 || checks.Length == 0 || matrix == null || matrix.Count == 0 || !SafeBundlePath((string)input["captureToolBundlePath"]) || !CanonicalHash((string)input["captureToolBundleSha256"]) || environmentBody == null || !Path.IsPathRooted((string)environmentBody["pythonExecutablePath"]) || !CanonicalHash((string)environmentBody["pythonExecutableSha256"]) || string.IsNullOrWhiteSpace((string)environmentBody["pythonVersion"]) || string.IsNullOrWhiteSpace((string)environmentBody["numpyVersion"]) || string.IsNullOrWhiteSpace((string)environmentBody["pillowVersion"]) || !string.Equals(environmentHash, Hash(CanonicalJson(environmentBody)), StringComparison.Ordinal) || !string.Equals((string)input["requiredEvidenceMatrixSha256"], Hash(CanonicalJson(matrix)), StringComparison.Ordinal)) throw new InvalidDataException("Metrics input needs raw typed evidence, a frozen executable/dependency environment, a hash-bound required matrix, checks, and the frozen capture-tool bundle identity.");
            var matrixKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in matrix.OfType<JObject>()) if (!Token((string)row["evidenceId"]) || !Token((string)row["passId"]) || (long?)row["seed"] < 0 || !Token((string)row["viewId"]) || (long?)row["logicalFrameIndex"] < 0 || !matrixKeys.Add((string)row["evidenceId"])) throw new InvalidDataException("Required evidence matrix has an invalid or duplicate evidenceId row.");
            var ids = entries.Select(item => (string)item["id"]).ToArray();
            if (ids.Any(string.IsNullOrEmpty) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length) throw new InvalidDataException("Metrics evidence IDs must be non-empty and unique.");
            var paths = entries.Select(item => (string)item["path"]).ToArray();
            if (paths.Distinct(StringComparer.Ordinal).Count() != paths.Length) throw new InvalidDataException("Metrics evidence paths must be unique.");
            var checkIds = checks.Select(item => (string)item["id"]).ToArray();
            if (checkIds.Any(string.IsNullOrEmpty) || checkIds.Distinct(StringComparer.Ordinal).Count() != checkIds.Length) throw new InvalidDataException("Metrics check IDs must be non-empty and unique.");
            foreach (var entry in entries)
            {
                if (!SafeDiagnosticPath((string)entry["path"]) || !CanonicalHash((string)entry["sha256"]) || !Token((string)entry["passId"]) || !Token((string)entry["encoding"]) || (long?)entry["seed"] < 0 || (long?)entry["logicalFrameIndex"] < 0 || (long?)entry["playerLoopSerial"] <= 0 || (long?)entry["playerLoopFrame"] < 0 || entry["playerLoopTime"] == null || !Token((string)entry["viewId"]) || string.IsNullOrWhiteSpace((string)entry["derivedFrom"])) throw new InvalidDataException("Metrics registry requires typed seed/view PlayerLoop provenance.");
            }
        }
        private static void ValidateReport(JObject report, JObject input, string inputHash, string toolHash)
        {
            if (!string.Equals((string)report["route"], "MEASURED", StringComparison.Ordinal) || (bool?)report["machineGatesPassed"] != true || !string.Equals((string)report["inputSha256"], inputHash, StringComparison.Ordinal) || !string.Equals((string)report["toolSha256"], toolHash, StringComparison.Ordinal)) throw new InvalidDataException("Metrics report route/input/tool identity is invalid.");
            var clone = (JObject)report.DeepClone(); var seal = (string)clone["sealedReportHash"]; clone.Remove("sealedReportHash");
            if (!string.Equals((string)clone["sealedReportEncoding"], W24TypedBinaryCanonicalEncoding.EncodingName, StringComparison.Ordinal) || !W24TypedBinaryCanonicalEncoding.Verify(seal, clone)) throw new InvalidDataException("Metrics report typed self-seal or encoding is invalid.");
            var frozen = (input["checks"] as JArray ?? new JArray()).OfType<JObject>().ToDictionary(check => (string)check["id"], check => (string)check["kind"], StringComparer.Ordinal);
            if (!(report["checks"] is JArray checks) || checks.Count == 0 || checks.OfType<JObject>().Any(check => (bool?)check["pass"] != true) || checks.OfType<JObject>().Select(check => (string)check["id"]).Distinct(StringComparer.Ordinal).Count() != checks.Count || checks.OfType<JObject>().Count(check => frozen.TryGetValue((string)check["id"], out var kind) && string.Equals(kind, (string)check["kind"], StringComparison.Ordinal)) != frozen.Count) throw new InvalidDataException("Metrics report checks must exactly match frozen input check IDs/kinds and all pass.");
        }
        private static JObject ParseStrict(string text)
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
            var obj = token as JObject;
            if (obj != null) foreach (var property in obj.Properties()) ValidateStrictUtf8(property.Name);
            var value = token as JValue;
            if (value != null && token.Type == JTokenType.String) ValidateStrictUtf8((string)value);
            foreach (var child in token.Children()) RejectLoneSurrogates(child);
        }
        private static void ValidateStrictUtf8(string value)
        {
            try { new UTF8Encoding(false, true).GetBytes(value); }
            catch (EncoderFallbackException error) { throw new InvalidDataException("JSON string/property name contains a lone surrogate.", error); }
        }
        private static string CanonicalJson(JToken value) { if (value is JObject obj) { var sorted = new JObject(); foreach (var property in obj.Properties().OrderBy(item => item.Name, StringComparer.Ordinal)) sorted.Add(property.Name, JToken.Parse(CanonicalJson(property.Value))); return sorted.ToString(Formatting.None); } if (value is JArray array) return new JArray(array.Select(item => JToken.Parse(CanonicalJson(item)))).ToString(Formatting.None); return value.ToString(Formatting.None); }
        private static string Hash(string value) { using (var sha = System.Security.Cryptography.SHA256.Create()) return "sha256:" + string.Concat(sha.ComputeHash(Utf8(value)).Select(item => item.ToString("x2"))); }
        private static byte[] Utf8(string text) { return new UTF8Encoding(false, true).GetBytes(text); }
        private static bool SafeDiagnosticPath(string value) { return !string.IsNullOrWhiteSpace(value) && value.StartsWith("diagnostics/", StringComparison.Ordinal) && !Path.IsPathRooted(value) && value.IndexOf('\\') < 0 && value.Split('/').All(part => !string.IsNullOrEmpty(part) && part != "." && part != ".."); }
        private static bool Token(string value) { return !string.IsNullOrEmpty(value) && value.All(character => char.IsLower(character) || char.IsDigit(character) || character == '-' || character == '_' || character == '.'); }
        private static bool CanonicalHash(string value) { return value != null && value.Length == 71 && value.StartsWith("sha256:", StringComparison.Ordinal) && value.Skip(7).All(character => (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')); }
        private static bool SafeBundlePath(string value) { return SafeRepositoryPath(value) && value.StartsWith("docs/vfx-contracts/capture-tools/", StringComparison.Ordinal) && value.EndsWith(".bundle.json", StringComparison.Ordinal); }
        private static bool SafeRepositoryPath(string value) { return !string.IsNullOrWhiteSpace(value) && !Path.IsPathRooted(value) && value.IndexOf('\\') < 0 && value.Split('/').All(part => !string.IsNullOrEmpty(part) && part != "." && part != ".."); }
        private static string RepositoryRoot() { return Directory.GetParent(Directory.GetParent(UnityEngine.Application.dataPath).FullName).FullName; }
        private static string ResolveRepositoryFile(string relative)
        {
            if (!SafeRepositoryPath(relative)) throw new InvalidDataException("Unsafe repository path in capture-tool bundle.");
            var root = Path.GetFullPath(RepositoryRoot()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var absolute = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(absolute) || HasReparsePoint(absolute, root)) throw new InvalidDataException("Capture-tool bundle source is missing, escaped, or reparse-backed: " + relative);
            return absolute;
        }
        private static bool HasReparsePoint(string path, string root)
        {
            for (var current = new FileInfo(path).Directory; current != null; current = current.Parent)
            {
                if ((current.Attributes & FileAttributes.ReparsePoint) != 0) return true;
                if (string.Equals(current.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) break;
            }
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        private static string VerifyToolBundle(string bundleRelative, string expectedBundleHash, string expectedToolHash, string suppliedToolPath)
        {
            if (!SafeBundlePath(bundleRelative) || !CanonicalHash(expectedBundleHash) || !CanonicalHash(expectedToolHash)) throw new InvalidDataException("Capture-tool bundle identity is invalid.");
            var bundlePath = ResolveRepositoryFile(bundleRelative); var bundle = ParseStrict(File.ReadAllText(bundlePath, new UTF8Encoding(false, true)));
            if (!string.Equals(Hash(CanonicalJson(bundle)), expectedBundleHash, StringComparison.Ordinal)) throw new InvalidDataException("Capture-tool bundle canonical hash differs from the frozen input/profile identity.");
            var sources = (bundle["sources"] as JArray ?? new JArray()).OfType<JObject>().ToArray(); var seen = new HashSet<string>(StringComparer.Ordinal); string metrics = null;
            foreach (var source in sources)
            {
                var relative = (string)source["path"]; var claimed = (string)source["sha256"];
                if (!seen.Add(relative ?? string.Empty) || !CanonicalHash(claimed)) throw new InvalidDataException("Capture-tool bundle contains a duplicate/invalid source.");
                var absolute = ResolveRepositoryFile(relative);
                if (!string.Equals(W24EvidenceStore.HashFile(absolute), claimed, StringComparison.Ordinal)) throw new InvalidDataException("Capture-tool bundle source drifted: " + relative);
                if (string.Equals(relative, "tools/vfx/metrics/render_metrics.py", StringComparison.Ordinal)) { if (metrics != null) throw new InvalidDataException("Capture-tool bundle repeats the metrics tool."); metrics = absolute; if (!string.Equals(claimed, expectedToolHash, StringComparison.Ordinal)) throw new InvalidDataException("Expected metrics tool hash is not the frozen bundle source hash."); }
            }
            if (metrics == null || (!string.IsNullOrEmpty(suppliedToolPath) && !string.Equals(Path.GetFullPath(suppliedToolPath), metrics, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Exactly the frozen bundle metrics tool must execute.");
            return metrics;
        }
        internal static string VerifyToolBundleForTests(string bundleRelative, string expectedBundleHash, string expectedToolHash, string suppliedToolPath) { return VerifyToolBundle(bundleRelative, expectedBundleHash, expectedToolHash, suppliedToolPath); }
        private static void CopySealedEvidenceToTemp(string evidenceRoot, string tempRoot, JObject input)
        {
            var root = Path.GetFullPath(evidenceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (var entry in (input["evidence"] as JArray ?? new JArray()).OfType<JObject>())
            {
                var relative = (string)entry["path"];
                if (!SafeDiagnosticPath(relative) || !string.Equals((string)entry["kind"], "diagnostic", StringComparison.Ordinal) || !Token((string)entry["passId"]) || !Token((string)entry["encoding"]) || !CanonicalHash((string)entry["sha256"])) throw new InvalidDataException("Invalid typed registry entry before isolated metrics execution.");
                var source = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
                if (!source.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(source) || HasReparsePoint(source, root) || !string.Equals(W24EvidenceStore.HashFile(source), (string)entry["sha256"], StringComparison.Ordinal)) throw new InvalidDataException("Typed raw evidence is missing, swapped, or reparse-backed: " + relative);
                var destination = Path.GetFullPath(Path.Combine(tempRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
                if (!destination.StartsWith(tempRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Temp evidence destination escaped its private root.");
                Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(source, destination, false); File.SetAttributes(destination, File.GetAttributes(destination) | FileAttributes.ReadOnly);
            }
        }
        private static void VerifyPrivateTempInputs(string tempRoot, string tempInput, byte[] expectedInputBytes, JObject input)
        {
            if (!File.Exists(tempInput) || HasReparsePoint(tempInput, tempRoot) || !File.ReadAllBytes(tempInput).SequenceEqual(expectedInputBytes)) throw new InvalidDataException("Private metrics input changed while the external tool ran.");
            var root = Path.GetFullPath(tempRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (var entry in (input["evidence"] as JArray ?? new JArray()).OfType<JObject>())
            {
                var relative = (string)entry["path"];
                if (!SafeDiagnosticPath(relative) || !CanonicalHash((string)entry["sha256"])) throw new InvalidDataException("Private raw registry identity is invalid after metrics execution.");
                var copy = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
                if (!copy.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(copy) || HasReparsePoint(copy, tempRoot) || !string.Equals(W24EvidenceStore.HashFile(copy), (string)entry["sha256"], StringComparison.Ordinal)) throw new InvalidDataException("Private typed raw evidence changed while the external tool ran: " + relative);
            }
        }
        /// <summary>
        /// Produces the only supported metricsEnvironment input object.  Producers must call
        /// this API rather than reflect private probe helpers or hand-author dependency claims.
        /// The returned object is detached and may be inserted directly into a metrics input.
        /// </summary>
        public static JObject ProbeMetricsEnvironmentForInput(string absolutePythonExecutable)
        {
            if (string.IsNullOrWhiteSpace(absolutePythonExecutable) || !Path.IsPathRooted(absolutePythonExecutable)) throw new ArgumentException("Python executable must be an absolute path.", "absolutePythonExecutable");
            var executable = Path.GetFullPath(absolutePythonExecutable);
            if (!File.Exists(executable) || HasAnyReparsePoint(executable)) throw new InvalidDataException("Python executable must be a real canonical file, not a link or reparse-backed path.");
            var pythonVersion = RunProbe(executable, "--version");
            var dependencyLines = RunProbe(executable, "-c " + Quote("import numpy, PIL; print(numpy.__version__); print(PIL.__version__)")).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (string.IsNullOrWhiteSpace(pythonVersion) || dependencyLines.Length != 2) throw new InvalidDataException("Could not identify the frozen Python/NumPy/Pillow environment.");
            var body = new JObject { ["pythonExecutablePath"] = executable.Replace('\\', '/'), ["pythonExecutableSha256"] = W24EvidenceStore.HashFile(executable), ["pythonVersion"] = pythonVersion.Trim(), ["numpyVersion"] = dependencyLines[0].Trim(), ["pillowVersion"] = dependencyLines[1].Trim() };
            body["environmentSha256"] = Hash(CanonicalJson(body)); return body;
        }
        private static bool HasAnyReparsePoint(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return true;
            for (var directory = new FileInfo(path).Directory; directory != null; directory = directory.Parent)
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) return true;
            return false;
        }
        private static string RunProbe(string executable, string arguments)
        {
            var info = new ProcessStartInfo { FileName = executable, Arguments = arguments, UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            using (var process = Process.Start(info)) { var stdout = process.StandardOutput.ReadToEnd(); var stderr = process.StandardError.ReadToEnd(); process.WaitForExit(); if (process.ExitCode != 0) throw new InvalidOperationException("Metrics environment probe failed: " + stderr); return string.IsNullOrWhiteSpace(stdout) ? stderr.Trim() : stdout.Trim(); }
        }
        private static void DeletePrivateTempTree(string tempRoot)
        {
            try
            {
                var systemTemp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var resolved = Path.GetFullPath(tempRoot);
                if (!resolved.StartsWith(systemTemp, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(resolved).StartsWith("w24-metrics-", StringComparison.Ordinal) == false) return;
                if (!Directory.Exists(resolved)) return;
                foreach (var file in Directory.EnumerateFiles(resolved, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(resolved, true);
            }
            catch { }
        }
        private static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }
    }
}
