using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using VFXComposer.Editor.Style;

namespace VFXComposer.Tests.EditMode
{
    public sealed class StyleSpecialNextCandidatePreviewSceneTests
    {
        [Test]
        public void ThreePreviewScenes_AreIdempotentHardClippedAndW16PairsStayAdjacent()
        {
            StyleSpecialNextCandidateAuthoring.BuildAllForBatch();
            var paths = new[] { StyleSpecialNextCandidateAuthoring.W9PreviewScenePath, StyleSpecialNextCandidateAuthoring.W10PreviewScenePath, StyleSpecialNextCandidateAuthoring.W16PreviewScenePath };
            var firstHashes = paths.ToDictionary(value => value, value => Sha256(Absolute(value)), StringComparer.Ordinal);
            StyleSpecialNextCandidateAuthoring.BuildAllForBatch();
            foreach (var path in paths) Assert.That(Sha256(Absolute(path)), Is.EqualTo(firstHashes[path]), path + " must not be rewritten when its candidate signature is unchanged.");

            foreach (var group in new[] { StyleSpecialCandidateGroup.W9Style2D, StyleSpecialCandidateGroup.W10Style3D, StyleSpecialCandidateGroup.W16StylePack2 })
            {
                var expected = group == StyleSpecialCandidateGroup.W16StylePack2 ? 12 : 10;
                var scene = EditorSceneManager.OpenScene(StyleSpecialNextCandidateAuthoring.PreviewScenePath(group), OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                var cells = roots.Select(value => value.GetComponent<StyleSpecialNextCandidateCell>()).Where(value => value != null).OrderBy(value => value.CellIndex).ToArray();
                Assert.That(cells.Length, Is.EqualTo(expected), group.ToString());
                Assert.That(cells.Select(value => value.IsolatedLayer).Distinct().Count(), Is.EqualTo(expected), group.ToString());
                foreach (var cell in cells)
                {
                    Assert.That(cell.Group, Is.EqualTo(group));
                    Assert.That(cell.EffectAndLabelAreDisjoint, Is.True, cell.Label);
                    Assert.That(cell.UsesExclusiveCullingMask, Is.True, cell.Label);
                    Assert.That(cell.EffectCamera.rect, Is.EqualTo(cell.EffectViewport), cell.Label);
                    Assert.That(cell.RuntimeEntry, Is.Not.Null, cell.Label);
                    Assert.That(cell.RuntimeEntry.transform.localScale, Is.EqualTo(Vector3.one * StyleSpecialNextCandidateAuthoring.PreviewEntryScale), cell.Label);
                    Assert.That(cell.RuntimeEntry.DeclaredLocalBounds.size, Is.EqualTo(StyleSpecialNextCandidateRuntimeEntry.UniformLocalEnvelope.size), cell.Label);
                }
                var driver = roots.Select(value => value.GetComponent<StyleSpecialNextCandidatePreviewDriver>()).Single(value => value != null);
                Assert.That(driver.Group, Is.EqualTo(group));
                Assert.That(driver.ConfiguredEntryCount, Is.EqualTo(expected));
                Assert.That(driver.CompilerVersion, Is.EqualTo(StyleSpecialNextCandidateAuthoring.CompilerVersion));
                Assert.That(roots.Count(value => value.name == StyleSpecialNextCandidateAuthoring.CandidateStatusRootName), Is.EqualTo(1));
                if (group == StyleSpecialCandidateGroup.W16StylePack2)
                {
                    for (var index = 0; index < cells.Length; index += 2)
                    {
                        Assert.That(cells[index].PairFamily, Is.Not.Empty);
                        Assert.That(cells[index + 1].PairFamily, Is.EqualTo(cells[index].PairFamily));
                        CollectionAssert.AreEquivalent(new[] { "new", "variant" }, new[] { cells[index].PairRole, cells[index + 1].PairRole });
                    }
                }
            }
        }

        private static string Sha256(string path)
        {
            using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
        }

        private static string Absolute(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
