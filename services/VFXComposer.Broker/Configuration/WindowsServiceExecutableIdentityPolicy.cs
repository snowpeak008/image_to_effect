using VFXComposer.Broker.Security;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;

namespace VFXComposer.Broker.Configuration;

/// <summary>
/// Immutable correlation facts for one supplied executable-content identity.
/// This type accepts no observation input and makes no claim about how either
/// identity was obtained.
/// </summary>
internal sealed class WindowsServiceExecutableContentIdentity
{
    internal const string ExecutableContentIdentityType = "vfxcomposer.executable-content/1";

    private readonly WindowsServiceInstallationIdentity _installationIdentity;

    internal WindowsServiceExecutableContentIdentity(
        WindowsServiceInstallationIdentity installationIdentity,
        TypedHash executableContentIdentity,
        long executableByteLength)
    {
        ArgumentNullException.ThrowIfNull(installationIdentity);
        ArgumentNullException.ThrowIfNull(executableContentIdentity);
        if (executableByteLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(executableByteLength));
        }

        if (!string.Equals(
                executableContentIdentity.TypeTag,
                ExecutableContentIdentityType,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The executable content identity must use the executable-content domain.",
                nameof(executableContentIdentity));
        }

        _installationIdentity = installationIdentity;
        ExecutableContentIdentity = executableContentIdentity;
        ExecutableByteLength = executableByteLength;
    }

    internal long BrokerGeneration => _installationIdentity.BrokerGeneration;

    internal WindowsSid ServiceSid => _installationIdentity.ServiceSid;

    internal TypedHash ProcessImageIdentity => _installationIdentity.ServiceImageIdentity;

    internal TypedHash ExecutableContentIdentity { get; }

    internal long ExecutableByteLength { get; }

    internal bool Matches(
        ProductionTrustProfile? profile,
        WindowsServiceExecutableContentIdentity? expectedIdentity) =>
        profile is not null &&
        expectedIdentity is not null &&
        string.Equals(
            ExecutableContentIdentity.TypeTag,
            ExecutableContentIdentityType,
            StringComparison.Ordinal) &&
        string.Equals(
            expectedIdentity.ExecutableContentIdentity.TypeTag,
            ExecutableContentIdentityType,
            StringComparison.Ordinal) &&
        string.Equals(
            ProcessImageIdentity.TypeTag,
            PeerHello.ProcessImageIdentityType,
            StringComparison.Ordinal) &&
        string.Equals(
            expectedIdentity.ProcessImageIdentity.TypeTag,
            PeerHello.ProcessImageIdentityType,
            StringComparison.Ordinal) &&
        ExecutableByteLength > 0 &&
        expectedIdentity.ExecutableByteLength > 0 &&
        _installationIdentity.Matches(profile, expectedIdentity._installationIdentity) &&
        ExecutableByteLength == expectedIdentity.ExecutableByteLength &&
        ExecutableContentIdentity.FixedTimeEquals(expectedIdentity.ExecutableContentIdentity);
}

/// <summary>
/// Internal immutable policy vocabulary for exact executable-content identity
/// correlation. It remains an in-memory comparison shape only.
/// </summary>
internal sealed class WindowsServiceExecutableIdentityPolicy
{
    private readonly WindowsServiceExecutableContentIdentity _identity;

    internal WindowsServiceExecutableIdentityPolicy(
        WindowsServiceExecutableContentIdentity identity)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
    }

    internal long BrokerGeneration => _identity.BrokerGeneration;

    internal WindowsSid ServiceSid => _identity.ServiceSid;

    internal TypedHash ProcessImageIdentity => _identity.ProcessImageIdentity;

    internal TypedHash ExecutableContentIdentity => _identity.ExecutableContentIdentity;

    internal long ExecutableByteLength => _identity.ExecutableByteLength;

    internal bool HasExactIdentityBinding(
        ProductionTrustProfile? profile,
        WindowsServiceExecutableContentIdentity? expectedIdentity) =>
        expectedIdentity is not null &&
        string.Equals(
            ExecutableContentIdentity.TypeTag,
            WindowsServiceExecutableContentIdentity.ExecutableContentIdentityType,
            StringComparison.Ordinal) &&
        string.Equals(
            expectedIdentity.ExecutableContentIdentity.TypeTag,
            WindowsServiceExecutableContentIdentity.ExecutableContentIdentityType,
            StringComparison.Ordinal) &&
        string.Equals(
            ProcessImageIdentity.TypeTag,
            PeerHello.ProcessImageIdentityType,
            StringComparison.Ordinal) &&
        string.Equals(
            expectedIdentity.ProcessImageIdentity.TypeTag,
            PeerHello.ProcessImageIdentityType,
            StringComparison.Ordinal) &&
        ExecutableByteLength > 0 &&
        expectedIdentity.ExecutableByteLength > 0 &&
        _identity.Matches(profile, expectedIdentity);
}
