using System.Text;
using System.Text.Json;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Json;
using VFXComposer.Protocol.Projects;

namespace VFXComposer.Protocol.Tests;

[TestClass]
public sealed class RegisteredProjectSelectionTests
{
    private const string GoldenCanonicalJson =
        "{\"brokerGeneration\":17,\"messageKind\":\"project.registered.selection\",\"projectIdentity\":{\"digest\":\"sha256:db939c7038b17318cbbcee5a374d8793648fc1b0050578438d3ac211e599d71a\",\"typeTag\":\"vfxcomposer.project-identity/1\"},\"protocolVersion\":\"vfxcomposer.protocol/1.0\",\"registeredProjectId\":\"registered-project-01\",\"registrationGeneration\":23,\"requestId\":\"request-01\"}";

    private static readonly TypedHash GoldenProjectIdentity = TypedHash.ComputeUtf8(
        RegisteredProjectSelection.ProjectIdentityType,
        "selected-project");

    [TestMethod]
    public void ConstructorRequiresExactVocabularyBoundedOpaqueTokensAndPositiveGenerations()
    {
        var selection = Create();
        Assert.AreEqual(ProtocolVersions.Current, selection.ProtocolVersion);
        Assert.AreEqual(MessageKinds.RegisteredProjectSelection, selection.MessageKind);
        Assert.IsTrue(GoldenProjectIdentity.FixedTimeEquals(selection.ProjectIdentity));

        Assert.ThrowsExactly<ArgumentException>(() => Create(protocolVersion: "future.selection.version"));
        Assert.ThrowsExactly<ArgumentException>(() => Create(messageKind: "authority.grant.selection"));
        Assert.ThrowsExactly<ArgumentException>(() => Create(requestId: "C:/untrusted"));
        Assert.ThrowsExactly<ArgumentException>(() => Create(registeredProjectId: "https://untrusted.example/project"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Create(requestId: new string('a', 129)));
        Assert.ThrowsExactly<ArgumentException>(() => Create(projectIdentity: TypedHash.ComputeUtf8(
            "vfxcomposer.volume-identity/1",
            "wrong-domain")));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Create(brokerGeneration: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Create(brokerGeneration: -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Create(registrationGeneration: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Create(registrationGeneration: -1));
    }

    [TestMethod]
    public void CanonicalGoldenBytesRoundTripThroughTheSoleStrictIngress()
    {
        var serialized = JsonSerializer.SerializeToUtf8Bytes(Create());
        var canonical = CanonicalJson.Canonicalize(serialized);

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(GoldenCanonicalJson), canonical);
        var decoded = StrictWireCodec.Decode<RegisteredProjectSelection>(canonical);
        Assert.AreEqual("request-01", decoded.RequestId);
        Assert.AreEqual("registered-project-01", decoded.RegisteredProjectId);
        Assert.AreEqual(17L, decoded.BrokerGeneration);
        Assert.AreEqual(23L, decoded.RegistrationGeneration);
        Assert.IsTrue(GoldenProjectIdentity.FixedTimeEquals(decoded.ProjectIdentity));
    }

    [TestMethod]
    public void DtoExposesExactlySevenCorrelationPropertiesAndNoAuthorityBearingSurface()
    {
        var properties = typeof(RegisteredProjectSelection)
            .GetProperties()
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(
            new[]
            {
                "BrokerGeneration",
                "MessageKind",
                "ProjectIdentity",
                "ProtocolVersion",
                "RegisteredProjectId",
                "RegistrationGeneration",
                "RequestId",
            },
            properties.Select(property => property.Name).ToArray());
        Assert.AreEqual(typeof(TypedHash), properties.Single(property => property.Name == "ProjectIdentity").PropertyType);
        Assert.AreEqual(typeof(long), properties.Single(property => property.Name == "BrokerGeneration").PropertyType);
        Assert.AreEqual(typeof(long), properties.Single(property => property.Name == "RegistrationGeneration").PropertyType);

        var prohibited = new[]
        {
            "Path", "Uri", "Label", "Volume", "Root", "Directory", "Handle", "Endpoint",
            "Grant", "Accepted", "Authorized", "Status", "Verdict", "Permission", "Command",
            "Authority", "Lease", "SelfHash",
        };
        foreach (var property in properties)
        {
            Assert.IsFalse(prohibited.Any(value => property.Name.Contains(value, StringComparison.OrdinalIgnoreCase)));
        }
    }

    private static RegisteredProjectSelection Create(
        string protocolVersion = ProtocolVersions.Current,
        string messageKind = MessageKinds.RegisteredProjectSelection,
        string requestId = "request-01",
        string registeredProjectId = "registered-project-01",
        TypedHash? projectIdentity = null,
        long brokerGeneration = 17,
        long registrationGeneration = 23) =>
        new(
            protocolVersion,
            messageKind,
            requestId,
            registeredProjectId,
            projectIdentity ?? GoldenProjectIdentity,
            brokerGeneration,
            registrationGeneration);
}
