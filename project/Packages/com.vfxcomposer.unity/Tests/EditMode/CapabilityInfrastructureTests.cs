using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using VFXComposer.Capabilities;
using VFXComposer.Editor.Capabilities;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Patch;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Tests.EditMode
{
    public sealed class CapabilityInfrastructureTests
    {
        [Test]
        public void LegacyRecipe_RemainsValidWithoutBehavior_AndLegacyStyleStringStillParses()
        {
            var json = Fixture("valid-fireball.json");
            var before = RecipeCanonicalizer.ComputeSha256(json);
            var parsed = VfxDomainParser.ParseRecipe(json);
            Assert.That(parsed.Report.HasErrors, Is.False, Entries(parsed.Report));
            Assert.That(parsed.Value.Behavior, Is.Null);
            Assert.That(parsed.Value.Style, Is.Not.Null);
            Assert.That(parsed.Value.Style.Token, Is.EqualTo("stylized"));
            Assert.That(parsed.Value.Style.UsedLegacyStringForm, Is.True);
            Assert.That(RecipeCanonicalizer.ComputeSha256(json), Is.EqualTo(before), "The migration must not rewrite legacy Recipe bytes.");
        }

        [Test]
        public void Contract12_ParsesBehaviorAndReservedStyleInOneMigration()
        {
            var root = JObject.Parse(Fixture("valid-fireball.json"));
            root["style"] = new JObject
            {
                ["token"] = "cartoon",
                ["palette"] = new JObject { ["primary"] = "#FF6A00", ["secondary"] = "#FFD84D", ["accent"] = "#FFFFFF" },
                ["outline"] = .12,
                ["shading_steps"] = 3,
                ["noise_scale"] = 1.0,
                ["glow_strength"] = .6
            };
            root["behavior"] = new JObject
            {
                ["motion"] = new JObject { ["type"] = "homing", ["turn_rate"] = 120, ["max_speed"] = 8, ["lose_target_mode"] = "straight" },
                ["hit"] = new JObject { ["type"] = "pierce", ["max_hits"] = 3, ["damping_per_hit"] = .25 },
                ["emission"] = new JObject { ["type"] = "fan", ["count"] = 5, ["spread_angle"] = 40 },
                ["timing"] = new JObject { ["type"] = "instant" }
            };
            var parsed = VfxDomainParser.ParseRecipe(root.ToString());
            Assert.That(parsed.Report.HasErrors, Is.False, Entries(parsed.Report));
            var report = CapabilityRegistry.Validate(parsed.Value);
            Assert.That(report.HasErrors, Is.False, Entries(report));
            Assert.That(parsed.Value.Style.Token, Is.EqualTo("cartoon"));
            Assert.That(parsed.Value.Behavior.Motion.Type, Is.EqualTo("homing"));
        }

        [Test]
        public void CapabilityRegistry_RejectsUnknownParameterRangeStyleAndIllegalCombinations()
        {
            AssertError(RecipeWith("projectile", new JObject { ["motion"] = new JObject { ["type"] = "teleport_curve" } }), "E320", "/behavior/motion/type");
            AssertError(RecipeWith("projectile", new JObject { ["motion"] = new JObject { ["type"] = "homing", ["turn_rate"] = 9000 } }), "E323", "/behavior/motion/turn_rate");
            AssertError(RecipeWith("projectile", new JObject { ["motion"] = new JObject { ["type"] = "homing", ["hallucinated"] = 1 } }), "E321", "/behavior/motion/hallucinated");
            AssertError(RecipeWith("projectile", new JObject { ["motion"] = new JObject { ["type"] = "parabola" }, ["timing"] = new JObject { ["type"] = "hitscan" } }), "E325", "/behavior");
            AssertError(RecipeWith("beam", new JObject { ["motion"] = new JObject { ["type"] = "boomerang" } }), "E324", "/behavior/motion/type");

            var root = JObject.Parse(Fixture("valid-fireball.json"));
            root["style"] = new JObject { ["token"] = "photoreal", ["outline"] = 2.0 };
            var style = VfxDomainParser.ParseRecipe(root.ToString());
            var styleReport = CapabilityRegistry.Validate(style.Value);
            Assert.That(styleReport.Contains("E326", "/style/token"), Is.True, Entries(styleReport));
            Assert.That(styleReport.Contains("E327", "/style/outline"), Is.True, Entries(styleReport));
        }

        [Test]
        public void SampleTrajectory_IsByteDeterministic_AndHomingRespectsTurnRate()
        {
            var request = new CapabilitySampleRequest
            {
                MotionType = "homing", Origin = Vector3.zero, Direction = Vector3.up, Target = new Vector3(6, 0, 0),
                Duration = 1f, DeltaTime = 1f / 60f, Seed = 42
            };
            request.Motion["turn_rate"] = 90; request.Motion["max_speed"] = 3;
            var first = CapabilitySampler.SampleTrajectory(request); var second = CapabilitySampler.SampleTrajectory(request);
            Assert.That(first.ToCanonicalJson(), Is.EqualTo(second.ToCanonicalJson()));
            var maxRadians = 90f * Mathf.Deg2Rad * request.DeltaTime + .0001f;
            for (var i = 2; i < first.Frames.Count; i++) Assert.That(Vector3.Angle(first.Frames[i - 1].Velocity, first.Frames[i].Velocity) * Mathf.Deg2Rad, Is.LessThanOrEqualTo(maxRadians), "frame " + i);
            var distances = first.Frames.Select(frame => Vector3.Distance(frame.Position, request.Target)).ToArray();
            for (var i = 1; i < distances.Length; i++) Assert.That(distances[i], Is.LessThanOrEqualTo(distances[i - 1] + .0001f), "distance frame " + i);
        }

        [Test]
        public void HitscanEmitsHitOnTriggerFrame_AndVisualSlotsAreValidatedAsRecipeIds()
        {
            var trace = CapabilitySampler.SampleTrajectory(new CapabilitySampleRequest { TimingType = "hitscan", Duration = .2f, DeltaTime = 1f / 60f });
            var hit = trace.Events.Single(value => value.Type == "on_hit");
            Assert.That(hit.Frame, Is.EqualTo(0)); Assert.That(hit.Time, Is.EqualTo(0));

            var valid = VfxDomainParser.ParseRecipe(RecipeWith("area", new JObject { ["timing"] = new JObject { ["type"] = "tick_pulse", ["tick_interval"] = .5, ["tick_visual_slot"] = "cap_hexflash_impact_2d" } }));
            Assert.That(CapabilityRegistry.Validate(valid.Value).HasErrors, Is.False);
            var invalid = VfxDomainParser.ParseRecipe(RecipeWith("area", new JObject { ["timing"] = new JObject { ["type"] = "tick_pulse", ["tick_interval"] = .5, ["tick_visual_slot"] = "" } }));
            Assert.That(CapabilityRegistry.Validate(invalid.Value).Contains("E323", "/behavior/timing/tick_visual_slot"), Is.True);
        }

        [Test]
        public void ExistingImplementations_AreExplicitlyRegisteredForMigration()
        {
            AssertMigrated("motion", "homing", "seeker_orb_3d");
            AssertMigrated("hit", "arc_link", "chain_arc_3d");
            AssertMigrated("timing", "telegraph", "warning_telegraph_3d");
            AssertMigrated("timing", "tick_pulse", "static_field");
            AssertMigrated("timing", "chain_sequence", "chain_blast");
        }

        [Test]
        public void Contract12Patch_ChangesParametersAndStyleButNeverCapabilityType()
        {
            var path = Path.Combine(Application.dataPath, "VFX/Recipes/fireball-2d.default.json");
            var root = JObject.Parse(File.ReadAllText(path));
            root["behavior"] = new JObject
            {
                ["motion"] = new JObject { ["type"] = "homing", ["turn_rate"] = 120, ["max_speed"] = 8, ["lose_target_mode"] = "straight" },
                ["hit"] = new JObject { ["type"] = "single" }, ["emission"] = new JObject { ["type"] = "single" }, ["timing"] = new JObject { ["type"] = "instant" }
            };
            var patch = new JArray
            {
                new JObject { ["op"] = "set_behavior_param", ["path"] = "/behavior/motion/max_speed", ["value"] = 12 },
                new JObject { ["op"] = "set_style_token", ["path"] = "/style/token", ["value"] = "cartoon" },
                new JObject { ["op"] = "set_palette", ["path"] = "/style/palette/primary", ["value"] = "#FF6600" }
            };
            var result = new VfxPatchService().Validate(root.ToString(), patch.ToString(), 1, VfxCompiler.LoadFormalCatalog());
            Assert.That(result.IsValid, Is.True, Entries(result.Report));
            var changed = JObject.Parse(result.PatchedRecipeJson);
            Assert.That((double)changed["behavior"]["motion"]["max_speed"], Is.EqualTo(12));
            Assert.That((string)changed["behavior"]["motion"]["type"], Is.EqualTo("homing"));
            Assert.That((string)changed["style"]["token"], Is.EqualTo("cartoon"));
            Assert.That((string)changed["style"]["palette"]["primary"], Is.EqualTo("#FF6600"));

            var forbidden = new JArray { new JObject { ["op"] = "set_behavior_param", ["path"] = "/behavior/motion/type", ["value"] = "wave" } };
            var rejected = new VfxPatchService().Validate(root.ToString(), forbidden.ToString(), 1, VfxCompiler.LoadFormalCatalog());
            Assert.That(rejected.Report.Contains("E705", "/behavior/motion/type"), Is.True, Entries(rejected.Report));
        }

        private static void AssertMigrated(string domain, string token, string expected)
        {
            CapabilityDefinition definition; Assert.That(CapabilityRegistry.TryGet(domain, token, out definition), Is.True);
            Assert.That(definition.MigratedFrom, Does.Contain(expected));
        }

        private static void AssertError(string json, string code, string path)
        {
            var parsed = VfxDomainParser.ParseRecipe(json); Assert.That(parsed.Report.HasErrors, Is.False, Entries(parsed.Report));
            var report = CapabilityRegistry.Validate(parsed.Value); Assert.That(report.Contains(code, path), Is.True, Entries(report));
        }

        private static string RecipeWith(string archetype, JObject behavior)
        {
            var root = JObject.Parse(Fixture("valid-fireball.json")); root["archetype"] = archetype; root["behavior"] = behavior; return root.ToString();
        }

        private static string Fixture(string name)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "../Packages/com.vfxcomposer.unity/Tests/EditMode/TestData", name));
        }

        private static string Entries(ValidationReport report) { return string.Join(" | ", report.Entries.Select(value => value.Code + " " + value.Path + " " + value.Message).ToArray()); }
    }
}
