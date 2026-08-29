using System.Text;
using System.Text.Json;

namespace VFXComposer.Jobs;

/// <summary>
/// Strict serializer for the versioned store schema. Unknown members are rejected by the
/// record contracts; any parse or validation failure surfaces as one stable store error.
/// </summary>
internal static class JobStoreCodec
{
    public const int MaximumSnapshotBytes = 33_554_432;
    public const int MaximumEventLineBytes = 8_192;

    private static readonly JsonSerializerOptions SnapshotOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly JsonSerializerOptions EventOptions = new()
    {
        WriteIndented = false,
    };

    public static byte[] SerializeSnapshot(JobStoreSnapshot snapshot) =>
        JsonSerializer.SerializeToUtf8Bytes(snapshot, SnapshotOptions);

    public static JobStoreSnapshot DeserializeSnapshot(byte[] bytes)
    {
        try
        {
            return JsonSerializer.Deserialize<JobStoreSnapshot>(bytes, SnapshotOptions)
                ?? throw new JobQueueException(JobQueueDiagnosticCodes.StoreUnavailable);
        }
        catch (JsonException exception)
        {
            throw new JobQueueException(JobQueueDiagnosticCodes.StoreUnavailable, exception);
        }
        catch (ArgumentException exception)
        {
            throw new JobQueueException(JobQueueDiagnosticCodes.StoreUnavailable, exception);
        }
    }

    public static string SerializeEventLine(JobStoreEvent storeEvent) =>
        JsonSerializer.Serialize(storeEvent, EventOptions);

    public static JobStoreEvent DeserializeEventLine(string line)
    {
        if (Encoding.UTF8.GetByteCount(line) > MaximumEventLineBytes)
        {
            throw new JobQueueException(JobQueueDiagnosticCodes.StoreUnavailable);
        }

        try
        {
            return JsonSerializer.Deserialize<JobStoreEvent>(line, EventOptions)
                ?? throw new JobQueueException(JobQueueDiagnosticCodes.StoreUnavailable);
        }
        catch (JsonException exception)
        {
            throw new JobQueueException(JobQueueDiagnosticCodes.StoreUnavailable, exception);
        }
        catch (ArgumentException exception)
        {
            throw new JobQueueException(JobQueueDiagnosticCodes.StoreUnavailable, exception);
        }
    }
}
