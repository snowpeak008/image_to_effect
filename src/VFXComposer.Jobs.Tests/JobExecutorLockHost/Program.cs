using VFXComposer.Jobs;

namespace VFXComposer.Jobs.Tests.JobExecutorLockHost;

/// <summary>
/// A real out-of-process executor host. It acquires the cross-process single-writer executor lock
/// through the production <see cref="JobQueueHost.Start"/> path — not a test-only seam — signals
/// readiness, and then holds the lock until the parent test kills it. Killing it is what proves the
/// OS releases the lease on a crashed holder so a fresh host can take over without any lock-file
/// cleanup race.
/// </summary>
internal static class Program
{
    private const int FailureExitCode = 70;

    public static int Main(string[] args)
    {
        try
        {
            return args.Length == 3 && string.Equals(args[0], "hold-executor", StringComparison.Ordinal)
                ? HoldExecutor(args[1], args[2])
                : FailureExitCode;
        }
        catch (JobQueueException)
        {
            return FailureExitCode;
        }
        catch (IOException)
        {
            return FailureExitCode;
        }
        catch (UnauthorizedAccessException)
        {
            return FailureExitCode;
        }
    }

    private static int HoldExecutor(string storeDirectory, string readyPath)
    {
        var store = new JobStore(storeDirectory);
        var host = new JobQueueHost(store, [new NoopExecutor()]);
        host.Start();
        WriteSignal(readyPath);
        Thread.Sleep(Timeout.Infinite);
        return 0;
    }

    private static void WriteSignal(string path)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(path));
        if (string.IsNullOrEmpty(parent))
        {
            throw new IOException("Signal path is invalid.");
        }

        Directory.CreateDirectory(parent);
        File.WriteAllText(path, "ready");
    }

    private sealed class NoopExecutor : IJobExecutor
    {
        public string JobKind => "noop.hold";

        public bool RequiresProjectLock => false;

        public Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
