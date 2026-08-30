using System.Buffers;
using System.Collections.Frozen;
using System.Text;
using System.Text.Json;

namespace VFXComposer.Mcp;

/// <summary>
/// The closed tool-name vocabulary of REQ-002 §7.2 plus the batch-level cancellation tool the
/// §6.2 errata added. Adding a tool is a requirement change, so there is no dynamic registration
/// and no name is ever composed at runtime.
/// </summary>
public static class McpToolNames
{
    public const string ValidateManifest = "vfx_validate_manifest";
    public const string SubmitBatch = "vfx_submit_batch";
    public const string GenerateEffect = "vfx_generate_effect";
    public const string BatchStatus = "vfx_batch_status";
    public const string JobStatus = "vfx_job_status";
    public const string CancelJob = "vfx_cancel_job";
    public const string CancelBatch = "vfx_cancel_batch";
    public const string GetBatchReport = "vfx_get_batch_report";
}

/// <summary>
/// One tool as advertised by <c>tools/list</c>. The input schema is documentation for the client:
/// it describes the bounds, while the bounds themselves are enforced by the hand-written argument
/// binder, matching the decision not to take a JSON Schema validation dependency.
/// </summary>
public sealed record McpTool
{
    public McpTool(string name, string description, string inputSchemaJson)
    {
        Name = name;
        Description = description;
        InputSchemaJson = Compact(inputSchemaJson);
    }

    public string Name { get; }

    public string Description { get; }

    /// <summary>
    /// The schema as it goes on the wire: schemas are authored readably in source and normalised
    /// here, because the newline-delimited framing has no room for a document's line breaks.
    /// </summary>
    public string InputSchemaJson { get; }

    public override string ToString() => "McpTool(" + Name + ")";

    private static string Compact(string json)
    {
        using var document = JsonDocument.Parse(json);
        var buffer = new ArrayBufferWriter<byte>(json.Length);
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            document.RootElement.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}

/// <summary>The closed tool set, in advertisement order.</summary>
public static class McpToolCatalog
{
    private const int MaximumManifestCharacters = 512 * 1024;

    private static readonly McpTool[] Ordered =
    [
        new McpTool(
            McpToolNames.ValidateManifest,
            "Validate a batch manifest document. No network, no writes and no enqueue.",
            ManifestSchema("manifest")),
        new McpTool(
            McpToolNames.SubmitBatch,
            "Enqueue every entry of a batch manifest in manifest order and return the batch and job identifiers.",
            SubmitSchema()),
        new McpTool(
            McpToolNames.GenerateEffect,
            "Enqueue one manifest entry as a single-entry batch and return its job identifier.",
            GenerateSchema()),
        new McpTool(
            McpToolNames.BatchStatus,
            "Report the queue state and every queue entry of one batch.",
            IdentifierSchema("batchId", "The batch identifier returned at submission time.")),
        new McpTool(
            McpToolNames.JobStatus,
            "Report the state, progress, diagnostic and artifact identities of one queue entry.",
            IdentifierSchema("jobId", "The job identifier returned at submission time.")),
        new McpTool(
            McpToolNames.CancelJob,
            "Request cancellation of one queue entry and report the acceptance outcome.",
            IdentifierSchema("jobId", "The job identifier to cancel.")),
        new McpTool(
            McpToolNames.CancelBatch,
            "Request cancellation of every entry of one batch and report the acceptance summary.",
            IdentifierSchema("batchId", "The batch identifier whose entries are cancelled.")),
        new McpTool(
            McpToolNames.GetBatchReport,
            "Return the vfxcomposer.batch-report/1 document of one batch, derived from the queue entries.",
            IdentifierSchema("batchId", "The batch identifier to report on.")),
    ];

    private static readonly FrozenDictionary<string, McpTool> ByName =
        Ordered.ToFrozenDictionary(tool => tool.Name, StringComparer.Ordinal);

    public static IReadOnlyList<McpTool> All => Ordered;

    public static IReadOnlySet<string> Names { get; } = ByName.Keys.ToFrozenSet(StringComparer.Ordinal);

    public static McpTool? Find(string name) => ByName.TryGetValue(name, out var tool) ? tool : null;

    private static string ManifestSchema(string property) =>
        """
        {
          "type": "object",
          "properties": {
            "__NAME__": {
              "type": "string",
              "maxLength": __MAX__,
              "description": "A vfxcomposer.batch-manifest/1 document as JSON text."
            }
          },
          "required": ["__NAME__"],
          "additionalProperties": false
        }
        """
            .Replace("__NAME__", property, StringComparison.Ordinal)
            .Replace("__MAX__", MaximumManifestCharacters.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static string SubmitSchema() =>
        """
        {
          "type": "object",
          "properties": {
            "manifest": {
              "type": "string",
              "maxLength": __MAX__,
              "description": "A vfxcomposer.batch-manifest/1 document as JSON text."
            },
            "onFailure": {
              "type": "string",
              "enum": ["continue", "abort"],
              "description": "Overrides the manifest failure policy."
            }
          },
          "required": ["manifest"],
          "additionalProperties": false
        }
        """
            .Replace("__MAX__", MaximumManifestCharacters.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static string GenerateSchema() =>
        """
        {
          "type": "object",
          "properties": {
            "item": {
              "type": "object",
              "description": "One vfxcomposer.batch-manifest/1 items[] element.",
              "properties": {
                "itemId": { "type": "string", "maxLength": 96 },
                "kind": { "type": "string", "enum": ["prompt", "recipe"] },
                "prompt": { "type": "string", "maxLength": 8192 },
                "recipePath": { "type": "string", "maxLength": 256 },
                "constraints": {
                  "type": "object",
                  "properties": {
                    "archetype": { "type": "string", "maxLength": 96 },
                    "dimension": { "type": "string", "enum": ["2d", "3d"] },
                    "element": { "type": "string", "maxLength": 96 },
                    "style": { "type": "string", "maxLength": 96 },
                    "targetProfile": { "type": "string", "maxLength": 96 },
                    "randomSeed": { "type": "integer" }
                  },
                  "additionalProperties": false
                }
              },
              "required": ["itemId", "kind"],
              "additionalProperties": false
            }
          },
          "required": ["item"],
          "additionalProperties": false
        }
        """;

    private static string IdentifierSchema(string property, string description) =>
        """
        {
          "type": "object",
          "properties": {
            "__NAME__": {
              "type": "string",
              "maxLength": 128,
              "description": "__DESCRIPTION__"
            }
          },
          "required": ["__NAME__"],
          "additionalProperties": false
        }
        """
            .Replace("__NAME__", property, StringComparison.Ordinal)
            .Replace("__DESCRIPTION__", description, StringComparison.Ordinal);
}
