namespace VFXComposer.Cli.Tests;

/// <summary>
/// Locates checked-in repository fixtures (the <c>batches/</c> samples) from a test binary, by
/// walking up to the solution that roots the tree. Shared by the manifest-validity guard and the
/// end-to-end flow so both consume the exact file that ships.
/// </summary>
internal static class TestRepository
{
    public static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "VFXComposer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate VFXComposer.sln above " + AppContext.BaseDirectory + ".");
    }

    /// <summary>The checked-in sample batch manifest that the F6 flow-two acceptance consumes.</summary>
    public static string SampleManifestPath() =>
        Path.Combine(Root(), "batches", "sample-batch.manifest.json");
}
