using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VFXComposer.W11W13NextCandidate;

namespace VFXComposer.Editor.NextCandidates
{
    public sealed class W11W13NextDefinition
    {
        public string Id;
        public string SourceId;
        public string Group;
        public string Archetype;
        public string Lifecycle;
        public W11W13NextFamily Family;
        public W11W13NextVariant Variant;
        public float Duration;
        public Color Primary;
        public Color Secondary;
        public Color Accent;
        public int RendererBudget;
        public int ParticleBudget;
        public string[] RequiredCarriers;
        public string[] Dependencies;
    }

    /// <summary>Frozen identities and protocol plans for the parallel W11/W12/W13 candidate line.</summary>
    public static class W11W13NextCandidatePlan
    {
        public const string RecipeSchema = "w11-w13-next-candidate/v1";
        public const string VisualStatus = "NEXT_CANDIDATE_VISUAL_PENDING";
        public const string RecipeRoot = "Assets/VFX/Recipes/W11W13NextCandidate";

        public static readonly W11W13NextDefinition[] Definitions =
        {
            D("w11nc_rain_weather_volume","rain_weather_volume","W11","environment","sustained",W11W13NextFamily.Environment,W11W13NextVariant.Rain,4f,"#4EA4D9","#B9E4FF","#FFFFFF",6,160,A("NearRainStreaks","MidRainCurtain","GroundSplashRipples","FarMist")),
            D("w11nc_sandstorm_weather_volume","sandstorm_weather_volume","W11","environment","sustained",W11W13NextFamily.Environment,W11W13NextVariant.Sandstorm,4.4f,"#9C672F","#D7A85E","#FFE6AE",6,160,A("CrosswindSandVeil","RollingDustKnots","GroundSkimSand","OccasionalGrit")),
            D("w11nc_mist_fog_volume","mist_fog_volume","W11","environment","sustained",W11W13NextFamily.Environment,W11W13NextVariant.MistFog,5f,"#708C99","#C9E0E5","#F4FCFF",6,160,A("LowFogBandA","LowFogBandB","TornFogEdge","BreathingDepthLayer")),
            D("w11nc_falling_leaves_volume","falling_leaves_volume","W11","environment","sustained",W11W13NextFamily.Environment,W11W13NextVariant.FallingLeaves,4.2f,"#B75A20","#E9A43B","#97B84E",6,160,A("NearTumblingLeaves","MidSwayLeaves","GroundSlideTail")),
            D("w11nc_fireflies_volume","fireflies_volume","W11","environment","sustained",W11W13NextFamily.Environment,W11W13NextVariant.Fireflies,4.8f,"#7CCB43","#DAFF77","#FFF7B5",6,160,A("WanderingGlowPoints","PairedOrbitMotes","NearLensMotes")),
            D("w11nc_ambient_dust_volume","ambient_dust_volume","W11","environment","sustained",W11W13NextFamily.Environment,W11W13NextVariant.AmbientDust,5.2f,"#9D8C75","#DEC9A7","#FFF2CF",6,160,A("AmbientFineDust","LightBandDust","VisibleLightShaft")),
            D("w11nc_waterfall_env_3d","waterfall_env_3d","W11","environment","sustained",W11W13NextFamily.Environment,W11W13NextVariant.Waterfall,4f,"#1F84C5","#78D9FF","#F4FDFF",6,160,A("CurvedWaterCurtain","WhiteWaterStrands","ImpactMistVolume","SplashPearls","DownstreamFoam")),

            D("w12nc_hit_flash_status_2d","hit_flash_status_2d","W12","status","one_shot",W11W13NextFamily.HitFeedback,W11W13NextVariant.HitFlash,.18f,"#FFFFFF","#FF3D37","#FFF3E9",3,8,A("ExternalRendererMpbFlash","DirectionalHitSparks")),
            D("w12nc_critical_strike_impact_2d","critical_strike_impact_2d","W12","impact","one_shot",W11W13NextFamily.HitFeedback,W11W13NextVariant.CriticalStrike,.35f,"#FF9B18","#FFE44F","#FFFFFF",6,32,A("RadialHardCracks","FourPointStar","TiltedImpactRing","GoldDebris")),
            D("w12nc_parry_spark_impact_3d","parry_spark_impact_3d","W12","impact","one_shot",W11W13NextFamily.HitFeedback,W11W13NextVariant.ParrySpark,.5f,"#E07B20","#FFD36A","#FFFFFF",5,40,A("CollisionSparkFan","ContactFlashRing","FallingMetalTails")),
            D("w12nc_knockup_launcher_impact_3d","knockup_launcher_impact_3d","W12","impact","one_shot",W11W13NextFamily.HitFeedback,W11W13NextVariant.KnockupLauncher,.6f,"#4695D8","#BDE9FF","#FFFFFF",6,48,A("GroundLaunchRing","VerticalAirColumn","RisingCoreLines","ThrownDebris")),
            D("w12nc_combo_surge_aura_2d","combo_surge_aura_2d","W12","aura","sustained",W11W13NextFamily.HitFeedback,W11W13NextVariant.ComboSurge,1.2f,"#FFF0C8","#F5B42D","#F14D27",7,56,A("FiveIndependentStackRings","LevelUpFootPulse","RisingComboMotes")),
            D("w12nc_elemental_reaction_burst_2d","elemental_reaction_burst_2d","W12","impact","one_shot",W11W13NextFamily.HitFeedback,W11W13NextVariant.ElementalReaction,.62f,"#FF5C18","#359CFF","#E9FBFF",7,48,A("ApproachEnergyA","ApproachEnergyB","FusionResultBody","DualColorSpiral","OpposedFragments")),
            D("w12nc_lifesteal_link_beam_2d","lifesteal_link_beam_2d","W12","beam","sustained",W11W13NextFamily.HitFeedback,W11W13NextVariant.LifestealLink,1.4f,"#76132D","#DB355B","#FFE9EE",6,40,A("SaggingDynamicLink","ReverseFlowMotes","TargetMist","CasterIntake")),

            U("w13nc_dragon_breath_ultimate_3d","dragon_breath_ultimate_3d",W11W13NextVariant.DragonBreath,4f,"#B72212","#FF6C1C","#FFE387",A("ChargeContinuityBody","DragonHeadSilhouette","SweepingBreathVolume","AfterburnField"),A("focus_charge_3d","flamethrower_beam_3d","ember_rain_area_3d","fire_nova_burst_3d")),
            U("w13nc_meteor_shower_ultimate_3d","meteor_shower_ultimate_3d",W11W13NextVariant.MeteorShower,5f,"#7E2614","#FF7B2D","#FFD59A",A("SkyWarningField","SixIndependentMeteorBodies","ImpactSequence","ClosingDustFront"),A("warning_telegraph_3d","meteor_impact_3d","meteor_impact_3d","meteor_impact_3d","meteor_impact_3d","meteor_impact_3d","meteor_impact_3d","smoke_plume_area_3d")),
            U("w13nc_frozen_domain_ultimate_3d","frozen_domain_ultimate_3d",W11W13NextVariant.FrozenDomain,6f,"#2A70AE","#7ADFFF","#F3FFFF",A("ExpandingIceBoundary","PersistentFrozenDomain","IndependentIceSpikes","DomainShatterRelease"),A("blizzard_area_3d","flash_freeze_transform_3d","ice_spike_spawn_3d")),
            U("w13nc_judgement_ray_ultimate_3d","judgement_ray_ultimate_3d",W11W13NextVariant.JudgementRay,4.2f,"#C58B17","#FFE36B","#FFFFFF",A("LayeredRuneArray","ContinuousFocusCore","VolumetricJudgementColumn","AshFeatherTail"),A("arcane_rune_spawn_2d","focus_charge_3d","divine_smite_impact_3d")),
            U("w13nc_demon_gate_boss_3d","demon_gate_boss_3d",W11W13NextVariant.DemonGate,8f,"#3C0C18","#B42B37","#FF8A45",A("BloodRitualFloor","DeepGateFrame","BreakingDemonHand","ThreatWaveTail"),A("blood_ritual_spawn_3d","rift_spawn_3d","demon_eruption_impact_3d")),
            U("w13nc_blade_tempest_ultimate_3d","blade_tempest_ultimate_3d",W11W13NextVariant.BladeTempest,5f,"#5F1720","#E73D39","#FFF0CD",A("DrawStanceContinuity","EightSpatialSlashCarriers","TempestVolume","SheatheFlashTail"),A("focus_charge_3d","slash_3d_stylized","slash_3d_stylized","slash_3d_stylized","slash_3d_stylized","slash_3d_stylized","slash_3d_stylized","slash_3d_stylized","slash_3d_stylized","parry_spark_impact_3d"))
        };

