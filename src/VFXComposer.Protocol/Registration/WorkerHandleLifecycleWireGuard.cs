namespace VFXComposer.Protocol.Registration;

internal static class WorkerHandleLifecycleWireGuard
{
    internal static void RequireHeader(
        string protocolVersion,
        string messageKind,
        string expectedMessageKind,
        long brokerGeneration,
        long leaseGeneration)
    {
        if (!string.Equals(protocolVersion, ProtocolVersions.Current, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported protocol version.", nameof(protocolVersion));
        }

        if (!string.Equals(messageKind, expectedMessageKind, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unexpected message kind.", nameof(messageKind));
        }

        if (brokerGeneration <= 0 || leaseGeneration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(brokerGeneration));
        }
    }
}
