using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using VFXComposer.Broker.Configuration;
using VFXComposer.Broker.Security;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;

namespace VFXComposer.Broker.Tests;

[TestClass]
public sealed class WindowsServiceProcessAttestationTests
{
    private const string TargetServiceSidText = "S-1-5-80-101-202-303-404-505";
    private const string AlternateServiceSidText = "S-1-5-80-801-802-803-804-805";
    private const string IssuerServiceSidText = "S-1-5-80-901-902-903-904-905";
    private const string UserSidText = "S-1-5-21-1001-1002-1003-1004";
    private const int TargetProcessId = 81;
    private const uint TargetWindowsSession = 0;
    private const string NativeBrokerImage = "\\Device\\HarddiskVolume7\\Broker\\VFXComposer.Broker.exe";

    [TestMethod]
    public void TokenGroupsRequireExactlyOneEnabledNonDenyOnlyConfiguredServiceSid()
    {
        var expectedServiceSid = WindowsSid.ParseService(TargetServiceSidText);
        var tokenUserContainingTheServiceText = Encoding.UTF8.GetBytes(
            expectedServiceSid.CanonicalValue);

        Assert.IsTrue(TokenFacts(
            [ServiceGroup(expectedServiceSid, WindowsTokenFactSnapshot.GroupEnabled)],
            tokenUserContainingTheServiceText)
            .HasExactlyOneEnabledServiceSid(expectedServiceSid));

        foreach (var facts in new[]
                 {
                     TokenFacts(Array.Empty<WindowsTokenGroupObservation>(), tokenUserContainingTheServiceText),
                     TokenFacts(
                         [ServiceGroup(
                             WindowsSid.ParseService(AlternateServiceSidText),
                             WindowsTokenFactSnapshot.GroupEnabled)]),
                     TokenFacts([ServiceGroup(expectedServiceSid, attributes: 0)]),
                     TokenFacts([ServiceGroup(
                         expectedServiceSid,
                         WindowsTokenFactSnapshot.GroupUseForDenyOnly)]),
                     TokenFacts(
                         [
                             ServiceGroup(expectedServiceSid, WindowsTokenFactSnapshot.GroupEnabled),
                             ServiceGroup(expectedServiceSid, WindowsTokenFactSnapshot.GroupEnabled),
                         ]),
                 })
        {
            Assert.IsFalse(facts.HasExactlyOneEnabledServiceSid(expectedServiceSid));
        }
    }

    [TestMethod]
    public void ProcessEpochIdentityAndBootstrapMaterialRejectCrossGenerationAndSessionFacts()
    {
        Assert.IsTrue(ProcessEpoch.IsCanonicalForProcess(
            TargetProcessId,
            Epoch(TargetProcessId, 84)));
        Assert.IsFalse(ProcessEpoch.IsCanonicalForProcess(
            TargetProcessId + 1,
            Epoch(TargetProcessId, 84)));

        var profile = CreateProfile();
        var target = CreateTargetService();
        var issuer = CreateIssuer();
        var material = CreateMaterial(profile, target);

        Assert.IsTrue(material.IsCurrentAt(1_001));
        Assert.IsFalse(material.IsCurrentAt(2_000));
        Assert.IsTrue(material.Matches(profile, issuer, target));
        Assert.IsFalse(material.Matches(
            profile,
            issuer,
            CreateTargetService(sessionId: "service-17-2")));
        Assert.IsFalse(material.Matches(
            profile,
            issuer,
            CreateTargetService(generation: 18, sessionId: "service-18-1")));

        AssertThrowsArgumentException(() => _ = new WindowsServiceProcessIdentity(
            WindowsSid.ParseService(TargetServiceSidText),
            Image("broker-service-image"),
            TargetProcessId,
            Epoch(TargetProcessId + 1, 84),
            generation: 17,
            sessionId: "service-17-1"));
    }

