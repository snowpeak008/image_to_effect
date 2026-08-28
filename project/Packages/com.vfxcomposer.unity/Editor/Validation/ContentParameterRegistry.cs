using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VFXComposer.Editor.Domain;

namespace VFXComposer.Editor.Validation
{
    public enum ContentParameterKind { Number, Integer, Boolean, String, Enum, IntegerArray }

    public sealed class ContentParameterDefinition
    {
        public string Name; public ContentParameterKind Kind; public double Min; public double Max; public string[] Values; public int ArrayLength;
    }

    public sealed class ContentDefinition
    {
        public string Id; public string Family;
        public readonly Dictionary<string,ContentParameterDefinition> Parameters=new Dictionary<string,ContentParameterDefinition>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Live authority for element-content semantics. Behavior stays in CapabilityRegistry and
    /// visual language stays in VfxStyleRegistry; this registry owns only content identity and
    /// artist-facing semantic controls. Legacy Recipes without a content block remain valid.
    /// </summary>
    public static class ContentParameterRegistry
    {
        private static readonly Dictionary<string,ContentDefinition> Definitions=Build();
        public static IEnumerable<ContentDefinition> All { get { return Definitions.Values.OrderBy(v=>v.Id,StringComparer.Ordinal); } }
        public static bool TryGet(string id,out ContentDefinition definition)
        {
            if(Definitions.TryGetValue(id??string.Empty,out definition))return true;
            foreach(var suffix in new[]{"_stylized","_cartoon","_pixel","_inkwash","_semireal","_holo","_dark","_neon","_lowpoly","_crystal","_candy","_cosmic","_steampunk","_ghost"})if(id!=null&&id.EndsWith(suffix,StringComparison.Ordinal)&&Definitions.TryGetValue(id.Substring(0,id.Length-suffix.Length),out definition))return true;
            return false;
        }

        public static ValidationReport Validate(Recipe recipe)
        {
            var report=new ValidationReport();if(recipe==null||recipe.Content==null)return report;
            ContentDefinition definition;if(!TryGet(recipe.Id,out definition)){report.Add("E1820",ValidationSeverity.Error,"/content","Content block is not registered for this Recipe id.",new JValue(recipe.Id));return report;}
            if(!string.Equals(recipe.Content.Family,definition.Family,StringComparison.Ordinal))report.Add("E1820",ValidationSeverity.Error,"/content/family","Content family does not match the registered Recipe.",new JValue(recipe.Content.Family),definition.Family);
            foreach(var pair in recipe.Content.Parameters){ContentParameterDefinition parameter;if(!definition.Parameters.TryGetValue(pair.Key,out parameter)){report.Add("E1821",ValidationSeverity.Error,"/content/parameters/"+pair.Key,"Content parameter is not registered for this Recipe.",pair.Value,"["+string.Join(", ",definition.Parameters.Keys.OrderBy(v=>v,StringComparer.Ordinal))+ "]");continue;}ValidateValue(pair.Value,parameter,report);}
            foreach(var parameter in definition.Parameters.Values)if(!recipe.Content.Parameters.ContainsKey(parameter.Name))report.Add("E1821",ValidationSeverity.Error,"/content/parameters/"+parameter.Name,"Required content parameter is missing.");
            return report;
        }

