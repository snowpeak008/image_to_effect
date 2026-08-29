using Avalonia;
using System.Buffers.Binary;
using System.IO.Compression;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Desktop;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.Desktop.Services;
using VFXComposer.Desktop.ViewModels;
using VFXComposer.Desktop;

namespace VFXComposer.Desktop.Tests;

[TestClass]
public sealed class AiDesktopIntegrationTests
{
    private const string RawEndpoint = "https://user:synthetic-secret@example.invalid/complete?token=synthetic-query#fragment";

    [ClassInitialize]
    public static void InitializeAvalonia(TestContext _) =>
        AppBuilder.Configure<App>().UsePlatformDetect().SetupWithoutStarting();

    [TestMethod]
    public void StartupSaveAndCreateSettingsPreviewNavigationDoNotCallEitherGateway()
    {
        var runtime = new FakeDesktopRuntime();
        var shell = MainWindowViewModel.CreateDisconnected(aiRuntime: runtime);

        Assert.IsTrue(shell.TryNavigate("create"));
        Assert.IsTrue(shell.TryNavigate("settings"));
        shell.SettingsPage.BeginNewProfileCommand.Execute(null);
        shell.SettingsPage.ProfileId = "profile-one";
        shell.SettingsPage.ProfileDisplayName = "Synthetic profile";
        shell.SettingsPage.ProfileOpaqueEndpoint = RawEndpoint;
        shell.SettingsPage.ChatCapabilityId = "chat-one";
        shell.SettingsPage.ChatModelId = "chat-model-one";
        shell.SettingsPage.SecretEntry = "synthetic-secret";
        shell.SettingsPage.SaveProfileCommand.Execute(null);
        Assert.IsTrue(shell.TryNavigate("preview"));

        Assert.AreEqual(0, runtime.ChatCalls);
        Assert.AreEqual(0, runtime.ImageCalls);
        Assert.AreEqual(0, runtime.OpenArtifactCalls);
        Assert.AreEqual(0, runtime.RecipeGenerateCalls);
        Assert.AreEqual(string.Empty, shell.SettingsPage.SecretEntry);
        Assert.AreEqual("synthetic-secret", runtime.Settings.LastSecretEntry);
    }

    [TestMethod]
    public async Task CreateSendsOnlyAnExplicitChatPromptThroughTheFakeGateway()
    {
        var runtime = new FakeDesktopRuntime();
        var viewModel = new CreateViewModel(runtime)
        {
            ChatPrompt = "synthetic chat prompt",
        };

        await viewModel.SendChatCommand.ExecuteAsync(null);

        Assert.AreEqual(1, runtime.ChatCalls);
        Assert.AreEqual(0, runtime.ImageCalls);
        Assert.IsNotNull(runtime.LastChatRequest);
        Assert.AreEqual(1, runtime.LastChatRequest.Messages.Count);
        Assert.AreEqual(ChatRole.User, runtime.LastChatRequest.Messages[0].Role);
        Assert.AreEqual("synthetic response", viewModel.ChatResponse);
        Assert.AreEqual("Chat completed.", viewModel.ChatStatus);
    }

