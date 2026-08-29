namespace VFXComposer.Jobs;

/// <summary>
/// Creates the current-user store under local application data, the same placement policy as
/// the AI settings store: never inside a Unity project, no elevation, no network.
/// </summary>
public static class JobQueueFactory
{
    public static JobStore CreateCurrentUserStore(JobStoreOptions? options = null)
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new JobQueueException(JobQueueDiagnosticCodes.StoreUnavailable);
        }

        return new JobStore(Path.Combine(localApplicationData, "VFXComposer", "Jobs"), options);
    }
}
