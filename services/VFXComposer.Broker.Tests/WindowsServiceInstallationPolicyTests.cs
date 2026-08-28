using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using VFXComposer.Broker.Configuration;
using VFXComposer.Broker.Security;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;

namespace VFXComposer.Broker.Tests;

[TestClass]
public sealed class WindowsServiceInstallationPolicyTests
{
    private const string ServiceSidText = "S-1-5-80-101-202-303-404-505";
    private const string AlternateServiceSidText = "S-1-5-80-801-802-803-804-805";
    private const string UserSidText = "S-1-5-21-1001-1002-1003-1004";

    [TestMethod]
    public void CandidateUsesTheFixedLeastPrivilegeVocabularyAndExactIdentityBinding()
    {
        var profile = CreateProfile();
        var serviceIdentity = CreateIdentity(profile);
        var candidate = WindowsServiceInstallationPolicy.CreateCandidate(serviceIdentity);

        Assert.IsTrue(serviceIdentity.Matches(profile, serviceIdentity));
        Assert.IsTrue(candidate.HasExactIdentityBinding(profile, serviceIdentity));
        Assert.IsTrue(WindowsServiceInstallationPolicyValidator.MatchesDormantCandidate(
            candidate,
            profile,
            serviceIdentity));
        Assert.AreEqual("VFXComposerBrokerHost", candidate.ServiceName);
        Assert.AreEqual("VFX Composer Broker Host", candidate.DisplayName);
        Assert.AreEqual(@"NT AUTHORITY\LocalService", candidate.ServiceAccountName);
        Assert.AreEqual(WindowsServiceType.Win32OwnProcess, candidate.ServiceType);
        Assert.AreEqual(WindowsServiceStartMode.Demand, candidate.StartMode);
        Assert.AreEqual(WindowsServiceErrorControl.Normal, candidate.ErrorControl);
        Assert.AreEqual(WindowsServiceSidType.Restricted, candidate.ServiceSidType);
        Assert.AreEqual(WindowsServiceRecoveryMode.None, candidate.RecoveryMode);
        Assert.AreEqual(WindowsServiceInstallationFlags.None, candidate.Flags);
        Assert.IsTrue(candidate.ServiceSid.FixedEquals(serviceIdentity.ServiceSid));
        Assert.IsTrue(candidate.ServiceImageIdentity.FixedTimeEquals(serviceIdentity.ServiceImageIdentity));
    }

