using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VFXComposer.W24;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W24TypedRawDiagnosticCaptureTests
    {
        [Test]
        public void Float32Npy_HwcRgbAndRgba_AreFiniteCContiguousAndExactShape()
        {
            var rgb = W24NpyWriter.EncodeFloat32(new[] { 0f, .25f, .5f, .75f, 1f, .125f }, 2, 1, 3);
            var rgba = W24NpyWriter.EncodeFloat32(new[] { 0f, .25f, .5f, 1f, .75f, 1f, .125f, 1f }, 2, 1, 4);
            AssertNpyFloatHwc(rgb, 2, 1, 3, new[] { 0f, .25f, .5f, .75f, 1f, .125f });
            AssertNpyFloatHwc(rgba, 2, 1, 4, new[] { 0f, .25f, .5f, 1f, .75f, 1f, .125f, 1f });
            Assert.Throws<ArgumentOutOfRangeException>(() => W24NpyWriter.EncodeFloat32(new float[2], 1, 2, 2));
            Assert.Throws<ArgumentException>(() => W24NpyWriter.EncodeFloat32(new float[5], 2, 1, 3));
            Assert.Throws<ArgumentException>(() => W24NpyWriter.EncodeFloat32(new[] { 0f, float.NaN, 0f }, 1, 1, 3));
        }

        [Test]
        public void RendererMask_PreflightRejectsEmptyNullDisabledInactiveAndDuplicateMembership()
        {
            var one = new GameObject("mask-one"); var two = new GameObject("mask-two");
            Material material = null;
            try
            {
                Assert.Throws<InvalidOperationException>(() => W24RendererMaskDiagnosticCapture.ValidateRenderers(new Renderer[0]));
                Assert.Throws<InvalidOperationException>(() => W24RendererMaskDiagnosticCapture.ValidateRenderers(new Renderer[] { null }));
                var shader = Shader.Find("Standard"); Assert.NotNull(shader, "EditMode needs Unity's built-in Standard shader for renderer preflight.");
                material = new Material(shader);
                var first = one.AddComponent<MeshRenderer>(); one.AddComponent<MeshFilter>(); first.sharedMaterial = material;
                first.enabled = false; Assert.Throws<InvalidOperationException>(() => W24RendererMaskDiagnosticCapture.ValidateRenderers(new[] { first }));
                first.enabled = true; two.SetActive(false); var second = two.AddComponent<MeshRenderer>(); two.AddComponent<MeshFilter>(); second.sharedMaterial = material;
                Assert.Throws<InvalidOperationException>(() => W24RendererMaskDiagnosticCapture.ValidateRenderers(new[] { first, second }));
                two.SetActive(true); Assert.Throws<InvalidOperationException>(() => W24RendererMaskDiagnosticCapture.ValidateRenderers(new[] { first, first }));
                Assert.DoesNotThrow(() => W24RendererMaskDiagnosticCapture.ValidateRenderers(new[] { first, second }));
            }
            finally { if (material != null) UnityEngine.Object.DestroyImmediate(material); UnityEngine.Object.DestroyImmediate(one); UnityEngine.Object.DestroyImmediate(two); }
        }

        [Test]
        public void TypedRawCapturePolicies_FailClosedWithoutFormatsOrFixedLdrCameraState()
        {
            Assert.DoesNotThrow(() => W24RendererMaskDiagnosticCapture.ValidateFormatSupport(true, true, true));
            Assert.Throws<NotSupportedException>(() => W24RendererMaskDiagnosticCapture.ValidateFormatSupport(false, true, true));
            Assert.Throws<NotSupportedException>(() => W24RendererMaskDiagnosticCapture.ValidateFormatSupport(true, false, true));
            Assert.Throws<NotSupportedException>(() => W24RendererMaskDiagnosticCapture.ValidateFormatSupport(true, true, false));
            Assert.DoesNotThrow(() => W24LinearLdrDiagnosticCapture.ValidateFormatSupport(true, true));
            Assert.Throws<NotSupportedException>(() => W24LinearLdrDiagnosticCapture.ValidateFormatSupport(false, true));
            Assert.Throws<NotSupportedException>(() => W24LinearLdrDiagnosticCapture.ValidateFormatSupport(true, false));
            Assert.Throws<ArgumentException>(() => W24LinearLdrDiagnosticCapture.ValidateLinearUnitRange(new[] { -0.001f }));
            Assert.Throws<ArgumentException>(() => W24LinearLdrDiagnosticCapture.ValidateLinearUnitRange(new[] { 1.001f }));
            Assert.Throws<ArgumentException>(() => W24LinearLdrDiagnosticCapture.ValidateLinearUnitRange(new[] { float.PositiveInfinity }));
            Assert.DoesNotThrow(() => W24LinearLdrDiagnosticCapture.ValidateLinearColorSpace(ColorSpace.Linear));
            Assert.Throws<InvalidOperationException>(() => W24LinearLdrDiagnosticCapture.ValidateLinearColorSpace(ColorSpace.Gamma));

            var go = new GameObject("MainCamera"); var camera = go.AddComponent<Camera>();
            try
            {
                go.tag = "MainCamera"; camera.allowHDR = false; camera.allowMSAA = false;
                if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                    Assert.DoesNotThrow(() => W24LinearLdrDiagnosticCapture.ValidateCaptureRequest(camera, 16, 16));
                else
                    Assert.Throws<InvalidOperationException>(() => W24LinearLdrDiagnosticCapture.ValidateCaptureRequest(camera, 16, 16), "A Gamma project must fail closed instead of pretending its readback is linear LDR.");
                camera.allowHDR = true; Assert.Throws<InvalidOperationException>(() => W24LinearLdrDiagnosticCapture.ValidateCaptureRequest(camera, 16, 16));
                camera.allowHDR = false; camera.allowMSAA = true; Assert.Throws<InvalidOperationException>(() => W24LinearLdrDiagnosticCapture.ValidateCaptureRequest(camera, 16, 16));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void TypedRawLdrResult_OnlyProvidesNpyAndDoesNotEncodePresentationPng()
        {
            var result = new W24LinearLdrDiagnosticCapture.Result { Width = 1, Height = 1, LinearRgba = new[] { .1f, .2f, .3f, 1f } };
            AssertNpyFloatHwc(result.LinearRgbNpy, 1, 1, 3, new[] { .1f, .2f, .3f });
            AssertNpyFloatHwc(result.LinearRgbaNpy, 1, 1, 4, new[] { .1f, .2f, .3f, 1f });
            Assert.IsFalse(typeof(W24LinearLdrDiagnosticCapture.Result).GetProperties().Any(property => property.PropertyType == typeof(byte[]) && property.Name.IndexOf("Png", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        [Test]
        public void TypedRawCaptureCameraState_RestoresSerializedMainCameraAfterFailure()
        {
            var root = new GameObject("typed-raw-camera-root"); var go = new GameObject("MainCamera"); go.transform.SetParent(root.transform); var camera = go.AddComponent<Camera>();
            var target = new RenderTexture(8, 8, 0);
            try
            {
                go.tag = "MainCamera"; go.transform.localPosition = new Vector3(1, 2, 3); camera.targetTexture = target; camera.clearFlags = CameraClearFlags.Depth; camera.cullingMask = 123; camera.allowHDR = false; camera.allowMSAA = false;
                try
                {
                    using (new W24ObjectIdDepthDiagnosticCapture.CameraState(camera))
                    {
                        go.transform.SetParent(null); go.transform.position = Vector3.one * 99; camera.targetTexture = null; camera.clearFlags = CameraClearFlags.Nothing; camera.cullingMask = 0; camera.allowHDR = true; camera.allowMSAA = true;
                        throw new InvalidOperationException("typed raw test failure");
                    }
                }
                catch (InvalidOperationException error) { Assert.That(error.Message, Is.EqualTo("typed raw test failure")); }
                Assert.That(go.transform.parent, Is.EqualTo(root.transform)); Assert.That(go.transform.localPosition, Is.EqualTo(new Vector3(1, 2, 3)));
                Assert.That(camera.targetTexture, Is.EqualTo(target)); Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.Depth)); Assert.That(camera.cullingMask, Is.EqualTo(123)); Assert.That(camera.allowHDR, Is.False); Assert.That(camera.allowMSAA, Is.False);
            }
            finally
            {
                // Unity reports an error if the RenderTexture is released while it is still the
                // serialized Camera target.  Detach it explicitly so the state-restoration test
                // also proves that cleanup leaves no dangling graphics reference.
                if (camera != null) camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AssertNpyFloatHwc(byte[] bytes, int width, int height, int channels, float[] expected)
        {
            CollectionAssert.AreEqual(new byte[] { 0x93, (byte)'N', (byte)'U', (byte)'M', (byte)'P', (byte)'Y', 1, 0 }, bytes.Take(8).ToArray());
            var headerLength = bytes[8] | (bytes[9] << 8); var header = System.Text.Encoding.ASCII.GetString(bytes, 10, headerLength);
            StringAssert.Contains("'descr': '<f4'", header); StringAssert.Contains("'fortran_order': False", header); StringAssert.Contains("'shape': (" + height + ", " + width + ", " + channels + ")", header);
            Assert.That((10 + headerLength) % 16, Is.EqualTo(0)); Assert.That(bytes.Length, Is.EqualTo(10 + headerLength + expected.Length * 4));
            for (var index = 0; index < expected.Length; index++) Assert.That(BitConverter.ToSingle(bytes, 10 + headerLength + index * 4), Is.EqualTo(expected[index]).Within(0f));
        }
    }
}
