using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;
using VFXComposer.Broker.Native;
using VFXComposer.Broker.Security;

namespace VFXComposer.Broker.Ipc;

internal enum UserModeChildLaunchFailurePoint
{
    None = 0,
    AfterSuspendedCreate = 1,
    JobAssignment = 2,
    Resume = 3,
}

internal sealed class UserModeChildLaunchException : InvalidOperationException
{
    internal UserModeChildLaunchException(
        int? processId,
        UserModeChildLaunchFailurePoint failurePoint,
        Exception innerException)
        : base("U2FS001", innerException)
    {
        ProcessId = processId;
        FailurePoint = failurePoint;
    }

    internal int? ProcessId { get; }

    internal UserModeChildLaunchFailurePoint FailurePoint { get; }
}

/// <summary>
/// Owns one exact ordinary-current-user child and its mandatory unique
/// kill-on-close Job. The child cannot execute before Job assignment.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class UserModeChildProcess : IAsyncDisposable, IDisposable
{
    private const uint CreateSuspended = 0x00000004;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const uint CreateNoWindow = 0x08000000;
    private const uint StartfUseStdHandles = 0x00000100;
    private const uint HandleFlagInherit = 0x00000001;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;
    private const nuint ProcThreadAttributeHandleList = 0x00020002;
    private const uint WaitObject0 = 0;
    private const uint WaitTimeout = 258;
    private const uint LaunchFailureExitCode = 0xE2000001;
    private const uint CleanupWaitMilliseconds = 5000;

    private readonly object _gate = new();
    private readonly Process _process;
    private readonly StreamWriter _standardInput;
    private readonly WindowsKillOnCloseJob _job;
    private Task? _disposeTask;

    private UserModeChildProcess(
        Process process,
        StreamWriter standardInput,
        WindowsKillOnCloseJob job,
        string expectedExecutablePath,
        string userSid,
        string processEpoch)
    {
        _process = process;
        _standardInput = standardInput;
        _job = job;
        ExpectedExecutablePath = expectedExecutablePath;
        UserSid = userSid;
        ProcessId = process.Id;
        ProcessEpoch = processEpoch;
    }

    internal int ProcessId { get; }

    internal string ProcessEpoch { get; }

    internal string UserSid { get; }

    internal string ExpectedExecutablePath { get; }

    internal bool HasActiveContainment => _job.IsActive;

    internal WindowsKillOnCloseJobCloseResult CloseContainmentForTest() => _job.Close();

    internal SafeProcessHandle ProcessHandle => _process.SafeHandle;

    internal StreamWriter StandardInput => _standardInput;

    internal bool IsExactProcessActive
    {
        get
        {
            try
            {
                return ProcessEpochValidator.IsActive(_process.SafeHandle) &&
                    string.Equals(
                        ProcessEpochValidator.Observe(_process.SafeHandle, ProcessId),
                        ProcessEpoch,
                        StringComparison.Ordinal);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    internal static UserModeChildProcess Launch(
        string expectedReleaseExecutablePath,
        ProcessStartInfo startInfo) =>
        LaunchCore(
            expectedReleaseExecutablePath,
            startInfo,
            UserModeChildLaunchFailurePoint.None);

    internal static UserModeChildProcess LaunchForTest(
        string expectedReleaseExecutablePath,
        ProcessStartInfo startInfo,
        UserModeChildLaunchFailurePoint failurePoint)
    {
        if (failurePoint == UserModeChildLaunchFailurePoint.None)
        {
            throw new ArgumentOutOfRangeException(nameof(failurePoint));
        }

        return LaunchCore(expectedReleaseExecutablePath, startInfo, failurePoint);
    }

    internal static string CanonicalizeExpectedExecutablePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException();
        }

        string canonical;
        try
        {
            canonical = Path.GetFullPath(path);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException("U2FS001", nameof(path), exception);
        }

        var root = Path.GetPathRoot(canonical);
        var pathSegments = canonical[(root?.Length ?? 0)..].Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries);
        if (root is null || root.Length != 3 || root[1] != ':' ||
            root[2] != Path.DirectorySeparatorChar ||
            canonical.StartsWith("\\\\", StringComparison.Ordinal) ||
            canonical.StartsWith("\\\\?\\", StringComparison.Ordinal) ||
            canonical.StartsWith("\\\\.\\", StringComparison.Ordinal) ||
            canonical.AsSpan(2).Contains(':') ||
            !pathSegments.Contains("Release", StringComparer.OrdinalIgnoreCase) ||
            !Path.GetFileName(canonical).StartsWith("VFXComposer.", StringComparison.Ordinal) ||
            !string.Equals(Path.GetExtension(canonical), ".exe", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(canonical))
        {
            throw new ArgumentException("U2FS001", nameof(path));
        }

        EnsureNoReparseComponents(canonical, root, nameof(path));
        return canonical;
    }

    internal bool Matches(int processId, string? processEpoch) =>
        processId == ProcessId &&
        string.Equals(processEpoch, ProcessEpoch, StringComparison.Ordinal) &&
        IsExactProcessActive;

    internal async Task<bool> WaitForExitAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        using var timeoutSource = timeout == Timeout.InfiniteTimeSpan
            ? null
            : new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource?.Token ?? CancellationToken.None);
        try
        {
            await _process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested && timeoutSource?.IsCancellationRequested == true)
        {
            return false;
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _disposeTask ??= DisposeCoreAsync();
            return new ValueTask(_disposeTask);
        }
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    ~UserModeChildProcess()
    {
        try
        {
            Dispose();
        }
        catch (Exception)
        {
            // A failed finalizer cleanup is never reported as a clean exit.
        }
    }

    private static UserModeChildProcess LaunchCore(
        string expectedReleaseExecutablePath,
        ProcessStartInfo startInfo,
        UserModeChildLaunchFailurePoint testFailurePoint)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        var expectedPath = CanonicalizeExpectedExecutablePath(expectedReleaseExecutablePath);
        ValidateStartInfo(startInfo, expectedPath);

        var expectedSid = WindowsIdentity.GetCurrent().User?.Value;
        if (string.IsNullOrWhiteSpace(expectedSid))
        {
            throw new InvalidOperationException("U2FS001");
        }

        WindowsKillOnCloseJob? job = null;
        SafeFileHandle? childInputRead = null;
        SafeFileHandle? parentInputWrite = null;
        SafeFileHandle? nullOutput = null;
        SafeProcessHandle? createdProcessHandle = null;
        SafeFileHandle? primaryThreadHandle = null;
        StreamWriter? standardInput = null;
        Process? process = null;
        IntPtr attributeList = IntPtr.Zero;
        IntPtr inheritedHandles = IntPtr.Zero;
        IntPtr environment = IntPtr.Zero;
        int? processId = null;
        var assigned = false;
        var failurePoint = UserModeChildLaunchFailurePoint.None;
        try
        {
            job = WindowsKillOnCloseJob.CreateUniqueConfigured();
            CreateOwnedStandardHandles(
                out childInputRead,
                out parentInputWrite,
                out nullOutput);
            attributeList = CreateInheritedHandleList(
                childInputRead,
                nullOutput,
                out inheritedHandles);
            environment = CreateEnvironmentBlock(startInfo);

            var startup = new StartupInfoEx
            {
                StartupInfo = new StartupInfo
                {
                    Cb = checked((uint)Marshal.SizeOf<StartupInfoEx>()),
                    Flags = StartfUseStdHandles,
                    StandardInput = childInputRead.DangerousGetHandle(),
                    StandardOutput = nullOutput.DangerousGetHandle(),
                    StandardError = nullOutput.DangerousGetHandle(),
                },
                AttributeList = attributeList,
            };
            var commandLine = BuildCommandLine(expectedPath, startInfo.ArgumentList);
            var creationFlags = CreateSuspended | CreateUnicodeEnvironment |
                ExtendedStartupInfoPresent;
            if (startInfo.CreateNoWindow)
            {
                creationFlags |= CreateNoWindow;
            }

            var currentDirectory = string.IsNullOrEmpty(startInfo.WorkingDirectory)
                ? null
                : startInfo.WorkingDirectory;
            if (!CreateProcessW(
                    expectedPath,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    inheritHandles: true,
                    creationFlags,
                    environment,
                    currentDirectory,
                    ref startup,
                    out var processInformation))
            {
                failurePoint = UserModeChildLaunchFailurePoint.AfterSuspendedCreate;
                throw new Win32Exception(Marshal.GetLastWin32Error(), "U2FS001");
            }

            createdProcessHandle = new SafeProcessHandle(
                processInformation.Process,
                ownsHandle: true);
            primaryThreadHandle = new SafeFileHandle(
                processInformation.Thread,
                ownsHandle: true);
            processId = checked((int)processInformation.ProcessId);
            process = Process.GetProcessById(processId.Value);
            _ = process.SafeHandle;

            var observedImage = QueryImagePath(createdProcessHandle);
            if (!string.Equals(observedImage, expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                failurePoint = UserModeChildLaunchFailurePoint.AfterSuspendedCreate;
                throw new InvalidOperationException("U2FS001");
            }

            var processEpoch = ProcessEpochValidator.Observe(
                createdProcessHandle,
                processId.Value);
            var inputStream = new FileStream(
                parentInputWrite,
                FileAccess.Write,
                bufferSize: 4096,
                isAsync: false);
            parentInputWrite = null;
            standardInput = new StreamWriter(
                inputStream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 4096,
                leaveOpen: false)
            {
                AutoFlush = true,
            };

            if (testFailurePoint == UserModeChildLaunchFailurePoint.AfterSuspendedCreate)
            {
                failurePoint = testFailurePoint;
                throw new InvalidOperationException("U2FS001");
            }

            if (testFailurePoint == UserModeChildLaunchFailurePoint.JobAssignment)
            {
                _ = job.Close();
            }

            if (!job.TryAssign(createdProcessHandle))
            {
                failurePoint = UserModeChildLaunchFailurePoint.JobAssignment;
                throw new InvalidOperationException("U2FS001");
            }

            assigned = true;
            failurePoint = UserModeChildLaunchFailurePoint.Resume;
            var previousSuspendCount = testFailurePoint == UserModeChildLaunchFailurePoint.Resume
                ? uint.MaxValue
                : ResumeThread(primaryThreadHandle);
            if (previousSuspendCount != 1)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "U2FS001");
            }

            failurePoint = UserModeChildLaunchFailurePoint.None;
            var result = new UserModeChildProcess(
                process,
                standardInput,
                job,
                expectedPath,
                expectedSid,
                processEpoch);
            process = null;
            standardInput = null;
            job = null;
            return result;
        }
        catch (Exception exception)
        {
            var cleanupFailures = CleanupFailedLaunch(
                standardInput,
                createdProcessHandle,
                job,
                assigned);
            standardInput = null;
            var inner = cleanupFailures.Count == 0
                ? exception
                : new AggregateException(new[] { exception }.Concat(cleanupFailures));
            throw new UserModeChildLaunchException(processId, failurePoint, inner);
        }
        finally
        {
            process?.Dispose();
            standardInput?.Dispose();
            primaryThreadHandle?.Dispose();
            createdProcessHandle?.Dispose();
            childInputRead?.Dispose();
            parentInputWrite?.Dispose();
            nullOutput?.Dispose();
            job?.Dispose();
            if (attributeList != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }

            if (inheritedHandles != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(inheritedHandles);
            }

            if (environment != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(environment);
            }
        }
    }

    private async Task DisposeCoreAsync()
    {
        var failures = new List<Exception>();
        try
        {
            await _standardInput.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        _ = _job.TryTerminate(LaunchFailureExitCode);
        var closeResult = _job.Close();
        if (closeResult == WindowsKillOnCloseJobCloseResult.CloseFailed)
        {
            failures.Add(new InvalidOperationException("U2FS001"));
        }

        if (IsExactProcessActive)
        {
            TryTerminate(_process);
        }

        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        {
            try
            {
                await _process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
            {
                failures.Add(new InvalidOperationException("U2FS001", exception));
            }
            catch (InvalidOperationException)
            {
                // The exact process had already exited before the wait began.
            }
        }

        _process.Dispose();
        _job.Dispose();
        if (failures.Count != 0)
        {
            throw new AggregateException("U2FS001", failures);
        }

        GC.SuppressFinalize(this);
    }

    private static void ValidateStartInfo(ProcessStartInfo startInfo, string expectedPath)
    {
        string requestedPath;
        try
        {
            requestedPath = Path.GetFullPath(startInfo.FileName);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException("U2FS001", nameof(startInfo), exception);
        }

        if (!Path.IsPathFullyQualified(startInfo.FileName) ||
            !string.Equals(startInfo.FileName, requestedPath, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(requestedPath, expectedPath, StringComparison.OrdinalIgnoreCase) ||
            startInfo.UseShellExecute ||
            !string.IsNullOrEmpty(startInfo.UserName) ||
            startInfo.Password is not null ||
            startInfo.PasswordInClearText is not null ||
            !startInfo.RedirectStandardInput ||
            startInfo.RedirectStandardOutput ||
            startInfo.RedirectStandardError ||
            !string.IsNullOrEmpty(startInfo.Arguments) ||
            !string.IsNullOrEmpty(startInfo.Verb))
        {
            throw new ArgumentException(
                "The child must be the exact canonical release executable with one owned bootstrap channel.",
                nameof(startInfo));
        }

        if (!string.IsNullOrEmpty(startInfo.WorkingDirectory))
        {
            var canonicalWorkingDirectory = Path.GetFullPath(startInfo.WorkingDirectory);
            if (!Path.IsPathFullyQualified(startInfo.WorkingDirectory) ||
                !string.Equals(
                    startInfo.WorkingDirectory,
                    canonicalWorkingDirectory,
                    StringComparison.OrdinalIgnoreCase) ||
                !Directory.Exists(canonicalWorkingDirectory) ||
                (File.GetAttributes(canonicalWorkingDirectory) & FileAttributes.ReparsePoint) != 0)
            {
                throw new ArgumentException("U2FS001", nameof(startInfo));
            }
        }
    }

    private static void EnsureNoReparseComponents(string path, string root, string parameterName)
    {
        var current = root;
        foreach (var segment in path[root.Length..].Split(
                     Path.DirectorySeparatorChar,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is "." or ".." || segment.EndsWith(' ') || segment.EndsWith('.'))
            {
                throw new ArgumentException("U2FS001", parameterName);
            }

            current = Path.Combine(current, segment);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new ArgumentException("U2FS001", parameterName);
            }
        }
    }

    private static void CreateOwnedStandardHandles(
        out SafeFileHandle childInputRead,
        out SafeFileHandle parentInputWrite,
        out SafeFileHandle nullOutput)
    {
        var attributes = new SecurityAttributes
        {
            Length = checked((uint)Marshal.SizeOf<SecurityAttributes>()),
            InheritHandle = true,
        };
        if (!CreatePipe(out childInputRead, out parentInputWrite, ref attributes, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "U2FS001");
        }

        if (!SetHandleInformation(parentInputWrite, HandleFlagInherit, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "U2FS001");
        }

        nullOutput = CreateFileW(
            "NUL",
            GenericWrite,
            FileShareRead | FileShareWrite,
            ref attributes,
            OpenExisting,
            FileAttributeNormal,
            IntPtr.Zero);
        if (nullOutput.IsInvalid || nullOutput.IsClosed)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "U2FS001");
        }
    }

    private static IntPtr CreateInheritedHandleList(
        SafeFileHandle input,
        SafeFileHandle output,
        out IntPtr inheritedHandles)
    {
        nuint size = 0;
        _ = InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
        if (size == 0 || size > int.MaxValue)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "U2FS001");
        }

        var list = Marshal.AllocHGlobal(checked((int)size));
        inheritedHandles = IntPtr.Zero;
        try
        {
            if (!InitializeProcThreadAttributeList(list, 1, 0, ref size))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "U2FS001");
            }

            inheritedHandles = Marshal.AllocHGlobal(checked(IntPtr.Size * 2));
            Marshal.WriteIntPtr(inheritedHandles, 0, input.DangerousGetHandle());
            Marshal.WriteIntPtr(inheritedHandles, IntPtr.Size, output.DangerousGetHandle());
            if (!UpdateProcThreadAttribute(
                    list,
                    0,
                    ProcThreadAttributeHandleList,
                    inheritedHandles,
                    checked((nuint)(IntPtr.Size * 2)),
                    IntPtr.Zero,
                    IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "U2FS001");
            }

            return list;
        }
        catch
        {
            if (inheritedHandles != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(inheritedHandles);
                inheritedHandles = IntPtr.Zero;
            }

            DeleteProcThreadAttributeList(list);
            Marshal.FreeHGlobal(list);
            throw;
        }
    }

    private static IntPtr CreateEnvironmentBlock(ProcessStartInfo startInfo)
    {
        var builder = new StringBuilder();
        foreach (var pair in startInfo.Environment.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(pair.Key) || pair.Key.Contains('=') || pair.Key.Contains('\0') ||
                pair.Value?.Contains('\0') == true)
            {
                throw new ArgumentException("U2FS001", nameof(startInfo));
            }

            builder.Append(pair.Key).Append('=').Append(pair.Value).Append('\0');
        }

        builder.Append('\0');
        return Marshal.StringToHGlobalUni(builder.ToString());
    }

    private static StringBuilder BuildCommandLine(
        string executablePath,
        Collection<string> arguments)
    {
        var builder = new StringBuilder();
        AppendQuotedArgument(builder, executablePath);
        foreach (var argument in arguments)
        {
            builder.Append(' ');
            AppendQuotedArgument(builder, argument);
        }

        if (builder.Length >= 32767)
        {
            throw new ArgumentException("U2FS001", nameof(arguments));
        }

        return builder;
    }

    private static void AppendQuotedArgument(StringBuilder builder, string argument)
    {
        builder.Append('"');
        var backslashes = 0;
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            if (character == '"')
            {
                builder.Append('\\', checked(backslashes * 2 + 1));
                builder.Append('"');
                backslashes = 0;
                continue;
            }

            builder.Append('\\', backslashes);
            backslashes = 0;
            builder.Append(character);
        }

        builder.Append('\\', checked(backslashes * 2));
        builder.Append('"');
    }

    private static string QueryImagePath(SafeProcessHandle processHandle)
    {
        var capacity = 512u;
        while (capacity <= 32768)
        {
            var builder = new StringBuilder(checked((int)capacity));
            var length = capacity;
            if (QueryFullProcessImageNameW(processHandle, 0, builder, ref length))
            {
                return Path.GetFullPath(builder.ToString());
            }

            var error = Marshal.GetLastWin32Error();
            if (error != 122)
            {
                throw new Win32Exception(error, "U2FS001");
            }

            capacity *= 2;
        }

        throw new InvalidOperationException("U2FS001");
    }

    private static List<Exception> CleanupFailedLaunch(
        StreamWriter? standardInput,
        SafeProcessHandle? processHandle,
        WindowsKillOnCloseJob? job,
        bool assigned)
    {
        var failures = new List<Exception>();
        try
        {
            standardInput?.Dispose();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        if (assigned && job is not null)
        {
            _ = job.TryTerminate(LaunchFailureExitCode);
            if (job.Close() == WindowsKillOnCloseJobCloseResult.CloseFailed)
            {
                failures.Add(new InvalidOperationException("U2FS001"));
            }
        }

        if (processHandle is { IsInvalid: false, IsClosed: false })
        {
            var waitResult = WaitForSingleObject(processHandle, 0);
            if (waitResult == WaitTimeout)
            {
                _ = TerminateProcess(processHandle, LaunchFailureExitCode);
                waitResult = WaitForSingleObject(processHandle, CleanupWaitMilliseconds);
            }

            if (waitResult != WaitObject0)
            {
                failures.Add(new Win32Exception(Marshal.GetLastWin32Error(), "U2FS001"));
            }
        }

        if (!assigned && job is not null &&
            job.Close() == WindowsKillOnCloseJobCloseResult.CloseFailed)
        {
            failures.Add(new InvalidOperationException("U2FS001"));
        }

        return failures;
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or NotSupportedException or Win32Exception)
        {
            // Exact-handle Job cleanup remains authoritative.
        }
    }

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreatePipe(
        out SafeFileHandle readPipe,
        out SafeFileHandle writePipe,
        ref SecurityAttributes pipeAttributes,
        uint size);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetHandleInformation(
        SafeFileHandle handle,
        uint mask,
        uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true,
        SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        ref SecurityAttributes securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(
        IntPtr attributeList,
        int attributeCount,
        uint flags,
        ref nuint size);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(
        IntPtr attributeList,
        uint flags,
        nuint attribute,
        IntPtr value,
        nuint size,
        IntPtr previousValue,
        IntPtr returnSize);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern void DeleteProcThreadAttributeList(IntPtr attributeList);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessW(
        string applicationName,
        StringBuilder commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref StartupInfoEx startupInfo,
        out ProcessInformation processInformation);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern uint ResumeThread(SafeFileHandle thread);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(
        SafeProcessHandle process,
        uint flags,
        StringBuilder executableName,
        ref uint size);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(SafeProcessHandle process, uint exitCode);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern uint WaitForSingleObject(SafeProcessHandle handle, uint milliseconds);

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        internal uint Length;
        internal IntPtr SecurityDescriptor;
        [MarshalAs(UnmanagedType.Bool)]
        internal bool InheritHandle;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        internal uint Cb;
        internal string? Reserved;
        internal string? Desktop;
        internal string? Title;
        internal uint X;
        internal uint Y;
        internal uint XSize;
        internal uint YSize;
        internal uint XCountChars;
        internal uint YCountChars;
        internal uint FillAttribute;
        internal uint Flags;
        internal ushort ShowWindow;
        internal ushort Reserved2;
        internal IntPtr ReservedBytes;
        internal IntPtr StandardInput;
        internal IntPtr StandardOutput;
        internal IntPtr StandardError;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfoEx
    {
        internal StartupInfo StartupInfo;
        internal IntPtr AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        internal IntPtr Process;
        internal IntPtr Thread;
        internal uint ProcessId;
        internal uint ThreadId;
    }
}

file static class ProcessEpochValidator
{
    internal static bool IsActive(SafeProcessHandle handle) => ProcessEpoch.IsActive(handle);

    internal static string Observe(SafeProcessHandle handle, int processId) =>
        ProcessEpoch.Observe(handle, processId);
}
