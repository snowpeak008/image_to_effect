using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VFXComposer.Editor.Style;
using VFXComposer.Editor.ValidationGallery;

namespace VFXComposer.Editor.Archetypes
{
    public static class W15NewArchetypeAuthoring
    {
        public const string RecipeRoot="Assets/VFX/Recipes/NewArchetypes";
        public const string PreviewScenePath="Assets/VFX/Preview/VFXPREVIEW_NewArchetypes.unity";
        public static readonly Definition[] Definitions=
        {
            D("scorch_decal_3d","Scorch Decal","decal","3d","dark",C("#8A240D","#FF6A17","#FFD38A"),.9,P("size",1.15,"lifetime",3.2,"stack_limit",4)),
            D("frost_decal_3d","Frost Decal","decal","3d","neon",C("#2694C7","#80ECFF","#E9FFFF"),1.1,P("size",1.1,"lifetime",3.6,"stack_limit",4)),
            D("katana_trail_weapon_3d","Katana Weapon Trail","weapon_trail","3d","semireal",C("#571319","#E14727","#FFF1C7"),2.4,P("speed_threshold",.35,"history_points",12,"fade_time",.15)),
            D("energy_whip_trail_2d","Energy Whip Trail","weapon_trail","2d","neon",C("#7C1CC7","#EB55FF","#FFF2FF"),2.4,P("speed_threshold",.25,"history_points",16,"fade_time",.18)),
            D("crate_break_destruction_3d","Crate Break","destruction","3d","cartoon",C("#6E351C","#C77A35","#FFE0A0"),1.25,P("piece_count",10,"explode_force",2.6,"debris_lifetime",1.25)),
            D("crystal_shatter_destruction_3d","Crystal Shatter","destruction","3d","holo",C("#196B94","#52D9FF","#E8FFFF"),1.7,P("piece_count",12,"explode_force",2.1,"debris_lifetime",1.7)),
            D("death_dissolve_lifecycle_3d","Death Dissolve","lifecycle","3d","dark",C("#253128","#72965C","#FF8D3A"),1.4,P("duration",1.4,"direction","up","edge_color","#FF8D3A")),
            D("hero_entrance_lifecycle_3d","Hero Entrance","lifecycle","3d","semireal",C("#B27516","#FFD659","#FFFFFF"),1.25,P("duration",1.25,"direction","down","edge_color","#FFF0A0")),
            D("twin_portal_3d","Twin Portal","portal","3d","holo",C("#3A1A91","#A46BFF","#89FFFF"),2.8,P("pair_id","twin_portal_default","portal_radius",1.0,"swirl_speed",2.8)),
            D("loot_beam_pickup_3d","Loot Beam Pickup","loot","3d","cartoon",C("#7B4E0C","#FFD24C","#FFFFFF"),2.8,P("rarity",3,"pickup_speed",4.8,"beam_height",2.4))
        };

        public static IEnumerable<string> RecipePaths{get{return Definitions.Select(value=>RecipeRoot+"/"+value.Id+".default.json");}}

        [MenuItem("Tools/VFX Composer/Archetypes/Build W15 New Archetypes and Preview")]
        public static void BuildAllMenu(){BuildAll();Debug.Log("W15 new Archetypes are current. User visual sign-off remains deferred until final acceptance.");}

        public static void BuildAll()
        {
            EnsureRecipes();
            foreach(var definition in Definitions){var path=RecipeRoot+"/"+definition.Id+".default.json";var result=StyledContentCompiler.BuildAsset(path);if(!result.Succeeded)throw new InvalidOperationException(path+": "+string.Join(" | ",result.Report.Entries.Select(value=>value.Code+" "+value.Path+" "+value.Message).ToArray()));}
            BuildPreview();AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }

