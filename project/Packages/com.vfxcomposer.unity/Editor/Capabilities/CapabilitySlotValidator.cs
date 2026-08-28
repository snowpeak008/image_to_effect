using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VFXComposer.Editor.Domain;

namespace VFXComposer.Editor.Capabilities
{
    /// <summary>Validates one-level Recipe composition slots against saved project Recipes.</summary>
    public static class CapabilitySlotValidator
    {
        public static ValidationReport Validate(Recipe recipe)
        {
            var report = new ValidationReport();
            if (recipe == null || recipe.Behavior == null) return report;
            var index = LoadIndex();
            foreach (var block in recipe.Behavior.Blocks())
            {
                CapabilityDefinition definition;
                if (!CapabilityRegistry.TryGet(block.Domain, block.Type ?? string.Empty, out definition)) continue;
                foreach (var parameter in block.Parameters)
                {
                    CapabilityParameterContract contract;
                    if (!definition.Parameters.TryGetValue(parameter.Key, out contract) || !contract.IsSlot || parameter.Value == null || parameter.Value.Type != JTokenType.String) continue;
                    var id = (string)parameter.Value;
                    var path = "/behavior/" + block.Domain + "/" + parameter.Key;
                    List<Recipe> matches;
                    if (string.IsNullOrWhiteSpace(id) || !index.TryGetValue(id, out matches) || matches.Count != 1)
                    {
                        report.Add("E328", ValidationSeverity.Error, path, "Visual slot must resolve to exactly one saved Recipe ID.", parameter.Value, "unique saved Recipe ID");
                        continue;
                    }
                    var referenced = matches[0];
                    if (string.Equals(referenced.Id, recipe.Id, StringComparison.Ordinal) || HasSlots(referenced))
                    {
                        report.Add("E329", ValidationSeverity.Error, path, "Visual slots support one nesting level and cannot reference self or another slotted Recipe.", parameter.Value, "non-slotted Recipe ID");
                        continue;
                    }
                    if ((parameter.Key == "impact_slot" || parameter.Key == "burn_point" || parameter.Key == "tick_visual_slot") && referenced.Archetype != RecipeArchetype.Impact)
                        report.Add("E328", ValidationSeverity.Error, path, "This visual slot requires an Impact Recipe.", parameter.Value, "Impact Recipe ID");
                    if (parameter.Key == "residue_slot" && referenced.Archetype != RecipeArchetype.Trail && referenced.Archetype != RecipeArchetype.Area)
                        report.Add("E328", ValidationSeverity.Error, path, "residue_slot requires a Trail or Area Recipe.", parameter.Value, "Trail or Area Recipe ID");
                }
            }
            return report;
        }

        private static Dictionary<string, List<Recipe>> LoadIndex()
        {
            var result = new Dictionary<string, List<Recipe>>(StringComparer.Ordinal);
            var root = Path.Combine(Application.dataPath, "VFX", "Recipes");
            if (!Directory.Exists(root)) return result;
            foreach (var path in Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).OrderBy(value => value, StringComparer.Ordinal))
            {
                if (path.EndsWith(".patch.json", StringComparison.OrdinalIgnoreCase) || path.IndexOf(Path.DirectorySeparatorChar + "Patches" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0) continue;
                try
                {
                    var parsed = VfxDomainParser.ParseRecipe(File.ReadAllText(path));
                    if (parsed.Report.HasErrors || parsed.Value == null || string.IsNullOrWhiteSpace(parsed.Value.Id)) continue;
                    List<Recipe> list;
                    if (!result.TryGetValue(parsed.Value.Id, out list)) result.Add(parsed.Value.Id, list = new List<Recipe>());
                    list.Add(parsed.Value);
                }
                catch { }
            }
            return result;
        }

        private static bool HasSlots(Recipe recipe)
        {
            if (recipe == null || recipe.Behavior == null) return false;
            foreach (var block in recipe.Behavior.Blocks())
            {
                CapabilityDefinition definition;
                if (!CapabilityRegistry.TryGet(block.Domain, block.Type ?? string.Empty, out definition)) continue;
                foreach (var parameter in block.Parameters)
                {
                    CapabilityParameterContract contract;
                    if (definition.Parameters.TryGetValue(parameter.Key, out contract) && contract.IsSlot && parameter.Value != null && parameter.Value.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)parameter.Value)) return true;
                }
            }
            return false;
        }
    }
}
