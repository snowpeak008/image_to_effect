using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Desktop;
using VFXComposer.AI.Providers;
using VFXComposer.AI.Providers.Desktop;

namespace VFXComposer.AI.Tests.Desktop;

[TestClass]
public sealed class ProviderDesktopRuntimeTests
{
    private const string OpaqueEndpoint =
        "  custom+relay://user:synthetic-user-info@[2001:db8::not-an-ipv6]:99999/complete?query=synthetic-query#fragment  ";

    [TestMethod]
    public async Task SettingsFacadeRoundTripsExactEndpointPreservesBlankSecretReplacesFreshReferenceAndRevokes()
    {
        using var directory = new A1TestDirectory();
        var store = new ProviderConfigurationStore(Path.Combine(directory.Path, "providers.json"));
        var secrets = new ProviderSecretStore(Path.Combine(directory.Path, "secrets"));
        await using var runtime = new ProviderDesktopRuntime(store, secrets, new ProviderHealthRegistry());

        var first = runtime.Settings.SaveProfile(Profile(OpaqueEndpoint), "synthetic-initial-secret");
        var firstSummary = first.Profiles.Single();
        Assert.IsFalse(firstSummary.EndpointSummary.Contains("synthetic-user-info", StringComparison.Ordinal));
        Assert.IsFalse(firstSummary.EndpointSummary.Contains("synthetic-query", StringComparison.Ordinal));
        Assert.IsFalse(first.ToString().Contains(OpaqueEndpoint, StringComparison.Ordinal));

        var firstProfile = store.Load().Configuration.Settings.Profiles.Single();
        Assert.IsTrue(secrets.IsReadable(firstProfile.Id, firstProfile.Auth.SecretRef));

        var edited = runtime.Settings.BeginProfileEdit("profile-one");
        Assert.AreEqual(OpaqueEndpoint, edited.Profile.OpaqueEndpoint);
        Assert.IsFalse(edited.ToString().Contains(OpaqueEndpoint, StringComparison.Ordinal));

        runtime.Settings.SaveProfile(Profile(OpaqueEndpoint), string.Empty);
        var preservedProfile = store.Load().Configuration.Settings.Profiles.Single();
        Assert.AreEqual(firstProfile.Auth.SecretRef, preservedProfile.Auth.SecretRef);
        Assert.IsTrue(secrets.IsReadable(preservedProfile.Id, preservedProfile.Auth.SecretRef));

        runtime.Settings.SaveProfile(Profile(OpaqueEndpoint), "synthetic-replacement-secret");
        var replacedProfile = store.Load().Configuration.Settings.Profiles.Single();
        Assert.AreNotEqual(firstProfile.Auth.SecretRef, replacedProfile.Auth.SecretRef);
        Assert.IsFalse(secrets.IsReadable(firstProfile.Id, firstProfile.Auth.SecretRef));
        Assert.IsTrue(secrets.IsReadable(replacedProfile.Id, replacedProfile.Auth.SecretRef));

        var revoked = runtime.Settings.RevokeSecret("profile-one");
        var revokedProfile = store.Load().Configuration.Settings.Profiles.Single();
        Assert.AreNotEqual(replacedProfile.Auth.SecretRef, revokedProfile.Auth.SecretRef);
        Assert.IsFalse(secrets.IsReadable(replacedProfile.Id, replacedProfile.Auth.SecretRef));
        Assert.IsFalse(secrets.IsReadable(revokedProfile.Id, revokedProfile.Auth.SecretRef));
        Assert.IsFalse(revoked.Profiles.Single().HasSecret);
        Assert.IsFalse(runtime.Settings.BeginProfileEdit("profile-one").HasSecret);

        runtime.Settings.DeleteProfile("profile-one");
        Assert.IsFalse(secrets.IsReadable(revokedProfile.Id, revokedProfile.Auth.SecretRef));
        Assert.AreEqual(0, runtime.Settings.Load().Profiles.Count);
    }

