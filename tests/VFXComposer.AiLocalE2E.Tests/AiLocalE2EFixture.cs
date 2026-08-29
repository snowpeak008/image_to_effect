using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Desktop;
using VFXComposer.AI.Providers;
using VFXComposer.AI.Providers.Desktop;
using VFXComposer.Desktop.ViewModels;

namespace VFXComposer.AiLocalE2E.Tests;

internal static class A5TestValues
{
    public const string ChatProfileId = "a5-chat-profile";
    public const string ImageProfileId = "a5-image-profile";
    public const string ChatCapabilityId = "a5-chat-capability";
    public const string ImageCapabilityId = "a5-image-capability";
    public const string ChatModel = "a5-chat-model";
    public const string ImageModel = "a5-image-model";
    public const string ChatSecret = "a5-chat-secret-sentinel";
    public const string ImageSecret = "a5-image-secret-sentinel";
    public const string ChatPrompt = "a5-chat-prompt-sentinel";
    public const string ImagePrompt = "a5-image-prompt-sentinel";
    public const string EndpointMarker = "a5-endpoint-secret-sentinel";
    public const string UpstreamBodySentinel = "a5-upstream-body-sentinel";
    public const string ImageRawBytesSentinel = "a5-image-raw-bytes-sentinel";
    public const string ImageBase64Sentinel = "YTUtaW1hZ2UtcmF3LWJ5dGVzLXNlbnRpbmVs";
    public const string ChatResult = "a5-chat-result";
}

