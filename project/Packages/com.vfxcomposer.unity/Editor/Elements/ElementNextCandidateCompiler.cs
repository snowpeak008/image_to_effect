using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VFXComposer.Editor.Domain;

namespace VFXComposer.Editor.Elements
{
    public sealed class ElementNextCandidateBuildResult
    {
        public bool Succeeded;
        public bool Unchanged;
        public string PrefabPath;
        public string ManifestPath;
        public string BuildHash;
        public ElementNextCandidatePlan Plan;
        public readonly ValidationReport Report = new ValidationReport();
    }

    /// <summary>
    /// Independent W3-W8 compiler.  W3-W5 and W6-W8 retain separate candidate roots and compiler
    /// versions while sharing bounded execution plumbing and dedicated per-family semantics.
    /// </summary>
    public static class ElementNextCandidateCompiler
    {
        public const string CandidateRoot = "Assets/VFX/NextCandidates/W3W5Elements";
        public const string GeneratedRoot = CandidateRoot + "/Generated";
        public const string CandidateRootW6W8 = "Assets/VFX/NextCandidates/W6W8Elements";
        public const string GeneratedRootW6W8 = CandidateRootW6W8 + "/Generated";
        public const string SharedRoot = "Assets/VFX/Shared/ElementNextCandidate";
        public const string MaterialRoot = SharedRoot + "/Materials";
        public const string MeshRoot = SharedRoot + "/Meshes";
        public const string ShaderPath = "Assets/VFX/Shared/Shaders/ElementNextCandidateLayeredUnlit.shader";
        public const string BodyMaterialPath = MaterialRoot + "/MAT_ElementNext_BodyAlpha.mat";
        public const string AtmosphereMaterialPath = MaterialRoot + "/MAT_ElementNext_AtmosphereAlpha.mat";
        public const string HighlightMaterialPath = MaterialRoot + "/MAT_ElementNext_HighlightAdditive.mat";
        public const string CandidateManifestFile = "element-next-candidate.json";

        public static ElementNextCandidatePlanResult PlanAsset(string recipePath)
        {
            var absolute = Absolute(recipePath);
            if (File.Exists(absolute)) return ElementNextCandidatePlanCompiler.PlanJson(recipePath, File.ReadAllText(absolute));
            var result = new ElementNextCandidatePlanResult(); result.Report.Add("E1935", ValidationSeverity.Error, "/recipe", "Element next-candidate Recipe is missing.", new JValue(recipePath)); return result;
        }

        public static ElementNextCandidateBuildResult BuildAsset(string recipePath)
        {
            var absolute = Absolute(recipePath);
            if (!File.Exists(absolute)) { var missing = new ElementNextCandidateBuildResult(); missing.Report.Add("E1935", ValidationSeverity.Error, "/recipe", "Element next-candidate Recipe is missing.", new JValue(recipePath)); return missing; }
            return BuildCore(recipePath, File.ReadAllText(absolute));
        }

        /// <summary>Patch transaction hook for future isolated callers; the source Recipe is never rewritten here.</summary>
        public static ElementNextCandidateBuildResult BuildJsonForTransaction(string recipePath, string patchedRecipeJson) { return BuildCore(recipePath, patchedRecipeJson); }

        public static string CandidateRootFor(string effectId)
        {
            ElementNextCandidateProfile profile; return ElementNextCandidatePlanCompiler.TryProfile(effectId,out profile) && profile>ElementNextCandidateProfile.VoltShield ? CandidateRootW6W8 : CandidateRoot;
        }
        public static string PrefabPath(string effectId) { return OutputFolder(effectId) + "/VFX_" + effectId + "_NEXT.prefab"; }
        public static string ManifestPath(string effectId) { return OutputFolder(effectId) + "/" + CandidateManifestFile; }
        public static string OutputFolder(string effectId) { return (CandidateRootFor(effectId)==CandidateRootW6W8?GeneratedRootW6W8:GeneratedRoot) + "/" + effectId; }