    [TestMethod]
    public void NativePathFactsAreStructuralAndNotExecutableContentIdentity()
    {
        Assert.IsTrue(WindowsExecutablePathObservation.TryCreate(
            NativeBrokerImage,
            out var baseline));
        Assert.IsNotNull(baseline);
        Assert.IsTrue(WindowsExecutablePathObservation.TryCreate(
            NativeBrokerImage,
            out var replay));
        Assert.IsTrue(WindowsExecutablePathObservation.TryCreate(
            "\\Device\\HarddiskVolume7\\Broker\\Other.exe",
            out var wrongImage));
        Assert.IsFalse(WindowsExecutablePathObservation.TryCreate(
            "C:\\Broker\\VFXComposer.Broker.exe",
            out _));

        Assert.IsTrue(baseline!.FixedEquals(replay));
        Assert.IsFalse(baseline.FixedEquals(wrongImage));

        var attestationSource = ReadWorkspaceSource(
            "services/VFXComposer.Broker/Security/WindowsServiceProcessAttestation.cs");
        StringAssert.Contains(attestationSource, "HasExecutableContentIdentity => false");
    }

    [TestMethod]
    public void DormantAdmissionFailsClosedForStaleAndUnavailableNativeObservation()
    {
        var profile = CreateProfile();
        var unavailableTarget = CreateTargetService(
            processId: int.MaxValue,
            processEpoch: Epoch(int.MaxValue, 84));
        Assert.IsTrue(WindowsExecutablePathObservation.TryCreate(
            NativeBrokerImage,
            out var structuralPath));
        Assert.IsNotNull(structuralPath);
        var expectation = new WindowsServiceProcessAttestationExpectation(
            unavailableTarget,
            TargetWindowsSession,
            structuralPath);
        var material = CreateMaterial(profile, unavailableTarget);

        using (var stale = new HostBootstrapAttestationAdmission(
                   material,
                   profile,
                   CreateIssuer(),
                   expectation))
        {
            Assert.IsFalse(stale.TryCorrelateObservationAt(2_000, out var staleObservation));
            Assert.IsNull(staleObservation);
        }

        using var unavailable = new HostBootstrapAttestationAdmission(
            material,
            profile,
            CreateIssuer(),
            expectation);
        Assert.IsFalse(unavailable.TryCorrelateObservationAt(1_001, out var observation));
        Assert.IsNull(observation);
        Assert.IsFalse(unavailable.TryCorrelateObservationAt(1_001, out var replay));
        Assert.IsNull(replay);
        unavailable.Revoke();
        unavailable.Dispose();
    }

