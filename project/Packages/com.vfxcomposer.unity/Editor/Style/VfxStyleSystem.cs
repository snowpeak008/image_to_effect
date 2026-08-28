using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Validation;
using VFXComposer.Editor.ValidationGallery;

namespace VFXComposer.Editor.Style
{
    public sealed class VfxStyleDefinition
    {
        public string Token;
        public string DisplayName;
        public string ShaderName;
        public float Mode;
        public float DefaultOutline;
        public int DefaultShadingSteps;
        public float DefaultNoiseScale;
        public float DefaultGlow;
        public bool Supports2D;
        public bool Supports3D;
    }

    public static class VfxStyleRegistry
    {
        private static readonly VfxStyleDefinition[] ValuesInternal =
        {
            D("stylized","Stylized","VFXComposer/Style/LayeredRamp",0,.08f,4,1.2f,1.0f,true,true),
            D("cartoon","Cartoon","VFXComposer/Style/LayeredRamp",1,.18f,3,.55f,.72f,true,true),
            D("semireal","Semi-real","VFXComposer/Style/SoftNoise",2,.03f,6,3.2f,1.18f,false,true),
            D("pixel","Pixel","VFXComposer/Style/PixelQuantize",3,.05f,4,.35f,.68f,true,false),
            D("inkwash","Ink Wash","VFXComposer/Style/InkBrush",4,.28f,3,4.2f,.45f,true,false),
            D("holo","Holographic","VFXComposer/Style/HoloFresnel",5,.2f,3,1.1f,1.15f,true,true),
            D("dark","Dark Ritual","VFXComposer/Style/DissolveEdge",6,.16f,3,2.7f,.82f,true,true),
            D("neon","Neon","VFXComposer/Style/LayeredRamp",7,.24f,2,.8f,1.38f,true,true),
            D("lowpoly","Low Poly","VFXComposer/Style/LayeredRamp",8,.02f,2,.4f,.72f,false,true),
            D("crystal","Crystal Glass","VFXComposer/Style/HoloFresnel",9,.12f,5,1.5f,1.28f,true,true),
            D("candy","Candy","VFXComposer/Style/LayeredRamp",10,.2f,3,.65f,1.05f,true,true),
            D("cosmic","Cosmic","VFXComposer/Style/SoftNoise",11,.08f,5,4.8f,1.18f,true,true),
            D("steampunk","Steampunk","VFXComposer/Style/LayeredRamp",12,.12f,4,1.6f,.88f,false,true),
            D("ghost","Ghost","VFXComposer/Style/SoftNoise",13,.06f,4,3.6f,.82f,true,true)
        };
        public static IEnumerable<VfxStyleDefinition> All { get { return ValuesInternal; } }
        public static bool TryGet(string token, out VfxStyleDefinition definition) { definition = ValuesInternal.FirstOrDefault(value => string.Equals(value.Token, token, StringComparison.Ordinal)); return definition != null; }
        private static VfxStyleDefinition D(string token,string name,string shader,float mode,float outline,int steps,float noise,float glow,bool d2,bool d3){return new VfxStyleDefinition{Token=token,DisplayName=name,ShaderName=shader,Mode=mode,DefaultOutline=outline,DefaultShadingSteps=steps,DefaultNoiseScale=noise,DefaultGlow=glow,Supports2D=d2,Supports3D=d3};}
    }

    public static class VfxStyleSharedLibrary
    {
        public const string Root = "Assets/VFX/Shared/Styles";
        public const string MaterialRoot = Root + "/Materials";
        public const string TextureRoot = Root + "/Textures";
        public const string MeshRoot = Root + "/Meshes";
        public const string QuadPath = MeshRoot + "/M_Style_Quad.asset";
        public const string RingPath = MeshRoot + "/M_Style_Ring.asset";
        public const string RibbonPath = MeshRoot + "/M_Style_Ribbon.asset";
        public const string BurstPath = MeshRoot + "/M_Style_Burst.asset";
        public const string ConePath = MeshRoot + "/M_Style_Cone.asset";
        public const string ShardPath = MeshRoot + "/M_Style_Shard.asset";
        public static readonly string[] FacetPaths = { MeshRoot+"/M_Style_Facet_A.asset", MeshRoot+"/M_Style_Facet_B.asset", MeshRoot+"/M_Style_Facet_C.asset" };
        public static readonly string[] GearPaths = { MeshRoot+"/M_Style_Gear_A.asset", MeshRoot+"/M_Style_Gear_B.asset", MeshRoot+"/M_Style_Gear_C.asset" };

        private static readonly string[] Masks = { "BrushWide", "BrushDry", "BrushBreakup", "BrushSwirl", "NoisePerlin", "NoiseVoronoi", "NoiseFiber", "NoiseGrain", "ShapeSoftCircle", "ShapeRing", "ShapeHex", "ShapeStar", "ShapeRune", "ShapeScanline" };
        private static readonly string[] Luts = { "PaletteWarmFire", "PaletteColdFrost", "PaletteToxicPurple" };

        [MenuItem("Tools/VFX Composer/Style/Build W1 Shared Style Library")]
        public static void EnsureAllMenu() { EnsureAll(); Debug.Log("W1 shared style library is current. Visual sign-off remains pending final user review."); }

        public static void EnsureAll()
        {
            EnsureFolder(MaterialRoot); EnsureFolder(TextureRoot); EnsureFolder(MeshRoot);
            EnsureMeshes(); EnsureTextures();
            foreach (var style in VfxStyleRegistry.All) EnsureMaterial(style);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }

