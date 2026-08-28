using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Editor.Elements
{
    public sealed class ElementNextBindingPlan
    {
        public string Parameter;
        public string Carrier;
        public float AuthoredValue;
    }

    public sealed class ElementNextCandidatePlan
    {
        public string SourceRecipePath;
        public string EffectId;
        public ElementNextCandidateFamily Family;
        public ElementNextCandidateProfile Profile;
        public StyledVfxLifecycle Lifecycle;
        public float Duration;
        public uint Seed;
        public Color Primary;
        public Color Secondary;
        public Color Accent;
        public string ShapeToken;
        public string TopologySignature;
        public string RecipeHash;
        public string BuildHash;
        public string CompilerVersion;
        public int ParticleBudget;
        public int RendererBudget = ElementNextCandidateVisualExecutor.AbsoluteMaxRendererCount;
        public int MaterialBudget = 4;
        public int ParticleSystemBudget = 3;
        public int ArcCarrierCount;
        public float MaxLocalExtent;
        public readonly Dictionary<string, JToken> Parameters = new Dictionary<string, JToken>(StringComparer.Ordinal);
        public readonly List<ElementNextBindingPlan> Bindings = new List<ElementNextBindingPlan>();

        public float Number(string key, float fallback)
        {
            JToken token;
            if (!Parameters.TryGetValue(key, out token)) return fallback;
            if (token.Type == JTokenType.Boolean) return (bool)token ? 1f : 0f;
            return Convert.ToSingle(((JValue)token).Value, CultureInfo.InvariantCulture);
        }

        public int Integer(string key, int fallback) { return Mathf.RoundToInt(Number(key, fallback)); }
        public bool Boolean(string key, bool fallback) { JToken token; return Parameters.TryGetValue(key, out token) && token.Type == JTokenType.Boolean ? (bool)token : fallback; }
        public string Text(string key, string fallback) { JToken token; return Parameters.TryGetValue(key, out token) && token.Type == JTokenType.String ? (string)token : fallback; }
        public string CarrierFor(string parameter) { var value = Bindings.FirstOrDefault(item => item.Parameter == parameter); return value == null ? string.Empty : value.Carrier; }
    }

    public sealed class ElementNextCandidatePlanResult
    {
        public ElementNextCandidatePlan Plan;
        public readonly ValidationReport Report = new ValidationReport();
        public bool Succeeded { get { return Plan != null && !Report.HasErrors; } }
    }

    /// <summary>Pure planning half of the new compiler; Patch tests can inspect this without writing assets.</summary>
    public static class ElementNextCandidatePlanCompiler
    {
        public const string CompilerVersionW3W5 = "element-next-w3-w5-1";
        public const string CompilerVersionW6W8 = "element-next-w6-w8-1";
        // Compatibility alias retained for the already-delivered W3-W5 cohort and its gates.
        public const string CompilerVersion = CompilerVersionW3W5;
        public const string VisualStatus = "VISUAL_PENDING";

        public static ElementNextCandidatePlanResult PlanJson(string sourceRecipePath, string recipeJson)
        {
            var result = new ElementNextCandidatePlanResult();
            var validation = RecipeValidator.Validate(recipeJson, VfxCompiler.LoadFormalCatalog());
            result.Report.AddRange(validation);
            if (result.Report.HasErrors) return result;
            var parsed = VfxDomainParser.ParseRecipe(recipeJson);
            result.Report.AddRange(parsed.Report);
            if (parsed.Value == null || result.Report.HasErrors) return result;
            var recipe = parsed.Value;
            ElementNextCandidateFamily family;
            if (recipe.Content == null || !TryFamily(recipe.Content.Family, out family))
            {
                result.Report.Add("E1930", ValidationSeverity.Error, "/content/family", "The element next-candidate compiler accepts only the W3-W8 authority families.", recipe.Content == null ? null : new JValue(recipe.Content.Family), "fire|frost|lightning|water|wind|earth|nature|toxic|holy|shadow|arcane");
                return result;
            }
            ElementNextCandidateProfile profile;
            if (!TryProfile(recipe.Id, out profile))
            {
                result.Report.Add("E1931", ValidationSeverity.Error, "/id", "Recipe is not registered in the W3-W8 element next-candidate cohort.", new JValue(recipe.Id));
                return result;
            }
            if (!ProfileBelongsToFamily(profile, family))
            {
                result.Report.Add("E1932", ValidationSeverity.Error, "/content/family", "Recipe id and element family disagree.", new JValue(recipe.Content.Family));
                return result;
            }

            var plan = new ElementNextCandidatePlan
            {
                SourceRecipePath = sourceRecipePath,
                EffectId = recipe.Id,
                Family = family,
                Profile = profile,
                Lifecycle = Lifecycle(profile),
                Duration = Duration(recipe, profile),
                Seed = recipe.RandomSeed,
                Primary = Palette(recipe.Style, "primary", DefaultPrimary(family)),
                Secondary = Palette(recipe.Style, "secondary", Color.white),
                Accent = Palette(recipe.Style, "accent", Color.white),
                ShapeToken = Shape(profile),
                ParticleBudget = ParticleBudget(profile),
                RendererBudget = RendererBudget(profile),
                MaterialBudget = 3,
                ParticleSystemBudget = 1,
                ArcCarrierCount = ArcCarrierBudget(profile),
                CompilerVersion = CompilerVersionFor(profile)
            };
            foreach (var pair in recipe.Content.Parameters.OrderBy(item => item.Key, StringComparer.Ordinal)) plan.Parameters.Add(pair.Key, pair.Value.DeepClone());
            foreach (var pair in plan.Parameters)
            {
                var carrier = Carrier(profile, pair.Key);
                var authored = pair.Value.Type == JTokenType.Boolean ? ((bool)pair.Value ? 1f : 0f) : pair.Value.Type == JTokenType.Integer || pair.Value.Type == JTokenType.Float ? Convert.ToSingle(((JValue)pair.Value).Value, CultureInfo.InvariantCulture) : StableTextValue((string)pair.Value);
                plan.Bindings.Add(new ElementNextBindingPlan { Parameter = pair.Key, Carrier = carrier, AuthoredValue = authored });
                if (string.IsNullOrEmpty(carrier)) result.Report.Add("E1933", ValidationSeverity.Error, "/content/parameters/" + pair.Key, "Content parameter has no physical carrier/timing binding.", pair.Value);
            }
            plan.MaxLocalExtent = MaxExtent(plan);
            plan.TopologySignature = TopologySignature(plan);
            plan.RecipeHash = Hash(recipeJson);
            plan.BuildHash = Hash(plan.RecipeHash + "|" + plan.CompilerVersion + "|" + plan.TopologySignature);
            if (plan.ParticleBudget > ElementNextCandidateVisualExecutor.AbsoluteMaxParticleCapacity || plan.RendererBudget > ElementNextCandidateVisualExecutor.AbsoluteMaxRendererCount || plan.MaterialBudget > ElementNextCandidateVisualExecutor.AbsoluteMaxMaterialCount)
                result.Report.Add("E1934", ValidationSeverity.Error, "/targetProfile", "Compiled next-candidate budget exceeds the fixed family ceiling.");
            result.Plan = result.Report.HasErrors ? null : plan;
            return result;
        }

        public static bool TryProfile(string id, out ElementNextCandidateProfile profile)
        {
            switch (id)
            {
                case "flame_slash_2d": profile = ElementNextCandidateProfile.FlameSlash; return true;
                case "fire_nova_burst_3d": profile = ElementNextCandidateProfile.FireNova; return true;
                case "flamethrower_beam_3d": profile = ElementNextCandidateProfile.Flamethrower; return true;
                case "burning_status_aura_2d": profile = ElementNextCandidateProfile.BurningStatus; return true;
                case "ember_rain_area_3d": profile = ElementNextCandidateProfile.EmberRain; return true;
                case "phoenix_dart_projectile_2d": profile = ElementNextCandidateProfile.PhoenixDart; return true;
                case "chain_blast_impact_2d": profile = ElementNextCandidateProfile.ChainBlast; return true;
                case "fire_shield_3d": profile = ElementNextCandidateProfile.FireShield; return true;
                case "ice_spike_spawn_3d": profile = ElementNextCandidateProfile.IceSpike; return true;
                case "blizzard_area_3d": profile = ElementNextCandidateProfile.Blizzard; return true;
                case "frost_breath_beam_2d": profile = ElementNextCandidateProfile.FrostBreath; return true;
                case "ice_shard_projectile_2d": profile = ElementNextCandidateProfile.IceShard; return true;
                case "freeze_status_2d": profile = ElementNextCandidateProfile.FreezeStatus; return true;
                case "crystal_shield_3d": profile = ElementNextCandidateProfile.CrystalShield; return true;
                case "flash_freeze_transform_3d": profile = ElementNextCandidateProfile.FlashFreeze; return true;
                case "thunder_strike_impact_3d": profile = ElementNextCandidateProfile.ThunderStrike; return true;
                case "ball_lightning_projectile_3d": profile = ElementNextCandidateProfile.BallLightning; return true;
                case "static_field_area_2d": profile = ElementNextCandidateProfile.StaticField; return true;
                case "storm_charge_aura_3d": profile = ElementNextCandidateProfile.StormCharge; return true;
                case "electro_slash_2d": profile = ElementNextCandidateProfile.ElectroSlash; return true;
                case "emp_nova_impact_2d": profile = ElementNextCandidateProfile.EmpNova; return true;
                case "volt_shield_3d": profile = ElementNextCandidateProfile.VoltShield; return true;
                default: return ElementNextCandidatePlanW6W8.TryProfile(id, out profile);
            }
        }

        public static string CompilerVersionFor(ElementNextCandidateProfile profile)
        {
            return profile <= ElementNextCandidateProfile.VoltShield ? CompilerVersionW3W5 : CompilerVersionW6W8;
        }

        public static bool TryFamily(string value, out ElementNextCandidateFamily family)
        {
            switch (value)
            {
                case "fire": family=ElementNextCandidateFamily.Fire; return true;
                case "frost": family=ElementNextCandidateFamily.Frost; return true;
                case "lightning": family=ElementNextCandidateFamily.Lightning; return true;
                case "water": family=ElementNextCandidateFamily.Water; return true;
                case "wind": family=ElementNextCandidateFamily.Wind; return true;
                case "earth": family=ElementNextCandidateFamily.Earth; return true;
                case "nature": family=ElementNextCandidateFamily.Nature; return true;
                case "toxic": family=ElementNextCandidateFamily.Toxic; return true;
                case "holy": family=ElementNextCandidateFamily.Holy; return true;
                case "shadow": family=ElementNextCandidateFamily.Shadow; return true;
                case "arcane": family=ElementNextCandidateFamily.Arcane; return true;
                default: family=default(ElementNextCandidateFamily); return false;
            }
        }

        private static bool ProfileBelongsToFamily(ElementNextCandidateProfile profile, ElementNextCandidateFamily family)
        {
            var value = (int)profile;
            if (profile > ElementNextCandidateProfile.VoltShield) return ElementNextCandidatePlanW6W8.ProfileBelongsToFamily(profile, family);
            return family == ElementNextCandidateFamily.Fire ? value <= (int)ElementNextCandidateProfile.FireShield
                : family == ElementNextCandidateFamily.Frost ? value >= (int)ElementNextCandidateProfile.IceSpike && value <= (int)ElementNextCandidateProfile.FlashFreeze
                : family == ElementNextCandidateFamily.Lightning && value >= (int)ElementNextCandidateProfile.ThunderStrike;
        }

        private static StyledVfxLifecycle Lifecycle(ElementNextCandidateProfile profile)
        {
            if (profile == ElementNextCandidateProfile.FireShield || profile == ElementNextCandidateProfile.CrystalShield || profile == ElementNextCandidateProfile.VoltShield) return StyledVfxLifecycle.EventDriven;
            if (profile == ElementNextCandidateProfile.Flamethrower || profile == ElementNextCandidateProfile.BurningStatus || profile == ElementNextCandidateProfile.EmberRain || profile == ElementNextCandidateProfile.Blizzard || profile == ElementNextCandidateProfile.FrostBreath || profile == ElementNextCandidateProfile.FreezeStatus || profile == ElementNextCandidateProfile.StaticField || profile == ElementNextCandidateProfile.StormCharge) return StyledVfxLifecycle.Sustained;
            return profile > ElementNextCandidateProfile.VoltShield ? ElementNextCandidatePlanW6W8.Lifecycle(profile) : StyledVfxLifecycle.OneShot;
        }

        private static float Duration(Recipe recipe, ElementNextCandidateProfile profile)
        {
            var value = Mathf.Max(.1f, (float)recipe.Stages.Where(stage => stage.Enabled).Sum(stage => stage.Duration));
            if (profile == ElementNextCandidateProfile.FreezeStatus) value = Mathf.Max(value, Read(recipe, "duration", 2f));
            if (profile == ElementNextCandidateProfile.FlashFreeze) value = Mathf.Max(value, Read(recipe, "freeze_duration", .45f) + .35f);
            return value;
        }

        private static float Read(Recipe recipe, string key, float fallback)
        {
            JToken token; return recipe.Content != null && recipe.Content.Parameters.TryGetValue(key, out token) ? Convert.ToSingle(((JValue)token).Value, CultureInfo.InvariantCulture) : fallback;
        }

        private static string Shape(ElementNextCandidateProfile profile)
        {
            switch (profile)
            {
                case ElementNextCandidateProfile.FlameSlash: return "combustion_crescent";
                case ElementNextCandidateProfile.FireNova: return "erupting_tongue_ring";
                case ElementNextCandidateProfile.Flamethrower: return "layered_flame_cone";
                case ElementNextCandidateProfile.BurningStatus: return "multi_lick_cluster";
                case ElementNextCandidateProfile.EmberRain: return "burn_patch_field";
                case ElementNextCandidateProfile.PhoenixDart: return "winged_phoenix";
                case ElementNextCandidateProfile.ChainBlast: return "sequenced_blast_rosette";
                case ElementNextCandidateProfile.FireShield: return "orbiting_flame_shell";
                case ElementNextCandidateProfile.IceSpike: return "patterned_crystal_spikes";
                case ElementNextCandidateProfile.Blizzard: return "wind_cut_snow_volume";
                case ElementNextCandidateProfile.FrostBreath: return "faceted_frost_fan";
                case ElementNextCandidateProfile.IceShard: return "variant_ice_prism";
                case ElementNextCandidateProfile.FreezeStatus: return "polygon_freeze_shell";
                case ElementNextCandidateProfile.CrystalShield: return "hex_crystal_petals";
                case ElementNextCandidateProfile.FlashFreeze: return "vertical_crystal_reveal";
                case ElementNextCandidateProfile.ThunderStrike: return "instant_branched_strike";
                case ElementNextCandidateProfile.BallLightning: return "tendril_orb";
                case ElementNextCandidateProfile.StaticField: return "jump_arc_net";
                case ElementNextCandidateProfile.StormCharge: return "charged_cloud_circuit";
                case ElementNextCandidateProfile.ElectroSlash: return "jagged_electric_crescent";
                case ElementNextCandidateProfile.EmpNova: return "glitch_concentric_pulse";
                case ElementNextCandidateProfile.VoltShield: return "walking_arc_net_shell";
                default: return ElementNextCandidatePlanW6W8.Shape(profile);
            }
        }

        private static int ParticleBudget(ElementNextCandidateProfile profile)
        {
            switch (profile)
            {
                case ElementNextCandidateProfile.FlameSlash: return 40;
                case ElementNextCandidateProfile.FireNova: return 64;
                case ElementNextCandidateProfile.Flamethrower: return 80;
                case ElementNextCandidateProfile.BurningStatus: return 24;
                case ElementNextCandidateProfile.EmberRain: return 96;
                case ElementNextCandidateProfile.PhoenixDart: return 48;
                case ElementNextCandidateProfile.ChainBlast: return 72;
                case ElementNextCandidateProfile.FireShield: return 48;
                case ElementNextCandidateProfile.IceSpike: return 48;
                case ElementNextCandidateProfile.Blizzard: return 120;
                case ElementNextCandidateProfile.FrostBreath: return 56;
                case ElementNextCandidateProfile.IceShard: return 40;
                case ElementNextCandidateProfile.FreezeStatus: return 32;
                case ElementNextCandidateProfile.CrystalShield: return 40;
                case ElementNextCandidateProfile.FlashFreeze: return 56;
                case ElementNextCandidateProfile.ThunderStrike: return 56;
                case ElementNextCandidateProfile.BallLightning: return 48;
                case ElementNextCandidateProfile.StaticField: return 48;
                case ElementNextCandidateProfile.StormCharge: return 56;
                case ElementNextCandidateProfile.ElectroSlash: return 40;
                case ElementNextCandidateProfile.EmpNova: return 32;
                case ElementNextCandidateProfile.VoltShield: return 40;
                default: return ElementNextCandidatePlanW6W8.ParticleBudget(profile);
            }
        }

        private static int RendererBudget(ElementNextCandidateProfile profile)
        {
            switch (profile)
            {
                case ElementNextCandidateProfile.Flamethrower:
                case ElementNextCandidateProfile.Blizzard:
                case ElementNextCandidateProfile.FrostBreath:
                case ElementNextCandidateProfile.IceShard:
                case ElementNextCandidateProfile.FreezeStatus:
                case ElementNextCandidateProfile.StaticField:
                case ElementNextCandidateProfile.EmpNova:
                    return 6;
                case ElementNextCandidateProfile.BurningStatus:
                    return 5;
                default:
                    return profile > ElementNextCandidateProfile.VoltShield ? ElementNextCandidatePlanW6W8.RendererBudget(profile) : 7;
            }
        }

        private static int ArcCarrierBudget(ElementNextCandidateProfile profile)
        {
            switch (profile)
            {
                case ElementNextCandidateProfile.FlameSlash:
                case ElementNextCandidateProfile.Flamethrower:
                case ElementNextCandidateProfile.PhoenixDart:
                case ElementNextCandidateProfile.StaticField:
                    return 1;
                case ElementNextCandidateProfile.StormCharge:
                case ElementNextCandidateProfile.EmpNova:
                    return 3;
                case ElementNextCandidateProfile.ThunderStrike:
                case ElementNextCandidateProfile.ElectroSlash:
                    return 4;
                case ElementNextCandidateProfile.BallLightning:
                case ElementNextCandidateProfile.VoltShield:
                    return ElementNextCandidateVisualExecutor.MaxArcCarriers;
                default:
                    return ElementNextCandidatePlanW6W8.ArcCarrierBudget(profile);
            }
        }

        private static float MaxExtent(ElementNextCandidatePlan plan)
        {
            switch (plan.Profile)
            {
                case ElementNextCandidateProfile.FlameSlash: return 1.5f;
                case ElementNextCandidateProfile.FireNova: return Mathf.Max(1.55f, plan.Number("radius", 4f) * 1.12f);
                case ElementNextCandidateProfile.Flamethrower: return plan.Number("length", 5f) + .4f;
                case ElementNextCandidateProfile.BurningStatus: return 1.45f;
                case ElementNextCandidateProfile.EmberRain: return Mathf.Max(2.35f, plan.Number("radius", 5f) * 1.08f);
                case ElementNextCandidateProfile.PhoenixDart:
                    return .95f + Mathf.Max(plan.Number("wing_span", 1.4f) * .85f, plan.Number("trail_length", 1.1f) * .5f + .4f);
                case ElementNextCandidateProfile.ChainBlast: return .75f + plan.Number("per_blast_scale", 1f) * 1.1f;
                case ElementNextCandidateProfile.FireShield: return plan.Number("shell_radius", 1.1f) * 1.2f;
                case ElementNextCandidateProfile.IceSpike: return Mathf.Max(1.3f, plan.Number("height", 1.5f) + .15f);
                case ElementNextCandidateProfile.Blizzard:
                {
                    var radius = plan.Number("radius", 6f);
                    return Mathf.Max(radius, radius * .45f + plan.Number("fog_height", .8f) + .4f);
                }
                case ElementNextCandidateProfile.FrostBreath: return plan.Number("length", 4f) + .4f;
                case ElementNextCandidateProfile.IceShard: return 1.1f + plan.Number("trail_length", .8f) * .5f;
                case ElementNextCandidateProfile.FreezeStatus: return 1.5f;
                case ElementNextCandidateProfile.CrystalShield: return plan.Number("orbit_radius", 1.1f) * 1.25f;
                case ElementNextCandidateProfile.FlashFreeze: return Mathf.Max(1.55f, plan.Number("shatter_scale", 1.2f) + .2f);
                case ElementNextCandidateProfile.ThunderStrike: return Mathf.Max(2f, plan.Number("strike_height", 7f));
                case ElementNextCandidateProfile.BallLightning:
                    return .9f + Mathf.Max(plan.Number("orb_radius", .55f) * 2.1f, plan.Number("discharge_range", 3f) * .5f);
                case ElementNextCandidateProfile.StaticField: return plan.Number("radius", 3f) * 1.1f;
                case ElementNextCandidateProfile.StormCharge: return Mathf.Max(1.3f, plan.Number("cloud_height", 1.8f) + .8f);
                case ElementNextCandidateProfile.ElectroSlash: return 1.35f + plan.Number("jag_amplitude", .35f);
                case ElementNextCandidateProfile.EmpNova: return plan.Number("ring_radius", 3f) * 1.1f;
                case ElementNextCandidateProfile.VoltShield: return 1.9f;
                default: return ElementNextCandidatePlanW6W8.MaxExtent(plan);
            }
        }

        private static string Carrier(ElementNextCandidateProfile profile, string key)
        {
            switch (profile)
            {
                case ElementNextCandidateProfile.FlameSlash: return key == "spark_count" ? "DetailBatch.embers" : key == "arc_width" ? "PrimaryFlameCrescent.width" : "PrimaryFlameCrescent.sweep_geometry";
                case ElementNextCandidateProfile.FireNova: return key == "tongue_count" ? "PrimaryEruptionRing.tongue_topology" : key == "scorch_lifetime" ? "ResidualScorch.lifetime" : key == "ring_speed" ? "PrimaryEruptionRing.expansion_timing" : "PrimaryEruptionRing.radius";
                case ElementNextCandidateProfile.Flamethrower: return key == "length" ? "PrimaryFlameCone.length" : key == "cone_angle" ? "PrimaryFlameCone.aperture" : key == "intensity" ? "HighlightCore.energy" : "PrimaryFlameCone.fuel_palette";
                case ElementNextCandidateProfile.BurningStatus: return key == "flame_count" ? "PrimaryFlameCluster.topology" : "HighlightCore.tick_timing";
                case ElementNextCandidateProfile.EmberRain: return key == "radius" ? "PrimaryBurnField.radius" : key == "rain_density" ? "DetailBatch.rain_count" : key == "tick_interval" ? "EventCarrier.tick_timing" : "ResidualBurnPatches.topology";
                case ElementNextCandidateProfile.PhoenixDart: return key == "wing_span" ? "PrimaryPhoenix.wing_geometry" : key == "trail_length" ? "OuterHeatTrail.length" : "DetailBatch.impact_feathers";
                case ElementNextCandidateProfile.ChainBlast: return key == "blast_count" ? "EventCarrier.sequence_count" : key == "interval" ? "EventCarrier.sequence_timing" : key == "per_blast_scale" ? "PrimaryBlast.scale" : "EventCarrier.spatial_topology";
                case ElementNextCandidateProfile.FireShield: return key == "shell_radius" ? "PrimaryFlameShell.radius" : key == "orbit_speed" ? "HighlightOrbit.angular_timing" : "EventCarrier.hit_eruption_scale";
                case ElementNextCandidateProfile.IceSpike: return key == "spike_count" ? "PrimaryCrystalSpikes.topology" : key == "height" ? "PrimaryCrystalSpikes.height" : key == "pattern" ? "PrimaryCrystalSpikes.layout" : "ResidualCrystal.exit_timing";
                case ElementNextCandidateProfile.Blizzard: return key == "radius" ? "PrimarySnowVolume.radius" : key == "wind_dir" ? "DetailBatch.wind_vector" : key == "density" ? "DetailBatch.snow_count" : "OuterFrostMist.height";
                case ElementNextCandidateProfile.FrostBreath: return key == "cone_angle" ? "PrimaryFrostFan.aperture" : key == "length" ? "PrimaryFrostFan.length" : "DetailBatch.crystal_count";
                case ElementNextCandidateProfile.IceShard: return key == "spin_speed" ? "PrimaryIcePrism.spin_timing" : key == "trail_length" ? "OuterFrostTrail.length" : "PrimaryIcePrism.variant_geometry";
                case ElementNextCandidateProfile.FreezeStatus: return key == "shell_opacity" ? "PrimaryFreezeShell.opacity" : key == "duration" ? "PrimaryFreezeShell.hold_timing" : "DetailBatch.fracture_count";
                case ElementNextCandidateProfile.CrystalShield: return key == "petal_count" ? "PrimaryCrystalPetals.topology" : key == "orbit_radius" ? "PrimaryCrystalPetals.radius" : "EventCarrier.hit_highlight_palette";
                case ElementNextCandidateProfile.FlashFreeze: return key == "freeze_duration" ? "PrimaryVerticalCrystal.hold_timing" : key == "rise_speed" ? "PrimaryVerticalCrystal.growth_timing" : "DetailBatch.fracture_scale";
                case ElementNextCandidateProfile.ThunderStrike: return key == "strike_height" ? "ArcMain.height" : key == "fork_count" ? "ArcBranchBatch.topology" : key == "ground_arc_count" ? "DetailBatch.ground_arcs" : "ArcMain.flash_timing";
                case ElementNextCandidateProfile.BallLightning: return key == "orb_radius" ? "PrimaryChargeOrb.radius" : key == "tendril_count" ? "ArcTendrilBatch.topology" : key == "drift_wobble" ? "PrimaryChargeOrb.drift_timing" : "ArcTendrilBatch.discharge_range";
                case ElementNextCandidateProfile.StaticField: return key == "radius" ? "PrimaryElectricNet.radius" : key == "arc_frequency" ? "ArcJump.flash_timing" : key == "tick_interval" ? "EventCarrier.tick_timing" : "PrimaryElectricNet.opacity";
                case ElementNextCandidateProfile.StormCharge: return key == "cloud_height" ? "PrimaryChargeCloud.height" : key == "arc_swap_interval" ? "ArcBodyBatch.discrete_timing" : "ArcBodyBatch.charge_tier";
                case ElementNextCandidateProfile.ElectroSlash: return key == "jag_amplitude" ? "ArcMain.jagged_geometry" : key == "afterimage_count" ? "ArcAfterimageBatch.flash_count" : "DetailBatch.spark_count";
                case ElementNextCandidateProfile.EmpNova: return key == "ring_radius" ? "PrimaryEmpRings.radius" : key == "glitch_strength" ? "PrimaryEmpRings.discrete_offset" : "PrimaryEmpRings.topology";
                case ElementNextCandidateProfile.VoltShield: return key == "net_density" ? "PrimaryVoltNet.geometry_density" : key == "walk_arc_count" ? "ArcWalkBatch.topology" : "EventCarrier.counter_discharge";
                default: return ElementNextCandidatePlanW6W8.Carrier(profile, key);
            }
        }

        private static Color DefaultPrimary(ElementNextCandidateFamily family)
        {
            switch (family)
            {
                case ElementNextCandidateFamily.Fire: return new Color(1f,.24f,.02f);
                case ElementNextCandidateFamily.Frost: return new Color(.28f,.78f,1f);
                case ElementNextCandidateFamily.Lightning: return new Color(.25f,.62f,1f);
                case ElementNextCandidateFamily.Water: return new Color(.08f,.5f,.82f);
                case ElementNextCandidateFamily.Wind: return new Color(.55f,.78f,.72f);
                case ElementNextCandidateFamily.Earth: return new Color(.38f,.24f,.12f);
                case ElementNextCandidateFamily.Nature: return new Color(.18f,.62f,.2f);
                case ElementNextCandidateFamily.Toxic: return new Color(.45f,.75f,.08f);
                case ElementNextCandidateFamily.Holy: return new Color(1f,.86f,.38f);
                case ElementNextCandidateFamily.Shadow: return new Color(.15f,.04f,.24f);
                default: return new Color(.46f,.2f,.9f);
            }
        }

        private static Color Palette(RecipeStyleContract style, string key, Color fallback)
        {
            string value; Color parsed; return style != null && style.Palette.TryGetValue(key, out value) && ColorUtility.TryParseHtmlString(value, out parsed) ? parsed : fallback;
        }

        private static string TopologySignature(ElementNextCandidatePlan plan)
        {
            var content = string.Join("|", plan.Parameters.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value.ToString(Newtonsoft.Json.Formatting.None)).ToArray());
            var bindings = string.Join("|", plan.Bindings.OrderBy(item => item.Parameter, StringComparer.Ordinal).Select(item => item.Parameter + "->" + item.Carrier).ToArray());
            return plan.EffectId + "|" + plan.Family + "|" + plan.Profile + "|" + plan.ShapeToken + "|" + plan.Lifecycle + "|" + plan.Duration.ToString("R", CultureInfo.InvariantCulture) + "|" + content + "|" + bindings;
        }

        private static float StableTextValue(string value)
        {
            unchecked { uint hash = 2166136261; foreach (var character in value ?? string.Empty) { hash ^= character; hash *= 16777619; } return hash & 0x00ffffffu; }
        }

        private static string Hash(string value)
        {
            using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)).Select(item => item.ToString("x2", CultureInfo.InvariantCulture)).ToArray());
        }
    }
}
