using System.Text;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Chat;
using VFXComposer.AI.Contracts.Recipes;

namespace VFXComposer.AI.Providers.Recipes;

/// <summary>
/// Versioned, deterministic prompt assembly for Recipe v1 generation. It absorbs the former
/// <c>RecipePromptTemplate</c> (F8b1, refactor-not-coexist): prompt content lives in named, independently
/// versioned fragments; fragments are packed into role messages and split only at fragment boundaries when a
/// message would exceed the per-message bound; the composite version string records the assembler revision plus
/// every fragment version. Identical inputs always produce identical messages (REQ-001-04). Prompt text is never
/// logged and never appears in diagnostics.
/// </summary>
internal static class RecipePromptAssembler
{
    /// <summary>Keeps every constructed message inside the chat contract's per-message prompt bound.</summary>
    internal const int MaximumMessageCharacters = 16 * 1024;

    private const int MaximumRenderedIssues = 64;

    private const string SystemFragmentId = "system";
    private const string ContractFragmentId = "contract";
    private const string RedlineFragmentId = "redline";
    private const string CatalogFragmentId = "catalog";
    private const string ReferenceFragmentId = "reference";
    private const string RequestFragmentId = "request";
    private const string PreviousOutputFragmentId = "previous-output";
    private const string RepairFragmentId = "repair";
    private const string RefineKnowledgeFragmentId = "refine-knowledge";
    private const string RefineRequestFragmentId = "refine-request";

    /// <summary>
    /// Every fragment the assembler can emit, in composite-version order. Changing any fragment's content
    /// requires bumping its version here, which changes <see cref="Version"/> and is pinned by the assembly
    /// snapshot test. The refine-knowledge fragment version follows the committed knowledge asset, so a
    /// re-export of <c>refine-artist-knowledge.fragment.json</c> propagates into the composite version
    /// (REQ-004-55) without touching this registry.
    /// </summary>
    private static readonly (string Id, int Version)[] FragmentRegistry =
    [
        (SystemFragmentId, 1),
        (ContractFragmentId, 1),
        (RedlineFragmentId, 1),
        (CatalogFragmentId, 1),
        (ReferenceFragmentId, 1),
        (RequestFragmentId, 1),
        (PreviousOutputFragmentId, 1),
        (RepairFragmentId, 1),
        (RefineKnowledgeFragmentId, RecipeRefineKnowledge.Default.Version),
        (RefineRequestFragmentId, 1),
    ];

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

    private static readonly Lazy<IReadOnlyList<RecipePromptFragment>> CachedSystemFragments =
        new(BuildSystemFragments);

    private static readonly Lazy<string> CachedSystemPrompt =
        new(() => string.Concat(CachedSystemFragments.Value.Select(static fragment => fragment.Content)));

    private static readonly Lazy<IReadOnlyList<RecipePromptFragment>> CachedRefinementSystemFragments =
        new(static () =>
        [
            .. CachedSystemFragments.Value,
            new RecipePromptFragment(
                RefineKnowledgeFragmentId,
                RegistryVersion(RefineKnowledgeFragmentId),
                "\n" + RecipeRefineKnowledge.Default.RenderPromptText()),
        ]);

    /// <summary>
    /// The composite prompt version written to <c>PromptTemplateVersion</c>: the assembler revision followed by
    /// every registered fragment version, in fixed registry order. It is deterministic, ordinally comparable,
    /// and bounded by the draft contract's 256-character short-text limit.
    /// </summary>
    public static string Version { get; } = ComposeVersion();

    /// <summary>
    /// The full system prompt: the concatenation of every system fragment in registry order, computed once and
    /// identical across calls. It fits one message; <see cref="CreateInitialMessages"/> and
    /// <see cref="CreateRepairMessages"/> emit exactly this text as their first message.
    /// </summary>
    public static string SystemPrompt => CachedSystemPrompt.Value;

    /// <summary>The strict-budget reference recipe exactly as embedded in the system prompt.</summary>
    public static string ReferenceRecipeJson => CachedReferenceRecipe.Value;

    /// <summary>
    /// Builds the first-attempt request: one System message (<see cref="SystemPrompt"/>) followed by one User
    /// message wrapping <paramref name="description"/> in the fixed request shell. Identical descriptions yield
    /// identical messages. A null, empty, or whitespace description is an <see cref="ArgumentException"/>; a
    /// description that clears the contract guard but, once wrapped, exceeds <see cref="MaximumMessageCharacters"/>
    /// fails closed from <see cref="Assemble"/> as <see cref="ChatChannelErrorCode.PayloadTooLarge"/> — it is
    /// never split or truncated.
    /// </summary>
    public static IReadOnlyList<ChatChannelMessage> CreateInitialMessages(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return Assemble(
        [
            new RecipePromptSection(ChatRole.System, CachedSystemFragments.Value),
            new RecipePromptSection(ChatRole.User, [RequestFragment(description)]),
        ]);
    }

