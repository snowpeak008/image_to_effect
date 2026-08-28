using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class StyleSpecialNextCandidatePlayModeTests
    {
        [UnityTest]
        public IEnumerator W9RealMaterials_WriteQuantizedOrBleedingObservableState()
        {
            var pixel = Create("pixel_burst_impact_2d_next_candidate");
            var anime = Create("anime_smear_slash_2d_next_candidate");
            var ink = Create("ink_slash_2d_next_candidate");
            pixel.Play(); anime.Play(); ink.Play();
            yield return new WaitForSeconds(.025f);
            var pixelPhaseA = pixel.LastMaterialPhase;
            yield return new WaitForSeconds(.025f);
            var pixelPhaseB = pixel.LastMaterialPhase;
            Assert.That(pixelPhaseB, Is.EqualTo(pixelPhaseA).Within(.0001f), "12fps pixel state must remain stable across adjacent 60fps frames.");
            yield return new WaitForSeconds(.11f);
            Assert.That(pixel.LastMaterialPhase, Is.GreaterThan(pixelPhaseB));
            Assert.That(pixel.MaterialHitCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(anime.LastMaterialPhase * 15f, Is.EqualTo(Mathf.Round(anime.LastMaterialPhase * 15f)).Within(.001f));
            Assert.That(ink.LastMaterialPhase, Is.GreaterThan(0f));
            foreach (var entry in new[] { pixel, anime, ink })
            {
                Assert.That(entry.VisibleRendererCount, Is.GreaterThan(0), entry.EffectId);
                Assert.That(entry.LastMaterialIntensity, Is.GreaterThan(0f), entry.EffectId);
                var renderer = entry.GetComponentsInChildren<Renderer>(true).First(value => value.enabled);
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_Intensity"), Is.GreaterThan(0f), entry.EffectId);
                Assert.That(entry.IsInsideDeclaredEnvelope(.04f), Is.True, entry.EffectId);
                entry.ResetForPool();
                Assert.That(entry.VisibleRendererCount, Is.EqualTo(0), entry.EffectId);
                UnityEngine.Object.Destroy(entry.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator W10Lifecycle_TraversesAnticipationHitSustainAndDissolveWithRealTopology()
        {
            var explosion = Create("real_explosion_impact_3d_next_candidate");
            var ritual = Create("blood_ritual_spawn_3d_next_candidate");
            explosion.Play(); ritual.Play();
            Assert.That(explosion.CurrentPhase, Is.EqualTo(StyleSpecialLifecyclePhase.Anticipation));
            Assert.That(ritual.CurrentPhase, Is.EqualTo(StyleSpecialLifecyclePhase.Anticipation));
            yield return new WaitForSeconds(.5f);
            Assert.That(explosion.MaterialHitCount, Is.EqualTo(1));
            Assert.That(ritual.MaterialHitCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(explosion.PeakVisibleRendererCount, Is.GreaterThanOrEqualTo(5));
            Assert.That(explosion.PhaseTransitionCount, Is.GreaterThanOrEqualTo(2));
            yield return new WaitForSeconds(.3f);
            Assert.That(ritual.MaterialHitCount, Is.EqualTo(1));
            Assert.That(ritual.PeakVisibleRendererCount, Is.GreaterThanOrEqualTo(4));
            yield return new WaitForSeconds(.68f);
            Assert.That(explosion.CurrentPhase, Is.EqualTo(StyleSpecialLifecyclePhase.Dissolve));
            Assert.That(ritual.CurrentPhase, Is.EqualTo(StyleSpecialLifecyclePhase.Sustain).Or.EqualTo(StyleSpecialLifecyclePhase.Dissolve));
            Assert.That(explosion.IsInsideDeclaredEnvelope(.05f), Is.True);
            Assert.That(ritual.IsInsideDeclaredEnvelope(.05f), Is.True);
            explosion.ResetForPool(); ritual.ResetForPool();
            UnityEngine.Object.Destroy(explosion.gameObject); UnityEngine.Object.Destroy(ritual.gameObject);
        }

        [UnityTest]
        public IEnumerator W16SixPairs_AreVisibleCompositionsWithDistinctNewAndVariantSignatures()
        {
            var ids = new[]
            {
                "poly_burst_impact_3d_next_candidate", "boulder_projectile_3d_lowpoly_next_candidate",
                "gem_lance_projectile_3d_next_candidate", "crystal_shield_3d_crystal_next_candidate",
                "candy_pop_impact_2d_next_candidate", "healing_bloom_aura_2d_candy_next_candidate",
                "nebula_orb_projectile_3d_next_candidate", "summoning_portal_2d_cosmic_next_candidate",
                "steam_vent_burst_impact_3d_next_candidate", "volt_shield_3d_steampunk_next_candidate",
                "phantom_wail_area_2d_next_candidate", "spectral_trail_3d_ghost_next_candidate"
            };
            var entries = ids.Select(Create).ToArray();
            foreach (var entry in entries) entry.Play();
            yield return new WaitForSeconds(.38f);
            foreach (var family in entries.GroupBy(value => value.PairFamily, StringComparer.Ordinal))
            {
                var pair = family.ToArray();
                Assert.That(pair.Length, Is.EqualTo(2), family.Key);
                Assert.That(pair.Select(value => value.StyleToken).Distinct().Single(), Is.EqualTo(family.Key));
                CollectionAssert.AreEquivalent(new[] { "new", "variant" }, pair.Select(value => value.PairRole));
                Assert.That(pair.Select(value => value.CombinationSignature).Distinct().Count(), Is.EqualTo(2), family.Key);
                Assert.That(pair.Select(value => value.VisualSignature).Distinct().Count(), Is.EqualTo(2), family.Key);
                foreach (var entry in pair)
                {
                    Assert.That(entry.VisibleRendererCount, Is.GreaterThanOrEqualTo(3), entry.EffectId);
                    Assert.That(entry.LastMaterialIntensity, Is.GreaterThan(0f), entry.EffectId);
                    Assert.That(entry.IsInsideDeclaredEnvelope(.05f), Is.True, entry.EffectId);
                }
            }
            foreach (var entry in entries) { entry.ResetForPool(); UnityEngine.Object.Destroy(entry.gameObject); }
        }

        [UnityTest]
        public IEnumerator PreviewScheduler_EnforcesCleanGapReplayAndFixedBudgets()
        {
            var entries = new[]
            {
                Create("pixel_heal_aura_2d_next_candidate"),
                Create("holo_scan_area_3d_next_candidate"),
                Create("phantom_wail_area_2d_next_candidate")
            };
            var driverObject = new GameObject("StyleSpecialNextCandidatePreviewDriverFixture");
            var driver = driverObject.AddComponent<StyleSpecialNextCandidatePreviewDriver>();
            SetPrivate(driver, "group", StyleSpecialCandidateGroup.W10Style3D);
            SetPrivate(driver, "runtimeEntries", entries);
            SetPrivate(driver, "playDuration", .32f);
            SetPrivate(driver, "cleanGap", .12f);
            yield return null;
            driver.BeginReplay();
            var firstViewpoint = driver.ReviewViewpointIndex;
            yield return new WaitForSeconds(.36f);
            Assert.That(driver.InCleanGap, Is.True);
            Assert.That(entries.All(value => !value.IsAlive && value.VisibleRendererCount == 0), Is.True);
            yield return new WaitForSeconds(.16f);
            Assert.That(driver.InCleanGap, Is.False);
            Assert.That(driver.ReplayCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(driver.ReviewViewpointIndex, Is.Not.EqualTo(firstViewpoint), "W10 Preview must alternate front and oblique review views between replays.");
            foreach (var entry in entries)
            {
                var budget = entry.ReadBudget();
                Assert.That(budget.GameObjects, Is.LessThanOrEqualTo(StyleSpecialNextCandidateRuntimeEntry.MaxGameObjectsBudget), entry.EffectId);
                Assert.That(budget.Renderers, Is.LessThanOrEqualTo(StyleSpecialNextCandidateRuntimeEntry.MaxRenderersBudget), entry.EffectId);
                Assert.That(budget.ParticleSystems, Is.EqualTo(0), entry.EffectId);
                Assert.That(budget.Materials, Is.LessThanOrEqualTo(1), entry.EffectId);
                entry.ResetForPool();
                UnityEngine.Object.Destroy(entry.gameObject);
            }
            UnityEngine.Object.Destroy(driverObject);
        }

        private static StyleSpecialNextCandidateRuntimeEntry Create(string id)
        {
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/" + id + "/VFX_" + id + ".prefab");
#endif
            Assert.That(prefab, Is.Not.Null, id + " must be built with StyleSpecialNextCandidateAuthoring.BuildAllForBatch before PlayMode.");
            var instance = UnityEngine.Object.Instantiate(prefab);
            var entry = instance.GetComponent<StyleSpecialNextCandidateRuntimeEntry>();
            Assert.That(entry, Is.Not.Null, id);
            return entry;
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
    }
}
