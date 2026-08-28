using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using VFXComposer.Editor.Catalog;
using VFXComposer.Editor.Capabilities;
using VFXComposer.Editor.Domain;

namespace VFXComposer.Editor.Validation
{
    public static class RecipeValidator
    {
        public static ValidationReport Validate(string recipeJson, TemplateCatalog catalog)
        {
            var parsed = VfxDomainParser.ParseRecipe(recipeJson);
            var report = parsed.Report;
            if (catalog != null) report.AddRange(catalog.Report);
            if (!report.HasErrors) { report.AddRange(ValidateSemantic(parsed.Value, catalog)); report.AddRange(ArchetypeParameterRegistry.Validate(parsed.Value)); report.AddRange(ContentParameterRegistry.Validate(parsed.Value)); report.AddRange(CapabilityRegistry.Validate(parsed.Value)); report.AddRange(CapabilitySlotValidator.Validate(parsed.Value)); }
            return report;
        }

        public static ValidationReport ValidateSemantic(Recipe recipe, TemplateCatalog catalog)
        {
            var report = new ValidationReport();
            if (recipe == null) { report.Add("E300", ValidationSeverity.Error, "/", "Recipe is missing."); return report; }
            if (recipe.RecipeVersion != 1) report.Add("E301", ValidationSeverity.Error, "/recipeVersion", "Recipe version is not supported.", new JValue(recipe.RecipeVersion), "1");
            if (recipe.Revision < 1) report.Add("E316", ValidationSeverity.Error, "/revision", "Recipe revision must be an integer greater than or equal to 1.", new JValue(recipe.Revision), "integer >= 1");
            if (string.IsNullOrWhiteSpace(recipe.Id)) report.Add("E302", ValidationSeverity.Error, "/id", "Recipe ID must not be empty.");
            if (recipe.Stages.Count == 0) report.Add("E302", ValidationSeverity.Error, "/stages", "Recipe must contain at least one stage.");
            var stageIds = new HashSet<string>(StringComparer.Ordinal);
            var moduleIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var stage in recipe.Stages)
            {
                var stagePath = VfxDomainParser.StagePath(stage.Id);
                if (string.IsNullOrWhiteSpace(stage.Id) || !stageIds.Add(stage.Id)) report.Add("E303", ValidationSeverity.Error, stagePath + "/id", "Stage ID must be unique.");
                if (!IsFinite(stage.Duration) || stage.Duration < 0) report.Add("E304", ValidationSeverity.Error, stagePath + "/duration", "Stage duration must be finite and non-negative.", new JValue(stage.Duration), "[0, +inf), finite");
                foreach (var module in stage.Modules)
                {
                    var modulePath = VfxDomainParser.ModulePath(stagePath, module.Id);
                    if (string.IsNullOrWhiteSpace(module.Id) || !moduleIds.Add(module.Id)) report.Add("E305", ValidationSeverity.Error, modulePath + "/id", "Module ID must be unique across the recipe.");
                    ValidateModule(module, modulePath, recipe.Dimension, catalog, report);
                    if (!string.IsNullOrEmpty(module.AttachTo) && !HasModule(stage, module.AttachTo)) report.Add("E306", ValidationSeverity.Error, modulePath + "/attachTo", "attachTo must reference a module ID in the same stage.", new JValue(module.AttachTo));
                    else if (string.Equals(module.AttachTo, module.Id, System.StringComparison.Ordinal)) report.Add("E306", ValidationSeverity.Error, modulePath + "/attachTo", "attachTo must not reference the module itself.", new JValue(module.AttachTo));
                }
                ValidateAttachmentCycles(stage, report);
            }
            ValidateComposite(recipe, report);
            return report;
        }

