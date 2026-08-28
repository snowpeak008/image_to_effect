using System.Buffers.Binary;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using VFXComposer.Broker.Security;
using VFXComposer.Protocol.Ipc;

namespace VFXComposer.Broker.Ipc;

/// <summary>One-use secret bootstrap material. It is never formatted for diagnostics.</summary>
[SupportedOSPlatform("windows")]
internal sealed class UserModeWorkerBootstrap : IDisposable
{
    private byte[] _nonce;
    private int _disposed;

    internal UserModeWorkerBootstrap(string pipeName, long generation, string sessionId, byte[] nonce)
    {
        if (generation <= 0 ||
            !UserModeNamedPipeServer.IsCanonicalPipeName(pipeName) ||
            !UserModeNamedPipeServer.IsCanonicalSessionId(sessionId, generation) ||
            nonce is null || nonce.Length != UserModeNamedPipeServer.NonceLength)
        {
            throw new ArgumentException("U2FS001");
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

internal sealed class UserModeWorkerConnection : IAsyncDisposable
{
    private NamedPipeServerStream? _pipe;

    internal UserModeWorkerConnection(
        NamedPipeServerStream pipe,
        long generation,
        string sessionId,
        int processId,
        string processEpoch)
    {
        _pipe = pipe;
        Generation = generation;
        SessionId = sessionId;
        ProcessId = processId;
        ProcessEpoch = processEpoch;
    }

    internal long Generation { get; }

    internal string SessionId { get; }

    internal int ProcessId { get; }

    internal string ProcessEpoch { get; }

    internal Stream Stream => Volatile.Read(ref _pipe) ?? throw new ObjectDisposedException(nameof(UserModeWorkerConnection));

    internal bool IsConnected => Volatile.Read(ref _pipe)?.IsConnected == true;

    public async ValueTask DisposeAsync()
    {
        var pipe = Interlocked.Exchange(ref _pipe, null);
        if (pipe is not null)
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
        }
    }
}

/// <summary>
/// A single-use, random-name local pipe. CurrentUserOnly is the cross-user
/// boundary; nonce and process correlation do not claim same-user resistance.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class UserModeNamedPipeServer : IAsyncDisposable, IDisposable
{
    internal const int NonceLength = 32;
    private const int MaximumTextLength = 256;
    private static ReadOnlySpan<byte> BootstrapMagic => "UMB1"u8;
    private static ReadOnlySpan<byte> HelloMagic => "UMH1"u8;

    private readonly NamedPipeServerStream _pipe;
    private readonly byte[] _nonce;
    private int _bootstrapIssued;
    private int _acceptStarted;
    private int _disposed;
    private bool _pipeTransferred;

    private UserModeNamedPipeServer(
        NamedPipeServerStream pipe,
        string pipeName,
        long generation,
        string sessionId,
        string currentUserSid,
        byte[] nonce)
    {
        _pipe = pipe;
        PipeName = pipeName;
        Generation = generation;
        SessionId = sessionId;
        CurrentUserSid = currentUserSid;
        _nonce = nonce;
    }

    internal string PipeName { get; }

    internal long Generation { get; }

    internal string SessionId { get; }

    internal string CurrentUserSid { get; }

    internal bool UsesCurrentUserOnly => true;

    internal bool IsConsumed => Volatile.Read(ref _acceptStarted) != 0;

    internal static UserModeNamedPipeServer Create(long generation)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException();
        }

        if (generation <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(generation));
        }

        var sid = WindowsIdentity.GetCurrent().User?.Value;
        if (string.IsNullOrWhiteSpace(sid))
        {
            throw new InvalidOperationException("U2FS001");
        }

