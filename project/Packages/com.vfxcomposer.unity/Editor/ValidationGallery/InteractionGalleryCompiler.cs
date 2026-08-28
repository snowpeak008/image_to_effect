using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Editor.ValidationGallery
{
    public static class InteractionGalleryCompiler
    {
        public const string CompilerVersion="interaction-gallery-3";

        internal sealed class Definition
        {
            public string Id,Archetype,RecipePath;public InteractionGalleryProfile Profile;
        }
        internal sealed class Recipe
        {
            public int RecipeVersion,Revision;public string Id,Archetype,Dimension,Lifecycle;public float Duration;public Color Primary,Secondary;public bool Sustained{get{return Lifecycle=="sustained"||Lifecycle=="event_driven";}}
        }

        internal static readonly Definition[] Definitions=
        {
            D("focus_charge_3d","aura","Aura",InteractionGalleryProfile.Charge),D("channel_tether_3d","beam","Beam",InteractionGalleryProfile.Channel),D("warning_telegraph_3d","area","Area",InteractionGalleryProfile.Telegraph),D("chain_arc_3d","beam","Beam",InteractionGalleryProfile.Chain),D("seeker_orb_3d","projectile","Projectile",InteractionGalleryProfile.Homing),D("weapon_enchant_3d","aura","Aura",InteractionGalleryProfile.WeaponEnchant),D("phase_dash_3d","trail","Trail",InteractionGalleryProfile.Dash),D("dissolve_transform_3d","transform","Transform",InteractionGalleryProfile.DissolveTransform),D("ultimate_sequence_3d","composite","Composite",InteractionGalleryProfile.MultiStage)
        };

        [MenuItem("Tools/VFX Composer/Interaction Gallery/Build Nine Runtime Entries")]
        public static void BuildAllMenu(){BuildAll();Debug.Log("Interaction Gallery: built nine Runtime Entries.");}
        public static void BuildAll(){CoverageGalleryBCompiler.EnsureShared();foreach(var definition in Definitions)BuildOne(definition);AssetDatabase.SaveAssets();AssetDatabase.Refresh();}

        private static Definition D(string id,string archetype,string folder,InteractionGalleryProfile profile){return new Definition{Id=id,Archetype=archetype,Profile=profile,RecipePath="Assets/VFX/Recipes/"+folder+"/"+id+".default.json"};}

        private static void BuildOne(Definition definition)
        {
            var json=File.ReadAllText(Absolute(definition.RecipePath));var recipe=Parse(json,definition);var recipeHash=RecipeCanonicalizer.ComputeSha256(json);var buildHash=Hash(recipeHash+"|"+CompilerVersion+"|"+AssetDatabase.GetAssetDependencyHash(CoverageGalleryBCompiler.SharedRoot)+"|"+Application.unityVersion);var folder="Assets/VFX/Generated/"+definition.Id;ValidationGalleryCompiler.EnsureFolder(folder);var prefabPath=folder+"/VFX_"+definition.Id+".prefab";var root=new GameObject("VFX_"+definition.Id);
            try
            {
                var renderers=new List<Renderer>();var animated=new List<Transform>();var lines=new List<LineRenderer>();var modes=new List<float>();var intensities=new List<float>();TrailRenderer trail=null;BuildProfile(definition.Profile,root,renderers,animated,lines,ref trail,modes,intensities);var controller=root.AddComponent<InteractionGalleryVfxController>();var so=new SerializedObject(controller);so.FindProperty("profile").enumValueIndex=(int)definition.Profile;SetObjects(so.FindProperty("renderers"),renderers.Cast<UnityEngine.Object>().ToArray());SetObjects(so.FindProperty("animatedTransforms"),animated.Cast<UnityEngine.Object>().ToArray());SetObjects(so.FindProperty("lines"),lines.Cast<UnityEngine.Object>().ToArray());so.FindProperty("trail").objectReferenceValue=trail;SetFloats(so.FindProperty("shapeModes"),modes.ToArray());SetFloats(so.FindProperty("intensities"),intensities.ToArray());so.FindProperty("primaryColor").colorValue=recipe.Primary;so.FindProperty("secondaryColor").colorValue=recipe.Secondary;so.FindProperty("sustained").boolValue=recipe.Sustained;so.FindProperty("duration").floatValue=recipe.Duration;so.ApplyModifiedPropertiesWithoutUndo();if(PrefabUtility.SaveAsPrefabAsset(root,prefabPath)==null)throw new InvalidOperationException("Could not save "+prefabPath);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            AssetDatabase.SaveAssets();var audit=VfxProductionRules.EnforceAndWriteManifest(recipe.Id,recipe.Archetype,recipe.RecipeVersion,recipe.Revision,recipeHash,buildHash,CompilerVersion,prefabPath,folder,recipe.Duration);if(audit.Report.HasErrors)throw new InvalidOperationException(string.Join(" | ",audit.Report.Entries.Select(e=>e.Code+" "+e.Path+" "+e.Message)));
        }

        private static void BuildProfile(InteractionGalleryProfile profile,GameObject root,List<Renderer> renderers,List<Transform> animated,List<LineRenderer> lines,ref TrailRenderer trail,List<float> modes,List<float> intensities)
        {
            var alpha=AssetDatabase.LoadAssetAtPath<Material>(CoverageGalleryBCompiler.AlphaMaterialPath);var add=AssetDatabase.LoadAssetAtPath<Material>(CoverageGalleryBCompiler.AdditiveMaterialPath);var quad=AssetDatabase.LoadAssetAtPath<Mesh>(CoverageGalleryBCompiler.QuadPath);var ring=AssetDatabase.LoadAssetAtPath<Mesh>(CoverageGalleryBCompiler.RingPath);var burst=AssetDatabase.LoadAssetAtPath<Mesh>(CoverageGalleryBCompiler.BurstPath);var sphere=Builtin(PrimitiveType.Sphere);var cube=Builtin(PrimitiveType.Cube);
            if(profile==InteractionGalleryProfile.Charge)
            {
                AddMesh(root,"ChargeCore",sphere,add,Vector3.zero,Vector3.one*.26f,Quaternion.identity,0,1.08f,renderers,modes,intensities,null);AddMesh(root,"ConvergenceRing",ring,add,Vector3.zero,Vector3.one*.8f,Quaternion.Euler(64,0,0),5,.52f,renderers,modes,intensities,null);for(var i=0;i<3;i++)AddMesh(root,"ChargeNode_"+i,sphere,add,Vector3.zero,Vector3.one*.12f,Quaternion.identity,0,.86f,renderers,modes,intensities,animated);
            }
            else if(profile==InteractionGalleryProfile.Channel)
            {
                for(var i=0;i<3;i++)AddLine(root,"ChannelLayer_"+i,add,i==0?.18f:(i==1?.075f:.035f),i==0?.38f:(i==1?1.1f:.78f),renderers,modes,intensities,lines);AddMesh(root,"CasterCap",sphere,add,new Vector3(-1.02f,0,0),Vector3.one*.13f,Quaternion.identity,0,.86f,renderers,modes,intensities,animated);AddMesh(root,"TargetCap",sphere,add,new Vector3(1.02f,0,0),Vector3.one*.16f,Quaternion.identity,0,1.05f,renderers,modes,intensities,animated);
            }
            else if(profile==InteractionGalleryProfile.Telegraph)
            {
                AddMesh(root,"TelegraphBoundary",ring,add,Vector3.zero,Vector3.one*.92f,Quaternion.Euler(62,0,0),5,.65f,renderers,modes,intensities,null);AddMesh(root,"CountdownFill",quad,alpha,new Vector3(0,-.03f,.03f),new Vector3(.82f,.52f,1),Quaternion.Euler(62,0,0),10,.5f,renderers,modes,intensities,animated);AddMesh(root,"CountdownTicks",ring,add,Vector3.zero,Vector3.one*.7f,Quaternion.Euler(62,0,0),1,.64f,renderers,modes,intensities,animated);AddMesh(root,"DetonationBurst",burst,add,new Vector3(0,.06f,-.04f),Vector3.one*.92f,Quaternion.Euler(22,0,0),8,1.08f,renderers,modes,intensities,animated);
            }
            else if(profile==InteractionGalleryProfile.Chain)
            {
                for(var i=0;i<3;i++)AddLine(root,"ChainSegment_"+i,add,i==0?.1f:.065f,.82f+i*.12f,renderers,modes,intensities,lines);var points=new[]{new Vector3(-1.02f,-.2f,0),new Vector3(-.35f,.38f,.06f),new Vector3(.28f,-.12f,-.04f),new Vector3(1.02f,.28f,0)};for(var i=0;i<points.Length;i++)AddMesh(root,"ChainNode_"+i,sphere,add,points[i],Vector3.one*(i==0||i==3?.13f:.09f),Quaternion.identity,0,.9f,renderers,modes,intensities,animated);
            }
            else if(profile==InteractionGalleryProfile.Homing)
            {
                var head=AddMesh(root,"HomingHead",sphere,add,new Vector3(-1.05f,-.34f,0),Vector3.one*.22f,Quaternion.identity,0,1.18f,renderers,modes,intensities,animated);trail=AddTrail(head,add,.38f,renderers,modes,intensities);trail.time=1.35f;AddMesh(root,"HomingTarget",ring,add,new Vector3(.95f,.08f,0),Vector3.one*.38f,Quaternion.identity,5,.52f,renderers,modes,intensities,null);AddMesh(root,"HomingTargetCore",sphere,alpha,new Vector3(.95f,.08f,.04f),Vector3.one*.1f,Quaternion.identity,0,.52f,renderers,modes,intensities,null);
            }
            else if(profile==InteractionGalleryProfile.WeaponEnchant)
            {
                var rig=new GameObject("WeaponRig");rig.transform.SetParent(root.transform,false);animated.Add(rig.transform);AddMesh(rig,"WeaponBlade",quad,alpha,new Vector3(0,.18f,0),new Vector3(.16f,.86f,1),Quaternion.Euler(0,0,-10),3,.62f,renderers,modes,intensities,null);AddMesh(rig,"EnchantEdge",quad,add,new Vector3(.06f,.2f,-.02f),new Vector3(.075f,.82f,1),Quaternion.Euler(0,0,-10),9,.92f,renderers,modes,intensities,null);AddMesh(rig,"EnchantFlame",burst,add,new Vector3(0,.2f,.04f),new Vector3(.36f,.8f,1),Quaternion.Euler(0,0,-10),8,.56f,renderers,modes,intensities,null);var tip=new GameObject("WeaponTip");tip.transform.SetParent(rig.transform,false);tip.transform.localPosition=new Vector3(-.15f,1.02f,0);trail=AddTrail(tip,add,.2f,renderers,modes,intensities);
            }
            else if(profile==InteractionGalleryProfile.Dash)
            {
                AddMesh(root,"StartGhost",sphere,alpha,new Vector3(-1.05f,-.2f,0),new Vector3(.32f,.72f,.26f),Quaternion.identity,9,.62f,renderers,modes,intensities,null);var head=AddMesh(root,"DashHead",sphere,add,new Vector3(-1.05f,-.2f,0),Vector3.one*.17f,Quaternion.identity,0,1.18f,renderers,modes,intensities,animated);trail=AddTrail(head,add,.42f,renderers,modes,intensities);trail.time=1.35f;AddLine(root,"DashStreak",add,.12f,.72f,renderers,modes,intensities,lines);AddMesh(root,"EndGhost",sphere,alpha,new Vector3(1.05f,.25f,0),new Vector3(.34f,.76f,.28f),Quaternion.identity,11,.78f,renderers,modes,intensities,null);
            }
            else if(profile==InteractionGalleryProfile.DissolveTransform)
            {
                AddMesh(root,"SourceForm",sphere,alpha,new Vector3(-.5f,0,0),new Vector3(.5f,.68f,.4f),Quaternion.identity,0,.72f,renderers,modes,intensities,null);AddMesh(root,"DissolveFragments",burst,add,Vector3.zero,Vector3.one*.9f,Quaternion.identity,8,.92f,renderers,modes,intensities,animated);AddMesh(root,"TargetForm",cube,alpha,new Vector3(.5f,0,0),Vector3.one*.58f,Quaternion.Euler(18,28,8),11,.8f,renderers,modes,intensities,animated);
            }
            else
            {
                AddMesh(root,"ChargeStage",sphere,add,new Vector3(-.78f,0,0),Vector3.one*.34f,Quaternion.identity,0,1.12f,renderers,modes,intensities,null);var projectile=AddMesh(root,"ProjectileStage",sphere,add,new Vector3(-.78f,0,0),Vector3.one*.21f,Quaternion.identity,0,1.2f,renderers,modes,intensities,animated);trail=AddTrail(projectile,add,.32f,renderers,modes,intensities);trail.time=1.1f;AddMesh(root,"ImpactStage",burst,add,new Vector3(.78f,.12f,0),Vector3.one*.96f,Quaternion.identity,8,1.16f,renderers,modes,intensities,null);AddMesh(root,"ResidueStage",ring,alpha,new Vector3(.78f,-.05f,.08f),Vector3.one*.78f,Quaternion.Euler(62,0,0),10,.64f,renderers,modes,intensities,null);
            }
        }

        private static GameObject AddMesh(GameObject parent,string name,Mesh mesh,Material material,Vector3 position,Vector3 scale,Quaternion rotation,float mode,float intensity,List<Renderer> renderers,List<float> modes,List<float> intensities,List<Transform> animated)
        {
            var go=new GameObject(name);go.transform.SetParent(parent.transform,false);go.transform.localPosition=position;go.transform.localScale=scale;go.transform.localRotation=rotation;go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.enabled=false;renderers.Add(renderer);modes.Add(mode);intensities.Add(intensity);if(animated!=null)animated.Add(go.transform);return go;
        }
        private static void AddLine(GameObject parent,string name,Material material,float width,float intensity,List<Renderer> renderers,List<float> modes,List<float> intensities,List<LineRenderer> lines)
        {
            var go=new GameObject(name);go.transform.SetParent(parent.transform,false);var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.alignment=LineAlignment.View;line.widthMultiplier=width;line.numCapVertices=5;line.sharedMaterial=material;line.enabled=false;renderers.Add(line);modes.Add(3);intensities.Add(intensity);lines.Add(line);
        }
        private static TrailRenderer AddTrail(GameObject parent,Material material,float width,List<Renderer> renderers,List<float> modes,List<float> intensities)
        {
            var trail=parent.AddComponent<TrailRenderer>();trail.time=.9f;trail.minVertexDistance=.025f;trail.widthMultiplier=width;trail.widthCurve=new AnimationCurve(new Keyframe(0,.1f),new Keyframe(.22f,1),new Keyframe(1,0));trail.colorGradient=Gradient();trail.sharedMaterial=material;trail.alignment=LineAlignment.View;trail.emitting=false;trail.enabled=false;renderers.Add(trail);modes.Add(3);intensities.Add(.86f);return trail;
        }
        private static Gradient Gradient(){var value=new Gradient();value.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});return value;}
        private static Mesh Builtin(PrimitiveType type){var temp=GameObject.CreatePrimitive(type);try{return temp.GetComponent<MeshFilter>().sharedMesh;}finally{UnityEngine.Object.DestroyImmediate(temp);}}

        internal static Recipe Parse(string json,Definition expected)
        {
            var root=JObject.Parse(json);var allowed=new[]{"recipeVersion","revision","id","archetype","dimension","lifecycle","duration","primaryColor","secondaryColor"};foreach(var property in root.Properties())if(!allowed.Contains(property.Name,StringComparer.Ordinal))throw new InvalidOperationException("Unknown field /"+property.Name);var recipe=new Recipe{RecipeVersion=Int(root,"recipeVersion"),Revision=Int(root,"revision"),Id=Text(root,"id"),Archetype=Text(root,"archetype"),Dimension=Text(root,"dimension"),Lifecycle=Text(root,"lifecycle"),Duration=(float)Number(root,"duration")};if(recipe.RecipeVersion!=1||recipe.Revision<1||recipe.Id!=expected.Id||recipe.Archetype!=expected.Archetype||recipe.Dimension!="3d")throw new InvalidOperationException("Recipe identity/version mismatch: "+expected.Id);if(recipe.Lifecycle!="sustained"&&recipe.Lifecycle!="event_driven"&&recipe.Lifecycle!="one_shot")throw new InvalidOperationException("Unsupported lifecycle: "+recipe.Lifecycle);if(!ColorUtility.TryParseHtmlString(Text(root,"primaryColor"),out recipe.Primary)||!ColorUtility.TryParseHtmlString(Text(root,"secondaryColor"),out recipe.Secondary))throw new InvalidOperationException("Invalid color.");return recipe;
        }
        private static string Text(JObject root,string name){var value=root[name];if(value==null||value.Type!=JTokenType.String||string.IsNullOrWhiteSpace((string)value))throw new InvalidOperationException("Missing string /"+name);return(string)value;}
        private static int Int(JObject root,string name){var value=root[name];if(value==null||value.Type!=JTokenType.Integer)throw new InvalidOperationException("Missing integer /"+name);return(int)value;}
        private static double Number(JObject root,string name){var value=root[name];if(value==null||(value.Type!=JTokenType.Integer&&value.Type!=JTokenType.Float))throw new InvalidOperationException("Missing number /"+name);return(double)value;}
        private static string Hash(string text){using(var sha=SHA256.Create())return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(value=>value.ToString("x2",CultureInfo.InvariantCulture)));}
        private static void SetObjects(SerializedProperty property,UnityEngine.Object[] values){property.arraySize=values.Length;for(var i=0;i<values.Length;i++)property.GetArrayElementAtIndex(i).objectReferenceValue=values[i];}
        private static void SetFloats(SerializedProperty property,float[] values){property.arraySize=values.Length;for(var i=0;i<values.Length;i++)property.GetArrayElementAtIndex(i).floatValue=values[i];}
        private static string Absolute(string path){return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,path.Replace('/',Path.DirectorySeparatorChar)));}
    }
}