        private static ElementNextCandidateBuildResult BuildCore(string recipePath, string recipeJson)
        {
            var result = new ElementNextCandidateBuildResult();
            var planned = ElementNextCandidatePlanCompiler.PlanJson(recipePath, recipeJson); result.Report.AddRange(planned.Report);
            if (!planned.Succeeded) return result;
            EnsureSharedAssets();
            var plan = planned.Plan;
            plan.BuildHash = Hash(plan.BuildHash + "|" + DependencySignature() + "|" + Application.unityVersion);
            result.Plan = plan; result.BuildHash = plan.BuildHash; result.PrefabPath = PrefabPath(plan.EffectId); result.ManifestPath = ManifestPath(plan.EffectId);
            if (ReadManifestHash(result.ManifestPath) == plan.BuildHash && AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath) != null)
            {
                result.Succeeded = true; result.Unchanged = true; return result;
            }

            var folder = OutputFolder(plan.EffectId); EnsureFolder(folder);
            var roleMeshes = ElementNextCandidateMeshFactory.EnsureRoleMeshes(plan, folder);
            var detailMeshPath = ElementNextCandidateMeshFactory.EnsureDetailMesh(MeshRoot);
            AssetDatabase.SaveAssets();
            var root = BuildRuntime(plan, roleMeshes, detailMeshPath);
            try
            {
                if (PrefabUtility.SaveAsPrefabAsset(root, result.PrefabPath) == null) throw new InvalidOperationException("Could not save next-candidate Prefab: " + result.PrefabPath);
            }
            catch (Exception exception)
            {
                result.Report.Add("E1936", ValidationSeverity.Error, "/build", exception.Message); return result;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath);
            ValidatePrefab(prefab, plan, result.Report);
            if (result.Report.HasErrors) return result;
            WriteCandidateManifest(plan, result.PrefabPath, result.ManifestPath, roleMeshes, detailMeshPath);
            AssetDatabase.ImportAsset(result.ManifestPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            result.Succeeded = true;
            return result;
        }

        private static GameObject BuildRuntime(ElementNextCandidatePlan plan, string[] roleMeshPaths, string detailMeshPath)
        {
            var root = new GameObject("VFX_" + plan.EffectId + "_NEXT");
            var owned = new List<Renderer>();
            var roleTransforms = new Transform[5]; var roleRenderers = new Renderer[5];
            var body = AssetDatabase.LoadAssetAtPath<Material>(BodyMaterialPath); var atmosphere = AssetDatabase.LoadAssetAtPath<Material>(AtmosphereMaterialPath); var highlight = AssetDatabase.LoadAssetAtPath<Material>(HighlightMaterialPath);
            if (plan.Family == ElementNextCandidateFamily.Lightning)
            {
                CreateMeshRole(root.transform, "PrimaryLightningCarrier", roleMeshPaths[0], body, 20, out roleTransforms[0], out roleRenderers[0]); owned.Add(roleRenderers[0]);
            }
            else
            {
                var names = new[] { "PrimaryCarrier", "InternalHighlightCarrier", "OuterEnergyCarrier", "ResidualCarrier", "EventCarrier" };
                var materials = new[] { body, highlight, atmosphere, body, highlight };
                for (var role = 0; role < 5; role++)
                {
                    if (!RoleIsUsed(plan.Profile, role)) continue;
                    CreateMeshRole(root.transform,names[role],roleMeshPaths[role],materials[role],20+role,out roleTransforms[role],out roleRenderers[role]); owned.Add(roleRenderers[role]);
                }
            }

            var lineCount = plan.ArcCarrierCount;
            var arcs = new LineRenderer[lineCount];
            for (var index = 0; index < lineCount; index++)
            {
                var lineObject = new GameObject((plan.Family == ElementNextCandidateFamily.Lightning ? "DiscreteArcCarrier_" : "ContourCarrier_") + (index + 1).ToString("00",CultureInfo.InvariantCulture)); lineObject.transform.SetParent(root.transform,false);
                var line = lineObject.AddComponent<LineRenderer>(); line.useWorldSpace=false; line.alignment=LineAlignment.View; line.positionCount=0; line.numCapVertices=0; line.numCornerVertices=0; line.textureMode=LineTextureMode.Stretch; line.sharedMaterial=highlight; line.enabled=false; arcs[index]=line; owned.Add(line);
            }

            var particleObject = new GameObject("DeterministicDetailBatch"); particleObject.transform.SetParent(root.transform,false); var particles = particleObject.AddComponent<ParticleSystem>();
            var main = particles.main; main.playOnAwake=false; main.loop=false; main.simulationSpace=ParticleSystemSimulationSpace.Local; main.maxParticles=plan.ParticleBudget; main.startLifetime=1f; main.startSpeed=0f; main.startSize=.06f;
            var emission = particles.emission; emission.enabled=false; var shape = particles.shape; shape.enabled=false;
            var particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>(); particleRenderer.renderMode=ParticleSystemRenderMode.Mesh; particleRenderer.mesh=AssetDatabase.LoadAssetAtPath<Mesh>(detailMeshPath); particleRenderer.sharedMaterial=highlight; particleRenderer.enabled=false; owned.Add(particleRenderer);
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);

