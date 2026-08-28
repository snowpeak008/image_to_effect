using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace VFXComposer.Broker.HandleProbe;

/// <summary>Marker for a test-only process; this assembly is not a Worker or deployable Broker component.</summary>
public static class ProbeMarker
{
}

internal static class Program
{
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeReparsePoint = 0x00000400;

    public static int Main(string[] args)
    {
        if (args.Length != 0)
        {
            return UnityWorkerLifecycleHost.TryRun(args);
        }

        var process = System.Diagnostics.Process.GetCurrentProcess();
        Console.Out.WriteLine($"READY {process.Id}");
        Console.Out.Flush();
        var command = Console.In.ReadLine();
        if (command is null)
        {
            return 11;
        }

        var tokens = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var holdUntilClose = tokens.Length == 4 &&
            string.Equals(tokens[0], "VERIFY_HOLD", StringComparison.Ordinal);
        if (tokens.Length != 4 ||
            !(holdUntilClose || string.Equals(tokens[0], "VERIFY", StringComparison.Ordinal)))
        {
            return 12;
        }

        var handles = new SafeFileHandle[3];
        try
        {
            for (var index = 0; index < handles.Length; index++)
            {
                if (!long.TryParse(tokens[index + 1], out var raw) || raw == 0 || raw == -1)
                {
                    return 13;
                }

                handles[index] = new SafeFileHandle(new IntPtr(raw), ownsHandle: true);
                if (handles[index].IsInvalid ||
                    !GetFileInformationByHandle(handles[index], out var information) ||
                    (information.FileAttributes & FileAttributeDirectory) == 0 ||
                    (information.FileAttributes & FileAttributeReparsePoint) != 0 ||
                    !GetHandleInformation(handles[index], out var flags) ||
                    (flags & 1u) != 0)
                {
                    return 14;
                }
            }

            Console.Out.WriteLine("PASS");
            Console.Out.Flush();
            if (holdUntilClose &&
                !string.Equals(Console.In.ReadLine(), "CLOSE", StringComparison.Ordinal))
            {
                return 15;
            }

            return 0;
        }
        finally
        {
            for (var index = handles.Length - 1; index >= 0; index--)
            {
                handles[index]?.Dispose();
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileTime
    {
        public uint Low;
        public uint High;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public NativeFileTime CreationTime;
        public NativeFileTime LastAccessTime;
        public NativeFileTime LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle fileHandle,
        out ByHandleFileInformation fileInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetHandleInformation(
        SafeFileHandle handle,
        out uint flags);
}
