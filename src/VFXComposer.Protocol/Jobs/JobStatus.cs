using System.Collections.Frozen;
using System.Text.Json.Serialization;
using VFXComposer.Protocol.Diagnostics;

namespace VFXComposer.Protocol.Jobs;

public static class JobStatusStates
{
    public const string Queued = "QUEUED";
    public const string Running = "RUNNING";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";
    public const string Disconnected = "DISCONNECTED";

    internal static FrozenSet<string> All { get; } =
        new[]
        {
            Queued,
            Running,
            Succeeded,
            Failed,
            Cancelled,
            Disconnected,
        }.ToFrozenSet(StringComparer.Ordinal);
}

/// <summary>Local presentation model; it is not a registered Phase 1 wire DTO.</summary>
public sealed record JobStatus
{
    [JsonConstructor]
    public JobStatus(
        JobIdentity identity,
        string state,
        DateTimeOffset updatedAtUtc,
        StableDiagnostic? diagnostic)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        State = JobStatusStates.All.Contains(state)
            ? state
            : throw new ArgumentOutOfRangeException(nameof(state));
        UpdatedAtUtc = Guard.Utc(updatedAtUtc, nameof(updatedAtUtc));
        Diagnostic = diagnostic;
    }

    [JsonPropertyName("identity")]
    public JobIdentity Identity { get; }

    [JsonPropertyName("state")]
    public string State { get; }

    [JsonPropertyName("updatedAtUtc")]
    public DateTimeOffset UpdatedAtUtc { get; }

    [JsonPropertyName("diagnostic")]
    public StableDiagnostic? Diagnostic { get; }
}
