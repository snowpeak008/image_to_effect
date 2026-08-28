using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace VFXComposer.Broker.Native;

internal enum WindowsKillOnCloseJobCloseResult
{
    Closed = 0,
    AlreadyClosed = 1,
    CloseFailed = 2,
}

/// <summary>
/// Mandatory ordinary-user child lifecycle containment. A Job is not a token,
/// filesystem, code-integrity, hostile-user, or sandbox boundary.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsKillOnCloseJob : IDisposable
{
    private const int ExtendedLimitInformationClass = 9;
    private const uint KillOnJobClose = 0x00002000;
    private const int Active = 0;
    private const int Closed = 1;

    private readonly object _gate = new();
    private SafeFileHandle? _jobHandle;
    private int _closeState;

    private WindowsKillOnCloseJob(SafeFileHandle jobHandle) =>
        _jobHandle = jobHandle ?? throw new ArgumentNullException(nameof(jobHandle));

    internal bool IsActive =>
        Volatile.Read(ref _closeState) == Active && _jobHandle is not null;

    internal static bool TryCreate(out WindowsKillOnCloseJob? job)
    {
        job = null;
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        SafeFileHandle? handle = null;
        try
        {
            handle = CreateJobObjectW(IntPtr.Zero, null);
            if (handle.IsInvalid || handle.IsClosed)
            {
                return false;
            }

            var information = new JobObjectExtendedLimitInformation
            {
                BasicLimitInformation = new JobObjectBasicLimitInformation
                {
                    LimitFlags = KillOnJobClose,
                },
            };
            if (!SetInformationJobObject(
                    handle,
                    ExtendedLimitInformationClass,
                    ref information,
                    checked((uint)Marshal.SizeOf<JobObjectExtendedLimitInformation>())))
            {
                return false;
            }

            job = new WindowsKillOnCloseJob(handle);
            handle = null;
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            DllNotFoundException or
            EntryPointNotFoundException or
            MarshalDirectiveException or
            OverflowException or
            SEHException)
        {
            return false;
        }
        finally
        {
            handle?.Dispose();
        }
    }

    internal static WindowsKillOnCloseJob CreateUniqueConfigured()
    {
        if (!TryCreate(out var job) || job is null)
        {
            throw new InvalidOperationException("U2FS001");
        }

        return job;
    }

    internal bool TryAssign(SafeProcessHandle? processHandle)
    {
        if (processHandle is null || processHandle.IsInvalid || processHandle.IsClosed)
        {
            return false;
        }

        lock (_gate)
        {
            if (_closeState != Active || _jobHandle is null ||
                _jobHandle.IsInvalid || _jobHandle.IsClosed)
            {
                return false;
            }

            try
            {
                return AssignProcessToJobObject(_jobHandle, processHandle);
            }
            catch (Exception exception) when (
                exception is ArgumentException or
                DllNotFoundException or
                EntryPointNotFoundException or
                MarshalDirectiveException or
                SEHException)
            {
                return false;
            }
        }
    }

    internal bool TryTerminate(uint exitCode)
    {
        lock (_gate)
        {
            if (_closeState != Active || _jobHandle is null ||
                _jobHandle.IsInvalid || _jobHandle.IsClosed)
            {
                return false;
            }

            try
            {
                return TerminateJobObject(_jobHandle, exitCode);
            }
            catch (Exception exception) when (
                exception is ArgumentException or
                DllNotFoundException or
                EntryPointNotFoundException or
                MarshalDirectiveException or
                SEHException)
            {
                return false;
            }
        }
    }

    internal WindowsKillOnCloseJobCloseResult Close()
    {
        lock (_gate)
        {
            if (_closeState == Closed)
            {
                return WindowsKillOnCloseJobCloseResult.AlreadyClosed;
            }

            if (_jobHandle is null)
            {
                return WindowsKillOnCloseJobCloseResult.CloseFailed;
            }

            var handle = _jobHandle;
            var addRef = false;
            var closed = false;
            try
            {
                handle.DangerousAddRef(ref addRef);
                closed = CloseHandle(handle.DangerousGetHandle());
                if (closed)
                {
                    // Only invalidate after the physical close has been observed.
                    handle.SetHandleAsInvalid();
                    _jobHandle = null;
                    _closeState = Closed;
                }
                else
                {
                    // Retain the exact handle and active state so an explicit
                    // owner or the finalizer can retry physical close.
                    _closeState = Active;
                }
            }
            catch (Exception)
            {
                _closeState = Active;
                closed = false;
            }
            finally
            {
                if (addRef)
                {
                    try
                    {
                        handle.DangerousRelease();
                    }
                    catch (Exception)
                    {
                        _closeState = Active;
                        closed = false;
                    }
                }
            }

            return closed
                ? WindowsKillOnCloseJobCloseResult.Closed
                : WindowsKillOnCloseJobCloseResult.CloseFailed;
        }
    }

    public void Dispose()
    {
        var result = Close();
        if (result is WindowsKillOnCloseJobCloseResult.Closed or
            WindowsKillOnCloseJobCloseResult.AlreadyClosed)
        {
            GC.SuppressFinalize(this);
        }
    }

    ~WindowsKillOnCloseJob() => _ = Close();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true,
        SetLastError = true, CallingConvention = CallingConvention.Winapi)]
    private static extern SafeFileHandle CreateJobObjectW(IntPtr attributes, string? name);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true,
        CallingConvention = CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(
        SafeFileHandle job,
        int informationClass,
        ref JobObjectExtendedLimitInformation information,
        uint informationLength);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true,
        CallingConvention = CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(
        SafeFileHandle job,
        SafeProcessHandle process);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true,
        CallingConvention = CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateJobObject(
        SafeFileHandle job,
        uint exitCode);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true,
        CallingConvention = CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        internal JobObjectBasicLimitInformation BasicLimitInformation;
        internal IoCounters IoInfo;
        internal UIntPtr ProcessMemoryLimit;
        internal UIntPtr JobMemoryLimit;
        internal UIntPtr PeakProcessMemoryUsed;
        internal UIntPtr PeakJobMemoryUsed;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        internal long PerProcessUserTimeLimit;
        internal long PerJobUserTimeLimit;
        internal uint LimitFlags;
        internal UIntPtr MinimumWorkingSetSize;
        internal UIntPtr MaximumWorkingSetSize;
        internal uint ActiveProcessLimit;
        internal UIntPtr Affinity;
        internal uint PriorityClass;
        internal uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        internal ulong ReadOperationCount;
        internal ulong WriteOperationCount;
        internal ulong OtherOperationCount;
        internal ulong ReadTransferCount;
        internal ulong WriteTransferCount;
        internal ulong OtherTransferCount;
    }
}
