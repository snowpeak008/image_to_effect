namespace VFXComposer.Desktop.Services;

public sealed record UiDiagnostic(
    long Sequence,
    string Code,
    string Message,
    string? Detail);