        public static string MaterialPath(string token) { return MaterialRoot + "/MAT_Style_" + token + ".mat"; }
        public static Material MaterialFor(string token) { return AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(token)); }
        public static string DependencySignature()
        {
            var extraTextures=new[]{"T_AnimeSmearAtlas_256.png","T_SymbolAtlas_128.png","T_NebulaA_64.png","T_NebulaB_64.png","T_StarAtlas_64.png"}.Select(value=>TextureRoot+"/"+value);
            var paths = VfxStyleRegistry.All.Select(value => MaterialPath(value.Token)).Concat(new[] { QuadPath, RingPath, RibbonPath, BurstPath, ConePath, ShardPath }).Concat(FacetPaths).Concat(GearPaths).Concat(Masks.Select(value => TextureRoot + "/T_" + value + "_64.png")).Concat(Luts.Select(value => TextureRoot + "/T_" + value + "_16.png")).Concat(extraTextures);
            return string.Join("|", paths.OrderBy(value => value, StringComparer.Ordinal).Select(value => value + ":" + AssetDatabase.GetAssetDependencyHash(value)));
        }

        private static void EnsureMaterial(VfxStyleDefinition style)
        {
            var shader = Shader.Find(style.ShaderName); if (shader == null) throw new InvalidOperationException("Missing style shader: " + style.ShaderName);
            var path = MaterialPath(style.Token); var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) }; AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.SetFloat("_StyleMode", style.Mode); material.SetFloat("_Outline", style.DefaultOutline); material.SetFloat("_ShadingSteps", style.DefaultShadingSteps); material.SetFloat("_NoiseScale", style.DefaultNoiseScale); material.SetFloat("_Intensity", style.DefaultGlow); material.SetFloat("_GlobalAlpha", 1f); material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", style.Token == "inkwash" ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.One); material.renderQueue = 3020 + (int)style.Mode; material.SetOverrideTag("RenderType", "Transparent"); EditorUtility.SetDirty(material);
        }

        private static void EnsureMeshes()
        {
            SaveMesh(QuadPath, Quad()); SaveMesh(RingPath, Ring(48,.64f,1f)); SaveMesh(RibbonPath, Ribbon(24)); SaveMesh(BurstPath, Burst(12)); SaveMesh(ConePath, Cone(20)); SaveMesh(ShardPath, Shard());for(var i=0;i<3;i++){SaveMesh(FacetPaths[i],Facet(i));SaveMesh(GearPaths[i],Gear(8+i*2,.56f+i*.035f,1f));}
        }

        private static void SaveMesh(string path, Mesh source)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { source.name = Path.GetFileNameWithoutExtension(path); AssetDatabase.CreateAsset(source,path); return; }
            EditorUtility.CopySerialized(source,mesh); mesh.name=Path.GetFileNameWithoutExtension(path); EditorUtility.SetDirty(mesh); UnityEngine.Object.DestroyImmediate(source);
        }
        private static Mesh Quad(){var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-1,-1),new Vector3(-1,1),new Vector3(1,1),new Vector3(1,-1)};mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
        private static Mesh Ring(int segments,float inner,float outer){var mesh=new Mesh();var vertices=new Vector3[(segments+1)*2];var uv=new Vector2[vertices.Length];var triangles=new int[segments*6];for(var i=0;i<=segments;i++){var a=i/(float)segments*Mathf.PI*2;var d=new Vector3(Mathf.Cos(a),Mathf.Sin(a));vertices[i*2]=d*inner;vertices[i*2+1]=d*outer;uv[i*2]=new Vector2(i/(float)segments,0);uv[i*2+1]=new Vector2(i/(float)segments,1);if(i<segments){var t=i*6;var v=i*2;triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+3;triangles[t+3]=v;triangles[t+4]=v+3;triangles[t+5]=v+2;}}mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
        private static Mesh Ribbon(int segments){var mesh=new Mesh();var vertices=new Vector3[(segments+1)*2];var uv=new Vector2[vertices.Length];var triangles=new int[segments*6];for(var i=0;i<=segments;i++){var t=i/(float)segments;var center=new Vector3(Mathf.Lerp(-1,1,t),Mathf.Sin(t*Mathf.PI)*.35f);var normal=new Vector3(-Mathf.Cos(t*Mathf.PI)*.35f,2f).normalized;var width=Mathf.Lerp(.08f,.42f,Mathf.Sin(t*Mathf.PI));vertices[i*2]=center-normal*width;vertices[i*2+1]=center+normal*width;uv[i*2]=new Vector2(t,0);uv[i*2+1]=new Vector2(t,1);if(i<segments){var q=i*6;var v=i*2;triangles[q]=v;triangles[q+1]=v+1;triangles[q+2]=v+3;triangles[q+3]=v;triangles[q+4]=v+3;triangles[q+5]=v+2;}}mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
        private static Mesh Burst(int rays){var mesh=new Mesh();var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();for(var i=0;i<rays;i++){var a=i*Mathf.PI*2/rays;var w=.07f;var start=vertices.Count;vertices.Add(Vector3.zero);vertices.Add(new Vector3(Mathf.Cos(a-w),Mathf.Sin(a-w))*(.65f+(i%3)*.08f));vertices.Add(new Vector3(Mathf.Cos(a+w),Mathf.Sin(a+w))*(1f+(i%4)*.11f));uv.Add(new Vector2(.5f,.5f));uv.Add(Vector2.zero);uv.Add(Vector2.one);triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);}mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
        private static Mesh Cone(int segments){var mesh=new Mesh();var vertices=new List<Vector3>{Vector3.zero};var uv=new List<Vector2>{new Vector2(.5f,0)};for(var i=0;i<=segments;i++){var t=i/(float)segments;vertices.Add(new Vector3(Mathf.Lerp(-.48f,.48f,t),1f-.14f*Mathf.Sin(t*Mathf.PI*3)));uv.Add(new Vector2(t,1));}var triangles=new List<int>();for(var i=0;i<segments;i++){triangles.Add(0);triangles.Add(i+1);triangles.Add(i+2);}mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
        private static Mesh Shard(){var mesh=new Mesh();mesh.vertices=new[]{new Vector3(0,1),new Vector3(-.34f,.08f),new Vector3(0,-1),new Vector3(.34f,.08f)};mesh.uv=new[]{new Vector2(.5f,1),Vector2.zero,new Vector2(.5f,0),Vector2.one};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
        private static Mesh Facet(int variant){var mesh=new Mesh();var sx=.55f+variant*.14f;mesh.vertices=new[]{new Vector3(0,1.15f,0),new Vector3(-sx,.12f,.16f),new Vector3(-.28f,-1,0),new Vector3(.32f,-.82f,-.12f),new Vector3(sx,.16f,.18f),new Vector3(0,.08f,-.32f)};mesh.uv=new[]{new Vector2(.5f,1),Vector2.zero,new Vector2(.2f,0),new Vector2(.8f,0),Vector2.one,new Vector2(.5f,.45f)};mesh.triangles=new[]{0,1,5,1,2,5,2,3,5,3,4,5,4,0,5};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
        private static Mesh Gear(int teeth,float inner,float outer){var segments=teeth*4;var vertices=new List<Vector3>{Vector3.zero};var uv=new List<Vector2>{new Vector2(.5f,.5f)};for(var i=0;i<=segments;i++){var a=i/(float)segments*Mathf.PI*2;var tooth=(i%4==1||i%4==2)?outer:inner;vertices.Add(new Vector3(Mathf.Cos(a),Mathf.Sin(a))*tooth);uv.Add(new Vector2(.5f+Mathf.Cos(a)*.5f,.5f+Mathf.Sin(a)*.5f));}var triangles=new List<int>();for(var i=0;i<segments;i++){triangles.Add(0);triangles.Add(i+1);triangles.Add(i+2);}var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}

        private static void EnsureTextures()
        {
            for(var i=0;i<Masks.Length;i++) WriteTexture(TextureRoot+"/T_"+Masks[i]+"_64.png",64,64,(x,y)=>Mask(i,x,y));
            for(var i=0;i<Luts.Length;i++){var index=i;WriteTexture(TextureRoot+"/T_"+Luts[i]+"_16.png",16,1,(x,y)=>Palette(index,x));}
            WriteTexture(TextureRoot+"/T_AnimeSmearAtlas_256.png",256,256,(x,y)=>AtlasMask(x,y,256,8,2));
            WriteTexture(TextureRoot+"/T_SymbolAtlas_128.png",128,128,(x,y)=>SymbolMask(x,y));
            WriteTexture(TextureRoot+"/T_NebulaA_64.png",64,64,(x,y)=>NoiseMask(x,y,3.1f,.17f));
            WriteTexture(TextureRoot+"/T_NebulaB_64.png",64,64,(x,y)=>NoiseMask(x,y,7.3f,.53f));
            WriteTexture(TextureRoot+"/T_StarAtlas_64.png",64,64,(x,y)=>StarMask(x,y));
        }
        private static Color32 AtlasMask(int x,int y,int size,int cols,int rows){var cellW=size/cols;var cellH=size/rows;var lx=(x%cellW+.5f)/cellW;var ly=(y%cellH+.5f)/cellH;var frame=x/cellW+(y/cellH)*cols;var ridge=.5f+.22f*Mathf.Sin((lx+frame*.071f)*Mathf.PI*2);var alpha=Mathf.Clamp01(1-Mathf.Abs(ly-ridge)/(.06f+.2f*lx))*Mathf.Sin(Mathf.PI*lx);return new Color32(255,255,255,(byte)Mathf.RoundToInt(alpha*255));}
        private static Color32 SymbolMask(int x,int y){var u=(x+.5f)/128f;var v=(y+.5f)/128f;var cx=(x<64?.25f:.75f);var cy=(y<64?.25f:.75f);var dx=u-cx;var dy=v-cy;var cell=(x/64)+(y/64)*2;float a;if(cell==0)a=Mathf.Clamp01(1-Mathf.Abs(dx*dx+dy*dy-.045f)*45);else if(cell==1)a=Mathf.Clamp01(1-Mathf.Min(Mathf.Abs(dx),Mathf.Abs(dy))*18)*Mathf.Clamp01(1-Mathf.Sqrt(dx*dx+dy*dy)*3);else if(cell==2)a=Mathf.Clamp01(1-Mathf.Abs(Mathf.Atan2(dy,dx)%(.5f*Mathf.PI))*.9f)*Mathf.Clamp01(1-Mathf.Sqrt(dx*dx+dy*dy)*3.2f);else a=Mathf.Clamp01(1-Mathf.Abs(Mathf.Sqrt(dx*dx+dy*dy)-.17f)*22);return new Color32(255,255,255,(byte)Mathf.RoundToInt(a*255));}
        private static Color32 NoiseMask(int x,int y,float scale,float offset){var u=(x+.5f)/64f;var v=(y+.5f)/64f;var n=Mathf.PerlinNoise(u*scale+offset,v*scale+offset*.7f);return new Color32((byte)(n*90),(byte)(n*150),(byte)(n*255),(byte)(n*255));}
        private static Color32 StarMask(int x,int y){var u=(x+.5f)/64f;var v=(y+.5f)/64f;var d=Mathf.Sqrt((u-.5f)*(u-.5f)+(v-.5f)*(v-.5f));var cross=Mathf.Max(Mathf.Exp(-Mathf.Abs(u-.5f)*48),Mathf.Exp(-Mathf.Abs(v-.5f)*48));var a=Mathf.Clamp01((1-d*4)*.65f+cross*(1-d*1.8f));return new Color32(255,255,255,(byte)Mathf.RoundToInt(a*255));}
        private static Color32 Mask(int variant,int x,int y){var u=(x+.5f)/64f;var v=(y+.5f)/64f;var dx=u-.5f;var dy=v-.5f;var radial=Mathf.Sqrt(dx*dx+dy*dy);var angle=Mathf.Atan2(dy,dx);var noise=Mathf.PerlinNoise(u*(3+variant%5)+variant*.31f,v*(4+variant%4)+variant*.17f);float a;if(variant<4)a=Mathf.Clamp01(1-Mathf.Abs(v-(.5f+.12f*Mathf.Sin(u*(7+variant))))/(.08f+.18f*u))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.12f,u))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.86f,1,u)))*(.55f+.45f*noise);else if(variant<8)a=Mathf.SmoothStep(.28f,.72f,noise)*Mathf.Clamp01(1-radial*1.7f);else if(variant==8)a=Mathf.SmoothStep(.52f,.05f,radial);else if(variant==9)a=Mathf.SmoothStep(.12f,0,Mathf.Abs(radial-.34f));else if(variant==10)a=Mathf.SmoothStep(.12f,0,Mathf.Abs(Mathf.Cos(angle*3)*.38f-radial+.16f));else if(variant==11)a=Mathf.SmoothStep(.15f,0,Mathf.Abs(Mathf.Sin(angle*4)*.3f-radial+.12f));else if(variant==12)a=Mathf.SmoothStep(.1f,0,Mathf.Abs(Mathf.Sin(angle*6+radial*18)))*Mathf.Clamp01(1-radial*1.4f);else a=(x%8<2?1f:0f)*Mathf.Clamp01(1-Mathf.Abs(dy)*2);var value=(byte)Mathf.RoundToInt(Mathf.Clamp01(a)*255);return new Color32(255,255,255,value);}
        private static Color32 Palette(int variant,int x){var t=x/15f;Color a,b,c;if(variant==0){a=new Color(.22f,.01f,0);b=new Color(1,.15f,.01f);c=new Color(1,.92f,.2f);}else if(variant==1){a=new Color(0,.08f,.24f);b=new Color(0,.55f,1);c=new Color(.8f,1,1);}else{a=new Color(.08f,0,.12f);b=new Color(.55f,.03f,.78f);c=new Color(.68f,1,.15f);}var color=t<.5f?Color.Lerp(a,b,t*2):Color.Lerp(b,c,(t-.5f)*2);return color;}
        private static void WriteTexture(string path,int width,int height,Func<int,int,Color32> pixel)
        {
            var absolute=Absolute(path);if(!File.Exists(absolute)){var texture=new Texture2D(width,height,TextureFormat.RGBA32,false,true);var pixels=new Color32[width*height];for(var y=0;y<height;y++)for(var x=0;x<width;x++)pixels[y*width+x]=pixel(x,y);texture.SetPixels32(pixels);texture.Apply(false,false);File.WriteAllBytes(absolute,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);}AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Default;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;importer.sRGBTexture=true;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=path.Contains("Palette")?FilterMode.Point:FilterMode.Bilinear;importer.maxTextureSize=Mathf.Max(width,height);importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();
        }
        internal static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;var parent=Path.GetDirectoryName(path).Replace('\\','/');if(!AssetDatabase.IsValidFolder(parent))EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
        private static string Absolute(string assetPath){return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,assetPath.Replace('/',Path.DirectorySeparatorChar)));}
    }

    public sealed class StyledContentBuildResult
    {
        public bool Succeeded;
        public bool Unchanged;
        public string PrefabPath;
        public string RecipeHash;
        public string BuildHash;
        public ValidationReport Report = new ValidationReport();
    }

    public static class StyledContentCompiler
    {
        public const string CompilerVersion = "styled-content-4";
        public static StyledContentBuildResult BuildAsset(string recipePath)
        {
            var absolute=Absolute(recipePath);if(!File.Exists(absolute)){var missing=new StyledContentBuildResult();missing.Report.Add("E1800",ValidationSeverity.Error,"/recipe","Styled content Recipe is missing.",new Newtonsoft.Json.Linq.JValue(recipePath));return missing;}return BuildCore(recipePath,File.ReadAllText(absolute),null);
        }

        public static StyledContentBuildResult BuildJsonForTransaction(string recipeAssetPath,string recipeJson){return BuildCore(recipeAssetPath,recipeJson,recipeAssetPath);}

        private static StyledContentBuildResult BuildCore(string recipePath,string json,string sourceOverride)
        {
            var result=new StyledContentBuildResult();VfxStyleSharedLibrary.EnsureAll();var catalog=VfxCompiler.LoadFormalCatalog();result.Report.AddRange(RecipeValidator.Validate(json,catalog));if(result.Report.HasErrors)return result;var recipe=VfxDomainParser.ParseRecipe(json).Value;var style=recipe.Style??new RecipeStyleContract{Token="stylized"};VfxStyleDefinition definition;if(!VfxStyleRegistry.TryGet(style.Token??"stylized",out definition)){result.Report.Add("E1801",ValidationSeverity.Error,"/style/token","Style is not registered.");return result;}if((recipe.Dimension==RecipeDimension.TwoD&&!definition.Supports2D)||(recipe.Dimension==RecipeDimension.ThreeD&&!definition.Supports3D)){result.Report.Add("E1802",ValidationSeverity.Error,"/style/token","Style does not support this Recipe dimension.");return result;}
            var recipeHash=RecipeCanonicalizer.ComputeSha256(json);var buildHash=Hash(recipeHash+"|"+CompilerVersion+"|"+VfxStyleSharedLibrary.DependencySignature()+"|"+Application.unityVersion);result.RecipeHash=recipeHash;result.BuildHash=buildHash;var folder="Assets/VFX/Generated/"+recipe.Id;var prefabPath=folder+"/VFX_"+recipe.Id+".prefab";result.PrefabPath=prefabPath;var existing=ReadManifestBuildHash(recipe.Id);if(existing==buildHash&&AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)!=null){result.Succeeded=true;result.Unchanged=true;return result;}
            VfxStyleSharedLibrary.EnsureFolder(folder);var root=new GameObject("VFX_"+recipe.Id);try{BuildRuntime(root,recipe,definition,style);if(PrefabUtility.SaveAsPrefabAsset(root,prefabPath)==null)throw new InvalidOperationException("Could not save "+prefabPath);}catch(Exception exception){result.Report.Add("E1803",ValidationSeverity.Error,"/build",exception.Message);return result;}finally{UnityEngine.Object.DestroyImmediate(root);}AssetDatabase.SaveAssets();var duration=Duration(recipe);var audit=VfxProductionRules.EnforceAndWriteManifest(recipe.Id,ArchetypeToken(recipe.Archetype),recipe.RecipeVersion,recipe.Revision,recipeHash,buildHash,CompilerVersion,prefabPath,folder,duration,sourceOverride);result.Report.AddRange(audit.Report);result.Succeeded=!result.Report.HasErrors;return result;
        }

        private static void BuildRuntime(GameObject root,Recipe recipe,VfxStyleDefinition style,RecipeStyleContract contract)
        {
            var material=VfxStyleSharedLibrary.MaterialFor(style.Token);if(material==null)throw new InvalidOperationException("Style material is missing: "+style.Token);var renderers=new List<Renderer>();var animated=new List<Transform>();var particles=new List<ParticleSystem>();var lines=new List<LineRenderer>();var trails=new List<TrailRenderer>();var profile=Profile(recipe.Archetype);var meshes=MeshesFor(recipe.Archetype);for(var i=0;i<meshes.Length;i++){var go=new GameObject("Layer_"+(i+1).ToString("00",CultureInfo.InvariantCulture));go.transform.SetParent(root.transform,false);go.transform.localScale=LayerScale(recipe.Archetype,i);go.transform.localRotation=LayerRotation(recipe.Archetype,i);go.transform.localPosition=LayerPosition(recipe.Archetype,i);go.AddComponent<MeshFilter>().sharedMesh=meshes[i];var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=10+i;renderer.enabled=false;renderers.Add(renderer);animated.Add(go.transform);}
            if(IsW15(recipe.Archetype)||recipe.Content!=null)AddParticles(root,recipe,material,renderers,particles);
            if(recipe.Archetype==RecipeArchetype.Beam||recipe.Archetype==RecipeArchetype.WeaponTrail){var go=new GameObject(recipe.Archetype==RecipeArchetype.Beam?"BeamLine":"WeaponEndpointLine");go.transform.SetParent(root.transform,false);var line=go.AddComponent<LineRenderer>();line.useWorldSpace=recipe.Archetype==RecipeArchetype.WeaponTrail;line.alignment=LineAlignment.View;line.positionCount=recipe.Archetype==RecipeArchetype.WeaponTrail?2:9;line.widthMultiplier=.12f;line.numCapVertices=5;line.sharedMaterial=material;line.enabled=false;renderers.Add(line);lines.Add(line);}
            if(recipe.Archetype==RecipeArchetype.Projectile||recipe.Archetype==RecipeArchetype.Trail||recipe.Archetype==RecipeArchetype.WeaponTrail){var host=animated[0].gameObject;var trail=host.AddComponent<TrailRenderer>();trail.time=.45f;trail.minVertexDistance=.02f;trail.widthMultiplier=.2f;trail.widthCurve=new AnimationCurve(new Keyframe(0,1),new Keyframe(1,0));trail.sharedMaterial=material;trail.emitting=false;trail.enabled=false;renderers.Add(trail);trails.Add(trail);}
            var controller=root.AddComponent<StyledVfxController>();var serialized=new SerializedObject(controller);serialized.FindProperty("profile").enumValueIndex=(int)profile;serialized.FindProperty("lifecycle").enumValueIndex=(int)Lifecycle(recipe);serialized.FindProperty("duration").floatValue=Duration(recipe);SetObjects(serialized.FindProperty("renderers"),renderers.Cast<UnityEngine.Object>().ToArray());SetObjects(serialized.FindProperty("animatedTransforms"),animated.Cast<UnityEngine.Object>().ToArray());SetObjects(serialized.FindProperty("particles"),particles.Cast<UnityEngine.Object>().ToArray());SetObjects(serialized.FindProperty("lines"),lines.Cast<UnityEngine.Object>().ToArray());SetObjects(serialized.FindProperty("trails"),trails.Cast<UnityEngine.Object>().ToArray());serialized.FindProperty("primary").colorValue=ColorValue(contract,"primary",new Color(.28f,.65f,1));serialized.FindProperty("secondary").colorValue=ColorValue(contract,"secondary",new Color(.75f,.92f,1));serialized.FindProperty("accent").colorValue=ColorValue(contract,"accent",Color.white);serialized.FindProperty("styleMode").floatValue=style.Mode;serialized.FindProperty("styleToken").stringValue=style.Token;ConfigureStyle(serialized,contract);var glow=style.DefaultGlow;Newtonsoft.Json.Linq.JToken token;if(contract.Parameters.TryGetValue("glow_strength",out token))glow=(float)token;serialized.FindProperty("intensity").floatValue=glow;serialized.FindProperty("seed").longValue=recipe.RandomSeed;ConfigureProtocol(serialized,recipe);ConfigureContent(serialized,recipe);ConfigureBehavior(serialized,recipe);serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static Mesh[] MeshesFor(RecipeArchetype archetype){if(archetype==RecipeArchetype.Destruction)return Enumerable.Repeat(AssetDatabase.LoadAssetAtPath<Mesh>(VfxStyleSharedLibrary.ShardPath),10).ToArray();if(archetype==RecipeArchetype.Projectile||archetype==RecipeArchetype.Trail||archetype==RecipeArchetype.WeaponTrail)return Load(VfxStyleSharedLibrary.ShardPath,VfxStyleSharedLibrary.RibbonPath,VfxStyleSharedLibrary.BurstPath);if(archetype==RecipeArchetype.Impact)return Load(VfxStyleSharedLibrary.BurstPath,VfxStyleSharedLibrary.RingPath,VfxStyleSharedLibrary.ShardPath);if(archetype==RecipeArchetype.Slash)return Load(VfxStyleSharedLibrary.RibbonPath,VfxStyleSharedLibrary.RibbonPath,VfxStyleSharedLibrary.BurstPath);if(archetype==RecipeArchetype.Beam)return Load(VfxStyleSharedLibrary.QuadPath,VfxStyleSharedLibrary.BurstPath);if(archetype==RecipeArchetype.Decal)return Load(VfxStyleSharedLibrary.RingPath,VfxStyleSharedLibrary.QuadPath,VfxStyleSharedLibrary.BurstPath);if(archetype==RecipeArchetype.Area||archetype==RecipeArchetype.Aura||archetype==RecipeArchetype.Shield||archetype==RecipeArchetype.Portal)return Load(VfxStyleSharedLibrary.RingPath,VfxStyleSharedLibrary.QuadPath,VfxStyleSharedLibrary.BurstPath);if(archetype==RecipeArchetype.Spawn||archetype==RecipeArchetype.Transform||archetype==RecipeArchetype.Composite||archetype==RecipeArchetype.LifeCycle||archetype==RecipeArchetype.Loot)return Load(VfxStyleSharedLibrary.RingPath,VfxStyleSharedLibrary.ConePath,VfxStyleSharedLibrary.BurstPath);return Load(VfxStyleSharedLibrary.QuadPath,VfxStyleSharedLibrary.RibbonPath,VfxStyleSharedLibrary.BurstPath);}
        private static Mesh[] Load(params string[] paths){return paths.Select(AssetDatabase.LoadAssetAtPath<Mesh>).ToArray();}
        private static Vector3 LayerScale(RecipeArchetype a,int i){if(a==RecipeArchetype.Beam)return new Vector3(1.1f,.16f,1)*(1-i*.18f);if(a==RecipeArchetype.WeaponTrail)return new Vector3(1.2f,.18f,1)*(1-i*.16f);if(a==RecipeArchetype.Destruction)return Vector3.one*(.18f+(i%3)*.045f);if(a==RecipeArchetype.Loot)return new Vector3(i==1?.24f:.78f,i==1?1.8f:.78f,.78f);if(a==RecipeArchetype.Projectile||a==RecipeArchetype.Trail)return Vector3.one*(i==0?.28f:i==1?.62f:.34f);return Vector3.one*(1f-i*.2f);}
        private static Vector3 LayerPosition(RecipeArchetype a,int i){if(a==RecipeArchetype.Destruction)return new Vector3(((i%5)-2)*.06f,(i/5)*.08f,(i%2)*.03f);if(a==RecipeArchetype.Loot)return new Vector3(0,i==1?.72f:0,.02f*i);if(a==RecipeArchetype.Projectile||a==RecipeArchetype.Trail)return new Vector3(-.18f*i,0,.02f*i);return new Vector3(0,0,.02f*i);}
        private static Quaternion LayerRotation(RecipeArchetype a,int i){if(a==RecipeArchetype.Destruction)return Quaternion.Euler(i*17,i*31,i*47);return a==RecipeArchetype.Area||a==RecipeArchetype.Spawn||a==RecipeArchetype.Decal?Quaternion.Euler(65,0,i*17):Quaternion.Euler(0,0,i*23);}
        private static StyledVfxProfile Profile(RecipeArchetype value){switch(value){case RecipeArchetype.Projectile:return StyledVfxProfile.Projectile;case RecipeArchetype.Impact:return StyledVfxProfile.Impact;case RecipeArchetype.Slash:return StyledVfxProfile.Slash;case RecipeArchetype.Aura:return StyledVfxProfile.Aura;case RecipeArchetype.Area:return StyledVfxProfile.Area;case RecipeArchetype.Beam:return StyledVfxProfile.Beam;case RecipeArchetype.Trail:return StyledVfxProfile.Trail;case RecipeArchetype.Shield:return StyledVfxProfile.Shield;case RecipeArchetype.Spawn:return StyledVfxProfile.Spawn;case RecipeArchetype.Transform:return StyledVfxProfile.Transform;case RecipeArchetype.Environment:return StyledVfxProfile.Environment;case RecipeArchetype.ScreenUi:return StyledVfxProfile.ScreenUi;case RecipeArchetype.Decal:return StyledVfxProfile.Decal;case RecipeArchetype.WeaponTrail:return StyledVfxProfile.WeaponTrail;case RecipeArchetype.Destruction:return StyledVfxProfile.Destruction;case RecipeArchetype.LifeCycle:return StyledVfxProfile.DeathRebirth;case RecipeArchetype.Portal:return StyledVfxProfile.Teleport;case RecipeArchetype.Loot:return StyledVfxProfile.Loot;default:return StyledVfxProfile.Composite;}}
        private static StyledVfxLifecycle Lifecycle(Recipe recipe){if(recipe.Archetype==RecipeArchetype.WeaponTrail||recipe.Archetype==RecipeArchetype.Portal)return StyledVfxLifecycle.Sustained;if(recipe.Archetype==RecipeArchetype.Loot)return StyledVfxLifecycle.EventDriven;var timing=recipe.Behavior==null?null:recipe.Behavior.Timing;if(timing!=null&&(timing.Type=="sustained"||timing.Type=="channel_interrupt"||timing.Type=="tick_pulse"))return StyledVfxLifecycle.Sustained;return StyledVfxLifecycle.OneShot;}
        private static Color ColorValue(RecipeStyleContract style,string key,Color fallback){string value;if(style!=null&&style.Palette.TryGetValue(key,out value)){Color parsed;if(ColorUtility.TryParseHtmlString(value,out parsed))return parsed;}return fallback;}
        private static void SetObjects(SerializedProperty property,UnityEngine.Object[] values){property.arraySize=values.Length;for(var i=0;i<values.Length;i++)property.GetArrayElementAtIndex(i).objectReferenceValue=values[i];}
        private static string ReadManifestBuildHash(string id){var path=VfxProjectRules.ManifestAbsolutePath(id);if(!File.Exists(path))return null;try{return(string)Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path))["buildHash"];}catch{return null;}}
        private static string ArchetypeToken(RecipeArchetype value){if(value==RecipeArchetype.ScreenUi)return "screen_ui";if(value==RecipeArchetype.WeaponTrail)return "weapon_trail";if(value==RecipeArchetype.LifeCycle)return "lifecycle";return value.ToString().ToLowerInvariant();}
        private static float Duration(Recipe recipe){var value=(float)Math.Max(.1,recipe.Stages.Where(item=>item.Enabled).Sum(item=>item.Duration));Newtonsoft.Json.Linq.JToken token;if(recipe.ArchetypeParameters.TryGetValue(recipe.Archetype==RecipeArchetype.Decal?"lifetime":recipe.Archetype==RecipeArchetype.Destruction?"debris_lifetime":recipe.Archetype==RecipeArchetype.LifeCycle?"duration":"",out token))value=Mathf.Max(.1f,(float)token);return value;}
        private static void ConfigureProtocol(SerializedObject serialized,Recipe recipe){SetInt(serialized,"stackLimit",recipe,"stack_limit");SetInt(serialized,"historyPoints",recipe,"history_points");SetInt(serialized,"pieceCount",recipe,"piece_count");SetInt(serialized,"rarity",recipe,"rarity");SetFloat(serialized,"speedThreshold",recipe,"speed_threshold");SetFloat(serialized,"fadeTime",recipe,"fade_time");SetFloat(serialized,"explodeForce",recipe,"explode_force");SetFloat(serialized,"debrisLifetime",recipe,"debris_lifetime");SetFloat(serialized,"portalRadius",recipe,"portal_radius");SetFloat(serialized,"swirlSpeed",recipe,"swirl_speed");SetFloat(serialized,"pickupSpeed",recipe,"pickup_speed");SetString(serialized,"pairId",recipe,"pair_id");SetString(serialized,"lifecycleDirection",recipe,"direction");}
        private static void ConfigureContent(SerializedObject serialized,Recipe recipe){var content=recipe.Content;serialized.FindProperty("contentFamily").enumValueIndex=(int)ContentFamily(content==null?null:content.Family);var numbers=content==null?new KeyValuePair<string,Newtonsoft.Json.Linq.JToken>[0]:content.Parameters.Where(v=>v.Value.Type==Newtonsoft.Json.Linq.JTokenType.Integer||v.Value.Type==Newtonsoft.Json.Linq.JTokenType.Float||v.Value.Type==Newtonsoft.Json.Linq.JTokenType.Boolean).OrderBy(v=>v.Key,StringComparer.Ordinal).ToArray();var texts=content==null?new KeyValuePair<string,Newtonsoft.Json.Linq.JToken>[0]:content.Parameters.Where(v=>v.Value.Type==Newtonsoft.Json.Linq.JTokenType.String).OrderBy(v=>v.Key,StringComparer.Ordinal).ToArray();SetPairs(serialized,"contentKeys","contentValues",numbers);SetTextPairs(serialized,"contentTextKeys","contentTextValues",texts);}
        private static void ConfigureStyle(SerializedObject serialized,RecipeStyleContract contract){var numbers=contract.Parameters.Where(v=>v.Value.Type==Newtonsoft.Json.Linq.JTokenType.Integer||v.Value.Type==Newtonsoft.Json.Linq.JTokenType.Float||v.Value.Type==Newtonsoft.Json.Linq.JTokenType.Boolean).OrderBy(v=>v.Key,StringComparer.Ordinal).ToArray();var texts=contract.Parameters.Where(v=>v.Value.Type==Newtonsoft.Json.Linq.JTokenType.String).OrderBy(v=>v.Key,StringComparer.Ordinal).ToArray();SetPairs(serialized,"styleKeys","styleValues",numbers);SetTextPairs(serialized,"styleTextKeys","styleTextValues",texts);}
        private static void ConfigureBehavior(SerializedObject serialized,Recipe recipe){var behavior=recipe.Behavior;serialized.FindProperty("behaviorEnabled").boolValue=behavior!=null;SetText(serialized,"motionType",behavior==null||behavior.Motion==null?"stationary":behavior.Motion.Type);SetText(serialized,"hitType",behavior==null||behavior.Hit==null?"single":behavior.Hit.Type);SetText(serialized,"emissionType",behavior==null||behavior.Emission==null?"single":behavior.Emission.Type);SetText(serialized,"timingType",behavior==null||behavior.Timing==null?"instant":behavior.Timing.Type);SetBlock(serialized,"motion",behavior==null?null:behavior.Motion);SetBlock(serialized,"hit",behavior==null?null:behavior.Hit);SetBlock(serialized,"emission",behavior==null?null:behavior.Emission);SetBlock(serialized,"timing",behavior==null?null:behavior.Timing);}
        private static void SetBlock(SerializedObject serialized,string prefix,RecipeCapabilityBlock block){var values=block==null?new KeyValuePair<string,Newtonsoft.Json.Linq.JToken>[0]:block.Parameters.Where(v=>v.Value.Type==Newtonsoft.Json.Linq.JTokenType.Integer||v.Value.Type==Newtonsoft.Json.Linq.JTokenType.Float||v.Value.Type==Newtonsoft.Json.Linq.JTokenType.Boolean).OrderBy(v=>v.Key,StringComparer.Ordinal).ToArray();SetPairs(serialized,prefix+"Keys",prefix+"Values",values);}
        private static void SetPairs(SerializedObject serialized,string keysName,string valuesName,KeyValuePair<string,Newtonsoft.Json.Linq.JToken>[] values){var keys=serialized.FindProperty(keysName);var numbers=serialized.FindProperty(valuesName);keys.arraySize=values.Length;numbers.arraySize=values.Length;for(var i=0;i<values.Length;i++){keys.GetArrayElementAtIndex(i).stringValue=values[i].Key;numbers.GetArrayElementAtIndex(i).floatValue=values[i].Value.Type==Newtonsoft.Json.Linq.JTokenType.Boolean?((bool)values[i].Value?1f:0f):(float)values[i].Value;}}
        private static void SetTextPairs(SerializedObject serialized,string keysName,string valuesName,KeyValuePair<string,Newtonsoft.Json.Linq.JToken>[] values){var keys=serialized.FindProperty(keysName);var texts=serialized.FindProperty(valuesName);keys.arraySize=values.Length;texts.arraySize=values.Length;for(var i=0;i<values.Length;i++){keys.GetArrayElementAtIndex(i).stringValue=values[i].Key;texts.GetArrayElementAtIndex(i).stringValue=(string)values[i].Value;}}
        private static void SetText(SerializedObject serialized,string field,string value){serialized.FindProperty(field).stringValue=value??string.Empty;}
        private static ElementContentFamily ContentFamily(string value){switch(value){case"fire":return ElementContentFamily.Fire;case"frost":return ElementContentFamily.Frost;case"lightning":return ElementContentFamily.Lightning;case"water":return ElementContentFamily.Water;case"wind":return ElementContentFamily.Wind;case"earth":return ElementContentFamily.Earth;case"nature":return ElementContentFamily.Nature;case"toxic":return ElementContentFamily.Toxic;case"holy":return ElementContentFamily.Holy;case"shadow":return ElementContentFamily.Shadow;case"arcane":return ElementContentFamily.Arcane;default:return ElementContentFamily.Neutral;}}
        private static void SetInt(SerializedObject serialized,string field,Recipe recipe,string key){Newtonsoft.Json.Linq.JToken token;if(recipe.ArchetypeParameters.TryGetValue(key,out token))serialized.FindProperty(field).intValue=(int)token;}
        private static void SetFloat(SerializedObject serialized,string field,Recipe recipe,string key){Newtonsoft.Json.Linq.JToken token;if(recipe.ArchetypeParameters.TryGetValue(key,out token))serialized.FindProperty(field).floatValue=(float)token;}
        private static void SetString(SerializedObject serialized,string field,Recipe recipe,string key){Newtonsoft.Json.Linq.JToken token;if(recipe.ArchetypeParameters.TryGetValue(key,out token))serialized.FindProperty(field).stringValue=(string)token;}
        private static bool IsW15(RecipeArchetype value){return value==RecipeArchetype.Decal||value==RecipeArchetype.WeaponTrail||value==RecipeArchetype.Destruction||value==RecipeArchetype.LifeCycle||value==RecipeArchetype.Portal||value==RecipeArchetype.Loot;}
        private static void AddParticles(GameObject root,Recipe recipe,Material material,List<Renderer> renderers,List<ParticleSystem> particles){var go=new GameObject("SecondaryParticles");go.transform.SetParent(root.transform,false);var particle=go.AddComponent<ParticleSystem>();var main=particle.main;main.playOnAwake=false;main.loop=Lifecycle(recipe)!=StyledVfxLifecycle.OneShot;main.duration=Mathf.Max(.1f,Duration(recipe));main.startLifetime=recipe.Archetype==RecipeArchetype.Decal?.45f:.7f;main.startSpeed=recipe.Archetype==RecipeArchetype.Destruction?1.5f:.35f;main.startSize=new ParticleSystem.MinMaxCurve(.04f,.12f);main.maxParticles=recipe.Archetype==RecipeArchetype.Destruction?24:16;var emission=particle.emission;emission.rateOverTime=main.loop?8:0;if(!main.loop)emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)(recipe.Archetype==RecipeArchetype.Destruction?18:8))});var shape=particle.shape;shape.shapeType=ParticleSystemShapeType.Circle;shape.radius=.45f;var color=particle.colorOverLifetime;color.enabled=true;color.color=new ParticleSystem.MinMaxGradient(new Gradient{colorKeys=new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},alphaKeys=new[]{new GradientAlphaKey(.9f,0),new GradientAlphaKey(0,1)}});var size=particle.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,1),new Keyframe(1,0)));var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=AssetDatabase.LoadAssetAtPath<Mesh>(VfxStyleSharedLibrary.ShardPath);renderer.sharedMaterial=material;renderer.enabled=false;renderers.Add(renderer);particles.Add(particle);}
        private static string Hash(string value){using(var sha=SHA256.Create())return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(v=>v.ToString("x2",CultureInfo.InvariantCulture)));}
        private static string Absolute(string assetPath){return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,assetPath.Replace('/',Path.DirectorySeparatorChar)));}
    }
}
