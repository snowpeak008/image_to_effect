using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace VFXComposer.Broker.Tests;

internal static class PinnedScratchTreeCleanup
{
    private const uint DeleteAccess = 0x00010000;
    private const uint Synchronize = 0x00100000;
    private const uint FileTraverse = 0x00000020;
    private const uint FileReadAttributes = 0x00000080;
    private const uint FileShareRead = 0x00000001;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeReparsePoint = 0x00000400;
    private const uint ObjectCaseInsensitive = 0x00000040;
    private const uint ObjectDontReparse = 0x00001000;
    private const uint FileDirectoryFile = 0x00000001;
    private const uint FileSynchronousIoNonAlert = 0x00000020;
    private const uint FileOpenReparsePoint = 0x00200000;
    private const int FileAttributeTagInfoClass = 9;
    private const int FileDispositionInfoClass = 4;

    internal static void DeleteExactEmptyTree(
        string project,
        string repository,
        string scratch)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException();
        }

        var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        var expectedScratch = Path.GetFullPath(scratch);
        var expectedRepository = Path.GetFullPath(repository);
        var expectedProject = Path.GetFullPath(project);
        if (!string.Equals(Path.GetDirectoryName(expectedScratch), temp, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetDirectoryName(expectedRepository), expectedScratch, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetDirectoryName(expectedProject), expectedRepository, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Scratch cleanup ancestry is invalid.");
        }

        var tempDirectory = new DirectoryInfo(temp);
        if (!tempDirectory.Exists ||
            (tempDirectory.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("Scratch cleanup Temp root is not physical.");
        }

        PinnedDirectory? scratchPin = null;
        PinnedDirectory? repositoryPin = null;
        PinnedDirectory? projectPin = null;
        try
        {
            scratchPin = OpenAbsolute(expectedScratch);
            repositoryPin = OpenChild(scratchPin, Path.GetFileName(expectedRepository), expectedRepository);
            projectPin = OpenChild(repositoryPin, Path.GetFileName(expectedProject), expectedProject);

            RequireExactChildSet(scratchPin, expectedRepository);
            RequireExactChildSet(repositoryPin, expectedProject);
            RequireExactChildSet(projectPin, expectedChild: null);

            DeleteByHandle(projectPin);
            projectPin = null;
            RequireExactChildSet(repositoryPin, expectedChild: null);

            DeleteByHandle(repositoryPin);
            repositoryPin = null;
            RequireExactChildSet(scratchPin, expectedChild: null);

            DeleteByHandle(scratchPin);
            scratchPin = null;
        }
        finally
        {
            projectPin?.Dispose();
            repositoryPin?.Dispose();
            scratchPin?.Dispose();
        }
    }

    private static PinnedDirectory OpenAbsolute(string expectedPath)
    {
        var handle = CreateFileW(
            expectedPath,
            DeleteAccess | FileTraverse | FileReadAttributes | Synchronize,
            FileShareRead,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics | FileFlagOpenReparsePoint,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw new InvalidOperationException("Scratch cleanup root could not be pinned.");
        }

        return Adopt(handle, expectedPath);
    }

    private static PinnedDirectory OpenChild(
        PinnedDirectory parent,
        string segment,
        string expectedPath)
    {
        if (string.IsNullOrEmpty(segment) ||
            segment.IndexOfAny(['/', '\\', ':']) >= 0 ||
            segment is "." or "..")
        {
            throw new InvalidOperationException("Scratch cleanup segment is invalid.");
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
            parent.Handle.DangerousAddRef(ref parentAddedRef);
            var attributes = new ObjectAttributes
            {
                Length = (uint)Marshal.SizeOf<ObjectAttributes>(),
                RootDirectory = parent.Handle.DangerousGetHandle(),
                ObjectName = unicodeBuffer,
                Attributes = ObjectCaseInsensitive | ObjectDontReparse,
            };
            var status = NtOpenFile(
                out rawHandle,
                DeleteAccess | FileTraverse | FileReadAttributes | Synchronize,
                ref attributes,
                out _,
                FileShareRead,
                FileDirectoryFile | FileSynchronousIoNonAlert | FileOpenReparsePoint);
            if (status != 0 || rawHandle == IntPtr.Zero || rawHandle == new IntPtr(-1))
            {
                CloseRawHandle(rawHandle);
                rawHandle = IntPtr.Zero;
                throw new InvalidOperationException("Scratch cleanup child could not be pinned.");
            }

            var handle = new SafeFileHandle(rawHandle, ownsHandle: true);
            rawHandle = IntPtr.Zero;
            return Adopt(handle, expectedPath);
        }
        finally
        {
            CloseRawHandle(rawHandle);
            if (parentAddedRef)
            {
                parent.Handle.DangerousRelease();
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

    private static PinnedDirectory Adopt(SafeFileHandle handle, string expectedPath)
    {
        try
        {
            if (!GetFileInformationByHandleEx(
                    handle,
                    FileAttributeTagInfoClass,
                    out FileAttributeTagInfo attributes,
                    (uint)Marshal.SizeOf<FileAttributeTagInfo>()) ||
                (attributes.FileAttributes & FileAttributeDirectory) == 0 ||
                (attributes.FileAttributes & FileAttributeReparsePoint) != 0 ||
                !string.Equals(
                    GetStrictDosPath(handle),
                    expectedPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Scratch cleanup identity is invalid.");
            }

            return new PinnedDirectory(handle, expectedPath);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private static void RequireExactChildSet(
        PinnedDirectory directory,
        string? expectedChild)
    {
        directory.Replay();
        var entries = new DirectoryInfo(directory.ExpectedPath)
            .EnumerateFileSystemInfos()
            .ToArray();
        if (expectedChild is null)
        {
            if (entries.Length != 0)
            {
                throw new InvalidOperationException("Scratch cleanup directory is not empty.");
            }

            return;
        }

        if (entries.Length != 1 ||
            !string.Equals(
                Path.GetFullPath(entries[0].FullName),
                expectedChild,
                StringComparison.OrdinalIgnoreCase) ||
            (entries[0].Attributes & FileAttributes.ReparsePoint) != 0 ||
            (entries[0].Attributes & FileAttributes.Directory) == 0)
        {
            throw new InvalidOperationException(
                "Scratch cleanup directory children are not the exact owned tree.");
        }
    }

    private static void DeleteByHandle(PinnedDirectory directory)
    {
        directory.Replay();
        var disposition = new FileDispositionInfo { DeleteFile = true };
        if (!SetFileInformationByHandle(
                directory.Handle,
                FileDispositionInfoClass,
                ref disposition,
                (uint)Marshal.SizeOf<FileDispositionInfo>()))
        {
            throw new InvalidOperationException("Scratch directory delete disposition failed.");
        }

        var expectedPath = directory.ExpectedPath;
        directory.Dispose();
        if (Directory.Exists(expectedPath) || File.Exists(expectedPath))
        {
            throw new InvalidOperationException("Scratch directory remained after handle deletion.");
        }
    }

    private static string GetStrictDosPath(SafeFileHandle handle)
    {
        var capacity = 512;
        while (capacity <= 32768)
        {
            var buffer = new StringBuilder(capacity);
            var length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, 0);
            if (length == 0)
            {
                throw new InvalidOperationException("Scratch cleanup final path lookup failed.");
            }

            if (length < buffer.Capacity)
            {
                var value = buffer.ToString();
                if (value.Length < 7 ||
                    !value.StartsWith("\\\\?\\", StringComparison.Ordinal) ||
                    !char.IsAsciiLetter(value[4]) ||
                    value[5] != ':' ||
                    value[6] != '\\')
                {
                    throw new InvalidOperationException("Scratch cleanup final path is not DOS form.");
                }

                return Path.GetFullPath(value[4..]);
            }

            capacity = checked((int)length + 1);
        }

        throw new InvalidOperationException("Scratch cleanup final path is too long.");
    }

    private static void CloseRawHandle(IntPtr handle)
    {
        if (handle != IntPtr.Zero && handle != new IntPtr(-1))
        {
            new SafeFileHandle(handle, ownsHandle: true).Dispose();
        }
    }

    private sealed class PinnedDirectory : IDisposable
    {
        private SafeFileHandle? _handle;

        internal PinnedDirectory(SafeFileHandle handle, string expectedPath)
        {
            _handle = handle;
            ExpectedPath = expectedPath;
        }

        internal SafeFileHandle Handle => _handle
            ?? throw new ObjectDisposedException(nameof(PinnedDirectory));

        internal string ExpectedPath { get; }

        internal void Replay()
        {
            var handle = Handle;
            if (!GetFileInformationByHandleEx(
                    handle,
                    FileAttributeTagInfoClass,
                    out FileAttributeTagInfo attributes,
                    (uint)Marshal.SizeOf<FileAttributeTagInfo>()) ||
                (attributes.FileAttributes & FileAttributeDirectory) == 0 ||
                (attributes.FileAttributes & FileAttributeReparsePoint) != 0 ||
                !string.Equals(
                    GetStrictDosPath(handle),
                    ExpectedPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Scratch cleanup identity drifted.");
            }
        }

        public void Dispose() => Interlocked.Exchange(ref _handle, null)?.Dispose();
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
    private struct FileDispositionInfo
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool DeleteFile;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(
        SafeFileHandle fileHandle,
        StringBuilder filePath,
        uint filePathSize,
        uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(
        SafeFileHandle fileHandle,
        int fileInformationClass,
        ref FileDispositionInfo fileInformation,
        uint bufferSize);
}
