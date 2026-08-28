using System.Security.Cryptography;
using VFXComposer.AI.Contracts;

namespace VFXComposer.AI.Providers;

/// <summary>
/// Current-user non-secret configuration store. Reads never synthesize an empty configuration.
/// </summary>
public sealed class ProviderConfigurationStore
{
    private readonly string _configurationPath;
    private readonly string _backupPath;

    public ProviderConfigurationStore(string configurationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationPath);
        _configurationPath = Path.GetFullPath(configurationPath);
        _backupPath = _configurationPath + ".bak";
    }

    public void Save(AiProviderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ProviderConfigurationValidator.Validate(settings);
        EnsureRevisionAdvances(settings.Revision);
        var bytes = ProviderConfigurationCodec.Serialize(settings);
        try
        {
            AtomicFileWriter.WriteReplace(_configurationPath, _backupPath, bytes);
        }
        catch (IOException)
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationUnavailable);
        }
        catch (UnauthorizedAccessException)
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationUnavailable);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public ProviderConfigurationStoreReadResult Load()
    {
        if (TryRead(_configurationPath, out var primary))
        {
            return new ProviderConfigurationStoreReadResult(primary!, recoveredFromBackup: false);
        }

        if (TryRead(_backupPath, out var backup))
        {
            return new ProviderConfigurationStoreReadResult(backup!, recoveredFromBackup: true);
        }

        throw new AiGatewayException(AiErrorCode.ConfigurationUnavailable);
    }

    public bool Exists() => File.Exists(_configurationPath) || File.Exists(_backupPath);

    public override string ToString() => "ProviderConfigurationStore(<redacted>)";

    private void EnsureRevisionAdvances(long nextRevision)
    {
        if (!Exists())
        {
            return;
        }

        var current = Load();
        if (nextRevision <= current.Configuration.Settings.Revision)
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
        }
    }

    private static bool TryRead(string path, out ProviderConfigurationReadResult? result)
    {
        result = null;
        if (!File.Exists(path))
        {
            return false;
        }

        byte[]? bytes = null;
        try
        {
            bytes = AtomicFileWriter.ReadBounded(path, ProviderConfigurationCodec.MaximumConfigurationBytes);
            result = ProviderConfigurationCodec.Deserialize(bytes);
            return true;
        }
        catch (AiGatewayException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            if (bytes is not null)
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }
    }
}

public sealed class ProviderConfigurationStoreReadResult
{
    public ProviderConfigurationStoreReadResult(ProviderConfigurationReadResult configuration, bool recoveredFromBackup)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        RecoveredFromBackup = recoveredFromBackup;
    }

    public ProviderConfigurationReadResult Configuration { get; }
    public bool RecoveredFromBackup { get; }
}
