using VFXComposer.AI.Contracts;

namespace VFXComposer.AI.Providers;

/// <summary>In-memory health state scoped to an exact profile/capability/channel and configuration fingerprint.</summary>
public sealed class ProviderHealthRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<HealthKey, ProviderHealth> _entries = [];

    public void Record(ProviderHealth health)
    {
        ArgumentNullException.ThrowIfNull(health);
        lock (_gate)
        {
            _entries[new HealthKey(health.ProfileId, health.CapabilityId, health.Channel)] = health;
        }
    }

    public ProviderHealth? Get(string profileId, string capabilityId, AiChannel channel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityId);
        lock (_gate)
        {
            _entries.TryGetValue(new HealthKey(profileId, capabilityId, channel), out var health);
            return health;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }
    }

    private readonly record struct HealthKey(string ProfileId, string CapabilityId, AiChannel Channel);
}
