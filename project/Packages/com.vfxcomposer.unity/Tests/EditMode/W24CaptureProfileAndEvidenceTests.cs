using System;
using System.IO;
using NUnit.Framework;
using VFXComposer.W24;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W24CaptureProfileAndEvidenceTests
    {
        [Test]
        public void CaptureProfile_RequiresOneCanonicalAndTwoDistinctRobustnessSeeds()
        {
            var profile = Profile();
            Assert.DoesNotThrow(profile.Validate);
            Assert.That(profile.AllSeeds(), Is.EquivalentTo(new[] { 101, 202, 303 }));
            Assert.That(profile.Sha256, Is.EqualTo(profile.Sha256), "The frozen Capture Profile hash must be deterministic.");
            StringAssert.StartsWith("sha256:", profile.Sha256);
            Assert.That(profile.Sha256, Has.Length.EqualTo(71));

            profile.RobustnessSeeds = new[] { 202 };
            Assert.Throws<InvalidOperationException>(profile.Validate);
            profile.RobustnessSeeds = new[] { 101, 303 };
            Assert.Throws<InvalidOperationException>(profile.Validate);
            profile.RobustnessSeeds = new[] { 202, 303 };
            profile.Background = new UnityEngine.Color(float.NaN, 0f, 0f, 1f);
            Assert.Throws<InvalidOperationException>(profile.Validate);
        }

        [Test]
        public void PersistentHashes_RequireCanonicalPrefixedLowercaseSha256()
        {
            var profile = Profile();
            profile.RendererAssetSha256 = new string('a', 64);
            Assert.Throws<InvalidOperationException>(profile.Validate, "Raw hashes are not persistent W24 evidence hashes.");

            profile = Profile();
            profile.VolumeSha256 = "sha256:" + new string('A', 64);
            Assert.Throws<InvalidOperationException>(profile.Validate, "Uppercase hexadecimal is not canonical.");

            profile = Profile();
            profile.RendererAssetSha256 = "sha256:" + new string('a', 63);
            Assert.Throws<InvalidOperationException>(profile.Validate, "Wrong-length hashes are not canonical.");
        }

        [Test]
        public void CaptureProfile_PreservesPositiveUInt32OperatorSeedBitsInCanonicalEvidence()
        {
            var profile = Profile();
            const uint operatorSeed = 2885465331u;
            profile.CanonicalSeed = unchecked((int)operatorSeed);
            profile.RobustnessSeeds = new[] { unchecked((int)1234567891u), unchecked((int)2345678912u) };
            Assert.DoesNotThrow(profile.Validate);
            Assert.That(profile.ContainsSeed(operatorSeed), Is.True);
            StringAssert.Contains("\"canonicalSeed\":2885465331", profile.ToCanonicalJson());
        }

        [Test]
        public void FormalCapturePolicy_RejectsNonBatchAndNoGraphicsConfigurations()
        {
            Assert.Throws<InvalidOperationException>(() => W24ContinuousCaptureRecorder.ValidateBatchmodePolicy(true, false));
            Assert.Throws<InvalidOperationException>(() => W24ContinuousCaptureRecorder.ValidateBatchmodePolicy(false, true));
            Assert.DoesNotThrow(() => W24ContinuousCaptureRecorder.ValidateBatchmodePolicy(true, true));
        }

        [Test]
        public void EvidenceStore_IsWriteOnce_AndRejectsTraversalAndPostSealWrites()
        {
            var root = TemporaryDirectory();
            try
            {
                var store = W24EvidenceStore.Create(root, "C0", Profile().Sha256);
                var firstHash = store.WriteText("frames/seed_101/frame_00000_beauty.txt", "beauty");
                Assert.That(firstHash, Has.Length.EqualTo(71));
                StringAssert.StartsWith("sha256:", firstHash);
                Assert.Throws<InvalidOperationException>(() => W24EvidenceStore.Create(Path.Combine(root, "raw-hash"), "C0", new string('a', 64)));
                Assert.Throws<InvalidOperationException>(() => store.WriteText("frames/seed_101/frame_00000_beauty.txt", "replacement"));
                Assert.Throws<ArgumentException>(() => store.WriteText("../outside.txt", "unsafe"));
                store.Seal();
                Assert.That(store.IsSealed, Is.True);
                Assert.Throws<InvalidOperationException>(() => store.WriteText("later.txt", "forbidden"));
                Assert.That(File.Exists(Path.Combine(root, "evidence-lock.json")), Is.True);
            }
            finally { DeleteTemporaryDirectory(root); }
        }

        [Test]
        public void SourceHashes_AreReadFromTheActualFrozenSourceFiles()
        {
            var root = TemporaryDirectory();
            try
            {
                var scene = Path.Combine(root, "scene.unity"); var prefab = Path.Combine(root, "effect.prefab"); var manifest = Path.Combine(root, "manifest.json"); var tool = Path.Combine(root, "capture-tool.cs");
                File.WriteAllText(scene, "scene-source"); File.WriteAllText(prefab, "prefab-source"); File.WriteAllText(manifest, "manifest-source"); File.WriteAllText(tool, "tool-source");
                Assert.Throws<InvalidOperationException>(() => W24CaptureSourceHashes.FromFiles(scene, prefab, "0123456789abcdef0123456789abcdef", manifest, new string('a', 64), tool, "w24-test-tool/v1"));
                var hashes = W24CaptureSourceHashes.FromFiles(scene, prefab, "0123456789abcdef0123456789abcdef", manifest, Hash('a'), tool, "w24-test-tool/v1");
                Assert.DoesNotThrow(hashes.Validate);
                Assert.That(hashes.SceneSha256, Has.Length.EqualTo(71));
                StringAssert.StartsWith("sha256:", hashes.SceneSha256);
                Assert.That(hashes.PrefabSha256, Is.Not.EqualTo(hashes.SceneSha256));
                StringAssert.Contains("manifest", hashes.ToJson());
                StringAssert.Contains("captureTool", hashes.ToJson());
                hashes.BuildHash = new string('a', 64);
                Assert.Throws<InvalidOperationException>(hashes.Validate);
            }
            finally { DeleteTemporaryDirectory(root); }
        }

        private static W24CaptureProfile Profile()
        {
            return new W24CaptureProfile
            {
                UnityVersion = "2022.3-test",
                UrpVersion = "14.0-test",
                GraphicsApi = "Direct3D11",
                GraphicsDevice = "test-device",
                GraphicsDriverVersion = "test-driver",
                RenderTextureFormat = "ARGB32",
                RendererAssetReference = "Assets/Settings/Renderer.asset",
                RendererAssetSha256 = Hash('e'),
                VolumeReference = "Assets/Settings/Volume.asset",
                VolumeSha256 = Hash('f'),
                ColorSpace = "Linear",
                ScenePath = "Assets/VFX/W24/Tests/SerializedCaptureScene.unity",
                SerializedCameraReference = "SerializedCaptureScene/MainCamera",
                CanonicalSeed = 101,
                RobustnessSeeds = new[] { 202, 303 },
                RetainedFrameIndices = new[] { 1, 9, 42 }
            };
        }

        private static string TemporaryDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "vfxcomposer-w24-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path); return path;
        }

        private static void DeleteTemporaryDirectory(string path)
        {
            if (!Directory.Exists(path)) return;
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(path, true);
        }

        private static string Hash(char character) { return "sha256:" + new string(character, 64); }
    }
}
