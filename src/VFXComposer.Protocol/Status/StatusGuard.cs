namespace VFXComposer.Protocol.Status;

internal static class StatusGuard
{
    public static string Validate(
        string state,
        IReadOnlySet<string> allowedStates,
        string domain,
        StatusProvenance? provenance,
        bool provenanceRequired)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!allowedStates.Contains(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        if (provenanceRequired && provenance is null)
        {
            throw new ArgumentException("This state requires provenance.", nameof(provenance));
        }

        if (provenance is not null &&
            !string.Equals(provenance.StatusDomain, domain, StringComparison.Ordinal))
        {
            throw new ArgumentException("Status provenance belongs to a different authority domain.", nameof(provenance));
        }

        return state;
    }
}
