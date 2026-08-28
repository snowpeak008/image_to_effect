using VFXComposer.Broker.Security;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;

namespace VFXComposer.Broker.Configuration;

/// <summary>
/// Immutable correlation facts for one host-owned service installation candidate.
/// It carries only the existing profile reference, service SID, typed image
/// identity, and Broker generation.
/// </summary>
internal sealed class WindowsServiceInstallationIdentity
{
    private readonly ProductionTrustProfile _profile;

    internal WindowsServiceInstallationIdentity(
        ProductionTrustProfile profile,
        WindowsSid serviceSid,
        TypedHash serviceImageIdentity,
        long brokerGeneration)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(serviceSid);
        ArgumentNullException.ThrowIfNull(serviceImageIdentity);
        if (brokerGeneration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(brokerGeneration));
        }

        if (!string.Equals(
                serviceImageIdentity.TypeTag,
                PeerHello.ProcessImageIdentityType,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The service image identity must use the process-image domain.",
                nameof(serviceImageIdentity));
        }

        _profile = profile;
        ServiceSid = serviceSid;
        ServiceImageIdentity = serviceImageIdentity;
        BrokerGeneration = brokerGeneration;
    }

    internal long BrokerGeneration { get; }

    internal WindowsSid ServiceSid { get; }

    // This typed correlation identity makes no executable-content assertion.
    internal TypedHash ServiceImageIdentity { get; }

    internal bool Matches(
        ProductionTrustProfile? profile,
        WindowsServiceInstallationIdentity? expectedIdentity) =>
        profile is not null &&
        expectedIdentity is not null &&
        ReferenceEquals(_profile, profile) &&
        ReferenceEquals(expectedIdentity._profile, profile) &&
        string.Equals(
            ServiceImageIdentity.TypeTag,
            PeerHello.ProcessImageIdentityType,
            StringComparison.Ordinal) &&
        string.Equals(
            expectedIdentity.ServiceImageIdentity.TypeTag,
            PeerHello.ProcessImageIdentityType,
            StringComparison.Ordinal) &&
        BrokerGeneration == profile.BrokerGeneration &&
        BrokerGeneration == expectedIdentity.BrokerGeneration &&
        ServiceSid.FixedEquals(profile.ServiceSid) &&
        ServiceSid.FixedEquals(expectedIdentity.ServiceSid) &&
        ServiceImageIdentity.FixedTimeEquals(expectedIdentity.ServiceImageIdentity);
}

/// <summary>
/// Immutable internal vocabulary for one dormant Windows service installation
/// candidate.
/// </summary>
internal sealed class WindowsServiceInstallationPolicy
{
    internal const string FixedServiceName = "VFXComposerBrokerHost";
    internal const string FixedDisplayName = "VFX Composer Broker Host";
    internal const string LocalServiceAccountName = @"NT AUTHORITY\LocalService";

    private readonly WindowsServiceInstallationIdentity _identity;

    // This constructor snapshots only already-host-owned correlation facts.
    internal WindowsServiceInstallationPolicy(
        WindowsServiceInstallationIdentity identity,
        WindowsServiceType serviceType,
        WindowsServiceAccount account,
        WindowsServiceCredentialMode credentialMode,
        WindowsServiceStartMode startMode,
        WindowsServiceErrorControl errorControl,
        WindowsServiceSidType serviceSidType,
        WindowsServiceRecoveryMode recoveryMode,
        WindowsServiceInstallationFlags flags)
    {
        ArgumentNullException.ThrowIfNull(identity);

        _identity = identity;
        ServiceType = serviceType;
        Account = account;
        CredentialMode = credentialMode;
        StartMode = startMode;
        ErrorControl = errorControl;
        ServiceSidType = serviceSidType;
        RecoveryMode = recoveryMode;
        Flags = flags;
    }

    internal long BrokerGeneration => _identity.BrokerGeneration;

    internal string ServiceName => FixedServiceName;

    internal string DisplayName => FixedDisplayName;

    internal WindowsServiceType ServiceType { get; }

    internal WindowsServiceAccount Account { get; }

    internal string ServiceAccountName =>
        Account == WindowsServiceAccount.LocalService
            ? LocalServiceAccountName
            : string.Empty;

    internal WindowsServiceCredentialMode CredentialMode { get; }

    internal WindowsServiceStartMode StartMode { get; }

    internal WindowsServiceErrorControl ErrorControl { get; }

    internal WindowsServiceSidType ServiceSidType { get; }

    internal WindowsServiceRecoveryMode RecoveryMode { get; }

    internal WindowsServiceInstallationFlags Flags { get; }

    internal WindowsSid ServiceSid => _identity.ServiceSid;

    // This is an existing typed correlation identity only.
    internal TypedHash ServiceImageIdentity => _identity.ServiceImageIdentity;

    internal static WindowsServiceInstallationPolicy CreateCandidate(
        WindowsServiceInstallationIdentity identity) =>
        new(
            identity,
            WindowsServiceType.Win32OwnProcess,
            WindowsServiceAccount.LocalService,
            WindowsServiceCredentialMode.None,
            WindowsServiceStartMode.Demand,
            WindowsServiceErrorControl.Normal,
            WindowsServiceSidType.Restricted,
            WindowsServiceRecoveryMode.None,
            WindowsServiceInstallationFlags.None);

    internal bool HasExactIdentityBinding(
        ProductionTrustProfile? profile,
        WindowsServiceInstallationIdentity? expectedIdentity) =>
        _identity.Matches(profile, expectedIdentity);
}

[Flags]
internal enum WindowsServiceType : uint
{
    Win32OwnProcess = 0x00000010,
    Win32ShareProcess = 0x00000020,
    InteractiveProcess = 0x00000100,
}

internal enum WindowsServiceAccount : uint
{
    LocalService = 0,
    LocalSystem = 1,
    NetworkService = 2,
    ArbitraryPrincipal = 3,
}

internal enum WindowsServiceCredentialMode : uint
{
    None = 0,
    PasswordPresent = 1,
}

internal enum WindowsServiceStartMode : uint
{
    Boot = 0x00000000,
    System = 0x00000001,
    Auto = 0x00000002,
    Demand = 0x00000003,
    Disabled = 0x00000004,
}

internal enum WindowsServiceErrorControl : uint
{
    Ignore = 0x00000000,
    Normal = 0x00000001,
    Severe = 0x00000002,
    Critical = 0x00000003,
}

internal enum WindowsServiceSidType : uint
{
    None = 0x00000000,
    Unrestricted = 0x00000001,
    Restricted = 0x00000003,
}

internal enum WindowsServiceRecoveryMode : uint
{
    None = 0,
    Restart = 1,
    Reboot = 2,
    ExternalAction = 3,
    OwnRestart = 4,
}

[Flags]
internal enum WindowsServiceInstallationFlags : uint
{
    None = 0,
    DelayedAutoStart = 1,
    DependenciesPresent = 2,
    ArgumentsPresent = 4,
}
