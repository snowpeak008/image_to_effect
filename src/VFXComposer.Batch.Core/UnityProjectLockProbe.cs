using System.Diagnostics;
using System.Text.Json;
using VFXComposer.Jobs;

namespace VFXComposer.Batch.Core;

/// <summary>
/// Real Unity project-lock probe. It answers "is a live editor holding this exact project" using two
/// project-scoped signals and no process command-line inspection, so it needs no extra dependency:
///
/// <list type="number">
/// <item><c>Temp/UnityLockfile</c> is held open by a running editor for as long as it owns the
/// project, so failing to open it exclusively for reading is direct evidence of a live owner.</item>
/// <item><c>Library/EditorInstance.json</c> records the owning editor's process id; a live process
/// with that id whose name is the Unity editor is the second, independent signal.</item>
/// </list>
///
/// <para>Residue alone is never treated as busy: a force-killed editor leaves both files behind, and
/// reporting busy for residue would wedge the queue forever. The reporting edges are therefore biased
/// towards letting the build start, because the write boundary is still protected — the build wrapper
/// repeats the same check and refuses to steal the lock (exit 73), which the orchestrator maps to a
/// stable failure instead of a partial write.</para>
///
/// <para>Known false-negative edges: a recorded process id recycled by an unrelated process is not
/// treated as busy (the process name must match); a corrupt or truncated
/// <c>EditorInstance.json</c> is treated as residue; and an editor that has just exited may keep the
/// lock file open for a moment, which produces a one-poll-late busy rather than a wrong answer.</para>
/// </summary>
public sealed class UnityProjectLockProbe : IProjectLockProbe
{
    private const string EditorProcessName = "Unity";
    private const int MaximumEditorInstanceBytes = 64 * 1024;

    private readonly string _lockFilePath;
    private readonly string _editorInstancePath;
    private readonly Func<int, string?> _readProcessName;

    /// <summary>Probes the given Unity project directory.</summary>
    public UnityProjectLockProbe(string projectPath)
        : this(projectPath, readProcessName: null)
    {
    }

    /// <summary>
    /// Test seam: process-name resolution is injectable so the stale-versus-live rule can be covered
    /// without starting a real editor.
    /// </summary>
    public UnityProjectLockProbe(string projectPath, Func<int, string?>? readProcessName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectPath));
        _lockFilePath = Path.Combine(root, "Temp", "UnityLockfile");
        _editorInstancePath = Path.Combine(root, "Library", "EditorInstance.json");
        _readProcessName = readProcessName ?? ReadLiveProcessName;
    }

    public override string ToString() => "UnityProjectLockProbe(<redacted>)";

    public ProjectLockAvailability Probe() =>
        IsLockFileHeld() || IsRecordedEditorAlive()
            ? ProjectLockAvailability.Busy
            : ProjectLockAvailability.Free;

    private bool IsLockFileHeld()
    {
        if (!File.Exists(_lockFilePath))
        {
            return false;
        }

        try
        {
            // Read-only exclusive open: it never modifies the file, and it fails exactly while a
            // live editor holds the handle.
            using (new FileStream(_lockFilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                return false;
            }
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private bool IsRecordedEditorAlive()
    {
        var processId = TryReadRecordedProcessId();
        if (processId is null)
        {
            return false;
        }

        var name = _readProcessName(processId.Value);
        return name is not null && string.Equals(name, EditorProcessName, StringComparison.OrdinalIgnoreCase);
    }

    private int? TryReadRecordedProcessId()
    {
        try
        {
            var info = new FileInfo(_editorInstancePath);
            if (!info.Exists || info.Length > MaximumEditorInstanceBytes)
            {
                return null;
            }

            using var stream = new FileStream(_editorInstancePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var document = JsonDocument.Parse(stream);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty("process_id", out var value) &&
                   value.ValueKind == JsonValueKind.Number &&
                   value.TryGetInt32(out var processId) &&
                   processId > 0
                ? processId
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static string? ReadLiveProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.HasExited ? null : process.ProcessName;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or SystemException)
        {
            return null;
        }
    }
}