        public static IEnumerable<W11W13NextDefinition> Group(string group) { return Definitions.Where(value => string.Equals(value.Group, group, StringComparison.Ordinal)); }
        public static string RecipePath(W11W13NextDefinition definition) { return RecipeRoot + "/" + definition.Group + "/" + definition.Id + ".default.json"; }

        public static W11W13TimelineCue[] Timeline(W11W13NextDefinition definition)
        {
            if (definition.Family != W11W13NextFamily.Ultimate) return new W11W13TimelineCue[0];
            switch (definition.Variant)
            {
                case W11W13NextVariant.DragonBreath:
                    return C(Q(0,0,true),Q(.58f,1,true),Q(.62f,0,false),Q(1.05f,1,false),Q(1.08f,2,true),Q(2.85f,2,false),Q(2.88f,3,true),Q(3.48f,3,false));
                case W11W13NextVariant.MeteorShower:
                    return C(Q(0,0,true),Q(.85f,0,false),Q(1.0f,1,true,P(-1.4f,0,0)),Q(1.36f,1,false),Q(1.38f,2,true,P(-.85f,0,.2f)),Q(1.76f,2,false),Q(1.78f,3,true,P(-.25f,0,-.15f)),Q(2.16f,3,false),Q(2.18f,4,true,P(.35f,0,.22f)),Q(2.56f,4,false),Q(2.58f,5,true,P(.9f,0,-.2f)),Q(2.98f,5,false),Q(3.0f,6,true,P(1.4f,0,.1f)),Q(3.68f,6,false),Q(3.75f,7,true),Q(4.65f,7,false));
                case W11W13NextVariant.FrozenDomain:
                    return C(Q(0,0,true),Q(1.25f,1,true),Q(2.2f,1,false),Q(3.55f,0,false),Q(3.65f,2,true),Q(5.55f,2,false));
                case W11W13NextVariant.JudgementRay:
                    return C(Q(0,0,true),Q(.55f,1,true),Q(1.5f,0,false),Q(1.55f,2,true),Q(3.15f,1,false),Q(3.55f,2,false));
                case W11W13NextVariant.DemonGate:
                    return C(Q(0,0,true),Q(1.15f,1,true),Q(3.15f,0,false),Q(3.3f,2,true),Q(6.5f,1,false),Q(7.1f,2,false));
                default:
                    var list = new List<W11W13TimelineCue> { Q(0,0,true), Q(.74f,0,false) };
                    for (var index = 0; index < 8; index++) list.Add(Q(.75f + index * .38f, index + 1, true, P(Mathf.Cos(index * Mathf.PI / 4f) * .55f, Mathf.Sin(index * Mathf.PI / 4f) * .55f, (index % 2) * .12f), new Vector3(0, 0, index * 45f), .85f, "hit"));
                    for (var index = 0; index < 8; index++) list.Add(Q(1.12f + index * .38f, index + 1, false));
                    list.Add(Q(4.05f,9,true)); list.Add(Q(4.62f,9,false));
                    return list.OrderBy(value=>value.Time).ThenBy(value=>value.Play?1:0).ToArray();
            }
        }

