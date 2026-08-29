using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Desktop;
using VFXComposer.Desktop.Services;

namespace VFXComposer.Desktop.ViewModels;

public sealed class PreviewViewModel : WorkspacePageViewModel, IDisposable
{
    private readonly IAiDesktopRuntime _runtime;
    private string _imagePrompt = string.Empty;
    private int _imageWidth = 1024;
    private int _imageHeight = 1024;
    private Bitmap? _previewImage;
    private string _imageStatus = "Image generation is not configured.";
    private bool _isGenerating;

    public PreviewViewModel(IAiDesktopRuntime? runtime = null)
        : base(
            "preview",
            "Preview",
            "Private image previews arrive only after an explicit ImageGeneration request.",
            "No private image preview is available")
    {
        _runtime = runtime ?? AiDesktopRuntime.Unavailable;
        GenerateImageCommand = new AsyncRelayCommand(GenerateImageAsync, CanGenerateImage);
    }

    public string ImagePrompt
    {
        get => _imagePrompt;
        set
        {
            if (SetProperty(ref _imagePrompt, value ?? string.Empty))
            {
                GenerateImageCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public int ImageWidth
    {
        get => _imageWidth;
        set => SetProperty(ref _imageWidth, value);
    }

    public int ImageHeight
    {
        get => _imageHeight;
        set => SetProperty(ref _imageHeight, value);
    }

    public Bitmap? PreviewImage
    {
        get => _previewImage;
        private set => SetProperty(ref _previewImage, value);
    }

    public string ImageStatus
    {
        get => _imageStatus;
        private set => SetProperty(ref _imageStatus, value ?? string.Empty);
    }

    public bool IsGenerating
    {
        get => _isGenerating;
        private set
        {
            if (SetProperty(ref _isGenerating, value))
            {
                GenerateImageCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public IAsyncRelayCommand GenerateImageCommand { get; }

    public void Dispose()
    {
        var image = PreviewImage;
        PreviewImage = null;
        image?.Dispose();
    }

    private bool CanGenerateImage() => !IsGenerating && !string.IsNullOrWhiteSpace(ImagePrompt);

    private async Task GenerateImageAsync()
    {
        if (!CanGenerateImage())
        {
            return;
        }

        IsGenerating = true;
        try
        {
            // Image has no health/display preflight. This deliberate action is the only path that can request an
            // image, and the runtime keeps the returned artifact private.
            var response = await _runtime.Gateway.GenerateImageAsync(
                new ImageGenerationRequest(
                    Guid.NewGuid().ToString("N"),
                    ImagePrompt,
                    ImageWidth,
                    ImageHeight),
                CancellationToken.None);
            var decoded = await PrivateImagePreviewDecoder.DecodeAsync(
                _runtime,
                response.PrivateArtifactId,
                CancellationToken.None);
            var previous = PreviewImage;
            PreviewImage = decoded;
            previous?.Dispose();
            ImageStatus = "Private image preview ready.";
        }
        catch (ImageGatewayException exception)
        {
            ImageStatus = "Image unavailable: " + exception.Code + ".";
        }
        catch (AiGatewayException exception)
        {
            ImageStatus = "Image unavailable: " + exception.Code + ".";
        }
        catch (OperationCanceledException)
        {
            ImageStatus = "Image generation cancelled.";
        }
        catch
        {
            ImageStatus = "Image unavailable.";
        }
        finally
        {
            IsGenerating = false;
        }
    }
}
