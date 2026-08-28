using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer.Capabilities;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Capabilities;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Tests.EditMode
{
    public sealed class TimingAreaCapabilityTests
    {
        [Test]
        public void TelegraphDelayTickAndCharge_HaveReadableProgressAndExactEvents()
        {
            var telegraph = Request(); telegraph.TimingType = "telegraph"; telegraph.Timing["warn_duration"] = .8; telegraph.Duration = 1;
            var telegraphTrace = CapabilitySampler.SampleTrajectory(telegraph);
            Assert.That(telegraphTrace.Frames.First(value => value.Time >= .4f).Progress, Is.EqualTo(.5f).Within(.03f));
            Assert.That(telegraphTrace.Events.Single(value => value.Detail == "telegraph_complete").Time, Is.EqualTo(.8f).Within(telegraph.DeltaTime));

            var fuse = Request(); fuse.TimingType = "delay_fuse"; fuse.Timing["fuse_time"] = 1; fuse.Timing["blink_accelerate"] = 1; fuse.Duration = 1.2f;
            var fuseTrace = CapabilitySampler.SampleTrajectory(fuse);
            Assert.That(fuseTrace.Frames.Last(value => value.Time < 1f).Width, Is.GreaterThan(fuseTrace.Frames.First(value => value.Time > .1f).Width));
            Assert.That(fuseTrace.Events.Single(value => value.Detail == "fuse_complete").Time, Is.EqualTo(1f).Within(fuse.DeltaTime));

            var tick = Request(); tick.TimingType = "tick_pulse"; tick.Timing["tick_interval"] = .25; tick.Duration = 1.05f;
            var tickEvents = CapabilitySampler.SampleTrajectory(tick).Events.Where(value => value.Type == "on_tick").ToArray();
            Assert.That(tickEvents.Length, Is.EqualTo(4));
            Assert.That(tickEvents.Select(value => value.Time), Is.EqualTo(new[] { .25f, .5f, .75f, 1f }).Within(tick.DeltaTime));

            var charge = Request(); charge.TimingType = "charge_release"; charge.Timing["level_1"] = .3; charge.Timing["level_2"] = .7; charge.Timing["per_level_scale"] = 1.7; charge.ReleaseTime = .9f; charge.Duration = 1.1f;
            var chargeTrace = CapabilitySampler.SampleTrajectory(charge);
            Assert.That(chargeTrace.Events.Count(value => value.Type == "on_charge_level"), Is.EqualTo(2));
            Assert.That(chargeTrace.Events.Single(value => value.Detail == "charge_release").Sequence, Is.EqualTo(3));
        }

        [Test]
        public void Channel_ProducesDistinctCompleteAndCancelExits()
        {
            var complete = Request(); complete.TimingType = "channel_interrupt"; complete.Timing["channel_time"] = 1; complete.Duration = 1.2f;
            var completeTrace = CapabilitySampler.SampleTrajectory(complete);
            Assert.That(completeTrace.Events.Count(value => value.Type == "on_complete"), Is.EqualTo(1));
            Assert.That(completeTrace.Events.Count(value => value.Type == "on_cancel"), Is.EqualTo(0));

            var cancel = Request(); cancel.TimingType = "channel_interrupt"; cancel.Timing["channel_time"] = 1; cancel.CancelTime = .45f; cancel.Duration = 1.2f;
            var cancelTrace = CapabilitySampler.SampleTrajectory(cancel);
            Assert.That(cancelTrace.Events.Count(value => value.Type == "on_cancel"), Is.EqualTo(1));
            Assert.That(cancelTrace.Events.Count(value => value.Type == "on_complete"), Is.EqualTo(0));
            Assert.That(cancelTrace.Events.Single(value => value.Type == "on_cancel").Time, Is.EqualTo(.45f).Within(cancel.DeltaTime));
        }

        [Test]
        public void ChainExpandImplodeMovingAndGrowth_FollowSpatialContracts()
        {
            var chain = Request(); chain.TimingType = "chain_sequence"; chain.Timing["count"] = 5; chain.Timing["interval"] = .2; chain.Duration = 1.2f;
            var chainHits = CapabilitySampler.SampleTrajectory(chain).Events.Where(value => value.Type == "on_hit").ToArray();
            Assert.That(chainHits.Length, Is.EqualTo(5));
            Assert.That(chainHits.Select(value => value.Time), Is.EqualTo(new[] { .2f, .4f, .6f, .8f, 1f }).Within(chain.DeltaTime));

            var expand = Request(); expand.MotionType = "expand_ring"; expand.Motion["max_radius"] = 4; expand.Motion["expand_speed"] = 2; expand.Duration = 2.2f;
            var expandTrace = CapabilitySampler.SampleTrajectory(expand);
            Assert.That(expandTrace.Frames.Last().Radius, Is.EqualTo(4f).Within(.01f));
            Assert.That(expandTrace.Events.Single(value => value.Detail == "expanding_edge").Time, Is.EqualTo(1f).Within(expand.DeltaTime));

            var implode = Request(); implode.MotionType = "implode"; implode.Motion["start_radius"] = 4; implode.Motion["collapse_time"] = 1; implode.Duration = 1.3f;
            var implodeTrace = CapabilitySampler.SampleTrajectory(implode);
            var breathFrames = implodeTrace.Frames.Where(value => value.Stage == 1).ToArray();
            Assert.That(breathFrames.Length * implode.DeltaTime, Is.GreaterThanOrEqualTo(.09f));
            Assert.That(implodeTrace.Events.Single(value => value.Detail == "implode_burst").Time, Is.GreaterThanOrEqualTo(1.1f - implode.DeltaTime));

            var moving = Request(); moving.MotionType = "moving_zone"; moving.Motion["follow_lag"] = .2; moving.Target = new Vector3(1, 0, 0); moving.TargetVelocity = Vector3.right; moving.Duration = 1;
            var movingTrace = CapabilitySampler.SampleTrajectory(moving);
            Assert.That(movingTrace.Frames.Last().Position.x, Is.GreaterThan(1f));
            Assert.That(movingTrace.Frames.Last().Position.x, Is.LessThan((moving.Target + moving.TargetVelocity * moving.Duration).x));

            var growth = Request(); growth.MotionType = "growth_stage"; growth.Motion["stage_count"] = 3; growth.Motion["base_radius"] = 1; growth.Duration = 1.5f;
            var growthTrace = CapabilitySampler.SampleTrajectory(growth);
            Assert.That(growthTrace.Events.Where(value => value.Type == "on_stage").Select(value => value.Sequence), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(growthTrace.Frames.Select(value => value.Radius).Distinct(), Is.EqualTo(new[] { 0f, 1f, 2f, 3f }).Within(.001f));
        }

        [Test]
        public void SlotsResolveOneLevelAndCombinationTableRejectsIllegalPairs()
        {
            var catalog = VfxCompiler.LoadFormalCatalog();
            var validPath = Absolute("Assets/VFX/Recipes/Capability/cap_tickpulse_area_2d.default.json");
            Assert.That(RecipeValidator.Validate(File.ReadAllText(validPath), catalog).HasErrors, Is.False);

            var unresolved = Parse("area", new JObject { ["timing"] = new JObject { ["type"] = "tick_pulse", ["tick_interval"] = .5, ["tick_visual_slot"] = "missing_recipe" } });
            Assert.That(CapabilitySlotValidator.Validate(unresolved).Contains("E328", "/behavior/timing/tick_visual_slot"), Is.True);
            var nested = Parse("area", new JObject { ["timing"] = new JObject { ["type"] = "tick_pulse", ["tick_interval"] = .5, ["tick_visual_slot"] = "cap_telegraph_impact_3d" } });
            Assert.That(CapabilitySlotValidator.Validate(nested).Contains("E329", "/behavior/timing/tick_visual_slot"), Is.True);

            Assert.That(CapabilityRegistry.Validate(Parse("impact", new JObject { ["motion"] = new JObject { ["type"] = "expand_ring", ["max_radius"] = 4 }, ["timing"] = new JObject { ["type"] = "telegraph", ["warn_duration"] = .8 } })).HasErrors, Is.False);
            Assert.That(CapabilityRegistry.Validate(Parse("impact", new JObject { ["timing"] = new JObject { ["type"] = "channel_interrupt", ["channel_time"] = 2 } })).Contains("E324", "/behavior/timing/type"), Is.True);
            Assert.That(CapabilityRegistry.Validate(Parse("projectile", new JObject { ["motion"] = new JObject { ["type"] = "moving_zone", ["follow_lag"] = .2 } })).Contains("E324", "/behavior/motion/type"), Is.True);
        }

        [Test]
        public void TenTimingAreaBlanks_AreDeterministicAndBuildIdempotentRuntimeEntries()
        {
            foreach (var request in AllTen()) Assert.That(CapabilitySampler.SampleTrajectory(request).ToCanonicalJson(), Is.EqualTo(CapabilitySampler.SampleTrajectory(request).ToCanonicalJson()));

            CapabilityBlankCompiler.BuildTimingAreaBlanks();
            var ids = TimingIds();
            var hashes = new Dictionary<string, string>(StringComparer.Ordinal); var guids = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                var path = "Assets/VFX/Generated/" + id + "/VFX_" + id + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); Assert.That(prefab, Is.Not.Null, id);
                var entry = prefab.GetComponent<CapabilityBlankVfxController>();
                Assert.That(entry, Is.Not.Null, id);
                Assert.That(entry.TimingAreaVisual, Is.Not.Null, id + " has a dedicated visual executor");
                Assert.That(prefab.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(5), id);
                Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true).Length, Is.EqualTo(1), id);
                Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true).Single().main.maxParticles, Is.EqualTo(TimingAreaCapabilityVisualExecutor.MaxParticleCapacity), id);
                hashes[id] = Sha256(Absolute(path)); guids[id] = AssetDatabase.AssetPathToGUID(path);
            }
            CapabilityBlankCompiler.BuildTimingAreaBlanks();
            foreach (var id in ids)
            {
                var path = "Assets/VFX/Generated/" + id + "/VFX_" + id + ".prefab";
                Assert.That(Sha256(Absolute(path)), Is.EqualTo(hashes[id]), id); Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guids[id]), id);
            }
        }

        [Test]
        public void TenRuntimeEntries_BindExactVisualModesAndRealPooledSlotCarriers()
        {
            CapabilityBlankCompiler.BuildTimingAreaBlanks();
            var ids = TimingIds();
            var modes = new[] { "telegraph", "delay_fuse", "tick_pulse", "charge_release", "channel_interrupt", "chain_sequence", "expand_ring", "implode", "moving_zone", "growth_stage" };
            var slots = new[] { "cap_hexflash_impact_2d", "cap_hexflash_impact_2d", "cap_hexflash_impact_2d", "cap_hexflash_impact_2d", string.Empty, "cap_hexflash_impact_2d", string.Empty, "cap_hexflash_impact_2d", "cap_residue_trail_3d", string.Empty };
            for (var i = 0; i < ids.Length; i++)
            {
                var path = "Assets/VFX/Generated/" + ids[i] + "/VFX_" + ids[i] + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, ids[i]);
                var executor = prefab.GetComponent<TimingAreaCapabilityVisualExecutor>();
                Assert.That(executor, Is.Not.Null, ids[i]);
                Assert.That(executor.VisualMode, Is.EqualTo(modes[i]), ids[i]);
                Assert.That(executor.ConfiguredSlotId, Is.EqualTo(slots[i]), ids[i]);
                Assert.That(executor.SlotBindingResolved, Is.True, ids[i]);
                Assert.That(executor.ParticleCapacity, Is.EqualTo(32), ids[i]);
                Assert.That(prefab.transform.Find("CapabilityDetailLine"), Is.Not.Null, ids[i]);
                var slotBatch = prefab.GetComponentsInChildren<ParticleSystem>(true).Single();
                Assert.That(slotBatch.name, Is.EqualTo("ResolvedVisualSlotBatch_" + (string.IsNullOrEmpty(slots[i]) ? "neutral" : slots[i])), ids[i]);
                Assert.That(slotBatch.main.maxParticles, Is.EqualTo(executor.ParticleCapacity), ids[i]);
                var slotRenderer = slotBatch.GetComponent<ParticleSystemRenderer>();
                Assert.That(slotRenderer.sharedMaterial, Is.Not.Null, ids[i]);
                var commonMaterial = AssetDatabase.LoadAssetAtPath<Material>(CapabilityBlankCompiler.AdditiveMaterialPath);
                if (string.IsNullOrEmpty(slots[i]))
                {
                    Assert.That(slotRenderer.renderMode, Is.EqualTo(ParticleSystemRenderMode.Mesh), ids[i]);
                    Assert.That(slotRenderer.mesh, Is.Not.Null, ids[i]);
                    Assert.That(slotRenderer.sharedMaterial, Is.EqualTo(commonMaterial), ids[i]);
                }
                else
                {
                    Assert.That(slotRenderer.sharedMaterial, Is.Not.EqualTo(commonMaterial), ids[i] + " resolves the support template material rather than the common carrier material");
                    Assert.That(AssetDatabase.GetAssetPath(slotRenderer.sharedMaterial), Does.StartWith("Assets/VFX/Templates/"), ids[i]);
                    if (slotRenderer.renderMode == ParticleSystemRenderMode.Mesh) Assert.That(slotRenderer.mesh, Is.Not.Null, ids[i]);
                    else Assert.That(slotRenderer.renderMode, Is.EqualTo(ParticleSystemRenderMode.Billboard).Or.EqualTo(ParticleSystemRenderMode.Stretch), ids[i]);
                }
                Assert.That(prefab.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(5), ids[i]);
                Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true).Length, Is.EqualTo(1), ids[i]);
                Assert.That(prefab.GetComponentsInChildren<Renderer>(true).Select(value => value.sharedMaterial).Where(value => value != null).Distinct().Count(), Is.LessThanOrEqualTo(2), "shared neutral plus resolved slot material: " + ids[i]);
            }
        }

        private static IEnumerable<CapabilitySampleRequest> AllTen()
        {
            var tokens = new[] { "telegraph", "delay_fuse", "tick_pulse", "charge_release", "channel_interrupt", "chain_sequence" };
            foreach (var token in tokens) { var value = Request(); value.TimingType = token; yield return value; }
            foreach (var token in new[] { "expand_ring", "implode", "moving_zone", "growth_stage" }) { var value = Request(); value.MotionType = token; yield return value; }
        }
        private static string[] TimingIds() { return new[] { "cap_telegraph_impact_3d", "cap_delayfuse_impact_2d", "cap_tickpulse_area_2d", "cap_charge_release_2d", "cap_channel_3d", "cap_chainseq_impact_2d", "cap_expand_area_3d", "cap_implode_area_3d", "cap_movingzone_area_3d", "cap_growth_area_2d" }; }
        private static CapabilitySampleRequest Request() { return new CapabilitySampleRequest { MotionType = "stationary", Origin = Vector3.zero, Target = new Vector3(2, 0, 0), Duration = 1.2f, DeltaTime = 1f / 60f, Seed = 333 }; }
        private static Recipe Parse(string archetype, JObject behavior)
        {
            var root = JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "../Packages/com.vfxcomposer.unity/Tests/EditMode/TestData/valid-fireball.json")));
            root["archetype"] = archetype; root["dimension"] = "3d"; root["behavior"] = behavior; var parsed = VfxDomainParser.ParseRecipe(root.ToString()); Assert.That(parsed.Report.HasErrors, Is.False); return parsed.Value;
        }
        private static string Absolute(string path) { return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path.Replace('/', Path.DirectorySeparatorChar))); }
        private static string Sha256(string path) { using (var stream = File.OpenRead(path)) using (var sha = System.Security.Cryptography.SHA256.Create()) return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2"))); }
    }
}
