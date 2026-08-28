using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using VFXComposer.Broker.Configuration;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Broker.Security;

/// <summary>
/// Reads the security descriptor returned for one already-created named-pipe
/// handle and accepts only the exact protected production descriptor. This is
/// intentionally a same-handle observation primitive: it never opens a name,
/// changes a descriptor, or treats an unreadable SACL as absent.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsNamedPipeAclReadback
{
    internal const ushort RequiredControlFlags = 0x9004;
    internal const byte RequiredSecurityDescriptorRevision = 1;
    internal const byte RequiredAclRevision = 2;

    private const uint OwnerSecurityInformation = 0x00000001;
    private const uint GroupSecurityInformation = 0x00000002;
    private const uint DaclSecurityInformation = 0x00000004;
    private const uint SaclSecurityInformation = 0x00000008;
    private const uint RequiredSecurityInformation =
        OwnerSecurityInformation |
        GroupSecurityInformation |
        DaclSecurityInformation |
        SaclSecurityInformation;
    private const int SecurityDescriptorRelativeHeaderLength = 20;
    private const int AclHeaderLength = 8;
    private const int AceHeaderAndMaskLength = 8;
    private const int MaximumSecurityDescriptorBytes = 64 * 1024;
    private const int SeKernelObject = 6;

    private readonly SafePipeHandle _verifiedHandle;

    private WindowsNamedPipeAclReadback(
        SafePipeHandle verifiedHandle,
        TypedHash durableProfileDigest,
        WindowsSid serviceSid,
        WindowsSid userSid,
        DescriptorFacts descriptorFacts)
    {
        _verifiedHandle = verifiedHandle ?? throw new ArgumentNullException(nameof(verifiedHandle));
        DurableProfileDigest = durableProfileDigest;
        ServiceSid = serviceSid;
        UserSid = userSid;
        Facts = descriptorFacts;
    }

    internal TypedHash DurableProfileDigest { get; }

    internal WindowsSid ServiceSid { get; }

    internal WindowsSid UserSid { get; }

    internal DescriptorFacts Facts { get; }

    /// <summary>
    /// Reports whether this result belongs to the precise native handle passed
    /// to GetSecurityInfo. This is intentionally reference identity rather
    /// than a reopen-by-name or raw-value comparison.
    /// </summary>
    internal bool IsVerifiedFor(SafePipeHandle? pipeHandle) =>
        ReferenceEquals(_verifiedHandle, pipeHandle);

    /// <summary>
    /// Calls GetSecurityInfo for owner, group, DACL and SACL in one request on
    /// the supplied pipe handle. Any native failure, unreadable SACL or binary
    /// mismatch returns false without creating a usable readback capability.
    /// </summary>
    internal static bool TryReadExact(
        SafePipeHandle? pipeHandle,
        DurableProductionProfile? durableProfile,
        WindowsNamedPipeAclProvisioningIntent? provisioningIntent,
        out WindowsNamedPipeAclReadback? readback)
    {
        readback = null;
        if (!OperatingSystem.IsWindows() ||
            pipeHandle is null ||
            pipeHandle.IsInvalid ||
            pipeHandle.IsClosed ||
            !HasExactBoundInputs(durableProfile, provisioningIntent))
        {
            return false;
        }

        IntPtr owner = IntPtr.Zero;
        IntPtr group = IntPtr.Zero;
        IntPtr dacl = IntPtr.Zero;
        IntPtr sacl = IntPtr.Zero;
        IntPtr securityDescriptor = IntPtr.Zero;
        byte[]? descriptorBytes = null;
        try
        {
            var status = GetSecurityInfo(
                pipeHandle,
                SeKernelObject,
                RequiredSecurityInformation,
                out owner,
                out group,
                out dacl,
                out sacl,
                out securityDescriptor);
            if (status != 0 ||
                owner == IntPtr.Zero ||
                group == IntPtr.Zero ||
                dacl == IntPtr.Zero ||
                sacl != IntPtr.Zero ||
                securityDescriptor == IntPtr.Zero ||
                !GetSecurityDescriptorControl(
                    securityDescriptor,
                    out var control,
                    out var revision) ||
                control != RequiredControlFlags ||
                revision != RequiredSecurityDescriptorRevision ||
                !IsValidSecurityDescriptor(securityDescriptor))
            {
                return false;
            }

            var nativeLength = GetSecurityDescriptorLength(securityDescriptor);
            if (nativeLength is < SecurityDescriptorRelativeHeaderLength or > MaximumSecurityDescriptorBytes)
            {
                return false;
            }

            descriptorBytes = new byte[checked((int)nativeLength)];
            Marshal.Copy(securityDescriptor, descriptorBytes, 0, descriptorBytes.Length);
            if (!TryValidateExactDescriptor(
                    descriptorBytes,
                    durableProfile!.ServiceSid,
                    provisioningIntent!.UserSid,
                    out var facts) ||
                facts is null ||
                !PointsAt(securityDescriptor, facts.OwnerOffset, owner) ||
                !PointsAt(securityDescriptor, facts.GroupOffset, group) ||
                !PointsAt(securityDescriptor, facts.DaclOffset, dacl))
            {
                return false;
            }

            readback = new WindowsNamedPipeAclReadback(
                pipeHandle,
                durableProfile.ProfileDigest,
                durableProfile.ServiceSid,
                provisioningIntent.UserSid,
                facts);
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            ArgumentOutOfRangeException or
            BadImageFormatException or
            DllNotFoundException or
            EntryPointNotFoundException or
            InvalidOperationException or
            MarshalDirectiveException or
            OverflowException or
            SEHException)
        {
            return false;
        }
        finally
        {
            if (descriptorBytes is not null)
            {
                CryptographicOperations.ZeroMemory(descriptorBytes);
            }

            if (securityDescriptor != IntPtr.Zero)
            {
                _ = LocalFree(securityDescriptor);
            }
        }
    }

    /// <summary>
    /// Validates a self-relative native descriptor copied from GetSecurityInfo.
    /// It is also used to reject descriptor drift before a descriptor is handed
    /// to CreateNamedPipeW, so synthetic tests exercise the production binary
    /// parser rather than an SDDL/string oracle.
    /// </summary>
    internal static bool TryValidateExactDescriptor(
        ReadOnlySpan<byte> descriptor,
        WindowsSid? expectedServiceSid,
        WindowsSid? expectedUserSid,
        out DescriptorFacts? facts)
    {
        facts = null;
        if (expectedServiceSid is null ||
            expectedUserSid is null ||
            expectedServiceSid.PrincipalKind != WindowsSidPrincipalKind.Service ||
            expectedUserSid.PrincipalKind != WindowsSidPrincipalKind.User ||
            expectedServiceSid.FixedEquals(expectedUserSid) ||
            descriptor.Length < SecurityDescriptorRelativeHeaderLength ||
            descriptor[0] != RequiredSecurityDescriptorRevision ||
            descriptor[1] != 0)
        {
            return false;
        }

        var control = BinaryPrimitives.ReadUInt16LittleEndian(descriptor.Slice(2, sizeof(ushort)));
        if (control != RequiredControlFlags ||
            !TryReadOffset(descriptor, 4, out var ownerOffset) ||
            !TryReadOffset(descriptor, 8, out var groupOffset) ||
            BinaryPrimitives.ReadUInt32LittleEndian(descriptor.Slice(12, sizeof(uint))) != 0 ||
            !TryReadOffset(descriptor, 16, out var daclOffset) ||
            !TryReadSid(
                descriptor,
                ownerOffset,
                expectedServiceSid,
                out var ownerLength) ||
            !TryReadSid(
                descriptor,
                groupOffset,
                expectedServiceSid,
                out var groupLength) ||
            !TryReadDacl(
                descriptor,
                daclOffset,
                expectedUserSid,
                expectedServiceSid,
                out var daclLength,
                out var firstAceOffset,
                out var secondAceOffset))
        {
            return false;
        }

        var ranges = new[]
        {
            new DescriptorRange(0, SecurityDescriptorRelativeHeaderLength),
            new DescriptorRange(ownerOffset, ownerLength),
            new DescriptorRange(groupOffset, groupLength),
            new DescriptorRange(daclOffset, daclLength),
        };
        Array.Sort(ranges, static (left, right) => left.Offset.CompareTo(right.Offset));
        var extent = 0;
        foreach (var range in ranges)
        {
            if (range.Offset != extent || range.Length <= 0)
            {
                return false;
            }

            try
            {
                extent = checked(range.Offset + range.Length);
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        if (extent != descriptor.Length)
        {
            return false;
        }

        facts = new DescriptorFacts(
            control,
            ownerOffset,
            ownerLength,
            groupOffset,
            groupLength,
            daclOffset,
            daclLength,
            firstAceOffset,
            secondAceOffset,
            descriptor.Length);
        return true;
    }

    private static bool HasExactBoundInputs(
        DurableProductionProfile? durableProfile,
        WindowsNamedPipeAclProvisioningIntent? provisioningIntent)
    {
        if (durableProfile is null ||
            provisioningIntent is null ||
            durableProfile.ProfileDigest is null ||
            !string.Equals(
                durableProfile.ProfileDigest.TypeTag,
                DurableProductionProfile.ProfileDigestType,
                StringComparison.Ordinal) ||
            durableProfile.ServiceSid.PrincipalKind != WindowsSidPrincipalKind.Service ||
            provisioningIntent.ServiceSid.PrincipalKind != WindowsSidPrincipalKind.Service ||
            provisioningIntent.UserSid.PrincipalKind != WindowsSidPrincipalKind.User ||
            !durableProfile.ServiceSid.FixedEquals(provisioningIntent.ServiceSid) ||
            provisioningIntent.ServiceSid.FixedEquals(provisioningIntent.UserSid) ||
            !CanonicalNamedPipeAcl.TryValidateCanonicalSddl(
                provisioningIntent.CanonicalSddl,
                provisioningIntent.ServiceSid,
                provisioningIntent.UserSid,
                out var canonicalAcl) ||
            canonicalAcl is null)
        {
            return false;
        }

        return canonicalAcl.ServiceSid.FixedEquals(durableProfile.ServiceSid) &&
            canonicalAcl.UserSid.FixedEquals(provisioningIntent.UserSid);
    }

    private static bool TryReadOffset(
        ReadOnlySpan<byte> descriptor,
        int headerOffset,
        out int value)
    {
        value = 0;
        var nativeValue = BinaryPrimitives.ReadUInt32LittleEndian(
            descriptor.Slice(headerOffset, sizeof(uint)));
        if (nativeValue < SecurityDescriptorRelativeHeaderLength ||
            nativeValue > int.MaxValue)
        {
            return false;
        }

        value = (int)nativeValue;
        return value < descriptor.Length;
    }

    private static bool TryReadSid(
        ReadOnlySpan<byte> descriptor,
        int offset,
        WindowsSid expectedSid,
        out int sidLength)
    {
        sidLength = 0;
        if (offset < SecurityDescriptorRelativeHeaderLength ||
            offset > descriptor.Length - 8)
        {
            return false;
        }

        var subAuthorityCount = descriptor[offset + 1];
        if (subAuthorityCount is 0 or > 15)
        {
            return false;
        }

        try
        {
            sidLength = checked(8 + (subAuthorityCount * sizeof(uint)));
        }
        catch (OverflowException)
        {
            return false;
        }

        if (sidLength > descriptor.Length - offset)
        {
            return false;
        }

        try
        {
            var actualSid = WindowsSid.FromBinary(
                descriptor.Slice(offset, sidLength),
                expectedSid.PrincipalKind);
            return actualSid.FixedEquals(expectedSid);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryReadDacl(
        ReadOnlySpan<byte> descriptor,
        int daclOffset,
        WindowsSid expectedUserSid,
        WindowsSid expectedServiceSid,
        out int daclLength,
        out int firstAceOffset,
        out int secondAceOffset)
    {
        daclLength = 0;
        firstAceOffset = 0;
        secondAceOffset = 0;
        if (daclOffset < SecurityDescriptorRelativeHeaderLength ||
            daclOffset > descriptor.Length - AclHeaderLength ||
            descriptor[daclOffset] != RequiredAclRevision ||
            descriptor[daclOffset + 1] != 0 ||
            BinaryPrimitives.ReadUInt16LittleEndian(descriptor.Slice(daclOffset + 6, sizeof(ushort))) != 0)
        {
            return false;
        }

        var nativeLength = BinaryPrimitives.ReadUInt16LittleEndian(
            descriptor.Slice(daclOffset + 2, sizeof(ushort)));
        var aceCount = BinaryPrimitives.ReadUInt16LittleEndian(
            descriptor.Slice(daclOffset + 4, sizeof(ushort)));
        if (nativeLength < AclHeaderLength ||
            nativeLength > descriptor.Length - daclOffset ||
            aceCount != 2)
        {
            return false;
        }

        daclLength = nativeLength;
        var cursor = daclOffset + AclHeaderLength;
        var end = daclOffset + daclLength;
        firstAceOffset = cursor;
        if (!TryReadAllowAce(
                descriptor,
                ref cursor,
                end,
                expectedUserSid,
                unchecked((uint)CanonicalNamedPipeAcl.UserReadWriteAccessMask)))
        {
            return false;
        }

        secondAceOffset = cursor;
        if (!TryReadAllowAce(
                descriptor,
                ref cursor,
                end,
                expectedServiceSid,
                unchecked((uint)CanonicalNamedPipeAcl.ServiceFullControlAccessMask)))
        {
            return false;
        }

        return cursor == end;
    }

    private static bool TryReadAllowAce(
        ReadOnlySpan<byte> descriptor,
        ref int cursor,
        int daclEnd,
        WindowsSid expectedSid,
        uint expectedAccessMask)
    {
        if (cursor > daclEnd - AceHeaderAndMaskLength ||
            descriptor[cursor] != 0 || // ACCESS_ALLOWED_ACE_TYPE
            descriptor[cursor + 1] != 0 || // no inherited or propagation flags
            BinaryPrimitives.ReadUInt32LittleEndian(
                descriptor.Slice(cursor + 4, sizeof(uint))) != expectedAccessMask)
        {
            return false;
        }

        var nativeAceLength = BinaryPrimitives.ReadUInt16LittleEndian(
            descriptor.Slice(cursor + 2, sizeof(ushort)));
        if (nativeAceLength < AceHeaderAndMaskLength ||
            nativeAceLength > daclEnd - cursor ||
            !TryReadSid(
                descriptor,
                cursor + AceHeaderAndMaskLength,
                expectedSid,
                out var sidLength) ||
            nativeAceLength != AceHeaderAndMaskLength + sidLength)
        {
            return false;
        }

        cursor += nativeAceLength;
        return true;
    }

    private static bool PointsAt(IntPtr descriptor, int offset, IntPtr observed)
    {
        try
        {
            return observed == IntPtr.Add(descriptor, offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    [DllImport(
        "advapi32.dll",
        ExactSpelling = true,
        SetLastError = true,
        CallingConvention = CallingConvention.Winapi)]
    private static extern uint GetSecurityInfo(
        SafePipeHandle handle,
        int objectType,
        uint securityInformation,
        out IntPtr owner,
        out IntPtr group,
        out IntPtr dacl,
        out IntPtr sacl,
        out IntPtr securityDescriptor);

    [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSecurityDescriptorControl(
        IntPtr securityDescriptor,
        out ushort control,
        out uint revision);

    [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsValidSecurityDescriptor(IntPtr securityDescriptor);

    [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern uint GetSecurityDescriptorLength(IntPtr securityDescriptor);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr localMemory);

    internal sealed record DescriptorFacts(
        ushort ControlFlags,
        int OwnerOffset,
        int OwnerLength,
        int GroupOffset,
        int GroupLength,
        int DaclOffset,
        int DaclLength,
        int FirstAceOffset,
        int SecondAceOffset,
        int DescriptorLength);

    private readonly record struct DescriptorRange(int Offset, int Length);
}
