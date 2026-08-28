using System.Text;
using System.Text.Json;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Protocol.Tests;

[TestClass]
public sealed class SelfHashTests
{
    private const string TypeTag = "vfxcomposer.test-document/1";

    [TestMethod]
    public void Compute_UsesSortedCanonicalJsonAndNormalizesNumbers()
    {
        var first = SelfHash.Compute(Encoding.UTF8.GetBytes("{\"z\":1.0,\"a\":true}"), TypeTag);
        var second = SelfHash.Compute(Encoding.UTF8.GetBytes("{ \"a\" : true, \"z\" : 1e0 }"), TypeTag);

        Assert.IsTrue(first.FixedTimeEquals(second));

        Assert.AreEqual(
            "{\"a\":true,\"z\":1}",
            Encoding.UTF8.GetString(CanonicalJson.Canonicalize(Encoding.UTF8.GetBytes("{\"z\":1.00,\"a\":true}"))));
    }

    [TestMethod]
    public void Verify_ExcludesOnlyTheNamedRootFieldAndRejectsTampering()
    {
        var body = Encoding.UTF8.GetBytes("{\"b\":2,\"nested\":{\"selfHash\":\"ordinary-data\"},\"a\":1}");
        var hash = SelfHash.Compute(body, TypeTag);
        var sealedJson = Encoding.UTF8.GetBytes(
            "{\"selfHash\":" + JsonSerializer.Serialize(hash) +
            ",\"a\":1,\"nested\":{\"selfHash\":\"ordinary-data\"},\"b\":2}");

        Assert.IsTrue(SelfHash.Verify(sealedJson, TypeTag));
        Assert.IsFalse(SelfHash.Verify(
            Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(sealedJson).Replace("\"b\":2", "\"b\":3", StringComparison.Ordinal)),
            TypeTag));
        Assert.IsFalse(SelfHash.Verify(sealedJson, "vfxcomposer.other-document/1"));
    }

    [TestMethod]
    public void Verify_RejectsMissingMalformedOrDuplicateSelfHash()
    {
        Assert.IsFalse(SelfHash.Verify(Encoding.UTF8.GetBytes("{\"a\":1}"), TypeTag));
        Assert.IsFalse(SelfHash.Verify(Encoding.UTF8.GetBytes("{\"a\":1,\"selfHash\":null}"), TypeTag));
        Assert.ThrowsExactly<Json.StrictJsonException>(() =>
            SelfHash.Verify(
                Encoding.UTF8.GetBytes("{\"selfHash\":{},\"selfHash\":{},\"a\":1}"),
                TypeTag));
    }

    [TestMethod]
    public void CanonicalJson_SortsDecodedNamesByUtf8Bytes()
    {
        var canonical = Encoding.UTF8.GetString(
            CanonicalJson.Canonicalize(Encoding.UTF8.GetBytes("{\"é\":1,\"a\":2,\"😀\":3}")));

        Assert.AreEqual("{\"a\":2,\"é\":1,\"\\uD83D\\uDE00\":3}", canonical);
    }
}
