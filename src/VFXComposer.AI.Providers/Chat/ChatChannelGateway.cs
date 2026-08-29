using System.Buffers;
using System.Net;
using System.Security.Cryptography;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Chat;

namespace VFXComposer.AI.Providers.Chat;

/// <summary>
/// Request-time transport for one persisted ChatLlm binding.  Endpoint text stays opaque until the instant a single
/// request is constructed, and a failed call never writes configuration or selects another route.
/// </summary>
public sealed class ChatChannelGateway : IChatChannelGateway, IDisposable
{
    private readonly ProviderConfigurationStore _configurationStore;
    private readonly ProviderSecretStore _secretStore;
    private readonly ChatRouteResolver _routeResolver;
    private readonly HttpClient _httpClient;
    private int _disposed;

    /// <summary>Creates the production client.  Tests use the internal handler constructor and never call a network.</summary>
    public ChatChannelGateway(
        ProviderConfigurationStore configurationStore,
        ProviderHealthRegistry healthRegistry,
        ProviderSecretStore secretStore)
        : this(configurationStore, healthRegistry, secretStore, CreateProductionClient())
    {
    }

    /// <summary>Test-only transport seam.  It is internal and exposed only to the scoped AI test assembly.</summary>
    internal ChatChannelGateway(
        ProviderConfigurationStore configurationStore,
        ProviderHealthRegistry healthRegistry,
        ProviderSecretStore secretStore,
        HttpMessageHandler handler)
        : this(
            configurationStore,
            healthRegistry,
            secretStore,
            new HttpClient(handler ?? throw new ArgumentNullException(nameof(handler)), disposeHandler: true)
            {
                Timeout = Timeout.InfiniteTimeSpan,
            })
    {
    }

