using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VFXComposer.AiLocalE2E.Tests;

/// <summary>
/// A deliberately small raw HTTP listener used only by A5. It accepts connections exclusively on 127.0.0.1 and
/// exposes safe boolean request checks so test failures cannot format endpoint, credential, prompt, or body values.
/// </summary>
internal sealed class LoopbackProviderServer : IAsyncDisposable
{
    private const int MaximumHeaderBytes = 64 * 1024;
    private const int MaximumRequestBodyBytes = 512 * 1024;
    private readonly TcpListener _listener = new(IPAddress.Loopback, port: 0);
    private readonly CancellationTokenSource _stopping = new();
    private readonly Func<LoopbackRequest, CancellationToken, ValueTask<LoopbackResponse>> _responder;
    private readonly object _connectionGate = new();
    private readonly List<Task> _connections = [];
    private readonly Task _acceptLoop;
    private int _requestCount;
    private int _responderFailure;
    private int _disposed;

    public LoopbackProviderServer(Func<LoopbackRequest, CancellationToken, ValueTask<LoopbackResponse>> responder)
    {
        _responder = responder ?? throw new ArgumentNullException(nameof(responder));
        _listener.Start();
        _acceptLoop = AcceptLoopAsync();
    }

    public int RequestCount => Volatile.Read(ref _requestCount);

    public bool HasResponderFailure => Volatile.Read(ref _responderFailure) != 0;

