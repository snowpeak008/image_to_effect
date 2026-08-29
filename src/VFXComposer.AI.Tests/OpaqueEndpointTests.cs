using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Providers;

namespace VFXComposer.AI.Tests;

[TestClass]
public sealed class OpaqueEndpointTests
{
    [TestMethod]
    public void OpaqueVectors_SaveLoadAndResolveExactly()
    {
        using var vectors = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "OpaqueEndpointVectors.json")));
        Assert.AreEqual(1, vectors.RootElement.GetProperty("formatVersion").GetInt32());

        using var directory = new A1TestDirectory();
        var observedNames = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var vector in vectors.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var name = vector.GetProperty("name").GetString();
            var value = vector.GetProperty("value").GetString();
            Assert.IsFalse(string.IsNullOrEmpty(name));
            Assert.IsNotNull(value, name);
            Assert.IsTrue(observedNames.Add(name!), name);

            var settings = A1TestSupport.Settings(endpointValue: value!);
            var store = new ProviderConfigurationStore(Path.Combine(directory.Path, "opaque-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".json"));
            store.Save(settings);
            var loaded = store.Load().Configuration;
            Assert.AreEqual(value, loaded.Settings.Profiles[0].Endpoint.Value, name);

            var health = new ProviderHealthRegistry();
            health.Record(A1TestSupport.VerifiedHealth(loaded, AiChannel.ChatLlm, "chat-main"));
            var route = A1TestSupport.Resolver(health).Resolve(AiChannel.ChatLlm, loaded);
            Assert.AreEqual(value, route.Profile.Endpoint.Value, name);

            var canonical = ProviderConfigurationCodec.Serialize(loaded.Settings);
            try
            {
                var roundTrip = ProviderConfigurationCodec.Deserialize(canonical);
                Assert.AreEqual(value, roundTrip.Settings.Profiles[0].Endpoint.Value, name);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(canonical);
            }

            index++;
        }

        Assert.AreEqual(9, index);
    }

    [TestMethod]
    public void EndpointSchemaHasOnlyOpaqueStringTypeAndStorageSize()
    {
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Schemas", "vfxcomposer-ai-provider-config-v1.schema.json");
        using var schema = JsonDocument.Parse(File.ReadAllBytes(schemaPath));
        var endpoint = schema.RootElement.GetProperty("$defs")
            .GetProperty("profile")
            .GetProperty("properties")
            .GetProperty("endpoint");
        var endpointProperties = endpoint.GetProperty("properties");

        Assert.AreEqual(JsonValueKind.Object, endpoint.ValueKind);
        Assert.IsTrue(endpoint.GetProperty("additionalProperties").GetBoolean() is false);
        Assert.AreEqual("value", endpoint.GetProperty("required")[0].GetString());
        Assert.AreEqual(1, endpointProperties.EnumerateObject().Count());
        Assert.IsFalse(endpointProperties.TryGetProperty("uri", out _));
        Assert.IsFalse(endpointProperties.TryGetProperty("allowLoopbackHttp", out _));

        var value = endpointProperties.GetProperty("value");
        Assert.AreEqual("string", value.GetProperty("type").GetString());
        Assert.AreEqual(OpaqueEndpoint.MaximumUtf8ByteLength, value.GetProperty("maxLength").GetInt32());
        Assert.IsFalse(value.TryGetProperty("minLength", out _));
        Assert.IsFalse(value.TryGetProperty("pattern", out _));
        Assert.IsFalse(value.TryGetProperty("format", out _));
    }

    [TestMethod]
    public void OpaqueEndpointAllowsEmptyAndRejectsOnlyUnpersistableTextOrStorageOverflow()
    {
        var empty = new OpaqueEndpoint(string.Empty);
        Assert.AreEqual(string.Empty, empty.Value);
        Assert.AreEqual(0, empty.Utf8ByteLength);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new OpaqueEndpoint(new string('a', OpaqueEndpoint.MaximumUtf8ByteLength + 1)));
        Assert.ThrowsExactly<ArgumentException>(() => new OpaqueEndpoint(new string('\ud800', 1)));
    }
}
