using System.Net;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Chat;
using VFXComposer.AI.Providers.Chat;

namespace VFXComposer.AI.Tests.Chat;

[TestClass]
public sealed class ChatChannelGatewayTests
{
    [TestMethod]
    public async Task ExplicitPromptAdmitsUnknownHealthAndRecordsVerifiedFromItsOwnSuccessfulResult()
    {
        using var fixture = new ChatTestFixture();
        fixture.Health.Clear();
        var handler = new RecordingHandler((_, _) => Task.FromResult(ChatTestResponses.Success(
            ChatProtocolIds.OpenAiChatCompletionsV1)));
        using var gateway = fixture.CreateGateway(handler);

        var result = await gateway.CompleteAsync(Request());

        Assert.AreEqual("synthetic-result", result.Text);
        Assert.AreEqual(1, handler.RequestCount);
        var configuration = fixture.Store.Load().Configuration;
        var observed = fixture.Health.Get("profile-primary", "chat-main", AiChannel.ChatLlm);
        Assert.IsNotNull(observed);
        Assert.AreEqual(ProviderHealthState.Verified, observed.State);
        Assert.AreEqual(configuration.Fingerprint, observed.ConfigurationFingerprint);
    }

    [TestMethod]
    public async Task ExplicitPromptRecordsUnhealthyFromItsOwnUpstreamFailureWithoutFallback()
    {
        using var fixture = new ChatTestFixture();
        fixture.Health.Clear();
        var handler = new RecordingHandler((_, _) => Task.FromResult(
            ChatTestResponses.Json(HttpStatusCode.ServiceUnavailable, "{}")));
        using var gateway = fixture.CreateGateway(handler);

        try
        {
            await gateway.CompleteAsync(Request());
            Assert.Fail("The selected upstream failure must remain fail-closed.");
        }
        catch (ChatChannelException exception)
        {
            Assert.AreEqual(ChatChannelErrorCode.UpstreamUnavailable, exception.Code);
        }

        Assert.AreEqual(1, handler.RequestCount);
        var observed = fixture.Health.Get("profile-primary", "chat-main", AiChannel.ChatLlm);
        Assert.IsNotNull(observed);
        Assert.AreEqual(ProviderHealthState.Unhealthy, observed.State);
    }

