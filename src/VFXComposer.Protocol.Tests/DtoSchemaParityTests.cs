using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using VFXComposer.Protocol.Diagnostics;
using VFXComposer.Protocol.Handshake;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;
using VFXComposer.Protocol.Json;
using VFXComposer.Protocol.Projects;
using VFXComposer.Protocol.Queries;
using VFXComposer.Protocol.Registration;
using VFXComposer.Protocol.Status;

namespace VFXComposer.Protocol.Tests;

[TestClass]
public sealed class DtoSchemaParityTests
{
    [TestMethod]
    public void EveryRegisteredDtoJsonPropertyExactlyMatchesSchemaPropertiesAndRequiredSet()
    {
        foreach (var descriptor in WireSchemaRegistry.All)
        {
            using var schema = ProtocolSchemaTestSupport.LoadSchema(descriptor.SchemaId);
            var schemaProperties = schema.RootElement
                .GetProperty("properties")
                .EnumerateObject()
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var requiredProperties = schema.RootElement
                .GetProperty("required")
                .EnumerateArray()
                .Select(value => value.GetString()!)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var dtoProperties = descriptor.DtoType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.GetCustomAttribute<JsonPropertyNameAttribute>())
                .Where(attribute => attribute is not null)
                .Select(attribute => attribute!.Name)
                .Order(StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(schemaProperties, requiredProperties, descriptor.SchemaId);
            CollectionAssert.AreEqual(schemaProperties, dtoProperties, descriptor.DtoType.FullName);
            CollectionAssert.AreEqual(
                requiredProperties,
                descriptor.RequiredTopLevelProperties.Order(StringComparer.Ordinal).ToArray(),
                descriptor.DtoType.FullName);
        }
    }

