using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using VFXComposer.Protocol.Ipc;

[assembly: InternalsVisibleTo("VFXComposer.Client.Tests")]

namespace VFXComposer.Client;

internal interface IUserModeBrokerProcessHost : IAsyncDisposable
{
    Stream Transport { get; }
    bool IsActive { get; }
    string SessionId { get; }
}

internal sealed class UserModeBrokerProcessHost : IUserModeBrokerProcessHost
{
    private readonly Process _process;
    private readonly SafeFileHandle _job;
    private NamedPipeServerStream? _transport;

    private UserModeBrokerProcessHost(
        Process process,
        SafeFileHandle job,
        NamedPipeServerStream transport,
        string sessionId)
    {
        _process = process;
        _job = job;
        _transport = transport;
        SessionId = sessionId;
    }

    public string SessionId { get; }

    public Stream Transport => Volatile.Read(ref _transport) ??
        throw new ObjectDisposedException(nameof(UserModeBrokerProcessHost));

    public bool IsActive
    {
        get
        {
            try
            {
                return Volatile.Read(ref _transport)?.IsConnected == true && !_process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }

    internal static string ResolveInstalledBrokerExecutable()
    {
        var candidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "VFXComposer.Broker.exe"));
        return ValidateExpectedExecutable(candidate);
    }

    internal static async ValueTask<UserModeBrokerProcessHost> StartAsync(
        string expectedBrokerExecutable,
        long generation,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException();
        }

        if (generation <= 0 || timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(generation));
        }

        var executable = ValidateExpectedExecutable(expectedBrokerExecutable);
        var pipeName = "vfxcomposer-desktop-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var sessionId = "desktop-session-" + generation.ToString(
            System.Globalization.CultureInfo.InvariantCulture) + "-" +
            Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var nonce = RandomNumberGenerator.GetBytes(UserModeDesktopSessionCodec.NonceLength);
        NamedPipeServerStream? pipe = null;
        Process? process = null;
        SafeFileHandle? job = null;
        try
        {
            pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
                4096,
                4096);
            var startInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
            };
            startInfo.ArgumentList.Add("--user-mode-desktop-child");
            process = Process.Start(startInfo) ?? throw new InvalidOperationException("U4FS001");
            job = CreateKillOnCloseJob(process.SafeHandle);

            using (var bootstrap = new UserModeDesktopBootstrap(pipeName, generation, sessionId, nonce))
            {
                var bootstrapBytes = UserModeDesktopSessionCodec.EncodeBootstrap(bootstrap);
                try
                {
                    await UserModeDesktopSessionCodec.WriteFrameAsync(
                        process.StandardInput.BaseStream, bootstrapBytes, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(bootstrapBytes);
                }
            }

            using var timeoutSource = new CancellationTokenSource(timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutSource.Token);
            try
            {
                await pipe.WaitForConnectionAsync(linked.Token).ConfigureAwait(false);
                if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var clientPid) ||
                    clientPid != (uint)process.Id || process.HasExited)
                {
                    throw new InvalidDataException("U4FS001");
                }

                var helloBytes = await UserModeDesktopSessionCodec.ReadFrameAsync(pipe, linked.Token)
                    .ConfigureAwait(false);
                using var hello = UserModeDesktopSessionCodec.Decode(helloBytes);
                var helloNonce = hello.CopyPayload();
                try
                {
                    if (!string.Equals(hello.MessageKind, UserModeDesktopControlKinds.Hello, StringComparison.Ordinal) ||
                        hello.Generation != generation ||
                        !string.Equals(hello.SessionId, sessionId, StringComparison.Ordinal) ||
                        !CryptographicOperations.FixedTimeEquals(helloNonce, nonce))
                    {
                        throw new InvalidDataException("U4FS001");
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(helloNonce);
                }
            }
            catch (OperationCanceledException) when (
                !cancellationToken.IsCancellationRequested && timeoutSource.IsCancellationRequested)
            {
                throw new TimeoutException("U4FS001");
            }

            var result = new UserModeBrokerProcessHost(process, job, pipe, sessionId);
            process = null;
            job = null;
            pipe = null;
            return result;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(nonce);
            if (pipe is not null)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
            }

            job?.Dispose();
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

    public async ValueTask DisposeAsync()
    {
        var pipe = Interlocked.Exchange(ref _transport, null);
        if (pipe is not null)
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
        }

        _job.Dispose();
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            _process.Dispose();
        }
    }

    private static string ValidateExpectedExecutable(string candidate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidate);
        var canonical = Path.GetFullPath(candidate);
        if (!Path.IsPathFullyQualified(canonical) ||
            !string.Equals(Path.GetFileName(canonical), "VFXComposer.Broker.exe", StringComparison.OrdinalIgnoreCase) ||
            !canonical.Split(Path.DirectorySeparatorChar).Contains("Release", StringComparer.OrdinalIgnoreCase) ||
            !File.Exists(canonical) ||
            !string.Equals(canonical, candidate, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("U4FS001", nameof(candidate));
        }

        return canonical;
    }

    private static SafeFileHandle CreateKillOnCloseJob(SafeProcessHandle processHandle)
    {
        var job = CreateJobObjectW(IntPtr.Zero, null);
        if (job.IsInvalid)
        {
            job.Dispose();
            throw new InvalidOperationException("U4FS001");
        }

        var limits = new JobObjectExtendedLimitInformation
        {
            BasicLimitInformation = new JobObjectBasicLimitInformation { LimitFlags = 0x00002000 },
        };
        var size = Marshal.SizeOf<JobObjectExtendedLimitInformation>();
        var memory = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(limits, memory, false);
            if (!SetInformationJobObject(job, 9, memory, (uint)size) ||
                !AssignProcessToJobObject(job, processHandle))
            {
                throw new InvalidOperationException("U4FS001");
            }

            return job;
        }
        catch
        {
            job.Dispose();
            throw;
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateJobObjectW(IntPtr securityAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(
        SafeFileHandle job, int informationClass, IntPtr information, uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(SafeFileHandle job, SafeProcessHandle process);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint clientProcessId);
}
