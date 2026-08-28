namespace VFXComposer.AI.Contracts;

public enum ChatRole
{
    System,
    User,
    Assistant,
}

public sealed class ChatMessage
{
    public ChatMessage(ChatRole role, string content)
    {
        Role = role;
        Content = AiContractGuard.Prompt(content, nameof(content));
    }

    public ChatRole Role { get; }
    public string Content { get; }

    public override string ToString() => "ChatMessage(<redacted>)";
}

/// <summary>Versioned caller input for the ChatLlm channel only.</summary>
public sealed class ChatRequest
{
    public ChatRequest(string correlationId, IEnumerable<ChatMessage> messages)
    {
        CorrelationId = AiContractGuard.CorrelationId(correlationId, nameof(correlationId));
        Messages = AiContractGuard.CopyList(messages, nameof(messages), maximumCount: 64);
        if (Messages.Count == 0)
        {
            throw new ArgumentException("At least one chat message is required.", nameof(messages));
        }
    }

    public string ContractVersion => AiContractVersions.ChatRequest;
    public string CorrelationId { get; }
    public IReadOnlyList<ChatMessage> Messages { get; }

    public override string ToString() => "ChatRequest(<redacted>)";
}

/// <summary>Versioned caller input for the ImageGeneration channel only.</summary>
public sealed class ImageGenerationRequest
{
    public ImageGenerationRequest(string correlationId, string prompt, int width, int height)
    {
        CorrelationId = AiContractGuard.CorrelationId(correlationId, nameof(correlationId));
        Prompt = AiContractGuard.Prompt(prompt, nameof(prompt));
        if (width is < 64 or > 4096 || height is < 64 or > 4096)
        {
            throw new ArgumentOutOfRangeException("Image dimensions are invalid.");
        }

        Width = width;
        Height = height;
    }

    public string ContractVersion => AiContractVersions.ImageGenerationRequest;
    public string CorrelationId { get; }
    public string Prompt { get; }
    public int Width { get; }
    public int Height { get; }

    public override string ToString() => "ImageGenerationRequest(<redacted>)";
}

public sealed class ChatResponse
{
    public ChatResponse(string correlationId, string text)
    {
        CorrelationId = AiContractGuard.CorrelationId(correlationId, nameof(correlationId));
        Text = AiContractGuard.Prompt(text, nameof(text));
    }

    public string CorrelationId { get; }
    public string Text { get; }

    public override string ToString() => "ChatResponse(<redacted>)";
}

/// <summary>No URL, bytes, base64 payload, or provider metadata is exposed by this contract.</summary>
public sealed class ImageGenerationResponse
{
    public ImageGenerationResponse(string correlationId, string privateArtifactId)
    {
        CorrelationId = AiContractGuard.CorrelationId(correlationId, nameof(correlationId));
        PrivateArtifactId = AiContractGuard.Identifier(privateArtifactId, nameof(privateArtifactId), maximumLength: 96);
    }

    public string CorrelationId { get; }
    public string PrivateArtifactId { get; }

    public override string ToString() => "ImageGenerationResponse(" + PrivateArtifactId + ")";
}

public interface IChatGateway
{
    ValueTask<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default);
}

public interface IImageGateway
{
    ValueTask<ImageGenerationResponse> GenerateImageAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The sole feature-facing AI gateway. Callers provide neither profile, endpoint, protocol, model nor fallback route.
/// </summary>
public interface IAiGateway : IChatGateway, IImageGateway
{
}

/// <summary>Stable, deliberately low-detail diagnostics that cannot carry provider or request content.</summary>
public sealed class AiDiagnostic
{
    public AiDiagnostic(AiErrorCode code, bool retryable)
    {
        Code = code;
        Retryable = retryable;
    }

    public AiErrorCode Code { get; }
    public bool Retryable { get; }

    public override string ToString() => "AiDiagnostic(" + Code + ")";
}

public sealed class AiGatewayException : Exception
{
    public AiGatewayException(AiErrorCode code, bool retryable = false)
        : base(AiErrorCatalog.MessageFor(code))
    {
        Code = code;
        Retryable = retryable;
    }

    public AiErrorCode Code { get; }
    public bool Retryable { get; }
}

public static class AiErrorCatalog
{
    public static string MessageFor(AiErrorCode code) => code switch
    {
        AiErrorCode.ConfigurationUnavailable => "AI provider configuration is unavailable.",
        AiErrorCode.ConfigurationInvalid => "AI provider configuration is invalid.",
        AiErrorCode.ChannelUnbound => "The requested AI channel is not explicitly bound.",
        AiErrorCode.ProfileDisabled => "The selected provider profile is disabled.",
        AiErrorCode.CapabilityMismatch => "The configured capability does not match the requested channel.",
        AiErrorCode.ProtocolNotAllowed => "The configured provider protocol is not allowed.",
        AiErrorCode.EndpointRejected => "The configured provider endpoint is not allowed.",
        AiErrorCode.SecretUnavailable => "The configured credential reference is unavailable.",
        AiErrorCode.HealthUnverified => "The configured provider route is not verified.",
        AiErrorCode.HealthStale => "The configured provider health result is stale.",
        AiErrorCode.AdapterUnavailable => "No provider adapter is available for the configured route.",
        AiErrorCode.ImportRejected => "The provider draft import was rejected.",
        AiErrorCode.ImportConfirmationRequired => "The relay import requires explicit confirmation.",
        AiErrorCode.RequestInvalid => "The AI request is invalid.",
        _ => "AI provider operation failed.",
    };
}
