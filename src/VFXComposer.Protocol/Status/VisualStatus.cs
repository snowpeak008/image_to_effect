using System.Collections.Frozen;
using System.Text.Json.Serialization;

namespace VFXComposer.Protocol.Status;

public static class VisualStatusStates
{
    public const string VisualPending = "VISUAL_PENDING";
    public const string Passed = "PASSED";
    public const string Failed = "FAILED";

    internal static FrozenSet<string> All { get; } =
        new[] { VisualPending, Passed, Failed }.ToFrozenSet(StringComparer.Ordinal);
}

public sealed record VisualStatus
{
    [JsonConstructor]
    public VisualStatus(string protocolVersion, string state, StatusProvenance? provenance)
    {
        if (!string.Equals(protocolVersion, ProtocolVersions.Current, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported protocol version.", nameof(protocolVersion));
        }

        ProtocolVersion = protocolVersion;
        State = StatusGuard.Validate(
            state,
            VisualStatusStates.All,
            StatusDomains.Visual,
            provenance,
            provenanceRequired: state is VisualStatusStates.Passed or VisualStatusStates.Failed);
        Provenance = provenance;
    }

    public VisualStatus(string state, StatusProvenance? provenance = null)
        : this(ProtocolVersions.Current, state, provenance)
    {
    }

    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; }

    [JsonPropertyName("state")]
    public string State { get; }

    [JsonPropertyName("provenance")]
    public StatusProvenance? Provenance { get; }
}
