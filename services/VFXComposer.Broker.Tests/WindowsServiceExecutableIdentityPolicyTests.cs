using System.Reflection;
using System.Runtime.InteropServices;
using VFXComposer.Broker.Configuration;
using VFXComposer.Broker.Security;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;

namespace VFXComposer.Broker.Tests;

[TestClass]
public sealed class WindowsServiceExecutableIdentityPolicyTests
{
    private const string ServiceSidText = "S-1-5-80-101-202-303-404-505";
    private const string AlternateServiceSidText = "S-1-5-80-801-802-803-804-805";
    private const string UserSidText = "S-1-5-21-1001-1002-1003-1004";

    [TestMethod]
    public void CandidateBindsTheExactProfileGenerationServiceSidProcessImageContentAndLength()
    {
        var profile = CreateProfile();
        var expectedIdentity = CreateExecutableIdentity(CreateInstallationIdentity(profile));
        var candidate = new WindowsServiceExecutableIdentityPolicy(expectedIdentity);

        Assert.IsTrue(expectedIdentity.Matches(profile, expectedIdentity));
        Assert.IsTrue(candidate.HasExactIdentityBinding(profile, expectedIdentity));
        Assert.IsTrue(WindowsServiceExecutableIdentityPolicyValidator.MatchesDormantCandidate(
            candidate,
            profile,
            expectedIdentity));
        Assert.AreEqual(profile.BrokerGeneration, candidate.BrokerGeneration);
        Assert.IsTrue(candidate.ServiceSid.FixedEquals(profile.ServiceSid));
        Assert.IsTrue(candidate.ProcessImageIdentity.FixedTimeEquals(
            expectedIdentity.ProcessImageIdentity));
        Assert.IsTrue(candidate.ExecutableContentIdentity.FixedTimeEquals(
            expectedIdentity.ExecutableContentIdentity));
        Assert.AreEqual(expectedIdentity.ExecutableByteLength, candidate.ExecutableByteLength);
    }

    [TestMethod]
    public void CorrelationRejectsCrossProfileGenerationServiceSidProcessImageContentAndLength()
    {
        var profile = CreateProfile();
        var expectedIdentity = CreateExecutableIdentity(CreateInstallationIdentity(profile));
        var candidate = new WindowsServiceExecutableIdentityPolicy(expectedIdentity);
        var alternateContent = CreateExecutableIdentity(
            CreateInstallationIdentity(profile),
            contentToken: "alternate-content");
        var alternateLength = CreateExecutableIdentity(
            CreateInstallationIdentity(profile),
            executableByteLength: 8_193);
        var alternateServiceSid = CreateExecutableIdentity(CreateInstallationIdentity(
            profile,
            serviceSidText: AlternateServiceSidText));
        var alternateProcessImage = CreateExecutableIdentity(CreateInstallationIdentity(
            profile,
            imageToken: "alternate-process-image"));

        Assert.IsFalse(WindowsServiceExecutableIdentityPolicyValidator.MatchesDormantCandidate(
            candidate,
            CreateProfile(),
            expectedIdentity));
        Assert.IsFalse(expectedIdentity.Matches(profile, alternateContent));
        Assert.IsFalse(candidate.HasExactIdentityBinding(profile, alternateContent));
        Assert.IsFalse(WindowsServiceExecutableIdentityPolicyValidator.MatchesDormantCandidate(
            candidate,
            profile,
            alternateContent));
        Assert.IsFalse(candidate.HasExactIdentityBinding(profile, alternateLength));
        Assert.IsFalse(WindowsServiceExecutableIdentityPolicyValidator.MatchesDormantCandidate(
            candidate,
            profile,
            alternateLength));
        Assert.IsFalse(candidate.HasExactIdentityBinding(profile, alternateServiceSid));
        Assert.IsFalse(candidate.HasExactIdentityBinding(profile, alternateProcessImage));

        var staleProfile = CreateProfile(generation: profile.BrokerGeneration - 1);
        var staleIdentity = CreateExecutableIdentity(CreateInstallationIdentity(staleProfile));
        Assert.IsFalse(WindowsServiceExecutableIdentityPolicyValidator.MatchesDormantCandidate(
            new WindowsServiceExecutableIdentityPolicy(staleIdentity),
            profile,
            expectedIdentity));
    }

