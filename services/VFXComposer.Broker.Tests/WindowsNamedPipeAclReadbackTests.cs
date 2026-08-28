using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using Microsoft.Win32.SafeHandles;
using VFXComposer.Broker.Configuration;
using VFXComposer.Broker.Security;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;

namespace VFXComposer.Broker.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
public sealed class WindowsNamedPipeAclReadbackTests
{
    private const string ServiceSidText = "S-1-5-80-101-202-303-404-505";
    private const string AlternateServiceSidText = "S-1-5-80-801-802-803-804-805";
    private const string UserSidText = "S-1-5-21-1001-1002-1003-1004";
    private const string AlternateUserSidText = "S-1-5-21-2001-2002-2003-2004";

    [TestMethod]
    public void ExactCanonicalDescriptorPassesWithAllNativeFacts()
    {
        var inputs = CreateBoundInputs();
        var descriptor = CanonicalDescriptor(inputs);

        Assert.IsTrue(WindowsNamedPipeAclReadback.TryValidateExactDescriptor(
            descriptor,
            inputs.DurableProfile.ServiceSid,
            inputs.ProvisioningIntent.UserSid,
            out var facts));
        Assert.IsNotNull(facts);
        Assert.AreEqual(0x9004, facts!.ControlFlags);
        Assert.IsTrue(facts.OwnerOffset >= 20);
        Assert.IsTrue(facts.OwnerLength > 0);
        Assert.IsTrue(facts.GroupLength > 0);
        Assert.IsTrue(facts.DaclLength > 8);
        Assert.IsTrue(facts.FirstAceOffset < facts.SecondAceOffset);
        Assert.AreEqual(facts.DescriptorLength, descriptor.Length);
    }

    [TestMethod]
    public void OwnerMismatchIsRejected()
    {
        var inputs = CreateBoundInputs();
        var descriptor = Descriptor(
            AlternateServiceSidText,
            ServiceSidText,
            UserSidText);

        Assert.IsFalse(WindowsNamedPipeAclReadback.TryValidateExactDescriptor(
            descriptor,
            inputs.DurableProfile.ServiceSid,
            inputs.ProvisioningIntent.UserSid,
            out _));
    }

    [TestMethod]
    public void GroupMismatchIsRejected()
    {
        var inputs = CreateBoundInputs();
        var descriptor = Descriptor(
            ServiceSidText,
            AlternateServiceSidText,
            UserSidText);

        Assert.IsFalse(WindowsNamedPipeAclReadback.TryValidateExactDescriptor(
            descriptor,
            inputs.DurableProfile.ServiceSid,
            inputs.ProvisioningIntent.UserSid,
            out _));
    }

    [TestMethod]
    public void PresentSaclAndUnreadableHandleFailClosed()
    {
        var inputs = CreateBoundInputs();
        var withSacl = Descriptor(
            ServiceSidText,
            ServiceSidText,
            UserSidText,
            sacl: string.Concat("S:(AU;SA;0x00000001;;;", UserSidText, ")"));

        Assert.IsFalse(WindowsNamedPipeAclReadback.TryValidateExactDescriptor(
            withSacl,
            inputs.DurableProfile.ServiceSid,
            inputs.ProvisioningIntent.UserSid,
            out _));

        using var invalidHandle = new SafePipeHandle(IntPtr.Zero, ownsHandle: false);
        Assert.IsFalse(WindowsNamedPipeAclReadback.TryReadExact(
            invalidHandle,
            inputs.DurableProfile,
            inputs.ProvisioningIntent,
            out var readback));
        Assert.IsNull(readback);
    }

    [TestMethod]
    public void ControlMismatchIsRejected()
    {
        var inputs = CreateBoundInputs();
        var descriptor = CanonicalDescriptor(inputs);
        BinaryPrimitives.WriteUInt16LittleEndian(descriptor.AsSpan(2, sizeof(ushort)), 0x8004);

        Assert.IsFalse(WindowsNamedPipeAclReadback.TryValidateExactDescriptor(
            descriptor,
            inputs.DurableProfile.ServiceSid,
            inputs.ProvisioningIntent.UserSid,
            out _));
    }

    [TestMethod]
    public void AceTypeFlagsInheritanceOpaqueAndOrderMismatchesAreRejected()
    {
        var inputs = CreateBoundInputs();
        var canonical = CanonicalDescriptor(inputs);
        Assert.IsTrue(WindowsNamedPipeAclReadback.TryValidateExactDescriptor(
            canonical,
            inputs.DurableProfile.ServiceSid,
            inputs.ProvisioningIntent.UserSid,
            out var facts));
        Assert.IsNotNull(facts);

        var wrongType = canonical.ToArray();
        wrongType[facts!.FirstAceOffset] = 1;
        AssertRejected(wrongType, inputs);

        var inherited = canonical.ToArray();
        inherited[facts.FirstAceOffset + 1] = 0x10;
        AssertRejected(inherited, inputs);

        var opaque = canonical.ToArray();
        opaque[facts.FirstAceOffset + 2]++;
        AssertRejected(opaque, inputs);

        var reversed = Descriptor(
            ServiceSidText,
            ServiceSidText,
            UserSidText,
            dacl: string.Concat(
                "(A;;0x001f019f;;;", ServiceSidText, ")",
                "(A;;0x0012019b;;;", UserSidText, ")"));
        AssertRejected(reversed, inputs);
    }

