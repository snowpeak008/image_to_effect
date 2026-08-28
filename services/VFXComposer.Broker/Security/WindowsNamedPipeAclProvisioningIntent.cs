using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using VFXComposer.Broker.Configuration;

namespace VFXComposer.Broker.Security;

/// <summary>
/// Immutable Windows-only intent for a future host to provision the exact pipe
/// security descriptor. It can build a <see cref="PipeSecurity"/> object but
/// never creates, listens on, or applies security to a named pipe.
/// </summary>
internal sealed class WindowsNamedPipeAclProvisioningIntent
{
    // This internal, in-memory foundation deliberately pins one immutable
    // profile instance. It is not a durable profile fingerprint or trust root.
    private readonly ProductionTrustProfile _profile;

    internal WindowsNamedPipeAclProvisioningIntent(
        ProductionTrustProfile profile,
        WindowsServiceProcessIdentity intendedService)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(intendedService);

        var acl = profile.PipeAcl;
        if (profile.BrokerGeneration != intendedService.Generation ||
            !profile.ServiceSid.FixedEquals(intendedService.ServiceSid) ||
            !acl.ServiceSid.FixedEquals(profile.ServiceSid) ||
            !acl.UserSid.FixedEquals(profile.UserSid) ||
            !CanonicalNamedPipeAcl.TryValidateCanonicalSddl(
                acl.CanonicalSddl,
                profile.ServiceSid,
                profile.UserSid,
                out var verifiedAcl) ||
            verifiedAcl is null ||
            !string.Equals(
                verifiedAcl.CanonicalSddl,
                acl.CanonicalSddl,
                StringComparison.Ordinal))
        {
            throw new ArgumentException("Named-pipe ACL intent does not bind the target service profile.");
        }

        _profile = profile;
        PipeName = profile.PipeName;
        BrokerInstanceId = profile.BrokerInstanceId;
        BrokerGeneration = profile.BrokerGeneration;
        ServiceSid = profile.ServiceSid;
        UserSid = profile.UserSid;
        CanonicalSddl = acl.CanonicalSddl;
    }

    public string PipeName { get; }

    public string BrokerInstanceId { get; }

    public long BrokerGeneration { get; }

    public WindowsSid ServiceSid { get; }

    public WindowsSid UserSid { get; }

    public string CanonicalSddl { get; }

    internal bool Matches(
        ProductionTrustProfile? profile,
        WindowsServiceProcessIdentity? intendedService)
    {
        if (!ReferenceEquals(profile, _profile) || intendedService is null ||
            profile.BrokerGeneration != BrokerGeneration ||
            intendedService.Generation != BrokerGeneration ||
            !profile.ServiceSid.FixedEquals(ServiceSid) ||
            !profile.UserSid.FixedEquals(UserSid) ||
            !intendedService.ServiceSid.FixedEquals(ServiceSid) ||
            !string.Equals(profile.PipeName, PipeName, StringComparison.Ordinal) ||
            !string.Equals(profile.BrokerInstanceId, BrokerInstanceId, StringComparison.Ordinal) ||
            !string.Equals(profile.PipeAcl.CanonicalSddl, CanonicalSddl, StringComparison.Ordinal))
        {
            return false;
        }

        return CanonicalNamedPipeAcl.TryValidateCanonicalSddl(
            CanonicalSddl,
            ServiceSid,
            UserSid,
            out var verifiedAcl) &&
            verifiedAcl is not null &&
            string.Equals(
                verifiedAcl.CanonicalSddl,
                CanonicalSddl,
                StringComparison.Ordinal);
    }

    [SupportedOSPlatform("windows")]
    internal PipeSecurity CreatePipeSecurity()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Named-pipe ACL provisioning is Windows-only.");
        }

        if (!CanonicalNamedPipeAcl.TryValidateCanonicalSddl(
                CanonicalSddl,
                ServiceSid,
                UserSid,
                out var verifiedAcl) ||
            verifiedAcl is null)
        {
            throw new InvalidOperationException("Named-pipe ACL intent is no longer canonical.");
        }

        var security = new PipeSecurity();
        security.SetSecurityDescriptorSddlForm(
            CanonicalSddl,
            AccessControlSections.All);
        return security;
    }
}
