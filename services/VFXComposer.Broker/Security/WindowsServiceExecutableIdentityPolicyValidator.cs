using VFXComposer.Broker.Configuration;
using VFXComposer.Protocol.Ipc;

namespace VFXComposer.Broker.Security;

/// <summary>
/// Pure in-memory comparison for the dormant executable-content identity
/// policy. It returns only a Boolean and remains detached from the Broker
/// production entry point.
/// </summary>
internal static class WindowsServiceExecutableIdentityPolicyValidator
{
    internal static bool MatchesDormantCandidate(
        WindowsServiceExecutableIdentityPolicy? candidate,
        ProductionTrustProfile? expectedProfile,
        WindowsServiceExecutableContentIdentity? expectedExecutableIdentity)
    {
        if (candidate is null || expectedProfile is null || expectedExecutableIdentity is null)
        {
            return false;
        }

        return string.Equals(
                   candidate.ExecutableContentIdentity.TypeTag,
                   WindowsServiceExecutableContentIdentity.ExecutableContentIdentityType,
                   StringComparison.Ordinal) &&
               string.Equals(
                   expectedExecutableIdentity.ExecutableContentIdentity.TypeTag,
                   WindowsServiceExecutableContentIdentity.ExecutableContentIdentityType,
                   StringComparison.Ordinal) &&
               string.Equals(
                   candidate.ProcessImageIdentity.TypeTag,
                   PeerHello.ProcessImageIdentityType,
                   StringComparison.Ordinal) &&
               string.Equals(
                   expectedExecutableIdentity.ProcessImageIdentity.TypeTag,
                   PeerHello.ProcessImageIdentityType,
                   StringComparison.Ordinal) &&
               candidate.ExecutableByteLength > 0 &&
               expectedExecutableIdentity.ExecutableByteLength > 0 &&
               candidate.BrokerGeneration == expectedProfile.BrokerGeneration &&
               candidate.BrokerGeneration == expectedExecutableIdentity.BrokerGeneration &&
               candidate.ServiceSid.FixedEquals(expectedProfile.ServiceSid) &&
               candidate.ServiceSid.FixedEquals(expectedExecutableIdentity.ServiceSid) &&
               candidate.ProcessImageIdentity.FixedTimeEquals(
                   expectedExecutableIdentity.ProcessImageIdentity) &&
               candidate.ExecutableByteLength == expectedExecutableIdentity.ExecutableByteLength &&
               candidate.ExecutableContentIdentity.FixedTimeEquals(
                   expectedExecutableIdentity.ExecutableContentIdentity) &&
               candidate.HasExactIdentityBinding(expectedProfile, expectedExecutableIdentity);
    }
}
