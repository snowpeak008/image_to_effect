using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using VFXComposer.AI.Contracts.Chat;
using VFXComposer.AI.Contracts.Recipes;

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

    private readonly FrozenDictionary<string, TemplateSnapshot> _templatesById;

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
        _templatesById = templates.ToFrozenDictionary(static template => template.TemplateId, StringComparer.Ordinal);
    }

    public static RecipeTemplateCatalogSnapshot Default => Cached.Value;

    public string TemplateCatalogVersion { get; }
    public string ContractRevision { get; }
    public IReadOnlyList<string> BuildableArchetypes { get; }
    public IReadOnlyList<string> BuildableDimensions { get; }
    public IReadOnlyList<TemplateSnapshot> Templates { get; }

    /// <summary>
    /// The machine-generated canonical Recipe example, serialized without indentation. It mirrors the Unity export
    /// of <c>fireball_2d</c> and is therefore an eight-module effect with attachTo edges that only clears the
    /// project build audit through the legacy per-id exemption. It is a contract fixture, not an authoring model:
    /// the recipe prompt injects its own strict-budget reference recipe instead.
    /// </summary>
    public string CanonicalExampleJson { get; }

    public override string ToString() => "RecipeTemplateCatalogSnapshot(" + TemplateCatalogVersion + ")";

    /// <summary>
    /// The committed recipe v1 JSON Schema as a structured-output constraint for protocols that accept one.
    /// Both request forms feed the same parse/validate pipeline afterwards.
    /// </summary>
    public static ChatStructuredOutput CreateStructuredOutput() =>
        new("vfx-recipe-v1", CachedRecipeSchema.Value);

    /// <summary>Looks up one committed template by its exact id. Unknown ids are not an exception.</summary>
    public bool TryGetTemplate(string templateId, [NotNullWhen(true)] out TemplateSnapshot? template)
    {
        ArgumentNullException.ThrowIfNull(templateId);
        return _templatesById.TryGetValue(templateId, out template);
    }

    /// <summary>
    /// Looks up one declared parameter by template id and parameter name, carrying its type, inclusive
    /// <c>[Minimum, Maximum]</c> bounds and default. This is the single read path for parameter editing surfaces
    /// and for rendering bounds into a suggestion sentence.
    /// </summary>
    public bool TryGetParameter(
        string templateId,
        string parameterName,
        [NotNullWhen(true)] out TemplateParameterSnapshot? parameter)
    {
        ArgumentNullException.ThrowIfNull(parameterName);
        if (!TryGetTemplate(templateId, out var template))
        {
            parameter = null;
            return false;
        }

        return template.TryGetParameter(parameterName, out parameter);
    }

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
        private readonly FrozenDictionary<string, TemplateParameterSnapshot> _parametersByName;

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
            _parametersByName = parameters.ToFrozenDictionary(static parameter => parameter.Name, StringComparer.Ordinal);
        }

        public string TemplateId { get; }
        public string Version { get; }
        public string Kind { get; }
        public string Dimension { get; }

        /// <summary>The declared parameters, ordinal-ordered by name. The set is exhaustive: nothing else is accepted.</summary>
        public IReadOnlyList<TemplateParameterSnapshot> Parameters { get; }

        /// <summary>Looks up one declared parameter by name. Undeclared names are not an exception.</summary>
        public bool TryGetParameter(string parameterName, [NotNullWhen(true)] out TemplateParameterSnapshot? parameter)
        {
            ArgumentNullException.ThrowIfNull(parameterName);
            return _parametersByName.TryGetValue(parameterName, out parameter);
        }

        public override string ToString() => "TemplateSnapshot(" + TemplateId + ")";
    }

    /// <summary>
    /// One declared template parameter. The <c>*Literal</c> members keep the exact committed JSON text for prompt
    /// rendering; the parsed members carry the same values as numbers for bounds checking and parameter editing.
    /// </summary>
    public sealed class TemplateParameterSnapshot
    {
        /// <summary>The closed set of parameter types the committed snapshot may declare.</summary>
        internal const string FloatType = "float";

        internal const string IntegerType = "integer";

        internal TemplateParameterSnapshot(string name, string type, string minLiteral, string defaultLiteral, string maxLiteral)
        {
            if (!string.Equals(type, FloatType, StringComparison.Ordinal) &&
                !string.Equals(type, IntegerType, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The embedded template catalog snapshot is invalid.");
            }

            Name = name;
            Type = type;
            MinLiteral = minLiteral;
            DefaultLiteral = defaultLiteral;
            MaxLiteral = maxLiteral;
            Minimum = ParseLiteral(minLiteral);
            Default = ParseLiteral(defaultLiteral);
            Maximum = ParseLiteral(maxLiteral);
            if (Minimum > Maximum || Default < Minimum || Default > Maximum)
            {
                throw new InvalidOperationException("The embedded template catalog snapshot is invalid.");
            }
        }

        public string Name { get; }
        public string Type { get; }
        public string MinLiteral { get; }
        public string DefaultLiteral { get; }
        public string MaxLiteral { get; }

        /// <summary>The inclusive lower bound.</summary>
        public double Minimum { get; }

        /// <summary>The inclusive upper bound.</summary>
        public double Maximum { get; }

        /// <summary>The catalog default, always inside the inclusive bounds.</summary>
        public double Default { get; }

        /// <summary>True when the declared type only accepts integral values.</summary>
        public bool IsInteger => string.Equals(Type, IntegerType, StringComparison.Ordinal);

        /// <summary>The inclusive bounds rendered as <c>[min, max]</c> from the exact committed literals.</summary>
        public string RangeLiteral => "[" + MinLiteral + ", " + MaxLiteral + "]";

        public override string ToString() => "TemplateParameterSnapshot(" + Name + ")";

        private static double ParseLiteral(string literal) =>
            double.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
            double.IsFinite(value)
                ? value
                : throw new InvalidOperationException("The embedded template catalog snapshot is invalid.");
    }
}