    /// <summary>
    /// Builds a retry request after a validation failure: the same System and User messages as
    /// <see cref="CreateInitialMessages"/>, then an optional Assistant message echoing
    /// <paramref name="previousOutput"/>, then a User message listing <paramref name="issues"/> as repair
    /// instructions. The echo is omitted when the previous output is blank or longer than
    /// <see cref="MaximumMessageCharacters"/>; the issue list renders at most 64 entries and stops early near
    /// the message bound, appending an "omitted" count. Identical inputs yield identical messages. The
    /// description guard and the size fail-closed behavior are those of <see cref="CreateInitialMessages"/>;
    /// a null issue list is an <see cref="ArgumentNullException"/>.
    /// </summary>
    public static IReadOnlyList<ChatChannelMessage> CreateRepairMessages(
        string description,
        string? previousOutput,
        IReadOnlyList<RecipeValidationIssue> issues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(issues);

        var sections = new List<RecipePromptSection>
        {
            new(ChatRole.System, CachedSystemFragments.Value),
            new(ChatRole.User, [RequestFragment(description)]),
        };

        // The previous output is echoed back only when it fits the per-message bound; the error list below is
        // sufficient on its own for a full regeneration.
        if (!string.IsNullOrWhiteSpace(previousOutput) && previousOutput.Length <= MaximumMessageCharacters)
        {
            sections.Add(new RecipePromptSection(
                ChatRole.Assistant,
                [new RecipePromptFragment(PreviousOutputFragmentId, RegistryVersion(PreviousOutputFragmentId), previousOutput)]));
        }

        sections.Add(new RecipePromptSection(
            ChatRole.User,
            [new RecipePromptFragment(RepairFragmentId, RegistryVersion(RepairFragmentId), BuildRepairInstruction(issues))]));
        return Assemble(sections);
    }

    /// <summary>
    /// Builds one refinement request (REQ-004-13): one System message carrying the generation system prompt plus
    /// the refine-knowledge fragment, then one User section carrying exactly the anchored triple — the lineage's
    /// original description, the current head recipe JSON, and this round's feedback. Nothing else is ever
    /// included: no earlier rounds, no other lineages. The head recipe is chunked into per-message-bound
    /// fragments, so an oversized draft splits into further same-role messages instead of being truncated;
    /// every other size overflow fails closed as <see cref="ChatChannelErrorCode.PayloadTooLarge"/> from
    /// <see cref="Assemble"/>. Identical inputs yield identical messages.
    /// </summary>
    public static IReadOnlyList<ChatChannelMessage> CreateRefinementMessages(
        string originalDescription,
        string headRecipeJson,
        string feedbackText)
    {
        return Assemble(
        [
            new RecipePromptSection(ChatRole.System, CachedRefinementSystemFragments.Value),
            new RecipePromptSection(ChatRole.User, RefinementRequestFragments(originalDescription, headRecipeJson, feedbackText)),
        ]);
    }

    /// <summary>
    /// Builds a refinement retry after a validation failure: the same System and User messages as
    /// <see cref="CreateRefinementMessages"/>, then the optional Assistant echo and the repair instruction under
    /// exactly the rules of <see cref="CreateRepairMessages"/>. The context stays the anchored triple of this
    /// round; repair never adds history.
    /// </summary>
    public static IReadOnlyList<ChatChannelMessage> CreateRefinementRepairMessages(
        string originalDescription,
        string headRecipeJson,
        string feedbackText,
        string? previousOutput,
        IReadOnlyList<RecipeValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);

        var sections = new List<RecipePromptSection>
        {
            new(ChatRole.System, CachedRefinementSystemFragments.Value),
            new(ChatRole.User, RefinementRequestFragments(originalDescription, headRecipeJson, feedbackText)),
        };

        if (!string.IsNullOrWhiteSpace(previousOutput) && previousOutput.Length <= MaximumMessageCharacters)
        {
            sections.Add(new RecipePromptSection(
                ChatRole.Assistant,
                [new RecipePromptFragment(PreviousOutputFragmentId, RegistryVersion(PreviousOutputFragmentId), previousOutput)]));
        }

