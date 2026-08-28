using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using VFXComposer.Broker.Registration;
using VFXComposer.Broker.Security;
using VFXComposer.Protocol;
using VFXComposer.Protocol.Diagnostics;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;
using VFXComposer.Protocol.Json;
using VFXComposer.Protocol.Queries;
using VFXComposer.Protocol.Registration;

namespace VFXComposer.Broker.Ipc;

/// <summary>
/// Owns the Desktop control connection. A Worker is intentionally absent until a
/// validated explicit selection arrives; each selection receives a new U2 session.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class UserModeDesktopBrokerHost : IAsyncDisposable
{
    private readonly SemaphoreSlim _stateGate = new(1, 1);
    private readonly Func<string, long, CancellationToken, Task<UserModeBrokerWorkerSession>> _startWorker;
    private readonly UserModeProjectSelectionStore _selections = new();
    private UserModeBrokerWorkerSession? _worker;
    private UserModeProjectLease? _lease;
    private UserModeProjectReadSession? _read;
    private int _disposed;

    private UserModeDesktopBrokerHost(
        Func<string, long, CancellationToken, Task<UserModeBrokerWorkerSession>> startWorker)
    {
        _startWorker = startWorker ?? throw new ArgumentNullException(nameof(startWorker));
    }

    internal static async Task<int> RunChildModeAsync(
        Stream standardInput,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(standardInput);
        UserModeDesktopBootstrap? bootstrap = null;
        NamedPipeClientStream? control = null;
        UserModeDesktopBrokerHost? host = null;
        try
        {
            var bootstrapBytes = await UserModeDesktopSessionCodec.ReadFrameAsync(
                standardInput, cancellationToken).ConfigureAwait(false);
            try
            {
                bootstrap = UserModeDesktopSessionCodec.DecodeBootstrap(bootstrapBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bootstrapBytes);
            }

            var generation = bootstrap.Generation;
            var sessionId = bootstrap.SessionId;
            control = new NamedPipeClientStream(
                ".",
                bootstrap.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await control.ConnectAsync(15_000, cancellationToken).ConfigureAwait(false);

            var nonce = bootstrap.CopyNonce();
            try
            {
                using var hello = new UserModeDesktopControlMessage(
                    ProtocolVersions.Current,
                    UserModeDesktopControlKinds.Hello,
                    "hello",
                    generation,
                    sessionId,
                    null,
                    null,
                    null,
                    nonce);
                var helloBytes = UserModeDesktopSessionCodec.Encode(hello);
                try
                {
                    await UserModeDesktopSessionCodec.WriteFrameAsync(
                        control, helloBytes, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(helloBytes);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(nonce);
                bootstrap.Dispose();
                bootstrap = null;
            }

            host = new UserModeDesktopBrokerHost(StartReleaseWorkerAsync);
            await host.ServeAsync(control, generation, sessionId, cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (exception is
            ArgumentException or InvalidDataException or EndOfStreamException or IOException or
            TimeoutException or InvalidOperationException or OperationCanceledException or
            ObjectDisposedException or AggregateException)
        {
            return 31;
        }
        finally
        {
            bootstrap?.Dispose();
            if (host is not null)
            {
                await host.DisposeAsync().ConfigureAwait(false);
            }

            if (control is not null)
            {
                await control.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Test-only component peer. It is not a Unity Worker artifact and does not
    /// make an E2E claim; U5 alone owns the real Worker executable.
    /// </summary>
    internal static async Task<int> RunScriptedWorkerPeerAsync(
        Stream standardInput,
        bool emitMalformedAcknowledgement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(standardInput);
        UserModeWorkerBootstrap? bootstrap = null;
        NamedPipeClientStream? pipe = null;
        try
        {
            bootstrap = await UserModeNamedPipeServer.ReadBootstrapAsync(standardInput, cancellationToken)
                .ConfigureAwait(false);
            pipe = new NamedPipeClientStream(
                ".",
                bootstrap.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(15_000, cancellationToken).ConfigureAwait(false);
            await UserModeNamedPipeServer.WriteHelloAsync(
                pipe,
                bootstrap,
                Environment.ProcessId,
                ProcessEpoch.Observe(Environment.ProcessId),
                cancellationToken).ConfigureAwait(false);

            var locatorBytes = await NamedPipeBrokerHost.ReadFrameAsync(pipe, cancellationToken)
                .ConfigureAwait(false);
            WorkerProjectLocator locator;
            try
            {
                locator = StrictWireCodec.Decode<WorkerProjectLocator>(locatorBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(locatorBytes);
            }

            var acknowledgementBytes = emitMalformedAcknowledgement
                ? "{}"u8.ToArray()
                : CreateScriptedLocatorAcknowledgement(locator);
            try
            {
                await NamedPipeBrokerHost.WriteFrameAsync(pipe, acknowledgementBytes, cancellationToken)
                    .ConfigureAwait(false);
                await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(acknowledgementBytes);
            }

            if (emitMalformedAcknowledgement)
            {
                return 0;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                var queryBytes = await NamedPipeBrokerHost.ReadFrameAsync(pipe, cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    var query = StrictWireCodec.Decode<ReadDocumentQuery>(queryBytes);
                    var result = new ReadDocumentResult(
                        ProtocolVersions.Current,
                        MessageKinds.ReadDocumentResult,
                        query.RequestId,
                        accepted: false,
                        query.ProjectIdentity,
                        query.DocumentKind,
                        query.DocumentId,
                        contentHash: null,
                        byteLength: 0,
                        contentBase64: null,
                        StableDiagnosticCatalog.Create(StableDiagnosticCodes.ProjectDocumentUnavailable));
                    var resultBytes = JsonSerializer.SerializeToUtf8Bytes(result);
                    try
                    {
                        _ = StrictWireCodec.Decode<ReadDocumentResult>(resultBytes);
                        await NamedPipeBrokerHost.WriteFrameAsync(pipe, resultBytes, cancellationToken)
                            .ConfigureAwait(false);
                        await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(resultBytes);
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(queryBytes);
                }
            }

            return 0;
        }
        catch (Exception exception) when (exception is
            ArgumentException or InvalidDataException or EndOfStreamException or IOException or
            TimeoutException or InvalidOperationException or OperationCanceledException)
        {
            return 31;
        }
        finally
        {
            bootstrap?.Dispose();
            if (pipe is not null)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    internal static UserModeDesktopBrokerHost CreateForScriptedWorkerForTests(
        string scriptedPeerExecutable,
        bool emitMalformedAcknowledgement = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptedPeerExecutable);
        return new UserModeDesktopBrokerHost((canonicalProjectRoot, generation, cancellationToken) =>
            StartWorkerCoreAsync(
                scriptedPeerExecutable,
                emitMalformedAcknowledgement
                    ? "--u4-scripted-worker-peer-invalid-ack"
                    : "--u4-scripted-worker-peer",
                canonicalProjectRoot,
                generation,
                cancellationToken));
    }

    internal static UserModeDesktopBrokerHost CreateForWorkerStarterForTests(
        Func<string, long, CancellationToken, Task<UserModeBrokerWorkerSession>> startWorker) => new(startWorker);

    internal static Task<UserModeBrokerWorkerSession> StartScriptedWorkerForTestsAsync(
        string scriptedPeerExecutable,
        string canonicalProjectRoot,
        long generation,
        bool emitMalformedAcknowledgement,
        CancellationToken cancellationToken = default) =>
        StartWorkerCoreAsync(
            scriptedPeerExecutable,
            emitMalformedAcknowledgement
                ? "--u4-scripted-worker-peer-invalid-ack"
                : "--u4-scripted-worker-peer",
            canonicalProjectRoot,
            generation,
            cancellationToken);

    internal async Task ServeAsync(
        Stream control,
        long generation,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (generation <= 0 || !UserModeDesktopSessionCodec.IsCanonicalSessionId(sessionId, generation))
        {
            throw new InvalidDataException("U4FS001");
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            var bytes = await UserModeDesktopSessionCodec.ReadFrameAsync(control, cancellationToken)
                .ConfigureAwait(false);
            UserModeDesktopControlMessage request;
            try
            {
                request = UserModeDesktopSessionCodec.Decode(bytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }

            using (request)
            {
                if (Volatile.Read(ref _disposed) != 0 || request.Generation != generation ||
                    !string.Equals(request.SessionId, sessionId, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("U4FS001");
                }

                UserModeDesktopControlMessage response = request.MessageKind switch
                {
                    UserModeDesktopControlKinds.Select => await SelectAsync(
                        request,
                        generation,
                        sessionId,
                        cancellationToken).ConfigureAwait(false),
                    UserModeDesktopControlKinds.Read => await ReadAsync(
                        request,
                        generation,
                        sessionId,
                        cancellationToken).ConfigureAwait(false),
                    _ => throw new InvalidDataException("U4FS001"),
                };
                using (response)
                {
                    var encoded = UserModeDesktopSessionCodec.Encode(response);
                    try
                    {
                        await UserModeDesktopSessionCodec.WriteFrameAsync(control, encoded, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(encoded);
                    }
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _stateGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await InvalidateCurrentSelectionUnderGateAsync().ConfigureAwait(false);
        }
        finally
        {
            _stateGate.Release();
        }
    }

    public override string ToString() =>
        $"UserModeDesktopBrokerHost(Selection={(_lease is null ? "None" : "Bound")})";

    private async Task<UserModeDesktopControlMessage> SelectAsync(
        UserModeDesktopControlMessage request,
        long generation,
        string sessionId,
        CancellationToken cancellationToken)
    {
        await _stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await InvalidateCurrentSelectionUnderGateAsync().ConfigureAwait(false);

            // U3 validates the user's explicit root before any Worker is created.
            var canonicalProjectRoot = UserModeProjectRootValidator.Validate(request.Selection!);
            UserModeBrokerWorkerSession? worker = null;
            UserModeProjectLease? lease = null;
            try
            {
                worker = await _startWorker(canonicalProjectRoot, generation, cancellationToken)
                    .ConfigureAwait(false);
                if (!worker.IsUsable || worker.Generation != generation)
                {
                    throw new InvalidDataException("U4FS001");
                }

                lease = await _selections.SelectAsync(canonicalProjectRoot, worker, cancellationToken)
                    .ConfigureAwait(false);
                await SendLocatorAndRequireAcknowledgementAsync(lease, worker, cancellationToken)
                    .ConfigureAwait(false);
                var read = new UserModeProjectReadSession(_selections, lease, worker);

                _worker = worker;
                _lease = lease;
                _read = read;
                worker = null;
                lease = null;
                return new UserModeDesktopControlMessage(
                    ProtocolVersions.Current,
                    UserModeDesktopControlKinds.SelectAccepted,
                    request.RequestId,
                    generation,
                    sessionId,
                    null,
                    null,
                    null,
                    []);
            }
            catch
            {
                await DisposeUnpublishedSelectionAsync(lease, worker).ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _stateGate.Release();
        }
    }

    private async Task<UserModeDesktopControlMessage> ReadAsync(
        UserModeDesktopControlMessage request,
        long generation,
        string sessionId,
        CancellationToken cancellationToken)
    {
        await _stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var worker = _worker;
            var read = _read;
            if (worker is null || read is null || !worker.IsUsable || !read.IsUsable)
            {
                await InvalidateCurrentSelectionUnderGateAsync().ConfigureAwait(false);
                throw new InvalidOperationException("U4FS001");
            }

            try
            {
                var result = await read.ReadAsync(
                    request.DocumentKind!,
                    request.DocumentId!,
                    null,
                    cancellationToken).ConfigureAwait(false);
                var resultBytes = JsonSerializer.SerializeToUtf8Bytes(result);
                try
                {
                    _ = StrictWireCodec.Decode<ReadDocumentResult>(resultBytes);
                    return new UserModeDesktopControlMessage(
                        ProtocolVersions.Current,
                        UserModeDesktopControlKinds.ReadResult,
                        request.RequestId,
                        generation,
                        sessionId,
                        null,
                        request.DocumentKind,
                        request.DocumentId,
                        resultBytes);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(resultBytes);
                }
            }
            catch
            {
                await InvalidateCurrentSelectionUnderGateAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _stateGate.Release();
        }
    }

    private static Task<UserModeBrokerWorkerSession> StartReleaseWorkerAsync(
        string canonicalProjectRoot,
        long generation,
        CancellationToken cancellationToken)
    {
        var workerExecutable = Path.Combine(AppContext.BaseDirectory, "VFXComposer.UnityWorker.exe");
        return StartWorkerCoreAsync(
            workerExecutable,
            "--user-mode-worker-child",
            canonicalProjectRoot,
            generation,
            cancellationToken);
    }

    private static Task<UserModeBrokerWorkerSession> StartWorkerCoreAsync(
        string expectedExecutable,
        string childArgument,
        string canonicalProjectRoot,
        long generation,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(expectedExecutable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            WorkingDirectory = canonicalProjectRoot,
        };
        startInfo.ArgumentList.Add(childArgument);
        return UserModeBrokerWorkerSession.StartAsync(
            expectedExecutable,
            startInfo,
            generation,
            TimeSpan.FromSeconds(15),
            cancellationToken);
    }

    private static async Task SendLocatorAndRequireAcknowledgementAsync(
        UserModeProjectLease lease,
        UserModeBrokerWorkerSession worker,
        CancellationToken cancellationToken)
    {
        var locatorBytes = lease.CopyLocatorBytes();
        try
        {
            await NamedPipeBrokerHost.WriteFrameAsync(worker.Transport, locatorBytes, cancellationToken)
                .ConfigureAwait(false);
            await worker.Transport.FlushAsync(cancellationToken).ConfigureAwait(false);
            var acknowledgementBytes = await NamedPipeBrokerHost.ReadFrameAsync(worker.Transport, cancellationToken)
                .ConfigureAwait(false);
            try
            {
                var acknowledgement = StrictWireCodec.Decode<WorkerProjectLocatorAcknowledgement>(
                    acknowledgementBytes);
                if (!MatchesLocatorAcknowledgement(lease, acknowledgement))
                {
                    throw new InvalidDataException("U4FS001");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(acknowledgementBytes);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(locatorBytes);
        }
    }

    private static bool MatchesLocatorAcknowledgement(
        UserModeProjectLease lease,
        WorkerProjectLocatorAcknowledgement acknowledgement) =>
        string.Equals(acknowledgement.ProtocolVersion, ProtocolVersions.Current, StringComparison.Ordinal) &&
        string.Equals(
            acknowledgement.MessageKind,
            MessageKinds.WorkerProjectLocatorAcknowledgement,
            StringComparison.Ordinal) &&
        string.Equals(acknowledgement.RequestId, lease.Locator.RequestId, StringComparison.Ordinal) &&
        string.Equals(acknowledgement.RegisteredProjectId, lease.Locator.RegisteredProjectId, StringComparison.Ordinal) &&
        acknowledgement.BrokerGeneration == lease.Locator.BrokerGeneration &&
        acknowledgement.RegistrationGeneration == lease.Locator.RegistrationGeneration &&
        acknowledgement.EnrollmentGeneration == lease.Locator.EnrollmentGeneration &&
        string.Equals(acknowledgement.WorkerSessionId, lease.Locator.WorkerSessionId, StringComparison.Ordinal) &&
        string.Equals(acknowledgement.WorkerProcessEpoch, lease.Locator.WorkerProcessEpoch, StringComparison.Ordinal) &&
        lease.Locator.SelfHash.FixedTimeEquals(acknowledgement.LocatorSelfHash) &&
        string.Equals(
            acknowledgement.Disposition,
            WorkerProjectLocatorAcknowledgement.AcceptedDisposition,
            StringComparison.Ordinal);

    private static byte[] CreateScriptedLocatorAcknowledgement(WorkerProjectLocator locator)
    {
        var placeholder = TypedHash.ComputeUtf8(
            WorkerProjectLocatorAcknowledgement.SelfHashType,
            "placeholder");
        var provisional = new WorkerProjectLocatorAcknowledgement(
            locator.ProtocolVersion,
            MessageKinds.WorkerProjectLocatorAcknowledgement,
            locator.RequestId,
            locator.RegisteredProjectId,
            locator.BrokerGeneration,
            locator.RegistrationGeneration,
            locator.EnrollmentGeneration,
            locator.WorkerSessionId,
            locator.WorkerProcessEpoch,
            locator.SelfHash,
            WorkerProjectLocatorAcknowledgement.AcceptedDisposition,
            placeholder);
        var selfHash = SelfHash.Compute(
            JsonSerializer.SerializeToUtf8Bytes(provisional),
            WorkerProjectLocatorAcknowledgement.SelfHashType);
        var acknowledgement = new WorkerProjectLocatorAcknowledgement(
            provisional.ProtocolVersion,
            provisional.MessageKind,
            provisional.RequestId,
            provisional.RegisteredProjectId,
            provisional.BrokerGeneration,
            provisional.RegistrationGeneration,
            provisional.EnrollmentGeneration,
            provisional.WorkerSessionId,
            provisional.WorkerProcessEpoch,
            provisional.LocatorSelfHash,
            provisional.Disposition,
            selfHash);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(acknowledgement);
        _ = StrictWireCodec.Decode<WorkerProjectLocatorAcknowledgement>(bytes);
        return bytes;
    }

    private async Task InvalidateCurrentSelectionUnderGateAsync()
    {
        _read = null;
        var lease = Interlocked.Exchange(ref _lease, null);
        var worker = Interlocked.Exchange(ref _worker, null);
        var failures = new List<Exception>();
        if (lease is not null)
        {
            try
            {
                await _selections.RevokeAsync(lease).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (worker is not null)
        {
            try
            {
                await worker.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (failures.Count != 0)
        {
            throw new AggregateException("U4FS001", failures);
        }
    }

    private async Task DisposeUnpublishedSelectionAsync(
        UserModeProjectLease? lease,
        UserModeBrokerWorkerSession? worker)
    {
        var failures = new List<Exception>();
        if (lease is not null)
        {
            try
            {
                await _selections.RevokeAsync(lease).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (worker is not null)
        {
            try
            {
                await worker.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (failures.Count != 0)
        {
            throw new AggregateException("U4FS001", failures);
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
}
