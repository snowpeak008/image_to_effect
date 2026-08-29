namespace VFXComposer.Jobs;

/// <summary>
/// Atomic replace-with-backup file discipline for the job store, following the same pattern as
/// the provider configuration store: write-through temp file, then <c>File.Replace</c> so the
/// previous primary becomes the <c>.bak</c> recovery copy.
/// </summary>
internal static class JobStoreFileWriter
{
    public static void WriteReplace(string destinationPath, string backupPath, ReadOnlySpan<byte> bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        WriteAtomically(destinationPath, bytes, temporaryPath =>
        {
            if (File.Exists(destinationPath))
            {
                File.Replace(temporaryPath, destinationPath, backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, destinationPath);
            }
        });
    }

    /// <summary>
    /// Repairs a known-good primary without touching the existing backup; used after recovery
    /// read the backup, because a normal replace would overwrite the only valid copy.
    /// </summary>
    public static void RestorePrimaryPreservingBackup(string destinationPath, ReadOnlySpan<byte> bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        WriteAtomically(destinationPath, bytes, temporaryPath =>
        {
            if (File.Exists(destinationPath))
            {
                File.Replace(temporaryPath, destinationPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, destinationPath);
            }
        });
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
            throw new InvalidDataException("Job store size is invalid.");
        }

        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1)
        {
            throw new InvalidDataException("Job store size changed while reading.");
        }

        return bytes;
    }

    private static void WriteAtomically(string destinationPath, ReadOnlySpan<byte> bytes, Action<string> commit)
    {
        var parent = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrEmpty(parent))
        {
            throw new InvalidOperationException("Job store path is invalid.");
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

            commit(temporaryPath);
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
}
