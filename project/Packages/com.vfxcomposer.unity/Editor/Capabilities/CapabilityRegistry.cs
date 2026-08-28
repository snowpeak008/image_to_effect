using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VFXComposer.Editor.Domain;

namespace VFXComposer.Editor.Capabilities
{
    public enum CapabilityParameterKind { Number, Integer, Boolean, String }

    public sealed class CapabilityParameterContract
    {
        public string Name;
        public CapabilityParameterKind Kind;
        public double Min;
        public double Max;
        public string[] Values;
        public bool IsSlot;
    }

    public sealed class CapabilityDefinition
    {
        public string Domain;
        public string Token;
        public readonly HashSet<RecipeArchetype> Archetypes = new HashSet<RecipeArchetype>();
        public readonly Dictionary<string, CapabilityParameterContract> Parameters = new Dictionary<string, CapabilityParameterContract>(StringComparer.Ordinal);
        public string MigratedFrom;
    }

    public static class CapabilityRegistry
    {
        private static readonly Dictionary<string, CapabilityDefinition> Definitions = BuildDefinitions();
        private static readonly HashSet<string> StyleTokens = new HashSet<string>(new[] { "stylized", "cartoon", "pixel", "inkwash", "semireal", "holo", "dark", "neon", "lowpoly", "crystal", "candy", "cosmic", "steampunk", "ghost" }, StringComparer.Ordinal);

        public static IEnumerable<CapabilityDefinition> All { get { return Definitions.Values.OrderBy(value => value.Domain, StringComparer.Ordinal).ThenBy(value => value.Token, StringComparer.Ordinal); } }
        public static bool TryGet(string domain, string token, out CapabilityDefinition definition) { return Definitions.TryGetValue(Key(domain, token), out definition); }

        public static ValidationReport Validate(Recipe recipe)
        {
            var report = new ValidationReport();
            if (recipe == null) return report;
            ValidateStyle(recipe.Style, report);
            if (recipe.Behavior == null) return report;
            foreach (var block in recipe.Behavior.Blocks()) ValidateBlock(recipe.Archetype, block, report);
            ValidateCombination(recipe, report);
            return report;
        }

        private static void ValidateStyle(RecipeStyleContract style, ValidationReport report)
        {
            if (style == null) return;
            if (!StyleTokens.Contains(style.Token ?? string.Empty)) report.Add("E326", ValidationSeverity.Error, "/style/token", "Style token is not registered.", new JValue(style.Token), "[" + string.Join(", ", StyleTokens.OrderBy(value => value, StringComparer.Ordinal)) + "]");
            foreach (var color in style.Palette)
            {
                Color parsed;
                if (!ColorUtility.TryParseHtmlString(color.Value, out parsed)) report.Add("E327", ValidationSeverity.Error, "/style/palette/" + color.Key, "Palette value must be a valid HTML color.", new JValue(color.Value), "#RRGGBB or #RRGGBBAA");
            }
            ValidateStyleNumber(style, "outline", 0, 1, report);
            ValidateStyleNumber(style, "shading_steps", 1, 8, report, true);
            ValidateStyleNumber(style, "noise_scale", .01, 32, report);
            ValidateStyleNumber(style, "glow_strength", 0, 8, report);
            ValidateStyleNumber(style, "snap_fps", 1, 60, report, true);
            ValidateStyleNumber(style, "virtual_res", 32, 256, report, true);
            ValidateStyleNumber(style, "atlas_fps", 1, 60, report);
            ValidateStyleNumber(style, "ink_density", 0, 1, report);
            ValidateStyleNumber(style, "bleed_radius", 0, 2, report);
            ValidateStyleNumber(style, "flyaway_threshold", 0, 1, report);
            ValidateStyleNumber(style, "noise_primary_speed", 0, 10, report);
            ValidateStyleNumber(style, "noise_detail_speed", 0, 20, report);
            ValidateStyleNumber(style, "glitch_rate", 0, 60, report);
            ValidateStyleNumber(style, "glitch_offset", 0, 1, report);
            ValidateStyleNumber(style, "dispersion_strength", 0, 1, report);
            ValidateStyleNumber(style, "step_fps", 1, 60, report, true);
            ValidateStyleNumber(style, "ghost_pulse_fps", .1, 30, report);
            ValidateStyleString(style,"palette_lut",report);ValidateStyleString(style,"atlas_id",report);ValidateStyleString(style,"loop_mode",report);ValidateStyleString(style,"facet_mesh",report);ValidateStyleString(style,"squash_curve",report);ValidateStyleString(style,"nebula_noise",report);
            JToken flag;if(style.Parameters.TryGetValue("flat_shading",out flag)&&flag.Type!=JTokenType.Boolean)report.Add("E327",ValidationSeverity.Error,"/style/flat_shading","Style parameter has the wrong type.",flag,"boolean");
        }

