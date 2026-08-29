using System;
using System.IO;
using System.Text;
using VFXComposer.Editor.W24;
using VFXComposer.Editor.W24.S6.Worker.Production;
using VFXComposer.Editor.W24.S6.Worker.Protocol;

namespace VFXComposer.Editor.W24.S6.Worker
{
    /// <summary>
    /// Ordinary-user, Worker-owned project read boundary. The current working
    /// directory is independently admitted and never appears in protocol data.
    /// </summary>
    internal sealed class W24S6UserModeProjectReadSession : IDisposable
    {
        private const int MaximumReadBytes = 512 * 1024;
        private const string PathIdentityVersion =
            "vfxcomposer.user-mode-project-path-correlation/1\0";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly object _gate = new object();
        private readonly W24S6DedicatedWorkerConnector _connector;
        private readonly string _projectRoot;
        private readonly W24S6WorkerProjectLocator _locator;
        private bool _revoked;

        private W24S6UserModeProjectReadSession(
            W24S6DedicatedWorkerConnector connector,
            string projectRoot,
            W24S6WorkerProjectLocator locator)
        {
            _connector = connector;
            _projectRoot = projectRoot;
            _locator = locator;
            LeaseId = "um-lease-" + locator.SelfHash.Digest.Substring("sha256:".Length);
        }

        internal string LeaseId { get; private set; }
        internal long LeaseGeneration { get { return _locator.EnrollmentGeneration; } }
        internal W24S6WorkerTypedHash ProjectIdentity { get { return _locator.ProjectIdentity; } }

        internal bool IsUsable
        {
            get
            {
                lock (_gate) return !_revoked && _connector.IsConnected;
            }
        }

        internal static W24S6UserModeProjectReadSession Open(
            byte[] exactLocatorBytes,
            long expectedBrokerGeneration,
            string expectedWorkerSessionId,
            string expectedWorkerProcessEpoch)
        {
            if (exactLocatorBytes == null) throw new ArgumentNullException("exactLocatorBytes");
            var projectRoot = ValidateWorkingDirectory();
            var connector = new W24S6DedicatedWorkerConnector();
            try
            {
                var accepted = connector.AcceptHostOwnedLocator(exactLocatorBytes);
                var locator = accepted.Projection;
                if (expectedBrokerGeneration < 1 ||
                    locator.BrokerGeneration != expectedBrokerGeneration ||
                    !string.Equals(locator.WorkerSessionId, expectedWorkerSessionId, StringComparison.Ordinal) ||
                    !string.Equals(locator.WorkerProcessEpoch, expectedWorkerProcessEpoch, StringComparison.Ordinal))
                    throw new InvalidDataException("U3FS001");
                RequireLocatorMatchesProjectRoot(locator, projectRoot);
                return new W24S6UserModeProjectReadSession(connector, projectRoot, locator);
            }
            catch
            {
                connector.Disconnect();
                throw;
            }
        }

        internal byte[] Handle(byte[] exactQueryBytes)
        {
            lock (_gate)
            {
                var query = W24S6WorkerReadQueryCodec.DecodeQuery(exactQueryBytes);
                if (_revoked || !_connector.IsConnected ||
                    !string.Equals(query.LeaseId, LeaseId, StringComparison.Ordinal) ||
                    query.LeaseGeneration != _locator.EnrollmentGeneration ||
                    !W24S6WorkerProtocolCodec.FixedTimeEquals(query.ProjectIdentity, _locator.ProjectIdentity))
                    return W24S6WorkerReadQueryCodec.CreateRejectedResult(
                        query,
                        W24S6WorkerReadQueryCodec.ProjectLeaseRejected);

                if (string.Equals(query.DocumentKind, W24S6WorkerReadQueryCodec.ContractKind, StringComparison.Ordinal) ||
                    string.Equals(query.DocumentKind, W24S6WorkerReadQueryCodec.TraceKind, StringComparison.Ordinal))
                    return W24S6WorkerReadQueryCodec.CreateRejectedResult(
                        query,
                        W24S6WorkerReadQueryCodec.ProjectDocumentUnavailable);

                try
                {
                    var relativePath = ResolveV1Target(query.DocumentKind, query.DocumentId);
                    var content = ReadStrictProjectJson(relativePath);
                    var actualHash = W24S6WorkerProtocolCodec.ComputeTypedHash(
                        W24S6WorkerReadQueryCodec.ContentHashType,
                        content);
                    if (query.ExpectedContentHash != null &&
                        !W24S6WorkerProtocolCodec.FixedTimeEquals(query.ExpectedContentHash, actualHash))
                        return W24S6WorkerReadQueryCodec.CreateRejectedResult(
                            query,
                            W24S6WorkerReadQueryCodec.ProjectDocumentContentMismatch);
                    return W24S6WorkerReadQueryCodec.CreateAcceptedResult(query, content);
                }
                catch (Exception exception)
                {
                    if (!IsExpectedReadFailure(exception)) throw;
                    return W24S6WorkerReadQueryCodec.CreateRejectedResult(
                        query,
                        W24S6WorkerReadQueryCodec.ProjectDocumentUnavailable);
                }
            }
        }

        internal void Revoke()
        {
            lock (_gate)
            {
                _revoked = true;
                _connector.Disconnect();
            }
        }

        public void Dispose()
        {
            Revoke();
        }

        public override string ToString()
        {
            return "W24S6UserModeProjectReadSession(Generation=" +
                   _locator.EnrollmentGeneration + ", Usable=" + IsUsable + ")";
        }

