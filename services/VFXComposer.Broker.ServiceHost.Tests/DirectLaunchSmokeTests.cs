using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Broker.ServiceHost;

namespace VFXComposer.Broker.ServiceHost.Tests;

[TestClass]
public sealed class DirectLaunchSmokeTests
{
    [TestMethod]
    public async Task DirectExecutableFailsClosedWithExactDiagnosticAndStreams()
    {
        var assemblyPath = typeof(ServiceLifecycle).Assembly.Location;
        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(assemblyPath);

        using var process = Process.Start(startInfo);
        Assert.IsNotNull(process);
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        Assert.AreEqual(ServiceHostDiagnostics.FailClosedExitCode, process.ExitCode);
        Assert.AreEqual(string.Empty, await standardOutput);
        Assert.AreEqual(ServiceHostDiagnostics.ProductionIssuerPending, await standardError);
    }
}
