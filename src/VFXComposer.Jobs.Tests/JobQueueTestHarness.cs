using VFXComposer.Jobs;

namespace VFXComposer.Jobs.Tests;

/// <summary>Shared synthetic fixtures: isolated store directories, polling waits and fake seams.</summary>
internal static class JobQueueTestHarness
{
    public static readonly JobQueueHostOptions FastOptions = new()
    {
        IdlePollInterval = TimeSpan.FromMilliseconds(25),
        ProjectLockInitialBackoff = TimeSpan.FromMilliseconds(25),
        ProjectLockMaximumBackoff = TimeSpan.FromMilliseconds(100),
        CancellationPollInterval = TimeSpan.FromMilliseconds(25),
        JobTimeout = TimeSpan.FromSeconds(30),
        CancellationGracePeriod = TimeSpan.FromMilliseconds(500),
    };

    public static string CreateStoreDirectory() =>
        Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(),
            "vfxc-jobs-tests",
            Guid.NewGuid().ToString("N"))).FullName;

    public static async Task WaitUntilAsync(Func<bool> condition, int timeoutMilliseconds = 10_000)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("The awaited queue condition was not reached in time.");
            }

            await Task.Delay(25);
        }
    }

    public static JobEnqueueRequest Request(
        string payload = "synthetic-payload",
        string jobKind = "test.job",
        string sourceEntry = JobSourceEntries.Desktop,
        string? batchId = null,
        string? batchPolicy = null,
        string? itemId = null) =>
        new(sourceEntry, jobKind, payload, batchId, batchPolicy, itemId);

    public static JobRecord GetJob(JobStore store, string jobId) =>
        store.ReadSnapshot().Jobs.Single(job => job.JobId == jobId);
}

/// <summary>Configurable payload executor driven by a delegate.</summary>
internal sealed class DelegateJobExecutor : IJobExecutor
{
    private readonly Func<JobExecutionContext, CancellationToken, Task> _execute;

    public DelegateJobExecutor(
        string jobKind,
        Func<JobExecutionContext, CancellationToken, Task> execute,
        bool requiresProjectLock = false)
    {
        JobKind = jobKind;
        _execute = execute;
        RequiresProjectLock = requiresProjectLock;
    }

    public string JobKind { get; }

    public bool RequiresProjectLock { get; }

    public Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken) =>
        _execute(context, cancellationToken);
}

/// <summary>Probe whose availability tests can flip at runtime.</summary>
internal sealed class FakeProjectLockProbe : IProjectLockProbe
{
    private int _busy;

    public bool Busy
    {
        get => Volatile.Read(ref _busy) == 1;
        set => Volatile.Write(ref _busy, value ? 1 : 0);
    }

    public ProjectLockAvailability Probe() =>
        Busy ? ProjectLockAvailability.Busy : ProjectLockAvailability.Free;
}

/// <summary>Records exact-termination requests instead of touching real processes.</summary>
internal sealed class RecordingProcessInspector : IJobProcessInspector
{
    private readonly List<(int ProcessId, DateTimeOffset StartUtc)> _terminated = [];

    public IReadOnlyList<(int ProcessId, DateTimeOffset StartUtc)> Terminated
    {
        get
        {
            lock (_terminated)
            {
                return _terminated.ToArray();
            }
        }
    }

    public void TerminateExact(int processId, DateTimeOffset processStartUtc)
    {
        lock (_terminated)
        {
            _terminated.Add((processId, processStartUtc));
        }
    }
}
