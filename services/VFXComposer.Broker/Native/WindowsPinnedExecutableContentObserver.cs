using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using VFXComposer.Broker.Configuration;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Broker.Native;

/// <summary>
/// Observes bytes from one caller-owned, already-open Windows file handle.
/// The returned value is a dormant fact only: it neither identifies a loaded
/// image nor grants any capability.
/// </summary>
internal static class WindowsPinnedExecutableContentObserver
{
    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeReparsePoint = 0x00000400;
    private const uint HandleFlagInherit = 0x00000001;
    private const uint FileTypeDisk = 0x00000001;
    private const uint FileDeviceDisk = 0x00000007;
    private const uint FileRemoteDevice = 0x00000010;
    private const uint FileBegin = 0;
    private const int StatusSuccess = 0;
    private const int FileFsDeviceInformationClass = 4;
    private const int FileIdInfoClass = 18;
    private const int BufferLength = 64 * 1024;
    internal const long MaximumObservedByteLength = 67_108_864;

    private static readonly byte[] TypedHashDomain = Encoding.ASCII.GetBytes(
        "vfxcomposer.typed-sha256.length-prefixed/1\0");
    private static readonly byte[] ExecutableContentTypeBytes = Encoding.UTF8.GetBytes(
        WindowsServiceExecutableContentIdentity.ExecutableContentIdentityType);

    /// <summary>
    /// Uses an independent non-inheritable read handle obtained from the supplied
    /// borrowed handle. Failures are intentionally indistinguishable and return
    /// no native status or caller-controlled location data.
    /// </summary>
    internal static bool TryObserve(
        SafeFileHandle? borrowedHandle,
        out WindowsPinnedExecutableContentObservation? observation)
    {
        observation = null;
        if (!OperatingSystem.IsWindows() ||
            borrowedHandle is null ||
            borrowedHandle.IsClosed ||
            borrowedHandle.IsInvalid)
        {
            return false;
        }

        var sourceReferenceAdded = false;
        var succeeded = false;
        try
        {
            borrowedHandle.DangerousAddRef(ref sourceReferenceAdded);
            if (borrowedHandle.IsClosed || borrowedHandle.IsInvalid)
            {
                return false;
            }

            using var reopenedHandle = ReOpenFile(
                borrowedHandle,
                GenericRead,
                FileShareRead,
                flagsAndAttributes: 0);
            if (reopenedHandle.IsClosed ||
                reopenedHandle.IsInvalid ||
                !SetHandleInformation(reopenedHandle, HandleFlagInherit, 0) ||
                !GetHandleInformation(reopenedHandle, out var handleFlags) ||
                (handleFlags & HandleFlagInherit) != 0 ||
                !TryRequireLocalDiskFileSystemDevice(reopenedHandle) ||
                !TryReadSnapshot(reopenedHandle, out var before) ||
                !TryComputeExactHash(reopenedHandle, before.ByteLength, out var firstHash) ||
                firstHash is null ||
                !TryReadSnapshot(reopenedHandle, out var between) ||
                !before.FixedEquals(between) ||
                !TryComputeExactHash(reopenedHandle, before.ByteLength, out var secondHash) ||
                secondHash is null ||
                !TryReadSnapshot(reopenedHandle, out var after) ||
                !before.FixedEquals(after) ||
                !firstHash.FixedTimeEquals(secondHash))
            {
                return false;
            }

            observation = new WindowsPinnedExecutableContentObservation(
                firstHash,
                checked((long)before.ByteLength),
                new WindowsPinnedExecutableFileIdentity(
                    before.VolumeSerialNumber,
                    before.FileIdLow,
                    before.FileIdHigh));
            succeeded = true;
        }
        catch (Exception)
        {
            observation = null;
        }
        finally
        {
            if (sourceReferenceAdded)
            {
                try
                {
                    borrowedHandle.DangerousRelease();
                }
                catch (Exception)
                {
                    observation = null;
                    succeeded = false;
                }
            }
        }

        return succeeded;
    }

    /// <summary>
    /// Classifies the filesystem only through the independently owned reopen.
    /// No caller-supplied location or volume fact participates in this decision.
    /// </summary>
    private static bool TryRequireLocalDiskFileSystemDevice(SafeFileHandle handle)
    {
        if (handle.IsClosed ||
            handle.IsInvalid ||
            NtQueryVolumeInformationFile(
                handle,
                out var ioStatusBlock,
                out var deviceInformation,
                (uint)Marshal.SizeOf<FileFsDeviceInformation>(),
                FileFsDeviceInformationClass) != StatusSuccess ||
            ioStatusBlock.StatusOrPointer != IntPtr.Zero)
        {
            return false;
        }

        return IsLocalDiskFileSystemDevice(
            deviceInformation.DeviceType,
            deviceInformation.Characteristics);
    }

