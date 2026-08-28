using UnityEngine;

namespace VFXComposer.Editor.Elements
{
    /// <summary>Authority mapping for W6-W8.  Kept separate so the frozen W3-W5 table remains auditable.</summary>
    internal static class ElementNextCandidatePlanW6W8
    {
        internal static bool TryProfile(string id, out ElementNextCandidateProfile profile)
        {
            switch (id)
            {
                case "water_jet_beam_3d": profile=ElementNextCandidateProfile.WaterJet; return true;
                case "tidal_wave_area_3d": profile=ElementNextCandidateProfile.TidalWave; return true;
                case "bubble_shield_2d": profile=ElementNextCandidateProfile.BubbleShield; return true;
                case "splash_impact_2d": profile=ElementNextCandidateProfile.SplashImpact; return true;
                case "whirlpool_spawn_3d": profile=ElementNextCandidateProfile.Whirlpool; return true;
                case "tornado_area_3d": profile=ElementNextCandidateProfile.Tornado; return true;
                case "wind_blade_slash_2d": profile=ElementNextCandidateProfile.WindBlade; return true;
                case "gale_dash_trail_2d": profile=ElementNextCandidateProfile.GaleDash; return true;
                case "earth_spike_spawn_3d": profile=ElementNextCandidateProfile.EarthSpike; return true;
                case "boulder_projectile_3d": profile=ElementNextCandidateProfile.Boulder; return true;
                case "quake_stomp_impact_3d": profile=ElementNextCandidateProfile.QuakeStomp; return true;
                case "thorn_snare_area_2d": profile=ElementNextCandidateProfile.ThornSnare; return true;
                case "vine_whip_slash_2d": profile=ElementNextCandidateProfile.VineWhip; return true;
                case "healing_bloom_aura_2d": profile=ElementNextCandidateProfile.HealingBloom; return true;
                case "spore_burst_impact_2d": profile=ElementNextCandidateProfile.SporeBurst; return true;
                case "acid_lob_projectile_2d": profile=ElementNextCandidateProfile.AcidLob; return true;
                case "divine_smite_impact_3d": profile=ElementNextCandidateProfile.DivineSmite; return true;
                case "holy_halo_aura_2d": profile=ElementNextCandidateProfile.HolyHalo; return true;
                case "resurrection_spawn_3d": profile=ElementNextCandidateProfile.Resurrection; return true;
                case "shadow_claw_slash_2d": profile=ElementNextCandidateProfile.ShadowClaw; return true;
                case "void_orb_projectile_3d": profile=ElementNextCandidateProfile.VoidOrb; return true;
                case "shadow_grasp_area_2d": profile=ElementNextCandidateProfile.ShadowGrasp; return true;
                case "curse_mark_status_2d": profile=ElementNextCandidateProfile.CurseMark; return true;
                case "arcane_missile_projectile_2d": profile=ElementNextCandidateProfile.ArcaneMissile; return true;
                case "arcane_rune_spawn_2d": profile=ElementNextCandidateProfile.ArcaneRune; return true;
                default: profile=default(ElementNextCandidateProfile); return false;
            }
        }

        internal static bool ProfileBelongsToFamily(ElementNextCandidateProfile profile, ElementNextCandidateFamily family)
        {
            return family == ElementNextCandidateFamily.Water ? profile >= ElementNextCandidateProfile.WaterJet && profile <= ElementNextCandidateProfile.Whirlpool
                : family == ElementNextCandidateFamily.Wind ? profile >= ElementNextCandidateProfile.Tornado && profile <= ElementNextCandidateProfile.GaleDash
                : family == ElementNextCandidateFamily.Earth ? profile >= ElementNextCandidateProfile.EarthSpike && profile <= ElementNextCandidateProfile.QuakeStomp
                : family == ElementNextCandidateFamily.Nature ? profile >= ElementNextCandidateProfile.ThornSnare && profile <= ElementNextCandidateProfile.HealingBloom
                : family == ElementNextCandidateFamily.Toxic ? profile >= ElementNextCandidateProfile.SporeBurst && profile <= ElementNextCandidateProfile.AcidLob
                : family == ElementNextCandidateFamily.Holy ? profile >= ElementNextCandidateProfile.DivineSmite && profile <= ElementNextCandidateProfile.Resurrection
                : family == ElementNextCandidateFamily.Shadow ? profile >= ElementNextCandidateProfile.ShadowClaw && profile <= ElementNextCandidateProfile.CurseMark
                : family == ElementNextCandidateFamily.Arcane && profile >= ElementNextCandidateProfile.ArcaneMissile && profile <= ElementNextCandidateProfile.ArcaneRune;
        }

