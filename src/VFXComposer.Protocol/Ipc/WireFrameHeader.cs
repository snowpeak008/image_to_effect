using System.Buffers.Binary;

namespace VFXComposer.Protocol.Ipc;

/// <summary>Exact length-prefixed local-pipe framing. It performs no I/O.</summary>
public static class WireFrameHeader
{
    public const int HeaderLength = 10;
    public const int MaximumPayloadLength = 1024 * 1024;
    private static ReadOnlySpan<byte> Magic => "VFXC"u8;

    public static void Write(Span<byte> destination, int payloadLength)
    {
        if (destination.Length < HeaderLength)
        {
            throw new ArgumentException("Frame header destination is too small.", nameof(destination));
        }

        if (payloadLength <= 0 || payloadLength > MaximumPayloadLength)
        {
            throw new ArgumentOutOfRangeException(nameof(payloadLength));
        }

        Magic.CopyTo(destination);
        destination[4] = 1;
        destination[5] = 0;
        BinaryPrimitives.WriteUInt32BigEndian(destination[6..10], checked((uint)payloadLength));
    }

    public static int Read(ReadOnlySpan<byte> source)
    {
        if (source.Length != HeaderLength ||
            !source[..4].SequenceEqual(Magic) ||
            source[4] != 1 ||
            source[5] != 0)
        {
            throw new ArgumentException("Frame header is invalid.", nameof(source));
        }

        var payloadLength = BinaryPrimitives.ReadUInt32BigEndian(source[6..10]);
        if (payloadLength is 0 or > MaximumPayloadLength)
        {
            throw new ArgumentException("Frame payload length is invalid.", nameof(source));
        }

        return checked((int)payloadLength);
    }
}
