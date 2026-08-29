using System.Collections.Concurrent;
using System.Diagnostics;

namespace VFXComposer.Jobs;

/// <summary>
/// Serializes every store observation and mutation through one durable lock-file anchor,
/// following the provider revision-lock pattern: the anchor is intentionally retained after a
/// lease ends, and exclusivity comes from the open <see cref="FileStream"/> handle rather than
/// from deleting and recreating a path that another process could race.
/// </summary>
internal sealed class JobStoreRevisionLock
{
    private const int RetryMilliseconds = 20;
    private static readonly ConcurrentDictionary<string, object> ProcessGates = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _lockPath;
    private readonly TimeSpan _timeout;
    private readonly object _processGate;

    public JobStoreRevisionLock(string storePath, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storePath);
        _lockPath = Path.GetFullPath(storePath) + ".lock";
        _timeout = timeout ?? TimeSpan.FromSeconds(10);
        if (_timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        _processGate = ProcessGates.GetOrAdd(_lockPath, static _ => new object());
    }

    internal string LockPath => _lockPath;

    /// <summary>Runs one complete read/validate/mutate/write operation while holding one lease.</summary>
    internal T Execute<T>(Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        lock (_processGate)
        {
            using var lease = Acquire();
            return operation();
        }
    }

    private FileStream Acquire()
    {
        var parent = Path.GetDirectoryName(_lockPath);
        if (string.IsNullOrEmpty(parent))
        {
            throw new IOException("Job store path is invalid.");
        }

        Directory.CreateDirectory(parent);
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
        }
    }
}
