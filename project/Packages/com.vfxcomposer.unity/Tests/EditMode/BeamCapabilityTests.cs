using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer.Capabilities;
using VFXComposer.Editor.Capabilities;
using VFXComposer.Editor.Domain;

namespace VFXComposer.Tests.EditMode
{
    public sealed class BeamCapabilityTests
    {
        [Test]
        public void HitscanAndSustained_RespectImmediateHitAndEndpointFollowing()
        {
            var hitscan = Beam(); hitscan.TimingType = "hitscan"; hitscan.Duration = .2f;
            var hitscanTrace = CapabilitySampler.SampleTrajectory(hitscan);
            var hit = hitscanTrace.Events.Single(value => value.Type == "on_hit");
            Assert.That(hit.Frame, Is.EqualTo(0));
            Assert.That(hit.Time, Is.EqualTo(0));
            Assert.That(hitscanTrace.Frames[0].Target, Is.EqualTo(hitscan.Target));
            Assert.That(hitscanTrace.Frames[0].Width, Is.EqualTo(1f).Within(.0001f));
            Assert.That(hitscanTrace.Frames.Last().Width, Is.EqualTo(0f).Within(.0001f), "hitscan trace carries a real fade-to-zero contract");

            var sustained = Beam(); sustained.TimingType = "sustained"; sustained.TargetVelocity = new Vector3(0, 1, 0);
            var sustainedTrace = CapabilitySampler.SampleTrajectory(sustained);
            Assert.That(Vector3.Distance(sustainedTrace.Frames.Last().Target, sustained.Target + sustained.TargetVelocity * sustained.Duration), Is.LessThan(.001f));
        }

        [Test]
        public void SweepAndCharge_RespectInertiaSpeedAndWidthLevels()
        {
            var sweep = Beam(); sweep.MotionType = "sweep"; sweep.TimingType = "sustained"; sweep.Motion["sweep_speed_max"] = 90; sweep.Motion["inertia"] = .15;
            var sweepTrace = CapabilitySampler.SampleTrajectory(sweep);
            Assert.That(Vector3.Distance(sweepTrace.Frames.First().Target, sweepTrace.Frames.Last().Target), Is.GreaterThan(.5f), "sweep must traverse a non-zero arc");
            for (var i = 2; i < sweepTrace.Frames.Count; i++)
            {
                var a = sweepTrace.Frames[i - 1].Target - sweep.Origin;
                var b = sweepTrace.Frames[i].Target - sweep.Origin;
                Assert.That(Vector3.Angle(a, b), Is.LessThanOrEqualTo(90f * sweep.DeltaTime + .02f), "frame " + i);
            }

            var charge = Beam(); charge.TimingType = "charge_scale"; charge.Timing["level_1"] = .4; charge.Timing["level_2"] = .8; charge.Timing["per_level_width"] = 1.7;
            var trace = CapabilitySampler.SampleTrajectory(charge);
            var widths = trace.Frames.Select(value => value.Width).Distinct().ToArray();
            Assert.That(widths, Is.EqualTo(new[] { 1f, 1.7f, 2.89f }).Within(.001f));
            Assert.That(widths[1] / widths[0], Is.GreaterThanOrEqualTo(1.6f));
            Assert.That(widths[2] / widths[1], Is.GreaterThanOrEqualTo(1.6f));
        }

