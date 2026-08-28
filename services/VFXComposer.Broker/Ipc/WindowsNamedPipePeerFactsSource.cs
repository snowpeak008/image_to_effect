using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using VFXComposer.Broker.Configuration;
using VFXComposer.Broker.Security;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Broker.Ipc;

/// <summary>
/// Windows OS-side peer facts. The image is opened from its kernel device path with
/// OBJ_DONT_REPARSE; no caller/DOS path or environment value participates.
/// </summary>
internal sealed class WindowsNamedPipePeerFactsSource : IPeerFactsSource
{
    private const uint ProcessQueryLimitedInformation = 0x00001000;
    private const uint ProcessDuplicateHandle = 0x00000040;
    private const uint ProcessSynchronize = 0x00100000;
    private const uint TokenQuery = 0x0008;
    private const int TokenUserClass = 1;
    private const uint FileReadData = 0x00000001;
    private const uint FileReadAttributes = 0x00000080;
    private const uint Synchronize = 0x00100000;
    private const uint FileShareRead = 0x00000001;
    private const uint ObjectCaseInsensitive = 0x00000040;
    private const uint ObjectDontReparse = 0x00001000;
    private const uint FileSequentialOnly = 0x00000004;
    private const uint FileSynchronousIoNonAlert = 0x00000020;
    private const uint FileNonDirectoryFile = 0x00000040;
    private const uint FileOpenReparsePoint = 0x00200000;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeReparsePoint = 0x00000400;
    private const int MaximumImageBytes = 256 * 1024 * 1024;
    private static readonly byte[] TypedHashDomain = Encoding.ASCII.GetBytes(
        "vfxcomposer.typed-sha256.length-prefixed/1\0");

    public ObservedPeerFacts Observe(
        System.IO.Pipes.NamedPipeServerStream connectedPipe,
        string claimedPeerRole)
    {
        ArgumentNullException.ThrowIfNull(connectedPipe);
        if (!OperatingSystem.IsWindows() || !connectedPipe.IsConnected ||
            !GetNamedPipeClientProcessId(connectedPipe.SafePipeHandle, out var processId) ||
            processId == 0 || processId > int.MaxValue)
        {
            throw new InvalidDataException(BrokerDiagnosticCodes.PeerRejected);
        }

        return ObserveProcess(
            checked((int)processId),
            allowHandleDuplication: string.Equals(
                claimedPeerRole,
                Protocol.Ipc.PeerRoles.Worker,
                StringComparison.Ordinal));
    }

