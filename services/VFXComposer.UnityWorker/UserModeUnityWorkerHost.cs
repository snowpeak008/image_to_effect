using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VFXComposer.Protocol;
using VFXComposer.Protocol.Diagnostics;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Json;
using VFXComposer.Protocol.Projects;
using VFXComposer.Protocol.Queries;
using VFXComposer.Protocol.Registration;

namespace VFXComposer.UnityWorker;

/// <summary>
/// Canonical runtime C2 consumer. The only project path it observes is its
/// Broker-selected working directory; no path ever crosses the Worker pipe.
/// </summary>
internal static class UserModeUnityWorkerHost
{
    private const string Failure = "U5FS001";
    private const string PathIdentityVersion = "vfxcomposer.user-mode-project-path-correlation/1\0";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static async Task<int> RunChildModeAsync(
        Stream standardInput,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(standardInput);
        UserModeWorkerBootstrap? bootstrap = null;
        NamedPipeClientStream? pipe = null;
        try
        {
            bootstrap = await UserModeWorkerBootstrapPeerCodec.ReadBootstrapAsync(
                standardInput,
                cancellationToken).ConfigureAwait(false);
            pipe = new NamedPipeClientStream(
                ".",
                bootstrap.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(15_000, cancellationToken).ConfigureAwait(false);

            var processEpoch = UserModeWorkerBootstrapPeerCodec.ObserveCurrentProcessEpoch();
            await UserModeWorkerBootstrapPeerCodec.WriteHelloAsync(
                pipe,
                bootstrap,
                Environment.ProcessId,
                processEpoch,
                cancellationToken).ConfigureAwait(false);

            var locatorBytes = await UserModeWorkerBootstrapPeerCodec.ReadFrameAsync(
                pipe,
                cancellationToken).ConfigureAwait(false);
            WorkerProjectLocator locator;
            try
            {
                locator = StrictWireCodec.Decode<WorkerProjectLocator>(locatorBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(locatorBytes);
            }

            var boundProject = BindLocatorToWorkingDirectory(locator, bootstrap, processEpoch);
            var acknowledgementBytes = CreateLocatorAcknowledgement(locator);
            try
            {
                // No project content is read until this exact C2 acknowledgement is framed.
                await UserModeWorkerBootstrapPeerCodec.WriteFrameAsync(
                    pipe,
                    acknowledgementBytes,
                    cancellationToken).ConfigureAwait(false);
                await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(acknowledgementBytes);
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                var queryBytes = await UserModeWorkerBootstrapPeerCodec.ReadFrameAsync(
                    pipe,
                    cancellationToken).ConfigureAwait(false);
                try
                {
                    var query = StrictWireCodec.Decode<ReadDocumentQuery>(queryBytes);
                    var resultBytes = CreateReadResult(boundProject, query);
                    try
                    {
                        await UserModeWorkerBootstrapPeerCodec.WriteFrameAsync(
                            pipe,
                            resultBytes,
                            cancellationToken).ConfigureAwait(false);
                        await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(resultBytes);
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(queryBytes);
                }
            }

            return 0;
        }
        catch (Exception exception) when (exception is
            ArgumentException or InvalidDataException or EndOfStreamException or IOException or
            TimeoutException or InvalidOperationException or OperationCanceledException or
            ObjectDisposedException or UnauthorizedAccessException or NotSupportedException or
            JsonException or StrictJsonException or WireDecodeException)
        {
            return 31;
        }
        finally
        {
            bootstrap?.Dispose();
            if (pipe is not null)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static BoundProject BindLocatorToWorkingDirectory(
        WorkerProjectLocator locator,
        UserModeWorkerBootstrap bootstrap,
        string processEpoch)
    {
        if (locator.BrokerGeneration != bootstrap.Generation ||
            !string.Equals(locator.WorkerSessionId, bootstrap.SessionId, StringComparison.Ordinal) ||
            !string.Equals(locator.WorkerProcessEpoch, processEpoch, StringComparison.Ordinal))
        {
            throw new InvalidDataException(Failure);
        }

        var projectRoot = ValidateWorkingDirectory();
        var identities = ComputeProjectPathIdentities(projectRoot);
        if (!locator.ProjectIdentity.FixedTimeEquals(identities.ProjectIdentity) ||
            !locator.VolumeIdentity.FixedTimeEquals(identities.VolumeIdentity) ||
            !locator.RepositoryIdentity.FixedTimeEquals(identities.RepositoryIdentity) ||
            !locator.ProjectRootIdentity.FixedTimeEquals(identities.ProjectRootIdentity))
        {
            throw new InvalidDataException(Failure);
        }

        var leaseId = "um-lease-" + locator.SelfHash.Digest["sha256:".Length..];
        return new BoundProject(projectRoot, locator, leaseId);
    }

    private static byte[] CreateLocatorAcknowledgement(WorkerProjectLocator locator)
    {
        var placeholder = TypedHash.ComputeUtf8(
            WorkerProjectLocatorAcknowledgement.SelfHashType,
            "placeholder");
        var provisional = new WorkerProjectLocatorAcknowledgement(
            locator.ProtocolVersion,
            MessageKinds.WorkerProjectLocatorAcknowledgement,
            locator.RequestId,
            locator.RegisteredProjectId,
            locator.BrokerGeneration,
            locator.RegistrationGeneration,
            locator.EnrollmentGeneration,
            locator.WorkerSessionId,
            locator.WorkerProcessEpoch,
            locator.SelfHash,
            WorkerProjectLocatorAcknowledgement.AcceptedDisposition,
            placeholder);
        var selfHash = SelfHash.Compute(
            JsonSerializer.SerializeToUtf8Bytes(provisional),
            WorkerProjectLocatorAcknowledgement.SelfHashType);
        var acknowledgement = new WorkerProjectLocatorAcknowledgement(
            provisional.ProtocolVersion,
            provisional.MessageKind,
            provisional.RequestId,
            provisional.RegisteredProjectId,
            provisional.BrokerGeneration,
            provisional.RegistrationGeneration,
            provisional.EnrollmentGeneration,
            provisional.WorkerSessionId,
            provisional.WorkerProcessEpoch,
            provisional.LocatorSelfHash,
            provisional.Disposition,
            selfHash);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(acknowledgement);
        _ = StrictWireCodec.Decode<WorkerProjectLocatorAcknowledgement>(bytes);
        return bytes;
    }

    private static byte[] CreateReadResult(BoundProject boundProject, ReadDocumentQuery query)
    {
        ReadDocumentResult result;
        if (!MatchesBoundProject(boundProject, query))
        {
            result = CreateRejectedResult(query, StableDiagnosticCodes.ProjectLeaseRejected);
        }
        else if (!IsSupportedRead(query))
        {
            result = CreateRejectedResult(query, StableDiagnosticCodes.ProjectDocumentUnavailable);
        }
        else
        {
            byte[]? content = null;
            try
            {
                content = ReadStrictProjectJson(boundProject.ProjectRoot, query.DocumentKind, query.DocumentId);
                var contentHash = TypedHash.Compute(ReadDocumentResult.ContentHashType, content);
                result = query.ExpectedContentHash is not null &&
                    !query.ExpectedContentHash.FixedTimeEquals(contentHash)
                    ? CreateRejectedResult(query, StableDiagnosticCodes.ProjectDocumentContentMismatch)
                    : new ReadDocumentResult(
                        ProtocolVersions.Current,
                        MessageKinds.ReadDocumentResult,
                        query.RequestId,
                        accepted: true,
                        query.ProjectIdentity,
                        query.DocumentKind,
                        query.DocumentId,
                        contentHash,
                        content.Length,
                        Convert.ToBase64String(content),
                        diagnostic: null);
            }
            catch (Exception exception) when (IsExpectedReadFailure(exception))
            {
                result = CreateRejectedResult(query, StableDiagnosticCodes.ProjectDocumentUnavailable);
            }
            finally
            {
                if (content is not null)
                {
                    CryptographicOperations.ZeroMemory(content);
                }
            }
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(result);
        _ = StrictWireCodec.Decode<ReadDocumentResult>(bytes);
        return bytes;
    }

    private static bool MatchesBoundProject(BoundProject boundProject, ReadDocumentQuery query) =>
        string.Equals(query.LeaseId, boundProject.LeaseId, StringComparison.Ordinal) &&
        query.LeaseGeneration == boundProject.Locator.EnrollmentGeneration &&
        query.ProjectIdentity.FixedTimeEquals(boundProject.Locator.ProjectIdentity);

    private static bool IsSupportedRead(ReadDocumentQuery query) =>
        string.Equals(query.DocumentKind, DocumentKinds.LibraryIndex, StringComparison.Ordinal) &&
        string.Equals(query.DocumentId, "project", StringComparison.Ordinal) ||
        string.Equals(query.DocumentKind, DocumentKinds.Manifest, StringComparison.Ordinal);

    private static ReadDocumentResult CreateRejectedResult(
        ReadDocumentQuery query,
        string diagnosticCode) =>
        new(
            ProtocolVersions.Current,
            MessageKinds.ReadDocumentResult,
            query.RequestId,
            accepted: false,
            query.ProjectIdentity,
            query.DocumentKind,
            query.DocumentId,
            contentHash: null,
            byteLength: 0,
            contentBase64: null,
            StableDiagnosticCatalog.Create(diagnosticCode));

    private static string ValidateWorkingDirectory()
    {
        var candidate = Directory.GetCurrentDirectory();
        if (string.IsNullOrEmpty(candidate) || candidate.Length < 3 ||
            !char.IsAsciiLetter(candidate[0]) || candidate[1] != ':' || candidate[2] != '\\' ||
            candidate.StartsWith("\\\\", StringComparison.Ordinal) ||
            candidate.StartsWith("\\\\?\\", StringComparison.Ordinal) ||
            candidate.StartsWith("\\\\.\\", StringComparison.Ordinal) ||
            candidate.IndexOf(':', 2) >= 0)
        {
            throw new InvalidDataException(Failure);
        }

        var segments = candidate[3..].Split('\\');
        if (segments.Length == 0 || segments.Any(segment =>
                segment.Length == 0 || segment is "." or ".." ||
                segment.EndsWith(' ') || segment.EndsWith('.')))
        {
            throw new InvalidDataException(Failure);
        }

        var canonical = Path.GetFullPath(candidate);
        if (!string.Equals(candidate, canonical, StringComparison.OrdinalIgnoreCase) ||
            !Directory.Exists(canonical))
        {
            throw new InvalidDataException(Failure);
        }

        RequireDirectoryTreeNotReparse(canonical);
        RequireMarker(Path.Combine(canonical, "Assets"), directory: true);
        RequireMarker(Path.Combine(canonical, "Packages", "manifest.json"), directory: false);
        RequireMarker(Path.Combine(canonical, "ProjectSettings", "ProjectVersion.txt"), directory: false);
        return canonical;
    }

    private static ProjectPathIdentities ComputeProjectPathIdentities(string canonicalProjectRoot)
    {
        var normalized = canonicalProjectRoot.TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
        var volumeRoot = Path.GetPathRoot(canonicalProjectRoot)?.ToUpperInvariant();
        if (string.IsNullOrEmpty(volumeRoot) || volumeRoot.Length != 3)
        {
            throw new InvalidDataException(Failure);
        }

        return new ProjectPathIdentities(
            PathIdentity(ProjectRegistrationAttestation.ProjectIdentityType, "project", normalized),
            PathIdentity(ProjectRegistrationAttestation.VolumeIdentityType, "volume", volumeRoot),
            PathIdentity(ProjectRegistrationAttestation.DirectoryIdentityType, "repository", normalized),
            PathIdentity(ProjectRegistrationAttestation.DirectoryIdentityType, "root", normalized));
    }

    private static TypedHash PathIdentity(string typeTag, string role, string value) =>
        TypedHash.ComputeUtf8(typeTag, string.Concat(PathIdentityVersion, role, "\0", value));

    private static byte[] ReadStrictProjectJson(
        string projectRoot,
        string documentKind,
        string documentId)
    {
        var relativePath = ResolveTarget(documentKind, documentId);
        if (Path.IsPathRooted(relativePath) || relativePath.IndexOf(':') >= 0 ||
            relativePath.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidDataException(Failure);
        }

        var target = Path.GetFullPath(Path.Combine(projectRoot, relativePath));
        var prefix = projectRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? projectRoot
            : projectRoot + Path.DirectorySeparatorChar;
        if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(target))
        {
            throw new FileNotFoundException(Failure);
        }

        RequireDirectoryTreeNotReparse(Path.GetDirectoryName(target) ?? throw new InvalidDataException(Failure));
        if ((File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(Failure);
        }

        byte[] content;
        using (var stream = new FileStream(
            target,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.SequentialScan))
        {
            if (stream.Length > ReadDocumentResult.MaximumDecodedBytes)
            {
                throw new InvalidDataException(Failure);
            }

            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int count;
            while ((count = stream.Read(chunk, 0, chunk.Length)) != 0)
            {
                if (buffer.Length + count > ReadDocumentResult.MaximumDecodedBytes)
                {
                    throw new InvalidDataException(Failure);
                }

                buffer.Write(chunk, 0, count);
            }

            content = buffer.ToArray();
            CryptographicOperations.ZeroMemory(chunk);
        }

        try
        {
            using var parsed = StrictJsonReader.Parse(
                content,
                new StrictJsonLimits(maximumBytes: ReadDocumentResult.MaximumDecodedBytes));
            if (parsed.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException(Failure);
            }

            return content;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(content);
            throw;
        }
    }

    private static string ResolveTarget(string documentKind, string documentId)
    {
        if (string.Equals(documentKind, DocumentKinds.LibraryIndex, StringComparison.Ordinal) &&
            string.Equals(documentId, "project", StringComparison.Ordinal))
        {
            return "ProjectSettings\\VFXComposer\\LibraryIndex.json";
        }

        if (string.Equals(documentKind, DocumentKinds.Manifest, StringComparison.Ordinal) &&
            IsCanonicalManifestId(documentId))
        {
            return "ProjectSettings\\VFXComposer\\BuildManifests\\" + documentId + ".manifest.json";
        }

        throw new InvalidDataException(Failure);
    }

    private static bool IsCanonicalManifestId(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 96 || value[0] is < 'a' or > 'z')
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character is not (>= 'a' and <= 'z') and
                not (>= '0' and <= '9') and not '_' and not '-')
            {
                return false;
            }
        }

        return true;
    }

    private static void RequireMarker(string path, bool directory)
    {
        if ((directory ? !Directory.Exists(path) : !File.Exists(path)) ||
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(Failure);
        }

        var parent = directory ? new DirectoryInfo(path).Parent : new FileInfo(path).Directory;
        if (parent is not null)
        {
            RequireDirectoryTreeNotReparse(parent.FullName);
        }
    }

    private static void RequireDirectoryTreeNotReparse(string path)
    {
        for (DirectoryInfo? current = new(path); current is not null; current = current.Parent)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException(Failure);
            }
        }
    }

    private static bool IsExpectedReadFailure(Exception exception) => exception is
        IOException or UnauthorizedAccessException or InvalidDataException or DecoderFallbackException or
        ArgumentException or NotSupportedException or JsonException or StrictJsonException;

    private sealed class BoundProject(
        string projectRoot,
        WorkerProjectLocator locator,
        string leaseId)
    {
        internal string ProjectRoot { get; } = projectRoot;
        internal WorkerProjectLocator Locator { get; } = locator;
        internal string LeaseId { get; } = leaseId;
    }

    private sealed record ProjectPathIdentities(
        TypedHash ProjectIdentity,
        TypedHash VolumeIdentity,
        TypedHash RepositoryIdentity,
        TypedHash ProjectRootIdentity);
}
