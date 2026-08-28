using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using VFXComposer.Editor.Domain;

namespace VFXComposer.Editor.Impact2D
{
    public sealed class Impact2DRecipe
    {
        public int RecipeVersion;
        public int Revision;
        public string Id;
        public string Archetype;
        public string Dimension;
        public string Lifecycle;
        public string TargetProfile;
        public uint RandomSeed;
        public double Duration;
        public int ShardCount;
        public double RingScale;
    }

    public static class Impact2DRecipeParser
    {
        private static readonly HashSet<string> Fields = new HashSet<string>(new[] { "recipeVersion", "revision", "id", "archetype", "dimension", "lifecycle", "targetProfile", "randomSeed", "duration", "shardCount", "ringScale" }, StringComparer.Ordinal);

        public static ParseResult<Impact2DRecipe> Parse(string json)
        {
            var result = new ParseResult<Impact2DRecipe> { Value = new Impact2DRecipe() };
            JObject root;
            try { root = JObject.Parse(json); }
            catch (Exception exception) { result.Report.Add("E1600", ValidationSeverity.Error, "/", "Impact Recipe is not valid JSON: " + exception.Message); return result; }
            foreach (var property in root.Properties().Where(property => !Fields.Contains(property.Name))) result.Report.Add("E1601", ValidationSeverity.Error, "/" + property.Name, "Unknown Impact Recipe field.", property.Value);
            result.Value.RecipeVersion = Integer(root, "recipeVersion", result.Report);
            result.Value.Revision = Integer(root, "revision", result.Report);
            result.Value.Id = String(root, "id", result.Report);
            result.Value.Archetype = String(root, "archetype", result.Report);
            result.Value.Dimension = String(root, "dimension", result.Report);
            result.Value.Lifecycle = String(root, "lifecycle", result.Report);
            result.Value.TargetProfile = String(root, "targetProfile", result.Report);
            result.Value.RandomSeed = Unsigned(root, "randomSeed", result.Report);
            result.Value.Duration = Number(root, "duration", result.Report);
            result.Value.ShardCount = Integer(root, "shardCount", result.Report);
            result.Value.RingScale = Number(root, "ringScale", result.Report);
            Validate(result.Value, result.Report);
            return result;
        }

        private static void Validate(Impact2DRecipe recipe, ValidationReport report)
        {
            Exact(report, "/recipeVersion", recipe.RecipeVersion, 1);
            if (recipe.Revision < 1) report.Add("E1602", ValidationSeverity.Error, "/revision", "Revision must be >= 1.", new JValue(recipe.Revision));
            if (string.IsNullOrEmpty(recipe.Id) || !Regex.IsMatch(recipe.Id, "^[a-z0-9]+(?:_[a-z0-9]+)*$")) report.Add("E1603", ValidationSeverity.Error, "/id", "Effect id must be lower_snake_case.", new JValue(recipe.Id));
            Exact(report, "/archetype", recipe.Archetype, "impact");
            Exact(report, "/dimension", recipe.Dimension, "2d");
            Exact(report, "/lifecycle", recipe.Lifecycle, "one_shot");
            if (recipe.TargetProfile != "mobile_medium" && recipe.TargetProfile != "pc_editor") report.Add("E1604", ValidationSeverity.Error, "/targetProfile", "Unsupported target profile.", new JValue(recipe.TargetProfile), "mobile_medium | pc_editor");
            Range(report, "/duration", recipe.Duration, .2, .8);
            if (recipe.ShardCount < 4 || recipe.ShardCount > 20) report.Add("E1605", ValidationSeverity.Error, "/shardCount", "Shard count is outside the safe range.", new JValue(recipe.ShardCount), "[4, 20]");
            Range(report, "/ringScale", recipe.RingScale, 1.2, 3.5);
        }

        private static void Exact(ValidationReport report, string path, object actual, object expected) { if (!object.Equals(actual, expected)) report.Add("E1606", ValidationSeverity.Error, path, "Value does not match the Impact v1 contract.", actual == null ? null : new JValue(actual), Convert.ToString(expected, CultureInfo.InvariantCulture)); }
        private static void Range(ValidationReport report, string path, double value, double min, double max) { if (double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max) report.Add("E1607", ValidationSeverity.Error, path, "Number is outside the safe range.", new JValue(value), "[" + min.ToString(CultureInfo.InvariantCulture) + ", " + max.ToString(CultureInfo.InvariantCulture) + "]"); }
        private static JToken Required(JObject root, string name, ValidationReport report) { JToken token; if (!root.TryGetValue(name, out token)) { report.Add("E1608", ValidationSeverity.Error, "/" + name, "Required Impact Recipe field is missing."); return null; } return token; }
        private static string String(JObject root, string name, ValidationReport report) { var token = Required(root, name, report); if (token == null) return null; if (token.Type != JTokenType.String) { report.Add("E1609", ValidationSeverity.Error, "/" + name, "Expected string.", token); return null; } return token.Value<string>(); }
        private static int Integer(JObject root, string name, ValidationReport report) { var token = Required(root, name, report); if (token == null) return 0; if (token.Type != JTokenType.Integer) { report.Add("E1609", ValidationSeverity.Error, "/" + name, "Expected integer.", token); return 0; } return token.Value<int>(); }
        private static uint Unsigned(JObject root, string name, ValidationReport report) { var token = Required(root, name, report); if (token == null) return 0; uint value; if (token.Type != JTokenType.Integer || !uint.TryParse(token.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out value)) { report.Add("E1609", ValidationSeverity.Error, "/" + name, "Expected uint32 integer.", token); return 0; } return value; }
        private static double Number(JObject root, string name, ValidationReport report) { var token = Required(root, name, report); if (token == null) return 0; if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float) { report.Add("E1609", ValidationSeverity.Error, "/" + name, "Expected number.", token); return 0; } return token.Value<double>(); }
    }
}
