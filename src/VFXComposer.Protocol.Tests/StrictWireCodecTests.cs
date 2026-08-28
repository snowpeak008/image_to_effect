using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using VFXComposer.Protocol.Diagnostics;
using VFXComposer.Protocol.Handshake;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Jobs;
using VFXComposer.Protocol.Json;
using VFXComposer.Protocol.Projects;
using VFXComposer.Protocol.Registration;
using VFXComposer.Protocol.Status;

namespace VFXComposer.Protocol.Tests;

[TestClass]
public sealed class StrictWireCodecTests
{
    private const string ValidRequest =
        "{\"protocolVersion\":\"vfxcomposer.protocol/1.0\",\"messageKind\":\"handshake.request\",\"requestId\":\"request-01\",\"clientInstanceId\":\"desktop-01\",\"offeredCapabilities\":[\"protocol.handshake.v1\"]}";

    [TestMethod]
    public void Decode_AcceptsOnlyTheRegisteredExactHandshakeShape()
    {
        var request = Decode<HandshakeRequest>(ValidRequest);

        Assert.AreEqual("request-01", request.RequestId);
        CollectionAssert.AreEqual(
            new[] { CapabilityIds.HandshakeV1 },
            request.OfferedCapabilities.ToArray());
    }

    [TestMethod]
    public void Decode_AcceptsCompleteNestedProvenanceAndTypedHashFromFrozenDocument()
    {
        var provenance = new StatusProvenance(
            StatusDomains.Machine,
            "TEST_FIXTURE",
            TypedHash.ComputeUtf8("vfxcomposer.status-source/1", "fixture"),
            DateTimeOffset.UnixEpoch);
        var source = new MachineStatus(MachineStatusStates.Passed, provenance);
        var mutableCallerBytes = JsonSerializer.SerializeToUtf8Bytes(source);

        var decoded = StrictWireCodec.Decode<MachineStatus>(mutableCallerBytes);
        Array.Fill<byte>(mutableCallerBytes, (byte)'X');

        Assert.AreEqual(MachineStatusStates.Passed, decoded.State);
        Assert.IsNotNull(decoded.Provenance);
        Assert.AreEqual(StatusDomains.Machine, decoded.Provenance.StatusDomain);
        Assert.IsTrue(provenance.SourceIdentity.FixedTimeEquals(decoded.Provenance.SourceIdentity));
    }

    [TestMethod]
    public void Decode_AcceptsOnlyExactUtcTimestampLexemesIncludingSerializerOutput()
    {
        var serializerOutput = JsonSerializer.Serialize(new StatusProvenance(
            StatusDomains.Machine,
            "TEST_FIXTURE",
            TypedHash.ComputeUtf8("vfxcomposer.status-source/1", "fixture"),
            DateTimeOffset.UnixEpoch));
        var serializedTimestamp = JsonDocument.Parse(serializerOutput)
            .RootElement
            .GetProperty("observedAtUtc")
            .GetString();
        Assert.IsNotNull(serializedTimestamp);

        foreach (var timestamp in new[]
                 {
                     serializedTimestamp,
                     "1970-01-01T00:00:00Z",
                     "2026-08-26T12:34:56.1Z",
                     "2026-08-26T12:34:56.1234567+00:00",
                 })
        {
            var decoded = Decode<StatusProvenance>(CreateProvenanceJson(timestamp));
            Assert.AreEqual(TimeSpan.Zero, decoded.ObservedAtUtc.Offset, timestamp);
        }

        foreach (var timestamp in new[]
                 {
                     "not-a-dateZ",
                     "1970-01-01 00:00:00Z",
                     "1970-01-01T00:00Z",
                     "1970-01-01T00:00:00+08:00",
                     "1970-01-01T00:00:00.12345678Z",
                     "1970-01-01T00:00:00z",
                     "2026-02-30T00:00:00Z",
                 })
        {
            var exception = Assert.ThrowsExactly<WireDecodeException>(() =>
                Decode<StatusProvenance>(CreateProvenanceJson(timestamp)), timestamp);
            Assert.AreEqual(StableDiagnosticCodes.MalformedMessage, exception.Diagnostic.Code);
        }
    }

