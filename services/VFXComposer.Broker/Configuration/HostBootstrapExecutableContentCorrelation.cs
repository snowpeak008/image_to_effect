using VFXComposer.Broker.Native;
using VFXComposer.Broker.Security;

namespace VFXComposer.Broker.Configuration;

/// <summary>
/// Pure Boolean correlation between a dormant observed content fact and the
/// complete supplied executable-identity policy binding. It does not issue an
/// admission, lease, or authority result.
/// </summary>
internal static class HostBootstrapExecutableContentCorrelation
{
    internal static bool MatchesObservedContent(
        WindowsPinnedExecutableContentObservation? observation,
        WindowsServiceExecutableIdentityPolicy? candidatePolicy,
        ProductionTrustProfile? expectedProfile,
        WindowsServiceExecutableContentIdentity? expectedExecutableIdentity)
    {
        if (observation is null ||
            candidatePolicy is null ||
            expectedProfile is null ||
            expectedExecutableIdentity is null)
        {
            return false;
        }

        try
        {
            return WindowsServiceExecutableIdentityPolicyValidator.MatchesDormantCandidate(
                       candidatePolicy,
                       expectedProfile,
                       expectedExecutableIdentity) &&
                   observation.ByteLength == candidatePolicy.ExecutableByteLength &&
                   observation.ByteLength == expectedExecutableIdentity.ExecutableByteLength &&
                   observation.ContentHash.FixedTimeEquals(
                       candidatePolicy.ExecutableContentIdentity) &&
                   observation.ContentHash.FixedTimeEquals(
                       expectedExecutableIdentity.ExecutableContentIdentity);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