        internal static StyledVfxLifecycle Lifecycle(ElementNextCandidateProfile profile)
        {
            switch (profile)
            {
                case ElementNextCandidateProfile.WaterJet:
                case ElementNextCandidateProfile.Tornado:
                case ElementNextCandidateProfile.ThornSnare:
                case ElementNextCandidateProfile.HealingBloom:
                case ElementNextCandidateProfile.HolyHalo:
                case ElementNextCandidateProfile.ShadowGrasp:
                case ElementNextCandidateProfile.CurseMark:
                    return StyledVfxLifecycle.Sustained;
                case ElementNextCandidateProfile.BubbleShield:
                case ElementNextCandidateProfile.GaleDash:
                case ElementNextCandidateProfile.Boulder:
                case ElementNextCandidateProfile.AcidLob:
                case ElementNextCandidateProfile.VoidOrb:
                case ElementNextCandidateProfile.ArcaneMissile:
                    return StyledVfxLifecycle.EventDriven;
                default:
                    return StyledVfxLifecycle.OneShot;
            }
        }

        internal static string Shape(ElementNextCandidateProfile profile)
        {
            switch (profile)
            {
                case ElementNextCandidateProfile.WaterJet: return "pressurized_strand_jet";
                case ElementNextCandidateProfile.TidalWave: return "curling_travel_wall";
                case ElementNextCandidateProfile.BubbleShield: return "wobbling_bubble_crescent";
                case ElementNextCandidateProfile.SplashImpact: return "crown_ring_splash";
                case ElementNextCandidateProfile.Whirlpool: return "accelerating_vortex_column";
                case ElementNextCandidateProfile.Tornado: return "debris_readable_funnel";
                case ElementNextCandidateProfile.WindBlade: return "three_thin_wind_arcs";
                case ElementNextCandidateProfile.GaleDash: return "flowline_afterimage_trail";
                case ElementNextCandidateProfile.EarthSpike: return "sequential_wedge_fault";
                case ElementNextCandidateProfile.Boulder: return "faceted_weighted_boulder";
                case ElementNextCandidateProfile.QuakeStomp: return "radial_crack_rock_lift";
                case ElementNextCandidateProfile.ThornSnare: return "revealed_thorn_snare";
                case ElementNextCandidateProfile.VineWhip: return "propagating_sine_vine";
                case ElementNextCandidateProfile.HealingBloom: return "sequential_petalled_bloom";
                case ElementNextCandidateProfile.SporeBurst: return "double_pulse_spore_cloud";
                case ElementNextCandidateProfile.AcidLob: return "viscous_blob_corrosion_pool";
                case ElementNextCandidateProfile.DivineSmite: return "ordered_smite_pillar_cross";
                case ElementNextCandidateProfile.HolyHalo: return "tilted_halo_cross_dust";
                case ElementNextCandidateProfile.Resurrection: return "gate_column_feather_spiral";
                case ElementNextCandidateProfile.ShadowClaw: return "staggered_negative_claw_tears";
                case ElementNextCandidateProfile.VoidOrb: return "solid_void_event_horizon";
                case ElementNextCandidateProfile.ShadowGrasp: return "swallow_pool_revealed_hands";
                case ElementNextCandidateProfile.CurseMark: return "variant_curse_glyph";
                case ElementNextCandidateProfile.ArcaneMissile: return "staggered_wobble_missiles";
                default: return "ordered_counter_rotating_runes";
            }
        }

