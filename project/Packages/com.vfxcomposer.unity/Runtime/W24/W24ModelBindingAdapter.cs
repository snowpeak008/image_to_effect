using System;
using System.Linq;
using System.Text;
using UnityEngine;

namespace VFXComposer.W24
{
    public enum W24BindingTarget { Transform, Socket, Renderer, Mesh, MeshRenderer, SkinnedMeshRenderer, Bone }
    public enum W24BindingFault { None, MissingRoot, MissingTarget, MissingRenderer, MissingMesh, MissingBone }

    [Serializable]
    public struct W24ModelBindingRequest
    {
        public W24BindingTarget Target;
        public string TargetName;
        public Transform ExplicitTransform;
        public bool RequireMesh;
    }
    [Serializable]
    public struct W24ModelBindingResult
    {
        public Transform Anchor;
        public Renderer Renderer;
        public W24BindingFault Fault;
        public string Detail;
        public bool IsBound { get { return Fault == W24BindingFault.None && Anchor != null; } }
    }

    [Serializable]
    public struct W24BindingProbeResult
    {
        public string ProbeId;
        public W24BindingFault ExpectedFault;
        public W24BindingFault ActualFault;
        public string Detail;
        public bool HadAnchor;
        public bool HadRenderer;
        public bool Passed;
    }

    /// <summary>Structured telemetry payload for the four frozen negative binding probes.</summary>
    [Serializable]
    public sealed class W24BindingProbeReport
    {
        public const string Schema = "w24-binding-probes/v1";
        public bool InputRootPresent;
        public W24BindingProbeResult[] Results = new W24BindingProbeResult[0];
        public bool Passed { get { return InputRootPresent && Results != null && Results.Length == 4 && Results.All(value => value.Passed); } }
        public string ToJson()
        {
            var builder = new StringBuilder();
            builder.Append("{\"schema\":\"").Append(Schema).Append("\",\"inputRootPresent\":").Append(InputRootPresent ? "true" : "false").Append(",\"passed\":").Append(Passed ? "true" : "false").Append(",\"results\":[");
            var values = Results ?? new W24BindingProbeResult[0];
            for (var index = 0; index < values.Length; index++)
            {
                if (index > 0) builder.Append(','); var value = values[index];
                builder.Append("{\"probeId\":\"").Append(Escape(value.ProbeId)).Append("\",\"expectedFault\":\"").Append(value.ExpectedFault).Append("\",\"actualFault\":\"").Append(value.ActualFault).Append("\",\"detail\":\"").Append(Escape(value.Detail)).Append("\",\"hadAnchor\":").Append(value.HadAnchor ? "true" : "false").Append(",\"hadRenderer\":").Append(value.HadRenderer ? "true" : "false").Append(",\"passed\":").Append(value.Passed ? "true" : "false").Append('}');
            }
            return builder.Append("]}").ToString();
        }
        private static string Escape(string value) { return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n"); }
    }

    /// <summary>
    /// Formal negative probes.  Every request names an impossible reserved target and requires
    /// its exact fault with no anchor/renderer fallback.  The report can be written directly as
    /// semantic telemetry by the formal capture producer.
    /// </summary>
    public static class W24BindingDiagnosticProbes
    {
        private const string MissingSocket = "__w24_probe_missing_socket__";
        private const string MissingRenderer = "__w24_probe_missing_renderer__";
        private const string MissingMesh = "__w24_probe_missing_mesh__";
        private const string MissingBone = "__w24_probe_missing_bone__";

        public static W24BindingProbeReport Run(Transform modelRoot)
        {
            var requests = new[]
            {
                Probe("missing_socket", W24BindingFault.MissingTarget, modelRoot, new W24ModelBindingRequest { Target = W24BindingTarget.Socket, TargetName = MissingSocket }),
                Probe("missing_renderer", W24BindingFault.MissingRenderer, modelRoot, new W24ModelBindingRequest { Target = W24BindingTarget.MeshRenderer, TargetName = MissingRenderer }),
                Probe("missing_mesh", W24BindingFault.MissingMesh, modelRoot, new W24ModelBindingRequest { Target = W24BindingTarget.Mesh, TargetName = MissingMesh }),
                Probe("missing_bone", W24BindingFault.MissingBone, modelRoot, new W24ModelBindingRequest { Target = W24BindingTarget.Bone, TargetName = MissingBone })
            };
            return new W24BindingProbeReport { InputRootPresent = modelRoot != null, Results = requests };
        }
        private static W24BindingProbeResult Probe(string id, W24BindingFault expected, Transform root, W24ModelBindingRequest request)
        {
            var result = W24ModelBindingResolver.Resolve(root, request);
            var noFallback = result.Anchor == null && result.Renderer == null && !result.IsBound;
            return new W24BindingProbeResult { ProbeId = id, ExpectedFault = expected, ActualFault = result.Fault, Detail = result.Detail, HadAnchor = result.Anchor != null, HadRenderer = result.Renderer != null, Passed = result.Fault == expected && noFallback };
        }
    }

