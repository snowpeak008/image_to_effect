using System;
using UnityEngine;

namespace VFXComposer.W24
{
    /// <summary>
    /// Typed raw linear-LDR camera readback for W24 diagnostics.  This deliberately exposes
    /// float32 NPY arrays only: an encoded PNG is presentation evidence and is never accepted as
    /// the formal raw input for a pixel measurement.
    /// </summary>
    public static class W24LinearLdrDiagnosticCapture
    {
        public sealed class Result
        {
            public int Width;
            public int Height;
            /// <summary>Row-major, normalized linear RGBA float32 values in [0,1].</summary>
            public float[] LinearRgba;
            public float[] LinearRgb
            {
                get
                {
                    var rgb = new float[checked(Width * Height * 3)];
                    for (var pixel = 0; pixel < Width * Height; pixel++)
                    {
                        rgb[pixel * 3] = LinearRgba[pixel * 4];
                        rgb[pixel * 3 + 1] = LinearRgba[pixel * 4 + 1];
                        rgb[pixel * 3 + 2] = LinearRgba[pixel * 4 + 2];
                    }
                    return rgb;
                }
            }
            public byte[] LinearRgbNpy { get { return W24NpyWriter.EncodeFloat32(LinearRgb, Width, Height, 3); } }
            public byte[] LinearRgbaNpy { get { return W24NpyWriter.EncodeFloat32(LinearRgba, Width, Height, 4); } }
        }

        public static void ValidateFormatSupport(bool supportsArgb32Render, bool supportsRgba32Readback)
        {
            if (!supportsArgb32Render || !supportsRgba32Readback) throw new NotSupportedException("W24 linear-LDR diagnostics require ARGB32 render and RGBA32 synchronous readback support; PNG fallback is forbidden.");
        }

        public static void ValidateFormatSupport()
        {
            ValidateFormatSupport(SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32), SystemInfo.SupportsTextureFormat(TextureFormat.RGBA32));
        }

        public static void ValidateCaptureRequest(Camera formalMainCamera, int width, int height)
        {
            if (formalMainCamera == null) throw new ArgumentNullException(nameof(formalMainCamera));
            ValidateLinearColorSpace(QualitySettings.activeColorSpace);
            var view = W24ObjectIdDepthDiagnosticCapture.W24DiagnosticView.FromPose(formalMainCamera, "linear-ldr-current-camera", formalMainCamera.transform.position, formalMainCamera.transform.rotation);
            W24ObjectIdDepthDiagnosticCapture.ValidateCaptureRequest(formalMainCamera, view, width, height);
            if (formalMainCamera.allowHDR) throw new InvalidOperationException("W24 fixed-exposure linear-LDR diagnostics require the serialized MainCamera HDR flag to be disabled.");
            if (formalMainCamera.allowMSAA) throw new InvalidOperationException("W24 fixed-exposure linear-LDR diagnostics require single-sample MainCamera output.");
        }

        public static void ValidateLinearColorSpace(ColorSpace activeColorSpace)
        {
            if (activeColorSpace != ColorSpace.Linear) throw new InvalidOperationException("W24 linear-LDR diagnostics require QualitySettings.activeColorSpace == Linear; gamma projects are not a formal raw-measurement fallback.");
        }

        public static Result Capture(Camera formalMainCamera, int width, int height)
        {
            ValidateCaptureRequest(formalMainCamera, width, height);
            ValidateFormatSupport();
            using (new W24ObjectIdDepthDiagnosticCapture.CameraState(formalMainCamera))
                return CaptureCore(formalMainCamera, width, height);
        }

        private static Result CaptureCore(Camera camera, int width, int height)
        {
            RenderTexture target = null;
            Texture2D readback = null;
            var previousActive = RenderTexture.active;
            try
            {
                target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                readback = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0, false); readback.Apply(false, false);
                var pixels = readback.GetPixels32();
                if (pixels.Length != checked(width * height)) throw new InvalidOperationException("W24 linear-LDR readback has unexpected dimensions.");
                var values = new float[checked(pixels.Length * 4)];
                for (var index = 0; index < pixels.Length; index++)
                {
                    values[index * 4] = pixels[index].r / 255f;
                    values[index * 4 + 1] = pixels[index].g / 255f;
                    values[index * 4 + 2] = pixels[index].b / 255f;
                    values[index * 4 + 3] = pixels[index].a / 255f;
                }
                ValidateLinearUnitRange(values);
                return new Result { Width = width, Height = height, LinearRgba = values };
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
                if (target != null) RenderTexture.ReleaseTemporary(target);
            }
        }

        public static void ValidateLinearUnitRange(float[] values)
        {
            if (values == null || values.Length == 0) throw new ArgumentException("W24 linear-LDR diagnostic values are required.", nameof(values));
            foreach (var value in values)
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f) throw new ArgumentException("W24 linear-LDR diagnostics must be finite normalized values in [0,1].", nameof(values));
        }
    }
}
