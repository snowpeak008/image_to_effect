using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Json;
using VFXComposer.Protocol.Registration;

namespace VFXComposer.Protocol.Tests;

[TestClass]
public sealed class WorkerProjectLocatorTests
{
    private static readonly TypedHash ProjectIdentity = TypedHash.ComputeUtf8(
        ProjectRegistrationAttestation.ProjectIdentityType,
        "locator-project");
    private static readonly TypedHash VolumeIdentity = TypedHash.ComputeUtf8(
        ProjectRegistrationAttestation.VolumeIdentityType,
        "locator-volume");
    private static readonly TypedHash RepositoryIdentity = TypedHash.ComputeUtf8(
        ProjectRegistrationAttestation.DirectoryIdentityType,
        "locator-repository");
    private static readonly TypedHash ProjectRootIdentity = TypedHash.ComputeUtf8(
        ProjectRegistrationAttestation.DirectoryIdentityType,
        "locator-project-root");

    [TestMethod]
    public void ConstructorRequiresExactVocabularyOpaqueTokensTypedDomainsAndPositiveGenerations()
    {
        var locator = CreateLocator();
        var acknowledgement = CreateAcknowledgement(locator.SelfHash);

        Assert.AreEqual(ProtocolVersions.Current, locator.ProtocolVersion);
        Assert.AreEqual(MessageKinds.WorkerProjectLocator, locator.MessageKind);
        Assert.AreEqual(MessageKinds.WorkerProjectLocatorAcknowledgement, acknowledgement.MessageKind);
        Assert.AreEqual(WorkerProjectLocatorAcknowledgement.AcceptedDisposition, acknowledgement.Disposition);
        Assert.IsTrue(locator.SelfHash.FixedTimeEquals(acknowledgement.LocatorSelfHash));

        Assert.ThrowsExactly<ArgumentException>(() => CreateLocator(protocolVersion: "future.locator.version"));
        Assert.ThrowsExactly<ArgumentException>(() => CreateLocator(messageKind: "worker.project.handle.grant"));
        Assert.ThrowsExactly<ArgumentException>(() => CreateLocator(requestId: "C:/untrusted"));
        Assert.ThrowsExactly<ArgumentException>(() => CreateLocator(registeredProjectId: "https://untrusted.example/project"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CreateLocator(brokerGeneration: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CreateLocator(registrationGeneration: -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CreateLocator(enrollmentGeneration: 0));
        Assert.ThrowsExactly<ArgumentException>(() => CreateLocator(projectIdentity: TypedHash.ComputeUtf8(
            ProjectRegistrationAttestation.VolumeIdentityType,
            "wrong-domain")));
        Assert.ThrowsExactly<ArgumentException>(() => CreateLocator(volumeIdentity: TypedHash.ComputeUtf8(
            ProjectRegistrationAttestation.DirectoryIdentityType,
            "wrong-domain")));
        Assert.ThrowsExactly<ArgumentException>(() => CreateLocator(repositoryIdentity: TypedHash.ComputeUtf8(
            ProjectRegistrationAttestation.ProjectIdentityType,
            "wrong-domain")));
        Assert.ThrowsExactly<ArgumentException>(() => CreateLocator(projectRootIdentity: TypedHash.ComputeUtf8(
            ProjectRegistrationAttestation.VolumeIdentityType,
            "wrong-domain")));
        Assert.ThrowsExactly<ArgumentException>(() => CreateAcknowledgement(
            locator.SelfHash,
            disposition: "GRANT_ACCEPTED"));
        Assert.ThrowsExactly<ArgumentException>(() => CreateAcknowledgement(
            TypedHash.ComputeUtf8(WorkerProjectHandleGrant.SelfHashType, "wrong-grant-hash")));
    }

    [TestMethod]
    public void DtosExposeOnlyTheExactLocatorAndAcknowledgementCorrelationSurfaces()
    {
        AssertProperties(
            typeof(WorkerProjectLocator),
            new[]
            {
                "BrokerGeneration",
                "EnrollmentGeneration",
                "MessageKind",
                "ProjectIdentity",
                "ProjectRootIdentity",
                "ProtocolVersion",
                "RegisteredProjectId",
                "RegistrationGeneration",
                "RepositoryIdentity",
                "RequestId",
                "SelfHash",
                "VolumeIdentity",
                "WorkerProcessEpoch",
                "WorkerSessionId",
            });
        AssertProperties(
            typeof(WorkerProjectLocatorAcknowledgement),
            new[]
            {
                "BrokerGeneration",
                "Disposition",
                "EnrollmentGeneration",
                "LocatorSelfHash",
                "MessageKind",
                "ProtocolVersion",
                "RegisteredProjectId",
                "RegistrationGeneration",
                "RequestId",
                "SelfHash",
                "WorkerProcessEpoch",
                "WorkerSessionId",
            });
    }

    [TestMethod]
    public void CanonicalSelfHashesExcludeOnlyTheirOwnFieldsAndBindTheExactAcknowledgementCorrelation()
    {
        var locatorBytes = CreateSelfHashedLocatorBytes();
        var locator = StrictWireCodec.Decode<WorkerProjectLocator>(locatorBytes);
        var acknowledgementBytes = CreateSelfHashedAcknowledgementBytes(locator);
        var acknowledgement = StrictWireCodec.Decode<WorkerProjectLocatorAcknowledgement>(acknowledgementBytes);

        CollectionAssert.AreEqual(locatorBytes, CanonicalJson.Canonicalize(locatorBytes));
        CollectionAssert.AreEqual(acknowledgementBytes, CanonicalJson.Canonicalize(acknowledgementBytes));
        Assert.IsTrue(SelfHash.Verify(locatorBytes, WorkerProjectLocator.SelfHashType));
        Assert.IsTrue(SelfHash.Verify(acknowledgementBytes, WorkerProjectLocatorAcknowledgement.SelfHashType));
        Assert.IsTrue(locator.SelfHash.FixedTimeEquals(acknowledgement.LocatorSelfHash));
        Assert.AreEqual(locator.RequestId, acknowledgement.RequestId);
        Assert.AreEqual(locator.RegisteredProjectId, acknowledgement.RegisteredProjectId);
        Assert.AreEqual(locator.BrokerGeneration, acknowledgement.BrokerGeneration);
        Assert.AreEqual(locator.RegistrationGeneration, acknowledgement.RegistrationGeneration);
        Assert.AreEqual(locator.EnrollmentGeneration, acknowledgement.EnrollmentGeneration);
        Assert.AreEqual(locator.WorkerSessionId, acknowledgement.WorkerSessionId);
        Assert.AreEqual(locator.WorkerProcessEpoch, acknowledgement.WorkerProcessEpoch);

        var alteredLocator = JsonNode.Parse(locatorBytes)!.AsObject();
        alteredLocator["selfHash"] = JsonSerializer.SerializeToNode(TypedHash.ComputeUtf8(
            WorkerProjectLocator.SelfHashType,
            "different-claimed-hash"));
        var alteredLocatorBytes = CanonicalJson.Canonicalize(JsonSerializer.SerializeToUtf8Bytes(alteredLocator));
        Assert.IsTrue(SelfHash.Compute(locatorBytes, WorkerProjectLocator.SelfHashType).FixedTimeEquals(
            SelfHash.Compute(alteredLocatorBytes, WorkerProjectLocator.SelfHashType)));
        Assert.ThrowsExactly<WireDecodeException>(() =>
            StrictWireCodec.Decode<WorkerProjectLocator>(alteredLocatorBytes));
    }

    internal static byte[] CreateSelfHashedLocatorBytes(
        string requestId = "locator-request-01",
        string registeredProjectId = "registered-project-01",
        long brokerGeneration = 17,
        long registrationGeneration = 23,
        long enrollmentGeneration = 29,
        string workerSessionId = "worker-session-01",
        string workerProcessEpoch = "worker-epoch-01")
    {
        var root = new JsonObject
        {
            ["protocolVersion"] = ProtocolVersions.Current,
            ["messageKind"] = MessageKinds.WorkerProjectLocator,
            ["requestId"] = requestId,
            ["registeredProjectId"] = registeredProjectId,
            ["projectIdentity"] = JsonSerializer.SerializeToNode(ProjectIdentity),
            ["volumeIdentity"] = JsonSerializer.SerializeToNode(VolumeIdentity),
            ["repositoryIdentity"] = JsonSerializer.SerializeToNode(RepositoryIdentity),
            ["projectRootIdentity"] = JsonSerializer.SerializeToNode(ProjectRootIdentity),
            ["brokerGeneration"] = brokerGeneration,
            ["registrationGeneration"] = registrationGeneration,
            ["enrollmentGeneration"] = enrollmentGeneration,
            ["workerSessionId"] = workerSessionId,
            ["workerProcessEpoch"] = workerProcessEpoch,
            ["selfHash"] = JsonSerializer.SerializeToNode(TypedHash.ComputeUtf8(
                WorkerProjectLocator.SelfHashType,
                "placeholder")),
        };
        return Seal(root, WorkerProjectLocator.SelfHashType);
    }

    internal static byte[] CreateSelfHashedAcknowledgementBytes(WorkerProjectLocator locator)
    {
        var root = new JsonObject
        {
            ["protocolVersion"] = ProtocolVersions.Current,
            ["messageKind"] = MessageKinds.WorkerProjectLocatorAcknowledgement,
            ["requestId"] = locator.RequestId,
            ["registeredProjectId"] = locator.RegisteredProjectId,
            ["brokerGeneration"] = locator.BrokerGeneration,
            ["registrationGeneration"] = locator.RegistrationGeneration,
            ["enrollmentGeneration"] = locator.EnrollmentGeneration,
            ["workerSessionId"] = locator.WorkerSessionId,
            ["workerProcessEpoch"] = locator.WorkerProcessEpoch,
            ["locatorSelfHash"] = JsonSerializer.SerializeToNode(locator.SelfHash),
            ["disposition"] = WorkerProjectLocatorAcknowledgement.AcceptedDisposition,
            ["selfHash"] = JsonSerializer.SerializeToNode(TypedHash.ComputeUtf8(
                WorkerProjectLocatorAcknowledgement.SelfHashType,
                "placeholder")),
        };
        return Seal(root, WorkerProjectLocatorAcknowledgement.SelfHashType);
    }

    private static WorkerProjectLocator CreateLocator(
        string protocolVersion = ProtocolVersions.Current,
        string messageKind = MessageKinds.WorkerProjectLocator,
        string requestId = "locator-request-01",
        string registeredProjectId = "registered-project-01",
        TypedHash? projectIdentity = null,
        TypedHash? volumeIdentity = null,
        TypedHash? repositoryIdentity = null,
        TypedHash? projectRootIdentity = null,
        long brokerGeneration = 17,
        long registrationGeneration = 23,
        long enrollmentGeneration = 29,
        string workerSessionId = "worker-session-01",
        string workerProcessEpoch = "worker-epoch-01") =>
        new(
            protocolVersion,
            messageKind,
            requestId,
            registeredProjectId,
            projectIdentity ?? ProjectIdentity,
            volumeIdentity ?? VolumeIdentity,
            repositoryIdentity ?? RepositoryIdentity,
            projectRootIdentity ?? ProjectRootIdentity,
            brokerGeneration,
            registrationGeneration,
            enrollmentGeneration,
            workerSessionId,
            workerProcessEpoch,
            TypedHash.ComputeUtf8(WorkerProjectLocator.SelfHashType, "constructor-sample"));

    private static WorkerProjectLocatorAcknowledgement CreateAcknowledgement(
        TypedHash locatorSelfHash,
        string disposition = WorkerProjectLocatorAcknowledgement.AcceptedDisposition) =>
        new(
            ProtocolVersions.Current,
            MessageKinds.WorkerProjectLocatorAcknowledgement,
            "locator-request-01",
            "registered-project-01",
            17,
            23,
            29,
            "worker-session-01",
            "worker-epoch-01",
            locatorSelfHash,
            disposition,
            TypedHash.ComputeUtf8(WorkerProjectLocatorAcknowledgement.SelfHashType, "constructor-sample"));

    private static void AssertProperties(Type type, string[] expected)
    {
        var properties = type.GetProperties()
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(expected, properties.Select(property => property.Name).ToArray());

        var prohibited = new[]
        {
            "Path", "Uri", "Drive", "Guid", "Raw", "Handle", "Lease", "Grant", "Permission",
            "Status", "Accepted", "Issuer", "Command", "Authority",
        };
        foreach (var property in properties)
        {
            Assert.IsFalse(property.PropertyType == typeof(bool), property.Name);
            Assert.IsFalse(prohibited.Any(value => property.Name.Contains(value, StringComparison.OrdinalIgnoreCase)),
                property.Name);
        }
    }

    private static byte[] Seal(JsonObject root, string typeTag)
    {
        var provisional = JsonSerializer.SerializeToUtf8Bytes(root);
        root["selfHash"] = JsonSerializer.SerializeToNode(SelfHash.Compute(provisional, typeTag));
        return CanonicalJson.Canonicalize(JsonSerializer.SerializeToUtf8Bytes(root));
    }
}