    [TestMethod]
    public void UserSidAndMaskMismatchesAreRejected()
    {
        var inputs = CreateBoundInputs();
        var wrongUser = Descriptor(
            ServiceSidText,
            ServiceSidText,
            AlternateUserSidText);
        AssertRejected(wrongUser, inputs);

        var wrongMask = CanonicalDescriptor(inputs);
        Assert.IsTrue(WindowsNamedPipeAclReadback.TryValidateExactDescriptor(
            wrongMask,
            inputs.DurableProfile.ServiceSid,
            inputs.ProvisioningIntent.UserSid,
            out var facts));
        BinaryPrimitives.WriteUInt32LittleEndian(
            wrongMask.AsSpan(facts!.FirstAceOffset + 4, sizeof(uint)),
            0x0012019CU);
        AssertRejected(wrongMask, inputs);
    }

    [TestMethod]
    public void ServiceSidMaskAndExtraAceMismatchesAreRejected()
    {
        var inputs = CreateBoundInputs();
        var wrongService = Descriptor(
            ServiceSidText,
            ServiceSidText,
            UserSidText,
            dacl: string.Concat(
                "(A;;0x0012019b;;;", UserSidText, ")",
                "(A;;0x001f019f;;;", AlternateServiceSidText, ")"));
        AssertRejected(wrongService, inputs);

        var wrongMask = CanonicalDescriptor(inputs);
        Assert.IsTrue(WindowsNamedPipeAclReadback.TryValidateExactDescriptor(
            wrongMask,
            inputs.DurableProfile.ServiceSid,
            inputs.ProvisioningIntent.UserSid,
            out var facts));
        BinaryPrimitives.WriteUInt32LittleEndian(
            wrongMask.AsSpan(facts!.SecondAceOffset + 4, sizeof(uint)),
            0x001f019EU);
        AssertRejected(wrongMask, inputs);

        var extraAce = Descriptor(
            ServiceSidText,
            ServiceSidText,
            UserSidText,
            dacl: string.Concat(
                "(A;;0x0012019b;;;", UserSidText, ")",
                "(A;;0x001f019f;;;", ServiceSidText, ")",
                "(A;;0x0012019b;;;", UserSidText, ")"));
        AssertRejected(extraAce, inputs);
    }

    private static void AssertRejected(byte[] descriptor, BoundInputs inputs)
    {
        Assert.IsFalse(WindowsNamedPipeAclReadback.TryValidateExactDescriptor(
            descriptor,
            inputs.DurableProfile.ServiceSid,
            inputs.ProvisioningIntent.UserSid,
            out _));
    }

    private static byte[] CanonicalDescriptor(BoundInputs inputs)
    {
        var security = inputs.ProvisioningIntent.CreatePipeSecurity();
        return security.GetSecurityDescriptorBinaryForm();
    }

    private static byte[] Descriptor(
        string owner,
        string group,
        string userSid,
        string? sacl = null,
        string? dacl = null)
    {
        dacl ??= string.Concat(
            "(A;;0x0012019b;;;", userSid, ")",
            "(A;;0x001f019f;;;", ServiceSidText, ")");
        var raw = new RawSecurityDescriptor(string.Concat(
            "O:", owner,
            "G:", group,
            sacl ?? string.Empty,
            "D:P",
            dacl));
        var bytes = new byte[raw.BinaryLength];
        raw.GetBinaryForm(bytes, 0);
        return bytes;
    }

    private static BoundInputs CreateBoundInputs()
    {
        var serviceSid = WindowsSid.ParseService(ServiceSidText);
        var userSid = WindowsSid.ParseUser(UserSidText);
        var profile = new ProductionTrustProfile(
            string.Concat("vfxcomposer-p1-readback-", Guid.NewGuid().ToString("N")),
            "broker-production",
            17,
            serviceSid,
            userSid,
            new Dictionary<string, IReadOnlySet<TypedHash>>(StringComparer.Ordinal)
            {
                [PeerRoles.Desktop] = new HashSet<TypedHash>
                {
                    TypedHash.ComputeUtf8(PeerHello.ProcessImageIdentityType, "desktop-image"),
                },
                [PeerRoles.Worker] = new HashSet<TypedHash>
                {
                    TypedHash.ComputeUtf8(PeerHello.ProcessImageIdentityType, "worker-image"),
                },
            });
        var intendedService = new WindowsServiceProcessIdentity(
            serviceSid,
            TypedHash.ComputeUtf8(PeerHello.ProcessImageIdentityType, "broker-service-image"),
            processId: 81,
            processEpoch: "winproc-81-0000000000000054",
            generation: 17,
            sessionId: "service-17-1");
        return new BoundInputs(
            new DurableProductionProfile("p1-named-pipe-profile", serviceSid),
            new WindowsNamedPipeAclProvisioningIntent(profile, intendedService));
    }

    private sealed record BoundInputs(
        DurableProductionProfile DurableProfile,
        WindowsNamedPipeAclProvisioningIntent ProvisioningIntent);
}
