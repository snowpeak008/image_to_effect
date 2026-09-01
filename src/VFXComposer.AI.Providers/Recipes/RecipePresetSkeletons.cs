using System.Text.Json;
using VFXComposer.AI.Contracts.Recipes;

namespace VFXComposer.AI.Providers.Recipes;

/// <summary>
/// One committed simple-mode preset: a strict-budget recipe skeleton an example card binds to. Applying a card
/// persists this skeleton as an ordinary pending draft without constructing a prompt or sending any request
/// (REQ-004-02/03). The skeleton is a committed asset with the same discipline as the catalog snapshot: it must
/// clear L1, the L1.5 catalog pre-validation and the strict prompt red line in build-time tests.
/// </summary>
public sealed class RecipePresetSkeleton
{
    /// <summary>Drafts created from presets carry this fixed marker instead of a prompt template version.</summary>
    public const string PresetPromptTemplateVersion = "preset/1";

    internal RecipePresetSkeleton(string presetId, string englishDescription, string authoredJson)
    {
        PresetId = presetId;
        EnglishDescription = englishDescription;
        RecipeJson = RecipeCanonicalJson.Canonicalize(authoredJson);
        CanonicalSha256 = RecipeCanonicalJson.ComputeSha256(RecipeJson);

        using var document = JsonDocument.Parse(RecipeJson);
        var root = document.RootElement;
        RecipeId = ReadString(root, "id");
        Archetype = ReadString(root, "archetype");
        Dimension = ReadString(root, "dimension");
        TargetProfile = ReadString(root, "targetProfile");
        TemplateCatalogVersion = ReadString(root.GetProperty("metadata"), "templateCatalogVersion");
        if (!string.Equals(
                TemplateCatalogVersion,
                RecipeTemplateCatalogSnapshot.Default.TemplateCatalogVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The preset skeleton catalog version does not match the committed snapshot.");
        }

        var templateIds = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var stage in root.GetProperty("stages").EnumerateArray())
        {
            foreach (var module in stage.GetProperty("modules").EnumerateArray())
            {
                templateIds.Add(ReadString(module, "templateId"));
            }
        }

        TemplateIds = templateIds.ToArray();
    }

    /// <summary>Stable card identity; the Desktop copy catalog keys off this value.</summary>
    public string PresetId { get; }

    /// <summary>
    /// The language-neutral English description of the card. It is the future lineage origin description
    /// (REQ-004 §6.1) and never a display string: the bilingual card copy lives in the Desktop catalog.
    /// </summary>
    public string EnglishDescription { get; }

    /// <summary>The canonicalized skeleton recipe document.</summary>
    public string RecipeJson { get; }

    /// <summary>The canonical hash of <see cref="RecipeJson"/>, precomputed once at load.</summary>
    public string CanonicalSha256 { get; }

    public string RecipeId { get; }
    public string Archetype { get; }
    public string Dimension { get; }
    public string TargetProfile { get; }
    public string TemplateCatalogVersion { get; }

    /// <summary>The distinct catalog templates this skeleton exercises, ordinal-ordered.</summary>
    public IReadOnlyList<string> TemplateIds { get; }

    public override string ToString() => "RecipePresetSkeleton(" + PresetId + ")";

    /// <summary>
    /// Builds a fresh pending draft record for one card click. Every click creates a new draft identity; the
    /// confirmation flow then binds to the precomputed canonical hash exactly like an AI-drafted record.
    /// </summary>
    public RecipeDraftRecord CreateDraftRecord(DateTimeOffset createdUtc) => new(
        "draft-" + Guid.NewGuid().ToString("N"),
        RecipeDraftStatus.PendingConfirmation,
        createdUtc,
        createdUtc,
        "preset-" + PresetId,
        PresetPromptTemplateVersion,
        TemplateCatalogVersion,
        RecipeJson,
        CanonicalSha256,
        RecipeId,
        Archetype,
        Dimension,
        TargetProfile,
        Array.Empty<RecipeValidationIssue>(),
        requestCount: 0);