    private static bool IsLocalDiskFileSystemDevice(
        uint deviceType,
        uint characteristics) =>
        deviceType == FileDeviceDisk &&
        (characteristics & FileRemoteDevice) == 0;

    private static bool TryReadSnapshot(
        SafeFileHandle handle,
        out NativeFileSnapshot snapshot)
    {
        snapshot = default;
        if (handle.IsClosed ||
            handle.IsInvalid ||
            GetFileType(handle) != FileTypeDisk ||
            !GetFileInformationByHandle(handle, out var basic) ||
            !GetFileInformationByHandleEx(
                handle,
                FileIdInfoClass,
                out var fileId,
                (uint)Marshal.SizeOf<FileIdInfo>()) ||
            (basic.FileAttributes & (FileAttributeDirectory | FileAttributeReparsePoint)) != 0 ||
            basic.NumberOfLinks != 1 ||
            (uint)fileId.VolumeSerialNumber != basic.VolumeSerialNumber)
        {
            return false;
        }

        var byteLength = ((ulong)basic.FileSizeHigh << 32) | basic.FileSizeLow;
        if (byteLength is 0 or > MaximumObservedByteLength)
        {
            return false;
        }

        var lastWriteTime = ((ulong)basic.LastWriteTime.HighDateTime << 32) |
            basic.LastWriteTime.LowDateTime;
        snapshot = new NativeFileSnapshot(
            fileId.VolumeSerialNumber,
            fileId.FileIdLow,
            fileId.FileIdHigh,
            basic.FileAttributes,
            basic.NumberOfLinks,
            byteLength,
            lastWriteTime);
        return true;
    }

