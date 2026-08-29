using System.Net;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Providers.Image;

namespace VFXComposer.AI.Tests;

[TestClass]
public sealed class OpenAiCompatibleImageGatewaySafetyTests
{
    [TestMethod]
    [DataRow(401, "AuthenticationFailed")]
    [DataRow(403, "AuthorizationFailed")]
    [DataRow(429, "RateLimited")]
    [DataRow(500, "UpstreamUnavailable")]
    public async Task ProviderStatusMapsToStableRedactedError(int statusCode, string expectedCodeName)
    {
        using var temp = new A3PrivateTempDirectory();
        using var cache = new PrivateImageArtifactCache(temp.Path);
        using var credentials = new A3StaticCredentialSource();
        using var api = new A3RecordingHandler((_, _, _) =>
            Task.FromResult(A3ImageTestSupport.Json((HttpStatusCode)statusCode, "synthetic provider failure body must not escape")));
        using var download = new A3RecordingHandler((_, _, _) => throw new AssertFailedException("Failure must not use a fallback download."));
        using var gateway = new OpenAiCompatibleImageGateway(A3ImageTestSupport.Route(), credentials, cache, api, download);

        var exception = await A3ImageTestSupport.AssertImageErrorAsync(
            Enum.Parse<ImageErrorCode>(expectedCodeName, ignoreCase: false),
            () => gateway.GenerateImageAsync(new ImageChannelRequest("correlation-status", "synthetic prompt")).AsTask());

        Assert.IsFalse(exception.Message.Contains("synthetic provider failure", StringComparison.Ordinal));
        Assert.AreEqual(1, api.CallCount);
        Assert.AreEqual(0, download.CallCount);
        if (statusCode is 429 or 500)
        {
            Assert.IsTrue(exception.Retryable);
        }
        else
        {
            Assert.IsFalse(exception.Retryable);
        }
    }

