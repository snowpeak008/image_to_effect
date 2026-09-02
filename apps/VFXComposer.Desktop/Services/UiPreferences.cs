using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.Services;

/// <summary>
/// Current-user UI preferences. Kept apart from provider/job configuration on purpose: this is presentation state,
/// not a security or revision-bound configuration.
/// </summary>
public sealed record UiPreferences(UiLanguage Language, GenerationMode GenerationMode = GenerationMode.Simple);
