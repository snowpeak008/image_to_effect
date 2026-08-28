using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Broker.ServiceHost;

namespace VFXComposer.Broker.ServiceHost.Tests;

[TestClass]
public sealed class ServiceHostBoundaryTests
{
    [TestMethod]
    public void ProductSourceHasNoForbiddenActivationSurface()
    {
        var source = string.Join(
            "\n",
            Directory.EnumerateFiles(ProductSourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase) &&
                               !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));

        var forbiddenPatterns = new[]
        {
            @"\bCreateService(?:W|A)?\b",
            @"\bOpenSCManager(?:W|A)?\b",
            @"\bDeleteService\b",
            @"\bStartService(?:W|A)?\b",
            @"\bChangeServiceConfig(?:2)?(?:W|A)?\b",
            @"\bCreateNamedPipe\b",
            @"\bNamedPipe(?:Server|Client)Stream\b",
            @"\b(?:TcpListener|Socket|HttpListener)\b",
            @"\bSystem\.Net\b",
            @"\bSystem\.IO\b",
            @"\bSystem\.Environment\b",
            @"\bProcessStartInfo\b",
            @"\bSystem\.Diagnostics\.Process\b",
            @"\bUnity(?:Engine|Editor)\b",
            @"\bAssetDatabase\b",
            @"\bProjectSettings\b",
            @"\bVFXComposer\.Broker\.Configuration\b",
            @"\bAuthority\b",
            @"\bVerdict\b",
            @"\bL3\b",
            @"\bL4\b",
        };

        foreach (var forbidden in forbiddenPatterns)
        {
            Assert.IsFalse(
                Regex.IsMatch(source, forbidden, RegexOptions.CultureInvariant),
                $"Forbidden product surface matched {forbidden}.");
        }
    }

    [TestMethod]
    public void ProductPeHasOnlyTheDeclaredInteropAndNoPublicDataContract()
    {
        var assembly = typeof(WindowsScmServiceHost).Assembly;

        Assert.AreEqual(0, assembly.GetExportedTypes().Length);
        Assert.IsFalse(assembly.GetReferencedAssemblies().Any(reference =>
            reference.Name?.Contains("Unity", StringComparison.OrdinalIgnoreCase) == true ||
            reference.Name?.Equals("VFXComposer.Broker", StringComparison.Ordinal) == true));
        Assert.IsFalse(assembly.GetTypes().Any(type =>
            type.Name.Contains("Authority", StringComparison.Ordinal) ||
            type.Name.Contains("Verdict", StringComparison.Ordinal) ||
            type.GetCustomAttributes().Any(attribute =>
                attribute.GetType().FullName is "System.SerializableAttribute" or
                "System.Runtime.Serialization.DataContractAttribute")));

        var imports = assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            .Select(method => method.GetCustomAttribute<DllImportAttribute>())
            .Where(attribute => attribute is not null)
            .Select(attribute => $"{attribute!.Value}|{attribute.EntryPoint}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "advapi32.dll|RegisterServiceCtrlHandlerExW",
                "advapi32.dll|SetServiceStatus",
                "advapi32.dll|StartServiceCtrlDispatcherW",
                "kernel32.dll|GetLastError",
            },
            imports);
    }

    private static string ProductSourceRoot
    {
        get
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "VFXComposer.sln")))
                {
                    return Path.Combine(current.FullName, "services", "VFXComposer.Broker.ServiceHost");
                }

                current = current.Parent;
            }

            throw new InvalidOperationException("The repository root is unavailable to the test host.");
        }
    }
}
