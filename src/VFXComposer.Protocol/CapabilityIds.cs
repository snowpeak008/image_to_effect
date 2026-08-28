using System.Collections.Frozen;

namespace VFXComposer.Protocol;

/// <summary>Capabilities understood by the Phase 1 protocol.</summary>
public static class CapabilityIds
{
    public const string HandshakeV1 = "protocol.handshake.v1";
    public const string StatusSnapshotV1 = "status.snapshot.v1";
    public const string StableDiagnosticsV1 = "diagnostics.stable.v1";

    private static readonly FrozenSet<string> KnownCapabilities =
        new[]
        {
            HandshakeV1,
            StatusSnapshotV1,
            StableDiagnosticsV1,
        }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => KnownCapabilities;

    public static bool IsKnown(string? capabilityId) =>
        capabilityId is not null && KnownCapabilities.Contains(capabilityId);
}
