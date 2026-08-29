using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Chat;

namespace VFXComposer.AI.Providers.Chat;

internal static class ChatProtocolCodec
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static byte[] CreateRequestPayload(ChatResolvedRoute route, ChatChannelRequest request)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(request);

        // Utf8JsonWriter writes directly into this bounded stream, so an over-large prompt cannot first expand an
        // unbounded intermediary before the request-size policy rejects it.
        using var buffer = new BoundedMemoryStream(ChatChannelLimits.MaximumRequestBytes);
        try
        {
            using (var writer = new Utf8JsonWriter(buffer))
            {
                switch (route.Protocol)
                {
                    case ChatWireProtocol.OpenAiChatCompletions:
                    case ChatWireProtocol.OpenAiCompatible:
                        WriteOpenAiChatCompletions(writer, route, request);
                        break;
                    case ChatWireProtocol.OpenAiResponses:
                        WriteOpenAiResponses(writer, route, request);
                        break;
                    case ChatWireProtocol.AnthropicMessages:
                        WriteAnthropicMessages(writer, route, request);
                        break;
                    case ChatWireProtocol.GeminiGenerateContent:
                        WriteGeminiGenerateContent(writer, request);
                        break;
                    default:
                        throw new ChatChannelException(ChatChannelErrorCode.ProtocolUnsupported);
                }
            }

            if (buffer.Length > ChatChannelLimits.MaximumRequestBytes)
            {
                throw new ChatChannelException(ChatChannelErrorCode.PayloadTooLarge);
            }

            return buffer.ToArray();
        }
        catch (ChatChannelException)
        {
            throw;
        }
        catch (ArgumentException)
        {
            throw new ChatChannelException(ChatChannelErrorCode.RequestInvalid);
        }
        catch (JsonException)
        {
            throw new ChatChannelException(ChatChannelErrorCode.RequestInvalid);
        }
        finally
        {
            if (buffer.TryGetBuffer(out var segment))
            {
                CryptographicOperations.ZeroMemory(segment.AsSpan(0, checked((int)buffer.Length)));
            }
        }
    }

    public static void ApplyAuthentication(HttpRequestMessage message, ChatWireProtocol protocol, ReadOnlySpan<byte> secretBytes)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (secretBytes.Length == 0)
        {
            throw new ChatChannelException(ChatChannelErrorCode.SecretUnavailable);
        }

        string? secret = null;
        try
        {
            secret = StrictUtf8.GetString(secretBytes);
            if (secret.Length == 0 || secret.IndexOfAny(['\r', '\n', '\0']) >= 0)
            {
                throw new ChatChannelException(ChatChannelErrorCode.SecretUnavailable);
            }

            switch (protocol)
            {
                case ChatWireProtocol.OpenAiChatCompletions:
                case ChatWireProtocol.OpenAiResponses:
                case ChatWireProtocol.OpenAiCompatible:
                    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
                    break;
                case ChatWireProtocol.AnthropicMessages:
                    message.Headers.Add("x-api-key", secret);
                    message.Headers.Add("anthropic-version", "2023-06-01");
                    break;
                case ChatWireProtocol.GeminiGenerateContent:
                    message.Headers.Add("x-goog-api-key", secret);
                    break;
                default:
                    throw new ChatChannelException(ChatChannelErrorCode.ProtocolUnsupported);
            }
        }
        catch (ChatChannelException)
        {
            throw;
        }
        catch (DecoderFallbackException)
        {
            throw new ChatChannelException(ChatChannelErrorCode.SecretUnavailable);
        }
        catch (ArgumentException)
        {
            throw new ChatChannelException(ChatChannelErrorCode.SecretUnavailable);
        }
        catch (FormatException)
        {
            throw new ChatChannelException(ChatChannelErrorCode.SecretUnavailable);
        }
        finally
        {
            // HttpHeaders retains its own request-local value through SendAsync.  Drop our only local reference as
            // soon as the header has been materialized; the DPAPI lease buffer is disposed by the caller.
            secret = null;
        }
    }

    public static ChatChannelResult ParseSuccessResponse(
        ChatWireProtocol protocol,
        ChatChannelRequest request,
        byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ChatChannelException(ChatChannelErrorCode.ResponseMalformed);
            }

            var text = protocol switch
            {
                ChatWireProtocol.OpenAiChatCompletions or ChatWireProtocol.OpenAiCompatible => ParseOpenAiChatText(root),
                ChatWireProtocol.OpenAiResponses => ParseOpenAiResponsesText(root),
                ChatWireProtocol.AnthropicMessages => ParseAnthropicText(root),
                ChatWireProtocol.GeminiGenerateContent => ParseGeminiText(root),
                _ => throw new ChatChannelException(ChatChannelErrorCode.ProtocolUnsupported),
            };
            var usage = protocol switch
            {
                ChatWireProtocol.OpenAiChatCompletions or ChatWireProtocol.OpenAiCompatible =>
                    ParseUsage(root, "usage", "prompt_tokens", "completion_tokens", "total_tokens"),
                ChatWireProtocol.OpenAiResponses =>
                    ParseUsage(root, "usage", "input_tokens", "output_tokens", "total_tokens"),
                ChatWireProtocol.AnthropicMessages =>
                    ParseUsage(root, "usage", "input_tokens", "output_tokens", totalProperty: null),
                ChatWireProtocol.GeminiGenerateContent =>
                    ParseUsage(root, "usageMetadata", "promptTokenCount", "candidatesTokenCount", "totalTokenCount"),
                _ => null,
            };

            JsonElement? structured = null;
            if (request.StructuredOutput is not null)
            {
                using var structuredDocument = JsonDocument.Parse(text);
                structured = structuredDocument.RootElement.Clone();
            }

            return new ChatChannelResult(request.CorrelationId, text, usage, structured);
        }
        catch (ChatChannelException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw new ChatChannelException(ChatChannelErrorCode.ResponseMalformed);
        }
        catch (ArgumentException)
        {
            throw new ChatChannelException(ChatChannelErrorCode.ResponseMalformed);
        }
        catch (OverflowException)
        {
            throw new ChatChannelException(ChatChannelErrorCode.ResponseMalformed);
        }
    }

    private static void WriteOpenAiChatCompletions(Utf8JsonWriter writer, ChatResolvedRoute route, ChatChannelRequest request)
    {
        writer.WriteStartObject();
        writer.WriteString("model", route.Capability.ModelId);
        writer.WritePropertyName("messages");
        writer.WriteStartArray();
        foreach (var message in request.Messages)
        {
            writer.WriteStartObject();
            writer.WriteString("role", OpenAiRole(message.Role));
            writer.WriteString("content", message.Content);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        if (request.StructuredOutput is not null)
        {
            writer.WritePropertyName("response_format");
            writer.WriteStartObject();
            writer.WriteString("type", "json_schema");
            writer.WritePropertyName("json_schema");
            writer.WriteStartObject();
            writer.WriteString("name", request.StructuredOutput.Name);
            writer.WriteBoolean("strict", true);
            writer.WritePropertyName("schema");
            request.StructuredOutput.JsonSchema.WriteTo(writer);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteOpenAiResponses(Utf8JsonWriter writer, ChatResolvedRoute route, ChatChannelRequest request)
    {
        writer.WriteStartObject();
        writer.WriteString("model", route.Capability.ModelId);
        writer.WritePropertyName("input");
        writer.WriteStartArray();
        foreach (var message in request.Messages)
        {
            writer.WriteStartObject();
            writer.WriteString("role", OpenAiRole(message.Role));
            writer.WritePropertyName("content");
            writer.WriteStartArray();
            writer.WriteStartObject();
            writer.WriteString("type", "input_text");
            writer.WriteString("text", message.Content);
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        if (request.StructuredOutput is not null)
        {
            writer.WritePropertyName("text");
            writer.WriteStartObject();
            writer.WritePropertyName("format");
            writer.WriteStartObject();
            writer.WriteString("type", "json_schema");
            writer.WriteString("name", request.StructuredOutput.Name);
            writer.WriteBoolean("strict", true);
            writer.WritePropertyName("schema");
            request.StructuredOutput.JsonSchema.WriteTo(writer);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteAnthropicMessages(Utf8JsonWriter writer, ChatResolvedRoute route, ChatChannelRequest request)
    {
        if (!request.Messages.Any(static message => message.Role != ChatRole.System))
        {
            throw new ChatChannelException(ChatChannelErrorCode.RequestInvalid);
        }

        writer.WriteStartObject();
        writer.WriteString("model", route.Capability.ModelId);
        writer.WriteNumber("max_tokens", 1024);
        WriteAnthropicSystem(writer, request.Messages);
        writer.WritePropertyName("messages");
        writer.WriteStartArray();
        foreach (var message in request.Messages.Where(static message => message.Role != ChatRole.System))
        {
            writer.WriteStartObject();
            writer.WriteString("role", message.Role == ChatRole.Assistant ? "assistant" : "user");
            writer.WriteString("content", message.Content);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        if (request.StructuredOutput is not null)
        {
            writer.WritePropertyName("output_config");
            writer.WriteStartObject();
            writer.WritePropertyName("format");
            writer.WriteStartObject();
            writer.WriteString("type", "json_schema");
            writer.WritePropertyName("schema");
            request.StructuredOutput.JsonSchema.WriteTo(writer);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteAnthropicSystem(Utf8JsonWriter writer, IReadOnlyList<ChatChannelMessage> messages)
    {
        var systems = messages.Where(static message => message.Role == ChatRole.System).ToArray();
        if (systems.Length == 0)
        {
            return;
        }

        writer.WritePropertyName("system");
        writer.WriteStartArray();
        foreach (var system in systems)
        {
            writer.WriteStartObject();
            writer.WriteString("type", "text");
            writer.WriteString("text", system.Content);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteGeminiGenerateContent(Utf8JsonWriter writer, ChatChannelRequest request)
    {
        if (!request.Messages.Any(static message => message.Role != ChatRole.System))
        {
            throw new ChatChannelException(ChatChannelErrorCode.RequestInvalid);
        }

        writer.WriteStartObject();
        WriteGeminiSystem(writer, request.Messages);
        writer.WritePropertyName("contents");
        writer.WriteStartArray();
        foreach (var message in request.Messages.Where(static message => message.Role != ChatRole.System))
        {
            writer.WriteStartObject();
            writer.WriteString("role", message.Role == ChatRole.Assistant ? "model" : "user");
            writer.WritePropertyName("parts");
            writer.WriteStartArray();
            writer.WriteStartObject();
            writer.WriteString("text", message.Content);
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        if (request.StructuredOutput is not null)
        {
            writer.WritePropertyName("generationConfig");
            writer.WriteStartObject();
            writer.WriteString("responseMimeType", "application/json");
            writer.WritePropertyName("responseJsonSchema");
            request.StructuredOutput.JsonSchema.WriteTo(writer);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteGeminiSystem(Utf8JsonWriter writer, IReadOnlyList<ChatChannelMessage> messages)
    {
        var systems = messages.Where(static message => message.Role == ChatRole.System).ToArray();
        if (systems.Length == 0)
        {
            return;
        }

        writer.WritePropertyName("systemInstruction");
        writer.WriteStartObject();
        writer.WritePropertyName("parts");
        writer.WriteStartArray();
        foreach (var system in systems)
        {
            writer.WriteStartObject();
            writer.WriteString("text", system.Content);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static string ParseOpenAiChatText(JsonElement root)
    {
        var choices = RequiredArray(root, "choices");
        if (choices.GetArrayLength() == 0)
        {
            throw new ChatChannelException(ChatChannelErrorCode.ResponseMalformed);
        }

        var choice = choices[0];
        var message = RequiredObject(choice, "message");
        return RequiredString(message, "content");
    }

    private static string ParseOpenAiResponsesText(JsonElement root)
    {
        var output = RequiredArray(root, "output");
        foreach (var item in output.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("type", out var type) ||
                type.ValueKind != JsonValueKind.String ||
                !string.Equals(type.GetString(), "message", StringComparison.Ordinal))
            {
                continue;
            }

            var content = RequiredArray(item, "content");
            foreach (var block in content.EnumerateArray())
            {
                if (block.ValueKind == JsonValueKind.Object &&
                    block.TryGetProperty("type", out var blockType) &&
                    blockType.ValueKind == JsonValueKind.String &&
                    string.Equals(blockType.GetString(), "output_text", StringComparison.Ordinal))
                {
                    return RequiredString(block, "text");
                }
            }
        }

        throw new ChatChannelException(ChatChannelErrorCode.ResponseMalformed);
    }

    private static string ParseAnthropicText(JsonElement root)
    {
        var content = RequiredArray(root, "content");
        foreach (var block in content.EnumerateArray())
        {
            if (block.ValueKind == JsonValueKind.Object &&
                block.TryGetProperty("type", out var type) &&
                type.ValueKind == JsonValueKind.String &&
                string.Equals(type.GetString(), "text", StringComparison.Ordinal))
            {
                return RequiredString(block, "text");
            }
        }

        throw new ChatChannelException(ChatChannelErrorCode.ResponseMalformed);
    }

    private static string ParseGeminiText(JsonElement root)
    {
        var candidates = RequiredArray(root, "candidates");
        if (candidates.GetArrayLength() == 0)
        {
            throw new ChatChannelException(ChatChannelErrorCode.ResponseMalformed);
        }

        var content = RequiredObject(candidates[0], "content");
        var parts = RequiredArray(content, "parts");
        foreach (var part in parts.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.Object && part.TryGetProperty("text", out var text))
            {
                if (text.ValueKind != JsonValueKind.String)
                {
                    throw new ChatChannelException(ChatChannelErrorCode.ResponseMalformed);
                }

                var value = text.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        throw new ChatChannelException(ChatChannelErrorCode.ResponseMalformed);
    }

    private static ChatTokenUsage? ParseUsage(
        JsonElement root,
        string usageProperty,
        string inputProperty,
        string outputProperty,
        string? totalProperty)
    {
        if (!root.TryGetProperty(usageProperty, out var usage))
        {
            return null;
        }

        if (usage.ValueKind != JsonValueKind.Object)
        {
            throw new ChatChannelException(ChatChannelErrorCode.ResponseMalformed);
        }

        var input = OptionalToken(usage, inputProperty);
        var output = OptionalToken(usage, outputProperty);
        var total = totalProperty is null ? null : OptionalToken(usage, totalProperty);
        if (input is null && output is null && total is null)
        {
            throw new ChatChannelException(ChatChannelErrorCode.ResponseMalformed);
        }

        if (total is null && input is not null && output is not null)
        {
            total = checked(input.Value + output.Value);
        }

        return new ChatTokenUsage(input, output, total);
    }

    private static int? OptionalToken(JsonElement objectElement, string name)
    {
        if (!objectElement.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (!value.TryGetInt32(out var parsed) || parsed < 0)
        {
            throw new ChatChannelException(ChatChannelErrorCode.ResponseMalformed);
        }

        return parsed;
    }

    private static JsonElement RequiredObject(JsonElement objectElement, string name)
    {
        if (!objectElement.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            throw new ChatChannelException(ChatChannelErrorCode.ResponseMalformed);
        }

        return value;
    }

    private static JsonElement RequiredArray(JsonElement objectElement, string name)
    {
        if (!objectElement.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new ChatChannelException(ChatChannelErrorCode.ResponseMalformed);
        }

        return value;
    }

    private static string RequiredString(JsonElement objectElement, string name)
    {
        if (!objectElement.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new ChatChannelException(ChatChannelErrorCode.ResponseMalformed);
        }

        var text = value.GetString();
        if (string.IsNullOrWhiteSpace(text) || text.Length > ChatChannelLimits.MaximumResultTextCharacters || text.IndexOf('\0') >= 0)
        {
            throw new ChatChannelException(ChatChannelErrorCode.ResponseMalformed);
        }

        return text;
    }

    private static string OpenAiRole(ChatRole role) => role switch
    {
        ChatRole.System => "system",
        ChatRole.User => "user",
        ChatRole.Assistant => "assistant",
        _ => throw new ChatChannelException(ChatChannelErrorCode.RequestInvalid),
    };

    /// <summary>
    /// A synchronous writer target for <see cref="Utf8JsonWriter"/> that rejects a write before allocating beyond
    /// the channel payload limit.  The codec clears its resulting backing segment in its caller-facing finally block.
    /// </summary>
    private sealed class BoundedMemoryStream : MemoryStream
    {
        private readonly int _maximumLength;

        public BoundedMemoryStream(int maximumLength)
        {
            if (maximumLength < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumLength));
            }

            _maximumLength = maximumLength;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureCanWrite(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureCanWrite(buffer.Length);
            base.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            EnsureCanWrite(1);
            base.WriteByte(value);
        }

        private void EnsureCanWrite(int count)
        {
            if (count < 0 || Length > _maximumLength || count > _maximumLength - Length)
            {
                throw new ChatChannelException(ChatChannelErrorCode.PayloadTooLarge);
            }
        }
    }
}
