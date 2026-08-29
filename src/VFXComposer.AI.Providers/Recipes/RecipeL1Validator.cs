using System.Globalization;
using System.Text.Json;
using VFXComposer.AI.Contracts.Recipes;

namespace VFXComposer.AI.Providers.Recipes;

/// <summary>
/// Hand-written .NET structural validation of a Recipe v1 document against the recipe-v1 schema contract
/// (docs/ai-workflow/recipe-v1.schema.json, contract revision 1.4). Codes, paths, and messages mirror the
/// Unity-side VfxDomainParser/RecipeValidator vocabulary (E10x structural, catalog-free E30x) so the one
/// repair-prompt template serves both layers. Open parameter objects (module/content/archetype parameters)
/// are typed at L2 by the live registries and catalog, which stay authoritative; L1 passing does not imply
/// the recipe is buildable.
/// </summary>
internal static class RecipeL1Validator
{
    private const string UnknownField = "E100";
    private const string RequiredField = "E101";
    private const string InvalidType = "E102";
    private const string InvalidEnum = "E103";
    private const string InvalidJson = "E104";
    private const string NonFiniteNumber = "E105";
    private const string UnsupportedVersion = "E301";
    private const string EmptyValue = "E302";
    private const string DuplicateStageId = "E303";
    private const string InvalidDuration = "E304";
    private const string DuplicateModuleId = "E305";
    private const string InvalidAttachment = "E306";
    private const string InvalidRevision = "E316";

    private static readonly string[] DimensionValues = ["2d", "3d"];
    private static readonly string[] ArchetypeValues =
    [
        "projectile", "impact", "slash", "aura", "area", "beam", "trail", "shield", "spawn", "transform",
        "composite", "environment", "screen_ui", "status", "decal", "weapon_trail", "destruction", "lifecycle",
        "portal", "loot",
    ];

    private static readonly string[] ProfileValues = ["mobile_medium", "pc_editor"];
    private static readonly string[] TriggerValues = ["manual", "after_previous", "on_launch", "on_hit", "on_end"];
    private static readonly string[] ModuleKindValues =
    [
        "energy_body", "sprite_emitter", "secondary_particles", "motion_trail", "impact_flash", "impact_burst",
        "shockwave", "sub_effect",
    ];

    private static readonly string[] StyleTokenValues =
    [
        "stylized", "cartoon", "pixel", "inkwash", "semireal", "holo", "dark", "neon", "lowpoly", "crystal",
        "candy", "cosmic", "steampunk", "ghost",
    ];

    private static readonly string[] ContentFamilyValues =
    [
        "fire", "frost", "lightning", "water", "wind", "earth", "nature", "toxic", "holy", "shadow", "arcane",
        "environment", "hit_feedback", "screen_ui", "game_ui",
    ];

    private static readonly string[] MotionTypeValues =
    [
        "linear", "accel", "parabola", "homing", "wave", "boomerang", "bounce", "orbit_then_strike", "sweep",
        "dash", "expand_ring", "implode", "moving_zone", "growth_stage",
    ];

    private static readonly string[] HitTypeValues =
        ["single", "pierce", "split", "chain_hop", "reflect", "occlude", "arc_link"];

    private static readonly string[] EmissionTypeValues =
        ["single", "fan", "burst_stagger", "ring", "volley_showcase", "converge"];

    private static readonly string[] TimingTypeValues =
    [
        "instant", "hitscan", "sustained", "charge_scale", "telegraph", "delay_fuse", "tick_pulse",
        "charge_release", "channel_interrupt", "chain_sequence",
    ];

    private static readonly string[] TimelineActionValues = ["play", "stop"];
    private static readonly string[] CameraHintTypeValues = ["shake", "zoom", "slowmo"];

    private enum Expected
    {
        Number,
        Integer,
        String,
        Boolean,
    }