    [TestMethod]
    public void ConstructionRejectsZeroNegativeAndOverflowShapedLengths()
    {
        var profile = CreateProfile();
        var installationIdentity = CreateInstallationIdentity(profile);
        var contentIdentity = Content("broker-executable-content");

        foreach (var length in new[] { 0L, -1L, long.MinValue })
        {
            var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                new WindowsServiceExecutableContentIdentity(
                    installationIdentity,
                    contentIdentity,
                    length));
            Assert.AreEqual("executableByteLength", exception.ParamName);
        }
    }

    [TestMethod]
    public void ConstructionRejectsSameWrongExecutableContentDomainAndDigest()
    {
        var profile = CreateProfile();
        var installationIdentity = CreateInstallationIdentity(profile);
        const string wrongTypeTag = "vfxcomposer.executable-content-test-wrong/1";
        var candidateContentIdentity = TypedHash.ComputeUtf8(
            wrongTypeTag,
            "broker-executable-content");
        var expectedContentIdentity = TypedHash.ComputeUtf8(
            wrongTypeTag,
            "broker-executable-content");

        Assert.IsTrue(candidateContentIdentity.FixedTimeEquals(expectedContentIdentity));

        var candidateException = Assert.ThrowsExactly<ArgumentException>(() =>
            new WindowsServiceExecutableContentIdentity(
                installationIdentity,
                candidateContentIdentity,
                8_192));
        var expectedException = Assert.ThrowsExactly<ArgumentException>(() =>
            new WindowsServiceExecutableContentIdentity(
                installationIdentity,
                expectedContentIdentity,
                8_192));

        Assert.AreEqual("executableContentIdentity", candidateException.ParamName);
        Assert.AreEqual("executableContentIdentity", expectedException.ParamName);
    }

    [TestMethod]
    public void EveryCorrelationEntrypointHasAnExplicitDualOrdinalContentDomainBoundary()
    {
        var policySource = ReadWorkspaceSource(
            "services/VFXComposer.Broker/Configuration/WindowsServiceExecutableIdentityPolicy.cs");
        var validatorSource = ReadWorkspaceSource(
            "services/VFXComposer.Broker/Security/WindowsServiceExecutableIdentityPolicyValidator.cs");

        Assert.IsTrue(CountOrdinal(policySource, "ExecutableContentIdentityType") >= 6);
        Assert.IsTrue(CountOrdinal(validatorSource, "ExecutableContentIdentityType") >= 2);
        Assert.IsTrue(CountOrdinal(policySource, "StringComparison.Ordinal") >= 7);
        Assert.IsTrue(CountOrdinal(validatorSource, "StringComparison.Ordinal") >= 4);
        Assert.IsTrue(policySource.Contains(
            "expectedIdentity.ExecutableContentIdentity.TypeTag",
            StringComparison.Ordinal));
        Assert.IsTrue(validatorSource.Contains(
            "expectedExecutableIdentity.ExecutableContentIdentity.TypeTag",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void PolicyAndValidatorAreInternalSealedImmutableAndNonWire()
    {
        var policyType = typeof(WindowsServiceExecutableIdentityPolicy);
        var identityType = typeof(WindowsServiceExecutableContentIdentity);
        var validatorType = typeof(WindowsServiceExecutableIdentityPolicyValidator);

        foreach (var type in new[] { policyType, identityType, validatorType })
        {
            Assert.IsFalse(type.IsPublic);
            Assert.IsTrue(type.IsSealed);
            Assert.IsFalse(type.GetCustomAttributes(typeof(SerializableAttribute), inherit: false).Any());
            Assert.IsFalse(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public).Any());
        }

        foreach (var type in new[] { policyType, identityType })
        {
            Assert.IsFalse(type.GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(property => property.SetMethod is not null));
            Assert.IsFalse(type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Any(field => !field.IsInitOnly));
            Assert.IsFalse(type.GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SelectMany(constructor => constructor.GetParameters())
                .Any(parameter => parameter.ParameterType == typeof(string) ||
                    parameter.ParameterType == typeof(IntPtr) ||
                    parameter.ParameterType == typeof(UIntPtr) ||
                    typeof(Delegate).IsAssignableFrom(parameter.ParameterType) ||
                    typeof(SafeHandle).IsAssignableFrom(parameter.ParameterType)));
        }

        Assert.IsFalse(policyType.GetMethods(
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Concat(identityType.GetMethods(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Concat(validatorType.GetMethods(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Any(method => method.GetCustomAttribute<DllImportAttribute>() is not null));
    }

    [TestMethod]
    public void ProductSourceIsUnwiredAndContainsNoForbiddenOperationalSurface()
    {
        var policySource = ReadWorkspaceSource(
            "services/VFXComposer.Broker/Configuration/WindowsServiceExecutableIdentityPolicy.cs");
        var validatorSource = ReadWorkspaceSource(
            "services/VFXComposer.Broker/Security/WindowsServiceExecutableIdentityPolicyValidator.cs");
        var productSources = string.Concat(policySource, "\n", validatorSource);

        foreach (var forbidden in new[]
                 {
                     "DllImport",
                     "SafeHandle",
                     "IntPtr",
                     "OpenSCManager",
                     "CreateService",
                     "ChangeServiceConfig",
                     "DeleteService",
                     "StartService",
                     "RegOpenKey",
                     "Microsoft.Win32",
                     "System.Diagnostics",
                     "OpenProcess",
                     "GetCurrentProcess",
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
                     "Authenticode",
                     "X509",
                     "Certificate",
                     "Signature",
                     "UnityEngine",
                     "UnityEditor",
                 })
        {
            Assert.IsFalse(productSources.Contains(forbidden, StringComparison.Ordinal), forbidden);
        }

        var programSource = ReadWorkspaceSource("services/VFXComposer.Broker/Program.cs");
        var brokerPolicySource = ReadWorkspaceSource(
            "services/VFXComposer.Broker/Configuration/BrokerPolicy.cs");
        Assert.IsFalse(programSource.Contains("WindowsServiceExecutableIdentityPolicy", StringComparison.Ordinal));
        Assert.IsFalse(brokerPolicySource.Contains("WindowsServiceExecutableIdentityPolicy", StringComparison.Ordinal));
    }

    private static ProductionTrustProfile CreateProfile(long generation = 17) =>
        new(
            "vfxcomposer-production",
            "broker-production",
            generation,
            WindowsSid.ParseService(ServiceSidText),
            WindowsSid.ParseUser(UserSidText),
            new Dictionary<string, IReadOnlySet<TypedHash>>(StringComparer.Ordinal)
            {
                [PeerRoles.Desktop] = new HashSet<TypedHash> { ProcessImage("desktop-image") },
                [PeerRoles.Worker] = new HashSet<TypedHash> { ProcessImage("worker-image") },
            });

    private static WindowsServiceInstallationIdentity CreateInstallationIdentity(
        ProductionTrustProfile profile,
        string serviceSidText = ServiceSidText,
        string imageToken = "broker-service-image",
        long? brokerGeneration = null) =>
        new(
            profile,
            WindowsSid.ParseService(serviceSidText),
            ProcessImage(imageToken),
            brokerGeneration ?? profile.BrokerGeneration);

    private static WindowsServiceExecutableContentIdentity CreateExecutableIdentity(
        WindowsServiceInstallationIdentity installationIdentity,
        string contentToken = "broker-executable-content",
        long executableByteLength = 8_192) =>
        new(
            installationIdentity,
            Content(contentToken),
            executableByteLength);

    private static TypedHash ProcessImage(string value) =>
        TypedHash.ComputeUtf8(PeerHello.ProcessImageIdentityType, value);

    private static TypedHash Content(string value) =>
        TypedHash.ComputeUtf8(
            WindowsServiceExecutableContentIdentity.ExecutableContentIdentityType,
            value);

    private static int CountOrdinal(string value, string token)
    {
        var count = 0;
        var startIndex = 0;
        while ((startIndex = value.IndexOf(token, startIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += token.Length;
        }

        return count;
    }

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