    public static class W24ModelBindingResolver
    {
        public static W24ModelBindingResult Resolve(Transform root, W24ModelBindingRequest request)
        {
            if (root == null) return Failure(W24BindingFault.MissingRoot, "model root is required");
            if (request.Target == W24BindingTarget.Transform && request.ExplicitTransform != null) return Success(request.ExplicitTransform, request.ExplicitTransform.GetComponent<Renderer>());
            if (request.Target == W24BindingTarget.Transform) return Success(root, root.GetComponent<Renderer>());
            if (request.Target == W24BindingTarget.Socket)
            {
                var named = FindDeep(root, request.TargetName);
                if (named == null) return Failure(W24BindingFault.MissingTarget, "missing named anchor: " + request.TargetName);
                return Success(named, named.GetComponent<Renderer>());
            }
            if (request.Target == W24BindingTarget.Bone)
            {
                foreach (var skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    foreach (var bone in skinned.bones)
                        if (bone != null && bone.name == request.TargetName) return Success(bone, skinned);
                return Failure(W24BindingFault.MissingBone, "missing SkinnedMeshRenderer bone: " + request.TargetName);
            }
            if (request.Target == W24BindingTarget.Mesh)
            {
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!string.IsNullOrEmpty(request.TargetName) && filter.name != request.TargetName) continue;
                    if (filter.sharedMesh != null) return Success(filter.transform, filter.GetComponent<Renderer>());
                }
                return Failure(W24BindingFault.MissingMesh, "missing requested MeshFilter: " + request.TargetName);
            }
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Renderer match = null;
            foreach (var renderer in renderers)
            {
                if (!string.IsNullOrEmpty(request.TargetName) && renderer.name != request.TargetName) continue;
                if (request.Target == W24BindingTarget.MeshRenderer && !(renderer is MeshRenderer)) continue;
                if (request.Target == W24BindingTarget.SkinnedMeshRenderer && !(renderer is SkinnedMeshRenderer)) continue;
                match = renderer; break;
            }
            if (match == null) return Failure(W24BindingFault.MissingRenderer, "missing requested renderer: " + request.TargetName);
            if (request.RequireMesh)
            {
                var mesh = (match as SkinnedMeshRenderer) != null ? ((SkinnedMeshRenderer)match).sharedMesh : match.GetComponent<MeshFilter>() == null ? null : match.GetComponent<MeshFilter>().sharedMesh;
                if (mesh == null) return Failure(W24BindingFault.MissingMesh, "renderer has no readable shared mesh");
            }
            return Success(match.transform, match);
        }
        private static Transform FindDeep(Transform root, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindDeep(child, name); if (found != null) return found;
            }
            return null;
        }
        private static W24ModelBindingResult Success(Transform anchor, Renderer renderer) { return new W24ModelBindingResult { Anchor = anchor, Renderer = renderer, Fault = W24BindingFault.None, Detail = "bound" }; }
        private static W24ModelBindingResult Failure(W24BindingFault fault, string detail) { return new W24ModelBindingResult { Fault = fault, Detail = detail }; }
    }

    [DisallowMultipleComponent]
    public sealed class W24ModelBindingAdapter : MonoBehaviour, IW24SemanticTelemetrySource
    {
        [SerializeField] private Transform modelRoot;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private W24ModelBindingRequest request;
        private W24ModelBindingResult result;
        private int eventSerial;
        private Transform originalParent;
        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private Vector3 originalLocalScale;
        private bool visualOriginCaptured;

        public W24ModelBindingResult Result { get { return result; } }
        private void Awake() { CaptureVisualOrigin(); ResetForPool(); }
        public bool Bind(Transform root)
        {
            CaptureVisualOrigin();
            RestoreVisualOrigin();
            modelRoot = root; result = W24ModelBindingResolver.Resolve(modelRoot, request); eventSerial++;
            if (!result.IsBound) return false;
            if (visualRoot != null) visualRoot.SetParent(result.Anchor, false);
            return true;
        }
        public void ResetForPool() { RestoreVisualOrigin(); modelRoot = null; eventSerial++; result = default(W24ModelBindingResult); }
        public W24SemanticTelemetry ReadSemanticTelemetry()
        {
            return new W24SemanticTelemetry { Module = "model_binding", State = result.IsBound ? W24SemanticState.Continuous : result.Fault == W24BindingFault.None ? W24SemanticState.Idle : W24SemanticState.Faulted, EventSerial = eventSerial, ActiveItemCount = result.IsBound ? 1 : 0, CleanupComplete = !result.IsBound, LastEventId = result.IsBound ? "bound" : "unbound", FaultCode = result.Fault.ToString() };
        }
        private void CaptureVisualOrigin()
        {
            if (visualOriginCaptured || visualRoot == null) return;
            originalParent = visualRoot.parent;
            originalLocalPosition = visualRoot.localPosition;
            originalLocalRotation = visualRoot.localRotation;
            originalLocalScale = visualRoot.localScale;
            visualOriginCaptured = true;
        }
        private void RestoreVisualOrigin()
        {
            if (!visualOriginCaptured || visualRoot == null) return;
            visualRoot.SetParent(originalParent, false);
            visualRoot.localPosition = originalLocalPosition;
            visualRoot.localRotation = originalLocalRotation;
            visualRoot.localScale = originalLocalScale;
        }
        private void OnDisable() { ResetForPool(); }
    }
}
