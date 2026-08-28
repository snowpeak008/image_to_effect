using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace VFXComposer.Protocol.Tests;

[TestClass]
public sealed class DependencyBoundaryTests
{
    private static readonly string[] ProhibitedTypePrefixes =
    [
        "UnityEngine",
        "UnityEditor",
        "Avalonia",
        "System.IO.File",
        "System.IO.Directory",
        "System.IO.Pipes",
        "System.Net.Sockets",
        "System.Net.Http",
    ];

    [TestMethod]
    public void ProtocolAssemblyHasNoUnityAvaloniaTransportOrFileDirectoryTypeReferences()
    {
        var assembly = typeof(ProtocolVersions).Assembly;
        using var stream = File.OpenRead(assembly.Location);
        using var peReader = new PEReader(stream);
        var metadata = peReader.GetMetadataReader();
        var typeReferences = metadata.TypeReferences
            .Select(handle => metadata.GetTypeReference(handle))
            .Select(reference =>
            {
                var typeNamespace = metadata.GetString(reference.Namespace);
                var typeName = metadata.GetString(reference.Name);
                return string.IsNullOrEmpty(typeNamespace)
                    ? typeName
                    : typeNamespace + "." + typeName;
            })
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var prefix in ProhibitedTypePrefixes)
        {
            Assert.IsFalse(
                typeReferences.Any(reference => reference.StartsWith(prefix, StringComparison.Ordinal)),
                $"Protocol references prohibited type prefix {prefix}." );
        }

        var assemblyReferences = metadata.AssemblyReferences
            .Select(handle => metadata.GetAssemblyReference(handle))
            .Select(reference => metadata.GetString(reference.Name))
            .ToArray();
        Assert.IsFalse(assemblyReferences.Any(name => name.Contains("Unity", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(assemblyReferences.Any(name => name.Contains("Avalonia", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void ProtocolPublicSurfaceContainsNoTransportProjectPathOrNativeHandleCapability()
    {
        var prohibitedFragments = new[]
        {
            "AbsolutePath",
            "ProjectPath",
            "CallerPath",
            "NamedPipe",
            "Socket",
            "Endpoint",
            "NativeHandle",
            "FileHandle",
            "DirectoryHandle",
        };
        var exported = typeof(ProtocolVersions).Assembly.GetExportedTypes();

        foreach (var type in exported)
        {
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                foreach (var fragment in prohibitedFragments)
                {
                    Assert.IsFalse(
                        member.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase),
                        $"Protocol member exposes prohibited capability: {type.FullName}.{member.Name}." );
                }
            }
        }
    }
}
