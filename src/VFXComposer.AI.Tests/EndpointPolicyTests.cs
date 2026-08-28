using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Providers;

namespace VFXComposer.AI.Tests;

[TestClass]
public sealed class EndpointPolicyTests
{
    [TestMethod]
    public void SharedVectors_KeepPolicyAndCanonicalCodecInAgreement()
    {
        using var vectors = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "EndpointPolicyVectors.json")));
        Assert.AreEqual(1, vectors.RootElement.GetProperty("formatVersion").GetInt32());

        foreach (var vector in vectors.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var name = vector.GetProperty("name").GetString()!;
            var uri = vector.GetProperty("uri").GetString()!;
            var allowLoopbackHttp = vector.GetProperty("allowLoopbackHttp").GetBoolean();
            var secretScope = Enum.Parse<SecretScope>(vector.GetProperty("secretScope").GetString()!, ignoreCase: false);
            var expected = vector.GetProperty("expected").GetBoolean();

            var accepted = EndpointPolicy.TryCreate(uri, allowLoopbackHttp, secretScope, out var endpoint);
            Assert.AreEqual(expected, accepted, name);
            if (!expected)
            {
                Assert.IsNull(endpoint, name);
                AssertCodecRejects(uri, allowLoopbackHttp, secretScope, name);
                continue;
            }

            Assert.IsNotNull(endpoint, name);
            var canonicalUri = vector.GetProperty("canonicalUri").GetString();
            Assert.AreEqual(canonicalUri, endpoint.CanonicalWireUri, name);
            Assert.AreEqual(endpoint.CanonicalWireUri, endpoint.Uri.AbsoluteUri, name);

            var canonicalBytes = ProviderConfigurationCodec.Serialize(A1TestSupport.Settings(
                endpoint: endpoint.Uri,
                secretScope: secretScope));
            try
            {
                var roundTrip = ProviderConfigurationCodec.Deserialize(canonicalBytes);
                Assert.AreEqual(canonicalUri, roundTrip.Settings.Profiles[0].Endpoint.CanonicalWireUri, name);
                CollectionAssert.AreEqual(canonicalBytes, ProviderConfigurationCodec.Serialize(roundTrip.Settings), name);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(canonicalBytes);
            }
        }
    }

    [TestMethod]
    public void SchemaEndpointFragment_IsTheExactPolicyProjection()
    {
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Schemas", "vfxcomposer-ai-provider-config-v1.schema.json");
        using var schema = JsonDocument.Parse(File.ReadAllBytes(schemaPath));
        var fragment = schema.RootElement.GetProperty("$defs")
            .GetProperty("profile")
            .GetProperty("properties")
            .GetProperty("endpoint")
            .GetRawText();
        CollectionAssert.AreEqual(
            Encoding.UTF8.GetBytes(EndpointPolicy.SchemaProjection),
            Encoding.UTF8.GetBytes(fragment));
    }

    private static void AssertCodecRejects(string uri, bool allowLoopbackHttp, SecretScope secretScope, string name)
    {
        var baseline = ProviderConfigurationCodec.Serialize(A1TestSupport.Settings(secretScope: secretScope));
        try
        {
            var baselineText = Encoding.UTF8.GetString(baseline);
            var altered = baselineText
                .Replace(
                    "\"uri\":\"https://provider.example.invalid/v1/\"",
                    "\"uri\":" + JsonSerializer.Serialize(uri),
                    StringComparison.Ordinal)
                .Replace(
                    "\"allowLoopbackHttp\":false",
                    "\"allowLoopbackHttp\":" + (allowLoopbackHttp ? "true" : "false"),
                    StringComparison.Ordinal);
            var bytes = Encoding.UTF8.GetBytes(altered);
            try
            {
                A1TestSupport.Throws(AiErrorCode.EndpointRejected, () => ProviderConfigurationCodec.Deserialize(bytes));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(baseline);
        }
    }
}
