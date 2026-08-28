using System.Text.Json.Serialization;
using VFXComposer.Protocol.Commands;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Protocol.Jobs;

/// <summary>
/// Immutable structural link to one sealed command. It proves neither admission nor
/// execution, freshness, completion authority, or a runtime state transition.
/// </summary>
public sealed record JobCorrelation
{
    [JsonConstructor]
    public JobCorrelation(
        string jobId,
        string originRequestId,
        string originCommandId,
        string originIdempotencyKey,
        string originCommandKind,
        TypedHash originCommandSelfHash)
    {
        JobId = Guard.Token(jobId, nameof(jobId));
        OriginRequestId = Guard.Token(originRequestId, nameof(originRequestId));
        OriginCommandId = Guard.Token(originCommandId, nameof(originCommandId));
        OriginIdempotencyKey = Guard.Token(originIdempotencyKey, nameof(originIdempotencyKey));
        OriginCommandKind = CommandKinds.Require(originCommandKind, nameof(originCommandKind));
        OriginCommandSelfHash = WireModelGuard.TypedHash(
            originCommandSelfHash,
            CommandSelfHashTypes.ForKind(OriginCommandKind),
            nameof(originCommandSelfHash));

        var values = new[] { JobId, OriginRequestId, OriginCommandId, OriginIdempotencyKey };
        if (values.Distinct(StringComparer.Ordinal).Count() != values.Length)
        {
            throw new ArgumentException("Job and origin identities must be structurally distinct.");
        }
    }

    [JsonPropertyName("jobId")] public string JobId { get; }
    [JsonPropertyName("originRequestId")] public string OriginRequestId { get; }
    [JsonPropertyName("originCommandId")] public string OriginCommandId { get; }
    [JsonPropertyName("originIdempotencyKey")] public string OriginIdempotencyKey { get; }
    [JsonPropertyName("originCommandKind")] public string OriginCommandKind { get; }
    [JsonPropertyName("originCommandSelfHash")] public TypedHash OriginCommandSelfHash { get; }
}
