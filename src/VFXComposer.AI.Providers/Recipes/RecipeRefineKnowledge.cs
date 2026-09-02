using System.Collections.ObjectModel;
using System.Text.Json;

namespace VFXComposer.AI.Providers.Recipes;

/// <summary>
/// The refinement artist-knowledge asset (REQ-004 §10), committed as an embedded resource exported from the
/// single human-edited source document. The English parts (translation guidance, aesthetic conventions,
/// refinement discipline) render deterministically into the <c>refine-knowledge</c> prompt fragment; the alias
/// lexicon (English and Chinese) is local matching data for the override guard and never enters a prompt
/// (REQ-004 O-3). Consistency with the source document is pinned by <see cref="SourceSha256"/>.
/// </summary>
public sealed class RecipeRefineKnowledge
{
    public const string SchemaId = "vfxcomposer.ai.refine-artist-knowledge/1";

    /// <summary>The repository-relative path of the human-edited source of truth.</summary>
    public const string SourceDocumentRepositoryPath = "docs/ai-workflow/refine-artist-knowledge.md";

    private const string ResourceName =
        "VFXComposer.AI.Providers.Recipes.Assets.refine-artist-knowledge.fragment.json";

    private static readonly Lazy<RecipeRefineKnowledge> Cached = new(Load);

    private readonly Lazy<string> _promptText;

    private RecipeRefineKnowledge(
        int version,
        string exportedOn,
        string sourceSha256,
        IReadOnlyList<RecipeRefineTranslation> feedbackTranslations,
        IReadOnlyList<string> aestheticConventions,
        IReadOnlyList<string> refinementDiscipline)
    {
        Version = version;
        ExportedOn = exportedOn;
        SourceSha256 = sourceSha256;
        FeedbackTranslations = feedbackTranslations;
        AestheticConventions = aestheticConventions;
        RefinementDiscipline = refinementDiscipline;
        _promptText = new Lazy<string>(RenderPromptTextCore);
    }

    /// <summary>The one committed knowledge asset.</summary>
    public static RecipeRefineKnowledge Default => Cached.Value;

    /// <summary>The asset revision; it feeds the <c>refine-knowledge</c> fragment version in the composite prompt version.</summary>
    public int Version { get; }

    public string ExportedOn { get; }

    /// <summary>SHA-256 (lowercase hex) of the exact bytes of the source document at export time.</summary>
    public string SourceSha256 { get; }

    /// <summary>One entry per feedback translation row; also the guard's alias lexicon (REQ-004-56).</summary>
    public IReadOnlyList<RecipeRefineTranslation> FeedbackTranslations { get; }

    public IReadOnlyList<string> AestheticConventions { get; }

    public IReadOnlyList<string> RefinementDiscipline { get; }

    public override string ToString() => "RecipeRefineKnowledge(" + Version + ")";

    /// <summary>
    /// The deterministic English prompt text of the knowledge fragment: the translation guidance with the
    /// inclusive bounds rendered from the committed catalog snapshot, the aesthetic conventions, and the
    /// refinement discipline. Aliases never appear here.
    /// </summary>
    public string RenderPromptText() => _promptText.Value;

    private string RenderPromptTextCore()
    {
        var snapshot = RecipeTemplateCatalogSnapshot.Default;
        var builder = new System.Text.StringBuilder();
        builder.Append("Refinement knowledge for the current template catalog:\n\n");

        builder.Append("Feedback translation table (how spoken feedback maps to parameter actions; ");
        builder.Append("every action stays inside the inclusive [min, max] and stops at the bound):\n");
        foreach (var translation in FeedbackTranslations)
        {
            if (!snapshot.TryGetParameter(translation.TemplateId, translation.Parameter, out var parameter))
            {
                throw new InvalidOperationException("The refinement knowledge asset names an uncommitted parameter.");
            }

            builder.Append("- ")
                .Append(translation.TemplateId).Append('.').Append(translation.Parameter)
                .Append(" (").Append(parameter.Type).Append(", ").Append(parameter.RangeLiteral).Append("): ")
                .Append(translation.Guidance)
                .Append('\n');
        }

        builder.Append('\n').Append("Catalog aesthetic conventions:\n");
        foreach (var convention in AestheticConventions)
        {
            builder.Append("- ").Append(convention).Append('\n');
        }

        builder.Append('\n').Append("Refinement discipline:\n");
        foreach (var rule in RefinementDiscipline)
        {
            builder.Append("- ").Append(rule).Append('\n');
        }

        return builder.ToString();
    }

