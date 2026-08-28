using System.Buffers.Binary;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using VFXComposer.Broker.Ipc;
using VFXComposer.Protocol;
using VFXComposer.Protocol.Ipc;

namespace VFXComposer.Broker.Tests;

[TestClass]
[DoNotParallelize]
[SupportedOSPlatform("windows")]
public sealed class UserModeDesktopBrokerHostTests
{
    private const long Generation = 407;
    private const string SessionId = "desktop-session-407-0123456789abcdef0123456789abcdef";

    [TestMethod]
    public async Task ControlConnectionWithoutSelectStartsNoWorker()
    {
        var starts = 0;
        await using var host = UserModeDesktopBrokerHost.CreateForWorkerStarterForTests((_, _, _) =>
        {
            starts++;
            return Task.FromException<UserModeBrokerWorkerSession>(new InvalidOperationException("unexpected start"));
        });
        await using var control = new ScriptedControlStream([]);

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(async () =>
            await host.ServeAsync(control, Generation, SessionId));

        Assert.AreEqual(0, starts, "The Desktop control connection alone must not start a Worker.");
    }

    [TestMethod]
    public async Task ScriptedComponentPeerIsAdmittedThenAckedBeforeBothSelectionsAreAccepted()
    {
        using var project = TestProject.Create();
        var sessions = new List<UserModeBrokerWorkerSession>();
        var scriptedPeer = ScriptedPeerExecutable();
        await using var host = UserModeDesktopBrokerHost.CreateForWorkerStarterForTests(async (
            canonicalProjectRoot,
            generation,
            cancellationToken) =>
        {
            var session = await UserModeDesktopBrokerHost.StartScriptedWorkerForTestsAsync(
                scriptedPeer,
                canonicalProjectRoot,
                generation,
                emitMalformedAcknowledgement: false,
                cancellationToken);
            sessions.Add(session);
            return session;
        });
        using var first = SelectRequest("desktop-select-001", project.Root);
        using var second = SelectRequest("desktop-select-002", project.Root);
        await using var control = new ScriptedControlStream([EncodeFrame(first), EncodeFrame(second)]);

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(async () =>
            await host.ServeAsync(control, Generation, SessionId));

        var responses = await control.DecodeResponsesAsync();
        try
        {
            Assert.HasCount(2, responses);
            CollectionAssert.AreEqual(
                new[] { "desktop-select-001", "desktop-select-002" },
                responses.Select(response => response.RequestId).ToArray());
            Assert.IsTrue(responses.All(response =>
                string.Equals(response.MessageKind, UserModeDesktopControlKinds.SelectAccepted, StringComparison.Ordinal)));
            Assert.HasCount(2, sessions);
            Assert.IsFalse(sessions[0].IsUsable, "Reselect must revoke and dispose the prior U2 Worker session.");
            Assert.IsTrue(sessions[1].IsUsable);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        await host.DisposeAsync();
        Assert.IsFalse(sessions[1].IsUsable, "Disconnect disposal must revoke the current Worker session.");
    }

    [TestMethod]
    public async Task MalformedScriptedAckCannotProduceSelectAccepted()
    {
        using var project = TestProject.Create();
        var scriptedPeer = ScriptedPeerExecutable();
        await using var host = UserModeDesktopBrokerHost.CreateForScriptedWorkerForTests(
            scriptedPeer,
            emitMalformedAcknowledgement: true);
        using var request = SelectRequest("desktop-select-invalid-ack", project.Root);
        await using var control = new ScriptedControlStream([EncodeFrame(request)]);

        Exception? exception = null;
        try
        {
            await host.ServeAsync(control, Generation, SessionId);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        Assert.IsNotNull(exception);
        Assert.IsInstanceOfType(exception, typeof(FormatException));
        Assert.AreEqual(0, control.WrittenLength,
            "A malformed C2 acknowledgement must fail before SelectAccepted is emitted.");
    }

    private static UserModeDesktopControlMessage SelectRequest(string requestId, string selection) => new(
        ProtocolVersions.Current,
        UserModeDesktopControlKinds.Select,
        requestId,
        Generation,
        SessionId,
        selection,
        null,
        null,
        []);

    private static string ScriptedPeerExecutable()
    {
        var executable = Path.GetFullPath(Path.ChangeExtension(
            typeof(UserModeDesktopBrokerHost).Assembly.Location,
            ".exe"));
        Assert.IsTrue(File.Exists(executable), "The test-only scripted peer must be the built Broker apphost.");
        return executable;
    }

    private static byte[] EncodeFrame(UserModeDesktopControlMessage message)
    {
        var payload = UserModeDesktopSessionCodec.Encode(message);
        try
        {
            var frame = new byte[sizeof(int) + payload.Length];
            BinaryPrimitives.WriteInt32BigEndian(frame, payload.Length);
            payload.CopyTo(frame, sizeof(int));
            return frame;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    private sealed class ScriptedControlStream(IEnumerable<byte[]> frames) : Stream
    {
        private readonly Queue<byte> _input = new(frames.SelectMany(frame => frame));
        private readonly MemoryStream _written = new();
        private int _disposed;

        public int WrittenLength => checked((int)_written.Length);
        public override bool CanRead => Volatile.Read(ref _disposed) == 0;
        public override bool CanSeek => false;
        public override bool CanWrite => Volatile.Read(ref _disposed) == 0;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            ThrowIfDisposed();
            var count = Math.Min(buffer.Length, _input.Count);
            for (var index = 0; index < count; index++)
            {
                buffer[index] = _input.Dequeue();
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

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            ThrowIfDisposed();
            _written.Write(buffer);
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public async Task<IReadOnlyList<UserModeDesktopControlMessage>> DecodeResponsesAsync()
        {
            var bytes = _written.ToArray();
            try
            {
                await using var stream = new MemoryStream(bytes, writable: false);
                var responses = new List<UserModeDesktopControlMessage>();
                while (stream.Position < stream.Length)
                {
                    var frame = await UserModeDesktopSessionCodec.ReadFrameAsync(stream);
                    try
                    {
                        responses.Add(UserModeDesktopSessionCodec.Decode(frame));
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(frame);
                    }
                }

                return responses;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _input.Clear();
                _written.Dispose();
            }

            base.Dispose(disposing);
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                throw new ObjectDisposedException(nameof(ScriptedControlStream));
            }
        }
    }

    private sealed class TestProject : IDisposable
    {
        private TestProject(string root) => Root = root;

        public string Root { get; }

        public static TestProject Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "vfxcomposer-u4-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "Assets"));
            Directory.CreateDirectory(Path.Combine(root, "Packages"));
            Directory.CreateDirectory(Path.Combine(root, "ProjectSettings"));
            File.WriteAllText(Path.Combine(root, "Packages", "manifest.json"), "{}", new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(root, "ProjectSettings", "ProjectVersion.txt"),
                "m_EditorVersion: 2022.3.62f3c1\n",
                new UTF8Encoding(false));
            return new TestProject(root);
        }

        public void Dispose()
        {
            for (var attempt = 0; attempt < 20 && Directory.Exists(Root); attempt++)
            {
                try
                {
                    Directory.Delete(Root, recursive: true);
                }
                catch (IOException) when (attempt < 19)
                {
                    Thread.Sleep(50);
                }
            }

            Assert.IsFalse(Directory.Exists(Root), "The scripted component peer must release its selected working directory.");
        }
    }
}
