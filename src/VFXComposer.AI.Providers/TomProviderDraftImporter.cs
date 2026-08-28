using System.Text;
using System.Text.Json;
using VFXComposer.AI.Contracts;

namespace VFXComposer.AI.Providers;

/// <summary>
/// Non-sensitive import of the fixed Tom provider-config shape. The parser is streaming: it materializes only the
/// allow-listed draft fields and skips credential, verification, command, and CLI fields before their values are read.
/// A draft cannot produce a profile, protocol binding, credential, capability, or active channel binding.
/// </summary>
public sealed class TomProviderDraftImporter
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public TomProviderDraft Import(ReadOnlySpan<byte> utf8Json, bool relayProtocolConfirmed)
    {
        try
        {
            if (utf8Json.Length is < 1 or > ProviderConfigurationCodec.MaximumConfigurationBytes || HasUtf8Bom(utf8Json))
            {
                throw new AiGatewayException(AiErrorCode.ImportRejected);
            }

            _ = StrictUtf8.GetCharCount(utf8Json);
            var fields = ReadTomFields(utf8Json);
            var origin = ParseOriginFromTomType(fields.Type!);
            var endpoint = ParseEndpoint(origin, fields.BaseUrl!);
            var modelId = new CapabilityDefinition("tom-import-capability", AiChannel.ChatLlm, fields.DefaultModel!).ModelId;
            var relayProtocolSuggestion = ParseRelayProtocolSuggestion(origin, fields.RelayProtocol!);
            if (origin == ProviderOrigin.Relay && !relayProtocolConfirmed)
            {
                // Tom's detector result is only a suggestion. It is neither an adapter choice nor an active binding.
                throw new AiGatewayException(AiErrorCode.ImportConfirmationRequired);
            }

            return new TomProviderDraft(
                ValidateDisplayName(fields.DisplayName!),
                origin,
                endpoint,
                modelId,
                fields.TimeoutSeconds!.Value,
                relayProtocolSuggestion,
                requiresRelayProtocolConfirmation: false);
        }
        catch (AiGatewayException)
        {
            throw;
        }
        catch (ArgumentException)
        {
            throw new AiGatewayException(AiErrorCode.ImportRejected);
        }
        catch (JsonException)
        {
            throw new AiGatewayException(AiErrorCode.ImportRejected);
        }
    }

    private static TomFields ReadTomFields(ReadOnlySpan<byte> utf8Json)
    {
        var reader = new Utf8JsonReader(
            utf8Json,
            new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 32,
            });
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            throw new AiGatewayException(AiErrorCode.ImportRejected);
        }

        var observed = new HashSet<string>(StringComparer.Ordinal);
        var fields = new TomFields();
        var completed = false;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                completed = true;
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != 1)
            {
                throw new AiGatewayException(AiErrorCode.ImportRejected);
            }

            var name = reader.GetString();
            if (name is null || !observed.Add(name) || !reader.Read())
            {
                throw new AiGatewayException(AiErrorCode.ImportRejected);
            }

            switch (name)
            {
                case "Type":
                    fields.Type = ReadString(ref reader);
                    break;
                case "DisplayName":
                    fields.DisplayName = ReadString(ref reader);
                    break;
                case "BaseUrl":
                    fields.BaseUrl = ReadString(ref reader);
                    break;
                case "DefaultModel":
                    fields.DefaultModel = ReadString(ref reader);
                    break;
                case "RelayProtocol":
                    fields.RelayProtocol = ReadString(ref reader);
                    break;
                case "TimeoutSeconds":
                    fields.TimeoutSeconds = ReadTimeout(ref reader);
                    break;
                case "Id":
                case "Enabled":
                case "ApiKeyProtected":
                case "CommandPath":
                case "RelayWebsiteName":
                case "RelayDetectionSummary":
                case "RelayDetectionConfidence":
                case "UseJsonSchema":
                case "SaveRawResponse":
                case "VerificationAvailable":
                case "VerificationSignature":
                case "VerificationMessage":
                case "LastVerifiedAtUtc":
                    // The reader is positioned at the value. Skip it without calling GetString/GetBytes/JsonDocument:
                    // encrypted Tom API keys, command paths, old verification, and CLI hints never enter our object model.
                    reader.Skip();
                    break;
                default:
                    throw new AiGatewayException(AiErrorCode.ImportRejected);
            }
        }

        if (!completed || reader.Read() ||
            fields.Type is null ||
            fields.DisplayName is null ||
            fields.BaseUrl is null ||
            fields.DefaultModel is null ||
            fields.RelayProtocol is null ||
            fields.TimeoutSeconds is null)
        {
            throw new AiGatewayException(AiErrorCode.ImportRejected);
        }

        return fields;
    }

    private static string ReadString(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new AiGatewayException(AiErrorCode.ImportRejected);
        }

        return reader.GetString() ?? throw new AiGatewayException(AiErrorCode.ImportRejected);
    }

    private static int ReadTimeout(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var value) || value is < 1 or > 300)
        {
            throw new AiGatewayException(AiErrorCode.ImportRejected);
        }

        return value;
    }

    private static ProviderOrigin ParseOriginFromTomType(string type) => type switch
    {
        "openai" or "anthropic" or "gemini" or "deepseek" or "official-web-manual" => ProviderOrigin.Official,
        "relay-api" => ProviderOrigin.Relay,
        "openai-compatible" => ProviderOrigin.Friend,
        "openai-codex-login" or "claude-code-login" or "gemini-cli-login" => ProviderOrigin.Subscription,
        "custom" => ProviderOrigin.Custom,
        _ => throw new AiGatewayException(AiErrorCode.ImportRejected),
    };

    private static Uri? ParseEndpoint(ProviderOrigin origin, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            if (origin == ProviderOrigin.Subscription)
            {
                return null;
            }

            throw new AiGatewayException(AiErrorCode.ImportRejected);
        }

        Uri endpoint;
        try
        {
            endpoint = new Uri(baseUrl, UriKind.Absolute);
            ProviderConfigurationValidator.ValidateEndpoint(
                new EndpointDefinition(endpoint, allowLoopbackHttp: false),
                new AuthDescriptor(new SecretRef("tom-import-placeholder"), SecretScope.Production));
        }
        catch (AiGatewayException)
        {
            throw new AiGatewayException(AiErrorCode.ImportRejected);
        }
        catch (UriFormatException)
        {
            throw new AiGatewayException(AiErrorCode.ImportRejected);
        }

        return endpoint;
    }

    private static string? ParseRelayProtocolSuggestion(ProviderOrigin origin, string relayProtocol)
    {
        if (relayProtocol is not "auto" and not "openai-chat" and not "openai-responses" and not "anthropic-messages" and not "gemini-generate-content")
        {
            throw new AiGatewayException(AiErrorCode.ImportRejected);
        }

        return origin == ProviderOrigin.Relay ? relayProtocol : null;
    }

    private static string ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 80 || displayName.Any(char.IsControl))
        {
            throw new AiGatewayException(AiErrorCode.ImportRejected);
        }

        return displayName;
    }

    private static bool HasUtf8Bom(ReadOnlySpan<byte> value) =>
        value.Length >= 3 && value[0] == 0xef && value[1] == 0xbb && value[2] == 0xbf;

    private sealed class TomFields
    {
        public string? Type { get; set; }
        public string? DisplayName { get; set; }
        public string? BaseUrl { get; set; }
        public string? DefaultModel { get; set; }
        public string? RelayProtocol { get; set; }
        public int? TimeoutSeconds { get; set; }
    }
}

/// <summary>Non-sensitive, unverified data for a UI preview only. It cannot be used as a runtime route.</summary>
public sealed class TomProviderDraft
{
    public TomProviderDraft(
        string displayName,
        ProviderOrigin originSuggestion,
        Uri? endpoint,
        string modelId,
        int timeoutSeconds,
        string? relayProtocolSuggestion,
        bool requiresRelayProtocolConfirmation)
    {
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        OriginSuggestion = originSuggestion;
        Endpoint = endpoint;
        ModelId = modelId ?? throw new ArgumentNullException(nameof(modelId));
        TimeoutSeconds = timeoutSeconds;
        RelayProtocolSuggestion = relayProtocolSuggestion;
        RequiresRelayProtocolConfirmation = requiresRelayProtocolConfirmation;
    }

    public string DisplayName { get; }
    public ProviderOrigin OriginSuggestion { get; }
    public Uri? Endpoint { get; }
    public string ModelId { get; }
    public int TimeoutSeconds { get; }
    public string? RelayProtocolSuggestion { get; }
    public bool RequiresRelayProtocolConfirmation { get; }
    public bool RequiresEndpointConfiguration => Endpoint is null;

    public override string ToString() => "TomProviderDraft(<redacted>)";
}
