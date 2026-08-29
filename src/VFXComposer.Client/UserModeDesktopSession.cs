using System.Text.Json;
using VFXComposer.Protocol;
using VFXComposer.Protocol.Ipc;
using VFXComposer.Protocol.Json;
using VFXComposer.Protocol.Queries;

namespace VFXComposer.Client;

public sealed class UserModeDesktopSession : IUserModeDesktopSession
{
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly Func<long, CancellationToken, ValueTask<IUserModeBrokerProcessHost>> _startHost;
    private IUserModeBrokerProcessHost? _host;
    private UserModeDesktopSessionState _state;
    private long _generation;
    private UserModeDesktopReadPresentation? _lastRead;
    private int _disposed;

    public UserModeDesktopSession()
        : this(StartInstalledHostAsync)
    {
    }

    internal UserModeDesktopSession(
        Func<long, CancellationToken, ValueTask<IUserModeBrokerProcessHost>> startHost)
    {
        _startHost = startHost ?? throw new ArgumentNullException(nameof(startHost));
        _state = UserModeDesktopSessionState.Disconnected;
    }

    public UserModeDesktopSessionState State => _state;
    public long Generation => Interlocked.Read(ref _generation);
    public UserModeDesktopReadPresentation? LastRead => Volatile.Read(ref _lastRead);
    public event EventHandler? StateChanged;