        private static void ValidateStyleString(RecipeStyleContract style,string name,ValidationReport report){JToken token;if(!style.Parameters.TryGetValue(name,out token))return;if(token.Type!=JTokenType.String||string.IsNullOrWhiteSpace((string)token))report.Add("E327",ValidationSeverity.Error,"/style/"+name,"Style parameter must be a non-empty string.",token,"string");}

        private static void ValidateStyleNumber(RecipeStyleContract style, string name, double min, double max, ValidationReport report, bool integer = false)
        {
            JToken token; if (!style.Parameters.TryGetValue(name, out token)) return; double value;
            if ((integer && token.Type != JTokenType.Integer) || (!integer && token.Type != JTokenType.Integer && token.Type != JTokenType.Float) || !TryFinite(token, out value)) { report.Add("E327", ValidationSeverity.Error, "/style/" + name, "Style parameter has the wrong type.", token, integer ? "integer" : "number"); return; }
            if (value < min || value > max) report.Add("E327", ValidationSeverity.Error, "/style/" + name, "Style parameter is outside the supported range.", token, Range(min, max));
        }

        private static void ValidateBlock(RecipeArchetype archetype, RecipeCapabilityBlock block, ValidationReport report)
        {
            var path = "/behavior/" + block.Domain; CapabilityDefinition definition;
            if (!TryGet(block.Domain, block.Type ?? string.Empty, out definition)) { report.Add("E320", ValidationSeverity.Error, path + "/type", "Capability token is not registered for this behavior domain.", new JValue(block.Type), AllowList(block.Domain)); return; }
            if (!definition.Archetypes.Contains(archetype)) report.Add("E324", ValidationSeverity.Error, path + "/type", "Capability is incompatible with the Recipe archetype.", new JValue(block.Type), "[" + string.Join(", ", definition.Archetypes.OrderBy(value => value).Select(ToToken).ToArray()) + "]");
            foreach (var parameter in block.Parameters)
            {
                CapabilityParameterContract contract; var parameterPath = path + "/" + parameter.Key;
                if (!definition.Parameters.TryGetValue(parameter.Key, out contract)) { report.Add("E321", ValidationSeverity.Error, parameterPath, "Capability parameter is not declared by the registered contract.", parameter.Value, "[" + string.Join(", ", definition.Parameters.Keys.OrderBy(value => value, StringComparer.Ordinal)) + "]"); continue; }
                ValidateParameter(parameter.Value, contract, parameterPath, report);
            }
        }