    public string Endpoint(string pathAndQuery)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pathAndQuery);
        if (!pathAndQuery.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException("A loopback endpoint must include an absolute path.", nameof(pathAndQuery));
        }

        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        return "http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + pathAndQuery;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _stopping.Cancel();
        _listener.Stop();
        await AwaitQuietlyAsync(_acceptLoop).ConfigureAwait(false);

        while (true)
        {
            Task[] active;
            lock (_connectionGate)
            {
                active = _connections.ToArray();
            }

            if (active.Length == 0)
            {
                break;
            }

            await Task.WhenAll(active).ConfigureAwait(false);
        }

        _stopping.Dispose();
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!_stopping.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_stopping.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException) when (_stopping.IsCancellationRequested)
                {
                    break;
                }
                catch (SocketException) when (_stopping.IsCancellationRequested)
                {
                    break;
                }

                var connection = ServeConnectionAsync(client);
                lock (_connectionGate)
                {
                    _connections.Add(connection);
                }

                _ = connection.ContinueWith(
                    completed =>
                    {
                        lock (_connectionGate)
                        {
                            _connections.Remove(completed);
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
        catch (SocketException) when (_stopping.IsCancellationRequested)
        {
            // Listener shutdown is expected during per-test cleanup.
        }
    }

    private async Task ServeConnectionAsync(TcpClient client)
    {
        using (client)
        await using (var stream = client.GetStream())
        {
            try
            {
                var request = await ReadRequestAsync(stream, _stopping.Token).ConfigureAwait(false);
                LoopbackResponse response;
                try
                {
                    Interlocked.Increment(ref _requestCount);
                    response = await _responder(request, _stopping.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
                {
                    return;
                }
                catch
                {
                    Interlocked.Exchange(ref _responderFailure, 1);
                    response = LoopbackResponse.Empty(statusCode: 500);
                }
                finally
                {
                    request.Dispose();
                }

                if (response.Delay > TimeSpan.Zero)
                {
                    await Task.Delay(response.Delay, _stopping.Token).ConfigureAwait(false);
                }

                await WriteResponseAsync(stream, response, _stopping.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
                // Listener shutdown is expected during per-test cleanup.
            }
            catch (IOException)
            {
                // A request-time timeout can close the client before the scripted response is written.
            }
            catch (SocketException)
            {
                // The client owns its connection lifetime.
            }
            catch
            {
                Interlocked.Exchange(ref _responderFailure, 1);
            }
        }
    }

    private static async ValueTask<LoopbackRequest> ReadRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var header = new List<byte>(capacity: 1024);
        var marker = 0;
        var one = new byte[1];
        while (marker != 4)
        {
            if (header.Count == MaximumHeaderBytes)
            {
                throw new InvalidDataException("Loopback request headers exceeded their bound.");
            }

            var read = await stream.ReadAsync(one.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new IOException("Loopback client closed before request headers completed.");
            }

            var current = one[0];
            header.Add(current);
            marker = marker switch
            {
                0 when current == (byte)'\r' => 1,
                1 when current == (byte)'\n' => 2,
                2 when current == (byte)'\r' => 3,
                3 when current == (byte)'\n' => 4,
                _ => 0,
            };
        }

        var headerText = Encoding.ASCII.GetString(header.ToArray());
        var lines = headerText.Split("\r\n", StringSplitOptions.None);
        var requestParts = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (requestParts.Length != 3)
        {
            throw new InvalidDataException("Loopback request line is invalid.");
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.Length == 0)
            {
                break;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                throw new InvalidDataException("Loopback request header is invalid.");
            }

            headers[line[..colon]] = line[(colon + 1)..].TrimStart();
        }

        var contentLength = 0;
        if (headers.TryGetValue("Content-Length", out var contentLengthText) &&
            (!int.TryParse(contentLengthText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out contentLength) ||
             contentLength is < 0 or > MaximumRequestBodyBytes))
        {
            throw new InvalidDataException("Loopback request content length is invalid.");
        }

        var body = new byte[contentLength];
        try
        {
            if (body.Length != 0)
            {
                await stream.ReadExactlyAsync(body, cancellationToken).ConfigureAwait(false);
            }

            return new LoopbackRequest(requestParts[0], requestParts[1], headers, body);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(body);
            throw;
        }
    }

    private static async ValueTask WriteResponseAsync(
        NetworkStream stream,
        LoopbackResponse response,
        CancellationToken cancellationToken)
    {
        var body = response.Body;
        var contentLength = response.DeclaredContentLength ?? body.Length;
        var headers = "HTTP/1.1 " + response.StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " +
            ReasonPhrase(response.StatusCode) + "\r\n" +
            "Content-Type: " + response.ContentType + "\r\n" +
            "Content-Length: " + contentLength.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\r\n" +
            "Connection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(headers), cancellationToken).ConfigureAwait(false);
        if (body.Length != 0)
        {
            await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        }

        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string ReasonPhrase(int statusCode) => statusCode switch
    {
        200 => "OK",
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        429 => "Too Many Requests",
        500 => "Internal Server Error",
        _ => "Loopback",
    };

    private static async ValueTask AwaitQuietlyAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation was requested by DisposeAsync.
        }
    }
}

/// <summary>Ephemeral raw request data. Its public surface offers only safe predicates and a redacted formatter.</summary>
internal sealed class LoopbackRequest : IDisposable
{
    private readonly string _method;
    private readonly string _target;
    private readonly IReadOnlyDictionary<string, string> _headers;
    private byte[] _body;

    internal LoopbackRequest(string method, string target, IReadOnlyDictionary<string, string> headers, byte[] body)
    {
        _method = method;
        _target = target;
        _headers = headers;
        _body = body;
    }

    public bool IsTarget(string expectedTarget) => string.Equals(_target, expectedTarget, StringComparison.Ordinal);

    public bool MatchesJsonPost(
        string expectedTarget,
        string expectedAuthorization,
        Func<JsonElement, bool> bodyMatches)
    {
        ArgumentNullException.ThrowIfNull(bodyMatches);
        if (!string.Equals(_method, "POST", StringComparison.Ordinal) ||
            !string.Equals(_target, expectedTarget, StringComparison.Ordinal) ||
            !_headers.TryGetValue("Authorization", out var authorization) ||
            !string.Equals(authorization, expectedAuthorization, StringComparison.Ordinal) ||
            !_headers.TryGetValue("Content-Type", out var contentType) ||
            !contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(_body);
            return bodyMatches(document.RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public bool MatchesUnauthenticatedGet(string expectedTarget) =>
        string.Equals(_method, "GET", StringComparison.Ordinal) &&
        string.Equals(_target, expectedTarget, StringComparison.Ordinal) &&
        !_headers.ContainsKey("Authorization") &&
        _body.Length == 0;

    public override string ToString() => "LoopbackRequest(<redacted>)";

    public void Dispose()
    {
        var body = Interlocked.Exchange(ref _body, Array.Empty<byte>());
        CryptographicOperations.ZeroMemory(body);
    }
}

/// <summary>Bounded scripted loopback response; it never formats or retains request data.</summary>
internal sealed class LoopbackResponse
{
    private LoopbackResponse(int statusCode, byte[] body, string contentType, long? declaredContentLength, TimeSpan delay)
    {
        StatusCode = statusCode;
        Body = body;
        ContentType = contentType;
        DeclaredContentLength = declaredContentLength;
        Delay = delay;
    }

    public int StatusCode { get; }
    public ReadOnlyMemory<byte> Body { get; }
    public string ContentType { get; }
    public long? DeclaredContentLength { get; }
    public TimeSpan Delay { get; }

    public static LoopbackResponse Json(int statusCode, string json, TimeSpan? delay = null, long? declaredContentLength = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        return new LoopbackResponse(
            statusCode,
            Encoding.UTF8.GetBytes(json),
            "application/json",
            declaredContentLength,
            delay ?? TimeSpan.Zero);
    }

    public static LoopbackResponse Image(byte[] bytes, string contentType, long? declaredContentLength = null) =>
        new(200, bytes ?? throw new ArgumentNullException(nameof(bytes)), contentType, declaredContentLength, TimeSpan.Zero);

    public static LoopbackResponse Empty(int statusCode) =>
        new(statusCode, Array.Empty<byte>(), "application/json", declaredContentLength: 0, TimeSpan.Zero);
}
