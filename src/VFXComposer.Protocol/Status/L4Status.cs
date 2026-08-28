using System.Collections.Frozen;
using System.Text.Json.Serialization;

namespace VFXComposer.Protocol.Status;

public static class L4StatusStates
{
    public const string NotGranted = "NOT_GRANTED";
    public const string Granted = "GRANTED";
    public const string Revoked = "REVOKED";

    internal static FrozenSet<string> All { get; } =
        new[] { NotGranted, Granted, Revoked }.ToFrozenSet(StringComparer.Ordinal);
}

public sealed record L4Status
{
    [JsonConstructor]
    public L4Status(string protocolVersion, string state, StatusProvenance? provenance)
    {
        if (!string.Equals(protocolVersion, ProtocolVersions.Current, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported protocol version.", nameof(protocolVersion));
        }

        ProtocolVersion = protocolVersion;
        State = StatusGuard.Validate(
            state,
            L4StatusStates.All,
            StatusDomains.L4,
            provenance,
            provenanceRequired: state is L4StatusStates.Granted or L4StatusStates.Revoked);
        Provenance = provenance;
    }

    public L4Status(string state, StatusProvenance? provenance = null)
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