            var executor = root.AddComponent<ElementNextCandidateVisualExecutor>(); var serialized = new SerializedObject(executor);
            serialized.FindProperty("effectId").stringValue=plan.EffectId; serialized.FindProperty("compilerVersion").stringValue=plan.CompilerVersion; serialized.FindProperty("visualStatus").stringValue=ElementNextCandidatePlanCompiler.VisualStatus; serialized.FindProperty("carrierShapeToken").stringValue=plan.ShapeToken; serialized.FindProperty("topologySignature").stringValue=plan.TopologySignature;
            serialized.FindProperty("family").enumValueIndex=(int)plan.Family; serialized.FindProperty("profile").enumValueIndex=(int)plan.Profile; serialized.FindProperty("lifecycle").enumValueIndex=(int)plan.Lifecycle; serialized.FindProperty("duration").floatValue=plan.Duration; serialized.FindProperty("seed").longValue=plan.Seed; serialized.FindProperty("primary").colorValue=plan.Primary; serialized.FindProperty("secondary").colorValue=plan.Secondary; serialized.FindProperty("accent").colorValue=plan.Accent;
            SetContent(serialized,plan); SetBindings(serialized,plan.Bindings);
            SetObject(serialized,"primaryCarrier",roleTransforms[0]); SetObject(serialized,"primaryRenderer",roleRenderers[0]); SetObject(serialized,"highlightCarrier",roleTransforms[1]); SetObject(serialized,"highlightRenderer",roleRenderers[1]); SetObject(serialized,"outerCarrier",roleTransforms[2]); SetObject(serialized,"outerRenderer",roleRenderers[2]); SetObject(serialized,"residualCarrier",roleTransforms[3]); SetObject(serialized,"residualRenderer",roleRenderers[3]); SetObject(serialized,"eventCarrier",roleTransforms[4]); SetObject(serialized,"eventRenderer",roleRenderers[4]);
            SetObjects(serialized.FindProperty("arcCarriers"),arcs.Cast<UnityEngine.Object>().ToArray()); SetObject(serialized,"detailParticles",particles); SetObject(serialized,"detailParticleRenderer",particleRenderer); SetObjects(serialized.FindProperty("ownedRenderers"),owned.Cast<UnityEngine.Object>().ToArray());
            serialized.FindProperty("particleBudget").intValue=plan.ParticleBudget; serialized.FindProperty("rendererBudget").intValue=plan.RendererBudget; serialized.FindProperty("materialBudget").intValue=plan.MaterialBudget; serialized.FindProperty("particleSystemBudget").intValue=plan.ParticleSystemBudget; serialized.FindProperty("maxLocalExtent").floatValue=plan.MaxLocalExtent;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static void CreateMeshRole(Transform parent, string name, string meshPath, Material material, int sortingOrder, out Transform roleTransform, out Renderer roleRenderer)
        {
            var item = new GameObject(name); item.transform.SetParent(parent,false); item.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath); var renderer=item.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material; renderer.sortingOrder=sortingOrder; renderer.enabled=false; roleTransform=item.transform; roleRenderer=renderer;
        }