    [TestMethod]
    public async Task ChatAndImageFailuresRemainIsolated()
    {
        var runtime = new FakeDesktopRuntime
        {
            ThrowChatFailure = true,
            ThrowImageFailure = true,
        };
        var create = new CreateViewModel(runtime)
        {
            ChatPrompt = "synthetic chat prompt",
        };
        var preview = new PreviewViewModel(runtime)
        {
            ImagePrompt = "synthetic image prompt",
        };

        await create.SendChatCommand.ExecuteAsync(null);
        var chatStatus = create.ChatStatus;
        await preview.GenerateImageCommand.ExecuteAsync(null);

        Assert.AreEqual(1, runtime.ChatCalls);
        Assert.AreEqual(1, runtime.ImageCalls);
        Assert.AreEqual(chatStatus, create.ChatStatus);
        Assert.IsTrue(chatStatus.Contains("ConfigurationUnavailable", StringComparison.Ordinal));
        Assert.IsTrue(preview.ImageStatus.Contains("NetworkFailure", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task PreviewDecodesOnlyThePrivateStreamAndClosesItImmediately()
    {
        var runtime = new FakeDesktopRuntime();
        var preview = new PreviewViewModel(runtime)
        {
            ImagePrompt = "synthetic image prompt",
        };

        await preview.GenerateImageCommand.ExecuteAsync(null);

        Assert.AreEqual(1, runtime.ImageCalls);
        Assert.AreEqual(1, runtime.OpenArtifactCalls);
        Assert.IsNotNull(runtime.LastOpenedStream);
        Assert.IsTrue(runtime.LastOpenedStream.WasDisposed);
        Assert.IsNotNull(preview.PreviewImage, preview.ImageStatus);
        preview.Dispose();
        Assert.IsNull(preview.PreviewImage);
    }

    [TestMethod]
    public void SettingsShowOnlyRedactedSummariesUntilAnExplicitEditAndNeverEchoSecrets()
    {
        var runtime = new FakeDesktopRuntime();
        runtime.Settings.SetExistingProfile();
        var viewModel = new SettingsViewModel(runtime);

        var summary = viewModel.Profiles.Single();
        Assert.IsFalse(summary.EndpointSummary.Contains("synthetic-secret", StringComparison.Ordinal));
        Assert.IsFalse(summary.EndpointSummary.Contains("synthetic-query", StringComparison.Ordinal));
        Assert.AreEqual("<endpoint redacted>", summary.EndpointSummary);

        viewModel.SelectedProfileId = "profile-one";
        viewModel.BeginSelectedProfileEditCommand.Execute(null);
        Assert.AreEqual(RawEndpoint, viewModel.ProfileOpaqueEndpoint);
        viewModel.SecretEntry = "replacement-secret";
        viewModel.SaveProfileCommand.Execute(null);

        Assert.AreEqual(string.Empty, viewModel.SecretEntry);
        Assert.AreEqual("replacement-secret", runtime.Settings.LastSecretEntry);
        Assert.AreEqual(string.Empty, viewModel.ProfileOpaqueEndpoint);
        Assert.AreEqual(0, runtime.ChatCalls);
        Assert.AreEqual(0, runtime.ImageCalls);
    }

    [TestMethod]
    public void SettingsExplicitSecretRevokeClearsEntryAndLeavesTheSelectedProfileFailClosed()
    {
        var runtime = new FakeDesktopRuntime();
        runtime.Settings.SetExistingProfile();
        var viewModel = new SettingsViewModel(runtime)
        {
            SelectedProfileId = "profile-one",
            SecretEntry = "transient-secret-entry",
        };

        viewModel.RevokeSecretCommand.Execute(null);

        Assert.AreEqual("profile-one", runtime.Settings.LastRevokedProfileId);
        Assert.AreEqual(string.Empty, viewModel.SecretEntry);
        Assert.AreEqual("No secret configured", viewModel.SecretPresence);
        Assert.IsFalse(viewModel.Profiles.Single().HasSecret);
        Assert.IsTrue(viewModel.ProfileStatus.Contains("fail-closed", StringComparison.Ordinal));
        Assert.AreEqual(0, runtime.ChatCalls);
        Assert.AreEqual(0, runtime.ImageCalls);
    }

    [TestMethod]
    public async Task MainWindowDisposesTheAiRuntimeAndPreviewResources()
    {
        var runtime = new FakeDesktopRuntime();
        var shell = MainWindowViewModel.CreateDisconnected(aiRuntime: runtime);

        await shell.DisposeAsync();

        Assert.AreEqual(1, runtime.DisposeCalls);
    }

    [TestMethod]
    public async Task DecoderClosesTheStreamWhenBitmapDecodingFails()
    {
        var runtime = new FakeDesktopRuntime
        {
            ArtifactBytes = [0x01, 0x02, 0x03],
        };

        try
        {
            await PrivateImagePreviewDecoder.DecodeAsync(runtime, "img-preview");
            Assert.Fail("Invalid private image bytes must not decode successfully.");
        }
        catch (Exception)
        {
            // The decoder deliberately does not expose provider error detail; closure is asserted below.
        }

        Assert.IsNotNull(runtime.LastOpenedStream);
        Assert.IsTrue(runtime.LastOpenedStream.WasDisposed);
    }

    private sealed class FakeDesktopRuntime : IAiDesktopRuntime, IAiGateway, IRecipeGenerationChannel, IRecipeDraftStore
    {
        private static readonly byte[] ValidPng = CreateValidPng();

        public FakeDesktopRuntime()
        {
            Settings = new FakeDesktopSettings();
        }

        public IAiGateway Gateway => this;
        public FakeDesktopSettings Settings { get; }
        IAiDesktopSettings IAiDesktopRuntime.Settings => Settings;
        public IRecipeGenerationChannel RecipeGeneration => this;
        public IRecipeDraftStore RecipeDrafts => this;
        public int ChatCalls { get; private set; }
        public int ImageCalls { get; private set; }
        public int OpenArtifactCalls { get; private set; }
        public int DisposeCalls { get; private set; }
        public int RecipeGenerateCalls { get; private set; }
        public int RecipeDraftSaveCalls { get; private set; }
        public bool ThrowChatFailure { get; init; }
        public bool ThrowImageFailure { get; init; }
        public ChatRequest? LastChatRequest { get; private set; }
        public ImageGenerationRequest? LastImageRequest { get; private set; }
        public TrackingStream? LastOpenedStream { get; private set; }
        public byte[] ArtifactBytes { get; init; } = ValidPng;

        public ValueTask<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
        {
            ChatCalls++;
            LastChatRequest = request;
            return ThrowChatFailure
                ? ValueTask.FromException<ChatResponse>(new AiGatewayException(AiErrorCode.ConfigurationUnavailable))
                : ValueTask.FromResult(new ChatResponse(request.CorrelationId, "synthetic response"));
        }

        public ValueTask<ImageGenerationResponse> GenerateImageAsync(
            ImageGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            ImageCalls++;
            LastImageRequest = request;
            return ThrowImageFailure
                ? ValueTask.FromException<ImageGenerationResponse>(new ImageGatewayException(ImageErrorCode.NetworkFailure))
                : ValueTask.FromResult(new ImageGenerationResponse(request.CorrelationId, "img-preview"));
        }

        public ValueTask<Stream> OpenImageArtifactAsync(string privateArtifactId, CancellationToken cancellationToken = default)
        {
            OpenArtifactCalls++;
            LastOpenedStream = new TrackingStream(ArtifactBytes);
            return ValueTask.FromResult<Stream>(LastOpenedStream);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCalls++;
            return ValueTask.CompletedTask;
        }

        public ValueTask<RecipeGenerationResult> GenerateAsync(
            RecipeGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            RecipeGenerateCalls++;
            return ValueTask.FromException<RecipeGenerationResult>(
                new AiGatewayException(AiErrorCode.ConfigurationUnavailable));
        }

        public RecipeDraftRecord Save(RecipeDraftRecord record)
        {
            RecipeDraftSaveCalls++;
            throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.StorageFailed);
        }

        public RecipeDraftRecord Confirm(string draftId, string canonicalSha256) =>
            throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.StorageFailed);

        public RecipeDraftRecord? TryGet(string draftId) => null;

        private static byte[] CreateValidPng()
        {
            using var output = new MemoryStream();
            output.Write([137, 80, 78, 71, 13, 10, 26, 10]);
            var header = new byte[13];
            BinaryPrimitives.WriteUInt32BigEndian(header, 1);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), 1);
            header[8] = 8;
            header[9] = 6;
            WritePngChunk(output, "IHDR"u8, header);

            byte[] compressed;
            using (var compressedOutput = new MemoryStream())
            {
                using (var compressor = new ZLibStream(compressedOutput, CompressionLevel.SmallestSize, leaveOpen: true))
                {
                    compressor.Write([0, 255, 0, 0, 255]);
                }

                compressed = compressedOutput.ToArray();
            }

            try
            {
                WritePngChunk(output, "IDAT"u8, compressed);
            }
            finally
            {
                Array.Clear(compressed);
            }

            WritePngChunk(output, "IEND"u8, ReadOnlySpan<byte>.Empty);
            return output.ToArray();
        }

