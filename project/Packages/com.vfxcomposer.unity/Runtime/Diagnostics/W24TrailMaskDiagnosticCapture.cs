using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace VFXComposer.W24
{
    /// <summary>
    /// Formal trail-only binary-mask capture. The rendered mask comes only from an explicitly
    /// supplied TrailRenderer; the expected corridor is supplied by external per-frame emitter
    /// history and is never reconstructed from TrailRenderer vertices.
    /// </summary>
    public static class W24TrailMaskDiagnosticCapture
    {
        public const string ShaderName = "Hidden/VFXComposer/W24/TrailMask";

        public sealed class Result
        {
            public int Width;
            public int Height;
            public byte[] BinaryMask;
            public Vector2[] ProjectedEmitterHistoryPixels;
            public byte[] BinaryMaskNpy { get { return W24NpyWriter.EncodeBinaryUInt8(BinaryMask, Width, Height); } }
        }

        public static void ValidateFormatSupport(bool supportsR8Render, bool supportsR8Readback, bool supportsDepthStencilRender)
        {
            if (!supportsR8Render || !supportsR8Readback) throw new NotSupportedException("W24 formal trail-mask diagnostics require R8_UNorm render and synchronous readback support; Beauty fallback is forbidden.");
            if (!supportsDepthStencilRender) throw new NotSupportedException("W24 formal trail-mask diagnostics require D24_UNorm_S8_UInt render support for deterministic depth testing.");
        }

        public static void ValidateFormatSupport()
        {
            ValidateFormatSupport(
                SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, FormatUsage.Render),
                SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, FormatUsage.ReadPixels),
                SystemInfo.IsFormatSupported(GraphicsFormat.D24_UNorm_S8_UInt, FormatUsage.Render));
        }

        public static Result Capture(Camera formalMainCamera, TrailRenderer trail, IEnumerable<Vector3> emitterHistory, int width, int height)
        {
            var input = BuildInput(formalMainCamera, trail, emitterHistory, width, height);
            ValidateFormatSupport();
            var shader = Shader.Find(ShaderName);
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("W24 trail-mask diagnostic shader is unavailable: " + ShaderName);
            using (new W24ObjectIdDepthDiagnosticCapture.CameraState(formalMainCamera))
            {
                return CaptureCore(formalMainCamera, trail, shader, input.ProjectedHistoryPixels, width, height);
            }
        }

        /// <summary>Pure/preflight input builder. It projects caller-owned emitter history using frozen camera matrices.</summary>
        public static TrailMaskInput BuildInput(Camera formalMainCamera, TrailRenderer trail, IEnumerable<Vector3> emitterHistory, int width, int height)
        {
            if (formalMainCamera == null) throw new ArgumentNullException(nameof(formalMainCamera));
            if (trail == null) throw new ArgumentNullException(nameof(trail));
            var view = new W24ObjectIdDepthDiagnosticCapture.W24DiagnosticView
            {
                ViewId = "trail-current-camera",
                Position = formalMainCamera.transform.position,
                Rotation = formalMainCamera.transform.rotation,
                WorldToCameraMatrix = formalMainCamera.worldToCameraMatrix,
                ProjectionMatrix = formalMainCamera.projectionMatrix
            };
            W24ObjectIdDepthDiagnosticCapture.ValidateCaptureRequest(formalMainCamera, view, width, height);
            if (!trail.enabled || !trail.gameObject.activeInHierarchy) throw new InvalidOperationException("W24 formal trail-mask capture requires an enabled active TrailRenderer.");
            if (emitterHistory == null) throw new ArgumentNullException(nameof(emitterHistory));
            var worldHistory = emitterHistory.ToArray();
            if (worldHistory.Length < 2) throw new ArgumentException("W24 trail-mask capture requires at least two external emitter-history samples.", nameof(emitterHistory));
            var gpuProjection = GL.GetGPUProjectionMatrix(formalMainCamera.projectionMatrix, true);
            return new TrailMaskInput
            {
                WorldHistory = worldHistory,
                ProjectedHistoryPixels = ProjectAllToPixels(
                    worldHistory,
                    formalMainCamera.worldToCameraMatrix,
                    gpuProjection,
                    width,
                    height,
                    SystemInfo.graphicsUVStartsAtTop)
            };
        }

        public struct TrailMaskInput
        {
            public Vector3[] WorldHistory;
            public Vector2[] ProjectedHistoryPixels;
        }

        /// <summary>Pure homogeneous projection helper. Every expected history point must be visible in this formal capture.</summary>
        public static Vector2[] ProjectAllToPixels(IEnumerable<Vector3> worldHistory, Matrix4x4 worldToCamera, Matrix4x4 gpuProjection, int width, int height, bool renderTargetUvStartsAtTop = false)
        {
            if (worldHistory == null) throw new ArgumentNullException(nameof(worldHistory));
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (!Finite(worldToCamera) || !Finite(gpuProjection)) throw new ArgumentException("W24 trail projection matrices must be finite.");
            var output = new List<Vector2>();
            foreach (var point in worldHistory)
            {
                if (!Finite(point)) throw new ArgumentException("W24 external emitter history contains a non-finite point.", nameof(worldHistory));
                var clip = gpuProjection * worldToCamera * new Vector4(point.x, point.y, point.z, 1f);
                if (!Finite(clip) || clip.w <= 1e-6f) throw new InvalidOperationException("W24 external emitter-history point is behind the frozen formal camera.");
                var ndc = new Vector3(clip.x / clip.w, clip.y / clip.w, clip.z / clip.w);
                // The projected z convention differs between graphics backends (D3D uses [0,1],
                // GL uses [-1,1]). The corridor contract is a screen-space diagnostic, so only
                // the front-facing homogeneous w and the unambiguous x/y viewport bounds are
                // formal preconditions here. The binary capture itself proves raster visibility.
                if (ndc.x < -1f || ndc.x > 1f || ndc.y < -1f || ndc.y > 1f) throw new InvalidOperationException("W24 external emitter-history point is outside the frozen formal camera viewport.");
                var normalizedY = ndc.y * .5f + .5f;
                // D3D-family render targets have a top-origin viewport convention.  The NPY
                // artifact deliberately preserves GetRawTextureData's platform-native row order,
                // so the rasterizer's post-projection viewport inversion must be mirrored here.
                // This keeps externally supplied emitter history and the R8 mask in the exact
                // same pixel coordinate system.  The explicit parameter keeps the pure helper
                // deterministic and testable on every host backend.
                if (renderTargetUvStartsAtTop) normalizedY = 1f - normalizedY;
                output.Add(new Vector2((ndc.x * .5f + .5f) * (width - 1), normalizedY * (height - 1)));
            }
            if (output.Count < 2) throw new InvalidOperationException("W24 trail-mask projection produced fewer than two history points.");
            return output.ToArray();
        }

        private static Result CaptureCore(Camera camera, TrailRenderer trail, Shader shader, Vector2[] projectedHistory, int width, int height)
        {
            RenderTexture maskTarget = null, depthTarget = null;
            Texture2D readback = null;
            CommandBuffer command = null;
            Material diagnosticMaterial = null;
            var previousActive = RenderTexture.active;
            try
            {
                maskTarget = CreateColorTarget(width, height);
                depthTarget = CreateDepthTarget(width, height);
                diagnosticMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                diagnosticMaterial.SetMatrix("_W24ViewProjection", GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix);
                command = new CommandBuffer { name = "W24 Trail-Only Binary Mask" };
                command.SetRenderTarget(new RenderTargetIdentifier(maskTarget), new RenderTargetIdentifier(depthTarget));
                command.SetViewport(new Rect(0f, 0f, width, height));
                command.ClearRenderTarget(true, true, Color.clear);
                command.DrawRenderer(trail, diagnosticMaterial, 0, 0);
                Graphics.ExecuteCommandBuffer(command);
                RenderTexture.active = maskTarget;
                readback = new Texture2D(width, height, GraphicsFormat.R8_UNorm, TextureCreationFlags.None);
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0, false); readback.Apply(false, false);
                var raw = readback.GetRawTextureData<byte>(); var mask = new byte[raw.Length]; raw.CopyTo(mask);
                if (mask.Length != width * height || mask.Any(value => value != 0 && value != 255)) throw new InvalidOperationException("W24 trail-only diagnostic output is not a lossless binary R8 mask.");
                if (!mask.Any(value => value == 255)) throw new InvalidOperationException("W24 trail-only diagnostic contains no foreground pixels.");
                return new Result { Width = width, Height = height, BinaryMask = mask, ProjectedEmitterHistoryPixels = projectedHistory };
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (command != null) command.Release();
                if (diagnosticMaterial != null) UnityEngine.Object.DestroyImmediate(diagnosticMaterial);
                if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
                Release(maskTarget); Release(depthTarget);
            }
        }

        private static RenderTexture CreateColorTarget(int width, int height)
        {
            var descriptor = new RenderTextureDescriptor(width, height) { graphicsFormat = GraphicsFormat.R8_UNorm, depthBufferBits = 0, msaaSamples = 1, sRGB = false, useMipMap = false, autoGenerateMips = false };
            var target = new RenderTexture(descriptor) { name = "W24_TrailMask_R8", hideFlags = HideFlags.HideAndDontSave };
            if (!target.Create()) throw new InvalidOperationException("Could not create W24 R8 trail-mask RenderTexture.");
            return target;
        }

        private static RenderTexture CreateDepthTarget(int width, int height)
        {
            var descriptor = new RenderTextureDescriptor(width, height, GraphicsFormat.None, GraphicsFormat.D24_UNorm_S8_UInt) { msaaSamples = 1, sRGB = false, useMipMap = false, autoGenerateMips = false };
            var target = new RenderTexture(descriptor) { name = "W24_TrailMask_Depth", hideFlags = HideFlags.HideAndDontSave };
            if (!target.Create()) throw new InvalidOperationException("Could not create W24 trail-mask depth RenderTexture.");
            return target;
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static bool Finite(Vector3 value) { return Finite(value.x) && Finite(value.y) && Finite(value.z); }
        private static bool Finite(Vector4 value) { return Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w); }
        private static bool Finite(Matrix4x4 value) { for (var row = 0; row < 4; row++) for (var column = 0; column < 4; column++) if (!Finite(value[row, column])) return false; return true; }
        private static void Release(RenderTexture target) { if (target == null) return; target.Release(); UnityEngine.Object.DestroyImmediate(target); }
    }
}
