using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace VFXComposer.W24
{
    /// <summary>
    /// Immutable description of the rendering conditions expected by an S0a evidence run.
    /// The serialized-camera field is an explicit scene reference, not a camera substituted by
    /// the recorder.  A Capture Profile is evidence metadata, never a visual-acceptance verdict.
    /// </summary>
    [Serializable]
    public sealed class W24CaptureProfile
    {
        public string ProfileVersion = "w24-s0a-capture-profile/v1";
        public string UnityVersion;
        public string UrpVersion;
        public string GraphicsApi;
        public string GraphicsDevice;
        public string GraphicsDriverVersion;
        public string RenderTextureFormat = "ARGB32";
        public string RendererAssetReference;
        public string RendererAssetSha256;
        public string VolumeReference;
        public string VolumeSha256;
        public string ScenePath;
        public string SerializedCameraReference;
        public int Width = 960;
        public int Height = 540;
        public int FramesPerSecond = 60;
        public Color Background = new Color(.035f, .04f, .055f, 1f);
        public string ColorSpace;
        public bool Hdr;
        public bool Msaa;
        public bool Bloom;
        // S0a has no universal URP runtime readback for these. The caller freezes them and
        // the Renderer/Volume source hashes bind that declaration to the captured project.
        public string BloomValidation = "caller-frozen";
        public string ToneMapping = "None";
        public string ToneMappingValidation = "caller-frozen";
        public int CanonicalSeed;
        public int[] RobustnessSeeds = new int[2];
        public int[] RetainedFrameIndices = new int[0];

        public IEnumerable<int> AllSeeds()
        {
            yield return CanonicalSeed;
            if (RobustnessSeeds == null) yield break;
            foreach (var seed in RobustnessSeeds) yield return seed;
        }

        /// <summary>Operator commands are positive UInt32 values; preserve their exact bits even when the legacy profile field is signed.</summary>
        public bool ContainsSeed(uint seed)
        {
            return AllSeeds().Any(value => unchecked((uint)value) == seed);
        }

        public void Validate()
        {
            if (string.IsNullOrEmpty(ProfileVersion) || string.IsNullOrEmpty(UnityVersion) || string.IsNullOrEmpty(UrpVersion) || string.IsNullOrEmpty(GraphicsApi) || string.IsNullOrEmpty(GraphicsDevice) || string.IsNullOrEmpty(GraphicsDriverVersion) || string.IsNullOrEmpty(RenderTextureFormat) || string.IsNullOrEmpty(RendererAssetReference) || string.IsNullOrEmpty(RendererAssetSha256) || string.IsNullOrEmpty(VolumeReference) || string.IsNullOrEmpty(VolumeSha256) || string.IsNullOrEmpty(ScenePath) || string.IsNullOrEmpty(SerializedCameraReference) || string.IsNullOrEmpty(ColorSpace) || string.IsNullOrEmpty(ToneMapping))
                throw new InvalidOperationException("W24 Capture Profile requires frozen Unity/URP/graphics/color settings plus scene and serialized camera reference.");
            RequireCanonicalSha256(RendererAssetSha256, "Renderer Asset hash");
            RequireCanonicalSha256(VolumeSha256, "Volume hash");
            if (Width <= 0 || Height <= 0 || FramesPerSecond <= 0)
                throw new InvalidOperationException("W24 Capture Profile resolution and fps must be positive.");
            if (RobustnessSeeds == null || RobustnessSeeds.Length != 2)
                throw new InvalidOperationException("W24 Capture Profile requires exactly two robustness seeds in addition to its canonical seed.");
            if (AllSeeds().Distinct().Count() != 3)
                throw new InvalidOperationException("W24 Capture Profile canonical and robustness seeds must be distinct.");
            if (RetainedFrameIndices == null || RetainedFrameIndices.Length == 0 || RetainedFrameIndices.Any(index => index < 0) || RetainedFrameIndices.Distinct().Count() != RetainedFrameIndices.Length)
                throw new InvalidOperationException("W24 Capture Profile requires a non-empty frozen, unique retained-frame table.");
            if (!IsFinite(Background.r) || !IsFinite(Background.g) || !IsFinite(Background.b) || !IsFinite(Background.a))
                throw new InvalidOperationException("W24 Capture Profile background must not contain NaN or Infinity.");
            if (!string.Equals(BloomValidation, "caller-frozen", StringComparison.Ordinal) || !string.Equals(ToneMappingValidation, "caller-frozen", StringComparison.Ordinal))
                throw new InvalidOperationException("S0a Bloom and Tone Mapping must be marked caller-frozen and bound by Renderer/Volume hashes; automatic runtime validation is not claimed.");
        }

        public string ToCanonicalJson()
        {
            Validate();
            return "{\"profileVersion\":\"" + Escape(ProfileVersion) + "\",\"unityVersion\":\"" + Escape(UnityVersion) + "\",\"urpVersion\":\"" + Escape(UrpVersion) + "\",\"graphicsApi\":\"" + Escape(GraphicsApi) + "\",\"graphicsDevice\":\"" + Escape(GraphicsDevice) + "\",\"graphicsDriverVersion\":\"" + Escape(GraphicsDriverVersion) + "\",\"renderTextureFormat\":\"" + Escape(RenderTextureFormat) + "\",\"rendererAsset\":{\"reference\":\"" + Escape(RendererAssetReference) + "\",\"sha256\":\"" + Escape(RendererAssetSha256) + "\"},\"volume\":{\"reference\":\"" + Escape(VolumeReference) + "\",\"sha256\":\"" + Escape(VolumeSha256) + "\"},\"scenePath\":\"" + Escape(ScenePath) + "\",\"serializedCameraReference\":\"" + Escape(SerializedCameraReference) + "\",\"resolution\":[" + Width + "," + Height + "],\"fps\":" + FramesPerSecond + ",\"background\":[" + Number(Background.r) + "," + Number(Background.g) + "," + Number(Background.b) + "," + Number(Background.a) + "],\"colorSpace\":\"" + Escape(ColorSpace) + "\",\"hdr\":" + Bool(Hdr) + ",\"msaa\":" + Bool(Msaa) + ",\"bloom\":{\"value\":" + Bool(Bloom) + ",\"validation\":\"" + Escape(BloomValidation) + "\"},\"toneMapping\":{\"value\":\"" + Escape(ToneMapping) + "\",\"validation\":\"" + Escape(ToneMappingValidation) + "\"},\"canonicalSeed\":" + SeedNumber(CanonicalSeed) + ",\"robustnessSeeds\":[" + SeedNumber(RobustnessSeeds[0]) + "," + SeedNumber(RobustnessSeeds[1]) + "],\"retainedFrameIndices\":[" + string.Join(",", RetainedFrameIndices) + "],\"retainedFrameIndicesSha256\":\"" + RetainedFrameIndicesSha256 + "\"}";
        }

        public string Sha256 { get { return HashText(ToCanonicalJson()); } }
        public string RetainedFrameIndicesSha256 { get { Validate(); return HashText(string.Join(",", RetainedFrameIndices)); } }
        public bool IsRetainedFrameIndex(int frameIndex) { return RetainedFrameIndices != null && RetainedFrameIndices.Contains(frameIndex); }

        internal static string Escape(string value) { return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n"); }
        internal static string Number(float value) { return value.ToString("0.######", CultureInfo.InvariantCulture); }
        internal static string SeedNumber(int value) { return unchecked((uint)value).ToString(CultureInfo.InvariantCulture); }
        internal static string Bool(bool value) { return value ? "true" : "false"; }
        internal static string HashText(string text) { using (var sha = SHA256.Create()) return PrefixSha256(string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)))); }
        internal static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        internal static string PrefixSha256(string lowercaseHex) { return "sha256:" + lowercaseHex; }
        internal static bool IsCanonicalSha256(string value)
        {
            if (value == null || value.Length != 71 || !value.StartsWith("sha256:", StringComparison.Ordinal)) return false;
            for (var index = 7; index < value.Length; index++)
            {
                var character = value[index];
                if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))) return false;
            }
            return true;
        }

        internal static void RequireCanonicalSha256(string value, string label)
        {
            if (!IsCanonicalSha256(value)) throw new InvalidOperationException(label + " must use canonical sha256:<64 lowercase hex> form.");
        }
    }

    /// <summary>Caller-supplied source identities recorded alongside every S0a capture.</summary>
    [Serializable]
    public sealed class W24CaptureSourceHashes
    {
        public string SceneSha256;
        public string SceneSourcePath;
        public string PrefabGuid;
        public string PrefabSourcePath;
        public string PrefabSha256;
        public string ManifestSourcePath;
        public string ManifestSha256;
        public string BuildHash;
        public string CaptureToolSourcePath;
        public string CaptureToolVersion;
        public string CaptureToolSha256;

        public void Validate()
        {
            if (string.IsNullOrEmpty(SceneSourcePath) || string.IsNullOrEmpty(SceneSha256) || string.IsNullOrEmpty(PrefabGuid) || string.IsNullOrEmpty(PrefabSourcePath) || string.IsNullOrEmpty(PrefabSha256) || string.IsNullOrEmpty(ManifestSourcePath) || string.IsNullOrEmpty(ManifestSha256) || string.IsNullOrEmpty(BuildHash) || string.IsNullOrEmpty(CaptureToolSourcePath) || string.IsNullOrEmpty(CaptureToolVersion) || string.IsNullOrEmpty(CaptureToolSha256))
                throw new InvalidOperationException("W24 source hashes must include scene/prefab/manifest/tool source paths and hashes, prefab GUID, build hash, and tool version.");
            W24CaptureProfile.RequireCanonicalSha256(SceneSha256, "Scene hash");
            W24CaptureProfile.RequireCanonicalSha256(PrefabSha256, "Prefab hash");
            W24CaptureProfile.RequireCanonicalSha256(ManifestSha256, "Manifest hash");
            W24CaptureProfile.RequireCanonicalSha256(BuildHash, "Build hash");
            W24CaptureProfile.RequireCanonicalSha256(CaptureToolSha256, "Capture tool hash");
        }

        public string ToJson()
        {
            Validate();
            return "{\"scene\":{\"path\":\"" + W24CaptureProfile.Escape(SceneSourcePath) + "\",\"sha256\":\"" + W24CaptureProfile.Escape(SceneSha256) + "\"},\"prefab\":{\"path\":\"" + W24CaptureProfile.Escape(PrefabSourcePath) + "\",\"guid\":\"" + W24CaptureProfile.Escape(PrefabGuid) + "\",\"sha256\":\"" + W24CaptureProfile.Escape(PrefabSha256) + "\"},\"manifest\":{\"path\":\"" + W24CaptureProfile.Escape(ManifestSourcePath) + "\",\"sha256\":\"" + W24CaptureProfile.Escape(ManifestSha256) + "\",\"buildHash\":\"" + W24CaptureProfile.Escape(BuildHash) + "\"},\"captureTool\":{\"path\":\"" + W24CaptureProfile.Escape(CaptureToolSourcePath) + "\",\"version\":\"" + W24CaptureProfile.Escape(CaptureToolVersion) + "\",\"sha256\":\"" + W24CaptureProfile.Escape(CaptureToolSha256) + "\"}}";
        }

        public static W24CaptureSourceHashes FromFiles(string scenePath, string prefabPath, string prefabGuid, string manifestPath, string buildHash, string captureToolPath, string captureToolVersion)
        {
            W24CaptureProfile.RequireCanonicalSha256(buildHash, "Build hash");
            return new W24CaptureSourceHashes
            {
                SceneSourcePath = scenePath,
                SceneSha256 = HashSourceFile(scenePath),
                PrefabSourcePath = prefabPath,
                PrefabGuid = prefabGuid,
                PrefabSha256 = HashSourceFile(prefabPath),
                ManifestSourcePath = manifestPath,
                ManifestSha256 = HashSourceFile(manifestPath),
                BuildHash = buildHash,
                CaptureToolSourcePath = captureToolPath,
                CaptureToolVersion = captureToolVersion,
                CaptureToolSha256 = HashSourceFile(captureToolPath)
            };
        }

        private static string HashSourceFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) throw new System.IO.FileNotFoundException("W24 capture source file was not found.", path);
            using (var sha = SHA256.Create()) using (var stream = System.IO.File.OpenRead(path)) return W24CaptureProfile.PrefixSha256(string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2", CultureInfo.InvariantCulture))));
        }
    }
}
