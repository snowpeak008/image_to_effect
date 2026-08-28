using System.Buffers.Binary;
using System.Text;
using VFXComposer.Protocol.Queries;

namespace VFXComposer.Protocol.Ipc;

public static class UserModeDesktopControlKinds
{
    public const string Hello = "desktop.hello";
    public const string Select = "desktop.select";
    public const string SelectAccepted = "desktop.select.accepted";
    public const string Read = "desktop.read";
    public const string ReadResult = "desktop.read.result";

    internal static bool IsKnown(string value) => value is
        Hello or Select or SelectAccepted or Read or ReadResult;
}

public sealed class UserModeDesktopBootstrap : IDisposable
{
    private byte[] _nonce;

    public UserModeDesktopBootstrap(string pipeName, long generation, string sessionId, byte[] nonce)
    {
        if (!UserModeDesktopSessionCodec.IsCanonicalPipeName(pipeName) || generation <= 0 ||
            !UserModeDesktopSessionCodec.IsCanonicalSessionId(sessionId, generation) ||
            nonce is null || nonce.Length != UserModeDesktopSessionCodec.NonceLength)
        {
            throw new ArgumentException("U4FS001");
        }

        PipeName = pipeName;
        Generation = generation;
        SessionId = sessionId;
        _nonce = nonce.ToArray();
    }

    public string PipeName { get; }
    public long Generation { get; }
    public string SessionId { get; }
    public byte[] CopyNonce() => _nonce.ToArray();
    public override string ToString() => "UserModeDesktopBootstrap(REDACTED)";

    public void Dispose()
    {
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(_nonce);
        _nonce = [];
    }
}

public sealed class UserModeDesktopControlMessage : IDisposable
{
    private byte[] _payload;

    public UserModeDesktopControlMessage(
        string protocolVersion,
        string messageKind,
        string requestId,
        long generation,
        string sessionId,
        string? selection,
        string? documentKind,
        string? documentId,
        byte[] payload)
    {
        if (!string.Equals(protocolVersion, ProtocolVersions.Current, StringComparison.Ordinal) ||
            !UserModeDesktopControlKinds.IsKnown(messageKind) || generation <= 0 ||
            !UserModeDesktopSessionCodec.IsCanonicalSessionId(sessionId, generation))
        {
            throw new ArgumentException("U4FS001");
        }

        ProtocolVersion = protocolVersion;
        MessageKind = messageKind;
        RequestId = Guard.Token(requestId, nameof(requestId));
        Generation = generation;
        SessionId = sessionId;
        Selection = selection;
        DocumentKind = documentKind;
        DocumentId = documentId;
        _payload = payload?.ToArray() ?? throw new ArgumentNullException(nameof(payload));
        ValidateShape();
    }

    public string ProtocolVersion { get; }
    public string MessageKind { get; }
    public string RequestId { get; }
    public long Generation { get; }
    public string SessionId { get; }
    public string? Selection { get; }
    public string? DocumentKind { get; }
    public string? DocumentId { get; }
    public byte[] CopyPayload() => Volatile.Read(ref _payload).ToArray();

    public override string ToString() =>
        $"UserModeDesktopControlMessage(Kind={MessageKind},Generation={Generation},Payload=REDACTED)";

    private void ValidateShape()
    {
        if (_payload.Length > UserModeDesktopSessionCodec.MaximumPayloadLength)
        {
            throw new ArgumentOutOfRangeException("payload");
        }

        var valid = MessageKind switch
        {
            UserModeDesktopControlKinds.Hello =>
                string.Equals(RequestId, "hello", StringComparison.Ordinal) &&
                Selection is null && DocumentKind is null && DocumentId is null &&
                _payload.Length == UserModeDesktopSessionCodec.NonceLength,
            UserModeDesktopControlKinds.Select =>
                !string.IsNullOrWhiteSpace(Selection) && Selection.Length <= 32767 &&
                DocumentKind is null && DocumentId is null && _payload.Length == 0,
            UserModeDesktopControlKinds.SelectAccepted =>
                Selection is null && DocumentKind is null && DocumentId is null && _payload.Length == 0,
            UserModeDesktopControlKinds.Read =>
                Selection is null && IsDocumentShapeValid() && _payload.Length == 0,
            UserModeDesktopControlKinds.ReadResult =>
                Selection is null && IsDocumentShapeValid() && _payload.Length > 0,
            _ => false,
        };
        if (!valid)
        {
            throw new ArgumentException("U4FS001");
        }
    }

