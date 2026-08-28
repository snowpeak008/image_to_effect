using System.Collections.Frozen;

namespace VFXComposer.Protocol;

/// <summary>Wire protocol versions implemented by this assembly.</summary>
public static class ProtocolVersions
{
    public const string V1 = "vfxcomposer.protocol/1.0";

    public const string Current = V1;

    private static readonly FrozenSet<string> SupportedVersions =
        new[] { V1 }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> Supported => SupportedVersions;

    public static bool IsSupported(string? version) =>
        version is not null && SupportedVersions.Contains(version);
}
