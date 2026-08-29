using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VFXComposer.AI.Contracts;

namespace VFXComposer.AI.Providers.Image;

/// <summary>
/// OpenAI Images / OpenAI-compatible adapter for the one already-resolved ImageGeneration route. The configured
/// <see cref="OpaqueEndpoint"/> is interpreted as one complete request URL only at call time; it is never rewritten,
/// persisted, logged, or used to discover a second route.
/// </summary>
public sealed class OpenAiCompatibleImageGateway : IImageGateway, IPrivateImageArtifactStore, IDisposable
{
    private const int MaximumApiResponseBytes = 28 * 1024 * 1024;
    private const int MaximumCredentialBytes = 16 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private readonly ResolvedProviderRoute _route;
    private readonly IImageCredentialSource _credentials;
    private readonly PrivateImageArtifactCache _cache;
    private readonly HttpClient _apiClient;
    private readonly HttpClient _artifactClient;
    private readonly bool _ownsCache;
    private int _disposed;

    /// <summary>
    /// Test-only transport seam. The scoped AI test assembly can inject deterministic mock
    /// <see cref="HttpMessageHandler"/> instances; production callers must use <see cref="Create"/>, which owns
    /// separate non-redirecting handlers for generation and artifact download.
    /// </summary>
    internal OpenAiCompatibleImageGateway(
        ResolvedProviderRoute route,
        IImageCredentialSource credentials,
        PrivateImageArtifactCache cache,
        HttpMessageHandler apiHandler,
        HttpMessageHandler artifactHandler)
        : this(route, credentials, cache, apiHandler, artifactHandler, disposeHandlers: false, ownsCache: false)
    {
    }

    /// <summary>Creates a production adapter with separate non-redirecting HTTP handlers for API and artifact fetches.</summary>
    public static OpenAiCompatibleImageGateway Create(
        ResolvedProviderRoute route,
        ProviderSecretStore secretStore,
        string? privateTempRoot = null)
    {
        ArgumentNullException.ThrowIfNull(secretStore);
        var cache = new PrivateImageArtifactCache(privateTempRoot);
        try
        {
            return new OpenAiCompatibleImageGateway(
                route,
                new ProviderSecretStoreImageCredentialSource(secretStore),
                cache,
                CreateNonRedirectingHandler(),
                CreateNonRedirectingHandler(),
                disposeHandlers: true,
                ownsCache: true);
        }
        catch
        {
            cache.Dispose();
            throw;
        }
    }

    private OpenAiCompatibleImageGateway(
        ResolvedProviderRoute route,
        IImageCredentialSource credentials,
        PrivateImageArtifactCache cache,
        HttpMessageHandler apiHandler,
        HttpMessageHandler artifactHandler,
        bool disposeHandlers,
        bool ownsCache)
    {
        ValidateRoute(route);
        _route = route;
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        ArgumentNullException.ThrowIfNull(apiHandler);
        ArgumentNullException.ThrowIfNull(artifactHandler);
        _apiClient = new HttpClient(apiHandler, disposeHandlers) { Timeout = Timeout.InfiniteTimeSpan };
        _artifactClient = new HttpClient(artifactHandler, disposeHandlers) { Timeout = Timeout.InfiniteTimeSpan };
        _ownsCache = ownsCache;
    }

    /// <summary>Uses the pre-existing gateway DTO and makes its required dimensions explicit for the compatible API.</summary>
    public async ValueTask<ImageGenerationResponse> GenerateImageAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ImageChannelRequest typedRequest;
        try
        {
            typedRequest = new ImageChannelRequest(
                request.CorrelationId,
                request.Prompt,
                new ImageGenerationOptions(new ImageRequestDimensions(request.Width, request.Height)));
        }
        catch (ArgumentException)
        {
            throw new ImageGatewayException(ImageErrorCode.RequestInvalid);
        }