        internal static int ParticleBudget(ElementNextCandidateProfile profile)
        {
            switch (profile)
            {
                case ElementNextCandidateProfile.WaterJet: return 72;
                case ElementNextCandidateProfile.TidalWave: return 96;
                case ElementNextCandidateProfile.BubbleShield: return 32;
                case ElementNextCandidateProfile.SplashImpact: return 40;
                case ElementNextCandidateProfile.Whirlpool: return 56;
                case ElementNextCandidateProfile.Tornado: return 88;
                case ElementNextCandidateProfile.WindBlade: return 24;
                case ElementNextCandidateProfile.GaleDash: return 40;
                case ElementNextCandidateProfile.EarthSpike: return 64;
                case ElementNextCandidateProfile.Boulder: return 64;
                case ElementNextCandidateProfile.QuakeStomp: return 72;
                case ElementNextCandidateProfile.ThornSnare: return 40;
                case ElementNextCandidateProfile.VineWhip: return 32;
                case ElementNextCandidateProfile.HealingBloom: return 48;
                case ElementNextCandidateProfile.SporeBurst: return 56;
                case ElementNextCandidateProfile.AcidLob: return 40;
                case ElementNextCandidateProfile.DivineSmite: return 56;
                case ElementNextCandidateProfile.HolyHalo: return 32;
                case ElementNextCandidateProfile.Resurrection: return 64;
                case ElementNextCandidateProfile.ShadowClaw: return 40;
                case ElementNextCandidateProfile.VoidOrb: return 48;
                case ElementNextCandidateProfile.ShadowGrasp: return 48;
                case ElementNextCandidateProfile.CurseMark: return 24;
                case ElementNextCandidateProfile.ArcaneMissile: return 56;
                default: return 32;
            }
        }

        internal static int RendererBudget(ElementNextCandidateProfile profile)
        {
            switch (profile)
            {
                case ElementNextCandidateProfile.WaterJet:
                case ElementNextCandidateProfile.Whirlpool:
                case ElementNextCandidateProfile.Tornado:
                case ElementNextCandidateProfile.WindBlade:
                case ElementNextCandidateProfile.VineWhip:
                case ElementNextCandidateProfile.Boulder:
                case ElementNextCandidateProfile.ThornSnare:
                case ElementNextCandidateProfile.HealingBloom:
                case ElementNextCandidateProfile.VoidOrb:
                    return 6;
                case ElementNextCandidateProfile.TidalWave:
                case ElementNextCandidateProfile.Resurrection:
                    return 7;
                case ElementNextCandidateProfile.BubbleShield:
                case ElementNextCandidateProfile.SplashImpact:
                case ElementNextCandidateProfile.SporeBurst:
                case ElementNextCandidateProfile.AcidLob:
                case ElementNextCandidateProfile.HolyHalo:
                case ElementNextCandidateProfile.CurseMark:
                    return 5;
                default:
                    return 7;
            }
        }

        internal static int ArcCarrierBudget(ElementNextCandidateProfile profile)
        {
            switch (profile)
            {
                case ElementNextCandidateProfile.WaterJet:
                case ElementNextCandidateProfile.Whirlpool:
                case ElementNextCandidateProfile.VineWhip:
                case ElementNextCandidateProfile.Resurrection:
                    return 1;
                case ElementNextCandidateProfile.Tornado:
                case ElementNextCandidateProfile.HolyHalo:
                case ElementNextCandidateProfile.DivineSmite:
                case ElementNextCandidateProfile.ArcaneRune:
                    return 2;
                case ElementNextCandidateProfile.WindBlade:
                case ElementNextCandidateProfile.ThornSnare:
                case ElementNextCandidateProfile.ShadowGrasp:
                    return 3;
                case ElementNextCandidateProfile.GaleDash:
                case ElementNextCandidateProfile.ShadowClaw:
                    return 4;
                case ElementNextCandidateProfile.QuakeStomp:
                case ElementNextCandidateProfile.ArcaneMissile:
                    return 5;
                default:
                    return 0;
            }
        }