    private static RecipeRefineKnowledge Load()
    {
        using var stream = typeof(RecipeRefineKnowledge).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The embedded refinement knowledge asset is missing.");
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !string.Equals(ReadString(root, "schema"), SchemaId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The embedded refinement knowledge asset is invalid.");
        }

        if (!root.TryGetProperty("version", out var versionElement) ||
            versionElement.ValueKind != JsonValueKind.Number ||
            !versionElement.TryGetInt32(out var version) ||
            version < 1)
        {
            throw new InvalidOperationException("The embedded refinement knowledge asset is invalid.");
        }

        var translations = new List<RecipeRefineTranslation>();
        foreach (var element in RequiredArray(root, "feedbackTranslations").EnumerateArray())
        {
            translations.Add(new RecipeRefineTranslation(
                ReadString(element, "templateId"),
                ReadString(element, "parameter"),
                ReadStringArray(element, "parameterPaths"),
                ReadString(element, "direction"),
                ReadString(element, "magnitude"),
                ReadStringArray(element, "aliases"),
                ReadStringArray(element, "aliasesZh"),
                ReadString(element, "guidance")));
        }

        if (translations.Count == 0)
        {
            throw new InvalidOperationException("The embedded refinement knowledge asset declares no translations.");
        }

        return new RecipeRefineKnowledge(
            version,
            ReadString(root, "exportedOn"),
            ReadString(root, "sourceSha256"),
            new ReadOnlyCollection<RecipeRefineTranslation>(translations.ToArray()),
            ReadStringArray(root, "aestheticConventions"),
            ReadStringArray(root, "refinementDiscipline"));
    }

    private static string ReadString(JsonElement objectElement, string name)
    {
        if (objectElement.ValueKind != JsonValueKind.Object ||
            !objectElement.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidOperationException("The embedded refinement knowledge asset is invalid.");
        }

        return value.GetString()!;
    }

    private static JsonElement RequiredArray(JsonElement objectElement, string name)
    {
        if (objectElement.ValueKind != JsonValueKind.Object ||
            !objectElement.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("The embedded refinement knowledge asset is invalid.");
        }

        return value;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement objectElement, string name)
    {
        var values = new List<string>();
        foreach (var item in RequiredArray(objectElement, name).EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                throw new InvalidOperationException("The embedded refinement knowledge asset is invalid.");
            }

            values.Add(item.GetString()!);
        }

        if (values.Count == 0)
        {
            throw new InvalidOperationException("The embedded refinement knowledge asset is invalid.");
        }

        return new ReadOnlyCollection<string>(values.ToArray());
    }
}

/// <summary>
/// One feedback translation row. <see cref="Aliases"/> (English word tokens) and <see cref="AliasesZh"/>
/// (Chinese substrings) are the deterministic naming lexicon the override guard matches against feedback;
/// only <see cref="Guidance"/> reaches the prompt.
/// </summary>
public sealed class RecipeRefineTranslation
{
    internal RecipeRefineTranslation(
        string templateId,
        string parameter,
        IReadOnlyList<string> parameterPaths,
        string direction,
        string magnitude,
        IReadOnlyList<string> aliases,
        IReadOnlyList<string> aliasesZh,
        string guidance)
    {
        TemplateId = templateId;
        Parameter = parameter;
        ParameterPaths = parameterPaths;
        Direction = direction;
        Magnitude = magnitude;
        Aliases = aliases;
        AliasesZh = aliasesZh;
        Guidance = guidance;
    }

    public string TemplateId { get; }
    public string Parameter { get; }

    /// <summary>The <c>templateId.parameter</c> path family this row covers (REQ-004-52 coverage unit).</summary>
    public IReadOnlyList<string> ParameterPaths { get; }

    public string Direction { get; }
    public string Magnitude { get; }
    public IReadOnlyList<string> Aliases { get; }
    public IReadOnlyList<string> AliasesZh { get; }
    public string Guidance { get; }

    public override string ToString() => "RecipeRefineTranslation(" + TemplateId + "." + Parameter + ")";
}
