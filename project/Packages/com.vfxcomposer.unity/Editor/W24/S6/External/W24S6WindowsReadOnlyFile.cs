using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace VFXComposer.Editor.W24.S6.External
{
    internal sealed class W24S6PinnedReadFailure : Exception
    {
        internal string Code { get; }
        internal W24S6PinnedReadFailure(string code, string message) : base(message) { Code = code; }
    }

    internal sealed class W24S6PinnedDirectorySegment
    {
        internal SafeFileHandle Handle { get; }
        internal string ExpectedDosPath { get; }
        internal uint VolumeSerialNumber { get; }
        internal ulong FileIndex { get; }
        internal uint FileAttributes { get; }

        internal W24S6PinnedDirectorySegment(SafeFileHandle handle, string expectedDosPath, uint volumeSerialNumber,
            ulong fileIndex, uint fileAttributes)
        {
            Handle = handle;
            ExpectedDosPath = expectedDosPath;
            VolumeSerialNumber = volumeSerialNumber;
            FileIndex = fileIndex;
            FileAttributes = fileAttributes;
        }
    }

    internal sealed class W24S6PinnedReadRoot : IDisposable
    {
        private readonly W24S6PinnedDirectorySegment[] directoryChain;
        internal string DeclaredPath { get; }
        internal string FinalPath { get; }
        internal uint VolumeSerialNumber { get; }
        internal ulong FileIndex { get; }
        internal SafeFileHandle DirectoryHandle { get { return directoryChain[directoryChain.Length - 1].Handle; } }
        internal IReadOnlyList<W24S6PinnedDirectorySegment> DirectoryChain { get { return Array.AsReadOnly(directoryChain); } }

        internal W24S6PinnedReadRoot(IEnumerable<W24S6PinnedDirectorySegment> directoryChain, string declaredPath,
            string finalPath, uint volumeSerialNumber, ulong fileIndex)
        {
            this.directoryChain = directoryChain.ToArray();
            if (this.directoryChain.Length == 0) throw new ArgumentException("At least one pinned root handle is required.", nameof(directoryChain));
            DeclaredPath = declaredPath;
            FinalPath = finalPath;
            VolumeSerialNumber = volumeSerialNumber;
            FileIndex = fileIndex;
        }

        public void Dispose()
        {
            for (var index = directoryChain.Length - 1; index >= 0; index--) directoryChain[index].Handle.Dispose();
        }
    }

    internal sealed class W24S6PinnedReadBytes
    {
        internal byte[] Bytes { get; }
        internal uint VolumeSerialNumber { get; }
        internal ulong FileIndex { get; }

        internal W24S6PinnedReadBytes(byte[] bytes, uint volumeSerialNumber, ulong fileIndex)
        {
            Bytes = bytes;
            VolumeSerialNumber = volumeSerialNumber;
            FileIndex = fileIndex;
        }
    }

    /// <summary>
    /// Windows-only pinned read primitive. Only the trusted fixed DOS drive bootstrap uses an
    /// absolute CreateFileW. Every later directory and target component is a single counted name
    /// opened relative to an already pinned parent handle, without following a reparse point.
    /// </summary>
    internal static class W24S6WindowsReadOnlyFile
    {
        private const uint Synchronize = 0x00100000;
        private const uint FileReadData = 0x00000001;
        private const uint FileTraverse = 0x00000020;
        private const uint FileReadAttributes = 0x00000080;
        private const uint FileShareRead = 0x00000001;
        private const uint OpenExisting = 3;
        private const uint FileAttributeDirectory = 0x00000010;
        private const uint FileAttributeReparsePoint = 0x00000400;
        private const uint FileFlagBackupSemantics = 0x02000000;
        private const uint FileFlagOpenReparsePoint = 0x00200000;
        private const uint DriveFixed = 3;
        private const uint ObjectAttributesCaseInsensitive = 0x00000040;
        private const uint ObjectAttributesDontReparse = 0x00001000;
        private const uint FileDirectoryFile = 0x00000001;
        private const uint FileSynchronousIoNonAlert = 0x00000020;
        private const uint FileNonDirectoryFile = 0x00000040;
        private const uint FileOpenReparsePoint = 0x00200000;
        private const int MaximumFinalPathCharacters = 32768;
        private const int MaximumDosPathCharacters = 259;
        private const int MaximumRegisteredRootCharacters = 240;

#if UNITY_INCLUDE_TESTS
        private static int openAttemptCount;
        private static int targetOpenAttemptCount;
        private static int driveTypeQueryCount;
        internal static Action BeforePostReadIdentityReplayForTests;
        internal static int OpenAttemptCountForTests { get { return openAttemptCount; } }
        internal static int TargetOpenAttemptCountForTests { get { return targetOpenAttemptCount; } }
        internal static int DriveTypeQueryCountForTests { get { return driveTypeQueryCount; } }
        internal static uint ShareModeForTests { get { return FileShareRead; } }
        internal static void ResetOpenAttemptCountForTests()
        {
            openAttemptCount = 0;
            targetOpenAttemptCount = 0;
            driveTypeQueryCount = 0;
            BeforePostReadIdentityReplayForTests = null;
        }
        internal static bool FileMetadataAcceptedForTests(uint attributes, uint links, ulong bytes)
        {
            return FileMetadataAccepted(attributes, links, bytes);
        }
        internal static bool DirectoryMetadataAcceptedForTests(uint attributes, uint volume, uint expectedVolume)
        {
            return DirectoryMetadataAccepted(attributes, volume, expectedVolume);
        }
        internal static bool FinalIdentityAcceptedForTests(string expectedDosPath, string actualNativeFinalPath, uint rootVolume, uint fileVolume)
        {
            return rootVolume == fileVolume && string.Equals(CanonicalDeclaredDosPath(expectedDosPath, "W24FS998"),
                CanonicalFinalPath(actualNativeFinalPath, "W24FS998"), StringComparison.OrdinalIgnoreCase);
        }
        internal static bool IdentityFieldsMatchForTests(uint leftVolume, ulong leftIndex, uint leftLinks, uint leftAttributes, ulong leftBytes, long leftWriteTime,
            uint rightVolume, ulong rightIndex, uint rightLinks, uint rightAttributes, ulong rightBytes, long rightWriteTime)
        {
            return leftVolume == rightVolume && leftIndex == rightIndex && leftLinks == rightLinks && leftAttributes == rightAttributes
                && leftBytes == rightBytes && leftWriteTime == rightWriteTime;
        }
#endif

        internal static bool TryNormalizeRegisteredRoot(string value, out string normalized)
        {
            return TryNormalizeLocalDosPath(value, MaximumRegisteredRootCharacters, out normalized);
        }

        internal static bool IsFixedLocalDrive(string absolutePath)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;
            string normalized;
            return TryNormalizeRegisteredRoot(absolutePath, out normalized) && QueryDriveType(normalized.Substring(0, 3)) == DriveFixed;
        }

        internal static W24S6PinnedReadRoot PinRoot(string declaredRoot)
        {
            RequireWindows();
            string normalized;
            if (!TryNormalizeRegisteredRoot(declaredRoot, out normalized))
                throw new W24S6PinnedReadFailure("W24FS102", "The registered root is not a canonical bounded DOS drive path.");
            var driveRoot = normalized.Substring(0, 3);
            if (QueryDriveType(driveRoot) != DriveFixed)
                throw new W24S6PinnedReadFailure("W24FS102", "The registered root is not on a fixed local drive.");

            var chain = new List<W24S6PinnedDirectorySegment>();
            try
            {
#if UNITY_INCLUDE_TESTS
                openAttemptCount++;
#endif
                var driveHandle = CreateFile(driveRoot, FileTraverse | FileReadAttributes | Synchronize, FileShareRead,
                    IntPtr.Zero, OpenExisting, FileFlagBackupSemantics | FileFlagOpenReparsePoint, IntPtr.Zero);
                if (driveHandle == null || driveHandle.IsInvalid)
                {
                    if (driveHandle != null) driveHandle.Dispose();
                    throw new W24S6PinnedReadFailure("W24FS103", "The fixed DOS drive root could not be pinned for read.");
                }
                var current = PinDirectoryIdentity(driveHandle, driveRoot, null, "W24FS104", "W24FS105");
                chain.Add(current);
                var driveVolume = current.VolumeSerialNumber;
                var currentPath = driveRoot;
                if (normalized.Length > 3)
                {
                    foreach (var segment in normalized.Substring(3).Split('\\'))
                    {
                        var nextHandle = OpenRelative(current.Handle, segment, FileTraverse | FileReadAttributes | Synchronize,
                            FileDirectoryFile | FileSynchronousIoNonAlert | FileOpenReparsePoint,
                            "W24FS103", "A registered root segment could not be pinned without following a reparse point.");
                        currentPath = CombineDosPath(currentPath, segment);
                        current = PinDirectoryIdentity(nextHandle, currentPath, driveVolume, "W24FS104", "W24FS105");
                        chain.Add(current);
                    }
                }
                return new W24S6PinnedReadRoot(chain, normalized, currentPath, current.VolumeSerialNumber, current.FileIndex);
            }
            catch
            {
                DisposeDirectoryChain(chain);
                throw;
            }
        }

        internal static W24S6PinnedReadBytes ReadExact(W24S6PinnedReadRoot root, string relativePath)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            return ReadExactCore(
                root.DirectoryHandle,
                root.DirectoryChain,
                root.DeclaredPath,
                root.FinalPath,
                root.VolumeSerialNumber,
                relativePath,
                W24S6LocalDocumentInspector.MaximumDocumentBytes,
                true);
        }

        internal static W24S6PinnedReadBytes ReadExactFromPinnedDirectoryHandle(
            SafeFileHandle directoryHandle,
            string relativePath,
            int maximumBytes)
        {
            RequireWindows();
            if (directoryHandle == null || directoryHandle.IsInvalid || directoryHandle.IsClosed ||
                maximumBytes < 1 || maximumBytes > W24S6LocalDocumentInspector.MaximumDocumentBytes)
                throw new W24S6PinnedReadFailure("W24FS108", "The pinned directory handle is unavailable.");
            var borrowedRoot = ObserveBorrowedDirectoryIdentity(directoryHandle);
            return ReadExactCore(
                directoryHandle,
                new[] { borrowedRoot },
                null,
                null,
                borrowedRoot.VolumeSerialNumber,
                relativePath,
                maximumBytes,
                false);
        }

        private static W24S6PinnedReadBytes ReadExactCore(
            SafeFileHandle rootHandle,
            IEnumerable<W24S6PinnedDirectorySegment> rootChain,
            string declaredRootPath,
            string pinnedRootFinalPath,
            uint rootVolumeSerialNumber,
            string relativePath,
            int maximumBytes,
            bool requireDosFinalPath)
        {
            if (!IsSafeRelativePath(relativePath))
                throw new W24S6PinnedReadFailure("W24FS106", "The declared target is not a canonical relative file path.");
            if (declaredRootPath != null)
            {
                var separatorCharacters = declaredRootPath.EndsWith("\\", StringComparison.Ordinal) ? 0 : 1;
                if (declaredRootPath.Length + separatorCharacters + relativePath.Length > MaximumDosPathCharacters)
                    throw new W24S6PinnedReadFailure("W24FS106", "The declared target path exceeds the bounded local DOS path limit.");
            }
            else if (relativePath.Length > MaximumDosPathCharacters)
                throw new W24S6PinnedReadFailure("W24FS106", "The declared target path exceeds the bounded local DOS path limit.");

            var segments = relativePath.Split('/');
            var openedParents = new List<W24S6PinnedDirectorySegment>();
            try
            {
                var parent = rootHandle;
                var expectedPath = pinnedRootFinalPath;
                for (var index = 0; index < segments.Length - 1; index++)
                {
                    var nextHandle = OpenRelative(parent, segments[index], FileTraverse | FileReadAttributes | Synchronize,
                        FileDirectoryFile | FileSynchronousIoNonAlert | FileOpenReparsePoint,
                        "W24FS110", "A target parent segment was rejected before its child could be opened.");
                    if (expectedPath != null) expectedPath = CombineDosPath(expectedPath, segments[index]);
                    var next = requireDosFinalPath
                        ? PinDirectoryIdentity(nextHandle, expectedPath, rootVolumeSerialNumber, "W24FS110", "W24FS110")
                        : PinDirectoryIdentityWithoutPath(nextHandle, rootVolumeSerialNumber, "W24FS110");
                    openedParents.Add(next);
                    parent = next.Handle;
                }

                if (expectedPath != null) expectedPath = CombineDosPath(expectedPath, segments[segments.Length - 1]);
#if UNITY_INCLUDE_TESTS
                targetOpenAttemptCount++;
#endif
                var handle = OpenRelative(parent, segments[segments.Length - 1], FileReadData | FileReadAttributes | Synchronize,
                    FileNonDirectoryFile | FileSynchronousIoNonAlert | FileOpenReparsePoint,
                    "W24FS107", "The declared target is missing or could not be pinned for read.");
                using (handle)
                {
                    ByHandleFileInformation before;
                    if (!GetFileInformationByHandle(handle, out before))
                        throw new W24S6PinnedReadFailure("W24FS108", "The pinned file identity could not be read.");
                    var size = FileSize(before);
                    if (!FileMetadataAccepted(before.FileAttributes, before.NumberOfLinks, size, maximumBytes))
                    {
                        if ((before.FileAttributes & (FileAttributeDirectory | FileAttributeReparsePoint)) != 0 || before.NumberOfLinks != 1)
                            throw new W24S6PinnedReadFailure("W24FS109", "The target must be a single-link non-reparse regular file.");
                        throw new W24S6PinnedReadFailure("W24FS111", "The target exceeds the bounded read limit.");
                    }

                    var actualFinalPath = requireDosFinalPath
                        ? GetCanonicalFinalPath(handle, "W24FS110")
                        : null;
                    if (before.VolumeSerialNumber != rootVolumeSerialNumber ||
                        requireDosFinalPath && !string.Equals(actualFinalPath,
                            CanonicalDeclaredDosPath(expectedPath, "W24FS110"), StringComparison.OrdinalIgnoreCase))
                        throw new W24S6PinnedReadFailure("W24FS110", "The pinned target final path or volume differs from its registered location.");

                    var bytes = new byte[(int)size];
                    try
                    {
                        using (var stream = new FileStream(handle, FileAccess.Read, 4096, false))
                        {
                            var offset = 0;
                            while (offset < bytes.Length)
                            {
                                var count = stream.Read(bytes, offset, bytes.Length - offset);
                                if (count <= 0)
                                    throw new W24S6PinnedReadFailure("W24FS112", "The pinned target ended before its declared handle size.");
                                offset += count;
                            }
                            if (stream.ReadByte() != -1)
                                throw new W24S6PinnedReadFailure("W24FS112", "The pinned target grew during the bounded read.");

#if UNITY_INCLUDE_TESTS
                            var hook = BeforePostReadIdentityReplayForTests;
                            if (hook != null) hook();
#endif
                            ByHandleFileInformation after;
                            if (!GetFileInformationByHandle(handle, out after) || !SameIdentity(before, after)
                                || requireDosFinalPath && !string.Equals(
                                    GetCanonicalFinalPath(handle, "W24FS113"),
                                    actualFinalPath,
                                    StringComparison.OrdinalIgnoreCase))
                                throw new W24S6PinnedReadFailure("W24FS113", "The pinned target identity changed during the read.");
                            ReplayDirectoryChain(rootChain, "W24FS113");
                            ReplayDirectoryChain(openedParents, "W24FS113");
                            return new W24S6PinnedReadBytes(bytes, before.VolumeSerialNumber, FileIndex(before));
                        }
                    }
                    catch (IOException) { throw new W24S6PinnedReadFailure("W24FS112", "The pinned target could not be read completely."); }
                    catch (UnauthorizedAccessException) { throw new W24S6PinnedReadFailure("W24FS112", "The pinned target could not be read completely."); }
                    catch (ArgumentException) { throw new W24S6PinnedReadFailure("W24FS112", "The pinned target could not be read completely."); }
                }
            }
            finally
            {
                DisposeDirectoryChain(openedParents);
            }
        }

        private static W24S6PinnedDirectorySegment ObserveBorrowedDirectoryIdentity(
            SafeFileHandle handle)
        {
            ByHandleFileInformation information;
            if (!GetFileInformationByHandle(handle, out information) ||
                (information.FileAttributes & FileAttributeDirectory) == 0 ||
                (information.FileAttributes & FileAttributeReparsePoint) != 0)
                throw new W24S6PinnedReadFailure("W24FS108", "The pinned directory handle failed its non-reparse identity check.");
            return new W24S6PinnedDirectorySegment(
                handle,
                null,
                information.VolumeSerialNumber,
                FileIndex(information),
                information.FileAttributes);
        }

        private static SafeFileHandle OpenRelative(SafeFileHandle parent, string segment, uint desiredAccess,
            uint openOptions, string code, string message)
        {
            if (parent == null || parent.IsInvalid || !IsSafeDosSegment(segment)) throw new W24S6PinnedReadFailure(code, message);
#if UNITY_INCLUDE_TESTS
            openAttemptCount++;
#endif
            GCHandle pinnedName = default(GCHandle);
            IntPtr unicodePointer = IntPtr.Zero;
            IntPtr rawHandle = IntPtr.Zero;
            var parentAddedRef = false;
            try
            {
                pinnedName = GCHandle.Alloc(segment, GCHandleType.Pinned);
                var unicodeName = new UnicodeString
                {
                    Length = checked((ushort)(segment.Length * 2)),
                    MaximumLength = checked((ushort)(segment.Length * 2)),
                    Buffer = pinnedName.AddrOfPinnedObject()
                };
                unicodePointer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(UnicodeString)));
                Marshal.StructureToPtr(unicodeName, unicodePointer, false);
                parent.DangerousAddRef(ref parentAddedRef);
                var attributes = new ObjectAttributes
                {
                    Length = (uint)Marshal.SizeOf(typeof(ObjectAttributes)),
                    RootDirectory = parent.DangerousGetHandle(),
                    ObjectName = unicodePointer,
                    Attributes = ObjectAttributesCaseInsensitive | ObjectAttributesDontReparse,
                    SecurityDescriptor = IntPtr.Zero,
                    SecurityQualityOfService = IntPtr.Zero
                };
                IoStatusBlock ioStatus;
                var status = NtOpenFile(out rawHandle, desiredAccess, ref attributes, out ioStatus, FileShareRead, openOptions);
                if (status != 0 || rawHandle == IntPtr.Zero || rawHandle == new IntPtr(-1))
                {
                    CloseRawHandle(rawHandle);
                    rawHandle = IntPtr.Zero;
                    throw new W24S6PinnedReadFailure(code, message);
                }
                return new SafeFileHandle(rawHandle, true);
            }
            catch (W24S6PinnedReadFailure) { throw; }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is MarshalDirectiveException
                || e is DllNotFoundException || e is EntryPointNotFoundException)
            {
                CloseRawHandle(rawHandle);
                throw new W24S6PinnedReadFailure(code, message);
            }
            finally
            {
                if (parentAddedRef) parent.DangerousRelease();
                if (unicodePointer != IntPtr.Zero) Marshal.FreeHGlobal(unicodePointer);
                if (pinnedName.IsAllocated) pinnedName.Free();
            }
        }

        private static W24S6PinnedDirectorySegment PinDirectoryIdentity(SafeFileHandle handle, string expectedDosPath,
            uint? expectedVolume, string metadataCode, string finalPathCode)
        {
            try
            {
                ByHandleFileInformation information;
                if (!GetFileInformationByHandle(handle, out information)
                    || (information.FileAttributes & FileAttributeDirectory) == 0
                    || (information.FileAttributes & FileAttributeReparsePoint) != 0
                    || expectedVolume.HasValue && information.VolumeSerialNumber != expectedVolume.Value)
                    throw new W24S6PinnedReadFailure(metadataCode, "A pinned directory segment failed its non-reparse local identity check.");
                var actualFinal = GetCanonicalFinalPath(handle, finalPathCode);
                if (!string.Equals(actualFinal, CanonicalDeclaredDosPath(expectedDosPath, finalPathCode), StringComparison.OrdinalIgnoreCase))
                    throw new W24S6PinnedReadFailure(finalPathCode, "A pinned directory segment differs from its frozen DOS path.");
                return new W24S6PinnedDirectorySegment(handle, expectedDosPath, information.VolumeSerialNumber,
                    FileIndex(information), information.FileAttributes);
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }

        private static W24S6PinnedDirectorySegment PinDirectoryIdentityWithoutPath(
            SafeFileHandle handle,
            uint expectedVolume,
            string code)
        {
            try
            {
                ByHandleFileInformation information;
                if (!GetFileInformationByHandle(handle, out information) ||
                    (information.FileAttributes & FileAttributeDirectory) == 0 ||
                    (information.FileAttributes & FileAttributeReparsePoint) != 0 ||
                    information.VolumeSerialNumber != expectedVolume)
                    throw new W24S6PinnedReadFailure(code, "A pinned directory segment failed its non-reparse identity check.");
                return new W24S6PinnedDirectorySegment(
                    handle,
                    null,
                    information.VolumeSerialNumber,
                    FileIndex(information),
                    information.FileAttributes);
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }

        private static void ReplayDirectoryChain(IEnumerable<W24S6PinnedDirectorySegment> chain, string code)
        {
            foreach (var segment in chain)
            {
                ByHandleFileInformation information;
                if (!GetFileInformationByHandle(segment.Handle, out information)
                    || information.VolumeSerialNumber != segment.VolumeSerialNumber
                    || FileIndex(information) != segment.FileIndex
                    || information.FileAttributes != segment.FileAttributes
                    || (information.FileAttributes & FileAttributeDirectory) == 0
                    || (information.FileAttributes & FileAttributeReparsePoint) != 0
                    || segment.ExpectedDosPath != null && !string.Equals(
                        GetCanonicalFinalPath(segment.Handle, code),
                        CanonicalDeclaredDosPath(segment.ExpectedDosPath, code),
                        StringComparison.OrdinalIgnoreCase))
                    throw new W24S6PinnedReadFailure(code, "A pinned parent directory identity changed during the read.");
            }
        }

        private static void RequireWindows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new W24S6PinnedReadFailure("W24FS101", "The local read scaffold is Windows-only and fails closed on this platform.");
        }

        private static uint QueryDriveType(string driveRoot)
        {
#if UNITY_INCLUDE_TESTS
            driveTypeQueryCount++;
#endif
            return GetDriveType(driveRoot);
        }

        private static bool IsSafeRelativePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 260 || value.IndexOf('\\') >= 0 || value.IndexOf(':') >= 0
                || value.StartsWith("/", StringComparison.Ordinal) || value.Contains("//") || value.Any(char.IsControl)) return false;
            return value.Split('/').All(IsSafeDosSegment);
        }

        private static bool TryNormalizeLocalDosPath(string value, int maximumCharacters, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(value) || value.Length < 3 || value.Length > maximumCharacters
                || !IsAsciiLetter(value[0]) || value[1] != ':' || value[2] != '\\' || value.IndexOf('/') >= 0
                || value.IndexOf(':', 2) >= 0 || value.Any(character => character <= '\u001f' || character == '\u007f')) return false;
            if (value.Length > 3)
            {
                if (value.EndsWith("\\", StringComparison.Ordinal)) return false;
                if (value.Substring(3).Split('\\').Any(segment => !IsSafeDosSegment(segment))) return false;
            }
            normalized = char.ToUpperInvariant(value[0]) + value.Substring(1);
            return true;
        }

        private static bool IsSafeDosSegment(string segment)
        {
            if (string.IsNullOrEmpty(segment) || segment.Length > 255 || segment == "." || segment == ".."
                || segment.EndsWith(".", StringComparison.Ordinal) || segment.EndsWith(" ", StringComparison.Ordinal)
                || segment.IndexOfAny(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' }) >= 0
                || segment.Any(character => character <= '\u001f' || character == '\u007f')) return false;
            var stem = segment.Split('.')[0].ToUpperInvariant();
            if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" || stem == "CLOCK$") return false;
            return !(stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal))
                && stem[3] >= '1' && stem[3] <= '9');
        }

        private static bool IsAsciiLetter(char value)
        {
            return value >= 'A' && value <= 'Z' || value >= 'a' && value <= 'z';
        }

        private static string CombineDosPath(string parent, string segment)
        {
            return parent.EndsWith("\\", StringComparison.Ordinal) ? parent + segment : parent + "\\" + segment;
        }

        private static bool DirectoryMetadataAccepted(uint attributes, uint volume, uint expectedVolume)
        {
            return (attributes & FileAttributeDirectory) != 0 && (attributes & FileAttributeReparsePoint) == 0 && volume == expectedVolume;
        }

        private static bool FileMetadataAccepted(uint attributes, uint links, ulong bytes)
        {
            return FileMetadataAccepted(
                attributes,
                links,
                bytes,
                W24S6LocalDocumentInspector.MaximumDocumentBytes);
        }

        private static bool FileMetadataAccepted(
            uint attributes,
            uint links,
            ulong bytes,
            int maximumBytes)
        {
            return maximumBytes > 0 &&
                   (attributes & (FileAttributeDirectory | FileAttributeReparsePoint)) == 0 &&
                   links == 1 &&
                   bytes <= (ulong)maximumBytes;
        }

        private static bool SameIdentity(ByHandleFileInformation left, ByHandleFileInformation right)
        {
            return left.VolumeSerialNumber == right.VolumeSerialNumber && FileIndex(left) == FileIndex(right)
                && left.NumberOfLinks == right.NumberOfLinks && left.FileAttributes == right.FileAttributes
                && FileSize(left) == FileSize(right) && left.LastWriteTimeHigh == right.LastWriteTimeHigh
                && left.LastWriteTimeLow == right.LastWriteTimeLow;
        }

        private static ulong FileSize(ByHandleFileInformation value) { return ((ulong)value.FileSizeHigh << 32) | value.FileSizeLow; }
        private static ulong FileIndex(ByHandleFileInformation value) { return ((ulong)value.FileIndexHigh << 32) | value.FileIndexLow; }

        private static string GetCanonicalFinalPath(SafeFileHandle handle, string code)
        {
            var required = GetFinalPathNameByHandle(handle, null, 0, 0);
            if (required == 0 || required > MaximumFinalPathCharacters)
                throw new W24S6PinnedReadFailure(code, "The pinned final path could not be resolved.");
            var builder = new StringBuilder((int)required + 1);
            var written = GetFinalPathNameByHandle(handle, builder, (uint)builder.Capacity, 0);
            if (written == 0 || written >= builder.Capacity)
                throw new W24S6PinnedReadFailure(code, "The pinned final path could not be resolved.");
            return CanonicalFinalPath(builder.ToString(), code);
        }

        private static string CanonicalFinalPath(string value, string code)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 7 || !value.StartsWith("\\\\?\\", StringComparison.Ordinal)
                || !IsAsciiLetter(value[4]) || value[5] != ':' || value[6] != '\\')
                throw new W24S6PinnedReadFailure(code, "The pinned final path is not a strict extended DOS drive path.");
            string normalized;
            if (!TryNormalizeLocalDosPath(value.Substring(4), MaximumFinalPathCharacters - 4, out normalized))
                throw new W24S6PinnedReadFailure(code, "The pinned final path is not a strict extended DOS drive path.");
            return normalized;
        }

        private static string CanonicalDeclaredDosPath(string value, string code)
        {
            string normalized;
            if (!TryNormalizeLocalDosPath(value, MaximumDosPathCharacters, out normalized))
                throw new W24S6PinnedReadFailure(code, "The declared DOS path is not canonical.");
            return normalized;
        }

        private static void DisposeDirectoryChain(IList<W24S6PinnedDirectorySegment> chain)
        {
            for (var index = chain.Count - 1; index >= 0; index--) chain[index].Handle.Dispose();
        }

        private static void CloseRawHandle(IntPtr handle)
        {
            if (handle != IntPtr.Zero && handle != new IntPtr(-1)) new SafeFileHandle(handle, true).Dispose();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct UnicodeString
        {
            internal ushort Length;
            internal ushort MaximumLength;
            internal IntPtr Buffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ObjectAttributes
        {
            internal uint Length;
            internal IntPtr RootDirectory;
            internal IntPtr ObjectName;
            internal uint Attributes;
            internal IntPtr SecurityDescriptor;
            internal IntPtr SecurityQualityOfService;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoStatusBlock
        {
            internal IntPtr Status;
            internal UIntPtr Information;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            internal uint FileAttributes;
            internal System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            internal System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            internal System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            internal uint VolumeSerialNumber;
            internal uint FileSizeHigh;
            internal uint FileSizeLow;
            internal uint NumberOfLinks;
            internal uint FileIndexHigh;
            internal uint FileIndexLow;

            internal int LastWriteTimeHigh { get { return LastWriteTime.dwHighDateTime; } }
            internal int LastWriteTimeLow { get { return LastWriteTime.dwLowDateTime; } }
        }

        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
            uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("ntdll.dll", ExactSpelling = true)]
        private static extern int NtOpenFile(out IntPtr fileHandle, uint desiredAccess, ref ObjectAttributes objectAttributes,
            out IoStatusBlock ioStatusBlock, uint shareAccess, uint openOptions);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle file, out ByHandleFileInformation information);

        [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandle(SafeFileHandle file, StringBuilder path, uint pathCharacters, uint flags);

        [DllImport("kernel32.dll", EntryPoint = "GetDriveTypeW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint GetDriveType(string rootPathName);
    }
}
