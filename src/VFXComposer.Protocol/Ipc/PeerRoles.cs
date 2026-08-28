using System.Collections.Frozen;

namespace VFXComposer.Protocol.Ipc;

public static class PeerRoles
{
    public const string Desktop = "DESKTOP";
    public const string Worker = "WORKER";

    private static readonly FrozenSet<string> KnownRoles =
        new[] { Desktop, Worker }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => KnownRoles;

    internal static string Require(string value, string parameterName) =>
        KnownRoles.Contains(value)
            ? value
            : throw new ArgumentOutOfRangeException(parameterName);
}
