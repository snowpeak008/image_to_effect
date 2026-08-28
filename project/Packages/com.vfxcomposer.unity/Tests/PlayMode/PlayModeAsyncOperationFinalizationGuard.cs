using System;
using NUnit.Framework;

namespace VFXComposer.Tests.PlayMode
{
    /// <summary>
    /// Unity 2022 can leave completed scene AsyncOperation wrappers for the Mono finalizer
    /// after the native scene manager has begun Editor shutdown. Draining them while the
    /// engine is still alive prevents a post-success native crash without changing product code.
    /// </summary>
    [SetUpFixture]
    public sealed class PlayModeAsyncOperationFinalizationGuard
    {
        [OneTimeTearDown]
        public void DrainCompletedSceneOperationFinalizers()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
