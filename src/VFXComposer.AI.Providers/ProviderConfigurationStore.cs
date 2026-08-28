using System.Security.Cryptography;
using VFXComposer.AI.Contracts;

namespace VFXComposer.AI.Providers;

/// <summary>
/// Current-user non-secret configuration store. Reads never synthesize an empty configuration. Every read-modify-write
/// operation takes <see cref="ProviderConfigurationRevisionLock"/> before it observes a revision.
/// </summary>
public sealed class ProviderConfigurationStore
{
    private readonly string _configurationPath;
    private readonly string _backupPath;
    private readonly ProviderConfigurationRevisionLock _revisionLock;

    public ProviderConfigurationStore(string configurationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationPath);
        _configurationPath = Path.GetFullPath(configurationPath);
        _backupPath = _configurationPath + ".bak";
        _revisionLock = new ProviderConfigurationRevisionLock(_configurationPath);
    }

    public void Save(AiProviderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ExecuteUnderRevisionLock(() =>
        {
            var current = TryLoadExistingCore();
            ProviderConfigurationValidator.Validate(settings);
            if (current is not null && settings.Revision <= current.Configuration.Settings.Revision)
            {
                throw new AiGatewayException(AiErrorCode.ConfigurationInvalid);
            }

            var bytes = ProviderConfigurationCodec.Serialize(settings);
            try
            {
                AtomicFileWriter.WriteReplace(_configurationPath, _backupPath, bytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }

            return 0;
        });
    }

    public ProviderConfigurationStoreReadResult Load() => ExecuteUnderRevisionLock(LoadCore);

    public bool Exists() => File.Exists(_configurationPath) || File.Exists(_backupPath);

    public override string ToString() => "ProviderConfigurationStore(<redacted>)";

    private ProviderConfigurationStoreReadResult? TryLoadExistingCore()
    {
        if (!File.Exists(_configurationPath) && !File.Exists(_backupPath))
        {
            return null;
        }

        return LoadCore();
    }

    private ProviderConfigurationStoreReadResult LoadCore()
    {
        if (TryRead(_configurationPath, out var primary))
        {
            return new ProviderConfigurationStoreReadResult(primary!, recoveredFromBackup: false);
        }

        if (TryRead(_backupPath, out var backup))
        {
            RestoreRecoveredPrimary(backup!.Settings);
            return new ProviderConfigurationStoreReadResult(backup!, recoveredFromBackup: true);
        }

        throw new AiGatewayException(AiErrorCode.ConfigurationUnavailable);
    }

    private void RestoreRecoveredPrimary(AiProviderSettings recoveredSettings)
    {
        var knownGoodBytes = ProviderConfigurationCodec.Serialize(recoveredSettings);
        try
        {
            AtomicFileWriter.RestorePrimaryPreservingBackup(_configurationPath, knownGoodBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(knownGoodBytes);
        }
    }

    private T ExecuteUnderRevisionLock<T>(Func<T> operation)
    {
        try
        {
            return _revisionLock.Execute(operation);
        }
        catch (AiGatewayException)
        {
            throw;
        }
        catch (IOException)
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationUnavailable);
        }
        catch (UnauthorizedAccessException)
        {
            throw new AiGatewayException(AiErrorCode.ConfigurationUnavailable);
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
