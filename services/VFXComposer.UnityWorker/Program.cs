using VFXComposer.UnityWorker;

if (OperatingSystem.IsWindows() &&
    args.Length == 1 &&
    string.Equals(args[0], "--user-mode-worker-child", StringComparison.Ordinal))
{
    return await UserModeUnityWorkerHost.RunChildModeAsync(Console.OpenStandardInput());
}

return 23;
