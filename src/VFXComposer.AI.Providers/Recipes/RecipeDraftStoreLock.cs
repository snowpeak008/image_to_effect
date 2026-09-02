using System.Collections.Concurrent;
using System.Diagnostics;
using VFXComposer.AI.Contracts.Recipes;

namespace VFXComposer.AI.Providers.Recipes;

/// <summary>
/// Serializes every load→mutate→persist cycle of one draft store across the Desktop, CLI and MCP processes that
/// share the file (REQ-004 RG-6). Same pattern as <c>ProviderConfigurationRevisionLock</c>: a durable anchor file
/// next to the store whose exclusive open handle is the lease; the anchor is never deleted, so no process can race
/// a delete/recreate. In-process callers on the same path serialize through one Monitor first. Both waits are
/// bounded by the same timeout; exceeding it fails closed with <see cref="RecipeDraftStoreErrorCode.StoreBusy"/>
/// without touching the store file.
/// </summary>
internal sealed class RecipeDraftStoreLock
{
    private const int RetryMilliseconds = 20;
    private static readonly ConcurrentDictionary<string, object> ProcessGates = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _lockPath;
    private readonly TimeSpan _timeout;
    private readonly object _processGate;

    public RecipeDraftStoreLock(string storePath, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storePath);
        _lockPath = Path.GetFullPath(storePath) + ".lock";
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
        if (_timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        _processGate = ProcessGates.GetOrAdd(_lockPath, static _ => new object());
    }

    internal string LockPath => _lockPath;

    /// <summary>Runs one complete store operation while holding the in-process gate and the cross-process lease.</summary>
    internal T Execute<T>(Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!Monitor.TryEnter(_processGate, _timeout))
        {
            throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.StoreBusy);
        }

        try
        {
            using var lease = Acquire();
            return operation();
        }
        finally
        {
            Monitor.Exit(_processGate);
        }
    }

    /// <summary>
    /// Acquires the durable anchor directly. Production code goes through <see cref="Execute{T}"/>; this seam lets a
    /// test hold the anchor exactly like a foreign process would.
    /// </summary>
    internal FileStream Acquire()
    {
        var parent = Path.GetDirectoryName(_lockPath);
        if (string.IsNullOrEmpty(parent))
        {
            throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.StorageFailed);
        }

        try
        {
            Directory.CreateDirectory(parent);
        }
        catch (IOException)
        {
            throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.StorageFailed);
        }
        catch (UnauthorizedAccessException)
        {
            throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.StorageFailed);
        }

        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return new FileStream(
                    _lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.WriteThrough);
            }
            catch (IOException) when (stopwatch.Elapsed < _timeout)
            {
                Thread.Sleep(RetryMilliseconds);
            }
            catch (IOException)
            {
                throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.StoreBusy);
            }
            catch (UnauthorizedAccessException)
            {
                throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.StorageFailed);
            }
        }
    }
}
