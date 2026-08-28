using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using VFXComposer.Protocol;
using VFXComposer.Protocol.Diagnostics;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;
using VFXComposer.Protocol.Queries;
using VFXComposer.Protocol.Registration;

namespace VFXComposer.Client.Tests;

[TestClass]
public sealed class UserModeDesktopSessionTests
{
    [TestMethod]
    public async Task ScriptedControlPeerTransitionsFromConnectToSelectedAndRead()
    {
        var host = new ScriptedBrokerHost(RespondNormally, generation: 1);
        await using var session = new UserModeDesktopSession((_, _) => ValueTask.FromResult<IUserModeBrokerProcessHost>(host));

        await session.ConnectAsync();
        await session.SelectAsync("selection-token");
        var presentation = await session.ReadAsync();

        Assert.AreEqual(UserModeDesktopSessionState.Selected, session.State);
        Assert.AreEqual(1L, session.Generation);
        Assert.IsFalse(presentation.Accepted);
        Assert.AreEqual(DocumentKinds.LibraryIndex, presentation.DocumentKind);
        Assert.AreEqual("project", presentation.DocumentId);
        Assert.AreEqual(StableDiagnosticCodes.ProjectDocumentUnavailable, presentation.DiagnosticCode);
        Assert.AreEqual(presentation, session.LastRead);
    }

    [TestMethod]
    public async Task CorrelationFailureEntersRecoveryAndRestartDisposesPriorControlHost()
    {
        var first = new ScriptedBrokerHost(
            request => new UserModeDesktopControlMessage(
                ProtocolVersions.Current,
                UserModeDesktopControlKinds.SelectAccepted,
                "different-request",
                request.Generation,
                request.SessionId,
                null,
                null,
                null,
                []),
            generation: 1);
        var second = new ScriptedBrokerHost(RespondNormally, generation: 2);
        var starts = 0;
        await using var session = new UserModeDesktopSession((_, _) =>
            ValueTask.FromResult<IUserModeBrokerProcessHost>(++starts == 1 ? first : second));

        await session.ConnectAsync();
        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await session.SelectAsync("selection-token"));

        Assert.AreEqual(UserModeDesktopSessionState.RecoveryRequired, session.State);
        Assert.AreEqual(1, first.DisposeCount);
        Assert.IsNull(session.LastRead);

        await session.RestartAsync();

        Assert.AreEqual(2L, session.Generation);
        Assert.AreEqual(UserModeDesktopSessionState.ConnectedNoProject, session.State);
        Assert.AreEqual(2, starts);
        Assert.AreEqual(1, first.DisposeCount);
    }

    private static UserModeDesktopControlMessage RespondNormally(UserModeDesktopControlMessage request)
    {
        if (string.Equals(request.MessageKind, UserModeDesktopControlKinds.Select, StringComparison.Ordinal))
        {
            return new UserModeDesktopControlMessage(
                ProtocolVersions.Current,
                UserModeDesktopControlKinds.SelectAccepted,
                request.RequestId,
                request.Generation,
                request.SessionId,
                null,
                null,
                null,
                []);
        }

        if (string.Equals(request.MessageKind, UserModeDesktopControlKinds.Read, StringComparison.Ordinal))
        {
            var result = new ReadDocumentResult(
                ProtocolVersions.Current,
                MessageKinds.ReadDocumentResult,
                "scripted-read-result",
                accepted: false,
                TypedHash.ComputeUtf8(ProjectRegistrationAttestation.ProjectIdentityType, "scripted-project"),
                request.DocumentKind!,
                request.DocumentId!,
                contentHash: null,
                byteLength: 0,
                contentBase64: null,
                StableDiagnosticCatalog.Create(StableDiagnosticCodes.ProjectDocumentUnavailable));
            var payload = JsonSerializer.SerializeToUtf8Bytes(result);
            try
            {
                return new UserModeDesktopControlMessage(
                    ProtocolVersions.Current,
                    UserModeDesktopControlKinds.ReadResult,
                    request.RequestId,
                    request.Generation,
                    request.SessionId,
                    null,
                    request.DocumentKind,
                    request.DocumentId,
                    payload);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(payload);
            }
        }

        throw new InvalidDataException("U4FS001");
    }

    private sealed class ScriptedBrokerHost(
        Func<UserModeDesktopControlMessage, UserModeDesktopControlMessage> responder,
        long generation) : IUserModeBrokerProcessHost
    {
        private readonly ScriptedControlStream _transport = new(responder);
        private int _disposed;

        public int DisposeCount { get; private set; }
        public Stream Transport => _transport;
        public bool IsActive => Volatile.Read(ref _disposed) == 0;
        public string SessionId { get; } = CanonicalSession(generation);

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                DisposeCount++;
                _transport.Dispose();
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class ScriptedControlStream(
        Func<UserModeDesktopControlMessage, UserModeDesktopControlMessage> responder) : Stream
    {
        private readonly Func<UserModeDesktopControlMessage, UserModeDesktopControlMessage> _responder = responder;
        private readonly MemoryStream _requestBytes = new();
        private readonly Queue<byte> _responseBytes = new();
        private int _disposed;

        public override bool CanRead => Volatile.Read(ref _disposed) == 0;
        public override bool CanSeek => false;
        public override bool CanWrite => Volatile.Read(ref _disposed) == 0;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => CompleteRequest();

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CompleteRequest();
            return Task.CompletedTask;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            ThrowIfDisposed();
            var count = Math.Min(buffer.Length, _responseBytes.Count);
            for (var index = 0; index < count; index++)
            {
                buffer[index] = _responseBytes.Dequeue();
            }

            return count;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Read(buffer.Span));
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            ThrowIfDisposed();
            _requestBytes.Write(buffer);
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _requestBytes.Dispose();
                _responseBytes.Clear();
            }

            base.Dispose(disposing);
        }

        private void CompleteRequest()
        {
            ThrowIfDisposed();
            var framedRequest = _requestBytes.ToArray();
            _requestBytes.SetLength(0);
            try
            {
                if (framedRequest.Length < sizeof(int))
                {
                    throw new InvalidDataException("U4FS001");
                }

                var length = BinaryPrimitives.ReadInt32BigEndian(framedRequest);
                if (length <= 0 || framedRequest.Length != sizeof(int) + length)
                {
                    throw new InvalidDataException("U4FS001");
                }

                using var request = UserModeDesktopSessionCodec.Decode(framedRequest.AsSpan(sizeof(int), length));
                using var response = _responder(request);
                var encodedResponse = UserModeDesktopSessionCodec.Encode(response);
                try
                {
                    var frame = new byte[sizeof(int) + encodedResponse.Length];
                    BinaryPrimitives.WriteInt32BigEndian(frame, encodedResponse.Length);
                    encodedResponse.CopyTo(frame, sizeof(int));
                    foreach (var value in frame)
                    {
                        _responseBytes.Enqueue(value);
                    }

                    CryptographicOperations.ZeroMemory(frame);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(encodedResponse);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(framedRequest);
            }
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                throw new ObjectDisposedException(nameof(ScriptedControlStream));
            }
        }
    }

    private static string CanonicalSession(long generation) =>
        $"desktop-session-{generation}-{new string('a', 32)}";
}
