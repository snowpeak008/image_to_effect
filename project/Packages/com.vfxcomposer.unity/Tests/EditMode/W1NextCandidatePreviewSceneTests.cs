using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VFXComposer.Editor.Style;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W1NextCandidatePreviewSceneTests
    {
        [Test]
        public void PreviewScene_HardClipsElevenCellsAndKeepsOverlayLabelsOutsideEffectViewports()
        {
            W1NextCandidateAuthoring.BuildAllForBatch();
            var firstSceneHash = Sha256(Absolute(W1NextCandidateAuthoring.PreviewScenePath));
            W1NextCandidateAuthoring.BuildAllForBatch();
            Assert.That(Sha256(Absolute(W1NextCandidateAuthoring.PreviewScenePath)), Is.EqualTo(firstSceneHash), "An unchanged W1 build must not rewrite its Preview scene.");
            var scene = EditorSceneManager.OpenScene(W1NextCandidateAuthoring.PreviewScenePath, OpenSceneMode.Single);
            try
            {
                var roots = scene.GetRootGameObjects();
                var cells = roots.SelectMany(value => value.GetComponentsInChildren<W1NextCandidateCell>(true)).OrderBy(value => value.CellIndex).ToArray();
                Assert.That(cells.Length, Is.EqualTo(11));
                Assert.That(roots.SelectMany(value => value.GetComponentsInChildren<Camera>(true)).Count(value => value.CompareTag("MainCamera")), Is.EqualTo(1));
                Assert.That(roots.SelectMany(value => value.GetComponentsInChildren<Canvas>(true)).Count(value => value.renderMode == RenderMode.ScreenSpaceOverlay), Is.EqualTo(1));
                Assert.That(roots.Count(value => value.name == W1NextCandidateAuthoring.CandidateStatusRootName), Is.EqualTo(1));
                Assert.That(cells.Select(value => value.IsolatedLayer).Distinct().Count(), Is.EqualTo(11));
                foreach (var cell in cells)
                {
                    Assert.That(cell.EffectAndLabelAreDisjoint, Is.True, cell.Label);
                    Assert.That(cell.UsesExclusiveCullingMask, Is.True, cell.Label);
                    Assert.That(cell.EffectCamera.rect, Is.EqualTo(cell.EffectViewport));
                    Assert.That(cell.RuntimeEntry.transform.localScale, Is.EqualTo(Vector3.one * W1NextCandidateAuthoring.PreviewEntryScale));
                    Assert.That(cell.RuntimeEntry.DeclaredLocalBounds.size, Is.EqualTo(W1NextCandidateRuntimeEntry.UniformLocalEnvelope.size));
                    Assert.That(cell.RuntimeEntry.GetComponentsInChildren<Transform>(true).All(value => value.gameObject.layer == cell.IsolatedLayer), Is.True, cell.Label);
                    Assert.That(FindNamed(roots, "CellLabelSafeBand_" + cell.CellIndex.ToString("00")), Is.Not.Null, cell.Label);
                }
                for (var left = 0; left < cells.Length; left++)
                {
                    for (var right = left + 1; right < cells.Length; right++)
                        Assert.That(cells[left].EffectViewport.Overlaps(cells[right].EffectViewport), Is.False, cells[left].Label + " / " + cells[right].Label);
                    for (var label = 0; label < cells.Length; label++)
                        Assert.That(cells[left].EffectViewport.Overlaps(cells[label].LabelViewport), Is.False, cells[left].Label + " effect / " + cells[label].Label + " label");
                }
                Assert.That(FindNamed(roots, "Cell_12_BoundsLegend"), Is.Not.Null);
                var driver = roots.SelectMany(value => value.GetComponentsInChildren<W1NextCandidatePreviewDriver>(true)).Single();
                Assert.That(driver.ConfiguredEntryCount, Is.EqualTo(11));
                Assert.That(driver.CompilerVersion, Is.EqualTo(W1NextCandidateAuthoring.CompilerVersion));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static Transform FindNamed(IEnumerable<GameObject> roots, string name)
        {
            return roots.SelectMany(value => value.GetComponentsInChildren<Transform>(true)).FirstOrDefault(value => value.name == name);
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