    [TestMethod]
    public void ValidatorRejectsEveryAlternatePolicyValueAndUnknownFlag()
    {
        var profile = CreateProfile();
        var serviceIdentity = CreateIdentity(profile);
        var alteredCandidates = new[]
        {
            CreatePolicy(serviceIdentity, serviceType: WindowsServiceType.Win32ShareProcess),
            CreatePolicy(serviceIdentity, serviceType: WindowsServiceType.Win32OwnProcess | WindowsServiceType.InteractiveProcess),
            CreatePolicy(serviceIdentity, serviceType: (WindowsServiceType)0x00000001),
            CreatePolicy(serviceIdentity, serviceType: (WindowsServiceType)0x00000002),
            CreatePolicy(serviceIdentity, serviceType: (WindowsServiceType)0x00000004),
            CreatePolicy(serviceIdentity, serviceType: (WindowsServiceType)0x00000008),
            CreatePolicy(serviceIdentity, serviceType: (WindowsServiceType)0x00000030),
            CreatePolicy(serviceIdentity, serviceType: (WindowsServiceType)0x00000040),
            CreatePolicy(serviceIdentity, serviceType: (WindowsServiceType)0x00000080),
            CreatePolicy(serviceIdentity, serviceType: (WindowsServiceType)0x00000200),
            CreatePolicy(serviceIdentity, serviceType: unchecked((WindowsServiceType)0x80000000)),
            CreatePolicy(serviceIdentity, account: WindowsServiceAccount.LocalSystem),
            CreatePolicy(serviceIdentity, account: WindowsServiceAccount.NetworkService),
            CreatePolicy(serviceIdentity, account: WindowsServiceAccount.ArbitraryPrincipal),
            CreatePolicy(serviceIdentity, credentialMode: WindowsServiceCredentialMode.PasswordPresent),
            CreatePolicy(serviceIdentity, credentialMode: (WindowsServiceCredentialMode)2),
            CreatePolicy(serviceIdentity, startMode: WindowsServiceStartMode.Boot),
            CreatePolicy(serviceIdentity, startMode: WindowsServiceStartMode.System),
            CreatePolicy(serviceIdentity, startMode: WindowsServiceStartMode.Auto),
            CreatePolicy(serviceIdentity, startMode: WindowsServiceStartMode.Disabled),
            CreatePolicy(serviceIdentity, startMode: (WindowsServiceStartMode)5),
            CreatePolicy(serviceIdentity, errorControl: WindowsServiceErrorControl.Ignore),
            CreatePolicy(serviceIdentity, errorControl: WindowsServiceErrorControl.Severe),
            CreatePolicy(serviceIdentity, errorControl: WindowsServiceErrorControl.Critical),
            CreatePolicy(serviceIdentity, errorControl: (WindowsServiceErrorControl)4),
            CreatePolicy(serviceIdentity, serviceSidType: WindowsServiceSidType.None),
            CreatePolicy(serviceIdentity, serviceSidType: WindowsServiceSidType.Unrestricted),
            CreatePolicy(serviceIdentity, serviceSidType: (WindowsServiceSidType)2),
            CreatePolicy(serviceIdentity, recoveryMode: WindowsServiceRecoveryMode.Restart),
            CreatePolicy(serviceIdentity, recoveryMode: WindowsServiceRecoveryMode.Reboot),
            CreatePolicy(serviceIdentity, recoveryMode: WindowsServiceRecoveryMode.ExternalAction),
            CreatePolicy(serviceIdentity, recoveryMode: WindowsServiceRecoveryMode.OwnRestart),
            CreatePolicy(serviceIdentity, recoveryMode: (WindowsServiceRecoveryMode)5),
            CreatePolicy(serviceIdentity, flags: WindowsServiceInstallationFlags.DelayedAutoStart),
            CreatePolicy(serviceIdentity, flags: WindowsServiceInstallationFlags.DependenciesPresent),
            CreatePolicy(serviceIdentity, flags: WindowsServiceInstallationFlags.ArgumentsPresent),
            CreatePolicy(serviceIdentity, flags: unchecked((WindowsServiceInstallationFlags)0x80000000)),
        };

        foreach (var candidate in alteredCandidates)
        {
            Assert.IsFalse(WindowsServiceInstallationPolicyValidator.MatchesDormantCandidate(
                candidate,
                profile,
                serviceIdentity));
        }
    }

    [TestMethod]
    public void ValidatorRejectsStaleCrossProfileGenerationServiceSidAndImageIdentity()
    {
        var profile = CreateProfile();
        var serviceIdentity = CreateIdentity(profile);
        var candidate = WindowsServiceInstallationPolicy.CreateCandidate(serviceIdentity);

        Assert.IsFalse(WindowsServiceInstallationPolicyValidator.MatchesDormantCandidate(
            candidate,
            CreateProfile(),
            serviceIdentity));
        Assert.IsFalse(WindowsServiceInstallationPolicyValidator.MatchesDormantCandidate(
            candidate,
            profile,
            CreateIdentity(profile, imageToken: "alternate-service-image")));
        Assert.IsFalse(WindowsServiceInstallationPolicyValidator.MatchesDormantCandidate(
            candidate,
            profile,
            CreateIdentity(profile, serviceSidText: AlternateServiceSidText)));

        var staleProfile = CreateProfile(generation: profile.BrokerGeneration - 1);
        var staleServiceIdentity = CreateIdentity(staleProfile);
        var staleCandidate = WindowsServiceInstallationPolicy.CreateCandidate(
            staleServiceIdentity);
        Assert.IsFalse(WindowsServiceInstallationPolicyValidator.MatchesDormantCandidate(
            staleCandidate,
            profile,
            serviceIdentity));
    }

