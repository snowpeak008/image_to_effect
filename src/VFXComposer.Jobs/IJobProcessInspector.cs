using System.Diagnostics;

namespace VFXComposer.Jobs;

/// <summary>
/// Exact child-process disposal: any termination requires both the recorded PID and the
/// recorded process start time to match, so a reused PID is never killed (REQ-003-08).
/// </summary>
public interface IJobProcessInspector
{
    /// <summary>Terminates the process only when PID and start time both match; otherwise does nothing.</summary>
    void TerminateExact(int processId, DateTimeOffset processStartUtc);
}

/// <summary>Real implementation over <see cref="Process"/> with a one-second start-time tolerance.</summary>
public sealed class SystemJobProcessInspector : IJobProcessInspector
{
    private static readonly TimeSpan StartTimeTolerance = TimeSpan.FromSeconds(1);

    public void TerminateExact(int processId, DateTimeOffset processStartUtc)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            var actualStartUtc = new DateTimeOffset(process.StartTime.ToUniversalTime());
            if ((actualStartUtc - processStartUtc).Duration() > StartTimeTolerance)
            {
                return;
            }

            process.Kill(entireProcessTree: true);
            process.WaitForExit(5_000);
        }
        catch (ArgumentException)
        {
            // The process no longer exists; nothing to dispose.
        }
        catch (InvalidOperationException)
        {
            // The process exited while being inspected; nothing to dispose.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Access to the process was denied; a foreign process is never forced.
        }
    }
}