        var result = await GenerateImageAsync(typedRequest, cancellationToken).ConfigureAwait(false);
        return new ImageGenerationResponse(result.CorrelationId, result.Artifact.Id);
    }

    /// <summary>Uses bounded optional size, quality, and style controls without exposing routing controls to callers.</summary>
    public async ValueTask<ImageChannelResult> GenerateImageAsync(
        ImageChannelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfDisposed();
        if (cancellationToken.IsCancellationRequested)
        {
            throw new ImageGatewayException(ImageErrorCode.Cancelled);
        }

        Uri endpoint;
        try
        {
            endpoint = ParseHttpEndpoint(_route.Profile.Endpoint);
        }
        catch (ImageGatewayException)
        {
            throw;
        }

        using var timeout = new CancellationTokenSource();
        timeout.CancelAfter(TimeSpan.FromSeconds(_route.Profile.TimeoutSeconds));
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        byte[]? imageBytes = null;
        try
        {
            var providerPayload = await _credentials.UseCredentialAsync(
                _route.Profile.Id,
                _route.Profile.Auth.SecretRef,
                (credential, token) => SendGenerationAsync(endpoint, request, credential, token),
                linkedCancellation.Token).ConfigureAwait(false);

            DownloadedImage downloaded;
            if (providerPayload.Base64Json is not null)
            {
                imageBytes = DecodeStrictBase64(providerPayload.Base64Json);
                downloaded = new DownloadedImage(imageBytes, ExpectedFormat: null);
            }
            else
            {
                downloaded = await DownloadArtifactAsync(providerPayload.Url!, linkedCancellation.Token).ConfigureAwait(false);
                imageBytes = downloaded.Bytes;
            }

            var inspected = InspectImage(imageBytes);
            if (downloaded.ExpectedFormat is not null && downloaded.ExpectedFormat.Value != inspected.Format)
            {
                throw new ImageGatewayException(ImageErrorCode.ArtifactMimeNotAllowed);
            }

            var artifact = await _cache.StoreAsync(
                imageBytes,
                inspected.Format,
                inspected.Width,
                inspected.Height,
                linkedCancellation.Token).ConfigureAwait(false);
            return new ImageChannelResult(request.CorrelationId, artifact);
        }
        catch (ImageGatewayException)
        {
            throw;
        }
        catch (AiGatewayException)
        {
            throw new ImageGatewayException(ImageErrorCode.CredentialUnavailable);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new ImageGatewayException(ImageErrorCode.Cancelled);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new ImageGatewayException(ImageErrorCode.TimedOut, retryable: true);
        }
        catch (OperationCanceledException)
        {
            throw new ImageGatewayException(ImageErrorCode.Cancelled);
        }
        catch (HttpRequestException)
        {
            throw new ImageGatewayException(ImageErrorCode.NetworkFailure, retryable: true);
        }
        catch (DecoderFallbackException)
        {
            throw new ImageGatewayException(ImageErrorCode.CredentialUnavailable);
        }
        catch (JsonException)
        {
            throw new ImageGatewayException(ImageErrorCode.MalformedResponse);
        }
        catch (FormatException)
        {
            throw new ImageGatewayException(ImageErrorCode.MalformedResponse);
        }
        catch (IOException)
        {
            throw new ImageGatewayException(ImageErrorCode.ArtifactCacheUnavailable);
        }
        catch (UnauthorizedAccessException)
        {
            throw new ImageGatewayException(ImageErrorCode.ArtifactCacheUnavailable);
        }
        catch (Exception)
        {
            // Provider handlers can throw arbitrary exceptions. Do not preserve their messages, requests, or response data.
            throw new ImageGatewayException(ImageErrorCode.NetworkFailure, retryable: true);
        }
        finally
        {
            if (imageBytes is not null)
            {
                CryptographicOperations.ZeroMemory(imageBytes);
            }
        }
    }

    public PrivateImageArtifact GetArtifact(string artifactId) => _cache.GetArtifact(artifactId);

    public ValueTask<Stream> OpenReadAsync(string artifactId, CancellationToken cancellationToken = default) =>
        _cache.OpenReadAsync(artifactId, cancellationToken);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _apiClient.Dispose();
        _artifactClient.Dispose();
        if (_ownsCache)
        {
            _cache.Dispose();
        }
    }

    public override string ToString() => "OpenAiCompatibleImageGateway(ImageGeneration,<redacted>)";

    private async ValueTask<ProviderImagePayload> SendGenerationAsync(
        Uri endpoint,
        ImageChannelRequest request,
        ReadOnlyMemory<byte> credential,
        CancellationToken cancellationToken)
    {
        byte[]? requestBytes = null;
        try
        {
            requestBytes = SerializeRequestForRoute(request);
            using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new ByteArrayContent(requestBytes),
            };
            message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            var bearer = CreateBearerToken(credential);
            if (!message.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearer))
            {
                throw new ImageGatewayException(ImageErrorCode.CredentialUnavailable);
            }

            using var response = await _apiClient.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            ThrowForProviderStatus(response.StatusCode);
            var responseBytes = await ReadBoundedAsync(
                response.Content,
                MaximumApiResponseBytes,
                ImageErrorCode.MalformedResponse,
                cancellationToken).ConfigureAwait(false);
            try
            {
                return ParseProviderPayload(responseBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(responseBytes);
            }
        }
        finally
        {
            if (requestBytes is not null)
            {
                CryptographicOperations.ZeroMemory(requestBytes);
            }
        }
    }

    private async ValueTask<DownloadedImage> DownloadArtifactAsync(string url, CancellationToken cancellationToken)
    {
        var artifactUri = ParseHttpUrl(url, ImageErrorCode.MalformedResponse);
        using var request = new HttpRequestMessage(HttpMethod.Get, artifactUri);
        // This is deliberately a different client and a fresh request: it never receives API Authorization or headers.
        using var response = await _artifactClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        if ((int)response.StatusCode is >= 300 and <= 399)
        {
            throw new ImageGatewayException(ImageErrorCode.ArtifactRedirectRejected);
        }

        ThrowForProviderStatus(response.StatusCode);

        var expectedFormat = FormatForContentType(response.Content.Headers.ContentType?.MediaType);
        if (response.Content.Headers.ContentLength is long contentLength &&
            (contentLength < 1 || contentLength > ImageArtifactLimits.MaximumImageBytes))
        {
            throw new ImageGatewayException(ImageErrorCode.ArtifactTooLarge);
        }

        var bytes = await ReadBoundedAsync(
            response.Content,
            ImageArtifactLimits.MaximumImageBytes,
            ImageErrorCode.ArtifactTooLarge,
            cancellationToken).ConfigureAwait(false);
        return new DownloadedImage(bytes, expectedFormat);
    }

    private static HttpMessageHandler CreateNonRedirectingHandler() => new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
    };

    private static void ValidateRoute(ResolvedProviderRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (route.Channel != AiChannel.ImageGeneration ||
            route.Binding.Channel != AiChannel.ImageGeneration ||
            route.Capability.Channel != AiChannel.ImageGeneration ||
            !string.Equals(route.Binding.ProfileId, route.Profile.Id, StringComparison.Ordinal) ||
            !string.Equals(route.Binding.CapabilityId, route.Capability.Id, StringComparison.Ordinal) ||
            !string.Equals(route.Binding.ModelId, route.Capability.ModelId, StringComparison.Ordinal))
        {
            throw new AiGatewayException(AiErrorCode.CapabilityMismatch);
        }

        if (!string.Equals(route.Profile.Protocol.ProtocolId, ProviderProtocols.OpenAiCompatibleV1, StringComparison.Ordinal))
        {
            throw new AiGatewayException(AiErrorCode.ProtocolNotAllowed);
        }
    }

    private static Uri ParseHttpEndpoint(OpaqueEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return ParseHttpUrl(endpoint.Value, ImageErrorCode.EndpointInvalid);
    }

    private static Uri ParseHttpUrl(string value, ImageErrorCode failureCode)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri is null ||
            string.IsNullOrEmpty(uri.Host) ||
            (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
             !uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ImageGatewayException(failureCode);
        }

        return uri;
    }

    private byte[] SerializeRequestForRoute(ImageChannelRequest request)
    {
        using var buffer = new MemoryStream();
        try
        {
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteString("model", _route.Capability.ModelId);
                writer.WriteString("prompt", request.Prompt);
                if (request.Options.Dimensions is { } dimensions)
                {
                    writer.WriteString(
                        "size",
                        dimensions.Width.ToString(System.Globalization.CultureInfo.InvariantCulture) + "x" +
                        dimensions.Height.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }

                writer.WriteNumber("n", 1);
                writer.WriteString("response_format", "b64_json");
                if (request.Options.Quality is { } quality)
                {
                    writer.WriteString("quality", QualityText(quality));
                }

                if (request.Options.Style is { } style)
                {
                    writer.WriteString("style", StyleText(style));
                }

                writer.WriteEndObject();
            }

            return buffer.ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)));
        }
    }

    private static string QualityText(ImageGenerationQuality quality) => quality switch
    {
        ImageGenerationQuality.Standard => "standard",
        ImageGenerationQuality.Hd => "hd",
        ImageGenerationQuality.Low => "low",
        ImageGenerationQuality.Medium => "medium",
        ImageGenerationQuality.High => "high",
        ImageGenerationQuality.Auto => "auto",
        _ => throw new ImageGatewayException(ImageErrorCode.RequestInvalid),
    };

    private static string StyleText(ImageGenerationStyle style) => style switch
    {
        ImageGenerationStyle.Vivid => "vivid",
        ImageGenerationStyle.Natural => "natural",
        _ => throw new ImageGatewayException(ImageErrorCode.RequestInvalid),
    };

    private static string CreateBearerToken(ReadOnlyMemory<byte> credential)
    {
        if (credential.Length is < 1 or > MaximumCredentialBytes)
        {
            throw new ImageGatewayException(ImageErrorCode.CredentialUnavailable);
        }

        var token = StrictUtf8.GetString(credential.Span);
        if (token.IndexOf('\0') >= 0 || token.Any(char.IsControl))
        {
            throw new ImageGatewayException(ImageErrorCode.CredentialUnavailable);
        }

        return token;
    }

    private static void ThrowForProviderStatus(HttpStatusCode statusCode)
    {
        switch ((int)statusCode)
        {
            case 401:
                throw new ImageGatewayException(ImageErrorCode.AuthenticationFailed);
            case 403:
                throw new ImageGatewayException(ImageErrorCode.AuthorizationFailed);
            case 429:
                throw new ImageGatewayException(ImageErrorCode.RateLimited, retryable: true);
            default:
                if ((int)statusCode >= 500)
                {
                    throw new ImageGatewayException(ImageErrorCode.UpstreamUnavailable, retryable: true);
                }

                if ((int)statusCode is < 200 or > 299)
                {
                    throw new ImageGatewayException(ImageErrorCode.UpstreamRejected);
                }

                return;
        }
    }

    private static async ValueTask<byte[]> ReadBoundedAsync(
        HttpContent content,
        int maximumBytes,
        ImageErrorCode oversizedCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Headers.ContentLength is long contentLength && (contentLength < 1 || contentLength > maximumBytes))
        {
            throw new ImageGatewayException(oversizedCode);
        }

        await using var input = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output = new MemoryStream();
        var rented = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            var total = 0;
            while (true)
            {
                var read = await input.ReadAsync(rented.AsMemory(0, rented.Length), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                if (read > maximumBytes - total)
                {
                    throw new ImageGatewayException(oversizedCode);
                }

                await output.WriteAsync(rented.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                total += read;
            }

            if (total == 0)
            {
                throw new ImageGatewayException(ImageErrorCode.MalformedResponse);
            }

            var result = output.ToArray();
            CryptographicOperations.ZeroMemory(output.GetBuffer().AsSpan(0, checked((int)output.Length)));
            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }

    private static ProviderImagePayload ParseProviderPayload(byte[] responseBytes)
    {
        using var document = JsonDocument.Parse(responseBytes);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array ||
            data.GetArrayLength() != 1)
        {
            throw new ImageGatewayException(ImageErrorCode.MalformedResponse);
        }

        var entry = data[0];
        if (entry.ValueKind != JsonValueKind.Object)
        {
            throw new ImageGatewayException(ImageErrorCode.MalformedResponse);
        }

        var base64Json = OptionalString(entry, "b64_json");
        var url = OptionalString(entry, "url");
        if ((base64Json is null && url is null) || (base64Json is not null && url is not null))
        {
            throw new ImageGatewayException(ImageErrorCode.MalformedResponse);
        }

        return new ProviderImagePayload(base64Json, url);
    }

    private static string? OptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw new ImageGatewayException(ImageErrorCode.MalformedResponse);
        }

        var value = property.GetString();
        if (string.IsNullOrEmpty(value))
        {
            throw new ImageGatewayException(ImageErrorCode.MalformedResponse);
        }

        return value;
    }

    private static byte[] DecodeStrictBase64(string base64)
    {
        ArgumentNullException.ThrowIfNull(base64);
        var maximumCharacters = checked(((ImageArtifactLimits.MaximumImageBytes + 2) / 3) * 4);
        if (base64.Length < 4 || base64.Length > maximumCharacters || base64.Length % 4 != 0)
        {
            throw new ImageGatewayException(ImageErrorCode.ArtifactTooLarge);
        }

        var paddingIndex = base64.IndexOf('=');
        var paddingCount = 0;
        for (var index = 0; index < base64.Length; index++)
        {
            var character = base64[index];
            var isAlphabet = character is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '+' or '/';
            if (character == '=')
            {
                if (paddingIndex < 0 || index < paddingIndex)
                {
                    throw new ImageGatewayException(ImageErrorCode.MalformedResponse);
                }

                paddingCount++;
                continue;
            }

            if (!isAlphabet || paddingIndex >= 0 && index > paddingIndex)
            {
                throw new ImageGatewayException(ImageErrorCode.MalformedResponse);
            }
        }

        if (paddingCount > 2 || (paddingCount > 0 && paddingIndex != base64.Length - paddingCount))
        {
            throw new ImageGatewayException(ImageErrorCode.MalformedResponse);
        }

        var expectedLength = checked((base64.Length / 4 * 3) - paddingCount);
        if (expectedLength is < 1 or > ImageArtifactLimits.MaximumImageBytes)
        {
            throw new ImageGatewayException(ImageErrorCode.ArtifactTooLarge);
        }

        var bytes = new byte[expectedLength];
        if (!Convert.TryFromBase64String(base64, bytes, out var written) || written != expectedLength)
        {
            CryptographicOperations.ZeroMemory(bytes);
            throw new ImageGatewayException(ImageErrorCode.MalformedResponse);
        }

        return bytes;
    }

    private static PrivateImageFormat FormatForContentType(string? contentType)
    {
        if (string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase))
        {
            return PrivateImageFormat.Png;
        }

        if (string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return PrivateImageFormat.Jpeg;
        }

        if (string.Equals(contentType, "image/webp", StringComparison.OrdinalIgnoreCase))
        {
            return PrivateImageFormat.Webp;
        }

        throw new ImageGatewayException(ImageErrorCode.ArtifactMimeNotAllowed);
    }

    private static ImageInspection InspectImage(ReadOnlySpan<byte> imageBytes)
    {
        if (imageBytes.Length < 10)
        {
            throw new ImageGatewayException(ImageErrorCode.ArtifactDimensionsInvalid);
        }

        ImageInspection inspected;
        if (imageBytes.Length >= 24 &&
            imageBytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            inspected = InspectPng(imageBytes);
        }
        else if (imageBytes[0] == 0xff && imageBytes[1] == 0xd8)
        {
            inspected = InspectJpeg(imageBytes);
        }
        else if (imageBytes.Length >= 12 &&
                 imageBytes[..4].SequenceEqual("RIFF"u8) &&
                 imageBytes.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            inspected = InspectWebp(imageBytes);
        }
        else
        {
            throw new ImageGatewayException(ImageErrorCode.ArtifactMimeNotAllowed);
        }

        ValidateImageDimensions(inspected.Width, inspected.Height);
        return inspected;
    }

    private static ImageInspection InspectPng(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 24 ||
            !bytes.Slice(12, 4).SequenceEqual("IHDR"u8) ||
            ReadBigEndianUInt32(bytes.Slice(8, 4)) != 13)
        {
            throw new ImageGatewayException(ImageErrorCode.ArtifactDimensionsInvalid);
        }

        return new ImageInspection(
            PrivateImageFormat.Png,
            ReadBigEndianUInt32(bytes.Slice(16, 4)),
            ReadBigEndianUInt32(bytes.Slice(20, 4)));
    }

    private static ImageInspection InspectJpeg(ReadOnlySpan<byte> bytes)
    {
        var index = 2;
        while (index < bytes.Length)
        {
            if (bytes[index++] != 0xff)
            {
                throw new ImageGatewayException(ImageErrorCode.ArtifactDimensionsInvalid);
            }

            while (index < bytes.Length && bytes[index] == 0xff)
            {
                index++;
            }

            if (index >= bytes.Length)
            {
                break;
            }

            var marker = bytes[index++];
            if (marker is 0xd8 or 0xd9 or 0x01 || marker is >= 0xd0 and <= 0xd7)
            {
                continue;
            }

            if (index + 2 > bytes.Length)
            {
                break;
            }

            var length = (bytes[index] << 8) | bytes[index + 1];
            if (length < 2 || length > bytes.Length - index)
            {
                break;
            }

            if (IsStartOfFrame(marker))
            {
                if (length < 8)
                {
                    break;
                }

                var height = (bytes[index + 3] << 8) | bytes[index + 4];
                var width = (bytes[index + 5] << 8) | bytes[index + 6];
                return new ImageInspection(PrivateImageFormat.Jpeg, width, height);
            }

            index += length;
        }

        throw new ImageGatewayException(ImageErrorCode.ArtifactDimensionsInvalid);
    }

    private static bool IsStartOfFrame(byte marker) =>
        marker is >= 0xc0 and <= 0xc3 ||
        marker is >= 0xc5 and <= 0xc7 ||
        marker is >= 0xc9 and <= 0xcb ||
        marker is >= 0xcd and <= 0xcf;

    private static ImageInspection InspectWebp(ReadOnlySpan<byte> bytes)
    {
        var offset = 12;
        while (offset <= bytes.Length - 8)
        {
            var chunk = bytes.Slice(offset, 4);
            var chunkLength = ReadLittleEndianUInt32(bytes.Slice(offset + 4, 4));
            var dataOffset = offset + 8;
            if (chunkLength > bytes.Length - dataOffset)
            {
                break;
            }

            var length = checked((int)chunkLength);
            var data = bytes.Slice(dataOffset, length);
            if (chunk.SequenceEqual("VP8 "u8))
            {
                if (data.Length < 10 || !data.Slice(3, 3).SequenceEqual(new byte[] { 0x9d, 0x01, 0x2a }))
                {
                    break;
                }

                var width = ReadLittleEndianUInt16(data.Slice(6, 2)) & 0x3fff;
                var height = ReadLittleEndianUInt16(data.Slice(8, 2)) & 0x3fff;
                return new ImageInspection(PrivateImageFormat.Webp, width, height);
            }

            if (chunk.SequenceEqual("VP8L"u8))
            {
                if (data.Length < 5 || data[0] != 0x2f)
                {
                    break;
                }

                var packed = ReadLittleEndianUInt32(data.Slice(1, 4));
                var width = checked((int)(1 + (packed & 0x3fff)));
                var height = checked((int)(1 + ((packed >> 14) & 0x3fff)));
                return new ImageInspection(PrivateImageFormat.Webp, width, height);
            }

            if (chunk.SequenceEqual("VP8X"u8))
            {
                if (data.Length < 10)
                {
                    break;
                }

                var width = checked(1 + ReadLittleEndianUInt24(data.Slice(4, 3)));
                var height = checked(1 + ReadLittleEndianUInt24(data.Slice(7, 3)));
                return new ImageInspection(PrivateImageFormat.Webp, width, height);
            }

            var paddedLength = checked(length + (length & 1));
            offset = checked(dataOffset + paddedLength);
        }

        throw new ImageGatewayException(ImageErrorCode.ArtifactDimensionsInvalid);
    }

    private static void ValidateImageDimensions(int width, int height)
    {
        if (width < 1 || height < 1)
        {
            throw new ImageGatewayException(ImageErrorCode.ArtifactDimensionsInvalid);
        }

        if ((long)width * height > ImageArtifactLimits.MaximumPixels)
        {
            throw new ImageGatewayException(ImageErrorCode.ArtifactPixelLimitExceeded);
        }

        if (width > ImageArtifactLimits.MaximumDimension || height > ImageArtifactLimits.MaximumDimension)
        {
            throw new ImageGatewayException(ImageErrorCode.ArtifactDimensionsInvalid);
        }
    }

    private static int ReadBigEndianUInt32(ReadOnlySpan<byte> bytes)
    {
        var value = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        return value > int.MaxValue ? int.MaxValue : checked((int)value);
    }

    private static int ReadLittleEndianUInt16(ReadOnlySpan<byte> bytes) => bytes[0] | (bytes[1] << 8);

    private static uint ReadLittleEndianUInt32(ReadOnlySpan<byte> bytes) =>
        (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24));

    private static int ReadLittleEndianUInt24(ReadOnlySpan<byte> bytes) => bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ImageGatewayException(ImageErrorCode.ArtifactCacheUnavailable);
        }
    }

    private sealed record ProviderImagePayload(string? Base64Json, string? Url);

    private sealed record DownloadedImage(byte[] Bytes, PrivateImageFormat? ExpectedFormat);

    private readonly record struct ImageInspection(PrivateImageFormat Format, int Width, int Height);
}
