namespace VFXComposer.Cli;

/// <summary>The fixed help text for the closed command surface (REQ-002 §6.2, §6.3).</summary>
internal static class CliUsage
{
    public static void Write(TextWriter writer)
    {
        writer.WriteLine("vfxc - VFX Composer batch entry surface");
        writer.WriteLine();
        writer.WriteLine("Commands:");
        writer.WriteLine("  vfxc batch validate <manifest>   Validate a batch manifest. No network, no writes, no enqueue.");
        writer.WriteLine("  vfxc batch run <manifest>        Enqueue every entry in manifest order and track the batch.");
        writer.WriteLine("  vfxc batch status <batchId>      Show the queue entries of one batch.");
        writer.WriteLine("  vfxc batch cancel <batchId>      Request cancellation of every entry of one batch.");
        writer.WriteLine("  vfxc job status <jobId>          Show one queue entry.");
        writer.WriteLine("  vfxc job cancel <jobId>          Request cancellation of one queue entry.");
        writer.WriteLine("  vfxc queue list                  Show the queue state and every entry.");
        writer.WriteLine();
        writer.WriteLine("Options for 'batch run':");
        writer.WriteLine("  --on-failure continue|abort      Override the manifest failure policy.");
        writer.WriteLine("  --resume                         Skip entries whose content already succeeded (the default).");
        writer.WriteLine("  --force                          Re-enqueue every entry, ignoring the idempotent skip.");
        writer.WriteLine("  --dry-run                        Print the submission plan without enqueueing anything.");
        writer.WriteLine("  --detach                         Enqueue and return immediately without tracking.");
        writer.WriteLine("  --report <path>                  Batch report destination; defaults to <manifest>.report.json.");
        writer.WriteLine("  --lock-timeout <seconds>         Give up tracking after the queue waits this long for the project lock.");
        writer.WriteLine();
        writer.WriteLine("Global options:");
        writer.WriteLine("  --json                           Emit NDJSON events instead of readable lines.");
        writer.WriteLine();
        writer.WriteLine("Exit codes: 0 success, 10 completed with failures, 11 aborted, 64 usage,");
        writer.WriteLine("            65 data error, 69 queue unavailable, 73 project lock timeout, 130 interrupted.");
    }
}
