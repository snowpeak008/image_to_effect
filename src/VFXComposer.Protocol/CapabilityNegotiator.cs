namespace VFXComposer.Protocol;

/// <summary>Computes a deterministic intersection constrained to the frozen allow-list.</summary>
public static class CapabilityNegotiator
{
    public static IReadOnlyList<string> Negotiate(
        IEnumerable<string> offered,
        IEnumerable<string> supported)
    {
        ArgumentNullException.ThrowIfNull(offered);
        ArgumentNullException.ThrowIfNull(supported);

        var supportedSet = new HashSet<string>(
            supported.Where(CapabilityIds.IsKnown),
            StringComparer.Ordinal);

        return Array.AsReadOnly(offered
            .Where(CapabilityIds.IsKnown)
            .Where(supportedSet.Contains)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray());
    }
}
