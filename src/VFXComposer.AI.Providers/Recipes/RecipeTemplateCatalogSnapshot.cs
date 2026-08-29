using System.Reflection;
using System.Text.Json;
using VFXComposer.AI.Contracts.Chat;

namespace VFXComposer.AI.Providers.Recipes;

/// <summary>
/// Versioned static snapshot of the v1 TemplateCatalog, committed as an embedded resource in the
/// S12SlashAiExporter pattern. It is the only template knowledge source for recipe prompts; the live Unity
/// TemplateCatalog remains authoritative at L2, and no read-only allowlist is extended to fetch it at runtime.
/// </summary>
public sealed class RecipeTemplateCatalogSnapshot
{
    public const string SchemaId = "vfxcomposer.ai.recipe-template-catalog-snapshot/1";

    private const string SnapshotResourceName =
        "VFXComposer.AI.Providers.Recipes.Assets.recipe-v1-template-catalog.snapshot.json";

    private const string RecipeSchemaResourceName =
        "VFXComposer.AI.Providers.Recipes.Assets.recipe-v1.schema.json";

    private static readonly Lazy<RecipeTemplateCatalogSnapshot> Cached = new(Load);
    private static readonly Lazy<JsonElement> CachedRecipeSchema = new(LoadRecipeSchema);

    private RecipeTemplateCatalogSnapshot(
        string templateCatalogVersion,
        string contractRevision,
        IReadOnlyList<string> buildableArchetypes,
        IReadOnlyList<string> buildableDimensions,
        IReadOnlyList<TemplateSnapshot> templates,
        string canonicalExampleJson)
    {
        TemplateCatalogVersion = templateCatalogVersion;
        ContractRevision = contractRevision;
        BuildableArchetypes = buildableArchetypes;
        BuildableDimensions = buildableDimensions;
        Templates = templates;
        CanonicalExampleJson = canonicalExampleJson;
    }

    public static RecipeTemplateCatalogSnapshot Default => Cached.Value;

    public string TemplateCatalogVersion { get; }
    public string ContractRevision { get; }
    public IReadOnlyList<string> BuildableArchetypes { get; }
    public IReadOnlyList<string> BuildableDimensions { get; }
    public IReadOnlyList<TemplateSnapshot> Templates { get; }

    /// <summary>The machine-generated canonical Recipe example, serialized without indentation.</summary>
    public string CanonicalExampleJson { get; }

    public override string ToString() => "RecipeTemplateCatalogSnapshot(" + TemplateCatalogVersion + ")";

    /// <summary>
    /// The committed recipe v1 JSON Schema as a structured-output constraint for protocols that accept one.
    /// Both request forms feed the same parse/validate pipeline afterwards.
    /// </summary>
    public static ChatStructuredOutput CreateStructuredOutput() =>
        new("vfx-recipe-v1", CachedRecipeSchema.Value);

    /// <summary>Renders the deterministic prompt table: one ordinal-ordered row per template parameter.</summary>
    public string RenderPromptTable()
    {
        var builder = new System.Text.StringBuilder();
        builder.Append("templateId | kind | dimension | parameter | type | min | default | max\n");
        foreach (var template in Templates)
        {
            foreach (var parameter in template.Parameters)
            {
                builder
                    .Append(template.TemplateId).Append(" | ")
                    .Append(template.Kind).Append(" | ")
                    .Append(template.Dimension).Append(" | ")
                    .Append(parameter.Name).Append(" | ")
                    .Append(parameter.Type).Append(" | ")
                    .Append(parameter.MinLiteral).Append(" | ")
                    .Append(parameter.DefaultLiteral).Append(" | ")
                    .Append(parameter.MaxLiteral).Append('\n');
            }
        }

        return builder.ToString();
    }

    private static RecipeTemplateCatalogSnapshot Load()
    {
        using var document = JsonDocument.Parse(ReadResource(SnapshotResourceName));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !string.Equals(ReadString(root, "schema"), SchemaId, StringComparison.Ordinal) ||
            ReadInt(root, "recipeVersion") != 1)
        {
            throw new InvalidOperationException("The embedded template catalog snapshot is invalid.");
        }

