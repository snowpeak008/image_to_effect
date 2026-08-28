using System;
using Newtonsoft.Json.Linq;
using VFXComposer.Editor.Catalog;
using VFXComposer.Editor.Domain;

namespace VFXComposer.Editor.Validation
{
    public sealed class BudgetProfile
    {
        public string Id;
        public int MaxPeakParticles;
        public int MaxMaterials;
        public int MaxTrails;
        public double MaxTotalDuration;
    }

    public static class BudgetProfiles
    {
        public static BudgetProfile For(TargetProfile profile)
        {
            switch (profile)
            {
                case TargetProfile.MobileMedium: return new BudgetProfile { Id = "mobile_medium", MaxPeakParticles = 200, MaxMaterials = 8, MaxTrails = 2, MaxTotalDuration = 6.0 };
                default: return new BudgetProfile { Id = "pc_editor", MaxPeakParticles = 1000, MaxMaterials = 32, MaxTrails = 8, MaxTotalDuration = 30.0 };
            }
        }
    }

    public static class BudgetCalculator
    {
        public static ValidationReport Evaluate(Recipe recipe, TemplateCatalog catalog, BudgetProfile profile = null)
        {
            var report = new ValidationReport();
            if (recipe == null || catalog == null) { report.Add("E400", ValidationSeverity.Error, "/", "Recipe and catalog are required for budget evaluation."); return report; }
            profile = profile ?? BudgetProfiles.For(recipe.TargetProfile);
            var particles = 0;
            var materials = 0;
            var trails = 0;
            var duration = 0.0;
            foreach (var stage in recipe.Stages)
            {
                if (!stage.Enabled) continue;
                duration += stage.Duration;
                foreach (var module in stage.Modules)
                {
                    if (!module.Enabled) continue;
                    TemplateManifest manifest;
                    if (!catalog.TryGet(module.TemplateId ?? string.Empty, out manifest)) continue;
                    particles += manifest.Cost.EstimatedPeakParticles;
                    materials += manifest.Cost.Materials;
                    trails += manifest.Cost.Trails;
                }
            }
            AddLimit(report, "E401", "/budget/estimatedPeakParticles", "Estimated peak particle count", particles, profile.MaxPeakParticles);
            AddLimit(report, "E402", "/budget/materials", "Material count", materials, profile.MaxMaterials);
            AddLimit(report, "E403", "/budget/trails", "Trail count", trails, profile.MaxTrails);
            AddLimit(report, "E404", "/budget/totalDuration", "Total stage duration", duration, profile.MaxTotalDuration);
            report.Add("I400", ValidationSeverity.Info, "/budget", "Static budget preflight completed for profile " + profile.Id + ".");
            return report;
        }

        private static void AddLimit(ValidationReport report, string code, string path, string label, double actual, double maximum)
        {
            var actualToken = new JValue(actual);
            var range = "[0, " + maximum.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
            if (actual > maximum) report.Add(code, ValidationSeverity.Error, path, label + " exceeds the profile limit.", actualToken, range);
            else if (maximum > 0 && actual >= maximum * 0.8) report.Add(code.Replace("E", "W"), ValidationSeverity.Warning, path, label + " is at least 80% of the profile limit.", actualToken, range);
        }
    }
}
