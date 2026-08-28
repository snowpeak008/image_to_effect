using System.Text;
using System.Text.Json;
using VFXComposer.Protocol.Diagnostics;
using VFXComposer.Protocol.Handshake;
using VFXComposer.Protocol.Json;

namespace VFXComposer.Protocol.Tests;

[TestClass]
public sealed class HandshakeSchemaTests
{
    [TestMethod]
    public void Request_RoundTripsWithExactVersionKindAndShape()
    {
        var request = new HandshakeRequest(
            "request-01",
            "desktop-01",
            [CapabilityIds.HandshakeV1, "future.unknown.v2"]);
        var json = JsonSerializer.SerializeToUtf8Bytes(request);

        using var document = StrictJsonReader.Parse(json);
        ProtocolSchemaTestSupport.AssertTopLevelExact(
            document.RootElement,
            ProtocolSchemaTestSupport.LoadSchema(WireSchemaIds.HandshakeRequestV1));
        Assert.AreEqual(ProtocolVersions.Current, document.RootElement.GetProperty("protocolVersion").GetString());
        Assert.AreEqual(MessageKinds.HandshakeRequest, document.RootElement.GetProperty("messageKind").GetString());

        var roundTrip = StrictWireCodec.Decode<HandshakeRequest>(json);
        CollectionAssert.AreEqual(request.OfferedCapabilities.ToArray(), roundTrip.OfferedCapabilities.ToArray());
    }

    [TestMethod]
    public void Response_RoundTripsBothAcceptedAndRejectedExactShapes()
    {
        var accepted = HandshakeResponse.Accept(
            "request-01",
            "broker-01",
            [CapabilityIds.HandshakeV1, CapabilityIds.StatusSnapshotV1]);
        var rejected = HandshakeResponse.Reject(
            "request-02",
            "broker-01",
            StableDiagnosticCatalog.Create(StableDiagnosticCodes.UnsupportedProtocolVersion));
        var schema = ProtocolSchemaTestSupport.LoadSchema(WireSchemaIds.HandshakeResponseV1);

        foreach (var response in new[] { accepted, rejected })
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(response);
            using var document = StrictJsonReader.Parse(json);
            ProtocolSchemaTestSupport.AssertTopLevelExact(document.RootElement, schema);
            Assert.AreEqual(MessageKinds.HandshakeResponse, document.RootElement.GetProperty("messageKind").GetString());
            Assert.IsNotNull(StrictWireCodec.Decode<HandshakeResponse>(json));
        }

        Assert.IsNull(accepted.Diagnostic);
        Assert.AreEqual(0, rejected.NegotiatedCapabilities.Count);
        Assert.IsNotNull(rejected.Diagnostic);
    }

    [TestMethod]
    public void Constructors_RejectUnknownVersionKindDuplicatesAndInconsistentOutcome()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new HandshakeRequest(
                "future/99",
                MessageKinds.HandshakeRequest,
                "request-01",
                "desktop-01",
                Array.Empty<string>()));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new HandshakeRequest(
                ProtocolVersions.Current,
                "wrong.kind",
                "request-01",
                "desktop-01",
                Array.Empty<string>()));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new HandshakeRequest(
                "request-01",
                "desktop-01",
                [CapabilityIds.HandshakeV1, CapabilityIds.HandshakeV1]));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new HandshakeResponse(
                ProtocolVersions.Current,
                MessageKinds.HandshakeResponse,
                "request-01",
                "broker-01",
                accepted: true,
                negotiatedCapabilities: Array.Empty<string>(),
                diagnostic: StableDiagnosticCatalog.Create(StableDiagnosticCodes.MalformedMessage)));
    }

    [TestMethod]
    public void ExactObjectHelper_RejectsUnknownHandshakeFieldBeforeDtoUse()
    {
        var json = Encoding.UTF8.GetBytes(
            "{\"protocolVersion\":\"vfxcomposer.protocol/1.0\",\"messageKind\":\"handshake.request\",\"requestId\":\"r1\",\"clientInstanceId\":\"c1\",\"offeredCapabilities\":[],\"callerPath\":\"C:/untrusted\"}");
        using var document = StrictJsonReader.Parse(json);
        var schema = ProtocolSchemaTestSupport.LoadSchema(WireSchemaIds.HandshakeRequestV1);

        Assert.ThrowsExactly<StrictJsonException>(() =>
            ProtocolSchemaTestSupport.AssertTopLevelExact(document.RootElement, schema));
    }
}
