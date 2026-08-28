using System.Collections.Concurrent;
using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using VFXComposer.Broker.Configuration;
using VFXComposer.Broker.Ipc;
using VFXComposer.Protocol;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;
using VFXComposer.Protocol.Json;
using VFXComposer.Protocol.Registration;
using VFXComposer.Protocol.Queries;
using VFXComposer.Broker.Native;

namespace VFXComposer.Broker.Registration;

internal sealed class ProjectRegistrationStore : IDisposable
{
    private const int RevocationAcknowledgementTombstoneLimit = 1024;

    private readonly object _issuer = new();
    private readonly object _lifecycleGate = new();
    private readonly BrokerPolicy _policy;
    private readonly PeerSessionRegistry _sessions;
    private readonly ConcurrentDictionary<string, RegisteredProjectIdentity> _projects =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, RegisteredProjectLease> _leases =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, RegisteredProjectLease> _revokingLeases =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, RevocationAcknowledgementTombstone> _revocationAcknowledgementTombstones =
        new(StringComparer.Ordinal);
    private readonly Queue<RevocationAcknowledgementTombstone> _revocationAcknowledgementTombstoneOrder = new();
    private readonly ConcurrentDictionary<string, WindowsPinnedProjectRoots> _pinnedRoots =
        new(StringComparer.Ordinal);
    private long _nextRegistration;
    private long _nextLease;
    private int _disposed;

