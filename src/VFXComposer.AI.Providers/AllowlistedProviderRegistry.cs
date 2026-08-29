using System.Collections.Frozen;
using VFXComposer.AI.Contracts;

namespace VFXComposer.AI.Providers;

/// <summary>
/// A1 intentionally exposes a closed descriptive allow-list only. It has no registration API, DLL loader,
/// script hook, header-template surface, adapter implementation, or network client.
/// </summary>
public sealed class AllowlistedProviderRegistry
{
    private static readonly FrozenSet<string> KnownProtocols = new[]
    {
        ProviderProtocols.OpenAiCompatibleV1,
    }.ToFrozenSet(StringComparer.Ordinal);

    public static AllowlistedProviderRegistry Default { get; } = new();

    private AllowlistedProviderRegistry()
    {
    }

    public bool IsAllowed(ProtocolBinding protocol) =>
        protocol is not null && KnownProtocols.Contains(protocol.ProtocolId);

    public IReadOnlySet<string> ProtocolIds => KnownProtocols;

    public override string ToString() => "AllowlistedProviderRegistry(" + KnownProtocols.Count + ")";
}