        private static void ValidateParameter(JToken token, CapabilityParameterContract contract, string path, ValidationReport report)
        {
            if (contract.Kind == CapabilityParameterKind.String)
            {
                if (token == null || token.Type != JTokenType.String) { report.Add("E322", ValidationSeverity.Error, path, "Capability parameter has the wrong type.", token, "string"); return; }
                if (contract.IsSlot && string.IsNullOrWhiteSpace((string)token)) report.Add("E323", ValidationSeverity.Error, path, "Visual slot must reference a non-empty Recipe ID.", token, "non-empty Recipe ID");
                else if (contract.Values != null && !contract.Values.Contains((string)token, StringComparer.Ordinal)) report.Add("E323", ValidationSeverity.Error, path, "Capability parameter is outside the registered enumeration.", token, "[" + string.Join(", ", contract.Values) + "]");
                return;
            }
            if (contract.Kind == CapabilityParameterKind.Boolean)
            {
                if (token == null || token.Type != JTokenType.Boolean) report.Add("E322", ValidationSeverity.Error, path, "Capability parameter has the wrong type.", token, "boolean");
                return;
            }
            if (token == null || (contract.Kind == CapabilityParameterKind.Integer ? token.Type != JTokenType.Integer : token.Type != JTokenType.Integer && token.Type != JTokenType.Float)) { report.Add("E322", ValidationSeverity.Error, path, "Capability parameter has the wrong type.", token, contract.Kind == CapabilityParameterKind.Integer ? "integer" : "number"); return; }
            double value; if (!TryFinite(token, out value) || value < contract.Min || value > contract.Max) report.Add("E323", ValidationSeverity.Error, path, "Capability parameter is outside the registered range.", token, Range(contract.Min, contract.Max));
        }

        private static void ValidateCombination(Recipe recipe, ValidationReport report)
        {
            var behavior = recipe.Behavior; var motion = behavior.Motion == null ? null : behavior.Motion.Type; var hit = behavior.Hit == null ? null : behavior.Hit.Type; var emission = behavior.Emission == null ? null : behavior.Emission.Type; var timing = behavior.Timing == null ? null : behavior.Timing.Type;
            if (timing == "hitscan" && motion != null && motion != "linear") report.Add("E325", ValidationSeverity.Error, "/behavior", "hitscan cannot be combined with a moving trajectory.", new JValue(motion), "motion omitted or linear");
            if (recipe.Archetype == RecipeArchetype.Beam && (motion == "parabola" || motion == "boomerang" || motion == "bounce" || motion == "orbit_then_strike")) report.Add("E325", ValidationSeverity.Error, "/behavior/motion/type", "Beam does not accept projectile-only trajectories.", new JValue(motion), "beam-compatible motion");
            if (hit == "split" && emission == "converge") report.Add("E325", ValidationSeverity.Error, "/behavior", "split cannot be combined with converge emission.", new JValue("split+converge"), "non-conflicting topology");
            if (timing == "channel_interrupt" && recipe.Archetype != RecipeArchetype.Beam && recipe.Archetype != RecipeArchetype.Area && recipe.Archetype != RecipeArchetype.Aura) report.Add("E325", ValidationSeverity.Error, "/behavior/timing/type", "Channel timing requires Beam, Area, or Aura.", new JValue(ToToken(recipe.Archetype)), "[beam, area, aura]");
        }

