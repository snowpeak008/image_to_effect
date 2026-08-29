using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;

namespace VFXComposer.AI.Contracts.Chat;

/// <summary>
/// The explicit protocol identifiers that the ChatLlm channel can send.  A value is selected only by the
/// already-resolved provider profile; callers cannot select one per request.
/// </summary>
public static class ChatProtocolIds
{
    public const string OpenAiChatCompletionsV1 = "openai-chat-completions-v1";
    public const string OpenAiResponsesV1 = "openai-responses-v1";
    public const string AnthropicMessagesV1 = "anthropic-messages-v1";
    public const string GeminiGenerateContentV1 = "gemini-generate-content-v1";

    /// <summary>The existing A1 provider identifier, interpreted here as the Chat Completions wire shape.</summary>
    public const string OpenAiCompatibleV1 = ProviderProtocols.OpenAiCompatibleV1;
}

/// <summary>Bounds applied before a chat request or provider response can be retained in memory.</summary>
public static class ChatChannelLimits
{
    public const int MaximumMessages = 64;
    public const int MaximumRequestBytes = 256 * 1024;
    public const int MaximumResponseBytes = 1024 * 1024;
    public const int MaximumStructuredOutputSchemaBytes = 32 * 1024;
    public const int MaximumResultTextCharacters = 128 * 1024;
}

/// <summary>One channel-local message.  Its textual content is never included in diagnostics or formatting.</summary>
public sealed class ChatChannelMessage
{
    public ChatChannelMessage(ChatRole role, string content)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        Role = role;
        Content = AiContractGuard.Prompt(content, nameof(content));
    }

    public ChatRole Role { get; }
    public string Content { get; }

    public override string ToString() => "ChatChannelMessage(<redacted>)";
}

/// <summary>
/// An optional JSON Schema response request.  It carries no provider-specific routing or header choice.
/// </summary>
public sealed class ChatStructuredOutput
{
    public ChatStructuredOutput(string name, JsonElement jsonSchema)
    {
        Name = AiContractGuard.Identifier(name, nameof(name));
        if (jsonSchema.ValueKind is JsonValueKind.Undefined)
        {
            throw new ArgumentException("A JSON Schema value is required.", nameof(jsonSchema));
        }

        byte[]? bytes = null;
        try
        {
            bytes = JsonSerializer.SerializeToUtf8Bytes(jsonSchema);
            if (bytes.Length is < 1 or > ChatChannelLimits.MaximumStructuredOutputSchemaBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(jsonSchema));
            }
        }
        finally
        {
            if (bytes is not null)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
            }
        }

        JsonSchema = jsonSchema.Clone();
    }

    public string Name { get; }
    public JsonElement JsonSchema { get; }

    public override string ToString() => "ChatStructuredOutput(<redacted>)";
}

/// <summary>
/// Typed ChatLlm request.  The profile, capability, endpoint, protocol, model, and credential are deliberately
/// absent: all are pinned by the one persisted ChatLlm binding before this request is sent.
/// </summary>
public sealed class ChatChannelRequest
{
    public ChatChannelRequest(
        string correlationId,
        IEnumerable<ChatChannelMessage> messages,
        ChatStructuredOutput? structuredOutput = null)
    {
        CorrelationId = AiContractGuard.CorrelationId(correlationId, nameof(correlationId));
        Messages = CopyMessages(messages);
        if (Messages.Count == 0)
        {
            throw new ArgumentException("At least one chat message is required.", nameof(messages));
        }

        StructuredOutput = structuredOutput;
    }

    public string CorrelationId { get; }
    public IReadOnlyList<ChatChannelMessage> Messages { get; }
    public ChatStructuredOutput? StructuredOutput { get; }

    public override string ToString() => "ChatChannelRequest(<redacted>)";

    private static IReadOnlyList<ChatChannelMessage> CopyMessages(IEnumerable<ChatChannelMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var copied = messages.ToArray();
        if (copied.Length > ChatChannelLimits.MaximumMessages || copied.Any(static message => message is null))
        {
            throw new ArgumentException("Chat messages are invalid.", nameof(messages));
        }

        return new ReadOnlyCollection<ChatChannelMessage>(copied);
    }
}

/// <summary>Provider-reported token counts.  A provider may omit any count, but present values are non-negative.</summary>
public sealed class ChatTokenUsage
{
    public ChatTokenUsage(int? inputTokens, int? outputTokens, int? totalTokens)
    {
        Validate(inputTokens, nameof(inputTokens));
        Validate(outputTokens, nameof(outputTokens));
        Validate(totalTokens, nameof(totalTokens));
        if (inputTokens is null && outputTokens is null && totalTokens is null)
        {
            throw new ArgumentException("At least one token count is required.");
        }

        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        TotalTokens = totalTokens;
    }

