using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VFXComposer.Editor.Catalog;
using VFXComposer.Editor.Domain;

namespace VFXComposer.Editor.SlashV2
{
    // S12A deliberately owns a closed v2 contract. It neither widens VfxDomainParser nor makes v1 projectile data ambiguous.
    public sealed class S12SlashRecipe
    {
        public int RecipeVersion;
        public int Revision;
        public string Id;
        public string Name;
        public string Dimension;
        public string Archetype;
        public string TargetProfile;
        public uint RandomSeed;
        public S12SlashTimeline Timeline = new S12SlashTimeline();
        public List<S12SlashPhase> Phases = new List<S12SlashPhase>();
        public S12SlashMetadata Metadata = new S12SlashMetadata();
    }

    public sealed class S12SlashTimeline { public double Duration; }
    public sealed class S12SlashMetadata { public string CreatedBy; public string TemplateCatalogVersion; }
    public sealed class S12SlashPhase { public string Id; public string Kind; public double StartTime; public double Duration; public bool Enabled; public List<S12SlashModule> Modules = new List<S12SlashModule>(); }
    public sealed class S12SlashModule { public string Id; public string Kind; public string TemplateId; public bool Enabled; public Dictionary<string, JToken> Parameters = new Dictionary<string, JToken>(StringComparer.Ordinal); }
    public sealed class S12SlashManifest { public int SlashManifestVersion; public string TemplateId; public string TemplateVersion; public string PhaseKind; public string ModuleKind; public string Dimension; public string AssetGuid; public string AssetPath; public List<string> Tags = new List<string>(); public List<string> MaterialGuids = new List<string>(); public Dictionary<string, S12SlashParameter> Parameters = new Dictionary<string, S12SlashParameter>(StringComparer.Ordinal); public S12SlashCost Cost = new S12SlashCost(); }
    public sealed class S12SlashParameter { public string Type; public JToken Min; public JToken Default; public JToken Max; public string Binding; }
    public sealed class S12SlashCost { public int EstimatedPeakParticles; public int ParticleSystems; public int Materials; public int TransparentRenderers; }

    public sealed class S12RecipeDispatch
    {
        public int RecipeVersion;
        public Recipe V1;
        public S12SlashRecipe SlashV2;
        public ValidationReport Report = new ValidationReport();
    }

