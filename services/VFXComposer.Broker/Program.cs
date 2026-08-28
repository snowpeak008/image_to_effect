using VFXComposer.Broker.Configuration;

namespace VFXComposer.Broker;

internal static class Program
{
    public static int Main()
    {
        if (!BrokerPolicy.TryLoadProduction(out _))
        {
            Console.Error.WriteLine(BrokerDiagnosticCodes.RegistrationIssuerPending);
            return 23;
        }

        // Production listening is intentionally unreachable until a host-owned policy
        // issuer and the Windows peer-facts gate are independently accepted.
        Console.Error.WriteLine(BrokerDiagnosticCodes.PeerAuthenticatorPending);
        return 24;
    }
}

internal static class BrokerDiagnosticCodes
{
    public const string RegistrationIssuerPending = "W24FS001";
    public const string PeerAuthenticatorPending = "VFXB0001";
    public const string PeerRejected = "VFXB0002";
    public const string SessionStale = "VFXB0003";
    public const string ProjectUnavailable = "VFXB0004";
    public const string QueryRejected = "VFXB0005";
}