        private static bool RoleIsUsed(ElementNextCandidateProfile profile, int role)
        {
            if (role == 0) return true;
            if(profile>ElementNextCandidateProfile.VoltShield)
            {
                switch(profile)
                {
                    case ElementNextCandidateProfile.WaterJet: return role==1||role==2||role==3;
                    case ElementNextCandidateProfile.TidalWave: return role<=4;
                    case ElementNextCandidateProfile.BubbleShield: return role==1||role==2||role==4;
                    case ElementNextCandidateProfile.SplashImpact: return role==1||role==2||role==3;
                    case ElementNextCandidateProfile.Whirlpool: return role==1||role==2||role==3;
                    case ElementNextCandidateProfile.Tornado: return role==2||role==3;
                    case ElementNextCandidateProfile.WindBlade: return role==2;
                    case ElementNextCandidateProfile.GaleDash: return role==3;
                    case ElementNextCandidateProfile.EarthSpike: return role==1||role==2||role==3;
                    case ElementNextCandidateProfile.Boulder: return role==3||role==4;
                    case ElementNextCandidateProfile.QuakeStomp: return false;
                    case ElementNextCandidateProfile.ThornSnare: return role==4;
                    case ElementNextCandidateProfile.VineWhip: return role==1||role==2||role==3;
                    case ElementNextCandidateProfile.HealingBloom: return role==1||role==2||role==3;
                    case ElementNextCandidateProfile.SporeBurst: return role==1||role==2||role==3;
                    case ElementNextCandidateProfile.AcidLob: return role==2||role==3||role==4;
                    case ElementNextCandidateProfile.DivineSmite: return role==1||role==2||role==3;
                    case ElementNextCandidateProfile.HolyHalo: return role==1;
                    case ElementNextCandidateProfile.Resurrection: return role<=4;
                    case ElementNextCandidateProfile.ShadowClaw: return role==2;
                    case ElementNextCandidateProfile.VoidOrb: return role<=4;
                    case ElementNextCandidateProfile.ShadowGrasp: return role==2||role==4;
                    case ElementNextCandidateProfile.CurseMark: return role==1||role==2||role==3;
                    case ElementNextCandidateProfile.ArcaneMissile: return false;
                    default: return role==1||role==2||role==3;
                }
            }
            switch (profile)
            {
                case ElementNextCandidateProfile.FlameSlash:
                case ElementNextCandidateProfile.FireNova:
                    return role <= 3;
                case ElementNextCandidateProfile.Flamethrower:
                case ElementNextCandidateProfile.BurningStatus:
                case ElementNextCandidateProfile.PhoenixDart:
                    return role == 1 || role == 2 || role == 3;
                case ElementNextCandidateProfile.IceSpike:
                case ElementNextCandidateProfile.FrostBreath:
                case ElementNextCandidateProfile.IceShard:
                case ElementNextCandidateProfile.FreezeStatus:
                case ElementNextCandidateProfile.FlashFreeze:
                    return role == 1 || role == 2;
                case ElementNextCandidateProfile.EmberRain:
                    return role == 2 || role == 3 || role == 4;
                case ElementNextCandidateProfile.ChainBlast:
                    return role == 1 || role == 3;
                case ElementNextCandidateProfile.FireShield:
                    return role == 1 || role == 2 || role == 3 || role == 4;
                case ElementNextCandidateProfile.CrystalShield:
                    return role == 1 || role == 2 || role == 4;
                case ElementNextCandidateProfile.Blizzard:
                    return role == 2;
                default:
                    return false;
            }
        }