    public static class S12RecipeDispatcher
    {
        public static S12RecipeDispatch Parse(string json)
        {
            var result = new S12RecipeDispatch();
            JObject root;
            try { root = JToken.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }) as JObject; }
            catch (Exception exception) { result.Report.Add("E1200", ValidationSeverity.Error, "/", "Invalid JSON for recipe dispatch: " + exception.Message); return result; }
            if (root == null || root["recipeVersion"] == null || root["recipeVersion"].Type != JTokenType.Integer) { result.Report.Add("E1202", ValidationSeverity.Error, "/recipeVersion", "Recipe dispatch requires integer recipeVersion."); return result; }
            result.RecipeVersion = root.Value<int>("recipeVersion");
            if (result.RecipeVersion == 1) { var parsed = VfxDomainParser.ParseRecipe(json); result.V1 = parsed.Value; result.Report.AddRange(parsed.Report); return result; }
            if (result.RecipeVersion == 2) { var parsed = S12SlashV2Parser.ParseRecipe(json); result.SlashV2 = parsed.Value; result.Report.AddRange(parsed.Report); return result; }
            result.Report.Add("E1203", ValidationSeverity.Error, "/recipeVersion", "Recipe version is not dispatched by S12.", new JValue(result.RecipeVersion), "[1, 2]");
            return result;
        }
    }

    public static class S12SlashV2Parser
    {
        private const string Unknown = "E1201";
        private const string Required = "E1204";
        private const string Type = "E1205";
        private const string NonFinite = "E1206";

        public static ParseResult<S12SlashRecipe> ParseRecipe(string json)
        {
            var result = new ParseResult<S12SlashRecipe> { Value = new S12SlashRecipe() };
            var root = ReadObject(json, "/", result.Report); if (root == null) return result;
            CheckUnknown(root, "/", result.Report, "recipeVersion", "revision", "id", "name", "dimension", "archetype", "targetProfile", "randomSeed", "timeline", "phases", "metadata");
            var value = result.Value;
            value.RecipeVersion = Int(root, "recipeVersion", "/recipeVersion", result.Report, true);
            value.Revision = Int(root, "revision", "/revision", result.Report, true);
            value.Id = String(root, "id", "/id", result.Report, true); value.Name = String(root, "name", "/name", result.Report, true);
            value.Dimension = String(root, "dimension", "/dimension", result.Report, true); value.Archetype = String(root, "archetype", "/archetype", result.Report, true);
            value.TargetProfile = String(root, "targetProfile", "/targetProfile", result.Report, true); value.RandomSeed = UInt(root, "randomSeed", "/randomSeed", result.Report, true);
            var timeline = Object(root, "timeline", "/timeline", result.Report, true);
            if (timeline != null) { CheckUnknown(timeline, "/timeline", result.Report, "duration"); value.Timeline.Duration = Number(timeline, "duration", "/timeline/duration", result.Report, true); }
            var metadata = Object(root, "metadata", "/metadata", result.Report, true);
            if (metadata != null) { CheckUnknown(metadata, "/metadata", result.Report, "createdBy", "templateCatalogVersion"); value.Metadata.CreatedBy = String(metadata, "createdBy", "/metadata/createdBy", result.Report, true); value.Metadata.TemplateCatalogVersion = String(metadata, "templateCatalogVersion", "/metadata/templateCatalogVersion", result.Report, true); }
            var phases = Array(root, "phases", "/phases", result.Report, true);
            if (phases != null) for (var index = 0; index < phases.Count; index++) { var phase = ParsePhase(phases[index] as JObject, "/phases/" + index, result.Report); if (phase != null) value.Phases.Add(phase); }
            return result;
        }

        public static ParseResult<S12SlashManifest> ParseManifest(string json, string sourcePath)
        {
            var result = new ParseResult<S12SlashManifest> { Value = new S12SlashManifest() };
            var prefix = string.IsNullOrEmpty(sourcePath) ? "/" : sourcePath; var root = ReadObject(json, prefix, result.Report); if (root == null) return result;
            CheckUnknown(root, prefix, result.Report, "slashManifestVersion", "templateId", "templateVersion", "phaseKind", "moduleKind", "dimension", "assetGuid", "assetPath", "tags", "materialGuids", "parameters", "cost");
            var value = result.Value; value.SlashManifestVersion = Int(root, "slashManifestVersion", prefix + "/slashManifestVersion", result.Report, true); value.TemplateId = String(root, "templateId", prefix + "/templateId", result.Report, true); value.TemplateVersion = String(root, "templateVersion", prefix + "/templateVersion", result.Report, true); value.PhaseKind = String(root, "phaseKind", prefix + "/phaseKind", result.Report, true); value.ModuleKind = String(root, "moduleKind", prefix + "/moduleKind", result.Report, true); value.Dimension = String(root, "dimension", prefix + "/dimension", result.Report, true); value.AssetGuid = String(root, "assetGuid", prefix + "/assetGuid", result.Report, true); value.AssetPath = String(root, "assetPath", prefix + "/assetPath", result.Report, true);
            var tags = Array(root, "tags", prefix + "/tags", result.Report, true); if (tags != null) foreach (var tag in tags) { if (tag.Type == JTokenType.String) value.Tags.Add(tag.Value<string>()); else result.Report.Add(Type, ValidationSeverity.Error, prefix + "/tags", "Tags must be strings.", tag); }
            var materialGuids = Array(root, "materialGuids", prefix + "/materialGuids", result.Report, true); if (materialGuids != null) foreach (var id in materialGuids) { if (id.Type == JTokenType.String) value.MaterialGuids.Add(id.Value<string>()); else result.Report.Add(Type, ValidationSeverity.Error, prefix + "/materialGuids", "Material GUIDs must be strings.", id); }
            var parameters = Object(root, "parameters", prefix + "/parameters", result.Report, true); if (parameters != null) foreach (var property in parameters.Properties()) value.Parameters[property.Name] = ParseParameter(property.Value as JObject, prefix + "/parameters/" + property.Name, result.Report);
            var cost = Object(root, "cost", prefix + "/cost", result.Report, true); if (cost != null) { CheckUnknown(cost, prefix + "/cost", result.Report, "estimatedPeakParticles", "particleSystems", "materials", "transparentRenderers"); value.Cost.EstimatedPeakParticles = Int(cost, "estimatedPeakParticles", prefix + "/cost/estimatedPeakParticles", result.Report, true); value.Cost.ParticleSystems = Int(cost, "particleSystems", prefix + "/cost/particleSystems", result.Report, true); value.Cost.Materials = Int(cost, "materials", prefix + "/cost/materials", result.Report, true); value.Cost.TransparentRenderers = Int(cost, "transparentRenderers", prefix + "/cost/transparentRenderers", result.Report, true); }
            return result;
        }

        private static S12SlashPhase ParsePhase(JObject source, string indexPath, ValidationReport report)
        {
            if (source == null) { report.Add(Type, ValidationSeverity.Error, indexPath, "Phase must be an object."); return null; }
            var id = String(source, "id", indexPath + "/id", report, true); var path = "/phases/" + (string.IsNullOrEmpty(id) ? "{invalid-phase}" : id);
            CheckUnknown(source, path, report, "id", "kind", "startTime", "duration", "enabled", "modules");
            var value = new S12SlashPhase { Id = id, Kind = String(source, "kind", path + "/kind", report, true), StartTime = Number(source, "startTime", path + "/startTime", report, true), Duration = Number(source, "duration", path + "/duration", report, true), Enabled = Bool(source, "enabled", path + "/enabled", report, true) };
            var modules = Array(source, "modules", path + "/modules", report, true); if (modules != null) for (var index = 0; index < modules.Count; index++) { var module = ParseModule(modules[index] as JObject, path, path + "/modules/" + index, report); if (module != null) value.Modules.Add(module); }
            return value;
        }

        private static S12SlashModule ParseModule(JObject source, string phasePath, string indexPath, ValidationReport report)
        {
            if (source == null) { report.Add(Type, ValidationSeverity.Error, indexPath, "Module must be an object."); return null; }
            var id = String(source, "id", indexPath + "/id", report, true); var path = phasePath + "/modules/" + (string.IsNullOrEmpty(id) ? "{invalid-module}" : id);
            CheckUnknown(source, path, report, "id", "kind", "templateId", "enabled", "parameters");
            var value = new S12SlashModule { Id = id, Kind = String(source, "kind", path + "/kind", report, true), TemplateId = String(source, "templateId", path + "/templateId", report, true), Enabled = Bool(source, "enabled", path + "/enabled", report, true) };
            var parameters = Object(source, "parameters", path + "/parameters", report, true); if (parameters != null) foreach (var property in parameters.Properties()) value.Parameters[property.Name] = property.Value.DeepClone();
            return value;
        }

        private static S12SlashParameter ParseParameter(JObject source, string path, ValidationReport report)
        {
            var value = new S12SlashParameter(); if (source == null) { report.Add(Type, ValidationSeverity.Error, path, "Parameter declaration must be an object."); return value; }
            CheckUnknown(source, path, report, "type", "min", "default", "max", "binding"); value.Type = String(source, "type", path + "/type", report, true); value.Min = Token(source, "min", path + "/min", report); value.Default = Token(source, "default", path + "/default", report); value.Max = Token(source, "max", path + "/max", report); value.Binding = String(source, "binding", path + "/binding", report, true); return value;
        }

        private static JObject ReadObject(string json, string path, ValidationReport report) { try { var token = JToken.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }); if (token is JObject) return (JObject)token; report.Add(Type, ValidationSeverity.Error, path, "Document root must be an object.", token); } catch (Exception exception) { report.Add("E1200", ValidationSeverity.Error, path, "Invalid JSON: " + exception.Message); } return null; }
        private static void CheckUnknown(JObject source, string path, ValidationReport report, params string[] allowed) { var set = new HashSet<string>(allowed, StringComparer.Ordinal); foreach (var property in source.Properties()) if (!set.Contains(property.Name)) report.Add(Unknown, ValidationSeverity.Error, Combine(path, property.Name), "Unknown v2 field is not allowed.", property.Value); }
        private static JToken RequiredToken(JObject source, string name, string path, ValidationReport report) { JToken token; if (!source.TryGetValue(name, out token)) report.Add(Required, ValidationSeverity.Error, path, "Required field is missing."); return token; }
        private static JToken Token(JObject source, string name, string path, ValidationReport report) { return RequiredToken(source, name, path, report); }
        private static string String(JObject source, string name, string path, ValidationReport report, bool required) { var token = required ? RequiredToken(source, name, path, report) : source[name]; if (token == null) return null; if (token.Type != JTokenType.String) { report.Add(Type, ValidationSeverity.Error, path, "Expected string.", token, "string"); return null; } return token.Value<string>(); }
        private static int Int(JObject source, string name, string path, ValidationReport report, bool required) { var token = required ? RequiredToken(source, name, path, report) : source[name]; if (token == null) return 0; if (token.Type != JTokenType.Integer) { report.Add(Type, ValidationSeverity.Error, path, "Expected integer.", token, "integer"); return 0; } return token.Value<int>(); }
        private static uint UInt(JObject source, string name, string path, ValidationReport report, bool required) { var token = required ? RequiredToken(source, name, path, report) : source[name]; uint value; if (token == null) return 0; if (token.Type != JTokenType.Integer || !uint.TryParse(Convert.ToString(((JValue)token).Value, CultureInfo.InvariantCulture), out value)) { report.Add(Type, ValidationSeverity.Error, path, "Expected uint32 integer.", token, "uint32"); return 0; } return value; }
        private static double Number(JObject source, string name, string path, ValidationReport report, bool required) { var token = required ? RequiredToken(source, name, path, report) : source[name]; double value; if (token == null) return 0; if ((token.Type != JTokenType.Integer && token.Type != JTokenType.Float) || !double.TryParse(Convert.ToString(((JValue)token).Value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) { report.Add(Type, ValidationSeverity.Error, path, "Expected number.", token, "number"); return 0; } if (double.IsNaN(value) || double.IsInfinity(value)) { report.Add(NonFinite, ValidationSeverity.Error, path, "Number must be finite.", token, "finite"); return 0; } return value; }
        private static bool Bool(JObject source, string name, string path, ValidationReport report, bool required) { var token = required ? RequiredToken(source, name, path, report) : source[name]; if (token == null) return false; if (token.Type != JTokenType.Boolean) { report.Add(Type, ValidationSeverity.Error, path, "Expected boolean.", token, "boolean"); return false; } return token.Value<bool>(); }
        private static JObject Object(JObject source, string name, string path, ValidationReport report, bool required) { var token = required ? RequiredToken(source, name, path, report) : source[name]; if (token == null) return null; var result = token as JObject; if (result == null) report.Add(Type, ValidationSeverity.Error, path, "Expected object.", token, "object"); return result; }
        private static JArray Array(JObject source, string name, string path, ValidationReport report, bool required) { var token = required ? RequiredToken(source, name, path, report) : source[name]; if (token == null) return null; var result = token as JArray; if (result == null) report.Add(Type, ValidationSeverity.Error, path, "Expected array.", token, "array"); return result; }
        private static string Combine(string path, string field) { return path == "/" ? "/" + field : path.TrimEnd('/') + "/" + field; }
    }

    public sealed class S12SlashTemplateCatalog
    {
        private readonly Dictionary<string, S12SlashManifest> manifests = new Dictionary<string, S12SlashManifest>(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, S12SlashManifest> ByTemplateId { get { return manifests; } }
        public ValidationReport Report = new ValidationReport();
        public bool TryGet(string id, out S12SlashManifest manifest) { return manifests.TryGetValue(id ?? string.Empty, out manifest); }

        public static S12SlashTemplateCatalog Load(string directory, IAssetReferenceResolver resolver)
        {
            var catalog = new S12SlashTemplateCatalog(); if (!Directory.Exists(directory)) { catalog.Report.Add("E1240", ValidationSeverity.Error, "/catalog", "S12 slash manifest directory does not exist."); return catalog; }
            foreach (var file in Directory.GetFiles(directory, "*.slash.manifest.json", SearchOption.TopDirectoryOnly).OrderBy(value => value, StringComparer.Ordinal))
            {
                var path = "/slashCatalog/" + Path.GetFileName(file); var parsed = S12SlashV2Parser.ParseManifest(File.ReadAllText(file), path); catalog.Report.AddRange(parsed.Report); if (parsed.Report.HasErrors) continue;
                var manifest = parsed.Value; var semantic = S12SlashV2Validator.ValidateManifestSemantic(manifest, path, resolver); catalog.Report.AddRange(semantic); if (semantic.HasErrors) continue;
                if (catalog.manifests.ContainsKey(manifest.TemplateId)) { catalog.Report.Add("E1241", ValidationSeverity.Error, path + "/templateId", "Slash template ID is duplicated."); continue; }
                if (resolver == null) { catalog.Report.Add("E1242", ValidationSeverity.Error, path + "/assetGuid", "Slash catalog requires an asset GUID resolver; unresolved manifests are never indexed."); continue; }
                AssetReferenceResolution resolved; try { resolved = resolver.Resolve(manifest.AssetGuid); } catch (Exception exception) { catalog.Report.Add("E1242", ValidationSeverity.Error, path + "/assetGuid", "Slash GUID resolver failed: " + exception.Message); continue; }
                if (resolved == null || !resolved.Found) { catalog.Report.Add("E1242", ValidationSeverity.Error, path + "/assetGuid", "Slash template GUID cannot be resolved."); continue; } if (!string.Equals(resolved.AssetPath, manifest.AssetPath, StringComparison.Ordinal)) { catalog.Report.Add("E1243", ValidationSeverity.Error, path + "/assetPath", "Slash manifest path does not match GUID resolution."); continue; }
                catalog.manifests.Add(manifest.TemplateId, manifest);
            }
            return catalog;
        }
    }

    public static class S12SlashV2Validator
    {
        private static readonly string[] PhaseOrder = { "anticipation", "primary_arc", "afterimage", "sparks", "dissipation" };
        private static readonly Dictionary<string, string> RoleModules = new Dictionary<string, string>(StringComparer.Ordinal) { { "anticipation", "anticipation_glint" }, { "primary_arc", "arc_sweep" }, { "afterimage", "arc_afterimage" }, { "sparks", "slash_sparks" }, { "dissipation", "slash_dissipation" } };

        public static ValidationReport Validate(string json, S12SlashTemplateCatalog catalog)
        {
            var parsed = S12SlashV2Parser.ParseRecipe(json); var report = parsed.Report; if (catalog != null) report.AddRange(catalog.Report); if (!report.HasErrors) report.AddRange(ValidateSemantic(parsed.Value, catalog)); return report;
        }

        public static ValidationReport ValidateSemantic(S12SlashRecipe recipe, S12SlashTemplateCatalog catalog)
        {
            var report = new ValidationReport(); if (recipe == null) { report.Add("E1210", ValidationSeverity.Error, "/", "Slash v2 recipe is missing."); return report; }
            if (recipe.RecipeVersion != 2) report.Add("E1210", ValidationSeverity.Error, "/recipeVersion", "Slash parser accepts only v2.", new JValue(recipe.RecipeVersion), "2");
            if (recipe.Revision < 1) report.Add("E1211", ValidationSeverity.Error, "/revision", "Revision must be >= 1.", new JValue(recipe.Revision), "integer >= 1");
            if (string.IsNullOrWhiteSpace(recipe.Id) || string.IsNullOrWhiteSpace(recipe.Name)) report.Add("E1211", ValidationSeverity.Error, "/id", "Slash id and name must be non-empty.");
            if (recipe.Dimension != "3d") report.Add("E1212", ValidationSeverity.Error, "/dimension", "Slash is 3d only.", new JValue(recipe.Dimension), "3d");
            if (recipe.Archetype != "slash") report.Add("E1212", ValidationSeverity.Error, "/archetype", "Slash v2 accepts only slash archetype.", new JValue(recipe.Archetype), "slash");
            if (recipe.TargetProfile != "mobile_medium" && recipe.TargetProfile != "pc_editor") report.Add("E1212", ValidationSeverity.Error, "/targetProfile", "Unsupported slash target profile.", new JValue(recipe.TargetProfile), "[mobile_medium, pc_editor]");
            if (!Finite(recipe.Timeline.Duration) || recipe.Timeline.Duration < .44 || recipe.Timeline.Duration > .55) report.Add("E1213", ValidationSeverity.Error, "/timeline/duration", "Slash timeline must be finite and within the reviewed 0.44–0.55 second envelope.", new JValue(recipe.Timeline.Duration), "[0.44, 0.55]");
            var phaseIds = new HashSet<string>(StringComparer.Ordinal); var moduleIds = new HashSet<string>(StringComparer.Ordinal); var byId = new Dictionary<string, S12SlashPhase>(StringComparer.Ordinal);
            foreach (var phase in recipe.Phases) if (!string.IsNullOrEmpty(phase.Id) && !byId.ContainsKey(phase.Id)) byId.Add(phase.Id, phase);
            if (recipe.Phases.Count != PhaseOrder.Length) report.Add("E1214", ValidationSeverity.Error, "/phases", "Slash v2 requires exactly five fixed sibling phases.", new JValue(recipe.Phases.Count), "5");
            for (var phaseIndex = 0; phaseIndex < recipe.Phases.Count; phaseIndex++)
            {
                var phase = recipe.Phases[phaseIndex];
                var path = "/phases/" + (string.IsNullOrEmpty(phase.Id) ? "{invalid-phase}" : phase.Id);
                if (string.IsNullOrWhiteSpace(phase.Id) || !SafeId(phase.Id) || !phaseIds.Add(phase.Id)) report.Add("E1216", ValidationSeverity.Error, path + "/id", "Phase IDs must be safe, stable and unique.");
                string expected; if (!RoleModules.TryGetValue(phase.Id ?? string.Empty, out expected) || phase.Kind != phase.Id) report.Add("E1214", ValidationSeverity.Error, path + "/kind", "Phase must use one known id/kind role pair.");
                if (phaseIndex >= PhaseOrder.Length || phase.Id != PhaseOrder[phaseIndex]) report.Add("E1214", ValidationSeverity.Error, path + "/id", "Slash phase array order is fixed and must follow the authored time story.", new JValue(phase.Id), phaseIndex < PhaseOrder.Length ? PhaseOrder[phaseIndex] : null);
                if (!phase.Enabled || phase.Modules.Count != 1) report.Add("E1214", ValidationSeverity.Error, path + "/modules", "Every required slash phase must be enabled and contain exactly one role module.", new JValue(phase.Modules.Count), "1");
                if (!Finite(phase.StartTime) || !Finite(phase.Duration) || phase.StartTime < 0 || phase.Duration <= 0 || phase.StartTime + phase.Duration > recipe.Timeline.Duration + .000001) report.Add("E1215", ValidationSeverity.Error, path, "Phase interval must be finite, positive, and contained by timeline.");
                foreach (var module in phase.Modules)
                {
                    var modulePath = path + "/modules/" + (string.IsNullOrWhiteSpace(module.Id) ? "{invalid-module}" : module.Id);
                    if (string.IsNullOrWhiteSpace(module.Id) || !SafeId(module.Id) || !moduleIds.Add(module.Id)) report.Add("E1216", ValidationSeverity.Error, modulePath + "/id", "Module IDs must be safe, stable and unique across slash recipe.");
                    if (!module.Enabled || module.Kind != expected) report.Add("E1217", ValidationSeverity.Error, modulePath + "/kind", "Phase module must be enabled and match its fixed role.", new JValue(module.Kind), expected);
                    ValidateTemplate(module, phase, modulePath, catalog, report);
                }
            }
            foreach (var id in PhaseOrder) if (!byId.ContainsKey(id)) report.Add("E1214", ValidationSeverity.Error, "/phases", "Required slash phase is missing: " + id);
            ValidateTimeStory(byId, recipe.Timeline.Duration, report);
            return report;
        }

        public static ValidationReport ValidateManifestSemantic(S12SlashManifest manifest, string path, IAssetReferenceResolver resolver)
        {
            var report = new ValidationReport(); if (manifest == null) { report.Add("E1244", ValidationSeverity.Error, path, "Slash Manifest is missing."); return report; }
            if (manifest.SlashManifestVersion != 2) report.Add("E1244", ValidationSeverity.Error, path + "/slashManifestVersion", "Slash Manifest version must be 2.", new JValue(manifest.SlashManifestVersion), "2");
            if (!SafeTemplateId(manifest.TemplateId) || string.IsNullOrWhiteSpace(manifest.TemplateVersion)) report.Add("E1244", ValidationSeverity.Error, path + "/templateId", "Slash template ID/version must be non-empty safe identifiers.");
            string expectedModule; if (!RoleModules.TryGetValue(manifest.PhaseKind ?? string.Empty, out expectedModule) || manifest.ModuleKind != expectedModule) report.Add("E1244", ValidationSeverity.Error, path + "/phaseKind", "Manifest phase/module mapping is not a fixed S12 role pair.");
            if (manifest.Dimension != "3d") report.Add("E1244", ValidationSeverity.Error, path + "/dimension", "Slash Manifest dimension must be 3d.");
            if (!Guid(manifest.AssetGuid)) report.Add("E1244", ValidationSeverity.Error, path + "/assetGuid", "Slash asset GUID must be 32 hexadecimal characters.");
            var expectedPath = "Assets/VFX/Templates/3D/Slash/Prefabs/" + manifest.TemplateId + ".prefab"; if (!string.Equals(manifest.AssetPath, expectedPath, StringComparison.Ordinal)) report.Add("E1244", ValidationSeverity.Error, path + "/assetPath", "Slash prefab path must remain in the protected formal Slash boundary.", new JValue(manifest.AssetPath), expectedPath);
            if (manifest.Cost.EstimatedPeakParticles < 0 || manifest.Cost.ParticleSystems < 0 || manifest.Cost.Materials < 0 || manifest.Cost.TransparentRenderers < 0) report.Add("E1244", ValidationSeverity.Error, path + "/cost", "Slash costs must be non-negative.");
            if (manifest.MaterialGuids.Count != manifest.MaterialGuids.Distinct(StringComparer.Ordinal).Count() || manifest.MaterialGuids.Any(value => !Guid(value))) report.Add("E1244", ValidationSeverity.Error, path + "/materialGuids", "Slash material GUIDs must be valid and unique within Manifest.");
            if (manifest.Cost.Materials != manifest.MaterialGuids.Count) report.Add("E1244", ValidationSeverity.Error, path + "/cost/materials", "Manifest material count must equal its explicit materialGuids count.");
            if (resolver == null) report.Add("E1244", ValidationSeverity.Error, path + "/materialGuids", "Slash Manifest semantic validation requires a GUID resolver.");
            else foreach (var materialGuid in manifest.MaterialGuids) { try { var material = resolver.Resolve(materialGuid); if (material == null || !material.Found || !material.AssetPath.StartsWith("Assets/VFX/Templates/3D/Slash/Materials/", StringComparison.Ordinal) || !material.AssetPath.EndsWith(".mat", StringComparison.Ordinal)) report.Add("E1244", ValidationSeverity.Error, path + "/materialGuids", "Material GUID must resolve to a formal Slash .mat asset.", new JValue(materialGuid)); } catch (Exception exception) { report.Add("E1244", ValidationSeverity.Error, path + "/materialGuids", "Material GUID resolver failed: " + exception.Message, new JValue(materialGuid)); } }
            var registry = S12SlashBindingRegistry.CreateFormal(); Dictionary<string, string> expectedBindings;
            if (!ExpectedBindings.TryGetValue(manifest.ModuleKind ?? string.Empty, out expectedBindings)) expectedBindings = new Dictionary<string, string>(StringComparer.Ordinal);
            if (manifest.Parameters.Count != expectedBindings.Count || manifest.Parameters.Keys.Except(expectedBindings.Keys, StringComparer.Ordinal).Any() || expectedBindings.Keys.Except(manifest.Parameters.Keys, StringComparer.Ordinal).Any()) report.Add("E1244", ValidationSeverity.Error, path + "/parameters", "Manifest parameter set must exactly match its fixed S12 module contract.");
            foreach (var parameter in manifest.Parameters)
            {
                var parameterPath = path + "/parameters/" + parameter.Key; var declaration = parameter.Value; double min; double value; double max;
                if (!SafeId(parameter.Key) || (declaration.Type != "float" && declaration.Type != "integer")) report.Add("E1244", ValidationSeverity.Error, parameterPath, "Parameter name/type is not in the closed S12 contract.");
                if (!TryFinite(declaration.Min, out min) || !TryFinite(declaration.Default, out value) || !TryFinite(declaration.Max, out max) || min > value || value > max) report.Add("E1244", ValidationSeverity.Error, parameterPath, "Parameter min/default/max must be finite and ordered.");
                if (declaration.Type == "integer" && (declaration.Min == null || declaration.Min.Type != JTokenType.Integer || declaration.Default == null || declaration.Default.Type != JTokenType.Integer || declaration.Max == null || declaration.Max.Type != JTokenType.Integer)) report.Add("E1244", ValidationSeverity.Error, parameterPath, "Integer parameter bounds must be integers.");
                string expectedBinding; if (!expectedBindings.TryGetValue(parameter.Key, out expectedBinding) || declaration.Binding != expectedBinding || !registry.Contains(declaration.Binding)) report.Add("E1244", ValidationSeverity.Error, parameterPath + "/binding", "Manifest binding must exactly match the fixed module parameter symbol.", new JValue(declaration.Binding), expectedBinding);
            }
            return report;
        }

        private static void ValidateTimeStory(Dictionary<string, S12SlashPhase> phases, double timeline, ValidationReport report)
        {
            S12SlashPhase anticipation; S12SlashPhase primary; S12SlashPhase after; S12SlashPhase sparks; S12SlashPhase dissipation;
            if (!phases.TryGetValue("anticipation", out anticipation) || !phases.TryGetValue("primary_arc", out primary) || !phases.TryGetValue("afterimage", out after) || !phases.TryGetValue("sparks", out sparks) || !phases.TryGetValue("dissipation", out dissipation)) return;
            if (Math.Abs(anticipation.StartTime) > .0001) report.Add("E1215", ValidationSeverity.Error, "/phases/anticipation/startTime", "Anticipation must start at 0.");
            if (!(anticipation.StartTime < primary.StartTime && primary.StartTime < after.StartTime && after.StartTime < sparks.StartTime && sparks.StartTime < dissipation.StartTime)) report.Add("E1215", ValidationSeverity.Error, "/phases", "Slash phase starts must be strictly ordered in the fixed time story.");
            if (after.StartTime >= primary.StartTime + primary.Duration || sparks.StartTime >= primary.StartTime + primary.Duration) report.Add("E1215", ValidationSeverity.Error, "/phases", "Afterimage and sparks must overlap the primary arc.");
            if (Math.Abs(dissipation.StartTime - (primary.StartTime + primary.Duration)) > .025) report.Add("E1215", ValidationSeverity.Error, "/phases/dissipation/startTime", "Dissipation must begin at primary completion (within 0.025 s).");
            if (Math.Abs(dissipation.StartTime + dissipation.Duration - timeline) > .0001) report.Add("E1215", ValidationSeverity.Error, "/phases/dissipation/duration", "Final dissipation must end exactly at timeline duration.");
        }

        private static void ValidateTemplate(S12SlashModule module, S12SlashPhase phase, string path, S12SlashTemplateCatalog catalog, ValidationReport report)
        {
            if (catalog == null) { report.Add("E1218", ValidationSeverity.Error, path + "/templateId", "Slash template catalog is required."); return; }
            S12SlashManifest manifest; if (!catalog.TryGet(module.TemplateId, out manifest)) { report.Add("E1218", ValidationSeverity.Error, path + "/templateId", "Slash template is not in the isolated v2 catalog.", new JValue(module.TemplateId)); return; }
            if (manifest.Dimension != "3d" || manifest.PhaseKind != phase.Kind || manifest.ModuleKind != module.Kind) report.Add("E1218", ValidationSeverity.Error, path + "/templateId", "Slash template phase/module/dimension does not match its module.");
            foreach (var parameter in module.Parameters)
            {
                S12SlashParameter declaration; var parameterPath = path + "/parameters/" + parameter.Key; if (!manifest.Parameters.TryGetValue(parameter.Key, out declaration)) { report.Add("E1219", ValidationSeverity.Error, parameterPath, "Parameter is not declared by slash Manifest."); continue; }
                ValidateParameter(parameter.Value, declaration, parameterPath, report);
            }
            foreach (var declaration in manifest.Parameters) if (!module.Parameters.ContainsKey(declaration.Key)) report.Add("E1219", ValidationSeverity.Error, path + "/parameters/" + declaration.Key, "Required slash Manifest parameter is missing.");
        }

        private static void ValidateParameter(JToken actual, S12SlashParameter declaration, string path, ValidationReport report)
        {
            if (declaration.Type != "float" && declaration.Type != "integer") { report.Add("E1220", ValidationSeverity.Error, path, "Unsupported slash parameter declaration type."); return; }
            if ((declaration.Type == "integer" && actual.Type != JTokenType.Integer) || (declaration.Type == "float" && actual.Type != JTokenType.Integer && actual.Type != JTokenType.Float)) { report.Add("E1220", ValidationSeverity.Error, path, "Slash parameter type does not match Manifest.", actual, declaration.Type); return; }
            double value; double min; double max; if (!TryFinite(actual, out value) || !TryFinite(declaration.Min, out min) || !TryFinite(declaration.Max, out max)) { report.Add("E1220", ValidationSeverity.Error, path, "Slash parameter and Manifest bounds must be finite."); return; }
            if (value < min || value > max) report.Add("E1220", ValidationSeverity.Error, path, "Slash parameter is outside inclusive Manifest range.", actual, "[" + min.ToString(CultureInfo.InvariantCulture) + ", " + max.ToString(CultureInfo.InvariantCulture) + "]");
        }

        private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        private static bool SafeId(string value) { if (string.IsNullOrEmpty(value) || value.Length > 64 || !char.IsLower(value[0])) return false; foreach (var character in value) if (!(char.IsLower(character) || char.IsDigit(character) || character == '_')) return false; return true; }
        private static bool SafeTemplateId(string value) { if (string.IsNullOrEmpty(value) || !value.StartsWith("PFT_3D_Slash", StringComparison.Ordinal)) return false; foreach (var character in value) if (!(char.IsLetterOrDigit(character) || character == '_')) return false; return true; }
        private static bool Guid(string value) { if (string.IsNullOrEmpty(value) || value.Length != 32) return false; foreach (var character in value) if (!Uri.IsHexDigit(character)) return false; return true; }
        private static readonly Dictionary<string, Dictionary<string, string>> ExpectedBindings = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
        {
            { "anticipation_glint", new Dictionary<string, string>(StringComparer.Ordinal) },
            { "arc_sweep", new Dictionary<string, string>(StringComparer.Ordinal) { { "scale", "3d.slash.arc.scale" }, { "width", "3d.slash.arc.width" }, { "duration", "3d.slash.arc.duration" } } },
            { "arc_afterimage", new Dictionary<string, string>(StringComparer.Ordinal) { { "count", "3d.slash.afterimage.count" }, { "alpha", "3d.slash.afterimage.alpha" } } },
            { "slash_sparks", new Dictionary<string, string>(StringComparer.Ordinal) { { "count", "3d.slash.sparks.count" }, { "speed", "3d.slash.sparks.speed" }, { "lifetime", "3d.slash.sparks.lifetime" } } },
            { "slash_dissipation", new Dictionary<string, string>(StringComparer.Ordinal) { { "lifetime", "3d.slash.dissipation.lifetime" } } }
        };
        private static bool TryFinite(JToken token, out double value) { value = 0; try { if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)) return false; value = Convert.ToDouble(((JValue)token).Value, CultureInfo.InvariantCulture); return Finite(value); } catch { return false; } }
    }

    public static class S12SlashBudgetCalculator
    {
        public static ValidationReport Evaluate(S12SlashRecipe recipe, S12SlashTemplateCatalog catalog)
        {
            var report = new ValidationReport(); if (recipe == null || catalog == null) { report.Add("E1230", ValidationSeverity.Error, "/budget", "Slash recipe and catalog are required."); return report; }
            var particles = 0; var systems = 0; var materials = new HashSet<string>(StringComparer.Ordinal); var renderers = 0;
            foreach (var phase in recipe.Phases.Where(item => item.Enabled)) foreach (var module in phase.Modules.Where(item => item.Enabled)) { S12SlashManifest manifest; if (!catalog.TryGet(module.TemplateId, out manifest)) continue; particles += manifest.Cost.EstimatedPeakParticles; systems += manifest.Cost.ParticleSystems; foreach (var material in manifest.MaterialGuids) materials.Add(material); renderers += manifest.Cost.TransparentRenderers; }
            Add(report, "/budget/estimatedPeakParticles", "peak particles", particles, 48); Add(report, "/budget/particleSystems", "particle systems", systems, 4); Add(report, "/budget/materials", "unique VFX materials", materials.Count, 5); Add(report, "/budget/transparentRenderers", "transparent renderers", renderers, 7); report.Add("I1230", ValidationSeverity.Info, "/budget", "S12 static budget evaluated for mobile_medium and pc_editor; unique materials derive from resolved materialGuids."); return report;
        }
        private static void Add(ValidationReport report, string path, string label, int actual, int maximum) { if (actual > maximum) report.Add("E1231", ValidationSeverity.Error, path, "Slash " + label + " exceeds S12 budget.", new JValue(actual), "[0, " + maximum + "]"); }
    }
}
