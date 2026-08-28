using System.Collections.Concurrent;
using System.Diagnostics;

namespace VFXComposer.AI.Providers;

/// <summary>
/// Serializes every configuration revision observation and mutation through one durable lock-file anchor.
/// The anchor is intentionally retained after a lease ends; exclusivity comes from the open FileStream handle,
/// not from deleting and recreating a path that another process could race.
/// </summary>
internal sealed class ProviderConfigurationRevisionLock
{
    private const int RetryMilliseconds = 20;
    private static readonly ConcurrentDictionary<string, object> ProcessGates = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _lockPath;
    private readonly TimeSpan _timeout;
    private readonly object _processGate;

    public ProviderConfigurationRevisionLock(string configurationPath, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationPath);
        _lockPath = Path.GetFullPath(configurationPath) + ".lock";
        _timeout = timeout ?? TimeSpan.FromSeconds(10);
        if (_timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        _processGate = ProcessGates.GetOrAdd(_lockPath, static _ => new object());
    }

    internal string LockPath => _lockPath;

    /// <summary>Runs the complete read/revision-check/validation/repair/write operation while holding one lease.</summary>
    internal T Execute<T>(Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        lock (_processGate)
        {
            using var lease = Acquire();
            return operation();
        }
    }

    /// <summary>
    /// Acquires the durable anchor directly. This internal seam exists solely for the isolated process host that
    /// proves OS-level recovery after a holder process is killed; production store operations use <see cref="Execute{T}"/>.
    /// </summary>
    internal FileStream Acquire()
    {
        var parent = Path.GetDirectoryName(_lockPath);
        if (string.IsNullOrEmpty(parent))
        {
            throw new IOException("Provider storage path is invalid.");
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
