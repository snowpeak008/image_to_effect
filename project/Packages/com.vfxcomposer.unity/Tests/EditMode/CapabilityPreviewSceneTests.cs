using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Capabilities;

namespace VFXComposer.Tests.EditMode
{
    public sealed class CapabilityPreviewSceneTests
    {
        [Test]
        public void ThreeCapabilityBatchScenes_AreBuildableButRemainPendingUserVisualSignoff()
        {
            CapabilityProjectilePreviewScene.BuildForBatch();
            CapabilityAdditionalPreviewScenes.BuildBeamForBatch();
            CapabilityAdditionalPreviewScenes.BuildTimingForBatch();
            AssertScene(CapabilityProjectilePreviewScene.ScenePath, 12);
            AssertScene(CapabilityAdditionalPreviewScenes.BeamScenePath, 8);
            AssertScene(CapabilityAdditionalPreviewScenes.TimingScenePath, 10);
        }

        [Test]
        public void ProjectileNextCandidate_IsCellBoundAndExplicitlyVisualPending()
        {
            CapabilityProjectilePreviewScene.BuildForBatch();
            AssertProjectileCandidateContract();
            AssertScene(CapabilityProjectilePreviewScene.ScenePath, 12);
        }

        [Test]
        public void TimingNextCandidate_IsBoundedFourByThreeRuntimeWallAndExplicitlyVisualPending()
        {
            CapabilityAdditionalPreviewScenes.BuildTimingForBatch();
            var status = GameObject.Find(CapabilityAdditionalPreviewScenes.TimingCandidateStatusRootName);
            Assert.That(status, Is.Not.Null);
            Assert.That(status.name, Is.EqualTo("W_C3_NEXT_CANDIDATE_VISUAL_PENDING"));
            Assert.That(status.GetComponent<TextMesh>().text, Does.Contain("NEXT CANDIDATE"));
            Assert.That(status.GetComponent<TextMesh>().text, Does.Contain("VISUAL SIGN-OFF PENDING"));
            Assert.That(status.GetComponent<TextMesh>().text, Does.Not.Contain("ACCEPTED"));

            var cells = Object.FindObjectsOfType<Transform>().Where(value => value.name.StartsWith("Cell_", System.StringComparison.Ordinal)).ToArray();
            Assert.That(cells.Length, Is.EqualTo(CapabilityAdditionalPreviewScenes.TimingColumns * CapabilityAdditionalPreviewScenes.TimingRows));
            for (var i = 0; i < cells.Length; i++)
            {
                var expectedColumn = i % CapabilityAdditionalPreviewScenes.TimingColumns;
                var expectedRow = i / CapabilityAdditionalPreviewScenes.TimingColumns;
                var cell = cells.Single(value => value.name.StartsWith("Cell_" + (i + 1).ToString("00") + "_", System.StringComparison.Ordinal));
                Assert.That(cell.position.x, Is.EqualTo((expectedColumn - 1.5f) * CapabilityAdditionalPreviewScenes.TimingCellWidth).Within(.0001f));
                Assert.That(cell.position.y, Is.EqualTo((1f - expectedRow) * CapabilityAdditionalPreviewScenes.TimingCellHeight).Within(.0001f));
                var boundary = cell.Find("CellBoundary");
                Assert.That(boundary, Is.Not.Null, cell.name);
                var line = boundary.GetComponent<LineRenderer>();
                Assert.That(line, Is.Not.Null, cell.name);
                Assert.That(line.loop, Is.True, cell.name);
                Assert.That(line.positionCount, Is.EqualTo(4), cell.name);
                var label = cell.Find("Label");
                Assert.That(label, Is.Not.Null, cell.name);
                Assert.That(label.localPosition.y, Is.EqualTo(CapabilityAdditionalPreviewScenes.TimingLabelY).Within(.0001f), cell.name);

                if (i >= 10) continue;
                var entry = cell.GetComponentsInChildren<CapabilityBlankVfxController>(true).Single();
                Assert.That(entry.TimingAreaVisual, Is.Not.Null, cell.name);
                Assert.That(entry.transform.localScale, Is.EqualTo(Vector3.one * CapabilityAdditionalPreviewScenes.TimingEntryScale), cell.name);
                Assert.That(entry.transform.localPosition.x, Is.EqualTo(CapabilityAdditionalPreviewScenes.TimingEntryOffsetX).Within(.0001f), cell.name);
                Assert.That(entry.transform.localPosition.y, Is.EqualTo(CapabilityAdditionalPreviewScenes.TimingEntryOffsetY).Within(.0001f), cell.name);
            }

            var halfWidth = CapabilityAdditionalPreviewScenes.TimingCellWidth * .5f;
            var halfHeight = CapabilityAdditionalPreviewScenes.TimingCellHeight * .5f;
            var scaledExtent = CapabilityAdditionalPreviewScenes.TimingEntryScale * CapabilityAdditionalPreviewScenes.TimingContractMaxExtent;
            Assert.That(Mathf.Abs(CapabilityAdditionalPreviewScenes.TimingEntryOffsetX) + scaledExtent, Is.LessThan(halfWidth - .08f), "max radius/path 4 stays within horizontal cell bounds");
            Assert.That(Mathf.Abs(CapabilityAdditionalPreviewScenes.TimingEntryOffsetY) + scaledExtent, Is.LessThan(halfHeight - .08f), "max radius/path 4 stays within vertical cell bounds");
            Assert.That(CapabilityAdditionalPreviewScenes.TimingEntryOffsetY - scaledExtent - CapabilityAdditionalPreviewScenes.TimingLabelY, Is.GreaterThan(.14f), "maximum visual envelope stays clear of label");

            var driver = Object.FindObjectOfType<TimingAreaCapabilityPreviewDriver>();
            Assert.That(driver, Is.Not.Null);
            var serialized = new SerializedObject(driver);
            Assert.That(serialized.FindProperty("runtimeEntries").arraySize, Is.EqualTo(10));
            Assert.That(serialized.FindProperty("chargeEntry").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("channelEntry").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("movingZoneEntry").objectReferenceValue, Is.Not.Null);
            var camera = Object.FindObjectsOfType<Camera>().Single(value => value.name == "VFXPREVIEW_CapTiming_Camera");
            Assert.That(camera.orthographic, Is.True);
            Assert.That(camera.orthographicSize * (16f / 9f), Is.GreaterThan(CapabilityAdditionalPreviewScenes.TimingColumns * CapabilityAdditionalPreviewScenes.TimingCellWidth * .5f + .15f), "16:9 view contains all four columns");
            Assert.That(camera.orthographicSize, Is.GreaterThan(CapabilityAdditionalPreviewScenes.TimingRows * CapabilityAdditionalPreviewScenes.TimingCellHeight * .5f + .15f), "view contains all three rows");
            Assert.That(Mathf.Abs(status.transform.position.y), Is.LessThan(camera.orthographicSize), "candidate status remains on camera");
            AssertScene(CapabilityAdditionalPreviewScenes.TimingScenePath, 10);
        }

