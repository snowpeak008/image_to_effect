using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Capabilities;

namespace VFXComposer.Tests.EditMode
{
    public sealed class BeamCapabilityPreviewSceneTests
    {
        [Test]
        public void Wc2OnlyNextCandidate_HasEightBoundedCellsRuntimeInputsAndVisualPendingState()
        {
            CapabilityAdditionalPreviewScenes.BuildBeamForBatch();
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(CapabilityAdditionalPreviewScenes.BeamScenePath), Is.Not.Null);
            Assert.That(EditorBuildSettings.scenes, Has.Some.Matches<EditorBuildSettingsScene>(value => value.path == CapabilityAdditionalPreviewScenes.BeamScenePath && value.enabled));

            var status = GameObject.Find(CapabilityAdditionalPreviewScenes.BeamCandidateStatusRootName);
            Assert.That(status, Is.Not.Null);
            var statusText = status.GetComponent<TextMesh>().text;
            Assert.That(statusText, Does.Contain("NEXT CANDIDATE"));
            Assert.That(statusText, Does.Contain("VISUAL SIGN-OFF PENDING"));
            Assert.That(statusText.ToUpperInvariant(), Does.Not.Contain("ACCEPTED"));

            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            var cells = roots.Where(value => value.name.StartsWith("Cell_", StringComparison.Ordinal)).OrderBy(value => value.name, StringComparer.Ordinal).ToArray();
            Assert.That(cells.Length, Is.EqualTo(8));
            var entries = cells.SelectMany(value => value.GetComponentsInChildren<CapabilityBlankVfxController>(true)).ToArray();
            Assert.That(entries.Length, Is.EqualTo(8));
            Assert.That(entries.All(value => value.BeamVisual != null), Is.True);
            Assert.That(entries.All(value => value.transform.localScale == Vector3.one * CapabilityAdditionalPreviewScenes.BeamEntryScale), Is.True);
            Assert.That(entries.All(value => Mathf.Abs(value.transform.localPosition.x - CapabilityAdditionalPreviewScenes.BeamEntryOffsetX) < .0001f), Is.True);
            Assert.That(entries.All(value => Mathf.Abs(value.transform.localPosition.y - CapabilityAdditionalPreviewScenes.BeamEntryOffsetY) < .0001f), Is.True);
            Assert.That(cells.All(value => Mathf.Abs(value.transform.Find("Label").localPosition.y - CapabilityAdditionalPreviewScenes.BeamLabelY) < .0001f), Is.True);

            var drivers = roots.SelectMany(value => value.GetComponentsInChildren<BeamCapabilityPreviewDriver>(true)).ToArray();
            Assert.That(drivers.Length, Is.EqualTo(1));
            Assert.That(entries.All(value => value.GetComponentInChildren<BeamCapabilityPreviewDriver>(true) == null), Is.True, "Preview inputs never enter formal Runtime Entries");
            Assert.That(roots.SelectMany(value => value.GetComponentsInChildren<ValidationGalleryPlaybackDriver>(true)).Any(), Is.False, "W-C2 uses its explicit endpoint/obstacle input scheduler");

            var blocker = cells[5].transform.Find("MovableOcclusionBlocker");
            var probe = cells[5].GetComponentInChildren<BeamCapabilityObstacleProbe>(true);
            Assert.That(blocker, Is.Not.Null, "Preview owns a movable blocker");
            Assert.That(blocker.GetComponent<Collider>(), Is.Not.Null);
            Assert.That(probe, Is.Not.Null);
            Assert.That(probe.IsConfigured, Is.True);
            Assert.That(cells[1].transform.Find("SustainedSourceAnchor"), Is.Not.Null);
            Assert.That(cells[1].transform.Find("SustainedMovingTargetAnchor"), Is.Not.Null);

            Assert.That(CapabilityAdditionalPreviewScenes.BeamEntryOffsetX + CapabilityAdditionalPreviewScenes.BeamEntryScale * 4f, Is.LessThan(CapabilityAdditionalPreviewScenes.BeamCellWidth * .5f), "four-unit endpoint remains inside the cell");
            Assert.That(CapabilityAdditionalPreviewScenes.BeamEntryOffsetX - CapabilityAdditionalPreviewScenes.BeamEntryScale, Is.GreaterThan(-CapabilityAdditionalPreviewScenes.BeamCellWidth * .5f), "converge ring source remains inside the cell");
            Assert.That(CapabilityAdditionalPreviewScenes.BeamEntryOffsetY + CapabilityAdditionalPreviewScenes.BeamEntryScale * 4f, Is.LessThan(CapabilityAdditionalPreviewScenes.BeamCellHeight * .5f), "sweep arc remains inside its row");
            var camera = roots.SelectMany(value => value.GetComponentsInChildren<Camera>(true)).Single();
            Assert.That(camera.orthographicSize * (16f / 9f), Is.GreaterThan(1.5f * CapabilityAdditionalPreviewScenes.BeamCellWidth + CapabilityAdditionalPreviewScenes.BeamCellWidth * .5f), "16:9 camera contains outer cell bounds");

            var absolute = Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, CapabilityAdditionalPreviewScenes.BeamScenePath.Replace('/', Path.DirectorySeparatorChar)));
            var yaml = File.ReadAllText(absolute);
            Assert.That(yaml, Does.Contain("m_Name: " + CapabilityAdditionalPreviewScenes.BeamCandidateStatusRootName));
            Assert.That(yaml.ToUpperInvariant(), Does.Not.Contain("ACCEPTED"));
        }
    }
}