        private static void ValidateComposite(Recipe recipe, ValidationReport report)
        {
            var composite = recipe.Composite;
            if (recipe.Archetype != RecipeArchetype.Composite)
            {
                if (composite != null && composite.IsDeclared) report.Add("E1850", ValidationSeverity.Error, "/timeline", "Composite orchestration fields are only legal for the composite archetype.");
                return;
            }
            if (composite == null || composite.Timeline.Count == 0) { report.Add("E1851", ValidationSeverity.Error, "/timeline", "Composite Recipe requires at least one timeline event."); return; }
            double last = -1; var refs = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < composite.Timeline.Count; i++)
            {
                var item = composite.Timeline[i]; var path = "/timeline/" + i;
                if (!IsFinite(item.Time) || item.Time < 0 || item.Time < last) report.Add("E1852", ValidationSeverity.Error, path + "/t", "Timeline time must be finite, non-negative, and ordered.", new JValue(item.Time)); last = item.Time;
                if (string.IsNullOrWhiteSpace(item.RefId)) report.Add("E1852", ValidationSeverity.Error, path + "/ref_id", "Timeline Runtime Entry reference is required."); else refs.Add(item.RefId);
                if (item.Action != "play" && item.Action != "stop") report.Add("E1852", ValidationSeverity.Error, path + "/action", "Timeline action must be play or stop.", new JValue(item.Action), "[play, stop]");
                foreach (var pair in item.Overrides) if (pair.Key != "palette" && pair.Key != "scale" && pair.Key != "position" && pair.Key != "rotation") report.Add("E1853", ValidationSeverity.Error, path + "/overrides/" + pair.Key, "Only semantic palette, scale, position, and rotation overrides are legal in v1 composition.", pair.Value);
            }
            last = -1;
            for (var i = 0; i < composite.CameraHints.Count; i++)
            {
                var item = composite.CameraHints[i]; var path = "/camera_hints/" + i;
                if (!IsFinite(item.Time) || item.Time < 0 || item.Time < last || !IsFinite(item.Strength) || item.Strength < 0 || item.Strength > 1) report.Add("E1854", ValidationSeverity.Error, path, "Camera hint must be ordered with finite t and strength in [0,1]."); last = item.Time;
                if (item.Type != "shake" && item.Type != "zoom" && item.Type != "slowmo") report.Add("E1854", ValidationSeverity.Error, path + "/type", "Camera hint type is unsupported.", new JValue(item.Type), "[shake, zoom, slowmo]");
            }
            last = -1; var gateIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < composite.Gates.Count; i++)
            {
                var item = composite.Gates[i]; var path = "/gates/" + i;
                if (!IsFinite(item.Time) || item.Time < 0 || item.Time < last) report.Add("E1855", ValidationSeverity.Error, path + "/t", "Gate times must be finite, non-negative, and ordered."); last = item.Time;
                if (string.IsNullOrWhiteSpace(item.WaitFor) || !gateIds.Add(item.WaitFor)) report.Add("E1855", ValidationSeverity.Error, path + "/wait_for", "Gate external-event ids must be non-empty and unique.", new JValue(item.WaitFor));
            }
        }

        private static void ValidateModule(RecipeModule module, string modulePath, RecipeDimension recipeDimension, TemplateCatalog catalog, ValidationReport report)
        {
            if (catalog == null) { report.Add("E307", ValidationSeverity.Error, modulePath + "/templateId", "A template catalog is required for semantic validation."); return; }
            TemplateManifest manifest;
            if (!catalog.TryGet(module.TemplateId ?? string.Empty, out manifest))
            {
                report.Add("E308", ValidationSeverity.Error, modulePath + "/templateId", "Template ID does not exist in the catalog.", new JValue(module.TemplateId), TemplateIdAllowList(catalog));
                return;
            }
            if (manifest.Kind != module.Kind) report.Add("E309", ValidationSeverity.Error, modulePath + "/kind", "Module kind does not match its template manifest.");
            if (manifest.Dimension != recipeDimension) report.Add("E310", ValidationSeverity.Error, modulePath + "/templateId", "Template dimension does not match recipe dimension.");
            foreach (var parameter in module.Parameters)
            {
                ManifestParameter declaration;
                var parameterPath = modulePath + "/parameters/" + parameter.Key;
                if (!manifest.Parameters.TryGetValue(parameter.Key, out declaration))
                {
                    report.Add("E311", ValidationSeverity.Error, parameterPath, "Parameter is not declared by the template manifest.", parameter.Value);
                    continue;
                }
                ValidateParameter(parameter.Value, declaration, parameterPath, report);
            }
            foreach (var declaration in manifest.Parameters)
            {
                if (!module.Parameters.ContainsKey(declaration.Key)) report.Add("E312", ValidationSeverity.Error, modulePath + "/parameters/" + declaration.Key, "Required manifest parameter is missing.");
            }
        }

        private static void ValidateParameter(JToken actual, ManifestParameter declaration, string path, ValidationReport report)
        {
            if (!HasExpectedType(actual, declaration.Type))
            {
                report.Add("E313", ValidationSeverity.Error, path, "Parameter value has the wrong type.", actual, AllowedType(declaration.Type));
                return;
            }
            if (declaration.Type == ManifestParameterType.Float || declaration.Type == ManifestParameterType.Integer)
            {
                double number;
                double min;
                double max;
                if (!TryFinite(actual, out number)) { report.Add("E315", ValidationSeverity.Error, path, "Numeric parameter value must be finite.", actual, "finite number"); return; }
                if (!TryFinite(declaration.Min, out min) || !TryFinite(declaration.Max, out max)) { report.Add("E315", ValidationSeverity.Error, path, "Manifest numeric bounds are invalid."); return; }
                if (number < min || number > max) report.Add("E314", ValidationSeverity.Error, path, "Parameter value is outside the inclusive manifest range.", actual, "[" + min.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", " + max.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]");
            }
        }

        private static bool HasExpectedType(JToken token, ManifestParameterType type)
        {
            if (token == null) return false;
            switch (type)
            {
                case ManifestParameterType.Float: return token.Type == JTokenType.Float || token.Type == JTokenType.Integer;
                case ManifestParameterType.Integer: return token.Type == JTokenType.Integer;
                case ManifestParameterType.Boolean: return token.Type == JTokenType.Boolean;
                case ManifestParameterType.String: return token.Type == JTokenType.String;
                default: return false;
            }
        }

        private static string AllowedType(ManifestParameterType type) { return type.ToString().ToLowerInvariant(); }
        private static bool IsFinite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        private static bool TryFinite(JToken token, out double value)
        {
            value = 0;
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)) return false;
            try { value = System.Convert.ToDouble(((JValue)token).Value, System.Globalization.CultureInfo.InvariantCulture); return IsFinite(value); }
            catch (System.Exception) { return false; }
        }
        private static bool HasModule(RecipeStage stage, string id) { foreach (var module in stage.Modules) if (module.Id == id) return true; return false; }
        private static void ValidateAttachmentCycles(RecipeStage stage, ValidationReport report)
        {
            var byId = new Dictionary<string, RecipeModule>(System.StringComparer.Ordinal);
            foreach (var module in stage.Modules) if (!string.IsNullOrEmpty(module.Id) && !byId.ContainsKey(module.Id)) byId.Add(module.Id, module);
            foreach (var module in stage.Modules)
            {
                if (string.Equals(module.AttachTo, module.Id, System.StringComparison.Ordinal)) continue;
                var seen = new HashSet<string>(System.StringComparer.Ordinal) { module.Id ?? string.Empty }; var current = module;
                while (current != null && !string.IsNullOrEmpty(current.AttachTo) && byId.TryGetValue(current.AttachTo, out current))
                {
                    if (!seen.Add(current.Id ?? string.Empty)) { report.Add("E306", ValidationSeverity.Error, VfxDomainParser.ModulePath(VfxDomainParser.StagePath(stage.Id), module.Id) + "/attachTo", "attachTo must not form a cycle within a stage.", new JValue(module.AttachTo)); break; }
                }
            }
        }
        private static string TemplateIdAllowList(TemplateCatalog catalog) { return catalog == null ? null : "[" + string.Join(", ", catalog.ByTemplateId.Keys.OrderBy(value => value, System.StringComparer.Ordinal).ToArray()) + "]"; }
    }
}
