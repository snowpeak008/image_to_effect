using System.Diagnostics;
using System.Globalization;

namespace VFXComposer.Batch.Core;

/// <summary>Exit codes the controlled build mode of <c>Invoke-Unity.ps1</c> can return.</summary>
public static class UnityRecipeBuildExitCodes
{
    /// <summary>The build succeeded and the structured result says so.</summary>
    public const int Succeeded = 0;

    /// <summary>No structured result was produced at all.</summary>
    public const int NoResult = 2;

    /// <summary>The request was executed and refused or failed; the result carries the stable code.</summary>
    public const int StructuredFailure = 4;

    /// <summary>Usage error in the wrapper invocation.</summary>
    public const int Usage = 64;

    /// <summary>A live editor owns the Unity project; the wrapper refuses to steal the lock.</summary>
    public const int ProjectLockHeld = 73;

    /// <summary>The wrapper terminated its own verified child after the timeout.</summary>
    public const int TimedOut = 124;

    /// <summary>The pinned Unity editor is not installed.</summary>
    public const int UnityMissing = 127;
}

/// <summary>One controlled build invocation: two paths, both outside the Unity project.</summary>
public sealed record UnityRecipeBuildLaunch(string RequestPath, string ResultPath, int TimeoutSeconds);

/// <summary>
/// A started build child process. The exact PID and start time are exposed so the queue can
/// terminate precisely that process and nothing else (REQ-003 §6.4).
/// </summary>
public interface IUnityRecipeBuildProcess : IDisposable
{
    int ProcessId { get; }

    DateTimeOffset StartUtc { get; }

    Task<int> WaitForExitAsync(CancellationToken cancellationToken);

    /// <summary>Best-effort termination of this exact process tree.</summary>
    void Terminate();
}

/// <summary>Launch seam for the Unity batchmode build, so the orchestrator is testable without Unity.</summary>
public interface IUnityRecipeBuildRunner
{
    IUnityRecipeBuildProcess Start(UnityRecipeBuildLaunch launch);
}

/// <summary>
/// Starts the build through the repository's <c>Invoke-Unity.ps1</c> wrapper, which owns the
/// project-lock check, the timeout discipline and the "terminate only my own verified PID" rule
/// (ADR-007 §2.3). Reusing the wrapper keeps one implementation of that discipline.
/// </summary>
public sealed class UnityBatchmodeRecipeBuildRunner : IUnityRecipeBuildRunner
{
    private readonly string _wrapperScriptPath;
    private readonly string _shellPath;

    public UnityBatchmodeRecipeBuildRunner(string wrapperScriptPath, string shellPath = "powershell.exe")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wrapperScriptPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(shellPath);
        _wrapperScriptPath = Path.GetFullPath(wrapperScriptPath);
        _shellPath = shellPath;
    }

    public override string ToString() => "UnityBatchmodeRecipeBuildRunner(<redacted>)";

    public IUnityRecipeBuildProcess Start(UnityRecipeBuildLaunch launch)
    {
        ArgumentNullException.ThrowIfNull(launch);
        var startInfo = new ProcessStartInfo(_shellPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(Path.GetDirectoryName(_wrapperScriptPath)) ?? Environment.CurrentDirectory,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(_wrapperScriptPath);
        startInfo.ArgumentList.Add("-Mode");
        startInfo.ArgumentList.Add("RecipeBuild");
        startInfo.ArgumentList.Add("-BuildRequestPath");
        startInfo.ArgumentList.Add(launch.RequestPath);
        startInfo.ArgumentList.Add("-BuildResultPath");
        startInfo.ArgumentList.Add(launch.ResultPath);
        startInfo.ArgumentList.Add("-TimeoutSeconds");
        startInfo.ArgumentList.Add(launch.TimeoutSeconds.ToString(CultureInfo.InvariantCulture));

        var process = Process.Start(startInfo)
            ?? throw new IOException("The Unity build wrapper could not be started.");
        return new SystemUnityRecipeBuildProcess(process);
    }

    private sealed class SystemUnityRecipeBuildProcess : IUnityRecipeBuildProcess
    {
        private readonly Process _process;

        internal SystemUnityRecipeBuildProcess(Process process)
        {
            _process = process;
            ProcessId = process.Id;
            StartUtc = new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
        }

        public int ProcessId { get; }

        public DateTimeOffset StartUtc { get; }

        public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
        {
            await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return _process.ExitCode;
        }

        public void Terminate()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // The process already exited; there is nothing to terminate.
            }
            catch (SystemException)
            {
                // Termination is best effort: the queue's own inspector still owns the precise
                // PID + start-time kill on cancellation, timeout and crash recovery.
            }
        }

        public void Dispose() => _process.Dispose();
    }
}
