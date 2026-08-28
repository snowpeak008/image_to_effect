using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace VFXComposer.W24
{
    /// <summary>
    /// Isolated Object-ID + linear-depth diagnostic capture. It explicitly draws registered
    /// renderers with ephemeral per-ID materials; it never samples Beauty or changes source
    /// renderers, material property blocks, shared materials, layers, or active state.
    /// </summary>
    public sealed class W24ObjectIdDepthDiagnosticCapture : IDisposable
    {
        public const string ShaderName = "Hidden/VFXComposer/W24/ObjectIdDepth";

        public struct W24DiagnosticView
        {
            public string ViewId;
            public Vector3 Position;
            public Quaternion Rotation;
            public Matrix4x4 WorldToCameraMatrix;
            public Matrix4x4 ProjectionMatrix;

            public static W24DiagnosticView FromPose(Camera referenceCamera, string viewId, Vector3 position, Quaternion rotation)
            {
                if (referenceCamera == null) throw new ArgumentNullException(nameof(referenceCamera));
                return new W24DiagnosticView
                {
                    ViewId = viewId,
                    Position = position,
                    Rotation = rotation,
                    WorldToCameraMatrix = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(position, rotation, Vector3.one).inverse,
                    ProjectionMatrix = referenceCamera.projectionMatrix
                };
            }
        }

        public sealed class Result
        {
            public int Width;
            public int Height;
            public uint[] ObjectIds;
            public float[] LinearDepth;
            public byte[] ObjectIdNpy { get { return W24NpyWriter.EncodeUInt32(ObjectIds, Width, Height); } }
            public byte[] LinearDepthNpy { get { return W24NpyWriter.EncodeFloat32(LinearDepth, Width, Height); } }
        }

        /// <summary>Pure platform policy seam. Any unavailable format is a formal-capture blocker.</summary>
        public static void ValidateFormatSupport(bool supportsR32UIntRender, bool supportsR32UIntReadback, bool supportsR32FloatRender, bool supportsR32FloatReadback, bool supportsDepthStencilRender)
        {
            if (!supportsR32UIntRender || !supportsR32UIntReadback) throw new NotSupportedException("W24 formal object-ID diagnostics require R32_UInt render and asynchronous GPU readback support; RGBA fallback is forbidden.");
            if (!supportsR32FloatRender || !supportsR32FloatReadback) throw new NotSupportedException("W24 formal depth diagnostics require R32_SFloat render and asynchronous GPU readback support; Beauty fallback is forbidden.");
            if (!supportsDepthStencilRender) throw new NotSupportedException("W24 formal Object-ID/Depth diagnostics require D24_UNorm_S8_UInt render support for deterministic depth testing.");
        }

        public static void ValidateFormatSupport()
        {
            ValidateFormatSupport(
                SystemInfo.IsFormatSupported(GraphicsFormat.R32_UInt, FormatUsage.Render), SystemInfo.supportsAsyncGPUReadback,
                SystemInfo.IsFormatSupported(GraphicsFormat.R32_SFloat, FormatUsage.Render), SystemInfo.supportsAsyncGPUReadback,
                SystemInfo.IsFormatSupported(GraphicsFormat.D24_UNorm_S8_UInt, FormatUsage.Render));
        }

        public static Result Capture(Camera formalMainCamera, IEnumerable<W24DiagnosticObjectRegistration> registrations, W24DiagnosticView view, int width, int height)
        {
            if (formalMainCamera == null) throw new ArgumentNullException(nameof(formalMainCamera));
            ValidateCaptureRequest(formalMainCamera, view, width, height);
            ValidateFormatSupport();
            var items = W24DiagnosticObjectRegistration.Validate(registrations);
            var shader = Shader.Find(ShaderName);
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("W24 Object-ID/Depth diagnostic shader is unavailable: " + ShaderName);

            using (var cameraState = new CameraState(formalMainCamera))
            {
                formalMainCamera.transform.SetPositionAndRotation(view.Position, view.Rotation);
                formalMainCamera.worldToCameraMatrix = view.WorldToCameraMatrix;
                formalMainCamera.projectionMatrix = view.ProjectionMatrix;
                return CaptureCore(formalMainCamera, items, shader, width, height);
            }
        }

        /// <summary>Non-graphics preflight for formal camera, frozen view, and diagnostic dimensions.</summary>
        public static void ValidateCaptureRequest(Camera formalMainCamera, W24DiagnosticView view, int width, int height)
        {
            if (formalMainCamera == null) throw new ArgumentNullException(nameof(formalMainCamera));
            ValidateCameraAndView(formalMainCamera, view, width, height);
        }

        private static Result CaptureCore(Camera camera, IReadOnlyList<W24DiagnosticObjectRegistration> items, Shader shader, int width, int height)
        {
            RenderTexture idTarget = null, depthTarget = null, zTarget = null;
            CommandBuffer command = null;
            var ephemeralMaterials = new List<Material>();
            var previousActive = RenderTexture.active;
            try
            {
                idTarget = CreateColorTarget(width, height, GraphicsFormat.R32_UInt, "W24_ObjectId_R32UInt");
                depthTarget = CreateColorTarget(width, height, GraphicsFormat.R32_SFloat, "W24_LinearDepth_R32Float");
                zTarget = CreateDepthTarget(width, height);
                command = new CommandBuffer { name = "W24 Object-ID + Linear Depth" };
                var gpuViewProjection = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix;
                foreach (var item in items)
                {
                    var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                    material.SetInteger("_W24ObjectId", unchecked((int)item.ObjectId));
                    material.SetMatrix("_W24ViewProjection", gpuViewProjection);
                    material.SetMatrix("_W24WorldToCamera", camera.worldToCameraMatrix);
                    ephemeralMaterials.Add(material);
                }
                DrawPass(command, items, ephemeralMaterials, idTarget, zTarget, width, height, 0);
                DrawPass(command, items, ephemeralMaterials, depthTarget, zTarget, width, height, 1);
                Graphics.ExecuteCommandBuffer(command);
                var ids = ReadUInt32(idTarget, width, height);
                var depths = ReadFloat32(depthTarget, width, height);
                ValidatePixels(items, ids, depths);
                return new Result { Width = width, Height = height, ObjectIds = ids, LinearDepth = depths };
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (command != null) command.Release();
                foreach (var material in ephemeralMaterials) if (material != null) UnityEngine.Object.DestroyImmediate(material);
                Release(idTarget); Release(depthTarget); Release(zTarget);
            }
        }

        private static void DrawPass(CommandBuffer command, IReadOnlyList<W24DiagnosticObjectRegistration> items, IReadOnlyList<Material> materials, RenderTexture colorTarget, RenderTexture zTarget, int width, int height, int shaderPass)
        {
            command.SetRenderTarget(new RenderTargetIdentifier(colorTarget), new RenderTargetIdentifier(zTarget));
            command.SetViewport(new Rect(0f, 0f, width, height));
            command.ClearRenderTarget(true, true, Color.clear);
            for (var index = 0; index < items.Count; index++)
            {
                var renderer = items[index].TargetRenderer;
                var material = materials[index];
                for (var submesh = 0; submesh < renderer.sharedMaterials.Length; submesh++) command.DrawRenderer(renderer, material, submesh, shaderPass);
            }
        }

        private static RenderTexture CreateColorTarget(int width, int height, GraphicsFormat format, string name)
        {
            var descriptor = new RenderTextureDescriptor(width, height) { graphicsFormat = format, depthBufferBits = 0, msaaSamples = 1, sRGB = false, useMipMap = false, autoGenerateMips = false };
            var target = new RenderTexture(descriptor) { name = name, hideFlags = HideFlags.HideAndDontSave };
            if (!target.Create()) throw new InvalidOperationException("Could not create W24 diagnostic RenderTexture: " + name);
            return target;
        }

        private static RenderTexture CreateDepthTarget(int width, int height)
        {
            var descriptor = new RenderTextureDescriptor(width, height, GraphicsFormat.None, GraphicsFormat.D24_UNorm_S8_UInt) { msaaSamples = 1, sRGB = false, useMipMap = false, autoGenerateMips = false };
            var target = new RenderTexture(descriptor) { name = "W24_Diagnostic_Depth", hideFlags = HideFlags.HideAndDontSave };
            if (!target.Create()) throw new InvalidOperationException("Could not create W24 diagnostic depth RenderTexture.");
            return target;
        }

        private static uint[] ReadUInt32(RenderTexture target, int width, int height)
        {
            return ReadGpuData<uint>(target, width * height, "R32_UInt object-ID");
        }

        private static float[] ReadFloat32(RenderTexture target, int width, int height)
        {
            return ReadGpuData<float>(target, width * height, "R32_SFloat linear-depth");
        }

        private static T[] ReadGpuData<T>(RenderTexture target, int expectedElementCount, string label) where T : struct
        {
            var request = AsyncGPUReadback.Request(target, 0);
            request.WaitForCompletion();
            if (request.hasError) throw new InvalidOperationException("W24 formal " + label + " asynchronous GPU readback failed; fallback evidence is forbidden.");
            var raw = request.GetData<T>();
            if (raw.Length != expectedElementCount) throw new InvalidOperationException("W24 formal " + label + " asynchronous GPU readback returned " + raw.Length + " elements; expected " + expectedElementCount + ".");
            var result = new T[raw.Length]; raw.CopyTo(result);
            return result;
        }

        private static void ValidatePixels(IReadOnlyList<W24DiagnosticObjectRegistration> registrations, uint[] ids, float[] depth)
        {
            if (ids == null || depth == null || ids.Length != depth.Length) throw new InvalidOperationException("W24 object-ID/depth readback dimensions are invalid.");
            if (depth.Any(value => !IsFinite(value))) throw new InvalidOperationException("W24 linear-depth diagnostic contains a non-finite value.");
            foreach (var item in registrations.Where(value => value.Required))
            {
                var found = false;
                for (var index = 0; index < ids.Length; index++)
                {
                    if (ids[index] != item.ObjectId) continue;
                    if (!(depth[index] > 0f)) throw new InvalidOperationException("W24 object-ID pixel has non-positive linear depth for required ID " + item.ObjectId + ".");
                    found = true;
                }
                if (!found)
                {
                    var observed = string.Join(",", ids.Where(value => value != 0u).Distinct().OrderBy(value => value).Take(32).Select(value => value.ToString()).ToArray());
                    throw new InvalidOperationException("W24 required object-ID has no diagnostic pixels: " + item.ObjectId + " (" + item.SemanticRole + "). Observed non-zero IDs: [" + observed + "].");
                }
            }
        }

        private static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static bool IsFinite(Vector3 value) { return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z); }
        private static bool IsFinite(Quaternion value) { return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w); }
        private static bool IsFinite(Matrix4x4 value)
        {
            for (var row = 0; row < 4; row++) for (var column = 0; column < 4; column++) if (!IsFinite(value[row, column])) return false;
            return true;
        }
        private static void ValidateCameraAndView(Camera camera, W24DiagnosticView view, int width, int height)
        {
            if (width <= 0 || height <= 0 || width > 8192 || height > 8192) throw new ArgumentOutOfRangeException(nameof(width), "W24 diagnostic dimensions must be in [1,8192].");
            if (!camera.isActiveAndEnabled || !camera.CompareTag("MainCamera")) throw new InvalidOperationException("W24 formal diagnostics require an active serialized MainCamera.");
            if (!IsFinite(camera.nearClipPlane) || !IsFinite(camera.farClipPlane) || camera.nearClipPlane <= 0f || camera.farClipPlane <= camera.nearClipPlane) throw new InvalidOperationException("W24 formal MainCamera clip planes are invalid.");
            if (!camera.orthographic && (!IsFinite(camera.fieldOfView) || camera.fieldOfView <= 0f || camera.fieldOfView >= 179f)) throw new InvalidOperationException("W24 formal MainCamera field of view is invalid.");
            if (camera.orthographic && (!IsFinite(camera.orthographicSize) || camera.orthographicSize <= 0f)) throw new InvalidOperationException("W24 formal orthographic MainCamera size is invalid.");
            var rotationMagnitude = Quaternion.Dot(view.Rotation, view.Rotation);
            var forwardInView = view.WorldToCameraMatrix.MultiplyPoint(view.Position + view.Rotation * Vector3.forward);
            if (string.IsNullOrWhiteSpace(view.ViewId) || !IsFinite(view.Position) || !IsFinite(view.Rotation) || rotationMagnitude < .999f || rotationMagnitude > 1.001f || !IsFinite(view.WorldToCameraMatrix) || !IsFinite(view.ProjectionMatrix) || Mathf.Abs(view.WorldToCameraMatrix.determinant) < 1e-8f || Mathf.Abs(view.ProjectionMatrix.determinant) < 1e-8f || !IsFinite(forwardInView) || forwardInView.z >= -1e-4f)
                throw new ArgumentException("W24 diagnostic view requires a non-empty id plus finite normalized pose and non-singular explicit view/projection matrices.", nameof(view));
        }
        private static void Release(RenderTexture target) { if (target == null) return; target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        public void Dispose() { }

        /// <summary>All camera fields potentially touched by a diagnostic view are restored even if GPU work fails.</summary>
        public sealed class CameraState : IDisposable
        {
            private readonly Camera camera;
            private readonly Transform parent;
            private readonly Vector3 localPosition, localScale;
            private readonly Quaternion localRotation;
            private readonly RenderTexture target;
            private readonly CameraClearFlags clearFlags;
            private readonly Color background;
            private readonly int cullingMask;
            private readonly bool hdr, msaa, enabled;
            private readonly Matrix4x4 worldToCameraMatrix, projectionMatrix;
            private bool restored;

            public CameraState(Camera value)
            {
                camera = value ?? throw new ArgumentNullException(nameof(value));
                var transform = camera.transform; parent = transform.parent; localPosition = transform.localPosition; localRotation = transform.localRotation; localScale = transform.localScale;
                target = camera.targetTexture; clearFlags = camera.clearFlags; background = camera.backgroundColor; cullingMask = camera.cullingMask; hdr = camera.allowHDR; msaa = camera.allowMSAA; enabled = camera.enabled;
                worldToCameraMatrix = camera.worldToCameraMatrix; projectionMatrix = camera.projectionMatrix;
            }

            public void Dispose()
            {
                if (restored) return;
                restored = true;
                var transform = camera.transform;
                transform.SetParent(parent, false); transform.localPosition = localPosition; transform.localRotation = localRotation; transform.localScale = localScale;
                camera.targetTexture = target; camera.clearFlags = clearFlags; camera.backgroundColor = background; camera.cullingMask = cullingMask; camera.allowHDR = hdr; camera.allowMSAA = msaa; camera.enabled = enabled;
                camera.worldToCameraMatrix = worldToCameraMatrix; camera.projectionMatrix = projectionMatrix;
            }
        }
    }
}
