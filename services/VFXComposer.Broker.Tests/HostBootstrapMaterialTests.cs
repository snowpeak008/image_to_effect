using System.Globalization;
using System.IO.Pipes;
using System.Reflection;
using System.Security.AccessControl;
using VFXComposer.Broker.Configuration;
using VFXComposer.Broker.Security;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;

namespace VFXComposer.Broker.Tests;

[TestClass]
public sealed class HostBootstrapMaterialTests
{
    private const string TargetServiceSidText = "S-1-5-80-101-202-303-404-505";
    private const string IssuerServiceSidText = "S-1-5-80-901-902-903-904-905";
    private const string AlternateServiceSidText = "S-1-5-80-801-802-803-804-805";
    private const string UserSidText = "S-1-5-21-1001-1002-1003-1004";
    private const string AlternateUserSidText = "S-1-5-21-2001-2002-2003-2004";

    [TestMethod]
    public void BootstrapMaterialHasAnImmutableInternalCapabilityShape()
    {
        var material = CreateMaterial();

        Assert.AreEqual("bootstrap-17-1", material.MaterialId);
        Assert.AreEqual(17L, material.BrokerService.Generation);
        Assert.AreEqual("service-17-1", material.BrokerService.SessionId);
        Assert.AreEqual("service-17-1", material.IssuerProvenance.IssuerProcess.SessionId);
        Assert.AreEqual(material.BrokerService.ServiceSid.CanonicalValue, material.PipeAclIntent.ServiceSid.CanonicalValue);
        Assert.AreEqual(UserSidText, material.PipeAclIntent.UserSid.CanonicalValue);

        var foundationTypes = new[]
        {
            typeof(WindowsServiceProcessIdentity),
            typeof(HostBootstrapIssuerProvenance),
            typeof(HostIssuedBootstrapMaterial),
            typeof(WindowsNamedPipeAclProvisioningIntent),
            typeof(HostBootstrapMaterialValidator),
        };
        foreach (var type in foundationTypes)
        {
            Assert.IsFalse(type.IsVisible, type.FullName);
            Assert.IsFalse(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Any(), type.FullName);
            Assert.IsFalse(type.IsDefined(typeof(SerializableAttribute), inherit: false), type.FullName);
            Assert.IsFalse(type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(property => property.SetMethod is not null), type.FullName);
        }
    }

    [TestMethod]
    public void ValidatorRequiresExactIssuerProvenanceEpochGenerationAndSession()
    {
        var profile = CreateProfile();
        var material = CreateMaterial(profile: profile);
        var expectedIssuer = CreateIssuer();
        var expectedBroker = CreateTargetService();

        foreach (var mismatch in new[]
                 {
                     new ValidationInputs(CreateIssuer(imageName: "wrong-issuer-image"), expectedBroker),
                     new ValidationInputs(CreateIssuer(processId: 72, epochOrdinal: 43), expectedBroker),
                     new ValidationInputs(CreateIssuer(epochOrdinal: 99), expectedBroker),
                     new ValidationInputs(CreateIssuer(sessionId: "service-17-2"), expectedBroker),
                     new ValidationInputs(expectedIssuer, CreateTargetService(imageName: "wrong-broker-image")),
                     new ValidationInputs(expectedIssuer, CreateTargetService(sessionId: "service-17-2")),
                 })
        {
            using var validator = new HostBootstrapMaterialValidator(
                profile,
                mismatch.Issuer,
                mismatch.BrokerService);
            Assert.IsFalse(validator.TryAcquire(material, 1_001, out var rejected));
            Assert.IsNull(rejected);
            Assert.AreEqual(0, validator.ActiveLeaseCount);
        }

        using var acceptedValidator = new HostBootstrapMaterialValidator(
            profile,
            expectedIssuer,
            expectedBroker);
        Assert.IsTrue(acceptedValidator.TryAcquire(material, 1_001, out var lease));
        Assert.IsNotNull(lease);
        Assert.IsTrue(lease!.IsUsable);
        lease.Dispose();
    }

    [TestMethod]
    public void MaterialBindsExactServiceSidImageGenerationAndCanonicalAcl()
    {
        var profile = CreateProfile();
        var material = CreateMaterial(profile: profile);

        using (var wrongUserAclValidator = new HostBootstrapMaterialValidator(
                   CreateProfile(userSidText: AlternateUserSidText),
                   CreateIssuer(),
                   CreateTargetService()))
        {
            Assert.IsFalse(wrongUserAclValidator.TryAcquire(material, 1_001, out _));
        }

        using (var wrongServiceValidator = new HostBootstrapMaterialValidator(
                   CreateProfile(serviceSidText: AlternateServiceSidText),
                   CreateIssuer(),
                   CreateTargetService(serviceSidText: AlternateServiceSidText)))
        {
            Assert.IsFalse(wrongServiceValidator.TryAcquire(material, 1_001, out _));
        }

        using (var wrongGenerationValidator = new HostBootstrapMaterialValidator(
                   CreateProfile(generation: 18),
                   CreateIssuer(generation: 18, sessionId: "service-18-1"),
                   CreateTargetService(generation: 18, sessionId: "service-18-1")))
        {
            Assert.IsFalse(wrongGenerationValidator.TryAcquire(material, 1_001, out _));
        }

        Assert.ThrowsExactly<ArgumentException>(() => new WindowsNamedPipeAclProvisioningIntent(
            profile,
            CreateTargetService(serviceSidText: AlternateServiceSidText)));
        Assert.ThrowsExactly<ArgumentException>(() => CreateMaterial(
            profile: profile,
            issuer: new HostBootstrapIssuerProvenance(CreateTargetService())));
        Assert.ThrowsExactly<ArgumentException>(() => CreateMaterial(
            profile: profile,
            issuer: CreateIssuer(generation: 16, sessionId: "service-16-1")));
        Assert.ThrowsExactly<ArgumentException>(() => CreateMaterial(
            profile: profile,
            expiresAtUnixMilliseconds: 301_001));
    }

