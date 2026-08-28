using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VFXComposer.W24;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W24ObjectIdDepthDiagnosticTests
    {
        [Test]
        public void NpyUInt32AndFloat32_AreLittleEndianNpyV1_WithExactDimensions()
        {
            var ids = W24NpyWriter.EncodeUInt32(new uint[] { 1u, 429u, 1024u, 77u }, 2, 2);
            var depths = W24NpyWriter.EncodeFloat32(new[] { .1f, 1.25f, 3.5f, 9f }, 2, 2);
            AssertNpyV1(ids, "<u4", 2, 2, new uint[] { 1u, 429u, 1024u, 77u });
            AssertNpyV1(depths, "<f4", 2, 2, new[] { .1f, 1.25f, 3.5f, 9f });
            Assert.Throws<ArgumentException>(() => W24NpyWriter.EncodeFloat32(new[] { float.NaN }, 1, 1));
            Assert.Throws<ArgumentException>(() => W24NpyWriter.EncodeFloat32(new[] { float.PositiveInfinity }, 1, 1));
        }

        [Test]
        public void Registration_RejectsMissingDuplicateAndUnsupportedBindings()
        {
            var one = new GameObject("one"); var two = new GameObject("two");
            try
            {
                Assert.Throws<InvalidOperationException>(() => W24DiagnosticObjectRegistration.Validate(new W24DiagnosticObjectRegistration[] { null }));
                var first = one.AddComponent<W24DiagnosticObjectRegistration>(); first.Configure(null, 1u, "first", true);
                Assert.Throws<InvalidOperationException>(() => W24DiagnosticObjectRegistration.Validate(new[] { first }));
                var firstRenderer = one.AddComponent<MeshRenderer>(); first.Configure(firstRenderer, 7u, "first", true);
                Assert.Throws<InvalidOperationException>(() => W24DiagnosticObjectRegistration.Validate(new[] { first, null }));
                var second = two.AddComponent<W24DiagnosticObjectRegistration>(); second.Configure(two.AddComponent<MeshRenderer>(), 7u, "second", true);
                Assert.Throws<InvalidOperationException>(() => W24DiagnosticObjectRegistration.Validate(new[] { first, second }));
                second.Configure(two.AddComponent<ParticleSystem>().GetComponent<ParticleSystemRenderer>(), 8u, "second", true);
                Assert.Throws<NotSupportedException>(() => W24DiagnosticObjectRegistration.Validate(new[] { first, second }));
                second.Configure(two.AddComponent<MeshRenderer>(), 8u, "second", true); two.SetActive(false);
                Assert.Throws<InvalidOperationException>(() => W24DiagnosticObjectRegistration.Validate(new[] { first, second }));
            }
            finally { UnityEngine.Object.DestroyImmediate(one); UnityEngine.Object.DestroyImmediate(two); }
        }

        [Test]
        public void FormatPolicy_FailsClosed_WhenAnyFormalFormatCapabilityIsMissing()
        {
            Assert.DoesNotThrow(() => W24ObjectIdDepthDiagnosticCapture.ValidateFormatSupport(true, true, true, true, true));
            Assert.Throws<NotSupportedException>(() => W24ObjectIdDepthDiagnosticCapture.ValidateFormatSupport(false, true, true, true, true));
            Assert.Throws<NotSupportedException>(() => W24ObjectIdDepthDiagnosticCapture.ValidateFormatSupport(true, true, true, false, true));
            Assert.Throws<NotSupportedException>(() => W24ObjectIdDepthDiagnosticCapture.ValidateFormatSupport(true, true, true, true, false));
        }

        [Test]
        public void CaptureRequest_RejectsInvalidFormalCameraViewAndDimensionsBeforeGraphicsWork()
        {
            var go = new GameObject("MainCamera"); var camera = go.AddComponent<Camera>();
            try
            {
                go.tag = "MainCamera";
                var view = W24ObjectIdDepthDiagnosticCapture.W24DiagnosticView.FromPose(camera, "front", new Vector3(0, 1, -3), Quaternion.identity);
                Assert.DoesNotThrow(() => W24ObjectIdDepthDiagnosticCapture.ValidateCaptureRequest(camera, view, 960, 540));
                view.ViewId = ""; Assert.Throws<ArgumentException>(() => W24ObjectIdDepthDiagnosticCapture.ValidateCaptureRequest(camera, view, 960, 540));
                view = W24ObjectIdDepthDiagnosticCapture.W24DiagnosticView.FromPose(camera, "front", new Vector3(0, 1, -3), Quaternion.identity);
                Assert.Throws<ArgumentOutOfRangeException>(() => W24ObjectIdDepthDiagnosticCapture.ValidateCaptureRequest(camera, view, 0, 540));
                view.ProjectionMatrix = Matrix4x4.zero; Assert.Throws<ArgumentException>(() => W24ObjectIdDepthDiagnosticCapture.ValidateCaptureRequest(camera, view, 960, 540));
                go.tag = "Untagged"; view = W24ObjectIdDepthDiagnosticCapture.W24DiagnosticView.FromPose(camera, "front", new Vector3(0, 1, -3), Quaternion.identity);
                Assert.Throws<InvalidOperationException>(() => W24ObjectIdDepthDiagnosticCapture.ValidateCaptureRequest(camera, view, 960, 540));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void FromPose_MapsWorldForwardToUnityCameraNegativeZ_AndRejectsWrongHandedView()
        {
            var go = new GameObject("MainCamera"); var camera = go.AddComponent<Camera>();
            try
            {
                go.tag = "MainCamera";
                var position = new Vector3(2f, 3f, -4f); var rotation = Quaternion.Euler(7f, 19f, -3f);
                var view = W24ObjectIdDepthDiagnosticCapture.W24DiagnosticView.FromPose(camera, "oblique", position, rotation);
                var forward = view.WorldToCameraMatrix.MultiplyPoint(position + rotation * Vector3.forward);
                Assert.That(forward.x, Is.EqualTo(0f).Within(1e-4f));
                Assert.That(forward.y, Is.EqualTo(0f).Within(1e-4f));
                Assert.That(forward.z, Is.EqualTo(-1f).Within(1e-4f));
                Assert.DoesNotThrow(() => W24ObjectIdDepthDiagnosticCapture.ValidateCaptureRequest(camera, view, 960, 540));
                view.WorldToCameraMatrix = Matrix4x4.TRS(position, rotation, Vector3.one).inverse;
                Assert.Throws<ArgumentException>(() => W24ObjectIdDepthDiagnosticCapture.ValidateCaptureRequest(camera, view, 960, 540));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void CameraState_RestoresCameraAndTransformAfterDiagnosticMutation()
        {
            var root = new GameObject("camera-root"); var go = new GameObject("camera"); go.transform.SetParent(root.transform); var camera = go.AddComponent<Camera>();
            var target = new RenderTexture(8, 8, 0);
            try
            {
                go.transform.localPosition = new Vector3(1, 2, 3); go.transform.localRotation = Quaternion.Euler(4, 5, 6); go.transform.localScale = new Vector3(2, 3, 4);
                camera.targetTexture = target; camera.clearFlags = CameraClearFlags.Depth; camera.backgroundColor = Color.magenta; camera.cullingMask = 123; camera.allowHDR = false; camera.allowMSAA = false; camera.enabled = false;
                var customView = Matrix4x4.Translate(new Vector3(3, 4, 5)); var customProjection = Matrix4x4.Scale(new Vector3(2, 3, 4)); camera.worldToCameraMatrix = customView; camera.projectionMatrix = customProjection;
                try
                {
                    using (new W24ObjectIdDepthDiagnosticCapture.CameraState(camera))
                    {
                        go.transform.SetParent(null); go.transform.SetPositionAndRotation(Vector3.one * 99, Quaternion.identity); camera.targetTexture = null; camera.clearFlags = CameraClearFlags.Nothing; camera.backgroundColor = Color.black; camera.cullingMask = 0; camera.allowHDR = true; camera.allowMSAA = true; camera.enabled = true; camera.worldToCameraMatrix = Matrix4x4.zero; camera.projectionMatrix = Matrix4x4.zero;
                        throw new InvalidOperationException("test exception");
                    }
                }
                catch (InvalidOperationException exception) { Assert.That(exception.Message, Is.EqualTo("test exception")); }
                Assert.That(go.transform.parent, Is.EqualTo(root.transform)); Assert.That(go.transform.localPosition, Is.EqualTo(new Vector3(1, 2, 3))); Assert.That(Quaternion.Angle(go.transform.localRotation, Quaternion.Euler(4, 5, 6)), Is.LessThan(.01f)); Assert.That(go.transform.localScale, Is.EqualTo(new Vector3(2, 3, 4)));
                Assert.That(camera.targetTexture, Is.EqualTo(target)); Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.Depth)); Assert.That(camera.backgroundColor, Is.EqualTo(Color.magenta)); Assert.That(camera.cullingMask, Is.EqualTo(123)); Assert.That(camera.allowHDR, Is.False); Assert.That(camera.allowMSAA, Is.False); Assert.That(camera.enabled, Is.False);
                Assert.That(camera.worldToCameraMatrix, Is.EqualTo(customView)); Assert.That(camera.projectionMatrix, Is.EqualTo(customProjection));
            }
            finally { UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void AssertNpyV1(byte[] bytes, string dtype, int width, int height, uint[] expected)
        {
            CollectionAssert.AreEqual(new byte[] { 0x93, (byte)'N', (byte)'U', (byte)'M', (byte)'P', (byte)'Y', 1, 0 }, bytes.Take(8).ToArray());
            var headerLength = bytes[8] | (bytes[9] << 8); var header = System.Text.Encoding.ASCII.GetString(bytes, 10, headerLength);
            StringAssert.Contains("'descr': '" + dtype + "'", header); StringAssert.Contains("'fortran_order': False", header); StringAssert.Contains("'shape': (" + height + ", " + width + ")", header);
            Assert.That((10 + headerLength) % 16, Is.EqualTo(0)); Assert.That(bytes.Length, Is.EqualTo(10 + headerLength + expected.Length * 4));
            for (var index = 0; index < expected.Length; index++) Assert.That(BitConverter.ToUInt32(bytes, 10 + headerLength + index * 4), Is.EqualTo(expected[index]));
        }

        private static void AssertNpyV1(byte[] bytes, string dtype, int width, int height, float[] expected)
        {
            CollectionAssert.AreEqual(new byte[] { 0x93, (byte)'N', (byte)'U', (byte)'M', (byte)'P', (byte)'Y', 1, 0 }, bytes.Take(8).ToArray());
            var headerLength = bytes[8] | (bytes[9] << 8); var header = System.Text.Encoding.ASCII.GetString(bytes, 10, headerLength);
            StringAssert.Contains("'descr': '" + dtype + "'", header); StringAssert.Contains("'fortran_order': False", header); StringAssert.Contains("'shape': (" + height + ", " + width + ")", header);
            Assert.That((10 + headerLength) % 16, Is.EqualTo(0)); Assert.That(bytes.Length, Is.EqualTo(10 + headerLength + expected.Length * 4));
            for (var index = 0; index < expected.Length; index++) Assert.That(BitConverter.ToSingle(bytes, 10 + headerLength + index * 4), Is.EqualTo(expected[index]).Within(0f));
        }
    }
}
