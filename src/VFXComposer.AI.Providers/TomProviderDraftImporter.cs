using System.Text;
using System.Text.Json;
using VFXComposer.AI.Contracts;

namespace VFXComposer.AI.Providers;

/// <summary>
/// Non-sensitive Tom draft import. It cannot produce a profile ID, protocol, credential, capability, or binding.
/// </summary>
public sealed class TomProviderDraftImporter
{
    public TomProviderDraft Import(ReadOnlySpan<byte> utf8Json, bool relayConfirmed)
    {
        RejectSensitivePropertyBeforeValueParsing(utf8Json);
        using var document = ProviderConfigurationCodec.ParseStrict(utf8Json);
        try
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new AiGatewayException(AiErrorCode.ImportRejected);
            }

            var expected = new HashSet<string>(StringComparer.Ordinal)
            {
                "displayName",
                "originSuggestion",
                "endpoint",
                "modelId",
                "timeoutSeconds",
            };
            var observed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                if (!observed.Add(property.Name) || !expected.Contains(property.Name))
                {
                    throw new AiGatewayException(AiErrorCode.ImportRejected);
                }
            }

            foreach (var required in expected)
            {
                if (!observed.Contains(required))
                {
                    throw new AiGatewayException(AiErrorCode.ImportRejected);
                }
            }

            var origin = ParseOrigin(RequireString(root, "originSuggestion"));
            if (origin == ProviderOrigin.Relay && !relayConfirmed)
            {
                throw new AiGatewayException(AiErrorCode.ImportConfirmationRequired);
            }

            var endpointText = RequireString(root, "endpoint");
            Uri endpoint;
            try
            {
                endpoint = new Uri(endpointText, UriKind.Absolute);
            }
            catch (UriFormatException)
            {
                throw new AiGatewayException(AiErrorCode.ImportRejected);
            }

            // A draft has no credential or user-visible loopback exception. HTTPS is therefore the only importable form.
            ProviderConfigurationValidator.ValidateEndpoint(
                new EndpointDefinition(endpoint, allowLoopbackHttp: false),
                new AuthDescriptor(new SecretRef("tom-import-placeholder"), SecretScope.Production));

            return new TomProviderDraft(
                RequireString(root, "displayName"),
                origin,
                endpoint,
                RequireString(root, "modelId"),
                RequireInt32(root, "timeoutSeconds"));
        }
        catch (AiGatewayException)
        {
            throw;
        }
        catch (ArgumentException)
        {
            throw new AiGatewayException(AiErrorCode.ImportRejected);
        }
    }

    private static void RejectSensitivePropertyBeforeValueParsing(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            var reader = new Utf8JsonReader(
                utf8Json,
                new JsonReaderOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 32,
                });
            while (reader.Read())
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                var name = reader.GetString();
                if (name is not null && IsSensitiveTomProperty(name))
                {
                    // Throw before Read() reaches this property's value: ApiKeyProtected is never materialized.
                    throw new AiGatewayException(AiErrorCode.ImportRejected);
                }
            }
        }
        catch (AiGatewayException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw new AiGatewayException(AiErrorCode.ImportRejected);
        }
    }

    private static bool IsSensitiveTomProperty(string name) =>
        name.Equals("ApiKeyProtected", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("ApiKey", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Cookie", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Secret", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("HeaderTemplate", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Command", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Script", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Sidecar", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("RelayProtocol", StringComparison.OrdinalIgnoreCase);

    private static ProviderOrigin ParseOrigin(string value)
    {
        if (!Enum.TryParse<ProviderOrigin>(value, ignoreCase: false, out var origin) || !Enum.IsDefined(origin))
        {
            throw new AiGatewayException(AiErrorCode.ImportRejected);
        }

        return origin;
    }

    private static string RequireString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new AiGatewayException(AiErrorCode.ImportRejected);
        }

        return property.GetString() ?? throw new AiGatewayException(AiErrorCode.ImportRejected);
    }

    private static int RequireInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number ||
            !property.TryGetInt32(out var value) || value is < 1 or > 300)
        {
            throw new AiGatewayException(AiErrorCode.ImportRejected);
        }

        return value;
    }
}

public sealed class TomProviderDraft
{
    public TomProviderDraft(string displayName, ProviderOrigin originSuggestion, Uri endpoint, string modelId, int timeoutSeconds)
    {
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        OriginSuggestion = originSuggestion;
        Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        ModelId = modelId ?? throw new ArgumentNullException(nameof(modelId));
        TimeoutSeconds = timeoutSeconds;
    }

    public string DisplayName { get; }
    public ProviderOrigin OriginSuggestion { get; }
    public Uri Endpoint { get; }
    public string ModelId { get; }
    public int TimeoutSeconds { get; }

    public override string ToString() => "TomProviderDraft(<redacted>)";
}
