using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32.SafeHandles;
using VFXComposer.Broker.Configuration;
using VFXComposer.Broker.Ipc;
using VFXComposer.Broker.Native;
using VFXComposer.Broker.Registration;
using VFXComposer.Broker.Queries;
using VFXComposer.Broker.Security;
using VFXComposer.Protocol;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;
using VFXComposer.Protocol.Json;
using VFXComposer.Protocol.Registration;
using VFXComposer.Protocol.Queries;

namespace VFXComposer.Broker.Tests;

[TestClass]
public sealed class NativeRegistrationTests
{
    [TestMethod]
    public void GlobalVolumeRelativeTraversalPinsDistinctReplayableRootsWithoutContentRead()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows native handle gate is Windows-only.");
        }

        var scratch = Path.Combine(Path.GetTempPath(), "vfxcomposer-broker-" + Guid.NewGuid().ToString("N"));
        var repository = Path.Combine(scratch, "repository");
        var project = Path.Combine(repository, "project");
        Directory.CreateDirectory(project);
        try
        {
            var driveRoot = Path.GetPathRoot(scratch)
                ?? throw new InvalidOperationException("Scratch drive root is missing.");
            var volumeGuid = GetVolumeGuid(driveRoot);
            var repositorySegments = repository[driveRoot.Length..]
                .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            var definition = new BrokerRegistrationDefinition(
                "project-native-01",
                volumeGuid,
                repositorySegments,
                ["project"]);
            var currentProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
            using var policyFacts = WindowsNamedPipePeerFactsSource.ObserveProcess(currentProcessId);
            var sid = policyFacts.UserSidIdentity;
            var desktopImage = policyFacts.ImageIdentity;
            var workerImage = policyFacts.ImageIdentity;
            var policy = BrokerTestFactory.CreatePolicy(
                "vfxcomposer-native-test",
                "broker-01",
                1,
                sid,
                desktopImage,
                workerImage,
                [definition]);
            using var sessions = new PeerSessionRegistry(policy);
            using var registrations = new ProjectRegistrationStore(policy, sessions);
            var workerFacts = WindowsNamedPipePeerFactsSource.ObserveProcess(
                currentProcessId,
                allowHandleDuplication: true);
            var currentEpoch = workerFacts.ProcessEpoch;
            var hello = new PeerHello(
                "hello-01",
                PeerRoles.Worker,
                "worker-01",
                currentProcessId,
                currentEpoch,
                [
                    PeerCapabilityIds.PeerSessionV1,
                    PeerCapabilityIds.ReadOnlyQueryV1,
                    PeerCapabilityIds.ProjectRegistrationV1,
                    PeerCapabilityIds.WorkerHandleLifecycleV1,
                ],
                workerImage);
            Assert.IsTrue(sessions.TryAuthenticate(
                hello,
                workerFacts,
                out var worker,
                out _,
                out _));

            Assert.IsTrue(registrations.TryRegisterPinned(
                worker!,
                definition.RegisteredProjectId,
                out var registered,
                out var attestation));
            Assert.IsNotNull(registered);
            Assert.IsNotNull(attestation);
            Assert.AreEqual(ProjectRegistrationAttestation.ProjectIdentityType, registered.ProjectIdentity.TypeTag);
            Assert.AreNotEqual(registered.RepositoryIdentity.Digest, registered.ProjectRootIdentity.Digest);

            var desktopFacts = WindowsNamedPipePeerFactsSource.ObserveProcess(currentProcessId);
            var desktopHello = new PeerHello(
                "hello-02",
                PeerRoles.Desktop,
                "desktop-01",
                currentProcessId,
                currentEpoch,
                [PeerCapabilityIds.PeerSessionV1, PeerCapabilityIds.ReadOnlyQueryV1],
                desktopImage);
            Assert.IsTrue(sessions.TryAuthenticate(
                desktopHello,
                desktopFacts,
                out var desktop,
                out _,
                out _));
            var legacyWorkerFacts = WindowsNamedPipePeerFactsSource.ObserveProcess(
                currentProcessId,
                allowHandleDuplication: true);
            var legacyWorkerHello = new PeerHello(
                "hello-legacy-worker",
                PeerRoles.Worker,
                "worker-legacy",
                currentProcessId,
                currentEpoch,
                [
                    PeerCapabilityIds.PeerSessionV1,
                    PeerCapabilityIds.ReadOnlyQueryV1,
                    PeerCapabilityIds.ProjectRegistrationV1,
                ],
                workerImage);
            Assert.IsFalse(sessions.TryAuthenticate(
                legacyWorkerHello,
                legacyWorkerFacts,
                out var rejectedConcurrentWorker,
                out _,
                out _), "One process epoch may own only one live Worker session.");
            Assert.IsNull(rejectedConcurrentWorker);
            Assert.IsTrue(sessions.Revoke(worker!.SessionId));

            legacyWorkerFacts = WindowsNamedPipePeerFactsSource.ObserveProcess(
                currentProcessId,
                allowHandleDuplication: true);
            Assert.IsTrue(sessions.TryAuthenticate(
                legacyWorkerHello,
                legacyWorkerFacts,
                out var legacyWorker,
                out _,
                out _));
            Assert.IsFalse(registrations.TryAcquirePinnedLease(
                desktop!,
                legacyWorker!,
                definition.RegisteredProjectId,
                "legacy-worker-lease",
                out _,
                out _,
                out _), "Handle duplication requires explicit lifecycle capability negotiation.");
            Assert.IsTrue(sessions.Revoke(legacyWorker!.SessionId));

            var replacementWorkerFacts = WindowsNamedPipePeerFactsSource.ObserveProcess(
                currentProcessId,
                allowHandleDuplication: true);
            var replacementWorkerHello = new PeerHello(
                "hello-replacement-worker",
                PeerRoles.Worker,
                "worker-replacement",
                currentProcessId,
                currentEpoch,
                [
                    PeerCapabilityIds.PeerSessionV1,
                    PeerCapabilityIds.ReadOnlyQueryV1,
                    PeerCapabilityIds.ProjectRegistrationV1,
                    PeerCapabilityIds.WorkerHandleLifecycleV1,
                ],
                workerImage);
            Assert.IsTrue(sessions.TryAuthenticate(
                replacementWorkerHello,
                replacementWorkerFacts,
                out worker,
                out _,
                out _));
            Assert.IsTrue(registrations.TryAcquirePinnedLease(
                desktop!,
                worker!,
                definition.RegisteredProjectId,
                "lease-request-01",
                out var lease,
                out var descriptor,
                out var duplicateDiagnostic));
            Assert.IsNotNull(lease);
            Assert.IsNotNull(descriptor);
            var duplicated = lease.WorkerHandles;
            Assert.IsNotNull(duplicated);
            Assert.AreEqual(string.Empty, duplicateDiagnostic);
            Assert.AreEqual(currentProcessId, duplicated.TargetProcessId);
            Assert.IsTrue(registrations.TryCreateWorkerHandleGrant(
                worker!,
                lease,
                "worker-grant-request-01",
                out var workerGrant,
                out var grantDiagnostic));
            Assert.IsNotNull(workerGrant);
            Assert.AreEqual(string.Empty, grantDiagnostic);
            Assert.AreEqual(worker!.SessionId, workerGrant!.WorkerSessionId);
            Assert.AreEqual(lease.LeaseId, workerGrant.LeaseId);
            Assert.AreEqual(WorkerProjectHandleGrant.HandleEncodingName, workerGrant.HandleEncoding);
            Assert.IsNotNull(StrictWireCodec.Decode<WorkerProjectHandleGrant>(
                System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(workerGrant)));
            Assert.IsTrue(registrations.TryCreateWorkerHandleGrant(
                worker!, lease, "worker-grant-request-01", out var replayedGrant, out _));
            Assert.IsTrue(workerGrant.SelfHash.FixedTimeEquals(replayedGrant!.SelfHash));
            Assert.IsFalse(registrations.TryCreateWorkerHandleGrant(
                worker!, lease, "different-grant-request", out _, out _));
            Assert.AreEqual(WorkerHandleLeaseState.GrantPublished, lease.HandleState);
            var grantAcknowledgement = CreateGrantAcknowledgement(lease, workerGrant);
            Assert.IsTrue(registrations.TryAcknowledgeWorkerHandleGrant(
                worker!,
                lease,
                grantAcknowledgement,
                out var grantAckDiagnostic));
            Assert.AreEqual(string.Empty, grantAckDiagnostic);
            Assert.AreEqual(WorkerHandleLeaseState.GrantAcknowledged, lease.HandleState);
            Assert.IsTrue(registrations.TryAcknowledgeWorkerHandleGrant(
                worker!, lease, grantAcknowledgement, out _));
            Assert.IsFalse(registrations.TryAcknowledgeWorkerHandleGrant(
                worker!,
                lease,
                CreateGrantAcknowledgement(lease, workerGrant, "different-grant-ack"),
                out _));
            using var duplicatedVolume = new SafeFileHandle(duplicated.VolumeHandle, ownsHandle: false);
            using var duplicatedRepository = new SafeFileHandle(duplicated.RepositoryHandle, ownsHandle: false);
            using var duplicatedProject = new SafeFileHandle(duplicated.ProjectRootHandle, ownsHandle: false);
            foreach (var handle in new[] { duplicatedVolume, duplicatedRepository, duplicatedProject })
            {
                Assert.IsFalse(handle.IsInvalid);
                Assert.IsTrue(GetHandleInformation(handle, out var flags));
                Assert.AreEqual(0u, flags & 1u, "Duplicated project handles must be non-inheritable.");
            }
            var router = new ReadOnlyQueryRouter(registrations, sessions);
            var query = new ReadDocumentQuery(
                ProtocolVersions.Current,
                MessageKinds.ReadDocumentQuery,
                "read-request-01",
                lease.LeaseId,
                registered.ProjectIdentity,
                lease.LeaseGeneration,
                DocumentKinds.Manifest,
                "effect-fire-manifest",
                null);
            Assert.IsTrue(router.TryRoute(
                desktop!,
                lease,
                query,
                out var routed,
                out var routeDiagnostic));
            Assert.IsNotNull(routed);
            Assert.AreSame(worker, routed.WorkerSession);
            Assert.AreEqual(string.Empty, routeDiagnostic);

            var stale = new ReadDocumentQuery(
                ProtocolVersions.Current,
                MessageKinds.ReadDocumentQuery,
                "read-request-02",
                lease.LeaseId,
                registered.ProjectIdentity,
                lease.LeaseGeneration + 1,
                DocumentKinds.Manifest,
                "effect-fire-manifest",
                null);
            Assert.IsFalse(router.TryRoute(desktop!, lease, stale, out _, out _));
            Assert.IsTrue(registrations.RevokeLease(lease.LeaseId));
            Assert.IsFalse(registrations.IsCurrent(lease));
            Assert.AreEqual(WorkerHandleLeaseState.RevocationPending, lease.HandleState);
            Assert.IsTrue(GetHandleInformation(duplicatedProject, out _),
                "Published raw handle numbers must not be duplicate-closed before Worker acknowledgement.");
            Assert.IsTrue(registrations.TryCreateWorkerHandleRevoke(
                worker!,
                lease.LeaseId,
                "worker-revoke-request-01",
                out var workerRevoke,
                out var revokeDiagnostic));
            Assert.IsNotNull(workerRevoke);
            Assert.AreEqual(string.Empty, revokeDiagnostic);
            Assert.AreEqual(WorkerHandleLeaseState.RevokePublished, lease.HandleState);
            Assert.IsTrue(registrations.TryCreateWorkerHandleRevoke(
                worker!, lease.LeaseId, "worker-revoke-request-01", out var replayedRevoke, out _));
            Assert.IsTrue(workerRevoke!.SelfHash.FixedTimeEquals(replayedRevoke!.SelfHash));
            Assert.IsFalse(registrations.TryCreateWorkerHandleRevoke(
                worker!, lease.LeaseId, "different-revoke-request", out _, out _));

            Assert.IsTrue(CloseHandle(duplicated.ProjectRootHandle));
            Assert.IsTrue(CloseHandle(duplicated.RepositoryHandle));
            Assert.IsTrue(CloseHandle(duplicated.VolumeHandle));
            var revokeAcknowledgement = CreateRevokeAcknowledgement(
                lease,
                workerGrant,
                workerRevoke!);
            var wrongGrantAcknowledgement = CreateRevokeAcknowledgement(
                lease,
                workerGrant,
                workerRevoke!,
                "wrong-revoke-ack",
                TypedHash.ComputeUtf8(WorkerProjectHandleGrant.SelfHashType, "wrong-grant"));
            Assert.IsFalse(registrations.TryAcknowledgeWorkerHandleRevoke(
                worker!, wrongGrantAcknowledgement, out _));
            Assert.IsTrue(registrations.TryAcknowledgeWorkerHandleRevoke(
                worker!,
                revokeAcknowledgement,
                out var revokeAckDiagnostic));
            Assert.AreEqual(string.Empty, revokeAckDiagnostic);
            Assert.IsTrue(registrations.TryAcknowledgeWorkerHandleRevoke(
                worker!,
                revokeAcknowledgement,
                out var replayedRevokeAckDiagnostic));
            Assert.AreEqual(string.Empty, replayedRevokeAckDiagnostic);
            Assert.IsFalse(registrations.TryAcknowledgeWorkerHandleRevoke(
                worker!,
                CreateRevokeAcknowledgement(
                    lease,
                    workerGrant,
                    workerRevoke!,
                    "different-revoke-ack-replay"),
                out _));
            Assert.AreEqual(WorkerHandleLeaseState.Revoked, lease.HandleState);
            Assert.IsFalse(GetHandleInformation(duplicatedProject, out _));
            Assert.IsTrue(sessions.Revoke(worker!.SessionId));
            Assert.IsFalse(registrations.TryAcknowledgeWorkerHandleRevoke(
                worker!,
                revokeAcknowledgement,
                out _),
                "A revoke-ACK tombstone must stop accepting retries when its Worker session is no longer current.");
            Assert.IsFalse(registrations.RevokeLease(lease.LeaseId));
            registrations.Dispose();
            Assert.IsFalse(registrations.TryRegisterPinned(
                worker!,
                definition.RegisteredProjectId,
                out _,
                out _));
            Assert.IsFalse(registrations.TryAcquirePinnedLease(
                desktop!,
                worker!,
                definition.RegisteredProjectId,
                "lease-after-dispose",
                out _,
                out _,
                out _));
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }
    }

    [TestMethod]
    public void RegistrationDefinitionRejectsDosUncAdsTraversalAndReservedSegmentsBeforeOpen()
    {
        const string volume = "\\\\?\\Volume{00000000-0000-0000-0000-000000000000}\\";
        foreach (var segments in new[]
                 {
                     new[] { ".." },
                     new[] { "child:stream" },
                     new[] { "\\\\server" },
                     new[] { "CON" },
                     new[] { "trailing." },
                 })
        {
            Assert.ThrowsExactly<ArgumentException>(() => new BrokerRegistrationDefinition(
                "project-01",
                volume,
                segments,
                ["project"]));
        }

        foreach (var forbiddenRoot in new[]
                 {
                     "D:\\",
                     "\\\\.\\D:\\",
                     "\\\\?\\D:\\",
                     "\\\\?\\UNC\\server\\share\\",
                     "\\\\?\\GLOBALROOT\\Device\\HarddiskVolume1\\",
                     "\\Device\\HarddiskVolume1\\",
                 })
        {
            Assert.ThrowsExactly<ArgumentException>(() => new BrokerRegistrationDefinition(
                "project-01",
                forbiddenRoot,
                ["repository"],
                ["project"]), forbiddenRoot);
        }
    }

    private static string GetVolumeGuid(string driveRoot)
    {
        var builder = new StringBuilder(64);
        if (!GetVolumeNameForVolumeMountPoint(driveRoot, builder, builder.Capacity))
        {
            throw new InvalidOperationException("Volume GUID lookup failed.");
        }

        return builder.ToString();
    }

    private static WorkerProjectHandleGrantAcknowledgement CreateGrantAcknowledgement(
        RegisteredProjectLease lease,
        WorkerProjectHandleGrant grant,
        string requestId = "grant-ack-01")
    {
        var root = new JsonObject
        {
            ["protocolVersion"] = ProtocolVersions.Current,
            ["messageKind"] = MessageKinds.WorkerProjectHandleGrantAcknowledgement,
            ["requestId"] = requestId,
            ["leaseId"] = lease.LeaseId,
            ["brokerGeneration"] = lease.BrokerGeneration,
            ["leaseGeneration"] = lease.LeaseGeneration,
            ["workerSessionId"] = lease.WorkerSession.SessionId,
            ["workerProcessEpoch"] = lease.WorkerSession.ProcessEpoch,
            ["grantSelfHash"] = JsonSerializer.SerializeToNode(grant.SelfHash),
            ["disposition"] = WorkerProjectHandleGrantAcknowledgement.AcceptedDisposition,
            ["selfHash"] = JsonSerializer.SerializeToNode(TypedHash.ComputeUtf8(
                WorkerProjectHandleGrantAcknowledgement.SelfHashType,
                "placeholder")),
        };
        return StrictWireCodec.Decode<WorkerProjectHandleGrantAcknowledgement>(
            Seal(root, WorkerProjectHandleGrantAcknowledgement.SelfHashType));
    }

    private static WorkerProjectHandleRevokeAcknowledgement CreateRevokeAcknowledgement(
        RegisteredProjectLease lease,
        WorkerProjectHandleGrant grant,
        WorkerProjectHandleRevoke revoke,
        string requestId = "revoke-ack-01",
        TypedHash? grantSelfHash = null)
    {
        var root = new JsonObject
        {
            ["protocolVersion"] = ProtocolVersions.Current,
            ["messageKind"] = MessageKinds.WorkerProjectHandleRevokeAcknowledgement,
            ["requestId"] = requestId,
            ["leaseId"] = lease.LeaseId,
            ["brokerGeneration"] = lease.BrokerGeneration,
            ["leaseGeneration"] = lease.LeaseGeneration,
            ["workerSessionId"] = lease.WorkerSession.SessionId,
            ["workerProcessEpoch"] = lease.WorkerSession.ProcessEpoch,
            ["grantSelfHash"] = JsonSerializer.SerializeToNode(grantSelfHash ?? grant.SelfHash),
            ["revokeSelfHash"] = JsonSerializer.SerializeToNode(revoke.SelfHash),
            ["disposition"] = WorkerProjectHandleRevokeAcknowledgement.ClosedDisposition,
            ["selfHash"] = JsonSerializer.SerializeToNode(TypedHash.ComputeUtf8(
                WorkerProjectHandleRevokeAcknowledgement.SelfHashType,
                "placeholder")),
        };
        return StrictWireCodec.Decode<WorkerProjectHandleRevokeAcknowledgement>(
            Seal(root, WorkerProjectHandleRevokeAcknowledgement.SelfHashType));
    }

    private static byte[] Seal(JsonObject root, string typeTag)
    {
        root["selfHash"] = JsonSerializer.SerializeToNode(SelfHash.Compute(
            JsonSerializer.SerializeToUtf8Bytes(root),
            typeTag));
        return JsonSerializer.SerializeToUtf8Bytes(root);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeNameForVolumeMountPoint(
        string volumeMountPoint,
        StringBuilder volumeName,
        int bufferLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetHandleInformation(
        SafeFileHandle handle,
        out uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