        [Test]
        public void ReflectOccludeConvergeAndArcLink_ExposeEndpointAndTopologyContracts()
        {
            var reflect = Beam(); reflect.HitType = "reflect"; reflect.Hit["max_segments"] = 3; reflect.Hit["damping_per_bounce"] = .25;
            var reflections = CapabilitySampler.SampleTrajectory(reflect).Events.Where(value => value.Type == "on_bounce").ToArray();
            Assert.That(reflections.Length, Is.EqualTo(3));
            Assert.That(reflections.All(value => Mathf.Abs(value.After.magnitude - value.Before.magnitude * .75f) < .001f), Is.True);

            var occlude = Beam(); occlude.HitType = "occlude"; occlude.ObstacleDistance = 2; occlude.ObstacleChangeTime = .5f; occlude.ObstacleSecondDistance = 5;
            var occlusion = CapabilitySampler.SampleTrajectory(occlude);
            var before = occlusion.Frames.Last(value => value.Time < .5f);
            var after = occlusion.Frames.First(value => value.Time >= .5f);
            Assert.That(before.Target.magnitude, Is.EqualTo(2f).Within(.001f));
            Assert.That(after.Target.magnitude, Is.EqualTo(5f).Within(.001f));
            Assert.That(after.Index - before.Index, Is.LessThanOrEqualTo(2), "Obstacle response must be <=2 frames.");

            var converge = Beam(); converge.EmissionType = "converge"; converge.Emission["source_count"] = 4;
            var sources = CapabilitySampler.SampleTrajectory(converge).Events.Where(value => value.Type == "on_emit").ToArray();
            Assert.That(sources.Length, Is.EqualTo(4));
            Assert.That(sources.All(value => Vector3.Angle(value.After, converge.Target - value.Position) < .01f), Is.True);

            var arc = Beam(); arc.HitType = "arc_link"; arc.Hit["hop_count"] = 4;
            var arcHits = CapabilitySampler.SampleTrajectory(arc).Events.Where(value => value.Type == "on_hit").ToArray();
            Assert.That(arcHits.Length, Is.EqualTo(4));
            Assert.That(arcHits.Select(value => value.Time).Distinct().Count(), Is.EqualTo(4), "arc-link hops must be sequential, not same-frame marker overwrites");
            for (var i = 1; i < arcHits.Length; i++) Assert.That(arcHits[i].Time, Is.GreaterThan(arcHits[i - 1].Time));
        }

        [Test]
        public void EightBeamBlanks_AreDeterministicAndCombinationTableBlocksInvalidPairs()
        {
            foreach (var request in AllEight())
                Assert.That(CapabilitySampler.SampleTrajectory(request).ToCanonicalJson(), Is.EqualTo(CapabilitySampler.SampleTrajectory(request).ToCanonicalJson()), request.MotionType + "/" + request.HitType + "/" + request.TimingType);

            AssertValid(new JObject { ["hit"] = Block("occlude", "impact_slot", "cap_hexflash_impact_2d"), ["timing"] = Block("charge_scale", "level_1", .4) });
            AssertValid(new JObject { ["motion"] = Block("sweep", "sweep_speed_max", 90), ["hit"] = Block("occlude", "impact_slot", "cap_hexflash_impact_2d"), ["timing"] = new JObject { ["type"] = "sustained" } });
            AssertInvalid("beam", new JObject { ["motion"] = Block("parabola", "flight_time", 1) });
            AssertInvalid("projectile", new JObject { ["hit"] = Block("reflect", "max_segments", 2) });
        }

