using System.Collections.Frozen;
using System.Text.Json.Serialization;

namespace VFXComposer.Protocol.Status;

public static class MachineStatusStates
{
    public const string Unknown = "UNKNOWN";
    public const string Pending = "PENDING";
    public const string Passed = "PASSED";
    public const string Failed = "FAILED";

    internal static FrozenSet<string> All { get; } =
        new[] { Unknown, Pending, Passed, Failed }.ToFrozenSet(StringComparer.Ordinal);
}

public sealed record MachineStatus
{
    [JsonConstructor]
    public MachineStatus(string protocolVersion, string state, StatusProvenance? provenance)
    {
        if (!string.Equals(protocolVersion, ProtocolVersions.Current, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported protocol version.", nameof(protocolVersion));
        }

        ProtocolVersion = protocolVersion;
        State = StatusGuard.Validate(
            state,
            MachineStatusStates.All,
            StatusDomains.Machine,
            provenance,
            provenanceRequired: state is MachineStatusStates.Passed or MachineStatusStates.Failed);
        Provenance = provenance;
    }

    public MachineStatus(string state, StatusProvenance? provenance = null)
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
