using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Composite;
using VFXComposer.Editor.Catalog;
using VFXComposer.Editor.Capabilities;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Validation;
using VFXComposer.Editor.Style;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Independent;

namespace VFXComposer.Editor.Patch
{
    /// <summary>
    /// Applies the deliberately small S8 semantic Patch language. It never accepts JSON Pointer
    /// escapes or array addressing: every target is a stable Recipe stage/module identifier.
    /// </summary>
    public sealed class VfxPatchService
    {
        public const string HistorySuffix = ".history.json";
        private const string PatchParse = "E700";
        private const string PatchUnknownField = "E701";
        private const string PatchRequired = "E702";
        private const string PatchType = "E703";
        private const string PatchOperation = "E704";
        private const string PatchPath = "E705";
        private const string PatchTarget = "E706";
        private const string PatchRevision = "E707";
        private const string PatchRequiredModule = "E708";
        private const string PatchAdd = "E709";
        private const string PatchTransaction = "E710";
        private const string PatchRollback = "E711";
        private readonly Func<VfxCompiler> compilerFactory;
        private readonly IVfxPatchTransactionHook transactionHook;
        private readonly IVfxPatchSnapshotProvider snapshotProvider;

        public VfxPatchService() : this(null, null, null) { }

        internal VfxPatchService(Func<VfxCompiler> compilerFactory, IVfxPatchTransactionHook transactionHook, IVfxPatchSnapshotProvider snapshotProvider = null)
        {
            this.compilerFactory = compilerFactory ?? (() => new VfxCompiler());
            this.transactionHook = transactionHook;
            this.snapshotProvider = snapshotProvider ?? new DefaultSnapshotProvider();
        }

        public VfxPatchResult Validate(string recipeJson, string patchJson, int expectedRevision, TemplateCatalog catalog = null)
        {
            catalog = catalog ?? VfxCompiler.LoadFormalCatalog();
            var result = new VfxPatchResult();
            var current = VfxDomainParser.ParseRecipe(recipeJson);
            var dispatch = VFXComposer.Editor.SlashV2.S12RecipeDispatcher.Parse(recipeJson);
            if (dispatch.RecipeVersion == 2) result.Report.Add("E712", ValidationSeverity.Error, "/recipeVersion", "v1 VfxPatchService rejects Recipe v2; use S12SlashPatchService.");
            result.Report.AddRange(current.Report);
            result.Report.AddRange(catalog.Report);
            if (!result.Report.HasErrors) { result.Report.AddRange(RecipeValidator.ValidateSemantic(current.Value, catalog)); result.Report.AddRange(ArchetypeParameterRegistry.Validate(current.Value)); result.Report.AddRange(CapabilityRegistry.Validate(current.Value)); result.Report.AddRange(CapabilitySlotValidator.Validate(current.Value)); }
            if (result.Report.HasErrors) return result;

            result.BeforeRevision = current.Value.Revision;
            result.BeforeCanonicalHash = RecipeCanonicalizer.ComputeSha256(recipeJson);
            if (current.Value.Revision != expectedRevision)
            {
                result.Report.Add(PatchRevision, ValidationSeverity.Error, "/revision", "Patch expectedRevision does not match the current Recipe revision.", new JValue(expectedRevision), current.Value.Revision.ToString(CultureInfo.InvariantCulture));
                return result;
            }

            var operations = ParseOperations(patchJson, result);
            if (!result.IsValid) return result;
            JObject root;
            try { root = (JObject)JToken.Parse(recipeJson, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }); }
            catch (Exception exception) { result.Report.Add(PatchParse, ValidationSeverity.Error, "/", "Recipe cannot be cloned for Patch: " + exception.Message); return result; }
            var patched = (JObject)root.DeepClone();
            for (var index = 0; index < operations.Count; index++)
            {
                if (!ApplyOperation(patched, operations[index], result)) return result;
            }
            patched["revision"] = expectedRevision + 1;
            var patchedJson = patched.ToString(Formatting.Indented);
            var postPatchStart = result.Report.Entries.Count;
            var parsedPatched = VfxDomainParser.ParseRecipe(patchedJson);
            result.Report.AddRange(parsedPatched.Report);
            if (!result.Report.HasErrors) { result.Report.AddRange(RecipeValidator.ValidateSemantic(parsedPatched.Value, catalog)); result.Report.AddRange(ArchetypeParameterRegistry.Validate(parsedPatched.Value)); result.Report.AddRange(CapabilityRegistry.Validate(parsedPatched.Value)); result.Report.AddRange(CapabilitySlotValidator.Validate(parsedPatched.Value)); }
            if (!result.Report.HasErrors&&parsedPatched.Value.Archetype==RecipeArchetype.Composite) result.Report.AddRange(CompositeContentCompiler.ValidateJson(patchedJson));
            else if (!result.Report.HasErrors) result.Report.AddRange(BudgetCalculator.Evaluate(parsedPatched.Value, catalog));
            if (!result.Report.HasErrors&&parsedPatched.Value.Archetype!=RecipeArchetype.Composite) result.Report.AddRange(compilerFactory().DryRun(patchedJson, catalog).Report);
            if (result.Report.HasErrors)
            {
                result.IsPostPatchValidationFailure = true;
                result.FailedOperationIndex = AttributePostPatchFailure(result.Report.Entries.Skip(postPatchStart), operations);
                return result;
            }
            result.AfterRevision = expectedRevision + 1;
            result.PatchedRecipeJson = patchedJson;
            result.AfterCanonicalHash = RecipeCanonicalizer.ComputeSha256(patchedJson);
            AddImpact(root, patched, result.AffectedItems);
            return result;
        }

