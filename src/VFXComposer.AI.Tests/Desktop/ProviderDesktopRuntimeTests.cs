using Microsoft.VisualStudio.TestTools.UnitTesting;
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
    public async Task SettingsFacadeRoundTripsExactEndpointPreservesBlankSecretReplacesNonemptyAndRevokesOnDelete()
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
        Assert.AreEqual(firstProfile.Auth.SecretRef, replacedProfile.Auth.SecretRef);
        Assert.IsTrue(secrets.IsReadable(replacedProfile.Id, replacedProfile.Auth.SecretRef));

        runtime.Settings.DeleteProfile("profile-one");
        Assert.IsFalse(secrets.IsReadable(firstProfile.Id, firstProfile.Auth.SecretRef));
        Assert.AreEqual(0, runtime.Settings.Load().Profiles.Count);
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
}