        private static void SetContent(SerializedObject serialized, ElementNextCandidatePlan plan)
        {
            var numbers=plan.Parameters.Where(pair=>pair.Value.Type==JTokenType.Integer||pair.Value.Type==JTokenType.Float||pair.Value.Type==JTokenType.Boolean).OrderBy(pair=>pair.Key,StringComparer.Ordinal).ToArray(); var texts=plan.Parameters.Where(pair=>pair.Value.Type==JTokenType.String).OrderBy(pair=>pair.Key,StringComparer.Ordinal).ToArray();
            var keys=serialized.FindProperty("contentKeys"); var values=serialized.FindProperty("contentValues"); keys.arraySize=numbers.Length; values.arraySize=numbers.Length;
            for(var index=0;index<numbers.Length;index++){keys.GetArrayElementAtIndex(index).stringValue=numbers[index].Key; values.GetArrayElementAtIndex(index).floatValue=numbers[index].Value.Type==JTokenType.Boolean?((bool)numbers[index].Value?1f:0f):Convert.ToSingle(((JValue)numbers[index].Value).Value,CultureInfo.InvariantCulture);}
            var textKeys=serialized.FindProperty("contentTextKeys"); var textValues=serialized.FindProperty("contentTextValues"); textKeys.arraySize=texts.Length; textValues.arraySize=texts.Length;
            for(var index=0;index<texts.Length;index++){textKeys.GetArrayElementAtIndex(index).stringValue=texts[index].Key; textValues.GetArrayElementAtIndex(index).stringValue=(string)texts[index].Value;}
        }

        private static void SetBindings(SerializedObject serialized, IList<ElementNextBindingPlan> bindings)
        {
            var property=serialized.FindProperty("parameterBindings"); property.arraySize=bindings.Count;
            for(var index=0;index<bindings.Count;index++){var item=property.GetArrayElementAtIndex(index);item.FindPropertyRelative("parameter").stringValue=bindings[index].Parameter;item.FindPropertyRelative("carrier").stringValue=bindings[index].Carrier;item.FindPropertyRelative("authoredValue").floatValue=bindings[index].AuthoredValue;}
        }

        private static void ValidatePrefab(GameObject prefab, ElementNextCandidatePlan plan, ValidationReport report)
        {
            if(prefab==null){report.Add("E1937",ValidationSeverity.Error,"/build/prefab","Next-candidate Prefab is missing after save.");return;}
            var entries=prefab.GetComponents<MonoBehaviour>().Count(value=>value is IVfxRuntimeEntry);if(entries!=1)report.Add("E1938",ValidationSeverity.Error,"/build/runtimeEntry","Next-candidate Prefab must own exactly one IVfxRuntimeEntry.",new JValue(entries));
            var executor=prefab.GetComponent<ElementNextCandidateVisualExecutor>();if(executor==null)report.Add("E1939",ValidationSeverity.Error,"/build/runtimeEntry","Dedicated element visual executor is missing.");
            var renderers=prefab.GetComponentsInChildren<Renderer>(true);if(renderers.Length>plan.RendererBudget)report.Add("E1940",ValidationSeverity.Error,"/budget/renderers","Renderer budget exceeded.",new JValue(renderers.Length),"<= "+plan.RendererBudget);
            if(prefab.GetComponentsInChildren<ParticleSystem>(true).Length!=1)report.Add("E1941",ValidationSeverity.Error,"/budget/particleSystems","Candidate must use one pooled deterministic detail batch.");
            if(prefab.GetComponentsInChildren<Rigidbody>(true).Length!=0)report.Add("E1942",ValidationSeverity.Error,"/build/physics","Element visual execution must remain deterministic and Rigidbody-free.");
            if(executor!=null&&!executor.BudgetWithinLimits)report.Add("E1943",ValidationSeverity.Error,"/budget","Runtime budget readback failed.");
        }

