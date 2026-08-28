using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace VFXComposer.Editor.Rules
{
    public enum VfxRulesEnforcement { Strict, LegacyAudit }

    [Serializable]
    public sealed class VfxStructureBudget
    {
        public int MaxGameObjects;
        public int MaxDepth;
        public int MaxLocalMaterials;
        public int MaxLocalTextures;
    }

    [Serializable]
    public sealed class VfxProjectRulesConfig
    {
        public int SchemaVersion;
        public string RulesVersion;
        public string DefaultEnforcement;
        public List<string> LegacyEffectIds = new List<string>();
        public VfxStructureBudget Simple = new VfxStructureBudget();
        public VfxStructureBudget Complex = new VfxStructureBudget();
        public Dictionary<string, string> ArchetypeProfiles = new Dictionary<string, string>(StringComparer.Ordinal);
        public List<string> ForbiddenRuntimeComponentTypeNames = new List<string>();
        public List<string> ForbiddenProductionNameTokens = new List<string>();
        public List<string> AllowedDependencyRoots = new List<string>();
    }

    public static class VfxProjectRules
    {
        public const string RelativeConfigPath = "ProjectSettings/VFXComposer/VfxProjectRules.json";
        public const string RelativeManifestRoot = "ProjectSettings/VFXComposer/BuildManifests";
        private static VfxProjectRulesConfig cached;

        public static VfxProjectRulesConfig Load()
        {
            if (cached != null) return cached;
            var absolute = ProjectAbsolute(RelativeConfigPath);
            if (!File.Exists(absolute)) throw new FileNotFoundException("VFX production rules are missing.", absolute);
            cached = JsonConvert.DeserializeObject<VfxProjectRulesConfig>(File.ReadAllText(absolute), new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
            if (cached == null || cached.SchemaVersion != 1 || string.IsNullOrWhiteSpace(cached.RulesVersion)) throw new InvalidDataException("Unsupported or incomplete VFX production rules config: " + absolute);
            if (cached.Simple == null || cached.Complex == null) throw new InvalidDataException("Both simple and complex structure budgets are required.");
            foreach (var archetype in new[] { "projectile", "impact", "slash", "aura", "area", "beam", "trail", "shield", "spawn", "summon", "transform", "environment", "screen_ui", "composite", "decal", "weapon_trail", "destruction", "lifecycle", "portal", "loot" })
                if (!cached.ArchetypeProfiles.ContainsKey(archetype)) throw new InvalidDataException("Missing archetype profile: " + archetype);
            return cached;
        }

        public static void ReloadForTests() { cached = null; }

        public static VfxRulesEnforcement EnforcementFor(string effectId)
        {
            var config = Load();
            return config.LegacyEffectIds.Exists(value => string.Equals(value, effectId, StringComparison.Ordinal)) ? VfxRulesEnforcement.LegacyAudit : VfxRulesEnforcement.Strict;
        }

        public static VfxStructureBudget BudgetFor(string archetype)
        {
            string profile;
            if (string.IsNullOrEmpty(archetype) || !Load().ArchetypeProfiles.TryGetValue(archetype, out profile)) throw new ArgumentException("Unknown VFX archetype: " + archetype, "archetype");
            if (string.Equals(profile, "simple", StringComparison.Ordinal)) return Load().Simple;
            if (string.Equals(profile, "complex", StringComparison.Ordinal)) return Load().Complex;
            throw new InvalidDataException("Unknown VFX structure profile '" + profile + "' for archetype " + archetype);
        }

        public static string ManifestAbsolutePath(string effectId)
        {
            var safe = SanitizeId(effectId);
            return ProjectAbsolute(RelativeManifestRoot + "/" + safe + ".manifest.json");
        }

        public static string ProjectAbsolute(string relative)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        }

        public static string SanitizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Effect id is required.", "value");
            foreach (var character in value) if (!(char.IsLower(character) || char.IsDigit(character) || character == '_')) throw new ArgumentException("Effect id must be lower_snake_case: " + value, "value");
            if (value.Contains("__") || value[0] == '_' || value[value.Length - 1] == '_') throw new ArgumentException("Effect id must be lower_snake_case: " + value, "value");
            return value;
        }
    }
}
