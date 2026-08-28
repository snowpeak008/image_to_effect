using System;

namespace VFXComposer.Editor.W24.S6.Worker.Production
{
    /// <summary>
    /// Locator-binding edge for an ordinary-user Worker started by its current-user host.
    /// Child launch, pipe/session admission, project reads, handles and authority live outside U1.
    /// </summary>
    internal sealed class W24S6DedicatedWorkerConnector
    {
        private W24S6HostOwnedProjectLocator _acceptedLocator;

        internal bool IsConnected
        {
            get { return _acceptedLocator != null; }
        }

        internal W24S6HostOwnedProjectLocator AcceptHostOwnedLocator(byte[] exactLocatorBytes)
        {
            var candidate = new W24S6HostOwnedProjectLocator(
                W24S6ProductionWorkerWireCodec.ProjectLocator(exactLocatorBytes));
            if (_acceptedLocator != null)
                throw new InvalidOperationException("A Worker locator is already bound.");
            _acceptedLocator = candidate;
            return candidate;
        }

        internal void Disconnect()
        {
            _acceptedLocator = null;
        }
    }
}