    [TestMethod]
    public async Task OversizedSecretReplacementLeavesTheExistingConfigurationAndSecretUntouched()
    {
        using var directory = new A1TestDirectory();
        var store = new ProviderConfigurationStore(Path.Combine(directory.Path, "providers.json"));
        var secrets = new ProviderSecretStore(Path.Combine(directory.Path, "secrets"));
        await using var runtime = new ProviderDesktopRuntime(store, secrets, new ProviderHealthRegistry());
        runtime.Settings.SaveProfile(Profile(OpaqueEndpoint), "synthetic-initial-secret");
        var before = store.Load().Configuration;
        var oldProfile = before.Settings.Profiles.Single();

        var exception = Throws(() =>
            runtime.Settings.SaveProfile(
                Profile("https://synthetic.invalid/replacement?opaque=one"),
                new string('x', (16 * 1024) + 1)));

        Assert.AreEqual(AiErrorCode.SecretUnavailable, exception.Code);
        var after = store.Load().Configuration;
        var afterProfile = after.Settings.Profiles.Single();
        Assert.AreEqual(before.Settings.Revision, after.Settings.Revision);
        Assert.AreEqual(oldProfile.Auth.SecretRef, afterProfile.Auth.SecretRef);
        Assert.AreEqual(OpaqueEndpoint, afterProfile.Endpoint.Value);
        Assert.IsTrue(secrets.IsReadable(oldProfile.Id, oldProfile.Auth.SecretRef));
    }

    [TestMethod]
    public async Task ConfigurationSaveFailureAfterSecretStagingReclaimsTheFreshReferenceAndKeepsTheOldOne()
    {
        using var directory = new A1TestDirectory();
        var store = new ProviderConfigurationStore(Path.Combine(directory.Path, "providers.json"));
        var secretRoot = Path.Combine(directory.Path, "secrets");
        var secrets = new ProviderSecretStore(secretRoot);
        var health = new ProviderHealthRegistry();
        await using var runtime = new ProviderDesktopRuntime(store, secrets, health);
        runtime.Settings.SaveProfile(Profile(OpaqueEndpoint), "synthetic-initial-secret");
        var before = store.Load().Configuration;
        var oldProfile = before.Settings.Profiles.Single();
        FileStream? writeBlocker = null;
        var settings = CreateSettingsWithCallbacks(
            store,
            secrets,
            health,
            () => { },
            () =>
            {
                // Store.Save can still read the existing configuration through this shared-read lease, but its atomic
                // replacement cannot write/delete it. This exercises a real post-staging configuration write failure.
                writeBlocker = new FileStream(
                    Path.Combine(directory.Path, "providers.json"),
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);
            });

        AiGatewayException exception;
        try
        {
            exception = Throws(() => settings.SaveProfile(
                Profile("https://synthetic.invalid/replacement?opaque=two"),
                "synthetic-replacement-secret"));
        }
        finally
        {
            writeBlocker?.Dispose();
        }

        Assert.AreEqual(AiErrorCode.ConfigurationUnavailable, exception.Code);
        var after = store.Load().Configuration;
        var afterProfile = after.Settings.Profiles.Single();
        Assert.AreEqual(before.Settings.Revision, after.Settings.Revision);
        Assert.AreEqual(oldProfile.Auth.SecretRef, afterProfile.Auth.SecretRef);
        Assert.AreEqual(OpaqueEndpoint, afterProfile.Endpoint.Value);
        Assert.IsTrue(secrets.IsReadable(oldProfile.Id, oldProfile.Auth.SecretRef));
        Assert.AreEqual(1, Directory.GetFiles(secretRoot, "*.secret", SearchOption.TopDirectoryOnly).Length);
    }

