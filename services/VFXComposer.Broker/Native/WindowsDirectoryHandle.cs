using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace VFXComposer.Broker.Native;

internal sealed class WindowsDirectoryHandle : IDisposable
{
    private const uint Synchronize = 0x00100000;
    private const uint FileTraverse = 0x00000020;
    private const uint FileReadAttributes = 0x00000080;
    private const uint FileShareRead = 0x00000001;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeReparsePoint = 0x00000400;
    private const uint ObjectCaseInsensitive = 0x00000040;
    private const uint ObjectDontReparse = 0x00001000;
    private const uint FileDirectoryFile = 0x00000001;
    private const uint FileSynchronousIoNonAlert = 0x00000020;
    private const uint FileOpenReparsePoint = 0x00200000;
    private const int FileAttributeTagInfoClass = 9;
    private const int FileIdInfoClass = 18;

    private WindowsDirectoryHandle(SafeFileHandle handle, NativeDirectoryIdentity identity)
    {
        Handle = handle;
        Identity = identity;
    }

    internal SafeFileHandle Handle { get; }
    public NativeDirectoryIdentity Identity { get; }

    public void Dispose() => Handle.Dispose();

    internal static WindowsDirectoryHandle AdoptAndVerify(
        SafeFileHandle handle,
        ulong? expectedVolume = null)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw new InvalidDataException(BrokerDiagnosticCodes.ProjectUnavailable);
        }

        try
        {
            var identity = QueryIdentity(handle);
            if ((identity.FileAttributes & FileAttributeDirectory) == 0 ||
                (identity.FileAttributes & FileAttributeReparsePoint) != 0 ||
                expectedVolume.HasValue && identity.VolumeSerialNumber != expectedVolume.Value)
            {
                throw new InvalidDataException(BrokerDiagnosticCodes.ProjectUnavailable);
            }

            return new WindowsDirectoryHandle(handle, identity);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    internal WindowsDirectoryHandle OpenChild(string segment)
    {
        if (!IsSafeSegment(segment))
        {
            throw new InvalidDataException(BrokerDiagnosticCodes.ProjectUnavailable);
        }

        var rawHandle = IntPtr.Zero;
        var parentAddedRef = false;
        var nameBuffer = IntPtr.Zero;
        var unicodeBuffer = IntPtr.Zero;
        try
        {
            nameBuffer = Marshal.StringToHGlobalUni(segment);
            var name = new UnicodeString
            {
                Length = checked((ushort)(segment.Length * 2)),
                MaximumLength = checked((ushort)(segment.Length * 2 + 2)),
                Buffer = nameBuffer,
            };
            unicodeBuffer = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());
            Marshal.StructureToPtr(name, unicodeBuffer, false);
            Handle.DangerousAddRef(ref parentAddedRef);
            var attributes = new ObjectAttributes
            {
                Length = (uint)Marshal.SizeOf<ObjectAttributes>(),
                RootDirectory = Handle.DangerousGetHandle(),
                ObjectName = unicodeBuffer,
                Attributes = ObjectCaseInsensitive | ObjectDontReparse,
            };
            var status = NtOpenFile(
                out rawHandle,
                FileTraverse | FileReadAttributes | Synchronize,
                ref attributes,
                out _,
                FileShareRead,
                FileDirectoryFile | FileSynchronousIoNonAlert | FileOpenReparsePoint);
            if (status != 0 || rawHandle == IntPtr.Zero || rawHandle == new IntPtr(-1))
            {
                CloseRawHandle(rawHandle);
                rawHandle = IntPtr.Zero;
                throw new InvalidDataException(BrokerDiagnosticCodes.ProjectUnavailable);
            }

            var child = new SafeFileHandle(rawHandle, ownsHandle: true);
            rawHandle = IntPtr.Zero;
            return AdoptAndVerify(child, Identity.VolumeSerialNumber);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            OverflowException or
            InvalidOperationException or
            MarshalDirectiveException or
            DllNotFoundException or
            EntryPointNotFoundException)
        {
            CloseRawHandle(rawHandle);
            throw new InvalidDataException(BrokerDiagnosticCodes.ProjectUnavailable);
        }
        finally
        {
            if (parentAddedRef)
            {
                Handle.DangerousRelease();
            }

            if (unicodeBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(unicodeBuffer);
            }

            if (nameBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(nameBuffer);
            }
        }
    }

    internal bool ReplayIdentity()
    {
        if (Handle.IsClosed || Handle.IsInvalid)
        {
            return false;
        }

        try
        {
            return Identity.FixedEquals(QueryIdentity(Handle));
        }
        catch (Exception exception) when (
            exception is InvalidDataException or ObjectDisposedException)
        {
            return false;
        }
    }

    internal static bool IsSafeSegment(string? segment)
    {
        if (string.IsNullOrEmpty(segment) || segment.Length > 255 ||
            segment is "." or ".." ||
            segment[^1] is '.' or ' ' ||
            segment.Any(character => character <= '\u001f' || character == '\u007f') ||
            segment.IndexOfAny(['<', '>', ':', '"', '/', '\\', '|', '?', '*']) >= 0)
        {
            return false;
        }

        var stem = segment.Split('.')[0].ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" or "CLOCK$")
        {
            return false;
        }

        return !(stem.Length == 4 &&
                 (stem.StartsWith("COM", StringComparison.Ordinal) ||
                  stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                 stem[3] is >= '1' and <= '9');
    }

    private static NativeDirectoryIdentity QueryIdentity(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandleEx(
                handle,
                FileAttributeTagInfoClass,
                out FileAttributeTagInfo attributes,
                (uint)Marshal.SizeOf<FileAttributeTagInfo>()) ||
            !GetFileInformationByHandleEx(
                handle,
                FileIdInfoClass,
                out FileIdInfo identity,
                (uint)Marshal.SizeOf<FileIdInfo>()))
        {
            throw new InvalidDataException(BrokerDiagnosticCodes.ProjectUnavailable);
        }

        return new NativeDirectoryIdentity(
            identity.VolumeSerialNumber,
            new FileIdentity128(identity.FileIdLow, identity.FileIdHigh),
            attributes.FileAttributes);
    }

    private static void CloseRawHandle(IntPtr handle)
    {
        if (handle != IntPtr.Zero && handle != new IntPtr(-1))
        {
            new SafeFileHandle(handle, ownsHandle: true).Dispose();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ObjectAttributes
    {
        public uint Length;
        public IntPtr RootDirectory;
        public IntPtr ObjectName;
        public uint Attributes;
        public IntPtr SecurityDescriptor;
        public IntPtr SecurityQualityOfService;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock
    {
        public IntPtr StatusOrPointer;
        public UIntPtr Information;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileAttributeTagInfo
    {
        public uint FileAttributes;
        public uint ReparseTag;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileIdInfo
    {
        public ulong VolumeSerialNumber;
        public ulong FileIdLow;
        public ulong FileIdHigh;
    }

    [DllImport("ntdll.dll", ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
    private static extern int NtOpenFile(
        out IntPtr fileHandle,
        uint desiredAccess,
        ref ObjectAttributes objectAttributes,
        out IoStatusBlock ioStatusBlock,
        uint shareAccess,
        uint openOptions);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle fileHandle,
        int fileInformationClass,
        out FileAttributeTagInfo fileInformation,
        uint bufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle fileHandle,
        int fileInformationClass,
        out FileIdInfo fileInformation,
        uint bufferSize);
}