        /// <summary>Build first, then atomically replace Recipe and its history. A failed Build leaves both text files untouched.</summary>
        public VfxPatchResult ApplyToAsset(string recipeAssetPath, string patchJson, int expectedRevision, TemplateCatalog catalog = null)
        {
            var result = new VfxPatchResult();
            if (string.IsNullOrWhiteSpace(recipeAssetPath) || !recipeAssetPath.StartsWith("Assets/VFX/Recipes/", StringComparison.Ordinal) || recipeAssetPath.IndexOf("..", StringComparison.Ordinal) >= 0)
            {
                result.Report.Add(PatchPath, ValidationSeverity.Error, "/recipe", "Patch can only write a Recipe under Assets/VFX/Recipes/.", new JValue(recipeAssetPath));
                return result;
            }
            var absoluteRecipePath = AbsoluteAssetPath(recipeAssetPath);
            if (!File.Exists(absoluteRecipePath)) { result.Report.Add(PatchTarget, ValidationSeverity.Error, "/recipe", "Recipe asset does not exist.", new JValue(recipeAssetPath)); return result; }
            var beforeJson = File.ReadAllText(absoluteRecipePath);
            result = Validate(beforeJson, patchJson, expectedRevision, catalog);
            if (!result.IsValid) return result;
            var historyAssetPath = recipeAssetPath + HistorySuffix;
            var absoluteHistoryPath = AbsoluteAssetPath(historyAssetPath);
            var beforeHistory = File.Exists(absoluteHistoryPath) ? File.ReadAllText(absoluteHistoryPath) : null;
            if (!IsHistoryArray(beforeHistory))
            {
                result.Report.Add(PatchTransaction, ValidationSeverity.Error, "/history", "Existing Patch history must be a JSON array; no Build was started.");
                return result;
            }

            IVfxPatchGeneratedSnapshot generatedSnapshot;
            try { generatedSnapshot = snapshotProvider.Capture(VfxCompiler.OutputFolder(VfxDomainParser.ParseRecipe(beforeJson).Value)); }
            catch (Exception exception)
            {
                result.Report.Add(PatchTransaction, ValidationSeverity.Error, "/transaction/snapshot", "Patch Generated snapshot could not be created; no Build was started: " + exception.Message);
                return result;
            }
            try
            {
                var patchedRecipe=VfxDomainParser.ParseRecipe(result.PatchedRecipeJson).Value;
                VfxBuildResult build;
                if(UsesCompositeCompiler(patchedRecipe,recipeAssetPath))
                {
                    var composite=CompositeContentCompiler.BuildJsonForTransaction(recipeAssetPath,result.PatchedRecipeJson);
                    build=new VfxBuildResult{Succeeded=composite.Succeeded,PrefabPath=composite.PrefabPath,Plan=new VfxBuildPlan{Report=composite.Report,RecipeRevision=patchedRecipe.Revision,RecipeHash=composite.RecipeHash,BuildHash=composite.BuildHash}};
                }
                else if(UsesIndependentCompiler(patchedRecipe,recipeAssetPath))
                {
                    var independent=IndependentContentCompiler.BuildJsonForTransaction(recipeAssetPath,result.PatchedRecipeJson);
                    build=new VfxBuildResult{Succeeded=independent.Succeeded,PrefabPath=independent.PrefabPath,Plan=new VfxBuildPlan{Report=independent.Report,RecipeRevision=patchedRecipe.Revision,RecipeHash=independent.RecipeHash,BuildHash=independent.BuildHash}};
                }
                else if(UsesStyledCompiler(patchedRecipe,recipeAssetPath))
                {
                    var styled=StyledContentCompiler.BuildJsonForTransaction(recipeAssetPath,result.PatchedRecipeJson);
                    build=new VfxBuildResult{Succeeded=styled.Succeeded,PrefabPath=styled.PrefabPath,Plan=new VfxBuildPlan{Report=styled.Report,RecipeRevision=patchedRecipe.Revision,RecipeHash=styled.RecipeHash,BuildHash=styled.BuildHash}};
                }
                else build = compilerFactory().Build(result.PatchedRecipeJson, catalog);
                result.Report.AddRange(build.Plan.Report);
                if (!build.Succeeded)
                {
                    RestoreGenerated(result, generatedSnapshot);
                    result.Report.Add(PatchTransaction, ValidationSeverity.Error, "/build", "Patch Build failed; Recipe, history and Generated were restored.");
                    return result;
                }
                if (transactionHook != null) transactionHook.AfterBuildBeforeTextCommit();
                WriteAtomically(absoluteRecipePath, result.PatchedRecipeJson);
                if (transactionHook != null) transactionHook.AfterRecipeWrittenBeforeHistoryWritten();
                WriteAtomically(absoluteHistoryPath, BuildHistory(beforeHistory, result, patchJson, build));
                AssetDatabase.ImportAsset(recipeAssetPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(historyAssetPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                RestoreText(result, absoluteRecipePath, beforeJson, absoluteHistoryPath, beforeHistory);
                RestoreGenerated(result, generatedSnapshot);
                result.Report.Add(PatchTransaction, ValidationSeverity.Error, "/transaction", "Patch text transaction failed: " + exception.Message);
            }
            finally
            {
                try { generatedSnapshot.Dispose(); }
                catch (Exception exception) { result.Report.Add(PatchRollback, ValidationSeverity.Error, "/transaction/backup-cleanup", "Patch backup cleanup failed: " + exception.Message + " Manual cleanup may be required."); }
            }
            return result;
        }

        private static bool UsesStyledCompiler(Recipe recipe,string recipeAssetPath)
        {
            if(recipe==null)return false;
            if(recipe.Archetype==RecipeArchetype.Decal||recipe.Archetype==RecipeArchetype.WeaponTrail||recipe.Archetype==RecipeArchetype.Destruction||recipe.Archetype==RecipeArchetype.LifeCycle||recipe.Archetype==RecipeArchetype.Portal||recipe.Archetype==RecipeArchetype.Loot)return true;
            if(!string.IsNullOrEmpty(recipeAssetPath)&&recipeAssetPath.IndexOf("/StyleSamples/",StringComparison.Ordinal)>=0)return true;
            try{var manifestPath=VfxProjectRules.ManifestAbsolutePath(recipe.Id);if(File.Exists(manifestPath)){var version=(string)JObject.Parse(File.ReadAllText(manifestPath))["compilerVersion"];return version!=null&&version.StartsWith("styled-content-",StringComparison.Ordinal);}}catch{}
            return false;
        }

        private static bool UsesCompositeCompiler(Recipe recipe,string recipeAssetPath)
        {
            if(recipe==null||recipe.Archetype!=RecipeArchetype.Composite)return false;
            if(!string.IsNullOrEmpty(recipeAssetPath)&&recipeAssetPath.IndexOf("/Composites/",StringComparison.Ordinal)>=0)return true;
            try{var manifestPath=VfxProjectRules.ManifestAbsolutePath(recipe.Id);if(File.Exists(manifestPath)){var version=(string)JObject.Parse(File.ReadAllText(manifestPath))["compilerVersion"];return version!=null&&version.StartsWith("composite-runtime-",StringComparison.Ordinal);}}catch{}
            return false;
        }

        private static bool UsesIndependentCompiler(Recipe recipe,string recipeAssetPath)
        {
            if(recipe==null)return false;
            if(!string.IsNullOrEmpty(recipeAssetPath)&&recipeAssetPath.IndexOf("/Independent/",StringComparison.Ordinal)>=0)return true;
            try{var manifestPath=VfxProjectRules.ManifestAbsolutePath(recipe.Id);if(File.Exists(manifestPath)){var version=(string)JObject.Parse(File.ReadAllText(manifestPath))["compilerVersion"];return version!=null&&version.StartsWith("planned-independent-",StringComparison.Ordinal);}}catch{}
            return false;
        }

        private static List<VfxPatchOperation> ParseOperations(string patchJson, VfxPatchResult result)
        {
            var operations = new List<VfxPatchOperation>();
            JToken root;
            try { root = JToken.Parse(patchJson, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }); }
            catch (Exception exception) { result.Report.Add(PatchParse, ValidationSeverity.Error, "/", "Patch JSON is invalid: " + exception.Message); return operations; }
            var array = root as JArray;
            if (array == null) { result.Report.Add(PatchType, ValidationSeverity.Error, "/", "Patch top level must be the S2 bare operation array.", root, "array"); return operations; }
            if (array.Count == 0) { result.Report.Add(PatchRequired, ValidationSeverity.Error, "/", "Patch must contain at least one operation."); return operations; }
            for (var index = 0; index < array.Count; index++)
            {
                var path = "/" + index;
                var obj = array[index] as JObject;
                if (obj == null) { result.Report.Add(PatchType, ValidationSeverity.Error, path, "Patch operation must be an object.", array[index], "object"); result.FailedOperationIndex = index; return operations; }
                var opToken = obj["op"];
                var pathToken = obj["path"];
                if (opToken == null) { result.Report.Add(PatchRequired, ValidationSeverity.Error, path + "/op", "Patch operation requires op."); result.FailedOperationIndex = index; return operations; }
                if (pathToken == null) { result.Report.Add(PatchRequired, ValidationSeverity.Error, path + "/path", "Patch operation requires path."); result.FailedOperationIndex = index; return operations; }
                if (opToken.Type != JTokenType.String || pathToken.Type != JTokenType.String) { result.Report.Add(PatchType, ValidationSeverity.Error, opToken.Type != JTokenType.String ? path + "/op" : path + "/path", "op and path must be strings."); result.FailedOperationIndex = index; return operations; }
                VfxPatchOperationKind kind;
                if (!TryParseKind((string)opToken, out kind)) { result.Report.Add(PatchOperation, ValidationSeverity.Error, path + "/op", "Patch op is not allow-listed.", opToken, "replace, add, remove, enable, disable, set_behavior_param, set_style_token, set_palette, set_archetype_param, set_content_param"); result.FailedOperationIndex = index; return operations; }
                var requiresValue = kind == VfxPatchOperationKind.Replace || kind == VfxPatchOperationKind.Add || kind == VfxPatchOperationKind.SetBehaviorParam || kind == VfxPatchOperationKind.SetStyleToken || kind == VfxPatchOperationKind.SetPalette || kind == VfxPatchOperationKind.SetArchetypeParam || kind == VfxPatchOperationKind.SetContentParam;
                foreach (var property in obj.Properties())
                    if (property.Name != "op" && property.Name != "path" && (property.Name != "value" || !requiresValue)) { result.Report.Add(PatchUnknownField, ValidationSeverity.Error, path + "/" + property.Name, "Unknown or forbidden Patch field."); result.FailedOperationIndex = index; return operations; }
                var value = obj["value"];
                if (requiresValue && value == null) { result.Report.Add(PatchRequired, ValidationSeverity.Error, path + "/value", "This Patch op requires value."); result.FailedOperationIndex = index; return operations; }
                operations.Add(new VfxPatchOperation { Index = index, Kind = kind, Path = (string)pathToken, Value = value == null ? null : value.DeepClone() });
            }
            return operations;
        }

        private static bool ApplyOperation(JObject root, VfxPatchOperation operation, VfxPatchResult result)
        {
            if (operation.Kind == VfxPatchOperationKind.SetBehaviorParam) return ApplyBehaviorParameter(root, operation, result);
            if (operation.Kind == VfxPatchOperationKind.SetStyleToken) return ApplyStyleToken(root, operation, result);
            if (operation.Kind == VfxPatchOperationKind.SetPalette) return ApplyPalette(root, operation, result);
            if (operation.Kind == VfxPatchOperationKind.SetArchetypeParam) return ApplyArchetypeParameter(root, operation, result);
            if (operation.Kind == VfxPatchOperationKind.SetContentParam) return ApplyContentParameter(root, operation, result);
            string stageId; string moduleId; string parameter;
            if (!TryParsePath(operation.Path, operation.Kind, out stageId, out moduleId, out parameter)) return Fail(result, operation, PatchPath, operation.Path, "Patch path is not an allowed stable semantic path.");
            var stage = FindById(root["stages"] as JArray, stageId);
            if (stage == null) return Fail(result, operation, PatchTarget, "/stages/" + stageId, "Patch stage target does not exist.");
            if (operation.Kind == VfxPatchOperationKind.Enable || operation.Kind == VfxPatchOperationKind.Disable)
            {
                var target = moduleId == null ? stage : FindById(stage["modules"] as JArray, moduleId);
                if (target == null) return Fail(result, operation, PatchTarget, "/stages/" + stageId + "/modules/" + moduleId, "Patch module target does not exist.");
                target["enabled"] = operation.Kind == VfxPatchOperationKind.Enable;
                return true;
            }
            var modules = stage["modules"] as JArray;
            var module = FindById(modules, moduleId);
            if (operation.Kind == VfxPatchOperationKind.Replace)
            {
                if (module == null) return Fail(result, operation, PatchTarget, "/stages/" + stageId + "/modules/" + moduleId, "Patch module target does not exist.");
                var parameters = module["parameters"] as JObject;
                if (parameters == null || parameters[parameter] == null) return Fail(result, operation, PatchTarget, operation.Path, "replace target parameter does not exist.");
                parameters[parameter] = operation.Value;
                return true;
            }
            if (operation.Kind == VfxPatchOperationKind.Add)
            {
                if (module != null) return Fail(result, operation, PatchAdd, operation.Path, "add target module ID already exists.");
                var value = operation.Value as JObject;
                if (value == null || value["id"] == null || value["id"].Type != JTokenType.String || !string.Equals((string)value["id"], moduleId, StringComparison.Ordinal)) return Fail(result, operation, PatchAdd, operation.Path, "add value must be a complete module object whose id matches the path module ID.");
                modules.Add(value);
                return true;
            }
            if (module == null) return Fail(result, operation, PatchTarget, operation.Path, "remove target module does not exist.");
            if (IsRequiredModule(stageId, moduleId, module)) return Fail(result, operation, PatchRequiredModule, operation.Path, "This v1 required module cannot be removed. Any travel-stage energy_body module is required; travel secondary_particles modules remain removable.");
            module.Remove();
            return true;
        }

        private static bool ApplyBehaviorParameter(JObject root, VfxPatchOperation operation, VfxPatchResult result)
        {
            var parts = SplitStablePath(operation.Path);
            if (parts == null || parts.Length != 3 || parts[0] != "behavior" || !new[] { "motion", "hit", "emission", "timing" }.Contains(parts[1], StringComparer.Ordinal) || parts[2] == "type") return Fail(result, operation, PatchPath, operation.Path, "set_behavior_param requires /behavior/{motion|hit|emission|timing}/{declaredParameter}; changing type requires template rebuild.");
            var behavior = root["behavior"] as JObject; var block = behavior == null ? null : behavior[parts[1]] as JObject;
            if (block == null || block["type"] == null || block[parts[2]] == null) return Fail(result, operation, PatchTarget, operation.Path, "Behavior parameter target does not exist; capability type creation/switching is not a Patch.");
            block[parts[2]] = operation.Value.DeepClone(); return true;
        }

        private static bool ApplyStyleToken(JObject root, VfxPatchOperation operation, VfxPatchResult result)
        {
            if (operation.Path != "/style/token" || operation.Value == null || operation.Value.Type != JTokenType.String) return Fail(result, operation, PatchPath, operation.Path, "set_style_token requires string value at /style/token.");
            var style = EnsureStyleObject(root); style["token"] = operation.Value.DeepClone(); return true;
        }

        private static bool ApplyPalette(JObject root, VfxPatchOperation operation, VfxPatchResult result)
        {
            var parts = SplitStablePath(operation.Path);
            if (parts == null || parts.Length != 3 || parts[0] != "style" || parts[1] != "palette" || !new[] { "primary", "secondary", "accent" }.Contains(parts[2], StringComparer.Ordinal) || operation.Value == null || operation.Value.Type != JTokenType.String) return Fail(result, operation, PatchPath, operation.Path, "set_palette requires a color string at /style/palette/{primary|secondary|accent}.");
            var style = EnsureStyleObject(root); var palette = style["palette"] as JObject; if (palette == null) { palette = new JObject(); style["palette"] = palette; } palette[parts[2]] = operation.Value.DeepClone(); return true;
        }

        private static bool ApplyArchetypeParameter(JObject root,VfxPatchOperation operation,VfxPatchResult result)
        {
            var parts=SplitStablePath(operation.Path);if(parts==null||parts.Length!=2||parts[0]!="archetypeParameters")return Fail(result,operation,PatchPath,operation.Path,"set_archetype_param requires /archetypeParameters/{registeredParameter}.");
            var parameters=root["archetypeParameters"] as JObject;if(parameters==null||parameters[parts[1]]==null)return Fail(result,operation,PatchTarget,operation.Path,"Archetype parameter target does not exist; adding protocol fields is not a Patch.");
            parameters[parts[1]]=operation.Value.DeepClone();return true;
        }

        private static bool ApplyContentParameter(JObject root,VfxPatchOperation operation,VfxPatchResult result)
        {
            var parts=SplitStablePath(operation.Path);if(parts==null||parts.Length!=3||parts[0]!="content"||parts[1]!="parameters")return Fail(result,operation,PatchPath,operation.Path,"set_content_param requires /content/parameters/{registeredParameter}.");
            var content=root["content"] as JObject;var parameters=content==null?null:content["parameters"] as JObject;if(parameters==null||parameters[parts[2]]==null)return Fail(result,operation,PatchTarget,operation.Path,"Content parameter target does not exist; adding or renaming visual semantics is not a Patch.");
            parameters[parts[2]]=operation.Value.DeepClone();return true;
        }

        private static JObject EnsureStyleObject(JObject root)
        {
            var style = root["style"] as JObject;
            if (style != null) return style;
            var token = root["style"] != null && root["style"].Type == JTokenType.String ? (string)root["style"] : "stylized";
            style = new JObject { ["token"] = token }; root["style"] = style; return style;
        }

        private static string[] SplitStablePath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("/", StringComparison.Ordinal) || path.IndexOf('~') >= 0 || path.IndexOf("..", StringComparison.Ordinal) >= 0) return null;
            var parts = path.Substring(1).Split('/'); return parts.Any(string.IsNullOrEmpty) || parts.Any(value => !IsStableId(value)) ? null : parts;
        }

        private static bool TryParsePath(string path, VfxPatchOperationKind kind, out string stageId, out string moduleId, out string parameter)
        {
            stageId = moduleId = parameter = null;
            if (string.IsNullOrEmpty(path) || path.IndexOf('~') >= 0 || path.IndexOf("..", StringComparison.Ordinal) >= 0 || !path.StartsWith("/stages/", StringComparison.Ordinal)) return false;
            var parts = path.Split('/');
            if (parts.Length == 0 || parts[0] != string.Empty || parts.Skip(1).Any(string.IsNullOrEmpty)) return false;
            if (parts.Length < 3 || parts[1] != "stages" || !IsStableId(parts[2])) return false;
            stageId = parts[2];
            if (kind == VfxPatchOperationKind.Enable || kind == VfxPatchOperationKind.Disable)
            {
                if (parts.Length == 3) return true;
                if (parts.Length != 5) return false;
            }
            else if (kind == VfxPatchOperationKind.Add || kind == VfxPatchOperationKind.Remove)
            {
                if (parts.Length != 5) return false;
            }
            else if (kind == VfxPatchOperationKind.Replace && parts.Length != 7) return false;
            if (parts.Length != 5 && parts.Length != 7) return false;
            if (parts[3] != "modules" || !IsStableId(parts[4])) return false;
            moduleId = parts[4];
            if (kind == VfxPatchOperationKind.Add || kind == VfxPatchOperationKind.Remove || kind == VfxPatchOperationKind.Enable || kind == VfxPatchOperationKind.Disable) return true;
            if (parts.Length != 7 || parts[5] != "parameters" || !IsStableId(parts[6])) return false;
            parameter = parts[6];
            return kind == VfxPatchOperationKind.Replace;
        }

        private static bool IsStableId(string value) { return !string.IsNullOrEmpty(value) && char.IsLetter(value[0]) && value.All(character => char.IsLetterOrDigit(character) || character == '_' || character == '-'); }
        // Gate D decision: protect semantics, not the canonical example's spelling. A renamed travel
        // energy body is still the required projectile body and cannot be silently removed by Patch.
        private static bool IsRequiredModule(string stageId, string moduleId, JObject module) { return stageId == "travel" && string.Equals((string)module["kind"], "energy_body", StringComparison.Ordinal); }
        private static JObject FindById(JArray values, string id) { return values == null ? null : values.Children<JObject>().FirstOrDefault(value => string.Equals((string)value["id"], id, StringComparison.Ordinal)); }
        private static bool Fail(VfxPatchResult result, VfxPatchOperation operation, string code, string path, string message) { result.Report.Add(code, ValidationSeverity.Error, path, message); result.FailedOperationIndex = operation.Index; return false; }
        private static bool TryParseKind(string value, out VfxPatchOperationKind kind)
        {
            switch (value)
            {
                case "replace": kind = VfxPatchOperationKind.Replace; return true;
                case "add": kind = VfxPatchOperationKind.Add; return true;
                case "remove": kind = VfxPatchOperationKind.Remove; return true;
                case "enable": kind = VfxPatchOperationKind.Enable; return true;
                case "disable": kind = VfxPatchOperationKind.Disable; return true;
                case "set_behavior_param": kind = VfxPatchOperationKind.SetBehaviorParam; return true;
                case "set_style_token": kind = VfxPatchOperationKind.SetStyleToken; return true;
                case "set_palette": kind = VfxPatchOperationKind.SetPalette; return true;
                case "set_archetype_param": kind = VfxPatchOperationKind.SetArchetypeParam; return true;
                case "set_content_param": kind = VfxPatchOperationKind.SetContentParam; return true;
                default: kind = default(VfxPatchOperationKind); return false;
            }
        }

        private static int? AttributePostPatchFailure(IEnumerable<ValidationEntry> entries, IList<VfxPatchOperation> operations)
        {
            var errorEntries = entries.Where(entry => entry.Severity == ValidationSeverity.Error).ToList();
            if (errorEntries.Count == 0) return null;
            var attributed = new List<int>();
            foreach (var entry in errorEntries)
            {
                var matching = operations.Where(operation => OperationCanCausePath(operation, entry.Path)).OrderBy(operation => operation.Index).LastOrDefault();
                if (matching == null) return null;
                attributed.Add(matching.Index);
            }
            return attributed.Distinct().Count() == 1 ? (int?)attributed[0] : null;
        }

        private static bool OperationCanCausePath(VfxPatchOperation operation, string validationPath)
        {
            if (string.IsNullOrEmpty(operation.Path) || string.IsNullOrEmpty(validationPath)) return false;
            // add has a module-root path; semantic parser entries beneath that module are attributable to it.
            return string.Equals(operation.Path, validationPath, StringComparison.Ordinal) || validationPath.StartsWith(operation.Path + "/", StringComparison.Ordinal);
        }

        private static void RestoreText(VfxPatchResult result, string recipePath, string beforeRecipe, string historyPath, string beforeHistory)
        {
            try
            {
                WriteAtomically(recipePath, beforeRecipe);
                if (beforeHistory == null) { if (File.Exists(historyPath)) File.Delete(historyPath); }
                else WriteAtomically(historyPath, beforeHistory);
                AssetDatabase.Refresh();
            }
            catch (Exception exception)
            {
                result.Report.Add(PatchRollback, ValidationSeverity.Error, "/transaction/rollback/text", "Patch text rollback failed: " + exception.Message + " Automatic rollback may be incomplete; manual recovery is required.");
            }
        }

        private static void RestoreGenerated(VfxPatchResult result, IVfxPatchGeneratedSnapshot snapshot)
        {
            try { snapshot.Restore(); }
            catch (Exception exception)
            {
                result.Report.Add(PatchRollback, ValidationSeverity.Error, "/transaction/rollback/generated", "Patch Generated rollback failed: " + exception.Message + " Automatic rollback may be incomplete; manual recovery is required.");
            }
        }

        private static void AddImpact(JObject before, JObject after, List<VfxPatchImpactItem> result)
        {
            var stageIds = before["stages"].Children<JObject>().Select(value => (string)value["id"]).Concat(after["stages"].Children<JObject>().Select(value => (string)value["id"])).Distinct().OrderBy(value => value, StringComparer.Ordinal);
            foreach (var stageId in stageIds)
            {
                var beforeStage = FindById(before["stages"] as JArray, stageId);
                var afterStage = FindById(after["stages"] as JArray, stageId);
                var oldStageContract = beforeStage == null ? null : (JObject)beforeStage.DeepClone();
                var newStageContract = afterStage == null ? null : (JObject)afterStage.DeepClone();
                if (oldStageContract != null) oldStageContract.Remove("modules");
                if (newStageContract != null) newStageContract.Remove("modules");
                result.Add(new VfxPatchImpactItem { StageId = stageId, ModuleId = null, IsStage = true, State = oldStageContract == null ? VfxPatchImpactState.Create : newStageContract == null ? VfxPatchImpactState.Remove : JToken.DeepEquals(oldStageContract, newStageContract) ? VfxPatchImpactState.Unchanged : VfxPatchImpactState.Update });
                var moduleIds = (beforeStage?["modules"] as JArray)?.Children<JObject>().Select(value => (string)value["id"]) ?? Enumerable.Empty<string>();
                moduleIds = moduleIds.Concat((afterStage?["modules"] as JArray)?.Children<JObject>().Select(value => (string)value["id"]) ?? Enumerable.Empty<string>()).Distinct().OrderBy(value => value, StringComparer.Ordinal);
                foreach (var moduleId in moduleIds)
                {
                    var oldModule = FindById(beforeStage?["modules"] as JArray, moduleId);
                    var newModule = FindById(afterStage?["modules"] as JArray, moduleId);
                    result.Add(new VfxPatchImpactItem { StageId = stageId, ModuleId = moduleId, State = oldModule == null ? VfxPatchImpactState.Create : newModule == null ? VfxPatchImpactState.Remove : JToken.DeepEquals(oldModule, newModule) ? VfxPatchImpactState.Unchanged : VfxPatchImpactState.Update });
                }
            }
        }

        private static string BuildHistory(string beforeHistory, VfxPatchResult result, string patchJson, VfxBuildResult build)
        {
            JArray entries;
            try { entries = string.IsNullOrWhiteSpace(beforeHistory) ? new JArray() : JArray.Parse(beforeHistory); }
            catch (Exception exception) { throw new InvalidOperationException("Existing Patch history is not a JSON array: " + exception.Message, exception); }
            var affected = new JObject();
            foreach (var item in result.AffectedItems) affected[item.IsStage ? "stage/" + item.StageId : item.StageId + "/" + item.ModuleId] = item.State.ToString().ToLowerInvariant();
            entries.Add(new JObject
            {
                ["beforeRevision"] = result.BeforeRevision,
                ["afterRevision"] = result.AfterRevision,
                ["beforeCanonicalHash"] = result.BeforeCanonicalHash,
                ["afterCanonicalHash"] = result.AfterCanonicalHash,
                ["utc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                ["ops"] = JToken.Parse(patchJson),
                ["affectedModules"] = affected,
                ["build"] = new JObject { ["succeeded"] = build.Succeeded, ["prefabPath"] = build.PrefabPath, ["buildHash"] = build.Plan.BuildHash }
            });
            return entries.ToString(Formatting.Indented);
        }

        private static bool IsHistoryArray(string history)
        {
            if (string.IsNullOrWhiteSpace(history)) return true;
            try { return JToken.Parse(history) is JArray; }
            catch (Exception) { return false; }
        }

        private static string AbsoluteAssetPath(string assetPath) { return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)); }
        private static void WriteAtomically(string path, string contents)
        {
            var pending = path + ".pending";
            try
            {
                if (File.Exists(pending)) File.Delete(pending);
                File.WriteAllText(pending, contents, new UTF8Encoding(false));
                if (File.Exists(path)) ReplaceWithRetry(pending, path); else File.Move(pending, path);
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
        }

        private static void ReplaceWithRetry(string pending, string destination)
        {
            Exception failure = null;
            for (var attempt = 0; attempt < 4; attempt++)
            {
                try { File.Replace(pending, destination, null); return; }
                catch (IOException exception) { failure = exception; }
                catch (UnauthorizedAccessException exception) { failure = exception; }
                if (attempt < 3) System.Threading.Thread.Sleep(25 * (attempt + 1));
            }
            throw new IOException("Patch atomic replacement failed after bounded retries: " + destination, failure);
        }

        private sealed class DefaultSnapshotProvider : IVfxPatchSnapshotProvider
        {
            public IVfxPatchGeneratedSnapshot Capture(string assetFolder) { return GeneratedSnapshot.Capture(assetFolder); }
        }

        private sealed class GeneratedSnapshot : IVfxPatchGeneratedSnapshot
        {
            private readonly string assetFolder;
            private readonly string absoluteFolder;
            private readonly string backupRoot;
            private readonly bool existed;
            private readonly bool folderMetaExisted;
            private bool disposed;

            private GeneratedSnapshot(string assetFolder, string absoluteFolder, string backupRoot, bool existed, bool folderMetaExisted)
            {
                this.assetFolder = assetFolder;
                this.absoluteFolder = absoluteFolder;
                this.backupRoot = backupRoot;
                this.existed = existed;
                this.folderMetaExisted = folderMetaExisted;
            }

            public static GeneratedSnapshot Capture(string assetFolder)
            {
                if (string.IsNullOrEmpty(assetFolder) || !assetFolder.StartsWith(VfxCompiler.GeneratedRoot + "/", StringComparison.Ordinal) || Path.GetDirectoryName(assetFolder).Replace('\\', '/') != VfxCompiler.GeneratedRoot) throw new InvalidOperationException("Patch snapshot target is outside the exact Generated recipe folder boundary.");
                var absolute = AbsoluteAssetPath(assetFolder);
                var backup = Path.Combine(Path.GetTempPath(), "vfxs8backup_" + Guid.NewGuid().ToString("N"));
                try
                {
                    var exists = Directory.Exists(absolute);
                    var metaExists = File.Exists(absolute + ".meta");
                    Directory.CreateDirectory(backup);
                    if (exists) CopyDirectory(absolute, Path.Combine(backup, "folder"));
                    if (metaExists) File.Copy(absolute + ".meta", Path.Combine(backup, "folder.meta"), true);
                    return new GeneratedSnapshot(assetFolder, absolute, backup, exists, metaExists);
                }
                catch (Exception copyException)
                {
                    try { if (Directory.Exists(backup)) Directory.Delete(backup, true); }
                    catch (Exception cleanupException) { throw new InvalidOperationException("Patch snapshot copy failed and its temporary backup could not be cleaned: " + cleanupException.Message, copyException); }
                    throw;
                }
            }

            public void Restore()
            {
                if (AssetDatabase.IsValidFolder(assetFolder)) AssetDatabase.DeleteAsset(assetFolder);
                else if (Directory.Exists(absoluteFolder)) Directory.Delete(absoluteFolder, true);
                if (File.Exists(absoluteFolder + ".meta")) File.Delete(absoluteFolder + ".meta");
                if (existed) CopyDirectory(Path.Combine(backupRoot, "folder"), absoluteFolder);
                if (folderMetaExisted) File.Copy(Path.Combine(backupRoot, "folder.meta"), absoluteFolder + ".meta", true);
                AssetDatabase.Refresh();
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                if (Directory.Exists(backupRoot)) Directory.Delete(backupRoot, true);
            }

            private static void CopyDirectory(string source, string destination)
            {
                Directory.CreateDirectory(destination);
                foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(destination, directory.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
                foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories)) File.Copy(file, Path.Combine(destination, file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), true);
            }
        }
    }

    internal interface IVfxPatchTransactionHook
    {
        void AfterBuildBeforeTextCommit();
        void AfterRecipeWrittenBeforeHistoryWritten();
    }

    internal interface IVfxPatchGeneratedSnapshot : IDisposable
    {
        void Restore();
    }

    internal interface IVfxPatchSnapshotProvider
    {
        IVfxPatchGeneratedSnapshot Capture(string assetFolder);
    }
}
