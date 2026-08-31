using System.Globalization;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.Tests;

/// <summary>
/// Tests pin semantics, not wording: every expectation is resolved through the catalog with an explicitly fixed
/// language, so translating a string can never break an unrelated test.
/// </summary>
internal static class LocalizationTestSupport
{
    public static LocalizationService CreateEnglish() => new(UiLanguage.English);

    public static LocalizationService CreateChineseSimplified() => new(UiLanguage.ChineseSimplified);

    public static string English(string key) => UiStringCatalog.Resolve(UiLanguage.English, key);

    public static string ChineseSimplified(string key) => UiStringCatalog.Resolve(UiLanguage.ChineseSimplified, key);

    public static string EnglishFormat(string key, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, English(key), arguments);

    public static string ChineseSimplifiedFormat(string key, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, ChineseSimplified(key), arguments);

    /// <summary>
    /// Every markup file of the shell. Compiled bindings validate property paths but not indexer keys, and the build
    /// keeps no XAML asset to read back, so key coverage is checked against the project sources.
    /// </summary>
    public static IReadOnlyList<FileInfo> DesktopMarkupFiles() => ProjectFiles("*.axaml");

    public static IReadOnlyList<FileInfo> DesktopSourceFiles() => ProjectFiles("*.cs");

    public static string ReadProjectText(FileInfo file) => File.ReadAllText(file.FullName, Encoding.UTF8);

    private static IReadOnlyList<FileInfo> ProjectFiles(string pattern) => DesktopProjectDirectory()
        .GetFiles(pattern, SearchOption.AllDirectories)
        .Where(static file => !IsBuildOutput(file))
        .OrderBy(static file => file.FullName, StringComparer.Ordinal)
        .ToArray();

    private static bool IsBuildOutput(FileInfo file) => file.FullName
        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(static segment =>
            string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase));

    private static DirectoryInfo DesktopProjectDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "VFXComposer.Desktop");
            if (File.Exists(Path.Combine(candidate, "VFXComposer.Desktop.csproj")))
            {
                return new DirectoryInfo(candidate);
            }
        }

        throw new AssertFailedException($"The Desktop project was not found above {AppContext.BaseDirectory}.");
    }
}