    internal static ObservedPeerFacts ObserveProcess(
        int processId,
        bool allowHandleDuplication = false)
    {
        if (!OperatingSystem.IsWindows() || processId <= 0)
        {
            throw new InvalidDataException(BrokerDiagnosticCodes.PeerRejected);
        }

        var process = OpenProcess(
            ProcessQueryLimitedInformation | ProcessSynchronize |
                (allowHandleDuplication ? ProcessDuplicateHandle : 0),
            inheritHandle: false,
            processId);
        if (process.IsInvalid)
        {
            process.Dispose();
            throw new InvalidDataException(BrokerDiagnosticCodes.PeerRejected);
        }

        try
        {
            var epoch = ProcessEpoch.Observe(process, processId);
            var sid = ObserveUserSid(process);
            var nativeImagePath = QueryNativeImagePath(process);
            var image = HashNativeImage(nativeImagePath);
            if (!string.Equals(epoch, ProcessEpoch.Observe(process, processId), StringComparison.Ordinal) ||
                !string.Equals(nativeImagePath, QueryNativeImagePath(process), StringComparison.Ordinal))
            {
                throw new InvalidDataException(BrokerDiagnosticCodes.PeerRejected);
            }

            var result = new ObservedPeerFacts(process, processId, epoch, sid, image);
            process = null!;
            return result;
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static WindowsSid ObserveUserSid(SafeProcessHandle process)
    {
        if (!OpenProcessToken(process, TokenQuery, out var token))
        {
            token?.Dispose();
            throw new InvalidDataException(BrokerDiagnosticCodes.PeerRejected);
        }

        using (token)
        {
            _ = GetTokenInformation(token, TokenUserClass, IntPtr.Zero, 0, out var required);
            if (required <= IntPtr.Size || required > 64 * 1024)
            {
                throw new InvalidDataException(BrokerDiagnosticCodes.PeerRejected);
            }

            var buffer = Marshal.AllocHGlobal(required);
            try
            {
                if (!GetTokenInformation(token, TokenUserClass, buffer, required, out _) ||
                    Marshal.ReadIntPtr(buffer) is var sidPointer &&
                    (sidPointer == IntPtr.Zero || !IsValidSid(sidPointer)))
                {
                    throw new InvalidDataException(BrokerDiagnosticCodes.PeerRejected);
                }

                var sidLength = GetLengthSid(sidPointer);
                if (sidLength == 0 || sidLength > 1024)
                {
                    throw new InvalidDataException(BrokerDiagnosticCodes.PeerRejected);
                }

                var sidBytes = new byte[sidLength];
                Marshal.Copy(sidPointer, sidBytes, 0, checked((int)sidLength));
                try
                {
                    return WindowsSid.FromBinary(sidBytes, WindowsSidPrincipalKind.User);
                }
                catch (ArgumentException)
                {
                    throw new InvalidDataException(BrokerDiagnosticCodes.PeerRejected);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    private static string QueryNativeImagePath(SafeProcessHandle process)
    {
        var builder = new StringBuilder(32768);
        var length = GetProcessImageFileName(process, builder, builder.Capacity);
        if (length == 0 || length >= builder.Capacity)
        {
            throw new InvalidDataException(BrokerDiagnosticCodes.PeerRejected);
        }

        var value = builder.ToString();
        const string prefix = "\\Device\\HarddiskVolume";
        if (!value.StartsWith(prefix, StringComparison.Ordinal) ||
            value.Length <= prefix.Length ||
            !char.IsAsciiDigit(value[prefix.Length]))
        {
            throw new InvalidDataException(BrokerDiagnosticCodes.PeerRejected);
        }

        return value;
    }

    private static TypedHash HashNativeImage(string nativeImagePath)
    {
        var rawHandle = IntPtr.Zero;
        var nameBuffer = IntPtr.Zero;
        var unicodeBuffer = IntPtr.Zero;
        try
        {
            nameBuffer = Marshal.StringToHGlobalUni(nativeImagePath);
            var name = new UnicodeString
            {
                Length = checked((ushort)(nativeImagePath.Length * 2)),
                MaximumLength = checked((ushort)(nativeImagePath.Length * 2 + 2)),
                Buffer = nameBuffer,
            };
            unicodeBuffer = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());
            Marshal.StructureToPtr(name, unicodeBuffer, false);
            var attributes = new ObjectAttributes
            {
                Length = (uint)Marshal.SizeOf<ObjectAttributes>(),
                RootDirectory = IntPtr.Zero,
                ObjectName = unicodeBuffer,
                Attributes = ObjectCaseInsensitive | ObjectDontReparse,
            };
            var status = NtOpenFile(
                out rawHandle,
                FileReadData | FileReadAttributes | Synchronize,
                ref attributes,
                out _,
                FileShareRead,
                FileSequentialOnly | FileSynchronousIoNonAlert |
                FileNonDirectoryFile | FileOpenReparsePoint);
            if (status != 0 || rawHandle == IntPtr.Zero || rawHandle == new IntPtr(-1))
            {
                CloseRawHandle(rawHandle);
                rawHandle = IntPtr.Zero;
                throw new InvalidDataException(BrokerDiagnosticCodes.PeerRejected);
            }

            using var handle = new SafeFileHandle(rawHandle, ownsHandle: true);
            rawHandle = IntPtr.Zero;
            if (!GetFileInformationByHandle(handle, out var before) ||
                (before.FileAttributes & (FileAttributeDirectory | FileAttributeReparsePoint)) != 0 ||
                before.NumberOfLinks != 1)
            {
                throw new InvalidDataException(BrokerDiagnosticCodes.PeerRejected);
            }

            var length = checked((long)(((ulong)before.FileSizeHigh << 32) | before.FileSizeLow));
            if (length <= 0 || length > MaximumImageBytes)
            {
                throw new InvalidDataException(BrokerDiagnosticCodes.PeerRejected);
            }

            var typeBytes = Encoding.UTF8.GetBytes(Protocol.Ipc.PeerHello.ProcessImageIdentityType);
            Span<byte> typeLength = stackalloc byte[4];
            Span<byte> payloadLength = stackalloc byte[8];
            BinaryPrimitives.WriteUInt32BigEndian(typeLength, checked((uint)typeBytes.Length));
            BinaryPrimitives.WriteUInt64BigEndian(payloadLength, checked((ulong)length));
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(TypedHashDomain);
            hash.AppendData(typeLength);
            hash.AppendData(typeBytes);
            hash.AppendData(payloadLength);
            using (var streamHandle = new SafeFileHandle(handle.DangerousGetHandle(), ownsHandle: false))
            using (var stream = new FileStream(streamHandle, FileAccess.Read, 64 * 1024, isAsync: false))
            {
                var buffer = new byte[64 * 1024];
                long total = 0;
                while (total < length)
                {
                    var requested = (int)Math.Min(buffer.Length, length - total);
                    var read = stream.Read(buffer, 0, requested);
                    if (read <= 0)
                    {
                        throw new InvalidDataException(BrokerDiagnosticCodes.PeerRejected);
                    }

                    hash.AppendData(buffer, 0, read);
                    total += read;
                }

                if (stream.ReadByte() != -1)
                {
                    throw new InvalidDataException(BrokerDiagnosticCodes.PeerRejected);
                }
            }

            if (!GetFileInformationByHandle(handle, out var after) || !SameIdentity(before, after))
            {
                throw new InvalidDataException(BrokerDiagnosticCodes.PeerRejected);
            }

            return new TypedHash(
                Protocol.Ipc.PeerHello.ProcessImageIdentityType,
                "sha256:" + Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            OverflowException or
            IOException or
            UnauthorizedAccessException or
            MarshalDirectiveException or
            DllNotFoundException or
            EntryPointNotFoundException)
        {
            CloseRawHandle(rawHandle);
            throw new InvalidDataException(BrokerDiagnosticCodes.PeerRejected);
        }
        finally
        {
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

    private static bool SameIdentity(ByHandleFileInformation left, ByHandleFileInformation right) =>
        left.FileAttributes == right.FileAttributes &&
        left.VolumeSerialNumber == right.VolumeSerialNumber &&
        left.FileSizeHigh == right.FileSizeHigh &&
        left.FileSizeLow == right.FileSizeLow &&
        left.NumberOfLinks == right.NumberOfLinks &&
        left.FileIndexHigh == right.FileIndexHigh &&
        left.FileIndexLow == right.FileIndexLow &&
        left.LastWriteTime.High == right.LastWriteTime.High &&
        left.LastWriteTime.Low == right.LastWriteTime.Low;

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
    private static extern bool GetNamedPipeClientProcessId(
        SafePipeHandle pipe,
        out uint clientProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        int processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(
        SafeProcessHandle processHandle,
        uint desiredAccess,
        out SafeAccessTokenHandle tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(
        SafeAccessTokenHandle tokenHandle,
        int tokenInformationClass,
        IntPtr tokenInformation,
        int tokenInformationLength,
        out int returnLength);

    [DllImport("advapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsValidSid(IntPtr sid);

    [DllImport("advapi32.dll")]
    private static extern uint GetLengthSid(IntPtr sid);

    [DllImport("psapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetProcessImageFileName(
        SafeProcessHandle process,
        StringBuilder imageFileName,
        int size);

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
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle fileHandle,
        out ByHandleFileInformation fileInformation);
}
