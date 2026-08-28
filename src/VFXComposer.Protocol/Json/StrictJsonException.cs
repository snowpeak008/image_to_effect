namespace VFXComposer.Protocol.Json;

public sealed class StrictJsonException : FormatException
{
    public StrictJsonException(string reasonCode, string message)
        : base(message)
    {
        ReasonCode = reasonCode;
    }

    public StrictJsonException(string reasonCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ReasonCode = reasonCode;
    }

    public string ReasonCode { get; }
}