    [TestMethod]
    [DataRow(ChatProtocolIds.OpenAiChatCompletionsV1, "https://synthetic.invalid/a2/openai-chat?opaque=one#fragment")]
    [DataRow(ChatProtocolIds.OpenAiResponsesV1, "https://synthetic.invalid/a2/openai-responses?opaque=two#fragment")]
    [DataRow(ChatProtocolIds.AnthropicMessagesV1, "https://synthetic.invalid/a2/anthropic-messages?opaque=three#fragment")]
    [DataRow(ChatProtocolIds.GeminiGenerateContentV1, "https://synthetic.invalid/v1beta/models/chat-model-primary:generateContent?opaque=four#fragment")]
    [DataRow(ChatProtocolIds.OpenAiCompatibleV1, "https://synthetic.invalid/a2/openai-compatible?opaque=five#fragment")]
    public async Task EveryExplicitProtocolUsesTheExactEndpointHeaderBodyModelAndParser(string protocolId, string endpoint)
    {
        using var fixture = new ChatTestFixture(protocolId, endpoint);
        var handler = new RecordingHandler((_, _) => Task.FromResult(ChatTestResponses.Success(protocolId)));
        using var gateway = fixture.CreateGateway(handler);

        var result = await gateway.CompleteAsync(Request()).ConfigureAwait(false);

        Assert.AreEqual("synthetic-result", result.Text);
        Assert.IsNotNull(result.TokenUsage);
        Assert.AreEqual(3, result.TokenUsage.InputTokens);
        Assert.AreEqual(5, result.TokenUsage.OutputTokens);
        Assert.AreEqual(8, result.TokenUsage.TotalTokens);
        Assert.AreEqual(1, handler.RequestCount);

        var capture = handler.Requests.Single();
        Assert.AreEqual("POST", capture.Method);
        Assert.AreEqual(endpoint, capture.Endpoint, "The adapter must use the complete user endpoint without appending or rewriting it.");
        Assert.AreEqual("application/json", capture.ContentType);
        AssertProtocolHeaders(protocolId, capture);

        using var document = JsonDocument.Parse(capture.Body);
        var body = document.RootElement;
        switch (protocolId)
        {
            case ChatProtocolIds.OpenAiChatCompletionsV1:
            case ChatProtocolIds.OpenAiCompatibleV1:
                Assert.AreEqual("chat-model-primary", body.GetProperty("model").GetString());
                Assert.AreEqual("system", body.GetProperty("messages")[0].GetProperty("role").GetString());
                Assert.AreEqual("synthetic user prompt", body.GetProperty("messages")[1].GetProperty("content").GetString());
                break;
            case ChatProtocolIds.OpenAiResponsesV1:
                Assert.AreEqual("chat-model-primary", body.GetProperty("model").GetString());
                Assert.AreEqual("system", body.GetProperty("input")[0].GetProperty("role").GetString());
                Assert.AreEqual("input_text", body.GetProperty("input")[1].GetProperty("content")[0].GetProperty("type").GetString());
                Assert.AreEqual("synthetic user prompt", body.GetProperty("input")[1].GetProperty("content")[0].GetProperty("text").GetString());
                break;
            case ChatProtocolIds.AnthropicMessagesV1:
                Assert.AreEqual("chat-model-primary", body.GetProperty("model").GetString());
                Assert.AreEqual(1024, body.GetProperty("max_tokens").GetInt32());
                Assert.AreEqual("synthetic system prompt", body.GetProperty("system")[0].GetProperty("text").GetString());
                Assert.AreEqual("user", body.GetProperty("messages")[0].GetProperty("role").GetString());
                break;
            case ChatProtocolIds.GeminiGenerateContentV1:
                Assert.IsFalse(body.TryGetProperty("model", out _), "Gemini chooses the model from the complete stored endpoint; A2 must not append or override it.");
                Assert.AreEqual("synthetic system prompt", body.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
                Assert.AreEqual("user", body.GetProperty("contents")[0].GetProperty("role").GetString());
                Assert.AreEqual("synthetic user prompt", body.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString());
                break;
            default:
                Assert.Fail("Unexpected protocol fixture.");
                break;
        }
    }

    [TestMethod]
    public async Task OptionalStructuredOutputIsSentAndReturnedAsTypedJson()
    {
        using var fixture = new ChatTestFixture();
        var handler = new RecordingHandler((_, _) => Task.FromResult(ChatTestResponses.Json(
            HttpStatusCode.OK,
            """{"choices":[{"message":{"content":"{\"answer\":\"yes\"}"}}],"usage":{"prompt_tokens":1,"completion_tokens":2,"total_tokens":3}}""")));
        using var gateway = fixture.CreateGateway(handler);
        using var schemaDocument = JsonDocument.Parse("""{"type":"object","properties":{"answer":{"type":"string"}},"required":["answer"]}""");
        var structuredOutput = new ChatStructuredOutput("result_shape", schemaDocument.RootElement);
        var request = new ChatChannelRequest(
            "correlation-structured",
            [new ChatChannelMessage(ChatRole.User, "return an object")],
            structuredOutput);

        var result = await gateway.CompleteAsync(request).ConfigureAwait(false);

        Assert.AreEqual("{\"answer\":\"yes\"}", result.Text);
        Assert.IsTrue(result.StructuredOutput.HasValue);
        Assert.AreEqual("yes", result.StructuredOutput.Value.GetProperty("answer").GetString());
        using var bodyDocument = JsonDocument.Parse(handler.Requests.Single().Body);
        var responseFormat = bodyDocument.RootElement.GetProperty("response_format");
        Assert.AreEqual("json_schema", responseFormat.GetProperty("type").GetString());
        Assert.AreEqual("result_shape", responseFormat.GetProperty("json_schema").GetProperty("name").GetString());
        Assert.AreEqual("object", responseFormat.GetProperty("json_schema").GetProperty("schema").GetProperty("type").GetString());
    }

    [TestMethod]
    public async Task OpaqueUnusableEndpointFailsRedactedWithoutMutatingConfigurationOrSending()
    {
        const string endpoint = "not a uri; opaque=user-info?token=synthetic-endpoint-token#fragment";
        const string secret = "synthetic-secret-not-to-leak";
        const string prompt = "synthetic prompt not to leak";
        using var fixture = new ChatTestFixture(endpoint: endpoint, secret: secret);
        var before = fixture.Store.Load().Configuration.Settings;
        var handler = new RecordingHandler((_, _) => throw new AssertFailedException("An unusable endpoint must not be sent."));
        using var gateway = fixture.CreateGateway(handler);

        var exception = await ThrowsAsync(
            ChatChannelErrorCode.EndpointUnusable,
            () => gateway.CompleteAsync(new ChatChannelRequest(
                "correlation-opaque",
                [new ChatChannelMessage(ChatRole.User, prompt)])).AsTask()).ConfigureAwait(false);

        Assert.AreEqual(0, handler.RequestCount);
        var after = fixture.Store.Load().Configuration.Settings;
        Assert.AreEqual(before.Revision, after.Revision);
        Assert.AreEqual(endpoint, after.Profiles.Single().Endpoint.Value);
        AssertNotLeaked(exception.ToString(), endpoint, secret, prompt, "synthetic-endpoint-token");
    }

    [TestMethod]
    public async Task CallerCancellationIsTypedAndNeverTriggersFallback()
    {
        using var fixture = new ChatTestFixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler(async (_, cancellationToken) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            throw new AssertFailedException("Cancellation must interrupt the test handler.");
        });
        using var gateway = fixture.CreateGateway(handler);
        using var cancellation = new CancellationTokenSource();

        var pending = gateway.CompleteAsync(Request(), cancellation.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        cancellation.Cancel();
        var exception = await ThrowsAsync(ChatChannelErrorCode.Cancelled, () => pending).ConfigureAwait(false);

        Assert.AreEqual(1, handler.RequestCount);
        Assert.IsFalse(exception.Retryable);
    }

    [TestMethod]
    public async Task BoundProfileTimeoutIsTyped()
    {
        using var fixture = new ChatTestFixture(timeoutSeconds: 1);
        var handler = new RecordingHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            throw new AssertFailedException("The configured timeout must interrupt the test handler.");
        });
        using var gateway = fixture.CreateGateway(handler);