        private static void ValidateValue(JToken token,ContentParameterDefinition definition,ValidationReport report)
        {
            var path="/content/parameters/"+definition.Name;
            if(definition.Kind==ContentParameterKind.IntegerArray){var array=token as JArray;if(array==null||array.Count!=definition.ArrayLength){report.Add("E1822",ValidationSeverity.Error,path,"Content parameter must be a fixed-length integer array.",token,"integer["+definition.ArrayLength+"]");return;}for(var i=0;i<array.Count;i++)if(array[i].Type!=JTokenType.Integer||(int)array[i]<definition.Min||(int)array[i]>definition.Max){report.Add("E1822",ValidationSeverity.Error,path+"/"+i,"Content array entry is outside its inclusive range.",array[i],"["+definition.Min+", "+definition.Max+"]");}return;}
            if(definition.Kind==ContentParameterKind.String||definition.Kind==ContentParameterKind.Enum){if(token==null||token.Type!=JTokenType.String||string.IsNullOrWhiteSpace((string)token)){report.Add("E1822",ValidationSeverity.Error,path,"Content parameter must be a non-empty string.",token,definition.Kind==ContentParameterKind.Enum?"["+string.Join(", ",definition.Values)+"]":"string");return;}if(definition.Kind==ContentParameterKind.Enum&&!definition.Values.Contains((string)token,StringComparer.Ordinal))report.Add("E1822",ValidationSeverity.Error,path,"Content parameter is outside the registered enumeration.",token,"["+string.Join(", ",definition.Values)+"]");return;}
            if(definition.Kind==ContentParameterKind.Boolean){if(token==null||token.Type!=JTokenType.Boolean)report.Add("E1822",ValidationSeverity.Error,path,"Content parameter must be boolean.",token,"boolean");return;}
            var integer=definition.Kind==ContentParameterKind.Integer;if(token==null||(integer?token.Type!=JTokenType.Integer:token.Type!=JTokenType.Integer&&token.Type!=JTokenType.Float)){report.Add("E1822",ValidationSeverity.Error,path,"Content parameter has the wrong numeric type.",token,integer?"integer":"number");return;}double value;try{value=Convert.ToDouble(((JValue)token).Value,CultureInfo.InvariantCulture);}catch{value=double.NaN;}if(double.IsNaN(value)||double.IsInfinity(value)||value<definition.Min||value>definition.Max)report.Add("E1822",ValidationSeverity.Error,path,"Content parameter is outside its inclusive range.",token,"["+definition.Min.ToString(CultureInfo.InvariantCulture)+", "+definition.Max.ToString(CultureInfo.InvariantCulture)+"]");
        }

