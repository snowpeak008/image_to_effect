using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VFXComposer.Tests.PlayMode.W24S0aFormalRuntime
{
    /// <summary>
    /// True PlayMode discovery proxy. The capture authority remains in the Editor assembly, while
    /// this assembly deliberately has no UnityEditor or Editor-assembly reference. Reflection is
    /// restricted to the single frozen authority type and public entry methods.
    /// </summary>
    [Explicit("Formal W24 S0a graphics capture. Requires generated sustained_flame_3d formal inputs and a graphics-backed Unity batch process; writes each calibration candidate once.")]
    public sealed class W24S0aFormalPlayModeProxyTests
    {
        private const string AuthorityTypeName =
            "VFXComposer.Tests.PlayMode.W24S0aFormal.W24S0aFormalCalibrationCaptureAuthority, VFXComposer.Tests.PlayMode.W24S0aFormal";

        [UnityTest]
        [Timeout(60 * 60 * 1000)]
        public IEnumerator Capture_Reduced66_OperatorOnlyMutants_FromAuthorityScene()
        {
            return RunAuthority("CaptureReduced66");
        }

        [UnityTest]
        [Timeout(60 * 60 * 1000)]
        public IEnumerator Capture_Full110_OperatorOnlyMutants_WhenTheFutureCohortExists()
        {
            return RunAuthority("CaptureFull110");
        }

        private static IEnumerator RunAuthority(string methodName)
        {
            Assert.That(Application.isPlaying, Is.True, "The S0a proxy must be driven by Unity's true PlayMode runner.");
            var type = Type.GetType(AuthorityTypeName, true);
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "The frozen S0a Editor authority entry is missing: " + methodName);
            var inner = method.Invoke(null, null) as IEnumerator;
            Assert.That(inner, Is.Not.Null, "The frozen S0a Editor authority must return an IEnumerator: " + methodName);
            while (inner.MoveNext()) yield return inner.Current;
        }
    }
}
