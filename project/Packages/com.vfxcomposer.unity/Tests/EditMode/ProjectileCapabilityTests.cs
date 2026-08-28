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
    public sealed class ProjectileCapabilityTests
    {
        [Test]
        public void LinearAccelParabolaAndWave_FollowDeclaredKinematics()
        {
            var linear = Request("linear", 1f); linear.Motion["speed"] = 4;
            var linearTrace = CapabilitySampler.SampleTrajectory(linear);
            Assert.That(linearTrace.Frames.Last().Position.x, Is.EqualTo(4f).Within(.001f));
            Assert.That(linearTrace.Frames.All(frame => Mathf.Abs(frame.Velocity.magnitude - 4f) < .001f), Is.True);

            var accel = Request("accel", 2f); accel.Motion["init_speed"] = 1; accel.Motion["accel"] = 3; accel.Motion["max_speed"] = 4;
            var accelTrace = CapabilitySampler.SampleTrajectory(accel);
            Assert.That(accelTrace.Frames.First().Velocity.magnitude, Is.EqualTo(1f).Within(.001f));
            Assert.That(accelTrace.Frames.Last().Velocity.magnitude, Is.EqualTo(4f).Within(.001f));
            for (var i = 1; i < accelTrace.Frames.Count; i++) Assert.That(accelTrace.Frames[i].Velocity.magnitude, Is.GreaterThanOrEqualTo(accelTrace.Frames[i - 1].Velocity.magnitude - .0001f));

            var parabola = Request("parabola", 2f); parabola.Target = new Vector3(8, 0, 0); parabola.Motion["apex_height"] = 3; parabola.Motion["flight_time"] = 2;
            var parabolaTrace = CapabilitySampler.SampleTrajectory(parabola);
            var apex = parabolaTrace.Frames.OrderByDescending(frame => frame.Position.y).First();
            Assert.That(apex.Time, Is.EqualTo(1f).Within(parabola.DeltaTime));
            Assert.That(apex.Position.y, Is.EqualTo(3f).Within(.01f));
            Assert.That(Vector3.Distance(parabolaTrace.Frames.Last().Position, parabola.Target), Is.LessThanOrEqualTo(.08f));

            var wave = Request("wave", 1f); wave.Motion["speed"] = 5; wave.Motion["amplitude"] = .6; wave.Motion["frequency"] = 2;
            var waveTrace = CapabilitySampler.SampleTrajectory(wave);
            Assert.That(waveTrace.Frames.Max(frame => Mathf.Abs(frame.Position.y)), Is.EqualTo(.6f).Within(.01f));
            Assert.That(waveTrace.Frames.Last().Position.x, Is.EqualTo(5f).Within(.001f));
        }

        [Test]
        public void HomingBoomerangAndOrbit_RespectStateTransitions()
        {
            var homing = Request("homing", 1f); homing.Direction = Vector3.up; homing.Target = new Vector3(6, 0, 0); homing.Motion["turn_rate"] = 120; homing.Motion["max_speed"] = 4;
            var homingTrace = CapabilitySampler.SampleTrajectory(homing);
            var maxAngle = 120f * Mathf.Deg2Rad * homing.DeltaTime + .0001f;
            for (var i = 2; i < homingTrace.Frames.Count; i++) Assert.That(Vector3.Angle(homingTrace.Frames[i - 1].Velocity, homingTrace.Frames[i].Velocity) * Mathf.Deg2Rad, Is.LessThanOrEqualTo(maxAngle));

            var boomerang = Request("boomerang", 2.5f); boomerang.Motion["out_distance"] = 2; boomerang.Motion["speed"] = 4; boomerang.Motion["hover_time"] = .5; boomerang.Motion["return_speed"] = 4;
            var boomerangTrace = CapabilitySampler.SampleTrajectory(boomerang);
            Assert.That(boomerangTrace.Frames.Any(frame => frame.Stage == 0), Is.True);
            Assert.That(boomerangTrace.Frames.Any(frame => frame.Stage == 1), Is.True);
            Assert.That(boomerangTrace.Frames.Any(frame => frame.Stage == 2), Is.True);
            Assert.That(Vector3.Distance(boomerangTrace.Frames.Last().Position, boomerang.Origin), Is.LessThan(.01f));

            var orbit = Request("orbit_then_strike", 1.5f); orbit.Target = new Vector3(5, 0, 0); orbit.Motion["orbit_radius"] = 1; orbit.Motion["orbit_turns"] = 1; orbit.Motion["orbit_time"] = 1; orbit.Motion["strike_speed"] = 8;
            var orbitTrace = CapabilitySampler.SampleTrajectory(orbit);
            Assert.That(orbitTrace.Frames.Any(frame => frame.Stage == 0), Is.True);
            Assert.That(orbitTrace.Frames.Any(frame => frame.Stage == 1), Is.True);
            var orbitEnd = orbitTrace.Frames.Last(frame => frame.Time <= 1f + .0001f);
            Assert.That(Vector3.Distance(orbitEnd.Position, orbit.Origin), Is.EqualTo(1f).Within(.02f));
        }

        [Test]
        public void Bounce_EmitsExactCountWithReflectionAndEnergyDamping()
        {
            var request = Request("bounce", 3f);
            request.Direction = new Vector3(1, 1, 0);
            request.CollisionMin = new Vector3(-2, -.5f, -1);
            request.CollisionMax = new Vector3(2, .5f, 1);
            request.Motion["speed"] = 6;
            request.Motion["bounce_count"] = 3;
            request.Motion["energy_damping"] = .2;
            var trace = CapabilitySampler.SampleTrajectory(request);
            var bounces = trace.Events.Where(value => value.Type == "on_bounce").ToArray();
            Assert.That(bounces.Length, Is.EqualTo(3));
            foreach (var bounce in bounces)
            {
                Assert.That(bounce.After.magnitude, Is.EqualTo(bounce.Before.magnitude * .8f).Within(.001f));
                Assert.That(Mathf.Abs(Vector3.Dot(bounce.Before.normalized, bounce.After.normalized)), Is.LessThan(1f), "A bounce must change direction.");
            }
        }

        [Test]
        public void PierceSplitAndChainHop_EmitDeclaredTopology()
        {
            var pierce = Request("linear", 2f); pierce.HitType = "pierce"; pierce.Hit["max_hits"] = 3; pierce.Hit["damping_per_hit"] = .25;
            var pierceTrace = CapabilitySampler.SampleTrajectory(pierce);
            Assert.That(pierceTrace.Events.Count(value => value.Type == "on_hit"), Is.EqualTo(3));
            Assert.That(pierceTrace.Frames.Last().Velocity.magnitude, Is.EqualTo(4f * .75f * .75f * .75f).Within(.001f));

            var split = Request("linear", 1f); split.HitType = "split"; split.Hit["child_count"] = 5; split.Hit["split_angle"] = 80;
            var splitEvents = CapabilitySampler.SampleTrajectory(split).Events.Where(value => value.Type == "on_split").ToArray();
            Assert.That(splitEvents.Length, Is.EqualTo(5));
            Assert.That(Vector3.Angle(splitEvents.First().After, splitEvents.Last().After), Is.EqualTo(80f).Within(.01f));

            var chain = Request("linear", 1f); chain.HitType = "chain_hop"; chain.Hit["hop_count"] = 4;
            chain.Hit["hop_range"] = 4; chain.Hit["damping"] = .15;
            var chainTrace = CapabilitySampler.SampleTrajectory(chain);
            var hops = chainTrace.Events.Where(value => value.Type == "on_hit").ToArray();
            Assert.That(hops.Select(value => value.Sequence), Is.EqualTo(new[] { 1, 2, 3, 4 }));
            for (var i = 1; i < hops.Length; i++)
            {
                Assert.That(hops[i].Time, Is.GreaterThan(hops[i - 1].Time), "chain hops must switch target at deterministic distinct times");
                Assert.That(hops[i].Frame, Is.GreaterThan(hops[i - 1].Frame));
                Assert.That(Vector3.Distance(hops[i].Position, hops[i - 1].Position), Is.GreaterThan(.1f));
                Assert.That(hops[i].Before.magnitude, Is.EqualTo(hops[i - 1].After.magnitude).Within(.0001f));
            }
            foreach (var hop in hops) Assert.That(hop.After.magnitude, Is.EqualTo(hop.Before.magnitude * .85f).Within(.0001f));
        }

        [Test]
        public void Volley_ProducesFanStaggerAndRingContracts()
        {
            var fan = Request("linear", .2f); fan.EmissionType = "fan"; fan.Emission["count"] = 5; fan.Emission["spread_angle"] = 40;
            var fanEvents = CapabilitySampler.SampleTrajectory(fan).Events.Where(value => value.Type == "on_emit").ToArray();
            Assert.That(fanEvents.Length, Is.EqualTo(5));
            Assert.That(Vector3.Angle(fanEvents.First().After, fanEvents.Last().After), Is.EqualTo(40f).Within(.01f));

            var stagger = Request("linear", .5f); stagger.EmissionType = "burst_stagger"; stagger.Emission["count"] = 4; stagger.Emission["stagger"] = .1;
            Assert.That(CapabilitySampler.SampleTrajectory(stagger).Events.Where(value => value.Type == "on_emit").Select(value => value.Time), Is.EqualTo(new[] { 0f, .1f, .2f, .3f }).Within(.0001f));

            var ring = Request("linear", .2f); ring.EmissionType = "ring"; ring.Emission["count"] = 8; ring.Emission["ring_radius"] = .5;
            var ringEvents = CapabilitySampler.SampleTrajectory(ring).Events.Where(value => value.Type == "on_emit").ToArray();
            Assert.That(ringEvents.Length, Is.EqualTo(8));
            Assert.That(ringEvents.All(value => Mathf.Abs(value.Position.magnitude - .5f) < .001f), Is.True);

            var showcase = Request("linear", 1.98f); showcase.EmissionType = "volley_showcase";
            showcase.Emission["fan_count"] = 5; showcase.Emission["fan_spread_angle"] = 50;
            showcase.Emission["burst_count"] = 5; showcase.Emission["burst_stagger"] = .09;
            showcase.Emission["ring_count"] = 8; showcase.Emission["ring_radius"] = .45; showcase.Emission["phase_duration"] = .66;
            var showcaseEvents = CapabilitySampler.SampleTrajectory(showcase).Events.Where(value => value.Type == "on_emit").ToArray();
            var showcaseFan = showcaseEvents.Where(value => value.Detail == "fan").ToArray();
            var showcaseBurst = showcaseEvents.Where(value => value.Detail == "burst_stagger").ToArray();
            var showcaseRing = showcaseEvents.Where(value => value.Detail == "ring").ToArray();
            Assert.That(showcaseFan.Length, Is.EqualTo(5));
            Assert.That(Vector3.Angle(showcaseFan.First().After, showcaseFan.Last().After), Is.EqualTo(50f).Within(.01f));
            Assert.That(showcaseBurst.Select(value => value.Time), Is.EqualTo(new[] { .66f, .75f, .84f, .93f, 1.02f }).Within(.0001f));
            Assert.That(showcaseRing.Length, Is.EqualTo(8));
            Assert.That(showcaseRing.Select(value => value.Time), Has.All.EqualTo(1.32f).Within(.0001f));
            for (var i = 0; i < showcaseRing.Length; i++) Assert.That(Vector3.Angle(showcaseRing[i].After, showcaseRing[(i + 1) % showcaseRing.Length].After), Is.EqualTo(45f).Within(.01f));
        }

        [Test]
        public void TwelveProjectileBlanks_AreDeterministic_AndCombinationsAreGated()
        {
            foreach (var request in AllTwelve())
            {
                var first = CapabilitySampler.SampleTrajectory(request).ToCanonicalJson();
                var second = CapabilitySampler.SampleTrajectory(request).ToCanonicalJson();
                Assert.That(second, Is.EqualTo(first), request.MotionType + "/" + request.HitType + "/" + request.EmissionType);
            }

            AssertCombinationValid(new JObject { ["motion"] = Block("wave", "speed", 4), ["emission"] = Block("fan", "count", 5) });
            AssertCombinationValid(new JObject { ["motion"] = Block("homing", "max_speed", 6), ["emission"] = Block("burst_stagger", "count", 3) });
            AssertCombinationValid(new JObject { ["motion"] = Block("parabola", "flight_time", 1), ["hit"] = Block("split", "child_count", 3) });
            AssertCombinationInvalid("beam", new JObject { ["motion"] = Block("bounce", "bounce_count", 2) });
            AssertCombinationInvalid("projectile", new JObject { ["motion"] = Block("wave", "speed", 4), ["timing"] = Block("hitscan", "linger", .1) });
        }

        [Test]
        public void TwelveFormalRecipes_BuildIdempotentPlayerSafeRuntimeEntries()
        {
            CapabilityBlankCompiler.BuildProjectileBlanks();
            var ids = new[]
            {
                "cap_linear_proj_3d", "cap_accel_proj_3d", "cap_parabola_proj_3d", "cap_homing_proj_3d",
                "cap_wave_proj_2d", "cap_boomerang_proj_3d", "cap_bounce_proj_3d", "cap_orbit_proj_3d",
                "cap_pierce_proj_3d", "cap_split_proj_2d", "cap_chainhop_proj_2d", "cap_volley_proj_2d"
            };
            var first = new Dictionary<string, string>(StringComparer.Ordinal);
            var firstGuids = new Dictionary<string, string>(StringComparer.Ordinal);
            var firstManifests = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                var recipePath = "Assets/VFX/Recipes/Capability/" + id + ".default.json";
                var prefabPath = "Assets/VFX/Generated/" + id + "/VFX_" + id + ".prefab";
                Assert.That(AssetDatabase.LoadAssetAtPath<TextAsset>(recipePath), Is.Not.Null, recipePath);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.That(prefab, Is.Not.Null, prefabPath);
                var controller = prefab.GetComponent<CapabilityBlankVfxController>();
                Assert.That(controller, Is.Not.Null, id);
                Assert.That(prefab.GetComponentsInChildren<Renderer>(true).Length, Is.LessThanOrEqualTo(4), id);
                Assert.That(prefab.GetComponentsInChildren<Renderer>(true).SelectMany(value => value.sharedMaterials).Where(value => value != null).Distinct().Count(), Is.LessThanOrEqualTo(2), id + " materials");
                var carrierCount = id == "cap_split_proj_2d" || id == "cap_volley_proj_2d" ? 1 : 0;
                Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true).Length, Is.EqualTo(carrierCount), id);
                if (carrierCount == 1)
                {
                    var particles = prefab.GetComponentInChildren<ParticleSystem>(true);
                    Assert.That(particles.main.maxParticles, Is.EqualTo(24), id);
                    Assert.That(particles.particleCount, Is.EqualTo(0), id);
                    Assert.That(particles.GetComponent<ParticleSystemRenderer>().enabled, Is.False, id);
                }
                Assert.That(prefab.GetComponentsInChildren<TrailRenderer>(true).Length, Is.LessThanOrEqualTo(1), id);
                first[id] = Sha256(Absolute(prefabPath));
                firstGuids[id] = AssetDatabase.AssetPathToGUID(prefabPath);
                firstManifests[id] = VFXComposer.Editor.Rules.VfxProductionRules.CaptureManifest(id);
                Assert.That(firstManifests[id], Is.Not.Null, id + " manifest");
            }

            var volleyRecipePath = "Assets/VFX/Recipes/Capability/cap_volley_proj_2d.default.json";
            var volleyRecipe = JObject.Parse(File.ReadAllText(Absolute(volleyRecipePath)));
            Assert.That((string)volleyRecipe["behavior"]["emission"]["type"], Is.EqualTo("volley_showcase"));
            Assert.That(volleyRecipe["stages"].Select(value => (string)value["id"]), Is.EqualTo(new[] { "showcase_fan", "showcase_burst_stagger", "showcase_ring" }));
            var volleyManifest = JObject.Parse(firstManifests["cap_volley_proj_2d"]);
            Assert.That((string)volleyManifest["sourceRecipePath"], Is.EqualTo(volleyRecipePath));
            Assert.That((string)volleyManifest["recipeHash"], Is.EqualTo(VFXComposer.Editor.Validation.RecipeCanonicalizer.ComputeSha256(File.ReadAllText(Absolute(volleyRecipePath)))));
            Assert.That((string)volleyManifest["compilerVersion"], Is.EqualTo(CapabilityBlankCompiler.CompilerVersion));
            Assert.That((int)volleyManifest["recipeRevision"], Is.EqualTo(2));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/cap_volley_proj_2d/VFX_cap_volley_proj_2d.prefab").GetComponent<CapabilityBlankVfxController>().EmissionType, Is.EqualTo("volley_showcase"));

            CapabilityBlankCompiler.BuildProjectileBlanks();
            foreach (var id in ids)
            {
                var prefabPath = "Assets/VFX/Generated/" + id + "/VFX_" + id + ".prefab";
                Assert.That(Sha256(Absolute(prefabPath)), Is.EqualTo(first[id]), id + " prefab bytes");
                Assert.That(AssetDatabase.AssetPathToGUID(prefabPath), Is.EqualTo(firstGuids[id]), id + " GUID");
                Assert.That(VFXComposer.Editor.Rules.VfxProductionRules.CaptureManifest(id), Is.EqualTo(firstManifests[id]), id + " manifest bytes");
            }
        }

        private static IEnumerable<CapabilitySampleRequest> AllTwelve()
        {
            var motions = new[] { "linear", "accel", "parabola", "homing", "wave", "boomerang", "bounce", "orbit_then_strike" };
            foreach (var motion in motions) yield return Request(motion, 1f);
            var pierce = Request("linear", 1f); pierce.HitType = "pierce"; yield return pierce;
            var split = Request("linear", 1f); split.HitType = "split"; yield return split;
            var chain = Request("linear", 1f); chain.HitType = "chain_hop"; yield return chain;
            var volley = Request("linear", 1.98f); volley.EmissionType = "volley_showcase"; volley.Emission["fan_count"] = 5; volley.Emission["fan_spread_angle"] = 50; volley.Emission["burst_count"] = 5; volley.Emission["burst_stagger"] = .09; volley.Emission["ring_count"] = 8; volley.Emission["ring_radius"] = .45; volley.Emission["phase_duration"] = .66; yield return volley;
        }

        private static CapabilitySampleRequest Request(string motion, float duration)
        {
            return new CapabilitySampleRequest { MotionType = motion, Origin = Vector3.zero, Direction = Vector3.right, Target = new Vector3(6, 0, 0), Duration = duration, DeltaTime = 1f / 60f, Seed = 123 };
        }

        private static JObject Block(string type, string key, JToken value) { return new JObject { ["type"] = type, [key] = value }; }

        private static void AssertCombinationValid(JObject behavior)
        {
            var recipe = ParseRecipe("projectile", behavior);
            Assert.That(CapabilityRegistry.Validate(recipe).HasErrors, Is.False);
        }

        private static void AssertCombinationInvalid(string archetype, JObject behavior)
        {
            var recipe = ParseRecipe(archetype, behavior);
            Assert.That(CapabilityRegistry.Validate(recipe).Entries.Any(value => value.Code == "E324" || value.Code == "E325"), Is.True);
        }

        private static Recipe ParseRecipe(string archetype, JObject behavior)
        {
            var fixturePath = Path.Combine(Application.dataPath, "../Packages/com.vfxcomposer.unity/Tests/EditMode/TestData/valid-fireball.json");
            var json = JObject.Parse(File.ReadAllText(fixturePath));
            json["archetype"] = archetype;
            json["dimension"] = "3d";
            json["behavior"] = behavior;
            var parsed = VfxDomainParser.ParseRecipe(json.ToString());
            Assert.That(parsed.Report.HasErrors, Is.False);
            return parsed.Value;
        }

        private static string Sha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
        }

        private static string Absolute(string assetPath) { return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath.Replace('/', Path.DirectorySeparatorChar))); }
    }
}
