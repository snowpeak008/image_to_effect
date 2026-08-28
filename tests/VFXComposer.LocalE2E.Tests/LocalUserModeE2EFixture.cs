using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Client;
using VFXComposer.Protocol;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;
using VFXComposer.Protocol.Json;
using VFXComposer.Protocol.Projects;
using VFXComposer.Protocol.Queries;
using VFXComposer.Protocol.Registration;

namespace VFXComposer.LocalE2E.Tests;

internal sealed class LocalUserModeE2EFixture : IAsyncDisposable, IDisposable
{
    private const string TemporaryPrefix = "vfxcomposer-u5-";
    private const int OwnedTreeDeleteAttempts = 12;
    private static readonly TimeSpan OwnedTreeDeleteRetryDelay = TimeSpan.FromMilliseconds(50);
    private readonly List<string> _temporaryRoots = [];
    private int _disposed;

    internal static string RuntimeDirectory => Path.GetFullPath(AppContext.BaseDirectory);
    internal static string BrokerExecutable => Path.Combine(RuntimeDirectory, "VFXComposer.Broker.exe");
    internal static string WorkerExecutable => Path.Combine(RuntimeDirectory, "VFXComposer.UnityWorker.exe");

    internal LocalUnityProject CreateUnityProject(
        string libraryIndex = "{\"library\":\"local-e2e\"}",
        string manifest = "{\"manifest\":\"sample\"}")
    {
        ThrowIfDisposed();
        var root = Path.Combine(Path.GetTempPath(), TemporaryPrefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Assets"));
        Directory.CreateDirectory(Path.Combine(root, "Packages"));
        Directory.CreateDirectory(Path.Combine(root, "ProjectSettings", "VFXComposer", "BuildManifests"));
        File.WriteAllText(Path.Combine(root, "Packages", "manifest.json"), "{}", new UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(root, "ProjectSettings", "ProjectVersion.txt"),
            "m_EditorVersion: 2022.3.0f1\n",
            new UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(root, "ProjectSettings", "VFXComposer", "LibraryIndex.json"),
            libraryIndex,
            new UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(root, "ProjectSettings", "VFXComposer", "BuildManifests", "sample.manifest.json"),
            manifest,
            new UTF8Encoding(false));
        _temporaryRoots.Add(root);
        return new LocalUnityProject(root);
    }

    internal static async Task<UserModeDesktopSession> ConnectDesktopSessionAsync(
        CancellationToken cancellationToken = default)
    {
        AssertRuntimeBundle();
        var session = new UserModeDesktopSession();
        await session.ConnectAsync(cancellationToken).ConfigureAwait(false);
        return session;
    }

    internal static void AssertRuntimeBundle()
    {
        var required = new[]
        {
            "VFXComposer.Broker.exe",
            "VFXComposer.Broker.dll",
            "VFXComposer.Broker.deps.json",
            "VFXComposer.Broker.runtimeconfig.json",
            "VFXComposer.UnityWorker.exe",
            "VFXComposer.UnityWorker.dll",
            "VFXComposer.UnityWorker.deps.json",
            "VFXComposer.UnityWorker.runtimeconfig.json",
            "VFXComposer.Protocol.dll",
        };
        foreach (var file in required)
        {
            Assert.IsTrue(File.Exists(Path.Combine(RuntimeDirectory, file)),
                $"Local E2E runtime bundle is missing {file}.");
        }
    }

    internal static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(RuntimeDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "VFXComposer.sln")))
            {
                return current.FullName;
            }
        }

        throw new AssertFailedException("Unable to locate the repository root from the LocalE2E runtime bundle.");
    }

    internal static WorkerProjectLocator CreateLocator(
        LocalUnityProject project,
        long generation,
        string sessionId,
        string workerProcessEpoch,
        long? locatorGeneration = null,
        string? locatorSessionId = null,
        LocalUnityProject? identityProject = null)
    {
        var identities = ComputeProjectPathIdentities((identityProject ?? project).Root);
        var actualGeneration = locatorGeneration ?? generation;
        var requestId = "um-select-" + Guid.NewGuid().ToString("N");
        var registeredProjectId = "um-project-" + identities.ProjectIdentity.Digest["sha256:".Length..("sha256:".Length + 32)];
        var placeholder = TypedHash.ComputeUtf8(WorkerProjectLocator.SelfHashType, "placeholder");
        var provisional = new WorkerProjectLocator(
            ProtocolVersions.Current,
            MessageKinds.WorkerProjectLocator,
            requestId,
            registeredProjectId,
            identities.ProjectIdentity,
            identities.VolumeIdentity,
            identities.RepositoryIdentity,
            identities.ProjectRootIdentity,
            actualGeneration,
            registrationGeneration: 1,
            enrollmentGeneration: 1,
            locatorSessionId ?? sessionId,
            workerProcessEpoch,
            placeholder);
        var selfHash = SelfHash.Compute(
            JsonSerializer.SerializeToUtf8Bytes(provisional),
            WorkerProjectLocator.SelfHashType);
        var locator = new WorkerProjectLocator(
            provisional.ProtocolVersion,
            provisional.MessageKind,
            provisional.RequestId,
            provisional.RegisteredProjectId,
            provisional.ProjectIdentity,
            provisional.VolumeIdentity,
            provisional.RepositoryIdentity,
            provisional.ProjectRootIdentity,
            provisional.BrokerGeneration,
            provisional.RegistrationGeneration,
            provisional.EnrollmentGeneration,
            provisional.WorkerSessionId,
            provisional.WorkerProcessEpoch,
            selfHash);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(locator);
        try
        {
            return StrictWireCodec.Decode<WorkerProjectLocator>(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    internal static async Task<RuntimeProcessIdentity> WaitForRuntimeProcessAsync(
        string processName,
        TimeSpan? timeout = null)
    {
        var stopAt = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (DateTime.UtcNow < stopAt)
        {
            var candidate = FindRuntimeProcess(processName);
            if (candidate is not null)
            {
                return candidate;
            }

            await Task.Delay(50).ConfigureAwait(false);
        }

        throw new AssertFailedException($"Timed out waiting for staged runtime process {processName}.");
    }

    internal static async Task AssertNoRuntimeResidueAsync(
        IEnumerable<RuntimeProcessIdentity>? expectedGone = null)
    {
        var stopAt = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        var expected = expectedGone?.ToArray() ?? [];
        while (DateTime.UtcNow < stopAt)
        {
            var live = GetRuntimeProcesses();
            var retainedExpectedPid = expected.Any(identity => live.Any(process =>
                process.ProcessId == identity.ProcessId &&
                string.Equals(process.Epoch, identity.Epoch, StringComparison.Ordinal)));
            if (live.Count == 0 && !retainedExpectedPid && GetVfxPipeNames().Count == 0)
            {
                return;
            }

            await Task.Delay(50).ConfigureAwait(false);
        }

        var residue = string.Join(", ", GetRuntimeProcesses().Select(value =>
            $"{value.ProcessName}:{value.ProcessId}:{value.Epoch}"));
        var pipes = string.Join(", ", GetVfxPipeNames());
        throw new AssertFailedException($"Runtime residue remained. Processes=[{residue}], pipes=[{pipes}].");
    }

    internal static async Task KillRuntimeProcessAsync(RuntimeProcessIdentity identity)
    {
        try
        {
            using var process = Process.GetProcessById(identity.ProcessId);
            if (!process.HasExited && string.Equals(GetEpoch(process), identity.Epoch, StringComparison.Ordinal))
            {
                process.Kill(entireProcessTree: true);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
        }
        catch (ArgumentException)
        {
            // The process already exited, which is the requested crash state.
        }
    }

    internal async Task<bool> TryReplaceAssetsWithReparsePointAsync(LocalUnityProject project)
    {
        ThrowIfDisposed();
        var target = Path.Combine(Path.GetTempPath(), TemporaryPrefix + "reparse-target-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(target);
        _temporaryRoots.Add(target);
        var assets = Path.Combine(project.Root, "Assets");
        Directory.Delete(assets);
        try
        {
            _ = Directory.CreateSymbolicLink(assets, target);
            if ((File.GetAttributes(assets) & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (IOException)
        {
        }

        if (Directory.Exists(assets))
        {
            var attributes = File.GetAttributes(assets);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }

            if (Directory.EnumerateFileSystemEntries(assets).Any())
            {
                throw new InvalidOperationException("The owned Assets link slot was unexpectedly populated.");
            }

            Directory.Delete(assets);
        }

        var createdJunction = TryCreateDirectoryJunction(assets, target);
        if (!createdJunction)
        {
            Directory.CreateDirectory(assets);
        }

        await Task.CompletedTask.ConfigureAwait(false);
        return createdJunction;
    }

    internal static bool HasWorkerReparseRejectionPredicate()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "services",
            "VFXComposer.UnityWorker",
            "UserModeUnityWorkerHost.cs"));
        return source.Contains("FileAttributes.ReparsePoint", StringComparison.Ordinal) &&
            source.Contains("RequireDirectoryTreeNotReparse", StringComparison.Ordinal);
    }

    internal static async Task<int> RunMalformedBootstrapWorkerAsync(
        LocalUnityProject project,
        byte[] bootstrapPayload)
    {
        using var process = StartWorker(project.Root);
        try
        {
            await WriteFrameAsync(process.StandardInput.BaseStream, bootstrapPayload).ConfigureAwait(false);
            process.StandardInput.Close();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            return process.ExitCode;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bootstrapPayload);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
    }

    internal static byte[] EncodeBootstrap(
        string pipeName,
        long generation,
        string sessionId,
        byte[] nonce)
    {
        var pipeBytes = new UTF8Encoding(false, true).GetBytes(pipeName);
        var sessionBytes = new UTF8Encoding(false, true).GetBytes(sessionId);
        var payload = new byte[checked(4 + 8 + 2 + pipeBytes.Length + 2 + sessionBytes.Length + nonce.Length)];
        var offset = 0;
        "UMB1"u8.CopyTo(payload);
        offset += 4;
        BinaryPrimitives.WriteInt64BigEndian(payload.AsSpan(offset, 8), generation);
        offset += 8;
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(offset, 2), checked((ushort)pipeBytes.Length));
        offset += 2;
        pipeBytes.CopyTo(payload, offset);
        offset += pipeBytes.Length;
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(offset, 2), checked((ushort)sessionBytes.Length));
        offset += 2;
        sessionBytes.CopyTo(payload, offset);
        offset += sessionBytes.Length;
        nonce.CopyTo(payload, offset);
        return payload;
    }

    internal static string CreatePipeName() =>
        "vfxcomposer-um-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    internal static string CreateSessionId(long generation) =>
        "um-session-" + generation.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" +
        Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        foreach (var root in _temporaryRoots.OrderByDescending(value => value.Length))
        {
            if (Directory.Exists(root))
            {
                await DeleteOwnedTreeWithRetryAsync(root).ConfigureAwait(false);
            }
        }

        _temporaryRoots.Clear();
    }

    private static Process StartWorker(string projectRoot)
    {
        AssertRuntimeBundle();
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(WorkerExecutable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                WorkingDirectory = projectRoot,
            },
        };
        process.StartInfo.ArgumentList.Add("--user-mode-worker-child");
        Assert.IsTrue(process.Start(), "The staged Worker did not start.");
        return process;
    }

    private static RuntimeProcessIdentity? FindRuntimeProcess(string processName) =>
        GetRuntimeProcesses().FirstOrDefault(process =>
            string.Equals(process.ProcessName, processName, StringComparison.Ordinal));

    private static List<RuntimeProcessIdentity> GetRuntimeProcesses()
    {
        var result = new List<RuntimeProcessIdentity>();
        foreach (var processName in new[] { "VFXComposer.Broker", "VFXComposer.UnityWorker" })
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    try
                    {
                        var executable = process.MainModule?.FileName;
                        if (executable is null ||
                            !string.Equals(Path.GetFullPath(executable),
                                Path.Combine(RuntimeDirectory, processName + ".exe"),
                                StringComparison.OrdinalIgnoreCase) ||
                            process.HasExited)
                        {
                            continue;
                        }

                        result.Add(new RuntimeProcessIdentity(
                            processName,
                            process.Id,
                            GetEpoch(process)));
                    }
                    catch (InvalidOperationException)
                    {
                        // The process exited while being enumerated.
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        // Only same-user staged processes are relevant; inaccessible ones are not ours.
                    }
                }
            }
        }

        return result;
    }

    private static string GetEpoch(Process process) =>
        $"winproc-{process.Id}-{((ulong)process.StartTime.ToFileTimeUtc()):x16}";

    private static List<string> GetVfxPipeNames()
    {
        try
        {
            return Directory.EnumerateFileSystemEntries("\\\\.\\pipe\\")
                .Select(Path.GetFileName)
                .Where(name => name is not null &&
                    (name.StartsWith("vfxcomposer-um-", StringComparison.Ordinal) ||
                     name.StartsWith("vfxcomposer-desktop-", StringComparison.Ordinal)))
                .Cast<string>()
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static void DeleteOwnedTree(string root)
    {
        var canonicalRoot = Path.GetFullPath(root);
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        var prefix = tempRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? tempRoot
            : tempRoot + Path.DirectorySeparatorChar;
        if (!canonicalRoot.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(canonicalRoot).StartsWith(TemporaryPrefix, StringComparison.Ordinal) ||
            (File.GetAttributes(canonicalRoot) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("Refusing to delete a non-owned temporary root.");
        }

        DeleteDirectoryNoFollow(new DirectoryInfo(canonicalRoot));
    }

    private static async Task DeleteOwnedTreeWithRetryAsync(string root)
    {
        Exception? lastFailure = null;
        for (var attempt = 1; attempt <= OwnedTreeDeleteAttempts; attempt++)
        {
            if (!Directory.Exists(root))
            {
                return;
            }

            try
            {
                DeleteOwnedTree(root);
                return;
            }
            catch (IOException exception)
            {
                if (!Directory.Exists(root))
                {
                    return;
                }

                lastFailure = exception;
            }
            catch (UnauthorizedAccessException exception)
            {
                if (!Directory.Exists(root))
                {
                    return;
                }

                lastFailure = exception;
            }

            if (attempt < OwnedTreeDeleteAttempts)
            {
                await Task.Delay(OwnedTreeDeleteRetryDelay).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException("Unable to delete the exact owned temporary root after bounded retries.", lastFailure);
    }

    private static void DeleteDirectoryNoFollow(DirectoryInfo directory)
    {
        foreach (var file in directory.EnumerateFiles())
        {
            file.Delete();
        }

        foreach (var child in directory.EnumerateDirectories())
        {
            if ((child.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                child.Delete();
            }
            else
            {
                DeleteDirectoryNoFollow(child);
            }
        }

        directory.Delete();
    }

    private static bool TryCreateDirectoryJunction(string linkPath, string targetPath)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        process.StartInfo.ArgumentList.Add("/d");
        process.StartInfo.ArgumentList.Add("/c");
        process.StartInfo.ArgumentList.Add("mklink");
        process.StartInfo.ArgumentList.Add("/J");
        process.StartInfo.ArgumentList.Add(linkPath);
        process.StartInfo.ArgumentList.Add(targetPath);
        if (!process.Start())
        {
            return false;
        }

        _ = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0 && Directory.Exists(linkPath) &&
            (File.GetAttributes(linkPath) & FileAttributes.ReparsePoint) != 0;
    }

    private static ProjectPathIdentities ComputeProjectPathIdentities(string projectRoot)
    {
        var canonical = Path.GetFullPath(projectRoot);
        var normalized = canonical.TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
        var volumeRoot = Path.GetPathRoot(canonical)?.ToUpperInvariant();
        if (string.IsNullOrEmpty(volumeRoot) || volumeRoot.Length != 3)
        {
            throw new AssertFailedException("A local U5 project must use a drive-rooted working directory.");
        }

        return new ProjectPathIdentities(
            PathIdentity(ProjectRegistrationAttestation.ProjectIdentityType, "project", normalized),
            PathIdentity(ProjectRegistrationAttestation.VolumeIdentityType, "volume", volumeRoot),
            PathIdentity(ProjectRegistrationAttestation.DirectoryIdentityType, "repository", normalized),
            PathIdentity(ProjectRegistrationAttestation.DirectoryIdentityType, "root", normalized));
    }

    private static TypedHash PathIdentity(string typeTag, string role, string value) =>
        TypedHash.ComputeUtf8(
            typeTag,
            string.Concat("vfxcomposer.user-mode-project-path-correlation/1\0", role, "\0", value));

    private static async ValueTask WriteFrameAsync(Stream destination, byte[] payload)
    {
        var header = new byte[WireFrameHeader.HeaderLength];
        WireFrameHeader.Write(header, payload.Length);
        await destination.WriteAsync(header).ConfigureAwait(false);
        await destination.WriteAsync(payload).ConfigureAwait(false);
        await destination.FlushAsync().ConfigureAwait(false);
    }

    private static async ValueTask<byte[]> ReadFrameAsync(Stream source, CancellationToken cancellationToken = default)
    {
        var header = new byte[WireFrameHeader.HeaderLength];
        await ReadExactlyAsync(source, header, cancellationToken).ConfigureAwait(false);
        var length = WireFrameHeader.Read(header);
        var payload = new byte[length];
        await ReadExactlyAsync(source, payload, cancellationToken).ConfigureAwait(false);
        return payload;
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
                throw new EndOfStreamException();
            }

            offset += read;
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

    internal sealed class LocalWorkerPeer : IAsyncDisposable
    {
        private NamedPipeServerStream? _pipe;
        private Process? _process;
        private byte[] _nonce;

        private LocalWorkerPeer(
            NamedPipeServerStream pipe,
            Process process,
            string pipeName,
            long generation,
            string sessionId,
            byte[] nonce,
            string workerProcessEpoch)
        {
            _pipe = pipe;
            _process = process;
            PipeName = pipeName;
            Generation = generation;
            SessionId = sessionId;
            _nonce = nonce;
            WorkerProcessEpoch = workerProcessEpoch;
        }

        internal string PipeName { get; }
        internal long Generation { get; }
        internal string SessionId { get; }
        internal string WorkerProcessEpoch { get; }
        internal int ProcessId => _process?.Id ?? throw new ObjectDisposedException(nameof(LocalWorkerPeer));
        internal Stream Pipe => _pipe ?? throw new ObjectDisposedException(nameof(LocalWorkerPeer));

        internal static async Task<LocalWorkerPeer> StartAsync(
            LocalUnityProject project,
            long generation = 1,
            CancellationToken cancellationToken = default)
        {
            AssertRuntimeBundle();
            var pipeName = CreatePipeName();
            var sessionId = CreateSessionId(generation);
            var nonce = RandomNumberGenerator.GetBytes(32);
            var pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
                4096,
                4096);
            Process? process = null;
            try
            {
                process = StartWorker(project.Root);
                var bootstrap = EncodeBootstrap(pipeName, generation, sessionId, nonce);
                try
                {
                    await WriteFrameAsync(process.StandardInput.BaseStream, bootstrap).ConfigureAwait(false);
                    process.StandardInput.Close();
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(bootstrap);
                }

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                await pipe.WaitForConnectionAsync(timeout.Token).ConfigureAwait(false);
                var helloBytes = await ReadFrameAsync(pipe, timeout.Token).ConfigureAwait(false);
                string workerEpoch;
                try
                {
                    workerEpoch = DecodeAndValidateHello(
                        helloBytes,
                        generation,
                        sessionId,
                        nonce,
                        process.Id);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(helloBytes);
                }

                var result = new LocalWorkerPeer(
                    pipe,
                    process,
                    pipeName,
                    generation,
                    sessionId,
                    nonce,
                    workerEpoch);
                pipe = null;
                process = null;
                nonce = [];
                return result;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(nonce);
                if (pipe is not null)
                {
                    await pipe.DisposeAsync().ConfigureAwait(false);
                }

                if (process is not null)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(entireProcessTree: true);
                            await process.WaitForExitAsync().ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
        }

        internal async Task SendLocatorAsync(WorkerProjectLocator locator)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(locator);
            try
            {
                _ = StrictWireCodec.Decode<WorkerProjectLocator>(bytes);
                await WriteFrameAsync(Pipe, bytes).ConfigureAwait(false);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }

        internal async Task SendRawFrameAsync(byte[] payload)
        {
            try
            {
                await WriteFrameAsync(Pipe, payload).ConfigureAwait(false);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(payload);
            }
        }

        internal async Task<WorkerProjectLocatorAcknowledgement> ReadLocatorAcknowledgementAsync()
        {
            var bytes = await ReadFrameAsync(Pipe).ConfigureAwait(false);
            try
            {
                return StrictWireCodec.Decode<WorkerProjectLocatorAcknowledgement>(bytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }

        internal async Task SendPartialFrameAndCloseAsync()
        {
            var header = new byte[WireFrameHeader.HeaderLength];
            WireFrameHeader.Write(header, 32);
            await Pipe.WriteAsync(header.AsMemory(0, 5)).ConfigureAwait(false);
            await Pipe.FlushAsync().ConfigureAwait(false);
            var pipe = Interlocked.Exchange(ref _pipe, null);
            if (pipe is not null)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
            }
        }

        internal async Task<int> WaitForExitAsync()
        {
            var process = _process ?? throw new ObjectDisposedException(nameof(LocalWorkerPeer));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            return process.ExitCode;
        }

        public async ValueTask DisposeAsync()
        {
            var pipe = Interlocked.Exchange(ref _pipe, null);
            if (pipe is not null)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
            }

            var process = Interlocked.Exchange(ref _process, null);
            if (process is not null)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync().ConfigureAwait(false);
                    }
                }
                finally
                {
                    process.Dispose();
                }
            }

            CryptographicOperations.ZeroMemory(_nonce);
            _nonce = [];
        }

        private static string DecodeAndValidateHello(
            ReadOnlySpan<byte> payload,
            long expectedGeneration,
            string expectedSessionId,
            byte[] expectedNonce,
            int expectedProcessId)
        {
            if (payload.Length < 4 + 8 + 2 + 4 + 2 + 32 ||
                !payload[..4].SequenceEqual("UMH1"u8))
            {
                throw new AssertFailedException("The real Worker did not emit the U2 UMH1 hello ABI.");
            }

            var generation = BinaryPrimitives.ReadInt64BigEndian(payload[4..12]);
            var offset = 12;
            var sessionId = ReadText(payload, ref offset);
            var processId = BinaryPrimitives.ReadInt32BigEndian(payload.Slice(offset, 4));
            offset += 4;
            var epoch = ReadText(payload, ref offset);
            if (payload.Length - offset != 32)
            {
                throw new AssertFailedException("The real Worker UMH1 hello nonce length was not 32 bytes.");
            }

            if (generation != expectedGeneration ||
                !string.Equals(sessionId, expectedSessionId, StringComparison.Ordinal) ||
                processId != expectedProcessId ||
                !CryptographicOperations.FixedTimeEquals(payload[offset..], expectedNonce) ||
                !epoch.StartsWith($"winproc-{expectedProcessId}-", StringComparison.Ordinal))
            {
                throw new AssertFailedException("The real Worker UMH1 hello did not bind generation/session/PID/epoch/nonce.");
            }

            return epoch;
        }

        private static string ReadText(ReadOnlySpan<byte> source, ref int offset)
        {
            if (source.Length - offset < 2)
            {
                throw new AssertFailedException("The real Worker UMH1 hello was truncated.");
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(source.Slice(offset, 2));
            offset += 2;
            if (length is 0 or > 256 || source.Length - offset < length)
            {
                throw new AssertFailedException("The real Worker UMH1 hello contains an invalid text field.");
            }

            var value = new UTF8Encoding(false, true).GetString(source.Slice(offset, length));
            offset += length;
            return value;
        }
    }

    internal sealed record LocalUnityProject(string Root);

    internal sealed record RuntimeProcessIdentity(string ProcessName, int ProcessId, string Epoch);

    private sealed record ProjectPathIdentities(
        TypedHash ProjectIdentity,
        TypedHash VolumeIdentity,
        TypedHash RepositoryIdentity,
        TypedHash ProjectRootIdentity);
}
