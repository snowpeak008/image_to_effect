using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Providers;
using VFXComposer.AI.Providers.Image;

namespace VFXComposer.AI.Tests;

internal static class A3ImageTestSupport
{
    public static ResolvedProviderRoute Route(
        string endpoint = "https://images.example.test/v1/images/generations",
        int timeoutSeconds = 30,
        string modelId = "image-model-1")
    {
        var capability = new CapabilityDefinition("image-main", AiChannel.ImageGeneration, modelId);
        var profile = new ProviderProfile(
            "profile-primary",
            "Primary image provider",
            ProviderOrigin.Custom,
            enabled: true,
            new ProtocolBinding(ProviderProtocols.OpenAiCompatibleV1),
            new OpaqueEndpoint(endpoint),
            new AuthDescriptor(new SecretRef("secret-primary"), SecretScope.Production),
            timeoutSeconds,
            [capability]);
        var binding = new ChannelBinding(AiChannel.ImageGeneration, profile.Id, capability.Id, capability.ModelId);
        return new ResolvedProviderRoute(
            AiChannel.ImageGeneration,
            profile,
            capability,
            binding,
            new ConfigurationFingerprint("sha256:" + new string('a', 64)));
    }

    public static byte[] Png(int width = 64, int height = 64)
    {
        var bytes = new byte[24];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
        bytes[11] = 13;
        "IHDR"u8.CopyTo(bytes.AsSpan(12, 4));
        WriteBigEndian(bytes.AsSpan(16, 4), width);
        WriteBigEndian(bytes.AsSpan(20, 4), height);
        return bytes;
    }

    public static byte[] Jpeg(int width = 64, int height = 64)
    {
        var bytes = new byte[]
        {
            0xff, 0xd8,
            0xff, 0xc0,
            0x00, 0x11,
            0x08,
            0x00, 0x00,
            0x00, 0x00,
            0x03,
            0x01, 0x11, 0x00,
            0x02, 0x11, 0x00,
            0x03, 0x11, 0x00,
            0xff, 0xd9,
        };
        bytes[7] = unchecked((byte)(height >> 8));
        bytes[8] = unchecked((byte)height);
        bytes[9] = unchecked((byte)(width >> 8));
        bytes[10] = unchecked((byte)width);
        return bytes;
    }

    public static byte[] Webp(int width = 64, int height = 64)
    {
        var bytes = new byte[30];
        "RIFF"u8.CopyTo(bytes.AsSpan(0, 4));
        "WEBP"u8.CopyTo(bytes.AsSpan(8, 4));
        "VP8 "u8.CopyTo(bytes.AsSpan(12, 4));
        bytes[16] = 10;
        bytes[23] = 0x9d;
        bytes[24] = 0x01;
        bytes[25] = 0x2a;
        bytes[26] = unchecked((byte)width);
        bytes[27] = unchecked((byte)(width >> 8));
        bytes[28] = unchecked((byte)height);
        bytes[29] = unchecked((byte)(height >> 8));
        return bytes;
    }

    public static HttpResponseMessage Json(HttpStatusCode statusCode, string json)
    {
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return new HttpResponseMessage(statusCode) { Content = content };
    }

    public static HttpResponseMessage B64Response(byte[] bytes) =>
        Json(HttpStatusCode.OK, JsonSerializer.Serialize(new { data = new[] { new { b64_json = Convert.ToBase64String(bytes) } } }));

    public static HttpResponseMessage UrlResponse(string url) =>
        Json(HttpStatusCode.OK, JsonSerializer.Serialize(new { data = new[] { new { url } } }));

    public static HttpResponseMessage Image(HttpStatusCode statusCode, byte[] bytes, string contentType)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new HttpResponseMessage(statusCode) { Content = content };
    }

    public static ImageGatewayException AssertImageError(ImageErrorCode expected, Action action)
    {
        var exception = Assert.ThrowsExactly<ImageGatewayException>(action);
        Assert.AreEqual(expected, exception.Code);
        return exception;
    }

    public static async Task<ImageGatewayException> AssertImageErrorAsync(ImageErrorCode expected, Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (ImageGatewayException exception)
        {
            Assert.AreEqual(expected, exception.Code);
            return exception;
        }

        Assert.Fail("Expected image gateway failure " + expected + ".");
        throw new InvalidOperationException("Unreachable.");
    }

    private static void WriteBigEndian(Span<byte> bytes, int value)
    {
        bytes[0] = unchecked((byte)(value >> 24));
        bytes[1] = unchecked((byte)(value >> 16));
        bytes[2] = unchecked((byte)(value >> 8));
        bytes[3] = unchecked((byte)value);
    }
}

internal sealed class A3PrivateTempDirectory : IDisposable
{
    public A3PrivateTempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "vfxcomposer-a3-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

internal sealed class A3StaticCredentialSource : IImageCredentialSource, IDisposable
{
    private byte[]? _credential;

    public A3StaticCredentialSource(string credential = "synthetic-a3-test-token")
    {
        _credential = Encoding.UTF8.GetBytes(credential);
    }

    public ValueTask<T> UseCredentialAsync<T>(
        string profileId,
        SecretRef secretRef,
        ImageCredentialUse<T> use,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentNullException.ThrowIfNull(secretRef);
        ArgumentNullException.ThrowIfNull(use);
        cancellationToken.ThrowIfCancellationRequested();
        var credential = _credential ?? throw new ObjectDisposedException(nameof(A3StaticCredentialSource));
        return use(credential, cancellationToken);
    }

    public void Dispose()
    {
        var credential = Interlocked.Exchange(ref _credential, null);
        if (credential is not null)
        {
            CryptographicOperations.ZeroMemory(credential);
        }
    }
}

internal sealed class A3RecordingHandler : HttpMessageHandler
{
    private readonly Func<A3CapturedRequest, int, CancellationToken, Task<HttpResponseMessage>> _responder;
    private readonly object _gate = new();
    private readonly List<A3CapturedRequest> _requests = [];
    private int _callCount;

    public A3RecordingHandler(Func<A3CapturedRequest, int, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        _responder = responder ?? throw new ArgumentNullException(nameof(responder));
    }

    public int CallCount => Volatile.Read(ref _callCount);

    public IReadOnlyList<A3CapturedRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return _requests.ToArray();
            }
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var capture = await A3CapturedRequest.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            _requests.Add(capture);
        }

        var call = Interlocked.Increment(ref _callCount);
        return await _responder(capture, call, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class A3CapturedRequest
{
    private A3CapturedRequest(string method, string uri, IReadOnlyDictionary<string, string[]> headers, byte[] body)
    {
        Method = method;
        Uri = uri;
        Headers = headers;
        Body = body;
    }

    public string Method { get; }
    public string Uri { get; }
    public IReadOnlyDictionary<string, string[]> Headers { get; }
    public byte[] Body { get; }

    public string BodyText => Encoding.UTF8.GetString(Body);

    public bool HasHeader(string name) => Headers.ContainsKey(name);

    public string Header(string name) => string.Join(",", Headers[name]);

    public static async Task<A3CapturedRequest> CreateAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers)
        {
            headers.Add(header.Key, header.Value.ToArray());
        }

        byte[] body;
        if (request.Content is null)
        {
            body = [];
        }
        else
        {
            foreach (var header in request.Content.Headers)
            {
                headers.Add(header.Key, header.Value.ToArray());
            }

            body = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }

        return new A3CapturedRequest(
            request.Method.Method,
            request.RequestUri?.AbsoluteUri ?? string.Empty,
            headers,
            body);
    }
}
