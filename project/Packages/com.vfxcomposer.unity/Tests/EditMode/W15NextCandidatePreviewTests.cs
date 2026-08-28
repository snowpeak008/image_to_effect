using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VFXComposer.Editor.Archetypes;
using VFXComposer.W15NextCandidate;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W15NextCandidatePreviewTests
    {
        [Test]
        public void Preview_HasSixDirectedSemanticComparisonsAndFixedCellBounds()
        {
            W15NextCandidateAuthoring.BuildAll();
            var scene = EditorSceneManager.OpenScene(W15NextCandidateAuthoring.PreviewScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var cells = roots.Where(value => value.name.StartsWith("W15_NEXT_Cell_", StringComparison.Ordinal)).OrderBy(value => value.name, StringComparer.Ordinal).ToArray();
                Assert.That(cells.Length, Is.EqualTo(10));
                Assert.That(roots.SelectMany(value => value.GetComponentsInChildren<Camera>(true)).Count(), Is.EqualTo(1));
                var driver = roots.SelectMany(value => value.GetComponentsInChildren<VFXComposer.W15NextCandidate.NewArchetypePreviewDriver>(true)).Single();
                var serialized = new SerializedObject(driver);
                Assert.That(serialized.FindProperty("entries").arraySize, Is.EqualTo(21), "6 decals + 4 trails + 2 destruction + 2 lifecycle + 2 portal + 5 loot");
                Assert.That(serialized.FindProperty("decalEntries").arraySize, Is.EqualTo(6));
                Assert.That(serialized.FindProperty("decalAnchors").arraySize, Is.EqualTo(6));
                Assert.That(serialized.FindProperty("fastWeaponEntries").arraySize, Is.EqualTo(2));
                Assert.That(serialized.FindProperty("slowWeaponEntries").arraySize, Is.EqualTo(2));
                Assert.That(serialized.FindProperty("deathCharacter").arraySize, Is.GreaterThanOrEqualTo(6));
                Assert.That(serialized.FindProperty("entranceCharacter").arraySize, Is.GreaterThanOrEqualTo(6));
                Assert.That(serialized.FindProperty("lootEntries").arraySize, Is.EqualTo(5));

                Assert.That(cells.SelectMany(value => value.GetComponentsInChildren<Transform>(true)).Count(value => value.name.StartsWith("SurfaceCarrier_", StringComparison.Ordinal)), Is.EqualTo(6));
                Assert.That(cells.SelectMany(value => value.GetComponentsInChildren<Transform>(true)).Count(value => value.name.StartsWith("DecalAnchor_", StringComparison.Ordinal)), Is.EqualTo(6));
                foreach (var cell in cells.Where(value => value.GetComponentsInChildren<Transform>(true).Any(item => item.name == "DecalAnchor_GROUND")))
                {
                    foreach (var suffix in new[] { "GROUND", "WALL", "SLOPE_45" })
                    {
                        var support = cell.GetComponentsInChildren<Transform>(true).Single(value => value.name == "SurfaceCarrier_" + suffix);
                        var anchor = cell.GetComponentsInChildren<Transform>(true).Single(value => value.name == "DecalAnchor_" + suffix);
                        var supportNormal = suffix == "WALL" ? -support.forward : support.up;
                        Assert.That(Vector3.Dot(anchor.forward, supportNormal), Is.GreaterThan(.999f), cell.name + " " + suffix + " surface/anchor orientation");
                    }
                }
                Assert.That(cells.SelectMany(value => value.GetComponentsInChildren<Transform>(true)).Count(value => value.name == "FAST_REAL_SWING"), Is.EqualTo(2));
                Assert.That(cells.SelectMany(value => value.GetComponentsInChildren<Transform>(true)).Count(value => value.name == "SLOW_BELOW_THRESHOLD"), Is.EqualTo(2));
                Assert.That(cells.SelectMany(value => value.GetComponentsInChildren<Transform>(true)).Count(value => value.name.StartsWith("BoundCharacter_", StringComparison.Ordinal)), Is.EqualTo(2));
                Assert.That(cells.SelectMany(value => value.GetComponentsInChildren<Transform>(true)).Any(value => value.name == "ENTRY_INTAKE"), Is.True);
                Assert.That(cells.SelectMany(value => value.GetComponentsInChildren<Transform>(true)).Any(value => value.name == "EXIT_EJECTION"), Is.True);
                var characterRenderers = cells.SelectMany(value => value.GetComponentsInChildren<Transform>(true))
                    .Where(value => value.name.StartsWith("BoundCharacter_", StringComparison.Ordinal))
                    .SelectMany(value => value.GetComponentsInChildren<Renderer>(true)).ToArray();
                Assert.That(characterRenderers.Length, Is.EqualTo(12));
                Assert.That(characterRenderers.All(value => value.sharedMaterial != null && value.sharedMaterial.shader != null && value.sharedMaterial.shader.name == "VFXComposer/W15NextCandidate/CharacterDissolve"), Is.True, "Both visible character models must use the shader that consumes the bound dissolve MPB.");

                var loot = cells.SelectMany(value => value.GetComponentsInChildren<W15NextCandidateController>(true)).Where(value => value.Archetype == W15NextArchetype.Loot).OrderBy(value => value.Rarity).ToArray();
                CollectionAssert.AreEqual(new[] { W15LootGeometry.Circle, W15LootGeometry.Diamond, W15LootGeometry.Hexagon, W15LootGeometry.Crown, W15LootGeometry.Star }, loot.Select(value => value.LootGeometry).ToArray());
                CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, loot.Select(value => value.LootLayerCount).ToArray());
                Assert.That(loot.Select(value => value.LootCadenceHz).Distinct().Count(), Is.EqualTo(5));
                Assert.That(loot.Select(value => value.LootPeakScale).Distinct().Count(), Is.EqualTo(5));

                foreach (var cell in cells)
                {
                    var bounds = cell.GetComponent<BoxCollider>();
                    Assert.That(bounds, Is.Not.Null, cell.name);
                    Assert.That(bounds.size, Is.EqualTo(W15NextCandidateAuthoring.PreviewCellSize));
                    foreach (var entry in cell.GetComponentsInChildren<W15NextCandidateController>(true))
                    {
                        var local = cell.transform.InverseTransformPoint(entry.transform.position);
                        Assert.That(Mathf.Abs(local.x), Is.LessThanOrEqualTo(bounds.size.x * .5f - .05f), cell.name + "/" + entry.name);
                        Assert.That(Mathf.Abs(local.y), Is.LessThanOrEqualTo(bounds.size.y * .5f - .05f), cell.name + "/" + entry.name);
                        Assert.That(Mathf.Abs(local.z), Is.LessThanOrEqualTo(bounds.size.z * .5f), cell.name + "/" + entry.name);
                        if (entry.Archetype == W15NextArchetype.Destruction)
                        {
                            var maxAge = entry.Variant == W15NextVariant.CrystalShatter ? 1.9f : 1.55f;
                            for (var piece = 0; piece < entry.PieceCount; piece++) for (var sample = 0; sample <= 32; sample++)
                            {
                                var worldPiece = entry.transform.TransformPoint(entry.GetDeterministicPiecePosition(piece, sample * maxAge / 32f));
                                var pieceLocal = cell.transform.InverseTransformPoint(worldPiece);
                                Assert.That(Mathf.Abs(pieceLocal.x), Is.LessThanOrEqualTo(bounds.size.x * .5f - .08f), cell.name + " fragment x envelope");
                                Assert.That(pieceLocal.y, Is.InRange(-bounds.size.y * .5f + .08f, bounds.size.y * .5f - .08f), cell.name + " fragment y envelope");
                                Assert.That(Mathf.Abs(pieceLocal.z), Is.LessThanOrEqualTo(bounds.size.z * .5f - .08f), cell.name + " fragment z envelope");
                            }
                        }
                    }
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