    private static readonly IReadOnlyDictionary<string, Expected> StyleParameterTypes =
        new Dictionary<string, Expected>(StringComparer.Ordinal)
        {
            ["outline"] = Expected.Number,
            ["shading_steps"] = Expected.Integer,
            ["noise_scale"] = Expected.Number,
            ["glow_strength"] = Expected.Number,
            ["snap_fps"] = Expected.Number,
            ["palette_lut"] = Expected.String,
            ["virtual_res"] = Expected.Number,
            ["atlas_id"] = Expected.String,
            ["atlas_fps"] = Expected.Number,
            ["loop_mode"] = Expected.String,
            ["ink_density"] = Expected.Number,
            ["bleed_radius"] = Expected.Number,
            ["flyaway_threshold"] = Expected.Number,
            ["noise_primary_speed"] = Expected.Number,
            ["noise_detail_speed"] = Expected.Number,
            ["glitch_rate"] = Expected.Number,
            ["glitch_offset"] = Expected.Number,
            ["flat_shading"] = Expected.Boolean,
            ["facet_mesh"] = Expected.String,
            ["dispersion_strength"] = Expected.Number,
            ["squash_curve"] = Expected.String,
            ["nebula_noise"] = Expected.String,
            ["step_fps"] = Expected.Number,
            ["ghost_pulse_fps"] = Expected.Number,
        };

    private static readonly IReadOnlyDictionary<string, Expected> MotionParameterTypes =
        new Dictionary<string, Expected>(StringComparer.Ordinal)
        {
            ["speed"] = Expected.Number,
            ["init_speed"] = Expected.Number,
            ["accel"] = Expected.Number,
            ["max_speed"] = Expected.Number,
            ["apex_height"] = Expected.Number,
            ["flight_time"] = Expected.Number,
            ["turn_rate"] = Expected.Number,
            ["lose_target_mode"] = Expected.String,
            ["amplitude"] = Expected.Number,
            ["frequency"] = Expected.Number,
            ["out_distance"] = Expected.Number,
            ["hover_time"] = Expected.Number,
            ["return_speed"] = Expected.Number,
            ["bounce_count"] = Expected.Integer,
            ["energy_damping"] = Expected.Number,
            ["orbit_radius"] = Expected.Number,
            ["orbit_turns"] = Expected.Number,
            ["orbit_time"] = Expected.Number,
            ["strike_speed"] = Expected.Number,
            ["sweep_speed_max"] = Expected.Number,
            ["inertia"] = Expected.Number,
            ["distance"] = Expected.Number,
            ["duration"] = Expected.Number,
            ["max_radius"] = Expected.Number,
            ["expand_speed"] = Expected.Number,
            ["edge_thickness"] = Expected.Number,
            ["start_radius"] = Expected.Number,
            ["collapse_time"] = Expected.Number,
            ["follow_lag"] = Expected.Number,
            ["residue_slot"] = Expected.String,
            ["stage_count"] = Expected.Integer,
            ["base_radius"] = Expected.Number,
        };

    private static readonly IReadOnlyDictionary<string, Expected> HitParameterTypes =
        new Dictionary<string, Expected>(StringComparer.Ordinal)
        {
            ["max_hits"] = Expected.Integer,
            ["damping_per_hit"] = Expected.Number,
            ["impact_slot"] = Expected.String,
            ["child_count"] = Expected.Integer,
            ["split_angle"] = Expected.Number,
            ["trigger"] = Expected.String,
            ["hop_count"] = Expected.Integer,
            ["hop_range"] = Expected.Number,
            ["damping"] = Expected.Number,
            ["max_segments"] = Expected.Integer,
            ["damping_per_bounce"] = Expected.Number,
            ["probe_interval"] = Expected.Number,
            ["burn_point"] = Expected.String,
            ["sag"] = Expected.Number,
            ["jitter"] = Expected.Number,
        };