        private static Dictionary<string, CapabilityDefinition> BuildDefinitions()
        {
            var result = new Dictionary<string, CapabilityDefinition>(StringComparer.Ordinal);
            Add(result, "motion", "linear", A(RecipeArchetype.Projectile, RecipeArchetype.Trail), N("speed", 0, 100));
            Add(result, "motion", "accel", A(RecipeArchetype.Projectile), N("init_speed", 0, 100), N("accel", -100, 100), N("max_speed", 0, 200));
            Add(result, "motion", "parabola", A(RecipeArchetype.Projectile), N("apex_height", 0, 100), N("flight_time", .01, 60));
            Add(result, "motion", "homing", A(RecipeArchetype.Projectile), "seeker_orb_3d", N("turn_rate", 0, 1440), N("max_speed", .01, 200), S("lose_target_mode", "straight", "expire"));
            Add(result, "motion", "wave", A(RecipeArchetype.Projectile, RecipeArchetype.Trail), N("speed", 0, 100), N("amplitude", 0, 20), N("frequency", .01, 50));
            Add(result, "motion", "boomerang", A(RecipeArchetype.Projectile), N("out_distance", .01, 100), N("hover_time", 0, 20), N("return_speed", .01, 200), N("speed", .01, 200));
            Add(result, "motion", "bounce", A(RecipeArchetype.Projectile), I("bounce_count", 1, 32), N("energy_damping", 0, 1));
            Add(result, "motion", "orbit_then_strike", A(RecipeArchetype.Projectile), N("orbit_radius", .01, 50), N("orbit_turns", .1, 20), N("orbit_time", .01, 60), N("strike_speed", .01, 200));
            Add(result, "motion", "sweep", A(RecipeArchetype.Beam), N("sweep_speed_max", 0, 1440), N("inertia", 0, 10));
            Add(result, "motion", "dash", A(RecipeArchetype.Trail, RecipeArchetype.Transform), "phase_dash_3d", N("distance", 0, 100), N("duration", .01, 10));
            Add(result, "motion", "expand_ring", A(RecipeArchetype.Area, RecipeArchetype.Impact), N("max_radius", .01, 100), N("expand_speed", .01, 100), N("edge_thickness", .001, 20));
            Add(result, "motion", "implode", A(RecipeArchetype.Area, RecipeArchetype.Impact), N("start_radius", .01, 100), N("collapse_time", .01, 60));
            Add(result, "motion", "moving_zone", A(RecipeArchetype.Area), N("follow_lag", 0, 10), Slot("residue_slot"));
            Add(result, "motion", "growth_stage", A(RecipeArchetype.Area), I("stage_count", 2, 3), N("base_radius", .01, 100));

            Add(result, "hit", "single", AAll());
            Add(result, "hit", "pierce", A(RecipeArchetype.Projectile, RecipeArchetype.Beam), I("max_hits", 1, 64), N("damping_per_hit", 0, 1), Slot("impact_slot"));
            Add(result, "hit", "split", A(RecipeArchetype.Projectile), I("child_count", 2, 32), N("split_angle", 0, 360), S("trigger", "hit", "range"));
            Add(result, "hit", "chain_hop", A(RecipeArchetype.Projectile), I("hop_count", 1, 32), N("hop_range", 0, 100), N("damping", 0, 1));
            Add(result, "hit", "reflect", A(RecipeArchetype.Beam), I("max_segments", 1, 16), N("damping_per_bounce", 0, 1), Slot("impact_slot"));
            Add(result, "hit", "occlude", A(RecipeArchetype.Beam), N("probe_interval", 0, 1), Slot("burn_point"), Slot("impact_slot"));
            Add(result, "hit", "arc_link", A(RecipeArchetype.Beam), "chain_arc_3d|channel_tether_3d", I("hop_count", 1, 32), N("sag", 0, 10), N("jitter", 0, 10), Slot("impact_slot"));

            Add(result, "emission", "single", AAll());
            Add(result, "emission", "fan", A(RecipeArchetype.Projectile), I("count", 2, 64), N("spread_angle", 0, 360));
            Add(result, "emission", "burst_stagger", A(RecipeArchetype.Projectile), I("count", 2, 64), N("stagger", 0, 10));
            Add(result, "emission", "ring", A(RecipeArchetype.Projectile, RecipeArchetype.Impact), I("count", 3, 128), N("ring_radius", 0, 100));
            Add(result, "emission", "volley_showcase", A(RecipeArchetype.Projectile), I("fan_count", 2, 24), N("fan_spread_angle", 0, 360), I("burst_count", 2, 24), N("burst_stagger", 0, 10), I("ring_count", 3, 24), N("ring_radius", 0, 100), N("phase_duration", .1, 10));
            Add(result, "emission", "converge", A(RecipeArchetype.Beam), I("source_count", 2, 5), N("focus_growth", 0, 20));

            Add(result, "timing", "instant", AAll());
            Add(result, "timing", "hitscan", A(RecipeArchetype.Beam), N("max_range", 0, 1000), N("linger", .01, 1));
            Add(result, "timing", "sustained", A(RecipeArchetype.Beam, RecipeArchetype.Area, RecipeArchetype.Aura, RecipeArchetype.Environment, RecipeArchetype.ScreenUi, RecipeArchetype.Status), "channel_tether_3d");
            Add(result, "timing", "charge_scale", A(RecipeArchetype.Beam), N("level_1", 0, 60), N("level_2", 0, 60), N("per_level_width", 0, 10));
            Add(result, "timing", "telegraph", A(RecipeArchetype.Impact, RecipeArchetype.Area), "warning_telegraph_3d", N("warn_duration", 0, 60), S("shape", "circle", "fan", "rectangle", "ring"), S("fill_style", "edge_collapse", "center_fill"), Slot("impact_slot"));
            Add(result, "timing", "delay_fuse", A(RecipeArchetype.Impact, RecipeArchetype.Projectile), N("fuse_time", 0, 60), B("blink_accelerate"));
            Add(result, "timing", "tick_pulse", A(RecipeArchetype.Area, RecipeArchetype.Aura), "static_field", N("tick_interval", .01, 60), Slot("tick_visual_slot"));
            Add(result, "timing", "charge_release", A(RecipeArchetype.Projectile, RecipeArchetype.Impact, RecipeArchetype.Aura), N("level_1", 0, 60), N("level_2", 0, 60), N("per_level_scale", 0, 10), B("overcharge"));
            Add(result, "timing", "channel_interrupt", A(RecipeArchetype.Beam, RecipeArchetype.Area, RecipeArchetype.Aura), N("channel_time", .01, 600), N("interrupt_scatter_scale", 0, 20));
            Add(result, "timing", "chain_sequence", A(RecipeArchetype.Impact, RecipeArchetype.Area), "chain_blast", I("count", 1, 64), N("interval", .01, 60), S("topology", "line", "ring", "converge"), Slot("impact_slot"));
            return result;
        }