    private bool IsDocumentShapeValid()
    {
        try
        {
            _ = DocumentKinds.Require(DocumentKind!, nameof(DocumentKind));
            _ = DocumentKinds.RequireDocumentId(DocumentKind!, DocumentId!, nameof(DocumentId));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        var payload = Interlocked.Exchange(ref _payload, []);
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(payload);
        GC.SuppressFinalize(this);
    }
}

public static class UserModeDesktopSessionCodec
{
    public const int NonceLength = 32;
    public const int MaximumPayloadLength = 1024 * 1024;
    private const int MaximumTextLength = 32767;
    private static ReadOnlySpan<byte> BootstrapMagic => "UDB1"u8;
    private static ReadOnlySpan<byte> MessageMagic => "UDM1"u8;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static byte[] EncodeBootstrap(UserModeDesktopBootstrap bootstrap)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        using var nonce = new ZeroingBuffer(bootstrap.CopyNonce());
        return EncodeFields(BootstrapMagic, bootstrap.Generation,
            [bootstrap.PipeName, bootstrap.SessionId], nonce.Bytes);
    }

    public static UserModeDesktopBootstrap DecodeBootstrap(ReadOnlySpan<byte> bytes)
    {
        var decoded = DecodeFields(bytes, BootstrapMagic, 2, NonceLength);
        try
        {
            return new UserModeDesktopBootstrap(
                decoded.Text[0] ?? throw new InvalidDataException("U4FS001"),
                decoded.Generation,
                decoded.Text[1] ?? throw new InvalidDataException("U4FS001"),
                decoded.Payload);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(decoded.Payload);
        }
    }

    public static byte[] Encode(UserModeDesktopControlMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var payload = message.CopyPayload();
        try
        {
            return EncodeFields(MessageMagic, message.Generation,
                [message.ProtocolVersion, message.MessageKind, message.RequestId, message.SessionId,
                 message.Selection, message.DocumentKind, message.DocumentId], payload);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(payload);
        }
    }

    public static UserModeDesktopControlMessage Decode(ReadOnlySpan<byte> bytes)
    {
        var decoded = DecodeFields(bytes, MessageMagic, 7, MaximumPayloadLength);
        try
        {
            return new UserModeDesktopControlMessage(
                decoded.Text[0]!, decoded.Text[1]!, decoded.Text[2]!, decoded.Generation,
                decoded.Text[3]!, decoded.Text[4], decoded.Text[5], decoded.Text[6], decoded.Payload);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(decoded.Payload);
        }
    }

    public static async ValueTask WriteFrameAsync(
        Stream destination,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (payload.Length is <= 0 or > MaximumPayloadLength)
        {
            throw new InvalidDataException("U4FS001");
        }

        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
        await destination.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await destination.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<byte[]> ReadFrameAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var header = new byte[4];
        await ReadExactlyAsync(source, header, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length is <= 0 or > MaximumPayloadLength)
        {
            throw new InvalidDataException("U4FS001");
        }

        var payload = new byte[length];
        await ReadExactlyAsync(source, payload, cancellationToken).ConfigureAwait(false);
        return payload;
    }

    public static bool IsCanonicalPipeName(string value) =>
        value is not null && value.StartsWith("vfxcomposer-desktop-", StringComparison.Ordinal) &&
        value.Length == 84 && value[20..].All(IsLowerHex);

    public static bool IsCanonicalSessionId(string value, long generation)
    {
        var prefix = "desktop-session-" + generation.ToString(
            System.Globalization.CultureInfo.InvariantCulture) + "-";
        return value is not null && value.StartsWith(prefix, StringComparison.Ordinal) &&
            value.Length == prefix.Length + 32 && value[prefix.Length..].All(IsLowerHex);
    }

