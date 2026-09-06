using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VFXComposer.Tests.EditMode
{
    /// <summary>
    /// COMPILER_BOUNDARY_V2 §3.3 asset admission list, bidirectional truthfulness:
    /// WL-1 — every listed entry resolves to an existing asset whose GUID and sha256 match;
    /// WL-2 — every asset under the governed shared roots is listed (no unregistered asset can
    ///        hide on a legal path);
    /// WL-3 — every mesh generator row pins the generator source file hash, so a code change
    ///        without a version bump fails closed;
    /// WL-6 — the list lives outside every build write surface and is never written by a build.
    /// The material family ships as hand-written HLSL (T2B_IMPL_REPORT §3-1), so the governed
    /// roots are the TechniqueFamilies shader/preset trees plus Assets/VFX/Shared (empty today,
    /// governed so anything dropped there without registration turns this red).
    /// </summary>
    public sealed class AssetAllowListGateTests
    {
        private const string AllowListPath = "ProjectSettings/VFXComposer/AssetAllowList.json";

        /// <summary>Governed roots (WL-2): every admissible asset file below these must be listed.</summary>
        private static readonly string[] GovernedRoots =
        {
            "Assets/VFX/TechniqueFamilies/Shaders",
            "Assets/VFX/TechniqueFamilies/Presets",
            "Assets/VFX/Shared"
        };

        private static readonly string[] GovernedExtensions = { ".shader", ".shadergraph", ".shadersubgraph", ".hlsl", ".vfx", ".asset", ".mat" };

        private static JObject Load()
        {
            var absolute = Absolute(AllowListPath);
            Assert.That(File.Exists(absolute), Is.True, AllowListPath + " must exist (human-maintained admission list).");
            return JObject.Parse(File.ReadAllText(absolute));
        }

        [Test]
        public void WL1_EveryListedEntryResolvesToAnExistingAssetWithMatchingGuidAndSha256()
        {
            var list = Load();
            foreach (var section in new[] { "shaderGraphs", "subgraphs", "vfxTemplates", "presets" })
            {
                foreach (var entry in (JArray)list[section])
                {
                    var id = (string)entry["id"];
                    var path = (string)entry["path"];
                    var absolute = Absolute(path);
                    Assert.That(File.Exists(absolute), Is.True, section + "/" + id + ": listed asset is missing on disk: " + path);
                    Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo((string)entry["guid"]), section + "/" + id + ": GUID mismatch.");
                    Assert.That(Sha256(absolute), Is.EqualTo((string)entry["sha256"]), section + "/" + id + ": sha256 mismatch — the asset changed after registration.");
                }
            }
        }

        [Test]
        public void WL2_EveryAssetUnderTheGovernedRootsIsListed()
        {
            var list = Load();
            var listedPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var section in new[] { "shaderGraphs", "subgraphs", "vfxTemplates", "presets" })
                foreach (var entry in (JArray)list[section])
                    listedPaths.Add((string)entry["path"]);

            var unlisted = new List<string>();
            foreach (var root in GovernedRoots)
            {
                var absoluteRoot = Absolute(root);
                if (!Directory.Exists(absoluteRoot)) continue;
                foreach (var file in Directory.GetFiles(absoluteRoot, "*", SearchOption.AllDirectories))
                {
                    var extension = Path.GetExtension(file).ToLowerInvariant();
                    if (!GovernedExtensions.Contains(extension)) continue;
                    var assetPath = ToAssetPath(file);
                    if (!listedPaths.Contains(assetPath)) unlisted.Add(assetPath);
                }
            }

            Assert.That(unlisted, Is.Empty,
                "Unregistered admissible asset(s) on a governed path (WL-2 closes the 'legal path, illegal asset' hole):\n" +
                string.Join("\n", unlisted));
        }

        [Test]
        public void WL3_EveryMeshGeneratorRowPinsTheGeneratorSourceHash()
        {
            var list = Load();
            const string generatorSource = "Packages/com.vfxcomposer.unity/Editor/TechniqueFamilies/VfxMeshGenerators.cs";
            var actual = Sha256(Absolute(generatorSource));
            var rows = (JArray)list["meshGenerators"];
            Assert.That(rows.Count, Is.EqualTo(20), "The registry declares exactly the twenty spec generators.");
            foreach (var entry in rows)
            {
                Assert.That((string)entry["codeSha256"], Is.EqualTo(actual),
                    (string)entry["id"] + ": generator source changed without re-registering (bump version + refresh codeSha256).");
                Assert.That(string.IsNullOrWhiteSpace((string)entry["version"]), Is.False);
            }

            // The row set mirrors the compiled registry exactly (no ghost generators, no missing rows).
            var listedIds = rows.Select(entry => (string)entry["id"]).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var registryIds = VFXComposer.Editor.TechniqueFamilies.VfxMeshGenerators.Ids
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(registryIds, listedIds, "AllowList meshGenerators must equal the compiled generator registry.");
        }

        [Test]
        public void WL6_TheAllowListIsOutsideEveryBuildWriteSurface()
        {
            Assert.That(VFXComposer.Editor.Build.VfxRecipeBuildWriteSurface.IsInsideWriteSurface(AllowListPath), Is.False,
                "The admission list is human-maintained; no build flow may write it (ADR-007 §2.1).");
        }

        [Test]
        public void AllowedDependencyRoots_AgreeWithTheBoundaryConstant()
        {
            var list = Load();
            var listed = ((JArray)list["allowedDependencyRoots"]).Select(value => (string)value).ToArray();
            var boundary = VFXComposer.Editor.TechniqueFamilies.VfxTechniqueFamilyBoundary.AllowedDependencyRoots;
            // The JSON adds the VFX Graph package root ahead of the T3 GPU-particle work; everything
            // in the compiled constant must be present, and nothing retired may reappear.
            foreach (var root in boundary) CollectionAssert.Contains(listed, root);
            CollectionAssert.DoesNotContain(listed, "Assets/VFX/Templates/");
            CollectionAssert.DoesNotContain(listed, "Assets/VFX/Effects/");
        }

        private static string Sha256(string absolutePath)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(absolutePath))
                return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
        }

        private static string Absolute(string projectRelative)
        {
            return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, projectRelative.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string ToAssetPath(string absolute)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return absolute.Substring(projectRoot.Length + 1).Replace('\\', '/');
        }
    }
}
