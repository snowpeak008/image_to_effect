using System.Text;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Protocol.Tests;

[TestClass]
public sealed class TypedHashTests
{
    [TestMethod]
    [DataRow("", "sha256:4c600c967ea5e8b5dcb789b89865cd05fcef09f569be09d3a827656d0b58e9e2")]
    [DataRow("hello", "sha256:26404f79b9fb11a05230dc82528276d0026134113373dd3e002c61db68574cab")]
    [DataRow("snowman ☃", "sha256:cab7f2f8086392fee763b529e6a435f3df4f5782437468f73e98f9afac69ebbb")]
    public void Compute_MatchesIndependentLengthPrefixedGoldenVectors(string payload, string expectedDigest)
    {
        var hash = TypedHash.Compute("vfxcomposer.test/1", Encoding.UTF8.GetBytes(payload));

        Assert.AreEqual("vfxcomposer.test/1", hash.TypeTag);
        Assert.AreEqual(expectedDigest, hash.Digest);
    }

    [TestMethod]
    public void Compute_BindsTypeAndBothLengths()
    {
        var payload = Encoding.UTF8.GetBytes("abc");
        var firstType = TypedHash.Compute("vfxcomposer.alpha/1", payload);
        var secondType = TypedHash.Compute("vfxcomposer.beta/1", payload);
        var secondPayload = TypedHash.Compute("vfxcomposer.alpha/1", Encoding.UTF8.GetBytes("abcd"));

        Assert.AreNotEqual(firstType.Digest, secondType.Digest);
        Assert.AreNotEqual(firstType.Digest, secondPayload.Digest);
        Assert.IsTrue(firstType.FixedTimeEquals(new TypedHash(firstType.TypeTag, firstType.Digest)));
        Assert.IsFalse(firstType.FixedTimeEquals(secondType));
    }

    [TestMethod]
    public void Constructor_RejectsNoncanonicalTypeAndDigestText()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new TypedHash("VFXComposer.Test/1", "sha256:" + new string('0', 64)));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new TypedHash("vfxcomposer.test/1", "sha256:" + new string('A', 64)));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new TypedHash("vfxcomposer.test", "sha256:" + new string('0', 64)));
    }

    [TestMethod]
    public void ComputeUtf8_RejectsAnIsolatedSurrogate()
    {
        Assert.ThrowsExactly<EncoderFallbackException>(() =>
            TypedHash.ComputeUtf8("vfxcomposer.test/1", "bad\ud800"));
    }
}
