using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VFXComposer.Editor.NextCandidates;
using VFXComposer.W17W18NextCandidate;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W17W18NextCandidatePreviewTests
    {
        [Test]
        public void W17Preview_HasTwelveHardClippedCellsThreeButtonSizesAndNoEffectViewportOverlap()
        {
            W17W18NextCandidateAuthoring.BuildW17ForBatch();
            var scene = EditorSceneManager.OpenScene(W17W18NextCandidateAuthoring.W17PreviewScenePath, OpenSceneMode.Single);
            try
            {
                var roots = scene.GetRootGameObjects();
                var cells = roots.SelectMany(value => value.GetComponentsInChildren<W17W18NextCandidateCell>(true)).OrderBy(value => value.CellIndex).ToArray();
                Assert.That(cells.Length, Is.EqualTo(12));
                Assert.That(cells.All(value => value.Family == W17W18PreviewFamily.W17Ui && value.UsesRealHardClip), Is.True);
                Assert.That(cells.Select(value => value.CandidateId).Distinct().Count(), Is.EqualTo(12));
                Assert.That(cells.Select(value => value.UiEntry).Distinct().Count(), Is.EqualTo(12));
                Assert.That(cells.Select(value => value.UiEntry).Count(value => value.Kind == W17UiEffectKind.ButtonPress), Is.EqualTo(3));
                CollectionAssert.AreEquivalent(new[] { new Vector2(92f, 44f), new Vector2(140f, 70f), new Vector2(220f, 92f) }, cells.Select(value => value.UiEntry).Where(value => value.Kind == W17UiEffectKind.ButtonPress).Select(value => value.ButtonRectSize).ToArray());
                Assert.That(roots.SelectMany(value => value.GetComponentsInChildren<Camera>(true)).Count(value => value.CompareTag("MainCamera")), Is.EqualTo(1));
                Assert.That(roots.Count(value => value.name == W17W18NextCandidateAuthoring.W17StatusRootName), Is.EqualTo(1));
                Assert.That(roots.SelectMany(value => value.GetComponentsInChildren<Canvas>(true)).Count(value => value.renderMode == RenderMode.ScreenSpaceOverlay), Is.EqualTo(1));
                for (var left = 0; left < cells.Length; left++)
                    for (var right = left + 1; right < cells.Length; right++)
                        Assert.That(cells[left].NormalizedViewport.Overlaps(cells[right].NormalizedViewport), Is.False, cells[left].CandidateId + " / " + cells[right].CandidateId);
                var driver = roots.SelectMany(value => value.GetComponentsInChildren<W17W18NextCandidatePreviewDriver>(true)).Single();
                Assert.That(driver.CompilerVersion, Is.EqualTo(W17W18NextCandidateAuthoring.CompilerVersion));
                Assert.That(driver.UiEntryCount, Is.EqualTo(12));
                Assert.That(driver.ThemeEntryCount, Is.Zero);
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }

        [Test]
        public void W18Preview_HasFourSimultaneousShaderClippedThemeCellsWithUniquePalettesAndShapes()
        {
            W17W18NextCandidateAuthoring.BuildW18ForBatch();
            var scene = EditorSceneManager.OpenScene(W17W18NextCandidateAuthoring.W18PreviewScenePath, OpenSceneMode.Single);
            try
            {
                var roots = scene.GetRootGameObjects();
                var cells = roots.SelectMany(value => value.GetComponentsInChildren<W17W18NextCandidateCell>(true)).OrderBy(value => value.CellIndex).ToArray();
                Assert.That(cells.Length, Is.EqualTo(4));
                Assert.That(cells.All(value => value.Family == W17W18PreviewFamily.W18Theme && value.UsesRealHardClip), Is.True);
                Assert.That(cells.Select(value => value.ThemeEntry.Theme).Distinct().Count(), Is.EqualTo(4));
                Assert.That(cells.Select(value => value.ThemeEntry.PaletteReference).Distinct().Count(), Is.EqualTo(4));
                Assert.That(cells.Select(value => value.ThemeEntry.ShapeLanguage).Distinct().Count(), Is.EqualTo(4));
                Assert.That(cells.All(value => value.ThemeEntry.UsesHardClipShader()), Is.True);
                Assert.That(roots.Count(value => value.name == W17W18NextCandidateAuthoring.W18StatusRootName), Is.EqualTo(1));
                Assert.That(roots.SelectMany(value => value.GetComponentsInChildren<Camera>(true)).Count(value => value.CompareTag("MainCamera")), Is.EqualTo(1));
                for (var left = 0; left < cells.Length; left++)
                    for (var right = left + 1; right < cells.Length; right++)
                    {
                        Assert.That(cells[left].NormalizedViewport.Overlaps(cells[right].NormalizedViewport), Is.False);
                        Assert.That(cells[left].WorldClipRect.Overlaps(cells[right].WorldClipRect), Is.False);
                    }
                var driver = roots.SelectMany(value => value.GetComponentsInChildren<W17W18NextCandidatePreviewDriver>(true)).Single();
                Assert.That(driver.CompilerVersion, Is.EqualTo(W17W18NextCandidateAuthoring.CompilerVersion));
                Assert.That(driver.UiEntryCount, Is.Zero);
                Assert.That(driver.ThemeEntryCount, Is.EqualTo(4));
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }
    }
}
