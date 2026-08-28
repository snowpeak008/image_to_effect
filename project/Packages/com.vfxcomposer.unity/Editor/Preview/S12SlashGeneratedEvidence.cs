using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using VFXComposer;
using VFXComposer.Editor.SlashV2;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Editor.Preview
{
    /// <summary>Captures generated Slash output only. Gold templates and S12A PNGs are never inputs to this evidence.</summary>
    public static class S12SlashGeneratedEvidence
    {
        public const string EvidenceRelative = "docs/stage-notes/s12b-evidence";
        private const int Width = 960;
        private const int Height = 540;
        [MenuItem("VFX Composer/S12B/Capture Generated Slash Evidence")]
        public static void CaptureBatch()
        {
#if false // Historical sampler deliberately cannot be regenerated as current runtime evidence.
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", EvidenceRelative)); Directory.CreateDirectory(root); var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(S12SlashCompiler.OutputPrefabPath); if (prefab == null) throw new InvalidOperationException("Build generated Slash before capture."); var cameraGo = new GameObject("S12B_EvidenceCamera"); var camera = cameraGo.AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f, .17f, .19f); camera.allowHDR = false; camera.allowMSAA = false; camera.fieldOfView = 60f; camera.transform.position = new Vector3(0f, 2.4f, -7.6f); camera.transform.LookAt(new Vector3(0f, .38f, 0f)); var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            try
            {
                var controller = instance.GetComponent<SlashEffectController>(); if (controller == null) throw new InvalidOperationException("Generated Slash lacks Runtime controller."); controller.PlaySlash(Vector3.zero, Quaternion.identity); var frames = new[] { new Frame("primary_overlap", .18f), new Frame("afterimage", .24f), new Frame("dissipation", .38f), new Frame("complete", .451f) }; var metadata = frames.Select(frame => { controller.SampleForPreview(frame.Time); var file = "time_" + frame.Name + ".png"; Capture(camera, Path.Combine(root, file)); var particles = instance.GetComponentsInChildren<ParticleSystem>(true).Select(ParticleFacts); return "{ \"phase\": \"" + frame.Name + "\", \"time\": " + frame.Time.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ", \"file\": \"" + file + "\", \"sha256\": \"" + Hash(Path.Combine(root, file)) + "\", \"particles\": [" + string.Join(",", particles) + "] }"; }); var manifest = JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "VFX", "Generated", "slash_3d_stylized", "BuildManifest.json"))); var json = "{\n  \"capture\": \"generated Prefab instantiated; SlashEffectController.PlaySlash then internal deterministic controller sample; natural template burst/shape simulation only; Camera.Render; HDR false; Bloom off\",\n  \"sourcePrefabPath\": \"" + S12SlashCompiler.OutputPrefabPath + "\",\n  \"sourcePrefabGuid\": \"" + AssetDatabase.AssetPathToGUID(S12SlashCompiler.OutputPrefabPath) + "\",\n  \"recipeHash\": \"" + (string)manifest["recipeHash"] + "\",\n  \"buildHash\": \"" + (string)manifest["buildHash"] + "\",\n  \"fov\": 60,\n  \"timelineFrames\": [" + string.Join(",", metadata) + "]\n}\n"; File.WriteAllText(Path.Combine(root, "metadata.json"), json, new UTF8Encoding(false));
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); UnityEngine.Object.DestroyImmediate(cameraGo); }
#endif
            throw new InvalidOperationException("S12B sampler evidence is permanently rejected. Use the serialized-camera continuous WYSIWYG capture instead.");
        }

        /// <summary>Normal regression runs verify the committed capture instead of invoking Camera.Render again.</summary>
        public static bool VerifyExisting()
        {
            try
            {
                var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", EvidenceRelative)); var path = Path.Combine(root, "metadata.json"); if (!File.Exists(path)) return false;
                var metadata = JObject.Parse(File.ReadAllText(path)); var manifest = JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "VFX", "Generated", "slash_3d_stylized", "BuildManifest.json")));
                if ((string)metadata["sourcePrefabPath"] != S12SlashCompiler.OutputPrefabPath || (string)metadata["sourcePrefabGuid"] != AssetDatabase.AssetPathToGUID(S12SlashCompiler.OutputPrefabPath) || (string)metadata["recipeHash"] != (string)manifest["recipeHash"] || (string)metadata["buildHash"] != (string)manifest["buildHash"] || (string)metadata["recipeHash"] != RecipeCanonicalizer.ComputeSha256(File.ReadAllText(Path.Combine(Application.dataPath, "VFX", "Recipes", "Slash", "slash-3d-stylized.default.v2.json")))) return false;
                var frames = (JArray)metadata["timelineFrames"]; if (frames == null || !frames.Select(item => (string)item["phase"]).SequenceEqual(new[] { "primary_overlap", "afterimage", "dissipation", "complete" })) return false;
                return frames.Children<JObject>().All(frame => File.Exists(Path.Combine(root, (string)frame["file"])) && Hash(Path.Combine(root, (string)frame["file"])) == (string)frame["sha256"]);
            }
            catch { return false; }
        }

        private static void Capture(Camera camera, string path) { var render = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32); var old = RenderTexture.active; try { camera.targetTexture = render; camera.Render(); RenderTexture.active = render; var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false); texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); texture.Apply(false); File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture); } finally { camera.targetTexture = null; RenderTexture.active = old; RenderTexture.ReleaseTemporary(render); } }
        private static string ParticleFacts(ParticleSystem particle) { var values = new ParticleSystem.Particle[particle.particleCount]; var count = particle.GetParticles(values); var distinct = values.Take(count).Select(value => value.position.ToString("F4")).Distinct(StringComparer.Ordinal).Count(); var bounds = count == 0 ? new Bounds(Vector3.zero, Vector3.zero) : new Bounds(values[0].position, Vector3.zero); for (var index = 0; index < count; index++) { var radius = Mathf.Max(.01f, values[index].GetCurrentSize(particle) * .5f); bounds.Encapsulate(values[index].position + Vector3.one * radius); bounds.Encapsulate(values[index].position - Vector3.one * radius); } return "{ \"name\": \"" + particle.name + "\", \"particleCount\": " + count + ", \"distinctPositions\": " + distinct + ", \"boundsSize\": [" + bounds.size.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ", " + bounds.size.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ", " + bounds.size.z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "] }"; }
        private static string Hash(string path) { using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(File.ReadAllBytes(path)).Select(value => value.ToString("X2"))); }
        private struct Frame { public readonly string Name; public readonly float Time; public Frame(string name, float time) { Name = name; Time = time; } }
    }
}