    [TestMethod]
    public void LocalCurrentProcessIsOnlyANegativeObservationAndNotAnInstalledService()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows process/token observation is Windows-only.");
            return;
        }

        var processId = Environment.ProcessId;
        Assert.IsTrue(ProcessIdToSessionId((uint)processId, out var sessionId));
        var expected = new WindowsServiceProcessIdentity(
            WindowsSid.ParseService(TargetServiceSidText),
            Image("unverified-current-process-image"),
            processId,
            ProcessEpoch.Observe(processId),
            generation: 17,
            sessionId: "service-17-1");
        var expectation = new WindowsServiceProcessAttestationExpectation(expected, sessionId);

        // The ordinary test process is not installed or presented as a service.
        // This is only a local OS negative/fail-closed check.
        Assert.IsFalse(WindowsServiceProcessAttestation.TryObserve(expectation, out var observation));
        Assert.IsNull(observation);
    }

    [TestMethod]
    public void ProductionDllAndSourceExposeNoInjectablePinSurface()
    {
        var productionAssembly = typeof(WindowsServiceProcessAttestation).Assembly;
        Assert.IsFalse(productionAssembly.DefinedTypes.Any(type =>
            string.Equals(
                type.Name,
                "IWindowsServiceProcessAttestationPin",
                StringComparison.Ordinal)));

        var observeMethods = typeof(WindowsServiceProcessAttestation)
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => string.Equals(
                method.Name,
                "TryObserve",
                StringComparison.Ordinal))
            .ToArray();
        Assert.AreEqual(1, observeMethods.Length);
        var observeParameters = observeMethods[0].GetParameters();
        Assert.AreEqual(2, observeParameters.Length);
        Assert.AreEqual(
            typeof(WindowsServiceProcessAttestationExpectation),
            observeParameters[0].ParameterType);
        Assert.IsTrue(observeParameters[1].IsOut);
        Assert.AreEqual(
            typeof(WindowsServiceProcessAttestation).MakeByRefType(),
            observeParameters[1].ParameterType);
        Assert.IsFalse(observeParameters.Any(parameter =>
            parameter.Name?.Contains("pin", StringComparison.OrdinalIgnoreCase) == true ||
            parameter.ParameterType.Name.Contains("Pin", StringComparison.Ordinal) ||
            typeof(Delegate).IsAssignableFrom(parameter.ParameterType)));

        var correlateMethods = typeof(HostBootstrapAttestationAdmission)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => string.Equals(
                method.Name,
                "TryCorrelateObservationAt",
                StringComparison.Ordinal))
            .ToArray();
        Assert.AreEqual(1, correlateMethods.Length);
        Assert.IsFalse(correlateMethods[0].GetParameters().Any(parameter =>
            parameter.Name?.Contains("pin", StringComparison.OrdinalIgnoreCase) == true ||
            parameter.ParameterType.Name.Contains("Pin", StringComparison.Ordinal) ||
            typeof(Delegate).IsAssignableFrom(parameter.ParameterType)));

        var nativePinType = productionAssembly.GetType(
            "VFXComposer.Broker.Security.WindowsServiceProcessAttestationPin",
            throwOnError: true);
        Assert.IsNotNull(nativePinType);
        Assert.IsTrue(nativePinType!.IsSealed);
        Assert.IsFalse(nativePinType.IsPublic);
        Assert.IsFalse(nativePinType.GetConstructors(
            BindingFlags.Instance | BindingFlags.Public).Any());

        var attestationSource = ReadWorkspaceSource(
            "services/VFXComposer.Broker/Security/WindowsServiceProcessAttestation.cs");
        var admissionSource = ReadWorkspaceSource(
            "services/VFXComposer.Broker/Configuration/HostBootstrapAttestationAdmission.cs");
        Assert.IsFalse(attestationSource.Contains(
            "IWindowsServiceProcessAttestationPin",
            StringComparison.Ordinal));
        Assert.IsFalse(admissionSource.Contains(
            "IWindowsServiceProcessAttestationPin",
            StringComparison.Ordinal));
        Assert.IsFalse(attestationSource.Contains("Func<", StringComparison.Ordinal));
        Assert.IsFalse(attestationSource.Contains("Action<", StringComparison.Ordinal));
        Assert.IsFalse(admissionSource.Contains("Func<", StringComparison.Ordinal));
        Assert.IsFalse(admissionSource.Contains("Action<", StringComparison.Ordinal));
        Assert.AreEqual(1, CountOrdinal(
            attestationSource,
            "WindowsServiceProcessAttestationPin.TryOpen("));
        StringAssert.Contains(admissionSource, "_state != 1");
        StringAssert.Contains(admissionSource, "candidate?.Dispose();");
    }

    private static WindowsTokenFactSnapshot TokenFacts(
        IEnumerable<WindowsTokenGroupObservation> groups,
        byte[]? tokenUserSid = null) =>
        new(tokenUserSid ?? new byte[] { 1, 5, 0, 0, 0, 5, 18, 0 }, groups);

    private static WindowsTokenGroupObservation ServiceGroup(
        WindowsSid serviceSid,
        uint attributes) =>
        WindowsTokenGroupObservation.FromNative(SidBinary(serviceSid.CanonicalValue), attributes);

    private static byte[] SidBinary(string canonicalSid)
    {
        var parts = canonicalSid.Split('-', StringSplitOptions.None);
        Assert.AreEqual("S", parts[0]);
        var subAuthorityCount = parts.Length - 3;
        var binary = new byte[8 + subAuthorityCount * sizeof(uint)];
        binary[0] = byte.Parse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture);
        binary[1] = checked((byte)subAuthorityCount);
        var authority = ulong.Parse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture);
        for (var index = 0; index < 6; index++)
        {
            binary[7 - index] = (byte)(authority >> (index * 8));
        }

        for (var index = 0; index < subAuthorityCount; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(
                binary.AsSpan(8 + index * sizeof(uint), sizeof(uint)),
                uint.Parse(parts[index + 3], NumberStyles.None, CultureInfo.InvariantCulture));
        }

        return binary;
    }

    private static ProductionTrustProfile CreateProfile(long generation = 17) =>
        new(
            "vfxcomposer-production",
            "broker-production",
            generation,
            WindowsSid.ParseService(TargetServiceSidText),
            WindowsSid.ParseUser(UserSidText),
            new Dictionary<string, IReadOnlySet<TypedHash>>(StringComparer.Ordinal)
            {
                [PeerRoles.Desktop] = new HashSet<TypedHash> { Image("desktop-image") },
                [PeerRoles.Worker] = new HashSet<TypedHash> { Image("worker-image") },
            });

    private static HostBootstrapIssuerProvenance CreateIssuer(long generation = 17) =>
        new(new WindowsServiceProcessIdentity(
            WindowsSid.ParseService(IssuerServiceSidText),
            Image("issuer-image"),
            processId: 71,
            processEpoch: Epoch(71, 42),
            generation,
            sessionId: ServiceSession(generation)));

    private static WindowsServiceProcessIdentity CreateTargetService(
        long generation = 17,
        string? sessionId = null,
        int processId = TargetProcessId,
        string? processEpoch = null) =>
        new(
            WindowsSid.ParseService(TargetServiceSidText),
            Image("broker-service-image"),
            processId,
            processEpoch ?? Epoch(processId, 84),
            generation,
            sessionId ?? ServiceSession(generation));

    private static HostIssuedBootstrapMaterial CreateMaterial(
        ProductionTrustProfile? profile = null,
        WindowsServiceProcessIdentity? target = null,
        long issuedAtUnixMilliseconds = 1_000,
        long expiresAtUnixMilliseconds = 2_000)
    {
        profile ??= CreateProfile();
        target ??= CreateTargetService(profile.BrokerGeneration);
        return new HostIssuedBootstrapMaterial(
            string.Concat("bootstrap-", profile.BrokerGeneration, "-1"),
            CreateIssuer(profile.BrokerGeneration),
            target,
            profile,
            issuedAtUnixMilliseconds,
            expiresAtUnixMilliseconds);
    }

    private static TypedHash Image(string value) =>
        TypedHash.ComputeUtf8(PeerHello.ProcessImageIdentityType, value);

    private static string Epoch(int processId, long ordinal) => string.Concat(
        "winproc-",
        processId,
        "-",
        ordinal.ToString("x16", CultureInfo.InvariantCulture));

    private static string ServiceSession(long generation) => string.Concat(
        "service-",
        generation,
        "-1");

    private static int CountOrdinal(string text, string value)
    {
        var count = 0;
        var startIndex = 0;
        while ((startIndex = text.IndexOf(value, startIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += value.Length;
        }

        return count;
    }

    private static void AssertThrowsArgumentException(Action action)
    {
        try
        {
            action();
            Assert.Fail("Expected an argument validation failure.");
        }
        catch (ArgumentException)
        {
        }
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

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ProcessIdToSessionId(
        uint processId,
        out uint sessionId);
}
