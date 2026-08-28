using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using VFXComposer.AI.Contracts;

namespace VFXComposer.AI.Providers;

/// <summary>
/// Exact, bounded configuration codec. Input must equal the canonical bytes emitted by this codec.
/// </summary>
public static class ProviderConfigurationCodec
{
    public const int MaximumConfigurationBytes = 256 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static byte[] Serialize(AiProviderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ProviderConfigurationValidator.Validate(settings);
        return SerializeCore(settings, AiContractVersions.ProviderConfigurationFormatVersion);
    }

    public static ProviderConfigurationReadResult Deserialize(ReadOnlySpan<byte> utf8Json)
    {
        using var document = ParseStrict(utf8Json);
        try
        {
            var root = document.RootElement;
            RequireObject(root);
            var formatVersion = RequireInt32(root, "formatVersion");
            if (formatVersion is not 0 and not AiContractVersions.ProviderConfigurationFormatVersion)
            {
                throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
            }

            var settings = ParseSettings(root);
            ProviderConfigurationValidator.Validate(settings);
            var canonical = SerializeCore(settings, formatVersion);
            try
            {
                if (!utf8Json.SequenceEqual(canonical))
                {
                    throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(canonical);
            }

            return new ProviderConfigurationReadResult(
                settings,
                ProviderConfigurationFingerprint.Compute(settings),
                requiresMigration: formatVersion == 0);
        }
        catch (AiGatewayException)
        {
            throw;
        }
        catch (ArgumentException)
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }
        catch (InvalidOperationException)
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }
        catch (JsonException)
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }
    }

    private static AiProviderSettings ParseSettings(JsonElement root)
    {
        ValidateObject(root, "formatVersion", "revision", "profiles", "channelBindings");
        var revision = RequireInt64(root, "revision");
        var profilesElement = RequireArray(root, "profiles");
        var profiles = new List<ProviderProfile>();
        foreach (var profileElement in profilesElement.EnumerateArray())
        {
            profiles.Add(ParseProfile(profileElement));
        }

        var bindingsElement = RequireArray(root, "channelBindings");
        var bindings = new List<ChannelBinding>();
        foreach (var bindingElement in bindingsElement.EnumerateArray())
        {
            bindings.Add(ParseBinding(bindingElement));
        }

        return new AiProviderSettings(revision, profiles, bindings);
    }

    private static ProviderProfile ParseProfile(JsonElement element)
    {
        ValidateObject(
            element,
            "id",
            "displayName",
            "origin",
            "enabled",
            "protocol",
            "endpoint",
            "auth",
            "timeoutSeconds",
            "capabilities");
        var protocolElement = RequireObjectProperty(element, "protocol");
        ValidateObject(protocolElement, "id");
        var endpointElement = RequireObjectProperty(element, "endpoint");
        ValidateObject(endpointElement, "uri", "allowLoopbackHttp");
        var authElement = RequireObjectProperty(element, "auth");
        ValidateObject(authElement, "secretRef", "secretScope");

        var capabilitiesElement = RequireArray(element, "capabilities");
        var capabilities = new List<CapabilityDefinition>();
        foreach (var capabilityElement in capabilitiesElement.EnumerateArray())
        {
            ValidateObject(capabilityElement, "id", "channel", "modelId");
            capabilities.Add(new CapabilityDefinition(
                RequireString(capabilityElement, "id"),
                ParseEnum<AiChannel>(RequireString(capabilityElement, "channel")),
                RequireString(capabilityElement, "modelId")));
        }

        var endpointText = RequireString(endpointElement, "uri");
        Uri endpointUri;
        try
        {
            endpointUri = new Uri(endpointText, UriKind.Absolute);
        }
        catch (UriFormatException)
        {
            throw new AiGatewayException(AiErrorCode.EndpointRejected);
        }

        return new ProviderProfile(
            RequireString(element, "id"),
            RequireString(element, "displayName"),
            ParseEnum<ProviderOrigin>(RequireString(element, "origin")),
            RequireBoolean(element, "enabled"),
            new ProtocolBinding(RequireString(protocolElement, "id")),
            new EndpointDefinition(endpointUri, RequireBoolean(endpointElement, "allowLoopbackHttp")),
            new AuthDescriptor(
                new SecretRef(RequireString(authElement, "secretRef")),
                ParseEnum<SecretScope>(RequireString(authElement, "secretScope"))),
            RequireInt32(element, "timeoutSeconds"),
            capabilities);
    }

    private static ChannelBinding ParseBinding(JsonElement element)
    {
        ValidateObject(element, "channel", "profileId", "capabilityId", "modelId");
        return new ChannelBinding(
            ParseEnum<AiChannel>(RequireString(element, "channel")),
            RequireString(element, "profileId"),
            RequireString(element, "capabilityId"),
            RequireString(element, "modelId"));
    }

    private static byte[] SerializeCore(AiProviderSettings settings, int formatVersion)
    {
        var output = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(
            output,
            new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = false,
                SkipValidation = false,
            }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", formatVersion);
            writer.WriteNumber("revision", settings.Revision);
            writer.WritePropertyName("profiles");
            writer.WriteStartArray();
            foreach (var profile in settings.Profiles.OrderBy(static profile => profile.Id, StringComparer.Ordinal))
            {
                WriteProfile(writer, profile);
            }

            writer.WriteEndArray();
            writer.WritePropertyName("channelBindings");
            writer.WriteStartArray();
            foreach (var binding in settings.ChannelBindings.OrderBy(static binding => binding.Channel))
            {
                WriteBinding(writer, binding);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return output.WrittenSpan.ToArray();
    }

    private static void WriteProfile(Utf8JsonWriter writer, ProviderProfile profile)
    {
        writer.WriteStartObject();
        writer.WriteString("id", profile.Id);
        writer.WriteString("displayName", profile.DisplayName);
        writer.WriteString("origin", profile.Origin.ToString());
        writer.WriteBoolean("enabled", profile.Enabled);
        writer.WriteStartObject("protocol");
        writer.WriteString("id", profile.Protocol.ProtocolId);
        writer.WriteEndObject();
        writer.WriteStartObject("endpoint");
        writer.WriteString("uri", profile.Endpoint.Uri.AbsoluteUri);
        writer.WriteBoolean("allowLoopbackHttp", profile.Endpoint.AllowLoopbackHttp);
        writer.WriteEndObject();
        writer.WriteStartObject("auth");
        writer.WriteString("secretRef", profile.Auth.SecretRef.Id);
        writer.WriteString("secretScope", profile.Auth.SecretScope.ToString());
        writer.WriteEndObject();
        writer.WriteNumber("timeoutSeconds", profile.TimeoutSeconds);
        writer.WritePropertyName("capabilities");
        writer.WriteStartArray();
        foreach (var capability in profile.Capabilities.OrderBy(static capability => capability.Id, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("id", capability.Id);
            writer.WriteString("channel", capability.Channel.ToString());
            writer.WriteString("modelId", capability.ModelId);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteBinding(Utf8JsonWriter writer, ChannelBinding binding)
    {
        writer.WriteStartObject();
        writer.WriteString("channel", binding.Channel.ToString());
        writer.WriteString("profileId", binding.ProfileId);
        writer.WriteString("capabilityId", binding.CapabilityId);
        writer.WriteString("modelId", binding.ModelId);
        writer.WriteEndObject();
    }

    internal static JsonDocument ParseStrict(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.Length is < 1 or > MaximumConfigurationBytes || HasUtf8Bom(utf8Json))
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }

        try
        {
            _ = StrictUtf8.GetCharCount(utf8Json);
            var objectKeys = new Stack<HashSet<string>>();
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
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        objectKeys.Push(new HashSet<string>(StringComparer.Ordinal));
                        break;
                    case JsonTokenType.EndObject:
                        if (objectKeys.Count == 0)
                        {
                            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
                        }

                        objectKeys.Pop();
                        break;
                    case JsonTokenType.PropertyName:
                    {
                        var name = reader.GetString();
                        if (name is null || objectKeys.Count == 0 || !objectKeys.Peek().Add(name))
                        {
                            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
                        }

                        break;
                    }
                }
            }

            if (objectKeys.Count != 0)
            {
                throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
            }

            return JsonDocument.Parse(
                utf8Json.ToArray(),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 32,
                });
        }
        catch (AiGatewayException)
        {
            throw;
        }
        catch (DecoderFallbackException)
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }
        catch (JsonException)
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }
    }

    private static bool HasUtf8Bom(ReadOnlySpan<byte> value) =>
        value.Length >= 3 && value[0] == 0xef && value[1] == 0xbb && value[2] == 0xbf;

    private static void ValidateObject(JsonElement element, params string[] required)
    {
        RequireObject(element);
        var expected = new HashSet<string>(required, StringComparer.Ordinal);
        var observed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!observed.Add(property.Name) || !expected.Contains(property.Name))
            {
                throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
            }
        }

        if (!expected.SetEquals(observed))
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }
    }

    private static JsonElement RequireObjectProperty(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }

        return property;
    }

    private static JsonElement RequireArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }

        return property;
    }

    private static string RequireString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }

        return property.GetString() ?? throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
    }

    private static bool RequireBoolean(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) ||
            property.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }

        return property.GetBoolean();
    }

    private static int RequireInt32(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.Number ||
            !property.TryGetInt32(out var value))
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }

        return value;
    }

    private static long RequireInt64(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.Number ||
            !property.TryGetInt64(out var value))
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }

        return value;
    }

    private static void RequireObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }
    }

    private static T ParseEnum<T>(string value)
        where T : struct, Enum
    {
        if (!Enum.TryParse<T>(value, ignoreCase: false, out var parsed) || !Enum.IsDefined(parsed))
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }

        return parsed;
    }
}

public sealed class ProviderConfigurationReadResult
{
    public ProviderConfigurationReadResult(
        AiProviderSettings settings,
        ConfigurationFingerprint fingerprint,
        bool requiresMigration)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Fingerprint = fingerprint ?? throw new ArgumentNullException(nameof(fingerprint));
        RequiresMigration = requiresMigration;
    }

    public AiProviderSettings Settings { get; }
    public ConfigurationFingerprint Fingerprint { get; }
    public bool RequiresMigration { get; }
}
