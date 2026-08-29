using Avalonia;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Chat;
using VFXComposer.AI.Contracts.Desktop;
using VFXComposer.AI.Providers.Desktop;
using VFXComposer.Desktop;
using VFXComposer.Desktop.ViewModels;

namespace VFXComposer.AiLocalE2E.Tests;

/// <summary>
/// End-to-end evidence for the production Desktop AI handlers. Every transport interaction below reaches a raw
/// listener bound to 127.0.0.1; this suite deliberately has no transport injection seam.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class ProviderLocalE2ETests
{
    private const string ChatTarget = "/a5/chat?opaque=a5-endpoint-secret-sentinel";
    private const string ImageTarget = "/a5/image?opaque=a5-endpoint-secret-sentinel";
    private const string ArtifactTarget = "/a5/artifact?opaque=a5-endpoint-secret-sentinel";

    [ClassInitialize]
    public static void InitializeAvalonia(TestContext _) =>
        AppBuilder.Configure<App>().UsePlatformDetect().SetupWithoutStarting();

    [TestMethod]
    public async Task ProductionDesktopHandlers_SendExactChatAndBase64PreviewRequests()
    {
        var chatRequestWasExact = false;
        var imageRequestWasExact = false;
        await using var server = new LoopbackProviderServer((request, _) =>
        {
            if (request.IsTarget(ChatTarget))
            {
                chatRequestWasExact = request.MatchesJsonPost(
                    ChatTarget,
                    "Bearer " + A5TestValues.ChatSecret,
                    A5LoopbackPayloads.IsExactChatBody);
                return ValueTask.FromResult(LoopbackResponse.Json(200, A5LoopbackPayloads.ChatSuccessJson()));
            }

            if (request.IsTarget(ImageTarget))
            {
                imageRequestWasExact = request.MatchesJsonPost(
                    ImageTarget,
                    "Bearer " + A5TestValues.ImageSecret,
                    A5LoopbackPayloads.IsExactImageBody);
                return ValueTask.FromResult(LoopbackResponse.Json(200, A5LoopbackPayloads.ImageBase64Json()));
            }

            return ValueTask.FromResult(LoopbackResponse.Empty(404));
        });

        using var root = new A5TemporaryRoot();
        var runtime = root.CreateRuntime();
        try
        {
            var chatEndpoint = server.Endpoint(ChatTarget) + "#a5-chat-fragment";
            var imageEndpoint = server.Endpoint(ImageTarget) + "#a5-image-fragment";
            var settings = A5DesktopSettings.ConfigureTwoProfiles(runtime, chatEndpoint, imageEndpoint);

            Assert.AreEqual(2, settings.Profiles.Count, "Two deliberate provider profiles were not persisted.");
            Assert.AreEqual(2, runtime.Settings.Load().Bindings.Count, "Both explicit channel bindings were not persisted.");
            Assert.IsTrue(settings.Profiles.All(profile =>
                !profile.EndpointSummary.Contains(A5TestValues.EndpointMarker, StringComparison.Ordinal)),
                "A normal settings summary exposed an opaque endpoint.");

            settings.SelectedProfileId = A5TestValues.ChatProfileId;
            settings.BeginSelectedProfileEditCommand.Execute(null);
            Assert.IsTrue(string.Equals(settings.ProfileOpaqueEndpoint, chatEndpoint, StringComparison.Ordinal),
                "The chat opaque endpoint was not retained verbatim in its explicit editor.");
            Assert.AreEqual(string.Empty, settings.SecretEntry, "The entry-only secret field was retained after save.");

            var create = new CreateViewModel(runtime)
            {
                ChatPrompt = A5TestValues.ChatPrompt,
            };
            await create.SendChatCommand.ExecuteAsync(null);
            Assert.AreEqual(A5TestValues.ChatResult, create.ChatResponse, "The bound chat response was not surfaced.");
            Assert.AreEqual("Chat completed.", create.ChatStatus, "The Create handler did not report completion.");

            var preview = new PreviewViewModel(runtime)
            {
                ImagePrompt = A5TestValues.ImagePrompt,
                ImageWidth = 64,
                ImageHeight = 64,
            };
            await preview.GenerateImageCommand.ExecuteAsync(null);
            try
            {
                Assert.IsNotNull(preview.PreviewImage, "The base64 image did not decode into a private preview.");
                Assert.AreEqual(1, preview.PreviewImage.PixelSize.Width, "The decoded image width was unexpected.");
                Assert.AreEqual(1, preview.PreviewImage.PixelSize.Height, "The decoded image height was unexpected.");
                Assert.AreEqual("Private image preview ready.", preview.ImageStatus,
                    "The Preview handler did not report a private image.");
            }
            finally
            {
                preview.Dispose();
            }

            Assert.IsTrue(chatRequestWasExact, "The production chat request did not preserve its required method, path, authorization, model, and body.");
            Assert.IsTrue(imageRequestWasExact, "The production image request did not preserve its required method, path, authorization, model, and body.");
            Assert.AreEqual(2, server.RequestCount, "The handlers made an unexpected number of loopback requests.");
        }
        finally
        {
            await runtime.DisposeAsync();
            root.AssertNoPrivateImageSessionDirectories();
        }

        Assert.IsFalse(server.HasResponderFailure, "The scripted loopback responder failed.");
    }

    [TestMethod]
    public async Task ProductionPreviewDownloadsUrlArtifactsWithoutForwardingAuthorization()
    {
        var imageRequestWasExact = false;
        var artifactRequestWasUnauthenticated = false;
        var artifactUrl = string.Empty;
        await using var server = new LoopbackProviderServer((request, _) =>
        {
            if (request.IsTarget(ImageTarget))
            {
                imageRequestWasExact = request.MatchesJsonPost(
                    ImageTarget,
                    "Bearer " + A5TestValues.ImageSecret,
                    A5LoopbackPayloads.IsExactImageBody);
                return ValueTask.FromResult(LoopbackResponse.Json(200, A5LoopbackPayloads.ImageUrlJson(artifactUrl)));
            }

            if (request.IsTarget(ArtifactTarget))
            {
                artifactRequestWasUnauthenticated = request.MatchesUnauthenticatedGet(ArtifactTarget);
                return ValueTask.FromResult(LoopbackResponse.Image(A5LoopbackPayloads.OnePixelPngBytes(), "image/png"));
            }

            return ValueTask.FromResult(LoopbackResponse.Empty(404));
        });
        artifactUrl = server.Endpoint(ArtifactTarget);

        using var root = new A5TemporaryRoot();
        var runtime = root.CreateRuntime();
        try
        {
            A5DesktopSettings.ConfigureTwoProfiles(runtime, server.Endpoint(ChatTarget), server.Endpoint(ImageTarget));
            var preview = new PreviewViewModel(runtime)
            {
                ImagePrompt = A5TestValues.ImagePrompt,
                ImageWidth = 64,
                ImageHeight = 64,
            };
            await preview.GenerateImageCommand.ExecuteAsync(null);
            try
            {
                Assert.IsNotNull(preview.PreviewImage, "The URL image did not decode into a private preview.");
                Assert.AreEqual(1, preview.PreviewImage.PixelSize.Width, "The URL image width was unexpected.");
                Assert.AreEqual(1, preview.PreviewImage.PixelSize.Height, "The URL image height was unexpected.");
            }
            finally
            {
                preview.Dispose();
            }

            Assert.IsTrue(imageRequestWasExact, "The generation request was not exact before the artifact download.");
            Assert.IsTrue(artifactRequestWasUnauthenticated,
                "The private artifact download changed method/path or forwarded generation authorization.");
            Assert.AreEqual(2, server.RequestCount, "The URL preview made an unexpected number of loopback requests.");
        }
        finally
        {
            await runtime.DisposeAsync();
            root.AssertNoPrivateImageSessionDirectories();
        }

        Assert.IsFalse(server.HasResponderFailure, "The scripted loopback responder failed.");
    }

    [TestMethod]
    public async Task ProfilesSecretsAndBindingsPersistAcrossRuntimeRestart()
    {
        var chatRequestWasExact = false;
        await using var server = new LoopbackProviderServer((request, _) =>
        {
            if (request.IsTarget(ChatTarget))
            {
                chatRequestWasExact = request.MatchesJsonPost(
                    ChatTarget,
                    "Bearer " + A5TestValues.ChatSecret,
                    A5LoopbackPayloads.IsExactChatBody);
                return ValueTask.FromResult(LoopbackResponse.Json(200, A5LoopbackPayloads.ChatSuccessJson()));
            }

            return ValueTask.FromResult(LoopbackResponse.Empty(404));
        });

        using var root = new A5TemporaryRoot();
        var chatEndpoint = server.Endpoint(ChatTarget) + "#a5-restart-fragment";
        var first = root.CreateRuntime();
        try
        {
            A5DesktopSettings.ConfigureTwoProfiles(first, chatEndpoint, server.Endpoint(ImageTarget));
        }
        finally
        {
            await first.DisposeAsync();
            root.AssertNoPrivateImageSessionDirectories();
        }

        var restarted = root.CreateRuntime();
        try
        {
            var snapshot = restarted.Settings.Load();
            Assert.AreEqual(2, snapshot.Profiles.Count, "Profiles did not survive the runtime restart.");
            Assert.AreEqual(2, snapshot.Bindings.Count, "Bindings did not survive the runtime restart.");
            Assert.IsTrue(snapshot.Profiles.All(profile => profile.HasSecret), "A persisted secret was not readable after restart.");
            var edit = restarted.Settings.BeginProfileEdit(A5TestValues.ChatProfileId);
            Assert.IsTrue(string.Equals(edit.Profile.OpaqueEndpoint, chatEndpoint, StringComparison.Ordinal),
                "The opaque endpoint was not persisted exactly across restart.");

            var create = new CreateViewModel(restarted)
            {
                ChatPrompt = A5TestValues.ChatPrompt,
            };
            await create.SendChatCommand.ExecuteAsync(null);
            Assert.AreEqual(A5TestValues.ChatResult, create.ChatResponse, "The persisted ChatLlm binding did not make its explicit request.");
            Assert.IsTrue(chatRequestWasExact, "The restarted chat request was not routed to its exact persisted binding.");
        }
        finally
        {
            await restarted.DisposeAsync();
            root.AssertNoPrivateImageSessionDirectories();
        }

        Assert.AreEqual(1, server.RequestCount, "Restart changed the number of request-time network actions.");
        Assert.IsFalse(server.HasResponderFailure, "The scripted loopback responder failed.");
    }

    [TestMethod]
    public async Task ChannelBindingsFailClosedWithoutCrossChannelFallback()
    {
        await using var server = new LoopbackProviderServer((request, _) =>
        {
            if (request.IsTarget(ChatTarget))
            {
                return ValueTask.FromResult(LoopbackResponse.Json(200, A5LoopbackPayloads.ChatSuccessJson()));
            }

            if (request.IsTarget(ImageTarget))
            {
                return ValueTask.FromResult(LoopbackResponse.Json(200, A5LoopbackPayloads.ImageBase64Json()));
            }

            return ValueTask.FromResult(LoopbackResponse.Empty(404));
        });

        using var root = new A5TemporaryRoot();
        var runtime = root.CreateRuntime();
        try
        {
            A5DesktopSettings.ConfigureTwoProfiles(runtime, server.Endpoint(ChatTarget), server.Endpoint(ImageTarget));
            await runtime.Gateway.ChatAsync(CreateChatRequest());
            await runtime.Gateway.GenerateImageAsync(CreateImageRequest());
            var requestsAfterExplicitSuccess = server.RequestCount;

            runtime.Settings.ClearChannelBinding(AiChannel.ChatLlm);
            var chatException = await CaptureChatFailureAsync(runtime);
            Assert.AreEqual(ChatChannelErrorCode.ChannelUnbound, chatException.Code,
                "An unbound ChatLlm route did not fail closed.");
            AssertRedacted(chatException);
            Assert.AreEqual(requestsAfterExplicitSuccess, server.RequestCount,
                "An unbound ChatLlm route attempted a fallback request.");

            runtime.Settings.ClearChannelBinding(AiChannel.ImageGeneration);
            var imageException = await CaptureAiFailureAsync(runtime);
            Assert.AreEqual(AiErrorCode.ChannelUnbound, imageException.Code,
                "An unbound ImageGeneration route did not fail closed.");
            AssertRedacted(imageException);
            Assert.AreEqual(requestsAfterExplicitSuccess, server.RequestCount,
                "An unbound ImageGeneration route attempted a fallback request.");
        }
        finally
        {
            await runtime.DisposeAsync();
            root.AssertNoPrivateImageSessionDirectories();
        }

        Assert.IsFalse(server.HasResponderFailure, "The scripted loopback responder failed.");
    }

    [TestMethod]
    public async Task OpaqueEndpointPersistsVerbatimAndFailsOnlyAtExplicitRequestTime()
    {
        const string opaqueEndpoint = "not-a-http-uri:a5-endpoint-secret-sentinel#opaque-fragment";
        await using var server = new LoopbackProviderServer((_, _) =>
            ValueTask.FromResult(LoopbackResponse.Empty(500)));

        using var root = new A5TemporaryRoot();
        var first = root.CreateRuntime();
        try
        {
            A5DesktopSettings.ConfigureTwoProfiles(first, opaqueEndpoint, server.Endpoint(ImageTarget));
            var editor = new SettingsViewModel(first)
            {
                SelectedProfileId = A5TestValues.ChatProfileId,
            };
            editor.BeginSelectedProfileEditCommand.Execute(null);
            Assert.IsTrue(string.Equals(editor.ProfileOpaqueEndpoint, opaqueEndpoint, StringComparison.Ordinal),
                "Saving an opaque endpoint changed its exact value.");
            Assert.AreEqual(0, server.RequestCount, "Settings persistence performed a network action.");
        }
        finally
        {
            await first.DisposeAsync();
            root.AssertNoPrivateImageSessionDirectories();
        }

        var restarted = root.CreateRuntime();
        try
        {
            var edit = restarted.Settings.BeginProfileEdit(A5TestValues.ChatProfileId);
            Assert.IsTrue(string.Equals(edit.Profile.OpaqueEndpoint, opaqueEndpoint, StringComparison.Ordinal),
                "Restart changed the opaque endpoint value.");

            var create = new CreateViewModel(restarted)
            {
                ChatPrompt = A5TestValues.ChatPrompt,
            };
            await create.SendChatCommand.ExecuteAsync(null);
            Assert.IsTrue(create.ChatStatus.Contains(ChatChannelErrorCode.EndpointUnusable.ToString(), StringComparison.Ordinal),
                "The unusable opaque endpoint did not fail at the explicit Create request.");
            AssertRedacted(create.ChatStatus);
            Assert.AreEqual(0, server.RequestCount, "An unusable opaque endpoint reached the network.");
        }
        finally
        {
            await restarted.DisposeAsync();
            root.AssertNoPrivateImageSessionDirectories();
        }
    }

    [TestMethod]
    public async Task ProductionChatTransportClassifiesFailuresAndRedactsDiagnostics()
    {
        const string rateLimitedTarget = "/a5/chat-rate?opaque=a5-endpoint-secret-sentinel";
        const string timeoutTarget = "/a5/chat-timeout?opaque=a5-endpoint-secret-sentinel";
        const string malformedTarget = "/a5/chat-malformed?opaque=a5-endpoint-secret-sentinel";
        const string oversizeTarget = "/a5/chat-oversize?opaque=a5-endpoint-secret-sentinel";
        await using var server = new LoopbackProviderServer((request, _) =>
        {
            if (request.IsTarget(rateLimitedTarget))
            {
                return ValueTask.FromResult(LoopbackResponse.Empty(429));
            }

            if (request.IsTarget(timeoutTarget))
            {
                return ValueTask.FromResult(LoopbackResponse.Json(
                    200,
                    A5LoopbackPayloads.ChatSuccessJson(),
                    delay: TimeSpan.FromMilliseconds(1500)));
            }

            if (request.IsTarget(malformedTarget))
            {
                return ValueTask.FromResult(LoopbackResponse.Json(200, "{\"choices\":[]}"));
            }

            if (request.IsTarget(oversizeTarget))
            {
                return ValueTask.FromResult(LoopbackResponse.Json(
                    200,
                    "{}",
                    declaredContentLength: ChatChannelLimits.MaximumResponseBytes + 1L));
            }

            return ValueTask.FromResult(LoopbackResponse.Empty(404));
        });

        await AssertChatFailureScenarioAsync(server, rateLimitedTarget, timeoutSeconds: 30, ChatChannelErrorCode.RateLimited);
        await AssertChatFailureScenarioAsync(server, timeoutTarget, timeoutSeconds: 1, ChatChannelErrorCode.TimedOut);
        await AssertChatFailureScenarioAsync(server, malformedTarget, timeoutSeconds: 30, ChatChannelErrorCode.ResponseMalformed);
        await AssertChatFailureScenarioAsync(server, oversizeTarget, timeoutSeconds: 30, ChatChannelErrorCode.ResponseTooLarge);

        Assert.IsFalse(server.HasResponderFailure, "The scripted loopback responder failed.");
    }

    [TestMethod]
    public async Task ProductionImageTransportClassifiesFailuresAndRedactsDiagnostics()
    {
        const string rateLimitedTarget = "/a5/image-rate?opaque=a5-endpoint-secret-sentinel";
        const string timeoutTarget = "/a5/image-timeout?opaque=a5-endpoint-secret-sentinel";
        const string malformedTarget = "/a5/image-malformed?opaque=a5-endpoint-secret-sentinel";
        const string oversizeTarget = "/a5/image-oversize?opaque=a5-endpoint-secret-sentinel";
        const string oversizeArtifactTarget = "/a5/artifact-oversize?opaque=a5-endpoint-secret-sentinel";
        var oversizeArtifactUrl = string.Empty;
        await using var server = new LoopbackProviderServer((request, _) =>
        {
            if (request.IsTarget(rateLimitedTarget))
            {
                return ValueTask.FromResult(LoopbackResponse.Empty(429));
            }

            if (request.IsTarget(timeoutTarget))
            {
                return ValueTask.FromResult(LoopbackResponse.Json(
                    200,
                    A5LoopbackPayloads.ImageBase64Json(),
                    delay: TimeSpan.FromMilliseconds(1500)));
            }

            if (request.IsTarget(malformedTarget))
            {
                return ValueTask.FromResult(LoopbackResponse.Json(200, "{\"data\":[]}"));
            }

            if (request.IsTarget(oversizeTarget))
            {
                return ValueTask.FromResult(LoopbackResponse.Json(200, A5LoopbackPayloads.ImageUrlJson(oversizeArtifactUrl)));
            }

            if (request.IsTarget(oversizeArtifactTarget))
            {
                return ValueTask.FromResult(LoopbackResponse.Image(
                    Array.Empty<byte>(),
                    "image/png",
                    declaredContentLength: ImageArtifactLimits.MaximumImageBytes + 1L));
            }

            return ValueTask.FromResult(LoopbackResponse.Empty(404));
        });
        oversizeArtifactUrl = server.Endpoint(oversizeArtifactTarget);

        await AssertImageFailureScenarioAsync(server, rateLimitedTarget, timeoutSeconds: 30, ImageErrorCode.RateLimited);
        await AssertImageFailureScenarioAsync(server, timeoutTarget, timeoutSeconds: 1, ImageErrorCode.TimedOut);
        await AssertImageFailureScenarioAsync(server, malformedTarget, timeoutSeconds: 30, ImageErrorCode.MalformedResponse);
        await AssertImageFailureScenarioAsync(server, oversizeTarget, timeoutSeconds: 30, ImageErrorCode.ArtifactTooLarge, expectedRequests: 2);

        Assert.IsFalse(server.HasResponderFailure, "The scripted loopback responder failed.");
    }

    [TestMethod]
    public async Task RevokedOrMissingSecretsFailBeforeAnyNetworkAction()
    {
        await using var server = new LoopbackProviderServer((_, _) =>
            ValueTask.FromResult(LoopbackResponse.Empty(500)));

        using var root = new A5TemporaryRoot();
        var runtime = root.CreateRuntime();
        try
        {
            var settings = A5DesktopSettings.ConfigureTwoProfiles(runtime, server.Endpoint(ChatTarget), server.Endpoint(ImageTarget));
            settings.SelectedProfileId = A5TestValues.ChatProfileId;
            settings.RevokeSecretCommand.Execute(null);
            Assert.AreEqual("No secret configured", settings.SecretPresence, "The Chat secret was not revoked.");
            var chatException = await CaptureChatFailureAsync(runtime);
            Assert.AreEqual(ChatChannelErrorCode.SecretUnavailable, chatException.Code,
                "A revoked Chat secret did not fail before transport.");
            AssertRedacted(chatException);
            Assert.AreEqual(0, server.RequestCount, "A revoked Chat secret reached the network.");

            settings.SelectedProfileId = A5TestValues.ImageProfileId;
            settings.RevokeSecretCommand.Execute(null);
            Assert.AreEqual("No secret configured", settings.SecretPresence, "The Image secret was not revoked.");
            var imageException = await CaptureAiFailureAsync(runtime);
            Assert.AreEqual(AiErrorCode.SecretUnavailable, imageException.Code,
                "A revoked Image secret did not fail before transport.");
            AssertRedacted(imageException);
            Assert.AreEqual(0, server.RequestCount, "A revoked Image secret reached the network.");
        }
        finally
        {
            await runtime.DisposeAsync();
            root.AssertNoPrivateImageSessionDirectories();
        }
    }

    private static async Task AssertChatFailureScenarioAsync(
        LoopbackProviderServer server,
        string target,
        int timeoutSeconds,
        ChatChannelErrorCode expectedCode)
    {
        using var root = new A5TemporaryRoot();
        var runtime = root.CreateRuntime();
        try
        {
            A5DesktopSettings.ConfigureTwoProfiles(runtime, server.Endpoint(target), server.Endpoint(ImageTarget), timeoutSeconds);
            var before = server.RequestCount;
            var exception = await CaptureChatFailureAsync(runtime);
            Assert.AreEqual(expectedCode, exception.Code, "The production ChatLlm handler returned the wrong failure category.");
            AssertRedacted(exception);
            Assert.AreEqual(before + 1, server.RequestCount, "The failed ChatLlm request did not remain a single selected-route request.");
        }
        finally
        {
            await runtime.DisposeAsync();
            root.AssertNoPrivateImageSessionDirectories();
        }
    }

    private static async Task AssertImageFailureScenarioAsync(
        LoopbackProviderServer server,
        string target,
        int timeoutSeconds,
        ImageErrorCode expectedCode,
        int expectedRequests = 1)
    {
        using var root = new A5TemporaryRoot();
        var runtime = root.CreateRuntime();
        try
        {
            A5DesktopSettings.ConfigureTwoProfiles(runtime, server.Endpoint(ChatTarget), server.Endpoint(target), timeoutSeconds);
            var before = server.RequestCount;
            var exception = await CaptureImageFailureAsync(runtime);
            Assert.AreEqual(expectedCode, exception.Code, "The production ImageGeneration handler returned the wrong failure category.");
            AssertRedacted(exception);
            Assert.AreEqual(before + expectedRequests, server.RequestCount,
                "The failed ImageGeneration request did not remain on its selected loopback route.");
        }
        finally
        {
            await runtime.DisposeAsync();
            root.AssertNoPrivateImageSessionDirectories();
        }
    }

    private static ChatRequest CreateChatRequest() => new(
        "a5-chat-correlation",
        [new ChatMessage(ChatRole.User, A5TestValues.ChatPrompt)]);

    private static ImageGenerationRequest CreateImageRequest() => new(
        "a5-image-correlation",
        A5TestValues.ImagePrompt,
        64,
        64);

    private static async Task<ChatChannelException> CaptureChatFailureAsync(ProviderDesktopRuntime runtime)
    {
        try
        {
            await runtime.Gateway.ChatAsync(CreateChatRequest());
        }
        catch (ChatChannelException exception)
        {
            return exception;
        }

        Assert.Fail("The ChatLlm action unexpectedly succeeded.");
        throw new InvalidOperationException();
    }

    private static async Task<ImageGatewayException> CaptureImageFailureAsync(ProviderDesktopRuntime runtime)
    {
        try
        {
            await runtime.Gateway.GenerateImageAsync(CreateImageRequest());
        }
        catch (ImageGatewayException exception)
        {
            return exception;
        }

        Assert.Fail("The ImageGeneration action unexpectedly succeeded.");
        throw new InvalidOperationException();
    }

    private static async Task<AiGatewayException> CaptureAiFailureAsync(ProviderDesktopRuntime runtime)
    {
        try
        {
            await runtime.Gateway.GenerateImageAsync(CreateImageRequest());
        }
        catch (AiGatewayException exception)
        {
            return exception;
        }

        Assert.Fail("The ImageGeneration action unexpectedly succeeded.");
        throw new InvalidOperationException();
    }

    private static void AssertRedacted(Exception exception) => AssertRedacted(exception.ToString());

    private static void AssertRedacted(string diagnostic)
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(diagnostic), "Expected a stable diagnostic.");
        foreach (var sensitiveValue in new[]
        {
            A5TestValues.ChatSecret,
            A5TestValues.ImageSecret,
            A5TestValues.ChatPrompt,
            A5TestValues.ImagePrompt,
            A5TestValues.EndpointMarker,
        })
        {
            Assert.IsFalse(diagnostic.Contains(sensitiveValue, StringComparison.Ordinal),
                "A diagnostic leaked sensitive A5 data.");
        }
    }
}
