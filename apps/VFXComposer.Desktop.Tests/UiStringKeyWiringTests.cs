using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.Tests;

/// <summary>
/// Closing guard of the migration: the catalog is a closed set that must stay exactly as large as the shell needs.
/// An orphan key is dead text that no view or view model can ever show; a hard-coded English sentence left in the
/// shell is text the language switch cannot reach.
/// </summary>
[TestClass]
public sealed class UiStringKeyWiringTests
{
    // The declaration and the catalog itself name every key by construction, so they cannot prove a key is wired.
    private static readonly string[] DeclarationFileNames =
    [
        "UiStringKeys.cs",
        "UiStringCatalog.cs",
    ];

    // Protocol vocabulary and channel identifiers that stay verbatim in every language (master plan §6 decision 2).
    private static readonly string[] AllowedMarkupLiterals =
    [
        "ChatLlm",
        "ImageGeneration",
    ];

    private static readonly Regex MarkupTextLiteralPattern = new(
        @"(?<attribute>Text|Content|Watermark|Header|Title)=""(?<value>[^{""][^""]*)""",
        RegexOptions.CultureInvariant);

    [TestMethod]
    public void EveryDeclaredKeyIsWiredToAtLeastOneViewOrViewModel()
    {
        var wiring = ProjectTextByFile();
        var orphans = DeclaredKeys()
            .Where(key => !wiring.Any(entry => new Regex($@"\b{Regex.Escape(key)}\b").IsMatch(entry.Value)))
            .ToArray();

        Assert.AreEqual(
            0,
            orphans.Length,
            $"Unreferenced catalog keys: {string.Join(", ", orphans)}.");
    }

    [TestMethod]
    public void NoViewCarriesAHardCodedUserVisibleLiteral()
    {
        var literals = new List<string>();
        foreach (var file in LocalizationTestSupport.DesktopMarkupFiles())
        {
            var markup = LocalizationTestSupport.ReadProjectText(file);
            foreach (var match in MarkupTextLiteralPattern.Matches(markup).Cast<Match>())
            {
                var value = match.Groups["value"].Value;
                if (!AllowedMarkupLiterals.Contains(value, StringComparer.Ordinal))
                {
                    literals.Add($"{file.Name}: {match.Groups["attribute"].Value}=\"{value}\"");
                }
            }
        }

        Assert.AreEqual(
            0,
            literals.Count,
            $"Hard-coded user-visible markup text: {string.Join("; ", literals)}.");
    }

    private static IReadOnlyList<KeyValuePair<string, string>> ProjectTextByFile() =>
    [
        .. LocalizationTestSupport.DesktopMarkupFiles()
            .Concat(LocalizationTestSupport.DesktopSourceFiles()
                .Where(static file => !DeclarationFileNames.Contains(file.Name, StringComparer.Ordinal)))
            .Select(static file => new KeyValuePair<string, string>(
                file.Name,
                LocalizationTestSupport.ReadProjectText(file))),
    ];

    private static string[] DeclaredKeys() => typeof(UiStringKeys)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Where(static field => field.IsLiteral && field.FieldType == typeof(string))
        .Select(static field => (string)field.GetRawConstantValue()!)
        .ToArray();
}
