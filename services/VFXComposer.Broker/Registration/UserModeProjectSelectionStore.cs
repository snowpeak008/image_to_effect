using System.Text.Json;
using System.Runtime.Versioning;
using VFXComposer.Broker.Ipc;
using VFXComposer.Protocol;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Json;
using VFXComposer.Protocol.Projects;
using VFXComposer.Protocol.Registration;

namespace VFXComposer.Broker.Registration;

/// <summary>
/// Holds one ordinary-user project selection. The validated path remains local to
/// selection and is deliberately not retained: only opaque correlations cross the
/// Broker/Worker boundary.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class UserModeProjectSelectionStore
{
    private readonly SemaphoreSlim _exchangeGate = new(1, 1);
    private UserModeProjectLease? _current;
    private long _registrationGeneration;

    internal UserModeProjectLease? Current => Volatile.Read(ref _current);

    internal async ValueTask<UserModeProjectLease> SelectAsync(
        string explicitProjectRoot,
        UserModeBrokerWorkerSession workerSession,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workerSession);
        await _exchangeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var canonicalProjectRoot = UserModeProjectRootValidator.Validate(explicitProjectRoot);
            RequireUsable(workerSession);

            var prior = Volatile.Read(ref _current);
            prior?.Revoke();
            var registrationGeneration = checked(++_registrationGeneration);
            var lease = UserModeProjectLease.Create(
                workerSession,
                registrationGeneration,
                canonicalProjectRoot);
            Volatile.Write(ref _current, lease);
            return lease;
        }
        finally
        {
            _exchangeGate.Release();
        }
    }

    internal async ValueTask RevokeAsync(
        UserModeProjectLease lease,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        await _exchangeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (ReferenceEquals(Volatile.Read(ref _current), lease))
            {
                lease.Revoke();
                Volatile.Write(ref _current, null);
            }
            else
            {
                lease.Revoke();
            }
        }
        finally
        {
            _exchangeGate.Release();
        }
    }

    internal async ValueTask<IDisposable> BeginExchangeAsync(
        UserModeProjectLease lease,
        UserModeBrokerWorkerSession workerSession,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(workerSession);
        await _exchangeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!ReferenceEquals(Volatile.Read(ref _current), lease) ||
                !lease.Matches(workerSession))
            {
                throw new InvalidOperationException("U3FS001");
            }

            return new ExchangeReleaser(_exchangeGate);
        }
        catch
        {
            _exchangeGate.Release();
            throw;
        }
    }

    internal bool IsCurrent(UserModeProjectLease lease, UserModeBrokerWorkerSession workerSession) =>
        ReferenceEquals(Volatile.Read(ref _current), lease) && lease.Matches(workerSession);

    private static void RequireUsable(UserModeBrokerWorkerSession workerSession)
    {
        if (!workerSession.IsUsable || workerSession.Generation <= 0 ||
            string.IsNullOrEmpty(workerSession.SessionId) ||
            string.IsNullOrEmpty(workerSession.ChildProcessEpoch))
        {
            throw new InvalidOperationException("U3FS001");
        }

        _ = workerSession.Transport;
    }

    private sealed class ExchangeReleaser : IDisposable
    {
        private SemaphoreSlim? _gate;

        internal ExchangeReleaser(SemaphoreSlim gate) => _gate = gate;

        public void Dispose() => Interlocked.Exchange(ref _gate, null)?.Release();
    }
}

[SupportedOSPlatform("windows")]
internal sealed class UserModeProjectLease
{
    private int _revoked;

    private UserModeProjectLease(
        RegisteredProjectSelection selection,
        WorkerProjectLocator locator,
        byte[] locatorBytes,
        string leaseId,
        Stream transport)
    {
        Selection = selection;
        Locator = locator;
        LocatorBytes = locatorBytes;
        LeaseId = leaseId;
        Transport = transport;
    }

    internal RegisteredProjectSelection Selection { get; }
    internal WorkerProjectLocator Locator { get; }
    private byte[] LocatorBytes { get; }
    internal string LeaseId { get; }
    internal long LeaseGeneration => Locator.EnrollmentGeneration;
    internal Stream Transport { get; }
    internal bool IsRevoked => Volatile.Read(ref _revoked) != 0;

