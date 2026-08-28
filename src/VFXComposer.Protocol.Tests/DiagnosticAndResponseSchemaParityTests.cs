using VFXComposer.Protocol.Diagnostics;

namespace VFXComposer.Protocol.Tests;

[TestClass]
public sealed class DiagnosticAndResponseSchemaParityTests
{
    [TestMethod]
    public void DiagnosticSchemaExactlyMatchesTheFixedCatalog()
    {
        using var schema = ProtocolSchemaTestSupport.LoadSchema(WireSchemaIds.DiagnosticV1);
        var schemaCodes = schema.RootElement
            .GetProperty("properties")
            .GetProperty("code")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(
            StableDiagnosticCodes.All.Order(StringComparer.Ordinal).ToArray(),
            schemaCodes);

        var schemaDefinitions = schema.RootElement
            .GetProperty("oneOf")
            .EnumerateArray()
            .Select(reference => reference.GetProperty("$ref").GetString()!.Split('/')[^1])
            .Select(name => schema.RootElement.GetProperty("$defs").GetProperty(name).GetProperty("properties"))
            .Select(properties => string.Join(
                "|",
                properties.GetProperty("code").GetProperty("const").GetString(),
                properties.GetProperty("severity").GetProperty("const").GetString(),
                properties.GetProperty("message").GetProperty("const").GetString(),
                properties.GetProperty("retryable").GetProperty("const").GetBoolean()))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var catalogDefinitions = StableDiagnosticCatalog.All.Values
            .Select(definition => string.Join(
                "|",
                definition.Code,
                definition.Severity,
                definition.Message,
                definition.Retryable))
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(catalogDefinitions, schemaDefinitions);
        foreach (var definition in StableDiagnosticCatalog.All.Values)
        {
            var diagnostic = StableDiagnosticCatalog.Create(definition.Code);
            Assert.AreEqual(definition, new StableDiagnosticDefinition(
                diagnostic.Code,
                diagnostic.Severity,
                diagnostic.Message,
                diagnostic.Retryable));
        }
    }

    [TestMethod]
    public void ResponseSchemaReferencesTheStandaloneDiagnosticWithoutAnEmbeddedCopy()
    {
        using var response = ProtocolSchemaTestSupport.LoadSchema(WireSchemaIds.HandshakeResponseV1);
        var diagnosticAlternatives = response.RootElement
            .GetProperty("properties")
            .GetProperty("diagnostic")
            .GetProperty("oneOf");
        Assert.AreEqual(
            "vfxcomposer-diagnostic-v1.schema.json",
            diagnosticAlternatives[1].GetProperty("$ref").GetString());
        Assert.IsFalse(response.RootElement.GetProperty("$defs").TryGetProperty("diagnostic", out _));
    }

    [TestMethod]
    public void ResponseSchemaEnumeratesExactlyEveryKnownSortedCapabilitySubset()
    {
        using var response = ProtocolSchemaTestSupport.LoadSchema(WireSchemaIds.HandshakeResponseV1);
        var actual = response.RootElement
            .GetProperty("$defs")
            .GetProperty("negotiatedCapabilities")
            .GetProperty("oneOf")
            .EnumerateArray()
            .Select(branch => string.Join(
                "\0",
                branch.GetProperty("const").EnumerateArray().Select(value => value.GetString())))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var known = CapabilityIds.All.Order(StringComparer.Ordinal).ToArray();
        var expected = Enumerable.Range(0, 1 << known.Length)
            .Select(mask => string.Join(
                "\0",
                known.Where((_, index) => (mask & (1 << index)) != 0)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(expected, actual);
        foreach (var subset in expected)
        {
            var capabilities = subset.Length == 0
                ? Array.Empty<string>()
                : subset.Split('\0');
            Assert.IsNotNull(Handshake.HandshakeResponse.Accept(
                "request-01",
                "broker-01",
                capabilities));
        }
    }
}
