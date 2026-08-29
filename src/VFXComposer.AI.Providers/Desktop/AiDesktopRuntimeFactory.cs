using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Desktop;

namespace VFXComposer.AI.Providers.Desktop;

/// <summary>
/// Creates the current-user-only Desktop runtime. Construction derives private local paths but does not create an
/// HTTP client, parse/probe an endpoint, read a secret, or make a provider request.
/// </summary>
public static class AiDesktopRuntimeFactory
{
    public static IAiDesktopRuntime CreateCurrentUser()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationUnavailable);
        }

        var root = Path.Combine(localApplicationData, "VFXComposer", "AI");
        return new ProviderDesktopRuntime(
            new ProviderConfigurationStore(Path.Combine(root, "providers.json")),
            new ProviderSecretStore(Path.Combine(root, "secrets")),
            new ProviderHealthRegistry(),
            privateImageTempRoot: null,
            recipeDraftStorePath: Path.Combine(root, "recipe-drafts.json"));
    }
}