    [TestMethod]
    public async Task InvalidOpaqueEndpointFailsAtCallTimeWithoutNetworkOrEndpointLeak()
    {
        const string endpoint = "not a URI; synthetic-opaque-endpoint-token";
        const string prompt = "synthetic-prompt-secret";
        using var temp = new A3PrivateTempDirectory();
        using var cache = new PrivateImageArtifactCache(temp.Path);
        using var credentials = new A3StaticCredentialSource("synthetic-credential-secret");
        using var api = new A3RecordingHandler((_, _, _) => throw new AssertFailedException("Bad endpoint must not call a handler."));
        using var download = new A3RecordingHandler((_, _, _) => throw new AssertFailedException("Bad endpoint must not call a handler."));
        using var gateway = new OpenAiCompatibleImageGateway(A3ImageTestSupport.Route(endpoint), credentials, cache, api, download);
        var request = new ImageChannelRequest("correlation-endpoint", prompt);

        var exception = await A3ImageTestSupport.AssertImageErrorAsync(
            ImageErrorCode.EndpointInvalid,
            () => gateway.GenerateImageAsync(request).AsTask());

        Assert.AreEqual(0, api.CallCount);
        Assert.AreEqual(0, download.CallCount);
        Assert.IsFalse(exception.ToString().Contains(endpoint, StringComparison.Ordinal));
        Assert.IsFalse(exception.ToString().Contains(prompt, StringComparison.Ordinal));
        Assert.IsFalse(exception.ToString().Contains("synthetic-credential-secret", StringComparison.Ordinal));
        Assert.IsFalse(gateway.ToString().Contains(endpoint, StringComparison.Ordinal));
        Assert.IsFalse(request.ToString().Contains(prompt, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task UpstreamFailureUsesExactlyOneRequestAndNeverFallsBack()
    {
        using var temp = new A3PrivateTempDirectory();
        using var cache = new PrivateImageArtifactCache(temp.Path);
        using var credentials = new A3StaticCredentialSource();
        using var api = new A3RecordingHandler((_, _, _) =>
            Task.FromResult(A3ImageTestSupport.Json(HttpStatusCode.ServiceUnavailable, "synthetic failure")));
        using var download = new A3RecordingHandler((_, _, _) => throw new AssertFailedException("No URL fallback is permitted."));
        using var gateway = new OpenAiCompatibleImageGateway(A3ImageTestSupport.Route(), credentials, cache, api, download);

        var exception = await A3ImageTestSupport.AssertImageErrorAsync(
            ImageErrorCode.UpstreamUnavailable,
            () => gateway.GenerateImageAsync(new ImageChannelRequest("correlation-no-retry", "synthetic prompt")).AsTask());

        Assert.IsTrue(exception.Retryable);
        Assert.AreEqual(1, api.CallCount);
        Assert.AreEqual(0, download.CallCount);
    }

    [TestMethod]
    public async Task HandlerFaultIsRedactedAndDoesNotExposeRequestOrCredentialText()
    {
        const string prompt = "synthetic-handler-prompt-secret";
        const string credential = "synthetic-handler-credential-secret";
        using var temp = new A3PrivateTempDirectory();
        using var cache = new PrivateImageArtifactCache(temp.Path);
        using var credentials = new A3StaticCredentialSource(credential);
        using var api = new A3RecordingHandler((_, _, _) =>
            throw new InvalidOperationException("synthetic-handler-provider-secret"));
        using var download = new A3RecordingHandler((_, _, _) => throw new AssertFailedException("No fallback is permitted."));
        using var gateway = new OpenAiCompatibleImageGateway(A3ImageTestSupport.Route(), credentials, cache, api, download);

        var exception = await A3ImageTestSupport.AssertImageErrorAsync(
            ImageErrorCode.NetworkFailure,
            () => gateway.GenerateImageAsync(new ImageChannelRequest("correlation-handler", prompt)).AsTask());

        Assert.IsFalse(exception.ToString().Contains(prompt, StringComparison.Ordinal));
        Assert.IsFalse(exception.ToString().Contains(credential, StringComparison.Ordinal));
        Assert.IsFalse(exception.ToString().Contains("synthetic-handler-provider-secret", StringComparison.Ordinal));
        Assert.AreEqual(1, api.CallCount);
        Assert.AreEqual(0, download.CallCount);
    }

    [TestMethod]
    public async Task UrlRedirectIsRejectedWithoutFollowingIt()
    {
        using var temp = new A3PrivateTempDirectory();
        using var cache = new PrivateImageArtifactCache(temp.Path);
        using var credentials = new A3StaticCredentialSource();
        using var api = new A3RecordingHandler((_, _, _) =>
            Task.FromResult(A3ImageTestSupport.UrlResponse("https://cdn.example.test/image.png")));
        using var download = new A3RecordingHandler((_, _, _) =>
        {
            var response = A3ImageTestSupport.Image(HttpStatusCode.Found, [], "image/png");
            response.Headers.Location = new Uri("https://other-host.example.test/redirected.png");
            return Task.FromResult(response);
        });
        using var gateway = new OpenAiCompatibleImageGateway(A3ImageTestSupport.Route(), credentials, cache, api, download);

        await A3ImageTestSupport.AssertImageErrorAsync(
            ImageErrorCode.ArtifactRedirectRejected,
            () => gateway.GenerateImageAsync(new ImageChannelRequest("correlation-redirect", "synthetic prompt")).AsTask());

        Assert.AreEqual(1, api.CallCount);
        Assert.AreEqual(1, download.CallCount);
    }

    [TestMethod]
    public async Task UrlArtifactRejectsMimeAndByteLimitBeforePrivateCacheWrite()
    {
        var png = A3ImageTestSupport.Png();
        using var temp = new A3PrivateTempDirectory();
        using var cache = new PrivateImageArtifactCache(temp.Path);
        using var credentials = new A3StaticCredentialSource();
        using var api = new A3RecordingHandler((_, call, _) =>
            Task.FromResult(A3ImageTestSupport.UrlResponse("https://cdn.example.test/image-" + call + ".png")));
        using var download = new A3RecordingHandler((_, call, _) =>
        {
            var response = A3ImageTestSupport.Image(HttpStatusCode.OK, png, call == 1 ? "image/gif" : "image/png");
            if (call == 2)
            {
                response.Content.Headers.ContentLength = ImageArtifactLimits.MaximumImageBytes + 1L;
            }

            return Task.FromResult(response);
        });
        using var gateway = new OpenAiCompatibleImageGateway(A3ImageTestSupport.Route(), credentials, cache, api, download);

        await A3ImageTestSupport.AssertImageErrorAsync(
            ImageErrorCode.ArtifactMimeNotAllowed,
            () => gateway.GenerateImageAsync(new ImageChannelRequest("correlation-mime", "synthetic prompt")).AsTask());
        await A3ImageTestSupport.AssertImageErrorAsync(
            ImageErrorCode.ArtifactTooLarge,
            () => gateway.GenerateImageAsync(new ImageChannelRequest("correlation-bytes", "synthetic prompt")).AsTask());

        Assert.AreEqual(2, api.CallCount);
        Assert.AreEqual(2, download.CallCount);
        Assert.IsFalse(Directory.EnumerateDirectories(temp.Path).SelectMany(Directory.EnumerateFiles).Any());
    }

    [TestMethod]
    public async Task ImageDimensionBombAndMalformedBase64AreRejected()
    {
        var dimensionBomb = A3ImageTestSupport.Png(4096, 4097);
        using var temp = new A3PrivateTempDirectory();
        using var cache = new PrivateImageArtifactCache(temp.Path);
        using var credentials = new A3StaticCredentialSource();
        using var api = new A3RecordingHandler((_, call, _) =>
        {
            var response = call == 1
                ? A3ImageTestSupport.B64Response(dimensionBomb)
                : A3ImageTestSupport.Json(HttpStatusCode.OK, "{\"data\":[{\"b64_json\":\"%%%not-base64%%%\"}]}");
            return Task.FromResult(response);
        });
        using var download = new A3RecordingHandler((_, _, _) => throw new AssertFailedException("No URL fetch expected."));
        using var gateway = new OpenAiCompatibleImageGateway(A3ImageTestSupport.Route(), credentials, cache, api, download);

        await A3ImageTestSupport.AssertImageErrorAsync(
            ImageErrorCode.ArtifactPixelLimitExceeded,
            () => gateway.GenerateImageAsync(new ImageChannelRequest("correlation-bomb", "synthetic prompt")).AsTask());
        await A3ImageTestSupport.AssertImageErrorAsync(
            ImageErrorCode.MalformedResponse,
            () => gateway.GenerateImageAsync(new ImageChannelRequest("correlation-b64", "synthetic prompt")).AsTask());
    }

    [TestMethod]
    public async Task CancellationAndTimeoutMapToStableErrors()
    {
        var png = A3ImageTestSupport.Png();
        using var cancelledTemp = new A3PrivateTempDirectory();
        using var cancelledCache = new PrivateImageArtifactCache(cancelledTemp.Path);
        using var credentials = new A3StaticCredentialSource();
        using var cancelledApi = new A3RecordingHandler((_, _, _) => Task.FromResult(A3ImageTestSupport.B64Response(png)));
        using var cancelledDownload = new A3RecordingHandler((_, _, _) => throw new AssertFailedException("No URL fetch expected."));
        using var cancelledGateway = new OpenAiCompatibleImageGateway(A3ImageTestSupport.Route(), credentials, cancelledCache, cancelledApi, cancelledDownload);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await A3ImageTestSupport.AssertImageErrorAsync(
            ImageErrorCode.Cancelled,
            () => cancelledGateway.GenerateImageAsync(new ImageChannelRequest("correlation-cancel", "synthetic prompt"), cancellation.Token).AsTask());
        Assert.AreEqual(0, cancelledApi.CallCount);

        using var timedTemp = new A3PrivateTempDirectory();
        using var timedCache = new PrivateImageArtifactCache(timedTemp.Path);
        using var timedApi = new A3RecordingHandler(async (_, _, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new AssertFailedException("Timeout handler should be cancelled.");
        });
        using var timedDownload = new A3RecordingHandler((_, _, _) => throw new AssertFailedException("No URL fetch expected."));
        using var timedGateway = new OpenAiCompatibleImageGateway(A3ImageTestSupport.Route(timeoutSeconds: 1), credentials, timedCache, timedApi, timedDownload);

        var timeoutException = await A3ImageTestSupport.AssertImageErrorAsync(
            ImageErrorCode.TimedOut,
            () => timedGateway.GenerateImageAsync(new ImageChannelRequest("correlation-timeout", "synthetic prompt")).AsTask());
        Assert.IsTrue(timeoutException.Retryable);
        Assert.AreEqual(1, timedApi.CallCount);
    }

    [TestMethod]
    public async Task OversizedB64JsonIsRejectedBeforeDecode()
    {
        var maximumCharacters = checked(((ImageArtifactLimits.MaximumImageBytes + 2) / 3) * 4);
        var oversizedBase64 = new string('A', maximumCharacters + 4);
        var body = "{\"data\":[{\"b64_json\":\"" + oversizedBase64 + "\"}]}";
        using var temp = new A3PrivateTempDirectory();
        using var cache = new PrivateImageArtifactCache(temp.Path);
        using var credentials = new A3StaticCredentialSource();
        using var api = new A3RecordingHandler((_, _, _) => Task.FromResult(A3ImageTestSupport.Json(HttpStatusCode.OK, body)));
        using var download = new A3RecordingHandler((_, _, _) => throw new AssertFailedException("No URL fetch expected."));
        using var gateway = new OpenAiCompatibleImageGateway(A3ImageTestSupport.Route(), credentials, cache, api, download);

        await A3ImageTestSupport.AssertImageErrorAsync(
            ImageErrorCode.ArtifactTooLarge,
            () => gateway.GenerateImageAsync(new ImageChannelRequest("correlation-b64-size", "synthetic prompt")).AsTask());
        Assert.AreEqual(1, api.CallCount);
    }
}