    [TestMethod]
    public void Decode_RejectsUnknownTopLevelAndNestedMembers()
    {
        AssertMalformed(ValidRequest[..^1] + ",\"unknown\":true}");

        var diagnostic = JsonSerializer.Serialize(
            StableDiagnosticCatalog.Create(StableDiagnosticCodes.MalformedMessage));
        var nestedUnknown =
            "{\"protocolVersion\":\"vfxcomposer.protocol/1.0\",\"messageKind\":\"handshake.response\",\"requestId\":\"request-01\",\"serverInstanceId\":\"broker-01\",\"accepted\":false,\"negotiatedCapabilities\":[],\"diagnostic\":" +
            diagnostic[..^1] + ",\"unknownNested\":true}}";

        var exception = Assert.ThrowsExactly<WireDecodeException>(() =>
            Decode<HandshakeResponse>(nestedUnknown));
        Assert.AreEqual(StableDiagnosticCodes.MalformedMessage, exception.Diagnostic.Code);
    }

    [TestMethod]
    public void Decode_RejectsDecodedDuplicateBomAndMissingRequiredField()
    {
        var duplicate =
            "{\"protocolVersion\":\"vfxcomposer.protocol/1.0\",\"messageKind\":\"handshake.request\",\"requestId\":\"request-01\",\"\\u0072equestId\":\"request-02\",\"clientInstanceId\":\"desktop-01\",\"offeredCapabilities\":[]}";
        AssertMalformed(duplicate);
        AssertMalformed(
            "{\"protocolVersion\":\"vfxcomposer.protocol/1.0\",\"messageKind\":\"handshake.request\",\"requestId\":\"request-01\",\"offeredCapabilities\":[]}");

        var withBom = new byte[] { 0xef, 0xbb, 0xbf }
            .Concat(Encoding.UTF8.GetBytes(ValidRequest))
            .ToArray();
        var exception = Assert.ThrowsExactly<WireDecodeException>(() =>
            StrictWireCodec.Decode<HandshakeRequest>(withBom));
        Assert.AreEqual(StableDiagnosticCodes.MalformedMessage, exception.Diagnostic.Code);
    }

    [TestMethod]
    public void Decode_DistinguishesUnsupportedVersionAndMessageKindWithoutEchoingInput()
    {
        var versionException = Assert.ThrowsExactly<WireDecodeException>(() =>
            Decode<HandshakeRequest>(ValidRequest.Replace(
                ProtocolVersions.Current,
                "future.secret.version",
                StringComparison.Ordinal)));
        Assert.AreEqual(StableDiagnosticCodes.UnsupportedProtocolVersion, versionException.Diagnostic.Code);

        var kindException = Assert.ThrowsExactly<WireDecodeException>(() =>
            Decode<HandshakeRequest>(ValidRequest.Replace(
                MessageKinds.HandshakeRequest,
                "authority.grant.secret",
                StringComparison.Ordinal)));
        Assert.AreEqual(StableDiagnosticCodes.UnsupportedMessageKind, kindException.Diagnostic.Code);

        const string secret = "DO_NOT_ECHO_SECRET_CONTENT";
        var contentException = Assert.ThrowsExactly<WireDecodeException>(() =>
            Decode<HandshakeRequest>(ValidRequest[..^1] + ",\"unknown\":\"" + secret + "\"}"));
        Assert.IsFalse(contentException.Message.Contains(secret, StringComparison.Ordinal));
        Assert.IsFalse(contentException.Diagnostic.Message.Contains(secret, StringComparison.Ordinal));
        Assert.IsNull(contentException.InnerException);
    }

    [TestMethod]
    public void Decode_RejectsResponseCapabilitiesThatAreUnknownDuplicatedOrUnsorted()
    {
        foreach (var capabilities in new[]
                 {
                     "[\"unknown.future.v1\"]",
                     "[\"protocol.handshake.v1\",\"protocol.handshake.v1\"]",
                     "[\"status.snapshot.v1\",\"protocol.handshake.v1\"]",
                 })
        {
            var response =
                "{\"protocolVersion\":\"vfxcomposer.protocol/1.0\",\"messageKind\":\"handshake.response\",\"requestId\":\"request-01\",\"serverInstanceId\":\"broker-01\",\"accepted\":true,\"negotiatedCapabilities\":" +
                capabilities + ",\"diagnostic\":null}";
            var exception = Assert.ThrowsExactly<WireDecodeException>(() =>
                Decode<HandshakeResponse>(response));
            Assert.AreEqual(StableDiagnosticCodes.MalformedMessage, exception.Diagnostic.Code);
        }
    }