    private static byte[] EncodeFields(
        ReadOnlySpan<byte> magic,
        long generation,
        IReadOnlyList<string?> text,
        ReadOnlySpan<byte> payload)
    {
        if (generation <= 0 || payload.Length > MaximumPayloadLength)
        {
            throw new ArgumentException("U4FS001");
        }

        var encoded = text.Select(value => value is null ? null : StrictUtf8.GetBytes(value)).ToArray();
        if (encoded.Any(value => value?.Length > MaximumTextLength))
        {
            throw new ArgumentOutOfRangeException(nameof(text));
        }

        var length = checked(4 + 8 + encoded.Sum(value => 4 + (value?.Length ?? 0)) + 4 + payload.Length);
        var result = new byte[length];
        magic.CopyTo(result);
        BinaryPrimitives.WriteInt64BigEndian(result.AsSpan(4), generation);
        var offset = 12;
        foreach (var value in encoded)
        {
            BinaryPrimitives.WriteInt32BigEndian(result.AsSpan(offset), value?.Length ?? -1);
            offset += 4;
            if (value is not null)
            {
                value.CopyTo(result, offset);
                offset += value.Length;
            }
        }

        BinaryPrimitives.WriteInt32BigEndian(result.AsSpan(offset), payload.Length);
        payload.CopyTo(result.AsSpan(offset + 4));
        return result;
    }

    private static DecodedFields DecodeFields(
        ReadOnlySpan<byte> bytes,
        ReadOnlySpan<byte> magic,
        int textCount,
        int requiredOrMaximumPayload)
    {
        if (bytes.Length < 16 || !bytes[..4].SequenceEqual(magic))
        {
            throw new InvalidDataException("U4FS001");
        }

        var generation = BinaryPrimitives.ReadInt64BigEndian(bytes[4..12]);
        if (generation <= 0)
        {
            throw new InvalidDataException("U4FS001");
        }

        var text = new string?[textCount];
        var offset = 12;
        for (var index = 0; index < textCount; index++)
        {
            if (bytes.Length - offset < 4)
            {
                throw new InvalidDataException("U4FS001");
            }

            var length = BinaryPrimitives.ReadInt32BigEndian(bytes[offset..]);
            offset += 4;
            if (length < -1 || length > MaximumTextLength || bytes.Length - offset < Math.Max(0, length))
            {
                throw new InvalidDataException("U4FS001");
            }

            if (length >= 0)
            {
                try
                {
                    text[index] = StrictUtf8.GetString(bytes.Slice(offset, length));
                }
                catch (DecoderFallbackException exception)
                {
                    throw new InvalidDataException("U4FS001", exception);
                }

                offset += length;
            }
        }

        if (bytes.Length - offset < 4)
        {
            throw new InvalidDataException("U4FS001");
        }

        var payloadLength = BinaryPrimitives.ReadInt32BigEndian(bytes[offset..]);
        offset += 4;
        if (payloadLength < 0 || payloadLength > MaximumPayloadLength || bytes.Length - offset != payloadLength ||
            (requiredOrMaximumPayload == NonceLength && payloadLength != NonceLength))
        {
            throw new InvalidDataException("U4FS001");
        }

        return new DecodedFields(generation, text, bytes[offset..].ToArray());
    }

    private static async ValueTask ReadExactlyAsync(
        Stream stream,
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < destination.Length)
        {
            var read = await stream.ReadAsync(destination[offset..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("U4FS001");
            }

            offset += read;
        }
    }

    private static bool IsLowerHex(char value) => value is >= '0' and <= '9' or >= 'a' and <= 'f';

    private sealed record DecodedFields(long Generation, string?[] Text, byte[] Payload);

    private sealed class ZeroingBuffer(byte[] bytes) : IDisposable
    {
        internal byte[] Bytes { get; } = bytes;
        public void Dispose() => System.Security.Cryptography.CryptographicOperations.ZeroMemory(Bytes);
    }
}