    [TestMethod]
    public void IdentityConstructionRejectsSameWrongImageTypeTagAndDigestBeforeDirectBindingCanExist()
    {
        var profile = CreateProfile();
        const string wrongImageTypeTag = "vfxcomposer.install-policy-test-image/1";
        var candidateImageIdentity = TypedHash.ComputeUtf8(
            wrongImageTypeTag,
            "broker-service-image");
        var expectedImageIdentity = TypedHash.ComputeUtf8(
            wrongImageTypeTag,
            "broker-service-image");

        Assert.IsTrue(candidateImageIdentity.FixedTimeEquals(expectedImageIdentity));

        var candidateException = Assert.ThrowsExactly<ArgumentException>(() =>
            new WindowsServiceInstallationIdentity(
                profile,
                WindowsSid.ParseService(ServiceSidText),
                candidateImageIdentity,
                profile.BrokerGeneration));
        var expectedException = Assert.ThrowsExactly<ArgumentException>(() =>
            new WindowsServiceInstallationIdentity(
                profile,
                WindowsSid.ParseService(ServiceSidText),
                expectedImageIdentity,
                profile.BrokerGeneration));

        Assert.AreEqual("serviceImageIdentity", candidateException.ParamName);
        Assert.AreEqual("serviceImageIdentity", expectedException.ParamName);
    }

