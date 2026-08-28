using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace VFXComposer.Editor.W24.Workflow
{
    public sealed class W24StatusRegistration
    {
        public string EffectId { get; internal set; }
        public string PrefabPath { get; internal set; }
        public string MachineReportPath { get; internal set; }
        public string RuntimeEntryGuid { get; internal set; }
        public string RuntimeEntryHash { get; internal set; }
        public string BuildHash { get; internal set; }
        public bool HasMachineReport { get; internal set; }
        public bool HasRuntimeEntry { get; internal set; }
        public bool RuntimeEntryPathIsValid { get; internal set; }
        public bool RuntimeEntryExists { get; internal set; }
        public bool RuntimeEntryGuidIsVerifiable { get; internal set; }
        public bool RuntimeEntryHashIsVerifiable { get; internal set; }
        public bool HasW24VisualQa { get; internal set; }
        public W24MaturityLevel Maturity { get; internal set; }
        public W24WorkingStatus WorkingStatus { get; internal set; }
        public string Basis { get; internal set; }
    }

    public sealed class W24StatusSnapshot
    {
        public W24StatusSnapshot(IList<W24StatusRegistration> entries)
        {
            Entries = entries.OrderBy(entry => entry.EffectId, StringComparer.Ordinal).ToArray();
            FreezeHash = W24StatusRegistry.ComputeFreezeHash(Entries);
        }

        public IReadOnlyList<W24StatusRegistration> Entries { get; private set; }
        public string FreezeHash { get; private set; }
    }

    /// <summary>Filesystem-only S0a inventory. It trusts a manifest only after its runtime entry is independently verified.</summary>
    public static class W24StatusRegistry
    {
        public const string GeneratedRelativePath = "Assets/VFX/Generated";
        public const string BuildManifestRelativePath = "ProjectSettings/VFXComposer/BuildManifests";
        public const string FreezeSchema = "W24-S0A-PROVISIONAL-STATUS-V2";

        public static W24StatusSnapshot ScanProject(string projectRoot)
        {
            if (string.IsNullOrEmpty(projectRoot)) throw new ArgumentException("Project root is required.", nameof(projectRoot));
            return ScanDirectories(projectRoot, Path.Combine(projectRoot, GeneratedRelativePath), Path.Combine(projectRoot, BuildManifestRelativePath));
        }

        public static W24StatusSnapshot ScanDirectories(string projectRoot, string generatedDirectory, string manifestDirectory)
        {
            if (string.IsNullOrEmpty(projectRoot)) throw new ArgumentException("Project root is required.", nameof(projectRoot));
            if (!Directory.Exists(generatedDirectory)) throw new DirectoryNotFoundException(generatedDirectory);
            var entries = new List<W24StatusRegistration>();
            foreach (var directory in Directory.GetDirectories(generatedDirectory).OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal))
            {
                var effectId = Path.GetFileName(directory);
                entries.Add(Register(projectRoot, effectId, Path.Combine(manifestDirectory, effectId + ".manifest.json")));
            }
            return new W24StatusSnapshot(entries);
        }

        public static string ComputeFreezeHash(IEnumerable<W24StatusRegistration> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            var builder = new StringBuilder(FreezeSchema + "\n");
            foreach (var entry in entries.OrderBy(value => value.EffectId, StringComparer.Ordinal))
            {
                builder.Append(entry.EffectId).Append('|')
                    .Append(entry.PrefabPath ?? string.Empty).Append('|')
                    .Append(entry.HasMachineReport ? '1' : '0').Append('|')
                    .Append(entry.HasRuntimeEntry ? '1' : '0').Append('|')
                    .Append(entry.RuntimeEntryPathIsValid ? '1' : '0').Append('|')
                    .Append(entry.RuntimeEntryExists ? '1' : '0').Append('|')
                    .Append(entry.RuntimeEntryGuidIsVerifiable ? '1' : '0').Append('|')
                    .Append(entry.RuntimeEntryHashIsVerifiable ? '1' : '0').Append('|')
                    .Append(entry.RuntimeEntryGuid ?? string.Empty).Append('|')
                    .Append(entry.RuntimeEntryHash ?? string.Empty).Append('|')
                    .Append(entry.BuildHash ?? string.Empty).Append('|')
                    .Append(entry.Maturity).Append('|').Append(entry.WorkingStatus).Append('\n');
            }
            return CanonicalSha256(Encoding.UTF8.GetBytes(builder.ToString()));
        }

        public static bool IsCanonicalSha256(string value)
        {
            if (value == null || value.Length != 71 || !value.StartsWith("sha256:", StringComparison.Ordinal)) return false;
            for (var index = 7; index < value.Length; index++)
            {
                var character = value[index];
                if (!(character >= '0' && character <= '9') && !(character >= 'a' && character <= 'f')) return false;
            }
            return true;
        }

        private static W24StatusRegistration Register(string projectRoot, string effectId, string manifestPath)
        {
            var entry = new W24StatusRegistration { EffectId = effectId, MachineReportPath = Normalize(manifestPath), HasW24VisualQa = false };
            if (!File.Exists(manifestPath)) return Finalize(entry, "Missing pre-W24 BuildManifest; no visual conclusion is asserted.");

            entry.HasMachineReport = true;
            var manifest = ParseManifest(manifestPath);
            if (manifest == null || !string.Equals(manifest.effectId, effectId, StringComparison.Ordinal))
                return Finalize(entry, "BuildManifest is unreadable or its effectId does not match the Generated directory; no visual conclusion is asserted.");
            if (manifest.runtimeEntry == null || !string.Equals(manifest.runtimeEntry.kind, "prefab", StringComparison.Ordinal) || string.IsNullOrEmpty(manifest.runtimeEntry.path))
                return Finalize(entry, "BuildManifest has no prefab runtimeEntry; no visual conclusion is asserted.");

            entry.HasRuntimeEntry = true;
            string runtimeEntryAbsolutePath;
            entry.RuntimeEntryPathIsValid = TryResolveProjectPath(projectRoot, manifest.runtimeEntry.path, out runtimeEntryAbsolutePath);
            entry.PrefabPath = Normalize(manifest.runtimeEntry.path);
            entry.BuildHash = ToCanonicalSha256(manifest.buildHash);
            entry.RuntimeEntryGuid = NormalizeGuid(manifest.runtimeEntry.guid);
            var ownedOutput = manifest.ownedOutputs == null ? null : manifest.ownedOutputs.FirstOrDefault(output => output != null && string.Equals(output.path, manifest.runtimeEntry.path, StringComparison.Ordinal));
            entry.RuntimeEntryHash = ownedOutput == null ? null : ToCanonicalSha256(ownedOutput.sha256);
            if (!entry.RuntimeEntryPathIsValid)
                return Finalize(entry, "BuildManifest runtimeEntry path escapes or is not rooted at this project; no visual conclusion is asserted.");

            entry.RuntimeEntryExists = File.Exists(runtimeEntryAbsolutePath);
            entry.RuntimeEntryGuidIsVerifiable = entry.RuntimeEntryExists && IsGuid(entry.RuntimeEntryGuid) && GuidMatches(runtimeEntryAbsolutePath, entry.RuntimeEntryGuid) && ownedOutput != null && string.Equals(entry.RuntimeEntryGuid, NormalizeGuid(ownedOutput.guid), StringComparison.Ordinal);
            entry.RuntimeEntryHashIsVerifiable = entry.RuntimeEntryExists && ownedOutput != null && IsCanonicalSha256(entry.RuntimeEntryHash) && string.Equals(entry.RuntimeEntryHash, FileSha256(runtimeEntryAbsolutePath), StringComparison.Ordinal);
            if (!IsCanonicalSha256(entry.BuildHash))
                return Finalize(entry, "BuildManifest buildHash is not a SHA-256 value; no visual conclusion is asserted.");
            if (!entry.RuntimeEntryExists || !entry.RuntimeEntryGuidIsVerifiable || !entry.RuntimeEntryHashIsVerifiable)
                return Finalize(entry, "BuildManifest runtimeEntry failed existence, GUID, or owned-output SHA-256 verification; no visual conclusion is asserted.");
            return Finalize(entry, "BuildManifest runtimeEntry and owned output independently verified; no W24 visual-QA evidence was scanned.");
        }

        private static W24StatusRegistration Finalize(W24StatusRegistration entry, string basis)
        {
            var verified = entry.HasMachineReport && entry.HasRuntimeEntry && entry.RuntimeEntryPathIsValid && entry.RuntimeEntryExists && entry.RuntimeEntryGuidIsVerifiable && entry.RuntimeEntryHashIsVerifiable && IsCanonicalSha256(entry.BuildHash);
            entry.Maturity = verified ? W24MaturityLevel.L2_VisualPlaceholder : W24MaturityLevel.L0_InvalidOrMissing;
            entry.WorkingStatus = verified ? W24WorkingStatus.VISUAL_PENDING : W24WorkingStatus.None;
            entry.Basis = basis;
            return entry;
        }

        private static W24BuildManifest ParseManifest(string path)
        {
            try { return JsonUtility.FromJson<W24BuildManifest>(File.ReadAllText(path)); }
            catch (Exception) { return null; }
        }

        private static bool TryResolveProjectPath(string projectRoot, string assetPath, out string absolutePath)
        {
            absolutePath = null;
            if (string.IsNullOrEmpty(assetPath) || Path.IsPathRooted(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal)) return false;
            var root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var assetsRoot = Path.Combine(root, "Assets") + Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(Path.Combine(root, assetPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!candidate.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase)) return false;
            absolutePath = candidate;
            return true;
        }

        private static bool GuidMatches(string assetPath, string expectedGuid)
        {
            try
            {
                var line = File.ReadAllLines(assetPath + ".meta").FirstOrDefault(value => value.StartsWith("guid:", StringComparison.Ordinal));
                return line != null && string.Equals(NormalizeGuid(line.Substring("guid:".Length).Trim()), expectedGuid, StringComparison.Ordinal);
            }
            catch (Exception) { return false; }
        }

        private static bool IsGuid(string value)
        {
            return value != null && value.Length == 32 && value.All(character => (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'));
        }

        private static string FileSha256(string path) { return CanonicalSha256(File.ReadAllBytes(path)); }

        private static string CanonicalSha256(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return "sha256:" + BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string ToCanonicalSha256(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            var normalized = value.StartsWith("sha256:", StringComparison.Ordinal) ? value : "sha256:" + value;
            return IsCanonicalSha256(normalized) ? normalized : null;
        }

        private static string NormalizeGuid(string value) { return value == null ? null : value.ToLowerInvariant(); }
        private static string Normalize(string path) { return path == null ? null : path.Replace('\\', '/'); }

        [Serializable]
        private sealed class W24BuildManifest
        {
            public string effectId;
            public string buildHash;
            public W24RuntimeEntry runtimeEntry;
            public W24OwnedOutput[] ownedOutputs;
        }

        [Serializable]
        private sealed class W24RuntimeEntry { public string kind; public string path; public string guid; }

        [Serializable]
        private sealed class W24OwnedOutput { public string path; public string guid; public string sha256; }
    }
}
