using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Desktop;
using VFXComposer.Desktop.Localization;
using VFXComposer.Desktop.Services;

namespace VFXComposer.Desktop.ViewModels;

public sealed class PreviewViewModel : WorkspacePageViewModel, IDisposable
{
    private readonly IAiDesktopRuntime _runtime;
    private string _imagePrompt = string.Empty;
    private int _imageWidth = 1024;
    private int _imageHeight = 1024;
    private Bitmap? _previewImage;
    private string _imageStatusKey = UiStringKeys.PreviewImageStatusNotConfigured;
    private object?[] _imageStatusArguments = [];
    private bool _isGenerating;

    public PreviewViewModel(LocalizationService localization, IAiDesktopRuntime? runtime = null)
        : base(
            localization,
            "preview",
            UiStringKeys.PreviewTitle,
            UiStringKeys.PreviewDescription,
            UiStringKeys.PreviewEmptyState)
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

    public string ImageStatus => Localized(_imageStatusKey, _imageStatusArguments);

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
            SetImageStatus(UiStringKeys.PreviewImageStatusReady);
        }
        catch (ImageGatewayException exception)
        {
            SetImageStatus(UiStringKeys.PreviewImageStatusUnavailableWithCode, exception.Code);
        }
        catch (AiGatewayException exception)
        {
            SetImageStatus(UiStringKeys.PreviewImageStatusUnavailableWithCode, exception.Code);
        }
        catch (OperationCanceledException)
        {
            SetImageStatus(UiStringKeys.PreviewImageStatusCancelled);
        }
        catch
        {
            SetImageStatus(UiStringKeys.PreviewImageStatusUnavailable);
        }
        finally
        {
            IsGenerating = false;
        }
    }

    protected override void RefreshLocalizedText() => OnPropertyChanged(nameof(ImageStatus));

    // The status line keeps its key and arguments instead of a rendered string, so a language switch re-renders it.
    private void SetImageStatus(string key, params object?[] arguments)
    {
        _imageStatusKey = key;
        _imageStatusArguments = arguments;
        OnPropertyChanged(nameof(ImageStatus));
    }
}
