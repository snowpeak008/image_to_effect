using System.Reflection;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Status;

namespace VFXComposer.Protocol.Tests;

[TestClass]
public sealed class AuthorityDomainSeparationTests
{
    private static readonly TypedHash SourceIdentity =
        TypedHash.ComputeUtf8("vfxcomposer.status-source/1", "fixture");

    [TestMethod]
    public void StatusTypes_AreDistinctAndVisualPendingIsNotMachinePassOrUserAuthority()
    {
        var machine = new MachineStatus(MachineStatusStates.Pending);
        var visual = new VisualStatus(VisualStatusStates.VisualPending);
        var verdict = new UserVerdictStatus(UserVerdictStatusStates.NotSigned);
        var l3 = new L3Status(L3StatusStates.NotGranted);
        var l4 = new L4Status(L4StatusStates.NotGranted);

        Assert.AreNotEqual(machine.GetType(), visual.GetType());
        Assert.AreNotEqual(visual.GetType(), verdict.GetType());
        Assert.AreNotEqual(verdict.GetType(), l3.GetType());
        Assert.AreNotEqual(l3.GetType(), l4.GetType());
        Assert.AreEqual("VISUAL_PENDING", visual.State);
        Assert.IsNull(visual.Provenance);
        Assert.AreEqual("NOT_SIGNED", verdict.State);
        Assert.AreEqual("NOT_GRANTED", l3.State);
        Assert.AreEqual("NOT_GRANTED", l4.State);
    }

    [TestMethod]
    public void AuthorityBearingStates_RequireSameDomainProvenance()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new MachineStatus(MachineStatusStates.Passed));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new VisualStatus(VisualStatusStates.Passed));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new UserVerdictStatus(UserVerdictStatusStates.Approved));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new L3Status(L3StatusStates.Granted));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new L4Status(L4StatusStates.Granted));

        var machineProvenance = Provenance(StatusDomains.Machine);
        Assert.AreEqual(MachineStatusStates.Passed, new MachineStatus(MachineStatusStates.Passed, machineProvenance).State);
        Assert.ThrowsExactly<ArgumentException>(() =>
            new VisualStatus(VisualStatusStates.Passed, machineProvenance));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new UserVerdictStatus(UserVerdictStatusStates.Approved, machineProvenance));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new L3Status(L3StatusStates.Granted, machineProvenance));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new L4Status(L4StatusStates.Granted, machineProvenance));
    }

    [TestMethod]
    public void ProtocolExportsNoAuthorityIssuerOrPromotionOperation()
    {
        var prohibitedPrefixes = new[] { "Issue", "Promote", "Grant", "Sign", "Approve", "Attest" };
        var exportedMethods = typeof(MachineStatus).Assembly
            .GetExportedTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Where(method => method.DeclaringType?.Namespace?.StartsWith("VFXComposer.Protocol", StringComparison.Ordinal) == true)
            .Select(method => method.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var prefix in prohibitedPrefixes)
        {
            Assert.IsFalse(
                exportedMethods.Any(name => name.StartsWith(prefix, StringComparison.Ordinal)),
                $"Protocol unexpectedly exports authority-like operation {prefix}*." );
        }
    }

    private static StatusProvenance Provenance(string domain) =>
        new(domain, "TEST_FIXTURE", SourceIdentity, DateTimeOffset.UnixEpoch);
}
