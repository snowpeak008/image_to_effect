using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Providers.Image;

namespace VFXComposer.AI.Tests;

[TestClass]
public sealed class OpenAiCompatibleImageGatewayTests
{
    [TestMethod]
    public async Task TypedRequest_UsesFixedRouteAndNormalizesB64JsonIntoPrivateArtifact()
    {
        var png = A3ImageTestSupport.Png(512, 256);
        using var temp = new A3PrivateTempDirectory();
        using var cache = new PrivateImageArtifactCache(temp.Path);
        using var credentials = new A3StaticCredentialSource();
        using var api = new A3RecordingHandler((_, _, _) => Task.FromResult(A3ImageTestSupport.B64Response(png)));
        using var download = new A3RecordingHandler((_, _, _) => throw new AssertFailedException("b64_json must not download a URL."));
        using var gateway = new OpenAiCompatibleImageGateway(A3ImageTestSupport.Route(), credentials, cache, api, download);

        var result = await gateway.GenerateImageAsync(
            new ImageChannelRequest(
                "correlation-b64",
                "synthetic image prompt",
                new ImageGenerationOptions(
                    new ImageRequestDimensions(512, 256),
                    ImageGenerationQuality.Hd,
                    ImageGenerationStyle.Natural)));

        Assert.AreEqual("correlation-b64", result.CorrelationId);
        Assert.AreEqual(PrivateImageFormat.Png, result.Artifact.Format);
        Assert.AreEqual(512, result.Artifact.Width);
        Assert.AreEqual(256, result.Artifact.Height);
        Assert.AreEqual("image/png", result.Artifact.ContentType);
        Assert.AreEqual(
            Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant(),
            result.Artifact.Sha256);
        Assert.IsFalse(result.ToString().Contains("synthetic image prompt", StringComparison.Ordinal));

        Assert.AreEqual(1, api.CallCount);
        Assert.AreEqual(0, download.CallCount);
        var sent = api.Requests.Single();
        Assert.AreEqual("POST", sent.Method);
        Assert.AreEqual("https://images.example.test/v1/images/generations", sent.Uri);
        Assert.AreEqual("Bearer synthetic-a3-test-token", sent.Header("Authorization"));
        using var requestJson = JsonDocument.Parse(sent.Body);
        Assert.AreEqual("image-model-1", requestJson.RootElement.GetProperty("model").GetString());
        Assert.AreEqual("synthetic image prompt", requestJson.RootElement.GetProperty("prompt").GetString());
        Assert.AreEqual("512x256", requestJson.RootElement.GetProperty("size").GetString());
        Assert.AreEqual("hd", requestJson.RootElement.GetProperty("quality").GetString());
        Assert.AreEqual("natural", requestJson.RootElement.GetProperty("style").GetString());
        Assert.AreEqual("b64_json", requestJson.RootElement.GetProperty("response_format").GetString());
        Assert.AreEqual(1, requestJson.RootElement.GetProperty("n").GetInt32());

        Assert.AreEqual(result.Artifact.Id, gateway.GetArtifact(result.Artifact.Id).Id);
        using var artifactStream = await gateway.OpenReadAsync(result.Artifact.Id);
        using var artifactBytes = new MemoryStream();
        await artifactStream.CopyToAsync(artifactBytes);
        CollectionAssert.AreEqual(png, artifactBytes.ToArray());
    }

    [TestMethod]
    public async Task GatewayInterface_UsesImageChannelAndReturnsOnlyPrivateArtifactId()
    {
        var png = A3ImageTestSupport.Png(64, 64);
        using var temp = new A3PrivateTempDirectory();
        using var cache = new PrivateImageArtifactCache(temp.Path);
        using var credentials = new A3StaticCredentialSource();
        using var api = new A3RecordingHandler((_, _, _) => Task.FromResult(A3ImageTestSupport.B64Response(png)));
        using var download = new A3RecordingHandler((_, _, _) => throw new AssertFailedException("No URL fetch expected."));
        using var implementation = new OpenAiCompatibleImageGateway(A3ImageTestSupport.Route(), credentials, cache, api, download);
        IImageGateway gateway = implementation;

        var response = await gateway.GenerateImageAsync(new ImageGenerationRequest("correlation-gateway", "synthetic prompt", 64, 64));

        Assert.AreEqual("correlation-gateway", response.CorrelationId);
        Assert.IsTrue(response.PrivateArtifactId.StartsWith("img-", StringComparison.Ordinal));
        Assert.IsFalse(response.ToString().Contains("synthetic prompt", StringComparison.Ordinal));
        Assert.AreEqual(1, api.CallCount);
        using var json = JsonDocument.Parse(api.Requests.Single().Body);
        Assert.AreEqual("image-model-1", json.RootElement.GetProperty("model").GetString());
        Assert.AreEqual("64x64", json.RootElement.GetProperty("size").GetString());
    }