    private static readonly IReadOnlyDictionary<string, Expected> EmissionParameterTypes =
        new Dictionary<string, Expected>(StringComparer.Ordinal)
        {
            ["count"] = Expected.Integer,
            ["spread_angle"] = Expected.Number,
            ["stagger"] = Expected.Number,
            ["ring_radius"] = Expected.Number,
            ["source_count"] = Expected.Integer,
            ["focus_growth"] = Expected.Number,
            ["fan_count"] = Expected.Integer,
            ["fan_spread_angle"] = Expected.Number,
            ["burst_count"] = Expected.Integer,
            ["burst_stagger"] = Expected.Number,
            ["ring_count"] = Expected.Integer,
            ["phase_duration"] = Expected.Number,
        };

    private static readonly IReadOnlyDictionary<string, Expected> TimingParameterTypes =
        new Dictionary<string, Expected>(StringComparer.Ordinal)
        {
            ["max_range"] = Expected.Number,
            ["linger"] = Expected.Number,
            ["level_1"] = Expected.Number,
            ["level_2"] = Expected.Number,
            ["per_level_width"] = Expected.Number,
            ["warn_duration"] = Expected.Number,
            ["shape"] = Expected.String,
            ["fill_style"] = Expected.String,
            ["impact_slot"] = Expected.String,
            ["fuse_time"] = Expected.Number,
            ["blink_accelerate"] = Expected.Boolean,
            ["tick_interval"] = Expected.Number,
            ["tick_visual_slot"] = Expected.String,
            ["per_level_scale"] = Expected.Number,
            ["overcharge"] = Expected.Boolean,
            ["channel_time"] = Expected.Number,
            ["interrupt_scatter_scale"] = Expected.Number,
            ["count"] = Expected.Integer,
            ["interval"] = Expected.Number,
            ["topology"] = Expected.String,
        };

