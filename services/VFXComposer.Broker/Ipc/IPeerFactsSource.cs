using System.IO.Pipes;

namespace VFXComposer.Broker.Ipc;

/// <summary>
/// OS-observed facts for one connected local pipe peer. The Windows implementation
/// exists, but the shipped entry point cannot activate it until a host-owned policy
/// issuer and production pipe ACL/profile have passed their separate gate.
/// </summary>
internal interface IPeerFactsSource
{
    ObservedPeerFacts Observe(NamedPipeServerStream connectedPipe, string claimedPeerRole);
}
