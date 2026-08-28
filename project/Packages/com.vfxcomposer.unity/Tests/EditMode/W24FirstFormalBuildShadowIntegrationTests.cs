using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.W24;
using VFXComposer.Editor.W24.S3;

namespace VFXComposer.Tests.EditMode
{
    /// <summary>
    /// Destructive only inside an isolated copied project. These tests exercise the actual S0b/S3
    /// authorers (not a hand-written fake transaction) and are deliberately opt-in so a normal
    /// contributor's open project is never used as a rollback fixture.
    /// </summary>
    [Explicit("Run only in a disposable shadow project with VFX_W24_SHADOW_INTEGRATION=1.")]
    public sealed class W24FirstFormalBuildShadowIntegrationTests
    {
        private string projectRoot;
        private string repositoryRoot;

        [SetUp]
        public void SetUp()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("VFX_W24_SHADOW_INTEGRATION"), "1", StringComparison.Ordinal))
                Assert.Ignore("W24 authoring rollback integration is shadow-project only.");
            projectRoot = Directory.GetParent(Application.dataPath).FullName;
            repositoryRoot = Directory.GetParent(projectRoot).FullName;
            W24FirstFormalBuildTransaction.FaultInjectionHook = null;
        }

        [TearDown]
        public void TearDown()
        {
            W24FirstFormalBuildTransaction.FaultInjectionHook = null;
        }

        [Test]
        public void S0b_ActualAuthorer_RollsBackReceiptCandidateAndExistingOwnedBytes()
        {
            RequireNoManifest(SustainedFlameAuthoring.EffectId);
            RequireNoCandidate(SustainedFlameAuthoring.EffectId);
            var sentinel = ProjectAbsolute(SustainedFlameAuthoring.OutputFolder + "/preexisting-sentinel.bin");
            var recipe = ProjectAbsolute(SustainedFlameAuthoring.RecipePath);
            Write(sentinel, "s0b-old-owned-bytes");
            Write(sentinel + ".meta", "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\n");
            Write(recipe, "s0b-old-recipe-bytes");
            var before = Capture(
                ProjectAbsolute(SustainedFlameAuthoring.OutputFolder), ProjectAbsolute(SustainedFlameAuthoring.RecipePath), ProjectAbsolute(SustainedFlameAuthoring.PreviewScenePath),
                ProjectAbsolute(SustainedFlameAuthoring.ManifestPath), RepositoryAbsolute("docs/vfx-candidates/" + SustainedFlameAuthoring.EffectId));

            W24FirstFormalBuildTransaction.FaultInjectionHook = checkpoint =>
            {
                if (checkpoint != "s0b.after-c0-freeze") return;
                var generatedRecipe = File.ReadAllText(recipe);
                Assert.That(generatedRecipe, Does.Contain("\"id\": \"sustained_flame_3d\""), "The real authorer must replace stale Recipe bytes before computing its formal Manifest identity.");
                Assert.That(generatedRecipe, Is.Not.EqualTo("s0b-old-recipe-bytes"));
                throw new InvalidOperationException("shadow S0b fault after receipt and C0 freeze");
            };
            Assert.Throws<InvalidOperationException>(() => SustainedFlameAuthoring.BuildAssetsAndPreview());

            CollectionAssert.AreEquivalent(before, Capture(
                ProjectAbsolute(SustainedFlameAuthoring.OutputFolder), ProjectAbsolute(SustainedFlameAuthoring.RecipePath), ProjectAbsolute(SustainedFlameAuthoring.PreviewScenePath),
                ProjectAbsolute(SustainedFlameAuthoring.ManifestPath), RepositoryAbsolute("docs/vfx-candidates/" + SustainedFlameAuthoring.EffectId)));
            Assert.That(File.Exists(ProjectAbsolute(SustainedFlameAuthoring.ManifestPath)), Is.False);
            Assert.That(File.ReadAllText(recipe), Is.EqualTo("s0b-old-recipe-bytes"), "A pre-existing owned Recipe must be restored byte-for-byte after the actual authorer fails.");
            Assert.That(Directory.Exists(RepositoryAbsolute("docs/vfx-candidates/" + SustainedFlameAuthoring.EffectId)), Is.False);
        }

        [Test]
        public void S3_ActualAuthorer_RollsBackAllThreeReceiptsCandidatesAndExistingOwnedBytes()
        {
            var cases = new[]
            {
                new { Id = W24S3BaselineAuthoring.ProjectileId, Output = W24S3BaselineAuthoring.ProjectileOutputFolder, Manifest = W24S3BaselineAuthoring.ProjectileManifest, Preview = W24S3BaselineAuthoring.ProjectilePreview },
                new { Id = W24S3BaselineAuthoring.BindingId, Output = W24S3BaselineAuthoring.BindingOutputFolder, Manifest = W24S3BaselineAuthoring.BindingManifest, Preview = W24S3BaselineAuthoring.BindingPreview },
                new { Id = W24S3BaselineAuthoring.LightingId, Output = W24S3BaselineAuthoring.LightingOutputFolder, Manifest = W24S3BaselineAuthoring.LightingManifest, Preview = W24S3BaselineAuthoring.LightingPreview }
            };
            foreach (var item in cases)
            {
                RequireNoManifest(item.Id);
                RequireNoCandidate(item.Id);
                Write(ProjectAbsolute(item.Output + "/preexisting-sentinel.bin"), item.Id + "-old-owned-bytes");
            }
            var targets = cases.SelectMany(item => new[]
            {
                ProjectAbsolute(item.Output), ProjectAbsolute(item.Preview), ProjectAbsolute(item.Manifest), RepositoryAbsolute("docs/vfx-candidates/" + item.Id)
            }).ToArray();
            var before = Capture(targets);

            W24FirstFormalBuildTransaction.FaultInjectionHook = checkpoint =>
            {
                if (checkpoint == "s3.after-c0-freezes") throw new InvalidOperationException("shadow S3 fault after all receipts and C0 freezes");
            };
            Assert.Throws<InvalidOperationException>(() => W24S3BaselineAuthoring.BuildAll());

            CollectionAssert.AreEquivalent(before, Capture(targets));
            foreach (var item in cases)
            {
                Assert.That(File.Exists(ProjectAbsolute(item.Manifest)), Is.False, item.Id);
                Assert.That(Directory.Exists(RepositoryAbsolute("docs/vfx-candidates/" + item.Id)), Is.False, item.Id);
            }
        }

        private void RequireNoManifest(string effectId)
        {
            var path = ProjectAbsolute("ProjectSettings/VFXComposer/BuildManifests/" + effectId + ".manifest.json");
            if (File.Exists(path)) Assert.Ignore("Shadow integration requires an unbuilt first-formal baseline: " + effectId);
        }

        private void RequireNoCandidate(string effectId)
        {
            var path = RepositoryAbsolute("docs/vfx-candidates/" + effectId);
            if (Directory.Exists(path)) Assert.Ignore("Shadow integration requires no pre-existing C0 candidate: " + effectId);
        }

        private static string[] Capture(params string[] targets)
        {
            return targets.SelectMany(CaptureOne).OrderBy(entry => entry, StringComparer.Ordinal).ToArray();
        }

        private static IEnumerable<string> CaptureOne(string target)
        {
            if (File.Exists(target)) return new[] { Entry(target) }.Concat(File.Exists(target + ".meta") ? new[] { Entry(target + ".meta") } : Array.Empty<string>());
            if (!Directory.Exists(target)) return Array.Empty<string>();
            return Directory.GetFiles(target, "*", SearchOption.AllDirectories).Select(Entry).Concat(File.Exists(target + ".meta") ? new[] { Entry(target + ".meta") } : Array.Empty<string>());
        }

        private static string Entry(string path) { return path + "=" + Convert.ToBase64String(File.ReadAllBytes(path)); }
        private static void Write(string path, string value) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, value); }
        private string ProjectAbsolute(string relative) { return Path.GetFullPath(Path.Combine(projectRoot, relative.Replace('/', Path.DirectorySeparatorChar))); }
        private string RepositoryAbsolute(string relative) { return Path.GetFullPath(Path.Combine(repositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar))); }
    }
}