    [TestMethod]
    public void RepresentativeDtosSerializeToTheirExactRegisteredTopLevelShapes()
    {
        var provenance = new StatusProvenance(
            StatusDomains.Machine,
            "MACHINE_TEST",
            TypedHash.ComputeUtf8("vfxcomposer.status-source/1", "fixture"),
            DateTimeOffset.UnixEpoch);
        var projectIdentity = TypedHash.ComputeUtf8(
            ProjectRegistrationAttestation.ProjectIdentityType,
            "project");
        var content = System.Text.Encoding.UTF8.GetBytes("{}");
        var samples = new Dictionary<Type, object>
        {
            [typeof(HandshakeRequest)] = new HandshakeRequest("request-01", "desktop-01", [CapabilityIds.HandshakeV1]),
            [typeof(HandshakeResponse)] = HandshakeResponse.Accept("request-01", "broker-01", [CapabilityIds.HandshakeV1]),
            [typeof(StableDiagnostic)] = StableDiagnosticCatalog.Create(StableDiagnosticCodes.Disconnected),
            [typeof(MachineStatus)] = new MachineStatus(MachineStatusStates.Passed, provenance),
            [typeof(VisualStatus)] = new VisualStatus(VisualStatusStates.VisualPending),
            [typeof(UserVerdictStatus)] = new UserVerdictStatus(UserVerdictStatusStates.NotSigned),
            [typeof(L3Status)] = new L3Status(L3StatusStates.NotGranted),
            [typeof(L4Status)] = new L4Status(L4StatusStates.NotGranted),
            [typeof(StatusProvenance)] = provenance,
            [typeof(PeerHello)] = new PeerHello(
                "request-02",
                PeerRoles.Desktop,
                "desktop-01",
                42,
                "epoch-01",
                [PeerCapabilityIds.PeerSessionV1],
                TypedHash.ComputeUtf8(PeerHello.ProcessImageIdentityType, "desktop-image")),
            [typeof(PeerSessionAccepted)] = new PeerSessionAccepted(
                "request-02",
                "session-01",
                PeerRoles.Desktop,
                "broker-01",
                1,
                "epoch-01",
                [PeerCapabilityIds.PeerSessionV1]),
            [typeof(RegisteredProjectSelection)] = new RegisteredProjectSelection(
                ProtocolVersions.Current,
                MessageKinds.RegisteredProjectSelection,
                "request-selection-01",
                "registered-project-01",
                TypedHash.ComputeUtf8(RegisteredProjectSelection.ProjectIdentityType, "selection-project"),
                1,
                1),
            [typeof(ProjectRegistrationAttestation)] = new ProjectRegistrationAttestation(
                ProtocolVersions.Current,
                MessageKinds.ProjectRegistrationAttestation,
                "request-03",
                "project-01",
                projectIdentity,
                TypedHash.ComputeUtf8(ProjectRegistrationAttestation.VolumeIdentityType, "volume"),
                TypedHash.ComputeUtf8(ProjectRegistrationAttestation.DirectoryIdentityType, "repository"),
                TypedHash.ComputeUtf8(ProjectRegistrationAttestation.DirectoryIdentityType, "project-root"),
                1,
                1,
                "worker-session-01",
                "worker-epoch-01",
                TypedHash.ComputeUtf8(ProjectRegistrationAttestation.SelfHashType, "sample")),
            [typeof(ProjectLeaseDescriptor)] = new ProjectLeaseDescriptor(
                ProtocolVersions.Current,
                MessageKinds.ProjectLeaseDescriptor,
                "request-04",
                "lease-01",
                "project-01",
                projectIdentity,
                1,
                1,
                "worker-session-01",
                "worker-epoch-01",
                1,
                TypedHash.ComputeUtf8(ProjectLeaseDescriptor.SelfHashType, "sample")),
            [typeof(WorkerProjectLocator)] = new WorkerProjectLocator(
                ProtocolVersions.Current,
                MessageKinds.WorkerProjectLocator,
                "request-04-locator",
                "project-01",
                projectIdentity,
                TypedHash.ComputeUtf8(ProjectRegistrationAttestation.VolumeIdentityType, "volume"),
                TypedHash.ComputeUtf8(ProjectRegistrationAttestation.DirectoryIdentityType, "repository"),
                TypedHash.ComputeUtf8(ProjectRegistrationAttestation.DirectoryIdentityType, "project-root"),
                1,
                1,
                1,
                "worker-session-01",
                "worker-epoch-01",
                TypedHash.ComputeUtf8(WorkerProjectLocator.SelfHashType, "sample")),
            [typeof(WorkerProjectLocatorAcknowledgement)] = new WorkerProjectLocatorAcknowledgement(
                ProtocolVersions.Current,
                MessageKinds.WorkerProjectLocatorAcknowledgement,
                "request-04-locator-ack",
                "project-01",
                1,
                1,
                1,
                "worker-session-01",
                "worker-epoch-01",
                TypedHash.ComputeUtf8(WorkerProjectLocator.SelfHashType, "locator"),
                WorkerProjectLocatorAcknowledgement.AcceptedDisposition,
                TypedHash.ComputeUtf8(WorkerProjectLocatorAcknowledgement.SelfHashType, "sample")),
            [typeof(WorkerProjectHandleGrant)] = new WorkerProjectHandleGrant(
                ProtocolVersions.Current,
                MessageKinds.WorkerProjectHandleGrant,
                "request-04-worker",
                "lease-01",
                "project-01",
                projectIdentity,
                TypedHash.ComputeUtf8(ProjectRegistrationAttestation.VolumeIdentityType, "volume"),
                TypedHash.ComputeUtf8(ProjectRegistrationAttestation.DirectoryIdentityType, "repository"),
                TypedHash.ComputeUtf8(ProjectRegistrationAttestation.DirectoryIdentityType, "project-root"),
                1,
                1,
                1,
                "worker-session-01",
                "worker-epoch-01",
                WorkerProjectHandleGrant.HandleEncodingName,
                "0000000000000100",
                "0000000000000104",
                "0000000000000108",
                TypedHash.ComputeUtf8(WorkerProjectHandleGrant.SelfHashType, "sample")),
            [typeof(WorkerProjectHandleGrantAcknowledgement)] = new WorkerProjectHandleGrantAcknowledgement(
                ProtocolVersions.Current,
                MessageKinds.WorkerProjectHandleGrantAcknowledgement,
                "request-04-worker-ack",
                "lease-01",
                1,
                1,
                "worker-session-01",
                "worker-epoch-01",
                TypedHash.ComputeUtf8(WorkerProjectHandleGrant.SelfHashType, "grant"),
                WorkerProjectHandleGrantAcknowledgement.AcceptedDisposition,
                TypedHash.ComputeUtf8(WorkerProjectHandleGrantAcknowledgement.SelfHashType, "sample")),
            [typeof(WorkerProjectHandleRevoke)] = new WorkerProjectHandleRevoke(
                ProtocolVersions.Current,
                MessageKinds.WorkerProjectHandleRevoke,
                "request-04-worker-revoke",
                "lease-01",
                1,
                1,
                "worker-session-01",
                "worker-epoch-01",
                TypedHash.ComputeUtf8(WorkerProjectHandleGrant.SelfHashType, "grant"),
                WorkerProjectHandleRevoke.LeaseRevokedReason,
                TypedHash.ComputeUtf8(WorkerProjectHandleRevoke.SelfHashType, "sample")),
            [typeof(WorkerProjectHandleRevokeAcknowledgement)] = new WorkerProjectHandleRevokeAcknowledgement(
                ProtocolVersions.Current,
                MessageKinds.WorkerProjectHandleRevokeAcknowledgement,
                "request-04-worker-revoke-ack",
                "lease-01",
                1,
                1,
                "worker-session-01",
                "worker-epoch-01",
                TypedHash.ComputeUtf8(WorkerProjectHandleGrant.SelfHashType, "grant"),
                TypedHash.ComputeUtf8(WorkerProjectHandleRevoke.SelfHashType, "revoke"),
                WorkerProjectHandleRevokeAcknowledgement.ClosedDisposition,
                TypedHash.ComputeUtf8(WorkerProjectHandleRevokeAcknowledgement.SelfHashType, "sample")),
            [typeof(ReadDocumentQuery)] = new ReadDocumentQuery(
                ProtocolVersions.Current,
                MessageKinds.ReadDocumentQuery,
                "request-05",
                "lease-01",
                projectIdentity,
                1,
                DocumentKinds.Manifest,
                "manifest-01",
                null),
            [typeof(ReadDocumentResult)] = new ReadDocumentResult(
                ProtocolVersions.Current,
                MessageKinds.ReadDocumentResult,
                "request-05",
                true,
                projectIdentity,
                DocumentKinds.Manifest,
                "manifest-01",
                TypedHash.Compute(ReadDocumentResult.ContentHashType, content),
                content.Length,
                Convert.ToBase64String(content),
                null),
        };

        foreach (var pair in Phase3WireFixtures.RepresentativeDtos)
        {
            samples.Add(pair.Key, pair.Value);
        }

        foreach (var descriptor in WireSchemaRegistry.All)
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(samples[descriptor.DtoType], descriptor.DtoType);
            using var document = StrictJsonReader.Parse(json);
            using var schema = ProtocolSchemaTestSupport.LoadSchema(descriptor.SchemaId);
            ProtocolSchemaTestSupport.AssertTopLevelExact(document.RootElement, schema);
        }
    }

    [TestMethod]
    public void StableDiagnosticRejectsAnyMessageOutsideTheFixedCatalogIncludingPathAndWhitespaceShapes()
    {
        foreach (var message in new[]
                 {
                     "bad\nsecond line",
                     "Failure at /home/user/secret.json",
                     "Failure at C:\\secret\\file.json",
                     "\\\\server\\share\\secret.json",
                     "\\\\?\\C:\\secret\\file.json",
                     " The wire message is malformed.",
                     "The wire message is malformed. ",
                 })
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
                new StableDiagnostic(
                    StableDiagnosticCodes.MalformedMessage,
                    DiagnosticSeverities.Error,
                    message,
                    retryable: false),
                message);
        }
    }

    [TestMethod]
    public void StableDiagnosticDeserializationRejectsSpoofedVersionAndMessageKind()
    {
        const string spoofed =
            "{\"protocolVersion\":\"future/99\",\"messageKind\":\"authority.grant\",\"code\":\"VFXP0002\",\"severity\":\"ERROR\",\"message\":\"Rejected.\",\"retryable\":false}";

        var exception = Assert.ThrowsExactly<WireDecodeException>(() =>
            StrictWireCodec.Decode<StableDiagnostic>(System.Text.Encoding.UTF8.GetBytes(spoofed)));
        Assert.AreEqual(StableDiagnosticCodes.UnsupportedProtocolVersion, exception.Diagnostic.Code);
    }
}