    [TestMethod]
    public void PolicyAndValidatorAreInternalImmutableAndContainNoCallerStringInputs()
    {
        var policyType = typeof(WindowsServiceInstallationPolicy);
        var identityType = typeof(WindowsServiceInstallationIdentity);
        var validatorType = typeof(WindowsServiceInstallationPolicyValidator);
        var policyProperties = policyType.GetProperties(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.IsFalse(policyType.IsPublic);
        Assert.IsFalse(identityType.IsPublic);
        Assert.IsFalse(validatorType.IsPublic);
        Assert.IsFalse(policyType.GetCustomAttributes(typeof(SerializableAttribute), inherit: false).Any());
        Assert.IsFalse(policyType.GetConstructors(BindingFlags.Instance | BindingFlags.Public).Any());
        Assert.IsFalse(identityType.GetConstructors(BindingFlags.Instance | BindingFlags.Public).Any());
        Assert.IsFalse(policyProperties.Any(property => property.SetMethod is not null));
        Assert.IsFalse(identityType.GetProperties(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(property => property.SetMethod is not null));
        Assert.IsFalse(policyType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Any(field => !field.IsInitOnly));
        Assert.IsFalse(identityType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Any(field => !field.IsInitOnly));
        Assert.IsFalse(policyType.GetConstructors(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(string)));
        Assert.IsFalse(identityType.GetConstructors(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(string)));
        Assert.IsFalse(policyType.GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Any(method => string.Equals(method.Name, "CreateCandidate", StringComparison.Ordinal)));
        CollectionAssert.AreEquivalent(
            new[] { "DisplayName", "ServiceAccountName", "ServiceName" },
            policyProperties
                .Where(property => property.PropertyType == typeof(string))
                .Select(property => property.Name)
                .ToArray());
        Assert.IsFalse(policyProperties.Any(property =>
            property.Name.Contains("Path", StringComparison.Ordinal) ||
            property.Name.Contains("Location", StringComparison.Ordinal) ||
            property.Name.Contains("Password", StringComparison.Ordinal) ||
            property.Name.Contains("Secret", StringComparison.Ordinal) ||
            property.Name.Contains("Handle", StringComparison.Ordinal) ||
            property.Name.Contains("Project", StringComparison.Ordinal) ||
            property.Name.Contains("Authority", StringComparison.Ordinal) ||
            property.Name.Contains("Verdict", StringComparison.Ordinal)));
        Assert.IsFalse(policyProperties.Any(property =>
            property.PropertyType == typeof(IntPtr) || property.PropertyType == typeof(UIntPtr)));
        Assert.IsFalse(policyType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Concat(validatorType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Any(method => method.GetCustomAttribute<DllImportAttribute>() is not null));
    }

    [TestMethod]
    public void ProductSourceIsUnwiredAndContainsNoForbiddenOperationalSurface()
    {
        var policySource = ReadWorkspaceSource(
            "services/VFXComposer.Broker/Configuration/WindowsServiceInstallationPolicy.cs");
        var validatorSource = ReadWorkspaceSource(
            "services/VFXComposer.Broker/Security/WindowsServiceInstallationPolicyValidator.cs");
        var productSources = string.Concat(policySource, "\n", validatorSource);

        foreach (var forbidden in new[]
                 {
                     "DllImport",
                     "OpenSCManager",
                     "CreateService",
                     "ChangeServiceConfig",
                     "DeleteService",
                     "StartService",
                     "RegOpenKey",
                     "Microsoft.Win32",
                     "System.Diagnostics",
                     "Process.Start",
                     "OpenProcess",
                     "GetCurrentProcess",
                     "WindowsServiceProcessIdentity",
                     "System.IO",
                     "System.Net",
                     "System.Environment",
                     "Environment.",
                     "NamedPipe",
                     "Socket",
                     "TcpListener",
                     "File.",
                     "Directory.",
                     "Path.",
                     "UnityEngine",
                     "UnityEditor",
                 })
        {
            Assert.IsFalse(productSources.Contains(forbidden, StringComparison.Ordinal), forbidden);
        }

        var programSource = ReadWorkspaceSource("services/VFXComposer.Broker/Program.cs");
        var brokerPolicySource = ReadWorkspaceSource(
            "services/VFXComposer.Broker/Configuration/BrokerPolicy.cs");
        Assert.IsFalse(programSource.Contains("WindowsServiceInstallationPolicy", StringComparison.Ordinal));
        Assert.IsFalse(brokerPolicySource.Contains("WindowsServiceInstallationPolicy", StringComparison.Ordinal));
    }

    [TestMethod]
    public void NumericVocabularyMatchesLocalWindowsSdkHeadersWhenAvailable()
    {
        var winntHeader = FindWindowsSdkHeader("winnt.h");
        var winsvcHeader = FindWindowsSdkHeader("winsvc.h");
        if (winntHeader is null || winsvcHeader is null)
        {
            return;
        }

        var winnt = File.ReadAllText(winntHeader);
        var winsvc = File.ReadAllText(winsvcHeader);
        AssertHexMacro(winnt, "SERVICE_WIN32_OWN_PROCESS", 0x00000010);
        AssertHexMacro(winnt, "SERVICE_DEMAND_START", 0x00000003);
        AssertHexMacro(winnt, "SERVICE_ERROR_NORMAL", 0x00000001);
        AssertHexMacro(winsvc, "SERVICE_SID_TYPE_NONE", 0x00000000);
        AssertHexMacro(winsvc, "SERVICE_SID_TYPE_UNRESTRICTED", 0x00000001);
        Assert.IsTrue(Regex.IsMatch(
            winsvc,
            @"(?m)^\s*#define\s+SERVICE_SID_TYPE_RESTRICTED\s+\(\s*0x00000002\s+\|\s+SERVICE_SID_TYPE_UNRESTRICTED\s*\)"));
        Assert.IsTrue(Regex.IsMatch(winsvc, @"SC_ACTION_NONE\s*=\s*0"));
        Assert.IsTrue(Regex.IsMatch(winsvc, @"SC_ACTION_RESTART\s*=\s*1"));
        AssertSdkValue((uint)WindowsServiceType.Win32OwnProcess, 0x00000010u);
        AssertSdkValue((uint)WindowsServiceStartMode.Demand, 0x00000003u);
        AssertSdkValue((uint)WindowsServiceErrorControl.Normal, 0x00000001u);
        AssertSdkValue((uint)WindowsServiceSidType.Restricted, 0x00000003u);
        AssertSdkValue((uint)WindowsServiceRecoveryMode.None, 0u);
    }

    private static WindowsServiceInstallationPolicy CreatePolicy(
        WindowsServiceInstallationIdentity serviceIdentity,
        WindowsServiceType serviceType = WindowsServiceType.Win32OwnProcess,
        WindowsServiceAccount account = WindowsServiceAccount.LocalService,
        WindowsServiceCredentialMode credentialMode = WindowsServiceCredentialMode.None,
        WindowsServiceStartMode startMode = WindowsServiceStartMode.Demand,
        WindowsServiceErrorControl errorControl = WindowsServiceErrorControl.Normal,
        WindowsServiceSidType serviceSidType = WindowsServiceSidType.Restricted,
        WindowsServiceRecoveryMode recoveryMode = WindowsServiceRecoveryMode.None,
        WindowsServiceInstallationFlags flags = WindowsServiceInstallationFlags.None) =>
        new(
            serviceIdentity,
            serviceType,
            account,
            credentialMode,
            startMode,
            errorControl,
            serviceSidType,
            recoveryMode,
            flags);

    private static ProductionTrustProfile CreateProfile(long generation = 17) =>
        new(
            "vfxcomposer-production",
            "broker-production",
            generation,
            WindowsSid.ParseService(ServiceSidText),
            WindowsSid.ParseUser(UserSidText),
            new Dictionary<string, IReadOnlySet<TypedHash>>(StringComparer.Ordinal)
            {
                [PeerRoles.Desktop] = new HashSet<TypedHash> { Image("desktop-image") },
                [PeerRoles.Worker] = new HashSet<TypedHash> { Image("worker-image") },
            });

    private static WindowsServiceInstallationIdentity CreateIdentity(
        ProductionTrustProfile profile,
        string serviceSidText = ServiceSidText,
        string imageToken = "broker-service-image") =>
        new(
            profile,
            WindowsSid.ParseService(serviceSidText),
            Image(imageToken),
            profile.BrokerGeneration);

    private static TypedHash Image(string value) =>
        TypedHash.ComputeUtf8(PeerHello.ProcessImageIdentityType, value);

    private static string? FindWindowsSdkHeader(string fileName)
    {
        const string includeRoot = @"C:\Program Files (x86)\Windows Kits\10\Include";
        if (!Directory.Exists(includeRoot))
        {
            return null;
        }

        return Directory.EnumerateDirectories(includeRoot)
            .OrderByDescending(directory => directory, StringComparer.Ordinal)
            .Select(directory => Path.Combine(directory, "um", fileName))
            .FirstOrDefault(File.Exists);
    }

    private static void AssertHexMacro(string header, string macro, uint expected)
    {
        var pattern = string.Concat(
            @"(?m)^\s*#define\s+",
            Regex.Escape(macro),
            @"\s+0x0*",
            expected.ToString("x", CultureInfo.InvariantCulture),
            @"\b");
        Assert.IsTrue(Regex.IsMatch(header, pattern), macro);
    }

    private static void AssertSdkValue(uint actual, uint expected) =>
        Assert.AreEqual(expected, actual);

    private static string ReadWorkspaceSource(string repositoryRelativePath)
    {
        for (DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(
                directory.FullName,
                repositoryRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }
        }

        throw new AssertFailedException($"Could not locate {repositoryRelativePath} from the test output.");
    }
}