        internal static float MaxExtent(ElementNextCandidatePlan plan)
        {
            switch (plan.Profile)
            {
                case ElementNextCandidateProfile.WaterJet: return plan.Number("length",6f)*1.12f+.65f;
                case ElementNextCandidateProfile.TidalWave: return Mathf.Max(plan.Number("travel_distance",6f)*.55f+plan.Number("wave_width",4f)*.6f+1f,3f+plan.Number("curl_amount",.65f)*2f);
                case ElementNextCandidateProfile.BubbleShield: return Mathf.Max(plan.Number("bubble_radius",1.2f)*1.5f,plan.Number("pop_splash_scale",1.2f)*1.7f)+.2f;
                case ElementNextCandidateProfile.SplashImpact: return plan.Number("crown_scale",1.2f)*2.1f+.2f;
                case ElementNextCandidateProfile.Whirlpool: return Mathf.Max(plan.Number("vortex_radius",2f)*1.3f,plan.Number("column_height",2.2f)+.4f);
                case ElementNextCandidateProfile.Tornado: return plan.Number("height",3.5f)*1.35f+.5f;
                case ElementNextCandidateProfile.WindBlade: return plan.Number("arc_length",3.2f)*.6f+.7f;
                case ElementNextCandidateProfile.GaleDash: return plan.Number("dash_length",5f)*.6f+.7f;
                case ElementNextCandidateProfile.EarthSpike: return plan.Number("line_length",4f)*1.02f+1f;
                case ElementNextCandidateProfile.Boulder: return plan.Number("boulder_scale",1.2f)*2f+.6f;
                case ElementNextCandidateProfile.QuakeStomp: return plan.Number("radius",4f)*1.15f+.4f;
                case ElementNextCandidateProfile.ThornSnare: return plan.Number("radius",3f)*1.2f+.4f;
                case ElementNextCandidateProfile.VineWhip: return plan.Number("whip_length",4f)+plan.Number("wave_amp",.5f)+.4f;
                case ElementNextCandidateProfile.HealingBloom: return 1.9f;
                case ElementNextCandidateProfile.SporeBurst: return plan.Number("cloud_radius",2.4f)*1.4f+.3f;
                case ElementNextCandidateProfile.AcidLob: return plan.Number("blob_scale",1f)*2.4f+.7f;
                case ElementNextCandidateProfile.DivineSmite: return plan.Number("pillar_height",7f)+plan.Number("pillar_radius",.7f)*1.6f;
                case ElementNextCandidateProfile.HolyHalo: return 1.8f;
                case ElementNextCandidateProfile.Resurrection: return Mathf.Max(plan.Number("column_height",5f)+.4f,plan.Number("gate_radius",2f)*1.4f);
                case ElementNextCandidateProfile.ShadowClaw: return 2.2f+plan.Number("tear_jaggedness",.6f)*.4f;
                case ElementNextCandidateProfile.VoidOrb: return Mathf.Max(plan.Number("orb_radius",.7f)*3f,plan.Number("implode_scale",1.25f)*2f)+.3f;
                case ElementNextCandidateProfile.ShadowGrasp: return Mathf.Max(plan.Number("pool_radius",3f)*1.2f,plan.Number("hand_height",1.5f)*1.4f+.5f);
                case ElementNextCandidateProfile.CurseMark: return 1.7f;
                case ElementNextCandidateProfile.ArcaneMissile: return 2.1f+plan.Number("wobble_amp",.22f);
                default: return plan.Number("ring_radius",2f)*1.25f+.3f;
            }
        }

