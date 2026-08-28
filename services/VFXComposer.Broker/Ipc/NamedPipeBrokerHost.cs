using System.IO.Pipes;
using System.Text.Json;
using VFXComposer.Broker.Configuration;
using VFXComposer.Protocol.Ipc;
using VFXComposer.Protocol.Json;

namespace VFXComposer.Broker.Ipc;

/// <summary>
/// Single-session local-pipe host used by the Phase 2 scaffold gate. The shipped
/// entry point cannot construct it because production policy and peer facts are pending.
/// </summary>
internal sealed class NamedPipeBrokerHost
{
    private readonly BrokerPolicy _policy;
    private readonly NamedPipePeerAuthenticator _authenticator;

    public NamedPipeBrokerHost(
        BrokerPolicy policy,
        NamedPipePeerAuthenticator authenticator)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
    }

    public async Task<AuthenticatedPeerConnection> AcceptOneAsync(CancellationToken cancellationToken)
    {
        AuthenticatedPeerSession? authenticatedSession = null;
        var pipe = new NamedPipeServerStream(
            _policy.PipeName,
            PipeDirection.InOut,
            2,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
            4096,
            4096);
        try
        {
            await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

            var requestPayload = await ReadFrameAsync(pipe, cancellationToken).ConfigureAwait(false);
            var hello = StrictWireCodec.Decode<PeerHello>(requestPayload);
            if (!_authenticator.TryAuthenticate(
                    pipe,
                    hello,
                    out var session,
                    out var receipt,
                    out _))
            {
                throw new InvalidDataException(BrokerDiagnosticCodes.PeerRejected);
            }

            authenticatedSession = session;
            var responsePayload = JsonSerializer.SerializeToUtf8Bytes(receipt!);
            await WriteFrameAsync(pipe, responsePayload, cancellationToken).ConfigureAwait(false);
            await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
            var connection = new AuthenticatedPeerConnection(pipe, session!, _authenticator);
            authenticatedSession = null;
            return connection;
        }
        catch
        {
            try
            {
                if (authenticatedSession is not null)
                {
                    _authenticator.Revoke(authenticatedSession.SessionId);
                }
            }
            finally
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
    }

    internal static async ValueTask<byte[]> ReadFrameAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var header = new byte[WireFrameHeader.HeaderLength];
        await ReadExactlyAsync(stream, header, cancellationToken).ConfigureAwait(false);
        var payloadLength = WireFrameHeader.Read(header);
        var payload = new byte[payloadLength];
        await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        return payload;
    }

    internal static async ValueTask WriteFrameAsync(
        Stream stream,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (payload.Length <= 0 || payload.Length > WireFrameHeader.MaximumPayloadLength)
        {
            throw new ArgumentOutOfRangeException(nameof(payload));
        }

        var header = new byte[WireFrameHeader.HeaderLength];
        WireFrameHeader.Write(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
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
                throw new EndOfStreamException("Pipe frame ended before the declared payload length.");
            }

            offset += read;
        }
    }
}

internal sealed class AuthenticatedPeerConnection : IAsyncDisposable
{
    private readonly NamedPipeServerStream _pipe;
    private readonly NamedPipePeerAuthenticator _authenticator;
    private readonly SemaphoreSlim _exchangeGate = new(1, 1);
    private readonly object _exchangeIssuer = new();
    private readonly object _disposeGate = new();
    private Task? _disposeTask;
    private TaskCompletionSource? _responsePublicationsDrained;
    private int _activeResponsePublications;
    private int _disposed;

    internal AuthenticatedPeerConnection(
        NamedPipeServerStream pipe,
        AuthenticatedPeerSession session,
        NamedPipePeerAuthenticator authenticator)
    {
        _pipe = pipe;
        Session = session;
        _authenticator = authenticator;
    }

    public AuthenticatedPeerSession Session { get; }

    internal async ValueTask<byte[]> ExchangeAsync(
        ReadOnlyMemory<byte> requestPayload,
        CancellationToken cancellationToken)
    {
        await using var exchange = await BeginExclusiveExchangeAsync(
            cancellationToken).ConfigureAwait(false);
        return await exchange.ExchangeAsync(requestPayload, cancellationToken).ConfigureAwait(false);
    }

