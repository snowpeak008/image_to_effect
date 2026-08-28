using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using VFXComposer.Editor.Domain;

namespace VFXComposer.Editor.Validation
{
    public enum ArchetypeParameterType { Float, Integer, String, Enum }

    public sealed class ArchetypeParameterDefinition
    {
        public readonly string Name;
        public readonly ArchetypeParameterType Type;
        public readonly double Min;
        public readonly double Max;
        public readonly string[] Values;
        public readonly bool Required;

        public ArchetypeParameterDefinition(string name, ArchetypeParameterType type, double min = 0, double max = 0, bool required = true, params string[] values)
        { Name = name; Type = type; Min = min; Max = max; Required = required; Values = values ?? new string[0]; }
    }

    /// <summary>Authoritative semantic contract for the six W15 archetype parameter blocks.</summary>
    public static class ArchetypeParameterRegistry
    {
        private static readonly Dictionary<RecipeArchetype, ArchetypeParameterDefinition[]> Definitions = new Dictionary<RecipeArchetype, ArchetypeParameterDefinition[]>
        {
            { RecipeArchetype.Decal, new[] { F("size",.1,10), F("lifetime",.1,60), I("stack_limit",1,16) } },
            { RecipeArchetype.WeaponTrail, new[] { F("speed_threshold",0,100), I("history_points",8,16), F("fade_time",.01,2) } },
            { RecipeArchetype.Destruction, new[] { I("piece_count",8,12), F("explode_force",.1,50), F("debris_lifetime",.1,10) } },
            { RecipeArchetype.LifeCycle, new[] { F("duration",.1,10), E("direction","up","down","radial"), S("edge_color") } },
            { RecipeArchetype.Portal, new[] { S("pair_id"), F("portal_radius",.2,10), F("swirl_speed",.1,20) } },
            { RecipeArchetype.Loot, new[] { I("rarity",1,5), F("pickup_speed",.1,50), F("beam_height",.5,10) } }
        };

        public static IReadOnlyList<ArchetypeParameterDefinition> For(RecipeArchetype archetype)
        { ArchetypeParameterDefinition[] value; return Definitions.TryGetValue(archetype, out value) ? value : new ArchetypeParameterDefinition[0]; }

        public static ValidationReport Validate(Recipe recipe)
        {
            var report = new ValidationReport(); if (recipe == null) return report;
            var definitions = For(recipe.Archetype); var byName = definitions.ToDictionary(value => value.Name, StringComparer.Ordinal);
            foreach (var parameter in recipe.ArchetypeParameters)
            {
                ArchetypeParameterDefinition definition;
                if (!byName.TryGetValue(parameter.Key, out definition)) { report.Add("E1810", ValidationSeverity.Error, "/archetypeParameters/" + parameter.Key, "Parameter is not registered for this archetype.", parameter.Value, "[" + string.Join(", ", byName.Keys.OrderBy(value => value, StringComparer.Ordinal)) + "]"); continue; }
                ValidateValue(parameter.Value, definition, report);
            }
            foreach (var definition in definitions) if (definition.Required && !recipe.ArchetypeParameters.ContainsKey(definition.Name)) report.Add("E1812", ValidationSeverity.Error, "/archetypeParameters/" + definition.Name, "Required archetype parameter is missing.");
            return report;
        }

        private static void ValidateValue(JToken token, ArchetypeParameterDefinition definition, ValidationReport report)
        {
            var path = "/archetypeParameters/" + definition.Name;
            if (definition.Type == ArchetypeParameterType.String || definition.Type == ArchetypeParameterType.Enum)
            {
                var text = token != null && token.Type == JTokenType.String ? (string)token : null;
                if (string.IsNullOrWhiteSpace(text)) { report.Add("E1811", ValidationSeverity.Error, path, "Archetype parameter must be a non-empty string.", token, definition.Type == ArchetypeParameterType.Enum ? "[" + string.Join(", ", definition.Values) + "]" : "non-empty string"); return; }
                if (definition.Type == ArchetypeParameterType.Enum && !definition.Values.Contains(text, StringComparer.Ordinal)) report.Add("E1811", ValidationSeverity.Error, path, "Archetype parameter is outside the supported enumeration.", token, "[" + string.Join(", ", definition.Values) + "]");
                return;
            }
            var integer = definition.Type == ArchetypeParameterType.Integer;
            if (token == null || (integer ? token.Type != JTokenType.Integer : token.Type != JTokenType.Integer && token.Type != JTokenType.Float)) { report.Add("E1811", ValidationSeverity.Error, path, "Archetype parameter has an invalid numeric type.", token, integer ? "integer" : "number"); return; }
            double value; try { value = Convert.ToDouble(((JValue)token).Value, CultureInfo.InvariantCulture); } catch { value = double.NaN; }
            if (double.IsNaN(value) || double.IsInfinity(value) || value < definition.Min || value > definition.Max) report.Add("E1811", ValidationSeverity.Error, path, "Archetype parameter is outside its inclusive range.", token, "[" + definition.Min.ToString(CultureInfo.InvariantCulture) + ", " + definition.Max.ToString(CultureInfo.InvariantCulture) + "]");
        }

        private static ArchetypeParameterDefinition F(string name,double min,double max){return new ArchetypeParameterDefinition(name,ArchetypeParameterType.Float,min,max);}
        private static ArchetypeParameterDefinition I(string name,double min,double max){return new ArchetypeParameterDefinition(name,ArchetypeParameterType.Integer,min,max);}
        private static ArchetypeParameterDefinition S(string name){return new ArchetypeParameterDefinition(name,ArchetypeParameterType.String);}
        private static ArchetypeParameterDefinition E(string name,params string[] values){return new ArchetypeParameterDefinition(name,ArchetypeParameterType.Enum,0,0,true,values);}
    }
}