    public ProjectRegistrationStore(BrokerPolicy policy, PeerSessionRegistry sessions)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _sessions.SessionRevoked += OnSessionRevoked;
    }

    public bool TryRegisterProduction(out RegisteredProjectIdentity? project, out string diagnosticCode)
    {
        project = null;
        diagnosticCode = BrokerDiagnosticCodes.RegistrationIssuerPending;
        return false;
    }

    internal bool TryRegisterPinned(
        AuthenticatedPeerSession workerSession,
        string registeredProjectId,
        out RegisteredProjectIdentity? project,
        out ProjectRegistrationAttestation? attestation)
    {
        lock (_lifecycleGate)
        {
            if (_disposed != 0)
            {
                project = null;
                attestation = null;
                return false;
            }

            return TryRegisterPinnedCore(
                workerSession,
                registeredProjectId,
                out project,
                out attestation);
        }
    }

    private bool TryRegisterPinnedCore(
        AuthenticatedPeerSession workerSession,
        string registeredProjectId,
        out RegisteredProjectIdentity? project,
        out ProjectRegistrationAttestation? attestation)
    {
        project = null;
        attestation = null;
        if (!_sessions.IsCurrent(workerSession, PeerRoles.Worker) ||
            !workerSession.Capabilities.Contains(PeerCapabilityIds.ProjectRegistrationV1, StringComparer.Ordinal) ||
            !_policy.RegistrationDefinitions.TryGetValue(registeredProjectId, out var definition))
        {
            return false;
        }

        WindowsPinnedProjectRoots? roots = null;
        try
        {
            roots = WindowsPinnedProjectRoots.Open(definition);
            if (!roots.ReplayIdentities())
            {
                return false;
            }

            var volumeIdentity = ComputeNativeIdentity(
                ProjectRegistrationAttestation.VolumeIdentityType,
                roots.Volume.Identity);
            var repositoryIdentity = ComputeNativeIdentity(
                ProjectRegistrationAttestation.DirectoryIdentityType,
                roots.Repository.Identity);
            var projectRootIdentity = ComputeNativeIdentity(
                ProjectRegistrationAttestation.DirectoryIdentityType,
                roots.Project.Identity);
            var projectIdentity = TypedHash.ComputeUtf8(
                ProjectRegistrationAttestation.ProjectIdentityType,
                string.Join("|", volumeIdentity.Digest, repositoryIdentity.Digest, projectRootIdentity.Digest));
            var generation = Interlocked.Increment(ref _nextRegistration);
            var candidate = new RegisteredProjectIdentity(
                registeredProjectId,
                projectIdentity,
                volumeIdentity,
                repositoryIdentity,
                projectRootIdentity,
                generation);
            var candidateAttestation = CreateAttestation(candidate, workerSession);
            if (!_projects.TryAdd(registeredProjectId, candidate))
            {
                return false;
            }

            if (!_pinnedRoots.TryAdd(registeredProjectId, roots))
            {
                _projects.TryRemove(registeredProjectId, out _);
                return false;
            }

            project = candidate;
            attestation = candidateAttestation;
            roots = null;
            return true;
        }
        catch (Exception exception) when (
            exception is InvalidDataException or
            IOException or
            UnauthorizedAccessException or
            ObjectDisposedException or
            PlatformNotSupportedException)
        {
            return false;
        }
        finally
        {
            roots?.Dispose();
        }
    }

    private bool TryAcquireLease(
        AuthenticatedPeerSession desktopSession,
        AuthenticatedPeerSession workerSession,
        string registeredProjectId,
        string requestId,
        out RegisteredProjectLease? lease,
        out ProjectLeaseDescriptor? descriptor,
        out string diagnosticCode)
    {
        lease = null;
        descriptor = null;
        diagnosticCode = BrokerDiagnosticCodes.ProjectUnavailable;
        if (!_sessions.IsCurrent(desktopSession, PeerRoles.Desktop) ||
            !_sessions.IsCurrent(workerSession, PeerRoles.Worker) ||
            !desktopSession.Capabilities.Contains(PeerCapabilityIds.ReadOnlyQueryV1, StringComparer.Ordinal) ||
            !workerSession.Capabilities.Contains(PeerCapabilityIds.ReadOnlyQueryV1, StringComparer.Ordinal) ||
            !workerSession.Capabilities.Contains(PeerCapabilityIds.WorkerHandleLifecycleV1, StringComparer.Ordinal) ||
            !_projects.TryGetValue(registeredProjectId, out var project))
        {
            return false;
        }

        requestId = RequireToken(requestId, nameof(requestId));
        var generation = Interlocked.Increment(ref _nextLease);
        var leaseId = $"lease-{_policy.BrokerGeneration}-{generation}";
        var candidate = RegisteredProjectLease.Issue(
            _issuer,
            _issuer,
            leaseId,
            project,
            _policy.BrokerGeneration,
            generation,
            desktopSession,
            workerSession);
        var candidateDescriptor = CreateLeaseDescriptor(candidate, requestId);
        if (!_leases.TryAdd(leaseId, candidate))
        {
            candidate.Dispose();
            return false;
        }

        lease = candidate;
        descriptor = candidateDescriptor;
        diagnosticCode = string.Empty;
        return true;
    }

    internal bool TryAcquirePinnedLease(
        AuthenticatedPeerSession desktopSession,
        AuthenticatedPeerSession workerSession,
        string registeredProjectId,
        string requestId,
        out RegisteredProjectLease? lease,
        out ProjectLeaseDescriptor? descriptor,
        out string diagnosticCode)
    {
        lock (_lifecycleGate)
        {
            if (_disposed != 0)
            {
                lease = null;
                descriptor = null;
                diagnosticCode = BrokerDiagnosticCodes.ProjectUnavailable;
                return false;
            }

            return TryAcquirePinnedLeaseCore(
                desktopSession,
                workerSession,
                registeredProjectId,
                requestId,
                out lease,
                out descriptor,
                out diagnosticCode);
        }
    }

    private bool TryAcquirePinnedLeaseCore(
        AuthenticatedPeerSession desktopSession,
        AuthenticatedPeerSession workerSession,
        string registeredProjectId,
        string requestId,
        out RegisteredProjectLease? lease,
        out ProjectLeaseDescriptor? descriptor,
        out string diagnosticCode)
    {
        lease = null;
        descriptor = null;
        diagnosticCode = BrokerDiagnosticCodes.ProjectUnavailable;
        if (!_pinnedRoots.TryGetValue(registeredProjectId, out var roots) ||
            !TryAcquireLease(
                desktopSession,
                workerSession,
                registeredProjectId,
                requestId,
                out var candidate,
                out var candidateDescriptor,
                out diagnosticCode))
        {
            return false;
        }

        DuplicatedProjectHandleSet? workerHandles = null;
        try
        {
            var duplicator = new HandleDuplicator(_sessions);
            if (!duplicator.TryDuplicateToWorker(
                    roots,
                    workerSession,
                    out workerHandles,
                    out diagnosticCode) ||
                workerHandles is null ||
                !candidate!.TryAttachWorkerHandles(_issuer, workerHandles))
            {
                workerHandles?.Dispose();
                RevokeLease(candidate!.LeaseId);
                return false;
            }
        }
        catch (Exception exception) when (
            exception is InvalidDataException or
            IOException or
            UnauthorizedAccessException or
            ObjectDisposedException)
        {
            workerHandles?.Dispose();
            RevokeLease(candidate!.LeaseId);
            diagnosticCode = BrokerDiagnosticCodes.SessionStale;
            return false;
        }

        lease = candidate;
        descriptor = candidateDescriptor;
        diagnosticCode = string.Empty;
        return true;
    }

    internal bool TryCreateWorkerHandleGrant(
        AuthenticatedPeerSession workerSession,
        RegisteredProjectLease lease,
        string requestId,
        out WorkerProjectHandleGrant? grant,
        out string diagnosticCode)
    {
        lock (_lifecycleGate)
        {
            if (_disposed != 0)
            {
                grant = null;
                diagnosticCode = BrokerDiagnosticCodes.SessionStale;
                return false;
            }

            return TryCreateWorkerHandleGrantCore(
                workerSession,
                lease,
                requestId,
                out grant,
                out diagnosticCode);
        }
    }

    private bool TryCreateWorkerHandleGrantCore(
        AuthenticatedPeerSession workerSession,
        RegisteredProjectLease lease,
        string requestId,
        out WorkerProjectHandleGrant? grant,
        out string diagnosticCode)
    {
        grant = null;
        diagnosticCode = BrokerDiagnosticCodes.SessionStale;
        if (!IsCurrent(lease) ||
            !ReferenceEquals(workerSession, lease.WorkerSession) ||
            !_sessions.IsCurrent(workerSession, PeerRoles.Worker) ||
            !workerSession.Capabilities.Contains(PeerCapabilityIds.WorkerHandleLifecycleV1, StringComparer.Ordinal) ||
            lease.WorkerHandles is not { } handles ||
            handles.TargetProcessId != workerSession.ProcessId ||
            handles.BrokerGeneration != workerSession.BrokerGeneration ||
            !string.Equals(
                handles.TargetProcessEpoch,
                workerSession.ProcessEpoch,
                StringComparison.Ordinal))
        {
            return false;
        }

        requestId = RequireToken(requestId, nameof(requestId));
        var root = new JsonObject
        {
            ["protocolVersion"] = ProtocolVersions.Current,
            ["messageKind"] = MessageKinds.WorkerProjectHandleGrant,
            ["requestId"] = requestId,
            ["leaseId"] = lease.LeaseId,
            ["registeredProjectId"] = lease.Project.RegisteredProjectId,
            ["projectIdentity"] = JsonSerializer.SerializeToNode(lease.Project.ProjectIdentity),
            ["volumeIdentity"] = JsonSerializer.SerializeToNode(lease.Project.VolumeIdentity),
            ["repositoryIdentity"] = JsonSerializer.SerializeToNode(lease.Project.RepositoryIdentity),
            ["projectRootIdentity"] = JsonSerializer.SerializeToNode(lease.Project.ProjectRootIdentity),
            ["brokerGeneration"] = lease.BrokerGeneration,
            ["registrationGeneration"] = lease.Project.RegistrationGeneration,
            ["leaseGeneration"] = lease.LeaseGeneration,
            ["workerSessionId"] = workerSession.SessionId,
            ["workerProcessEpoch"] = workerSession.ProcessEpoch,
            ["handleEncoding"] = WorkerProjectHandleGrant.HandleEncodingName,
            ["volumeHandle"] = EncodeRemoteHandle(handles.VolumeHandle),
            ["repositoryHandle"] = EncodeRemoteHandle(handles.RepositoryHandle),
            ["projectRootHandle"] = EncodeRemoteHandle(handles.ProjectRootHandle),
            ["selfHash"] = JsonSerializer.SerializeToNode(TypedHash.ComputeUtf8(
                WorkerProjectHandleGrant.SelfHashType,
                "placeholder")),
        };
        var hash = SelfHash.Compute(
            JsonSerializer.SerializeToUtf8Bytes(root),
            WorkerProjectHandleGrant.SelfHashType);
        root["selfHash"] = JsonSerializer.SerializeToNode(hash);
        var candidate = StrictWireCodec.Decode<WorkerProjectHandleGrant>(
            JsonSerializer.SerializeToUtf8Bytes(root));
        if (!lease.TryPublishWorkerHandleGrant(_issuer, candidate, out grant))
        {
            grant = null;
            return false;
        }

        diagnosticCode = string.Empty;
        return true;
    }

    internal bool TryAcknowledgeWorkerHandleGrant(
        AuthenticatedPeerSession workerSession,
        RegisteredProjectLease lease,
        WorkerProjectHandleGrantAcknowledgement acknowledgement,
        out string diagnosticCode)
    {
        lock (_lifecycleGate)
        {
            diagnosticCode = BrokerDiagnosticCodes.SessionStale;
            try
            {
                acknowledgement = StrictWireCodec.Decode<WorkerProjectHandleGrantAcknowledgement>(
                    JsonSerializer.SerializeToUtf8Bytes(acknowledgement));
            }
            catch (WireDecodeException)
            {
                return false;
            }

            if (_disposed != 0 || !IsCurrent(lease) ||
                !ReferenceEquals(workerSession, lease.WorkerSession) ||
                !_sessions.IsCurrent(workerSession, PeerRoles.Worker) ||
                !lease.TryAcknowledgeWorkerHandleGrant(_issuer, acknowledgement))
            {
                return false;
            }

            diagnosticCode = string.Empty;
            return true;
        }
    }

    internal bool TryCreateWorkerHandleRevoke(
        AuthenticatedPeerSession workerSession,
        string leaseId,
        string requestId,
        out WorkerProjectHandleRevoke? revoke,
        out string diagnosticCode)
    {
        lock (_lifecycleGate)
        {
            revoke = null;
            diagnosticCode = BrokerDiagnosticCodes.SessionStale;
            if (_disposed != 0 ||
                !_revokingLeases.TryGetValue(leaseId, out var lease) ||
                !ReferenceEquals(workerSession, lease.WorkerSession) ||
                !_sessions.IsCurrent(workerSession, PeerRoles.Worker) ||
                lease.WorkerHandleGrant is not { } grant)
            {
                return false;
            }

            requestId = RequireToken(requestId, nameof(requestId));
            var root = new JsonObject
            {
                ["protocolVersion"] = ProtocolVersions.Current,
                ["messageKind"] = MessageKinds.WorkerProjectHandleRevoke,
                ["requestId"] = requestId,
                ["leaseId"] = lease.LeaseId,
                ["brokerGeneration"] = lease.BrokerGeneration,
                ["leaseGeneration"] = lease.LeaseGeneration,
                ["workerSessionId"] = workerSession.SessionId,
                ["workerProcessEpoch"] = workerSession.ProcessEpoch,
                ["grantSelfHash"] = JsonSerializer.SerializeToNode(grant.SelfHash),
                ["reasonCode"] = WorkerProjectHandleRevoke.LeaseRevokedReason,
                ["selfHash"] = JsonSerializer.SerializeToNode(TypedHash.ComputeUtf8(
                    WorkerProjectHandleRevoke.SelfHashType,
                    "placeholder")),
            };
            root["selfHash"] = JsonSerializer.SerializeToNode(SelfHash.Compute(
                JsonSerializer.SerializeToUtf8Bytes(root),
                WorkerProjectHandleRevoke.SelfHashType));
            var candidate = StrictWireCodec.Decode<WorkerProjectHandleRevoke>(
                JsonSerializer.SerializeToUtf8Bytes(root));
            if (!lease.TryPublishWorkerHandleRevoke(_issuer, candidate, out revoke))
            {
                revoke = null;
                return false;
            }

            diagnosticCode = string.Empty;
            return true;
        }
    }

    internal bool TryAcknowledgeWorkerHandleRevoke(
        AuthenticatedPeerSession workerSession,
        WorkerProjectHandleRevokeAcknowledgement acknowledgement,
        out string diagnosticCode)
    {
        lock (_lifecycleGate)
        {
            diagnosticCode = BrokerDiagnosticCodes.SessionStale;
            try
            {
                acknowledgement = StrictWireCodec.Decode<WorkerProjectHandleRevokeAcknowledgement>(
                    JsonSerializer.SerializeToUtf8Bytes(acknowledgement));
            }
            catch (WireDecodeException)
            {
                return false;
            }

            if (_disposed != 0 || !_sessions.IsCurrent(workerSession, PeerRoles.Worker))
            {
                return false;
            }

            if (!_revokingLeases.TryGetValue(acknowledgement.LeaseId, out var lease))
            {
                if (_revocationAcknowledgementTombstones.TryGetValue(
                        acknowledgement.LeaseId,
                        out var tombstone) &&
                    tombstone.Matches(workerSession, acknowledgement))
                {
                    diagnosticCode = string.Empty;
                    return true;
                }

                return false;
            }

            if (!ReferenceEquals(workerSession, lease.WorkerSession) ||
                !lease.TryAcknowledgeWorkerHandleRevoke(_issuer, acknowledgement) ||
                !_revokingLeases.TryRemove(lease.LeaseId, out var removed) ||
                !ReferenceEquals(removed, lease))
            {
                return false;
            }

            AddRevocationAcknowledgementTombstone(workerSession, acknowledgement);
            lease.Dispose();
            diagnosticCode = string.Empty;
            return true;
        }
    }

    internal int FinalizeExitedWorkerRevocations()
    {
        lock (_lifecycleGate)
        {
            var finalized = 0;
            foreach (var pair in _revokingLeases.ToArray())
            {
                if (pair.Value.TryFinalizeAfterWorkerExit(_issuer) &&
                    _revokingLeases.TryRemove(pair.Key, out var removed) &&
                    ReferenceEquals(removed, pair.Value))
                {
                    removed.Dispose();
                    finalized++;
                }
            }

            return finalized;
        }
    }

    public bool IsCurrent(RegisteredProjectLease? lease)
    {
        lock (_lifecycleGate)
        {
            if (_disposed != 0 || lease is null || !lease.IsUsable ||
                lease.WorkerHandles is null || !lease.WasIssuedBy(_issuer) ||
                lease.BrokerGeneration != _policy.BrokerGeneration ||
                !_sessions.IsCurrent(lease.DesktopSession, PeerRoles.Desktop) ||
                !_sessions.IsCurrent(lease.WorkerSession, PeerRoles.Worker))
            {
                return false;
            }

            return _leases.TryGetValue(lease.LeaseId, out var current) && ReferenceEquals(current, lease);
        }
    }

    internal bool TryReserveReadResponsePublication(
        AuthenticatedPeerSession desktopSession,
        AuthenticatedPeerSession workerSession,
        RegisteredProjectLease lease,
        ReadDocumentQuery query,
        out RegisteredProjectLease.ReadResponsePublicationReservation? reservation)
    {
        lock (_lifecycleGate)
        {
            reservation = null;
            if (_disposed != 0 ||
                !_sessions.IsCurrent(desktopSession, PeerRoles.Desktop) ||
                !_sessions.IsCurrent(workerSession, PeerRoles.Worker) ||
                !_leases.TryGetValue(lease.LeaseId, out var current) ||
                !ReferenceEquals(current, lease) ||
                !ReferenceEquals(desktopSession, lease.DesktopSession) ||
                !ReferenceEquals(workerSession, lease.WorkerSession) ||
                !query.ProjectIdentity.FixedTimeEquals(lease.Project.ProjectIdentity) ||
                !string.Equals(query.LeaseId, lease.LeaseId, StringComparison.Ordinal) ||
                query.LeaseGeneration != lease.LeaseGeneration)
            {
                return false;
            }

            return lease.TryReserveReadResponsePublication(_issuer, out reservation);
        }
    }

    public bool RevokeLease(string leaseId)
    {
        lock (_lifecycleGate)
        {
            if (!_leases.TryRemove(leaseId, out var lease))
            {
                return false;
            }

            if (!lease.TryBeginRevocation(_issuer, out var requiresWorkerAcknowledgement))
            {
                lease.Dispose();
                return false;
            }

            if (!requiresWorkerAcknowledgement || lease.TryFinalizeAfterWorkerExit(_issuer))
            {
                lease.Dispose();
                return true;
            }

            if (!_revokingLeases.TryAdd(leaseId, lease))
            {
                // Safe fail-closed: published handle numbers are abandoned, never
                // duplicate-closed after the Worker may have reused them.
                lease.Dispose();
                return false;
            }

            return true;
        }
    }

    public void Dispose()
    {
        lock (_lifecycleGate)
        {
            if (_disposed != 0)
            {
                return;
            }

            _disposed = 1;
            _sessions.SessionRevoked -= OnSessionRevoked;
            foreach (var pair in _leases.ToArray())
            {
                RevokeLease(pair.Key);
            }

            foreach (var pair in _revokingLeases.ToArray())
            {
                if (_revokingLeases.TryRemove(pair.Key, out var lease))
                {
                    lease.Dispose();
                }
            }

            _revocationAcknowledgementTombstones.Clear();
            _revocationAcknowledgementTombstoneOrder.Clear();

            foreach (var pair in _pinnedRoots.ToArray())
            {
                if (_pinnedRoots.TryRemove(pair.Key, out var roots))
                {
                    roots.Dispose();
                }
            }
        }
    }

    private void OnSessionRevoked(AuthenticatedPeerSession session)
    {
        lock (_lifecycleGate)
        {
            if (_disposed != 0)
            {
                return;
            }

            foreach (var pair in _leases.ToArray())
            {
                if (ReferenceEquals(pair.Value.DesktopSession, session) ||
                    ReferenceEquals(pair.Value.WorkerSession, session))
                {
                    RevokeLease(pair.Key);
                }
            }

            RemoveRevocationAcknowledgementTombstones(session);

            FinalizeExitedWorkerRevocations();
        }
    }

    private void AddRevocationAcknowledgementTombstone(
        AuthenticatedPeerSession workerSession,
        WorkerProjectHandleRevokeAcknowledgement acknowledgement)
    {
        var tombstone = new RevocationAcknowledgementTombstone(workerSession, acknowledgement);
        _revocationAcknowledgementTombstones.Add(acknowledgement.LeaseId, tombstone);
        _revocationAcknowledgementTombstoneOrder.Enqueue(tombstone);
        while (_revocationAcknowledgementTombstoneOrder.Count > RevocationAcknowledgementTombstoneLimit)
        {
            var expired = _revocationAcknowledgementTombstoneOrder.Dequeue();
            if (_revocationAcknowledgementTombstones.TryGetValue(expired.LeaseId, out var current) &&
                ReferenceEquals(current, expired))
            {
                _revocationAcknowledgementTombstones.Remove(expired.LeaseId);
            }
        }
    }

    private void RemoveRevocationAcknowledgementTombstones(AuthenticatedPeerSession workerSession)
    {
        foreach (var pair in _revocationAcknowledgementTombstones.ToArray())
        {
            if (ReferenceEquals(pair.Value.WorkerSession, workerSession))
            {
                _revocationAcknowledgementTombstones.Remove(pair.Key);
            }
        }

        if (_revocationAcknowledgementTombstoneOrder.Count == 0)
        {
            return;
        }

        var retained = _revocationAcknowledgementTombstoneOrder
            .Where(tombstone => !ReferenceEquals(tombstone.WorkerSession, workerSession))
            .ToArray();
        _revocationAcknowledgementTombstoneOrder.Clear();
        foreach (var tombstone in retained)
        {
            _revocationAcknowledgementTombstoneOrder.Enqueue(tombstone);
        }
    }

    private ProjectRegistrationAttestation CreateAttestation(
        RegisteredProjectIdentity project,
        AuthenticatedPeerSession worker)
    {
        var root = new JsonObject
        {
            ["protocolVersion"] = ProtocolVersions.Current,
            ["messageKind"] = MessageKinds.ProjectRegistrationAttestation,
            ["requestId"] = $"registration-{project.RegistrationGeneration}",
            ["registeredProjectId"] = project.RegisteredProjectId,
            ["projectIdentity"] = JsonSerializer.SerializeToNode(project.ProjectIdentity),
            ["volumeIdentity"] = JsonSerializer.SerializeToNode(project.VolumeIdentity),
            ["repositoryIdentity"] = JsonSerializer.SerializeToNode(project.RepositoryIdentity),
            ["projectRootIdentity"] = JsonSerializer.SerializeToNode(project.ProjectRootIdentity),
            ["brokerGeneration"] = _policy.BrokerGeneration,
            ["registrationGeneration"] = project.RegistrationGeneration,
            ["workerSessionId"] = worker.SessionId,
            ["workerProcessEpoch"] = worker.ProcessEpoch,
            ["selfHash"] = JsonSerializer.SerializeToNode(TypedHash.ComputeUtf8(ProjectRegistrationAttestation.SelfHashType, "placeholder")),
        };
        var hash = SelfHash.Compute(JsonSerializer.SerializeToUtf8Bytes(root), ProjectRegistrationAttestation.SelfHashType);
        root["selfHash"] = JsonSerializer.SerializeToNode(hash);
        return StrictWireCodec.Decode<ProjectRegistrationAttestation>(
            JsonSerializer.SerializeToUtf8Bytes(root));
    }

    private ProjectLeaseDescriptor CreateLeaseDescriptor(RegisteredProjectLease lease, string requestId)
    {
        var root = new JsonObject
        {
            ["protocolVersion"] = ProtocolVersions.Current,
            ["messageKind"] = MessageKinds.ProjectLeaseDescriptor,
            ["requestId"] = requestId,
            ["leaseId"] = lease.LeaseId,
            ["registeredProjectId"] = lease.Project.RegisteredProjectId,
            ["projectIdentity"] = JsonSerializer.SerializeToNode(lease.Project.ProjectIdentity),
            ["brokerGeneration"] = lease.BrokerGeneration,
            ["registrationGeneration"] = lease.Project.RegistrationGeneration,
            ["workerSessionId"] = lease.WorkerSession.SessionId,
            ["workerProcessEpoch"] = lease.WorkerSession.ProcessEpoch,
            ["leaseGeneration"] = lease.LeaseGeneration,
            ["selfHash"] = JsonSerializer.SerializeToNode(TypedHash.ComputeUtf8(ProjectLeaseDescriptor.SelfHashType, "placeholder")),
        };
        var hash = SelfHash.Compute(JsonSerializer.SerializeToUtf8Bytes(root), ProjectLeaseDescriptor.SelfHashType);
        root["selfHash"] = JsonSerializer.SerializeToNode(hash);
        return StrictWireCodec.Decode<ProjectLeaseDescriptor>(
            JsonSerializer.SerializeToUtf8Bytes(root));
    }

    private static TypedHash ComputeNativeIdentity(
        string typeTag,
        NativeDirectoryIdentity identity)
    {
        Span<byte> payload = stackalloc byte[24];
        BinaryPrimitives.WriteUInt64BigEndian(payload[..8], identity.VolumeSerialNumber);
        identity.FileId.Bytes.Span.CopyTo(payload[8..]);
        return TypedHash.Compute(typeTag, payload);
    }

    private static string EncodeRemoteHandle(IntPtr handle)
    {
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            throw new InvalidDataException(BrokerDiagnosticCodes.SessionStale);
        }

        return unchecked((ulong)handle.ToInt64()).ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string RequireToken(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > 128 || value.Any(character =>
                character is not (>= 'A' and <= 'Z') and
                not (>= 'a' and <= 'z') and
                not (>= '0' and <= '9') and
                not '.' and not '_' and not ':' and not '-'))
        {
            throw new ArgumentException("Token has an invalid shape.", parameterName);
        }

        return value;
    }

    private sealed class RevocationAcknowledgementTombstone
    {
        public RevocationAcknowledgementTombstone(
            AuthenticatedPeerSession workerSession,
            WorkerProjectHandleRevokeAcknowledgement acknowledgement)
        {
            WorkerSession = workerSession;
            Acknowledgement = acknowledgement;
        }

        public string LeaseId => Acknowledgement.LeaseId;
        public AuthenticatedPeerSession WorkerSession { get; }
        private WorkerProjectHandleRevokeAcknowledgement Acknowledgement { get; }

        public bool Matches(
            AuthenticatedPeerSession workerSession,
            WorkerProjectHandleRevokeAcknowledgement acknowledgement) =>
            ReferenceEquals(WorkerSession, workerSession) &&
            string.Equals(Acknowledgement.RequestId, acknowledgement.RequestId, StringComparison.Ordinal) &&
            string.Equals(Acknowledgement.LeaseId, acknowledgement.LeaseId, StringComparison.Ordinal) &&
            Acknowledgement.BrokerGeneration == acknowledgement.BrokerGeneration &&
            Acknowledgement.LeaseGeneration == acknowledgement.LeaseGeneration &&
            string.Equals(Acknowledgement.WorkerSessionId, acknowledgement.WorkerSessionId, StringComparison.Ordinal) &&
            string.Equals(Acknowledgement.WorkerProcessEpoch, acknowledgement.WorkerProcessEpoch, StringComparison.Ordinal) &&
            Acknowledgement.GrantSelfHash.FixedTimeEquals(acknowledgement.GrantSelfHash) &&
            Acknowledgement.RevokeSelfHash.FixedTimeEquals(acknowledgement.RevokeSelfHash) &&
            string.Equals(Acknowledgement.Disposition, acknowledgement.Disposition, StringComparison.Ordinal) &&
            Acknowledgement.SelfHash.FixedTimeEquals(acknowledgement.SelfHash);
    }

}
