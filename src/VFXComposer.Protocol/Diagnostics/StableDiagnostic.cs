using System.Collections.Frozen;
using System.Text.Json.Serialization;

namespace VFXComposer.Protocol.Diagnostics;

public static class DiagnosticSeverities
{
    public const string Info = "INFO";
    public const string Warning = "WARNING";
    public const string Error = "ERROR";

    internal static bool IsKnown(string value) =>
        value is Info or Warning or Error;
}

public static class StableDiagnosticCodes
{
    public const string Disconnected = "VFXP0001";
    public const string MalformedMessage = "VFXP0002";
    public const string UnsupportedProtocolVersion = "VFXP0003";
    public const string UnsupportedMessageKind = "VFXP0004";
    public const string CapabilityRejected = "VFXP0005";
    public const string InvalidStatusProvenance = "VFXP0006";
    public const string ProjectLeaseRejected = "VFXP0007";
    public const string ProjectDocumentUnavailable = "VFXP0008";
    public const string ProjectDocumentContentMismatch = "VFXP0009";

    private static readonly FrozenSet<string> KnownCodes = new[]
    {
        Disconnected,
        MalformedMessage,
        UnsupportedProtocolVersion,
        UnsupportedMessageKind,
        CapabilityRejected,
        InvalidStatusProvenance,
        ProjectLeaseRejected,
        ProjectDocumentUnavailable,
        ProjectDocumentContentMismatch,
    }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => KnownCodes;
}

/// <summary>
/// A stable, path-free diagnostic payload. Exception strings are never wire contracts.
/// </summary>
public sealed record StableDiagnostic
{
    public StableDiagnostic(string code, string message)
        : this(
            code,
            StableDiagnosticCatalog.Require(code).Severity,
            message,
            StableDiagnosticCatalog.Require(code).Retryable)
    {
    }

    public StableDiagnostic(string code, string severity, string message, bool retryable)
        : this(ProtocolVersions.Current, MessageKinds.Diagnostic, code, severity, message, retryable)
    {
    }

    [JsonConstructor]
    public StableDiagnostic(
        string protocolVersion,
        string messageKind,
        string code,
        string severity,
        string message,
        bool retryable)
    {
        if (!string.Equals(protocolVersion, ProtocolVersions.Current, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported protocol version.", nameof(protocolVersion));
        }

        if (!string.Equals(messageKind, MessageKinds.Diagnostic, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unexpected message kind.", nameof(messageKind));
        }

        var definition = StableDiagnosticCatalog.Require(code);
        if (!string.Equals(severity, definition.Severity, StringComparison.Ordinal) ||
            !string.Equals(message, definition.Message, StringComparison.Ordinal) ||
            retryable != definition.Retryable)
        {
            throw new ArgumentException("Diagnostic fields do not match the stable catalog.");
        }

        ProtocolVersion = protocolVersion;
        MessageKind = messageKind;
        Code = definition.Code;
        Severity = definition.Severity;
        Message = Guard.DiagnosticMessage(definition.Message, nameof(message));
        Retryable = definition.Retryable;
    }

    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; }

    [JsonPropertyName("messageKind")]
    public string MessageKind { get; }

    [JsonPropertyName("code")]
    public string Code { get; }

    [JsonPropertyName("severity")]
    public string Severity { get; }

    [JsonPropertyName("message")]
    public string Message { get; }

    [JsonPropertyName("retryable")]
    public bool Retryable { get; }
}
