using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Batch.Core;
using VFXComposer.Cli;

namespace VFXComposer.Cli.Tests;

[TestClass]
public sealed class CliArgumentsTests
{
    [TestMethod]
    public void NoArgumentsAsksForHelp()
    {
        Assert.IsTrue(CliArguments.Parse(Array.Empty<string>()).HelpRequested);
        Assert.IsTrue(CliArguments.Parse(["--help"]).HelpRequested);
    }

    [TestMethod]
    public void EveryDocumentedCommandBinds()
    {
        var commands = new[]
        {
            (new[] { "batch", "validate", "m.json" }, CliCommandGroups.Batch, CliCommandActions.Validate),
            (new[] { "batch", "run", "m.json" }, CliCommandGroups.Batch, CliCommandActions.Run),
            (new[] { "batch", "status", "b" }, CliCommandGroups.Batch, CliCommandActions.Status),
            (new[] { "job", "status", "j" }, CliCommandGroups.Job, CliCommandActions.Status),
            (new[] { "job", "cancel", "j" }, CliCommandGroups.Job, CliCommandActions.Cancel),
            (new[] { "queue", "list" }, CliCommandGroups.Queue, CliCommandActions.List),
        };

        foreach (var (arguments, group, action) in commands)
        {
            var parsed = CliArguments.Parse(arguments);
            Assert.IsNotNull(parsed.Command, string.Join(' ', arguments));
            Assert.AreEqual(group, parsed.Command!.Group);
            Assert.AreEqual(action, parsed.Command.Action);
        }
    }

    [TestMethod]
    public void RunOptionsBind()
    {
        var parsed = CliArguments.Parse([
            "batch", "run", "m.json",
            "--on-failure", "abort",
            "--resume",
            "--detach",
            "--json",
            "--report", "out.json",
            "--lock-timeout", "30",
        ]);

        Assert.IsNotNull(parsed.Command);
        Assert.AreEqual("m.json", parsed.Command!.Argument);
        Assert.IsTrue(parsed.Command.Json);
        Assert.AreEqual(BatchFailurePolicies.Abort, parsed.Command.Run.OnFailureOverride);
        Assert.IsTrue(parsed.Command.Run.Resume);
        Assert.IsTrue(parsed.Command.Run.Detach);
        Assert.AreEqual("out.json", parsed.Command.Run.ReportPath);
        Assert.AreEqual(TimeSpan.FromSeconds(30), parsed.Command.Run.LockTimeout);
    }

    [TestMethod]
    public void AuthorityStyleAndUnknownOptionsAreRefused()
    {
        foreach (var option in new[] { "--approve", "--authority", "--skip-validation", "--force-write" })
        {
            var parsed = CliArguments.Parse(["batch", "run", "m.json", option]);

            Assert.IsNull(parsed.Command, option);
            Assert.IsNotNull(parsed.UsageError, option);
        }
    }

    [TestMethod]
    public void RunOnlyOptionsAreRefusedOnOtherCommands()
    {
        Assert.IsNull(CliArguments.Parse(["queue", "list", "--force"]).Command);
        Assert.IsNull(CliArguments.Parse(["job", "cancel", "j", "--detach"]).Command);
    }

    [TestMethod]
    public void UnknownVerbsAndMissingArgumentsAreUsageErrors()
    {
        Assert.IsNotNull(CliArguments.Parse(["batch", "destroy", "m.json"]).UsageError);
        Assert.IsNotNull(CliArguments.Parse(["batch", "run"]).UsageError);
        Assert.IsNotNull(CliArguments.Parse(["batch"]).UsageError);
        Assert.IsNotNull(CliArguments.Parse(["batch", "run", "a.json", "b.json"]).UsageError);
        Assert.IsNotNull(CliArguments.Parse(["queue", "list", "extra"]).UsageError);
    }

    [TestMethod]
    public void OptionValuesAreValidated()
    {
        Assert.IsNotNull(CliArguments.Parse(["batch", "run", "m.json", "--on-failure", "retry"]).UsageError);
        Assert.IsNotNull(CliArguments.Parse(["batch", "run", "m.json", "--on-failure"]).UsageError);
        Assert.IsNotNull(CliArguments.Parse(["batch", "run", "m.json", "--lock-timeout", "-1"]).UsageError);
        Assert.IsNotNull(CliArguments.Parse(["batch", "run", "m.json", "--lock-timeout", "abc"]).UsageError);
        Assert.IsNotNull(CliArguments.Parse(["batch", "run", "m.json", "--report"]).UsageError);
    }

    [TestMethod]
    public void MutuallyExclusiveOptionsAreRefused()
    {
        Assert.IsNotNull(CliArguments.Parse(["batch", "run", "m.json", "--resume", "--force"]).UsageError);
        Assert.IsNotNull(CliArguments.Parse(["batch", "run", "m.json", "--dry-run", "--detach"]).UsageError);
    }

    [TestMethod]
    public void UsageMessagesDoNotEchoUnboundedInput()
    {
        var parsed = CliArguments.Parse(["batch", "--" + new string('x', 400)]);

        Assert.IsNotNull(parsed.UsageError);
        Assert.IsTrue(parsed.UsageError!.Length < 200, "A usage message must stay bounded.");
    }
}
