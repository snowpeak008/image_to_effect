using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace VFXComposer.W24
{
    /// <summary>
    /// A deliberately small write-once evidence directory. It is not a general file store:
    /// duplicate relative paths, traversal, and writes after sealing are all rejected.
    /// </summary>
    public sealed class W24EvidenceStore
    {
        private readonly string root;
        private readonly HashSet<string> written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> artifactHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly string candidateId;
        private readonly string captureProfileHash;
        private bool sealedStore;

        private W24EvidenceStore(string root, string candidateId, string captureProfileHash)
        {
            this.root = root;
            this.candidateId = candidateId;
            this.captureProfileHash = captureProfileHash;
        }

        public string Root { get { return root; } }
        public bool IsSealed { get { return sealedStore; } }

        public static W24EvidenceStore Create(string root, string candidateId, string captureProfileHash)
        {
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(candidateId) || string.IsNullOrEmpty(captureProfileHash)) throw new ArgumentException("W24 evidence root, candidate ID, and Capture Profile hash are required.");
            W24CaptureProfile.RequireCanonicalSha256(captureProfileHash, "Capture Profile hash");
            var full = Path.GetFullPath(root);
            if (Directory.Exists(full) && Directory.EnumerateFileSystemEntries(full).Any()) throw new InvalidOperationException("W24 evidence directory is write-once and already exists: " + full);
            Directory.CreateDirectory(full);
            var store = new W24EvidenceStore(full, candidateId, captureProfileHash);
            store.WriteText("evidence-lock.json", "{\"schema\":\"w24-s0a-evidence-lock/v1\",\"candidateId\":\"" + W24CaptureProfile.Escape(candidateId) + "\",\"captureProfileSha256\":\"" + W24CaptureProfile.Escape(captureProfileHash) + "\"}");
            return store;
        }

        public string WriteBytes(string relativePath, byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException("bytes");
            var path = ResolveNewPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) stream.Write(bytes, 0, bytes.Length);
            var normalized = Normalize(relativePath);
            var hash = HashFile(path);
            written.Add(normalized);
            artifactHashes.Add(normalized, hash);
            return hash;
        }

        public string WriteText(string relativePath, string text)
        {
            return WriteBytes(relativePath, System.Text.Encoding.UTF8.GetBytes(text ?? string.Empty));
        }

        /// <summary>
        /// Finalizes a capture with a hash-bound index of every preceding artifact.  The initial
        /// evidence-lock is intentionally only a run reservation; this final seal is the object
        /// that makes a completed capture distinguishable from a hand-written partial directory.
        /// The caller supplies compact, already-validated provenance JSON (command/tool/source
        /// identities).  Hashes detect drift and accidental/local tampering; they are not a
        /// substitute for an external signature when writers are mutually untrusted.
        /// </summary>
        public void Seal(string provenanceJson)
        {
            if (sealedStore) return;
            if (string.IsNullOrEmpty(provenanceJson)) throw new ArgumentException("W24 final evidence seal requires provenance JSON.", "provenanceJson");
            if (written.Contains("evidence-seal.json")) throw new InvalidOperationException("W24 final evidence seal already exists.");
            var artifacts = string.Join(",", artifactHashes.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => "{\"file\":\"" + Escape(pair.Key) + "\",\"sha256\":\"" + Escape(pair.Value) + "\"}"));
            var body = "{\"schema\":\"w24-s0a-final-evidence-seal/v1\",\"candidateId\":\"" + Escape(candidateId) + "\",\"captureProfileSha256\":\"" + Escape(captureProfileHash) + "\",\"artifacts\":[" + artifacts + "],\"provenance\":" + provenanceJson + "}";
            var seal = body.Substring(0, body.Length - 1) + ",\"sealHash\":\"" + HashText(body) + "\"}";
            WriteText("evidence-seal.json", seal);
            foreach (var path in Directory.GetFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
            sealedStore = true;
        }

        /// <summary>Legacy non-formal callers cannot silently create an S0a seal without provenance.</summary>
        public void Seal()
        {
            Seal("{\"kind\":\"nonformal-capture\"}");
        }

        public static string HashFile(string path)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path)) return W24CaptureProfile.PrefixSha256(string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2", CultureInfo.InvariantCulture))));
        }

        private static string HashText(string value)
        {
            using (var sha = SHA256.Create())
                return W24CaptureProfile.PrefixSha256(string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(item => item.ToString("x2", CultureInfo.InvariantCulture))));
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private string ResolveNewPath(string relativePath)
        {
            if (sealedStore) throw new InvalidOperationException("W24 evidence directory is sealed: " + root);
            var normalized = Normalize(relativePath);
            if (written.Contains(normalized)) throw new InvalidOperationException("W24 evidence artifact is write-once: " + relativePath);
            var path = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && !string.Equals(path, root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("W24 evidence path escapes its run directory: " + relativePath);
            if (File.Exists(path) || Directory.Exists(path)) throw new InvalidOperationException("W24 evidence artifact already exists: " + relativePath);
            return path;
        }

        private static string Normalize(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath) || Path.IsPathRooted(relativePath)) throw new ArgumentException("W24 evidence artifact path must be relative.", "relativePath");
            // Evidence manifests are cross-platform protocol artifacts.  Keep their paths in
            // canonical forward-slash form even on Windows; translate to the host separator
            // only when resolving a physical path.  Otherwise a Windows-produced seal indexes
            // `frames\\...` while validators enumerate the same file as `frames/...`.
            var normalized = relativePath.Replace('\\', '/').TrimStart('/');
            if (normalized.Split('/').Any(part => part == ".." || part.Length == 0)) throw new ArgumentException("W24 evidence artifact path may not traverse directories.", "relativePath");
            return normalized;
        }
    }
}
