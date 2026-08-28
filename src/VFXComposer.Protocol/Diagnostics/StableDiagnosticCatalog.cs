using System.Collections.Frozen;

namespace VFXComposer.Protocol.Diagnostics;

public sealed record StableDiagnosticDefinition(
    string Code,
    string Severity,
    string Message,
    bool Retryable);

/// <summary>Closed diagnostic catalog; wire messages never contain caller-authored text.</summary>
public static class StableDiagnosticCatalog
{
    private static readonly FrozenDictionary<string, StableDiagnosticDefinition> Definitions =
        new[]
        {
            new StableDiagnosticDefinition(
                StableDiagnosticCodes.Disconnected,
                DiagnosticSeverities.Info,
                "No broker or Unity worker connection is active.",
                Retryable: true),
            new StableDiagnosticDefinition(
                StableDiagnosticCodes.MalformedMessage,
                DiagnosticSeverities.Error,
                "The wire message is malformed.",
                Retryable: false),
            new StableDiagnosticDefinition(
                StableDiagnosticCodes.UnsupportedProtocolVersion,
                DiagnosticSeverities.Error,
                "The protocol version is unsupported.",
                Retryable: false),
            new StableDiagnosticDefinition(
                StableDiagnosticCodes.UnsupportedMessageKind,
                DiagnosticSeverities.Error,
                "The wire message kind is unsupported.",
                Retryable: false),
            new StableDiagnosticDefinition(
                StableDiagnosticCodes.CapabilityRejected,
                DiagnosticSeverities.Error,
                "The requested capability is unsupported.",
                Retryable: false),
            new StableDiagnosticDefinition(
                StableDiagnosticCodes.InvalidStatusProvenance,
                DiagnosticSeverities.Error,
                "Status provenance is invalid.",
                Retryable: false),
            new StableDiagnosticDefinition(
                StableDiagnosticCodes.ProjectLeaseRejected,
                DiagnosticSeverities.Error,
                "The project lease is unavailable or no longer current.",
                Retryable: true),
            new StableDiagnosticDefinition(
                StableDiagnosticCodes.ProjectDocumentUnavailable,
                DiagnosticSeverities.Error,
                "The requested project document is unavailable.",
                Retryable: true),
            new StableDiagnosticDefinition(
                StableDiagnosticCodes.ProjectDocumentContentMismatch,
                DiagnosticSeverities.Error,
                "The project document does not match the requested content identity.",
                Retryable: true),
        }.ToFrozenDictionary(definition => definition.Code, StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, StableDiagnosticDefinition> All => Definitions;

    public static StableDiagnostic Create(string code)
    {
        var definition = Require(code);
        return new StableDiagnostic(
            definition.Code,
            definition.Severity,
            definition.Message,
            definition.Retryable);
    }

    public static bool TryGet(string code, out StableDiagnosticDefinition? definition) =>
        Definitions.TryGetValue(code, out definition);

    internal static StableDiagnosticDefinition Require(string code) =>
        Definitions.TryGetValue(code, out var definition)
            ? definition
            : throw new ArgumentOutOfRangeException(nameof(code));
}
