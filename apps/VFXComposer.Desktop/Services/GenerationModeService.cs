using CommunityToolkit.Mvvm.ComponentModel;

namespace VFXComposer.Desktop.Services;

/// <summary>
/// Single source of the Create-page generation mode for the shell, mirroring <see cref="Localization.LocalizationService"/>:
/// selecting a mode applies immediately, notifies bound visibility, and persists for the current user. Switching is
/// pure presentation (REQ-004-01): it makes no network request and never touches a draft or version chain — this
/// type holds no reference through which either would even be reachable.
/// </summary>
public sealed class GenerationModeService : ObservableObject
{
    private readonly IUiPreferencesStore? _preferences;
    private GenerationMode _mode;

    public GenerationModeService(GenerationMode mode = GenerationMode.Simple, IUiPreferencesStore? preferences = null)
    {
        ThrowIfUnsupported(mode);
        _mode = mode;
        _preferences = preferences;
    }

    /// <summary>Raised after <see cref="Mode"/> changed, for view models that derive gated visibility.</summary>
    public event EventHandler? ModeChanged;

    public GenerationMode Mode => _mode;

    public bool IsProfessional => _mode == GenerationMode.Professional;

    public void SetMode(GenerationMode mode)
    {
        ThrowIfUnsupported(mode);
        if (_mode == mode)
        {
            return;
        }

        _mode = mode;
        OnPropertyChanged(nameof(Mode));
        OnPropertyChanged(nameof(IsProfessional));
        ModeChanged?.Invoke(this, EventArgs.Empty);
        if (_preferences is not null)
        {
            // Merge into the stored document (same discipline as the language switch): changing the mode must never
            // reset the stored language. An absent or unusable document contributes only defaults.
            var stored = _preferences.Load() ?? new UiPreferences(Localization.UiLanguages.FromCurrentUiCulture());
            _preferences.Save(stored with { GenerationMode = mode });
        }
    }

    private static void ThrowIfUnsupported(GenerationMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }
}
