namespace VFXComposer.Batch.Core;

/// <summary>
/// Where the restricted build lives: the Unity project directory and the build wrapper script. Both
/// are repository paths, so entry surfaces discover them instead of hard-coding a machine layout.
/// </summary>
public sealed record UnityBuildHost(string RepositoryRoot, string ProjectPath, string WrapperScriptPath);

/// <summary>
/// Locates the repository that owns the Unity project. Discovery walks up from the running
/// assembly, which is how every entry surface in this repository is launched, and an explicit
/// environment override exists for hosts started from elsewhere. A host that cannot find both the
/// project and the wrapper reports no build capability rather than guessing a path.
/// </summary>
public static class UnityBuildHostLocator
{
    /// <summary>Explicit override for the repository root, checked before any directory walk.</summary>
    public const string RepositoryRootVariable = "VFXCOMPOSER_REPOSITORY_ROOT";

    private const int MaximumAncestorsSearched = 12;

    /// <summary>Returns the located host, or null when this machine cannot run a restricted build.</summary>
    public static UnityBuildHost? TryLocate()
    {
        var declared = Environment.GetEnvironmentVariable(RepositoryRootVariable);
        if (!string.IsNullOrWhiteSpace(declared))
        {
            return TryLocateAt(declared);
        }

        var current = AppContext.BaseDirectory;
        for (var depth = 0; depth < MaximumAncestorsSearched && !string.IsNullOrEmpty(current); depth++)
        {
            var located = TryLocateAt(current);
            if (located is not null)
            {
                return located;
            }

            current = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(current));
        }

        return null;
    }

    /// <summary>Returns the host rooted at the given directory when it holds both required parts.</summary>
    public static UnityBuildHost? TryLocateAt(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        string root;
        try
        {
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRoot));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        var projectPath = Path.Combine(root, "project");
        var wrapperPath = Path.Combine(root, "tools", "Invoke-Unity.ps1");
        return Directory.Exists(Path.Combine(projectPath, "Assets")) && File.Exists(wrapperPath)
            ? new UnityBuildHost(root, projectPath, wrapperPath)
            : null;
    }
}
