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
using UnityEngine.UI;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Editor.ValidationGallery
{
    public static class CoverageGalleryBCompiler
    {
        public const string CompilerVersion = "coverage-b-4";
        public const string SharedRoot = "Assets/VFX/Shared/CoverageGalleryB";
        public const string ShaderPath = "Assets/VFX/Shared/Shaders/CoverageGalleryBUnlit.shader";
        public const string ShaderName = "Universal Render Pipeline/VFXComposer Coverage Gallery B Unlit";
        public const string QuadPath = SharedRoot + "/Meshes/M_CoverageB_Quad.asset";
        public const string RingPath = SharedRoot + "/Meshes/M_CoverageB_Ring.asset";
        public const string BurstPath = SharedRoot + "/Meshes/M_CoverageB_Burst.asset";
        public const string CloudPath = SharedRoot + "/Meshes/M_CoverageB_Cloud.asset";
        public const string AlphaMaterialPath = SharedRoot + "/Materials/MAT_CoverageB_Alpha.mat";
        public const string AdditiveMaterialPath = SharedRoot + "/Materials/MAT_CoverageB_Additive.mat";
        public const string ParticlePrefabPath = SharedRoot + "/Prefabs/PF_CoverageB_Particles.prefab";

        internal sealed class Definition
        {
            public string Id, Archetype, Dimension, RecipePath;
            public CoverageGalleryProfile Profile;
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
            D("meteor_impact_3d","impact","3d",CoverageGalleryProfile.Impact3D),
            D("astral_aura_3d","aura","3d",CoverageGalleryProfile.Aura3D),
            D("toxic_field_3d","area","3d",CoverageGalleryProfile.Area3D),
            D("plasma_link_3d","beam","3d",CoverageGalleryProfile.Beam3D),
            D("spectral_trail_3d","trail","3d",CoverageGalleryProfile.Trail3D),
            D("prismatic_shield_3d","shield","3d",CoverageGalleryProfile.Shield3D),
            D("rift_spawn_3d","spawn","3d",CoverageGalleryProfile.Spawn3D),
            D("snow_weather_volume","environment","3d",CoverageGalleryProfile.Environment),
            D("damage_warning_ui","screen_ui","screen",CoverageGalleryProfile.ScreenUi)
        };

        [MenuItem("Tools/VFX Composer/Coverage Gallery B/Build Nine Runtime Entries")]
        public static void BuildAllMenu() { BuildAll(); Debug.Log("Coverage Gallery B: built nine Runtime Entries."); }

        public static void BuildAll()
        {
            EnsureShared(); foreach (var definition in Definitions) BuildOne(definition); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }

        private static Definition D(string id,string archetype,string dimension,CoverageGalleryProfile profile)
        {
            var folder = archetype == "screen_ui" ? "Screen" : char.ToUpperInvariant(archetype[0]) + archetype.Substring(1);
            return new Definition { Id=id, Archetype=archetype, Dimension=dimension, Profile=profile, RecipePath="Assets/VFX/Recipes/"+folder+"/"+id+".default.json" };
        }

        internal static void EnsureShared()
        {
            ValidationGalleryCompiler.EnsureFolder(SharedRoot+"/Meshes"); ValidationGalleryCompiler.EnsureFolder(SharedRoot+"/Materials"); ValidationGalleryCompiler.EnsureFolder(SharedRoot+"/Prefabs");
            var shader=Shader.Find(ShaderName); if(shader==null)throw new InvalidOperationException("Missing shader: "+ShaderPath);
            CreateMesh(QuadPath,CreateQuad); CreateMesh(RingPath,CreateRing); CreateMesh(BurstPath,CreateBurst); CreateMesh(CloudPath,CreateCloud);
            CreateMaterial(AlphaMaterialPath,shader,BlendMode.OneMinusSrcAlpha,3020); CreateMaterial(AdditiveMaterialPath,shader,BlendMode.One,3030);
            foreach(var stale in new[]{SharedRoot+"/Prefabs/PF_CoverageB_Burst.prefab",SharedRoot+"/Prefabs/PF_CoverageB_Motes.prefab",SharedRoot+"/Prefabs/PF_CoverageB_Weather.prefab"})if(AssetDatabase.LoadMainAssetAtPath(stale)!=null)AssetDatabase.DeleteAsset(stale);
            CreateParticles(ParticlePrefabPath);
        }

        private static void CreateMesh(string path,Func<Mesh> factory)
        {
            var current=AssetDatabase.LoadAssetAtPath<Mesh>(path); var built=factory();
            if(current==null){built.name=Path.GetFileNameWithoutExtension(path);AssetDatabase.CreateAsset(built,path);return;}
            current.Clear();current.vertices=built.vertices;current.uv=built.uv;current.normals=built.normals;current.triangles=built.triangles;current.bounds=built.bounds;EditorUtility.SetDirty(current);UnityEngine.Object.DestroyImmediate(built);
        }

        private static Mesh CreateQuad()
        {
            var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-1,-1,0),new Vector3(-1,1,0),new Vector3(1,1,0),new Vector3(1,-1,0)};mesh.uv=new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        private static Mesh CreateRing()
        {
            const int segments=64;var vertices=new Vector3[(segments+1)*2];var uv=new Vector2[vertices.Length];var triangles=new int[segments*6];
            for(var i=0;i<=segments;i++){var t=i/(float)segments;var a=t*Mathf.PI*2;var d=new Vector3(Mathf.Cos(a),Mathf.Sin(a),.05f*Mathf.Sin(a*3));vertices[i*2]=d*.68f;vertices[i*2+1]=d;uv[i*2]=new Vector2(t,0);uv[i*2+1]=new Vector2(t,1);if(i<segments){var q=i*2;var k=i*6;triangles[k]=q;triangles[k+1]=q+3;triangles[k+2]=q+1;triangles[k+3]=q;triangles[k+4]=q+2;triangles[k+5]=q+3;}}
            var mesh=new Mesh{vertices=vertices,uv=uv,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        private static Mesh CreateBurst()
        {
            const int rays=12;var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(var i=0;i<rays;i++){var a=i*Mathf.PI*2/rays;var tangent=new Vector3(-Mathf.Sin(a),Mathf.Cos(a),0)*.07f;var inner=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.18f;var outer=new Vector3(Mathf.Cos(a),Mathf.Sin(a),(i%2==0?.12f:-.08f))*(i%3==0?1f:.72f);var baseIndex=vertices.Count;vertices.Add(inner-tangent);vertices.Add(inner+tangent);vertices.Add(outer);uv.Add(new Vector2(0,.5f));uv.Add(new Vector2(0,1));uv.Add(new Vector2(1,.5f));triangles.Add(baseIndex);triangles.Add(baseIndex+1);triangles.Add(baseIndex+2);}
            var mesh=new Mesh{vertices=vertices.ToArray(),uv=uv.ToArray(),triangles=triangles.ToArray()};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        private static Mesh CreateCloud()
        {
            const int segments=48;var vertices=new Vector3[segments+1];var uv=new Vector2[segments+1];var triangles=new int[segments*3];vertices[0]=Vector3.zero;uv[0]=new Vector2(.5f,.5f);
            for(var i=0;i<segments;i++){var a=i*Mathf.PI*2/segments;var radius=1f+.10f*Mathf.Sin(a*5+.4f)+.06f*Mathf.Sin(a*9-1.1f);vertices[i+1]=new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0);uv[i+1]=new Vector2(.5f+Mathf.Cos(a)*.5f,.5f+Mathf.Sin(a)*.5f);var k=i*3;triangles[k]=0;triangles[k+1]=i+1;triangles[k+2]=(i+1)%segments+1;}
            var mesh=new Mesh{vertices=vertices,uv=uv,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        private static void CreateMaterial(string path,Shader shader,BlendMode destination,int queue)
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null){material=new Material(shader){name=Path.GetFileNameWithoutExtension(path)};AssetDatabase.CreateAsset(material,path);}else material.shader=shader;
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)destination);material.renderQueue=queue;material.SetOverrideTag("RenderType","Transparent");EditorUtility.SetDirty(material);
        }

        private static void CreateParticles(string path)
        {
            var root=new GameObject(Path.GetFileNameWithoutExtension(path));
            try
            {
                var ps=root.AddComponent<ParticleSystem>();var main=ps.main;main.playOnAwake=false;main.loop=true;main.duration=2f;main.maxParticles=48;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.startLifetime=new ParticleSystem.MinMaxCurve(.55f,1.2f);main.startSize=new ParticleSystem.MinMaxCurve(.055f,.12f);main.startSpeed=new ParticleSystem.MinMaxCurve(.08f,.42f);main.startColor=Color.white;
                var emission=ps.emission;emission.rateOverTime=12f;var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Circle;shape.scale=new Vector3(.8f,.8f,.2f);shape.radius=.65f;shape.radiusThickness=.2f;
                var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;velocity.x=new ParticleSystem.MinMaxCurve(-.16f,.16f);velocity.y=new ParticleSystem.MinMaxCurve(.04f,.3f);velocity.z=new ParticleSystem.MinMaxCurve(-.12f,.12f);
                var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.14f),new GradientAlphaKey(.7f,.72f),new GradientAlphaKey(0,1)});color.color=gradient;
                var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,0),new Keyframe(.12f,1),new Keyframe(1,.12f)));
                var renderer=root.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(AdditiveMaterialPath);renderer.enabled=false;ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                if(PrefabUtility.SaveAsPrefabAsset(root,path)==null)throw new InvalidOperationException("Could not save shared particle prefab: "+path);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }

        private static void BuildOne(Definition definition)
        {
            var json=File.ReadAllText(Absolute(definition.RecipePath));var recipe=Parse(json,definition);var recipeHash=RecipeCanonicalizer.ComputeSha256(json);var buildHash=Hash(recipeHash+"|"+CompilerVersion+"|"+AssetDatabase.GetAssetDependencyHash(ShaderPath)+"|"+AssetDatabase.GetAssetDependencyHash(SharedRoot)+"|"+Application.unityVersion);
            var folder="Assets/VFX/Generated/"+definition.Id;ValidationGalleryCompiler.EnsureFolder(folder);var prefabPath=folder+"/VFX_"+definition.Id+".prefab";var root=new GameObject("VFX_"+definition.Id);
            try
            {
                var renderers=new List<Renderer>();var particles=new List<ParticleSystem>();var animated=new List<Transform>();var lines=new List<LineRenderer>();var modes=new List<float>();var intensities=new List<float>();TrailRenderer trail=null;Canvas canvas=null;var graphics=new List<Graphic>();
                BuildProfile(definition.Profile,root,renderers,particles,animated,lines,graphics,ref trail,ref canvas,modes,intensities);
                var controller=root.AddComponent<CoverageGalleryVfxController>();var so=new SerializedObject(controller);so.FindProperty("profile").enumValueIndex=(int)definition.Profile;SetObjects(so.FindProperty("renderers"),renderers.Cast<UnityEngine.Object>().ToArray());SetObjects(so.FindProperty("particles"),particles.Cast<UnityEngine.Object>().ToArray());SetObjects(so.FindProperty("animatedTransforms"),animated.Cast<UnityEngine.Object>().ToArray());SetObjects(so.FindProperty("lines"),lines.Cast<UnityEngine.Object>().ToArray());so.FindProperty("trail").objectReferenceValue=trail;so.FindProperty("screenCanvas").objectReferenceValue=canvas;SetObjects(so.FindProperty("graphics"),graphics.Cast<UnityEngine.Object>().ToArray());SetFloats(so.FindProperty("shapeModes"),modes.ToArray());SetFloats(so.FindProperty("intensities"),intensities.ToArray());so.FindProperty("primaryColor").colorValue=recipe.Primary;so.FindProperty("secondaryColor").colorValue=recipe.Secondary;so.FindProperty("sustained").boolValue=recipe.Sustained;so.FindProperty("duration").floatValue=recipe.Duration;so.ApplyModifiedPropertiesWithoutUndo();
                if(PrefabUtility.SaveAsPrefabAsset(root,prefabPath)==null)throw new InvalidOperationException("Could not save "+prefabPath);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            AssetDatabase.SaveAssets();var audit=VfxProductionRules.EnforceAndWriteManifest(recipe.Id,recipe.Archetype,recipe.RecipeVersion,recipe.Revision,recipeHash,buildHash,CompilerVersion,prefabPath,folder,recipe.Duration);if(audit.Report.HasErrors)throw new InvalidOperationException(string.Join(" | ",audit.Report.Entries.Select(e=>e.Code+" "+e.Path+" "+e.Message)));
        }

        private static void BuildProfile(CoverageGalleryProfile profile,GameObject root,List<Renderer> renderers,List<ParticleSystem> particles,List<Transform> animated,List<LineRenderer> lines,List<Graphic> graphics,ref TrailRenderer trail,ref Canvas canvas,List<float> modes,List<float> intensities)
        {
            var alpha=AssetDatabase.LoadAssetAtPath<Material>(AlphaMaterialPath);var add=AssetDatabase.LoadAssetAtPath<Material>(AdditiveMaterialPath);var quad=AssetDatabase.LoadAssetAtPath<Mesh>(QuadPath);var ring=AssetDatabase.LoadAssetAtPath<Mesh>(RingPath);var burst=AssetDatabase.LoadAssetAtPath<Mesh>(BurstPath);var cloud=AssetDatabase.LoadAssetAtPath<Mesh>(CloudPath);var sphere=BuiltinSphere();
            if(profile==CoverageGalleryProfile.Impact3D)
            {
                AddMesh(root,"Core",sphere,add,new Vector3(0,0,.08f),Vector3.one*.32f,Quaternion.identity,0,1.25f,renderers,modes,intensities,animated);
                AddMesh(root,"Shockwave",ring,add,Vector3.zero,Vector3.one*.82f,Quaternion.Euler(62,0,18),1,.92f,renderers,modes,intensities,animated);
                AddMesh(root,"BurstCrown",burst,add,Vector3.zero,Vector3.one*.9f,Quaternion.Euler(18,22,0),8,1.1f,renderers,modes,intensities,animated);AddParticle(root,ParticlePrefabPath,Vector3.zero,Vector3.one,renderers,particles,modes,intensities,1f);
            }
            else if(profile==CoverageGalleryProfile.Aura3D)
            {
                AddMesh(root,"BodyEnvelope",sphere,alpha,new Vector3(0,.08f,0),new Vector3(.48f,.78f,.42f),Quaternion.identity,9,.62f,renderers,modes,intensities,animated);
                AddMesh(root,"VerticalWispA",quad,add,new Vector3(-.18f,.08f,.06f),new Vector3(.22f,.78f,1),Quaternion.Euler(0,-18,-8),9,.78f,renderers,modes,intensities,animated);
                AddMesh(root,"VerticalWispB",quad,add,new Vector3(.2f,.02f,.02f),new Vector3(.18f,.68f,1),Quaternion.Euler(0,22,11),9,.68f,renderers,modes,intensities,animated);
                AddMesh(root,"GroundHalo",ring,add,new Vector3(0,-.48f,.04f),Vector3.one*.58f,Quaternion.Euler(70,0,0),5,.34f,renderers,modes,intensities,null);AddParticle(root,ParticlePrefabPath,new Vector3(0,-.08f,0),new Vector3(.72f,1.15f,.72f),renderers,particles,modes,intensities,.82f);
            }
            else if(profile==CoverageGalleryProfile.Area3D)
            {
                AddMesh(root,"GroundPool",quad,alpha,new Vector3(0,-.05f,.08f),new Vector3(1.05f,.68f,1),Quaternion.Euler(61,0,0),10,.72f,renderers,modes,intensities,null);
                AddMesh(root,"InnerPool",quad,add,new Vector3(-.12f,-.02f,.02f),new Vector3(.72f,.44f,1),Quaternion.Euler(61,0,13),10,.52f,renderers,modes,intensities,animated);
                AddMesh(root,"BoundaryHint",ring,add,new Vector3(0,-.04f,.02f),Vector3.one*.94f,Quaternion.Euler(61,0,0),5,.24f,renderers,modes,intensities,null);AddParticle(root,ParticlePrefabPath,new Vector3(0,.08f,0),new Vector3(1.2f,.58f,1),renderers,particles,modes,intensities,.76f);
            }
            else if(profile==CoverageGalleryProfile.Beam3D)
            {
                for(var i=0;i<3;i++){var go=new GameObject("Beam_"+(i+1));go.transform.SetParent(root.transform,false);var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.alignment=LineAlignment.View;line.textureMode=LineTextureMode.Tile;line.widthMultiplier=i==0?.21f:(i==1?.085f:.045f);line.sharedMaterial=add;line.numCapVertices=6;line.enabled=false;renderers.Add(line);lines.Add(line);modes.Add(3);intensities.Add(i==0?.42f:(i==1?1.18f:.88f));}
                AddMesh(root,"StartCap",sphere,add,new Vector3(-.93f,0,0),Vector3.one*.14f,Quaternion.identity,0,.92f,renderers,modes,intensities,animated);AddMesh(root,"EndCap",sphere,add,new Vector3(.93f,0,0),Vector3.one*.14f,Quaternion.identity,0,.92f,renderers,modes,intensities,animated);AddParticle(root,ParticlePrefabPath,Vector3.zero,new Vector3(1.25f,.24f,.35f),renderers,particles,modes,intensities,.68f);
            }
            else if(profile==CoverageGalleryProfile.Trail3D)
            {
                var head=new GameObject("MotionHead");head.transform.SetParent(root.transform,false);animated.Add(head.transform);AddMesh(head,"HeadCore",sphere,add,Vector3.zero,Vector3.one*.18f,Quaternion.identity,0,1.1f,renderers,modes,intensities,null);
                trail=head.AddComponent<TrailRenderer>();trail.time=1.05f;trail.minVertexDistance=.025f;trail.widthMultiplier=.34f;trail.widthCurve=new AnimationCurve(new Keyframe(0,.16f),new Keyframe(.28f,1),new Keyframe(1,0));trail.colorGradient=Gradient(Color.white);trail.sharedMaterial=add;trail.alignment=LineAlignment.View;trail.emitting=false;trail.enabled=false;renderers.Add(trail);modes.Add(3);intensities.Add(.92f);AddParticle(head,ParticlePrefabPath,Vector3.zero,Vector3.one*.72f,renderers,particles,modes,intensities,.86f);
            }
            else if(profile==CoverageGalleryProfile.Shield3D)
            {
                AddMesh(root,"OuterShell",sphere,alpha,Vector3.zero,new Vector3(.78f,.82f,.58f),Quaternion.identity,11,.82f,renderers,modes,intensities,animated);
                AddMesh(root,"InnerLattice",sphere,add,Vector3.zero,new Vector3(.69f,.73f,.52f),Quaternion.Euler(0,25,0),2,.52f,renderers,modes,intensities,animated);
                AddMesh(root,"ImpactFacet",burst,add,new Vector3(.34f,.14f,-.12f),Vector3.one*.42f,Quaternion.Euler(0,-28,-12),8,.78f,renderers,modes,intensities,animated);AddParticle(root,ParticlePrefabPath,Vector3.zero,Vector3.one*.76f,renderers,particles,modes,intensities,.48f);
            }
            else if(profile==CoverageGalleryProfile.Spawn3D)
            {
                AddMesh(root,"RisingCore",sphere,add,new Vector3(0,-.22f,0),Vector3.one*.22f,Quaternion.identity,0,1.08f,renderers,modes,intensities,animated);AddMesh(root,"Portal",ring,add,new Vector3(0,-.42f,0),Vector3.one*.84f,Quaternion.Euler(66,0,0),5,.88f,renderers,modes,intensities,animated);AddMesh(root,"SigilRing",ring,add,new Vector3(0,-.4f,.02f),Vector3.one*.64f,Quaternion.Euler(66,0,0),1,.7f,renderers,modes,intensities,animated);AddMesh(root,"EnergyColumn",quad,alpha,new Vector3(0,.05f,.12f),new Vector3(.34f,.72f,1),Quaternion.identity,6,.5f,renderers,modes,intensities,animated);AddParticle(root,ParticlePrefabPath,new Vector3(0,-.22f,0),Vector3.one*.8f,renderers,particles,modes,intensities,.9f);
            }
            else if(profile==CoverageGalleryProfile.Environment)
            {
                AddMesh(root,"FogBack",cloud,alpha,new Vector3(-.18f,.12f,.22f),new Vector3(1.08f,.42f,1),Quaternion.Euler(0,0,-5),12,.38f,renderers,modes,intensities,animated);
                AddMesh(root,"FogMiddle",cloud,alpha,new Vector3(.18f,-.02f,.08f),new Vector3(.88f,.34f,1),Quaternion.Euler(0,0,7),12,.31f,renderers,modes,intensities,animated);
                AddMesh(root,"FogFront",cloud,alpha,new Vector3(-.04f,-.18f,-.08f),new Vector3(.68f,.25f,1),Quaternion.Euler(0,0,-2),12,.24f,renderers,modes,intensities,animated);AddParticle(root,ParticlePrefabPath,new Vector3(0,.34f,0),new Vector3(1.25f,1.12f,1),renderers,particles,modes,intensities,1.05f);
            }
            else CreateScreenUi(root,graphics,animated,ref canvas);
        }

        private static void CreateScreenUi(GameObject root,List<Graphic> graphics,List<Transform> animated,ref Canvas canvas)
        {
            canvas=root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.planeDistance=1f;canvas.sortingOrder=220;var scaler=root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;root.AddComponent<GraphicRaycaster>().enabled=false;
            var safe=new GameObject("ScreenFeedbackSafeArea",typeof(RectTransform),typeof(RectMask2D));safe.transform.SetParent(root.transform,false);var safeRect=(RectTransform)safe.transform;safeRect.anchorMin=Vector2.zero;safeRect.anchorMax=Vector2.one;safeRect.offsetMin=new Vector2(24,24);safeRect.offsetMax=new Vector2(-24,-24);
            var vignetteObject=new GameObject("DamageVignette",typeof(RectTransform),typeof(CanvasRenderer),typeof(CoverageScreenFeedbackGraphic));vignetteObject.transform.SetParent(safe.transform,false);var vignetteRect=(RectTransform)vignetteObject.transform;vignetteRect.anchorMin=Vector2.zero;vignetteRect.anchorMax=Vector2.one;vignetteRect.offsetMin=Vector2.zero;vignetteRect.offsetMax=Vector2.zero;var vignette=vignetteObject.GetComponent<CoverageScreenFeedbackGraphic>();vignette.raycastTarget=false;vignette.color=Color.clear;graphics.Add(vignette);
            var directionLeft=UiImage(safe.transform,"DamageDirectionLeft",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-16,66),new Vector2(38,7));directionLeft.rectTransform.localRotation=Quaternion.Euler(0,0,-28);graphics.Add(directionLeft);animated.Add(directionLeft.transform);
            var directionRight=UiImage(safe.transform,"DamageDirectionRight",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(16,66),new Vector2(38,7));directionRight.rectTransform.localRotation=Quaternion.Euler(0,0,28);graphics.Add(directionRight);animated.Add(directionRight.transform);
            for(var i=0;i<4;i++){var angle=45+i*90;var radians=angle*Mathf.Deg2Rad;var marker=UiImage(safe.transform,"HitMarker_"+i,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(Mathf.Cos(radians)*16,Mathf.Sin(radians)*16-4),new Vector2(16,3));marker.rectTransform.localRotation=Quaternion.Euler(0,0,angle);graphics.Add(marker);animated.Add(marker.transform);}
            foreach(var graphic in graphics)graphic.enabled=false;canvas.enabled=false;
        }

        private static Image UiImage(Transform parent,string name,Vector2 anchorMin,Vector2 anchorMax,Vector2 anchored,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(parent,false);var rect=(RectTransform)go.transform;rect.anchorMin=anchorMin;rect.anchorMax=anchorMax;rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;if(size!=Vector2.zero){rect.anchorMin=rect.anchorMax=anchorMin;rect.sizeDelta=size;rect.anchoredPosition=anchored;}var image=go.GetComponent<Image>();image.raycastTarget=false;image.color=Color.clear;return image;
        }

        private static void AddMesh(GameObject parent,string name,Mesh mesh,Material material,Vector3 position,Vector3 scale,Quaternion rotation,float mode,float intensity,List<Renderer> renderers,List<float> modes,List<float> intensities,List<Transform> animated)
        {
            var go=new GameObject(name);go.transform.SetParent(parent.transform,false);go.transform.localPosition=position;go.transform.localScale=scale;go.transform.localRotation=rotation;go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.enabled=false;renderers.Add(renderer);modes.Add(mode);intensities.Add(intensity);if(animated!=null)animated.Add(go.transform);
        }

        private static void AddParticle(GameObject parent,string path,Vector3 position,Vector3 scale,List<Renderer> renderers,List<ParticleSystem> particles,List<float> modes,List<float> intensities,float intensity)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab==null)throw new InvalidOperationException("Missing shared particles: "+path);var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent.transform);go.transform.localPosition=position;go.transform.localRotation=Quaternion.identity;go.transform.localScale=scale;var ps=go.GetComponent<ParticleSystem>();particles.Add(ps);renderers.Add(go.GetComponent<ParticleSystemRenderer>());modes.Add(8);intensities.Add(intensity);
        }

        private static Mesh BuiltinSphere(){var temp=GameObject.CreatePrimitive(PrimitiveType.Sphere);try{return temp.GetComponent<MeshFilter>().sharedMesh;}finally{UnityEngine.Object.DestroyImmediate(temp);}}
        private static Gradient Gradient(Color color){var value=new Gradient();value.SetKeys(new[]{new GradientColorKey(color,0),new GradientColorKey(color,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});return value;}

        internal static Recipe Parse(string json,Definition expected)
        {
            var root=JObject.Parse(json);var allowed=new[]{"recipeVersion","revision","id","archetype","dimension","lifecycle","duration","primaryColor","secondaryColor"};foreach(var property in root.Properties())if(!allowed.Contains(property.Name,StringComparer.Ordinal))throw new InvalidOperationException("Unknown field /"+property.Name);
            var recipe=new Recipe{RecipeVersion=RequiredInt(root,"recipeVersion"),Revision=RequiredInt(root,"revision"),Id=RequiredString(root,"id"),Archetype=RequiredString(root,"archetype"),Dimension=RequiredString(root,"dimension"),Lifecycle=RequiredString(root,"lifecycle"),Duration=(float)RequiredNumber(root,"duration")};if(recipe.RecipeVersion!=1||recipe.Revision<1||recipe.Id!=expected.Id||recipe.Archetype!=expected.Archetype||recipe.Dimension!=expected.Dimension)throw new InvalidOperationException("Recipe identity/version mismatch: "+expected.Id);if(recipe.Lifecycle!="sustained"&&recipe.Lifecycle!="event_driven"&&recipe.Lifecycle!="one_shot")throw new InvalidOperationException("Unsupported lifecycle: "+recipe.Lifecycle);if(recipe.Duration<.4f||recipe.Duration>8f)throw new InvalidOperationException("Duration outside 0.4..8 seconds.");if(!ColorUtility.TryParseHtmlString(RequiredString(root,"primaryColor"),out recipe.Primary)||!ColorUtility.TryParseHtmlString(RequiredString(root,"secondaryColor"),out recipe.Secondary))throw new InvalidOperationException("Invalid color.");return recipe;
        }

        private static string RequiredString(JObject root,string name){var token=root[name];if(token==null||token.Type!=JTokenType.String||string.IsNullOrWhiteSpace((string)token))throw new InvalidOperationException("Missing string /"+name);return(string)token;}
        private static int RequiredInt(JObject root,string name){var token=root[name];if(token==null||token.Type!=JTokenType.Integer)throw new InvalidOperationException("Missing integer /"+name);return(int)token;}
        private static double RequiredNumber(JObject root,string name){var token=root[name];if(token==null||(token.Type!=JTokenType.Integer&&token.Type!=JTokenType.Float))throw new InvalidOperationException("Missing number /"+name);return(double)token;}
        private static string Hash(string text){using(var sha=SHA256.Create())return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(v=>v.ToString("x2",CultureInfo.InvariantCulture)));}
        private static void SetObjects(SerializedProperty property,UnityEngine.Object[] values){property.arraySize=values.Length;for(var i=0;i<values.Length;i++)property.GetArrayElementAtIndex(i).objectReferenceValue=values[i];}
        private static void SetFloats(SerializedProperty property,float[] values){property.arraySize=values.Length;for(var i=0;i<values.Length;i++)property.GetArrayElementAtIndex(i).floatValue=values[i];}
        private static string Absolute(string path){return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,path.Replace('/',Path.DirectorySeparatorChar)));}
    }
}
