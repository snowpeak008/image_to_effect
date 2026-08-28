using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VFXComposer.Tests.EditMode
{
    /// <summary>Guards the painted S15 layers against visual-origin drift across phases.</summary>
    public sealed class S15SlashOriginAnchorTests
    {
        private const string GeneratedPrefab = "Assets/VFX/Generated/slash_3d_stylized/VFX_Slash_3D_Stylized.prefab";

        [Test]
        public void GeneratedPaintedLayers_MapTheSharedTextureAnchorToOneWorldOrigin()
        {
            Assert.That(SlashOriginAnchor.MainTextureUv.x, Is.EqualTo(.166f).Within(.0001f));
            Assert.That(SlashOriginAnchor.MainTextureUv.y, Is.EqualTo(.068f).Within(.0001f));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedPrefab);
            Assert.That(prefab, Is.Not.Null, "Build the formal S15 prefab before validating its origin contract.");
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            try
            {
                var primary = Painted(instance, "PaintedCrescentActionPlane");
                var afterimage = Painted(instance, "PaintedAfterimage");
                var residue = Painted(instance, "PaintedResidue");
                var primaryAnchor = AnchorWorld(primary);
                var afterAnchor = AnchorWorld(afterimage);
                var residueAnchor = AnchorWorld(residue);
                Assert.That(Vector3.Distance(primaryAnchor, afterAnchor), Is.LessThanOrEqualTo(.01f), "Primary and afterimage must share the lower-left ignition anchor.");
                Assert.That(Vector3.Distance(primaryAnchor, residueAnchor), Is.LessThanOrEqualTo(.01f), "Primary and residue must share the lower-left ignition anchor.");
                Assert.That(afterimage.transform.localPosition.x, Is.EqualTo(0f).Within(.0001f));
                Assert.That(afterimage.transform.localPosition.y, Is.EqualTo(0f).Within(.0001f));
                Assert.That(residue.transform.localPosition.x, Is.EqualTo(0f).Within(.0001f));
                Assert.That(residue.transform.localPosition.y, Is.EqualTo(0f).Within(.0001f));
            }
            finally { if (instance != null) UnityEngine.Object.DestroyImmediate(instance); }
        }

        [Test]
        public void CurrentAuthorityRun_RecordsAlignedScreenAnchorsForEveryFrame()
        {
            var root = Path.Combine(Directory.GetParent(Application.dataPath).Parent.FullName, "docs", "stage-notes", "s15-wysiwyg-evidence");
            var run = Directory.GetDirectories(root, "run-*").OrderByDescending(Directory.GetLastWriteTimeUtc).Single();
            var metadata = JObject.Parse(File.ReadAllText(Path.Combine(run, "metadata.json")));
            Assert.That((float)metadata["anchorTextureUv"][0], Is.EqualTo(SlashOriginAnchor.MainTextureUv.x).Within(.0001f));
            Assert.That((float)metadata["anchorTextureUv"][1], Is.EqualTo(SlashOriginAnchor.MainTextureUv.y).Within(.0001f));
            var frames = (JArray)metadata["frames"]; var anchors = (JArray)metadata["anchorReadback"];
            Assert.That(anchors.Count, Is.EqualTo(frames.Count), "Every authority PNG needs its projected anchor record.");
            foreach (var anchor in anchors)
            {
                Assert.That((float)anchor["maxDistancePx"], Is.LessThanOrEqualTo(3f), "Painted phase anchors must remain within three screen pixels.");
                Assert.That(anchor["primary"].Count(), Is.EqualTo(2)); Assert.That(anchor["afterimage"].Count(), Is.EqualTo(2)); Assert.That(anchor["residue"].Count(), Is.EqualTo(2));
            }
        }

        private static MeshFilter Painted(GameObject root, string name)
        {
            var match = root.GetComponentsInChildren<MeshFilter>(true).SingleOrDefault(item => item.name == name);
            Assert.That(match, Is.Not.Null, "Missing generated painted phase: " + name);
            return match;
        }

        private static Vector3 AnchorWorld(MeshFilter filter)
        {
            var mesh = filter.sharedMesh; var vertices = mesh.vertices; var uv = mesh.uv; var triangles = mesh.triangles; var target = SlashOriginAnchor.MainTextureUv;
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = uv[triangles[i]]; var b = uv[triangles[i + 1]]; var c = uv[triangles[i + 2]]; float one, two, three;
                if (!Barycentric(target, a, b, c, out one, out two, out three)) continue;
                if (one < -.0001f || two < -.0001f || three < -.0001f) continue;
                var local = vertices[triangles[i]] * one + vertices[triangles[i + 1]] * two + vertices[triangles[i + 2]] * three;
                return filter.transform.TransformPoint(local);
            }
            Assert.Fail("Anchor UV does not belong to mesh " + mesh.name); return default(Vector3);
        }

        private static bool Barycentric(Vector2 point, Vector2 a, Vector2 b, Vector2 c, out float one, out float two, out float three)
        {
            var denominator = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
            if (Mathf.Abs(denominator) < .000001f) { one = two = three = 0f; return false; }
            one = ((b.y - c.y) * (point.x - c.x) + (c.x - b.x) * (point.y - c.y)) / denominator;
            two = ((c.y - a.y) * (point.x - c.x) + (a.x - c.x) * (point.y - c.y)) / denominator;
            three = 1f - one - two; return true;
        }
    }
}