        var templates = new List<TemplateSnapshot>();
        foreach (var templateElement in RequiredArray(root, "templates").EnumerateArray())
        {
            var parameters = new List<TemplateParameterSnapshot>();
            foreach (var property in RequiredObject(templateElement, "parameters").EnumerateObject())
            {
                parameters.Add(new TemplateParameterSnapshot(
                    property.Name,
                    ReadString(property.Value, "type"),
                    RequiredProperty(property.Value, "min").GetRawText(),
                    RequiredProperty(property.Value, "default").GetRawText(),
                    RequiredProperty(property.Value, "max").GetRawText()));
            }

            templates.Add(new TemplateSnapshot(
                ReadString(templateElement, "templateId"),
                ReadString(templateElement, "version"),
                ReadString(templateElement, "kind"),
                ReadString(templateElement, "dimension"),
                parameters
                    .OrderBy(static parameter => parameter.Name, StringComparer.Ordinal)
                    .ToArray()));
        }

        if (templates.Count == 0)
        {
            throw new InvalidOperationException("The embedded template catalog snapshot declares no templates.");
        }

        return new RecipeTemplateCatalogSnapshot(
            ReadString(root, "templateCatalogVersion"),
            ReadString(root, "contractRevision"),
            ReadStringArray(root, "buildableArchetypes"),
            ReadStringArray(root, "buildableDimensions"),
            templates.OrderBy(static template => template.TemplateId, StringComparer.Ordinal).ToArray(),
            RecipeCanonicalJson.Canonicalize(RequiredObject(root, "canonicalExample")));
    }

    private static JsonElement LoadRecipeSchema()
    {
        using var document = JsonDocument.Parse(ReadResource(RecipeSchemaResourceName));
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("The embedded recipe schema is invalid.");
        }

        return document.RootElement.Clone();
    }

    private static byte[] ReadResource(string resourceName)
    {
        using var stream = typeof(RecipeTemplateCatalogSnapshot).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("An embedded recipe resource is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string ReadString(JsonElement objectElement, string name)
    {
        var value = RequiredProperty(objectElement, name);
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidOperationException("The embedded template catalog snapshot is invalid.");
        }

        return value.GetString()!;
    }

    private static int ReadInt(JsonElement objectElement, string name)
    {
        var value = RequiredProperty(objectElement, name);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var parsed))
        {
            throw new InvalidOperationException("The embedded template catalog snapshot is invalid.");
        }

        return parsed;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement objectElement, string name)
    {
        var values = new List<string>();
        foreach (var item in RequiredArray(objectElement, name).EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                throw new InvalidOperationException("The embedded template catalog snapshot is invalid.");
            }

            values.Add(item.GetString()!);
        }

        return values.ToArray();
    }

    private static JsonElement RequiredProperty(JsonElement objectElement, string name)
    {
        if (objectElement.ValueKind != JsonValueKind.Object || !objectElement.TryGetProperty(name, out var value))
        {
            throw new InvalidOperationException("The embedded template catalog snapshot is invalid.");
        }

        return value;
    }

    private static JsonElement RequiredArray(JsonElement objectElement, string name)
    {
        var value = RequiredProperty(objectElement, name);
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("The embedded template catalog snapshot is invalid.");
        }

        return value;
    }

    private static JsonElement RequiredObject(JsonElement objectElement, string name)
    {
        var value = RequiredProperty(objectElement, name);
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("The embedded template catalog snapshot is invalid.");
        }

        return value;
    }

    /// <summary>One template row of the committed snapshot.</summary>
    public sealed class TemplateSnapshot
    {
        internal TemplateSnapshot(
            string templateId,
            string version,
            string kind,
            string dimension,
            IReadOnlyList<TemplateParameterSnapshot> parameters)
        {
            TemplateId = templateId;
            Version = version;
            Kind = kind;
            Dimension = dimension;
            Parameters = parameters;
        }

        public string TemplateId { get; }
        public string Version { get; }
        public string Kind { get; }
        public string Dimension { get; }
        public IReadOnlyList<TemplateParameterSnapshot> Parameters { get; }

        public override string ToString() => "TemplateSnapshot(" + TemplateId + ")";
    }

    /// <summary>One declared template parameter. Bounds keep their exact committed JSON literals.</summary>
    public sealed class TemplateParameterSnapshot
    {
        internal TemplateParameterSnapshot(string name, string type, string minLiteral, string defaultLiteral, string maxLiteral)
        {
            Name = name;
            Type = type;
            MinLiteral = minLiteral;
            DefaultLiteral = defaultLiteral;
            MaxLiteral = maxLiteral;
        }

        public string Name { get; }
        public string Type { get; }
        public string MinLiteral { get; }
        public string DefaultLiteral { get; }
        public string MaxLiteral { get; }

        public override string ToString() => "TemplateParameterSnapshot(" + Name + ")";
    }
}