        [Test]
        public void EightFormalBeamRecipes_BuildIdempotentPlayerSafeRuntimeEntries()
        {
            CapabilityBlankCompiler.BuildBeamBlanks();
            var ids = new[] { "cap_hitscan_beam_3d", "cap_sustained_beam_3d", "cap_sweep_beam_3d", "cap_charge_beam_3d", "cap_reflect_beam_3d", "cap_occlude_beam_3d", "cap_converge_beam_3d", "cap_arclink_beam_2d" };
            var first = new Dictionary<string, string>(StringComparer.Ordinal);
            var guids = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                var path = "Assets/VFX/Generated/" + id + "/VFX_" + id + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, id);
                Assert.That(prefab.GetComponent<CapabilityBlankVfxController>(), Is.Not.Null, id);
                var visual = prefab.GetComponent<BeamCapabilityVisualExecutor>();
                Assert.That(visual, Is.Not.Null, id);
                var converge = id == "cap_converge_beam_3d";
                var reflect = id == "cap_reflect_beam_3d";
                Assert.That(prefab.GetComponentsInChildren<LineRenderer>(true).Length, Is.EqualTo(converge ? 4 : reflect ? 3 : 1), id);
                Assert.That(prefab.GetComponentsInChildren<Renderer>(true).Length, Is.LessThanOrEqualTo(converge ? 7 : 5), id);
                Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true).Length, Is.LessThanOrEqualTo(1), id);
                Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true).Sum(value => value.main.maxParticles), Is.LessThanOrEqualTo(24), id);
                Assert.That(prefab.GetComponentsInChildren<Renderer>(true).SelectMany(value => value.sharedMaterials).Where(value => value != null).Distinct().Count(), Is.LessThanOrEqualTo(3), id);
                Assert.That(prefab.GetComponentInChildren<BeamCapabilityPreviewDriver>(true), Is.Null, id + " preview driver leaked into Runtime Entry");
                first[id] = Sha256(Absolute(path)); guids[id] = AssetDatabase.AssetPathToGUID(path);
                Assert.That(VFXComposer.Editor.Rules.VfxProductionRules.CaptureManifest(id), Is.Not.Null, id + " manifest");
                var manifestPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ProjectSettings", "VFXComposer", "BuildManifests", id + ".manifest.json");
                Assert.That((string)JObject.Parse(File.ReadAllText(manifestPath))["compilerVersion"], Is.EqualTo(CapabilityBlankCompiler.BeamCompilerVersion), id);
            }
            CapabilityBlankCompiler.BuildBeamBlanks();
            foreach (var id in ids)
            {
                var path = "Assets/VFX/Generated/" + id + "/VFX_" + id + ".prefab";
                Assert.That(Sha256(Absolute(path)), Is.EqualTo(first[id]), id);
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guids[id]), id);
            }
        }

        private static IEnumerable<CapabilitySampleRequest> AllEight()
        {
            var hitscan = Beam(); hitscan.TimingType = "hitscan"; yield return hitscan;
            var sustained = Beam(); sustained.TimingType = "sustained"; yield return sustained;
            var sweep = Beam(); sweep.MotionType = "sweep"; sweep.TimingType = "sustained"; yield return sweep;
            var charge = Beam(); charge.TimingType = "charge_scale"; yield return charge;
            var reflect = Beam(); reflect.HitType = "reflect"; yield return reflect;
            var occlude = Beam(); occlude.HitType = "occlude"; yield return occlude;
            var converge = Beam(); converge.EmissionType = "converge"; yield return converge;
            var arc = Beam(); arc.HitType = "arc_link"; yield return arc;
        }

        private static CapabilitySampleRequest Beam() { return new CapabilitySampleRequest { Origin = Vector3.zero, Direction = Vector3.right, Target = new Vector3(6, 0, 0), Duration = 1.2f, DeltaTime = 1f / 60f, Seed = 222 }; }
        private static JObject Block(string type, string key, JToken value) { return new JObject { ["type"] = type, [key] = value }; }

        private static void AssertValid(JObject behavior) { Assert.That(CapabilityRegistry.Validate(Parse("beam", behavior)).HasErrors, Is.False); }
        private static void AssertInvalid(string archetype, JObject behavior) { Assert.That(CapabilityRegistry.Validate(Parse(archetype, behavior)).Entries.Any(value => value.Code == "E324" || value.Code == "E325"), Is.True); }
        private static Recipe Parse(string archetype, JObject behavior)
        {
            var path = Path.Combine(Application.dataPath, "../Packages/com.vfxcomposer.unity/Tests/EditMode/TestData/valid-fireball.json");
            var root = JObject.Parse(File.ReadAllText(path)); root["archetype"] = archetype; root["dimension"] = "3d"; root["behavior"] = behavior;
            var parsed = VfxDomainParser.ParseRecipe(root.ToString()); Assert.That(parsed.Report.HasErrors, Is.False); return parsed.Value;
        }
        private static string Absolute(string path) { return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path.Replace('/', Path.DirectorySeparatorChar))); }
        private static string Sha256(string path) { using (var stream = File.OpenRead(path)) using (var sha = System.Security.Cryptography.SHA256.Create()) return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2"))); }
    }
}
