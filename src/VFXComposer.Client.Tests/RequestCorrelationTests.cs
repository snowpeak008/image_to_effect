using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VFXComposer.Client.Tests;

[TestClass]
public sealed class RequestCorrelationTests
{
    [TestMethod]
    public void ConstructorPreservesDistinctRequestAndIdempotencyTokens()
    {
        var correlation = new RequestCorrelation("request:alpha-1", "idem_alpha.1");

        Assert.AreEqual("request:alpha-1", correlation.RequestId);
        Assert.AreEqual("idem_alpha.1", correlation.IdempotencyKey);
    }

    [TestMethod]
    public void CreateNewProducesDistinctValidTokens()
    {
        var first = RequestCorrelation.CreateNew();
        var second = RequestCorrelation.CreateNew();

        Assert.AreNotEqual(first.RequestId, second.RequestId);
        Assert.AreNotEqual(first.IdempotencyKey, second.IdempotencyKey);
        Assert.IsTrue(first.RequestId.StartsWith("req_", StringComparison.Ordinal));
        Assert.IsTrue(first.IdempotencyKey.StartsWith("idem_", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("contains space")]
    [DataRow("contains/slash")]
    [DataRow("contains\\slash")]
    public void InvalidTokensAreRejected(string token)
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            _ = new RequestCorrelation(token, "valid-token"));
    }

    [TestMethod]
    public void EmptyAndExcessivelyLongTokensAreRejected()
    {
        var token = new string('a', RequestCorrelation.MaximumTokenLength + 1);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            _ = new RequestCorrelation(string.Empty, "valid-token"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            _ = new RequestCorrelation(token, "valid-token"));
    }
}
