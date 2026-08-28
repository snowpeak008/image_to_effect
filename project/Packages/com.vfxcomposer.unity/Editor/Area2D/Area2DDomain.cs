using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using VFXComposer.Editor.Domain;

namespace VFXComposer.Editor.Area2D
{
    public sealed class Area2DRecipe
    {
        public int RecipeVersion;
        public int Revision;
        public string Id;
        public string Archetype;
        public string Dimension;
        public string Lifecycle;
        public string TargetProfile;
        public uint RandomSeed;
        public double Radius;
        public double LoopDuration;
        public double TickInterval;
        public int FlameCount;
    }

    public static class Area2DRecipeParser
    {
        private static readonly HashSet<string> Fields = new HashSet<string>(new[] { "recipeVersion", "revision", "id", "archetype", "dimension", "lifecycle", "targetProfile", "randomSeed", "radius", "loopDuration", "tickInterval", "flameCount" }, StringComparer.Ordinal);

        public static ParseResult<Area2DRecipe> Parse(string json)
        {
            var result = new ParseResult<Area2DRecipe> { Value = new Area2DRecipe() };
            JObject root;
            try { root = JObject.Parse(json); }
            catch (Exception exception) { result.Report.Add("E1700", ValidationSeverity.Error, "/", "Area Recipe is not valid JSON: " + exception.Message); return result; }
            foreach (var property in root.Properties().Where(property => !Fields.Contains(property.Name))) result.Report.Add("E1701", ValidationSeverity.Error, "/" + property.Name, "Unknown Area Recipe field.", property.Value);
            result.Value.RecipeVersion = Integer(root, "recipeVersion", result.Report);
            result.Value.Revision = Integer(root, "revision", result.Report);
            result.Value.Id = String(root, "id", result.Report);
            result.Value.Archetype = String(root, "archetype", result.Report);
            result.Value.Dimension = String(root, "dimension", result.Report);
            result.Value.Lifecycle = String(root, "lifecycle", result.Report);
            result.Value.TargetProfile = String(root, "targetProfile", result.Report);
            result.Value.RandomSeed = Unsigned(root, "randomSeed", result.Report);
            result.Value.Radius = Number(root, "radius", result.Report);
            result.Value.LoopDuration = Number(root, "loopDuration", result.Report);
            result.Value.TickInterval = Number(root, "tickInterval", result.Report);
            result.Value.FlameCount = Integer(root, "flameCount", result.Report);
            Validate(result.Value, result.Report);
            return result;
        }

        private static void Validate(Area2DRecipe recipe, ValidationReport report)
        {
            Exact(report, "/recipeVersion", recipe.RecipeVersion, 1);
            if (recipe.Revision < 1) report.Add("E1702", ValidationSeverity.Error, "/revision", "Revision must be >= 1.", new JValue(recipe.Revision));
            if (string.IsNullOrEmpty(recipe.Id) || !Regex.IsMatch(recipe.Id, "^[a-z0-9]+(?:_[a-z0-9]+)*$")) report.Add("E1703", ValidationSeverity.Error, "/id", "Effect id must be lower_snake_case.", new JValue(recipe.Id));
            Exact(report, "/archetype", recipe.Archetype, "area");
            Exact(report, "/dimension", recipe.Dimension, "2d");
            Exact(report, "/lifecycle", recipe.Lifecycle, "sustained");
            if (recipe.TargetProfile != "mobile_medium" && recipe.TargetProfile != "pc_editor") report.Add("E1704", ValidationSeverity.Error, "/targetProfile", "Unsupported target profile.", new JValue(recipe.TargetProfile), "mobile_medium | pc_editor");
            Range(report, "/radius", recipe.Radius, .8, 3.5);
            Range(report, "/loopDuration", recipe.LoopDuration, 1.0, 3.0);
            Range(report, "/tickInterval", recipe.TickInterval, .4, 2.0);
            if (recipe.FlameCount < 12 || recipe.FlameCount > 40) report.Add("E1705", ValidationSeverity.Error, "/flameCount", "Flame count is outside the safe range.", new JValue(recipe.FlameCount), "[12, 40]");
        }

        private static void Exact(ValidationReport report, string path, object actual, object expected) { if (!object.Equals(actual, expected)) report.Add("E1706", ValidationSeverity.Error, path, "Value does not match the Area 2D v1 contract.", actual == null ? null : new JValue(actual), Convert.ToString(expected, CultureInfo.InvariantCulture)); }
        private static void Range(ValidationReport report, string path, double value, double min, double max) { if (double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max) report.Add("E1707", ValidationSeverity.Error, path, "Number is outside the safe range.", new JValue(value), "[" + min.ToString(CultureInfo.InvariantCulture) + ", " + max.ToString(CultureInfo.InvariantCulture) + "]"); }
        private static JToken Required(JObject root, string name, ValidationReport report) { JToken token; if (!root.TryGetValue(name, out token)) { report.Add("E1708", ValidationSeverity.Error, "/" + name, "Required Area Recipe field is missing."); return null; } return token; }
        private static string String(JObject root, string name, ValidationReport report) { var token = Required(root, name, report); if (token == null) return null; if (token.Type != JTokenType.String) { report.Add("E1709", ValidationSeverity.Error, "/" + name, "Expected string.", token); return null; } return token.Value<string>(); }
        private static int Integer(JObject root, string name, ValidationReport report) { var token = Required(root, name, report); if (token == null) return 0; if (token.Type != JTokenType.Integer) { report.Add("E1709", ValidationSeverity.Error, "/" + name, "Expected integer.", token); return 0; } return token.Value<int>(); }
        private static uint Unsigned(JObject root, string name, ValidationReport report) { var token = Required(root, name, report); if (token == null) return 0; uint value; if (token.Type != JTokenType.Integer || !uint.TryParse(token.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out value)) { report.Add("E1709", ValidationSeverity.Error, "/" + name, "Expected uint32 integer.", token); return 0; } return value; }
        private static double Number(JObject root, string name, ValidationReport report) { var token = Required(root, name, report); if (token == null) return 0; if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float) { report.Add("E1709", ValidationSeverity.Error, "/" + name, "Expected number.", token); return 0; } return token.Value<double>(); }
    }
}
