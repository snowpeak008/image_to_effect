namespace VFXComposer.Desktop.Services;

public interface IUiPreferencesStore
{
    /// <summary>
    /// Returns the stored preferences, or null when none is usable: absent, unreadable, corrupt or written by an
    /// unknown schema version. Fail-safe by design — a preference is not a security configuration, so an unusable
    /// file falls back to the default instead of blocking startup.
    /// </summary>
    UiPreferences? Load();

    /// <summary>
    /// Persists preferences atomically. Fail-safe: a storage failure is recorded as a diagnostic instead of being
    /// thrown, so a preference write can never break the running session.
    /// </summary>
    void Save(UiPreferences preferences);
}