        private static Dictionary<string,ContentDefinition> Build()
        {
            var r=new Dictionary<string,ContentDefinition>(StringComparer.Ordinal);
            Add(r,"flame_slash_2d","fire",N("sweep_angle",60,140),N("arc_width",.3,1),I("spark_count",0,12));
            Add(r,"fire_nova_burst_3d","fire",N("radius",1,6),N("ring_speed",.1,20),I("tongue_count",8,16),N("scorch_lifetime",0,3));
            Add(r,"flamethrower_beam_3d","fire",N("length",2,8),N("cone_angle",10,35),N("intensity",0,8),S("fuel_color"));
            Add(r,"burning_status_aura_2d","fire",I("flame_count",1,4),B("tick_pulse"));
            Add(r,"ember_rain_area_3d","fire",N("radius",2,8),N("rain_density",0,100),N("tick_interval",.05,10),I("burn_patch_count",3,6));
            Add(r,"phoenix_dart_projectile_2d","fire",N("wing_span",.2,4),N("trail_length",.05,5),I("impact_feather_count",0,24));
            Add(r,"chain_blast_impact_2d","fire",I("blast_count",2,4),N("interval",.08,.3),N("per_blast_scale",.2,3),E("spread_pattern","line","triangle"));
            Add(r,"fire_shield_3d","fire",N("shell_radius",.2,5),N("orbit_speed",0,20),N("hit_burst_scale",.1,4));

            Add(r,"ice_spike_spawn_3d","frost",I("spike_count",3,7),N("height",.5,2.5),E("pattern","line","fan","ring"),E("exit_mode","sink","shatter"));
            Add(r,"blizzard_area_3d","frost",N("radius",3,10),E("wind_dir","north_east","east","west"),N("density",0,160),N("fog_height",0,5));
            Add(r,"frost_breath_beam_2d","frost",N("cone_angle",30,70),N("length",1,10),N("crystal_density",0,100));
            Add(r,"ice_shard_projectile_2d","frost",N("spin_speed",0,1440),N("trail_length",0,5),I("shard_variant",1,4));
            Add(r,"freeze_status_2d","frost",N("shell_opacity",0,1),N("duration",.1,30),I("shatter_piece_count",6,10));
            Add(r,"crystal_shield_3d","frost",I("petal_count",4,8),N("orbit_radius",.2,5),S("hit_flash_color"));
            Add(r,"flash_freeze_transform_3d","frost",N("freeze_duration",.1,10),N("rise_speed",.1,20),N("shatter_scale",.1,5));

            Add(r,"thunder_strike_impact_3d","lightning",N("strike_height",4,10),I("fork_count",0,3),I("ground_arc_count",4,6),I("flash_times",1,3));
            Add(r,"ball_lightning_projectile_3d","lightning",N("orb_radius",.1,3),I("tendril_count",3,5),N("drift_wobble",0,3),N("discharge_range",0,20));
            Add(r,"static_field_area_2d","lightning",N("radius",.5,10),N("arc_frequency",.3,.8),N("tick_interval",.05,10),N("net_opacity",0,1));
            Add(r,"storm_charge_aura_3d","lightning",N("cloud_height",.1,5),N("arc_swap_interval",.05,5),I("charge_level",1,3));
            Add(r,"electro_slash_2d","lightning",N("jag_amplitude",0,1),I("afterimage_count",1,3),I("spark_count",0,20));
            Add(r,"emp_nova_impact_2d","lightning",N("ring_radius",.2,10),N("glitch_strength",0,1),I("ring_count",1,3));
            Add(r,"volt_shield_3d","lightning",N("net_density",.1,10),I("walk_arc_count",0,8),B("counter_arc"));

            Add(r,"water_jet_beam_3d","water",N("length",1,12),N("pressure",0,10),N("foam_amount",0,1));
            Add(r,"tidal_wave_area_3d","water",N("wave_width",2,6),N("travel_distance",1,12),N("curl_amount",0,1));
            Add(r,"bubble_shield_2d","water",N("bubble_radius",.2,5),N("wobble",0,1),N("pop_splash_scale",.1,5));
            Add(r,"splash_impact_2d","water",N("crown_scale",.1,5),I("droplet_count",0,24),I("ring_count",1,2));
            Add(r,"whirlpool_spawn_3d","water",N("vortex_radius",.2,8),N("spin_accel",0,30),N("column_height",.1,8));
            Add(r,"tornado_area_3d","wind",N("height",2,5),N("move_speed",0,30),E("debris_type","dust","leaf","snow"));
            Add(r,"wind_blade_slash_2d","wind",I("blade_count",1,3),N("arc_length",.2,8),I("leaf_count",0,12));
            Add(r,"gale_dash_trail_2d","wind",N("dash_length",.2,20),I("afterimage_count",0,3),N("line_density",0,40));

            Add(r,"earth_spike_spawn_3d","earth",I("spike_count",5,8),N("advance_speed",.1,20),N("line_length",2,6),S("rock_tint"));
            Add(r,"boulder_projectile_3d","earth",N("boulder_scale",.2,5),N("spin",0,1440),I("impact_debris_count",5,8),N("dust_lifetime",.1,5));
            Add(r,"quake_stomp_impact_3d","earth",I("crack_count",4,6),N("radius",.5,10),I("float_rock_count",4,6),N("magma_glow",0,1));
            Add(r,"thorn_snare_area_2d","nature",N("radius",.5,10),N("thorn_density",0,40),N("pulse_interval",.05,10),N("wither_time",.1,5));
            Add(r,"vine_whip_slash_2d","nature",N("whip_length",.2,10),N("wave_amp",0,3),I("leaf_count",0,16));
            Add(r,"healing_bloom_aura_2d","nature",I("flower_count",4,6),N("rise_speed",0,10));
            Add(r,"spore_burst_impact_2d","toxic",N("cloud_radius",.2,8),N("linger_time",.1,5),I("spore_count",0,80));
            Add(r,"acid_lob_projectile_2d","toxic",N("blob_scale",.1,5),N("drip_rate",0,30),N("pool_lifetime",.1,10),N("bubble_rate",0,30));

            Add(r,"divine_smite_impact_3d","holy",N("pillar_height",1,12),N("pillar_radius",.1,5),I("feather_count",6,10),N("afterglow",0,3));
            Add(r,"holy_halo_aura_2d","holy",N("halo_tilt",-80,80),N("dust_density",0,80),N("sparkle_rate",0,30));
            Add(r,"resurrection_spawn_3d","holy",N("gate_radius",.2,8),N("column_height",.5,12),N("feather_spiral_speed",0,20));
            Add(r,"shadow_claw_slash_2d","shadow",I("claw_count",2,4),N("tear_jaggedness",0,1),N("mist_amount",0,1));
            Add(r,"void_orb_projectile_3d","shadow",N("orb_radius",.1,5),N("suction_particle_rate",0,80),N("implode_scale",.1,5));
            Add(r,"shadow_grasp_area_2d","shadow",N("pool_radius",.2,8),I("hand_count",2,3),N("tick_interval",.05,10),N("hand_height",.1,5));
            Add(r,"curse_mark_status_2d","shadow",I("mark_glyph",1,4),N("pulse_rate",.1,20),N("smoke_amount",0,1));
            Add(r,"arcane_missile_projectile_2d","arcane",I("missile_count",1,5),N("stagger_interval",0,2),N("wobble_amp",0,3));
            Add(r,"arcane_rune_spawn_2d","arcane",N("ring_radius",.2,8),I("glyph_count",8,12),N("spin_speed",0,30),E("activate_order","forward","reverse","seeded_random"));

            Add(r,"rain_weather_volume","environment",N("intensity",0,1),N("wind_strength",0,20),N("area_size",1,100),B("camera_follow"),N("near_density",0,1),N("mid_density",0,1),N("far_density",0,1));
            Add(r,"sandstorm_weather_volume","environment",N("intensity",0,1),N("wind_strength",0,30),N("area_size",1,100),B("camera_follow"),N("near_density",0,1),N("mid_density",0,1),N("far_density",0,1));
            Add(r,"mist_fog_volume","environment",N("intensity",0,1),N("wind_strength",0,10),N("area_size",1,100),B("camera_follow"),I("fog_layers",2,3));
            Add(r,"falling_leaves_volume","environment",N("intensity",0,1),N("wind_strength",0,20),N("area_size",1,100),B("camera_follow"),N("flip_rate",0,20));
            Add(r,"fireflies_volume","environment",N("intensity",0,1),N("wind_strength",0,5),N("area_size",1,100),B("camera_follow"),N("wander_speed",0,5));
            Add(r,"ambient_dust_volume","environment",N("intensity",0,1),N("wind_strength",0,5),N("area_size",1,100),B("camera_follow"),N("light_band_gain",0,3));
            Add(r,"waterfall_env_3d","environment",N("intensity",0,1),N("wind_strength",0,10),N("area_size",1,100),B("camera_follow"),N("curtain_width",.5,20),N("mist_amount",0,1));

            Add(r,"hit_flash_status_2d","hit_feedback",I("flash_frames",1,3),S("tint"),N("edge_width",0,1));
            Add(r,"critical_strike_impact_2d","hit_feedback",I("crack_count",6,10),N("star_scale",.1,5),E("palette","gold","red","purple"));
            Add(r,"parry_spark_impact_3d","hit_feedback",I("spark_count",8,16),N("cone_angle",5,120),I("bounce",0,2));
            Add(r,"knockup_launcher_impact_3d","hit_feedback",N("column_height",2,4),N("ring_scale",.1,5),I("debris_count",0,24));
            Add(r,"combo_surge_aura_2d","hit_feedback",I("stack_level",1,5),S("per_level_palette"),B("pulse_on_levelup"));
            Add(r,"elemental_reaction_burst_2d","hit_feedback",S("color_a"),S("color_b"),S("result_color"),N("burst_scale",.1,5),N("swirl_turns",.1,8));
            Add(r,"lifesteal_link_beam_2d","hit_feedback",N("drain_rate",0,40),N("sag",0,2),S("palette"));

            Add(r,"heal_glow_ui","screen_ui",N("intensity",0,1),S("palette"));
            Add(r,"poison_veil_ui","screen_ui",N("intensity",0,1),S("palette"),I("stack_level",1,3));
            Add(r,"levelup_burst_ui","screen_ui",N("intensity",0,1),S("palette"));
            Add(r,"skill_ready_flash_ui","screen_ui",N("intensity",0,1),S("palette"),B("follow_anchor"));
            Add(r,"screen_shatter_transition_ui","screen_ui",N("intensity",0,1),S("palette"),I("fragment_count",20,30));
            Add(r,"frost_creep_ui","screen_ui",N("intensity",0,1),S("palette"),I("stack_level",1,3));

            Add(r,"button_press_fx_ui","game_ui",S("palette"),B("scale_with_button"));
            Add(r,"button_confirm_burst_ui","game_ui",S("palette"),B("scale_with_button"));
            Add(r,"card_flip_reveal_ui","game_ui",I("rarity",1,5),N("flash_scale",.1,5));
            Add(r,"card_merge_fx_ui","game_ui",I("source_count",2,3),I("result_rarity",1,5));
            Add(r,"chest_open_burst_ui","game_ui",N("leak_intensity",0,1),N("burst_scale",.1,5),I("tease_count",3,5));
            Add(r,"gacha_single_reveal_ui","game_ui",I("rarity",1,5),N("buildup_time",0,10),N("fullscreen_grace",0,.8));
            Add(r,"gacha_ten_sequence_ui","game_ui",IA("rarities",10,1,5),N("reveal_interval",.05,3));
            Add(r,"reward_fly_collect_ui","game_ui",I("item_count",1,12),N("arc_height",0,5),N("stagger",0,1));
            Add(r,"daily_check_stamp_ui","game_ui",S("stamp_tint"),B("ink_ring"));
            Add(r,"progress_charge_fx_ui","game_ui",S("bar_rect"),S("palette"),N("fill_ratio",0,1));

            Add(r,"pixel_burst_impact_2d","style_special",N("burst_scale",.1,5),I("debris_count",4,12));
            Add(r,"pixel_sword_slash_2d","style_special",I("arc_frames",3,4),I("star_count",1,12));
            Add(r,"pixel_heal_aura_2d","style_special",S("symbol_mix"),N("rise_speed",0,10));
            Add(r,"anime_smear_slash_2d","style_special",N("smear_scale",.1,5),I("speedline_count",3,5),S("palette"));
            Add(r,"poof_smoke_spawn_2d","style_special",N("poof_scale",.1,5),I("satellite_count",4,4));
            Add(r,"anime_charge_aura_2d","style_special",I("flame_atlas_row",0,1),I("intensity",1,3));
            Add(r,"ink_slash_2d","style_special",N("stroke_width",.05,2),S("accent_color"));
            Add(r,"ink_splash_impact_2d","style_special",I("splash_count",1,12),N("bleed_time",.05,3));
            Add(r,"ink_dragon_trail_2d","style_special",N("body_width",.05,2),N("fade_bleed",0,2));

            Add(r,"real_explosion_impact_3d","style_special",N("blast_scale",.1,8),I("fireball_count",3,4),B("dust_ring"),N("smoke_lifetime",.2,5));
            Add(r,"smoke_plume_area_3d","style_special",N("plume_height",.5,10),N("wind_bend",0,5),B("ember_glow"));
            Add(r,"muzzle_flash_impact_3d","style_special",N("flash_scale",.1,5),I("petal_count",4,6));
            Add(r,"holo_barrier_shield_3d","style_special",N("hex_density",1,32),N("scan_speed",0,20),E("barrier_shape","plane","arc","sphere"));
            Add(r,"holo_scan_area_3d","style_special",N("scan_interval",.05,10),I("mark_count",2,3));
            Add(r,"glitch_blink_transform_3d","style_special",N("voxel_size",.02,1),N("blink_distance",0,30),N("reassemble_time",.05,3));
            Add(r,"blood_ritual_spawn_3d","style_special",N("circle_radius",.2,8),S("rune_set"),I("candle_count",3,8),N("smoke_height",.2,10));
            Add(r,"soul_drain_beam_3d","style_special",N("drain_rate",0,50),I("wisp_count",1,24),N("sag",0,3));
            Add(r,"demon_eruption_impact_3d","style_special",N("hand_scale",.1,5),N("black_fire_amount",0,1),N("ash_lifetime",.1,5));

            Add(r,"poly_burst_impact_3d","style_special",I("piece_count",6,24),N("spread",.1,8));
            Add(r,"gem_lance_projectile_3d","style_special",N("lance_length",.2,8),N("refraction",0,1));
            Add(r,"candy_pop_impact_2d","style_special",I("symbol_count",4,24),N("bounce",0,2));
            Add(r,"nebula_orb_projectile_3d","style_special",N("star_density",0,1),N("parallax",0,2));
            Add(r,"steam_vent_burst_impact_3d","style_special",I("gear_count",2,12),N("steam_pressure",0,10));
            Add(r,"phantom_wail_area_2d","style_special",I("ghost_count",1,12),N("drift",0,5));
            return r;
        }

