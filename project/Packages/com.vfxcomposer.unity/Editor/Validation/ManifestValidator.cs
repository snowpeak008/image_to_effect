using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using VFXComposer.Editor.Domain;

namespace VFXComposer.Editor.Validation
{
    public static class ManifestValidator
    {
        public static ValidationReport ValidateSemantic(TemplateManifest manifest, string path)
        {
            var report = new ValidationReport();
            if (manifest == null) { report.Add("E204", ValidationSeverity.Error, path, "Manifest is missing."); return report; }
            if (manifest.ManifestVersion != 1) report.Add("E204", ValidationSeverity.Error, path + "/manifestVersion", "Manifest version is not supported.", new JValue(manifest.ManifestVersion), "1");
            RequireText(manifest.TemplateId, path + "/templateId", "templateId", report);
            RequireText(manifest.TemplateVersion, path + "/templateVersion", "templateVersion", report);
            RequireText(manifest.AssetGuid, path + "/assetGuid", "assetGuid", report);
            RequireText(manifest.AssetPath, path + "/assetPath", "assetPath", report);
            if (!string.IsNullOrWhiteSpace(manifest.AssetPath) && !IsCanonicalTemplatePath(manifest.AssetPath)) report.Add("E206", ValidationSeverity.Error, path + "/assetPath", "assetPath must be a canonical path below Assets/VFX/Templates/ and must not contain backslashes or traversal segments.", new JValue(manifest.AssetPath), "Assets/VFX/Templates/<file>");
            CheckNonNegative(manifest.Cost.EstimatedPeakParticles, path + "/cost/estimatedPeakParticles", report);
            CheckNonNegative(manifest.Cost.Materials, path + "/cost/materials", report);
            CheckNonNegative(manifest.Cost.Trails, path + "/cost/trails", report);
            foreach (var pair in manifest.Parameters)
            {
                var parameterPath = path + "/parameters/" + pair.Key;
                var parameter = pair.Value;
                if (parameter == null) { report.Add("E209", ValidationSeverity.Error, parameterPath, "Parameter declaration is missing."); continue; }
                if (string.IsNullOrWhiteSpace(parameter.Binding)) report.Add("E208", ValidationSeverity.Error, parameterPath + "/binding", "Parameter binding must be a non-empty symbolic value.");
                ValidateParameter(parameter, parameterPath, report);
            }
            return report;
        }

        public static bool IsCanonicalTemplatePath(string path)
        {
            const string prefix = "Assets/VFX/Templates/";
            if (string.IsNullOrEmpty(path) || path.IndexOf('\\') >= 0 || !path.StartsWith(prefix, StringComparison.Ordinal)) return false;
            var segments = path.Split('/');
            if (segments.Length < 4) return false;
            foreach (var segment in segments) if (string.IsNullOrEmpty(segment) || segment == "." || segment == "..") return false;
            return true;
        }

        private static void ValidateParameter(ManifestParameter parameter, string path, ValidationReport report)
        {
            switch (parameter.Type)
            {
                case ManifestParameterType.Float:
                    ValidateNumeric(parameter, path, false, report);
                    break;
                case ManifestParameterType.Integer:
                    ValidateNumeric(parameter, path, true, report);
                    break;
                case ManifestParameterType.Boolean:
                    ValidateUnbounded(parameter, path, JTokenType.Boolean, "boolean", report);
                    break;
                case ManifestParameterType.String:
                    ValidateUnbounded(parameter, path, JTokenType.String, "string", report);
                    break;
                default:
                    report.Add("E209", ValidationSeverity.Error, path + "/type", "Parameter type is not supported.");
                    break;
            }
        }

        private static void ValidateNumeric(ManifestParameter parameter, string path, bool integer, ValidationReport report)
        {
            double min;
            double max;
            double defaultValue;
            var typeName = integer ? "finite integer" : "finite number";
            var validMin = TryNumeric(parameter.Min, integer, out min);
            var validMax = TryNumeric(parameter.Max, integer, out max);
            var validDefault = TryNumeric(parameter.Default, integer, out defaultValue);
            if (!validMin) report.Add("E209", ValidationSeverity.Error, path + "/min", "Numeric parameter minimum must be " + typeName + ".", parameter.Min, typeName);
            if (!validMax) report.Add("E209", ValidationSeverity.Error, path + "/max", "Numeric parameter maximum must be " + typeName + ".", parameter.Max, typeName);
            if (!validDefault) report.Add("E209", ValidationSeverity.Error, path + "/default", "Numeric parameter default must be " + typeName + ".", parameter.Default, typeName);
            if (!validMin || !validMax || !validDefault) return;
            if (min > max) report.Add("E209", ValidationSeverity.Error, path, "Numeric parameter minimum must be less than or equal to maximum.", null, "min <= max");
            else if (defaultValue < min || defaultValue > max) report.Add("E209", ValidationSeverity.Error, path + "/default", "Numeric parameter default must lie within its inclusive range.", parameter.Default, "[" + min.ToString(CultureInfo.InvariantCulture) + ", " + max.ToString(CultureInfo.InvariantCulture) + "]");
        }

        private static void ValidateUnbounded(ManifestParameter parameter, string path, JTokenType expected, string typeName, ValidationReport report)
        {
            // v1 deliberately supports bounds only for numeric parameter types.
            if (parameter.Min != null) report.Add("E210", ValidationSeverity.Error, path + "/min", "v1 does not allow min for " + typeName + " parameters.", parameter.Min);
            if (parameter.Max != null) report.Add("E210", ValidationSeverity.Error, path + "/max", "v1 does not allow max for " + typeName + " parameters.", parameter.Max);
            if (parameter.Default == null || parameter.Default.Type != expected) report.Add("E210", ValidationSeverity.Error, path + "/default", "Parameter default must be a " + typeName + ".", parameter.Default, typeName);
        }

        private static bool TryNumeric(JToken token, bool integer, out double value)
        {
            value = 0;
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float) || (integer && token.Type != JTokenType.Integer)) return false;
            try
            {
                value = Convert.ToDouble(((JValue)token).Value, CultureInfo.InvariantCulture);
                return !double.IsNaN(value) && !double.IsInfinity(value);
            }
            catch (Exception) { return false; }
        }

        private static void RequireText(string value, string path, string name, ValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(value)) report.Add("E205", ValidationSeverity.Error, path, name + " must be non-empty.");
        }

        private static void CheckNonNegative(int value, string path, ValidationReport report)
        {
            if (value < 0) report.Add("E207", ValidationSeverity.Error, path, "Cost values must be non-negative.", new JValue(value), "[0, +inf)");
        }
    }
}
