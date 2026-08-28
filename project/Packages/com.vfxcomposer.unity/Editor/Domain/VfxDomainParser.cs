using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VFXComposer.Editor.Domain
{
    public static class VfxDomainParser
    {
        private const string UnknownField = "E100";
        private const string RequiredField = "E101";
        private const string InvalidType = "E102";
        private const string InvalidEnum = "E103";
        private const string NonFiniteNumber = "E105";

        public static ParseResult<Recipe> ParseRecipe(string json)
        {
            var result = new ParseResult<Recipe> { Value = new Recipe() };
            JObject root = ReadObject(json, "/", result.Report);
            if (root == null) return result;
            CheckUnknown(root, "/", result.Report, "recipeVersion", "revision", "id", "name", "dimension", "archetype", "style", "behavior", "content", "archetypeParameters", "targetProfile", "randomSeed", "stages", "timeline", "camera_hints", "gates", "metadata");
            result.Value.RecipeVersion = ReadInt(root, "recipeVersion", "/recipeVersion", result.Report, true);
            result.Value.Revision = ReadInt(root, "revision", "/revision", result.Report, false);
            if (!root.ContainsKey("revision")) result.Value.Revision = 1;
            result.Value.Id = ReadString(root, "id", "/id", result.Report, true);
            result.Value.Name = ReadString(root, "name", "/name", result.Report, false);
            result.Value.Dimension = ReadEnum(root, "dimension", "/dimension", result.Report, true, ParseDimension, DimensionValues);
            result.Value.Archetype = ReadEnum(root, "archetype", "/archetype", result.Report, true, ParseArchetype, ArchetypeValues);
            result.Value.Style = ParseStyleContract(root["style"], result.Report);
            result.Value.Behavior = ParseBehaviorContract(root["behavior"], result.Report);
            result.Value.Content = ParseContentContract(root["content"], result.Report);
            var archetypeParameters = ReadPropertyObject(root, "archetypeParameters", "/archetypeParameters", result.Report, false);
            if (archetypeParameters != null) foreach (var property in archetypeParameters.Properties()) result.Value.ArchetypeParameters[property.Name] = property.Value.DeepClone();
            result.Value.TargetProfile = ReadEnum(root, "targetProfile", "/targetProfile", result.Report, true, ParseProfile, ProfileValues);
            result.Value.RandomSeed = ReadUInt(root, "randomSeed", "/randomSeed", result.Report, true);
            var stages = ReadArray(root, "stages", "/stages", result.Report, true);
            if (stages != null)
            {
                foreach (var token in stages)
                {
                    var stage = ParseStage(token as JObject, result.Report);
                    if (stage != null) result.Value.Stages.Add(stage);
                }
            }
            ParseCompositeContract(root, result.Value.Composite, result.Report);
            var metadata = ReadPropertyObject(root, "metadata", "/metadata", result.Report, true);
            if (metadata != null)
            {
                CheckUnknown(metadata, "/metadata", result.Report, "createdBy", "templateCatalogVersion");
                result.Value.Metadata.CreatedBy = ReadString(metadata, "createdBy", "/metadata/createdBy", result.Report, true);
                result.Value.Metadata.TemplateCatalogVersion = ReadString(metadata, "templateCatalogVersion", "/metadata/templateCatalogVersion", result.Report, true);
            }
            return result;
        }

        private static void ParseCompositeContract(JObject root, RecipeCompositeContract value, ValidationReport report)
        {
            var timeline = ReadArray(root, "timeline", "/timeline", report, false);
            if (timeline != null) foreach (var token in timeline)
            {
                var obj = token as JObject; if (obj == null) { report.Add(InvalidType, ValidationSeverity.Error, "/timeline", "Timeline entries must be objects.", token, "object"); continue; }
                CheckUnknown(obj, "/timeline", report, "t", "ref_id", "action", "overrides");
                var item = new RecipeTimelineEvent { Time = ReadNumber(obj, "t", "/timeline/t", report, true), RefId = ReadString(obj, "ref_id", "/timeline/ref_id", report, true), Action = ReadString(obj, "action", "/timeline/action", report, true) };
                var overrides = ReadPropertyObject(obj, "overrides", "/timeline/overrides", report, false); if (overrides != null) foreach (var property in overrides.Properties()) item.Overrides[property.Name] = property.Value.DeepClone();
                value.Timeline.Add(item);
            }
            var hints = ReadArray(root, "camera_hints", "/camera_hints", report, false);
            if (hints != null) foreach (var token in hints)
            {
                var obj = token as JObject; if (obj == null) { report.Add(InvalidType, ValidationSeverity.Error, "/camera_hints", "Camera hint entries must be objects.", token, "object"); continue; }
                CheckUnknown(obj, "/camera_hints", report, "t", "type", "strength");
                value.CameraHints.Add(new RecipeCameraHint { Time = ReadNumber(obj, "t", "/camera_hints/t", report, true), Type = ReadString(obj, "type", "/camera_hints/type", report, true), Strength = ReadNumber(obj, "strength", "/camera_hints/strength", report, true) });
            }
            var gates = ReadArray(root, "gates", "/gates", report, false);
            if (gates != null) foreach (var token in gates)
            {
                var obj = token as JObject; if (obj == null) { report.Add(InvalidType, ValidationSeverity.Error, "/gates", "Gate entries must be objects.", token, "object"); continue; }
                CheckUnknown(obj, "/gates", report, "t", "wait_for");
                value.Gates.Add(new RecipeStageGate { Time = ReadNumber(obj, "t", "/gates/t", report, true), WaitFor = ReadString(obj, "wait_for", "/gates/wait_for", report, true) });
            }
        }

        public static ParseResult<TemplateManifest> ParseManifest(string json, string sourcePath = null)
        {
            var result = new ParseResult<TemplateManifest> { Value = new TemplateManifest() };
            JObject root = ReadObject(json, sourcePath ?? "/", result.Report);
            if (root == null) return result;
            var prefix = string.IsNullOrEmpty(sourcePath) ? string.Empty : sourcePath.TrimEnd('/');
            CheckUnknown(root, prefix, result.Report, "manifestVersion", "templateId", "templateVersion", "kind", "dimension", "assetGuid", "assetPath", "tags", "parameters", "cost");
            var value = result.Value;
            value.ManifestVersion = ReadInt(root, "manifestVersion", prefix + "/manifestVersion", result.Report, true);
            value.TemplateId = ReadString(root, "templateId", prefix + "/templateId", result.Report, true);
            value.TemplateVersion = ReadString(root, "templateVersion", prefix + "/templateVersion", result.Report, true);
            value.Kind = ReadEnum(root, "kind", prefix + "/kind", result.Report, true, ParseModuleKind, ModuleKindValues);
            value.Dimension = ReadEnum(root, "dimension", prefix + "/dimension", result.Report, true, ParseDimension, DimensionValues);
            value.AssetGuid = ReadString(root, "assetGuid", prefix + "/assetGuid", result.Report, true);
            value.AssetPath = ReadString(root, "assetPath", prefix + "/assetPath", result.Report, true);
            var tags = ReadArray(root, "tags", prefix + "/tags", result.Report, true);
            if (tags != null) foreach (var tag in tags) if (tag.Type == JTokenType.String) value.Tags.Add((string)tag); else result.Report.Add(InvalidType, ValidationSeverity.Error, prefix + "/tags", "Tags must contain strings.", tag, "string");
            var parameters = ReadPropertyObject(root, "parameters", prefix + "/parameters", result.Report, true);
            if (parameters != null) foreach (var property in parameters.Properties()) value.Parameters[property.Name] = ParseManifestParameter(property.Value as JObject, prefix + "/parameters/" + property.Name, result.Report);
            var cost = ReadPropertyObject(root, "cost", prefix + "/cost", result.Report, true);
            if (cost != null)
            {
                CheckUnknown(cost, prefix + "/cost", result.Report, "estimatedPeakParticles", "materials", "trails");
                value.Cost.EstimatedPeakParticles = ReadInt(cost, "estimatedPeakParticles", prefix + "/cost/estimatedPeakParticles", result.Report, true);
                value.Cost.Materials = ReadInt(cost, "materials", prefix + "/cost/materials", result.Report, true);
                value.Cost.Trails = ReadInt(cost, "trails", prefix + "/cost/trails", result.Report, true);
            }
            return result;
        }

        private static RecipeStage ParseStage(JObject stage, ValidationReport report)
        {
            if (stage == null) { report.Add(InvalidType, ValidationSeverity.Error, "/stages", "Each stage must be an object.", null, "object"); return null; }
            var value = new RecipeStage();
            value.Id = ReadString(stage, "id", StagePath(null) + "/id", report, true);
            var path = StagePath(value.Id);
            CheckUnknown(stage, path, report, "id", "trigger", "duration", "modules", "enabled");
            value.Trigger = ReadEnum(stage, "trigger", path + "/trigger", report, true, ParseTrigger, TriggerValues);
            value.Duration = ReadNumber(stage, "duration", path + "/duration", report, true);
            value.Enabled = ReadBoolean(stage, "enabled", path + "/enabled", report, true);
            var modules = ReadArray(stage, "modules", path + "/modules", report, true);
            if (modules != null) foreach (var token in modules) { var module = ParseModule(token as JObject, path, report); if (module != null) value.Modules.Add(module); }
            return value;
        }

        private static RecipeModule ParseModule(JObject module, string stagePath, ValidationReport report)
        {
            if (module == null) { report.Add(InvalidType, ValidationSeverity.Error, stagePath + "/modules", "Each module must be an object.", null, "object"); return null; }
            var value = new RecipeModule();
            value.Id = ReadString(module, "id", ModulePath(stagePath, null) + "/id", report, true);
            var path = ModulePath(stagePath, value.Id);
            CheckUnknown(module, path, report, "id", "kind", "templateId", "parameters", "attachTo", "enabled");
            value.Kind = ReadEnum(module, "kind", path + "/kind", report, true, ParseModuleKind, ModuleKindValues);
            value.TemplateId = ReadString(module, "templateId", path + "/templateId", report, true);
            value.AttachTo = ReadString(module, "attachTo", path + "/attachTo", report, false);
            value.Enabled = ReadBoolean(module, "enabled", path + "/enabled", report, true);
            var parameters = ReadPropertyObject(module, "parameters", path + "/parameters", report, true);
            if (parameters != null) foreach (var property in parameters.Properties()) value.Parameters[property.Name] = property.Value.DeepClone();
            return value;
        }

        private static RecipeStyleContract ParseStyleContract(JToken token, ValidationReport report)
        {
            if (token == null) return null;
            var result = new RecipeStyleContract();
            if (token.Type == JTokenType.String)
            {
                result.UsedLegacyStringForm = true;
                result.Token = (string)token;
                if (result.Token != "stylized") report.Add(InvalidEnum, ValidationSeverity.Error, "/style", "Legacy style string only supports stylized.", token, "[stylized]");
                return result;
            }
            var obj = token as JObject;
            if (obj == null) { TypeError("/style", token, "object or legacy string", report); return result; }
            CheckUnknown(obj, "/style", report, "token", "palette", "outline", "shading_steps", "noise_scale", "glow_strength", "snap_fps", "palette_lut", "virtual_res", "atlas_id", "atlas_fps", "loop_mode", "ink_density", "bleed_radius", "flyaway_threshold", "noise_primary_speed", "noise_detail_speed", "glitch_rate", "glitch_offset", "flat_shading", "facet_mesh", "dispersion_strength", "squash_curve", "nebula_noise", "step_fps", "ghost_pulse_fps");
            result.Token = ReadString(obj, "token", "/style/token", report, true);
            var palette = ReadPropertyObject(obj, "palette", "/style/palette", report, false);
            if (palette != null)
            {
                CheckUnknown(palette, "/style/palette", report, "primary", "secondary", "accent");
                foreach (var property in palette.Properties())
                {
                    if (property.Value.Type != JTokenType.String) TypeError("/style/palette/" + property.Name, property.Value, "color string", report);
                    else result.Palette[property.Name] = (string)property.Value;
                }
            }
            foreach (var property in obj.Properties())
                if (property.Name != "token" && property.Name != "palette") result.Parameters[property.Name] = property.Value.DeepClone();
            return result;
        }

        private static RecipeBehaviorContract ParseBehaviorContract(JToken token, ValidationReport report)
        {
            if (token == null) return null;
            var obj = token as JObject;
            if (obj == null) { TypeError("/behavior", token, "object", report); return new RecipeBehaviorContract(); }
            CheckUnknown(obj, "/behavior", report, "motion", "hit", "emission", "timing");
            return new RecipeBehaviorContract
            {
                Motion = ParseCapabilityBlock(obj["motion"], "motion", report),
                Hit = ParseCapabilityBlock(obj["hit"], "hit", report),
                Emission = ParseCapabilityBlock(obj["emission"], "emission", report),
                Timing = ParseCapabilityBlock(obj["timing"], "timing", report)
            };
        }

        private static RecipeContentContract ParseContentContract(JToken token, ValidationReport report)
        {
            if (token == null) return null;
            var obj = token as JObject;
            if (obj == null) { TypeError("/content", token, "object", report); return new RecipeContentContract(); }
            CheckUnknown(obj, "/content", report, "family", "parameters");
            var result = new RecipeContentContract { Family = ReadString(obj, "family", "/content/family", report, true) };
            var parameters = ReadPropertyObject(obj, "parameters", "/content/parameters", report, true);
            if (parameters != null) foreach (var property in parameters.Properties()) result.Parameters[property.Name] = property.Value.DeepClone();
            return result;
        }

        private static RecipeCapabilityBlock ParseCapabilityBlock(JToken token, string domain, ValidationReport report)
        {
            if (token == null) return null;
            var path = "/behavior/" + domain; var obj = token as JObject;
            if (obj == null) { TypeError(path, token, "object", report); return new RecipeCapabilityBlock { Domain = domain }; }
            var result = new RecipeCapabilityBlock { Domain = domain, Type = ReadString(obj, "type", path + "/type", report, true) };
            foreach (var property in obj.Properties()) if (property.Name != "type") result.Parameters[property.Name] = property.Value.DeepClone();
            return result;
        }

        private static ManifestParameter ParseManifestParameter(JObject parameter, string path, ValidationReport report)
        {
            var value = new ManifestParameter();
            if (parameter == null) { report.Add(InvalidType, ValidationSeverity.Error, path, "Manifest parameter must be an object.", null, "object"); return value; }
            CheckUnknown(parameter, path, report, "type", "min", "max", "default", "binding");
            value.Type = ReadEnum(parameter, "type", path + "/type", report, true, ParseParameterType, ParameterTypeValues);
            value.Min = ReadRequired(parameter, "min", path + "/min", report, false);
            value.Max = ReadRequired(parameter, "max", path + "/max", report, false);
            value.Default = ReadRequiredToken(parameter, "default", path + "/default", report);
            value.Binding = ReadString(parameter, "binding", path + "/binding", report, true);
            return value;
        }

        private static JObject ReadObject(string json, string path, ValidationReport report)
        {
            try { var token = JToken.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }); if (token is JObject) return (JObject)token; report.Add(InvalidType, ValidationSeverity.Error, path, "Document root must be an object.", token, "object"); }
            catch (JsonReaderException exception) { report.Add("E104", ValidationSeverity.Error, path, "Invalid JSON: " + exception.Message); }
            catch (Exception exception) { report.Add("E104", ValidationSeverity.Error, path, "Invalid JSON input: " + exception.Message); }
            return null;
        }

        private static void CheckUnknown(JObject obj, string path, ValidationReport report, params string[] allowed)
        {
            var set = new HashSet<string>(allowed, StringComparer.Ordinal);
            foreach (var property in obj.Properties()) if (!set.Contains(property.Name)) report.Add(UnknownField, ValidationSeverity.Error, Combine(path, property.Name), "Unknown field is not allowed.", property.Value);
        }

        private static string ReadString(JObject obj, string property, string path, ValidationReport report, bool required)
        {
            JToken token; if (!obj.TryGetValue(property, out token)) { if (required) Missing(path, report); return null; }
            if (token.Type != JTokenType.String) { TypeError(path, token, "string", report); return null; }
            return (string)token;
        }
        private static int ReadInt(JObject obj, string property, string path, ValidationReport report, bool required) { JToken token = ReadRequired(obj, property, path, report, required); if (token == null) return 0; int value; var text = TokenText(token); if (token.Type != JTokenType.Integer || !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) { TypeError(path, token, "32-bit integer", report); return 0; } return value; }
        private static uint ReadUInt(JObject obj, string property, string path, ValidationReport report, bool required) { JToken token = ReadRequired(obj, property, path, report, required); if (token == null) return 0; uint value; var text = token is JValue ? Convert.ToString(((JValue)token).Value, CultureInfo.InvariantCulture) : token.ToString(); if (token.Type != JTokenType.Integer || !uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value)) { TypeError(path, token, "uint32 integer", report); return 0; } return value; }
        private static double ReadNumber(JObject obj, string property, string path, ValidationReport report, bool required) { JToken token = ReadRequired(obj, property, path, report, required); if (token == null) return 0; double value; if ((token.Type != JTokenType.Integer && token.Type != JTokenType.Float) || !TryReadFiniteNumber(token, out value)) { if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float) report.Add(NonFiniteNumber, ValidationSeverity.Error, path, "Number must be finite.", token, "finite number"); else TypeError(path, token, "number", report); return 0; } return value; }
        private static bool ReadBoolean(JObject obj, string property, string path, ValidationReport report, bool required) { JToken token = ReadRequired(obj, property, path, report, required); if (token == null) return false; if (token.Type != JTokenType.Boolean) { TypeError(path, token, "boolean", report); return false; } return token.Value<bool>(); }
        private static JArray ReadArray(JObject obj, string property, string path, ValidationReport report, bool required) { JToken token = ReadRequired(obj, property, path, report, required); if (token == null) return null; if (!(token is JArray)) { TypeError(path, token, "array", report); return null; } return (JArray)token; }
        private static JObject ReadPropertyObject(JObject obj, string property, string path, ValidationReport report, bool required) { JToken token = ReadRequired(obj, property, path, report, required); if (token == null) return null; if (!(token is JObject)) { TypeError(path, token, "object", report); return null; } return (JObject)token; }
        private static JToken ReadRequired(JObject obj, string property, string path, ValidationReport report, bool required) { JToken token; if (!obj.TryGetValue(property, out token)) { if (required) Missing(path, report); return null; } return token; }
        private static JToken ReadRequiredToken(JObject obj, string property, string path, ValidationReport report) { return ReadRequired(obj, property, path, report, true); }
        private static string TokenText(JToken token) { return token is JValue ? Convert.ToString(((JValue)token).Value, CultureInfo.InvariantCulture) : token.ToString(); }
        private static bool TryReadFiniteNumber(JToken token, out double value) { value = 0; try { value = Convert.ToDouble(((JValue)token).Value, CultureInfo.InvariantCulture); return !double.IsNaN(value) && !double.IsInfinity(value); } catch (Exception) { return false; } }
        private static void Missing(string path, ValidationReport report) { report.Add(RequiredField, ValidationSeverity.Error, path, "Required field is missing."); }
        private static void TypeError(string path, JToken token, string allowed, ValidationReport report) { report.Add(InvalidType, ValidationSeverity.Error, path, "Value has an invalid type.", token, allowed); }
        // Contract order is deliberate and is also the order exposed to AI repair prompts.
        private static readonly string[] DimensionValues = { "2d", "3d" };
        private static readonly string[] ArchetypeValues = { "projectile", "impact", "slash", "aura", "area", "beam", "trail", "shield", "spawn", "transform", "composite", "environment", "screen_ui", "status", "decal", "weapon_trail", "destruction", "lifecycle", "portal", "loot" };
        private static readonly string[] ProfileValues = { "mobile_medium", "pc_editor" };
        private static readonly string[] TriggerValues = { "manual", "after_previous", "on_launch", "on_hit", "on_end" };
        private static readonly string[] ModuleKindValues = { "energy_body", "sprite_emitter", "secondary_particles", "motion_trail", "impact_flash", "impact_burst", "shockwave", "sub_effect" };
        private static readonly string[] ParameterTypeValues = { "float", "integer", "boolean", "string" };
        private static T ReadEnum<T>(JObject obj, string property, string path, ValidationReport report, bool required, Func<string, T?> parse, string[] allowedValues) where T : struct { string text = ReadString(obj, property, path, report, required); if (text == null) return default(T); T? value = parse(text); if (!value.HasValue) report.Add(InvalidEnum, ValidationSeverity.Error, path, "Value is not in the supported enumeration.", new JValue(text), AllowedValues(allowedValues)); return value.GetValueOrDefault(); }
        private static T? ReadNullableEnum<T>(JObject obj, string property, string path, ValidationReport report, Func<string, T?> parse, string[] allowedValues) where T : struct { string text = ReadString(obj, property, path, report, false); if (text == null) return null; T? value = parse(text); if (!value.HasValue) report.Add(InvalidEnum, ValidationSeverity.Error, path, "Value is not in the supported enumeration.", new JValue(text), AllowedValues(allowedValues)); return value; }
        private static string AllowedValues(string[] values) { return "[" + string.Join(", ", values) + "]"; }
        private static RecipeDimension? ParseDimension(string value) { return value == "2d" ? RecipeDimension.TwoD : value == "3d" ? RecipeDimension.ThreeD : (RecipeDimension?)null; }
        private static RecipeArchetype? ParseArchetype(string value) { switch (value) { case "projectile": return RecipeArchetype.Projectile; case "impact": return RecipeArchetype.Impact; case "slash": return RecipeArchetype.Slash; case "aura": return RecipeArchetype.Aura; case "area": return RecipeArchetype.Area; case "beam": return RecipeArchetype.Beam; case "trail": return RecipeArchetype.Trail; case "shield": return RecipeArchetype.Shield; case "spawn": return RecipeArchetype.Spawn; case "transform": return RecipeArchetype.Transform; case "composite": return RecipeArchetype.Composite; case "environment": return RecipeArchetype.Environment; case "screen_ui": return RecipeArchetype.ScreenUi; case "status": return RecipeArchetype.Status; case "decal": return RecipeArchetype.Decal; case "weapon_trail": return RecipeArchetype.WeaponTrail; case "destruction": return RecipeArchetype.Destruction; case "lifecycle": return RecipeArchetype.LifeCycle; case "portal": return RecipeArchetype.Portal; case "loot": return RecipeArchetype.Loot; default: return null; } }
        private static TargetProfile? ParseProfile(string value) { return value == "mobile_medium" ? TargetProfile.MobileMedium : value == "pc_editor" ? TargetProfile.PcEditor : (TargetProfile?)null; }
        private static StageTrigger? ParseTrigger(string value) { switch (value) { case "manual": return StageTrigger.Manual; case "after_previous": return StageTrigger.AfterPrevious; case "on_launch": return StageTrigger.OnLaunch; case "on_hit": return StageTrigger.OnHit; case "on_end": return StageTrigger.OnEnd; default: return null; } }
        private static ModuleKind? ParseModuleKind(string value) { switch (value) { case "energy_body": return ModuleKind.EnergyBody; case "sprite_emitter": return ModuleKind.SpriteEmitter; case "secondary_particles": return ModuleKind.SecondaryParticles; case "motion_trail": return ModuleKind.MotionTrail; case "impact_flash": return ModuleKind.ImpactFlash; case "impact_burst": return ModuleKind.ImpactBurst; case "shockwave": return ModuleKind.Shockwave; case "sub_effect": return ModuleKind.SubEffect; default: return null; } }
        private static ManifestParameterType? ParseParameterType(string value) { switch (value) { case "float": return ManifestParameterType.Float; case "integer": return ManifestParameterType.Integer; case "boolean": return ManifestParameterType.Boolean; case "string": return ManifestParameterType.String; default: return null; } }
        public static string StagePath(string stageId) { return "/stages/" + (string.IsNullOrEmpty(stageId) ? "{invalid-stage}" : stageId); }
        public static string ModulePath(string stagePath, string moduleId) { return stagePath + "/modules/" + (string.IsNullOrEmpty(moduleId) ? "{invalid-module}" : moduleId); }
        private static string Combine(string path, string field) { return path == "/" ? "/" + field : path.TrimEnd('/') + "/" + field; }
    }
}
