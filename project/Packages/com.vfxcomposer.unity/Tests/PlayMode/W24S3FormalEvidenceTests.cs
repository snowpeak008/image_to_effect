using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace VFXComposer.Tests.PlayMode
{
    /// <summary>Explicit only: formal capture is WYSIWYG graphics work and is never part of normal regression.</summary>
    public sealed class W24S3FormalEvidenceTests
    {
        [Test, Explicit("Run only with Unity graphics enabled, formal scenes built, fixed capture profile and natural Update/LateUpdate playback.")]
        public void FormalEvidence_PreconditionsArePresentBeforeCapture()
        {
            var project = Directory.GetParent(Application.dataPath).FullName;
            var repository = Directory.GetParent(project).FullName;
            var cases = new[]
            {
                new { Id="w24_moving_projectile_trail", Scene="Assets/VFX/Preview/W24S3/VFXPREVIEW_MovingProjectileTrail.unity", Prefab="Assets/VFX/Generated/w24_moving_projectile_trail/VFX_w24_moving_projectile_trail.prefab" },
                new { Id="w24_weapon_socket_fragments", Scene="Assets/VFX/Preview/W24S3/VFXPREVIEW_ModelSocketFragments.unity", Prefab="Assets/VFX/Generated/w24_weapon_socket_fragments/VFX_w24_weapon_socket_fragments.prefab" },
                new { Id="w24_real_light_receivers", Scene="Assets/VFX/Preview/W24S3/VFXPREVIEW_RealLightReceivers.unity", Prefab="Assets/VFX/Generated/w24_real_light_receivers/VFX_w24_real_light_receivers.prefab" }
            };
            foreach (var item in cases)
            {
                var scenePath = ProjectAbsolute(project, item.Scene);
                var prefabPath = ProjectAbsolute(project, item.Prefab);
                var manifestPath = ProjectAbsolute(project, "ProjectSettings/VFXComposer/BuildManifests/" + item.Id + ".manifest.json");
                var candidateRoot = Path.Combine(repository, "docs", "vfx-candidates", item.Id, "C0");
                var contractPath = Path.Combine(candidateRoot, "design-contract.json");
                var tracePath = Path.Combine(candidateRoot, "implementation-trace.json");
                var receiptPath = Path.Combine(candidateRoot, "candidate-receipt.json");
                foreach (var path in new[] { scenePath, prefabPath, prefabPath + ".meta", manifestPath, contractPath, tracePath, receiptPath }) Assert.That(File.Exists(path), Is.True, "Missing formal C0 input: " + path);

                var manifest = JObject.Parse(File.ReadAllText(manifestPath));
                var contract = JObject.Parse(File.ReadAllText(contractPath));
                var trace = JObject.Parse(File.ReadAllText(tracePath));
                var receipt = JObject.Parse(File.ReadAllText(receiptPath));
                Assert.That((string)manifest["formalProduction"]["admissionPhase"], Is.EqualTo("PRE_C0_FIRST_FORMAL_BUILD"));
                Assert.That((string)manifest["formalProduction"]["visualStatus"], Is.EqualTo("VISUAL_PENDING"));
                Assert.That((string)contract["extensions"]["captureBindingStatus"], Is.EqualTo("FROZEN_PRE_C0"));
                Assert.That((string)contract["extensions"]["candidateStatus"], Is.EqualTo("C0_CAPTURE_PENDING"));
                Assert.That((string)contract["captureProfile"]["sceneSerializedReference"], Is.EqualTo(item.Scene));
                Assert.That((string)contract["captureProfile"]["sceneHash"], Is.EqualTo(HashFile(scenePath)));
                Assert.That((string)contract["captureProfile"]["prefabManifestHash"], Is.EqualTo("sha256:" + (string)manifest["buildHash"]));
                Assert.That((string)trace["traceStatus"], Is.EqualTo("C0_CAPTURE_PENDING"));
                Assert.That((string)trace["contractHash"], Is.EqualTo((string)contract["contractHash"]));
                Assert.That((string)trace["buildHash"], Is.EqualTo("sha256:" + (string)manifest["buildHash"]));
                Assert.That((string)trace["runtimeEntryAssetPath"], Is.EqualTo(item.Prefab));
                Assert.That((string)trace["runtimeEntryGuid"], Is.EqualTo(ReadGuid(prefabPath + ".meta")));
                Assert.That((string)receipt["contractFileHash"], Is.EqualTo(HashFile(contractPath)));
                Assert.That((string)receipt["traceFileHash"], Is.EqualTo(HashFile(tracePath)));
                var bootstrapManifestPath = Path.Combine(candidateRoot, "bootstrap-manifest.json");
                Assert.That(File.Exists(bootstrapManifestPath), Is.True);
                Assert.That((string)receipt["bootstrapManifestSnapshotPath"], Is.EqualTo("docs/vfx-candidates/" + item.Id + "/C0/bootstrap-manifest.json"));
                Assert.That((string)receipt["bootstrapManifestSnapshotFileHash"], Is.EqualTo(HashFile(bootstrapManifestPath)));
            }

            var bundlePath = Path.Combine(repository, "docs", "vfx-contracts", "capture-tools", "w24-s3-capture-tool.bundle.json");
            var bundleText = File.ReadAllText(bundlePath);
            var bundle = JObject.Parse(bundleText);
            foreach (var source in (JArray)bundle["sources"])
                Assert.That((string)source["sha256"], Is.EqualTo(HashFile(Path.Combine(repository, ((string)source["path"]).Replace('/', Path.DirectorySeparatorChar)))), "Capture-tool source drifted after registration.");
            var bundleHash = HashText(CanonicalJson(bundle));
            foreach (var item in cases)
            {
                var contractPath = Path.Combine(repository, "docs", "vfx-candidates", item.Id, "C0", "design-contract.json");
                Assert.That((string)JObject.Parse(File.ReadAllText(contractPath))["captureProfile"]["captureToolHash"], Is.EqualTo(bundleHash));
            }
            Assert.Pass("C0 candidate identities are real and internally consistent. Use W24ContinuousCaptureRecorder; do not Emit, Simulate, Sample, or jump time.");
        }

        private static string ProjectAbsolute(string projectRoot, string relative) { return Path.Combine(projectRoot, relative.Replace('/', Path.DirectorySeparatorChar)); }
        private static string ReadGuid(string metaPath)
        {
            var line = File.ReadLines(metaPath).FirstOrDefault(value => value.StartsWith("guid: ", StringComparison.Ordinal));
            Assert.That(line, Is.Not.Null);
            return line.Substring(6).Trim();
        }
        private static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
                return "sha256:" + string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
        }
        private static string HashText(string value)
        {
            using (var sha = SHA256.Create())
                return "sha256:" + string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(item => item.ToString("x2")));
        }
        private static string CanonicalJson(JToken token)
        {
            if (token is JObject obj)
            {
                var sorted = new JObject();
                foreach (var property in obj.Properties().OrderBy(value => value.Name, StringComparer.Ordinal)) sorted.Add(property.Name, JToken.Parse(CanonicalJson(property.Value)));
                return sorted.ToString(Formatting.None);
            }
            if (token is JArray array) return new JArray(array.Select(value => JToken.Parse(CanonicalJson(value)))).ToString(Formatting.None);
            return token.ToString(Formatting.None);
        }
    }
}
