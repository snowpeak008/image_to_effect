#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VFXComposer.Spike.S12.Tests
{
    public sealed class S12SlashSpikeEditorTests
    {
        [Test]
        public void IsolatedSpike_HasFiniteNonDegenerateMeshesUrpMaterialsAndSeparatedParticles()
        {
            foreach (var path in AssetDatabase.FindAssets("t:Mesh", new[] { S12SlashSpikeAuthoring.MeshRoot }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); Assert.That(mesh.vertexCount, Is.GreaterThan(0), path); Assert.That(mesh.triangles.Length, Is.GreaterThan(0)); Assert.That(mesh.triangles.Length % 3, Is.EqualTo(0));
                foreach (var vertex in mesh.vertices) Assert.That(float.IsNaN(vertex.x) || float.IsInfinity(vertex.x) || float.IsNaN(vertex.y) || float.IsInfinity(vertex.y) || float.IsNaN(vertex.z) || float.IsInfinity(vertex.z), Is.False, path);
                for (var i = 0; i < mesh.triangles.Length; i += 3) Assert.That(Vector3.Cross(mesh.vertices[mesh.triangles[i + 1]] - mesh.vertices[mesh.triangles[i]], mesh.vertices[mesh.triangles[i + 2]] - mesh.vertices[mesh.triangles[i]]).sqrMagnitude, Is.GreaterThan(0.0000001f), path + " has a degenerate triangle.");
                Assert.That(mesh.bounds.size.sqrMagnitude, Is.GreaterThan(.0001f), path);
            }
            foreach (var path in AssetDatabase.FindAssets("t:Material", new[] { S12SlashSpikeAuthoring.MaterialRoot }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path); Assert.That(material.shader, Is.Not.Null, path); Assert.That(material.shader.name, Does.Contain("Universal Render Pipeline")); Assert.That(material.renderQueue, Is.GreaterThanOrEqualTo(3000));
            }
            var scene = EditorSceneManager.OpenScene(S12SlashSpikeAuthoring.ScenePath, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().Single(item => item.name == "S12_SlashSpikeRoot");
            var phaseNames = new[] { "anticipation", "primary_arc", "afterimage", "sparks", "dissipation" };
            foreach (var phaseName in phaseNames) { var phase = root.transform.Find(phaseName); Assert.That(phase, Is.Not.Null, phaseName + " must exist as an independent root."); Assert.That(phase.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0), phaseName + " must be non-empty."); }
            S12SlashSpikeAuthoring.ApplyTime(root, .16f);
            var sparkParticles = root.transform.Find("sparks/SeparatedDiamondSparks").GetComponent<ParticleSystem>(); Assert.That(sparkParticles.particleCount, Is.GreaterThanOrEqualTo(8), "Live sparks must be emitted after primary/afterimage overlap is applied.");
            var sparkBounds = sparkParticles.GetComponent<ParticleSystemRenderer>().localBounds; Assert.That(sparkBounds.size.x, Is.GreaterThan(1.4f)); Assert.That(sparkBounds.size.y, Is.GreaterThan(1.2f), "Spark local bounds must cover the emitted separated point positions.");
            S12SlashSpikeAuthoring.ApplyTime(root, .24f);
            var dissipationParticles = root.transform.Find("dissipation/DissipationMotes").GetComponent<ParticleSystem>(); Assert.That(dissipationParticles.particleCount, Is.GreaterThanOrEqualTo(5), "Live dissipation motes must be emitted after their phase is applied.");
            var dissipationBounds = dissipationParticles.GetComponent<ParticleSystemRenderer>().localBounds; Assert.That(dissipationBounds.size.x, Is.GreaterThan(1.2f)); Assert.That(dissipationBounds.size.y, Is.GreaterThan(.5f), "Dissipation bounds must cover its actual mote positions.");
            var positions = root.transform.Find("sparks").Cast<Transform>().Where(item => item.name.StartsWith("Spark_", StringComparison.Ordinal)).Select(item => item.position).ToArray(); Assert.That(positions.Length, Is.GreaterThanOrEqualTo(8)); Assert.That(positions.Distinct().Count(), Is.GreaterThan(4), "Sparks must be spatially separated.");
        }

        [Test]
        public void Evidence_HasFiveDistinctViewsBackgroundCoverageTimelineAndAccurateHashes()
        {
            var root = S12SlashSpikeAuthoring.EvidencePath; var metadataPath = Path.Combine(root, "metadata.json"); Assert.That(File.Exists(metadataPath), Is.True);
            var metadata = JObject.Parse(File.ReadAllText(metadataPath)); StringAssert.Contains("hidden-graphics-device batch Camera.Render", (string)metadata["capture"]); StringAssert.Contains("Bloom disabled", (string)metadata["capture"]);
            var views = (JArray)metadata["views"]; Assert.That(views.Count, Is.EqualTo(5)); CollectionAssert.AreEquivalent(new[] { "front.png", "side.png", "oblique_top.png", "close.png", "game_distance.png" }, views.Select(v => (string)v["file"])); Assert.That(views.Select(v => (string)v["background"]).Distinct().Count(), Is.GreaterThanOrEqualTo(3)); Assert.That(views.Select(v => (string)v["sha256"]).Distinct().Count(), Is.EqualTo(5));
            // Standard review cameras must not use a narrow FOV to make distant readability look better than a third-person presentation.
            foreach (var view in views) Assert.That((float)view["fov"], Is.InRange(55f, 65f), (string)view["file"] + " must retain the reviewed third-person FOV range.");
            var side = views.Single(view => (string)view["file"] == "side.png"); var sidePosition = (JArray)side["position"]; var sideTarget = (JArray)side["target"]; var sideX = Mathf.Abs((float)sidePosition[0] - (float)sideTarget[0]); var sideZ = Mathf.Abs((float)sidePosition[2] - (float)sideTarget[2]); Assert.That(sideX, Is.GreaterThan(4f), "Side witness needs a meaningful +X offset."); Assert.That(sideZ, Is.LessThanOrEqualTo(sideX * .15f), "Side witness must be near the +X axis, not a 45-degree oblique.");
            foreach (var view in views) Assert.That(S12SlashSpikeAuthoring.Hash(Path.Combine(root, (string)view["file"])), Is.EqualTo((string)view["sha256"]));
            var times = (JArray)metadata["timelineFrames"]; CollectionAssert.AreEquivalent(new[] { "anticipation", "primary", "afterimage", "dissipation" }, times.Select(v => (string)v["phase"])); Assert.That(times.Select(v => (string)v["sha256"]).Distinct().Count(), Is.EqualTo(4)); foreach (var time in times) Assert.That(S12SlashSpikeAuthoring.Hash(Path.Combine(root, (string)time["file"])), Is.EqualTo((string)time["sha256"]));
            var anticipation = ReadFrameStats(root, "time_anticipation.png"); var primary = ReadFrameStats(root, "time_primary.png"); var afterimage = ReadFrameStats(root, "time_afterimage.png"); var dissipation = ReadFrameStats(root, "time_dissipation.png");
            AssertFrameReadable(anticipation); AssertFrameReadable(primary); AssertFrameReadable(afterimage); AssertFrameReadable(dissipation);
            TestContext.WriteLine("S12 frame pixels: anticipation=" + anticipation + ", primary=" + primary + ", afterimage=" + afterimage + ", dissipation=" + dissipation);
            Assert.That(primary.WarmPixels, Is.GreaterThan(afterimage.WarmPixels), "Primary sweep must cover more warm color than its residual afterimage."); Assert.That(afterimage.WarmPixels, Is.GreaterThan(dissipation.WarmPixels), "Independent afterimage must remain more substantial than dissipating motes."); Assert.That(anticipation.WarmPixels, Is.LessThan(primary.WarmPixels), "Short anticipation cannot dominate the primary sweep.");
            Assert.That(((JArray)metadata["meshes"]).Count, Is.GreaterThanOrEqualTo(6)); Assert.That(((JArray)metadata["materials"]).Count, Is.GreaterThanOrEqualTo(7)); Assert.That(((JArray)metadata["materials"]).Select(v => (int)v["renderQueue"]).Distinct().Count(), Is.GreaterThanOrEqualTo(6)); Assert.That(((JArray)metadata["rendererBounds"]).Count, Is.GreaterThanOrEqualTo(15)); Assert.That(((JArray)metadata["rendererBounds"]).All(v => ((JObject)v["bounds"])["size"].Values<float>().Any(size => size > 0f)), Is.True);
        }

        private static FrameStats ReadFrameStats(string root, string file)
        {
            var bytes = File.ReadAllBytes(Path.Combine(root, file)); var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false); try { Assert.That(texture.LoadImage(bytes), Is.True, file); var visible = 0; var warm = 0; var red = 0; foreach (var color in texture.GetPixels32()) { var delta = Mathf.Abs(color.r - 41) + Mathf.Abs(color.g - 43) + Mathf.Abs(color.b - 48); if (delta > 28) visible++; if (color.r > 80 && color.r > color.g + 16 && color.r > color.b + 10) warm++; if (color.r > color.g + 24 && color.r > color.b + 24) red++; } return new FrameStats { File = file, VisiblePixels = visible, WarmPixels = warm, RedPixels = red }; } finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static void AssertFrameReadable(FrameStats frame)
        {
            Assert.That(frame.VisiblePixels, Is.GreaterThan(0), frame.File + " must not be an empty/background-only phase."); Assert.That(frame.WarmPixels, Is.GreaterThan(0), frame.File + " must contain its visible warm/red VFX layer."); Assert.That(frame.RedPixels, Is.GreaterThan(0), frame.File + " must retain red/orange phase color.");
        }

        private struct FrameStats
        {
            public string File; public int VisiblePixels; public int WarmPixels; public int RedPixels;
            public override string ToString() { return "visible=" + VisiblePixels + ", warm=" + WarmPixels + ", red=" + RedPixels; }
        }
    }
}
#endif
