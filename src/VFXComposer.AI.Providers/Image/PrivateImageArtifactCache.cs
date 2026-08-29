using System.Collections.Concurrent;
using System.Security.Cryptography;
using VFXComposer.AI.Contracts;

namespace VFXComposer.AI.Providers.Image;

/// <summary>Private artifact lookup surface. It exposes neither a disk path nor the provider's source URL.</summary>
public interface IPrivateImageArtifactStore
{
    PrivateImageArtifact GetArtifact(string artifactId);

    ValueTask<Stream> OpenReadAsync(string artifactId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Per-instance, user-temp artifact cache for untrusted image bytes. Each instance owns one random session directory,
/// uses atomic file replacement, and deletes that directory when disposed.
/// </summary>
public sealed class PrivateImageArtifactCache : IPrivateImageArtifactStore, IDisposable
{
    private readonly ConcurrentDictionary<string, StoredArtifact> _artifacts = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _sessionDirectory;
    private int _disposed;

    public PrivateImageArtifactCache(string? privateTempRoot = null)
    {
        _sessionDirectory = CreateSessionDirectory(privateTempRoot);
    }

    public async ValueTask<PrivateImageArtifact> StoreAsync(
        ReadOnlyMemory<byte> content,
        PrivateImageFormat format,
        int width,
        int height,
        CancellationToken cancellationToken = default)
    {
        if (content.Length is < 1 or > ImageArtifactLimits.MaximumImageBytes)
        {
            throw new ImageGatewayException(ImageErrorCode.ArtifactTooLarge);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var artifactId = "img-" + Guid.NewGuid().ToString("N");
            var extension = ExtensionFor(format);
            var finalPath = Path.Combine(_sessionDirectory, artifactId + extension);
            var stagingPath = Path.Combine(_sessionDirectory, "." + Guid.NewGuid().ToString("N") + ".tmp");
            var sha256Bytes = ComputeSha256(content.Span);
            try
            {
                var sha256 = Convert.ToHexString(sha256Bytes).ToLowerInvariant();
                var artifact = new PrivateImageArtifact(artifactId, format, content.Length, width, height, sha256);
                try
                {
                    await WriteAtomicallyAsync(stagingPath, finalPath, content, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (IOException)
                {
                    throw new ImageGatewayException(ImageErrorCode.ArtifactCacheUnavailable);
                }
                catch (UnauthorizedAccessException)
                {
                    throw new ImageGatewayException(ImageErrorCode.ArtifactCacheUnavailable);
                }

                if (!_artifacts.TryAdd(artifact.Id, new StoredArtifact(artifact, finalPath)))
                {
                    throw new ImageGatewayException(ImageErrorCode.ArtifactCacheUnavailable);
                }

                return artifact;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(sha256Bytes);
                TryDelete(stagingPath);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public PrivateImageArtifact GetArtifact(string artifactId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        ThrowIfDisposed();
        if (!_artifacts.TryGetValue(artifactId, out var stored))
        {
            throw new ImageGatewayException(ImageErrorCode.ArtifactCacheUnavailable);
        }

        return stored.Artifact;
    }

    public async ValueTask<Stream> OpenReadAsync(string artifactId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        cancellationToken.ThrowIfCancellationRequested();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (!_artifacts.TryGetValue(artifactId, out var stored))
            {
                throw new ImageGatewayException(ImageErrorCode.ArtifactCacheUnavailable);
            }

            var bytes = new byte[stored.Artifact.ByteLength];
            try
            {
                await using var input = new FileStream(
                    stored.Path,
                    new FileStreamOptions
                    {
                        Access = FileAccess.Read,
                        Mode = FileMode.Open,
                        Share = FileShare.Read,
                        Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                    });
                await input.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
                if (input.ReadByte() != -1)
                {
                    CryptographicOperations.ZeroMemory(bytes);
                    throw new ImageGatewayException(ImageErrorCode.ArtifactCacheUnavailable);
                }

                return new MemoryStream(bytes, writable: false);
            }
            catch (OperationCanceledException)
            {
                CryptographicOperations.ZeroMemory(bytes);
                throw;
            }
            catch (ImageGatewayException)
            {
                throw;
            }
            catch (IOException)
            {
                CryptographicOperations.ZeroMemory(bytes);
                throw new ImageGatewayException(ImageErrorCode.ArtifactCacheUnavailable);
            }
            catch (UnauthorizedAccessException)
            {
                CryptographicOperations.ZeroMemory(bytes);
                throw new ImageGatewayException(ImageErrorCode.ArtifactCacheUnavailable);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _gate.Wait();
        try
        {
            _artifacts.Clear();
            try
            {
                if (Directory.Exists(_sessionDirectory))
                {
                    Directory.Delete(_sessionDirectory, recursive: true);
                }
            }
            catch (IOException)
            {
                // Disposal must never disclose a path or leave a cleanup exception on an ordinary feature path.
            }
            catch (UnauthorizedAccessException)
            {
                // Disposal must never disclose a path or leave a cleanup exception on an ordinary feature path.
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public override string ToString() => "PrivateImageArtifactCache(<private>)";

    private static string CreateSessionDirectory(string? privateTempRoot)
    {
        var userTempRoot = Path.GetFullPath(Path.GetTempPath());
        var cacheRoot = Path.GetFullPath(privateTempRoot ?? Path.Combine(userTempRoot, "VFXComposer.AI", "private-image-artifacts"));
        if (!IsWithin(userTempRoot, cacheRoot))
        {
            throw new ArgumentException("Private image artifact storage must be under the current user's temporary directory.", nameof(privateTempRoot));
        }

        var sessionDirectory = Path.Combine(cacheRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sessionDirectory);
        return sessionDirectory;
    }

    private static bool IsWithin(string parent, string child)
    {
        var relative = Path.GetRelativePath(parent, child);
        return !Path.IsPathRooted(relative) &&
            !relative.Equals("..", StringComparison.Ordinal) &&
            !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
            !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static string ExtensionFor(PrivateImageFormat format) => format switch
    {
        PrivateImageFormat.Png => ".png",
        PrivateImageFormat.Jpeg => ".jpg",
        PrivateImageFormat.Webp => ".webp",
        _ => throw new ImageGatewayException(ImageErrorCode.ArtifactMimeNotAllowed),
    };

    private static byte[] ComputeSha256(ReadOnlySpan<byte> content) => SHA256.HashData(content);

    private static async ValueTask WriteAtomicallyAsync(
        string stagingPath,
        string finalPath,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        await using (var output = new FileStream(
            stagingPath,
            new FileStreamOptions
            {
                Access = FileAccess.Write,
                Mode = FileMode.CreateNew,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
            }))
        {
            await output.WriteAsync(content, cancellationToken).ConfigureAwait(false);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            output.Flush(flushToDisk: true);
        }

        File.Move(stagingPath, finalPath, overwrite: false);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup of a failed atomic staging file.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup of a failed atomic staging file.
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ImageGatewayException(ImageErrorCode.ArtifactCacheUnavailable);
        }
    }

    private sealed record StoredArtifact(PrivateImageArtifact Artifact, string Path);
}