        var exception = await ThrowsAsync(
            ChatChannelErrorCode.TimedOut,
            () => gateway.CompleteAsync(Request()).AsTask()).ConfigureAwait(false);

        Assert.AreEqual(1, handler.RequestCount);
        Assert.IsFalse(exception.Retryable);
    }

    [TestMethod]
    public async Task NetworkFailureIsTypedRedactedAndDoesNotRetryAnotherRoute()
    {
        const string endpoint = "https://synthetic.invalid/a2/network?token=synthetic-endpoint-token";
        const string prompt = "synthetic-network-prompt";
        using var fixture = new ChatTestFixture(endpoint: endpoint, secret: "synthetic-network-secret");
        var handler = new RecordingHandler((_, _) => throw new HttpRequestException(
            "synthetic transport detail that must not escape: " + endpoint));
        using var gateway = fixture.CreateGateway(handler);

        var exception = await ThrowsAsync(
            ChatChannelErrorCode.TransportFailed,
            () => gateway.CompleteAsync(new ChatChannelRequest(
                "correlation-network",
                [new ChatChannelMessage(ChatRole.User, prompt)])).AsTask()).ConfigureAwait(false);

        Assert.IsTrue(exception.Retryable);
        Assert.AreEqual(1, handler.RequestCount);
        AssertNotLeaked(exception.Message, exception.ToString(), endpoint, prompt, "synthetic-endpoint-token");
    }

    [TestMethod]
    [DataRow(401, ChatChannelErrorCode.AuthenticationFailed, false)]
    [DataRow(403, ChatChannelErrorCode.AuthenticationFailed, false)]
    [DataRow(429, ChatChannelErrorCode.RateLimited, true)]
    [DataRow(500, ChatChannelErrorCode.UpstreamUnavailable, true)]
    public async Task UpstreamStatusFailuresAreStableAndRedacted(int statusCode, ChatChannelErrorCode expected, bool retryable)
    {
        const string endpoint = "https://user:synthetic-user-info@synthetic.invalid/a2/status?synthetic-query=token#fragment";
        const string secret = "synthetic-status-secret";
        const string prompt = "synthetic-status-prompt";
        const string rawResponse = "synthetic raw response must not escape";
        using var fixture = new ChatTestFixture(endpoint: endpoint, secret: secret);
        var handler = new RecordingHandler((_, _) => Task.FromResult(ChatTestResponses.Json((HttpStatusCode)statusCode, rawResponse)));
        using var gateway = fixture.CreateGateway(handler);

        var exception = await ThrowsAsync(
            expected,
            () => gateway.CompleteAsync(new ChatChannelRequest(
                "correlation-status",
                [new ChatChannelMessage(ChatRole.User, prompt)])).AsTask()).ConfigureAwait(false);

        Assert.AreEqual(retryable, exception.Retryable);
        AssertNotLeaked(exception.Message, exception.ToString(), endpoint, secret, prompt, rawResponse, "synthetic-user-info", "synthetic-query");
    }

    [TestMethod]
    [DataRow(300)]
    [DataRow(302)]
    [DataRow(399)]
    public async Task RedirectStatusesAreRejectedWithoutFollowingLocationOrForwardingAuthorization(int statusCode)
    {
        const string endpoint = "https://synthetic.invalid/a2/redirect?synthetic-query=token";
        const string redirectedEndpoint = "https://redirected.invalid/a2/target?redirect-token=synthetic-location-token";
        const string secret = "synthetic-redirect-secret";
        const string prompt = "synthetic-redirect-prompt";
        const string rawResponse = "synthetic redirect response must not escape";
        using var fixture = new ChatTestFixture(endpoint: endpoint, secret: secret);
        var handler = new RecordingHandler((request, _) =>
        {
            Assert.AreEqual(endpoint, request.RequestUri?.OriginalString);
            Assert.AreEqual("Bearer " + secret, request.Headers.Authorization?.ToString());
            var response = ChatTestResponses.Json((HttpStatusCode)statusCode, rawResponse);
            response.Headers.Location = new Uri(redirectedEndpoint);
            return Task.FromResult(response);
        });
        using var gateway = fixture.CreateGateway(handler);

        var exception = await ThrowsAsync(
            ChatChannelErrorCode.UpstreamRejected,
            () => gateway.CompleteAsync(new ChatChannelRequest(
                "correlation-redirect",
                [new ChatChannelMessage(ChatRole.User, prompt)])).AsTask()).ConfigureAwait(false);

        Assert.IsFalse(exception.Retryable);
        Assert.AreEqual(1, handler.RequestCount, "A redirect must not trigger a second request.");
        var captures = handler.Requests;
        Assert.AreEqual(1, captures.Count);
        Assert.AreEqual(endpoint, captures[0].Endpoint);
        Assert.AreNotEqual(redirectedEndpoint, captures[0].Endpoint);
        Assert.AreEqual("Bearer " + secret, captures[0].Header("Authorization"));
        Assert.IsFalse(captures.Skip(1).Any(static capture => capture.Headers.ContainsKey("Authorization")));
        AssertNotLeaked(
            exception.Message,
            exception.ToString(),
            endpoint,
            redirectedEndpoint,
            secret,
            prompt,
            rawResponse,
            "synthetic-query",
            "synthetic-location-token");
    }

    [TestMethod]
    public async Task MalformedProviderJsonAndMissingRequiredFieldsAreRejectedWhileUnknownFieldsAreAllowed()
    {
        using var fixture = new ChatTestFixture();
        var handler = new RecordingHandler((_, _) => Task.FromResult(ChatTestResponses.Json(
            HttpStatusCode.OK,
            """{"unknown":"allowed","choices":[{"message":{"content":42}}]}""")));
        using var gateway = fixture.CreateGateway(handler);

        await ThrowsAsync(ChatChannelErrorCode.ResponseMalformed, () => gateway.CompleteAsync(Request()).AsTask()).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ResponseAndRequestSizeLimitsFailBeforeUnboundedProcessingOrSending()
    {
        using (var fixture = new ChatTestFixture())
        {
            var oversized = new byte[ChatChannelLimits.MaximumResponseBytes + 1];
            var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(oversized),
            }));
            using var gateway = fixture.CreateGateway(handler);

            await ThrowsAsync(ChatChannelErrorCode.ResponseTooLarge, () => gateway.CompleteAsync(Request()).AsTask()).ConfigureAwait(false);
        }

        using (var fixture = new ChatTestFixture())
        {
            var handler = new RecordingHandler((_, _) => throw new AssertFailedException("An oversized request must not be sent."));
            using var gateway = fixture.CreateGateway(handler);
            var maximumMessage = new string('x', 16 * 1024);
            var messages = Enumerable.Range(0, ChatChannelLimits.MaximumMessages)
                .Select(_ => new ChatChannelMessage(ChatRole.User, maximumMessage))
                .ToArray();
            var oversizedRequest = new ChatChannelRequest("correlation-request-limit", messages);

            await ThrowsAsync(ChatChannelErrorCode.PayloadTooLarge, () => gateway.CompleteAsync(oversizedRequest).AsTask()).ConfigureAwait(false);
            Assert.AreEqual(0, handler.RequestCount);
        }
    }

    [TestMethod]
    public async Task UnsupportedBoundProtocolDoesNotSearchTheAvailableFallbackProfile()
    {
        using var fixture = new ChatTestFixture();
        const string unsupportedProtocol = "unsupported-chat-v1";
        var primary = ChatTestFixture.Profile(
            "profile-primary",
            "chat-main",
            "secret-primary",
            unsupportedProtocol,
            "https://synthetic.invalid/a2/unsupported",
            "chat-model-primary");
        var fallback = ChatTestFixture.Profile(
            "profile-fallback",
            "chat-fallback",
            "secret-fallback",
            ChatProtocolIds.OpenAiChatCompletionsV1,
            "https://synthetic.invalid/a2/fallback",
            "chat-model-fallback");
        fixture.SaveSettings(
            new AiProviderSettings(
                2,
                [primary, fallback],
                [new ChannelBinding(AiChannel.ChatLlm, "profile-primary", "chat-main", "chat-model-primary")]),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["profile-primary"] = "synthetic-primary-secret",
                ["profile-fallback"] = "synthetic-fallback-secret",
            });
        var handler = new RecordingHandler((_, _) => throw new AssertFailedException("A2 must not send a fallback request."));
        using var gateway = fixture.CreateGateway(handler);

        await ThrowsAsync(ChatChannelErrorCode.ProtocolUnsupported, () => gateway.CompleteAsync(Request()).AsTask()).ConfigureAwait(false);
        Assert.AreEqual(0, handler.RequestCount);
    }

    [TestMethod]
    public async Task InFlightRequestPinsTheResolvedBindingDespiteConcurrentConfigurationSave()
    {
        const string oldEndpoint = "https://synthetic.invalid/a2/pinned-old";
        const string newEndpoint = "https://synthetic.invalid/a2/pinned-new";
        using var fixture = new ChatTestFixture(endpoint: oldEndpoint, model: "chat-model-old");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler(async (_, cancellationToken) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return ChatTestResponses.Success(ChatProtocolIds.OpenAiChatCompletionsV1);
        });
        using var gateway = fixture.CreateGateway(handler);

        var pending = gateway.CompleteAsync(Request()).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        fixture.SaveConfiguration(
            2,
            ChatProtocolIds.OpenAiChatCompletionsV1,
            newEndpoint,
            "chat-model-new");
        release.TrySetResult();

        var result = await pending.ConfigureAwait(false);
        Assert.AreEqual("synthetic-result", result.Text);
        var capture = handler.Requests.Single();
        Assert.AreEqual(oldEndpoint, capture.Endpoint);
        using var body = JsonDocument.Parse(capture.Body);
        Assert.AreEqual("chat-model-old", body.RootElement.GetProperty("model").GetString());
    }

    [TestMethod]
    public async Task AuthenticationIsAppliedPerRequestAndLegacyChatCallsUseTheSameChannel()
    {
        using var fixture = new ChatTestFixture(secret: "synthetic-secret-one");
        var handler = new RecordingHandler((_, _) => Task.FromResult(ChatTestResponses.Success(ChatProtocolIds.OpenAiChatCompletionsV1)));
        using var gateway = fixture.CreateGateway(handler);

        var first = await gateway.ChatAsync(new ChatRequest(
            "correlation-legacy",
            [new ChatMessage(ChatRole.User, "legacy synthetic prompt")])).ConfigureAwait(false);
        fixture.Secrets.SaveSecret("profile-primary", new SecretRef("secret-primary"), "synthetic-secret-two".AsSpan());
        var second = await gateway.CompleteAsync(Request("correlation-second")).ConfigureAwait(false);

        Assert.AreEqual("synthetic-result", first.Text);
        Assert.AreEqual("synthetic-result", second.Text);
        var captures = handler.Requests;
        Assert.AreEqual(2, captures.Count);
        Assert.AreEqual("Bearer synthetic-secret-one", captures[0].Header("Authorization"));
        Assert.AreEqual("Bearer synthetic-secret-two", captures[1].Header("Authorization"));
        Assert.IsFalse(captures[0].Headers.ContainsKey("x-api-key"));
        Assert.IsFalse(captures[1].Headers.ContainsKey("x-goog-api-key"));
    }

    private static ChatChannelRequest Request(string correlationId = "correlation-a2") => new(
        correlationId,
        [
            new ChatChannelMessage(ChatRole.System, "synthetic system prompt"),
            new ChatChannelMessage(ChatRole.User, "synthetic user prompt"),
            new ChatChannelMessage(ChatRole.Assistant, "synthetic assistant history"),
        ]);

    private static void AssertProtocolHeaders(string protocolId, ChatRequestCapture capture)
    {
        switch (protocolId)
        {
            case ChatProtocolIds.OpenAiChatCompletionsV1:
            case ChatProtocolIds.OpenAiResponsesV1:
            case ChatProtocolIds.OpenAiCompatibleV1:
                Assert.AreEqual("Bearer synthetic-a2-secret", capture.Header("Authorization"));
                Assert.IsFalse(capture.Headers.ContainsKey("x-api-key"));
                Assert.IsFalse(capture.Headers.ContainsKey("x-goog-api-key"));
                break;
            case ChatProtocolIds.AnthropicMessagesV1:
                Assert.AreEqual("synthetic-a2-secret", capture.Header("x-api-key"));
                Assert.AreEqual("2023-06-01", capture.Header("anthropic-version"));
                Assert.IsFalse(capture.Headers.ContainsKey("Authorization"));
                break;
            case ChatProtocolIds.GeminiGenerateContentV1:
                Assert.AreEqual("synthetic-a2-secret", capture.Header("x-goog-api-key"));
                Assert.IsFalse(capture.Headers.ContainsKey("Authorization"));
                Assert.IsFalse(capture.Headers.ContainsKey("x-api-key"));
                break;
            default:
                Assert.Fail("Unexpected protocol fixture.");
                break;
        }
    }

    private static async Task<ChatChannelException> ThrowsAsync(ChatChannelErrorCode expected, Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (ChatChannelException exception)
        {
            Assert.AreEqual(expected, exception.Code);
            return exception;
        }

        Assert.Fail("Expected a ChatChannelException.");
        throw new InvalidOperationException("Unreachable.");
    }

    private static void AssertNotLeaked(string value, params string[] sensitiveValues)
    {
        foreach (var sensitive in sensitiveValues)
        {
            Assert.IsFalse(value.Contains(sensitive, StringComparison.Ordinal), "Sensitive test material escaped a redacted surface.");
        }
    }
}