        private static void Add(Dictionary<string,ContentDefinition> r,string id,string family,params ContentParameterDefinition[] parameters){var d=new ContentDefinition{Id=id,Family=family};foreach(var p in parameters)d.Parameters.Add(p.Name,p);r.Add(id,d);}
        private static ContentParameterDefinition N(string n,double min,double max){return new ContentParameterDefinition{Name=n,Kind=ContentParameterKind.Number,Min=min,Max=max};}
        private static ContentParameterDefinition I(string n,double min,double max){return new ContentParameterDefinition{Name=n,Kind=ContentParameterKind.Integer,Min=min,Max=max};}
        private static ContentParameterDefinition B(string n){return new ContentParameterDefinition{Name=n,Kind=ContentParameterKind.Boolean};}
        private static ContentParameterDefinition S(string n){return new ContentParameterDefinition{Name=n,Kind=ContentParameterKind.String};}
        private static ContentParameterDefinition IA(string n,int length,double min,double max){return new ContentParameterDefinition{Name=n,Kind=ContentParameterKind.IntegerArray,ArrayLength=length,Min=min,Max=max};}
        private static ContentParameterDefinition E(string n,params string[] values){return new ContentParameterDefinition{Name=n,Kind=ContentParameterKind.Enum,Values=values};}
    }
}
