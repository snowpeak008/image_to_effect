using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using UnityEngine;
using VFXComposer;
using VFXComposer.Editor.Catalog;
using VFXComposer.Editor.Capabilities;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Validation;
using VFXComposer.Editor.W24.S5;

namespace VFXComposer.Editor.Build
{
    public sealed class VfxBindingException : Exception
    {
        public readonly string Path;
        public VfxBindingException(string path, string message, Exception innerException) : base(message, innerException) { Path = path; }
    }

    /// <summary>Builds formal dimension-matched templates into a deep-copied managed Prefab. All writes are confined to Generated.</summary>
    public sealed class VfxCompiler
    {
        // Release identity, deliberately separate from Recipe v1. Changing it invalidates
        // the managed build hash so the manifest truthfully records the producing compiler.
        public const string CompilerVersion = "0.1.0";
        public const string GeneratedRoot = "Assets/VFX/Generated";
        public const string ManifestFileName = "BuildManifest.json";
        private const string ManifestRoot = "Assets/VFX/Templates";
        private readonly VfxBindingHandlerRegistry bindings;
        private readonly IVfxCompilerBuildHook buildHook;
        private readonly ITemplateDependencyHashProvider dependencyHashProvider;

        public VfxCompiler(VfxBindingHandlerRegistry bindings = null)
        {
            this.bindings = bindings ?? VfxBindingHandlerRegistry.CreateFormal();
            dependencyHashProvider = new UnityTemplateDependencyHashProvider();
        }

        internal VfxCompiler(VfxBindingHandlerRegistry bindings, IVfxCompilerBuildHook buildHook)
        {
            this.bindings = bindings ?? VfxBindingHandlerRegistry.CreateFormal();
            this.buildHook = buildHook;
            dependencyHashProvider = new UnityTemplateDependencyHashProvider();
        }

        internal VfxCompiler(VfxBindingHandlerRegistry bindings, IVfxCompilerBuildHook buildHook, ITemplateDependencyHashProvider dependencyHashProvider)
        {
            this.bindings = bindings ?? VfxBindingHandlerRegistry.CreateFormal();
            this.buildHook = buildHook;
            this.dependencyHashProvider = dependencyHashProvider ?? new UnityTemplateDependencyHashProvider();
        }

        public static TemplateCatalog LoadFormalCatalog()
        {
            return TemplateCatalog.LoadFromDirectory(Path.Combine(Application.dataPath, "VFX", "Templates"), new UnityAssetReferenceResolver());
        }

