namespace VFXComposer.Desktop.Services;

/// <summary>
/// The closed set of Create-page presentation modes (REQ-004-01). Exactly one is in effect at any time; both render
/// the same stores and lineages, so switching is pure presentation and never touches a draft.
/// </summary>
public enum GenerationMode
{
    /// <summary>Example cards, free-text AI generation, suggestions and capability notices (REQ-004 §5.1).</summary>
    Simple,

    /// <summary>Everything of Simple plus the parameter panel, refinement input, version chain and timeline (§5.2).</summary>
    Professional,
}
