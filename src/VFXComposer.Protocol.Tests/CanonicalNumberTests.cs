using System.Text;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Json;

namespace VFXComposer.Protocol.Tests;

[TestClass]
public sealed class CanonicalNumberTests
{
    [TestMethod]
    [DataRow("1", "1")]
    [DataRow("1.0", "1")]
    [DataRow("1e0", "1")]
    [DataRow("10e-1", "1")]
    [DataRow("-0", "0")]
    [DataRow("123.4500", "123.45")]
    [DataRow("0.000001", "0.000001")]
    [DataRow("0.0000001", "1e-7")]
    [DataRow("100000000000000000000", "100000000000000000000")]
    [DataRow("1000000000000000000000", "1e21")]
    public void Canonicalize_NormalizesEquivalentDecimalsWithoutRounding(string input, string expected)
    {
        Assert.AreEqual(expected, CanonicalizeNumber(input));
    }

    [TestMethod]
    public void Canonicalize_DistinguishesUnderflowLongFractionAndBinary64CollisionPairs()
    {
        AssertDistinct("0", "1e-999");
        AssertDistinct("0.1", "0.1000000000000000000000000000000000000001");
        AssertDistinct("9007199254740992", "9007199254740993");
        AssertDistinct("1e308", "1.0000000000000001e308");
        AssertDistinct(
            "0.12345678901234567890123456789012345678901234567890",
            "0.12345678901234567890123456789012345678901234567891");
    }

    [TestMethod]
    public void SelfHash_DoesNotCollideForPreviouslyRoundedNumberPairs()
    {
        const string typeTag = "vfxcomposer.number-fixture/1";
        var zero = SelfHash.Compute(Encoding.UTF8.GetBytes("{\"value\":0}"), typeTag);
        var tiny = SelfHash.Compute(Encoding.UTF8.GetBytes("{\"value\":1e-999}"), typeTag);
        var firstInteger = SelfHash.Compute(
            Encoding.UTF8.GetBytes("{\"value\":9007199254740992}"),
            typeTag);
        var secondInteger = SelfHash.Compute(
            Encoding.UTF8.GetBytes("{\"value\":9007199254740993}"),
            typeTag);

        Assert.IsFalse(zero.FixedTimeEquals(tiny));
        Assert.IsFalse(firstInteger.FixedTimeEquals(secondInteger));
        Assert.IsTrue(
            SelfHash.Compute(Encoding.UTF8.GetBytes("{\"value\":1}"), typeTag)
                .FixedTimeEquals(SelfHash.Compute(Encoding.UTF8.GetBytes("{\"value\":1.0}"), typeTag)));
    }

    [TestMethod]
    public void Canonicalize_AcceptsExactLimitsAndRejectsLargerLexicalResources()
    {
        var maximumCoefficient = new string('9', 256);
        var overlongCoefficient = new string('9', 257);
        Assert.AreEqual(
            "9." + new string('9', 255) + "e255",
            CanonicalizeNumber(maximumCoefficient));
        Assert.AreEqual("1e100000", CanonicalizeNumber("1e100000"));
        Assert.AreEqual("1e100000", CanonicalizeNumber("10e99999"));
        Assert.AreEqual("1e-100000", CanonicalizeNumber("1e-100000"));

        Assert.AreEqual(
            "NUMBER_DIGIT_LIMIT",
            Assert.ThrowsExactly<StrictJsonException>(() =>
                CanonicalizeNumber(overlongCoefficient)).ReasonCode);
        Assert.AreEqual(
            "NUMBER_EXPONENT_LIMIT",
            Assert.ThrowsExactly<StrictJsonException>(() =>
                CanonicalizeNumber("1e100001")).ReasonCode);
        Assert.AreEqual(
            "NUMBER_EXPONENT_LIMIT",
            Assert.ThrowsExactly<StrictJsonException>(() =>
                CanonicalizeNumber("1e0000000")).ReasonCode);
    }

    private static void AssertDistinct(string left, string right)
    {
        Assert.AreNotEqual(CanonicalizeNumber(left), CanonicalizeNumber(right));
    }

    private static string CanonicalizeNumber(string input) =>
        Encoding.UTF8.GetString(CanonicalJson.Canonicalize(Encoding.UTF8.GetBytes(input)));
}