    internal static UserModeProjectLease Create(
        UserModeBrokerWorkerSession workerSession,
        long registrationGeneration,
        string canonicalProjectRoot)
    {
        var identities = UserModeProjectPathIdentity.Compute(canonicalProjectRoot);
        var projectIdentity = identities.ProjectIdentity;
        var volumeIdentity = identities.VolumeIdentity;
        var repositoryIdentity = identities.RepositoryIdentity;
        var rootIdentity = identities.ProjectRootIdentity;
        var registeredProjectId =
            "um-project-" + projectIdentity.Digest["sha256:".Length..("sha256:".Length + 32)];
        var requestId = "um-select-" + Guid.NewGuid().ToString("N");

        var selection = new RegisteredProjectSelection(
            ProtocolVersions.Current,
            MessageKinds.RegisteredProjectSelection,
            requestId,
            registeredProjectId,
            projectIdentity,
            workerSession.Generation,
            registrationGeneration);
        selection = StrictWireCodec.Decode<RegisteredProjectSelection>(
            JsonSerializer.SerializeToUtf8Bytes(selection));

        var placeholder = TypedHash.ComputeUtf8(WorkerProjectLocator.SelfHashType, "placeholder");
        var provisional = new WorkerProjectLocator(
            ProtocolVersions.Current,
            MessageKinds.WorkerProjectLocator,
            requestId,
            registeredProjectId,
            projectIdentity,
            volumeIdentity,
            repositoryIdentity,
            rootIdentity,
            workerSession.Generation,
            registrationGeneration,
            registrationGeneration,
            workerSession.SessionId,
            workerSession.ChildProcessEpoch,
            placeholder);
        var selfHash = SelfHash.Compute(
            JsonSerializer.SerializeToUtf8Bytes(provisional),
            WorkerProjectLocator.SelfHashType);
        var locator = new WorkerProjectLocator(
            provisional.ProtocolVersion,
            provisional.MessageKind,
            provisional.RequestId,
            provisional.RegisteredProjectId,
            provisional.ProjectIdentity,
            provisional.VolumeIdentity,
            provisional.RepositoryIdentity,
            provisional.ProjectRootIdentity,
            provisional.BrokerGeneration,
            provisional.RegistrationGeneration,
            provisional.EnrollmentGeneration,
            provisional.WorkerSessionId,
            provisional.WorkerProcessEpoch,
            selfHash);
        var locatorBytes = JsonSerializer.SerializeToUtf8Bytes(locator);
        locator = StrictWireCodec.Decode<WorkerProjectLocator>(locatorBytes);

        return new UserModeProjectLease(
            selection,
            locator,
            (byte[])locatorBytes.Clone(),
            "um-lease-" + selfHash.Digest["sha256:".Length..],
            workerSession.Transport);
    }

    internal byte[] CopyLocatorBytes() => (byte[])LocatorBytes.Clone();

