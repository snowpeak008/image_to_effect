using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;

namespace VFXComposer.Editor.W24
{
    /// <summary>
    /// Rollback boundary for one pre-C0 first formal build. It is deliberately not a general
    /// filesystem transaction: every target must be one of the approved, effect-owned roots.
    /// </summary>
    internal sealed class W24FirstFormalBuildTransaction : IDisposable
    {
        private enum TargetKind { OutputRoot, Recipe, Preview, Manifest, Candidate }

        private sealed class Target
        {
            public string Absolute;
            public TargetKind Kind;
            public string EffectId;
            public string CleanupBoundary;
        }

        private sealed class Snapshot
        {
            public Target Target;
            public string Backup;
            public bool Existed;
            public bool Directory;
        }

        /// <summary>Exact initial bytes for a target or its approved ancestor .meta file.</summary>
        private sealed class MetaSnapshot
        {
            public string MetaPath;
            public bool Existed;
            public byte[] Bytes;
        }

        private readonly List<Snapshot> snapshots = new List<Snapshot>();
        private readonly List<string> createdParentCandidates = new List<string>();
        private readonly List<MetaSnapshot> metaSnapshots = new List<MetaSnapshot>();
        private readonly string scratch;
        private bool completed;
        private bool disposed;

        /// <summary>Test-only deterministic fault seam. Production code never assigns it.</summary>
        internal static Action<string> FaultInjectionHook;
        // A using block's body exception is more informative than cleanup fallout.  Keep rollback
        // diagnostics observable without allowing Dispose to replace the original authoring error.
        internal Exception LastRollbackFailure { get; private set; }

        private W24FirstFormalBuildTransaction(IEnumerable<string> absoluteTargets)
        {
            var targets = ValidateTargetSet(absoluteTargets).OrderBy(item => item.Absolute, StringComparer.OrdinalIgnoreCase).ToArray();
            scratch = Path.Combine(Path.GetTempPath(), "vfxcomposer-w24-first-build-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(scratch);
            try
            {
                for (var index = 0; index < targets.Length; index++)
                {
                    for (var prior = 0; prior < index; prior++)
                        if (IsAncestorOrSame(targets[prior].Absolute, targets[index].Absolute) || IsAncestorOrSame(targets[index].Absolute, targets[prior].Absolute))
                            throw new ArgumentException("First-formal-build transaction targets may not overlap: " + targets[prior].Absolute + " / " + targets[index].Absolute);
                    CaptureTargetAndAncestorMeta(targets[index]);
                    RegisterInitiallyMissingParents(targets[index]);
                    snapshots.Add(Capture(targets[index], index));
                }
            }
            catch
            {
                // Preserve the setup failure too; scratch cleanup is diagnostic-only here.
                try { if (Directory.Exists(scratch)) Directory.Delete(scratch, true); } catch { }
                throw;
            }
        }

        internal static W24FirstFormalBuildTransaction Begin(params string[] absoluteOwnedTargets)
        {
            return new W24FirstFormalBuildTransaction(absoluteOwnedTargets);
        }

        internal static void ThrowIfFaultInjected(string checkpoint)
        {
            var hook = FaultInjectionHook;
            if (hook != null) hook(checkpoint);
        }

        /// <summary>
        /// Imports only registered Assets targets. It never calls global SaveAssets or Refresh,
        /// which could persist unrelated dirty assets.
        /// </summary>
        internal void ImportOwnedAssets()
        {
            if (disposed) throw new ObjectDisposedException(nameof(W24FirstFormalBuildTransaction));
            foreach (var snapshot in snapshots)
            {
                var assetPath = ToAssetPath(snapshot.Target.Absolute);
                if (!string.IsNullOrEmpty(assetPath) && (File.Exists(snapshot.Target.Absolute) || Directory.Exists(snapshot.Target.Absolute)))
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            }
        }

        internal void Commit()
        {
            if (disposed) throw new ObjectDisposedException(nameof(W24FirstFormalBuildTransaction));
            completed = true;
        }

        internal void Rollback()
        {
            if (disposed || completed) return;
            foreach (var snapshot in snapshots.AsEnumerable().Reverse()) Restore(snapshot);
            RemoveOnlyInitiallyMissingEmptyParents();
            ImportOwnedAssets();
            RestoreInitialMetaBytes();
        }

        public void Dispose()
        {
            if (disposed) return;
            try
            {
                if (!completed)
                {
                    try { Rollback(); }
                    catch (Exception e) { LastRollbackFailure = e; UnityEngine.Debug.LogError("W24 first-formal-build rollback failed after the original authoring path: " + e); }
                }
            }
            finally
            {
                disposed = true;
                try
                {
                    if (Directory.Exists(scratch)) Directory.Delete(scratch, true);
                }
                catch (Exception e)
                {
                    LastRollbackFailure = LastRollbackFailure == null ? e : new AggregateException(LastRollbackFailure, e);
                    UnityEngine.Debug.LogError("W24 first-formal-build scratch cleanup failed after the original authoring path: " + e);
                }
            }
        }

        private Snapshot Capture(Target target, int index)
        {
            RejectReparsePoints(target.Absolute, target.CleanupBoundary);
            var snapshot = new Snapshot
            {
                Target = target,
                Backup = Path.Combine(scratch, index.ToString("D3")),
                Directory = Directory.Exists(target.Absolute),
                Existed = Directory.Exists(target.Absolute) || File.Exists(target.Absolute)
            };
            if (!snapshot.Existed) return snapshot;
            if (snapshot.Directory) CopyDirectory(target.Absolute, snapshot.Backup);
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(snapshot.Backup));
                File.Copy(target.Absolute, snapshot.Backup, true);
                CopyMeta(target.Absolute, snapshot.Backup);
            }
            return snapshot;
        }