        public VfxBuildPlan DryRun(string recipeJson, TemplateCatalog catalog = null)
        {
            catalog = catalog ?? LoadFormalCatalog();
            var plan = new VfxBuildPlan();
            var parsed = VfxDomainParser.ParseRecipe(recipeJson);
            plan.Report.AddRange(parsed.Report);
            plan.Report.AddRange(catalog.Report);
            if (!plan.Report.HasErrors) plan.Report.AddRange(RecipeValidator.ValidateSemantic(parsed.Value, catalog));
            if (!plan.Report.HasErrors) plan.Report.AddRange(CapabilityRegistry.Validate(parsed.Value));
            if (!plan.Report.HasErrors) plan.Report.AddRange(CapabilitySlotValidator.Validate(parsed.Value));
            if (!plan.Report.HasErrors) plan.Report.AddRange(BudgetCalculator.Evaluate(parsed.Value, catalog));
            if (!plan.Report.HasErrors) ValidateBindings(parsed.Value, catalog, plan.Report);
            if (plan.Report.HasErrors)
            {
                plan.Items.Add(new VfxBuildItem { State = VfxBuildItemState.Blocked, AssetPath = "/", Reason = FirstError(plan.Report) });
                return plan;
            }

            plan.RecipeHash = RecipeCanonicalizer.ComputeSha256(recipeJson);
            plan.RecipeRevision = parsed.Value.Revision;
            plan.BuildHash = ComputeBuildHash(plan.RecipeHash, parsed.Value, catalog);
            var prefabPath = PrefabPath(parsed.Value);
            if (!IsGeneratedPath(prefabPath))
            {
                plan.Report.Add("E600", ValidationSeverity.Error, "/output", "Compiler output path is outside Assets/VFX/Generated/.", new JValue(prefabPath), GeneratedRoot + "/<recipe>");
                plan.Items.Add(new VfxBuildItem { State = VfxBuildItemState.Blocked, AssetPath = prefabPath, Reason = "Unsafe output path." });
                return plan;
            }
            var current = LoadManifest(ManifestPath(parsed.Value));
            if (current != null && string.Equals(current.BuildHash, plan.BuildHash, StringComparison.Ordinal) && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                plan.Items.Add(new VfxBuildItem { State = VfxBuildItemState.Unchanged, AssetPath = prefabPath, Reason = "Canonical inputs and template versions are unchanged." });
            else
                plan.Items.Add(new VfxBuildItem { State = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null ? VfxBuildItemState.Create : VfxBuildItemState.Update, AssetPath = prefabPath, Reason = "Managed output differs from the recorded build hash." });
            return plan;
        }

        /// <summary>
        /// Explicit W24 S5 admission path.  The legacy Build/DryRun methods intentionally remain
        /// source-compatible; new formal authoring must use this path so the planned build hash is
        /// bound into the contract trace before any managed output is touched.
        /// </summary>
        public VfxBuildPlan DryRunProduction(string recipeJson, W24S5ProductionGateRequest productionRequest, TemplateCatalog catalog = null)
        {
            var plan = DryRun(recipeJson, catalog);
            if (plan.IsBlocked) return plan;
            if (productionRequest == null)
            {
                plan.Report.Add("E24S500", ValidationSeverity.Error, "/productionGate", "Formal production Build requires a W24 S5 contract-first gate request.");
                plan.Items.Add(new VfxBuildItem { State = VfxBuildItemState.Blocked, AssetPath = "/productionGate", Reason = "Production gate request is missing." });
                return plan;
            }
            var recipe = VfxDomainParser.ParseRecipe(recipeJson).Value;
            productionRequest.EffectId = recipe.Id;
            productionRequest.PlannedBuildHash = "sha256:" + plan.BuildHash;
            productionRequest.ExpectedRuntimeEntryPath = PrefabPath(recipe);
            productionRequest.ExpectedManifestPath = W24S5ProductionGate.ManifestRoot + recipe.Id + ".manifest.json";
            var gate = W24S5ProductionGate.Evaluate(productionRequest);
            foreach (var issue in gate.Issues)
                plan.Report.Add(issue.Code, issue.IsError ? ValidationSeverity.Error : ValidationSeverity.Warning, "/productionGate/" + issue.Path, issue.Message);
            if (gate.HasErrors) plan.Items.Add(new VfxBuildItem { State = VfxBuildItemState.Blocked, AssetPath = productionRequest.ExpectedRuntimeEntryPath, Reason = "W24 S5 production gate rejected this entry." });
            else plan.ProductionApproval = gate.Approval;
            return plan;
        }

        public VfxBuildResult BuildProduction(string recipeJson, W24S5ProductionGateRequest productionRequest, TemplateCatalog catalog = null)
        {
            var gatePlan = DryRunProduction(recipeJson, productionRequest, catalog);
            if (gatePlan.IsBlocked) return new VfxBuildResult { Plan = gatePlan, Succeeded = false };
            return BuildProduction(gatePlan, recipeJson, catalog);
        }

        /// <summary>Commits the exact plan returned by DryRunProduction; arbitrary plans have no production authority.</summary>
        public VfxBuildResult BuildProduction(VfxBuildPlan approvedPlan, string recipeJson, TemplateCatalog catalog = null)
        {
            if (approvedPlan == null || approvedPlan.IsBlocked || approvedPlan.ProductionApproval == null)
                return new VfxBuildResult { Plan = approvedPlan, Succeeded = false };
            catalog = catalog ?? LoadFormalCatalog();
            string error;
            if (!W24S5ProductionGate.IsApprovalCurrent(approvedPlan.ProductionApproval, out error) || !MatchesExactPlan(approvedPlan, recipeJson, catalog, out error))
            {
                approvedPlan.Report.Add("E24S501", ValidationSeverity.Error, "/productionGate", error);
                approvedPlan.Items.Add(new VfxBuildItem { State = VfxBuildItemState.Blocked, AssetPath = "/productionGate", Reason = error });
                return new VfxBuildResult { Plan = approvedPlan, Succeeded = false };
            }
            return BuildExactPlan(approvedPlan, recipeJson, catalog, approvedPlan.ProductionApproval);
        }

        public VfxBuildResult Build(string recipeJson, TemplateCatalog catalog = null)
        {
            catalog = catalog ?? LoadFormalCatalog();
            var plan = DryRun(recipeJson, catalog);
            if (plan.IsBlocked) return new VfxBuildResult { Plan = plan, Succeeded = false };
            var parsed = VfxDomainParser.ParseRecipe(recipeJson);
            if (!parsed.Report.HasErrors && W24S5ProductionGate.IsW24ProtectedEffect(parsed.Value.Id))
            {
                plan.Report.Add("E24S5-090", ValidationSeverity.Error, "/productionGate", "W24-protected effects require DryRunProduction/BuildProduction and an S5 gate-owned approval.");
                plan.Items.Add(new VfxBuildItem { State = VfxBuildItemState.Blocked, AssetPath = PrefabPath(parsed.Value), Reason = "W24 S5 gate-owned commit required." });
                return new VfxBuildResult { Plan = plan, Succeeded = false };
            }
            return BuildExactPlan(plan, recipeJson, catalog, null);
        }

        private VfxBuildResult BuildExactPlan(VfxBuildPlan plan, string recipeJson, TemplateCatalog catalog, W24S5FormalApproval formalApproval)
        {
            var result = new VfxBuildResult { Plan = plan, Succeeded = false };
            var parsed = VfxDomainParser.ParseRecipe(recipeJson);
            var recipe = parsed.Value;
            var prefabPath = PrefabPath(recipe);
            result.PrefabPath = prefabPath;
            if (plan.Items.All(item => item.State == VfxBuildItemState.Unchanged))
            {
                string identityError;
                if (formalApproval != null && (!MatchesExactPlan(plan, recipeJson, catalog, out identityError) || !W24S5ProductionGate.IsApprovalCurrent(formalApproval, out identityError)))
                {
                    plan.Report.Add("E24S501", ValidationSeverity.Error, "/productionGate", identityError);
                    plan.Items.Add(new VfxBuildItem { State = VfxBuildItemState.Blocked, AssetPath = "/productionGate", Reason = identityError });
                    return result;
                }
                var priorRulesManifest = VfxProductionRules.CaptureManifest(recipe.Id);
                try
                {
                    var compliance = formalApproval == null
                        ? VfxProductionRules.EnforceAndWriteManifest(recipe.Id, "projectile", recipe.RecipeVersion, recipe.Revision, plan.RecipeHash, plan.BuildHash, CompilerVersion, prefabPath, OutputFolder(recipe), EnabledDuration(recipe))
                        : W24S5ProductionGate.CommitFormalManifest(formalApproval, recipe.Id, "projectile", recipe.RecipeVersion, recipe.Revision, plan.RecipeHash, plan.BuildHash, CompilerVersion, prefabPath, OutputFolder(recipe), EnabledDuration(recipe));
                    plan.Report.AddRange(compliance.Report);
                    if (plan.Report.HasErrors) VfxProductionRules.RestoreManifest(recipe.Id, priorRulesManifest);
                    else result.Succeeded = true;
                }
                catch (Exception exception)
                {
                    VfxProductionRules.RestoreManifest(recipe.Id, priorRulesManifest);
                    if (!plan.Report.HasErrors) plan.Report.Add("E602", ValidationSeverity.Error, "/build", "Unchanged build manifest update failed: " + exception.Message);
                }
                return result;
            }

            var tempFolder = CreateTempFolder(recipe.Id);
            try
            {
                var tempPrefab = BuildTemporaryPrefab(recipe, catalog, tempFolder);
                ValidateGeneratedPrefab(tempPrefab, recipe, plan.Report);
                if (plan.Report.HasErrors) throw new InvalidOperationException("Generated Prefab validation failed.");
                string identityError;
                if (formalApproval != null && !MatchesExactPlan(plan, recipeJson, catalog, out identityError)) throw new InvalidOperationException(identityError);
                if (formalApproval != null && !W24S5ProductionGate.IsApprovalCurrent(formalApproval, out identityError)) throw new InvalidOperationException(identityError);
                Commit(recipe, catalog, plan, tempFolder, tempPrefab, formalApproval);
                result.Succeeded = true;
            }
            catch (Exception exception)
            {
                var bindingException = exception as VfxBindingException;
                if (bindingException != null) plan.Report.Add("E501", ValidationSeverity.Error, bindingException.Path, bindingException.Message);
                else if (!plan.Report.HasErrors) plan.Report.Add("E602", ValidationSeverity.Error, "/build", "Build failed: " + exception.Message);
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(tempFolder)) AssetDatabase.DeleteAsset(tempFolder);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            return result;
        }

        private void ValidateBindings(Recipe recipe, TemplateCatalog catalog, ValidationReport report)
        {
            foreach (var stage in recipe.Stages)
            foreach (var module in stage.Modules)
            {
                TemplateManifest manifest;
                if (!catalog.TryGet(module.TemplateId, out manifest)) continue;
                foreach (var pair in module.Parameters)
                {
                    ManifestParameter parameter;
                    if (manifest.Parameters.TryGetValue(pair.Key, out parameter) && !bindings.Contains(parameter.Binding))
                        report.Add("E500", ValidationSeverity.Error, VfxDomainParser.ModulePath(VfxDomainParser.StagePath(stage.Id), module.Id) + "/parameters/" + pair.Key, "Manifest binding is not in the compiler allow-list.", new JValue(parameter.Binding));
                }
            }
        }

        private GameObject BuildTemporaryPrefab(Recipe recipe, TemplateCatalog catalog, string tempFolder)
        {
            var root = new GameObject(PrefabName(recipe));
            try
            {
                root.AddComponent<GeneratedVfxController>();
                var modules = new Dictionary<string, GameObject>(StringComparer.Ordinal);
                var ordinal = 0;
                foreach (var stage in recipe.Stages)
                {
                    var stageRoot = new GameObject(StageName(stage.Id));
                    stageRoot.transform.SetParent(root.transform, false);
                    // Runtime owns activation. Keeping all stage roots inactive prevents template playOnAwake
                    // from leaking into a generated Prefab before a caller selects a stage.
                    stageRoot.SetActive(false);
                    foreach (var module in stage.Modules)
                    {
                        TemplateManifest manifest;
                        if (!catalog.TryGet(module.TemplateId, out manifest)) throw new InvalidOperationException("Template was not resolved: " + module.TemplateId);
                        var template = AssetDatabase.LoadAssetAtPath<GameObject>(manifest.AssetPath);
                        if (template == null) throw new InvalidOperationException("Template asset is missing: " + manifest.AssetPath);
                        var instance = PrefabUtility.InstantiatePrefab(template) as GameObject;
                        if (instance == null) throw new InvalidOperationException("Template could not be instantiated: " + manifest.AssetPath);
                        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                        instance.name = ModuleName(module.Id);
                        instance.transform.SetParent(stageRoot.transform, false);
                        instance.SetActive(module.Enabled);
                        ApplyModule(module, manifest, instance, stage.Id);
                        SetParticleSeeds(instance, recipe.RandomSeed, ordinal++);
                        CloneMaterials(instance, tempFolder, module.Id);
                        modules.Add(module.Id, instance);
                    }
                }
                foreach (var stage in recipe.Stages)
                foreach (var module in stage.Modules)
                {
                    if (string.IsNullOrEmpty(module.AttachTo)) continue;
                    GameObject child; GameObject parent;
                    if (!modules.TryGetValue(module.Id, out child) || !modules.TryGetValue(module.AttachTo, out parent)) throw new InvalidOperationException("attachTo target was not built: " + module.AttachTo);
                    child.transform.SetParent(parent.transform, false);
                }
                WireControllerStageRoots(root, recipe);
                var path = tempFolder + "/" + PrefabName(recipe) + ".prefab";
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void WireControllerStageRoots(GameObject root, Recipe recipe)
        {
            var controller = root.GetComponent<GeneratedVfxController>();
            if (controller == null) throw new InvalidOperationException("Generated Prefab is missing its Runtime controller.");
            var serialized = new SerializedObject(controller);
            WireStage(serialized, root.transform, "launch", "Launch", "launchRoot", "launchEnabled", recipe);
            WireStage(serialized, root.transform, "travel", "Travel", "travelRoot", "travelEnabled", recipe);
            WireStage(serialized, root.transform, "impact", "Impact", "impactRoot", "impactEnabled", recipe);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireStage(SerializedObject serialized, Transform root, string stageId, string stageName, string rootProperty, string enabledProperty, Recipe recipe)
        {
            var stage = recipe.Stages.FirstOrDefault(value => string.Equals(value.Id, stageId, StringComparison.Ordinal));
            var transform = root.Find(stageName);
            serialized.FindProperty(rootProperty).objectReferenceValue = transform == null ? null : transform.gameObject;
            serialized.FindProperty(enabledProperty).boolValue = stage != null && stage.Enabled;
        }

        private void ApplyModule(RecipeModule module, TemplateManifest manifest, GameObject target, string stageId)
        {
            foreach (var parameter in module.Parameters.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                ManifestParameter declaration;
                if (!manifest.Parameters.TryGetValue(parameter.Key, out declaration)) throw new InvalidOperationException("Parameter was not declared: " + parameter.Key);
                try { bindings.Apply(declaration.Binding, target, parameter.Value); }
                catch (Exception exception)
                {
                    var path = VfxDomainParser.ModulePath(VfxDomainParser.StagePath(stageId), module.Id) + "/parameters/" + parameter.Key;
                    throw new VfxBindingException(path, "Binding '" + declaration.Binding + "' failed: " + exception.Message, exception);
                }
            }
        }

        private static void SetParticleSeeds(GameObject instance, uint randomSeed, int ordinal)
        {
            foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.useAutoRandomSeed = false;
                particle.randomSeed = randomSeed + (uint)ordinal;
            }
        }

        private static void CloneMaterials(GameObject instance, string folder, string moduleId)
        {
            var materialIndex = 0;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var source = renderer.sharedMaterial;
                if (source == null) continue;
                var copy = new Material(source) { name = Sanitize(moduleId) + "_Material_" + materialIndex++ };
                var path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + copy.name + ".mat");
                AssetDatabase.CreateAsset(copy, path);
                renderer.sharedMaterial = copy;
            }
        }

        private static void ValidateGeneratedPrefab(GameObject prefab, Recipe recipe, ValidationReport report)
        {
            if (prefab == null) { report.Add("E601", ValidationSeverity.Error, "/build", "Temporary Prefab was not saved."); return; }
            var controller = prefab.GetComponent<GeneratedVfxController>();
            if (controller == null) report.Add("E601", ValidationSeverity.Error, "/build", "Generated Prefab is missing its Runtime controller.");
            else
            {
                var serialized = new SerializedObject(controller);
                foreach (var pair in new[] {
                    new KeyValuePair<string, string>("launchRoot", "Launch"), new KeyValuePair<string, string>("travelRoot", "Travel"), new KeyValuePair<string, string>("impactRoot", "Impact") })
                {
                    var assigned = serialized.FindProperty(pair.Key).objectReferenceValue as GameObject;
                    if (assigned == null || assigned.transform.parent != prefab.transform || assigned.name != pair.Value)
                        report.Add("E601", ValidationSeverity.Error, "/build", "Generated Runtime controller has a missing or incorrect " + pair.Value + " stage reference.");
                }
                foreach (var pair in new[] {
                    new KeyValuePair<string, string>("launchEnabled", "launch"), new KeyValuePair<string, string>("travelEnabled", "travel"), new KeyValuePair<string, string>("impactEnabled", "impact") })
                {
                    var stage = recipe.Stages.FirstOrDefault(value => string.Equals(value.Id, pair.Value, StringComparison.Ordinal));
                    if (serialized.FindProperty(pair.Key).boolValue != (stage != null && stage.Enabled))
                        report.Add("E601", ValidationSeverity.Error, "/build", "Generated Runtime controller has an incorrect " + pair.Value + " stage enabled flag.");
                }
            }
            foreach (var stage in recipe.Stages)
                if (prefab.transform.Find(StageName(stage.Id)) == null) report.Add("E601", ValidationSeverity.Error, VfxDomainParser.StagePath(stage.Id), "Generated stage hierarchy is missing.");
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                if (renderer.sharedMaterial == null) report.Add("E601", ValidationSeverity.Error, "/build", "Generated Prefab has a renderer without a material.");
            var prefabPath = AssetDatabase.GetAssetPath(prefab);
            foreach (var dependency in AssetDatabase.GetDependencies(prefabPath, true))
            {
                if (!dependency.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                if (!dependency.StartsWith(GeneratedRoot + "/", StringComparison.Ordinal) && !dependency.StartsWith("Assets/VFX/Templates/", StringComparison.Ordinal))
                    report.Add("E601", ValidationSeverity.Error, "/build/dependencies", "Generated Prefab has an out-of-bound project dependency.", new JValue(dependency), GeneratedRoot + "/ or Assets/VFX/Templates/");
            }
        }

        private void Commit(Recipe recipe, TemplateCatalog catalog, VfxBuildPlan plan, string tempFolder, GameObject tempPrefab, W24S5FormalApproval formalApproval)
        {
            var outputFolder = OutputFolder(recipe);
            var outputFolderExisted = AssetDatabase.IsValidFolder(outputFolder);
            EnsureFolder(outputFolder);
            var prefabPath = PrefabPath(recipe);
            var manifestPath = ManifestPath(recipe);
            var priorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var backupFolder = tempFolder + "/backup";
            EnsureFolder(backupFolder);
            var backupPrefabPath = backupFolder + "/prior.prefab";
            var hadPrefab = priorPrefab != null;
            if (hadPrefab && !AssetDatabase.CopyAsset(prefabPath, backupPrefabPath)) throw new InvalidOperationException("Could not snapshot existing managed Prefab for recovery.");
            var priorManifest = File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : null;
            var priorRulesManifest = VfxProductionRules.CaptureManifest(recipe.Id);
            var materialBackups = new List<KeyValuePair<string, string>>();
            var createdMaterials = new List<string>();
            try
            {
                foreach (var renderer in tempPrefab.GetComponentsInChildren<Renderer>(true))
                {
                    var temporaryMaterial = renderer.sharedMaterial;
                    if (temporaryMaterial == null) continue;
                    var temporaryPath = AssetDatabase.GetAssetPath(temporaryMaterial);
                    var finalPath = outputFolder + "/" + Path.GetFileName(temporaryPath);
                    var finalMaterial = AssetDatabase.LoadAssetAtPath<Material>(finalPath);
                    if (finalMaterial == null)
                    {
                        if (!AssetDatabase.CopyAsset(temporaryPath, finalPath)) throw new InvalidOperationException("Could not create generated material: " + finalPath);
                        createdMaterials.Add(finalPath);
                        finalMaterial = AssetDatabase.LoadAssetAtPath<Material>(finalPath);
                    }
                    else
                    {
                        var backupPath = backupFolder + "/" + Path.GetFileName(finalPath);
                        if (!AssetDatabase.CopyAsset(finalPath, backupPath)) throw new InvalidOperationException("Could not snapshot generated material for recovery: " + finalPath);
                        materialBackups.Add(new KeyValuePair<string, string>(finalPath, backupPath));
                        EditorUtility.CopySerialized(temporaryMaterial, finalMaterial);
                    }
                    renderer.sharedMaterial = finalMaterial;
                }
                var saved = PrefabUtility.SaveAsPrefabAsset(tempPrefab, prefabPath);
                if (saved == null) throw new InvalidOperationException("Could not save managed Prefab.");
                if (buildHook != null) buildHook.AfterPrefabAndMaterialsSaved(outputFolder);
                var manifest = CreateManifest(recipe, catalog, plan, prefabPath);
                WriteManifest(manifestPath, manifest);
                AssetDatabase.SaveAssets();
                var compliance = formalApproval == null
                    ? VfxProductionRules.EnforceAndWriteManifest(recipe.Id, "projectile", recipe.RecipeVersion, recipe.Revision, plan.RecipeHash, plan.BuildHash, CompilerVersion, prefabPath, outputFolder, EnabledDuration(recipe))
                    : W24S5ProductionGate.CommitFormalManifest(formalApproval, recipe.Id, "projectile", recipe.RecipeVersion, recipe.Revision, plan.RecipeHash, plan.BuildHash, CompilerVersion, prefabPath, outputFolder, EnabledDuration(recipe));
                plan.Report.AddRange(compliance.Report);
                if (compliance.Report.HasErrors) throw new InvalidOperationException("Production rules rejected the generated Runtime Entry.");
            }
            catch
            {
                foreach (var backup in materialBackups)
                {
                    var source = AssetDatabase.LoadAssetAtPath<Material>(backup.Value);
                    var destination = AssetDatabase.LoadAssetAtPath<Material>(backup.Key);
                    if (source != null && destination != null) EditorUtility.CopySerialized(source, destination);
                }
                foreach (var created in createdMaterials) if (AssetDatabase.LoadAssetAtPath<Material>(created) != null) AssetDatabase.DeleteAsset(created);
                if (hadPrefab)
                {
                    var backup = AssetDatabase.LoadAssetAtPath<GameObject>(backupPrefabPath);
                    if (backup != null) PrefabUtility.SaveAsPrefabAsset(backup, prefabPath);
                }
                else if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) AssetDatabase.DeleteAsset(prefabPath);
                if (priorManifest == null) { if (File.Exists(manifestPath)) File.Delete(manifestPath); }
                else File.WriteAllText(manifestPath, priorManifest, new UTF8Encoding(false));
                VfxProductionRules.RestoreManifest(recipe.Id, priorRulesManifest);
                if (!outputFolderExisted && IsExactGeneratedChild(outputFolder) && IsAssetFolderEmpty(outputFolder)) AssetDatabase.DeleteAsset(outputFolder);
                throw;
            }
        }

        private VfxBuildManifest CreateManifest(Recipe recipe, TemplateCatalog catalog, VfxBuildPlan plan, string prefabPath)
        {
            var result = new VfxBuildManifest { RecipeId = recipe.Id, RecipeRevision = recipe.Revision, RecipeHash = plan.RecipeHash, BuildHash = plan.BuildHash, CompilerVersion = CompilerVersion, UnityVersion = Application.unityVersion, OutputPrefabPath = prefabPath, GeneratedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture), Cost = new VfxBuildCost() };
            foreach (var stage in recipe.Stages)
            {
                if (stage.Enabled) result.Cost.TotalDuration += stage.Duration;
                foreach (var module in stage.Modules)
                {
                    TemplateManifest template;
                    if (!catalog.TryGet(module.TemplateId, out template)) continue;
                    if (!result.Templates.Any(existing => string.Equals(existing.TemplateId, template.TemplateId, StringComparison.Ordinal))) result.Templates.Add(new VfxBuildTemplate { TemplateId = template.TemplateId, TemplateVersion = template.TemplateVersion, AssetGuid = template.AssetGuid, AssetPath = template.AssetPath, DependencyHash = dependencyHashProvider.GetDependencyHash(template.AssetPath) });
                    if (stage.Enabled && module.Enabled) { result.Cost.EstimatedPeakParticles += template.Cost.EstimatedPeakParticles; result.Cost.Materials += template.Cost.Materials; result.Cost.Trails += template.Cost.Trails; }
                }
            }
            result.Templates = result.Templates.OrderBy(template => template.TemplateId, StringComparer.Ordinal).ToList();
            return result;
        }

