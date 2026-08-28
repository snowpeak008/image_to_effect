using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Style;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Editor.Elements
{
    public sealed class ElementContentEntry
    {
        public string Id,Family,Archetype,Dimension,Style,Primary,Secondary,Accent,ParametersJson,BehaviorJson;
        public float Duration;
    }

    /// <summary>Single source of truth for W3-W8 formal elemental content.</summary>
    public static class ElementFamilyCatalog
    {
        public static readonly ElementContentEntry[] All =
        {
            E("flame_slash_2d","fire","slash","2d","stylized","#FF6A00","#FFD84D","#FFF6E0",.45f,"{'sweep_angle':110,'arc_width':.72,'spark_count':8}",Instant()),
            E("fire_nova_burst_3d","fire","impact","3d","stylized","#FF6A00","#FFD84D","#FFF6E0",1.2f,"{'radius':4,'ring_speed':8,'tongue_count':12,'scorch_lifetime':1.2}","{'motion':{'type':'expand_ring','max_radius':4,'expand_speed':8,'edge_thickness':.3},'hit':{'type':'single'},'emission':{'type':'ring','count':12,'ring_radius':.4},'timing':{'type':'instant'}}"),
            E("flamethrower_beam_3d","fire","beam","3d","semireal","#FF6A00","#FFD84D","#FFF6E0",1.1f,"{'length':5,'cone_angle':24,'intensity':1.3,'fuel_color':'#FF6A00'}",Sustained()),
            E("burning_status_aura_2d","fire","aura","2d","cartoon","#FF6A00","#FFD84D","#FFF6E0",.8f,"{'flame_count':3,'tick_pulse':true}",Sustained()),
            E("ember_rain_area_3d","fire","area","3d","stylized","#FF6A00","#FFD84D","#FFF6E0",1.2f,"{'radius':5,'rain_density':48,'tick_interval':.5,'burn_patch_count':5}","{'hit':{'type':'single'},'emission':{'type':'single'},'timing':{'type':'tick_pulse','tick_interval':.5,'tick_visual_slot':'cap_hexflash_impact_2d'}}"),
            E("phoenix_dart_projectile_2d","fire","projectile","2d","cartoon","#FF6A00","#FFD84D","#FFF6E0",1.25f,"{'wing_span':1.4,'trail_length':1.1,'impact_feather_count':10}","{'motion':{'type':'wave','speed':4.5,'amplitude':.3,'frequency':2},'hit':{'type':'single'},'emission':{'type':'single'},'timing':{'type':'instant'}}"),
            E("chain_blast_impact_2d","fire","impact","2d","stylized","#FF6A00","#FFD84D","#FFF6E0",.72f,"{'blast_count':3,'interval':.12,'per_blast_scale':1,'spread_pattern':'triangle'}","{'hit':{'type':'single'},'emission':{'type':'single'},'timing':{'type':'chain_sequence','count':3,'interval':.12,'topology':'line','impact_slot':'cap_hexflash_impact_2d'}}"),
            E("fire_shield_3d","fire","shield","3d","stylized","#FF6A00","#FFD84D","#FFF6E0",1f,"{'shell_radius':1.1,'orbit_speed':3,'hit_burst_scale':1.2}",Instant()),

            E("ice_spike_spawn_3d","frost","spawn","3d","stylized","#7FD8FF","#FFFFFF","#2E6BD6",1.1f,"{'spike_count':5,'height':1.5,'pattern':'fan','exit_mode':'shatter'}",Instant()),
            E("blizzard_area_3d","frost","area","3d","semireal","#7FD8FF","#FFFFFF","#2E6BD6",1.2f,"{'radius':6,'wind_dir':'north_east','density':86,'fog_height':.8}",Sustained()),
            E("frost_breath_beam_2d","frost","beam","2d","cartoon","#7FD8FF","#FFFFFF","#2E6BD6",1f,"{'cone_angle':52,'length':4,'crystal_density':34}",Sustained()),
            E("ice_shard_projectile_2d","frost","projectile","2d","stylized","#7FD8FF","#FFFFFF","#2E6BD6",1.2f,"{'spin_speed':360,'trail_length':.8,'shard_variant':2}","{'motion':{'type':'linear','speed':5},'hit':{'type':'single'},'emission':{'type':'single'},'timing':{'type':'instant'}}"),
            E("freeze_status_2d","frost","aura","2d","stylized","#7FD8FF","#FFFFFF","#2E6BD6",1f,"{'shell_opacity':.62,'duration':2,'shatter_piece_count':8}",Sustained()),
            E("crystal_shield_3d","frost","shield","3d","stylized","#7FD8FF","#FFFFFF","#2E6BD6",1f,"{'petal_count':6,'orbit_radius':1.1,'hit_flash_color':'#FFFFFF'}",Instant()),
            E("flash_freeze_transform_3d","frost","transform","3d","stylized","#7FD8FF","#FFFFFF","#2E6BD6",.9f,"{'freeze_duration':.45,'rise_speed':3,'shatter_scale':1.2}",Instant()),

            E("thunder_strike_impact_3d","lightning","impact","3d","stylized","#8FE8FF","#FFFFFF","#3D6BFF",.6f,"{'strike_height':7,'fork_count':2,'ground_arc_count':5,'flash_times':2}",Instant()),
            E("ball_lightning_projectile_3d","lightning","projectile","3d","stylized","#8FE8FF","#FFFFFF","#3D6BFF",1.4f,"{'orb_radius':.55,'tendril_count':4,'drift_wobble':.45,'discharge_range':3}","{'motion':{'type':'wave','speed':2.8,'amplitude':.25,'frequency':1.7},'hit':{'type':'single'},'emission':{'type':'single'},'timing':{'type':'instant'}}"),
            E("static_field_area_2d","lightning","area","2d","stylized","#8FE8FF","#FFFFFF","#3D6BFF",.9f,"{'radius':3,'arc_frequency':.5,'tick_interval':.45,'net_opacity':.45}","{'hit':{'type':'single'},'emission':{'type':'single'},'timing':{'type':'tick_pulse','tick_interval':.45,'tick_visual_slot':'cap_hexflash_impact_2d'}}"),
            E("storm_charge_aura_3d","lightning","aura","3d","stylized","#8FE8FF","#FFFFFF","#3D6BFF",1f,"{'cloud_height':1.8,'arc_swap_interval':.3,'charge_level':2}",Sustained()),
            E("electro_slash_2d","lightning","slash","2d","neon","#B84DFF","#8FE8FF","#FFFFFF",.38f,"{'jag_amplitude':.35,'afterimage_count':2,'spark_count':8}",Instant()),
            E("emp_nova_impact_2d","lightning","impact","2d","holo","#8FE8FF","#FFFFFF","#3D6BFF",.65f,"{'ring_radius':3,'glitch_strength':.45,'ring_count':2}","{'motion':{'type':'expand_ring','max_radius':3,'expand_speed':8,'edge_thickness':.12},'hit':{'type':'single'},'emission':{'type':'ring','count':12,'ring_radius':.4},'timing':{'type':'instant'}}"),
            E("volt_shield_3d","lightning","shield","3d","stylized","#8FE8FF","#FFFFFF","#3D6BFF",1f,"{'net_density':4,'walk_arc_count':3,'counter_arc':true}",Instant()),

            E("water_jet_beam_3d","water","beam","3d","stylized","#4DA6FF","#EAF8FF","#FFFFFF",1f,"{'length':6,'pressure':6,'foam_amount':.5}",Sustained()),
            E("tidal_wave_area_3d","water","area","3d","stylized","#4DA6FF","#EAF8FF","#FFFFFF",1.1f,"{'wave_width':4,'travel_distance':6,'curl_amount':.65}",Instant()),
            E("bubble_shield_2d","water","shield","2d","cartoon","#4DA6FF","#EAF8FF","#FFFFFF",.8f,"{'bubble_radius':1.2,'wobble':.28,'pop_splash_scale':1.2}",Instant()),
            E("splash_impact_2d","water","impact","2d","cartoon","#4DA6FF","#EAF8FF","#FFFFFF",.55f,"{'crown_scale':1.2,'droplet_count':10,'ring_count':1}",Instant()),
            E("whirlpool_spawn_3d","water","spawn","3d","stylized","#4DA6FF","#EAF8FF","#FFFFFF",.9f,"{'vortex_radius':2,'spin_accel':8,'column_height':2.2}",Instant()),
            E("tornado_area_3d","wind","area","3d","stylized","#CFEFE0","#D8CBA8","#FFFFFF",1.1f,"{'height':3.5,'move_speed':2,'debris_type':'leaf'}",Sustained()),
            E("wind_blade_slash_2d","wind","slash","2d","inkwash","#CFEFE0","#D8CBA8","#5FBF5A",.42f,"{'blade_count':3,'arc_length':3.2,'leaf_count':3}",Instant()),
            E("gale_dash_trail_2d","wind","trail","2d","stylized","#CFEFE0","#D8CBA8","#FFFFFF",.6f,"{'dash_length':5,'afterimage_count':2,'line_density':14}","{'motion':{'type':'dash','distance':5,'duration':.35},'hit':{'type':'single'},'emission':{'type':'single'},'timing':{'type':'instant'}}"),

            E("earth_spike_spawn_3d","earth","spawn","3d","stylized","#A88860","#C9B48E","#FFF0D2",1.1f,"{'spike_count':6,'advance_speed':5,'line_length':4,'rock_tint':'#A88860'}",Instant()),
            E("boulder_projectile_3d","earth","projectile","3d","stylized","#A88860","#C9B48E","#FFF0D2",1.3f,"{'boulder_scale':1.2,'spin':240,'impact_debris_count':7,'dust_lifetime':1}","{'motion':{'type':'parabola','apex_height':2,'flight_time':1.2},'hit':{'type':'single'},'emission':{'type':'single'},'timing':{'type':'instant'}}"),
            E("quake_stomp_impact_3d","earth","impact","3d","stylized","#A88860","#C9B48E","#FF7A2E",.9f,"{'crack_count':5,'radius':4,'float_rock_count':5,'magma_glow':.25}",Instant()),
            E("thorn_snare_area_2d","nature","area","2d","stylized","#5FBF5A","#FFD1E8","#FFFFFF",1f,"{'radius':3,'thorn_density':16,'pulse_interval':.7,'wither_time':.8}",Sustained()),
            E("vine_whip_slash_2d","nature","slash","2d","cartoon","#5FBF5A","#FFD1E8","#FFFFFF",.5f,"{'whip_length':4,'wave_amp':.5,'leaf_count':6}",Instant()),
            E("healing_bloom_aura_2d","nature","aura","2d","cartoon","#5FBF5A","#FFD1E8","#FFFFFF",1f,"{'flower_count':5,'rise_speed':1.4}",Sustained()),
            E("spore_burst_impact_2d","toxic","impact","2d","stylized","#8CD62E","#4E7A1E","#D9FF84",1.2f,"{'cloud_radius':2.4,'linger_time':1.2,'spore_count':32}",Instant()),
            E("acid_lob_projectile_2d","toxic","projectile","2d","cartoon","#8CD62E","#4E7A1E","#D9FF84",1.25f,"{'blob_scale':1,'drip_rate':8,'pool_lifetime':1.5,'bubble_rate':5}","{'motion':{'type':'parabola','apex_height':1.8,'flight_time':1.1},'hit':{'type':'single'},'emission':{'type':'single'},'timing':{'type':'instant'}}"),

            E("divine_smite_impact_3d","holy","impact","3d","stylized","#FFE9A8","#FFFFFF","#FFF6D0",.8f,"{'pillar_height':7,'pillar_radius':.7,'feather_count':8,'afterglow':.5}",Instant()),
            E("holy_halo_aura_2d","holy","aura","2d","stylized","#FFE9A8","#FFFFFF","#FFF6D0",1f,"{'halo_tilt':24,'dust_density':22,'sparkle_rate':4}",Sustained()),
            E("resurrection_spawn_3d","holy","spawn","3d","stylized","#FFE9A8","#FFFFFF","#FFF6D0",1.1f,"{'gate_radius':2,'column_height':5,'feather_spiral_speed':3}",Instant()),
            E("shadow_claw_slash_2d","shadow","slash","2d","dark","#5A2E8C","#1A0F2E","#C24DFF",.48f,"{'claw_count':3,'tear_jaggedness':.6,'mist_amount':.55}",Instant()),
            E("void_orb_projectile_3d","shadow","projectile","3d","dark","#1A0F2E","#5A2E8C","#C24DFF",1.25f,"{'orb_radius':.7,'suction_particle_rate':30,'implode_scale':1.25}","{'motion':{'type':'linear','speed':3.2},'hit':{'type':'single'},'emission':{'type':'single'},'timing':{'type':'instant'}}"),
            E("shadow_grasp_area_2d","shadow","area","2d","dark","#5A2E8C","#1A0F2E","#C24DFF",1f,"{'pool_radius':3,'hand_count':3,'tick_interval':.65,'hand_height':1.5}","{'hit':{'type':'single'},'emission':{'type':'single'},'timing':{'type':'tick_pulse','tick_interval':.65,'tick_visual_slot':'cap_hexflash_impact_2d'}}"),
            E("curse_mark_status_2d","shadow","aura","2d","dark","#5A2E8C","#1A0F2E","#C24DFF",1f,"{'mark_glyph':2,'pulse_rate':1.5,'smoke_amount':.4}",Sustained()),
            E("arcane_missile_projectile_2d","arcane","projectile","2d","stylized","#4D7CFF","#9AD1FF","#FFFFFF",1.25f,"{'missile_count':3,'stagger_interval':.1,'wobble_amp':.22}","{'motion':{'type':'homing','turn_rate':180,'max_speed':5,'lose_target_mode':'straight'},'hit':{'type':'single'},'emission':{'type':'burst_stagger','count':3,'stagger':.1},'timing':{'type':'instant'}}"),
            E("arcane_rune_spawn_2d","arcane","spawn","2d","stylized","#4D7CFF","#9AD1FF","#FFFFFF",.9f,"{'ring_radius':2,'glyph_count':10,'spin_speed':3,'activate_order':'forward'}",Instant())
        };

        public static IEnumerable<ElementContentEntry> Family(string family){return All.Where(v=>v.Family==family);}
        private static ElementContentEntry E(string id,string family,string archetype,string dimension,string style,string primary,string secondary,string accent,float duration,string parameters,string behavior){return new ElementContentEntry{Id=id,Family=family,Archetype=archetype,Dimension=dimension,Style=style,Primary=primary,Secondary=secondary,Accent=accent,Duration=duration,ParametersJson=parameters,BehaviorJson=behavior};}
        private static string Instant(){return "{'hit':{'type':'single'},'emission':{'type':'single'},'timing':{'type':'instant'}}";}
        private static string Sustained(){return "{'hit':{'type':'single'},'emission':{'type':'single'},'timing':{'type':'sustained'}}";}
    }

    public static class ElementFamilyAuthoring
    {
        public const string RecipeRoot="Assets/VFX/Recipes/Elements";
        public const string PatchRoot="Assets/VFX/Recipes/Patches";
        public const string PreviewRoot="Assets/VFX/Preview";
        private static readonly Dictionary<string,string> PreviewNames=new Dictionary<string,string>(StringComparer.Ordinal){{"fire","VFXPREVIEW_FireFamily.unity"},{"frost","VFXPREVIEW_FrostFamily.unity"},{"lightning","VFXPREVIEW_LightningFamily.unity"},{"water_wind","VFXPREVIEW_WaterWindFamily.unity"},{"earth_nature_toxic","VFXPREVIEW_EarthNatureFamily.unity"},{"magic","VFXPREVIEW_MagicFamily.unity"}};

        [MenuItem("Tools/VFX Composer/Elements/Build W3-W8 All Families")]
        public static void BuildAll(){BuildEntries();BuildPreview("fire",new[]{"fire"});BuildPreview("frost",new[]{"frost"});BuildPreview("lightning",new[]{"lightning"});BuildPreview("water_wind",new[]{"water","wind"});BuildPreview("earth_nature_toxic",new[]{"earth","nature","toxic"});BuildPreview("magic",new[]{"holy","shadow","arcane"});AssetDatabase.SaveAssets();AssetDatabase.Refresh();}
        public static void BuildEntries(){WriteAllRecipes();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);foreach(var entry in ElementFamilyCatalog.All)Build(entry.Id);BuildVariants();AssetDatabase.SaveAssets();AssetDatabase.Refresh();}

        public static void WriteAllRecipes()
        {
            EnsureFolder(RecipeRoot);EnsureFolder(PatchRoot);
            foreach(var entry in ElementFamilyCatalog.All){var folder=RecipeRoot+"/"+FamilyFolder(entry.Family);EnsureFolder(folder);WriteAssetText(folder+"/"+entry.Id+".default.json",Recipe(entry,entry.Id,entry.Style));WritePatch(entry);}
            WriteVariant("flame_slash_2d","neon");WriteVariant("fire_nova_burst_3d","semireal");WriteVariant("ice_spike_spawn_3d","dark");WriteVariant("ice_shard_projectile_2d","inkwash");
        }

        public static string PreviewPath(string group){return PreviewRoot+"/"+PreviewNames[group];}

        private static JObject Recipe(ElementContentEntry entry,string id,string style)
        {
            return new JObject{{"recipeVersion",1},{"revision",1},{"id",id},{"name",Title(id)},{"dimension",entry.Dimension},{"archetype",entry.Archetype},{"style",new JObject{{"token",style},{"palette",new JObject{{"primary",entry.Primary},{"secondary",entry.Secondary},{"accent",entry.Accent}}},{"outline",style=="cartoon"?.28:.12},{"shading_steps",style=="cartoon"?3:4},{"noise_scale",style=="semireal"?2.2:1.1},{"glow_strength",style=="dark"?.8:1.25}}},{"behavior",ParseObject(entry.BehaviorJson)},{"content",new JObject{{"family",entry.Family},{"parameters",ParseObject(entry.ParametersJson)}}},{"targetProfile","mobile_medium"},{"randomSeed",Seed(id)},{"stages",Stages(entry)},{"metadata",new JObject{{"createdBy","w3-w8-element-family"},{"templateCatalogVersion","1.0.0"}}}};
        }

        private static JArray Stages(ElementContentEntry entry)
        {
            var d=entry.Dimension;var launch="PFT_"+(d=="2d"?"2D":"3D")+"_LaunchFlash";var core="PFT_"+(d=="2d"?"2D":"3D")+"_FireCore";var trail="PFT_"+(d=="2d"?"2D":"3D")+"_FireTrail";var particles="PFT_"+(d=="2d"?"2D":"3D")+"_Embers";var burst="PFT_"+(d=="2d"?"2D":"3D")+"_FireImpact";var shock="PFT_"+(d=="2d"?"2D":"3D")+"_Shockwave";
            if(entry.Archetype=="projectile")return new JArray{Stage("launch","on_launch",.08,new JObject{{"id","launchFlash"},{"kind","impact_flash"},{"templateId",launch},{"parameters",new JObject{{"lifetime",.1},{"size",.7}}},{"enabled",true}}),Stage("travel","after_previous",Math.Max(.2,entry.Duration-.35),new JObject{{"id","core"},{"kind","energy_body"},{"templateId",core},{"parameters",new JObject{{"scale",1.0}}},{"enabled",true}},new JObject{{"id","trail"},{"kind","motion_trail"},{"templateId",trail},{"parameters",new JObject{{"time",.3},{"width",.28}}},{"attachTo","core"},{"enabled",true}},new JObject{{"id","secondary"},{"kind","secondary_particles"},{"templateId",particles},{"parameters",new JObject{{"rate",10},{"lifetime",.5}}},{"attachTo","core"},{"enabled",true}}),Stage("impact","on_hit",.27,new JObject{{"id","impactFlash"},{"kind","impact_flash"},{"templateId",launch},{"parameters",new JObject{{"lifetime",.1},{"size",.8}}},{"enabled",true}},new JObject{{"id","impactBurst"},{"kind","impact_burst"},{"templateId",burst},{"parameters",new JObject{{"count",16},{"speed",3.0}}},{"enabled",true}},new JObject{{"id","shockwave"},{"kind","shockwave"},{"templateId",shock},{"parameters",new JObject{{"lifetime",.24},{"endSize",2.2}}},{"enabled",true}})};
            var trigger=entry.Archetype=="impact"?"on_hit":"manual";return new JArray{Stage("main",trigger,entry.Duration,new JObject{{"id","body"},{"kind","energy_body"},{"templateId",core},{"parameters",new JObject{{"scale",1.0}}},{"enabled",true}},new JObject{{"id","flow"},{"kind","motion_trail"},{"templateId",trail},{"parameters",new JObject{{"time",.28},{"width",.25}}},{"attachTo","body"},{"enabled",true}},new JObject{{"id","secondary"},{"kind","secondary_particles"},{"templateId",particles},{"parameters",new JObject{{"rate",12},{"lifetime",.55}}},{"attachTo","body"},{"enabled",true}},new JObject{{"id","accent"},{"kind","shockwave"},{"templateId",shock},{"parameters",new JObject{{"lifetime",.3},{"endSize",2.4}}},{"enabled",true}})};
        }

        private static JObject Stage(string id,string trigger,double duration,params JObject[] modules){return new JObject{{"id",id},{"trigger",trigger},{"duration",duration},{"enabled",true},{"modules",new JArray(modules)}};}
        private static void Build(string id){var entry=ElementFamilyCatalog.All.First(v=>v.Id==id);var path=RecipeRoot+"/"+FamilyFolder(entry.Family)+"/"+id+".default.json";var result=StyledContentCompiler.BuildAsset(path);if(!result.Succeeded)throw new InvalidOperationException(id+": "+string.Join(" | ",result.Report.Entries.Select(v=>v.Code+" "+v.Path+" "+v.Message)));}
        private static void BuildVariants(){foreach(var pair in new[]{new[]{"flame_slash_2d","neon"},new[]{"fire_nova_burst_3d","semireal"},new[]{"ice_spike_spawn_3d","dark"},new[]{"ice_shard_projectile_2d","inkwash"}}){var entry=ElementFamilyCatalog.All.First(v=>v.Id==pair[0]);var path=RecipeRoot+"/"+FamilyFolder(entry.Family)+"/"+entry.Id+"."+pair[1]+".json";var result=StyledContentCompiler.BuildAsset(path);if(!result.Succeeded)throw new InvalidOperationException(path+": "+string.Join(" | ",result.Report.Entries.Select(v=>v.Code+" "+v.Path+" "+v.Message)));}}
        private static void WriteVariant(string baseId,string style){var entry=ElementFamilyCatalog.All.First(v=>v.Id==baseId);WriteAssetText(RecipeRoot+"/"+FamilyFolder(entry.Family)+"/"+baseId+"."+style+".json",Recipe(entry,baseId+"_"+style,style));}
        private static void WritePatch(ElementContentEntry entry){ContentDefinition definition;ContentParameterRegistry.TryGet(entry.Id,out definition);var values=ParseObject(entry.ParametersJson);var first=definition.Parameters.Values.OrderBy(v=>v.Name,StringComparer.Ordinal).First();var current=values[first.Name];var value=Alternative(first,current);var patch=new JArray{new JObject{{"op","set_content_param"},{"path","/content/parameters/"+first.Name},{"value",value}}};WriteAssetText(PatchRoot+"/"+entry.Id+".semantic.patch.json",patch);}
        private static JToken Alternative(ContentParameterDefinition d,JToken current){if(d.Kind==ContentParameterKind.Boolean)return new JValue(!(bool)current);if(d.Kind==ContentParameterKind.Enum){var index=Array.IndexOf(d.Values,(string)current);return new JValue(d.Values[(index+1)%d.Values.Length]);}if(d.Kind==ContentParameterKind.Integer){var value=(int)Math.Round(((int)current+d.Max)*.5);if(value==(int)current)value=(int)d.Min;return new JValue(value);}if(d.Kind==ContentParameterKind.Number){var value=((double)current+d.Max)*.5;if(Math.Abs(value-(double)current)<.00001)value=d.Min;return new JValue(value);}return new JValue((string)current+"_variant");}

        private static void BuildPreview(string group,string[] families)
        {
            EnsureFolder(PreviewRoot);var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);scene.name=Path.GetFileNameWithoutExtension(PreviewNames[group]);var cameraGo=new GameObject("ElementFamilyReviewCamera");var camera=cameraGo.AddComponent<Camera>();camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.018f,.022f,.03f,1);camera.orthographic=true;camera.orthographicSize=4.7f;camera.transform.position=new Vector3(0,0,-12);camera.cullingMask=~0;camera.allowHDR=false;camera.allowMSAA=false;
            var controllers=new List<StyledVfxController>();var entries=ElementFamilyCatalog.All.Where(v=>families.Contains(v.Family)).ToArray();for(var i=0;i<entries.Length;i++){var entry=entries[i];var row=i/3;var col=i%3;var cell=new GameObject("Cell_"+(i+1).ToString("00",CultureInfo.InvariantCulture)+"_"+entry.Id);cell.transform.position=new Vector3((col-1)*3.25f,2.7f-row*2.7f,0);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/"+entry.Id+"/VFX_"+entry.Id+".prefab");var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);instance.name=entry.Id;instance.transform.SetParent(cell.transform,false);instance.transform.localScale=Vector3.one*.78f;if(entry.Dimension=="3d")instance.transform.localRotation=Quaternion.Euler(24,-18,0);controllers.Add(instance.GetComponent<StyledVfxController>());var label=new GameObject("Label");label.transform.SetParent(cell.transform,false);label.transform.localPosition=new Vector3(0,-1.05f,0);var text=label.AddComponent<TextMesh>();text.text=(i+1).ToString(CultureInfo.InvariantCulture)+" "+entry.Id;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontSize=44;text.characterSize=.035f;text.color=new Color(.68f,.75f,.84f,1);}
            var driverGo=new GameObject("ElementFamilyPreviewDriver");var driver=driverGo.AddComponent<ElementFamilyPreviewDriver>();var serialized=new SerializedObject(driver);var property=serialized.FindProperty("entries");property.arraySize=controllers.Count;for(var i=0;i<controllers.Count;i++)property.GetArrayElementAtIndex(i).objectReferenceValue=controllers[i];serialized.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.SaveScene(scene,PreviewPath(group));
        }

        private static string FamilyFolder(string family){if(family=="water"||family=="wind")return"WaterWind";if(family=="earth"||family=="nature"||family=="toxic")return"EarthNatureToxic";if(family=="holy"||family=="shadow"||family=="arcane")return"Magic";return char.ToUpperInvariant(family[0])+family.Substring(1);}
        private static string Title(string id){return string.Join(" ",id.Split('_').Select(v=>v.Length==0?v:char.ToUpperInvariant(v[0])+v.Substring(1)));}
        private static uint Seed(string id){unchecked{uint hash=2166136261;foreach(var c in id){hash^=c;hash*=16777619;}return hash;}}
        private static JObject ParseObject(string json){return JObject.Parse(json.Replace(":.",":0."));}
        private static void WriteAssetText(string path,JToken token){WriteAssetText(path,token.ToString(Formatting.Indented));}
        private static void WriteAssetText(string path,string text){var absolute=Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,path.Replace('/',Path.DirectorySeparatorChar)));Directory.CreateDirectory(Path.GetDirectoryName(absolute));File.WriteAllText(absolute,text.Replace("\r\n","\n"));}
        private static void EnsureFolder(string path){VfxStyleSharedLibrary.EnsureFolder(path);}
    }
}
