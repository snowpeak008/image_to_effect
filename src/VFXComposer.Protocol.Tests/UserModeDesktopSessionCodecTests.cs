using System.Buffers.Binary;
using System.Security.Cryptography;
using VFXComposer.Protocol.Ipc;

namespace VFXComposer.Protocol.Tests;

[TestClass]
public sealed class UserModeDesktopSessionCodecTests
{
    private const long Generation = 17;
    private const string PipeName = "vfxcomposer-desktop-0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string SessionId = "desktop-session-17-0123456789abcdef0123456789abcdef";

    [TestMethod]
    public void BootstrapRoundTripCopiesAndRedactsNonce()
    {
        var nonce = Enumerable.Range(0, UserModeDesktopSessionCodec.NonceLength)
            .Select(value => (byte)value)
            .ToArray();
        using var bootstrap = new UserModeDesktopBootstrap(PipeName, Generation, SessionId, nonce);
        var encoded = UserModeDesktopSessionCodec.EncodeBootstrap(bootstrap);
        try
        {
            using var decoded = UserModeDesktopSessionCodec.DecodeBootstrap(encoded);
            var copiedNonce = decoded.CopyNonce();
            try
            {
                Assert.AreEqual(PipeName, decoded.PipeName);
                Assert.AreEqual(Generation, decoded.Generation);
                Assert.AreEqual(SessionId, decoded.SessionId);
                CollectionAssert.AreEqual(nonce, copiedNonce);
                Assert.IsFalse(decoded.ToString().Contains(Convert.ToHexString(nonce), StringComparison.Ordinal));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(copiedNonce);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encoded);
            CryptographicOperations.ZeroMemory(nonce);
        }
    }

    [TestMethod]
    public void ControlRoundTripUsesDefensivePayloadCopies()
    {
        var payload = new byte[] { 1, 2, 3, 4 };
        using var message = new UserModeDesktopControlMessage(
            ProtocolVersions.Current,
            UserModeDesktopControlKinds.ReadResult,
            "desktop-read-001",
            Generation,
            SessionId,
            null,
            "LIBRARY_INDEX",
            "project",
            payload);
        payload[0] = 99;
        var encoded = UserModeDesktopSessionCodec.Encode(message);
        try
        {
            using var decoded = UserModeDesktopSessionCodec.Decode(encoded);
            var copiedPayload = decoded.CopyPayload();
            try
            {
                Assert.AreEqual(UserModeDesktopControlKinds.ReadResult, decoded.MessageKind);
                Assert.AreEqual("desktop-read-001", decoded.RequestId);
                Assert.AreEqual("LIBRARY_INDEX", decoded.DocumentKind);
                Assert.AreEqual("project", decoded.DocumentId);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, copiedPayload);
                copiedPayload[0] = 55;
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, decoded.CopyPayload());
                Assert.IsFalse(decoded.ToString().Contains("01020304", StringComparison.Ordinal));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(copiedPayload);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encoded);
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    [TestMethod]
    public async Task FramingRejectsZeroLengthAndTruncatedPayload()
    {
        var zeroLength = new byte[sizeof(int)];
        await using (var stream = new MemoryStream(zeroLength, writable: false))
        {
            await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
                await UserModeDesktopSessionCodec.ReadFrameAsync(stream));
        }

        var truncated = new byte[sizeof(int) + 1];
        BinaryPrimitives.WriteInt32BigEndian(truncated, 2);
        truncated[^1] = 0x5a;
        await using var truncatedStream = new MemoryStream(truncated, writable: false);
        await Assert.ThrowsExactlyAsync<EndOfStreamException>(async () =>
            await UserModeDesktopSessionCodec.ReadFrameAsync(truncatedStream));
    }

    [TestMethod]
    public void ShapeRejectsCrossSessionAndUnknownMessageKinds()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new UserModeDesktopControlMessage(
            ProtocolVersions.Current,
            "desktop.unknown",
            "request-001",
            Generation,
            SessionId,
            null,
            null,
            null,
            []));
        Assert.ThrowsExactly<ArgumentException>(() => new UserModeDesktopControlMessage(
            ProtocolVersions.Current,
            UserModeDesktopControlKinds.Select,
            "request-002",
            Generation,
            "desktop-session-18-0123456789abcdef0123456789abcdef",
            "selection-token",
            null,
            null,
            []));
    }
}
