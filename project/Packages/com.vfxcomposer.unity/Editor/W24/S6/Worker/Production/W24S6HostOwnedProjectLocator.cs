using System;
using VFXComposer.Editor.W24.S6.Worker.Protocol;

namespace VFXComposer.Editor.W24.S6.Worker.Production
{
    /// <summary>
    /// Composition boundary retaining the exact immutable C3 projection.
    /// It does not copy identities or translate them into a path or capability.
    /// </summary>
    internal sealed class W24S6HostOwnedProjectLocator
    {
        internal W24S6HostOwnedProjectLocator(W24S6WorkerProjectLocator projection)
        {
            if (projection == null) throw new ArgumentNullException("projection");
            Projection = projection;
        }

        internal W24S6WorkerProjectLocator Projection { get; private set; }
    }
}
