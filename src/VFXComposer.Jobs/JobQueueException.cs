namespace VFXComposer.Jobs;

/// <summary>
/// Typed queue failure carrying one stable jobs-domain diagnostic code. The exception message
/// is always the fixed catalog message for that code, never caller-authored or path-bearing text.
/// </summary>
public sealed class JobQueueException : Exception
{
    public JobQueueException(string code)
        : base(JobQueueDiagnosticCatalog.Require(code).Message)
    {
        Code = code;
    }

    public JobQueueException(string code, Exception innerException)
        : base(JobQueueDiagnosticCatalog.Require(code).Message, innerException)
    {
        Code = code;
    }

    /// <summary>Stable code from <see cref="JobQueueDiagnosticCodes"/>.</summary>
    public string Code { get; }
}