    private ChatChannelGateway(
        ProviderConfigurationStore configurationStore,
        ProviderHealthRegistry healthRegistry,
        ProviderSecretStore secretStore,
        HttpClient httpClient)
    {
        _configurationStore = configurationStore ?? throw new ArgumentNullException(nameof(configurationStore));
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _routeResolver = new ChatRouteResolver(healthRegistry, _secretStore);
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async ValueTask<ChatChannelResult> CompleteAsync(
        ChatChannelRequest request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            throw new ChatChannelException(ChatChannelErrorCode.Cancelled);
        }

        ChatResolvedRoute route;
        try
        {
            // Resolve once, before any await.  This immutable snapshot pins the selected profile/capability/model/
            // protocol for the complete call even if another thread saves a newer configuration while it is in flight.
            route = _routeResolver.Resolve(_configurationStore.Load().Configuration);
        }
        catch (ChatChannelException)
        {
            throw;
        }
        catch (AiGatewayException exception)
        {
            throw ChatErrorMapper.FromA1(exception);
        }

        var requestUri = CreateRequestUri(route.Profile.Endpoint);
        byte[]? payload = null;
        try
        {
            payload = ChatProtocolCodec.CreateRequestPayload(route, request);
            using var message = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new ByteArrayContent(payload),
            };
            message.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            try
            {
                using (var secret = _secretStore.OpenSecret(route.Profile.Id, route.Profile.Auth.SecretRef))
                {
                    ChatProtocolCodec.ApplyAuthentication(message, route.Protocol, secret.Bytes);
                }
            }
            catch (ChatChannelException)
            {
                throw;
            }
            catch (AiGatewayException exception)
            {
                throw ChatErrorMapper.FromA1(exception);
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(route.Profile.TimeoutSeconds));
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(
                    message,
                    HttpCompletionOption.ResponseHeadersRead,
                    linkedCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw new ChatChannelException(ChatChannelErrorCode.Cancelled);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                throw new ChatChannelException(ChatChannelErrorCode.TimedOut);
            }
            catch (OperationCanceledException)
            {
                throw new ChatChannelException(ChatChannelErrorCode.Cancelled);
            }
            catch (HttpRequestException)
            {
                throw new ChatChannelException(ChatChannelErrorCode.TransportFailed, retryable: true);
            }
            catch (IOException)
            {
                throw new ChatChannelException(ChatChannelErrorCode.TransportFailed, retryable: true);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw MapStatus(response.StatusCode);
                }

                if (response.Content is null)
                {
                    throw new ChatChannelException(ChatChannelErrorCode.ResponseMalformed);
                }

                byte[]? responseBytes = null;
                try
                {
                    responseBytes = await ReadBoundedResponseAsync(response.Content, linkedCancellation.Token).ConfigureAwait(false);
                    return ChatProtocolCodec.ParseSuccessResponse(route.Protocol, request, responseBytes);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw new ChatChannelException(ChatChannelErrorCode.Cancelled);
                }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                {
                    throw new ChatChannelException(ChatChannelErrorCode.TimedOut);
                }
                catch (HttpRequestException)
                {
                    throw new ChatChannelException(ChatChannelErrorCode.TransportFailed, retryable: true);
                }
                catch (IOException)
                {
                    throw new ChatChannelException(ChatChannelErrorCode.TransportFailed, retryable: true);
                }
                finally
                {
                    if (responseBytes is not null)
                    {
                        CryptographicOperations.ZeroMemory(responseBytes);
                    }
                }
            }
        }
        finally
        {
            if (payload is not null)
            {
                CryptographicOperations.ZeroMemory(payload);
            }
        }
    }

    public async ValueTask<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var channelRequest = new ChatChannelRequest(
            request.CorrelationId,
            request.Messages.Select(static message => new ChatChannelMessage(message.Role, message.Content)));
        var result = await CompleteAsync(channelRequest, cancellationToken).ConfigureAwait(false);
        try
        {
            return new ChatResponse(request.CorrelationId, result.Text);
        }
        catch (ArgumentException)
        {
            // The A1-compatible response DTO has a smaller text bound than the richer A2 result contract.
            throw new ChatChannelException(ChatChannelErrorCode.ResponseTooLarge);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _httpClient.Dispose();
        }
    }

    public override string ToString() => "ChatChannelGateway(<redacted>)";

    private static HttpClient CreateProductionClient() => new()
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };

    private static Uri CreateRequestUri(OpaqueEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        try
        {
            // Preserve endpoint.Value as supplied.  The only interpretation is the necessary best-effort URI creation
            // at send time; there is deliberately no trimming, normalisation, path append, query edit, or concatenation.
            if (!Uri.TryCreate(endpoint.Value, UriKind.Absolute, out var requestUri) ||
                requestUri is null ||
                (requestUri.Scheme != Uri.UriSchemeHttp && requestUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ChatChannelException(ChatChannelErrorCode.EndpointUnusable);
            }

            return requestUri;
        }
        catch (ChatChannelException)
        {
            throw;
        }
        catch (UriFormatException)
        {
            throw new ChatChannelException(ChatChannelErrorCode.EndpointUnusable);
        }
        catch (ArgumentException)
        {
            throw new ChatChannelException(ChatChannelErrorCode.EndpointUnusable);
        }
    }

    private static ChatChannelException MapStatus(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
            new ChatChannelException(ChatChannelErrorCode.AuthenticationFailed),
        (HttpStatusCode)429 => new ChatChannelException(ChatChannelErrorCode.RateLimited, retryable: true),
        >= HttpStatusCode.InternalServerError =>
            new ChatChannelException(ChatChannelErrorCode.UpstreamUnavailable, retryable: true),
        _ => new ChatChannelException(ChatChannelErrorCode.UpstreamRejected),
    };

    private static async ValueTask<byte[]> ReadBoundedResponseAsync(HttpContent content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Headers.ContentLength is long contentLength && contentLength > ChatChannelLimits.MaximumResponseBytes)
        {
            throw new ChatChannelException(ChatChannelErrorCode.ResponseTooLarge);
        }

        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var rented = ArrayPool<byte>.Shared.Rent(16 * 1024);
        using var buffer = new MemoryStream();
        try
        {
            while (true)
            {
                var count = await stream.ReadAsync(rented.AsMemory(0, rented.Length), cancellationToken).ConfigureAwait(false);
                if (count == 0)
                {
                    break;
                }

                if (buffer.Length > ChatChannelLimits.MaximumResponseBytes - count)
                {
                    throw new ChatChannelException(ChatChannelErrorCode.ResponseTooLarge);
                }

                buffer.Write(rented, 0, count);
            }

            return buffer.ToArray();
        }
        finally
        {
            if (buffer.TryGetBuffer(out var segment))
            {
                CryptographicOperations.ZeroMemory(segment.AsSpan(0, checked((int)buffer.Length)));
            }

            CryptographicOperations.ZeroMemory(rented);
            ArrayPool<byte>.Shared.Return(rented, clearArray: false);
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(ChatChannelGateway));
        }
    }
}
