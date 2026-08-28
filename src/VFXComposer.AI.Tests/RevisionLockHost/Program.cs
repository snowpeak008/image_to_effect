using System.Diagnostics;
using System.Globalization;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Providers;

namespace VFXComposer.AI.Tests.RevisionLockHost;

internal static class Program
{
    private const int ConfigurationInvalidExitCode = 12;
    private const int FailureExitCode = 70;

    public static int Main(string[] args)
    {
        try
        {
            return args.Length == 0 ? FailureExitCode : args[0] switch
            {
                "save" when args.Length == 3 => Save(args[1], ParseRevision(args[2])),
                "save-after-barrier" when args.Length == 5 => SaveAfterBarrier(args[1], ParseRevision(args[2]), args[3], args[4]),
                "hold-lock" when args.Length == 3 => HoldLock(args[1], args[2]),
                _ => FailureExitCode,
            };
        }
        catch (AiGatewayException exception) when (exception.Code == AiErrorCode.ConfigurationInvalid)
        {
            return ConfigurationInvalidExitCode;
        }
        catch (AiGatewayException)
        {
            return FailureExitCode;
        }
        catch (ArgumentException)
        {
            return FailureExitCode;
        }
        catch (IOException)
        {
            return FailureExitCode;
        }
        catch (UnauthorizedAccessException)
        {
            return FailureExitCode;
        }
    }

    private static int SaveAfterBarrier(string configurationPath, long revision, string readyPath, string releasePath)
    {
        WriteSignal(readyPath);
        if (!WaitForSignal(releasePath, TimeSpan.FromSeconds(30)))
        {
            return FailureExitCode;
        }

        return Save(configurationPath, revision);
    }

    private static int Save(string configurationPath, long revision)
    {
        new ProviderConfigurationStore(configurationPath).Save(CreateSettings(revision));
        return 0;
    }

    private static int HoldLock(string configurationPath, string readyPath)
    {
        var revisionLock = new ProviderConfigurationRevisionLock(configurationPath);
        using var lease = revisionLock.Acquire();
        WriteSignal(readyPath);
        Thread.Sleep(Timeout.Infinite);
        return 0;
    }

    private static AiProviderSettings CreateSettings(long revision)
    {
        var secretScope = SecretScope.Production;
        var profile = new ProviderProfile(
            "profile-primary",
            "Revision lock host",
            ProviderOrigin.Official,
            true,
            new ProtocolBinding(ProviderProtocols.OpenAiCompatibleV1),
            EndpointPolicy.Create("https://provider.example.invalid/v1/", false, secretScope),
            new AuthDescriptor(new SecretRef("secret-primary"), secretScope),
            30,
            [new CapabilityDefinition("chat-main", AiChannel.ChatLlm, "chat-model-1")]);
        return new AiProviderSettings(
            revision,
            [profile],
            [new ChannelBinding(AiChannel.ChatLlm, "profile-primary", "chat-main", "chat-model-1")]);
    }

    private static long ParseRevision(string value)
    {
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var revision) || revision < 1)
        {
            throw new ArgumentException("Revision is invalid.", nameof(value));
        }

        return revision;
    }

    private static void WriteSignal(string path)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(path));
        if (string.IsNullOrEmpty(parent))
        {
            throw new IOException("Signal path is invalid.");
        }

        Directory.CreateDirectory(parent);
        File.WriteAllText(path, "ready");
    }

    private static bool WaitForSignal(string path, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!File.Exists(path))
        {
            if (stopwatch.Elapsed >= timeout)
            {
                return false;
            }

            Thread.Sleep(20);
        }

        return true;
    }
}