    [TestMethod]
    public void MaterialRequiresTheExactFrozenProfileInstanceIncludingPeerImagePolicy()
    {
        var issuedProfile = CreateProfile();
        var material = CreateMaterial(profile: issuedProfile);

        using (var sameValuesDifferentInstance = new HostBootstrapMaterialValidator(
                   CreateProfile(),
                   CreateIssuer(),
                   CreateTargetService()))
        {
            Assert.IsFalse(sameValuesDifferentInstance.TryAcquire(material, 1_001, out _));
        }

        using (var changedPeerImagePolicy = new HostBootstrapMaterialValidator(
                   CreateProfile(
                       desktopImageName: "desktop-image-rotated",
                       workerImageName: "worker-image-rotated"),
                   CreateIssuer(),
                   CreateTargetService()))
        {
            Assert.IsFalse(changedPeerImagePolicy.TryAcquire(material, 1_001, out _));
        }

        using var exactProfile = new HostBootstrapMaterialValidator(
            issuedProfile,
            CreateIssuer(),
            CreateTargetService());
        Assert.IsTrue(exactProfile.TryAcquire(material, 1_001, out var lease));
        lease!.Dispose();
    }

    [TestMethod]
    public void ProvisioningIntentBuildsOnlyTheExactWindowsPipeSecurityDescriptor()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Named-pipe ACL provisioning intent is Windows-only.");
            return;
        }

        var material = CreateMaterial();
        var security = material.PipeAclIntent.CreatePipeSecurity();
        var raw = new RawSecurityDescriptor(security.GetSecurityDescriptorBinaryForm(), 0);

        Assert.AreEqual(TargetServiceSidText, raw.Owner!.Value);
        Assert.AreEqual(TargetServiceSidText, raw.Group!.Value);
        Assert.IsNotNull(raw.DiscretionaryAcl);
        Assert.AreEqual(2, raw.DiscretionaryAcl!.Count);
        Assert.AreEqual(UserSidText, ((CommonAce)raw.DiscretionaryAcl[0]).SecurityIdentifier.Value);
        Assert.AreEqual(TargetServiceSidText, ((CommonAce)raw.DiscretionaryAcl[1]).SecurityIdentifier.Value);
        Assert.IsFalse(typeof(WindowsNamedPipeAclProvisioningIntent)
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Any(method => typeof(NamedPipeServerStream).IsAssignableFrom(method.ReturnType)));
    }

    [TestMethod]
    public void ValidatorRejectsReplayStaleAndCrossSessionMaterial()
    {
        var profile = CreateProfile();
        var material = CreateMaterial(profile: profile);
        using var validator = new HostBootstrapMaterialValidator(
            profile,
            CreateIssuer(),
            CreateTargetService());

        Assert.IsTrue(validator.TryAcquire(material, 1_001, out var accepted));
        Assert.IsNotNull(accepted);
        Assert.IsFalse(validator.TryAcquire(material, 1_001, out var replay));
        Assert.IsNull(replay);
        accepted!.Dispose();
        Assert.IsFalse(validator.TryAcquire(material, 1_001, out _));

        var stale = CreateMaterial(
            materialId: "bootstrap-17-2",
            profile: profile,
            issuedAtUnixMilliseconds: 1_000,
            expiresAtUnixMilliseconds: 1_001);
        Assert.IsFalse(validator.TryAcquire(stale, 1_001, out _));

        var crossSession = CreateMaterial(
            materialId: "bootstrap-17-3",
            profile: profile);
        using var wrongSessionValidator = new HostBootstrapMaterialValidator(
            profile,
            CreateIssuer(),
            CreateTargetService(sessionId: "service-17-2"));
        Assert.IsFalse(wrongSessionValidator.TryAcquire(crossSession, 1_001, out _));
    }

    [TestMethod]
    public void LeaseRevocationAndValidatorDisposalInvalidateWithoutReplayRecovery()
    {
        var profile = CreateProfile();
        var first = CreateMaterial(profile: profile);
        var second = CreateMaterial(materialId: "bootstrap-17-2", profile: profile);
        var third = CreateMaterial(materialId: "bootstrap-17-3", profile: profile);
        var validator = new HostBootstrapMaterialValidator(
            profile,
            CreateIssuer(),
            CreateTargetService());

        Assert.IsTrue(validator.TryAcquire(first, 1_001, out var firstLease));
        Assert.IsNotNull(firstLease);
        Assert.IsTrue(validator.Revoke(first.MaterialId));
        Assert.IsFalse(firstLease!.IsUsable);
        Assert.IsFalse(validator.Revoke(first.MaterialId));
        Assert.AreEqual(0, validator.ActiveLeaseCount);
        firstLease.Dispose();

        Assert.IsTrue(validator.TryAcquire(second, 1_001, out var secondLease));
        Assert.IsNotNull(secondLease);
        Assert.IsTrue(secondLease!.IsUsable);
        validator.Dispose();
        Assert.IsFalse(secondLease.IsUsable);
        Assert.AreEqual(0, validator.ActiveLeaseCount);
        Assert.IsFalse(validator.TryAcquire(third, 1_001, out _));
        secondLease.Dispose();
    }

    [TestMethod]
    public void ProductionEntryRemainsFailClosedWhenDormantMaterialValidatesInTests()
    {
        var profile = CreateProfile();
        var material = CreateMaterial(profile: profile);
        using var validator = new HostBootstrapMaterialValidator(
            profile,
            CreateIssuer(),
            CreateTargetService());
        Assert.IsTrue(validator.TryAcquire(material, 1_001, out var lease));
        Assert.IsNotNull(lease);

        Assert.IsFalse(BrokerPolicy.TryLoadProduction(out var policy));
        Assert.IsNull(policy);
        Assert.AreEqual(23, Program.Main());
        Assert.IsTrue(lease!.IsUsable);
        lease.Dispose();
    }

    private static ProductionTrustProfile CreateProfile(
        string serviceSidText = TargetServiceSidText,
        string userSidText = UserSidText,
        long generation = 17,
        string desktopImageName = "desktop-image",
        string workerImageName = "worker-image") =>
        new(
            "vfxcomposer-production",
            "broker-production",
            generation,
            WindowsSid.ParseService(serviceSidText),
            WindowsSid.ParseUser(userSidText),
            new Dictionary<string, IReadOnlySet<TypedHash>>(StringComparer.Ordinal)
            {
                [PeerRoles.Desktop] = new HashSet<TypedHash> { Image(desktopImageName) },
                [PeerRoles.Worker] = new HashSet<TypedHash> { Image(workerImageName) },
            });

    private static HostBootstrapIssuerProvenance CreateIssuer(
        string serviceSidText = IssuerServiceSidText,
        string imageName = "issuer-image",
        int processId = 71,
        long epochOrdinal = 42,
        long generation = 17,
        string? sessionId = null) =>
        new(new WindowsServiceProcessIdentity(
            WindowsSid.ParseService(serviceSidText),
            Image(imageName),
            processId,
            Epoch(processId, epochOrdinal),
            generation,
            sessionId ?? ServiceSession(generation)));

    private static WindowsServiceProcessIdentity CreateTargetService(
        string serviceSidText = TargetServiceSidText,
        string imageName = "broker-service-image",
        int processId = 81,
        long epochOrdinal = 84,
        long generation = 17,
        string? sessionId = null) =>
        new(
            WindowsSid.ParseService(serviceSidText),
            Image(imageName),
            processId,
            Epoch(processId, epochOrdinal),
            generation,
            sessionId ?? ServiceSession(generation));

    private static HostIssuedBootstrapMaterial CreateMaterial(
        string? materialId = null,
        ProductionTrustProfile? profile = null,
        HostBootstrapIssuerProvenance? issuer = null,
        WindowsServiceProcessIdentity? brokerService = null,
        long issuedAtUnixMilliseconds = 1_000,
        long expiresAtUnixMilliseconds = 2_000)
    {
        profile ??= CreateProfile();
        issuer ??= CreateIssuer(generation: profile.BrokerGeneration);
        brokerService ??= CreateTargetService(generation: profile.BrokerGeneration);
        materialId ??= string.Concat(
            "bootstrap-",
            profile.BrokerGeneration.ToString(CultureInfo.InvariantCulture),
            "-1");
        return new HostIssuedBootstrapMaterial(
            materialId,
            issuer,
            brokerService,
            profile,
            issuedAtUnixMilliseconds,
            expiresAtUnixMilliseconds);
    }

    private static TypedHash Image(string value) =>
        TypedHash.ComputeUtf8(PeerHello.ProcessImageIdentityType, value);

    private static string Epoch(int processId, long ordinal) => string.Concat(
        "winproc-",
        processId.ToString(CultureInfo.InvariantCulture),
        "-",
        ordinal.ToString("x16", CultureInfo.InvariantCulture));

    private static string ServiceSession(long generation) => string.Concat(
        "service-",
        generation.ToString(CultureInfo.InvariantCulture),
        "-1");

    private sealed record ValidationInputs(
        HostBootstrapIssuerProvenance Issuer,
        WindowsServiceProcessIdentity BrokerService);
}