    internal bool Matches(UserModeBrokerWorkerSession workerSession)
    {
        if (IsRevoked || !workerSession.IsUsable ||
            workerSession.Generation != Locator.BrokerGeneration ||
            !string.Equals(workerSession.SessionId, Locator.WorkerSessionId, StringComparison.Ordinal) ||
            !string.Equals(workerSession.ChildProcessEpoch, Locator.WorkerProcessEpoch, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            return ReferenceEquals(workerSession.Transport, Transport);
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    internal bool MatchesCorrelation(long generation, string sessionId, string processEpoch) =>
        !IsRevoked && generation == Locator.BrokerGeneration &&
        string.Equals(sessionId, Locator.WorkerSessionId, StringComparison.Ordinal) &&
        string.Equals(processEpoch, Locator.WorkerProcessEpoch, StringComparison.Ordinal);

    internal void Revoke() => Interlocked.Exchange(ref _revoked, 1);

    public override string ToString() =>
        $"UserModeProjectLease(Generation={Locator.EnrollmentGeneration}, Revoked={IsRevoked})";
}

internal static class UserModeProjectRootValidator
{
    internal static string Validate(string explicitProjectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(explicitProjectRoot);
        if (explicitProjectRoot.Length < 3 ||
            !char.IsAsciiLetter(explicitProjectRoot[0]) ||
            explicitProjectRoot[1] != ':' || explicitProjectRoot[2] != '\\' ||
            explicitProjectRoot.StartsWith("\\\\", StringComparison.Ordinal) ||
            explicitProjectRoot.StartsWith("\\\\?\\", StringComparison.Ordinal) ||
            explicitProjectRoot.StartsWith("\\\\.\\", StringComparison.Ordinal) ||
            explicitProjectRoot.IndexOf(':', 2) >= 0)
        {
            throw new ArgumentException("U3FS001", nameof(explicitProjectRoot));
        }

        var segments = explicitProjectRoot[3..].Split('\\');
        if (segments.Length == 0 || segments.Any(segment =>
                segment.Length == 0 || segment is "." or ".." ||
                segment.EndsWith(' ') || segment.EndsWith('.')))
        {
            throw new ArgumentException("U3FS001", nameof(explicitProjectRoot));
        }

        string canonical;
        try
        {
            canonical = Path.GetFullPath(explicitProjectRoot);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException("U3FS001", nameof(explicitProjectRoot), exception);
        }

        if (!string.Equals(explicitProjectRoot, canonical, StringComparison.OrdinalIgnoreCase) ||
            !Directory.Exists(canonical))
        {
            throw new ArgumentException("U3FS001", nameof(explicitProjectRoot));
        }

        RequireDirectoryTreeNotReparse(canonical);
        RequireMarker(Path.Combine(canonical, "Assets"), directory: true);
        RequireMarker(Path.Combine(canonical, "Packages", "manifest.json"), directory: false);
        RequireMarker(Path.Combine(canonical, "ProjectSettings", "ProjectVersion.txt"), directory: false);
        return canonical;
    }

    private static void RequireDirectoryTreeNotReparse(string path)
    {
        for (DirectoryInfo? current = new(path); current is not null; current = current.Parent)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new ArgumentException("U3FS001", nameof(path));
            }
        }
    }

    private static void RequireMarker(string path, bool directory)
    {
        if ((directory ? !Directory.Exists(path) : !File.Exists(path)) ||
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new ArgumentException("U3FS001", nameof(path));
        }

        var parent = directory ? new DirectoryInfo(path).Parent : new FileInfo(path).Directory;
        if (parent is not null)
        {
            RequireDirectoryTreeNotReparse(parent.FullName);
        }
    }
}

internal sealed record UserModeProjectPathIdentities(
    TypedHash ProjectIdentity,
    TypedHash VolumeIdentity,
    TypedHash RepositoryIdentity,
    TypedHash ProjectRootIdentity);

/// <summary>
/// Correlates the user's canonical local selection with the Worker's independently
/// admitted current directory without putting a path on the C1/C2 wire. These
/// typed hashes are correlation values, not hostile-same-user security claims.
/// </summary>
internal static class UserModeProjectPathIdentity
{
    private const string Version = "vfxcomposer.user-mode-project-path-correlation/1\0";

    internal static UserModeProjectPathIdentities Compute(string canonicalProjectRoot)
    {
        var canonical = UserModeProjectRootValidator.Validate(canonicalProjectRoot);
        var normalized = canonical.TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
        var volumeRoot = Path.GetPathRoot(canonical)?.ToUpperInvariant();
        if (string.IsNullOrEmpty(volumeRoot) || volumeRoot.Length != 3)
        {
            throw new ArgumentException("U3FS001", nameof(canonicalProjectRoot));
        }

        return new UserModeProjectPathIdentities(
            Hash(ProjectRegistrationAttestation.ProjectIdentityType, "project", normalized),
            Hash(ProjectRegistrationAttestation.VolumeIdentityType, "volume", volumeRoot),
            Hash(ProjectRegistrationAttestation.DirectoryIdentityType, "repository", normalized),
            Hash(ProjectRegistrationAttestation.DirectoryIdentityType, "root", normalized));
    }

    private static TypedHash Hash(string typeTag, string role, string value) =>
        TypedHash.ComputeUtf8(typeTag, string.Concat(Version, role, "\0", value));
}
