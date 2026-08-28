using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace VFXComposer.Broker.Security;

internal static class ProcessEpoch
{
    private const uint ProcessQueryLimitedInformation = 0x00001000;
    private const uint ProcessSynchronize = 0x00100000;
    private const uint WaitTimeout = 0x00000102;

    public static string Observe(int processId)
    {
        if (!OperatingSystem.IsWindows() || processId <= 0)
        {
            throw new InvalidDataException(BrokerDiagnosticCodes.PeerRejected);
        }

        using var process = OpenProcess(
            ProcessQueryLimitedInformation | ProcessSynchronize,
            inheritHandle: false,
            processId);
        if (process.IsInvalid)
        {
            throw new InvalidDataException(BrokerDiagnosticCodes.PeerRejected);
        }

        return Observe(process, processId);
    }

    internal static string Observe(SafeProcessHandle process, int processId)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (process.IsInvalid || processId <= 0 ||
            !GetProcessTimes(process, out var creation, out _, out _, out _))
        {
            throw new InvalidDataException(BrokerDiagnosticCodes.PeerRejected);
        }

        var creationTicks = ((ulong)(uint)creation.High << 32) | (uint)creation.Low;
        return $"winproc-{processId}-{creationTicks:x16}";
    }

    internal static bool IsActive(SafeProcessHandle process) =>
        process is not null &&
        !process.IsInvalid &&
        !process.IsClosed &&
        WaitForSingleObject(process, 0) == WaitTimeout;

    internal static bool IsCanonicalForProcess(int processId, string? processEpoch)
    {
        if (processId <= 0 || processEpoch is null)
        {
            return false;
        }

        var prefix = $"winproc-{processId}-";
        if (!processEpoch.StartsWith(prefix, StringComparison.Ordinal) ||
            processEpoch.Length != prefix.Length + 16)
        {
            return false;
        }

        foreach (var character in processEpoch.AsSpan(prefix.Length))
        {
            if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileTime
    {
        public int Low;
        public int High;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(
        SafeProcessHandle process,
        out NativeFileTime creationTime,
        out NativeFileTime exitTime,
        out NativeFileTime kernelTime,
        out NativeFileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(
        SafeProcessHandle process,
        uint milliseconds);
}
