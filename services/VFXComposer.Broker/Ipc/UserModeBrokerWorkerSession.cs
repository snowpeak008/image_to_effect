using System.Diagnostics;
using System.Runtime.Versioning;

namespace VFXComposer.Broker.Ipc;

/// <summary>
/// Owns one direct ordinary-user Worker, its one-use pipe admission, and its
/// mandatory kill-on-close Job. This is lifecycle/session correlation only.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class UserModeBrokerWorkerSession : IAsyncDisposable, IDisposable
{
    private readonly object _gate = new();
    private readonly UserModeNamedPipeServer _server;
    private readonly UserModeChildProcess _child;
    private readonly Task _childExitMonitor;
    private UserModeWorkerConnection? _connection;
    private Task? _disposeTask;

    private UserModeBrokerWorkerSession(
        UserModeNamedPipeServer server,
        UserModeChildProcess child,
        UserModeWorkerConnection connection)
    {
        _server = server;
        _child = child;
        _connection = connection;
        _childExitMonitor = MonitorChildExitAsync(
            child,
            new WeakReference<UserModeBrokerWorkerSession>(this));
    }

    internal long Generation => _server.Generation;

    internal string SessionId => _server.SessionId;

    internal int ChildProcessId => _child.ProcessId;

    internal string ChildProcessEpoch => _child.ProcessEpoch;

    internal string ExpectedExecutablePath => _child.ExpectedExecutablePath;

    internal Stream Transport =>
        Volatile.Read(ref _connection)?.Stream ??
        throw new ObjectDisposedException(nameof(UserModeBrokerWorkerSession));

    internal bool IsUsable =>
        Volatile.Read(ref _disposeTask) is null &&
        Volatile.Read(ref _connection)?.IsConnected == true &&
        _child.HasActiveContainment &&
        _child.IsExactProcessActive;

    internal Task ChildExitMonitor => _childExitMonitor;

    internal static async Task<UserModeBrokerWorkerSession> StartAsync(
        string expectedReleaseExecutablePath,
        ProcessStartInfo workerStartInfo,
        long generation,
        TimeSpan admissionTimeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workerStartInfo);
        UserModeNamedPipeServer? server = null;
        UserModeChildProcess? child = null;
        UserModeWorkerConnection? connection = null;
        try
        {
            server = UserModeNamedPipeServer.Create(generation);
            child = UserModeChildProcess.Launch(
                expectedReleaseExecutablePath,
                workerStartInfo);

            await server.WriteBootstrapAsync(
                    child.StandardInput.BaseStream,
                    cancellationToken)
                .ConfigureAwait(false);
            connection = await server.AcceptAsync(child, admissionTimeout, cancellationToken)
                .ConfigureAwait(false);
            return new UserModeBrokerWorkerSession(server, child, connection);
        }
        catch (Exception primaryFailure)
        {
            var cleanupFailures = new List<Exception>();
            if (connection is not null)
            {
                try
                {
                    await connection.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    cleanupFailures.Add(exception);
                }
            }

            if (child is not null)
            {
                try
                {
                    await child.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    cleanupFailures.Add(exception);
                }
            }

            if (server is not null)
            {
                try
                {
                    await server.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    cleanupFailures.Add(exception);
                }
            }

            if (cleanupFailures.Count != 0)
            {
                throw new AggregateException(
                    "U2FS001",
                    new[] { primaryFailure }.Concat(cleanupFailures));
            }

            throw;
        }
    }

    internal async Task<bool> WaitForChildExitAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var exited = await _child.WaitForExitAsync(timeout, cancellationToken).ConfigureAwait(false);
        if (exited)
        {
            await DisposeAsync().ConfigureAwait(false);
        }

        return exited;
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposeTask is null || _disposeTask.IsFaulted)
            {
                _disposeTask = DisposeCoreAsync();
            }

            return new ValueTask(_disposeTask);
        }
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    ~UserModeBrokerWorkerSession()
    {
        try
        {
            Dispose();
        }
        catch (Exception)
        {
            // Do not promote unobserved cleanup to success from a finalizer.
        }
    }

    private async Task DisposeCoreAsync()
    {
        var failures = new List<Exception>();
        var connection = Interlocked.Exchange(ref _connection, null);
        if (connection is not null)
        {
            try
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        try
        {
            await _child.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        try
        {
            await _server.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        if (failures.Count != 0)
        {
            throw new AggregateException("U2FS001", failures);
        }

        GC.SuppressFinalize(this);
    }

    private static async Task MonitorChildExitAsync(
        UserModeChildProcess child,
        WeakReference<UserModeBrokerWorkerSession> sessionReference)
    {
        try
        {
            await child.WaitForExitAsync(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
            if (sessionReference.TryGetTarget(out var session))
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            // Explicit disposal/finalization retains the authoritative cleanup
            // task; this observer never promotes a failed cleanup to success.
        }
    }
}
