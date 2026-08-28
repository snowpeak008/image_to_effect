using System.Text;
using System.Text.Json;
using VFXComposer.Protocol.Json;

namespace VFXComposer.Protocol.Tests;

[TestClass]
public sealed class StrictJsonTests
{
    [TestMethod]
    public void Parse_AcceptsOneBoundedUtf8ValueAndValidSurrogatePairs()
    {
        var json = Encoding.UTF8.GetBytes("{\"escaped\":\"\\ud83d\\ude00\",\"raw\":\"😀\",\"literal\":\"\\\\ud800\"}");

        using var document = StrictJsonReader.Parse(json);

        Assert.AreEqual("😀", document.RootElement.GetProperty("escaped").GetString());
        Assert.AreEqual("😀", document.RootElement.GetProperty("raw").GetString());
        Assert.AreEqual("\\ud800", document.RootElement.GetProperty("literal").GetString());
    }

    [TestMethod]
    public void Parse_RejectsDecodedDuplicateKeys()
    {
        var exception = Assert.ThrowsExactly<StrictJsonException>(() =>
            StrictJsonReader.Parse(Encoding.UTF8.GetBytes("{\"a\":1,\"\\u0061\":2}")));

        Assert.AreEqual("DUPLICATE_KEY", exception.ReasonCode);
    }

    [TestMethod]
    public void Parse_RejectsCommentsTrailingContentAndTrailingCommas()
    {
        foreach (var json in new[]
                 {
                     "{/*comment*/\"a\":1}",
                     "{\"a\":1,}",
                     "{\"a\":1} {\"b\":2}",
                 })
        {
            Assert.ThrowsExactly<StrictJsonException>(() =>
                StrictJsonReader.Parse(Encoding.UTF8.GetBytes(json)));
        }
    }

    [TestMethod]
    public void Parse_RejectsBomAndInvalidUtf8()
    {
        Assert.AreEqual(
            "UTF8_BOM",
            Assert.ThrowsExactly<StrictJsonException>(() =>
                StrictJsonReader.Parse([0xef, 0xbb, 0xbf, (byte)'{', (byte)'}'])).ReasonCode);
        Assert.AreEqual(
            "INVALID_UTF8",
            Assert.ThrowsExactly<StrictJsonException>(() =>
                StrictJsonReader.Parse([(byte)'"', 0xc3, 0x28, (byte)'"'])).ReasonCode);
    }

    [TestMethod]
    public void Parse_RejectsNonJsonNumberTokensAndConfiguredExactDecimalBounds()
    {
        foreach (var json in new[] { "NaN", "Infinity", "-Infinity", "1e100001", "1e0000000" })
        {
            Assert.ThrowsExactly<StrictJsonException>(() =>
                StrictJsonReader.Parse(Encoding.UTF8.GetBytes(json)), json);
        }

        using var exactLarge = StrictJsonReader.Parse(Encoding.UTF8.GetBytes("1e999"));
        Assert.AreEqual("1e999", exactLarge.RootElement.GetRawText());
    }

    [TestMethod]
    public void Parse_RejectsEveryIsolatedEscapedSurrogateShape()
    {
        foreach (var json in new[]
                 {
                     "{\"x\":\"\\ud800\"}",
                     "{\"x\":\"\\udc00\"}",
                     "{\"x\":\"\\ud800A\"}",
                     "{\"x\":\"\\ud800\\u0041\"}",
                     "{\"x\":\"\\ud800\\\\udc00\"}",
                     "{\"\\ud800\":1}",
                 })
        {
            var exception = Assert.ThrowsExactly<StrictJsonException>(() =>
                StrictJsonReader.Parse(Encoding.UTF8.GetBytes(json)), json);
            Assert.AreEqual("ISOLATED_SURROGATE", exception.ReasonCode, json);
        }
    }

    [TestMethod]
    public void Parse_EnforcesByteDepthAndNodeLimits()
    {
        Assert.AreEqual(
            "MAX_BYTES",
            Assert.ThrowsExactly<StrictJsonException>(() =>
                StrictJsonReader.Parse(Encoding.UTF8.GetBytes("[1,2]"), new StrictJsonLimits(4, 8, 8))).ReasonCode);
        Assert.ThrowsExactly<StrictJsonException>(() =>
            StrictJsonReader.Parse(Encoding.UTF8.GetBytes("[[[0]]]"), new StrictJsonLimits(64, 2, 8)));
        Assert.AreEqual(
            "MAX_NODES",
            Assert.ThrowsExactly<StrictJsonException>(() =>
                StrictJsonReader.Parse(Encoding.UTF8.GetBytes("[1,2]"), new StrictJsonLimits(64, 8, 2))).ReasonCode);
    }

    [TestMethod]
    public void ExactObjectValidator_RejectsMissingUnknownAndWrongType()
    {
        using var valid = StrictJsonReader.Parse(Encoding.UTF8.GetBytes("{\"a\":\"value\",\"b\":1}"));
        ExactObjectValidator.Validate(valid.RootElement, ["a"], ["b"]);
        Assert.AreEqual("value", ExactObjectValidator.RequireString(valid.RootElement, "a"));

        Assert.AreEqual(
            "UNKNOWN_PROPERTY",
            Assert.ThrowsExactly<StrictJsonException>(() =>
                ExactObjectValidator.Validate(valid.RootElement, ["a"])).ReasonCode);
        Assert.AreEqual(
            "MISSING_PROPERTY",
            Assert.ThrowsExactly<StrictJsonException>(() =>
                ExactObjectValidator.Validate(valid.RootElement, ["a", "missing"], ["b"])).ReasonCode);
        Assert.AreEqual(
            "WRONG_TYPE",
            Assert.ThrowsExactly<StrictJsonException>(() =>
                ExactObjectValidator.RequireProperty(valid.RootElement, "b", JsonValueKind.String)).ReasonCode);
    }
}
