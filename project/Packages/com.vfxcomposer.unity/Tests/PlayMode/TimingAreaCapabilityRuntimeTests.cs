using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class TimingAreaCapabilityRuntimeTests
    {
        [UnityTest]
        public IEnumerator TenTimingAreaBlanks_PlayAndChannelExposesBothRuntimeExits()
        {
            var ids = TimingIds();
            foreach (var id in ids)
            {
                GameObject prefab = null;
#if UNITY_EDITOR
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/" + id + "/VFX_" + id + ".prefab");
#endif
                Assert.That(prefab, Is.Not.Null, id);
                var instance = Object.Instantiate(prefab);
                try
                {
                    var controller = instance.GetComponent<CapabilityBlankVfxController>(); Assert.That(controller, Is.Not.Null, id);
                    Assert.That(controller.TimingAreaVisual, Is.Not.Null, id);
                    controller.Play(); yield return null;
                    Assert.That(controller.IsAlive, Is.True, id);
                    Assert.That(controller.Trace, Is.Not.Null, id);
                    // This is only the common launch-frame smoke assertion. Expand-ring is
                    // contractually radius zero at t=0, so its boundary and edge carriers must
                    // remain hidden until a non-zero sample; the dedicated spatial test below
                    // verifies both at t=.72 without fabricating launch-frame geometry.
                    Assert.That(controller.TimingAreaVisual.AllVisualsHidden, Is.False, id);
                    Assert.That(instance.GetComponentsInChildren<Renderer>(true).Count(value => value.enabled), Is.GreaterThanOrEqualTo(1), id);
                    controller.Complete(); Assert.That(controller.LastExit, Is.EqualTo("complete"), id); Assert.That(controller.IsAlive, Is.False, id);
                    controller.Play(); controller.Cancel(); Assert.That(controller.LastExit, Is.EqualTo("cancel"), id); Assert.That(controller.IsAlive, Is.False, id);
                    controller.ResetForPool(); Assert.That(controller.LastExit, Is.Empty, id);
                }
                finally { Object.Destroy(instance); }
            }
        }

        [UnityTest]
        public IEnumerator TelegraphFuseTickAndChain_ExecuteRealVisibleSlotBatches()
        {
            var telegraph = Instantiate("cap_telegraph_impact_3d");
            var fuse = Instantiate("cap_delayfuse_impact_2d");
            var tick = Instantiate("cap_tickpulse_area_2d");
            var chain = Instantiate("cap_chainseq_impact_2d");
            try
            {
                telegraph.Controller.Play();
                telegraph.Controller.EvaluateVisualAtTime(.82f);
                Assert.That(telegraph.Visual.TelegraphShape, Is.EqualTo("circle"));
                Assert.That(telegraph.Visual.TelegraphFill, Is.EqualTo(1f).Within(.001f));
                Assert.That(telegraph.Visual.ImpactSlotVisible, Is.True);
                Assert.That(telegraph.Visual.VisualSlotExecutionCount, Is.EqualTo(1));
                AssertPhysicalSlotBatch(telegraph, 1);

                fuse.Controller.Play();
                fuse.Controller.EvaluateVisualAtTime(.12f);
                var earlyFrequency = fuse.Visual.FuseBlinkFrequency;
                fuse.Controller.EvaluateVisualAtTime(.92f);
                Assert.That(fuse.Visual.FuseBlinkFrequency, Is.GreaterThan(earlyFrequency + 4f));
                fuse.Controller.EvaluateVisualAtTime(1.02f);
                Assert.That(fuse.Visual.ImpactSlotVisible, Is.True);
                Assert.That(fuse.Visual.VisualSlotExecutionCount, Is.EqualTo(1));
                AssertPhysicalSlotBatch(fuse, 1);

                tick.Controller.Play();
                tick.Controller.EvaluateVisualAtTime(1.62f);
                Assert.That(tick.Visual.TickExecutionCount, Is.EqualTo(4));
                Assert.That(tick.Visual.VisualSlotExecutionCount, Is.EqualTo(4));
                Assert.That(tick.Visual.LastSlotSequence, Is.EqualTo(4));
                AssertPhysicalSlotBatch(tick, 1);

                chain.Controller.Play();
                chain.Controller.EvaluateVisualAtTime(1.02f);
                Assert.That(chain.Visual.SequenceExecutionCount, Is.EqualTo(5));
                Assert.That(chain.Visual.UniqueSequencePositionCount, Is.EqualTo(5));
                Assert.That(chain.Visual.VisualSlotExecutionCount, Is.EqualTo(5));
                Assert.That(chain.Visual.LastSlotSequence, Is.EqualTo(5));
                Assert.That(Enumerable.Range(0, 5).Select(chain.Visual.GetSequencePosition).Distinct().Count(), Is.EqualTo(5));
                AssertPhysicalSlotBatch(chain, 1);
                yield return null;
            }
            finally
            {
                Destroy(telegraph, fuse, tick, chain);
            }
        }

        [UnityTest]
        public IEnumerator ChargeAndChannel_ExposeThreeTiersAndDistinctRealExitVisuals()
        {
            var charge = Instantiate("cap_charge_release_2d");
            var channel = Instantiate("cap_channel_3d");
            try
            {
                charge.Controller.Play();
                charge.Controller.EvaluateVisualAtTime(.2f);
                Assert.That(charge.Visual.ChargeTier, Is.EqualTo(1));
                Assert.That(charge.Visual.VisualDensity, Is.EqualTo(4));
                charge.Controller.EvaluateVisualAtTime(.5f);
                Assert.That(charge.Visual.ChargeTier, Is.EqualTo(2));
                Assert.That(charge.Visual.VisualDensity, Is.EqualTo(8));
                charge.Controller.EvaluateVisualAtTime(.95f);
                Assert.That(charge.Visual.ChargeTier, Is.EqualTo(3));
                Assert.That(charge.Visual.VisualDensity, Is.EqualTo(12));
                Assert.That(charge.Visual.FullChargePromptVisible, Is.True);
                AssertPhysicalSlotBatch(charge, 12);
                charge.Controller.Complete();
                Assert.That(charge.Visual.ExitVisual, Is.EqualTo("charge_release_tier_3"));
                Assert.That(charge.Visual.ReleaseTier, Is.EqualTo(3));
                AssertPhysicalSlotBatch(charge, 1);
                charge.Visual.EvaluateExitAtTime(.33f);
                Assert.That(charge.Visual.ExitVisual, Is.Empty);
                Assert.That(charge.Visual.AllVisualsHidden, Is.True);

                charge.Controller.ResetForPool();
                charge.Controller.Play();
                charge.Controller.EvaluateVisualAtTime(.5f);
                charge.Controller.Cancel();
                Assert.That(charge.Visual.ExitVisual, Is.EqualTo("charge_cancel"));
                Assert.That(charge.Visual.ReleaseTier, Is.EqualTo(2));
                AssertPhysicalSlotBatch(charge, 1);

                channel.Controller.Play();
                channel.Controller.EvaluateVisualAtTime(1f);
                Assert.That(channel.Visual.Progress, Is.EqualTo(.5f).Within(.02f));
                channel.Controller.Complete();
                Assert.That(channel.Visual.ExitVisual, Is.EqualTo("channel_converge"));
                AssertPhysicalSlotBatch(channel, 1);
                channel.Controller.ResetForPool();
                channel.Controller.Play();
                channel.Controller.EvaluateVisualAtTime(.7f);
                channel.Controller.Cancel();
                Assert.That(channel.Visual.ExitVisual, Is.EqualTo("channel_scatter"));
                AssertPhysicalSlotBatch(channel, 1);
                yield return null;
            }
            finally
            {
                Destroy(charge, channel);
            }
        }

        [UnityTest]
        public IEnumerator ExpandImplodeMovingAndGrowth_HaveBoundedDistinctPhysicalExecution()
        {
            var expand = Instantiate("cap_expand_area_3d");
            var implode = Instantiate("cap_implode_area_3d");
            var moving = Instantiate("cap_movingzone_area_3d");
            var growth = Instantiate("cap_growth_area_2d");
            try
            {
                expand.Controller.Play();
                expand.Controller.EvaluateVisualAtTime(.72f);
                Assert.That(expand.Visual.BoundaryRadius, Is.GreaterThan(2f));
                Assert.That(expand.Visual.BoundaryRadius, Is.LessThanOrEqualTo(4f));
                Assert.That(expand.Visual.EdgeHitLayerVisible, Is.True);
                AssertPhysicalSlotBatch(expand, 12);

                implode.Controller.Play();
                implode.Controller.EvaluateVisualAtTime(1.05f);
                Assert.That(implode.Visual.BreathHoldVisible, Is.True);
                Assert.That(implode.Visual.ImplodeBurstVisible, Is.False);
                implode.Controller.EvaluateVisualAtTime(1.12f);
                Assert.That(implode.Visual.BreathHoldVisible, Is.False);
                Assert.That(implode.Visual.ImplodeBurstVisible, Is.True);
                AssertPhysicalSlotBatch(implode, 12);

                var localPath = new[] { new Vector3(-3.4f, -.35f, 0f), new Vector3(-1.1f, .55f, 0f), new Vector3(1.05f, -.45f, 0f), new Vector3(3.45f, .3f, 0f) };
                moving.Visual.SetExternalPath(localPath);
                moving.Controller.Play();
                moving.Controller.EvaluateVisualAtTime(1f);
                Assert.That(moving.Visual.UsesExternalPath, Is.True);
                Assert.That(Mathf.Abs(moving.Visual.ZoneCenter.x), Is.LessThan(3.5f));
                Assert.That(Mathf.Abs(moving.Visual.ZoneCenter.y), Is.LessThan(.6f));
                var movingFrame = moving.Controller.Trace.Frames[Mathf.Clamp(Mathf.RoundToInt(60f), 0, moving.Controller.Trace.Frames.Count - 1)];
                Assert.That(moving.Visual.ZoneCenter, Is.EqualTo(movingFrame.Position), "visual zone center is sampler-authoritative, not a preview-only path evaluator");
                Assert.That(moving.Visual.ResidueCount, Is.GreaterThanOrEqualTo(4));
                Assert.That(moving.Visual.VisualSlotExecutionCount, Is.EqualTo(moving.Visual.ResidueCount));
                AssertPhysicalSlotBatch(moving, moving.Visual.ResidueCount);
                moving.Controller.Complete();
                Assert.That(moving.Visual.ExitVisual, Is.EqualTo("zone_complete"));
                AssertPhysicalSlotBatch(moving, 1);
                moving.Controller.ResetForPool();
                Assert.That(moving.Visual.UsesExternalPath, Is.False, "pool reset clears external-owner input");
                moving.Visual.SetExternalPath(localPath);
                moving.Controller.Play();
                moving.Controller.EvaluateVisualAtTime(.8f);
                moving.Controller.Cancel();
                Assert.That(moving.Visual.ExitVisual, Is.EqualTo("zone_cancel"));
                AssertPhysicalSlotBatch(moving, 1);

                growth.Controller.Play();
                growth.Controller.EvaluateVisualAtTime(.1f);
                Assert.That(growth.Visual.GrowthStage, Is.EqualTo(1));
                Assert.That(growth.Visual.VisualDensity, Is.EqualTo(6));
                growth.Controller.EvaluateVisualAtTime(.75f);
                Assert.That(growth.Visual.GrowthStage, Is.EqualTo(2));
                Assert.That(growth.Visual.VisualDensity, Is.EqualTo(10));
                growth.Controller.EvaluateVisualAtTime(1.42f);
                Assert.That(growth.Visual.GrowthStage, Is.EqualTo(3));
                Assert.That(growth.Visual.VisualDensity, Is.EqualTo(14));
                Assert.That(growth.Visual.UpgradePulseVisible, Is.True);
                AssertPhysicalSlotBatch(growth, 14);
                yield return null;
            }
            finally
            {
                Destroy(expand, implode, moving, growth);
            }
        }

        [UnityTest]
        public IEnumerator AllTen_StopAndResetClearPhysicalVisualsAndRespectBudget()
        {
            foreach (var id in TimingIds())
            {
                var fixture = Instantiate(id);
                try
                {
                    Assert.That(fixture.Instance.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(5), id);
                    Assert.That(fixture.Instance.GetComponentsInChildren<ParticleSystem>(true).Length, Is.EqualTo(1), id);
                    Assert.That(fixture.Visual.ParticleCapacity, Is.EqualTo(32), id);
                    fixture.Controller.Play();
                    fixture.Controller.EvaluateVisualAtTime(Mathf.Min(.55f, fixture.Controller.Duration * .5f));
                    Assert.That(fixture.Visual.IsExecuting, Is.True, id);
                    fixture.Controller.Stop(VfxStopMode.Immediate);
                    Assert.That(fixture.Visual.IsExecuting, Is.False, id);
                    Assert.That(fixture.Visual.AllVisualsHidden, Is.True, id);
                    Assert.That(fixture.SlotParticles.particleCount, Is.Zero, id);
                    fixture.Controller.ResetForPool();
                    Assert.That(fixture.Visual.AllVisualsHidden, Is.True, id);
                    Assert.That(fixture.Visual.ExitVisual, Is.Empty, id);
                    Assert.That(fixture.SlotParticles.particleCount, Is.Zero, id);
                }
                finally { Destroy(fixture); }
                yield return null;
            }
        }

        private static RuntimeFixture Instantiate(string id)
        {
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/" + id + "/VFX_" + id + ".prefab");
#endif
            Assert.That(prefab, Is.Not.Null, id);
            var instance = Object.Instantiate(prefab);
            var controller = instance.GetComponent<CapabilityBlankVfxController>();
            Assert.That(controller, Is.Not.Null, id);
            Assert.That(controller.TimingAreaVisual, Is.Not.Null, id);
            var particles = instance.GetComponentsInChildren<ParticleSystem>(true).Single(value => value.name.StartsWith("ResolvedVisualSlotBatch_", System.StringComparison.Ordinal));
            return new RuntimeFixture(instance, controller, controller.TimingAreaVisual, particles);
        }

        private static void AssertPhysicalSlotBatch(RuntimeFixture fixture, int minimum)
        {
            Assert.That(fixture.Visual.VisibleSlotCarrierCount, Is.GreaterThanOrEqualTo(minimum), fixture.Instance.name);
            Assert.That(fixture.Visual.VisibleSlotCarrierCount, Is.LessThanOrEqualTo(fixture.Visual.ParticleCapacity), fixture.Instance.name);
            Assert.That(fixture.SlotParticles.particleCount, Is.EqualTo(fixture.Visual.VisibleSlotCarrierCount), fixture.Instance.name);
            Assert.That(fixture.SlotParticles.GetComponent<Renderer>().enabled, Is.True, fixture.Instance.name);
            for (var i = 0; i < fixture.Visual.VisibleSlotCarrierCount; i++)
                Assert.That(fixture.Visual.GetVisibleSlotPosition(i), Is.EqualTo(ReadParticlePosition(fixture.SlotParticles, i)), fixture.Instance.name + " carrier " + i);
        }

        private static Vector3 ReadParticlePosition(ParticleSystem particles, int index)
        {
            var buffer = new ParticleSystem.Particle[TimingAreaCapabilityVisualExecutor.MaxParticleCapacity];
            var count = particles.GetParticles(buffer);
            Assert.That(index, Is.LessThan(count));
            return buffer[index].position;
        }

        private static void Destroy(params RuntimeFixture[] fixtures)
        {
            foreach (var fixture in fixtures)
                if (fixture != null && fixture.Instance != null) Object.Destroy(fixture.Instance);
        }

        private static string[] TimingIds() { return new[] { "cap_telegraph_impact_3d", "cap_delayfuse_impact_2d", "cap_tickpulse_area_2d", "cap_charge_release_2d", "cap_channel_3d", "cap_chainseq_impact_2d", "cap_expand_area_3d", "cap_implode_area_3d", "cap_movingzone_area_3d", "cap_growth_area_2d" }; }

        private sealed class RuntimeFixture
        {
            public readonly GameObject Instance;
            public readonly CapabilityBlankVfxController Controller;
            public readonly TimingAreaCapabilityVisualExecutor Visual;
            public readonly ParticleSystem SlotParticles;
            public RuntimeFixture(GameObject instance, CapabilityBlankVfxController controller, TimingAreaCapabilityVisualExecutor visual, ParticleSystem slotParticles)
            {
                Instance = instance;
                Controller = controller;
                Visual = visual;
                SlotParticles = slotParticles;
            }
        }
    }
}
