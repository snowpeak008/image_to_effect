using System.Collections.Frozen;

namespace VFXComposer.Protocol.Ipc;

public static class PeerCapabilityIds
{
    public const string PeerSessionV1 = "broker.peer-session.v1";
    public const string ProjectRegistrationV1 = "project.registration.v1";
    public const string ReadOnlyQueryV1 = "project.readonly-query.v1";
    public const string ProjectSelectionV1 = "project.selection.v1";
    public const string WorkerProjectLocatorV1 = "worker.project-locator.v1";
    public const string WorkerHandleLifecycleV1 = "worker.handle-lifecycle.v1";

    private static readonly FrozenSet<string> KnownCapabilities =
        new[]
        {
            PeerSessionV1,
            ProjectRegistrationV1,
            ReadOnlyQueryV1,
            ProjectSelectionV1,
            WorkerProjectLocatorV1,
            WorkerHandleLifecycleV1,
        }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => KnownCapabilities;
}
