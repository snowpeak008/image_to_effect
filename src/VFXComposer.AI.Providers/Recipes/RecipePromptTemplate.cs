using System.Text;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Chat;
using VFXComposer.AI.Contracts.Recipes;

namespace VFXComposer.AI.Providers.Recipes;

/// <summary>
/// Versioned, deterministic prompt construction for Recipe v1 generation. The template embeds the recipe-v1
/// contract summary and the committed template catalog snapshot; identical inputs always produce identical
/// messages (REQ-001-04). Prompt text is never logged and never appears in diagnostics.
/// </summary>
internal static class RecipePromptTemplate
{
    public const string Version = AiContractVersions.RecipeSystemPrompt;

    /// <summary>Keeps every constructed message inside the chat contract's per-message prompt bound.</summary>
    private const int MaximumMessageCharacters = 16 * 1024;

    private const int MaximumRenderedIssues = 64;

    /// <summary>
    /// The reference recipe injected into the system prompt. It is authored on the prompt side after the
    /// machine-verified sample <c>batches/recipes/spark_projectile_2d.json</c> and deliberately not taken from the
    /// catalog snapshot: the snapshot's <c>canonicalExample</c> mirrors the Unity export of <c>fireball_2d</c>, an
    /// eight-module effect with two attachTo edges that only clears the project build audit through the legacy
    /// exemption list. Copying that shape under a new id always fails the strict budget, so the prompt shows the
    /// three-stage-root, single-module, attachTo-free shape instead.
    /// </summary>
    private const string ReferenceRecipeSource = """
        {
          "recipeVersion": 1,
          "revision": 1,
          "id": "spark_projectile_2d",
          "name": "Spark Projectile 2D",
          "dimension": "2d",
          "archetype": "projectile",
          "targetProfile": "mobile_medium",
          "randomSeed": 20260830,
          "stages": [
            { "id": "launch", "trigger": "on_launch", "duration": 0.1, "enabled": true, "modules": [] },
            { "id": "travel", "trigger": "after_previous", "duration": 1.0, "enabled": true, "modules": [
              { "id": "core", "kind": "energy_body", "templateId": "PFT_2D_FireCore", "parameters": { "scale": 1.2 }, "enabled": true }
            ] },
            { "id": "impact", "trigger": "on_hit", "duration": 0.2, "enabled": true, "modules": [] }
          ],
          "metadata": { "createdBy": "vfxcomposer.ai", "templateCatalogVersion": "1.0.0" }
        }
        """;

    private static readonly Lazy<string> CachedReferenceRecipe =
        new(() => RecipeCanonicalJson.Canonicalize(ReferenceRecipeSource));

    private static readonly Lazy<string> CachedSystemPrompt = new(BuildSystemPrompt);

    public static string SystemPrompt => CachedSystemPrompt.Value;

    /// <summary>The strict-budget reference recipe exactly as embedded in the system prompt.</summary>
    public static string ReferenceRecipeJson => CachedReferenceRecipe.Value;

    public static IReadOnlyList<ChatChannelMessage> CreateInitialMessages(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return
        [
            new ChatChannelMessage(ChatRole.System, SystemPrompt),
            new ChatChannelMessage(
                ChatRole.User,
                "Effect description:\n" + description + "\n\nReturn the complete Recipe v1 JSON object now."),
        ];
    }

    public static IReadOnlyList<ChatChannelMessage> CreateRepairMessages(
        string description,
        string? previousOutput,
        IReadOnlyList<RecipeValidationIssue> issues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(issues);

        var messages = new List<ChatChannelMessage>
        {
            new(ChatRole.System, SystemPrompt),
            new(
                ChatRole.User,
                "Effect description:\n" + description + "\n\nReturn the complete Recipe v1 JSON object now."),
        };

        // The previous output is echoed back only when it fits the per-message bound; the error list below is
        // sufficient on its own for a full regeneration.
        if (!string.IsNullOrWhiteSpace(previousOutput) && previousOutput.Length <= MaximumMessageCharacters)
        {
            messages.Add(new ChatChannelMessage(ChatRole.Assistant, previousOutput));
        }

        messages.Add(new ChatChannelMessage(ChatRole.User, BuildRepairInstruction(issues)));
        return messages;
    }

