using VFXComposer.Cli;

using var interrupt = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    // Ctrl+C stops foreground tracking only. Already enqueued jobs keep running under the
    // executor, which is the same observable behaviour as --detach (REQ-002 §12).
    eventArgs.Cancel = true;
    interrupt.Cancel();
};

var environment = CliProductionEnvironment.Create(Console.Out, Console.Error);
return await CliRunner.RunAsync(args, environment, interrupt.Token).ConfigureAwait(false);
