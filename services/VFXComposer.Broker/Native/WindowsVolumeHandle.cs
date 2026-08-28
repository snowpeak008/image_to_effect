using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace VFXComposer.Broker.Native;

internal sealed class WindowsPinnedProjectRoots : IDisposable
{
    private const uint Synchronize = 0x00100000;
    private const uint FileTraverse = 0x00000020;
    private const uint FileReadAttributes = 0x00000080;
    private const uint FileShareRead = 0x00000001;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private readonly WindowsDirectoryHandle[] _chain;

    private WindowsPinnedProjectRoots(
        WindowsDirectoryHandle[] chain,
        WindowsDirectoryHandle volume,
        WindowsDirectoryHandle repository,
        WindowsDirectoryHandle project)
    {
        _chain = chain;
        Volume = volume;
        Repository = repository;
        Project = project;
    }

    public WindowsDirectoryHandle Volume { get; }
    public WindowsDirectoryHandle Repository { get; }
    public WindowsDirectoryHandle Project { get; }

    public static WindowsPinnedProjectRoots Open(BrokerRegistrationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(BrokerDiagnosticCodes.ProjectUnavailable);
        }

        var chain = new List<WindowsDirectoryHandle>();
        try
        {
            var rawVolume = CreateFile(
                definition.VolumeGuidPath,
                FileTraverse | FileReadAttributes | Synchronize,
                FileShareRead,
                IntPtr.Zero,
                OpenExisting,
                FileFlagBackupSemantics | FileFlagOpenReparsePoint,
                IntPtr.Zero);
            if (rawVolume.IsInvalid)
            {
                rawVolume.Dispose();
                throw new InvalidDataException(BrokerDiagnosticCodes.ProjectUnavailable);
            }

            var volume = WindowsDirectoryHandle.AdoptAndVerify(rawVolume);
            chain.Add(volume);
            RequireNtfsVolume(volume.Handle);
            var current = volume;
            foreach (var segment in definition.RepositorySegments)
            {
                current = current.OpenChild(segment);
                chain.Add(current);
            }

            var repository = current;
            foreach (var segment in definition.ProjectSegments)
            {
                current = current.OpenChild(segment);
                chain.Add(current);
            }

            var project = current;
            return new WindowsPinnedProjectRoots(chain.ToArray(), volume, repository, project);
        }
        catch
        {
            for (var index = chain.Count - 1; index >= 0; index--)
            {
                chain[index].Dispose();
            }

            throw;
        }
    }

    public bool ReplayIdentities() =>
        _chain.All(value => value.ReplayIdentity()) &&
        _chain.All(value => value.Identity.VolumeSerialNumber == Volume.Identity.VolumeSerialNumber);

    public void Dispose()
    {
        for (var index = _chain.Length - 1; index >= 0; index--)
        {
            _chain[index].Dispose();
        }
    }

    private static void RequireNtfsVolume(SafeFileHandle handle)
    {
        var fileSystemName = new StringBuilder(32);
        if (!GetVolumeInformationByHandleW(
                handle,
                null,
                0,
                out _,
                out _,
                out _,
                fileSystemName,
                fileSystemName.Capacity) ||
            !string.Equals(fileSystemName.ToString(), "NTFS", StringComparison.Ordinal))
        {
            throw new InvalidDataException(BrokerDiagnosticCodes.ProjectUnavailable);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeInformationByHandleW(
        SafeFileHandle fileHandle,
        StringBuilder? volumeNameBuffer,
        int volumeNameSize,
        out uint volumeSerialNumber,
        out uint maximumComponentLength,
        out uint fileSystemFlags,
        StringBuilder fileSystemNameBuffer,
        int fileSystemNameSize);
}
