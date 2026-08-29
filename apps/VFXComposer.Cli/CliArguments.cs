using System.Globalization;
using VFXComposer.Batch.Core;

namespace VFXComposer.Cli;

/// <summary>Closed command-group vocabulary.</summary>
public static class CliCommandGroups
{
    public const string Batch = "batch";
    public const string Job = "job";
    public const string Queue = "queue";
}

/// <summary>Closed command-action vocabulary.</summary>
public static class CliCommandActions
{
    public const string Validate = "validate";
    public const string Run = "run";
    public const string Status = "status";
    public const string Cancel = "cancel";
    public const string List = "list";
}

/// <summary>Parameters that only <c>batch run</c> accepts (REQ-002 §6.3).</summary>
public sealed record CliRunOptions
{
    public string? OnFailureOverride { get; init; }
    public bool Resume { get; init; }
    public bool Force { get; init; }
    public bool DryRun { get; init; }
    public bool Detach { get; init; }
    public string? ReportPath { get; init; }
    public TimeSpan? LockTimeout { get; init; }
}

/// <summary>One fully bound command line.</summary>
public sealed record CliCommand(string Group, string Action, string? Argument, bool Json, CliRunOptions Run);

/// <summary>Parse outcome: exactly one of the three states is set.</summary>
public sealed record CliParseResult(CliCommand? Command, string? UsageError, bool HelpRequested)
{
    public static CliParseResult Help { get; } = new(null, null, HelpRequested: true);

    public static CliParseResult Invalid(string reason) => new(null, reason, HelpRequested: false);

    public static CliParseResult Bound(CliCommand command) => new(command, null, HelpRequested: false);
}

/// <summary>
/// Hand-written parser for the closed command surface. There is no option library and no dynamic
/// registration: an unrecognised verb, flag or value is a usage error, which is what keeps
/// authority/approval/skip style parameters (REQ-002-12) from ever being accepted.
/// </summary>
public static class CliArguments
{
    private const int MaximumTokenEcho = 64;
    private const int MaximumLockTimeoutSeconds = 86_400;

    public static CliParseResult Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count == 0)
        {
            return CliParseResult.Help;
        }

        if (arguments.Count == 1 && IsHelp(arguments[0]))
        {
            return CliParseResult.Help;
        }

        if (arguments.Count < 2)
        {
            return CliParseResult.Invalid("Expected a command group and action.");
        }

        var group = arguments[0];
        var action = arguments[1];
        if (!IsKnownCommand(group, action))
        {
            return CliParseResult.Invalid("Unknown command '" + Echo(group) + " " + Echo(action) + "'.");
        }

        var expectsArgument = !(string.Equals(group, CliCommandGroups.Queue, StringComparison.Ordinal) &&
            string.Equals(action, CliCommandActions.List, StringComparison.Ordinal));
        var allowsRunOptions = string.Equals(group, CliCommandGroups.Batch, StringComparison.Ordinal) &&
            string.Equals(action, CliCommandActions.Run, StringComparison.Ordinal);
        string? argument = null;
        var json = false;
        var run = new CliRunOptions();
        for (var index = 2; index < arguments.Count; index++)
        {
            var token = arguments[index];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                if (!expectsArgument || argument is not null)
                {
                    return CliParseResult.Invalid("Unexpected positional argument '" + Echo(token) + "'.");
                }

                argument = token;
                continue;
            }

            switch (token)
            {
                case "--json":
                    json = true;
                    continue;
            }

            if (!allowsRunOptions)
            {
                return CliParseResult.Invalid("Unknown option '" + Echo(token) + "' for this command.");
            }

            switch (token)
            {
                case "--resume":
                    run = run with { Resume = true };
                    break;
                case "--force":
                    run = run with { Force = true };
                    break;
                case "--dry-run":
                    run = run with { DryRun = true };
                    break;
                case "--detach":
                    run = run with { Detach = true };
                    break;
                case "--on-failure":
                    if (!TryTakeValue(arguments, ref index, out var policy))
                    {
                        return CliParseResult.Invalid("Option '--on-failure' requires a value.");
                    }

                    if (!BatchFailurePolicies.IsKnown(policy))
                    {
                        return CliParseResult.Invalid("Option '--on-failure' accepts continue|abort.");
                    }

                    run = run with { OnFailureOverride = policy };
                    break;
                case "--report":
                    if (!TryTakeValue(arguments, ref index, out var reportPath))
                    {
                        return CliParseResult.Invalid("Option '--report' requires a value.");
                    }

                    run = run with { ReportPath = reportPath };
                    break;
                case "--lock-timeout":
                    if (!TryTakeValue(arguments, ref index, out var seconds))
                    {
                        return CliParseResult.Invalid("Option '--lock-timeout' requires a value.");
                    }

                    if (!int.TryParse(seconds, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
                        parsed is < 0 or > MaximumLockTimeoutSeconds)
                    {
                        return CliParseResult.Invalid("Option '--lock-timeout' accepts 0.." +
                            MaximumLockTimeoutSeconds.ToString(CultureInfo.InvariantCulture) + " seconds.");
                    }

                    run = run with { LockTimeout = TimeSpan.FromSeconds(parsed) };
                    break;
                default:
                    return CliParseResult.Invalid("Unknown option '" + Echo(token) + "'.");
            }
        }

        if (expectsArgument && argument is null)
        {
            return CliParseResult.Invalid("Command '" + Echo(group) + " " + Echo(action) + "' requires one argument.");
        }

        if (run.DryRun && run.Detach)
        {
            return CliParseResult.Invalid("Options '--dry-run' and '--detach' are mutually exclusive.");
        }

        if (run.Resume && run.Force)
        {
            return CliParseResult.Invalid("Options '--resume' and '--force' are mutually exclusive.");
        }

        return CliParseResult.Bound(new CliCommand(group, action, argument, json, run));
    }

    private static bool IsKnownCommand(string group, string action) => (group, action) switch
    {
        (CliCommandGroups.Batch, CliCommandActions.Validate) => true,
        (CliCommandGroups.Batch, CliCommandActions.Run) => true,
        (CliCommandGroups.Batch, CliCommandActions.Status) => true,
        (CliCommandGroups.Job, CliCommandActions.Status) => true,
        (CliCommandGroups.Job, CliCommandActions.Cancel) => true,
        (CliCommandGroups.Queue, CliCommandActions.List) => true,
        _ => false,
    };

    private static bool IsHelp(string token) =>
        string.Equals(token, "--help", StringComparison.Ordinal) ||
        string.Equals(token, "-h", StringComparison.Ordinal) ||
        string.Equals(token, "help", StringComparison.Ordinal);

    private static bool TryTakeValue(IReadOnlyList<string> arguments, ref int index, out string value)
    {
        if (index + 1 >= arguments.Count || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            value = string.Empty;
            return false;
        }

        index++;
        value = arguments[index];
        return true;
    }

    /// <summary>Bounds and sanitises an argument before it is quoted back in a usage message.</summary>
    private static string Echo(string token)
    {
        var trimmed = token.Length > MaximumTokenEcho ? token[..MaximumTokenEcho] : token;
        return string.Create(trimmed.Length, trimmed, static (span, source) =>
        {
            for (var index = 0; index < source.Length; index++)
            {
                span[index] = char.IsControl(source[index]) ? '?' : source[index];
            }
        });
    }
}