    internal async ValueTask<ExclusiveExchange> BeginExclusiveExchangeAsync(
        CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _disposed) != 0 || !Session.IsUsable)
        {
            throw new InvalidOperationException(BrokerDiagnosticCodes.SessionStale);
        }

        await _exchangeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (Volatile.Read(ref _disposed) != 0 || !Session.IsUsable || !_pipe.IsConnected)
        {
            _exchangeGate.Release();
            try
            {
                await DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // The disposed state is already published and the pipe close
                // runs in DisposeCoreAsync's finally path.
            }

            throw new InvalidOperationException(BrokerDiagnosticCodes.SessionStale);
        }

        return new ExclusiveExchange(this, _exchangeIssuer);
    }

    public ValueTask DisposeAsync()
    {
        Task completion;
        TaskCompletionSource? starter = null;
        lock (_disposeGate)
        {
            if (_disposeTask is null)
            {
                starter = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _disposeTask = starter.Task;
                Volatile.Write(ref _disposed, 1);
            }

            completion = _disposeTask;
        }

        if (starter is not null)
        {
            _ = DisposeCoreAsync(starter);
        }

        return new ValueTask(completion);
    }

    private async Task DisposeCoreAsync(TaskCompletionSource completion)
    {
        try
        {
            Task responsePublicationsDrained;
            lock (_disposeGate)
            {
                responsePublicationsDrained = _activeResponsePublications == 0
                    ? Task.CompletedTask
                    : (_responsePublicationsDrained ??= new TaskCompletionSource(
                        TaskCreationOptions.RunContinuationsAsynchronously)).Task;
            }

            await responsePublicationsDrained.ConfigureAwait(false);
            try
            {
                _authenticator.Revoke(Session.SessionId);
            }
            finally
            {
                await _pipe.DisposeAsync().ConfigureAwait(false);
            }

            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    internal bool TryReserveResponsePublication(out IDisposable? reservation)
    {
        lock (_disposeGate)
        {
            reservation = null;
            if (_disposed != 0 || _activeResponsePublications != 0)
            {
                return false;
            }

            _activeResponsePublications = 1;
            reservation = new ResponsePublicationReservation(this);
            return true;
        }
    }

    private void ReleaseResponsePublication()
    {
        lock (_disposeGate)
        {
            if (_activeResponsePublications != 1)
            {
                throw new InvalidOperationException(
                    "Connection response publication reservation is not active.");
            }

            _activeResponsePublications = 0;
            _responsePublicationsDrained?.TrySetResult();
        }
    }

    internal bool HasActiveResponsePublication
    {
        get
        {
            lock (_disposeGate)
            {
                return _activeResponsePublications != 0;
            }
        }
    }

    private sealed class ResponsePublicationReservation : IDisposable
    {
        private AuthenticatedPeerConnection? _owner;

        internal ResponsePublicationReservation(AuthenticatedPeerConnection owner) =>
            _owner = owner;

        public void Dispose() =>
            Interlocked.Exchange(ref _owner, null)?.ReleaseResponsePublication();
    }

    internal sealed class ExclusiveExchange : IAsyncDisposable
    {
        private AuthenticatedPeerConnection? _owner;

        internal ExclusiveExchange(AuthenticatedPeerConnection owner, object issuer)
        {
            if (!ReferenceEquals(owner._exchangeIssuer, issuer))
            {
                throw new InvalidOperationException("Exclusive exchange issuer is invalid.");
            }

            _owner = owner;
        }

        internal async ValueTask<byte[]> ExchangeAsync(
            ReadOnlyMemory<byte> requestPayload,
            CancellationToken cancellationToken)
        {
            var owner = _owner
                ?? throw new ObjectDisposedException(nameof(ExclusiveExchange));

            try
            {
                if (requestPayload.Length <= 0 ||
                    requestPayload.Length > WireFrameHeader.MaximumPayloadLength ||
                    Volatile.Read(ref owner._disposed) != 0 ||
                    !owner.Session.IsUsable ||
                    !owner._pipe.IsConnected)
                {
                    throw new InvalidOperationException(BrokerDiagnosticCodes.SessionStale);
                }

                await NamedPipeBrokerHost.WriteFrameAsync(
                    owner._pipe,
                    requestPayload,
                    cancellationToken).ConfigureAwait(false);
                await owner._pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
                return await NamedPipeBrokerHost.ReadFrameAsync(
                    owner._pipe,
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                try
                {
                    await owner.DisposeAsync().ConfigureAwait(false);
                }
                catch
                {
                    // Preserve the frame/codec failure while the connection remains
                    // fail-closed even if a revocation observer also fails.
                }

                throw;
            }
        }

        internal async ValueTask ReceiveAndReplyAsync(
            Func<byte[], CancellationToken, ValueTask<GuardedReply>> handler,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(handler);
            var owner = _owner
                ?? throw new ObjectDisposedException(nameof(ExclusiveExchange));

            try
            {
                if (Volatile.Read(ref owner._disposed) != 0 ||
                    !owner.Session.IsUsable ||
                    !owner._pipe.IsConnected)
                {
                    throw new InvalidOperationException(BrokerDiagnosticCodes.SessionStale);
                }

                var request = await NamedPipeBrokerHost.ReadFrameAsync(
                    owner._pipe,
                    cancellationToken).ConfigureAwait(false);
                using var response = await handler(request, cancellationToken).ConfigureAwait(false);
                if (response.Payload.Length <= 0 ||
                    response.Payload.Length > WireFrameHeader.MaximumPayloadLength)
                {
                    throw new InvalidDataException(BrokerDiagnosticCodes.QueryRejected);
                }

                if (!owner.TryReserveResponsePublication(out var connectionPublication) ||
                    connectionPublication is null)
                {
                    throw new InvalidOperationException(BrokerDiagnosticCodes.SessionStale);
                }

                using (connectionPublication)
                {
                    await NamedPipeBrokerHost.WriteFrameAsync(
                        owner._pipe,
                        response.Payload,
                        cancellationToken).ConfigureAwait(false);
                    await owner._pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

            }
            catch
            {
                try
                {
                    await owner.DisposeAsync().ConfigureAwait(false);
                }
                catch
                {
                    // Preserve the frame/codec failure while the connection remains
                    // fail-closed even if a revocation observer also fails.
                }

                throw;
            }
        }

        internal sealed class GuardedReply : IDisposable
        {
            private IDisposable? _publicationReservation;

            internal GuardedReply(byte[] payload, IDisposable publicationReservation)
            {
                Payload = payload ?? throw new ArgumentNullException(nameof(payload));
                _publicationReservation = publicationReservation
                    ?? throw new ArgumentNullException(nameof(publicationReservation));
            }

            internal byte[] Payload { get; }

            public void Dispose() =>
                Interlocked.Exchange(ref _publicationReservation, null)?.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?._exchangeGate.Release();
            return ValueTask.CompletedTask;
        }
    }
}