        public static W11W13CameraHint[] CameraHints(W11W13NextDefinition definition)
        {
            if (definition.Family != W11W13NextFamily.Ultimate) return new W11W13CameraHint[0];
            return new[]
            {
                H(Mathf.Min(.55f, definition.Duration * .12f), "zoom", .18f),
                H(definition.Duration * .46f, "shake", definition.Variant == W11W13NextVariant.MeteorShower ? .78f : .52f),
                H(definition.Duration * .78f, "slowmo", .28f)
            };
        }

        public static W11W13StageGate[] Gates(W11W13NextDefinition definition)
        {
            return definition.Variant == W11W13NextVariant.DemonGate
                ? new[] { G(1.15f,"gate_formed"), G(3.2f,"hand_release") }
                : new W11W13StageGate[0];
        }

        public static void ValidateRecipe(JObject recipe, W11W13NextDefinition definition)
        {
            if (recipe == null) throw new InvalidOperationException("Recipe JSON is empty for " + definition.Id);
            Require(recipe, "schema", RecipeSchema, definition.Id);
            Require(recipe, "id", definition.Id, definition.Id);
            Require(recipe, "sourceId", definition.SourceId, definition.Id);
            Require(recipe, "group", definition.Group, definition.Id);
            Require(recipe, "status", VisualStatus, definition.Id);
            if ((float?)recipe["duration"] == null || Math.Abs((float)recipe["duration"] - definition.Duration) > .001f) throw new InvalidOperationException(definition.Id + " duration is not frozen.");
            if ((int?)recipe.SelectToken("budget.renderers") != definition.RendererBudget || (int?)recipe.SelectToken("budget.particles") != definition.ParticleBudget) throw new InvalidOperationException(definition.Id + " budget is not frozen.");
            var carriers = ((JArray)recipe["requiredCarriers"]).Values<string>().ToArray();
            if (!definition.RequiredCarriers.SequenceEqual(carriers)) throw new InvalidOperationException(definition.Id + " required carrier contract differs from the source plan.");
            var dependencies = ((JArray)recipe["dependencies"]).Values<string>().ToArray();
            if (!(definition.Dependencies ?? new string[0]).SequenceEqual(dependencies)) throw new InvalidOperationException(definition.Id + " dependency list differs from the source plan.");
        }

