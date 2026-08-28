using VFXComposer.Broker.Ipc;

namespace VFXComposer.Broker;

internal static class Program
{
    public static int Main()
    {
        if (ReferenceEquals(System.Reflection.Assembly.GetEntryAssembly(), typeof(Program).Assembly))
        {
            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            if (OperatingSystem.IsWindows() && args.Length == 1 &&
                string.Equals(args[0], "--user-mode-desktop-child", StringComparison.Ordinal))
            {
                return UserModeDesktopBrokerHost.RunChildModeAsync(Console.OpenStandardInput())
                    .GetAwaiter().GetResult();
            }

            if (OperatingSystem.IsWindows() && args.Length == 1 &&
                string.Equals(args[0], "--u4-scripted-worker-peer", StringComparison.Ordinal))
            {
                return UserModeDesktopBrokerHost.RunScriptedWorkerPeerAsync(
                        Console.OpenStandardInput(),
                        emitMalformedAcknowledgement: false)
                    .GetAwaiter().GetResult();
            }

            if (OperatingSystem.IsWindows() && args.Length == 1 &&
                string.Equals(args[0], "--u4-scripted-worker-peer-invalid-ack", StringComparison.Ordinal))
            {
                return UserModeDesktopBrokerHost.RunScriptedWorkerPeerAsync(
                        Console.OpenStandardInput(),
                        emitMalformedAcknowledgement: true)
                    .GetAwaiter().GetResult();
            }
        }

        // The ordinary default is a no-listener, no-Worker fail-closed process.
        Console.Error.WriteLine(BrokerDiagnosticCodes.RegistrationIssuerPending);
        return 23;
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