        public static void EnsureRecipes()
        {
            EnsureFolder(RecipeRoot);EnsureFolder("Assets/VFX/Recipes/Patches");
            foreach(var definition in Definitions){WriteIfChanged(RecipeRoot+"/"+definition.Id+".default.json",Recipe(definition).ToString(Formatting.Indented)+"\n");WriteIfChanged("Assets/VFX/Recipes/Patches/"+definition.Id+".semantic.patch.json",Patch(definition).ToString(Formatting.Indented)+"\n");}
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static JArray Patch(Definition d)
        {
            string key;JToken value;
            switch(d.Archetype){case "decal":key="stack_limit";value=3;break;case "weapon_trail":key="fade_time";value=.12;break;case "destruction":key="explode_force";value=3.2;break;case "lifecycle":key="duration";value=1.1;break;case "portal":key="swirl_speed";value=3.3;break;default:key="rarity";value=5;break;}
            return new JArray(new JObject{{"op","set_archetype_param"},{"path","/archetypeParameters/"+key},{"value",value}});
        }

        private static JObject Recipe(Definition d)
        {
            return new JObject
            {
                ["recipeVersion"]=1,["revision"]=1,["id"]=d.Id,["name"]=d.Name,["dimension"]=d.Dimension,["archetype"]=d.Archetype,
                ["style"]=new JObject{{"token",d.Style},{"palette",d.Palette},{"glow_strength",d.Style=="dark"?.75:1.2}},
                ["archetypeParameters"]=d.Parameters,["targetProfile"]="mobile_medium",["randomSeed"]=(uint)(2166136261u^(uint)StableHash(d.Id)),
                ["stages"]=new JArray(new JObject{{"id","active"},{"trigger","manual"},{"duration",d.Duration},{"enabled",true},{"modules",Modules(d)}}),
                ["metadata"]=new JObject{{"createdBy","w15-authoring"},{"templateCatalogVersion","formal-1"}}
            };
        }

        private static JArray Modules(Definition d)
        {
            var prefix=d.Dimension=="2d"?"PFT_2D_":"PFT_3D_";var result=new JArray();
            result.Add(new JObject{{"id","core"},{"kind","energy_body"},{"templateId",prefix+"FireCore"},{"parameters",new JObject{{"scale",1.0}}},{"enabled",true}});
            if(d.Archetype=="weapon_trail"||d.Archetype=="portal")result.Add(new JObject{{"id","flow"},{"kind","motion_trail"},{"templateId",prefix+"FireTrail"},{"parameters",new JObject{{"time",.22},{"width",.3}}},{"attachTo","core"},{"enabled",true}});
            else if(d.Archetype=="destruction")result.Add(new JObject{{"id","debris"},{"kind","impact_burst"},{"templateId",prefix+"FireImpact"},{"parameters",new JObject{{"count",12},{"speed",3.0}}},{"attachTo","core"},{"enabled",true}});
            else result.Add(new JObject{{"id","secondary"},{"kind","secondary_particles"},{"templateId",prefix+"Embers"},{"parameters",new JObject{{"rate",8.0},{"lifetime",.45}}},{"attachTo","core"},{"enabled",true}});
            return result;
        }

        private static void BuildPreview()
        {
            EnsureFolder("Assets/VFX/Preview");var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var entries=new List<StyledVfxController>();
            for(var index=0;index<Definitions.Length;index++)
            {
                var d=Definitions[index];var row=index/3;var column=index%3;var holder=new GameObject("Cell_"+(index+1).ToString("00")+"_"+d.Id);holder.transform.position=new Vector3((column-1)*3.1f,3.25f-row*2.15f,0);var prefabPath="Assets/VFX/Generated/"+d.Id+"/VFX_"+d.Id+".prefab";var source=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);if(source==null)throw new InvalidOperationException("Missing W15 Prefab: "+prefabPath);
                var copies=d.Archetype=="loot"?5:d.Archetype=="portal"?2:1;
                for(var copy=0;copy<copies;copy++)
                {
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(source,holder.transform);instance.name="Runtime_"+(copy+1).ToString("00");instance.transform.localPosition=copies==1?Vector3.zero:new Vector3((copy-(copies-1)*.5f)*(d.Archetype=="loot"?.34f:.72f),0,0);instance.transform.localScale=Vector3.one*(d.Archetype=="loot"?.33f:d.Archetype=="portal"?.58f:.68f);if(d.Dimension=="3d"&&d.Archetype!="decal")instance.transform.localRotation=Quaternion.Euler(12,20,0);var entry=instance.GetComponent<StyledVfxController>();if(entry==null)throw new InvalidOperationException(prefabPath+" has no StyledVfxController");if(d.Archetype=="loot")entry.SetRarity(copy+1);if(d.Archetype=="portal")entry.ConfigurePortal("twin_portal_default",copy==0?PortalEndpointRole.Entry:PortalEndpointRole.Exit);entries.Add(entry);
                }
                AddLabel(holder.transform,(index+1)+" "+d.Archetype.ToUpperInvariant());
            }
            var cameraObject=new GameObject("W15NewArchetypesCamera");cameraObject.tag="MainCamera";var camera=cameraObject.AddComponent<Camera>();camera.transform.position=new Vector3(0,.15f,-14);camera.transform.rotation=Quaternion.identity;camera.fieldOfView=39;camera.nearClipPlane=.05f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.018f,.022f,.034f);camera.allowHDR=false;camera.allowMSAA=false;
            var driverObject=new GameObject("W15NewArchetypesPreviewDriver");var driver=driverObject.AddComponent<NewArchetypePreviewDriver>();var serialized=new SerializedObject(driver);var property=serialized.FindProperty("entries");property.arraySize=entries.Count;for(var index=0;index<entries.Count;index++)property.GetArrayElementAtIndex(index).objectReferenceValue=entries[index];serialized.FindProperty("cycleDuration").floatValue=4f;serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene,PreviewScenePath);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }

        private static void AddLabel(Transform parent,string text){var go=new GameObject("Label");go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(0,-.75f,0);var label=go.AddComponent<TextMesh>();label.text=text;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.fontSize=30;label.characterSize=.04f;label.color=new Color(.66f,.72f,.82f);}
        private static void WriteIfChanged(string path,string content){var absolute=Absolute(path);Directory.CreateDirectory(Path.GetDirectoryName(absolute));if(File.Exists(absolute)&&string.Equals(File.ReadAllText(absolute),content,StringComparison.Ordinal))return;File.WriteAllText(absolute,content,new UTF8Encoding(false));}
        private static string Absolute(string assetPath){return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,assetPath.Replace('/',Path.DirectorySeparatorChar)));}
        private static void EnsureFolder(string path){var parts=path.Split('/');var current=parts[0];for(var i=1;i<parts.Length;i++){var next=current+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);current=next;}}
        private static int StableHash(string value){unchecked{var hash=17;foreach(var c in value)hash=hash*31+c;return hash;}}
        private static JObject C(string primary,string secondary,string accent){return new JObject{{"primary",primary},{"secondary",secondary},{"accent",accent}};}
        private static JObject P(params object[] values){var result=new JObject();for(var i=0;i<values.Length;i+=2)result[(string)values[i]]=JToken.FromObject(values[i+1]);return result;}
        private static Definition D(string id,string name,string archetype,string dimension,string style,JObject palette,double duration,JObject parameters){return new Definition(id,name,archetype,dimension,style,palette,duration,parameters);}

        public sealed class Definition
        {
            public readonly string Id,Name,Archetype,Dimension,Style;public readonly JObject Palette,Parameters;public readonly double Duration;
            public Definition(string id,string name,string archetype,string dimension,string style,JObject palette,double duration,JObject parameters){Id=id;Name=name;Archetype=archetype;Dimension=dimension;Style=style;Palette=palette;Duration=duration;Parameters=parameters;}
        }
    }
}