    [TestMethod]
    public async Task ConcurrentRevisionSaveRejectsTheStagedEditWithoutReplacingTheCurrentSecret()
    {
        using var directory = new A1TestDirectory();
        var store = new ProviderConfigurationStore(Path.Combine(directory.Path, "providers.json"));
        var secretRoot = Path.Combine(directory.Path, "secrets");
        var secrets = new ProviderSecretStore(secretRoot);
        var health = new ProviderHealthRegistry();
        await using var runtime = new ProviderDesktopRuntime(store, secrets, health);
        runtime.Settings.SaveProfile(Profile(OpaqueEndpoint), "synthetic-initial-secret");
        var before = store.Load().Configuration;
        var oldProfile = before.Settings.Profiles.Single();
        var concurrentWriteCount = 0;
        var settings = CreateSettingsWithRevisionConflict(store, secrets, health, () =>
        {
            Interlocked.Increment(ref concurrentWriteCount);
            var current = store.Load().Configuration.Settings;
            store.Save(new AiProviderSettings(
                checked(current.Revision + 1),
                current.Profiles,
                current.ChannelBindings));
        });

        var exception = Throws(() => settings.SaveProfile(
            Profile("https://synthetic.invalid/concurrent?opaque=three"),
            "synthetic-replacement-secret"));

        Assert.AreEqual(AiErrorCode.ConfigurationInvalid, exception.Code);
        Assert.AreEqual(1, concurrentWriteCount);
        var after = store.Load().Configuration;
        var afterProfile = after.Settings.Profiles.Single();
        Assert.AreEqual(before.Settings.Revision + 1, after.Settings.Revision);
        Assert.AreEqual(oldProfile.Auth.SecretRef, afterProfile.Auth.SecretRef);
        Assert.AreEqual(OpaqueEndpoint, afterProfile.Endpoint.Value);
        Assert.IsTrue(secrets.IsReadable(oldProfile.Id, oldProfile.Auth.SecretRef));
        Assert.AreEqual(1, Directory.GetFiles(secretRoot, "*.secret", SearchOption.TopDirectoryOnly).Length);
    }

    [TestMethod]
    public async Task CommittedSecretReplacementStillReturnsTheSavedSnapshotWhenAdapterInvalidationThrows()
    {
        using var directory = new A1TestDirectory();
        var store = new ProviderConfigurationStore(Path.Combine(directory.Path, "providers.json"));
        var secrets = new ProviderSecretStore(Path.Combine(directory.Path, "secrets"));
        var health = new ProviderHealthRegistry();
        await using var runtime = new ProviderDesktopRuntime(store, secrets, health);
        runtime.Settings.SaveProfile(Profile(OpaqueEndpoint), "synthetic-initial-secret");
        var oldProfile = store.Load().Configuration.Settings.Profiles.Single();
        var settings = CreateSettingsWithCallbacks(
            store,
            secrets,
            health,
            () => throw new InvalidOperationException("synthetic adapter invalidation failure"),
            beforeConfigurationSave: null);

        var snapshot = settings.SaveProfile(
            Profile("https://synthetic.invalid/committed?opaque=four"),
            "synthetic-replacement-secret");

        var committedProfile = store.Load().Configuration.Settings.Profiles.Single();
        Assert.AreNotEqual(oldProfile.Auth.SecretRef, committedProfile.Auth.SecretRef);
        Assert.IsTrue(snapshot.Profiles.Single().HasSecret);
        Assert.IsTrue(secrets.IsReadable(committedProfile.Id, committedProfile.Auth.SecretRef));
        Assert.IsFalse(secrets.IsReadable(oldProfile.Id, oldProfile.Auth.SecretRef));
    }

