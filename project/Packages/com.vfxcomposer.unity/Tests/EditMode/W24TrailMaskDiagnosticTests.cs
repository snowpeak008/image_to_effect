using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VFXComposer.W24;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W24TrailMaskDiagnosticTests
    {
        [Test]
        public void BinaryNpy_IsExactUnsignedByteMaskAndRejectsNonBinaryValues()
        {
            var bytes = W24NpyWriter.EncodeBinaryUInt8(new byte[] { 0, 255, 255, 0 }, 2, 2);
            CollectionAssert.AreEqual(new byte[] { 0x93, (byte)'N', (byte)'U', (byte)'M', (byte)'P', (byte)'Y', 1, 0 }, bytes.Take(8).ToArray());
            var headerLength = bytes[8] | (bytes[9] << 8); var header = System.Text.Encoding.ASCII.GetString(bytes, 10, headerLength);
            StringAssert.Contains("'descr': '|u1'", header); StringAssert.Contains("'fortran_order': False", header); StringAssert.Contains("'shape': (2, 2)", header);
            Assert.That((10 + headerLength) % 16, Is.EqualTo(0)); CollectionAssert.AreEqual(new byte[] { 0, 255, 255, 0 }, bytes.Skip(10 + headerLength).ToArray());
            Assert.Throws<ArgumentException>(() => W24NpyWriter.EncodeBinaryUInt8(new byte[] { 1 }, 1, 1));
        }

        [Test]
        public void FormatPolicy_FailsClosedWithoutR8ReadbackOrDepthStencil()
        {
            Assert.DoesNotThrow(() => W24TrailMaskDiagnosticCapture.ValidateFormatSupport(true, true, true));
            Assert.Throws<NotSupportedException>(() => W24TrailMaskDiagnosticCapture.ValidateFormatSupport(false, true, true));
            Assert.Throws<NotSupportedException>(() => W24TrailMaskDiagnosticCapture.ValidateFormatSupport(true, false, true));
            Assert.Throws<NotSupportedException>(() => W24TrailMaskDiagnosticCapture.ValidateFormatSupport(true, true, false));
        }

        [Test]
        public void Projection_UsesCallerEmitterHistoryAndFrozenMatrices_NotTrailVertices()
        {
            var view = Matrix4x4.identity; var projection = Matrix4x4.identity;
            var projected = W24TrailMaskDiagnosticCapture.ProjectAllToPixels(new[] { new Vector3(-1, -1, 0), new Vector3(1, 1, 0) }, view, projection, 101, 51);
            Assert.That(projected[0], Is.EqualTo(new Vector2(0, 0))); Assert.That(projected[1], Is.EqualTo(new Vector2(100, 50)));
            var topOriginTarget = W24TrailMaskDiagnosticCapture.ProjectAllToPixels(new[] { new Vector3(-1, -1, 0), new Vector3(1, 1, 0) }, view, projection, 101, 51, true);
            Assert.That(topOriginTarget[0], Is.EqualTo(new Vector2(0, 50))); Assert.That(topOriginTarget[1], Is.EqualTo(new Vector2(100, 0)));
            Assert.Throws<InvalidOperationException>(() => W24TrailMaskDiagnosticCapture.ProjectAllToPixels(new[] { new Vector3(2, 0, 0), new Vector3(0, 0, 0) }, view, projection, 10, 10));
            Assert.Throws<ArgumentException>(() => W24TrailMaskDiagnosticCapture.ProjectAllToPixels(new[] { new Vector3(float.NaN, 0, 0), Vector3.zero }, view, projection, 10, 10));
        }
    }
}
