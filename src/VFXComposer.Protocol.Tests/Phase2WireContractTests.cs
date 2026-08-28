using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using VFXComposer.Protocol.Diagnostics;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;
using VFXComposer.Protocol.Json;
using VFXComposer.Protocol.Projects;
using VFXComposer.Protocol.Queries;
using VFXComposer.Protocol.Registration;

namespace VFXComposer.Protocol.Tests;

[TestClass]
public sealed class Phase2WireContractTests
{
    private static readonly TypedHash ProjectIdentity = TypedHash.ComputeUtf8(
        ProjectRegistrationAttestation.ProjectIdentityType,
        "project-fixture");

    [TestMethod]
    public void PeerHelloAndSessionAreExactClaimsWithFrozenSortedCapabilities()
    {
        var hello = new PeerHello(
            "request-01",
            PeerRoles.Worker,
            "worker-01",
            4242,
            "epoch-01",
            [PeerCapabilityIds.PeerSessionV1, PeerCapabilityIds.ReadOnlyQueryV1],
            TypedHash.ComputeUtf8(PeerHello.ProcessImageIdentityType, "worker-image"));
        var session = new PeerSessionAccepted(
            "request-01",
            "session-01",
            PeerRoles.Worker,
            "broker-01",
            7,
            "epoch-01",
            [PeerCapabilityIds.PeerSessionV1, PeerCapabilityIds.ReadOnlyQueryV1]);

        Assert.IsNotNull(StrictWireCodec.Decode<PeerHello>(JsonSerializer.SerializeToUtf8Bytes(hello)));
        Assert.IsNotNull(StrictWireCodec.Decode<PeerSessionAccepted>(JsonSerializer.SerializeToUtf8Bytes(session)));

        Assert.ThrowsExactly<ArgumentException>(() => new PeerHello(
            "request-01",
            PeerRoles.Worker,
            "worker-01",
            4242,
            "epoch-01",
            [PeerCapabilityIds.ReadOnlyQueryV1, PeerCapabilityIds.PeerSessionV1],
            TypedHash.ComputeUtf8(PeerHello.ProcessImageIdentityType, "worker-image")));
        Assert.ThrowsExactly<ArgumentException>(() => new PeerHello(
            "request-01",
            PeerRoles.Worker,
            "worker-01",
            4242,
            "epoch-01",
            ["project.path.submit.v1"],
            TypedHash.ComputeUtf8(PeerHello.ProcessImageIdentityType, "worker-image")));
    }

    [TestMethod]
    public void RegistrationAndLeaseRequireValidPhysicalSelfHashes()
    {
        var attestation = CreateSelfHashedAttestation();
        var lease = CreateSelfHashedLease();
        Assert.IsNotNull(StrictWireCodec.Decode<ProjectRegistrationAttestation>(attestation));
        Assert.IsNotNull(StrictWireCodec.Decode<ProjectLeaseDescriptor>(lease));

        var tampered = Encoding.UTF8.GetString(attestation).Replace(
            "worker-session-01",
            "worker-session-02",
            StringComparison.Ordinal);
        var exception = Assert.ThrowsExactly<WireDecodeException>(() =>
            StrictWireCodec.Decode<ProjectRegistrationAttestation>(Encoding.UTF8.GetBytes(tampered)));
        Assert.AreEqual(StableDiagnosticCodes.MalformedMessage, exception.Diagnostic.Code);
    }

