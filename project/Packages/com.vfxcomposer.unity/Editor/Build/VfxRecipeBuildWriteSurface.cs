using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Rules;

namespace VFXComposer.Editor.Build
{
    /// <summary>
    /// The executor-layer half of ADR-007's double defense. It resolves and independently re-checks the
    /// closed three-member write surface before the compiler runs, so a rejected target never reaches
    /// the compile flow at all. Every predicate here is an independent implementation on purpose: it must
    /// not be able to inherit a bug from <see cref="VfxCompiler"/>'s own path guard.
    /// </summary>
    public static class VfxRecipeBuildWriteSurface
    {
        public const string GeneratedRoot = "Assets/VFX/Generated";
        public const string OwnershipManifestRoot = "ProjectSettings/VFXComposer/BuildManifests";
        public const string ProvenanceRecipeRoot = "Assets/VFX/Recipes";
        public const string BuildManifestFileName = "BuildManifest.json";

        /// <summary>Longest accepted effect id. Well below any filesystem component limit.</summary>
        public const int MaximumEffectIdLength = 64;

        /// <summary>
        /// Known compiler temporary directory prefixes (ADR-007 §2.4). A process-level interrupt cannot
        /// run the compiler's own recovery, so the entry point sweeps these before every build.
        /// </summary>
        private static readonly string[] TemporaryDirectoryPrefixes = { "vfxs6tmp_", "impacttmp_", "areatmp_" };

        /// <summary>
        /// Reserved Windows device names. The lower_snake_case charset alone accepts "con", "nul",
        /// "com1" and friends, and AssetDatabase would fail late and opaquely on them.
        /// </summary>
        private static readonly HashSet<string> ReservedDeviceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "con", "prn", "aux", "nul", "clock$",
            "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
            "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9"
        };

        /// <summary>
        /// Accepts an effect id only if it is safe as a single path component under every member of the
        /// write surface. Stricter than both the compiler's character filter and the rules id guard.
        /// </summary>
        public static bool IsAcceptedEffectId(string effectId, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(effectId))
            {
                error = "Effect id is required.";
                return false;
            }

            if (effectId.Length > MaximumEffectIdLength)
            {
                error = "Effect id exceeds " + MaximumEffectIdLength + " characters.";
                return false;
            }

            foreach (var character in effectId)
            {
                if (!(character >= 'a' && character <= 'z') && !(character >= '0' && character <= '9') && character != '_')
                {
                    error = "Effect id must be lower_snake_case (a-z, 0-9, underscore).";
                    return false;
                }
            }

            if (effectId[0] == '_' || effectId[effectId.Length - 1] == '_' || effectId.Contains("__"))
            {
                error = "Effect id must not start, end or double up on underscores.";
                return false;
            }

            if (IsReservedDeviceName(effectId))
            {
                error = "Effect id is a reserved Windows device name.";
                return false;
            }