    private static string ReadString(JsonElement objectElement, string name) =>
        objectElement.GetProperty(name).GetString()
            ?? throw new InvalidOperationException("The preset skeleton is invalid.");
}

/// <summary>
/// The closed, committed set of simple-mode preset skeletons (REQ-004-02: between four and six cards). Every
/// skeleton keeps the strict shape the prompt red line teaches: the three stage roots in order, at most two
/// modules, no attachTo, declared parameters only, values inside the committed bounds.
/// </summary>
public static class RecipePresetSkeletons
{
    private static readonly Lazy<IReadOnlyList<RecipePresetSkeleton>> Cached = new(Load);

    /// <summary>Every committed preset, in fixed card order.</summary>
    public static IReadOnlyList<RecipePresetSkeleton> All => Cached.Value;

    private static IReadOnlyList<RecipePresetSkeleton> Load() =>
    [
        new(
            "fire-bolt",
            "A single fiery core travelling in a straight line.",
            """
            {
              "recipeVersion": 1,
              "revision": 1,
              "id": "preset_fire_bolt_2d",
              "name": "Fire Bolt 2D",
              "dimension": "2d",
              "archetype": "projectile",
              "targetProfile": "mobile_medium",
              "randomSeed": 20260901,
              "stages": [
                { "id": "launch", "trigger": "on_launch", "duration": 0.1, "enabled": true, "modules": [] },
                { "id": "travel", "trigger": "after_previous", "duration": 1.0, "enabled": true, "modules": [
                  { "id": "core", "kind": "energy_body", "templateId": "PFT_2D_FireCore", "parameters": { "scale": 1.2 }, "enabled": true }
                ] },
                { "id": "impact", "trigger": "on_hit", "duration": 0.2, "enabled": true, "modules": [] }
              ],
              "metadata": { "createdBy": "vfxcomposer.preset", "templateCatalogVersion": "1.0.0" }
            }
            """),
        new(
            "trailing-fireball",
            "A fiery core with a motion trail following its flight.",
            """
            {
              "recipeVersion": 1,
              "revision": 1,
              "id": "preset_trailing_fireball_2d",
              "name": "Trailing Fireball 2D",
              "dimension": "2d",
              "archetype": "projectile",
              "targetProfile": "mobile_medium",
              "randomSeed": 20260902,
              "stages": [
                { "id": "launch", "trigger": "on_launch", "duration": 0.1, "enabled": true, "modules": [] },
                { "id": "travel", "trigger": "after_previous", "duration": 1.0, "enabled": true, "modules": [
                  { "id": "core", "kind": "energy_body", "templateId": "PFT_2D_FireCore", "parameters": { "scale": 1.2 }, "enabled": true },
                  { "id": "trail", "kind": "motion_trail", "templateId": "PFT_2D_FireTrail", "parameters": { "time": 0.22, "width": 0.42 }, "enabled": true }
                ] },
                { "id": "impact", "trigger": "on_hit", "duration": 0.2, "enabled": true, "modules": [] }
              ],
              "metadata": { "createdBy": "vfxcomposer.preset", "templateCatalogVersion": "1.0.0" }
            }
            """),
        new(
            "bursting-fireball",
            "A fiery core that ends in a burst of sparks on impact.",
            """
            {
              "recipeVersion": 1,
              "revision": 1,
              "id": "preset_bursting_fireball_2d",
              "name": "Bursting Fireball 2D",
              "dimension": "2d",
              "archetype": "projectile",
              "targetProfile": "mobile_medium",
              "randomSeed": 20260903,
              "stages": [
                { "id": "launch", "trigger": "on_launch", "duration": 0.1, "enabled": true, "modules": [] },
                { "id": "travel", "trigger": "after_previous", "duration": 1.0, "enabled": true, "modules": [
                  { "id": "core", "kind": "energy_body", "templateId": "PFT_2D_FireCore", "parameters": { "scale": 1.2 }, "enabled": true }
                ] },
                { "id": "impact", "trigger": "on_hit", "duration": 0.5, "enabled": true, "modules": [
                  { "id": "burst", "kind": "impact_burst", "templateId": "PFT_2D_FireImpact", "parameters": { "count": 24, "speed": 3.5 }, "enabled": true }
                ] }
              ],
              "metadata": { "createdBy": "vfxcomposer.preset", "templateCatalogVersion": "1.0.0" }
            }
            """),
        new(
            "shock-impact",
            "A fiery core whose impact sends out an expanding shockwave ring.",
            """
            {
              "recipeVersion": 1,
              "revision": 1,
              "id": "preset_shock_impact_2d",
              "name": "Shock Impact 2D",
              "dimension": "2d",
              "archetype": "projectile",
              "targetProfile": "mobile_medium",
              "randomSeed": 20260904,
              "stages": [
                { "id": "launch", "trigger": "on_launch", "duration": 0.1, "enabled": true, "modules": [] },
                { "id": "travel", "trigger": "after_previous", "duration": 1.0, "enabled": true, "modules": [
                  { "id": "core", "kind": "energy_body", "templateId": "PFT_2D_FireCore", "parameters": { "scale": 1.2 }, "enabled": true }
                ] },
                { "id": "impact", "trigger": "on_hit", "duration": 0.5, "enabled": true, "modules": [
                  { "id": "shockwave", "kind": "shockwave", "templateId": "PFT_2D_Shockwave", "parameters": { "lifetime": 0.28, "endSize": 2.8 }, "enabled": true }
                ] }
              ],
              "metadata": { "createdBy": "vfxcomposer.preset", "templateCatalogVersion": "1.0.0" }
            }
            """),
        new(
            "launch-flash",
            "A bright launch flash followed by a fiery core.",
            """
            {
              "recipeVersion": 1,
              "revision": 1,
              "id": "preset_launch_flash_2d",
              "name": "Launch Flash 2D",
              "dimension": "2d",
              "archetype": "projectile",
              "targetProfile": "mobile_medium",
              "randomSeed": 20260905,
              "stages": [
                { "id": "launch", "trigger": "on_launch", "duration": 0.12, "enabled": true, "modules": [
                  { "id": "flash", "kind": "impact_flash", "templateId": "PFT_2D_LaunchFlash", "parameters": { "lifetime": 0.12, "size": 1.0 }, "enabled": true }
                ] },
                { "id": "travel", "trigger": "after_previous", "duration": 1.0, "enabled": true, "modules": [
                  { "id": "core", "kind": "energy_body", "templateId": "PFT_2D_FireCore", "parameters": { "scale": 1.2 }, "enabled": true }
                ] },
                { "id": "impact", "trigger": "on_hit", "duration": 0.2, "enabled": true, "modules": [] }
              ],
              "metadata": { "createdBy": "vfxcomposer.preset", "templateCatalogVersion": "1.0.0" }
            }
            """),
        new(
            "ember-streak",
            "A fiery core scattering embers along its flight.",
            """
            {
              "recipeVersion": 1,
              "revision": 1,
              "id": "preset_ember_streak_2d",
              "name": "Ember Streak 2D",
              "dimension": "2d",
              "archetype": "projectile",
              "targetProfile": "mobile_medium",
              "randomSeed": 20260906,
              "stages": [
                { "id": "launch", "trigger": "on_launch", "duration": 0.1, "enabled": true, "modules": [] },
                { "id": "travel", "trigger": "after_previous", "duration": 1.0, "enabled": true, "modules": [
                  { "id": "core", "kind": "energy_body", "templateId": "PFT_2D_FireCore", "parameters": { "scale": 1.2 }, "enabled": true },
                  { "id": "embers", "kind": "secondary_particles", "templateId": "PFT_2D_Embers", "parameters": { "rate": 18, "lifetime": 0.55 }, "enabled": true }
                ] },
                { "id": "impact", "trigger": "on_hit", "duration": 0.2, "enabled": true, "modules": [] }
              ],
              "metadata": { "createdBy": "vfxcomposer.preset", "templateCatalogVersion": "1.0.0" }
            }
            """),
    ];
}
