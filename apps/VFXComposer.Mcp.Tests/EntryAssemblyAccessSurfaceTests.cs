using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Cli;
using VFXComposer.Mcp;

namespace VFXComposer.Mcp.Tests;

/// <summary>
/// The access surface of the two entry executables, read straight out of their compiled metadata.
/// <c>vfxc</c> and <c>vfxc-mcp</c> own files by design — a manifest goes in, a report and a queue
/// store come out — so unlike the Desktop scan this one does not forbid filesystem access. What it
/// forbids is everything an entry surface must never be able to do at all: link the Unity editor
/// API, name a location inside a Unity project, open a socket, or leave managed code.
///
/// Reading the metadata tables rather than walking IL is what makes the result exhaustive: the
/// type-reference and member-reference tables list every external member the assembly names
/// anywhere — signature, local, attribute or instruction — and the user-string heap lists every
/// literal it can push.
/// </summary>
[TestClass]
public sealed class EntryAssemblyAccessSurfaceTests
{
    /// <summary>Namespaces no entry assembly may name. Filesystem and process are theirs to use.</summary>
    private static readonly string[] ProhibitedNamespacePrefixes =
    [
        "System.Net",
        "UnityEditor",
        "UnityEngine",
    ];

    /// <summary>
    /// Identifier fragments that would mean a transport or an editor API is being named here. Note
    /// that "Unity" itself is deliberately absent: starting one short-lived Unity batchmode process
    /// through the repository wrapper is exactly what the CLI is for (ADR-007 §2.3), so the
    /// execution-layer types that do it are named from these assemblies on purpose.
    /// </summary>
    private static readonly string[] ProhibitedIdentifierFragments =
    [
        "AssetDatabase",
        "EditorPrefs",
        "Http",
        "Listen",
        "NamedPipe",
        "Socket",
        "Tcp",
        "Udp",
    ];

    /// <summary>Literal fragments that would hard-code a project location or a transport type.</summary>
    private static readonly string[] ProhibitedLiteralFragments =
    [
        "Assets/",
        "Assets\\",
        "AssetDatabase",
        "Packages/",
        "Packages\\",
        "ProjectSettings/",
        "ProjectSettings\\",
        "System.Net.",
        "UnityEditor.",
        "UnityEngine.",
    ];

    private const string FixtureLiteral = "UnityEditor.AssetDatabase";

    [TestMethod]
    public void BothEntryExecutablesStayInsideTheirAccessSurface()
    {
        foreach (var assembly in new[] { typeof(CliRunner).Assembly, typeof(McpServer).Assembly })
        {
            var violations = Scan(assembly);

            Assert.AreEqual(
                0,
                violations.Length,
                assembly.GetName().Name + " access-surface violations:\n" + string.Join("\n", violations));
        }
    }

    [TestMethod]
    public void TheTwoScannedAssembliesAreTheEntryExecutablesAndNothingElse()
    {
        CollectionAssert.AreEquivalent(
            new[] { "vfxc", "vfxc-mcp" },
            new[] { typeof(CliRunner).Assembly, typeof(McpServer).Assembly }
                .Select(static assembly => assembly.GetName().Name)
                .ToArray());
    }

    [TestMethod]
    public void TheScannerFindsTheProhibitedReferencesPlantedInItsOwnAssembly()
    {
        var violations = Scan(typeof(EntryAssemblyAccessSurfaceTests).Assembly);

        Assert.IsTrue(
            violations.Any(static violation => violation.Contains("System.Net.IPAddress", StringComparison.Ordinal)),
            "The scanner must report the prohibited type reference the fixture below plants.");
        Assert.IsTrue(
            violations.Any(static violation => violation.Contains("UnityEditor.", StringComparison.Ordinal)),
            "The scanner must report the prohibited literal the fixture below plants.");
    }

    /// <summary>
    /// The planted violation the self-check looks for: a prohibited type reference and a prohibited
    /// literal, both in this test assembly and never in a product one.
    /// </summary>
    private static bool ProhibitedFixture() =>
        System.Net.IPAddress.IsLoopback(System.Net.IPAddress.Loopback) && FixtureLiteral.Length > 0;

    private static string[] Scan(Assembly assembly)
    {
        var violations = new List<string>();
        var name = assembly.GetName().Name ?? "<unnamed>";
        using var stream = File.OpenRead(assembly.Location);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();

        foreach (var handle in reader.AssemblyReferences)
        {
            var referenced = reader.GetString(reader.GetAssemblyReference(handle).Name);
            if (referenced.StartsWith("Unity", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(name + ": prohibited Unity assembly reference " + referenced + ".");
            }
        }

        foreach (var handle in reader.TypeReferences)
        {
            var typeReference = reader.GetTypeReference(handle);
            var fullName = Qualify(
                reader.GetString(typeReference.Namespace),
                reader.GetString(typeReference.Name));
            violations.AddRange(CheckTypeName(name, fullName));
        }

        foreach (var handle in reader.TypeDefinitions)
        {
            var typeDefinition = reader.GetTypeDefinition(handle);
            var fullName = Qualify(
                reader.GetString(typeDefinition.Namespace),
                reader.GetString(typeDefinition.Name));
            violations.AddRange(CheckIdentifier(name, fullName, "defined type"));
        }

        foreach (var handle in reader.MemberReferences)
        {
            var member = reader.GetString(reader.GetMemberReference(handle).Name);
            violations.AddRange(CheckIdentifier(name, member, "member reference"));
        }

        foreach (var literal in UserStrings(reader))
        {
            foreach (var fragment in ProhibitedLiteralFragments)
            {
                if (literal.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add(name + ": prohibited literal fragment " + fragment + ".");
                }
            }
        }

        var pinvokes = reader.MethodDefinitions
            .Select(reader.GetMethodDefinition)
            .Count(static method => (method.Attributes & MethodAttributes.PinvokeImpl) != 0);
        if (pinvokes > 0)
        {
            violations.Add(name + ": " + pinvokes + " native P/Invoke declaration(s); managed code only.");
        }

        return violations.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<string> CheckTypeName(string assemblyName, string fullName)
    {
        foreach (var prefix in ProhibitedNamespacePrefixes)
        {
            if (fullName.StartsWith(prefix + ".", StringComparison.Ordinal) ||
                string.Equals(fullName, prefix, StringComparison.Ordinal))
            {
                yield return assemblyName + ": prohibited type reference " + fullName + ".";
            }
        }

        foreach (var violation in CheckIdentifier(assemblyName, fullName, "type reference"))
        {
            yield return violation;
        }
    }

    private static IEnumerable<string> CheckIdentifier(string assemblyName, string identifier, string context)
    {
        foreach (var fragment in ProhibitedIdentifierFragments)
        {
            if (identifier.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                yield return assemblyName + ": prohibited identifier fragment " + fragment +
                    " in " + context + " " + identifier + ".";
            }
        }
    }

    /// <summary>
    /// Every literal in the user-string heap. The heap is a self-describing blob rather than a
    /// table, so it is walked by handle from its first entry until it runs out.
    /// </summary>
    private static ImmutableArray<string> UserStrings(MetadataReader reader)
    {
        var literals = ImmutableArray.CreateBuilder<string>();
        var handle = MetadataTokens.UserStringHandle(0);
        while (true)
        {
            handle = reader.GetNextHandle(handle);
            if (handle.IsNil)
            {
                return literals.ToImmutable();
            }

            literals.Add(reader.GetUserString(handle));
        }
    }

    private static string Qualify(string @namespace, string name) =>
        string.IsNullOrEmpty(@namespace) ? name : @namespace + "." + name;
}
