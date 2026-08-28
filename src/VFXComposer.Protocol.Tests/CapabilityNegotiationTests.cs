using System.Collections.Frozen;

namespace VFXComposer.Protocol.Tests;

[TestClass]
public sealed class CapabilityNegotiationTests
{
    [TestMethod]
    public void Negotiate_ReturnsOnlyKnownOfferedSupportedCapabilitiesInOrdinalOrder()
    {
        var result = CapabilityNegotiator.Negotiate(
            [
                CapabilityIds.StatusSnapshotV1,
                "unknown.future.v99",
                CapabilityIds.HandshakeV1,
                CapabilityIds.HandshakeV1,
            ],
            [
                CapabilityIds.HandshakeV1,
                CapabilityIds.StatusSnapshotV1,
                "unknown.server.v99",
            ]);

        CollectionAssert.AreEqual(
            new[] { CapabilityIds.HandshakeV1, CapabilityIds.StatusSnapshotV1 },
            result.ToArray());
    }

    [TestMethod]
    public void Negotiate_NeverTreatsUnknownIdsAsBackwardCompatible()
    {
        var result = CapabilityNegotiator.Negotiate(
            ["unknown.same.v1"],
            ["unknown.same.v1"]);

        Assert.AreEqual(0, result.Count);
        Assert.IsFalse(CapabilityIds.IsKnown("unknown.same.v1"));
    }

    [TestMethod]
    public void PublishedCapabilityAndVersionSetsCannotBeDowncastAndMutated()
    {
        Assert.IsInstanceOfType<FrozenSet<string>>(CapabilityIds.All);
        Assert.IsInstanceOfType<FrozenSet<string>>(ProtocolVersions.Supported);
        Assert.IsNull(CapabilityIds.All as HashSet<string>);
        Assert.IsNull(ProtocolVersions.Supported as HashSet<string>);
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((ISet<string>)CapabilityIds.All).Add("mutated.v1"));
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((ISet<string>)ProtocolVersions.Supported).Add("mutated/99"));
        Assert.IsFalse(CapabilityIds.IsKnown("mutated.v1"));
        Assert.IsFalse(ProtocolVersions.IsSupported("mutated/99"));

        var negotiated = CapabilityNegotiator.Negotiate(
            [CapabilityIds.HandshakeV1],
            [CapabilityIds.HandshakeV1]);
        var list = (IList<string>)negotiated;
        Assert.IsTrue(list.IsReadOnly);
        Assert.ThrowsExactly<NotSupportedException>(() => list.Add("mutated.v1"));
    }
}
