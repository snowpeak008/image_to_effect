namespace VFXComposer.AI.Contracts;

/// <summary>Bounded optional controls supported by the OpenAI-compatible image channel.</summary>
public sealed class ImageGenerationOptions
{
    public static ImageGenerationOptions Default { get; } = new();

    public ImageGenerationOptions(
        ImageRequestDimensions? dimensions = null,
        ImageGenerationQuality? quality = null,
        ImageGenerationStyle? style = null)
    {
        if (quality is not null && !Enum.IsDefined(quality.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(quality));
        }

        if (style is not null && !Enum.IsDefined(style.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(style));
        }

        Dimensions = dimensions;
        Quality = quality;
        Style = style;
    }

    /// <summary>When null, the compatible provider selects its documented default size.</summary>
    public ImageRequestDimensions? Dimensions { get; }
    public ImageGenerationQuality? Quality { get; }
    public ImageGenerationStyle? Style { get; }

    public override string ToString() => "ImageGenerationOptions(<bounded>)";
}

/// <summary>Optional quality selector; arbitrary provider strings are deliberately not accepted.</summary>
public enum ImageGenerationQuality
{
    Standard,
    Hd,
    Low,
    Medium,
    High,
    Auto,
}

/// <summary>Optional OpenAI-compatible image style selector.</summary>
public enum ImageGenerationStyle
{
    Vivid,
    Natural,
}

/// <summary>Bounded request dimensions. The value is optional through <see cref="ImageGenerationOptions"/>.</summary>
public sealed class ImageRequestDimensions
{
    public ImageRequestDimensions(int width, int height)
    {
        if (width is < 64 or > ImageArtifactLimits.MaximumDimension ||
            height is < 64 or > ImageArtifactLimits.MaximumDimension ||
            (long)width * height > ImageArtifactLimits.MaximumPixels)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Image request dimensions are invalid.");
        }

        Width = width;
        Height = height;
    }

    public int Width { get; }
    public int Height { get; }

    public override string ToString() => "ImageRequestDimensions(" +
        Width.ToString(System.Globalization.CultureInfo.InvariantCulture) + "x" +
        Height.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
}

/// <summary>
/// Typed image-channel input. It has no profile, capability, model, endpoint, credential, header, or fallback field.
/// </summary>
public sealed class ImageChannelRequest
{
    public ImageChannelRequest(string correlationId, string prompt, ImageGenerationOptions? options = null)
    {
        CorrelationId = AiContractGuard.CorrelationId(correlationId, nameof(correlationId));
        Prompt = AiContractGuard.Prompt(prompt, nameof(prompt));
        Options = options ?? ImageGenerationOptions.Default;
    }

    public string ContractVersion => AiContractVersions.ImageGenerationRequest;
    public string CorrelationId { get; }
    public string Prompt { get; }
    public ImageGenerationOptions Options { get; }

    public override string ToString() => "ImageChannelRequest(<redacted>)";
}

/// <summary>Only the locally accepted, normalized image formats may be retained as a private artifact.</summary>
public enum PrivateImageFormat
{
    Png,
    Jpeg,
    Webp,
}

/// <summary>Shared limits for untrusted provider image output.</summary>
public static class ImageArtifactLimits
{
    public const int MaximumDimension = 4096;
    public const long MaximumPixels = 16L * 1024L * 1024L;
    public const int MaximumImageBytes = 20 * 1024 * 1024;
}

/// <summary>
/// Safe private-artifact receipt. It deliberately has no URL, file path, base64, prompt, provider payload, or token.
/// </summary>
public sealed class PrivateImageArtifact
{
    public PrivateImageArtifact(
        string id,
        PrivateImageFormat format,
        int byteLength,
        int width,
        int height,
        string sha256)
    {
        Id = AiContractGuard.Identifier(id, nameof(id), maximumLength: 96);
        if (!Id.StartsWith("img-", StringComparison.Ordinal) || !Enum.IsDefined(format))
        {
            throw new ArgumentException("Private image artifact is invalid.", nameof(id));
        }

        if (byteLength is < 1 or > ImageArtifactLimits.MaximumImageBytes ||
            width is < 1 or > ImageArtifactLimits.MaximumDimension ||
            height is < 1 or > ImageArtifactLimits.MaximumDimension ||
            (long)width * height > ImageArtifactLimits.MaximumPixels ||
            !IsSha256(sha256))
        {
            throw new ArgumentException("Private image artifact is invalid.", nameof(byteLength));
        }

        Format = format;
        ByteLength = byteLength;
        Width = width;
        Height = height;
        Sha256 = sha256;
    }