    [TestMethod]
    public void WorkerHandleGrantIsSelfHashedWorkerOnlyAndUsesCanonicalOpaqueHandleText()
    {
        var grantBytes = CreateSelfHashedWorkerGrant();
        var grant = StrictWireCodec.Decode<WorkerProjectHandleGrant>(grantBytes);
        Assert.AreEqual(WorkerProjectHandleGrant.HandleEncodingName, grant.HandleEncoding);
        Assert.AreEqual("0000000000000100", grant.VolumeHandle);

        var tampered = Encoding.UTF8.GetString(grantBytes).Replace(
            "0000000000000100",
            "0000000000000200",
            StringComparison.Ordinal);
        Assert.ThrowsExactly<WireDecodeException>(() =>
            StrictWireCodec.Decode<WorkerProjectHandleGrant>(Encoding.UTF8.GetBytes(tampered)));
        Assert.ThrowsExactly<ArgumentException>(() => new WorkerProjectHandleGrant(
            ProtocolVersions.Current,
            MessageKinds.WorkerProjectHandleGrant,
            "request-02",
            "lease-01",
            "project-01",
            ProjectIdentity,
            TypedHash.ComputeUtf8(ProjectRegistrationAttestation.VolumeIdentityType, "volume"),
            TypedHash.ComputeUtf8(ProjectRegistrationAttestation.DirectoryIdentityType, "repository"),
            TypedHash.ComputeUtf8(ProjectRegistrationAttestation.DirectoryIdentityType, "project-root"),
            1,
            1,
            1,
            "worker-session-01",
            "worker-epoch-01",
            WorkerProjectHandleGrant.HandleEncodingName,
            "0000000000000000",
            "0000000000000104",
            "0000000000000108",
            TypedHash.ComputeUtf8(WorkerProjectHandleGrant.SelfHashType, "sample")));
    }