        internal static string Carrier(ElementNextCandidateProfile profile, string key)
        {
            switch (profile)
            {
                case ElementNextCandidateProfile.WaterJet: return key=="length"?"PrimaryWaterStrands.length":key=="pressure"?"PrimaryWaterStrands.thickness_and_speed":"OuterFoam.amount";
                case ElementNextCandidateProfile.TidalWave: return key=="wave_width"?"PrimaryWaveWall.width":key=="travel_distance"?"PrimaryWaveWall.travel_timing":"PrimaryWaveWall.curl_geometry";
                case ElementNextCandidateProfile.BubbleShield: return key=="bubble_radius"?"PrimaryBubble.radius":key=="wobble"?"PrimaryBubble.ellipsoid_wobble":"EventPop.splash_scale";
                case ElementNextCandidateProfile.SplashImpact: return key=="crown_scale"?"PrimaryCrown.scale":key=="droplet_count"?"DetailDroplets.count":"ResidualRings.topology";
                case ElementNextCandidateProfile.Whirlpool: return key=="vortex_radius"?"PrimaryVortex.radius":key=="spin_accel"?"PrimaryVortex.angular_acceleration":"HighlightColumn.height";
                case ElementNextCandidateProfile.Tornado: return key=="height"?"PrimaryFunnel.height":key=="move_speed"?"PrimaryFunnel.drift_timing":"DetailDebris.medium_geometry";
                case ElementNextCandidateProfile.WindBlade: return key=="blade_count"?"ArcBladeBatch.topology":key=="arc_length"?"ArcBladeBatch.length":"DetailLeaves.count";
                case ElementNextCandidateProfile.GaleDash: return key=="dash_length"?"FlowLineBatch.length":key=="afterimage_count"?"ResidualAfterimages.topology":"DetailFlowLines.count";
                case ElementNextCandidateProfile.EarthSpike: return key=="spike_count"?"PrimaryWedgeFault.topology":key=="advance_speed"?"PrimaryWedgeFault.reveal_timing":key=="line_length"?"PrimaryWedgeFault.length":"PrimaryWedgeFault.rock_palette";
                case ElementNextCandidateProfile.Boulder: return key=="boulder_scale"?"PrimaryBoulder.scale":key=="spin"?"PrimaryBoulder.angular_timing":key=="impact_debris_count"?"ImpactDebris.count":"ResidualDust.lifetime";
                case ElementNextCandidateProfile.QuakeStomp: return key=="crack_count"?"ArcCrackBatch.topology":key=="radius"?"PrimaryQuakeDisk.radius":key=="float_rock_count"?"DetailFloatRocks.count":"HighlightMagma.energy";
                case ElementNextCandidateProfile.ThornSnare: return key=="radius"?"PrimaryThornRing.radius":key=="thorn_density"?"PrimaryThornRing.topology":key=="pulse_interval"?"EventPulse.interval":"PrimaryThornRing.wither_timing";
                case ElementNextCandidateProfile.VineWhip: return key=="whip_length"?"ArcVine.length":key=="wave_amp"?"ArcVine.sine_geometry":"DetailLeaves.count";
                case ElementNextCandidateProfile.HealingBloom: return key=="flower_count"?"PrimaryBloom.sequential_topology":"DetailLeaves.rise_timing";
                case ElementNextCandidateProfile.SporeBurst: return key=="cloud_radius"?"PrimarySporeCloud.radius":key=="linger_time"?"ResidualSporeCloud.convergence_timing":"DetailSpores.count";
                case ElementNextCandidateProfile.AcidLob: return key=="blob_scale"?"PrimaryAcidBlob.scale":key=="drip_rate"?"DetailDrips.rate":key=="pool_lifetime"?"ResidualCorrosionPool.lifetime":"ResidualPoolBubbles.rate";
                case ElementNextCandidateProfile.DivineSmite: return key=="pillar_height"?"PrimaryOrderedPillar.height":key=="pillar_radius"?"PrimaryOrderedPillar.radius":key=="feather_count"?"DetailFeathers.count":"OuterAfterglow.lifetime";
                case ElementNextCandidateProfile.HolyHalo: return key=="halo_tilt"?"PrimaryHalo.ellipse_tilt":key=="dust_density"?"DetailHolyDust.count":"EventCross.sparkle_timing";
                case ElementNextCandidateProfile.Resurrection: return key=="gate_radius"?"PrimaryGate.radius":key=="column_height"?"HighlightColumn.height":"DetailFeathers.spiral_timing";
                case ElementNextCandidateProfile.ShadowClaw: return key=="claw_count"?"ArcClawBatch.staggered_topology":key=="tear_jaggedness"?"ArcClawBatch.jagged_geometry":"OuterFallingMist.amount";
                case ElementNextCandidateProfile.VoidOrb: return key=="orb_radius"?"PrimarySolidVoid.radius":key=="suction_particle_rate"?"DetailInwardSpiral.count":"EventImplode.scale";
                case ElementNextCandidateProfile.ShadowGrasp: return key=="pool_radius"?"PrimarySwallowPool.radius":key=="hand_count"?"EventHands.topology":key=="tick_interval"?"EventHands.reveal_timing":"EventHands.height";
                case ElementNextCandidateProfile.CurseMark: return key=="mark_glyph"?"PrimaryCurseMark.glyph_geometry":key=="pulse_rate"?"HighlightGlyph.pulse_timing":"OuterAshSmoke.amount";
                case ElementNextCandidateProfile.ArcaneMissile: return key=="missile_count"?"ArcMissileBatch.topology":key=="stagger_interval"?"ArcMissileBatch.launch_timing":"ArcMissileBatch.wobble_geometry";
                default: return key=="ring_radius"?"PrimaryRuneRings.radius":key=="glyph_count"?"PrimaryRuneRings.glyph_topology":key=="spin_speed"?"PrimaryRuneRings.counter_rotation":"PrimaryRuneRings.activation_order";
            }
        }
    }
}
