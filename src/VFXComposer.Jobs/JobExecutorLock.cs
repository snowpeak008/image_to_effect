namespace VFXComposer.Jobs;

/// <summary>
/// Long-lived cross-process single-writer lock guaranteeing that at most one executor host runs
/// per store. Same durable-anchor discipline as <see cref="JobStoreRevisionLock"/>: the anchor
/// file is never deleted, exclusivity is the open <c>FileShare.None</c> handle, and the OS
/// releases the lease when the holder process ends, so a crashed holder can be taken over
/// without any lock-file cleanup race. A second instance fails closed instead of queueing to
/// become a shadow executor.
/// </summary>
internal sealed class JobExecutorLock : IDisposable
{
    private readonly FileStream _lease;

    private JobExecutorLock(FileStream lease) => _lease = lease;

    /// <summary>Acquires the executor lease or throws the stable lock-unavailable error.</summary>
    public static JobExecutorLock Acquire(string lockPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockPath);
        var fullPath = Path.GetFullPath(lockPath);
        var parent = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(parent))
        {
            throw new JobQueueException(JobQueueDiagnosticCodes.StoreUnavailable);
        }

        try
        {
            Directory.CreateDirectory(parent);
            return new JobExecutorLock(new FileStream(
                fullPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.WriteThrough));
        }
        catch (IOException exception)
        {
            throw new JobQueueException(JobQueueDiagnosticCodes.ExecutorLockUnavailable, exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new JobQueueException(JobQueueDiagnosticCodes.ExecutorLockUnavailable, exception);
        }
    }

    public void Dispose() => _lease.Dispose();
}