        sections.Add(new RecipePromptSection(
            ChatRole.User,
            [new RecipePromptFragment(RepairFragmentId, RegistryVersion(RepairFragmentId), BuildRepairInstruction(issues))]));
        return Assemble(sections);
    }

    /// <summary>
    /// The anchored-triple user section: fixed shell text around the description, the head recipe chunked at the
    /// per-message bound (the only splittable piece; REQ-001-07 forbids truncation, so chunk fragments let
    /// <see cref="Assemble"/> continue the same-role section across messages), and the feedback with the output
    /// instruction. All three pieces are required.
    /// </summary>
    private static IReadOnlyList<RecipePromptFragment> RefinementRequestFragments(
        string originalDescription,
        string headRecipeJson,
        string feedbackText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalDescription);
        ArgumentException.ThrowIfNullOrWhiteSpace(headRecipeJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(feedbackText);

        var version = RegistryVersion(RefineRequestFragmentId);
        var fragments = new List<RecipePromptFragment>
        {
            new(
                RefineRequestFragmentId,
                version,
                "Original effect description:\n" + originalDescription +
                "\n\nCurrent recipe (the head version being refined):\n"),
        };

        for (var offset = 0; offset < headRecipeJson.Length; offset += MaximumMessageCharacters)
        {
            fragments.Add(new RecipePromptFragment(
                RefineRequestFragmentId,
                version,
                headRecipeJson.Substring(offset, Math.Min(MaximumMessageCharacters, headRecipeJson.Length - offset))));
        }

        fragments.Add(new RecipePromptFragment(
            RefineRequestFragmentId,
            version,
            "\n\nRefinement feedback for this round:\n" + feedbackText +
            "\n\nApply the feedback to the current recipe. Return the complete revised Recipe v1 JSON object now."));
        return fragments;
    }

    /// <summary>
    /// Packs the ordered sections into chat messages. A message never crosses a section (role) boundary, and a
    /// section that outgrows the per-message bound splits into further same-role messages at fragment boundaries
    /// only. Any overflow — a fragment too large for one message, too many messages, or a request beyond the
    /// byte bound — fails closed as <see cref="ChatChannelErrorCode.PayloadTooLarge"/>; content is never
    /// truncated (REQ-001-07). The byte check bounds message content; the exact wire-size bound stays with the
    /// protocol codec, which serializes into a stream capped at the same limit.
    /// </summary>
    public static IReadOnlyList<ChatChannelMessage> Assemble(IReadOnlyList<RecipePromptSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        if (sections.Count == 0 || sections.Any(static section => section is null))
        {
            throw new ArgumentException("Section list is invalid.", nameof(sections));
        }

        var messages = new List<ChatChannelMessage>();
        foreach (var section in sections)
        {
            var builder = new StringBuilder();
            foreach (var fragment in section.Fragments)
            {
                if (fragment.Content.Length > MaximumMessageCharacters)
                {
                    throw new ChatChannelException(ChatChannelErrorCode.PayloadTooLarge);
                }

                if (builder.Length > 0 && builder.Length + fragment.Content.Length > MaximumMessageCharacters)
                {
                    messages.Add(new ChatChannelMessage(section.Role, builder.ToString()));
                    builder.Clear();
                }

                builder.Append(fragment.Content);
            }

            messages.Add(new ChatChannelMessage(section.Role, builder.ToString()));
        }

        if (messages.Count > ChatChannelLimits.MaximumMessages)
        {
            throw new ChatChannelException(ChatChannelErrorCode.PayloadTooLarge);
        }

        var totalContentBytes = 0L;
        foreach (var message in messages)
        {
            totalContentBytes += Encoding.UTF8.GetByteCount(message.Content);
        }

        if (totalContentBytes > ChatChannelLimits.MaximumRequestBytes)
        {
            throw new ChatChannelException(ChatChannelErrorCode.PayloadTooLarge);
        }

        return messages;
    }

    private static RecipePromptFragment RequestFragment(string description) =>
        new(
            RequestFragmentId,
            RegistryVersion(RequestFragmentId),
            "Effect description:\n" + description + "\n\nReturn the complete Recipe v1 JSON object now.");

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

    /// <summary>
    /// The system prompt as ordered fragments. Their concatenation is byte-identical to the monolithic prompt
    /// the former template produced, which the assembly snapshot test pins.
    /// </summary>
    private static IReadOnlyList<RecipePromptFragment> BuildSystemFragments()
    {
        var snapshot = RecipeTemplateCatalogSnapshot.Default;

        var instructions = new StringBuilder();
        instructions.Append("You are the VFX Composer recipe author. ");
        instructions.Append("Output exactly one JSON object: a VFX Composer Recipe v1. ");
        instructions.Append("No markdown fence, no explanation, no comments, no trailing commas.\n\n");

        var contract = new StringBuilder();
        contract.Append("Recipe v1 contract (revision ").Append(snapshot.ContractRevision).Append("):\n");
        contract.Append("- Required top-level fields: recipeVersion, id, dimension, archetype, targetProfile, randomSeed, stages, metadata. Optional: revision, name, style.\n");
        contract.Append("- recipeVersion is the integer 1. Write \"revision\": 1 explicitly.\n");
        contract.Append("- id: nonempty; use lowercase letters, digits, _ or -.\n");
        contract.Append("- dimension: ").Append(Quoted(snapshot.BuildableDimensions)).Append(". archetype: ")
            .Append(Quoted(snapshot.BuildableArchetypes))
            .Append(". style, if present, is the string \"stylized\". Only these values are buildable by the current catalog.\n");
        contract.Append("- targetProfile: \"mobile_medium\" or \"pc_editor\".\n");
        contract.Append("- randomSeed: an integer between 0 and 4294967295.\n");
        contract.Append("- metadata is exactly {\"createdBy\": <string>, \"templateCatalogVersion\": \"")
            .Append(snapshot.TemplateCatalogVersion).Append("\"}.\n");
        contract.Append("- stages: array of stage objects. A stage has exactly: id, trigger, duration, enabled, modules. ");
        contract.Append("trigger is one of [manual, after_previous, on_launch, on_hit, on_end]; duration is a finite number >= 0.\n");
        contract.Append("- Stage roots are fixed: a buildable recipe carries the three stages id \"launch\" (trigger on_launch), id \"travel\" (trigger after_previous), id \"impact\" (trigger on_hit), in that order. ");
        contract.Append("All three stage roots must be present even when a stage stays empty, because the runtime controller wires exactly those three roots.\n");
        contract.Append("- A module has exactly: id, kind, templateId, parameters, enabled. ");
        contract.Append("kind is one of [energy_body, sprite_emitter, secondary_particles, motion_trail, impact_flash, impact_burst, shockwave, sub_effect] and must match the template's kind.\n");
        contract.Append("- Stage and module ids must be nonempty and globally unique.\n");
        contract.Append("- Unknown fields are errors. Do not invent helper fields such as color, intensity, position, notes, or durationSeconds. ");
        contract.Append("Every key inside a module's parameters object must be declared by the template table below for that exact templateId, ");
        contract.Append("every declared parameter must be present exactly once, and every value must lie inside the inclusive [min, max] range.\n\n");

        var redline = new StringBuilder();
        redline.Append("Strict build budget (the project build audit rejects a recipe that exceeds it; the budget cannot be raised from the recipe):\n");
        redline.Append("- The recipe carries at most two modules in total across all three stages, so at least one stage must be written with \"modules\": []. ");
        redline.Append("A single travel module is the safe default; add a second module only when the description clearly asks for a separate launch or impact beat.\n");
        redline.Append("- Never emit attachTo. Nesting one module under another exceeds the hierarchy depth budget and the build fails. ");
        redline.Append("A few legacy effects still carry attachTo under a per-id exemption; a newly authored recipe never qualifies for it.\n");
        redline.Append("- Every parameter value must lie inside the inclusive [min, max] of the table below. Out-of-range values are not clamped: the build fails.\n");
        redline.Append("- Keep all three stage ids launch, travel, impact with the triggers listed above, even when two of them are empty.\n\n");

        var catalog = new StringBuilder();
        catalog.Append("Template catalog (version ").Append(snapshot.TemplateCatalogVersion).Append("):\n");
        catalog.Append(snapshot.RenderPromptTable());
        catalog.Append('\n');

        var reference = new StringBuilder();
        reference.Append("Reference recipe (a strict-budget example that builds green: three stage roots, one module, no attachTo). ");
        reference.Append("Adapt id, name, randomSeed, metadata.createdBy, stage durations, and parameter values to the description, and keep this shape:\n");
        reference.Append(ReferenceRecipeJson);

        return
        [
            new RecipePromptFragment(SystemFragmentId, RegistryVersion(SystemFragmentId), instructions.ToString()),
            new RecipePromptFragment(ContractFragmentId, RegistryVersion(ContractFragmentId), contract.ToString()),
            new RecipePromptFragment(RedlineFragmentId, RegistryVersion(RedlineFragmentId), redline.ToString()),
            new RecipePromptFragment(CatalogFragmentId, RegistryVersion(CatalogFragmentId), catalog.ToString()),
            new RecipePromptFragment(ReferenceFragmentId, RegistryVersion(ReferenceFragmentId), reference.ToString()),
        ];
    }

    private static string ComposeVersion()
    {
        var version = AiContractVersions.RecipePromptAssembler + ";" + string.Join(
            ";",
            FragmentRegistry.Select(static entry =>
                entry.Id + "/" + entry.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        if (version.Length > 256)
        {
            // The draft contract bounds PromptTemplateVersion as short text (≤ 256 characters).
            throw new InvalidOperationException("The composite prompt version exceeds the draft record bound.");
        }

        return version;
    }

    private static int RegistryVersion(string fragmentId) =>
        FragmentRegistry.Single(entry => string.Equals(entry.Id, fragmentId, StringComparison.Ordinal)).Version;

    private static string Quoted(IReadOnlyList<string> values) =>
        string.Join(" or ", values.Select(static value => "\"" + value + "\""));
}
