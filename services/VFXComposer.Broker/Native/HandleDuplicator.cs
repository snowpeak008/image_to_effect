using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using VFXComposer.Broker.Ipc;
using VFXComposer.Broker.Security;
using VFXComposer.Protocol.Ipc;

namespace VFXComposer.Broker.Native;

internal sealed class DuplicatedProjectHandleSet : IDisposable
{
    private readonly SafeProcessHandle _targetProcess;
    private readonly Action<SafeProcessHandle, IntPtr> _remoteCloser;
    // 0=prepared, 1=published, 2=disposed-unpublished, 3=disposed-published.
    private int _lifecycleState;
    private int _workerConfirmedClosed;

    internal DuplicatedProjectHandleSet(
        SafeProcessHandle targetProcess,
        int targetProcessId,
        string targetProcessEpoch,
        long brokerGeneration,
        IntPtr volumeHandle,
        IntPtr repositoryHandle,
        IntPtr projectRootHandle)
        : this(
            targetProcess,
            targetProcessId,
            targetProcessEpoch,
            brokerGeneration,
            volumeHandle,
            repositoryHandle,
            projectRootHandle,
            HandleDuplicator.CloseRemoteHandle)
    {
    }

    internal DuplicatedProjectHandleSet(
        SafeProcessHandle targetProcess,
        int targetProcessId,
        string targetProcessEpoch,
        long brokerGeneration,
        IntPtr volumeHandle,
        IntPtr repositoryHandle,
        IntPtr projectRootHandle,
        Action<SafeProcessHandle, IntPtr> remoteCloser)
    {
        _targetProcess = targetProcess;
        _remoteCloser = remoteCloser ?? throw new ArgumentNullException(nameof(remoteCloser));
        TargetProcessId = targetProcessId;
        TargetProcessEpoch = targetProcessEpoch;
        BrokerGeneration = brokerGeneration;
        VolumeHandle = volumeHandle;
        RepositoryHandle = repositoryHandle;
        ProjectRootHandle = projectRootHandle;
    }

    public int TargetProcessId { get; }
    public string TargetProcessEpoch { get; }
    public long BrokerGeneration { get; }
    public IntPtr VolumeHandle { get; }
    public IntPtr RepositoryHandle { get; }
    public IntPtr ProjectRootHandle { get; }
    public bool IsPublished => Volatile.Read(ref _lifecycleState) is 1 or 3;
    public bool WorkerConfirmedClosed => Volatile.Read(ref _workerConfirmedClosed) == 1;
    public bool IsTargetProcessActive => ProcessEpoch.IsActive(_targetProcess);

    internal bool TryMarkPublished() =>
        Interlocked.CompareExchange(ref _lifecycleState, 1, 0) == 0;

    internal void ConfirmWorkerClosed()
    {
        if (!IsPublished)
        {
            throw new InvalidOperationException("Unpublished handles cannot be worker-confirmed.");
        }

        Volatile.Write(ref _workerConfirmedClosed, 1);
    }

    public void Dispose()
    {
        int previous;
        while (true)
        {
            previous = Volatile.Read(ref _lifecycleState);
            if (previous is 2 or 3)
            {
                return;
            }

            var disposed = previous == 0 ? 2 : 3;
            if (Interlocked.CompareExchange(ref _lifecycleState, disposed, previous) == previous)
            {
                break;
            }
        }

        // A published raw handle number may already have been closed and reused by the
        // Worker. DUPLICATE_CLOSE_SOURCE is therefore safe only before publication.
        if (previous == 0)
        {
            _remoteCloser(_targetProcess, ProjectRootHandle);
            _remoteCloser(_targetProcess, RepositoryHandle);
            _remoteCloser(_targetProcess, VolumeHandle);
        }

        _targetProcess.Dispose();
    }
}

internal sealed class HandleDuplicator
{
    private const uint Synchronize = 0x00100000;
    private const uint FileTraverse = 0x00000020;
    private const uint FileReadAttributes = 0x00000080;
    private const uint DuplicateCloseSource = 0x00000001;
    private readonly PeerSessionRegistry _sessions;

