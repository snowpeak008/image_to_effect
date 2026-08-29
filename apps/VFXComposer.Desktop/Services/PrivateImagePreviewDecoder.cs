using Avalonia.Media.Imaging;
using VFXComposer.AI.Contracts.Desktop;

namespace VFXComposer.Desktop.Services;

/// <summary>
/// The one Desktop stream boundary: it receives a provider-owned private artifact stream, materializes an in-memory
/// bitmap, and closes the stream before returning. It is intentionally not a persistence or outbound-transport
/// abstraction.
/// </summary>
public static class PrivateImagePreviewDecoder
{
    public static async ValueTask<Bitmap> DecodeAsync(
        IAiDesktopRuntime runtime,
        string privateArtifactId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateArtifactId);

        Stream? source = null;
        try
        {
            source = await runtime.OpenImageArtifactAsync(privateArtifactId, cancellationToken).ConfigureAwait(false);
            return new Bitmap(source);
        }
        finally
        {
            if (source is not null)
            {
                await source.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
