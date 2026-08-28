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
using UnityEngine.Rendering;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Editor.ValidationGallery
{
    public static class ValidationGalleryCompiler
    {
        public const string CompilerVersion = "gallery2d-8";
        public const string ShaderPath = "Assets/VFX/Shared/Shaders/ValidationGallery2DUnlit.shader";
        public const string ShaderName = "Universal Render Pipeline/VFXComposer Validation Gallery 2D Unlit";
        public const string SharedRoot = "Assets/VFX/Shared/ValidationGallery";
        public const string QuadPath = SharedRoot + "/Meshes/M_ValidationGallery_Quad.asset";
        public const string AlphaMaterialPath = SharedRoot + "/Materials/MAT_ValidationGallery_Alpha.mat";
        public const string AdditiveMaterialPath = SharedRoot + "/Materials/MAT_ValidationGallery_Additive.mat";
        public const string MaskAtlasPath = SharedRoot + "/Textures/T_ValidationGallery_MaskAtlas_128.png";
        public const string SharedParticlePrefabPath = SharedRoot + "/Prefabs/PF_ValidationGallery_SecondaryParticles.prefab";

        internal sealed class Definition
        {
            public string Id, Archetype, RecipePath;
            public ValidationArchetypeProfile Profile;
            public float[] Modes, Intensities;
            public Vector3[] Scales;
            public bool[] Additive;
        }

        internal sealed class Recipe
        {
            public int RecipeVersion, Revision;
            public string Id, Archetype, Dimension, Lifecycle;
            public float Duration;
            public Color Primary, Secondary;
            public bool Sustained { get { return Lifecycle == "sustained" || Lifecycle == "event_driven"; } }
        }

        internal static readonly Definition[] Definitions =
        {
            D("guardian_aura_2d", "aura", ValidationArchetypeProfile.Aura, new[]{0f,1f,2f,3f,4f}, new[]{.48f,.72f,1.02f,.72f,.92f}, new[]{S(1.08f,1.08f),S(1f,1f),S(.8f,.8f),S(.95f,.95f),S(1.15f,1.15f)}, new[]{false,true,true,true,true}),
            D("arc_lightning_beam_2d", "beam", ValidationArchetypeProfile.Beam, new[]{5f,6f,7f,8f,9f}, new[]{.3f,1.12f,.72f,.9f,.7f}, new[]{S(1.52f,.64f),S(1.52f,.64f),S(1.52f,.64f),S(1.52f,.64f),S(1.52f,.64f)}, new[]{true,true,true,true,true}),
            D("comet_motion_trail_2d", "trail", ValidationArchetypeProfile.Trail, new[]{10f,11f,12f,13f,14f}, new[]{.58f,1.05f,1.28f,.7f,.86f}, new[]{S(1.48f,.7f),S(1.48f,.7f),S(1.48f,.7f),S(1.48f,.7f),S(1.48f,.7f)}, new[]{true,true,true,true,true}),
            D("hex_guard_shield_2d", "shield", ValidationArchetypeProfile.Shield, new[]{15f,16f,17f,18f,19f}, new[]{.62f,.94f,.7f,.78f,1.02f}, new[]{S(.98f,.98f),S(.98f,.98f),S(.92f,.92f),S(.98f,.98f),S(1.08f,1.08f)}, new[]{false,true,true,true,true}),
            D("summoning_portal_2d", "spawn", ValidationArchetypeProfile.Spawn, new[]{20f,21f,22f,23f,24f}, new[]{.5f,.82f,1.02f,.98f,.92f}, new[]{S(1.02f,1.02f),S(.96f,.96f),S(.76f,.76f),S(.9f,.9f),S(1.02f,1.02f)}, new[]{false,true,true,true,true})
        };

        [MenuItem("Tools/VFX Composer/Validation Gallery/Build Five New Runtime Prefabs")]
        public static void BuildAllMenu() { BuildAll(); Debug.Log("Built Aura, Beam, Trail, Shield and Spawn Runtime Prefabs."); }

        public static void BuildAll()
        {
            EnsureShared();
            foreach (var definition in Definitions) BuildOne(definition);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }

        private static Definition D(string id, string archetype, ValidationArchetypeProfile profile, float[] modes, float[] intensities, Vector3[] scales, bool[] additive)
        {
            return new Definition { Id=id, Archetype=archetype, Profile=profile, RecipePath="Assets/VFX/Recipes/"+Upper(archetype)+"/"+id+".default.json", Modes=modes, Intensities=intensities, Scales=scales, Additive=additive };
        }
        private static Vector3 S(float x,float y) { return new Vector3(x,y,1f); }
        private static string Upper(string value) { return char.ToUpperInvariant(value[0])+value.Substring(1); }

        private static void EnsureShared()
        {
            EnsureFolder(SharedRoot+"/Meshes"); EnsureFolder(SharedRoot+"/Materials"); EnsureFolder(SharedRoot+"/Textures"); EnsureFolder(SharedRoot+"/Prefabs"); EnsureMaskAtlas();
            var shader=Shader.Find(ShaderName); if(shader==null) throw new InvalidOperationException("Missing gallery shader at "+ShaderPath);
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(QuadPath);
            if(mesh==null){ mesh=new Mesh{name="M_ValidationGallery_Quad"}; AssetDatabase.CreateAsset(mesh,QuadPath); }
            mesh.Clear(); mesh.vertices=new[]{new Vector3(-1,-1,0),new Vector3(-1,1,0),new Vector3(1,1,0),new Vector3(1,-1,0)}; mesh.uv=new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)}; mesh.triangles=new[]{0,1,2,0,2,3}; mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
            var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(MaskAtlasPath); if(atlas==null)throw new InvalidOperationException("Mask atlas import failed: "+MaskAtlasPath);
            CreateMaterial(AlphaMaterialPath,shader,atlas,BlendMode.OneMinusSrcAlpha,3000);
            CreateMaterial(AdditiveMaterialPath,shader,atlas,BlendMode.One,3010);
            CreateSharedParticles(mesh,AssetDatabase.LoadAssetAtPath<Material>(AdditiveMaterialPath));
        }

        private static void CreateSharedParticles(Mesh quad,Material material)
        {
            var go=new GameObject("PF_ValidationGallery_SecondaryParticles");
            try
            {
                var particle=go.AddComponent<ParticleSystem>();var main=particle.main;main.playOnAwake=false;main.loop=true;main.duration=2.5f;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.maxParticles=24;main.startLifetime=new ParticleSystem.MinMaxCurve(.35f,.9f);main.startSize=new ParticleSystem.MinMaxCurve(.07f,.145f);main.startSpeed=new ParticleSystem.MinMaxCurve(.08f,.26f);main.startColor=Color.white;
                var emission=particle.emission;emission.rateOverTime=16f;var shape=particle.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Circle;shape.radius=.7f;shape.radiusThickness=.12f;shape.randomDirectionAmount=.22f;
                var velocity=particle.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;velocity.x=new ParticleSystem.MinMaxCurve(-.08f,.08f);velocity.y=new ParticleSystem.MinMaxCurve(-.08f,.08f);velocity.z=new ParticleSystem.MinMaxCurve(0f,0f);
                var color=particle.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.72f,.62f),new GradientAlphaKey(0,1)});color.color=gradient;
                var size=particle.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1f,new AnimationCurve(new Keyframe(0,0),new Keyframe(.14f,1),new Keyframe(1,0)));
                var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=quad;renderer.sharedMaterial=material;renderer.sortingOrder=32;renderer.enabled=false;particle.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                if(PrefabUtility.SaveAsPrefabAsset(go,SharedParticlePrefabPath)==null)throw new InvalidOperationException("Could not save shared particle prefab.");
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }

        private static void CreateMaterial(string path,Shader shader,Texture2D atlas,BlendMode destination,int queue)
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(shader){name=Path.GetFileNameWithoutExtension(path)};AssetDatabase.CreateAsset(material,path);} else material.shader=shader;
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend",(float)destination); material.SetTexture("_MaskAtlas",atlas); material.renderQueue=queue; material.SetOverrideTag("RenderType","Transparent"); EditorUtility.SetDirty(material);
        }

        private static void EnsureMaskAtlas()
        {
            const int size=128,tile=64;var absolute=Absolute(MaskAtlasPath);
            if(!File.Exists(absolute))
            {
                var texture=new Texture2D(size,size,TextureFormat.RGBA32,false,true){name="T_ValidationGallery_MaskAtlas_128"};var pixels=new Color32[size*size];
                for(var y=0;y<size;y++)for(var x=0;x<size;x++)
                {
                    var tileX=x/tile;var tileY=y/tile;var u=((x%tile)+.5f)/tile;var v=((y%tile)+.5f)/tile;float alpha;
                    if(tileX==0&&tileY==0)alpha=StrokeMask(u,v);
                    else if(tileX==1&&tileY==0)alpha=SmokeMask(u,v);
                    else if(tileX==0)alpha=ShardMask(u,v);
                    else alpha=SparkMask(u,v);
                    var value=(byte)Mathf.RoundToInt(Mathf.Clamp01(alpha)*255f);pixels[y*size+x]=new Color32(255,255,255,value);
                }
                texture.SetPixels32(pixels);texture.Apply(false,false);var bytes=texture.EncodeToPNG();UnityEngine.Object.DestroyImmediate(texture);File.WriteAllBytes(absolute,bytes);
            }
            AssetDatabase.ImportAsset(MaskAtlasPath,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(MaskAtlasPath);importer.textureType=TextureImporterType.Default;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;importer.sRGBTexture=false;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=size;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
        }

        private static float StrokeMask(float u,float v)
        {
            var center=.5f+.09f*Mathf.Sin(u*7.1f)+.035f*Mathf.Sin(u*17.3f);var width=.025f+.19f*Mathf.SmoothStep(0,1,u);var fade=Mathf.SmoothStep(0,.12f,u)*(1-Mathf.SmoothStep(.9f,1,u));var noise=.5f+.32f*Mathf.PerlinNoise(u*8.2f,v*6.7f)+.18f*Mathf.PerlinNoise(u*21.3f,v*17.1f);var primary=Mathf.Clamp01(1-Mathf.Abs(v-center)/width)*fade*noise;var filament=Mathf.Clamp01(1-Mathf.Abs(v-(center+.13f*Mathf.Sin(u*9.4f)))/(width*.22f))*fade*.7f;return Mathf.Max(primary,filament);
        }
        private static float SmokeMask(float u,float v)
        {
            var dx=u-.5f;var dy=v-.5f;var radial=Mathf.Clamp01(1-Mathf.Sqrt(dx*dx+dy*dy)*1.75f);var noise=.45f*Mathf.PerlinNoise(u*4.1f,v*4.1f)+.35f*Mathf.PerlinNoise(u*9.7f+3.2f,v*8.3f)+.2f*Mathf.PerlinNoise(u*19.1f,v*17.7f);return Mathf.SmoothStep(.08f,.85f,radial*noise*1.7f);
        }
        private static float ShardMask(float u,float v)
        {
            var result=0f;var centers=new[]{new Vector4(.24f,.58f,.11f,.3f),new Vector4(.48f,.42f,.09f,.22f),new Vector4(.68f,.65f,.08f,.2f),new Vector4(.79f,.31f,.055f,.13f),new Vector4(.36f,.2f,.045f,.1f)};foreach(var item in centers){var diamond=1-(Mathf.Abs(u-item.x)/item.z+Mathf.Abs(v-item.y)/item.w);result=Mathf.Max(result,Mathf.SmoothStep(0,.22f,diamond));}return result;
        }
        private static float SparkMask(float u,float v)
        {
            var result=0f;var centers=new[]{new Vector3(.2f,.68f,.07f),new Vector3(.42f,.34f,.05f),new Vector3(.62f,.72f,.06f),new Vector3(.8f,.46f,.045f),new Vector3(.7f,.2f,.035f)};foreach(var item in centers){var diamond=1-(Mathf.Abs(u-item.x)+Mathf.Abs(v-item.y))/item.z;result=Mathf.Max(result,Mathf.SmoothStep(0,.3f,diamond));}return result;
        }

        private static void BuildOne(Definition definition)
        {
            var json=File.ReadAllText(Absolute(definition.RecipePath)); var recipe=Parse(json,definition);
            var recipeHash=RecipeCanonicalizer.ComputeSha256(json); var buildHash=Hash(recipeHash+"|"+CompilerVersion+"|"+AssetDatabase.GetAssetDependencyHash(ShaderPath)+"|"+AssetDatabase.GetAssetDependencyHash(MaskAtlasPath)+"|"+AssetDatabase.GetAssetDependencyHash(SharedParticlePrefabPath)+"|"+Application.unityVersion);
            var folder="Assets/VFX/Generated/"+definition.Id; EnsureFolder(folder); var prefabPath=folder+"/VFX_"+definition.Id+".prefab";
            var root=new GameObject("VFX_"+definition.Id);
            try
            {
                var rendererList=new List<Renderer>(); var quad=AssetDatabase.LoadAssetAtPath<Mesh>(QuadPath); var alpha=AssetDatabase.LoadAssetAtPath<Material>(AlphaMaterialPath); var add=AssetDatabase.LoadAssetAtPath<Material>(AdditiveMaterialPath);
                for(var i=0;i<5;i++)
                {
                    var layer=new GameObject("Layer_"+(i+1).ToString("00",CultureInfo.InvariantCulture)); layer.transform.SetParent(root.transform,false); layer.transform.localScale=definition.Scales[i];
                    layer.AddComponent<MeshFilter>().sharedMesh=quad; var renderer=layer.AddComponent<MeshRenderer>(); renderer.sharedMaterial=definition.Additive[i]?add:alpha; renderer.sortingOrder=10+i*2; renderer.enabled=false; rendererList.Add(renderer);
                }
                var sharedParticle=AssetDatabase.LoadAssetAtPath<GameObject>(SharedParticlePrefabPath);if(sharedParticle==null)throw new InvalidOperationException("Missing shared particle prefab.");var particleObject=(GameObject)PrefabUtility.InstantiatePrefab(sharedParticle,root.transform);particleObject.transform.localPosition=Vector3.zero;particleObject.transform.localRotation=Quaternion.identity;particleObject.transform.localScale=ParticleScale(definition.Profile);var particle=particleObject.GetComponent<ParticleSystem>();rendererList.Add(particle.GetComponent<ParticleSystemRenderer>());
                var controller=root.AddComponent<ValidationArchetypeVfxController>(); var so=new SerializedObject(controller);
                so.FindProperty("profile").enumValueIndex=(int)definition.Profile; SetObjects(so.FindProperty("renderers"),rendererList.ToArray()); SetObjects(so.FindProperty("particles"),new UnityEngine.Object[]{particle}); SetFloats(so.FindProperty("shapeModes"),definition.Modes.Concat(new[]{25f}).ToArray()); SetFloats(so.FindProperty("intensities"),definition.Intensities.Concat(new[]{.92f}).ToArray());
                so.FindProperty("primaryColor").colorValue=recipe.Primary; so.FindProperty("secondaryColor").colorValue=recipe.Secondary; so.FindProperty("sustained").boolValue=recipe.Sustained; so.FindProperty("duration").floatValue=recipe.Duration; so.ApplyModifiedPropertiesWithoutUndo();
                if(PrefabUtility.SaveAsPrefabAsset(root,prefabPath)==null) throw new InvalidOperationException("Could not save "+prefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            var audit=VfxProductionRules.EnforceAndWriteManifest(recipe.Id,recipe.Archetype,recipe.RecipeVersion,recipe.Revision,recipeHash,buildHash,CompilerVersion,prefabPath,folder,recipe.Duration);
            if(audit.Report.HasErrors) throw new InvalidOperationException(string.Join(" | ",audit.Report.Entries.Select(e=>e.Code+" "+e.Path+" "+e.Message)));
        }

        private static Vector3 ParticleScale(ValidationArchetypeProfile profile){if(profile==ValidationArchetypeProfile.Beam)return new Vector3(1.35f,.34f,1);if(profile==ValidationArchetypeProfile.Trail)return new Vector3(1.18f,.42f,1);return Vector3.one;}

        internal static Recipe Parse(string json,Definition expected)
        {
            var root=JObject.Parse(json); var allowed=new[]{"recipeVersion","revision","id","archetype","dimension","lifecycle","duration","primaryColor","secondaryColor"};
            foreach(var property in root.Properties()) if(!allowed.Contains(property.Name,StringComparer.Ordinal)) throw new InvalidOperationException("Unknown field /"+property.Name);
            var recipe=new Recipe{RecipeVersion=RequiredInt(root,"recipeVersion"),Revision=RequiredInt(root,"revision"),Id=RequiredString(root,"id"),Archetype=RequiredString(root,"archetype"),Dimension=RequiredString(root,"dimension"),Lifecycle=RequiredString(root,"lifecycle"),Duration=(float)RequiredNumber(root,"duration")};
            if(recipe.RecipeVersion!=1||recipe.Revision<1||recipe.Id!=expected.Id||recipe.Archetype!=expected.Archetype||recipe.Dimension!="2d") throw new InvalidOperationException("Recipe identity/version does not match its frozen gallery definition: "+expected.Id);
            if(recipe.Lifecycle!="sustained"&&recipe.Lifecycle!="event_driven"&&recipe.Lifecycle!="one_shot") throw new InvalidOperationException("Unsupported lifecycle: "+recipe.Lifecycle);
            if(recipe.Duration<.4f||recipe.Duration>8f) throw new InvalidOperationException("Duration is outside 0.4..8 seconds.");
            if(!ColorUtility.TryParseHtmlString(RequiredString(root,"primaryColor"),out recipe.Primary)||!ColorUtility.TryParseHtmlString(RequiredString(root,"secondaryColor"),out recipe.Secondary)) throw new InvalidOperationException("Colors must be #RRGGBB or #RRGGBBAA.");
            return recipe;
        }

        private static string RequiredString(JObject root,string name){var token=root[name];if(token==null||token.Type!=JTokenType.String||string.IsNullOrWhiteSpace((string)token))throw new InvalidOperationException("Missing string /"+name);return(string)token;}
        private static int RequiredInt(JObject root,string name){var token=root[name];if(token==null||token.Type!=JTokenType.Integer)throw new InvalidOperationException("Missing integer /"+name);return(int)token;}
        private static double RequiredNumber(JObject root,string name){var token=root[name];if(token==null||(token.Type!=JTokenType.Integer&&token.Type!=JTokenType.Float))throw new InvalidOperationException("Missing number /"+name);return(double)token;}
        private static string Hash(string text){using(var sha=SHA256.Create())return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(v=>v.ToString("x2",CultureInfo.InvariantCulture)));}
        private static void SetObjects(SerializedProperty property,UnityEngine.Object[] values){property.arraySize=values.Length;for(var i=0;i<values.Length;i++)property.GetArrayElementAtIndex(i).objectReferenceValue=values[i];}
        private static void SetFloats(SerializedProperty property,float[] values){property.arraySize=values.Length;for(var i=0;i<values.Length;i++)property.GetArrayElementAtIndex(i).floatValue=values[i];}
        internal static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;var parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
        private static string Absolute(string path){return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,path.Replace('/',Path.DirectorySeparatorChar)));}
    }
}