        private static void Restore(Snapshot snapshot)
        {
            RejectReparsePoints(snapshot.Target.Absolute, snapshot.Target.CleanupBoundary);
            DeleteTarget(snapshot.Target.Absolute);
            if (!snapshot.Existed) return;
            if (snapshot.Directory) CopyDirectory(snapshot.Backup, snapshot.Target.Absolute);
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(snapshot.Target.Absolute));
                File.Copy(snapshot.Backup, snapshot.Target.Absolute, true);
                CopyMeta(snapshot.Backup, snapshot.Target.Absolute);
            }
        }

        private void RegisterInitiallyMissingParents(Target target)
        {
            var current = Directory.GetParent(target.Absolute);
            while (current != null && IsAncestorOrSame(target.CleanupBoundary, current.FullName))
            {
                var path = Canonical(current.FullName);
                if (!Directory.Exists(path) && !File.Exists(path) && !createdParentCandidates.Contains(path, StringComparer.OrdinalIgnoreCase)) createdParentCandidates.Add(path);
                if (string.Equals(path, target.CleanupBoundary, StringComparison.OrdinalIgnoreCase)) break;
                current = current.Parent;
            }
        }

        private void CaptureTargetAndAncestorMeta(Target target)
        {
            CaptureMeta(target.Absolute + ".meta");
            var current = Directory.GetParent(target.Absolute);
            while (current != null && IsAncestorOrSame(target.CleanupBoundary, current.FullName))
            {
                CaptureMeta(Canonical(current.FullName) + ".meta");
                if (string.Equals(Canonical(current.FullName), target.CleanupBoundary, StringComparison.OrdinalIgnoreCase)) break;
                current = current.Parent;
            }
        }

        private void CaptureMeta(string metaPath)
        {
            if (metaSnapshots.Any(item => string.Equals(item.MetaPath, metaPath, StringComparison.OrdinalIgnoreCase))) return;
            if (File.Exists(metaPath)) RejectReparsePoint(metaPath);
            metaSnapshots.Add(new MetaSnapshot
            {
                MetaPath = metaPath,
                Existed = File.Exists(metaPath),
                Bytes = File.Exists(metaPath) ? File.ReadAllBytes(metaPath) : null
            });
        }

        private void RestoreInitialMetaBytes()
        {
            foreach (var snapshot in metaSnapshots)
            {
                if (!snapshot.Existed)
                {
                    if (File.Exists(snapshot.MetaPath)) { RejectReparsePoint(snapshot.MetaPath); File.Delete(snapshot.MetaPath); }
                    continue;
                }
                if (File.Exists(snapshot.MetaPath)) RejectReparsePoint(snapshot.MetaPath);
                Directory.CreateDirectory(Path.GetDirectoryName(snapshot.MetaPath));
                if (!File.Exists(snapshot.MetaPath) || !File.ReadAllBytes(snapshot.MetaPath).SequenceEqual(snapshot.Bytes)) File.WriteAllBytes(snapshot.MetaPath, snapshot.Bytes);
            }
        }

        private void RemoveOnlyInitiallyMissingEmptyParents()
        {
            foreach (var parent in createdParentCandidates.OrderByDescending(path => path.Length))
            {
                if (!Directory.Exists(parent)) continue;
                RejectReparsePoints(parent, parent);
                if (Directory.EnumerateFileSystemEntries(parent).Any()) continue;
                Directory.Delete(parent, false);
                var meta = parent + ".meta";
                if (File.Exists(meta)) File.Delete(meta);
            }
        }

        private static void DeleteTarget(string target)
        {
            if (Directory.Exists(target))
            {
                RejectReparseTree(target);
                var assetPath = ToAssetPath(target);
                if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.IsValidFolder(assetPath)) AssetDatabase.DeleteAsset(assetPath);
                else Directory.Delete(target, true);
            }
            else if (File.Exists(target))
            {
                RejectReparsePoint(target);
                var assetPath = ToAssetPath(target);
                if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.LoadMainAssetAtPath(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
                else File.Delete(target);
            }
            var meta = target + ".meta";
            if (File.Exists(meta)) { RejectReparsePoint(meta); File.Delete(meta); }
        }

        private static void CopyMeta(string from, string to)
        {
            var sourceMeta = from + ".meta";
            if (!File.Exists(sourceMeta)) return;
            RejectReparsePoint(sourceMeta);
            var destinationMeta = to + ".meta";
            Directory.CreateDirectory(Path.GetDirectoryName(destinationMeta));
            File.Copy(sourceMeta, destinationMeta, true);
        }

        private static void CopyDirectory(string source, string destination)
        {
            RejectReparsePoint(source);
            Directory.CreateDirectory(destination);
            var pending = new Stack<string>();
            pending.Push(source);
            while (pending.Count != 0)
            {
                var current = pending.Pop();
                RejectReparsePoint(current);
                foreach (var child in Directory.GetFileSystemEntries(current))
                {
                    RejectReparsePoint(child);
                    if (Directory.Exists(child))
                    {
                        var directoryTarget = Path.Combine(destination, child.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                        Directory.CreateDirectory(directoryTarget);
                        pending.Push(child);
                    }
                    else
                    {
                        var fileTarget = Path.Combine(destination, child.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(fileTarget));
                        File.Copy(child, fileTarget, true);
                    }
                }
            }
            CopyMeta(source, destination);
        }

        private static IEnumerable<Target> ValidateTargetSet(IEnumerable<string> absoluteTargets)
        {
            var candidates = (absoluteTargets ?? Enumerable.Empty<string>()).Select(DescribeTarget).ToArray();
            if (candidates.Length == 0) throw new ArgumentException("A first-formal-build transaction needs owned targets.", nameof(absoluteTargets));
            if (candidates.Select(item => item.Absolute).Distinct(StringComparer.OrdinalIgnoreCase).Count() != candidates.Length) throw new ArgumentException("First-formal-build transaction targets must be unique.", nameof(absoluteTargets));
            var effectIds = candidates.Where(item => !string.IsNullOrEmpty(item.EffectId)).Select(item => item.EffectId).Distinct(StringComparer.Ordinal).ToArray();
            if (effectIds.Length == 0 || candidates.Count(item => item.Kind == TargetKind.OutputRoot) != effectIds.Length || candidates.Count(item => item.Kind == TargetKind.Manifest) != effectIds.Length || candidates.Count(item => item.Kind == TargetKind.Candidate) != effectIds.Length)
                throw new ArgumentException("Every first-formal-build transaction must declare exactly one owned output root, manifest, and C0 candidate for each effect.", nameof(absoluteTargets));
            foreach (var effectId in effectIds)
            {
                if (candidates.Count(item => item.Kind == TargetKind.OutputRoot && item.EffectId == effectId) != 1 || candidates.Count(item => item.Kind == TargetKind.Manifest && item.EffectId == effectId) != 1 || candidates.Count(item => item.Kind == TargetKind.Candidate && item.EffectId == effectId) != 1)
                    throw new ArgumentException("First-formal-build target identities do not agree for " + effectId + ".", nameof(absoluteTargets));
            }
            if (candidates.Count(item => item.Kind == TargetKind.Recipe) != effectIds.Length || candidates.Count(item => item.Kind == TargetKind.Preview) != effectIds.Length)
                throw new ArgumentException("Every first-formal-build effect needs exactly one owned Recipe and Preview Scene target.", nameof(absoluteTargets));
            return candidates;
        }

        private static Target DescribeTarget(string raw)
        {
            var absolute = Canonical(raw);
            var projectRoot = Canonical(Directory.GetParent(UnityEngine.Application.dataPath).FullName);
            var repositoryRoot = Canonical(Directory.GetParent(projectRoot).FullName);
            var assetsRoot = Path.Combine(projectRoot, "Assets", "VFX");
            var effectsRoot = Path.Combine(assetsRoot, "Effects");
            var generatedRoot = Path.Combine(assetsRoot, "Generated");
            var recipesRoot = Path.Combine(assetsRoot, "Recipes");
            var previewRoot = Path.Combine(assetsRoot, "Preview");
            var manifestsRoot = Path.Combine(projectRoot, "ProjectSettings", "VFXComposer", "BuildManifests");
            var candidatesRoot = Path.Combine(repositoryRoot, "docs", "vfx-candidates");

            if (IsDescendantOf(effectsRoot, absolute) || IsDescendantOf(generatedRoot, absolute))
            {
                var root = IsDescendantOf(effectsRoot, absolute) ? effectsRoot : generatedRoot;
                var relative = Relative(root, absolute).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var effectId = relative.LastOrDefault();
                var effectsLayout = string.Equals(Canonical(root), Canonical(effectsRoot), StringComparison.OrdinalIgnoreCase);
                if ((effectsLayout && (relative.Length != 2 || !IsSafeArchetypeSegment(relative[0]))) || (!effectsLayout && relative.Length != 1) || !IsEffectId(effectId) || Path.HasExtension(absolute))
                    throw new ArgumentException("Owned output must be exactly Assets/VFX/Effects/<archetype>/<effectId> or Assets/VFX/Generated/<effectId>: " + raw);
                return new Target { Absolute = absolute, Kind = TargetKind.OutputRoot, EffectId = effectId, CleanupBoundary = Canonical(root) };
            }
            if (IsDescendantOf(recipesRoot, absolute) && string.Equals(Path.GetExtension(absolute), ".json", StringComparison.OrdinalIgnoreCase)) return new Target { Absolute = absolute, Kind = TargetKind.Recipe, CleanupBoundary = Canonical(recipesRoot) };
            if (IsDescendantOf(previewRoot, absolute) && string.Equals(Path.GetExtension(absolute), ".unity", StringComparison.OrdinalIgnoreCase)) return new Target { Absolute = absolute, Kind = TargetKind.Preview, CleanupBoundary = Canonical(previewRoot) };
            if (IsDescendantOf(manifestsRoot, absolute))
            {
                var name = Path.GetFileName(absolute);
                const string suffix = ".manifest.json";
                var effectId = name != null && name.EndsWith(suffix, StringComparison.Ordinal) ? name.Substring(0, name.Length - suffix.Length) : null;
                if (!IsEffectId(effectId)) throw new ArgumentException("Manifest target must use <effectId>.manifest.json: " + raw);
                return new Target { Absolute = absolute, Kind = TargetKind.Manifest, EffectId = effectId, CleanupBoundary = Canonical(manifestsRoot) };
            }
            if (IsDescendantOf(candidatesRoot, absolute))
            {
                var relative = Relative(candidatesRoot, absolute).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (relative.Length != 2 || !IsEffectId(relative[0]) || !string.Equals(relative[1], "C0", StringComparison.Ordinal)) throw new ArgumentException("Candidate target must be docs/vfx-candidates/<effectId>/C0: " + raw);
                return new Target { Absolute = absolute, Kind = TargetKind.Candidate, EffectId = relative[0], CleanupBoundary = Canonical(candidatesRoot) };
            }
            throw new ArgumentException("First-formal-build target is outside approved effect-owned roots: " + raw);
        }

        private static void RejectReparsePoints(string target, string boundary)
        {
            var current = new DirectoryInfo(Canonical(target));
            while (current != null && IsAncestorOrSame(boundary, current.FullName))
            {
                if (File.Exists(current.FullName) || Directory.Exists(current.FullName)) RejectReparsePoint(current.FullName);
                if (string.Equals(Canonical(current.FullName), Canonical(boundary), StringComparison.OrdinalIgnoreCase)) break;
                current = current.Parent;
            }
        }

        private static void RejectReparsePoint(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return;
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("First-formal-build transaction rejects symlink/junction/reparse targets: " + path);
        }

        private static void RejectReparseTree(string root)
        {
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count != 0)
            {
                var current = pending.Pop();
                RejectReparsePoint(current);
                if (!Directory.Exists(current)) continue;
                foreach (var child in Directory.GetFileSystemEntries(current))
                {
                    RejectReparsePoint(child);
                    if (Directory.Exists(child)) pending.Push(child);
                }
            }
        }

        private static string ToAssetPath(string absolute)
        {
            var projectRoot = Canonical(Directory.GetParent(UnityEngine.Application.dataPath).FullName) + Path.DirectorySeparatorChar;
            var full = Canonical(absolute);
            if (!full.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)) return null;
            var relative = full.Substring(projectRoot.Length).Replace('\\', '/');
            return relative.StartsWith("Assets/", StringComparison.Ordinal) ? relative : null;
        }

        private static bool IsEffectId(string value) { return value != null && Regex.IsMatch(value, "^[a-z][a-z0-9_]*$"); }
        private static bool IsSafeArchetypeSegment(string value) { return value != null && Regex.IsMatch(value, "^[A-Za-z][A-Za-z0-9_-]*$"); }
        private static string Canonical(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("First-formal-build transaction target is empty.", nameof(path));
            var full = Path.GetFullPath(path);
            var root = Path.GetPathRoot(full);
            return string.Equals(full, root, StringComparison.OrdinalIgnoreCase) ? root : full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        private static bool IsDescendantOf(string parent, string child) { return !string.Equals(Canonical(parent), Canonical(child), StringComparison.OrdinalIgnoreCase) && IsAncestorOrSame(Canonical(parent), Canonical(child)); }
        private static string Relative(string parent, string child) { return Canonical(child).Substring(Canonical(parent).Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        private static bool IsAncestorOrSame(string parent, string child)
        {
            if (string.Equals(parent, child, StringComparison.OrdinalIgnoreCase)) return true;
            return child.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || child.StartsWith(parent + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
    }
}