    private static bool TryComputeExactHash(
        SafeFileHandle handle,
        ulong expectedByteLength,
        out TypedHash? contentHash)
    {
        contentHash = null;
        if (expectedByteLength is 0 or > MaximumObservedByteLength ||
            !SetFilePointerEx(handle, 0, out var position, FileBegin) ||
            position != 0)
        {
            return false;
        }

        Span<byte> typeLength = stackalloc byte[sizeof(uint)];
        Span<byte> payloadLength = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt32BigEndian(
            typeLength,
            checked((uint)ExecutableContentTypeBytes.Length));
        BinaryPrimitives.WriteUInt64BigEndian(payloadLength, expectedByteLength);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(TypedHashDomain);
        hash.AppendData(typeLength);
        hash.AppendData(ExecutableContentTypeBytes);
        hash.AppendData(payloadLength);

        var buffer = new byte[BufferLength];
        var remaining = expectedByteLength;
        while (remaining > 0)
        {
            var requested = checked((uint)Math.Min((ulong)buffer.Length, remaining));
            if (!ReadFile(handle, buffer, requested, out var bytesRead, IntPtr.Zero) ||
                bytesRead == 0 ||
                bytesRead > requested)
            {
                return false;
            }

            hash.AppendData(buffer.AsSpan(0, checked((int)bytesRead)));
            remaining -= bytesRead;
        }

        var eofProbe = new byte[1];
        if (!ReadFile(handle, eofProbe, 1, out var eofBytesRead, IntPtr.Zero) ||
            eofBytesRead != 0)
        {
            return false;
        }

        contentHash = new TypedHash(
            WindowsServiceExecutableContentIdentity.ExecutableContentIdentityType,
            "sha256:" + Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
        return true;
    }

    private readonly struct NativeFileSnapshot
    {
        internal NativeFileSnapshot(
            ulong volumeSerialNumber,
            ulong fileIdLow,
            ulong fileIdHigh,
            uint fileAttributes,
            uint numberOfLinks,
            ulong byteLength,
            ulong lastWriteTime)
        {
            VolumeSerialNumber = volumeSerialNumber;
            FileIdLow = fileIdLow;
            FileIdHigh = fileIdHigh;
            FileAttributes = fileAttributes;
            NumberOfLinks = numberOfLinks;
            ByteLength = byteLength;
            LastWriteTime = lastWriteTime;
        }

        internal ulong VolumeSerialNumber { get; }

        internal ulong FileIdLow { get; }

        internal ulong FileIdHigh { get; }

        internal uint FileAttributes { get; }

        internal uint NumberOfLinks { get; }

        internal ulong ByteLength { get; }

        internal ulong LastWriteTime { get; }

        internal bool FixedEquals(NativeFileSnapshot other) =>
            VolumeSerialNumber == other.VolumeSerialNumber &&
            FileIdLow == other.FileIdLow &&
            FileIdHigh == other.FileIdHigh &&
            FileAttributes == other.FileAttributes &&
            NumberOfLinks == other.NumberOfLinks &&
            ByteLength == other.ByteLength &&
            LastWriteTime == other.LastWriteTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock
    {
        internal IntPtr StatusOrPointer;
        internal UIntPtr Information;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileFsDeviceInformation
    {
        internal uint DeviceType;
        internal uint Characteristics;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileTime
    {
        internal uint LowDateTime;
        internal uint HighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        internal uint FileAttributes;
        internal NativeFileTime CreationTime;
        internal NativeFileTime LastAccessTime;
        internal NativeFileTime LastWriteTime;
        internal uint VolumeSerialNumber;
        internal uint FileSizeHigh;
        internal uint FileSizeLow;
        internal uint NumberOfLinks;
        internal uint FileIndexHigh;
        internal uint FileIndexLow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileIdInfo
    {
        internal ulong VolumeSerialNumber;
        internal ulong FileIdLow;
        internal ulong FileIdHigh;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeFileHandle ReOpenFile(
        SafeFileHandle originalFile,
        uint desiredAccess,
        uint shareMode,
        uint flagsAndAttributes);

    [DllImport("ntdll.dll", ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
    private static extern int NtQueryVolumeInformationFile(
        SafeFileHandle fileHandle,
        out IoStatusBlock ioStatusBlock,
        out FileFsDeviceInformation fsInformation,
        uint length,
        int fsInformationClass);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetHandleInformation(
        SafeFileHandle handle,
        uint mask,
        uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetHandleInformation(
        SafeFileHandle handle,
        out uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetFileType(SafeFileHandle handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle handle,
        out ByHandleFileInformation fileInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle handle,
        int fileInformationClass,
        out FileIdInfo fileInformation,
        uint bufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFilePointerEx(
        SafeFileHandle handle,
        long distanceToMove,
        out long newFilePointer,
        uint moveMethod);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadFile(
        SafeFileHandle handle,
        byte[] buffer,
        uint numberOfBytesToRead,
        out uint numberOfBytesRead,
        IntPtr overlapped);
}

/// <summary>
/// Immutable opaque native identity for an observed local file object.
/// </summary>
internal sealed class WindowsPinnedExecutableFileIdentity
{
    private readonly ulong _volumeSerialNumber;
    private readonly ulong _fileIdLow;
    private readonly ulong _fileIdHigh;

    internal WindowsPinnedExecutableFileIdentity(
        ulong volumeSerialNumber,
        ulong fileIdLow,
        ulong fileIdHigh)
    {
        _volumeSerialNumber = volumeSerialNumber;
        _fileIdLow = fileIdLow;
        _fileIdHigh = fileIdHigh;
    }

    internal bool FixedEquals(WindowsPinnedExecutableFileIdentity? other) =>
        other is not null &&
        _volumeSerialNumber == other._volumeSerialNumber &&
        _fileIdLow == other._fileIdLow &&
        _fileIdHigh == other._fileIdHigh;
}

/// <summary>
/// Immutable output from the dormant pinned-handle byte observer.
/// </summary>
internal sealed class WindowsPinnedExecutableContentObservation
{
    internal WindowsPinnedExecutableContentObservation(
        TypedHash contentHash,
        long byteLength,
        WindowsPinnedExecutableFileIdentity fileIdentity)
    {
        ContentHash = contentHash ?? throw new ArgumentNullException(nameof(contentHash));
        if (byteLength is <= 0 or > WindowsPinnedExecutableContentObserver.MaximumObservedByteLength)
        {
            throw new ArgumentOutOfRangeException(nameof(byteLength));
        }

        ByteLength = byteLength;
        FileIdentity = fileIdentity ?? throw new ArgumentNullException(nameof(fileIdentity));
    }

    internal TypedHash ContentHash { get; }

    internal long ByteLength { get; }

    internal WindowsPinnedExecutableFileIdentity FileIdentity { get; }
}