    public string Id { get; }
    public PrivateImageFormat Format { get; }
    public int ByteLength { get; }
    public int Width { get; }
    public int Height { get; }
    public string Sha256 { get; }

    public string ContentType => Format switch
    {
        PrivateImageFormat.Png => "image/png",
        PrivateImageFormat.Jpeg => "image/jpeg",
        PrivateImageFormat.Webp => "image/webp",
        _ => throw new InvalidOperationException("Private image format is invalid."),
    };

    public override string ToString() => "PrivateImageArtifact(" + Id + ")";

    private static bool IsSha256(string value)
    {
        if (value is null || value.Length != 64)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>Typed result for callers that need safe image-artifact metadata in addition to the gateway receipt.</summary>
public sealed class ImageChannelResult
{
    public ImageChannelResult(string correlationId, PrivateImageArtifact artifact)
    {
        CorrelationId = AiContractGuard.CorrelationId(correlationId, nameof(correlationId));
        Artifact = artifact ?? throw new ArgumentNullException(nameof(artifact));
    }

    public string CorrelationId { get; }
    public PrivateImageArtifact Artifact { get; }

    public override string ToString() => "ImageChannelResult(" + Artifact.Id + ")";
}

/// <summary>Stable, low-detail failure categories for the image channel.</summary>
public enum ImageErrorCode
{
    EndpointInvalid,
    RequestInvalid,
    CredentialUnavailable,
    Cancelled,
    TimedOut,
    AuthenticationFailed,
    AuthorizationFailed,
    RateLimited,
    UpstreamUnavailable,
    UpstreamRejected,
    NetworkFailure,
    MalformedResponse,
    ArtifactRedirectRejected,
    ArtifactMimeNotAllowed,
    ArtifactTooLarge,
    ArtifactDimensionsInvalid,
    ArtifactPixelLimitExceeded,
    ArtifactCacheUnavailable,
}

/// <summary>Redacted image-channel exception; it intentionally has no provider exception or response payload attached.</summary>
public sealed class ImageGatewayException : Exception
{
    public ImageGatewayException(ImageErrorCode code, bool retryable = false)
        : base(ImageErrorCatalog.MessageFor(code))
    {
        if (!Enum.IsDefined(code))
        {
            throw new ArgumentOutOfRangeException(nameof(code));
        }

        Code = code;
        Retryable = retryable;
    }

    public ImageErrorCode Code { get; }
    public bool Retryable { get; }
}

/// <summary>Stable messages that deliberately omit endpoint, prompt, credential, and provider details.</summary>
public static class ImageErrorCatalog
{
    public static string MessageFor(ImageErrorCode code) => code switch
    {
        ImageErrorCode.EndpointInvalid => "The configured image endpoint cannot be used for this request.",
        ImageErrorCode.RequestInvalid => "The image request is invalid.",
        ImageErrorCode.CredentialUnavailable => "The configured image credential is unavailable.",
        ImageErrorCode.Cancelled => "The image request was cancelled.",
        ImageErrorCode.TimedOut => "The image request timed out.",
        ImageErrorCode.AuthenticationFailed => "The image provider rejected authentication.",
        ImageErrorCode.AuthorizationFailed => "The image provider rejected authorization.",
        ImageErrorCode.RateLimited => "The image provider rate limited the request.",
        ImageErrorCode.UpstreamUnavailable => "The image provider is temporarily unavailable.",
        ImageErrorCode.UpstreamRejected => "The image provider rejected the request.",
        ImageErrorCode.NetworkFailure => "The image provider request failed.",
        ImageErrorCode.MalformedResponse => "The image provider response was invalid.",
        ImageErrorCode.ArtifactRedirectRejected => "The image artifact redirect was rejected.",
        ImageErrorCode.ArtifactMimeNotAllowed => "The image artifact format is not allowed.",
        ImageErrorCode.ArtifactTooLarge => "The image artifact exceeds the allowed size.",
        ImageErrorCode.ArtifactDimensionsInvalid => "The image artifact dimensions are invalid.",
        ImageErrorCode.ArtifactPixelLimitExceeded => "The image artifact exceeds the allowed pixel limit.",
        ImageErrorCode.ArtifactCacheUnavailable => "The private image artifact cache is unavailable.",
        _ => "The image provider operation failed.",
    };
}