    public int? InputTokens { get; }
    public int? OutputTokens { get; }
    public int? TotalTokens { get; }

    public override string ToString() => "ChatTokenUsage(<redacted>)";

    private static void Validate(int? value, string parameterName)
    {
        if (value is < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}

/// <summary>Typed result from the exact bound ChatLlm route.</summary>
public sealed class ChatChannelResult
{
    public ChatChannelResult(
        string correlationId,
        string text,
        ChatTokenUsage? tokenUsage = null,
        JsonElement? structuredOutput = null)
    {
        CorrelationId = AiContractGuard.CorrelationId(correlationId, nameof(correlationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(text, nameof(text));
        if (text.Length > ChatChannelLimits.MaximumResultTextCharacters || text.IndexOf('\0') >= 0)
        {
            throw new ArgumentException("Chat result text is invalid.", nameof(text));
        }

        Text = text;
        TokenUsage = tokenUsage;
        StructuredOutput = structuredOutput?.Clone();
    }

    public string CorrelationId { get; }
    public string Text { get; }
    public ChatTokenUsage? TokenUsage { get; }
    public JsonElement? StructuredOutput { get; }

    public override string ToString() => "ChatChannelResult(<redacted>)";
}

/// <summary>The channel-local gateway used by A4 to compose the sole feature-facing IAiGateway.</summary>
public interface IChatChannelGateway : IChatGateway
{
    ValueTask<ChatChannelResult> CompleteAsync(
        ChatChannelRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Stable, low-detail outcomes for all request-time ChatLlm failures.</summary>
public enum ChatChannelErrorCode
{
    ConfigurationUnavailable,
    ConfigurationInvalid,
    ChannelUnbound,
    ProfileDisabled,
    CapabilityMismatch,
    ProtocolUnsupported,
    SecretUnavailable,
    HealthUnverified,
    HealthStale,
    EndpointUnusable,
    RequestInvalid,
    PayloadTooLarge,
    Cancelled,
    TimedOut,
    AuthenticationFailed,
    RateLimited,
    UpstreamUnavailable,
    UpstreamRejected,
    TransportFailed,
    ResponseMalformed,
    ResponseTooLarge,
}

/// <summary>
/// Deliberately redacted channel failure.  It never carries an endpoint, secret, authorization value, prompt,
/// request body, response body, or inner exception.
/// </summary>
public sealed class ChatChannelException : Exception
{
    public ChatChannelException(ChatChannelErrorCode code, bool retryable = false)
        : base(ChatChannelErrorCatalog.MessageFor(code))
    {
        Code = code;
        Retryable = retryable;
    }

    public ChatChannelErrorCode Code { get; }
    public bool Retryable { get; }

    public override string ToString() => "ChatChannelException(" + Code + ")";
}

public static class ChatChannelErrorCatalog
{
    public static string MessageFor(ChatChannelErrorCode code) => code switch
    {
        ChatChannelErrorCode.ConfigurationUnavailable => "AI provider configuration is unavailable.",
        ChatChannelErrorCode.ConfigurationInvalid => "AI provider configuration is invalid.",
        ChatChannelErrorCode.ChannelUnbound => "The requested AI channel is not explicitly bound.",
        ChatChannelErrorCode.ProfileDisabled => "The selected provider profile is disabled.",
        ChatChannelErrorCode.CapabilityMismatch => "The configured capability does not match the requested channel.",
        ChatChannelErrorCode.ProtocolUnsupported => "The configured chat protocol is not supported.",
        ChatChannelErrorCode.SecretUnavailable => "The configured credential reference is unavailable.",
        ChatChannelErrorCode.HealthUnverified => "The configured provider route is not verified.",
        ChatChannelErrorCode.HealthStale => "The configured provider health result is stale.",
        ChatChannelErrorCode.EndpointUnusable => "The configured endpoint cannot be used for this request.",
        ChatChannelErrorCode.RequestInvalid => "The chat request is invalid.",
        ChatChannelErrorCode.PayloadTooLarge => "The chat request exceeds the configured size limit.",
        ChatChannelErrorCode.Cancelled => "The chat request was cancelled.",
        ChatChannelErrorCode.TimedOut => "The chat request timed out.",
        ChatChannelErrorCode.AuthenticationFailed => "The provider rejected the configured credential.",
        ChatChannelErrorCode.RateLimited => "The provider rate limited the request.",
        ChatChannelErrorCode.UpstreamUnavailable => "The provider is temporarily unavailable.",
        ChatChannelErrorCode.UpstreamRejected => "The provider rejected the request.",
        ChatChannelErrorCode.TransportFailed => "The provider request could not be completed.",
        ChatChannelErrorCode.ResponseMalformed => "The provider response was malformed.",
        ChatChannelErrorCode.ResponseTooLarge => "The provider response exceeds the configured size limit.",
        _ => "The chat provider operation failed.",
    };
}
