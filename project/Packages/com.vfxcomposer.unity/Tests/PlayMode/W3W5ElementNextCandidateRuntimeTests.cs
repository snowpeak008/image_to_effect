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
    public sealed class W3W5ElementNextCandidateRuntimeTests
    {
        private static readonly string[] FireIds =
        {
            "flame_slash_2d", "fire_nova_burst_3d", "flamethrower_beam_3d", "burning_status_aura_2d",
            "ember_rain_area_3d", "phoenix_dart_projectile_2d", "chain_blast_impact_2d", "fire_shield_3d"
        };

        private static readonly string[] FrostIds =
        {
            "ice_spike_spawn_3d", "blizzard_area_3d", "frost_breath_beam_2d", "ice_shard_projectile_2d",
            "freeze_status_2d", "crystal_shield_3d", "flash_freeze_transform_3d"
        };

        private static readonly string[] LightningIds =
        {
            "thunder_strike_impact_3d", "ball_lightning_projectile_3d", "static_field_area_2d", "storm_charge_aura_3d",
            "electro_slash_2d", "emp_nova_impact_2d", "volt_shield_3d"
        };

        [UnityTest]
        public IEnumerator Fire_ExecutesCombustionEruptionEmbersHeatHazeResidueAndTail()
        {
            var fire = Create("fire_nova_burst_3d");
            fire.Play();
            fire.EvaluateAtTime(.15f);
            Assert.That(fire.Phase, Is.EqualTo(ElementNextCandidatePhase.Eruption));
            Assert.That(fire.FireCombustion, Is.GreaterThan(0f));
            Assert.That(fire.FireEruption, Is.GreaterThan(0f));
            Assert.That(fire.FireHeatHaze, Is.GreaterThan(0f));
            Assert.That(fire.FireEmberCount, Is.GreaterThan(0));
            Assert.That(fire.PrimaryCarrierMultiplicity, Is.EqualTo(12));
            Assert.That(fire.ActiveLayerCount, Is.GreaterThanOrEqualTo(4));

            fire.EvaluateAtTime(.8f);
            Assert.That(fire.Phase, Is.EqualTo(ElementNextCandidatePhase.Residue));
            Assert.That(fire.FireResidue, Is.GreaterThan(0f));
            fire.Stop(VfxStopMode.AllowTail);
            fire.EvaluateTailAtTime(.2f);
            Assert.That(fire.Phase, Is.EqualTo(ElementNextCandidatePhase.Residue));
            Assert.That(fire.FireHeatHaze, Is.GreaterThan(0f));
            fire.EvaluateTailAtTime(.5f);
            Assert.That(fire.IsAlive, Is.False);
            Assert.That(fire.AllVisualsHidden, Is.True);
            var authoredEnd = Create("fire_nova_burst_3d");
            authoredEnd.Play(); authoredEnd.EvaluateAtTime(authoredEnd.Duration); authoredEnd.Stop(VfxStopMode.AllowTail);
            Assert.That(authoredEnd.IsAlive, Is.False, "An already faded one-shot must not reappear as a generic tail.");
            Assert.That(authoredEnd.AllVisualsHidden, Is.True);
            UnityEngine.Object.Destroy(fire.gameObject);
            UnityEngine.Object.Destroy(authoredEnd.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Frost_ExecutesSharpGrowthMistFractureAndAuthoredMeltExit()
        {
            var shatter = Create("ice_spike_spawn_3d");
            shatter.Play();
            shatter.EvaluateAtTime(.26f);
            Assert.That(shatter.Phase, Is.EqualTo(ElementNextCandidatePhase.Growth));
            Assert.That(shatter.IceCrystalGrowth, Is.GreaterThan(0f));
            Assert.That(shatter.IceSharpness, Is.GreaterThan(.5f));
            Assert.That(shatter.IceMistOpacity, Is.GreaterThan(0f));
            Assert.That(shatter.PrimaryCarrierMultiplicity, Is.EqualTo(5));
            shatter.EvaluateAtTime(.9f);
            Assert.That(shatter.Phase, Is.EqualTo(ElementNextCandidatePhase.Fracture));
            Assert.That(shatter.IceFractureCount, Is.EqualTo(10));

            var melt = Create("ice_spike_spawn_3d");
            SetTextParameter(melt, "exit_mode", "sink");
            melt.Play();
            melt.EvaluateAtTime(.95f);
            Assert.That(melt.Phase, Is.EqualTo(ElementNextCandidatePhase.Melt));
            Assert.That(melt.IceMelt, Is.GreaterThan(0f));
            Assert.That(melt.IceFractureCount, Is.Zero);
            shatter.Stop(VfxStopMode.Immediate);
            melt.Stop(VfxStopMode.Immediate);
            UnityEngine.Object.Destroy(shatter.gameObject);
            UnityEngine.Object.Destroy(melt.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Lightning_IsDeterministicallyBranchedDiscreteAndSeparatesDischargeFromAfterglow()
        {
            var first = Create("thunder_strike_impact_3d");
            var replay = Create("thunder_strike_impact_3d");
            first.Play();
            replay.Play();
            first.EvaluateAtTime(.05f);
            replay.EvaluateAtTime(.05f);
            Assert.That(first.Phase, Is.EqualTo(ElementNextCandidatePhase.Discharge));
            Assert.That(first.LightningCharge, Is.EqualTo(1f).Within(.0001f));
            Assert.That(first.LightningFlashOn, Is.True);
            Assert.That(first.LightningForkCount, Is.EqualTo(2));
            Assert.That(first.LightningControlledFlashCount, Is.EqualTo(2));
            Assert.That(first.VisibleArcCount, Is.EqualTo(3));
            AssertArcsEqual(first, replay);
            var stepOnePoint = first.GetArcPoint(0, 1);

            first.EvaluateAtTime(.1f);
            replay.EvaluateAtTime(.1f);
            Assert.That(first.LightningDiscreteStep, Is.GreaterThan(1));
            Assert.That(first.GetArcPoint(0, 1), Is.Not.EqualTo(stepOnePoint), "A later discrete flash must resample the jagged path.");
            AssertArcsEqual(first, replay);
            first.EvaluateAtTime(.3f);
            Assert.That(first.Phase, Is.EqualTo(ElementNextCandidatePhase.Afterglow));
            Assert.That(first.LightningFlashOn, Is.False);
            Assert.That(first.VisibleArcCount, Is.Zero);
            Assert.That(first.LightningAfterglow, Is.GreaterThan(0f));
            first.Stop(VfxStopMode.Immediate);
            replay.Stop(VfxStopMode.Immediate);
            UnityEngine.Object.Destroy(first.gameObject);
            UnityEngine.Object.Destroy(replay.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ContentValuesDriveRealFireFrostAndLightningCarriersInsteadOfPaletteOnly()
        {
            var narrowFire = Create("flame_slash_2d");
            var wideFire = Create("flame_slash_2d");
            SetNumberParameter(narrowFire, "arc_width", .3f);
            SetNumberParameter(wideFire, "arc_width", 1f);
            narrowFire.Play(); wideFire.Play();
            narrowFire.EvaluateAtTime(.2f); wideFire.EvaluateAtTime(.2f);
            Assert.That(wideFire.transform.Find("PrimaryCarrier").localScale.y, Is.GreaterThan(narrowFire.transform.Find("PrimaryCarrier").localScale.y + .5f));

            var shortIce = Create("ice_spike_spawn_3d");
            var tallIce = Create("ice_spike_spawn_3d");
            SetNumberParameter(shortIce, "height", .5f);
            SetNumberParameter(tallIce, "height", 2.5f);
            shortIce.Play(); tallIce.Play();
            shortIce.EvaluateAtTime(.4f); tallIce.EvaluateAtTime(.4f);
            Assert.That(tallIce.transform.Find("PrimaryCarrier").localScale.y, Is.GreaterThan(shortIce.transform.Find("PrimaryCarrier").localScale.y * 4f));

            var trunkOnly = Create("thunder_strike_impact_3d");
            var forked = Create("thunder_strike_impact_3d");
            SetNumberParameter(trunkOnly, "fork_count", 0f);
            SetNumberParameter(forked, "fork_count", 3f);
            trunkOnly.Play(); forked.Play();
            trunkOnly.EvaluateAtTime(.05f); forked.EvaluateAtTime(.05f);
            Assert.That(trunkOnly.VisibleArcCount, Is.EqualTo(1));
            Assert.That(forked.VisibleArcCount, Is.EqualTo(4));
            Assert.That(forked.LightningForkCount, Is.EqualTo(3));

            var fuel = Create("flamethrower_beam_3d");
            SetTextParameter(fuel, "fuel_color", "#00FF00");
            fuel.Play(); fuel.EvaluateAtTime(.3f);
            Assert.That(fuel.FireFuelColor, Is.EqualTo(Color.green));
            AssertRendererPrimary(fuel.transform.Find("PrimaryCarrier").GetComponent<Renderer>(), Color.green);

            var hitFlash = Create("crystal_shield_3d");
            SetTextParameter(hitFlash, "hit_flash_color", "#FF00FF");
            hitFlash.Play(); hitFlash.EvaluateAtTime(2.2f); hitFlash.TriggerLocalEvent(Vector3.right * .25f);
            Assert.That(hitFlash.Phase, Is.EqualTo(ElementNextCandidatePhase.Discharge), "Event-driven hits must still work after multiple duration loops.");
            Assert.That(hitFlash.IceHitFlashColor, Is.EqualTo(Color.magenta));
            AssertRendererPrimary(hitFlash.transform.Find("EventCarrier").GetComponent<Renderer>(), Color.magenta);

            var sparseNet = Create("volt_shield_3d");
            var denseNet = Create("volt_shield_3d");
            SetNumberParameter(sparseNet, "walk_arc_count", 3f);
            SetNumberParameter(denseNet, "walk_arc_count", 8f);
            sparseNet.Play(); denseNet.Play();
            sparseNet.EvaluateAtTime(.2f); denseNet.EvaluateAtTime(.2f);
            Assert.That(sparseNet.PrimaryCarrierMultiplicity, Is.EqualTo(3));
            Assert.That(denseNet.PrimaryCarrierMultiplicity, Is.EqualTo(8));
            Assert.That(denseNet.VisibleArcCount, Is.GreaterThan(sparseNet.VisibleArcCount));
            var counterOrigin = Vector3.right * .4f;
            denseNet.TriggerLocalEvent(counterOrigin);
            Assert.That(denseNet.Phase, Is.EqualTo(ElementNextCandidatePhase.Discharge));
            Assert.That(denseNet.GetArcPoint(4, 0), Is.EqualTo(counterOrigin), "At the five-carrier ceiling the hit counter temporarily replaces one walking arc.");

            foreach (var entry in new[] { narrowFire, wideFire, shortIce, tallIce, trunkOnly, forked, fuel, hitFlash, sparseNet, denseNet })
            {
                entry.Stop(VfxStopMode.Immediate);
                UnityEngine.Object.Destroy(entry.gameObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllTwentyTwoEntries_StayBudgetedCleanAndReplayWithoutAllocatingNewMaterials()
        {
            var entries = FireIds.Concat(FrostIds).Concat(LightningIds).Select(Create).ToArray();
            var materialBindings = entries.ToDictionary(
                value => value,
                value => value.GetComponentsInChildren<Renderer>(true).Select(renderer => renderer.sharedMaterial).ToArray());
            foreach (var entry in entries)
            {
                Assert.That(entry.BudgetWithinLimits, Is.True, entry.EffectId);
                Assert.That(entry.ParameterBindingCount, Is.GreaterThan(0), entry.EffectId);
                entry.Play();
                entry.EvaluateAtTime(.11f);
                Assert.That(entry.VisibleParticleCount, Is.LessThanOrEqualTo(entry.ParticleBudget), entry.EffectId);
                entry.Stop(VfxStopMode.Immediate);
                Assert.That(entry.IsAlive, Is.False, entry.EffectId);
                Assert.That(entry.AllVisualsHidden, Is.True, entry.EffectId);
                entry.Play();
                if (entry.Lifecycle == StyledVfxLifecycle.OneShot) entry.EvaluateAtTime(entry.Duration);
                entry.Stop(VfxStopMode.AllowTail);
                if (entry.IsAlive) entry.EvaluateTailAtTime(.5f);
                Assert.That(entry.IsAlive, Is.False, entry.EffectId);
                Assert.That(entry.AllVisualsHidden, Is.True, entry.EffectId);
                Assert.That(entry.PlayCount, Is.EqualTo(2), entry.EffectId);
                CollectionAssert.AreEqual(materialBindings[entry], entry.GetComponentsInChildren<Renderer>(true).Select(renderer => renderer.sharedMaterial).ToArray(), entry.EffectId);
            }
            foreach (var entry in entries) UnityEngine.Object.Destroy(entry.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PreviewDriver_StopsSustainedEntriesResetsOnDisableAndReplaysCleanly()
        {
            var oneShot = Create("fire_nova_burst_3d");
            var sustainedFire = Create("flamethrower_beam_3d");
            var sustainedLightning = Create("static_field_area_2d");
            var entries = new[] { oneShot, sustainedFire, sustainedLightning };
            var root = new GameObject("W3W5NextPreviewDriverFixture");
            var driver = root.AddComponent<ElementNextCandidatePreviewDriver>();
            SetPrivate(driver, "entries", entries);
            SetPrivate(driver, "replayInterval", .4f);
            SetPrivate(driver, "sustainedStopTime", .05f);
            driver.ReplayNow();
            yield return new WaitForSeconds(.09f);
            Assert.That(driver.SustainedStopped, Is.True);
            Assert.That(oneShot.IsAlive, Is.True);
            Assert.That(sustainedFire.IsStopping || !sustainedFire.IsAlive, Is.True);
            Assert.That(sustainedLightning.IsStopping || !sustainedLightning.IsAlive, Is.True);

            driver.enabled = false;
            Assert.That(entries.All(value => !value.IsAlive && value.AllVisualsHidden), Is.True);
            driver.enabled = true;
            Assert.That(entries.All(value => value.IsAlive), Is.True);
            driver.enabled = false;
            Assert.That(entries.All(value => !value.IsAlive && value.AllVisualsHidden), Is.True);
            UnityEngine.Object.Destroy(root);
            foreach (var entry in entries) UnityEngine.Object.Destroy(entry.gameObject);
            yield return null;
        }

        private static void AssertArcsEqual(ElementNextCandidateVisualExecutor first, ElementNextCandidateVisualExecutor second)
        {
            Assert.That(second.VisibleArcCount, Is.EqualTo(first.VisibleArcCount));
            for (var arc = 0; arc < ElementNextCandidateVisualExecutor.MaxArcCarriers; arc++)
            {
                Assert.That(second.GetArcPointCount(arc), Is.EqualTo(first.GetArcPointCount(arc)));
                for (var point = 0; point < first.GetArcPointCount(arc); point++)
                    Assert.That(second.GetArcPoint(arc, point), Is.EqualTo(first.GetArcPoint(arc, point)));
            }
        }

        private static void AssertRendererPrimary(Renderer renderer, Color expected)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetColor("_PrimaryColor"), Is.EqualTo(expected));
        }

        private static ElementNextCandidateVisualExecutor Create(string id)
        {
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/NextCandidates/W3W5Elements/Generated/" + id + "/VFX_" + id + "_NEXT.prefab");
#endif
            Assert.That(prefab, Is.Not.Null, id + " must be built with ElementNextCandidateAuthoring.BuildW3W5ForBatch before PlayMode.");
            var instance = UnityEngine.Object.Instantiate(prefab);
            var executor = instance.GetComponent<ElementNextCandidateVisualExecutor>();
            Assert.That(executor, Is.Not.Null, id);
            return executor;
        }

        private static void SetNumberParameter(ElementNextCandidateVisualExecutor target, string key, float value)
        {
            var keys = (string[])GetPrivate(target, "contentKeys");
            var values = (float[])GetPrivate(target, "contentValues");
            var index = Array.IndexOf(keys, key);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), target.EffectId + ": " + key);
            values[index] = value;
        }

        private static void SetTextParameter(ElementNextCandidateVisualExecutor target, string key, string value)
        {
            var keys = (string[])GetPrivate(target, "contentTextKeys");
            var values = (string[])GetPrivate(target, "contentTextValues");
            var index = Array.IndexOf(keys, key);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), target.EffectId + ": " + key);
            values[index] = value;
        }

        private static object GetPrivate(object target, string fieldName)
        {
            return target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
    }
}
