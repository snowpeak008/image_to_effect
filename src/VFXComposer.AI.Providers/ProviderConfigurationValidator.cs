using System.Net;
using System.Net.Sockets;
using VFXComposer.AI.Contracts;

namespace VFXComposer.AI.Providers;

/// <summary>Validation that is intentionally stricter than a future adapter's input needs.</summary>
public static class ProviderConfigurationValidator
{
    public static void Validate(AiProviderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.FormatVersion != AiContractVersions.ProviderConfigurationFormatVersion)
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }

        var profiles = new Dictionary<string, ProviderProfile>(StringComparer.Ordinal);
        var allCapabilities = new HashSet<string>(StringComparer.Ordinal);
        var secretOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var profile in settings.Profiles)
        {
            if (!Enum.IsDefined(profile.Origin) || !profiles.TryAdd(profile.Id, profile))
            {
                throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
            }

            if (!secretOwners.TryAdd(profile.Auth.SecretRef.Id, profile.Id))
            {
                throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
            }

            ValidateEndpoint(profile.Endpoint, profile.Auth);
            foreach (var capability in profile.Capabilities)
            {
                if (!Enum.IsDefined(capability.Channel) || !allCapabilities.Add(capability.Id))
                {
                    throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
                }
            }
        }

        var boundChannels = new HashSet<AiChannel>();
        foreach (var binding in settings.ChannelBindings)
        {
            if (!Enum.IsDefined(binding.Channel) || !boundChannels.Add(binding.Channel))
            {
                throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
            }

            if (!profiles.TryGetValue(binding.ProfileId, out var profile))
            {
                throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
            }

            var capability = profile.Capabilities.SingleOrDefault(capability =>
                string.Equals(capability.Id, binding.CapabilityId, StringComparison.Ordinal));
            if (capability is null ||
                capability.Channel != binding.Channel ||
                !string.Equals(capability.ModelId, binding.ModelId, StringComparison.Ordinal))
            {
                throw new AiGatewayException(AiErrorCode.CapabilityMismatch);
            }
        }
    }

    public static void ValidateEndpoint(EndpointDefinition endpoint, AuthDescriptor auth)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(auth);
        var uri = endpoint.Uri;
        if (!uri.IsAbsoluteUri ||
            uri.AbsoluteUri.Length > EndpointDefinition.MaximumUriLength ||
            string.IsNullOrEmpty(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new AiGatewayException(AiErrorCode.EndpointRejected);
        }

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            if (endpoint.AllowLoopbackHttp)
            {
                throw new AiGatewayException(AiErrorCode.EndpointRejected);
            }

            return;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            !endpoint.AllowLoopbackHttp ||
            !IsExactLoopbackHost(uri.Host) ||
            auth.SecretScope != SecretScope.DevelopmentOnly)
        {
            throw new AiGatewayException(AiErrorCode.EndpointRejected);
        }
    }

    public static bool IsExactLoopbackHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address) &&
            address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6 &&
            IPAddress.IsLoopback(address);
    }
}
