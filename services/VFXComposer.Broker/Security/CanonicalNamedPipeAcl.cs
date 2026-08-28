using System.Globalization;
using System.Security.AccessControl;
using System.Runtime.Versioning;

namespace VFXComposer.Broker.Security;

/// <summary>
/// Exact production named-pipe DACL requirements. This is a dormant descriptor
/// model only; it neither creates a pipe nor treats CurrentUserOnly as production ACL.
/// </summary>
internal sealed class CanonicalNamedPipeAcl
{
    internal const int UserReadWriteAccessMask = 0x0012019B;
    internal const int ServiceFullControlAccessMask = 0x001F019F;

    private CanonicalNamedPipeAcl(
        string canonicalSddl,
        WindowsSid serviceSid,
        WindowsSid userSid)
    {
        CanonicalSddl = canonicalSddl;
        ServiceSid = serviceSid;
        UserSid = userSid;
    }

    public string CanonicalSddl { get; }

    public WindowsSid ServiceSid { get; }

    public WindowsSid UserSid { get; }

    internal static CanonicalNamedPipeAcl Create(
        WindowsSid serviceSid,
        WindowsSid userSid)
    {
        if (!TryValidateCanonicalSddl(
                BuildCanonicalSddl(serviceSid, userSid),
                serviceSid,
                userSid,
                out var acl) ||
            acl is null)
        {
            throw new ArgumentException("Named-pipe ACL requirements are invalid.");
        }

        return acl;
    }

    internal static bool TryValidateCanonicalSddl(
        string? descriptor,
        WindowsSid? serviceSid,
        WindowsSid? userSid,
        out CanonicalNamedPipeAcl? acl)
    {
        acl = null;
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        if (descriptor is null || serviceSid is null || userSid is null ||
            serviceSid.PrincipalKind != WindowsSidPrincipalKind.Service ||
            userSid.PrincipalKind != WindowsSidPrincipalKind.User ||
            serviceSid.FixedEquals(userSid) ||
            !string.Equals(
                descriptor,
                BuildCanonicalSddl(serviceSid, userSid),
                StringComparison.Ordinal))
        {
            return false;
        }

        RawSecurityDescriptor raw;
        try
        {
            raw = new RawSecurityDescriptor(descriptor);
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            return false;
        }

        var requiredFlags = ControlFlags.DiscretionaryAclPresent |
            ControlFlags.DiscretionaryAclProtected;
        var permittedFlags = requiredFlags | ControlFlags.SelfRelative;
        if ((raw.ControlFlags & requiredFlags) != requiredFlags ||
            (raw.ControlFlags & ~permittedFlags) != 0 ||
            raw.SystemAcl is not null ||
            raw.Owner is null ||
            raw.Group is null ||
            raw.DiscretionaryAcl is null ||
            raw.DiscretionaryAcl.Count != 2 ||
            !string.Equals(raw.Owner.Value, serviceSid.CanonicalValue, StringComparison.Ordinal) ||
            !string.Equals(raw.Group.Value, serviceSid.CanonicalValue, StringComparison.Ordinal) ||
            !IsExactAllowAce(
                raw.DiscretionaryAcl[0],
                userSid,
                UserReadWriteAccessMask) ||
            !IsExactAllowAce(
                raw.DiscretionaryAcl[1],
                serviceSid,
                ServiceFullControlAccessMask))
        {
            return false;
        }

        acl = new CanonicalNamedPipeAcl(descriptor, serviceSid, userSid);
        return true;
    }

    private static string BuildCanonicalSddl(WindowsSid serviceSid, WindowsSid userSid)
    {
        ArgumentNullException.ThrowIfNull(serviceSid);
        ArgumentNullException.ThrowIfNull(userSid);
        if (serviceSid.PrincipalKind != WindowsSidPrincipalKind.Service ||
            userSid.PrincipalKind != WindowsSidPrincipalKind.User)
        {
            throw new ArgumentException("Named-pipe ACL requires exact service and user SIDs.");
        }

        return string.Concat(
            "O:", serviceSid.CanonicalValue,
            "G:", serviceSid.CanonicalValue,
            "D:P(A;;0x", UserReadWriteAccessMask.ToString("x", CultureInfo.InvariantCulture), ";;;",
            userSid.CanonicalValue,
            ")(A;;0x", ServiceFullControlAccessMask.ToString("x", CultureInfo.InvariantCulture), ";;;",
            serviceSid.CanonicalValue,
            ")");
    }

    [SupportedOSPlatform("windows")]
    private static bool IsExactAllowAce(
        GenericAce ace,
        WindowsSid expectedSid,
        int expectedAccessMask) =>
        ace is CommonAce common &&
        common.AceType == AceType.AccessAllowed &&
        common.AceFlags == AceFlags.None &&
        !common.IsInherited &&
        common.OpaqueLength == 0 &&
        common.AccessMask == expectedAccessMask &&
        common.SecurityIdentifier is { } actualSid &&
        string.Equals(actualSid.Value, expectedSid.CanonicalValue, StringComparison.Ordinal);
}
