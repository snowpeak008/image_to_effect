using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using VFXComposer.Broker.Native;

namespace VFXComposer.Broker.Tests;

[TestClass]
public sealed class JunctionAndReparseTests
{
    [TestMethod]
    public void GlobalVolumeTraversalRejectsLocalJunctionBeforeOpeningItsChild()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows reparse gate is Windows-only.");
        }

        var scratch = Path.Combine(Path.GetTempPath(), "vfxcomposer-junction-" + Guid.NewGuid().ToString("N"));
        if (Directory.Exists(scratch))
        {
            Assert.Fail("Unique scratch root already exists.");
        }

        var repositoryParent = Path.Combine(scratch, "repository-parent");
        var outside = Path.Combine(scratch, "outside");
        var outsideProject = Path.Combine(outside, "project");
        var junction = Path.Combine(repositoryParent, "junction");
        Directory.CreateDirectory(outsideProject);
        Directory.CreateDirectory(junction);
        try
        {
            Assert.IsTrue(CreateDirectoryJunction(junction, outside, out var error), $"FSCTL junction failed: {error}");
            var driveRoot = Path.GetPathRoot(scratch)
                ?? throw new InvalidOperationException("Scratch drive root is missing.");
            var volumeGuid = new StringBuilder(64);
            Assert.IsTrue(GetVolumeNameForVolumeMountPoint(driveRoot, volumeGuid, volumeGuid.Capacity));
            var definition = new BrokerRegistrationDefinition(
                "project-junction-01",
                volumeGuid.ToString(),
                junction[driveRoot.Length..].Split(
                    Path.DirectorySeparatorChar,
                    StringSplitOptions.RemoveEmptyEntries),
                ["project"]);

            Assert.ThrowsExactly<InvalidDataException>(() => WindowsPinnedProjectRoots.Open(definition));
        }
        finally
        {
            if (Directory.Exists(junction) &&
                (File.GetAttributes(junction) & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(junction, recursive: false);
            }

            Directory.Delete(scratch, recursive: true);
        }
    }

    private static bool CreateDirectoryJunction(string junctionPath, string targetPath, out int error)
    {
        const uint genericWrite = 0x40000000;
        const uint shareReadWriteDelete = 0x00000007;
        const uint openExisting = 3;
        const uint backupSemantics = 0x02000000;
        const uint openReparsePoint = 0x00200000;
        const uint fsctlSetReparsePoint = 0x000900A4;
        const uint mountPointTag = 0xA0000003;

        var substituteName = "\\??\\" + targetPath;
        var printName = targetPath;
        var substituteBytes = Encoding.Unicode.GetBytes(substituteName);
        var printBytes = Encoding.Unicode.GetBytes(printName);
        var pathBytes = Encoding.Unicode.GetBytes(substituteName + "\0" + printName + "\0");
        var buffer = new byte[16 + pathBytes.Length];
        WriteUInt32(buffer, 0, mountPointTag);
        WriteUInt16(buffer, 4, checked((ushort)(8 + pathBytes.Length)));
        WriteUInt16(buffer, 8, 0);
        WriteUInt16(buffer, 10, checked((ushort)substituteBytes.Length));
        WriteUInt16(buffer, 12, checked((ushort)(substituteBytes.Length + 2)));
        WriteUInt16(buffer, 14, checked((ushort)printBytes.Length));
        Buffer.BlockCopy(pathBytes, 0, buffer, 16, pathBytes.Length);

        using var handle = CreateFileForJunction(
            junctionPath,
            genericWrite,
            shareReadWriteDelete,
            IntPtr.Zero,
            openExisting,
            backupSemantics | openReparsePoint,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            error = Marshal.GetLastWin32Error();
            return false;
        }

        var created = DeviceIoControl(
            handle,
            fsctlSetReparsePoint,
            buffer,
            buffer.Length,
            IntPtr.Zero,
            0,
            out _,
            IntPtr.Zero);
        error = created ? 0 : Marshal.GetLastWin32Error();
        return created;
    }

    private static void WriteUInt16(byte[] buffer, int offset, ushort value) =>
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buffer, offset, 2);

    private static void WriteUInt32(byte[] buffer, int offset, uint value) =>
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buffer, offset, 4);

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileForJunction(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint controlCode,
        byte[] inputBuffer,
        int inputBufferBytes,
        IntPtr outputBuffer,
        int outputBufferBytes,
        out uint bytesReturned,
        IntPtr overlapped);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeNameForVolumeMountPoint(
        string volumeMountPoint,
        StringBuilder volumeName,
        int bufferLength);
}