        var pipeName = "vfxcomposer-um-" + RandomHex(32);
        var sessionId = string.Concat(
            "um-session-",
            generation.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "-",
            RandomHex(16));
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
            4096,
            4096);
        return new UserModeNamedPipeServer(pipe, pipeName, generation, sessionId, sid, nonce);
    }

    internal UserModeWorkerBootstrap CreateBootstrap()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (IsConsumed || Interlocked.CompareExchange(ref _bootstrapIssued, 1, 0) != 0)
        {
            throw new InvalidOperationException("U2FS001");
        }

        return new UserModeWorkerBootstrap(PipeName, Generation, SessionId, _nonce);
    }

    internal async Task WriteBootstrapAsync(
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        using var bootstrap = CreateBootstrap();
        var payload = EncodeBootstrap(bootstrap);
        try
        {
            await NamedPipeBrokerHost.WriteFrameAsync(destination, payload, cancellationToken)
                .ConfigureAwait(false);
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    internal async Task<UserModeWorkerConnection> AcceptAsync(
        UserModeChildProcess child,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (timeout <= TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Interlocked.CompareExchange(ref _acceptStarted, 1, 0) != 0)
        {
            throw new InvalidOperationException("U2FS001");
        }

        using var timeoutSource = timeout == Timeout.InfiniteTimeSpan
            ? null
            : new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource?.Token ?? CancellationToken.None);
        try
        {
            await _pipe.WaitForConnectionAsync(linked.Token).ConfigureAwait(false);
            if (!GetNamedPipeClientProcessId(_pipe.SafePipeHandle, out var observedProcessId) ||
                observedProcessId > int.MaxValue ||
                observedProcessId != (uint)child.ProcessId ||
                !child.IsExactProcessActive)
            {
                throw new InvalidDataException("U2FS001");
            }

            var payload = await NamedPipeBrokerHost.ReadFrameAsync(_pipe, linked.Token)
                .ConfigureAwait(false);
            try
            {
                var hello = DecodeHello(payload);
                try
                {
                    if (hello.Generation != Generation ||
                        !string.Equals(hello.SessionId, SessionId, StringComparison.Ordinal) ||
                        !child.Matches(hello.ProcessId, hello.ProcessEpoch) ||
                        !CryptographicOperations.FixedTimeEquals(hello.Nonce, _nonce))
                    {
                        throw new InvalidDataException("U2FS001");
                    }

                    _pipeTransferred = true;
                    return new UserModeWorkerConnection(
                        _pipe,
                        Generation,
                        SessionId,
                        child.ProcessId,
                        child.ProcessEpoch);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(hello.Nonce);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(payload);
            }
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested && timeoutSource?.IsCancellationRequested == true)
        {
            throw new TimeoutException("U2FS001");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(_nonce);
            if (!_pipeTransferred)
            {
                await _pipe.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    internal static async Task<UserModeWorkerBootstrap> ReadBootstrapAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var payload = await NamedPipeBrokerHost.ReadFrameAsync(source, cancellationToken)
            .ConfigureAwait(false);
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
        if (processId <= 0 || !ProcessEpoch.IsCanonicalForProcess(processId, processEpoch))
        {
            throw new ArgumentException("U2FS001");
        }

        var payload = EncodeHello(bootstrap, processId, processEpoch);
        try
        {
            await NamedPipeBrokerHost.WriteFrameAsync(destination, payload, cancellationToken)
                .ConfigureAwait(false);
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    internal static byte[] EncodeBootstrap(UserModeWorkerBootstrap bootstrap)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        var pipeBytes = StrictUtf8(bootstrap.PipeName);
        var sessionBytes = StrictUtf8(bootstrap.SessionId);
        using var nonce = new ZeroingBytes(bootstrap.CopyNonce());
        var payload = new byte[checked(4 + 8 + 2 + pipeBytes.Length + 2 + sessionBytes.Length + NonceLength)];
        var offset = 0;
        BootstrapMagic.CopyTo(payload);
        offset += 4;
        BinaryPrimitives.WriteInt64BigEndian(payload.AsSpan(offset, 8), bootstrap.Generation);
        offset += 8;
        offset = WriteText(payload, offset, pipeBytes);
        offset = WriteText(payload, offset, sessionBytes);
        nonce.Bytes.CopyTo(payload, offset);
        return payload;
    }

    private static UserModeWorkerBootstrap DecodeBootstrap(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 4 + 8 + 2 + 2 + NonceLength ||
            !payload[..4].SequenceEqual(BootstrapMagic))
        {
            throw new InvalidDataException("U2FS001");
        }

        var generation = BinaryPrimitives.ReadInt64BigEndian(payload[4..12]);
        var offset = 12;
        var pipeName = ReadText(payload, ref offset);
        var sessionId = ReadText(payload, ref offset);
        if (generation <= 0 || payload.Length - offset != NonceLength)
        {
            throw new InvalidDataException("U2FS001");
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
        var sessionBytes = StrictUtf8(bootstrap.SessionId);
        var epochBytes = StrictUtf8(processEpoch);
        using var nonce = new ZeroingBytes(bootstrap.CopyNonce());
        var payload = new byte[checked(4 + 8 + 2 + sessionBytes.Length + 4 + 2 + epochBytes.Length + NonceLength)];
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

    private static DecodedHello DecodeHello(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 4 + 8 + 2 + 4 + 2 + NonceLength ||
            !payload[..4].SequenceEqual(HelloMagic))
        {
            throw new InvalidDataException("U2FS001");
        }

        var generation = BinaryPrimitives.ReadInt64BigEndian(payload[4..12]);
        var offset = 12;
        var sessionId = ReadText(payload, ref offset);
        if (payload.Length - offset < 4)
        {
            throw new InvalidDataException("U2FS001");
        }

        var processId = BinaryPrimitives.ReadInt32BigEndian(payload.Slice(offset, 4));
        offset += 4;
        var processEpoch = ReadText(payload, ref offset);
        if (generation <= 0 || processId <= 0 ||
            !ProcessEpoch.IsCanonicalForProcess(processId, processEpoch) ||
            payload.Length - offset != NonceLength)
        {
            throw new InvalidDataException("U2FS001");
        }

        return new DecodedHello(
            generation,
            sessionId,
            processId,
            processEpoch,
            payload[offset..].ToArray());
    }

    private static int WriteText(byte[] destination, int offset, byte[] value)
    {
        if (value.Length is 0 or > MaximumTextLength)
        {
            throw new InvalidDataException("U2FS001");
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
            throw new InvalidDataException("U2FS001");
        }

        var length = BinaryPrimitives.ReadUInt16BigEndian(source.Slice(offset, 2));
        offset += 2;
        if (length is 0 or > MaximumTextLength || source.Length - offset < length)
        {
            throw new InvalidDataException("U2FS001");
        }

        string value;
        try
        {
            value = new UTF8Encoding(false, true).GetString(source.Slice(offset, length));
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("U2FS001", exception);
        }

        if (!StrictUtf8(value).AsSpan().SequenceEqual(source.Slice(offset, length)))
        {
            throw new InvalidDataException("U2FS001");
        }

        offset += length;
        return value;
    }

    private static byte[] StrictUtf8(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidDataException("U2FS001");
        }

        return new UTF8Encoding(false, true).GetBytes(value);
    }

    private static string RandomHex(int byteCount) =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(byteCount)).ToLowerInvariant();

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

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(_nonce);
        if (!_pipeTransferred)
        {
            await _pipe.DisposeAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public override string ToString() => "UserModeNamedPipeServer(REDACTED)";

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(
        Microsoft.Win32.SafeHandles.SafePipeHandle pipe,
        out uint clientProcessId);

    private sealed record DecodedHello(
        long Generation,
        string SessionId,
        int ProcessId,
        string ProcessEpoch,
        byte[] Nonce);

    private sealed class ZeroingBytes : IDisposable
    {
        internal ZeroingBytes(byte[] bytes) => Bytes = bytes;

        internal byte[] Bytes { get; }

        public void Dispose() => CryptographicOperations.ZeroMemory(Bytes);
    }
}