    [TestMethod]
    public void Decode_RejectsEmbeddedDiagnosticPathDeviceAndWhitespaceMessageSpoofs()
    {
        foreach (var message in new[]
                 {
                     "Failure at /home/user/secret.json",
                     "\\\\server\\share\\secret.json",
                     "\\\\?\\C:\\secret\\file.json",
                     "C:\\secret\\file.json",
                     " The wire message is malformed.",
                     "The wire message is malformed. ",
                 })
        {
            var diagnostic =
                "{\"protocolVersion\":\"vfxcomposer.protocol/1.0\",\"messageKind\":\"diagnostic\",\"code\":\"VFXP0002\",\"severity\":\"ERROR\",\"message\":" +
                JsonSerializer.Serialize(message) + ",\"retryable\":false}";
            var response =
                "{\"protocolVersion\":\"vfxcomposer.protocol/1.0\",\"messageKind\":\"handshake.response\",\"requestId\":\"request-01\",\"serverInstanceId\":\"broker-01\",\"accepted\":false,\"negotiatedCapabilities\":[],\"diagnostic\":" +
                diagnostic + "}";
            var exception = Assert.ThrowsExactly<WireDecodeException>(() =>
                Decode<HandshakeResponse>(response), message);
            Assert.AreEqual(StableDiagnosticCodes.MalformedMessage, exception.Diagnostic.Code);
            Assert.IsFalse(exception.Message.Contains(message, StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void Decode_RejectsMissingFieldsInsideDiagnosticProvenanceAndTypedHash()
    {
        var diagnosticMissingRetryable =
            "{\"protocolVersion\":\"vfxcomposer.protocol/1.0\",\"messageKind\":\"handshake.response\",\"requestId\":\"request-01\",\"serverInstanceId\":\"broker-01\",\"accepted\":false,\"negotiatedCapabilities\":[],\"diagnostic\":{\"protocolVersion\":\"vfxcomposer.protocol/1.0\",\"messageKind\":\"diagnostic\",\"code\":\"VFXP0002\",\"severity\":\"ERROR\",\"message\":\"The wire message is malformed.\"}}";
        AssertMalformedResponse(diagnosticMissingRetryable);

        var provenanceMissingObservedAt =
            "{\"protocolVersion\":\"vfxcomposer.protocol/1.0\",\"state\":\"PASSED\",\"provenance\":{\"protocolVersion\":\"vfxcomposer.protocol/1.0\",\"statusDomain\":\"MACHINE\",\"sourceKind\":\"TEST_FIXTURE\",\"sourceIdentity\":{\"typeTag\":\"vfxcomposer.status-source/1\",\"digest\":\"sha256:" +
            new string('0', 64) + "\"}}}";
        AssertMalformedStatus(provenanceMissingObservedAt);

        var typedHashMissingDigest =
            "{\"protocolVersion\":\"vfxcomposer.protocol/1.0\",\"state\":\"PASSED\",\"provenance\":{\"protocolVersion\":\"vfxcomposer.protocol/1.0\",\"statusDomain\":\"MACHINE\",\"sourceKind\":\"TEST_FIXTURE\",\"sourceIdentity\":{\"typeTag\":\"vfxcomposer.status-source/1\"},\"observedAtUtc\":\"1970-01-01T00:00:00+00:00\"}}";
        AssertMalformedStatus(typedHashMissingDigest);

        var provenanceMalformedTimestamp =
            "{\"protocolVersion\":\"vfxcomposer.protocol/1.0\",\"state\":\"PASSED\",\"provenance\":{\"protocolVersion\":\"vfxcomposer.protocol/1.0\",\"statusDomain\":\"MACHINE\",\"sourceKind\":\"TEST_FIXTURE\",\"sourceIdentity\":{\"typeTag\":\"vfxcomposer.status-source/1\",\"digest\":\"sha256:" +
            new string('0', 64) + "\"},\"observedAtUtc\":\"not-a-dateZ\"}}";
        AssertMalformedStatus(provenanceMalformedTimestamp);

        var provenanceNonUtcTimestamp = provenanceMalformedTimestamp.Replace(
            "not-a-dateZ",
            "1970-01-01T08:00:00+08:00",
            StringComparison.Ordinal);
        AssertMalformedStatus(provenanceNonUtcTimestamp);
    }

    [TestMethod]
    public void Decode_RejectsLocalJobModelsBecauseTheyAreNotWireDtos()
    {
        Assert.IsFalse(WireSchemaRegistry.TryGetByType(typeof(JobIdentity), out _));
        Assert.IsFalse(WireSchemaRegistry.TryGetByType(typeof(JobStatus), out _));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            StrictWireCodec.Decode<JobIdentity>(Encoding.UTF8.GetBytes("{}")));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            StrictWireCodec.Decode<JobStatus>(Encoding.UTF8.GetBytes("{}")));
    }

    [TestMethod]
    public void Decode_RegisteredProjectSelectionRejectsStrictShapeAndCorrelationDriftWithoutEchoingInput()
    {
        var valid = CreateRegisteredProjectSelectionJson();
        var decoded = StrictWireCodec.Decode<RegisteredProjectSelection>(Encoding.UTF8.GetBytes(valid));
        Assert.AreEqual("request-selection-01", decoded.RequestId);
        Assert.AreEqual("registered-project-01", decoded.RegisteredProjectId);
        Assert.AreEqual(17L, decoded.BrokerGeneration);
        Assert.AreEqual(23L, decoded.RegistrationGeneration);

        var missing = JsonNode.Parse(valid)!.AsObject();
        missing.Remove("requestId");
        AssertSelectionMalformed(missing.ToJsonString());

        var nestedMissing = JsonNode.Parse(valid)!.AsObject();
        nestedMissing["projectIdentity"]!.AsObject().Remove("digest");
        AssertSelectionMalformed(nestedMissing.ToJsonString());

        var nestedExtra = JsonNode.Parse(valid)!.AsObject();
        nestedExtra["projectIdentity"]!.AsObject()["rawPath"] = "C:/untrusted";
        AssertSelectionMalformed(nestedExtra.ToJsonString());

        var wrongType = JsonNode.Parse(valid)!.AsObject();
        wrongType["brokerGeneration"] = "17";
        AssertSelectionMalformed(wrongType.ToJsonString());

        var wrongDomain = JsonNode.Parse(valid)!.AsObject();
        wrongDomain["projectIdentity"]!.AsObject()["typeTag"] = "vfxcomposer.volume-identity/1";
        AssertSelectionMalformed(wrongDomain.ToJsonString());

        foreach (var propertyName in new[] { "brokerGeneration", "registrationGeneration" })
        {
            foreach (var value in new JsonNode?[] { JsonValue.Create(0), JsonValue.Create(-1), JsonValue.Create(9223372036854775808m) })
            {
                var invalidGeneration = JsonNode.Parse(valid)!.AsObject();
                invalidGeneration[propertyName] = value;
                AssertSelectionMalformed(invalidGeneration.ToJsonString());
            }
        }

        var pathValue = JsonNode.Parse(valid)!.AsObject();
        pathValue["registeredProjectId"] = "C:/untrusted";
        AssertSelectionMalformed(pathValue.ToJsonString());

        var pathField = JsonNode.Parse(valid)!.AsObject();
        pathField["callerPath"] = "C:/untrusted";
        AssertSelectionMalformed(pathField.ToJsonString());

        const string hostileAuthority = "DO_NOT_ECHO_SELECTION_AUTHORITY";
        var authorityField = JsonNode.Parse(valid)!.AsObject();
        authorityField["authorityGrant"] = hostileAuthority;
        var authorityException = Assert.ThrowsExactly<WireDecodeException>(() =>
            StrictWireCodec.Decode<RegisteredProjectSelection>(
                Encoding.UTF8.GetBytes(authorityField.ToJsonString())));
        Assert.AreEqual(StableDiagnosticCodes.MalformedMessage, authorityException.Diagnostic.Code);
        Assert.IsFalse(authorityException.Message.Contains(hostileAuthority, StringComparison.Ordinal));
        Assert.IsFalse(authorityException.Diagnostic.Message.Contains(hostileAuthority, StringComparison.Ordinal));
        Assert.IsNull(authorityException.InnerException);

        var duplicate = valid.Replace(
            "\"requestId\":\"request-selection-01\"",
            "\"requestId\":\"request-selection-01\",\"\\u0072equestId\":\"request-selection-02\"",
            StringComparison.Ordinal);
        AssertSelectionMalformed(duplicate);

        var withBom = new byte[] { 0xef, 0xbb, 0xbf }
            .Concat(Encoding.UTF8.GetBytes(valid))
            .ToArray();
        var bomException = Assert.ThrowsExactly<WireDecodeException>(() =>
            StrictWireCodec.Decode<RegisteredProjectSelection>(withBom));
        Assert.AreEqual(StableDiagnosticCodes.MalformedMessage, bomException.Diagnostic.Code);
    }

    [TestMethod]
    public void Decode_RegisteredProjectSelectionDistinguishesVersionAndKind()
    {
        var valid = CreateRegisteredProjectSelectionJson();
        var versionException = Assert.ThrowsExactly<WireDecodeException>(() =>
            StrictWireCodec.Decode<RegisteredProjectSelection>(Encoding.UTF8.GetBytes(valid.Replace(
                ProtocolVersions.Current,
                "future.selection.version",
                StringComparison.Ordinal))));
        Assert.AreEqual(StableDiagnosticCodes.UnsupportedProtocolVersion, versionException.Diagnostic.Code);

        var kindException = Assert.ThrowsExactly<WireDecodeException>(() =>
            StrictWireCodec.Decode<RegisteredProjectSelection>(Encoding.UTF8.GetBytes(valid.Replace(
                MessageKinds.RegisteredProjectSelection,
                "authority.grant.selection",
                StringComparison.Ordinal))));
        Assert.AreEqual(StableDiagnosticCodes.UnsupportedMessageKind, kindException.Diagnostic.Code);
    }

    [TestMethod]
    public void Decode_WorkerProjectLocatorRejectsStrictShapeDomainsGenerationsAndSelfHashDrift()
    {
        var validBytes = WorkerProjectLocatorTests.CreateSelfHashedLocatorBytes();
        var valid = Encoding.UTF8.GetString(validBytes);
        var decoded = StrictWireCodec.Decode<WorkerProjectLocator>(validBytes);
        Assert.AreEqual("locator-request-01", decoded.RequestId);
        Assert.AreEqual(29L, decoded.EnrollmentGeneration);

        var missing = JsonNode.Parse(valid)!.AsObject();
        missing.Remove("projectRootIdentity");
        AssertLocatorMalformed(missing.ToJsonString());

        var nestedMissing = JsonNode.Parse(valid)!.AsObject();
        nestedMissing["volumeIdentity"]!.AsObject().Remove("digest");
        AssertLocatorMalformed(nestedMissing.ToJsonString());

        var nestedExtra = JsonNode.Parse(valid)!.AsObject();
        nestedExtra["repositoryIdentity"]!.AsObject()["rawPath"] = "C:/untrusted";
        AssertLocatorMalformed(nestedExtra.ToJsonString());

        var wrongType = JsonNode.Parse(valid)!.AsObject();
        wrongType["enrollmentGeneration"] = "29";
        AssertLocatorMalformed(wrongType.ToJsonString());

        var wrongDomain = JsonNode.Parse(valid)!.AsObject();
        wrongDomain["projectIdentity"]!.AsObject()["typeTag"] =
            ProjectRegistrationAttestation.VolumeIdentityType;
        AssertLocatorMalformed(wrongDomain.ToJsonString());

        foreach (var propertyName in new[]
                 {
                     "brokerGeneration",
                     "registrationGeneration",
                     "enrollmentGeneration",
                 })
        {
            foreach (var value in new JsonNode?[]
                     {
                         JsonValue.Create(0),
                         JsonValue.Create(-1),
                         JsonValue.Create(9223372036854775808m),
                     })
            {
                var invalidGeneration = JsonNode.Parse(valid)!.AsObject();
                invalidGeneration[propertyName] = value;
                AssertLocatorMalformed(invalidGeneration.ToJsonString());
            }
        }

        var pathValue = JsonNode.Parse(valid)!.AsObject();
        pathValue["workerSessionId"] = "C:/untrusted";
        AssertLocatorMalformed(pathValue.ToJsonString());

        const string hostileAuthority = "DO_NOT_ECHO_LOCATOR_AUTHORITY";
        var authorityField = JsonNode.Parse(valid)!.AsObject();
        authorityField["authorityGrant"] = hostileAuthority;
        var authorityException = Assert.ThrowsExactly<WireDecodeException>(() =>
            StrictWireCodec.Decode<WorkerProjectLocator>(
                Encoding.UTF8.GetBytes(authorityField.ToJsonString())));
        Assert.AreEqual(StableDiagnosticCodes.MalformedMessage, authorityException.Diagnostic.Code);
        Assert.IsFalse(authorityException.Message.Contains(hostileAuthority, StringComparison.Ordinal));
        Assert.IsFalse(authorityException.Diagnostic.Message.Contains(hostileAuthority, StringComparison.Ordinal));
        Assert.IsNull(authorityException.InnerException);

        var selfHashDrift = JsonNode.Parse(valid)!.AsObject();
        selfHashDrift["selfHash"]!.AsObject()["digest"] = "sha256:" + new string('0', 64);
        AssertLocatorMalformed(selfHashDrift.ToJsonString());

        var duplicate = valid.Replace(
            "\"requestId\":\"locator-request-01\"",
            "\"requestId\":\"locator-request-01\",\"\\u0072equestId\":\"locator-request-02\"",
            StringComparison.Ordinal);
        AssertLocatorMalformed(duplicate);

        var withBom = new byte[] { 0xef, 0xbb, 0xbf }.Concat(validBytes).ToArray();
        var bomException = Assert.ThrowsExactly<WireDecodeException>(() =>
            StrictWireCodec.Decode<WorkerProjectLocator>(withBom));
        Assert.AreEqual(StableDiagnosticCodes.MalformedMessage, bomException.Diagnostic.Code);
    }

    [TestMethod]
    public void Decode_WorkerProjectLocatorAcknowledgementRejectsDriftAndHandleGrantVocabulary()
    {
        var locator = StrictWireCodec.Decode<WorkerProjectLocator>(
            WorkerProjectLocatorTests.CreateSelfHashedLocatorBytes());
        var validBytes = WorkerProjectLocatorTests.CreateSelfHashedAcknowledgementBytes(locator);
        var valid = Encoding.UTF8.GetString(validBytes);
        var decoded = StrictWireCodec.Decode<WorkerProjectLocatorAcknowledgement>(validBytes);
        Assert.AreEqual(WorkerProjectLocatorAcknowledgement.AcceptedDisposition, decoded.Disposition);
        Assert.IsTrue(locator.SelfHash.FixedTimeEquals(decoded.LocatorSelfHash));

        var missing = JsonNode.Parse(valid)!.AsObject();
        missing.Remove("locatorSelfHash");
        AssertLocatorAcknowledgementMalformed(missing.ToJsonString());

        var wrongHashDomain = JsonNode.Parse(valid)!.AsObject();
        wrongHashDomain["locatorSelfHash"]!.AsObject()["typeTag"] = WorkerProjectHandleGrant.SelfHashType;
        AssertLocatorAcknowledgementMalformed(wrongHashDomain.ToJsonString());

        var wrongDisposition = JsonNode.Parse(valid)!.AsObject();
        wrongDisposition["disposition"] = WorkerProjectHandleGrantAcknowledgement.AcceptedDisposition;
        AssertLocatorAcknowledgementMalformed(wrongDisposition.ToJsonString());

        var booleanAccepted = JsonNode.Parse(valid)!.AsObject();
        booleanAccepted["accepted"] = true;
        AssertLocatorAcknowledgementMalformed(booleanAccepted.ToJsonString());

        var wrongGeneration = JsonNode.Parse(valid)!.AsObject();
        wrongGeneration["registrationGeneration"] = 0;
        AssertLocatorAcknowledgementMalformed(wrongGeneration.ToJsonString());

        var selfHashDrift = JsonNode.Parse(valid)!.AsObject();
        selfHashDrift["workerProcessEpoch"] = "worker-epoch-02";
        AssertLocatorAcknowledgementMalformed(selfHashDrift.ToJsonString());

        var versionException = Assert.ThrowsExactly<WireDecodeException>(() =>
            StrictWireCodec.Decode<WorkerProjectLocatorAcknowledgement>(Encoding.UTF8.GetBytes(valid.Replace(
                ProtocolVersions.Current,
                "future.locator.version",
                StringComparison.Ordinal))));
        Assert.AreEqual(StableDiagnosticCodes.UnsupportedProtocolVersion, versionException.Diagnostic.Code);

        var kindException = Assert.ThrowsExactly<WireDecodeException>(() =>
            StrictWireCodec.Decode<WorkerProjectLocatorAcknowledgement>(Encoding.UTF8.GetBytes(valid.Replace(
                MessageKinds.WorkerProjectLocatorAcknowledgement,
                MessageKinds.WorkerProjectHandleGrantAcknowledgement,
                StringComparison.Ordinal))));
        Assert.AreEqual(StableDiagnosticCodes.UnsupportedMessageKind, kindException.Diagnostic.Code);
    }

    private static T Decode<T>(string json)
        where T : class =>
        StrictWireCodec.Decode<T>(Encoding.UTF8.GetBytes(json));

    private static void AssertMalformed(string json)
    {
        var exception = Assert.ThrowsExactly<WireDecodeException>(() =>
            Decode<HandshakeRequest>(json));
        Assert.AreEqual(StableDiagnosticCodes.MalformedMessage, exception.Diagnostic.Code);
    }

    private static void AssertMalformedResponse(string json)
    {
        var exception = Assert.ThrowsExactly<WireDecodeException>(() =>
            Decode<HandshakeResponse>(json));
        Assert.AreEqual(StableDiagnosticCodes.MalformedMessage, exception.Diagnostic.Code);
    }

    private static void AssertMalformedStatus(string json)
    {
        var exception = Assert.ThrowsExactly<WireDecodeException>(() =>
            Decode<MachineStatus>(json));
        Assert.AreEqual(StableDiagnosticCodes.MalformedMessage, exception.Diagnostic.Code);
    }

    private static void AssertSelectionMalformed(string json)
    {
        var exception = Assert.ThrowsExactly<WireDecodeException>(() =>
            StrictWireCodec.Decode<RegisteredProjectSelection>(Encoding.UTF8.GetBytes(json)));
        Assert.AreEqual(StableDiagnosticCodes.MalformedMessage, exception.Diagnostic.Code);
        Assert.IsNull(exception.InnerException);
    }

    private static void AssertLocatorMalformed(string json)
    {
        var exception = Assert.ThrowsExactly<WireDecodeException>(() =>
            StrictWireCodec.Decode<WorkerProjectLocator>(Encoding.UTF8.GetBytes(json)));
        Assert.AreEqual(StableDiagnosticCodes.MalformedMessage, exception.Diagnostic.Code);
        Assert.IsNull(exception.InnerException);
    }

    private static void AssertLocatorAcknowledgementMalformed(string json)
    {
        var exception = Assert.ThrowsExactly<WireDecodeException>(() =>
            StrictWireCodec.Decode<WorkerProjectLocatorAcknowledgement>(Encoding.UTF8.GetBytes(json)));
        Assert.AreEqual(StableDiagnosticCodes.MalformedMessage, exception.Diagnostic.Code);
        Assert.IsNull(exception.InnerException);
    }

    private static string CreateRegisteredProjectSelectionJson() => JsonSerializer.Serialize(
        new RegisteredProjectSelection(
            ProtocolVersions.Current,
            MessageKinds.RegisteredProjectSelection,
            "request-selection-01",
            "registered-project-01",
            TypedHash.ComputeUtf8(RegisteredProjectSelection.ProjectIdentityType, "selection-fixture"),
            17,
            23));

    private static string CreateProvenanceJson(string timestamp) =>
        "{\"protocolVersion\":\"vfxcomposer.protocol/1.0\",\"statusDomain\":\"MACHINE\",\"sourceKind\":\"TEST_FIXTURE\",\"sourceIdentity\":{\"typeTag\":\"vfxcomposer.status-source/1\",\"digest\":\"sha256:" +
        new string('0', 64) + "\"},\"observedAtUtc\":" + JsonSerializer.Serialize(timestamp) + "}";
}
