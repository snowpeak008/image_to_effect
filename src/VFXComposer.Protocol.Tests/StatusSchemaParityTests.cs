using System.Text.Json;
using System.Text.RegularExpressions;
using VFXComposer.Protocol.Status;

namespace VFXComposer.Protocol.Tests;

[TestClass]
public sealed class StatusSchemaParityTests
{
    [TestMethod]
    public void StatusSchemas_PinExactAndNonEquivalentStateSets()
    {
        AssertStateSet(
            WireSchemaIds.MachineStatusV1,
            MachineStatusStates.Unknown,
            MachineStatusStates.Pending,
            MachineStatusStates.Passed,
            MachineStatusStates.Failed);
        AssertStateSet(
            WireSchemaIds.VisualStatusV1,
            VisualStatusStates.VisualPending,
            VisualStatusStates.Passed,
            VisualStatusStates.Failed);
        AssertStateSet(
            WireSchemaIds.UserVerdictStatusV1,
            UserVerdictStatusStates.NotSigned,
            UserVerdictStatusStates.Approved,
            UserVerdictStatusStates.Rejected);
        AssertStateSet(
            WireSchemaIds.L3StatusV1,
            L3StatusStates.NotGranted,
            L3StatusStates.Granted,
            L3StatusStates.Revoked);
        AssertStateSet(
            WireSchemaIds.L4StatusV1,
            L4StatusStates.NotGranted,
            L4StatusStates.Granted,
            L4StatusStates.Revoked);
    }

    [TestMethod]
    public void ProvenanceSchemaPinsAllFiveDomainsAndTypedIdentity()
    {
        using var schema = ProtocolSchemaTestSupport.LoadSchema(WireSchemaIds.StatusProvenanceV1);
        var domains = schema.RootElement
            .GetProperty("properties")
            .GetProperty("statusDomain")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();

        CollectionAssert.AreEquivalent(
            new[]
            {
                StatusDomains.Machine,
                StatusDomains.Visual,
                StatusDomains.UserVerdict,
                StatusDomains.L3,
                StatusDomains.L4,
            },
            domains);
        Assert.IsTrue(schema.RootElement.GetProperty("$defs").TryGetProperty("typedHash", out _));
    }

    [TestMethod]
    public void ProvenanceTimestampSchemaPinsCompleteUtcIso8601LexicalShape()
    {
        using var schema = ProtocolSchemaTestSupport.LoadSchema(WireSchemaIds.StatusProvenanceV1);
        var timestamp = schema.RootElement
            .GetProperty("properties")
            .GetProperty("observedAtUtc");
        Assert.AreEqual("date-time", timestamp.GetProperty("format").GetString());
        var pattern = timestamp.GetProperty("pattern").GetString();
        Assert.IsNotNull(pattern);

        foreach (var valid in new[]
                 {
                     "1970-01-01T00:00:00Z",
                     "1970-01-01T00:00:00+00:00",
                     "2026-08-26T12:34:56.1Z",
                     "2026-08-26T12:34:56.1234567+00:00",
                 })
        {
            Assert.IsTrue(Regex.IsMatch(valid, pattern, RegexOptions.CultureInvariant), valid);
        }

        foreach (var invalid in new[]
                 {
                     "not-a-dateZ",
                     "1970-01-01 00:00:00Z",
                     "1970-01-01T00:00Z",
                     "1970-01-01T00:00:00+08:00",
                     "1970-01-01T00:00:00.12345678Z",
                     "1970-01-01T00:00:00z",
                 })
        {
            Assert.IsFalse(Regex.IsMatch(invalid, pattern, RegexOptions.CultureInvariant), invalid);
        }
    }

    [TestMethod]
    public void EveryAuthorityStatusSchemaRequiresExplicitProvenanceFieldAndConditionalGate()
    {
        var expectedDomains = new Dictionary<string, string>
                 {
                     [WireSchemaIds.MachineStatusV1] = StatusDomains.Machine,
                     [WireSchemaIds.VisualStatusV1] = StatusDomains.Visual,
                     [WireSchemaIds.UserVerdictStatusV1] = StatusDomains.UserVerdict,
                     [WireSchemaIds.L3StatusV1] = StatusDomains.L3,
                     [WireSchemaIds.L4StatusV1] = StatusDomains.L4,
                 };
        foreach (var pair in expectedDomains)
        {
            using var schema = ProtocolSchemaTestSupport.LoadSchema(pair.Key);
            var required = schema.RootElement.GetProperty("required")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray();
            CollectionAssert.Contains(required, "provenance");
            Assert.IsTrue(schema.RootElement.TryGetProperty("allOf", out var conditional));
            Assert.AreEqual(JsonValueKind.Array, conditional.ValueKind);
            Assert.AreEqual(
                pair.Value,
                schema.RootElement
                    .GetProperty("$defs")
                    .GetProperty("domainProvenance")
                    .GetProperty("allOf")[1]
                    .GetProperty("properties")
                    .GetProperty("statusDomain")
                    .GetProperty("const")
                    .GetString());
        }
    }

    private static void AssertStateSet(string schemaId, params string[] expected)
    {
        using var schema = ProtocolSchemaTestSupport.LoadSchema(schemaId);
        var actual = schema.RootElement
            .GetProperty("properties")
            .GetProperty("state")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();
        CollectionAssert.AreEquivalent(expected, actual);
    }
}
