using System.Text;

namespace VFXComposer.Desktop.Services;

/// <summary>
/// Current-user UI preference storage: <c>%LocalAppData%/VFXComposer/ui-preferences.json</c>, written atomically
/// through a temporary file plus replace.
/// </summary>
/// <remarks>
/// This is the only Desktop type that touches the filesystem. It exists here because the equivalent atomic writer in
/// <c>VFXComposer.AI.Providers</c> is assembly-internal, and a UI preference must not be mixed into the provider
/// configuration domain. It never receives, derives, or emits a project path: the location comes from the current-user
/// local application data root only, and the Desktop access-surface test keeps that storage exemption closed to this
/// single type.
/// </remarks>
public sealed class UiPreferencesStore : IUiPreferencesStore
{
    public const string LoadFailureDiagnosticCode = "UI_PREFERENCES_UNUSABLE";
    public const string SaveFailureDiagnosticCode = "UI_PREFERENCES_NOT_SAVED";
    public const string StorageUnavailableDiagnosticCode = "UI_PREFERENCES_STORAGE_UNAVAILABLE";

    private const string DocumentName = "ui-preferences.json";
    private const int MaximumBytes = 4096;

    private readonly string _storageDirectory;
    private readonly string _document;
    private readonly IInMemoryDiagnosticSink? _diagnostics;

    public UiPreferencesStore(string storageDirectory, IInMemoryDiagnosticSink? diagnostics = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageDirectory);
        if (!Path.IsPathFullyQualified(storageDirectory))
        {
            // A relative location would follow the working directory of whoever launched the shell.
            throw new ArgumentException("UI preference storage must be a fully qualified location.", nameof(storageDirectory));
        }

        _storageDirectory = storageDirectory;
        _document = Path.Combine(storageDirectory, DocumentName);
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Creates the current-user store, or returns null when the local application data root is unavailable: the
    /// shell still starts, only preference persistence stays off for that session.
    /// </summary>
    public static UiPreferencesStore? TryCreateCurrentUser(IInMemoryDiagnosticSink? diagnostics = null)
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            diagnostics?.Record(
                StorageUnavailableDiagnosticCode,
                "Current-user preference storage is unavailable; the session keeps the default language.");
            return null;
        }

        return new UiPreferencesStore(Path.Combine(localApplicationData, "VFXComposer"), diagnostics);
    }

    public UiPreferences? Load()
    {
        try
        {
            if (!File.Exists(_document))
            {
                // First run: absence is normal and needs no diagnostic.
                return null;
            }

            byte[] bytes;
            using (var stream = new FileStream(_document, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length < 1 || stream.Length > MaximumBytes)
                {
                    RecordUnusable("size");
                    return null;
                }

                bytes = new byte[checked((int)stream.Length)];
                var read = 0;
                while (read < bytes.Length)
                {
                    var count = stream.Read(bytes, read, bytes.Length - read);
                    if (count == 0)
                    {
                        RecordUnusable("truncated");
                        return null;
                    }

                    read += count;
                }
            }

            if (!UiPreferencesCodec.TryParse(Encoding.UTF8.GetString(bytes), out var preferences))
            {
                RecordUnusable("schema");
                return null;
            }

            return preferences;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            RecordUnusable(exception.GetType().Name);
            return null;
        }
    }

    public void Save(UiPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var bytes = Encoding.UTF8.GetBytes(UiPreferencesCodec.Serialize(preferences));
        var temporary = _document + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(_storageDirectory);
            using (var stream = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(_document))
            {
                File.Replace(temporary, _document, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporary, _document);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _diagnostics?.Record(
                SaveFailureDiagnosticCode,
                "The UI language preference could not be stored; it stays in effect for this session only.",
                exception.GetType().Name);
            Discard(temporary);
        }
    }

    private static void Discard(string temporary)
    {
        try
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The stored preference remains authoritative; a leftover temporary file changes no behaviour.
        }
    }

    // Only the failure kind is recorded: diagnostics never carry a storage location.
    private void RecordUnusable(string detail) => _diagnostics?.Record(
        LoadFailureDiagnosticCode,
        "Stored UI preferences were unusable; the default language applies.",
        detail);
}