        private static void Add(Dictionary<string, CapabilityDefinition> result, string domain, string token, RecipeArchetype[] archetypes, params CapabilityParameterContract[] parameters) { Add(result, domain, token, archetypes, null, parameters); }
        private static void Add(Dictionary<string, CapabilityDefinition> result, string domain, string token, RecipeArchetype[] archetypes, string migratedFrom, params CapabilityParameterContract[] parameters)
        {
            var definition = new CapabilityDefinition { Domain = domain, Token = token, MigratedFrom = migratedFrom }; foreach (var value in archetypes) definition.Archetypes.Add(value); foreach (var value in parameters) definition.Parameters[value.Name] = value; result.Add(Key(domain, token), definition);
        }
        private static CapabilityParameterContract N(string name, double min, double max) { return P(name, CapabilityParameterKind.Number, min, max); }
        private static CapabilityParameterContract I(string name, double min, double max) { return P(name, CapabilityParameterKind.Integer, min, max); }
        private static CapabilityParameterContract B(string name) { return P(name, CapabilityParameterKind.Boolean, 0, 0); }
        private static CapabilityParameterContract S(string name, params string[] values) { return new CapabilityParameterContract { Name = name, Kind = CapabilityParameterKind.String, Values = values }; }
        private static CapabilityParameterContract Slot(string name) { return new CapabilityParameterContract { Name = name, Kind = CapabilityParameterKind.String, IsSlot = true }; }
        private static CapabilityParameterContract P(string name, CapabilityParameterKind kind, double min, double max) { return new CapabilityParameterContract { Name = name, Kind = kind, Min = min, Max = max }; }
        private static RecipeArchetype[] A(params RecipeArchetype[] values) { return values; }
        private static RecipeArchetype[] AAll() { return (RecipeArchetype[])Enum.GetValues(typeof(RecipeArchetype)); }
        private static string Key(string domain, string token) { return (domain ?? string.Empty) + "." + (token ?? string.Empty); }
        private static string AllowList(string domain) { return "[" + string.Join(", ", Definitions.Values.Where(value => value.Domain == domain).Select(value => value.Token).OrderBy(value => value, StringComparer.Ordinal)) + "]"; }
        private static string ToToken(RecipeArchetype value) { return value == RecipeArchetype.ScreenUi ? "screen_ui" : value.ToString().ToLowerInvariant(); }
        private static string Range(double min, double max) { return "[" + min.ToString(CultureInfo.InvariantCulture) + ", " + max.ToString(CultureInfo.InvariantCulture) + "]"; }
        private static bool TryFinite(JToken token, out double value) { value = 0; try { value = Convert.ToDouble(((JValue)token).Value, CultureInfo.InvariantCulture); return !double.IsNaN(value) && !double.IsInfinity(value); } catch { return false; } }
    }
}
