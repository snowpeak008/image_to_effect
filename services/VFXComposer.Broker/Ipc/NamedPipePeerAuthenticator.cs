using System.IO.Pipes;
using VFXComposer.Protocol.Ipc;

namespace VFXComposer.Broker.Ipc;

internal sealed class NamedPipePeerAuthenticator
{
    private readonly IPeerFactsSource _factsSource;
    private readonly PeerSessionRegistry _sessions;

    public NamedPipePeerAuthenticator(
        IPeerFactsSource factsSource,
        PeerSessionRegistry sessions)
    {
        _factsSource = factsSource ?? throw new ArgumentNullException(nameof(factsSource));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
    }

    public bool TryAuthenticate(
        NamedPipeServerStream connectedPipe,
        PeerHello hello,
        out AuthenticatedPeerSession? session,
        out PeerSessionAccepted? receipt,
        out string diagnosticCode)
    {
        ArgumentNullException.ThrowIfNull(connectedPipe);
        if (!connectedPipe.IsConnected)
        {
            throw new InvalidOperationException("Pipe is not connected.");
        }

        var observed = _factsSource.Observe(connectedPipe, hello.PeerRole);
        return _sessions.TryAuthenticate(
            hello,
            observed,
            out session,
            out receipt,
            out diagnosticCode);
    }

    internal void Revoke(string sessionId) => _sessions.Revoke(sessionId);
}