    [TestMethod]
    public void WorkerHandleLifecycleMessagesBindExactGrantAndRevokeSelfHashes()
    {
        var grant = StrictWireCodec.Decode<WorkerProjectHandleGrant>(CreateSelfHashedWorkerGrant());
        var grantAck = CreateGrantAcknowledgement(grant.SelfHash);
        var revoke = CreateRevoke(grant.SelfHash);
        var revokeAck = CreateRevokeAcknowledgement(grant.SelfHash, revoke.SelfHash);

        Assert.AreEqual(
            WorkerProjectHandleGrantAcknowledgement.AcceptedDisposition,
            StrictWireCodec.Decode<WorkerProjectHandleGrantAcknowledgement>(
                JsonSerializer.SerializeToUtf8Bytes(grantAck)).Disposition);
        Assert.AreEqual(
            WorkerProjectHandleRevoke.LeaseRevokedReason,
            StrictWireCodec.Decode<WorkerProjectHandleRevoke>(
                JsonSerializer.SerializeToUtf8Bytes(revoke)).ReasonCode);
        Assert.AreEqual(
            WorkerProjectHandleRevokeAcknowledgement.ClosedDisposition,
            StrictWireCodec.Decode<WorkerProjectHandleRevokeAcknowledgement>(
                JsonSerializer.SerializeToUtf8Bytes(revokeAck)).Disposition);

        var tampered = Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(revokeAck))
            .Replace(grant.SelfHash.Digest, TypedHash.ComputeUtf8(
                WorkerProjectHandleGrant.SelfHashType,
                "other-grant").Digest, StringComparison.Ordinal);
        Assert.ThrowsExactly<WireDecodeException>(() =>
            StrictWireCodec.Decode<WorkerProjectHandleRevokeAcknowledgement>(
                Encoding.UTF8.GetBytes(tampered)));
    }

    [TestMethod]
    public void WorkerProjectLocatorAcknowledgementBindsAllSharedLocatorCorrelationsWithoutHandleGrantSemantics()
    {
        var locator = StrictWireCodec.Decode<WorkerProjectLocator>(
            WorkerProjectLocatorTests.CreateSelfHashedLocatorBytes());
        var acknowledgementBytes = WorkerProjectLocatorTests.CreateSelfHashedAcknowledgementBytes(locator);
        var acknowledgement = StrictWireCodec.Decode<WorkerProjectLocatorAcknowledgement>(acknowledgementBytes);

        Assert.IsTrue(locator.SelfHash.FixedTimeEquals(acknowledgement.LocatorSelfHash));
        Assert.AreEqual(locator.RequestId, acknowledgement.RequestId);
        Assert.AreEqual(locator.RegisteredProjectId, acknowledgement.RegisteredProjectId);
        Assert.AreEqual(locator.BrokerGeneration, acknowledgement.BrokerGeneration);
        Assert.AreEqual(locator.RegistrationGeneration, acknowledgement.RegistrationGeneration);
        Assert.AreEqual(locator.EnrollmentGeneration, acknowledgement.EnrollmentGeneration);
        Assert.AreEqual(locator.WorkerSessionId, acknowledgement.WorkerSessionId);
        Assert.AreEqual(locator.WorkerProcessEpoch, acknowledgement.WorkerProcessEpoch);
        Assert.AreEqual(WorkerProjectLocatorAcknowledgement.AcceptedDisposition, acknowledgement.Disposition);
        Assert.AreNotEqual(
            WorkerProjectHandleGrantAcknowledgement.AcceptedDisposition,
            acknowledgement.Disposition);
        Assert.IsTrue(PeerCapabilityIds.All.Contains(PeerCapabilityIds.WorkerProjectLocatorV1));

        foreach (var propertyName in new[]
                 {
                     "registeredProjectId",
                     "brokerGeneration",
                     "registrationGeneration",
                     "enrollmentGeneration",
                     "workerSessionId",
                     "workerProcessEpoch",
                 })
        {
            var drifted = JsonNode.Parse(acknowledgementBytes)!.AsObject();
            drifted[propertyName] = propertyName.EndsWith("Generation", StringComparison.Ordinal)
                ? JsonValue.Create(31)
                : JsonValue.Create("different-" + propertyName);
            Assert.ThrowsExactly<WireDecodeException>(() =>
                StrictWireCodec.Decode<WorkerProjectLocatorAcknowledgement>(
                    Encoding.UTF8.GetBytes(drifted.ToJsonString())), propertyName);
        }
    }

    [TestMethod]
    public void ReadQueryContainsRegistryIdentityOnlyAndRejectsCallerPath()
    {
        var query = new ReadDocumentQuery(
            ProtocolVersions.Current,
            MessageKinds.ReadDocumentQuery,
            "request-03",
            "lease-01",
            ProjectIdentity,
            3,
            DocumentKinds.Contract,
            "effect-fire-contract",
            null);
        var json = Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(query));
        Assert.IsNotNull(StrictWireCodec.Decode<ReadDocumentQuery>(Encoding.UTF8.GetBytes(json)));
        Assert.IsFalse(json.Contains("path", StringComparison.OrdinalIgnoreCase));

        var withCallerPath = json[..^1] + ",\"callerPath\":\"C:/untrusted\"}";
        var exception = Assert.ThrowsExactly<WireDecodeException>(() =>
            StrictWireCodec.Decode<ReadDocumentQuery>(Encoding.UTF8.GetBytes(withCallerPath)));
        Assert.AreEqual(StableDiagnosticCodes.MalformedMessage, exception.Diagnostic.Code);
    }

    [TestMethod]
    public void ReadDocumentIdentityIsKindBoundAndCannotBecomeAPath()
    {
        Assert.IsNotNull(new ReadDocumentQuery(
            ProtocolVersions.Current,
            MessageKinds.ReadDocumentQuery,
            "request-library",
            "lease-01",
            ProjectIdentity,
            1,
            DocumentKinds.LibraryIndex,
            "project",
            null));

        foreach (var value in new[]
                 {
                     "other",
                     "../project",
                     "Project",
                     "project.json"
                 })
        {
            Assert.ThrowsExactly<ArgumentException>(() => new ReadDocumentQuery(
                ProtocolVersions.Current,
                MessageKinds.ReadDocumentQuery,
                "request-library",
                "lease-01",
                ProjectIdentity,
                1,
                DocumentKinds.LibraryIndex,
                value,
                null));
        }

        foreach (var value in new[]
                 {
                     "../effect",
                     "Effect",
                     "effect.json",
                     "effect/sub"
                 })
        {
            Assert.ThrowsExactly<ArgumentException>(() => new ReadDocumentQuery(
                ProtocolVersions.Current,
                MessageKinds.ReadDocumentQuery,
                "request-document",
                "lease-01",
                ProjectIdentity,
                1,
                DocumentKinds.Manifest,
                value,
                null));
        }
    }

    [TestMethod]
    public void ReadResultBindsCanonicalBase64LengthAndTypedContentHash()
    {
        var content = Encoding.UTF8.GetBytes("{\"manifestVersion\":\"fixture\"}");
        var result = new ReadDocumentResult(
            ProtocolVersions.Current,
            MessageKinds.ReadDocumentResult,
            "request-04",
            true,
            ProjectIdentity,
            DocumentKinds.Manifest,
            "effect-fire-manifest",
            TypedHash.Compute(ReadDocumentResult.ContentHashType, content),
            content.Length,
            Convert.ToBase64String(content),
            null);
        var roundTrip = StrictWireCodec.Decode<ReadDocumentResult>(
            JsonSerializer.SerializeToUtf8Bytes(result));
        Assert.IsTrue(roundTrip.Accepted);
        Assert.AreEqual(content.Length, roundTrip.ByteLength);

        Assert.ThrowsExactly<ArgumentException>(() => new ReadDocumentResult(
            ProtocolVersions.Current,
            MessageKinds.ReadDocumentResult,
            "request-04",
            true,
            ProjectIdentity,
            DocumentKinds.Manifest,
            "effect-fire-manifest",
            TypedHash.ComputeUtf8(ReadDocumentResult.ContentHashType, "different"),
            content.Length,
            Convert.ToBase64String(content),
            null));
    }

    [TestMethod]
    public void RejectedReadContainsNoContentAndOnlyStableDiagnostic()
    {
        var result = new ReadDocumentResult(
            ProtocolVersions.Current,
            MessageKinds.ReadDocumentResult,
            "request-05",
            false,
            ProjectIdentity,
            DocumentKinds.Trace,
            "effect-fire-trace",
            null,
            0,
            null,
            StableDiagnosticCatalog.Create(StableDiagnosticCodes.Disconnected));
        var decoded = StrictWireCodec.Decode<ReadDocumentResult>(
            JsonSerializer.SerializeToUtf8Bytes(result));
        Assert.IsFalse(decoded.Accepted);
        Assert.IsNull(decoded.ContentHash);
        Assert.IsNull(decoded.ContentBase64);
        Assert.IsNotNull(decoded.Diagnostic);
    }

    [TestMethod]
    public void ProjectReadDiagnosticsAreClosedPathFreeCatalogEntries()
    {
        var expected = new[]
        {
            StableDiagnosticCodes.ProjectLeaseRejected,
            StableDiagnosticCodes.ProjectDocumentUnavailable,
            StableDiagnosticCodes.ProjectDocumentContentMismatch,
        };
        foreach (var code in expected)
        {
            var diagnostic = StableDiagnosticCatalog.Create(code);
            Assert.AreEqual(DiagnosticSeverities.Error, diagnostic.Severity);
            Assert.IsTrue(diagnostic.Retryable);
            Assert.IsFalse(diagnostic.Message.Contains("/", StringComparison.Ordinal));
            Assert.IsFalse(diagnostic.Message.Contains("\\", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void RegisteredProjectSelectionIsBoundedCorrelationOnlyAndAdvertisesItsVocabulary()
    {
        var selection = new RegisteredProjectSelection(
            ProtocolVersions.Current,
            MessageKinds.RegisteredProjectSelection,
            "request-selection-01",
            "registered-project-01",
            TypedHash.ComputeUtf8(RegisteredProjectSelection.ProjectIdentityType, "project-selection"),
            7,
            11);
        var wire = JsonSerializer.SerializeToUtf8Bytes(selection);
        var decoded = StrictWireCodec.Decode<RegisteredProjectSelection>(wire);

        Assert.AreEqual(selection.RequestId, decoded.RequestId);
        Assert.AreEqual(selection.RegisteredProjectId, decoded.RegisteredProjectId);
        Assert.IsTrue(selection.ProjectIdentity.FixedTimeEquals(decoded.ProjectIdentity));
        Assert.AreEqual(7L, decoded.BrokerGeneration);
        Assert.AreEqual(11L, decoded.RegistrationGeneration);
        Assert.IsTrue(PeerCapabilityIds.All.Contains(PeerCapabilityIds.ProjectSelectionV1));

        var propertyNames = typeof(RegisteredProjectSelection)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();
        var prohibited = new[]
        {
            "Path", "Uri", "Label", "Volume", "Root", "Directory", "Handle", "Endpoint",
            "Grant", "Accepted", "Authorized", "Status", "Verdict", "Permission", "Command",
            "Authority", "Lease", "SelfHash",
        };
        Assert.AreEqual(7, propertyNames.Length);
        Assert.IsFalse(propertyNames.Any(name => prohibited.Any(value =>
            name.Contains(value, StringComparison.OrdinalIgnoreCase))));
    }

    [TestMethod]
    public void Phase2DtosExposeNoPathOrNativeHandleMembers()
    {
        var prohibited = new[] { "Path", "FileName", "Directory", "Handle", "Endpoint", "Socket" };
        var types = new[]
        {
            typeof(PeerHello),
            typeof(PeerSessionAccepted),
            typeof(ProjectRegistrationAttestation),
            typeof(ProjectLeaseDescriptor),
            typeof(WorkerProjectLocator),
            typeof(WorkerProjectLocatorAcknowledgement),
            typeof(ReadDocumentQuery),
            typeof(ReadDocumentResult),
        };

        foreach (var type in types)
        {
            foreach (var property in type.GetProperties())
            {
                Assert.IsFalse(
                    prohibited.Any(value => property.Name.Contains(value, StringComparison.OrdinalIgnoreCase)),
                    $"{type.FullName}.{property.Name}");
            }
        }
    }

    private static byte[] CreateSelfHashedAttestation()
    {
        var root = new JsonObject
        {
            ["protocolVersion"] = ProtocolVersions.Current,
            ["messageKind"] = MessageKinds.ProjectRegistrationAttestation,
            ["requestId"] = "request-01",
            ["registeredProjectId"] = "project-01",
            ["projectIdentity"] = JsonSerializer.SerializeToNode(ProjectIdentity),
            ["volumeIdentity"] = JsonSerializer.SerializeToNode(TypedHash.ComputeUtf8(ProjectRegistrationAttestation.VolumeIdentityType, "volume")),
            ["repositoryIdentity"] = JsonSerializer.SerializeToNode(TypedHash.ComputeUtf8(ProjectRegistrationAttestation.DirectoryIdentityType, "repository")),
            ["projectRootIdentity"] = JsonSerializer.SerializeToNode(TypedHash.ComputeUtf8(ProjectRegistrationAttestation.DirectoryIdentityType, "project-root")),
            ["brokerGeneration"] = 1,
            ["registrationGeneration"] = 1,
            ["workerSessionId"] = "worker-session-01",
            ["workerProcessEpoch"] = "worker-epoch-01",
            ["selfHash"] = JsonSerializer.SerializeToNode(TypedHash.ComputeUtf8(ProjectRegistrationAttestation.SelfHashType, "placeholder")),
        };
        return Seal(root, ProjectRegistrationAttestation.SelfHashType);
    }

    private static byte[] CreateSelfHashedLease()
    {
        var root = new JsonObject
        {
            ["protocolVersion"] = ProtocolVersions.Current,
            ["messageKind"] = MessageKinds.ProjectLeaseDescriptor,
            ["requestId"] = "request-02",
            ["leaseId"] = "lease-01",
            ["registeredProjectId"] = "project-01",
            ["projectIdentity"] = JsonSerializer.SerializeToNode(ProjectIdentity),
            ["brokerGeneration"] = 1,
            ["registrationGeneration"] = 1,
            ["workerSessionId"] = "worker-session-01",
            ["workerProcessEpoch"] = "worker-epoch-01",
            ["leaseGeneration"] = 1,
            ["selfHash"] = JsonSerializer.SerializeToNode(TypedHash.ComputeUtf8(ProjectLeaseDescriptor.SelfHashType, "placeholder")),
        };
        return Seal(root, ProjectLeaseDescriptor.SelfHashType);
    }

    private static byte[] CreateSelfHashedWorkerGrant()
    {
        var root = new JsonObject
        {
            ["protocolVersion"] = ProtocolVersions.Current,
            ["messageKind"] = MessageKinds.WorkerProjectHandleGrant,
            ["requestId"] = "request-02-worker",
            ["leaseId"] = "lease-01",
            ["registeredProjectId"] = "project-01",
            ["projectIdentity"] = JsonSerializer.SerializeToNode(ProjectIdentity),
            ["volumeIdentity"] = JsonSerializer.SerializeToNode(TypedHash.ComputeUtf8(ProjectRegistrationAttestation.VolumeIdentityType, "volume")),
            ["repositoryIdentity"] = JsonSerializer.SerializeToNode(TypedHash.ComputeUtf8(ProjectRegistrationAttestation.DirectoryIdentityType, "repository")),
            ["projectRootIdentity"] = JsonSerializer.SerializeToNode(TypedHash.ComputeUtf8(ProjectRegistrationAttestation.DirectoryIdentityType, "project-root")),
            ["brokerGeneration"] = 1,
            ["registrationGeneration"] = 1,
            ["leaseGeneration"] = 1,
            ["workerSessionId"] = "worker-session-01",
            ["workerProcessEpoch"] = "worker-epoch-01",
            ["handleEncoding"] = WorkerProjectHandleGrant.HandleEncodingName,
            ["volumeHandle"] = "0000000000000100",
            ["repositoryHandle"] = "0000000000000104",
            ["projectRootHandle"] = "0000000000000108",
            ["selfHash"] = JsonSerializer.SerializeToNode(TypedHash.ComputeUtf8(WorkerProjectHandleGrant.SelfHashType, "placeholder")),
        };
        return Seal(root, WorkerProjectHandleGrant.SelfHashType);
    }

    private static WorkerProjectHandleGrantAcknowledgement CreateGrantAcknowledgement(TypedHash grantSelfHash)
    {
        var root = new JsonObject
        {
            ["protocolVersion"] = ProtocolVersions.Current,
            ["messageKind"] = MessageKinds.WorkerProjectHandleGrantAcknowledgement,
            ["requestId"] = "request-02-worker-ack",
            ["leaseId"] = "lease-01",
            ["brokerGeneration"] = 1,
            ["leaseGeneration"] = 1,
            ["workerSessionId"] = "worker-session-01",
            ["workerProcessEpoch"] = "worker-epoch-01",
            ["grantSelfHash"] = JsonSerializer.SerializeToNode(grantSelfHash),
            ["disposition"] = WorkerProjectHandleGrantAcknowledgement.AcceptedDisposition,
            ["selfHash"] = JsonSerializer.SerializeToNode(TypedHash.ComputeUtf8(
                WorkerProjectHandleGrantAcknowledgement.SelfHashType,
                "placeholder")),
        };
        return StrictWireCodec.Decode<WorkerProjectHandleGrantAcknowledgement>(
            Seal(root, WorkerProjectHandleGrantAcknowledgement.SelfHashType));
    }

    private static WorkerProjectHandleRevoke CreateRevoke(TypedHash grantSelfHash)
    {
        var root = new JsonObject
        {
            ["protocolVersion"] = ProtocolVersions.Current,
            ["messageKind"] = MessageKinds.WorkerProjectHandleRevoke,
            ["requestId"] = "request-02-worker-revoke",
            ["leaseId"] = "lease-01",
            ["brokerGeneration"] = 1,
            ["leaseGeneration"] = 1,
            ["workerSessionId"] = "worker-session-01",
            ["workerProcessEpoch"] = "worker-epoch-01",
            ["grantSelfHash"] = JsonSerializer.SerializeToNode(grantSelfHash),
            ["reasonCode"] = WorkerProjectHandleRevoke.LeaseRevokedReason,
            ["selfHash"] = JsonSerializer.SerializeToNode(TypedHash.ComputeUtf8(
                WorkerProjectHandleRevoke.SelfHashType,
                "placeholder")),
        };
        return StrictWireCodec.Decode<WorkerProjectHandleRevoke>(
            Seal(root, WorkerProjectHandleRevoke.SelfHashType));
    }

    private static WorkerProjectHandleRevokeAcknowledgement CreateRevokeAcknowledgement(
        TypedHash grantSelfHash,
        TypedHash revokeSelfHash)
    {
        var root = new JsonObject
        {
            ["protocolVersion"] = ProtocolVersions.Current,
            ["messageKind"] = MessageKinds.WorkerProjectHandleRevokeAcknowledgement,
            ["requestId"] = "request-02-worker-revoke-ack",
            ["leaseId"] = "lease-01",
            ["brokerGeneration"] = 1,
            ["leaseGeneration"] = 1,
            ["workerSessionId"] = "worker-session-01",
            ["workerProcessEpoch"] = "worker-epoch-01",
            ["grantSelfHash"] = JsonSerializer.SerializeToNode(grantSelfHash),
            ["revokeSelfHash"] = JsonSerializer.SerializeToNode(revokeSelfHash),
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
        var provisional = JsonSerializer.SerializeToUtf8Bytes(root);
        var hash = SelfHash.Compute(provisional, typeTag);
        root["selfHash"] = JsonSerializer.SerializeToNode(hash);
        return JsonSerializer.SerializeToUtf8Bytes(root);
    }
}
