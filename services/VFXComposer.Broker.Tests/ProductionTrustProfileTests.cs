using System.Security.AccessControl;
using VFXComposer.Broker.Configuration;
using VFXComposer.Broker.Security;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;

namespace VFXComposer.Broker.Tests;

[TestClass]
public sealed class ProductionTrustProfileTests
{
    private const string ServiceSidText = "S-1-5-80-101-202-303-404-505";
    private const string UserSidText = "S-1-5-21-1001-1002-1003-1004";

    [TestMethod]
    public void CanonicalPipeAclHasOnlyTheExactProtectedServiceAndUserEntries()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Production named-pipe ACL semantics are Windows-only.");
            return;
        }

        var profile = CreateProfile();

        // RawSecurityDescriptor is an independent Windows ACL parser. This test
        // does not use CanonicalNamedPipeAcl validation as its oracle.
        var raw = new RawSecurityDescriptor(profile.PipeAcl.CanonicalSddl);
        Assert.AreEqual(ServiceSidText, raw.Owner!.Value);
        Assert.AreEqual(ServiceSidText, raw.Group!.Value);
        Assert.IsNotNull(raw.DiscretionaryAcl);
        Assert.AreEqual(2, raw.DiscretionaryAcl!.Count);
        Assert.IsTrue((raw.ControlFlags & ControlFlags.DiscretionaryAclPresent) != 0);
        Assert.IsTrue((raw.ControlFlags & ControlFlags.DiscretionaryAclProtected) != 0);
        Assert.IsNull(raw.SystemAcl);

        var userAce = raw.DiscretionaryAcl[0] as CommonAce;
        var serviceAce = raw.DiscretionaryAcl[1] as CommonAce;
        Assert.IsNotNull(userAce);
        Assert.IsNotNull(serviceAce);
        Assert.AreEqual(AceType.AccessAllowed, userAce!.AceType);
        Assert.AreEqual(AceFlags.None, userAce.AceFlags);
        Assert.AreEqual(UserSidText, userAce.SecurityIdentifier.Value);
        Assert.AreEqual(CanonicalNamedPipeAcl.UserReadWriteAccessMask, userAce.AccessMask);
        Assert.AreEqual(AceType.AccessAllowed, serviceAce!.AceType);
        Assert.AreEqual(AceFlags.None, serviceAce.AceFlags);
        Assert.AreEqual(ServiceSidText, serviceAce.SecurityIdentifier.Value);
        Assert.AreEqual(CanonicalNamedPipeAcl.ServiceFullControlAccessMask, serviceAce.AccessMask);
    }

    [TestMethod]
    public void CanonicalPipeAclRejectsBroadInheritedDenyMalformedAndNoncanonicalDescriptors()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Production named-pipe ACL semantics are Windows-only.");
            return;
        }

        var service = WindowsSid.ParseService(ServiceSidText);
        var user = WindowsSid.ParseUser(UserSidText);
        var canonical = CanonicalNamedPipeAcl.Create(service, user).CanonicalSddl;
        var invalidDescriptors = new[]
        {
            canonical.Replace("O:" + ServiceSidText, "O:" + UserSidText, StringComparison.Ordinal),
            canonical.Replace(UserSidText, "S-1-1-0", StringComparison.Ordinal),
            canonical.Replace(UserSidText, "S-1-5-7", StringComparison.Ordinal),
            canonical.Replace(UserSidText, "S-1-5-11", StringComparison.Ordinal),
            canonical.Replace(UserSidText, "S-1-5-32-545", StringComparison.Ordinal),
            canonical.Replace("(A;;0x12019b", "(A;ID;0x12019b", StringComparison.Ordinal),
            canonical.Replace("(A;;0x12019b", "(D;;0x12019b", StringComparison.Ordinal),
            canonical.Replace("D:P", "D:", StringComparison.Ordinal),
            "O:" + ServiceSidText + "G:" + ServiceSidText +
                "D:P(A;;0x1f019f;;;" + ServiceSidText +
                ")(A;;0x12019b;;;" + UserSidText + ")",
            "not-an-sddl-descriptor",
        };

        foreach (var descriptor in invalidDescriptors)
        {
            Assert.IsFalse(CanonicalNamedPipeAcl.TryValidateCanonicalSddl(
                descriptor,
                service,
                user,
                out var rejected), descriptor);
            Assert.IsNull(rejected);
        }
    }

    [TestMethod]
    public void ProfilePinsAllRoleSidImageGenerationProcessEpochAndSessionBindings()
    {
        var profile = CreateProfile();
        var desktopImage = Image("desktop-image");
        var workerImage = Image("worker-image");
        var epoch = "winproc-71-0000000000000042";

        Assert.IsTrue(profile.MatchesExactPeerBinding(
            PeerRoles.Desktop,
            WindowsSid.ParseUser(UserSidText),
            desktopImage,
            71,
            epoch,
            "session-17-1",
            17));

        foreach (var invalid in new[]
                 {
                     new PeerBinding(null, WindowsSid.ParseUser(UserSidText), desktopImage, 71, epoch, "session-17-1", 17),
                     new PeerBinding(PeerRoles.Desktop, null, desktopImage, 71, epoch, "session-17-1", 17),
                     new PeerBinding(PeerRoles.Desktop, WindowsSid.ParseUser(UserSidText), null, 71, epoch, "session-17-1", 17),
                     new PeerBinding(PeerRoles.Desktop, WindowsSid.ParseUser(UserSidText), workerImage, 71, epoch, "session-17-1", 17),
                     new PeerBinding(PeerRoles.Desktop, WindowsSid.ParseUser(UserSidText), desktopImage, 0, epoch, "session-17-1", 17),
                     new PeerBinding(PeerRoles.Desktop, WindowsSid.ParseUser(UserSidText), desktopImage, 71, "winproc-72-0000000000000042", "session-17-1", 17),
                     new PeerBinding(PeerRoles.Desktop, WindowsSid.ParseUser(UserSidText), desktopImage, 71, epoch, null, 17),
                     new PeerBinding(PeerRoles.Desktop, WindowsSid.ParseUser(UserSidText), desktopImage, 71, epoch, "session-17-0", 17),
                     new PeerBinding(PeerRoles.Desktop, WindowsSid.ParseUser(UserSidText), desktopImage, 71, epoch, "session-16-1", 17),
                     new PeerBinding(PeerRoles.Desktop, WindowsSid.ParseUser(UserSidText), desktopImage, 71, epoch, "session-17-1", 16),
                     new PeerBinding(PeerRoles.Worker, WindowsSid.ParseUser(UserSidText), desktopImage, 71, epoch, "session-17-1", 17),
                 })
        {
            Assert.IsFalse(profile.MatchesExactPeerBinding(
                invalid.Role,
                invalid.UserSid,
                invalid.ImageIdentity,
                invalid.ProcessId,
                invalid.ProcessEpoch,
                invalid.SessionId,
                invalid.BrokerGeneration));
        }
    }

    [TestMethod]
    public void ProfileFreezesApprovedImagesAndRejectsWrongPrincipalKinds()
    {
        var images = CreateImages();
        var profile = new ProductionTrustProfile(
            "vfxcomposer-production",
            "broker-production",
            17,
            WindowsSid.ParseService(ServiceSidText),
            WindowsSid.ParseUser(UserSidText),
            images);
        ((HashSet<TypedHash>)images[PeerRoles.Desktop]).Clear();

        Assert.IsTrue(profile.MatchesExactPeerBinding(
            PeerRoles.Desktop,
            WindowsSid.ParseUser(UserSidText),
            Image("desktop-image"),
            71,
            "winproc-71-0000000000000042",
            "session-17-1",
            17));
        Assert.ThrowsExactly<ArgumentException>(() => WindowsSid.ParseService(UserSidText));
        Assert.ThrowsExactly<ArgumentException>(() => WindowsSid.ParseUser(ServiceSidText));
        Assert.ThrowsExactly<ArgumentException>(() => WindowsSid.ParseUser("S-1-1-0"));
        Assert.ThrowsExactly<ArgumentException>(() => new ProductionTrustProfile(
            "vfxcomposer-production",
            "broker-production",
            17,
            WindowsSid.ParseService(ServiceSidText),
            WindowsSid.ParseUser(UserSidText),
            new Dictionary<string, IReadOnlySet<TypedHash>>(StringComparer.Ordinal)
            {
                [PeerRoles.Desktop] = new HashSet<TypedHash> { Image("desktop-image") },
            }));
    }

    [TestMethod]
    public void ProductionPolicyRemainsUnloadedDespiteAValidDormantProfile()
    {
        _ = CreateProfile();

        Assert.IsFalse(BrokerPolicy.TryLoadProduction(out var policy));
        Assert.IsNull(policy);
    }

    private static ProductionTrustProfile CreateProfile() =>
        new(
            "vfxcomposer-production",
            "broker-production",
            17,
            WindowsSid.ParseService(ServiceSidText),
            WindowsSid.ParseUser(UserSidText),
            CreateImages());

    private static Dictionary<string, IReadOnlySet<TypedHash>> CreateImages() =>
        new(StringComparer.Ordinal)
        {
            [PeerRoles.Desktop] = new HashSet<TypedHash> { Image("desktop-image") },
            [PeerRoles.Worker] = new HashSet<TypedHash> { Image("worker-image") },
        };

    private static TypedHash Image(string value) =>
        TypedHash.ComputeUtf8(PeerHello.ProcessImageIdentityType, value);

    private sealed record PeerBinding(
        string? Role,
        WindowsSid? UserSid,
        TypedHash? ImageIdentity,
        int ProcessId,
        string? ProcessEpoch,
        string? SessionId,
        long BrokerGeneration);
}