    [TestMethod]
    public async Task SettingsFacadeKeepsChatAndImageBindingsSeparateAndStartsBothUnknown()
    {
        using var directory = new A1TestDirectory();
        await using var runtime = new ProviderDesktopRuntime(
            new ProviderConfigurationStore(Path.Combine(directory.Path, "providers.json")),
            new ProviderSecretStore(Path.Combine(directory.Path, "secrets")),
            new ProviderHealthRegistry());

        runtime.Settings.SaveProfile(Profile(OpaqueEndpoint), secretEntry: null);
        runtime.Settings.SaveChannelBinding(new AiDesktopChannelBindingDraft(
            AiChannel.ChatLlm,
            "profile-one",
            "chat-one",
            "chat-model-one"));
        var snapshot = runtime.Settings.SaveChannelBinding(new AiDesktopChannelBindingDraft(
            AiChannel.ImageGeneration,
            "profile-one",
            "image-one",
            "image-model-one"));

        Assert.AreEqual(2, snapshot.Bindings.Count);
        var chat = snapshot.Bindings.Single(binding => binding.Channel == AiChannel.ChatLlm);
        var image = snapshot.Bindings.Single(binding => binding.Channel == AiChannel.ImageGeneration);
        Assert.AreEqual("chat-one", chat.CapabilityId);
        Assert.AreEqual("chat-model-one", chat.ModelId);
        Assert.AreEqual("image-one", image.CapabilityId);
        Assert.AreEqual("image-model-one", image.ModelId);
        Assert.AreEqual(
            AiDesktopChannelStatusKind.Unknown,
            snapshot.ChannelStatuses.Single(status => status.Channel == AiChannel.ChatLlm).State);
        Assert.AreEqual(
            AiDesktopChannelStatusKind.Unknown,
            snapshot.ChannelStatuses.Single(status => status.Channel == AiChannel.ImageGeneration).State);
    }

    [TestMethod]
    public async Task RuntimeConstructionAndSettingsOperationsDoNotCreateTransportOrAlterObservedHealth()
    {
        using var directory = new A1TestDirectory();
        var health = new ProviderHealthRegistry();
        await using var runtime = new ProviderDesktopRuntime(
            new ProviderConfigurationStore(Path.Combine(directory.Path, "providers.json")),
            new ProviderSecretStore(Path.Combine(directory.Path, "secrets")),
            health);

        runtime.Settings.Load();
        runtime.Settings.SaveProfile(Profile(OpaqueEndpoint), secretEntry: null);
        runtime.Settings.SaveChannelBinding(new AiDesktopChannelBindingDraft(
            AiChannel.ChatLlm,
            "profile-one",
            "chat-one",
            "chat-model-one"));
        runtime.Settings.Load();

        Assert.IsNull(health.Get("profile-one", "chat-one", AiChannel.ChatLlm));
    }

    private static AiDesktopProfileDraft Profile(string endpoint) => new(
        "profile-one",
        "Synthetic profile",
        ProviderOrigin.Custom,
        enabled: true,
        ProviderProtocols.OpenAiCompatibleV1,
        endpoint,
        30,
        [
            new AiDesktopCapabilityDraft("chat-one", AiChannel.ChatLlm, "chat-model-one"),
            new AiDesktopCapabilityDraft("image-one", AiChannel.ImageGeneration, "image-model-one"),
        ]);

    private static IAiDesktopSettings CreateSettingsWithRevisionConflict(
        ProviderConfigurationStore store,
        ProviderSecretStore secrets,
        ProviderHealthRegistry health,
        Action beforeConfigurationSave)
        => CreateSettingsWithCallbacks(
            store,
            secrets,
            health,
            () => { },
            beforeConfigurationSave);

    private static IAiDesktopSettings CreateSettingsWithCallbacks(
        ProviderConfigurationStore store,
        ProviderSecretStore secrets,
        ProviderHealthRegistry health,
        Action configurationChanged,
        Action? beforeConfigurationSave)
    {
        var settingsType = typeof(ProviderDesktopRuntime).Assembly.GetType(
            "VFXComposer.AI.Providers.Desktop.ProviderDesktopSettings",
            throwOnError: true)!;
        var instance = Activator.CreateInstance(
            settingsType,
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                store,
                secrets,
                health,
                configurationChanged,
                beforeConfigurationSave,
            ],
            culture: null);
        Assert.IsInstanceOfType<IAiDesktopSettings>(instance);
        return (IAiDesktopSettings)instance!;
    }

    private static AiGatewayException Throws(Action action)
    {
        try
        {
            action();
        }
        catch (AiGatewayException exception)
        {
            return exception;
        }

        Assert.Fail("Expected an AiGatewayException.");
        throw new InvalidOperationException("Unreachable.");
    }
}
