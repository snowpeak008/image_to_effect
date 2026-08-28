using System.Text.Json.Serialization;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Protocol.Status;

public static class StatusDomains
{
    public const string Machine = "MACHINE";
    public const string Visual = "VISUAL";
    public const string UserVerdict = "USER_VERDICT";
    public const string L3 = "L3";
    public const string L4 = "L4";

    internal static bool IsKnown(string value) =>
        value is Machine or Visual or UserVerdict or L3 or L4;
}

/// <summary>Identity-bearing provenance only; this type contains no issue or promotion operation.</summary>
public sealed record StatusProvenance
{
    [JsonConstructor]
    public StatusProvenance(
        string protocolVersion,
        string statusDomain,
        string sourceKind,
        TypedHash sourceIdentity,
        DateTimeOffset observedAtUtc)
    {
        if (!string.Equals(protocolVersion, ProtocolVersions.Current, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported protocol version.", nameof(protocolVersion));
        }

        if (!StatusDomains.IsKnown(statusDomain))
        {
            throw new ArgumentOutOfRangeException(nameof(statusDomain));
        }

        ProtocolVersion = protocolVersion;
        StatusDomain = statusDomain;
        SourceKind = Guard.Token(sourceKind, nameof(sourceKind), 64);
        SourceIdentity = sourceIdentity ?? throw new ArgumentNullException(nameof(sourceIdentity));
        ObservedAtUtc = Guard.Utc(observedAtUtc, nameof(observedAtUtc));
    }

    public StatusProvenance(
        string statusDomain,
        string sourceKind,
        TypedHash sourceIdentity,
        DateTimeOffset observedAtUtc)
        : this(ProtocolVersions.Current, statusDomain, sourceKind, sourceIdentity, observedAtUtc)
    {
    }

    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; }

    [JsonPropertyName("statusDomain")]
    public string StatusDomain { get; }

    [JsonPropertyName("sourceKind")]
    public string SourceKind { get; }

    [JsonPropertyName("sourceIdentity")]
    public TypedHash SourceIdentity { get; }

    [JsonPropertyName("observedAtUtc")]
    public DateTimeOffset ObservedAtUtc { get; }
}
