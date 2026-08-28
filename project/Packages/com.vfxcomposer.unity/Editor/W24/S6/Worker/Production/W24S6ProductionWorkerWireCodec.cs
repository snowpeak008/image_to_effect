using VFXComposer.Editor.W24.S6.Worker.Protocol;

namespace VFXComposer.Editor.W24.S6.Worker.Production
{
    /// <summary>
    /// Zero-semantics U1 composition facade over the single ADR-003 adapter.
    /// It declares no wire model, token, decoder, encoder, sealer or canonicalizer.
    /// </summary>
    internal static class W24S6ProductionWorkerWireCodec
    {
        internal static W24S6WorkerProjectLocator ProjectLocator(byte[] exactLocatorBytes)
        {
            return W24S6WorkerProtocolCodec.DecodeLocator(exactLocatorBytes);
        }
    }
}
