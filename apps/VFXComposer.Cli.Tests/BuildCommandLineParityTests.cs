using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Cli;

namespace VFXComposer.Cli.Tests;

/// <summary>
/// Simple-mode milestone audit item ① (assigned to F8c): the Desktop build-handoff card carries a
/// hard-coded copyable command, and nothing asserted it against the real CLI command surface. This
/// test reads that literal from the Desktop source (the two projects deliberately do not reference
/// each other) and proves the command it names parses as <c>batch run</c> through the production
/// argument parser, and that the project path it names is the CLI project that actually ships.
/// </summary>
[TestClass]
public sealed class BuildCommandLineParityTests
{
    [TestMethod]
    public void TheDesktopHandoffCommandParsesAsBatchRunOnTheRealCommandSurface()
    {
        var commandLine = ReadDesktopBuildCommandLine();

        // Shape: dotnet run --project <cli-project> -- <vfxc arguments>. The tail after "--" is
        // what vfxc receives; the manifest placeholder becomes a concrete argument for parsing.
        var separatorIndex = commandLine.IndexOf(" -- ", StringComparison.Ordinal);
        Assert.IsTrue(separatorIndex > 0, "The handoff command must delegate to the CLI after '--': " + commandLine);
        var vfxcArguments = commandLine[(separatorIndex + 4)..]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(static token => token == "<manifest.json>" ? "manifest.json" : token)
            .ToArray();

        var parsed = CliArguments.Parse(vfxcArguments);

        Assert.IsNotNull(parsed.Command, parsed.UsageError ?? "The handoff command must parse cleanly.");
        Assert.AreEqual(CliCommandGroups.Batch, parsed.Command.Group);
        Assert.AreEqual(CliCommandActions.Run, parsed.Command.Action);
        Assert.AreEqual("manifest.json", parsed.Command.Argument);
    }

    [TestMethod]
    public void TheDesktopHandoffCommandNamesTheCliProjectThatActuallyShips()
    {
        var commandLine = ReadDesktopBuildCommandLine();
        var projectMatch = Regex.Match(commandLine, @"--project\s+(?<path>\S+)");
        Assert.IsTrue(projectMatch.Success, "The handoff command must name the CLI project: " + commandLine);

        var projectPath = Path.Combine(
            TestRepository.Root(),
            projectMatch.Groups["path"].Value.Replace('/', Path.DirectorySeparatorChar));
        Assert.IsTrue(
            File.Exists(Path.Combine(projectPath, "VFXComposer.Cli.csproj")),
            "The named project directory must contain the CLI project file: " + projectPath);
    }

    /// <summary>
    /// Extracts the <c>BuildCommandLine</c> literal from the Desktop view model source. Reading the
    /// source is deliberate: Desktop and CLI must stay reference-free of each other, and this is
    /// the same source-scanning discipline the Desktop wiring tests already use.
    /// </summary>
    private static string ReadDesktopBuildCommandLine()
    {
        var sourcePath = Path.Combine(
            TestRepository.Root(),
            "apps",
            "VFXComposer.Desktop",
            "ViewModels",
            "CreateViewModel.cs");
        var source = File.ReadAllText(sourcePath, Encoding.UTF8);
        var match = Regex.Match(source, "BuildCommandLine\\s*=>\\s*\"(?<command>[^\"]+)\"");
        Assert.IsTrue(match.Success, "CreateViewModel must declare the BuildCommandLine literal.");
        return match.Groups["command"].Value;
    }
}