        private static void WriteManifest(string path, VfxBuildManifest manifest)
        {
            var pending = path + ".pending";
            try
            {
                if (File.Exists(pending)) File.Delete(pending);
                File.WriteAllText(pending, JsonConvert.SerializeObject(manifest, Formatting.Indented, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }), new UTF8Encoding(false));
                if (File.Exists(path)) ReplaceWithRetry(pending, path); else File.Move(pending, path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
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
            throw new IOException("Build manifest atomic replacement failed after bounded retries: " + destination, failure);
        }

        private static VfxBuildManifest LoadManifest(string path)
        {
            if (!File.Exists(path)) return null;
            try { return JsonConvert.DeserializeObject<VfxBuildManifest>(File.ReadAllText(path)); }
            catch { return null; }
        }

        private string ComputeBuildHash(string recipeHash, Recipe recipe, TemplateCatalog catalog)
        {
            var input = new StringBuilder(recipeHash).Append('|').Append(CompilerVersion).Append('|').Append(Application.unityVersion);
            foreach (var id in recipe.Stages.SelectMany(stage => stage.Modules).Select(module => module.TemplateId).Distinct().OrderBy(id => id, StringComparer.Ordinal))
            {
                TemplateManifest template; if (!catalog.TryGet(id, out template)) continue;
                input.Append('|').Append(template.TemplateId).Append('|').Append(template.TemplateVersion).Append('|').Append(template.AssetGuid).Append('|').Append(dependencyHashProvider.GetDependencyHash(template.AssetPath));
            }
            using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(input.ToString())).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        // Recompute identities only as a comparison. The approved VfxBuildPlan remains the sole
        // plan committed, preventing a post-gate recipe/catalog substitution.
        private bool MatchesExactPlan(VfxBuildPlan plan, string recipeJson, TemplateCatalog catalog, out string error)
        {
            error = null;
            try
            {
                var parsed = VfxDomainParser.ParseRecipe(recipeJson);
                if (parsed.Report.HasErrors || parsed.Value == null) { error = "Recipe is no longer parseable after production gating."; return false; }
                var recipe = parsed.Value;
                var recipeHash = RecipeCanonicalizer.ComputeSha256(recipeJson);
                var buildHash = ComputeBuildHash(recipeHash, recipe, catalog);
                if (!string.Equals(recipeHash, plan.RecipeHash, StringComparison.Ordinal) || recipe.Revision != plan.RecipeRevision || !string.Equals(buildHash, plan.BuildHash, StringComparison.Ordinal) || (plan.ProductionApproval != null && !string.Equals(PrefabPath(recipe), plan.ProductionApproval.RuntimeEntryPath, StringComparison.Ordinal)))
                { error = "Recipe, catalog dependency, or planned output identity changed after production gating."; return false; }
                return true;
            }
            catch (Exception exception) { error = "Could not verify the approved plan identity: " + exception.Message; return false; }
        }

        public static string OutputFolder(Recipe recipe) { return GeneratedRoot + "/" + Sanitize(recipe.Id); }
        public static string PrefabPath(Recipe recipe) { return OutputFolder(recipe) + "/" + PrefabName(recipe) + ".prefab"; }
        public static string ManifestPath(Recipe recipe) { return OutputFolder(recipe) + "/" + ManifestFileName; }
        private static string PrefabName(Recipe recipe)
        {
            if (VfxProjectRules.EnforcementFor(recipe.Id) == VfxRulesEnforcement.Strict) return "VFX_" + recipe.Id;
            return "VFX_" + string.Join("_", (recipe.Id ?? "unnamed").Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries).Select(part => string.Equals(part, "2d", StringComparison.OrdinalIgnoreCase) ? "2D" : string.Equals(part, "3d", StringComparison.OrdinalIgnoreCase) ? "3D" : char.ToUpperInvariant(part[0]) + part.Substring(1)));
        }
        private static string StageName(string value) { return char.ToUpperInvariant(value[0]) + value.Substring(1); }
        private static string ModuleName(string value) { return char.ToUpperInvariant(value[0]) + value.Substring(1); }
        private static string Sanitize(string value) { return string.IsNullOrWhiteSpace(value) ? "unnamed" : new string(value.Where(character => char.IsLetterOrDigit(character) || character == '_' || character == '-').ToArray()); }
        private static bool IsGeneratedPath(string path) { return path != null && path.StartsWith(GeneratedRoot + "/", StringComparison.Ordinal) && path.IndexOf("..", StringComparison.Ordinal) < 0; }
        private static bool IsExactGeneratedChild(string path) { return path != null && string.Equals(Path.GetDirectoryName(path).Replace('\\', '/'), GeneratedRoot, StringComparison.Ordinal); }
        private static bool IsAssetFolderEmpty(string path)
        {
            var absolute = Path.Combine(Application.dataPath, path.Substring("Assets/".Length));
            return Directory.Exists(absolute) && Directory.GetFileSystemEntries(absolute).Length == 0;
        }
        private static double EnabledDuration(Recipe recipe) { return recipe.Stages.Where(stage => stage.Enabled).Sum(stage => stage.Duration); }
        private static string CreateTempFolder(string recipeId)
        {
            EnsureFolder(GeneratedRoot);
            var name = "vfxs6tmp_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var guid = AssetDatabase.CreateFolder(GeneratedRoot, name);
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path) || !string.Equals(Path.GetDirectoryName(path).Replace('\\', '/'), GeneratedRoot, StringComparison.Ordinal) || !Path.GetFileName(path).StartsWith("vfxs6tmp_", StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(path) && path.StartsWith(GeneratedRoot + "/", StringComparison.Ordinal) && Path.GetFileName(path).StartsWith("vfxs6tmp_", StringComparison.Ordinal)) AssetDatabase.DeleteAsset(path);
                throw new InvalidOperationException("Could not create a safe compiler temporary directory.");
            }
            return path;
        }
        private static void EnsureFolder(string path) { if (AssetDatabase.IsValidFolder(path)) return; var parent = Path.GetDirectoryName(path).Replace('\\', '/'); if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); }
        private static string FirstError(ValidationReport report) { var entry = report.Entries.FirstOrDefault(value => value.Severity == ValidationSeverity.Error); return entry == null ? "Build blocked." : entry.Code + " " + entry.Path + " " + entry.Message; }
    }
}
