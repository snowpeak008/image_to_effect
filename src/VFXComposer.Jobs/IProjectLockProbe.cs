namespace VFXComposer.Jobs;

/// <summary>Observed availability of the Unity project for a build-class payload.</summary>
public enum ProjectLockAvailability
{
    /// <summary>No live editor owns the project; execution may start.</summary>
    Free,

    /// <summary>A live editor instance owns the project; the queue must wait, not fail.</summary>
    Busy,
}

/// <summary>
/// Detection seam for the Unity project lock, semantics aligned with the Invoke-Unity
/// discipline: a live editor process means busy; a stale lock file alone does not.
/// The concrete Unity-facing probe arrives with the F2 build payloads.
/// </summary>
public interface IProjectLockProbe
{
    ProjectLockAvailability Probe();
}

/// <summary>Default probe for hosts that run no project-bound payloads.</summary>
public sealed class AlwaysFreeProjectLockProbe : IProjectLockProbe
{
    public ProjectLockAvailability Probe() => ProjectLockAvailability.Free;
}