    public static IReadOnlyList<RecipeValidationIssue> Validate(string recipeJson)
    {
        ArgumentNullException.ThrowIfNull(recipeJson);
        var issues = new List<RecipeValidationIssue>();
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(recipeJson);
        }
        catch (JsonException)
        {
            issues.Add(Issue(InvalidJson, "/", "Invalid JSON: the document could not be parsed."));
            return issues;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Issue(InvalidType, "/", "Document root must be an object.", root, "object"));
                return issues;
            }

            ValidateRoot(root, issues);
        }

        return issues;
    }

    public static bool HasErrors(IReadOnlyList<RecipeValidationIssue> issues) =>
        issues.Any(static issue => issue.Severity == RecipeValidationSeverity.Error);

    private static void ValidateRoot(JsonElement root, List<RecipeValidationIssue> issues)
    {
        CheckUnknown(root, "/", issues,
            "recipeVersion", "revision", "id", "name", "dimension", "archetype", "style", "behavior", "content",
            "archetypeParameters", "targetProfile", "randomSeed", "stages", "timeline", "camera_hints", "gates",
            "metadata");

        var recipeVersion = ReadInt32(root, "recipeVersion", "/recipeVersion", issues, required: true);
        if (recipeVersion is not null && recipeVersion != 1)
        {
            issues.Add(Issue(
                UnsupportedVersion,
                "/recipeVersion",
                "Recipe version is not supported.",
                root.GetProperty("recipeVersion"),
                "1"));
        }

        var revision = ReadInt32(root, "revision", "/revision", issues, required: false);
        if (revision is < 1)
        {
            issues.Add(Issue(
                InvalidRevision,
                "/revision",
                "Recipe revision must be an integer greater than or equal to 1.",
                root.GetProperty("revision"),
                "integer >= 1"));
        }

        var id = ReadString(root, "id", "/id", issues, required: true);
        if (id is not null && string.IsNullOrWhiteSpace(id))
        {
            issues.Add(Issue(EmptyValue, "/id", "Recipe ID must not be empty."));
        }

        ReadString(root, "name", "/name", issues, required: false);
        ReadEnum(root, "dimension", "/dimension", issues, required: true, DimensionValues);
        ReadEnum(root, "archetype", "/archetype", issues, required: true, ArchetypeValues);
        ValidateStyle(root, issues);
        ValidateBehavior(root, issues);
        ValidateContent(root, issues);
        ReadObject(root, "archetypeParameters", "/archetypeParameters", issues, required: false);
        ReadEnum(root, "targetProfile", "/targetProfile", issues, required: true, ProfileValues);
        ReadUInt32(root, "randomSeed", "/randomSeed", issues, required: true);
        ValidateStages(root, issues);
        ValidateComposite(root, issues);
        ValidateMetadata(root, issues);
    }

    private static void ValidateStyle(JsonElement root, List<RecipeValidationIssue> issues)
    {
        if (!root.TryGetProperty("style", out var style))
        {
            return;
        }

        if (style.ValueKind == JsonValueKind.String)
        {
            if (!string.Equals(style.GetString(), "stylized", StringComparison.Ordinal))
            {
                issues.Add(Issue(
                    InvalidEnum, "/style", "Legacy style string only supports stylized.", style, "[stylized]"));
            }

            return;
        }

        if (style.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue(InvalidType, "/style", "Value has an invalid type.", style, "object or legacy string"));
            return;
        }

        var allowed = new List<string>(StyleParameterTypes.Keys) { "token", "palette" };
        CheckUnknown(style, "/style", issues, allowed.ToArray());
        ReadEnum(style, "token", "/style/token", issues, required: true, StyleTokenValues);
        var palette = ReadObject(style, "palette", "/style/palette", issues, required: false);
        if (palette is not null)
        {
            CheckUnknown(palette.Value, "/style/palette", issues, "primary", "secondary", "accent");
            foreach (var property in palette.Value.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    issues.Add(Issue(
                        InvalidType,
                        "/style/palette/" + property.Name,
                        "Value has an invalid type.",
                        property.Value,
                        "color string"));
                }
            }
        }

        ValidateTypedParameters(style, "/style", StyleParameterTypes, issues, "token", "palette");
    }

    private static void ValidateBehavior(JsonElement root, List<RecipeValidationIssue> issues)
    {
        var behavior = ReadObject(root, "behavior", "/behavior", issues, required: false);
        if (behavior is null)
        {
            return;
        }

        CheckUnknown(behavior.Value, "/behavior", issues, "motion", "hit", "emission", "timing");
        ValidateBehaviorBlock(behavior.Value, "motion", MotionTypeValues, MotionParameterTypes, issues);
        ValidateBehaviorBlock(behavior.Value, "hit", HitTypeValues, HitParameterTypes, issues);
        ValidateBehaviorBlock(behavior.Value, "emission", EmissionTypeValues, EmissionParameterTypes, issues);
        ValidateBehaviorBlock(behavior.Value, "timing", TimingTypeValues, TimingParameterTypes, issues);
    }

    private static void ValidateBehaviorBlock(
        JsonElement behavior,
        string domain,
        string[] typeValues,
        IReadOnlyDictionary<string, Expected> parameterTypes,
        List<RecipeValidationIssue> issues)
    {
        var path = "/behavior/" + domain;
        var block = ReadObject(behavior, domain, path, issues, required: false);
        if (block is null)
        {
            return;
        }

        var allowed = new List<string>(parameterTypes.Keys) { "type" };
        CheckUnknown(block.Value, path, issues, allowed.ToArray());
        ReadEnum(block.Value, "type", path + "/type", issues, required: true, typeValues);
        ValidateTypedParameters(block.Value, path, parameterTypes, issues, "type");
    }

    private static void ValidateTypedParameters(
        JsonElement objectElement,
        string path,
        IReadOnlyDictionary<string, Expected> parameterTypes,
        List<RecipeValidationIssue> issues,
        params string[] skipNames)
    {
        foreach (var property in objectElement.EnumerateObject())
        {
            if (skipNames.Contains(property.Name, StringComparer.Ordinal) ||
                !parameterTypes.TryGetValue(property.Name, out var expected))
            {
                continue;
            }

            CheckExpectedType(property.Value, path + "/" + property.Name, expected, issues);
        }
    }

    private static void ValidateContent(JsonElement root, List<RecipeValidationIssue> issues)
    {
        var content = ReadObject(root, "content", "/content", issues, required: false);
        if (content is null)
        {
            return;
        }

        CheckUnknown(content.Value, "/content", issues, "family", "parameters");
        ReadEnum(content.Value, "family", "/content/family", issues, required: true, ContentFamilyValues);
        ReadObject(content.Value, "parameters", "/content/parameters", issues, required: true);
    }

    private static void ValidateStages(JsonElement root, List<RecipeValidationIssue> issues)
    {
        var stages = ReadArray(root, "stages", "/stages", issues, required: true);
        if (stages is null)
        {
            return;
        }

        if (stages.Value.GetArrayLength() == 0)
        {
            issues.Add(Issue(EmptyValue, "/stages", "Recipe must contain at least one stage."));
            return;
        }

        var stageIds = new HashSet<string>(StringComparer.Ordinal);
        var moduleIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stage in stages.Value.EnumerateArray())
        {
            ValidateStage(stage, stageIds, moduleIds, issues);
        }
    }

    private static void ValidateStage(
        JsonElement stage,
        HashSet<string> stageIds,
        HashSet<string> moduleIds,
        List<RecipeValidationIssue> issues)
    {
        if (stage.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue(InvalidType, "/stages", "Each stage must be an object.", stage, "object"));
            return;
        }

        var id = ReadString(stage, "id", StagePath(null) + "/id", issues, required: true);
        var path = StagePath(id);
        CheckUnknown(stage, path, issues, "id", "trigger", "duration", "modules", "enabled");
        if (string.IsNullOrWhiteSpace(id) || !stageIds.Add(id))
        {
            issues.Add(Issue(DuplicateStageId, path + "/id", "Stage ID must be unique."));
        }

        ReadEnum(stage, "trigger", path + "/trigger", issues, required: true, TriggerValues);
        var duration = ReadNumber(stage, "duration", path + "/duration", issues, required: true);
        if (duration is < 0)
        {
            issues.Add(Issue(
                InvalidDuration,
                path + "/duration",
                "Stage duration must be finite and non-negative.",
                stage.GetProperty("duration"),
                "[0, +inf), finite"));
        }

        ReadBoolean(stage, "enabled", path + "/enabled", issues, required: true);
        var modules = ReadArray(stage, "modules", path + "/modules", issues, required: true);
        if (modules is null)
        {
            return;
        }

        var stageModules = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var module in modules.Value.EnumerateArray())
        {
            ValidateModule(module, path, moduleIds, stageModules, issues);
        }

        ValidateAttachmentCycles(path, stageModules, issues);
    }

    private static void ValidateModule(
        JsonElement module,
        string stagePath,
        HashSet<string> moduleIds,
        Dictionary<string, string?> stageModules,
        List<RecipeValidationIssue> issues)
    {
        if (module.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue(InvalidType, stagePath + "/modules", "Each module must be an object.", module, "object"));
            return;
        }

        var id = ReadString(module, "id", ModulePath(stagePath, null) + "/id", issues, required: true);
        var path = ModulePath(stagePath, id);
        CheckUnknown(module, path, issues, "id", "kind", "templateId", "parameters", "attachTo", "enabled");
        if (string.IsNullOrWhiteSpace(id) || !moduleIds.Add(id))
        {
            issues.Add(Issue(DuplicateModuleId, path + "/id", "Module ID must be unique across the recipe."));
        }
        else
        {
            stageModules[id] = null;
        }

        ReadEnum(module, "kind", path + "/kind", issues, required: true, ModuleKindValues);
        ReadString(module, "templateId", path + "/templateId", issues, required: true);
        var attachTo = ReadString(module, "attachTo", path + "/attachTo", issues, required: false);
        if (attachTo is not null && id is not null && stageModules.ContainsKey(id))
        {
            stageModules[id] = attachTo;
            if (string.Equals(attachTo, id, StringComparison.Ordinal))
            {
                issues.Add(Issue(
                    InvalidAttachment,
                    path + "/attachTo",
                    "attachTo must not reference the module itself.",
                    module.GetProperty("attachTo")));
            }
        }

        ReadBoolean(module, "enabled", path + "/enabled", issues, required: true);
        ReadObject(module, "parameters", path + "/parameters", issues, required: true);
    }

    private static void ValidateAttachmentCycles(
        string stagePath,
        Dictionary<string, string?> stageModules,
        List<RecipeValidationIssue> issues)
    {
        foreach (var (moduleId, attachTo) in stageModules)
        {
            if (attachTo is null || string.Equals(attachTo, moduleId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!stageModules.ContainsKey(attachTo))
            {
                issues.Add(new RecipeValidationIssue(
                    InvalidAttachment,
                    RecipeValidationSeverity.Error,
                    ModulePath(stagePath, moduleId) + "/attachTo",
                    "attachTo must reference a module ID in the same stage.",
                    "\"" + attachTo + "\""));
                continue;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal) { moduleId };
            var current = attachTo;
            while (current is not null && stageModules.TryGetValue(current, out var next))
            {
                if (!seen.Add(current))
                {
                    issues.Add(new RecipeValidationIssue(
                        InvalidAttachment,
                        RecipeValidationSeverity.Error,
                        ModulePath(stagePath, moduleId) + "/attachTo",
                        "attachTo must not form a cycle within a stage.",
                        "\"" + attachTo + "\""));
                    break;
                }

                current = next;
            }
        }
    }

    private static void ValidateComposite(JsonElement root, List<RecipeValidationIssue> issues)
    {
        var timeline = ReadArray(root, "timeline", "/timeline", issues, required: false);
        if (timeline is not null)
        {
            foreach (var item in timeline.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(Issue(InvalidType, "/timeline", "Timeline entries must be objects.", item, "object"));
                    continue;
                }

                CheckUnknown(item, "/timeline", issues, "t", "ref_id", "action", "overrides");
                ReadNumber(item, "t", "/timeline/t", issues, required: true);
                ReadString(item, "ref_id", "/timeline/ref_id", issues, required: true);
                ReadEnum(item, "action", "/timeline/action", issues, required: true, TimelineActionValues);
                var overrides = ReadObject(item, "overrides", "/timeline/overrides", issues, required: false);
                if (overrides is not null)
                {
                    ValidateTimelineOverrides(overrides.Value, issues);
                }
            }
        }

        var hints = ReadArray(root, "camera_hints", "/camera_hints", issues, required: false);
        if (hints is not null)
        {
            foreach (var item in hints.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(Issue(InvalidType, "/camera_hints", "Camera hint entries must be objects.", item, "object"));
                    continue;
                }

                CheckUnknown(item, "/camera_hints", issues, "t", "type", "strength");
                ReadNumber(item, "t", "/camera_hints/t", issues, required: true);
                ReadEnum(item, "type", "/camera_hints/type", issues, required: true, CameraHintTypeValues);
                ReadNumber(item, "strength", "/camera_hints/strength", issues, required: true);
            }
        }

        var gates = ReadArray(root, "gates", "/gates", issues, required: false);
        if (gates is not null)
        {
            foreach (var item in gates.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(Issue(InvalidType, "/gates", "Gate entries must be objects.", item, "object"));
                    continue;
                }

                CheckUnknown(item, "/gates", issues, "t", "wait_for");
                ReadNumber(item, "t", "/gates/t", issues, required: true);
                ReadString(item, "wait_for", "/gates/wait_for", issues, required: true);
            }
        }
    }

    private static void ValidateTimelineOverrides(JsonElement overrides, List<RecipeValidationIssue> issues)
    {
        CheckUnknown(overrides, "/timeline/overrides", issues, "palette", "scale", "position", "rotation");
        foreach (var property in overrides.EnumerateObject())
        {
            var path = "/timeline/overrides/" + property.Name;
            switch (property.Name)
            {
                case "palette":
                    CheckExpectedType(property.Value, path, Expected.String, issues);
                    break;
                case "scale":
                    CheckExpectedType(property.Value, path, Expected.Number, issues);
                    break;
                case "position" or "rotation":
                    if (property.Value.ValueKind != JsonValueKind.Array ||
                        property.Value.GetArrayLength() != 3 ||
                        property.Value.EnumerateArray().Any(static item => item.ValueKind != JsonValueKind.Number))
                    {
                        issues.Add(Issue(InvalidType, path, "Value has an invalid type.", property.Value, "array of 3 numbers"));
                    }

                    break;
            }
        }
    }

    private static void ValidateMetadata(JsonElement root, List<RecipeValidationIssue> issues)
    {
        var metadata = ReadObject(root, "metadata", "/metadata", issues, required: true);
        if (metadata is null)
        {
            return;
        }

        CheckUnknown(metadata.Value, "/metadata", issues, "createdBy", "templateCatalogVersion");
        ReadString(metadata.Value, "createdBy", "/metadata/createdBy", issues, required: true);
        ReadString(metadata.Value, "templateCatalogVersion", "/metadata/templateCatalogVersion", issues, required: true);
    }

    private static void CheckUnknown(
        JsonElement objectElement,
        string path,
        List<RecipeValidationIssue> issues,
        params string[] allowed)
    {
        var set = new HashSet<string>(allowed, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in objectElement.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                issues.Add(Issue(InvalidJson, Combine(path, property.Name), "Invalid JSON: duplicate property is not allowed."));
            }

            if (!set.Contains(property.Name))
            {
                issues.Add(Issue(
                    UnknownField, Combine(path, property.Name), "Unknown field is not allowed.", property.Value));
            }
        }
    }

    private static void CheckExpectedType(
        JsonElement value,
        string path,
        Expected expected,
        List<RecipeValidationIssue> issues)
    {
        var valid = expected switch
        {
            Expected.Number => value.ValueKind == JsonValueKind.Number,
            Expected.Integer => IsIntegerToken(value),
            Expected.String => value.ValueKind == JsonValueKind.String,
            Expected.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            _ => false,
        };
        if (!valid)
        {
            issues.Add(Issue(InvalidType, path, "Value has an invalid type.", value, AllowedType(expected)));
        }
    }

    private static string AllowedType(Expected expected) => expected switch
    {
        Expected.Number => "number",
        Expected.Integer => "integer",
        Expected.String => "string",
        _ => "boolean",
    };

    private static string? ReadString(
        JsonElement objectElement,
        string name,
        string path,
        List<RecipeValidationIssue> issues,
        bool required)
    {
        if (!objectElement.TryGetProperty(name, out var value))
        {
            if (required)
            {
                issues.Add(Issue(RequiredField, path, "Required field is missing."));
            }

            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            issues.Add(Issue(InvalidType, path, "Value has an invalid type.", value, "string"));
            return null;
        }

        return value.GetString();
    }

    private static bool? ReadBoolean(
        JsonElement objectElement,
        string name,
        string path,
        List<RecipeValidationIssue> issues,
        bool required)
    {
        if (!objectElement.TryGetProperty(name, out var value))
        {
            if (required)
            {
                issues.Add(Issue(RequiredField, path, "Required field is missing."));
            }

            return null;
        }

        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            issues.Add(Issue(InvalidType, path, "Value has an invalid type.", value, "boolean"));
            return null;
        }

        return value.GetBoolean();
    }

    private static int? ReadInt32(
        JsonElement objectElement,
        string name,
        string path,
        List<RecipeValidationIssue> issues,
        bool required)
    {
        if (!objectElement.TryGetProperty(name, out var value))
        {
            if (required)
            {
                issues.Add(Issue(RequiredField, path, "Required field is missing."));
            }

            return null;
        }

        if (!IsIntegerToken(value) || !value.TryGetInt32(out var parsed))
        {
            issues.Add(Issue(InvalidType, path, "Value has an invalid type.", value, "32-bit integer"));
            return null;
        }

        return parsed;
    }

    private static void ReadUInt32(
        JsonElement objectElement,
        string name,
        string path,
        List<RecipeValidationIssue> issues,
        bool required)
    {
        if (!objectElement.TryGetProperty(name, out var value))
        {
            if (required)
            {
                issues.Add(Issue(RequiredField, path, "Required field is missing."));
            }

            return;
        }

        if (!IsIntegerToken(value) || !value.TryGetUInt32(out _))
        {
            issues.Add(Issue(InvalidType, path, "Value has an invalid type.", value, "uint32 integer"));
        }
    }

    private static double? ReadNumber(
        JsonElement objectElement,
        string name,
        string path,
        List<RecipeValidationIssue> issues,
        bool required)
    {
        if (!objectElement.TryGetProperty(name, out var value))
        {
            if (required)
            {
                issues.Add(Issue(RequiredField, path, "Required field is missing."));
            }

            return null;
        }

        if (value.ValueKind != JsonValueKind.Number)
        {
            issues.Add(Issue(InvalidType, path, "Value has an invalid type.", value, "number"));
            return null;
        }

        var number = value.GetDouble();
        if (double.IsNaN(number) || double.IsInfinity(number))
        {
            issues.Add(Issue(NonFiniteNumber, path, "Number must be finite.", value, "finite number"));
            return null;
        }

        return number;
    }

    private static string? ReadEnum(
        JsonElement objectElement,
        string name,
        string path,
        List<RecipeValidationIssue> issues,
        bool required,
        string[] allowedValues)
    {
        var text = ReadString(objectElement, name, path, issues, required);
        if (text is null)
        {
            return null;
        }

        if (!allowedValues.Contains(text, StringComparer.Ordinal))
        {
            issues.Add(Issue(
                InvalidEnum,
                path,
                "Value is not in the supported enumeration.",
                objectElement.GetProperty(name),
                "[" + string.Join(", ", allowedValues) + "]"));
            return null;
        }

        return text;
    }

    private static JsonElement? ReadObject(
        JsonElement objectElement,
        string name,
        string path,
        List<RecipeValidationIssue> issues,
        bool required)
    {
        if (!objectElement.TryGetProperty(name, out var value))
        {
            if (required)
            {
                issues.Add(Issue(RequiredField, path, "Required field is missing."));
            }

            return null;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue(InvalidType, path, "Value has an invalid type.", value, "object"));
            return null;
        }

        return value;
    }

    private static JsonElement? ReadArray(
        JsonElement objectElement,
        string name,
        string path,
        List<RecipeValidationIssue> issues,
        bool required)
    {
        if (!objectElement.TryGetProperty(name, out var value))
        {
            if (required)
            {
                issues.Add(Issue(RequiredField, path, "Required field is missing."));
            }

            return null;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Issue(InvalidType, path, "Value has an invalid type.", value, "array"));
            return null;
        }

        return value;
    }

    private static bool IsIntegerToken(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number && value.GetRawText().IndexOfAny(['.', 'e', 'E']) < 0;

    private static RecipeValidationIssue Issue(
        string code,
        string path,
        string message,
        JsonElement? actualValue = null,
        string? allowedRange = null) =>
        new(
            code,
            RecipeValidationSeverity.Error,
            path,
            message,
            actualValue is null ? null : Truncate(actualValue.Value.GetRawText()),
            allowedRange);

    private static string Truncate(string value) =>
        value.Length <= 200 ? value : value[..200] + "…";

    private static string StagePath(string? stageId) =>
        "/stages/" + (string.IsNullOrEmpty(stageId) ? "{invalid-stage}" : stageId);

    private static string ModulePath(string stagePath, string? moduleId) =>
        stagePath + "/modules/" + (string.IsNullOrEmpty(moduleId) ? "{invalid-module}" : moduleId);

    private static string Combine(string path, string field) =>
        path == "/" ? "/" + field : path.TrimEnd('/') + "/" + field;
}