        private static void EnsureSharedAssets()
        {
            EnsureFolder(MaterialRoot); EnsureFolder(MeshRoot); AssetDatabase.ImportAsset(ShaderPath,ImportAssetOptions.ForceSynchronousImport); var shader=Shader.Find("VFXComposer/ElementNextCandidate/LayeredUnlit");if(shader==null)throw new InvalidOperationException("Missing dedicated element next-candidate shader: "+ShaderPath);
            EnsureMaterial(BodyMaterialPath,shader,BlendMode.SrcAlpha,BlendMode.OneMinusSrcAlpha,3000,1f); EnsureMaterial(AtmosphereMaterialPath,shader,BlendMode.SrcAlpha,BlendMode.OneMinusSrcAlpha,3010,.62f); EnsureMaterial(HighlightMaterialPath,shader,BlendMode.SrcAlpha,BlendMode.One,3020,1.35f); AssetDatabase.SaveAssets();
        }

        private static void EnsureMaterial(string path,Shader shader,BlendMode source,BlendMode destination,int queue,float intensity)
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null){material=new Material(shader){name=Path.GetFileNameWithoutExtension(path)};AssetDatabase.CreateAsset(material,path);}material.shader=shader;material.SetFloat("_SrcBlend",(float)source);material.SetFloat("_DstBlend",(float)destination);material.SetFloat("_Intensity",intensity);material.SetFloat("_GlobalAlpha",1f);material.renderQueue=queue;material.SetOverrideTag("RenderType","Transparent");EditorUtility.SetDirty(material);
        }

        private static string DependencySignature()
        {
            var paths=new[]{ShaderPath,BodyMaterialPath,AtmosphereMaterialPath,HighlightMaterialPath};return string.Join("|",paths.Select(path=>path+":"+AssetDatabase.GetAssetDependencyHash(path)).ToArray());
        }

        private static void WriteCandidateManifest(ElementNextCandidatePlan plan,string prefabPath,string manifestPath,string[] roleMeshes,string detailMeshPath)
        {
            var bindings=new JArray(plan.Bindings.OrderBy(item=>item.Parameter,StringComparer.Ordinal).Select(item=>new JObject{{"parameter",item.Parameter},{"carrier",item.Carrier},{"authoredValue",item.AuthoredValue}}));
            var semantics=Semantics(plan.Family);
            var owned=new JArray(new JObject{{"path",prefabPath},{"kind","runtime_entry"}});foreach(var mesh in roleMeshes)owned.Add(new JObject{{"path",mesh},{"kind","recipe_shaped_mesh"}});
            var cohort=plan.Profile<=ElementNextCandidateProfile.VoltShield?"w3-w5":"w6-w8";
            var root=new JObject{{"candidateVersion",1},{"candidateId",plan.EffectId+"@"+cohort+"-next"},{"sourceEffectId",plan.EffectId},{"compilerVersion",plan.CompilerVersion},{"visualStatus",ElementNextCandidatePlanCompiler.VisualStatus},{"userVisualVerdict",JValue.CreateNull()},{"oldRejectedCandidateModified",false},{"sourceRecipePath",plan.SourceRecipePath},{"sourceRecipeHash",plan.RecipeHash},{"buildHash",plan.BuildHash},{"prefabPath",prefabPath},{"runtimeEntry",new JObject{{"path",prefabPath},{"component",typeof(ElementNextCandidateVisualExecutor).FullName}}},{"carrierShape",plan.ShapeToken},{"topologySignature",plan.TopologySignature},{"semantics",semantics},{"parameterBindings",bindings},{"ownedOutputs",owned},{"dependencies",new JArray(new JObject{{"path",plan.SourceRecipePath},{"kind","recipe"}},new JObject{{"path",ShaderPath},{"kind","shader"}},new JObject{{"path",BodyMaterialPath},{"kind","alpha_body_material"}},new JObject{{"path",AtmosphereMaterialPath},{"kind","alpha_atmosphere_material"}},new JObject{{"path",HighlightMaterialPath},{"kind","additive_highlight_material"}},new JObject{{"path",detailMeshPath},{"kind","detail_mesh"}})},{"cost",new JObject{{"peakParticles",plan.ParticleBudget},{"particleSystems",1},{"transparentRenderers",plan.RendererBudget},{"materials",3},{"localTextureBytes",0}}},{"bounds",new JObject{{"maxLocalExtent",plan.MaxLocalExtent},{"previewMustScaleToCell",true}}},{"machineEvidenceIsVisualAcceptance",false}};
            WriteIfChanged(manifestPath,root.ToString(Formatting.Indented)+"\n");
        }

        private static JArray Semantics(ElementNextCandidateFamily family)
        {
            switch(family)
            {
                case ElementNextCandidateFamily.Fire:return new JArray("combustion","eruption","embers","heat_haze","residue");
                case ElementNextCandidateFamily.Frost:return new JArray("crystal_growth","frost_mist","fracture","melt","geometric_sharpness");
                case ElementNextCandidateFamily.Lightning:return new JArray("branching","discrete_flash","charge","discharge","impact_afterglow");
                case ElementNextCandidateFamily.Water:return new JArray("volume_flow","foam","splash","residue","stop_sag");
                case ElementNextCandidateFamily.Wind:return new JArray("low_opacity_medium","debris_readability","flow_lines","afterimages");
                case ElementNextCandidateFamily.Earth:return new JArray("weight","sequential_rise","overshoot","cracks","dust");
                case ElementNextCandidateFamily.Nature:return new JArray("reveal_growth","pulse","bloom","wither_retract");
                case ElementNextCandidateFamily.Toxic:return new JArray("viscous_swelling","double_pulse","linger_convergence","corrosion_pool");
                case ElementNextCandidateFamily.Holy:return new JArray("ordered_reveal","vertical_pillar","cross_halo","feather_afterglow");
                case ElementNextCandidateFamily.Shadow:return new JArray("negative_space","staggered_tears","inward_suction","implode","reveal_hands");
                default:return new JArray("deterministic_activation_order","counter_rotation","missile_stagger","wobble","reverse_shutdown");
            }
        }

        private static string ReadManifestHash(string assetPath){var absolute=Absolute(assetPath);if(!File.Exists(absolute))return null;try{return(string)JObject.Parse(File.ReadAllText(absolute))["buildHash"];}catch{return null;}}
        private static void WriteIfChanged(string assetPath,string value){var absolute=Absolute(assetPath);Directory.CreateDirectory(Path.GetDirectoryName(absolute));if(File.Exists(absolute)&&string.Equals(File.ReadAllText(absolute),value,StringComparison.Ordinal))return;File.WriteAllText(absolute,value,new UTF8Encoding(false));}
        private static void SetObject(SerializedObject serialized,string property,UnityEngine.Object value){serialized.FindProperty(property).objectReferenceValue=value;}
        private static void SetObjects(SerializedProperty property,UnityEngine.Object[] values){property.arraySize=values.Length;for(var index=0;index<values.Length;index++)property.GetArrayElementAtIndex(index).objectReferenceValue=values[index];}
        private static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;var parent=Path.GetDirectoryName(path).Replace('\\','/');if(!AssetDatabase.IsValidFolder(parent))EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
        private static string Absolute(string assetPath){return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,assetPath.Replace('/',Path.DirectorySeparatorChar)));}
        private static string Hash(string value){using(var sha=SHA256.Create())return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value??string.Empty)).Select(item=>item.ToString("x2",CultureInfo.InvariantCulture)).ToArray());}
    }
}