            return true;
        }

        /// <summary>True for a reserved device name, with or without an extension.</summary>
        public static bool IsReservedDeviceName(string fileNameOrStem)
        {
            if (string.IsNullOrEmpty(fileNameOrStem)) return false;
            var stem = fileNameOrStem;
            var dot = stem.IndexOf('.');
            if (dot >= 0) stem = stem.Substring(0, dot);
            return ReservedDeviceNames.Contains(stem.Trim());
        }

        public static string AssetRootFor(string effectId) { return GeneratedRoot + "/" + effectId; }
        public static string BuildManifestFor(string effectId) { return AssetRootFor(effectId) + "/" + BuildManifestFileName; }
        public static string OwnershipManifestFor(string effectId) { return OwnershipManifestRoot + "/" + effectId + ".manifest.json"; }
        public static string ProvenanceRecipeFor(string effectId) { return ProvenanceRecipeRoot + "/" + effectId + ".json"; }

        /// <summary>
        /// Verifies that every target this build may touch is a literal member of the closed clause, and
        /// that each one resolves inside the project without traversal or reparse-point indirection.
        /// </summary>
        public static bool AreTargetsContained(string effectId, string prefabPath, out string error)
        {
            error = null;
            string idError;
            if (!IsAcceptedEffectId(effectId, out idError))
            {
                error = idError;
                return false;
            }

            var assetRoot = AssetRootFor(effectId);
            var expectedPrefab = assetRoot + "/";
            if (string.IsNullOrEmpty(prefabPath) ||
                !prefabPath.StartsWith(expectedPrefab, StringComparison.Ordinal) ||
                !prefabPath.EndsWith(".prefab", StringComparison.Ordinal) ||
                prefabPath.Substring(expectedPrefab.Length).IndexOf('/') >= 0)
            {
                error = "Runtime Entry must be a prefab directly under " + assetRoot + ".";
                return false;
            }

            var targets = new[]
            {
                prefabPath,
                BuildManifestFor(effectId),
                OwnershipManifestFor(effectId),
                ProvenanceRecipeFor(effectId)
            };

            foreach (var target in targets)
            {
                if (!IsSafeProjectRelativePath(target))
                {
                    error = "Write target is not a safe project-relative path: " + target;
                    return false;
                }

                if (!IsInsideWriteSurface(target))
                {
                    error = "Write target is outside the closed write surface: " + target;
                    return false;
                }

                string resolveError;
                if (!TryResolveInsideProject(target, out resolveError))
                {
                    error = resolveError;
                    return false;
                }
            }

            return true;
        }

        /// <summary>Membership test for the closed clause. Everything not enumerated here is refused.</summary>
        public static bool IsInsideWriteSurface(string projectRelativePath)
        {
            if (!IsSafeProjectRelativePath(projectRelativePath)) return false;
            if (projectRelativePath.StartsWith(GeneratedRoot + "/", StringComparison.Ordinal)) return true;

            var name = Path.GetFileName(projectRelativePath);
            var folder = ParentOf(projectRelativePath);
            if (string.Equals(folder, OwnershipManifestRoot, StringComparison.Ordinal) &&
                name.EndsWith(".manifest.json", StringComparison.Ordinal))
            {
                return true;
            }

            return string.Equals(folder, ProvenanceRecipeRoot, StringComparison.Ordinal) &&
                   name.EndsWith(".json", StringComparison.Ordinal);
        }

        public static bool IsSafeProjectRelativePath(string projectRelativePath)
        {
            if (string.IsNullOrWhiteSpace(projectRelativePath)) return false;
            if (Path.IsPathRooted(projectRelativePath)) return false;
            if (projectRelativePath.IndexOf('\\') >= 0 || projectRelativePath.IndexOf('\0') >= 0) return false;
            foreach (var segment in projectRelativePath.Split('/'))
            {
                if (string.IsNullOrEmpty(segment) || segment == "." || segment == "..") return false;
                if (IsReservedDeviceName(segment)) return false;
            }

            return true;
        }

        public static string ProjectRoot()
        {
            return Path.GetFullPath(Directory.GetParent(Application.dataPath).FullName);
        }

        public static string ProjectAbsolute(string projectRelativePath)
        {
            return Path.GetFullPath(Path.Combine(ProjectRoot(), projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        /// <summary>True when the path resolves under the project root and no ancestor is a reparse point.</summary>
        public static bool TryResolveInsideProject(string projectRelativePath, out string error)
        {
            error = null;
            var root = ProjectRoot();
            var boundary = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string absolute;
            try { absolute = ProjectAbsolute(projectRelativePath); }
            catch (Exception exception)
            {
                error = "Write target is not a valid path: " + projectRelativePath + " (" + exception.Message + ")";
                return false;
            }

            if (!absolute.StartsWith(boundary, StringComparison.OrdinalIgnoreCase))
            {
                error = "Write target escaped the project root: " + projectRelativePath;
                return false;
            }

            if (HasReparsePointAtOrAbove(absolute, root))
            {
                error = "Write target traverses a symlink, junction or reparse point: " + projectRelativePath;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Removes orphan compiler temporaries and pending residue under the write surface. Residue that
        /// carries the only known-good bytes of a manifest is never deleted; it is reported instead.
        /// </summary>
        public static bool TrySweepResidue(string effectId, List<string> cleaned, out string error)
        {
            error = null;
            var ownershipManifest = ProjectAbsolute(OwnershipManifestFor(effectId));
            var atomicBackup = ownershipManifest + ".atomic-backup";
            if (File.Exists(atomicBackup))
            {
                error = "Ownership manifest atomic-write residue must be recovered before retrying: " +
                        OwnershipManifestFor(effectId) + ".atomic-backup";
                return false;
            }

            try
            {
                foreach (var folder in AssetDatabase.GetSubFolders(GeneratedRoot))
                {
                    var name = Path.GetFileName(folder);
                    if (!IsKnownTemporaryDirectory(name)) continue;
                    if (!AssetDatabase.DeleteAsset(folder))
                    {
                        error = "Orphan compiler temporary directory could not be removed: " + folder;
                        return false;
                    }

                    cleaned.Add(folder);
                }

                var generatedAbsolute = ProjectAbsolute(GeneratedRoot);
                if (Directory.Exists(generatedAbsolute))
                {
                    foreach (var pending in Directory.GetFiles(generatedAbsolute, "*.pending", SearchOption.AllDirectories))
                    {
                        File.Delete(pending);
                        cleaned.Add(ToProjectRelative(pending));
                    }
                }

                foreach (var single in new[] { ownershipManifest, ProjectAbsolute(ProvenanceRecipeFor(effectId)) })
                {
                    var pending = single + ".pending";
                    if (!File.Exists(pending)) continue;
                    File.Delete(pending);
                    cleaned.Add(ToProjectRelative(pending));
                }
            }
            catch (Exception exception)
            {
                error = "Residue sweep failed: " + exception.Message;
                return false;
            }

            if (cleaned.Count > 0) AssetDatabase.Refresh();
            return true;
        }

        public static bool IsKnownTemporaryDirectory(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return false;
            foreach (var prefix in TemporaryDirectoryPrefixes)
            {
                if (folderName.StartsWith(prefix, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        public static string ToProjectRelative(string absolutePath)
        {
            var boundary = ProjectRoot().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(absolutePath);
            return full.StartsWith(boundary, StringComparison.OrdinalIgnoreCase)
                ? full.Substring(boundary.Length).Replace('\\', '/')
                : full.Replace('\\', '/');
        }

        private static string ParentOf(string projectRelativePath)
        {
            var separator = projectRelativePath.LastIndexOf('/');
            return separator < 0 ? string.Empty : projectRelativePath.Substring(0, separator);
        }

        private static bool HasReparsePointAtOrAbove(string absolutePath, string stopAt)
        {
            var boundary = Path.GetFullPath(stopAt).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var current = Path.GetFullPath(absolutePath);
            while (!string.IsNullOrEmpty(current))
            {
                if (File.Exists(current) || Directory.Exists(current))
                {
                    var attributes = File.GetAttributes(current);
                    if ((attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint) return true;
                }

                if (string.Equals(current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), boundary, StringComparison.OrdinalIgnoreCase)) return false;
                var parent = Path.GetDirectoryName(current);
                if (string.Equals(parent, current, StringComparison.Ordinal)) return false;
                current = parent;
            }

            return false;
        }

        /// <summary>
        /// The rules id guard is the authority for the ownership manifest file name. Calling it here keeps
        /// the entry point from accepting an id the manifest writer would later reject deep in the flow.
        /// </summary>
        public static bool AgreesWithProjectRules(string effectId)
        {
            try { return string.Equals(VfxProjectRules.SanitizeId(effectId), effectId, StringComparison.Ordinal); }
            catch (ArgumentException) { return false; }
        }
    }
}
