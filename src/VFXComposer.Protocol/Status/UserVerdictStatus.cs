using System.Collections.Frozen;
using System.Text.Json.Serialization;

namespace VFXComposer.Protocol.Status;

public static class UserVerdictStatusStates
{
    public const string NotSigned = "NOT_SIGNED";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";

    internal static FrozenSet<string> All { get; } =
        new[] { NotSigned, Approved, Rejected }.ToFrozenSet(StringComparer.Ordinal);
}

public sealed record UserVerdictStatus
{
    [JsonConstructor]
    public UserVerdictStatus(string protocolVersion, string state, StatusProvenance? provenance)
    {
        if (!string.Equals(protocolVersion, ProtocolVersions.Current, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported protocol version.", nameof(protocolVersion));
        }

        ProtocolVersion = protocolVersion;
        State = StatusGuard.Validate(
            state,
            UserVerdictStatusStates.All,
            StatusDomains.UserVerdict,
            provenance,
            provenanceRequired: state is UserVerdictStatusStates.Approved or UserVerdictStatusStates.Rejected);
        Provenance = provenance;
    }

    public UserVerdictStatus(string state, StatusProvenance? provenance = null)
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
