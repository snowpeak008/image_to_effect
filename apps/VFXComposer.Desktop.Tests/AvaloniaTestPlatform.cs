using Avalonia;

namespace VFXComposer.Desktop.Tests;

/// <summary>
/// Avalonia may only be set up once per process, so every test class that needs loaded XAML shares this gate.
/// </summary>
internal static class AvaloniaTestPlatform
{
    private static readonly object Gate = new();
    private static bool _initialized;

    public static void EnsureInitialized()
    {
        lock (Gate)
        {
            if (_initialized)
            {
                return;
            }

            AppBuilder.Configure<App>().UsePlatformDetect().SetupWithoutStarting();
            _initialized = true;
        }
    }
}