        private static W11W13NextDefinition D(string id,string source,string group,string archetype,string lifecycle,W11W13NextFamily family,W11W13NextVariant variant,float duration,string primary,string secondary,string accent,int renderers,int particles,string[] carriers,string[] dependencies=null)
        {
            return new W11W13NextDefinition { Id=id,SourceId=source,Group=group,Archetype=archetype,Lifecycle=lifecycle,Family=family,Variant=variant,Duration=duration,Primary=ColorOf(primary),Secondary=ColorOf(secondary),Accent=ColorOf(accent),RendererBudget=renderers,ParticleBudget=particles,RequiredCarriers=carriers,Dependencies=dependencies??new string[0] };
        }
        private static W11W13NextDefinition U(string id,string source,W11W13NextVariant variant,float duration,string primary,string secondary,string accent,string[] carriers,string[] dependencies)
        { return D(id,source,"W13","composite","event_driven",W11W13NextFamily.Ultimate,variant,duration,primary,secondary,accent,14,200,carriers,dependencies); }
        private static string[] A(params string[] values){return values;}
        private static W11W13TimelineCue[] C(params W11W13TimelineCue[] values){return values;}
        private static W11W13TimelineCue Q(float time,int source,bool play,Vector3? position=null,Vector3? euler=null,float scale=1f,string eventId=null){return new W11W13TimelineCue{Time=time,SourceIndex=source,Play=play,LocalPosition=position??Vector3.zero,LocalEuler=euler??Vector3.zero,Scale=scale,EventId=eventId};}
        private static Vector3 P(float x,float y,float z){return new Vector3(x,y,z);}
        private static W11W13CameraHint H(float time,string type,float strength){return new W11W13CameraHint{Time=time,Type=type,Strength=strength};}
        private static W11W13StageGate G(float time,string id){return new W11W13StageGate{Time=time,EventId=id};}
        private static Color ColorOf(string value){Color result;return ColorUtility.TryParseHtmlString(value,out result)?result:Color.white;}
        private static void Require(JObject recipe,string key,string expected,string id){if(!string.Equals((string)recipe[key],expected,StringComparison.Ordinal))throw new InvalidOperationException(id+" "+key+" must equal "+expected+".");}
    }
}
