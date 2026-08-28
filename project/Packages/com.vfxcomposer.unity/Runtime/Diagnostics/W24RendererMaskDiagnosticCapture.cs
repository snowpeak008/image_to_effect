using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace VFXComposer.W24
{
    /// <summary>
    /// Produces a typed, binary R8 effect mask from an explicit renderer set.  It never uses a
    /// Beauty PNG as raw evidence, and it never infers membership from layers, hierarchy or
    /// renderer names.  The caller is responsible for recording its natural-frame provenance.
    /// </summary>
    public static class W24RendererMaskDiagnosticCapture
    {
        public const string ShaderName = "Hidden/VFXComposer/W24/RendererMask";

        public sealed class Result
        {
            public int Width;
            public int Height;
            public byte[] BinaryMask;
            public byte[] BinaryMaskNpy { get { return W24NpyWriter.EncodeBinaryUInt8(BinaryMask, Width, Height); } }
        }

        public static void ValidateFormatSupport(bool supportsR8Render, bool supportsR8Readback, bool supportsDepthStencilRender)
        {
            if (!supportsR8Render || !supportsR8Readback) throw new NotSupportedException("W24 renderer-mask diagnostics require R8_UNorm render and synchronous readback support; PNG/Beauty fallback is forbidden.");
            if (!supportsDepthStencilRender) throw new NotSupportedException("W24 renderer-mask diagnostics require D24_UNorm_S8_UInt depth support.");
        }

        public static void ValidateFormatSupport()
        {
            ValidateFormatSupport(
                SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, FormatUsage.Render),
                SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, FormatUsage.ReadPixels),
                SystemInfo.IsFormatSupported(GraphicsFormat.D24_UNorm_S8_UInt, FormatUsage.Render));
        }

        public static IReadOnlyList<Renderer> ValidateRenderers(IEnumerable<Renderer> renderers)
        {
            if (renderers == null) throw new ArgumentNullException(nameof(renderers));
            var values = renderers.ToArray();
            if (values.Length == 0) throw new InvalidOperationException("W24 renderer-mask diagnostics require one or more explicit Renderers.");
            var identities = new HashSet<int>();
            foreach (var renderer in values)
            {
                if (renderer == null) throw new InvalidOperationException("W24 renderer-mask diagnostics reject null Renderer membership.");
                if (!identities.Add(renderer.GetInstanceID())) throw new InvalidOperationException("W24 renderer-mask diagnostics reject duplicate Renderer membership: " + renderer.name);
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) throw new InvalidOperationException("W24 renderer-mask diagnostics require every explicit Renderer to be enabled and active: " + renderer.name);
                if (renderer.sharedMaterials == null || renderer.sharedMaterials.Length == 0) throw new NotSupportedException("W24 renderer-mask diagnostics require at least one drawable submesh/material: " + renderer.name);
            }
            return values;
        }

        public static void ValidateCaptureRequest(Camera formalMainCamera, IEnumerable<Renderer> renderers, int width, int height)
        {
            if (formalMainCamera == null) throw new ArgumentNullException(nameof(formalMainCamera));
            var view = W24ObjectIdDepthDiagnosticCapture.W24DiagnosticView.FromPose(formalMainCamera, "renderer-mask-current-camera", formalMainCamera.transform.position, formalMainCamera.transform.rotation);
            W24ObjectIdDepthDiagnosticCapture.ValidateCaptureRequest(formalMainCamera, view, width, height);
            ValidateRenderers(renderers);
        }

        public static Result Capture(Camera formalMainCamera, IEnumerable<Renderer> renderers, int width, int height)
        {
            ValidateCaptureRequest(formalMainCamera, renderers, width, height);
            ValidateFormatSupport();
            var shader = Shader.Find(ShaderName);
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("W24 renderer-mask diagnostic shader is unavailable: " + ShaderName);
            var items = ValidateRenderers(renderers);
            using (new W24ObjectIdDepthDiagnosticCapture.CameraState(formalMainCamera))
                return CaptureCore(formalMainCamera, items, shader, width, height);
        }

        private static Result CaptureCore(Camera camera, IReadOnlyList<Renderer> renderers, Shader shader, int width, int height)
        {
            RenderTexture maskTarget = null, depthTarget = null;
            Texture2D readback = null;
            CommandBuffer command = null;
            Material material = null;
            var previousActive = RenderTexture.active;
            try
            {
                maskTarget = CreateColorTarget(width, height);
                depthTarget = CreateDepthTarget(width, height);
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                material.SetMatrix("_W24ViewProjection", GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix);
                command = new CommandBuffer { name = "W24 Explicit Renderer Binary Mask" };
                command.SetRenderTarget(new RenderTargetIdentifier(maskTarget), new RenderTargetIdentifier(depthTarget));
                command.SetViewport(new Rect(0f, 0f, width, height));
                command.ClearRenderTarget(true, true, Color.clear);
                foreach (var renderer in renderers)
                    for (var submesh = 0; submesh < renderer.sharedMaterials.Length; submesh++) command.DrawRenderer(renderer, material, submesh, 0);
                Graphics.ExecuteCommandBuffer(command);
                RenderTexture.active = maskTarget;
                readback = new Texture2D(width, height, GraphicsFormat.R8_UNorm, TextureCreationFlags.None);
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0, false); readback.Apply(false, false);
                var raw = readback.GetRawTextureData<byte>(); var mask = new byte[raw.Length]; raw.CopyTo(mask);
                if (mask.Length != checked(width * height) || mask.Any(value => value != 0 && value != 255)) throw new InvalidOperationException("W24 renderer-mask output is not a lossless binary R8 mask.");
                if (!mask.Any(value => value == 255)) throw new InvalidOperationException("W24 renderer-mask capture contains no explicit-renderer foreground pixels.");
                return new Result { Width = width, Height = height, BinaryMask = mask };
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (command != null) command.Release();
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
                if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
                Release(maskTarget); Release(depthTarget);
            }
        }

        private static RenderTexture CreateColorTarget(int width, int height)
        {
            var descriptor = new RenderTextureDescriptor(width, height) { graphicsFormat = GraphicsFormat.R8_UNorm, depthBufferBits = 0, msaaSamples = 1, sRGB = false, useMipMap = false, autoGenerateMips = false };
            var target = new RenderTexture(descriptor) { name = "W24_RendererMask_R8", hideFlags = HideFlags.HideAndDontSave };
            if (!target.Create()) throw new InvalidOperationException("Could not create W24 R8 renderer-mask RenderTexture.");
            return target;
        }

        private static RenderTexture CreateDepthTarget(int width, int height)
        {
            var descriptor = new RenderTextureDescriptor(width, height, GraphicsFormat.None, GraphicsFormat.D24_UNorm_S8_UInt) { msaaSamples = 1, sRGB = false, useMipMap = false, autoGenerateMips = false };
            var target = new RenderTexture(descriptor) { name = "W24_RendererMask_Depth", hideFlags = HideFlags.HideAndDontSave };
            if (!target.Create()) throw new InvalidOperationException("Could not create W24 renderer-mask depth RenderTexture.");
            return target;
        }

        private static void Release(RenderTexture target) { if (target == null) return; target.Release(); UnityEngine.Object.DestroyImmediate(target); }
    }
}