    [TestMethod]
    public async Task UrlResponse_DownloadsWithSeparateUnauthenticatedRequestAndKeepsUrlPrivate()
    {
        const string artifactUrl = "https://cdn.example.test/private/image.png?synthetic-url-token";
        var png = A3ImageTestSupport.Png(64, 64);
        using var temp = new A3PrivateTempDirectory();
        using var cache = new PrivateImageArtifactCache(temp.Path);
        using var credentials = new A3StaticCredentialSource();
        using var api = new A3RecordingHandler((_, _, _) => Task.FromResult(A3ImageTestSupport.UrlResponse(artifactUrl)));
        using var download = new A3RecordingHandler((request, _, _) =>
        {
            Assert.AreEqual("GET", request.Method);
            Assert.AreEqual(artifactUrl, request.Uri);
            return Task.FromResult(A3ImageTestSupport.Image(HttpStatusCode.OK, png, "image/png"));
        });
        using var gateway = new OpenAiCompatibleImageGateway(A3ImageTestSupport.Route(), credentials, cache, api, download);

        var result = await gateway.GenerateImageAsync(new ImageChannelRequest("correlation-url", "synthetic prompt"));

        Assert.AreEqual(1, api.CallCount);
        Assert.AreEqual(1, download.CallCount);
        Assert.IsTrue(api.Requests.Single().HasHeader("Authorization"));
        Assert.IsFalse(download.Requests.Single().HasHeader("Authorization"));
        Assert.IsFalse(download.Requests.Single().HasHeader("Content-Type"));
        Assert.IsFalse(result.ToString().Contains("synthetic-url-token", StringComparison.Ordinal));
        Assert.IsFalse(gateway.ToString().Contains("synthetic-url-token", StringComparison.Ordinal));
        Assert.AreEqual("image/png", gateway.GetArtifact(result.Artifact.Id).ContentType);
    }

    [TestMethod]
    public async Task B64Json_InspectsPngJpegAndWebpDimensionsBeforeCaching()
    {
        var images = new[]
        {
            (Bytes: A3ImageTestSupport.Png(65, 66), Format: PrivateImageFormat.Png, Width: 65, Height: 66),
            (Bytes: A3ImageTestSupport.Jpeg(67, 68), Format: PrivateImageFormat.Jpeg, Width: 67, Height: 68),
            (Bytes: A3ImageTestSupport.Webp(69, 70), Format: PrivateImageFormat.Webp, Width: 69, Height: 70),
        };
        using var temp = new A3PrivateTempDirectory();
        using var cache = new PrivateImageArtifactCache(temp.Path);
        using var credentials = new A3StaticCredentialSource();
        using var api = new A3RecordingHandler((_, call, _) => Task.FromResult(A3ImageTestSupport.B64Response(images[call - 1].Bytes)));
        using var download = new A3RecordingHandler((_, _, _) => throw new AssertFailedException("No URL fetch expected."));
        using var gateway = new OpenAiCompatibleImageGateway(A3ImageTestSupport.Route(), credentials, cache, api, download);

        for (var index = 0; index < images.Length; index++)
        {
            var result = await gateway.GenerateImageAsync(new ImageChannelRequest("correlation-format-" + index, "synthetic prompt"));
            Assert.AreEqual(images[index].Format, result.Artifact.Format);
            Assert.AreEqual(images[index].Width, result.Artifact.Width);
            Assert.AreEqual(images[index].Height, result.Artifact.Height);
        }

        Assert.AreEqual(3, api.CallCount);
    }

    [TestMethod]
    public void TypedImageControlsAreBoundedAndCacheRejectsNonTemporaryRoots()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ImageGenerationOptions(quality: (ImageGenerationQuality)999));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ImageRequestDimensions(63, 64));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ImageRequestDimensions(4096, 4097));
        var nonTemporaryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "..", "vfxcomposer-a3-not-temp"));
        Assert.ThrowsExactly<ArgumentException>(() => new PrivateImageArtifactCache(nonTemporaryRoot));
    }

    [TestMethod]
    public async Task ConcurrentGeneration_UsesUniquePrivateArtifactsAndDisposeCleansSessionCache()
    {
        var png = A3ImageTestSupport.Png(64, 64);
        using var temp = new A3PrivateTempDirectory();
        var cache = new PrivateImageArtifactCache(temp.Path);
        using var credentials = new A3StaticCredentialSource();
        using var api = new A3RecordingHandler((_, _, _) => Task.FromResult(A3ImageTestSupport.B64Response(png)));
        using var download = new A3RecordingHandler((_, _, _) => throw new AssertFailedException("No URL fetch expected."));
        using var gateway = new OpenAiCompatibleImageGateway(A3ImageTestSupport.Route(), credentials, cache, api, download);

        var tasks = Enumerable.Range(0, 12)
            .Select(index => gateway.GenerateImageAsync(new ImageChannelRequest("correlation-" + index, "synthetic prompt")).AsTask())
            .ToArray();
        var results = await Task.WhenAll(tasks);

        Assert.AreEqual(12, results.Select(static result => result.Artifact.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.AreEqual(12, api.CallCount);
        Assert.IsTrue(Directory.EnumerateDirectories(temp.Path).Any());
        foreach (var result in results)
        {
            Assert.AreEqual(result.Artifact.Sha256, cache.GetArtifact(result.Artifact.Id).Sha256);
        }

        cache.Dispose();
        Assert.IsFalse(Directory.EnumerateFileSystemEntries(temp.Path).Any());
    }
}