        private static string ValidateWorkingDirectory()
        {
            var candidate = Directory.GetCurrentDirectory();
            if (string.IsNullOrEmpty(candidate) || candidate.Length < 3 ||
                !IsAsciiLetter(candidate[0]) || candidate[1] != ':' || candidate[2] != '\\' ||
                candidate.StartsWith("\\\\", StringComparison.Ordinal) ||
                candidate.StartsWith("\\\\?\\", StringComparison.Ordinal) ||
                candidate.StartsWith("\\\\.\\", StringComparison.Ordinal) ||
                candidate.IndexOf(':', 2) >= 0)
                throw new InvalidDataException("U3FS001");
            var canonical = Path.GetFullPath(candidate);
            if (!string.Equals(candidate, canonical, StringComparison.OrdinalIgnoreCase) ||
                !Directory.Exists(canonical))
                throw new InvalidDataException("U3FS001");
            RequireDirectoryTreeNotReparse(canonical);
            RequireMarker(Path.Combine(canonical, "Assets"), true);
            RequireMarker(Path.Combine(canonical, "Packages", "manifest.json"), false);
            RequireMarker(Path.Combine(canonical, "ProjectSettings", "ProjectVersion.txt"), false);
            return canonical;
        }

        private static string ResolveV1Target(string documentKind, string documentId)
        {
            if (string.Equals(documentKind, W24S6WorkerReadQueryCodec.LibraryIndexKind, StringComparison.Ordinal) &&
                string.Equals(documentId, "project", StringComparison.Ordinal))
                return "ProjectSettings/VFXComposer/LibraryIndex.json";
            if (string.Equals(documentKind, W24S6WorkerReadQueryCodec.ManifestKind, StringComparison.Ordinal))
                return "ProjectSettings/VFXComposer/BuildManifests/" + documentId + ".manifest.json";
            throw new InvalidDataException("U3FS001");
        }

        private static void RequireLocatorMatchesProjectRoot(
            W24S6WorkerProjectLocator locator,
            string projectRoot)
        {
            var normalized = projectRoot.TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
            var volumeRoot = Path.GetPathRoot(projectRoot);
            if (string.IsNullOrEmpty(volumeRoot) || volumeRoot.Length != 3)
                throw new InvalidDataException("U3FS001");
            volumeRoot = volumeRoot.ToUpperInvariant();

            if (!W24S6WorkerProtocolCodec.FixedTimeEquals(
                    locator.ProjectIdentity,
                    PathIdentity(W24S6WorkerProtocolCodec.ProjectIdentityType, "project", normalized)) ||
                !W24S6WorkerProtocolCodec.FixedTimeEquals(
                    locator.VolumeIdentity,
                    PathIdentity(W24S6WorkerProtocolCodec.VolumeIdentityType, "volume", volumeRoot)) ||
                !W24S6WorkerProtocolCodec.FixedTimeEquals(
                    locator.RepositoryIdentity,
                    PathIdentity(W24S6WorkerProtocolCodec.DirectoryIdentityType, "repository", normalized)) ||
                !W24S6WorkerProtocolCodec.FixedTimeEquals(
                    locator.ProjectRootIdentity,
                    PathIdentity(W24S6WorkerProtocolCodec.DirectoryIdentityType, "root", normalized)))
                throw new InvalidDataException("U3FS001");
        }

        private static W24S6WorkerTypedHash PathIdentity(
            string typeTag,
            string role,
            string value)
        {
            return W24S6WorkerProtocolCodec.ComputeTypedHash(
                typeTag,
                StrictUtf8.GetBytes(PathIdentityVersion + role + "\0" + value));
        }

        private byte[] ReadStrictProjectJson(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath) || Path.IsPathRooted(relativePath) ||
                relativePath.IndexOf(':') >= 0 || relativePath.Contains(".."))
                throw new InvalidDataException("U3FS001");
            var target = Path.GetFullPath(Path.Combine(_projectRoot, relativePath));
            var prefix = _projectRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? _projectRoot
                : _projectRoot + Path.DirectorySeparatorChar;
            if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(target))
                throw new FileNotFoundException("U3FS001");
            RequireDirectoryTreeNotReparse(Path.GetDirectoryName(target));
            if ((File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("U3FS001");

            byte[] content;
            using (var stream = new FileStream(
                target,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.SequentialScan))
            {
                if (stream.Length > MaximumReadBytes) throw new InvalidDataException("U3FS001");
                using (var buffer = new MemoryStream())
                {
                    var chunk = new byte[8192];
                    int count;
                    while ((count = stream.Read(chunk, 0, chunk.Length)) != 0)
                    {
                        if (buffer.Length + count > MaximumReadBytes)
                            throw new InvalidDataException("U3FS001");
                        buffer.Write(chunk, 0, count);
                    }
                    content = buffer.ToArray();
                }
            }

            W24StrictJsonText.ParseObject(StrictUtf8.GetString(content), "User-mode project document");
            return content;
        }

        private static void RequireMarker(string path, bool directory)
        {
            if ((directory ? !Directory.Exists(path) : !File.Exists(path)) ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("U3FS001");
            var parent = directory ? new DirectoryInfo(path).Parent : new FileInfo(path).Directory;
            if (parent != null) RequireDirectoryTreeNotReparse(parent.FullName);
        }

        private static void RequireDirectoryTreeNotReparse(string path)
        {
            for (var current = new DirectoryInfo(path); current != null; current = current.Parent)
            {
                if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("U3FS001");
            }
        }

        private static bool IsExpectedReadFailure(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException ||
                   exception is InvalidDataException || exception is DecoderFallbackException ||
                   exception is Newtonsoft.Json.JsonException || exception is ArgumentException ||
                   exception is NotSupportedException;
        }

        private static bool IsAsciiLetter(char value)
        {
            return value >= 'A' && value <= 'Z' || value >= 'a' && value <= 'z';
        }
    }
}