        private static void WritePngChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
        {
            Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)data.Length));
            output.Write(length);
            output.Write(type);
            output.Write(data);
            Span<byte> crc = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(crc, CalculatePngCrc(type, data));
            output.Write(crc);
        }

        private static uint CalculatePngCrc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
        {
            var value = uint.MaxValue;
            foreach (var current in type)
            {
                value = UpdatePngCrc(value, current);
            }

            foreach (var current in data)
            {
                value = UpdatePngCrc(value, current);
            }

            return ~value;
        }

        private static uint UpdatePngCrc(uint value, byte current)
        {
            value ^= current;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) == 0 ? value >> 1 : (value >> 1) ^ 0xedb88320u;
            }

            return value;
        }
    }

    private sealed class FakeDesktopSettings : IAiDesktopSettings
    {
        private AiDesktopSettingsSnapshot _snapshot = EmptySnapshot();
        private AiDesktopProfileEdit? _edit;

        public string? LastSecretEntry { get; private set; }
        public string? LastRevokedProfileId { get; private set; }

        public AiDesktopSettingsSnapshot Load() => _snapshot;

        public AiDesktopProfileEdit BeginProfileEdit(string profileId) =>
            _edit ?? throw new AiGatewayException(AiErrorCode.ConfigurationUnavailable);

        public AiDesktopSettingsSnapshot SaveProfile(AiDesktopProfileDraft profile, string? secretEntry)
        {
            LastSecretEntry = secretEntry;
            _edit = new AiDesktopProfileEdit(profile, hasSecret: !string.IsNullOrEmpty(secretEntry));
            _snapshot = SnapshotFor(profile, hasSecret: !string.IsNullOrEmpty(secretEntry));
            return _snapshot;
        }

        public AiDesktopSettingsSnapshot DeleteProfile(string profileId)
        {
            _edit = null;
            _snapshot = EmptySnapshot();
            return _snapshot;
        }

        public AiDesktopSettingsSnapshot RevokeSecret(string profileId)
        {
            if (_edit is null || !string.Equals(_edit.Profile.Id, profileId, StringComparison.Ordinal))
            {
                throw new AiGatewayException(AiErrorCode.ConfigurationUnavailable);
            }

            LastRevokedProfileId = profileId;
            _edit = new AiDesktopProfileEdit(_edit.Profile, hasSecret: false);
            _snapshot = SnapshotFor(_edit.Profile, hasSecret: false);
            return _snapshot;
        }

        public AiDesktopSettingsSnapshot SaveChannelBinding(AiDesktopChannelBindingDraft binding) => _snapshot;

        public AiDesktopSettingsSnapshot ClearChannelBinding(AiChannel channel) => _snapshot;

        public void SetExistingProfile()
        {
            var profile = new AiDesktopProfileDraft(
                "profile-one",
                "Synthetic profile",
                ProviderOrigin.Custom,
                enabled: true,
                ProviderProtocols.OpenAiCompatibleV1,
                RawEndpoint,
                30,
                [new AiDesktopCapabilityDraft("chat-one", AiChannel.ChatLlm, "chat-model-one")]);
            _edit = new AiDesktopProfileEdit(profile, hasSecret: true);
            _snapshot = SnapshotFor(profile, hasSecret: true);
        }

        private static AiDesktopSettingsSnapshot SnapshotFor(AiDesktopProfileDraft profile, bool hasSecret) => new(
            1,
            [new AiDesktopProfileSummary(
                profile.Id,
                profile.DisplayName,
                profile.Origin,
                profile.Enabled,
                profile.ProtocolId,
                "<endpoint redacted>",
                profile.TimeoutSeconds,
                hasSecret,
                profile.Capabilities)],
            Array.Empty<AiDesktopChannelBinding>(),
            [
                new AiDesktopChannelStatus(AiChannel.ChatLlm, AiDesktopChannelStatusKind.Unbound),
                new AiDesktopChannelStatus(AiChannel.ImageGeneration, AiDesktopChannelStatusKind.Unbound),
            ]);

        private static AiDesktopSettingsSnapshot EmptySnapshot() => new(
            0,
            Array.Empty<AiDesktopProfileSummary>(),
            Array.Empty<AiDesktopChannelBinding>(),
            [
                new AiDesktopChannelStatus(AiChannel.ChatLlm, AiDesktopChannelStatusKind.Unbound),
                new AiDesktopChannelStatus(AiChannel.ImageGeneration, AiDesktopChannelStatusKind.Unbound),
            ]);
    }

    private sealed class TrackingStream : MemoryStream
    {
        public TrackingStream(byte[] bytes)
            : base(bytes, writable: false)
        {
        }

        public bool WasDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }
}
