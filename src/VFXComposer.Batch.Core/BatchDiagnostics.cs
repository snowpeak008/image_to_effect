using System.Collections.Frozen;
using VFXComposer.Protocol.Diagnostics;

namespace VFXComposer.Batch.Core;

/// <summary>
/// Closed batch entry-surface diagnostic code set (REQ-002 §5.4): B1xx for manifest structure,
/// B2xx for manifest semantics and B3xx for the executable-capability gate. The Protocol wire
/// catalog and the jobs catalog are both sealed in their own assemblies, so this surface carries
/// its own stable codes with the same discipline: fixed, single-line, path-free messages.
/// </summary>
public static class BatchDiagnosticCodes
{
    public const string MalformedJson = "B101";
    public const string ManifestTooLarge = "B102";
    public const string UnsupportedSchemaVersion = "B103";
    public const string MissingRequiredField = "B104";
    public const string UnexpectedFieldType = "B105";
    public const string UnknownField = "B106";
    public const string ValueOutOfRange = "B107";
    public const string UnknownEnumValue = "B108";
    public const string DuplicateItemId = "B109";
    public const string FieldNotAllowedForKind = "B110";
    public const string UnsafeRecipePath = "B111";
    public const string ManifestUnreadable = "B112";

    public const string RecipeFileMissing = "B201";
    public const string RecipeFileNotJsonObject = "B202";
    public const string ComposedDescriptionTooLong = "B203";

    public const string RecipeBuildNotSupported = "B301";
    public const string PromptGenerationUnavailable = "B302";

    public static IReadOnlySet<string> All => BatchDiagnosticCatalog.Codes;
}

/// <summary>One immutable definition per batch diagnostic code.</summary>
public sealed record BatchDiagnosticDefinition(string Code, string Severity, string Message);

/// <summary>Closed catalog resolving batch codes to fixed severities and messages.</summary>
public static class BatchDiagnosticCatalog
{
    private static readonly FrozenDictionary<string, BatchDiagnosticDefinition> Definitions =
        new[]
        {
            new BatchDiagnosticDefinition(
                BatchDiagnosticCodes.MalformedJson,
                DiagnosticSeverities.Error,
                "The manifest is not a well-formed JSON document."),
            new BatchDiagnosticDefinition(
                BatchDiagnosticCodes.ManifestTooLarge,
                DiagnosticSeverities.Error,
                "The manifest exceeds the maximum manifest size."),
            new BatchDiagnosticDefinition(
                BatchDiagnosticCodes.UnsupportedSchemaVersion,
                DiagnosticSeverities.Error,
                "The manifest schema version is not supported."),
            new BatchDiagnosticDefinition(
                BatchDiagnosticCodes.MissingRequiredField,
                DiagnosticSeverities.Error,
                "A required manifest field is missing."),
            new BatchDiagnosticDefinition(
                BatchDiagnosticCodes.UnexpectedFieldType,
                DiagnosticSeverities.Error,
                "A manifest field has the wrong JSON type."),
            new BatchDiagnosticDefinition(
                BatchDiagnosticCodes.UnknownField,
                DiagnosticSeverities.Error,
                "The manifest contains a field that is not part of the schema."),
            new BatchDiagnosticDefinition(
                BatchDiagnosticCodes.ValueOutOfRange,
                DiagnosticSeverities.Error,
                "A manifest value is outside its allowed range."),
            new BatchDiagnosticDefinition(
                BatchDiagnosticCodes.UnknownEnumValue,
                DiagnosticSeverities.Error,
                "A manifest value is outside its closed vocabulary."),
            new BatchDiagnosticDefinition(
                BatchDiagnosticCodes.DuplicateItemId,
                DiagnosticSeverities.Error,
                "Item identifiers must be unique within one manifest."),
            new BatchDiagnosticDefinition(
                BatchDiagnosticCodes.FieldNotAllowedForKind,
                DiagnosticSeverities.Error,
                "The field is not allowed for this item kind."),
            new BatchDiagnosticDefinition(
                BatchDiagnosticCodes.UnsafeRecipePath,
                DiagnosticSeverities.Error,
                "The recipe path is not a contained relative JSON path."),
            new BatchDiagnosticDefinition(
                BatchDiagnosticCodes.ManifestUnreadable,
                DiagnosticSeverities.Error,
                "The manifest file could not be read."),
            new BatchDiagnosticDefinition(
                BatchDiagnosticCodes.RecipeFileMissing,
                DiagnosticSeverities.Error,
                "The referenced recipe file does not exist next to the manifest."),
            new BatchDiagnosticDefinition(
                BatchDiagnosticCodes.RecipeFileNotJsonObject,
                DiagnosticSeverities.Error,
                "The referenced recipe file is not a strict JSON object."),
            new BatchDiagnosticDefinition(
                BatchDiagnosticCodes.ComposedDescriptionTooLong,
                DiagnosticSeverities.Error,
                "The prompt and its constraints exceed the generation channel description bound."),
            new BatchDiagnosticDefinition(
                BatchDiagnosticCodes.RecipeBuildNotSupported,
                DiagnosticSeverities.Error,
                "Recipe build entries are not supported by this build; only prompt generation entries can be executed."),
            new BatchDiagnosticDefinition(
                BatchDiagnosticCodes.PromptGenerationUnavailable,
                DiagnosticSeverities.Error,
                "The recipe generation channel is unbound; a manifest with prompt entries is rejected as a whole."),
        }.ToFrozenDictionary(definition => definition.Code, StringComparer.Ordinal);

    internal static FrozenSet<string> Codes { get; } = Definitions.Keys.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, BatchDiagnosticDefinition> All => Definitions;

    public static BatchDiagnosticDefinition Require(string code) =>
        Definitions.TryGetValue(code, out var definition)
            ? definition
            : throw new ArgumentOutOfRangeException(nameof(code));
}