    public async ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_state is not UserModeDesktopSessionState.Disconnected and
                not UserModeDesktopSessionState.Restarting and
                not UserModeDesktopSessionState.RecoveryRequired)
            {
                throw new InvalidOperationException("U4FS001");
            }

            SetState(_state == UserModeDesktopSessionState.Restarting
                ? UserModeDesktopSessionState.Restarting
                : UserModeDesktopSessionState.Starting);
            await DisposeHostAsync().ConfigureAwait(false);
            Volatile.Write(ref _lastRead, null);
            var generation = checked(Interlocked.Increment(ref _generation));
            try
            {
                _host = await _startHost(generation, cancellationToken).ConfigureAwait(false);
                if (!_host.IsActive)
                {
                    throw new InvalidDataException("U4FS001");
                }

                SetState(UserModeDesktopSessionState.ConnectedNoProject);
            }
            catch
            {
                await EnterRecoveryAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async ValueTask SelectAsync(
        string selection,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selection);
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            RequireState(UserModeDesktopSessionState.ConnectedNoProject, UserModeDesktopSessionState.Selected);
            SetState(UserModeDesktopSessionState.Selecting);
            Volatile.Write(ref _lastRead, null);
            try
            {
                var generation = Generation;
                var sessionId = SessionIdFor(generation);
                var requestId = "desktop-select-" + Guid.NewGuid().ToString("N");
                using var request = new UserModeDesktopControlMessage(
                    ProtocolVersions.Current,
                    UserModeDesktopControlKinds.Select,
                    requestId,
                    generation,
                    sessionId,
                    selection,
                    null,
                    null,
                    []);
                using var response = await ExchangeAsync(request, cancellationToken).ConfigureAwait(false);
                RequireResponse(response, UserModeDesktopControlKinds.SelectAccepted, requestId, generation, sessionId);
                SetState(UserModeDesktopSessionState.Selected);
            }
            catch
            {
                await EnterRecoveryAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async ValueTask<UserModeDesktopReadPresentation> ReadAsync(
        string documentKind = DocumentKinds.LibraryIndex,
        string documentId = "project",
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            RequireState(UserModeDesktopSessionState.Selected);
            SetState(UserModeDesktopSessionState.Reading);
            try
            {
                var generation = Generation;
                var sessionId = SessionIdFor(generation);
                var requestId = "desktop-read-" + Guid.NewGuid().ToString("N");
                using var request = new UserModeDesktopControlMessage(
                    ProtocolVersions.Current,
                    UserModeDesktopControlKinds.Read,
                    requestId,
                    generation,
                    sessionId,
                    null,
                    documentKind,
                    documentId,
                    []);
                using var response = await ExchangeAsync(request, cancellationToken).ConfigureAwait(false);
                RequireResponse(response, UserModeDesktopControlKinds.ReadResult, requestId, generation, sessionId);
                if (!string.Equals(response.DocumentKind, documentKind, StringComparison.Ordinal) ||
                    !string.Equals(response.DocumentId, documentId, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("U4FS001");
                }

                var responsePayload = response.CopyPayload();
                ReadDocumentResult result;
                try
                {
                    result = StrictWireCodec.Decode<ReadDocumentResult>(responsePayload);
                }
                finally
                {
                    System.Security.Cryptography.CryptographicOperations.ZeroMemory(responsePayload);
                }
                if (!string.Equals(result.DocumentKind, documentKind, StringComparison.Ordinal) ||
                    !string.Equals(result.DocumentId, documentId, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("U4FS001");
                }

                var presentation = new UserModeDesktopReadPresentation(
                    result.Accepted,
                    result.DocumentKind,
                    result.DocumentId,
                    result.ByteLength,
                    result.ContentBase64,
                    result.Diagnostic?.Code);
                Volatile.Write(ref _lastRead, presentation);
                SetState(UserModeDesktopSessionState.Selected);
                return presentation;
            }
            catch
            {
                await EnterRecoveryAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async ValueTask RestartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_state is not UserModeDesktopSessionState.RecoveryRequired and
                not UserModeDesktopSessionState.ConnectedNoProject and
                not UserModeDesktopSessionState.Selected)
            {
                throw new InvalidOperationException("U4FS001");
            }

            SetState(UserModeDesktopSessionState.Restarting);
            await DisposeHostAsync().ConfigureAwait(false);
            Volatile.Write(ref _lastRead, null);
            var generation = checked(Interlocked.Increment(ref _generation));
            try
            {
                _host = await _startHost(generation, cancellationToken).ConfigureAwait(false);
                if (!_host.IsActive)
                {
                    throw new InvalidDataException("U4FS001");
                }

                SetState(UserModeDesktopSessionState.ConnectedNoProject);
            }
            catch
            {
                await EnterRecoveryAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await DisposeHostAsync().ConfigureAwait(false);
            Volatile.Write(ref _lastRead, null);
            SetState(UserModeDesktopSessionState.Disconnected);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async ValueTask<UserModeDesktopControlMessage> ExchangeAsync(
        UserModeDesktopControlMessage request,
        CancellationToken cancellationToken)
    {
        var host = _host;
        if (host is null || !host.IsActive)
        {
            throw new EndOfStreamException("U4FS001");
        }

        var encoded = UserModeDesktopSessionCodec.Encode(request);
        try
        {
            await UserModeDesktopSessionCodec.WriteFrameAsync(host.Transport, encoded, cancellationToken)
                .ConfigureAwait(false);
            var responseBytes = await UserModeDesktopSessionCodec.ReadFrameAsync(host.Transport, cancellationToken)
                .ConfigureAwait(false);
            try
            {
                if (!ReferenceEquals(host, _host) || request.Generation != Generation)
                {
                    throw new InvalidDataException("U4FS001");
                }

                return UserModeDesktopSessionCodec.Decode(responseBytes);
            }
            finally
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(responseBytes);
            }
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(encoded);
        }
    }

    private static async ValueTask<IUserModeBrokerProcessHost> StartInstalledHostAsync(
        long generation,
        CancellationToken cancellationToken) =>
        await UserModeBrokerProcessHost.StartAsync(
            UserModeBrokerProcessHost.ResolveInstalledBrokerExecutable(),
            generation,
            TimeSpan.FromSeconds(15),
            cancellationToken).ConfigureAwait(false);

    private static void RequireResponse(
        UserModeDesktopControlMessage response,
        string kind,
        string requestId,
        long generation,
        string sessionId)
    {
        if (!string.Equals(response.MessageKind, kind, StringComparison.Ordinal) ||
            !string.Equals(response.RequestId, requestId, StringComparison.Ordinal) ||
            response.Generation != generation ||
            !string.Equals(response.SessionId, sessionId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("U4FS001");
        }
    }

    private string SessionIdFor(long generation)
    {
        var host = _host ?? throw new EndOfStreamException("U4FS001");
        if (!host.IsActive || !UserModeDesktopSessionCodec.IsCanonicalSessionId(host.SessionId, generation))
        {
            throw new InvalidOperationException("U4FS001");
        }

        return host.SessionId;
    }

    private void RequireState(params UserModeDesktopSessionState[] permitted)
    {
        ThrowIfDisposed();
        if (!permitted.Contains(_state))
        {
            throw new InvalidOperationException("U4FS001");
        }
    }

    private async ValueTask EnterRecoveryAsync()
    {
        Volatile.Write(ref _lastRead, null);
        SetState(UserModeDesktopSessionState.RecoveryRequired);
        await DisposeHostAsync().ConfigureAwait(false);
    }

    private async ValueTask DisposeHostAsync()
    {
        var host = Interlocked.Exchange(ref _host, null);
        if (host is not null)
        {
            await host.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void SetState(UserModeDesktopSessionState value)
    {
        if (_state == value)
        {
            return;
        }

        _state = value;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
}
