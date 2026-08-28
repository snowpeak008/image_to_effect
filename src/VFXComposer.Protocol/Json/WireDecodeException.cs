using VFXComposer.Protocol.Diagnostics;

namespace VFXComposer.Protocol.Json;

/// <summary>A content-free wire failure backed only by the frozen diagnostic catalog.</summary>
public sealed class WireDecodeException : FormatException
{
    internal WireDecodeException(string diagnosticCode)
        : this(StableDiagnosticCatalog.Create(diagnosticCode))
    {
    }

    private WireDecodeException(StableDiagnostic diagnostic)
        : base(diagnostic.Message)
    {
        Diagnostic = diagnostic;
    }

    public StableDiagnostic Diagnostic { get; }
}