    private static string BuildRepairInstruction(IReadOnlyList<RecipeValidationIssue> issues)
    {
        var builder = new StringBuilder();
        builder.Append("The previous output failed VFX Composer Recipe v1 validation.\n");
        builder.Append("Validation errors (fix every entry):\n");
        var rendered = 0;
        foreach (var issue in issues)
        {
            if (rendered >= MaximumRenderedIssues || builder.Length > MaximumMessageCharacters - 1024)
            {
                builder.Append("- (")
                    .Append((issues.Count - rendered).ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Append(" further errors omitted)\n");
                break;
            }

            builder.Append("- ").Append(issue.Code).Append(" at ").Append(issue.Path).Append(": ").Append(issue.Message);
            if (issue.ActualValueJson is not null)
            {
                builder.Append(" actual=").Append(issue.ActualValueJson);
            }

            if (issue.AllowedRange is not null)
            {
                builder.Append(" allowed=").Append(issue.AllowedRange);
            }

            builder.Append('\n');
            rendered++;
        }

        builder.Append('\n');
        builder.Append("Return the complete corrected Recipe v1 JSON object. ");
        builder.Append("Fix only the listed errors and do not change any other field. ");
        builder.Append("Output exactly one JSON object with no markdown fence and no explanation.");
        return builder.ToString();
    }

    private static string BuildSystemPrompt()
    {
        var snapshot = RecipeTemplateCatalogSnapshot.Default;
        var builder = new StringBuilder();
        builder.Append("You are the VFX Composer recipe author. ");
        builder.Append("Output exactly one JSON object: a VFX Composer Recipe v1. ");
        builder.Append("No markdown fence, no explanation, no comments, no trailing commas.\n\n");

        builder.Append("Recipe v1 contract (revision ").Append(snapshot.ContractRevision).Append("):\n");
        builder.Append("- Required top-level fields: recipeVersion, id, dimension, archetype, targetProfile, randomSeed, stages, metadata. Optional: revision, name, style.\n");
        builder.Append("- recipeVersion is the integer 1. Write \"revision\": 1 explicitly.\n");
        builder.Append("- id: nonempty; use lowercase letters, digits, _ or -.\n");
        builder.Append("- dimension: ").Append(Quoted(snapshot.BuildableDimensions)).Append(". archetype: ")
            .Append(Quoted(snapshot.BuildableArchetypes))
            .Append(". style, if present, is the string \"stylized\". Only these values are buildable by the current catalog.\n");
        builder.Append("- targetProfile: \"mobile_medium\" or \"pc_editor\".\n");
        builder.Append("- randomSeed: an integer between 0 and 4294967295.\n");
        builder.Append("- metadata is exactly {\"createdBy\": <string>, \"templateCatalogVersion\": \"")
            .Append(snapshot.TemplateCatalogVersion).Append("\"}.\n");
        builder.Append("- stages: array of stage objects. A stage has exactly: id, trigger, duration, enabled, modules. ");
        builder.Append("trigger is one of [manual, after_previous, on_launch, on_hit, on_end]; duration is a finite number >= 0.\n");
        builder.Append("- Stage roots are fixed: a buildable recipe carries the three stages id \"launch\" (trigger on_launch), id \"travel\" (trigger after_previous), id \"impact\" (trigger on_hit), in that order. ");
        builder.Append("All three stage roots must be present even when a stage stays empty, because the runtime controller wires exactly those three roots.\n");
        builder.Append("- A module has exactly: id, kind, templateId, parameters, enabled. ");
        builder.Append("kind is one of [energy_body, sprite_emitter, secondary_particles, motion_trail, impact_flash, impact_burst, shockwave, sub_effect] and must match the template's kind.\n");
        builder.Append("- Stage and module ids must be nonempty and globally unique.\n");
        builder.Append("- Unknown fields are errors. Do not invent helper fields such as color, intensity, position, notes, or durationSeconds. ");
        builder.Append("Every key inside a module's parameters object must be declared by the template table below for that exact templateId, ");
        builder.Append("every declared parameter must be present exactly once, and every value must lie inside the inclusive [min, max] range.\n\n");

        builder.Append("Strict build budget (the project build audit rejects a recipe that exceeds it; the budget cannot be raised from the recipe):\n");
        builder.Append("- The recipe carries at most two modules in total across all three stages, so at least one stage must be written with \"modules\": []. ");
        builder.Append("A single travel module is the safe default; add a second module only when the description clearly asks for a separate launch or impact beat.\n");
        builder.Append("- Never emit attachTo. Nesting one module under another exceeds the hierarchy depth budget and the build fails. ");
        builder.Append("A few legacy effects still carry attachTo under a per-id exemption; a newly authored recipe never qualifies for it.\n");
        builder.Append("- Every parameter value must lie inside the inclusive [min, max] of the table below. Out-of-range values are not clamped: the build fails.\n");
        builder.Append("- Keep all three stage ids launch, travel, impact with the triggers listed above, even when two of them are empty.\n\n");

        builder.Append("Template catalog (version ").Append(snapshot.TemplateCatalogVersion).Append("):\n");
        builder.Append(snapshot.RenderPromptTable());
        builder.Append('\n');

        builder.Append("Reference recipe (a strict-budget example that builds green: three stage roots, one module, no attachTo). ");
        builder.Append("Adapt id, name, randomSeed, metadata.createdBy, stage durations, and parameter values to the description, and keep this shape:\n");
        builder.Append(ReferenceRecipeJson);
        var prompt = builder.ToString();
        if (prompt.Length > MaximumMessageCharacters)
        {
            throw new InvalidOperationException("The recipe system prompt exceeds the chat message bound.");
        }

        return prompt;
    }

    private static string Quoted(IReadOnlyList<string> values) =>
        string.Join(" or ", values.Select(static value => "\"" + value + "\""));
}
