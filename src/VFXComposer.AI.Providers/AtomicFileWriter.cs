using System.Security.Cryptography;

namespace VFXComposer.AI.Providers;

internal static class AtomicFileWriter
{
    public static void WriteReplace(string destinationPath, string backupPath, ReadOnlySpan<byte> bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        var parent = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrEmpty(parent))
        {
            throw new InvalidOperationException("Provider storage path is invalid.");
        }

        Directory.CreateDirectory(parent);
        var temporaryPath = destinationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var completed = false;
        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(destinationPath))
            {
                File.Replace(temporaryPath, destinationPath, backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, destinationPath);
            }

            completed = true;
        }
        finally
        {
            if (!completed && File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                    // The original remains authoritative; a later cleanup may remove the temp file.
                }
            }
        }
    }

    public static byte[] ReadBounded(string path, int maximumBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (maximumBytes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length < 1 || stream.Length > maximumBytes)
        {
            throw new InvalidDataException("Provider storage size is invalid.");
        }

        var bytes = new byte[checked((int)stream.Length)];
        var read = 0;
        while (read < bytes.Length)
        {
            var count = stream.Read(bytes, read, bytes.Length - read);
            if (count == 0)
            {
                CryptographicOperations.ZeroMemory(bytes);
                throw new EndOfStreamException("Provider storage ended unexpectedly.");
            }

            read += count;
        }

        if (stream.ReadByte() != -1)
        {
            CryptographicOperations.ZeroMemory(bytes);
            throw new InvalidDataException("Provider storage size changed while reading.");
        }

        return bytes;
    }
}
