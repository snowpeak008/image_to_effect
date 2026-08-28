namespace VFXComposer.Broker.ServiceHost;

internal static class Program
{
    private static int Main()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var host = new WindowsScmServiceHost(new WindowsScmNativeApi());
                _ = host.Run();
            }
        }
        catch (Exception)
        {
            // An SCM callback must not expose managed exception text or alter the
            // fixed fail-closed result.
        }

        Console.Error.Write(ServiceHostDiagnostics.ProductionIssuerPending);
        return ServiceHostDiagnostics.FailClosedExitCode;
    }
}
