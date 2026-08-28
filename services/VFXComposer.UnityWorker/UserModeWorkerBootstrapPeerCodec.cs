using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using VFXComposer.Protocol.Ipc;

namespace VFXComposer.UnityWorker;

/// <summary>
/// The minimal private U2 bootstrap peer ABI retained by the standalone Worker.
/// It deliberately mirrors the Broker's UMB1/UMH1 bytes and VFXC framing, while
/// keeping the C2 project-locator contract in Protocol.
/// </summary>
internal static class UserModeWorkerBootstrapPeerCodec
{
    internal const int NonceLength = 32;
    private const int MaximumTextLength = 256;
    private const string Failure = "U5FS001";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static ReadOnlySpan<byte> BootstrapMagic => "UMB1"u8;
    private static ReadOnlySpan<byte> HelloMagic => "UMH1"u8;

    internal static async Task<UserModeWorkerBootstrap> ReadBootstrapAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var payload = await ReadFrameAsync(source, cancellationToken).ConfigureAwait(false);
        try
        {
            return DecodeBootstrap(payload);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    internal static async Task WriteHelloAsync(
        Stream destination,
        UserModeWorkerBootstrap bootstrap,
        int processId,
        string processEpoch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(bootstrap);
        if (processId <= 0 || !IsCanonicalProcessEpoch(processId, processEpoch))
        {
            throw new InvalidDataException(Failure);
        }

        var payload = EncodeHello(bootstrap, processId, processEpoch);
        try
        {
            await WriteFrameAsync(destination, payload, cancellationToken).ConfigureAwait(false);
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    internal static async ValueTask<byte[]> ReadFrameAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var header = new byte[WireFrameHeader.HeaderLength];
        await ReadExactlyAsync(source, header, cancellationToken).ConfigureAwait(false);
        int payloadLength;
        try
        {
            payloadLength = WireFrameHeader.Read(header);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(Failure, exception);
        }

        var payload = new byte[payloadLength];
        await ReadExactlyAsync(source, payload, cancellationToken).ConfigureAwait(false);
        return payload;
    }

    internal static async ValueTask WriteFrameAsync(
        Stream destination,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (payload.Length is <= 0 or > WireFrameHeader.MaximumPayloadLength)
        {
            throw new ArgumentOutOfRangeException(nameof(payload));
        }

        var header = new byte[WireFrameHeader.HeaderLength];
        WireFrameHeader.Write(header, payload.Length);
        await destination.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await destination.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    internal static string ObserveCurrentProcessEpoch()
    {
        using var process = Process.GetCurrentProcess();
        if (process.Id <= 0 ||
            !GetProcessTimes(process.SafeHandle, out var creation, out _, out _, out _))
        {
            throw new InvalidDataException(Failure);
        }

        var creationTicks = ((ulong)(uint)creation.High << 32) | (uint)creation.Low;
        return $"winproc-{process.Id}-{creationTicks:x16}";
    }

    internal static bool IsCanonicalPipeName(string? value) =>
        value is not null &&
        value.StartsWith("vfxcomposer-um-", StringComparison.Ordinal) &&
        value.Length == "vfxcomposer-um-".Length + 64 &&
        IsLowerHex(value.AsSpan("vfxcomposer-um-".Length));

    internal static bool IsCanonicalSessionId(string? value, long generation)
    {
        if (value is null || generation <= 0)
        {
            return false;
        }

        var prefix = string.Concat(
            "um-session-",
            generation.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "-");
        return value.StartsWith(prefix, StringComparison.Ordinal) &&
            value.Length == prefix.Length + 32 &&
            IsLowerHex(value.AsSpan(prefix.Length));
    }

    internal static bool IsCanonicalProcessEpoch(int processId, string? processEpoch)
    {
        if (processId <= 0 || processEpoch is null)
        {
            return false;
        }

        var prefix = $"winproc-{processId}-";
        return processEpoch.StartsWith(prefix, StringComparison.Ordinal) &&
            processEpoch.Length == prefix.Length + 16 &&
            IsLowerHex(processEpoch.AsSpan(prefix.Length));
    }

    private static UserModeWorkerBootstrap DecodeBootstrap(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 4 + 8 + 2 + 2 + NonceLength ||
            !payload[..4].SequenceEqual(BootstrapMagic))
        {
            throw new InvalidDataException(Failure);
        }

        var generation = BinaryPrimitives.ReadInt64BigEndian(payload[4..12]);
        var offset = 12;
        var pipeName = ReadText(payload, ref offset);
        var sessionId = ReadText(payload, ref offset);
        if (generation <= 0 || payload.Length - offset != NonceLength)
        {
            throw new InvalidDataException(Failure);
        }

        var nonce = payload[offset..].ToArray();
        try
        {
            return new UserModeWorkerBootstrap(pipeName, generation, sessionId, nonce);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(nonce);
        }
    }

    private static byte[] EncodeHello(
        UserModeWorkerBootstrap bootstrap,
        int processId,
        string processEpoch)
    {
        var sessionBytes = StrictUtf8.GetBytes(bootstrap.SessionId);
        var epochBytes = StrictUtf8.GetBytes(processEpoch);
        using var nonce = new ZeroingBytes(bootstrap.CopyNonce());
        var payload = new byte[checked(
            4 + 8 + 2 + sessionBytes.Length + 4 + 2 + epochBytes.Length + NonceLength)];
        var offset = 0;
        HelloMagic.CopyTo(payload);
        offset += 4;
        BinaryPrimitives.WriteInt64BigEndian(payload.AsSpan(offset, 8), bootstrap.Generation);
        offset += 8;
        offset = WriteText(payload, offset, sessionBytes);
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(offset, 4), processId);
        offset += 4;
        offset = WriteText(payload, offset, epochBytes);
        nonce.Bytes.CopyTo(payload, offset);
        return payload;
    }

    private static int WriteText(byte[] destination, int offset, byte[] value)
    {
        if (value.Length is 0 or > MaximumTextLength)
        {
            throw new InvalidDataException(Failure);
        }

        BinaryPrimitives.WriteUInt16BigEndian(destination.AsSpan(offset, 2), checked((ushort)value.Length));
        offset += 2;
        value.CopyTo(destination, offset);
        return offset + value.Length;
    }

    private static string ReadText(ReadOnlySpan<byte> source, ref int offset)
    {
        if (source.Length - offset < 2)
        {
            throw new InvalidDataException(Failure);
        }

        var length = BinaryPrimitives.ReadUInt16BigEndian(source.Slice(offset, 2));
        offset += 2;
        if (length is 0 or > MaximumTextLength || source.Length - offset < length)
        {
            throw new InvalidDataException(Failure);
        }

        string value;
        try
        {
            value = StrictUtf8.GetString(source.Slice(offset, length));
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException(Failure, exception);
        }

        if (!StrictUtf8.GetBytes(value).AsSpan().SequenceEqual(source.Slice(offset, length)))
        {
            throw new InvalidDataException(Failure);
        }

        offset += length;
        return value;
    }

    private static async ValueTask ReadExactlyAsync(
        Stream source,
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < destination.Length)
        {
            var read = await source.ReadAsync(destination[offset..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException(Failure);
            }

            offset += read;
        }
    }

    private static bool IsLowerHex(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileTime
    {
        public int Low;
        public int High;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(
        SafeProcessHandle process,
        out NativeFileTime creationTime,
        out NativeFileTime exitTime,
        out NativeFileTime kernelTime,
        out NativeFileTime userTime);

    private sealed class ZeroingBytes(byte[] bytes) : IDisposable
    {
        internal byte[] Bytes { get; } = bytes;

        public void Dispose() => CryptographicOperations.ZeroMemory(Bytes);
    }
}

/// <summary>One-use bootstrap secret that is never emitted in diagnostics.</summary>
internal sealed class UserModeWorkerBootstrap : IDisposable
{
    private byte[] _nonce;
    private int _disposed;

    internal UserModeWorkerBootstrap(string pipeName, long generation, string sessionId, byte[] nonce)
    {
        if (generation <= 0 ||
            !UserModeWorkerBootstrapPeerCodec.IsCanonicalPipeName(pipeName) ||
            !UserModeWorkerBootstrapPeerCodec.IsCanonicalSessionId(sessionId, generation) ||
            nonce is null || nonce.Length != UserModeWorkerBootstrapPeerCodec.NonceLength)
        {
            throw new ArgumentException("U5FS001");
        }

        PipeName = pipeName;
        Generation = generation;
        SessionId = sessionId;
        _nonce = nonce.ToArray();
    }

    internal string PipeName { get; }

    internal long Generation { get; }

    internal string SessionId { get; }

    internal byte[] CopyNonce()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _nonce.ToArray();
    }

    public override string ToString() => "UserModeWorkerBootstrap(REDACTED)";

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            CryptographicOperations.ZeroMemory(_nonce);
            _nonce = [];
        }
    }
}
