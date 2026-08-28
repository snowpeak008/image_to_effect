using System.Text.Json.Serialization;

namespace VFXComposer.Protocol.Jobs;

/// <summary>
/// Correlation-only local model; it is not a registered Phase 1 wire DTO and conveys
/// no command or project capability.
/// </summary>
public sealed record JobIdentity
{
    [JsonConstructor]
    public JobIdentity(string requestId, string jobId, string idempotencyKey)
    {
        RequestId = Guard.Token(requestId, nameof(requestId));
        JobId = Guard.Token(jobId, nameof(jobId));
        IdempotencyKey = Guard.Token(idempotencyKey, nameof(idempotencyKey));
    }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("jobId")]
    public string JobId { get; }

    [JsonPropertyName("idempotencyKey")]
    public string IdempotencyKey { get; }
}