internal static class ProtocolSchemaTestSupport
{
    public static JsonDocument LoadSchema(string schemaId)
    {
        var uri = new Uri(schemaId, UriKind.Absolute);
        const string desktopRoot = "/desktop/";
        var rootIndex = uri.AbsolutePath.IndexOf(desktopRoot, StringComparison.Ordinal);
        if (rootIndex < 0)
        {
            throw new InvalidOperationException("Schema id is outside the desktop schema root.");
        }

        var relativePath = uri.AbsolutePath[(rootIndex + desktopRoot.Length)..]
            .Replace('/', Path.DirectorySeparatorChar);
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Schemas", relativePath);
        return StrictJsonReader.Parse(File.ReadAllBytes(schemaPath));
    }

    public static void AssertTopLevelExact(JsonElement value, JsonDocument schema)
    {
        var required = schema.RootElement
            .GetProperty("required")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
        var allowed = schema.RootElement
            .GetProperty("properties")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
        ExactObjectValidator.Validate(value, required, allowed.Except(required, StringComparer.Ordinal));

        foreach (var property in schema.RootElement.GetProperty("properties").EnumerateObject())
        {
            var observed = value.GetProperty(property.Name);
            if (property.Value.TryGetProperty("const", out var constant))
            {
                if (!Equivalent(observed, constant))
                {
                    throw new StrictJsonException("CONST_MISMATCH", "A property differs from its schema constant.");
                }
            }

            if (property.Value.TryGetProperty("enum", out var enumeration) &&
                !enumeration.EnumerateArray().Any(candidate => Equivalent(observed, candidate)))
            {
                throw new StrictJsonException("ENUM_MISMATCH", "A property is outside its schema enumeration.");
            }
        }
    }

    private static bool Equivalent(JsonElement left, JsonElement right) =>
        left.ValueKind == right.ValueKind &&
        string.Equals(left.ToString(), right.ToString(), StringComparison.Ordinal);
}