        private static void AssertScene(string path, int expectedEntries)
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, path);
            Assert.That(EditorBuildSettings.scenes, Has.Some.Matches<EditorBuildSettingsScene>(value => value.path == path && value.enabled), path);
            var absolute = Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path.Replace('/', Path.DirectorySeparatorChar)));
            var yaml = File.ReadAllText(absolute);
            var count = 0; var offset = 0;
            while ((offset = yaml.IndexOf("m_Name: Cell_", offset, System.StringComparison.Ordinal)) >= 0) { count++; offset += 8; }
            Assert.That(count, Is.GreaterThanOrEqualTo(expectedEntries), path);
        }

        private static void AssertProjectileCandidateContract()
        {
            var status = GameObject.Find(CapabilityProjectilePreviewScene.CandidateStatusRootName);
            Assert.That(status, Is.Not.Null);
            Assert.That(status.GetComponent<TextMesh>().text, Does.Contain("NEXT CANDIDATE"));
            Assert.That(status.GetComponent<TextMesh>().text, Does.Contain("VISUAL SIGN-OFF PENDING"));
            Assert.That(status.GetComponent<TextMesh>().text, Does.Not.Contain("ACCEPTED"));
            for (var i = 1; i <= 12; i++)
            {
                var prefix = "Cell_" + i.ToString("00") + "_";
                var cell = Object.FindObjectsOfType<Transform>().Single(value => value.name.StartsWith(prefix, System.StringComparison.Ordinal));
                var entry = cell.GetComponentsInChildren<CapabilityBlankVfxController>(true).Single();
                Assert.That(entry.transform.localScale, Is.EqualTo(Vector3.one * CapabilityProjectilePreviewScene.EntryScale));
                Assert.That(entry.transform.localPosition.x, Is.EqualTo(CapabilityProjectilePreviewScene.EntryOffsetX).Within(.0001f));
                Assert.That(entry.transform.localPosition.y, Is.EqualTo(CapabilityProjectilePreviewScene.EntryOffsetY).Within(.0001f));
                var label = cell.Find("Label");
                Assert.That(label, Is.Not.Null);
                Assert.That(label.localPosition.y, Is.EqualTo(CapabilityProjectilePreviewScene.LabelY).Within(.0001f));
            }
            Assert.That(CapabilityProjectilePreviewScene.EntryScale * 8f + CapabilityProjectilePreviewScene.EntryOffsetX, Is.LessThan(CapabilityProjectilePreviewScene.CellWidth * .5f), "longest 8-unit projectile path stays inside its cell");
            Assert.That(CapabilityProjectilePreviewScene.EntryOffsetY + CapabilityProjectilePreviewScene.EntryScale * 3f, Is.LessThan(CapabilityProjectilePreviewScene.CellHeight * .5f), "three-unit parabola apex stays inside its row");
            var lowestSplitChild = CapabilityProjectilePreviewScene.EntryOffsetY - Mathf.Sin(40f * Mathf.Deg2Rad) * 4f * CapabilityProjectilePreviewScene.EntryScale;
            Assert.That(lowestSplitChild - CapabilityProjectilePreviewScene.LabelY, Is.GreaterThan(.25f), "split child envelope does not cover its label");
        }
    }
}