    public HandleDuplicator(PeerSessionRegistry sessions) =>
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));

    public bool TryDuplicateToWorker(
        WindowsPinnedProjectRoots roots,
        AuthenticatedPeerSession workerSession,
        out DuplicatedProjectHandleSet? duplicated,
        out string diagnosticCode)
    {
        duplicated = null;
        diagnosticCode = BrokerDiagnosticCodes.SessionStale;
        if (roots is null || !roots.ReplayIdentities() ||
            !_sessions.IsCurrent(workerSession, PeerRoles.Worker))
        {
            return false;
        }

        var targetProcess = DuplicateLocalProcessHandle(workerSession.ProcessHandle);
        if (targetProcess.IsInvalid)
        {
            targetProcess.Dispose();
            return false;
        }

        if (!string.Equals(
                ProcessEpoch.Observe(targetProcess, workerSession.ProcessId),
                workerSession.ProcessEpoch,
                StringComparison.Ordinal))
        {
            targetProcess.Dispose();
            return false;
        }

        var remoteHandles = new List<IntPtr>(3);
        try
        {
            remoteHandles.Add(DuplicateReadDirectoryHandle(roots.Volume.Handle, targetProcess));
            remoteHandles.Add(DuplicateReadDirectoryHandle(roots.Repository.Handle, targetProcess));
            remoteHandles.Add(DuplicateReadDirectoryHandle(roots.Project.Handle, targetProcess));
            if (!roots.ReplayIdentities())
            {
                return false;
            }

            duplicated = new DuplicatedProjectHandleSet(
                targetProcess,
                workerSession.ProcessId,
                workerSession.ProcessEpoch,
                workerSession.BrokerGeneration,
                remoteHandles[0],
                remoteHandles[1],
                remoteHandles[2]);
            remoteHandles.Clear();
            targetProcess = null!;
            diagnosticCode = string.Empty;
            return true;
        }
        finally
        {
            if (targetProcess is not null)
            {
                foreach (var remoteHandle in remoteHandles)
                {
                    CloseRemoteHandle(targetProcess, remoteHandle);
                }

                targetProcess.Dispose();
            }
        }
    }

    private static IntPtr DuplicateReadDirectoryHandle(
        SafeFileHandle source,
        SafeProcessHandle targetProcess)
    {
        using var currentProcess = GetCurrentProcess();
        if (!DuplicateHandle(
                currentProcess,
                source.DangerousGetHandle(),
                targetProcess,
                out var remoteHandle,
                FileTraverse | FileReadAttributes | Synchronize,
                inheritHandle: false,
                options: 0) ||
            remoteHandle == IntPtr.Zero || remoteHandle == new IntPtr(-1))
        {
            throw new InvalidDataException(BrokerDiagnosticCodes.SessionStale);
        }

        return remoteHandle;
    }

    private static SafeProcessHandle DuplicateLocalProcessHandle(SafeProcessHandle source)
    {
        using var currentProcess = GetCurrentProcess();
        if (!DuplicateHandle(
                currentProcess,
                source.DangerousGetHandle(),
                currentProcess,
                out var duplicate,
                0,
                inheritHandle: false,
                options: 0x00000002) ||
            duplicate == IntPtr.Zero || duplicate == new IntPtr(-1))
        {
            CloseRawHandle(duplicate);
            throw new InvalidDataException(BrokerDiagnosticCodes.SessionStale);
        }

        return new SafeProcessHandle(duplicate, ownsHandle: true);
    }

    private static void CloseRawHandle(IntPtr handle)
    {
        if (handle != IntPtr.Zero && handle != new IntPtr(-1))
        {
            new SafeFileHandle(handle, ownsHandle: true).Dispose();
        }
    }

    internal static void CloseRemoteHandle(SafeProcessHandle targetProcess, IntPtr remoteHandle)
    {
        if (remoteHandle == IntPtr.Zero || remoteHandle == new IntPtr(-1))
        {
            return;
        }

        using var currentProcess = GetCurrentProcess();
        if (DuplicateHandle(
                targetProcess,
                remoteHandle,
                currentProcess,
                out var localHandle,
                0,
                inheritHandle: false,
                DuplicateCloseSource) &&
            localHandle != IntPtr.Zero && localHandle != new IntPtr(-1))
        {
            new SafeFileHandle(localHandle, ownsHandle: true).Dispose();
        }
    }

    [DllImport("kernel32.dll")]
    private static extern SafeProcessHandle GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateHandle(
        SafeProcessHandle sourceProcessHandle,
        IntPtr sourceHandle,
        SafeProcessHandle targetProcessHandle,
        out IntPtr targetHandle,
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        uint options);
}