/// <summary>Owns only one unique current-user temporary root and deletes it deterministically after every test.</summary>
internal sealed class A5TemporaryRoot : IDisposable
{
    public A5TemporaryRoot()
    {
        Root = Path.Combine(Path.GetTempPath(), "vfxcomposer-a5-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string ConfigurationPath => Path.Combine(Root, "settings", "providers.json");

    public string SecretRoot => Path.Combine(Root, "settings", "secrets");

    public string PrivateImageRoot => Path.Combine(Root, "private-images");

    public ProviderDesktopRuntime CreateRuntime() => new(
        new ProviderConfigurationStore(ConfigurationPath),
        new ProviderSecretStore(SecretRoot),
        new ProviderHealthRegistry(),
        PrivateImageRoot);

    public void AssertNoPrivateImageSessionDirectories()
    {
        var sessions = Directory.Exists(PrivateImageRoot)
            ? Directory.GetDirectories(PrivateImageRoot, "*", SearchOption.TopDirectoryOnly)
            : Array.Empty<string>();
        if (sessions.Length != 0)
        {
            throw new InvalidOperationException("Private image session cleanup did not complete.");
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}

internal static class A5DesktopSettings
{
    public static SettingsViewModel ConfigureTwoProfiles(
        ProviderDesktopRuntime runtime,
        string chatEndpoint,
        string imageEndpoint,
        int timeoutSeconds = 30)
    {
        var settings = new SettingsViewModel(runtime);
        SaveProfile(
            settings,
            A5TestValues.ChatProfileId,
            chatEndpoint,
            timeoutSeconds,
            A5TestValues.ChatCapabilityId,
            A5TestValues.ChatModel,
            imageCapabilityId: null,
            imageModel: null,
            secret: A5TestValues.ChatSecret);
        SaveProfile(
            settings,
            A5TestValues.ImageProfileId,
            imageEndpoint,
            timeoutSeconds,
            chatCapabilityId: null,
            chatModel: null,
            imageCapabilityId: A5TestValues.ImageCapabilityId,
            imageModel: A5TestValues.ImageModel,
            secret: A5TestValues.ImageSecret);
        BindChat(settings, A5TestValues.ChatProfileId, A5TestValues.ChatCapabilityId, A5TestValues.ChatModel);
        BindImage(settings, A5TestValues.ImageProfileId, A5TestValues.ImageCapabilityId, A5TestValues.ImageModel);
        return settings;
    }

    public static SettingsViewModel ConfigureTwoProfilesWithoutSecrets(
        ProviderDesktopRuntime runtime,
        string chatEndpoint,
        string imageEndpoint,
        int timeoutSeconds = 30)
    {
        var settings = new SettingsViewModel(runtime);
        SaveProfile(
            settings,
            A5TestValues.ChatProfileId,
            chatEndpoint,
            timeoutSeconds,
            A5TestValues.ChatCapabilityId,
            A5TestValues.ChatModel,
            imageCapabilityId: null,
            imageModel: null,
            secret: null);
        SaveProfile(
            settings,
            A5TestValues.ImageProfileId,
            imageEndpoint,
            timeoutSeconds,
            chatCapabilityId: null,
            chatModel: null,
            imageCapabilityId: A5TestValues.ImageCapabilityId,
            imageModel: A5TestValues.ImageModel,
            secret: null);
        BindChat(settings, A5TestValues.ChatProfileId, A5TestValues.ChatCapabilityId, A5TestValues.ChatModel);
        BindImage(settings, A5TestValues.ImageProfileId, A5TestValues.ImageCapabilityId, A5TestValues.ImageModel);
        return settings;
    }

    public static void BindChat(SettingsViewModel settings, string profileId, string capabilityId, string modelId)
    {
        settings.ChatBindingProfileId = profileId;
        settings.ChatBindingCapabilityId = capabilityId;
        settings.ChatBindingModelId = modelId;
        settings.SaveChatBindingCommand.Execute(null);
    }

    public static void BindImage(SettingsViewModel settings, string profileId, string capabilityId, string modelId)
    {
        settings.ImageBindingProfileId = profileId;
        settings.ImageBindingCapabilityId = capabilityId;
        settings.ImageBindingModelId = modelId;
        settings.SaveImageBindingCommand.Execute(null);
    }

    public static void SaveChatProfile(
        ProviderDesktopRuntime runtime,
        string endpoint,
        int timeoutSeconds,
        string? secretEntry)
    {
        runtime.Settings.SaveProfile(
            new AiDesktopProfileDraft(
                A5TestValues.ChatProfileId,
                "A5 Chat",
                ProviderOrigin.Custom,
                enabled: true,
                ProviderProtocols.OpenAiCompatibleV1,
                endpoint,
                timeoutSeconds,
                [new AiDesktopCapabilityDraft(A5TestValues.ChatCapabilityId, AiChannel.ChatLlm, A5TestValues.ChatModel)]),
            secretEntry);
    }

    public static void SaveImageProfile(
        ProviderDesktopRuntime runtime,
        string endpoint,
        int timeoutSeconds,
        string? secretEntry)
    {
        runtime.Settings.SaveProfile(
            new AiDesktopProfileDraft(
                A5TestValues.ImageProfileId,
                "A5 Image",
                ProviderOrigin.Custom,
                enabled: true,
                ProviderProtocols.OpenAiCompatibleV1,
                endpoint,
                timeoutSeconds,
                [new AiDesktopCapabilityDraft(A5TestValues.ImageCapabilityId, AiChannel.ImageGeneration, A5TestValues.ImageModel)]),
            secretEntry);
    }

    private static void SaveProfile(
        SettingsViewModel settings,
        string profileId,
        string endpoint,
        int timeoutSeconds,
        string? chatCapabilityId,
        string? chatModel,
        string? imageCapabilityId,
        string? imageModel,
        string? secret)
    {
        settings.BeginNewProfileCommand.Execute(null);
        settings.ProfileId = profileId;
        settings.ProfileDisplayName = "A5 loopback profile";
        settings.ProfileOrigin = ProviderOrigin.Custom;
        settings.ProfileEnabled = true;
        settings.ProfileProtocolId = ProviderProtocols.OpenAiCompatibleV1;
        settings.ProfileOpaqueEndpoint = endpoint;
        settings.ProfileTimeoutSeconds = timeoutSeconds;
        settings.ChatCapabilityId = chatCapabilityId ?? string.Empty;
        settings.ChatModelId = chatModel ?? string.Empty;
        settings.ImageCapabilityId = imageCapabilityId ?? string.Empty;
        settings.ImageModelId = imageModel ?? string.Empty;
        settings.SecretEntry = secret ?? string.Empty;
        settings.SaveProfileCommand.Execute(null);
    }
}

internal static class A5LoopbackPayloads
{
    private static readonly byte[] OnePixelPng = CreateOnePixelPng();

    public static string ChatSuccessJson() =>
        "{\"choices\":[{\"message\":{\"content\":\"" + A5TestValues.ChatResult + "\"}}]}";

    public static string ImageBase64Json() =>
        "{\"data\":[{\"b64_json\":\"" + Convert.ToBase64String(OnePixelPng) + "\"}]}";

    public static string ImageUrlJson(string url) =>
        "{\"data\":[{\"url\":" + JsonSerializer.Serialize(url) + "}]}";

    public static string UpstreamFailureJson() =>
        "{\"error\":\"" + A5TestValues.UpstreamBodySentinel + "\"}";

    public static string ImageBase64SentinelJson() =>
        "{\"data\":[{\"b64_json\":\"" + A5TestValues.ImageBase64Sentinel + "\"}]}";

    public static byte[] OnePixelPngBytes() => OnePixelPng.ToArray();

    public static byte[] ImageRawBytesSentinelBytes() =>
        Encoding.UTF8.GetBytes(A5TestValues.ImageRawBytesSentinel);

    public static bool IsExactChatBody(JsonElement body)
    {
        try
        {
            return body.ValueKind == JsonValueKind.Object &&
                HasExactlyPropertyNames(body, "model", "messages") &&
                body.TryGetProperty("model", out var model) &&
                string.Equals(model.GetString(), A5TestValues.ChatModel, StringComparison.Ordinal) &&
                body.TryGetProperty("messages", out var messages) &&
                messages.ValueKind == JsonValueKind.Array &&
                messages.GetArrayLength() == 1 &&
                messages[0].ValueKind == JsonValueKind.Object &&
                HasExactlyPropertyNames(messages[0], "role", "content") &&
                messages[0].TryGetProperty("role", out var role) &&
                string.Equals(role.GetString(), "user", StringComparison.Ordinal) &&
                messages[0].TryGetProperty("content", out var content) &&
                string.Equals(content.GetString(), A5TestValues.ChatPrompt, StringComparison.Ordinal);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static bool IsExactImageBody(JsonElement body)
    {
        try
        {
            return body.ValueKind == JsonValueKind.Object &&
                HasExactlyPropertyNames(body, "model", "prompt", "size", "n", "response_format") &&
                body.TryGetProperty("model", out var model) &&
                string.Equals(model.GetString(), A5TestValues.ImageModel, StringComparison.Ordinal) &&
                body.TryGetProperty("prompt", out var prompt) &&
                string.Equals(prompt.GetString(), A5TestValues.ImagePrompt, StringComparison.Ordinal) &&
                body.TryGetProperty("size", out var size) &&
                string.Equals(size.GetString(), "64x64", StringComparison.Ordinal) &&
                body.TryGetProperty("n", out var count) &&
                count.ValueKind == JsonValueKind.Number && count.GetInt32() == 1 &&
                body.TryGetProperty("response_format", out var format) &&
                string.Equals(format.GetString(), "b64_json", StringComparison.Ordinal);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool HasExactlyPropertyNames(JsonElement element, params string[] expectedNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var remaining = new HashSet<string>(expectedNames, StringComparer.Ordinal);
        if (remaining.Count != expectedNames.Length)
        {
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!remaining.Remove(property.Name))
            {
                return false;
            }
        }

        return remaining.Count == 0;
    }

    private static byte[] CreateOnePixelPng()
    {
        using var output = new MemoryStream();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header, 1);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), 1);
        header[8] = 8;
        header[9] = 6;
        WriteChunk(output, "IHDR"u8, header);

        byte[] compressed;
        using (var compressedOutput = new MemoryStream())
        {
            using (var compressor = new ZLibStream(compressedOutput, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                compressor.Write([0, 255, 0, 0, 255]);
            }

            compressed = compressedOutput.ToArray();
        }

        try
        {
            WriteChunk(output, "IDAT"u8, compressed);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(compressed);
        }

        WriteChunk(output, "IEND"u8, ReadOnlySpan<byte>.Empty);
        return output.ToArray();
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)data.Length));
        output.Write(length);
        output.Write(type);
        output.Write(data);
        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc(type, data));
        output.Write(crc);
    }

    private static uint Crc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var value = uint.MaxValue;
        foreach (var current in type)
        {
            value = UpdateCrc(value, current);
        }

        foreach (var current in data)
        {
            value = UpdateCrc(value, current);
        }

        return ~value;
    }

    private static uint UpdateCrc(uint value, byte current)
    {
        value ^= current;
        for (var bit = 0; bit < 8; bit++)
        {
            value = (value & 1) == 0 ? value >> 1 : (value >> 1) ^ 0xedb88320u;
        }

        return value;
    }
}
