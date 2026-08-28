using VFXComposer.Broker.Configuration;
using VFXComposer.Protocol.Ipc;

namespace VFXComposer.Broker.Security;

/// <summary>
/// Pure in-memory comparison for the dormant installation-policy candidate.
/// It returns only a Boolean and remains detached from the Broker production
/// entry point.
/// </summary>
internal static class WindowsServiceInstallationPolicyValidator
{
    internal static bool MatchesDormantCandidate(
        WindowsServiceInstallationPolicy? candidate,
        ProductionTrustProfile? expectedProfile,
        WindowsServiceInstallationIdentity? expectedServiceIdentity)
    {
        if (candidate is null || expectedProfile is null || expectedServiceIdentity is null)
        {
            return false;
        }

        return string.Equals(
                   candidate.ServiceName,
                   WindowsServiceInstallationPolicy.FixedServiceName,
                   StringComparison.Ordinal) &&
               string.Equals(
                   candidate.DisplayName,
                   WindowsServiceInstallationPolicy.FixedDisplayName,
                   StringComparison.Ordinal) &&
               candidate.ServiceType == WindowsServiceType.Win32OwnProcess &&
               candidate.Account == WindowsServiceAccount.LocalService &&
               string.Equals(
                   candidate.ServiceAccountName,
                   WindowsServiceInstallationPolicy.LocalServiceAccountName,
                   StringComparison.Ordinal) &&
               candidate.CredentialMode == WindowsServiceCredentialMode.None &&
               candidate.StartMode == WindowsServiceStartMode.Demand &&
               candidate.ErrorControl == WindowsServiceErrorControl.Normal &&
               candidate.ServiceSidType == WindowsServiceSidType.Restricted &&
               candidate.RecoveryMode == WindowsServiceRecoveryMode.None &&
               candidate.Flags == WindowsServiceInstallationFlags.None &&
               string.Equals(
                   candidate.ServiceImageIdentity.TypeTag,
                   PeerHello.ProcessImageIdentityType,
                   StringComparison.Ordinal) &&
               string.Equals(
                   expectedServiceIdentity.ServiceImageIdentity.TypeTag,
                   PeerHello.ProcessImageIdentityType,
                   StringComparison.Ordinal) &&
               candidate.HasExactIdentityBinding(expectedProfile, expectedServiceIdentity);
    }
}
