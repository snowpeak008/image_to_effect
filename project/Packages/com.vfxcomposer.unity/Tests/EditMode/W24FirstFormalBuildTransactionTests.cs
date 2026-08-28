using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.W24;
using VFXComposer.Editor.W24.S3;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W24FirstFormalBuildTransactionTests
    {
        private string projectRoot;
        private string repositoryRoot;
        private string token;
        private string output;
        private string recipe;
        private string preview;
        private string manifest;
        private string candidate;

        [SetUp]
        public void SetUp()
        {
            projectRoot = Directory.GetParent(Application.dataPath).FullName;
            repositoryRoot = Directory.GetParent(projectRoot).FullName;
            token = "w24_transaction_" + Guid.NewGuid().ToString("N");
            output = Path.Combine(projectRoot, "Assets", "VFX", "Effects", "W24TransactionTests", token);
            recipe = Path.Combine(projectRoot, "Assets", "VFX", "Recipes", "W24TransactionTests", token + ".json");
            preview = Path.Combine(projectRoot, "Assets", "VFX", "Preview", "W24TransactionTests", token + ".unity");
            manifest = Path.Combine(projectRoot, "ProjectSettings", "VFXComposer", "BuildManifests", token + ".manifest.json");
            candidate = Path.Combine(repositoryRoot, "docs", "vfx-candidates", token, "C0");
        }

        [TearDown]
        public void TearDown()
        {
            W24FirstFormalBuildTransaction.FaultInjectionHook = null;
            Delete(output); Delete(recipe); Delete(preview); Delete(manifest); Delete(candidate);
            var candidateParent = Directory.GetParent(candidate).FullName;
            if (Directory.Exists(candidateParent) && !Directory.EnumerateFileSystemEntries(candidateParent).Any()) Directory.Delete(candidateParent);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [Test]
        public void ExistingOwnedState_IsRestoredByteForByte_WhenFaultIsInjectedAfterAllArtifactsAreWritten()
        {
            SeedExistingState();
            var before = Capture(output, recipe, preview, manifest, candidate);
            W24FirstFormalBuildTransaction.FaultInjectionHook = checkpoint =>
            {
                if (checkpoint == "transaction.test.after-write") throw new InvalidOperationException("injected failure");
            };

            Assert.Throws<InvalidOperationException>(() =>
            {
                using (var transaction = W24FirstFormalBuildTransaction.Begin(output, recipe, preview, manifest, candidate))
                {
                    MutateAllOwnedTargets();
                    W24FirstFormalBuildTransaction.ThrowIfFaultInjected("transaction.test.after-write");
                    transaction.Commit();
                }
            });

            CollectionAssert.AreEquivalent(before, Capture(output, recipe, preview, manifest, candidate), "A failed first build must restore old asset bytes, .meta GUIDs, preview, manifest and C0 candidate exactly.");
        }

        [Test]
        public void FirstBuildFailure_LeavesNoOwnedArtifactOrMetaResidue()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                using (var transaction = W24FirstFormalBuildTransaction.Begin(output, recipe, preview, manifest, candidate))
                {
                    MutateAllOwnedTargets();
                    throw new InvalidOperationException("injected first-build failure");
                }
            });

            foreach (var target in new[] { output, recipe, preview, manifest, candidate })
            {
                Assert.That(File.Exists(target) || Directory.Exists(target), Is.False, "Failed first build leaked: " + target);
                Assert.That(File.Exists(target + ".meta"), Is.False, "Failed first build leaked GUID meta: " + target + ".meta");
            }
        }

        [Test]
        public void FirstBuildFailure_RemovesOnlyParentsCreatedByThisTransaction()
        {
            // The final segment remains the approved effect id; the intermediate directories are
            // intentionally absent at Begin and must not survive a failed first build.
            output = Path.Combine(projectRoot, "Assets", "VFX", "Effects", "W24Parent_" + token, token);
            var effectParent = Directory.GetParent(output).FullName;
            Assert.That(Directory.Exists(effectParent), Is.False);

            Assert.Throws<InvalidOperationException>(() =>
            {
                using (var transaction = W24FirstFormalBuildTransaction.Begin(output, recipe, preview, manifest, candidate))
                {
                    MutateAllOwnedTargets();
                    throw new InvalidOperationException("injected parent cleanup failure");
                }
            });

            Assert.That(Directory.Exists(effectParent), Is.False, "A newly-created owned parent must be removed on rollback.");
            Assert.That(File.Exists(effectParent + ".meta"), Is.False);
            var candidateEffectParent = Directory.GetParent(candidate).FullName;
            Assert.That(Directory.Exists(candidateEffectParent), Is.False, "C0 candidate parent must not remain after a failed first build.");
        }

        [Test]
        public void TransactionRejectsOutsideRootsAndInconsistentEffectIdentityBeforeSnapshotting()
        {
            Assert.Throws<ArgumentException>(() => W24FirstFormalBuildTransaction.Begin(projectRoot));
            var mismatchedManifest = Path.Combine(projectRoot, "ProjectSettings", "VFXComposer", "BuildManifests", "other_effect.manifest.json");
            Assert.Throws<ArgumentException>(() => W24FirstFormalBuildTransaction.Begin(output, recipe, preview, mismatchedManifest, candidate));
            Assert.Throws<ArgumentException>(() => W24FirstFormalBuildTransaction.Begin(output, recipe, preview, manifest, Path.Combine(repositoryRoot, "docs", "vfx-candidates", token, "C1")));
        }

        [Test]
        public void BaselineAuthoring_UsesEffectOwnedMaterials_AndDoesNotNameLegacySharedWriteTargets()
        {
            foreach (var path in new[]
            {
                SustainedFlameAuthoring.AdditiveMaterialPath, SustainedFlameAuthoring.AlphaMaterialPath
            }) Assert.That(path, Does.StartWith(SustainedFlameAuthoring.OutputFolder + "/"), path);
            Assert.That(SustainedFlameAuthoring.ReceiverMaterialPath, Does.Not.StartWith(SustainedFlameAuthoring.OutputFolder + "/"), "Preview-only receiver material must remain a read-only dependency, not an unreachable formal output.");

            foreach (var id in new[] { W24S3BaselineAuthoring.ProjectileId, W24S3BaselineAuthoring.BindingId, W24S3BaselineAuthoring.LightingId })
                Assert.That(W24S3BaselineAuthoring.MaterialPath(id), Does.StartWith("Assets/VFX/Generated/" + id + "/"), id);
        }

        private void SeedExistingState()
        {
            Write(Path.Combine(output, "old.prefab"), "old-prefab");
            Write(Path.Combine(output, "old.prefab.meta"), "guid: 1a0e5fdb2b9f4d6a8c3e7f1b5d9a2468");
            Write(recipe, "old-recipe"); Write(recipe + ".meta", "guid: 2b1f6aec3c0a5e7b9d4f8a2c6e0b3579");
            Directory.CreateDirectory(Path.GetDirectoryName(preview));
            var validSceneFixture = Path.Combine(projectRoot, "Assets", "VFX", "Preview", "S5_2D_FireballGoldSample.unity");
            Assert.That(File.Exists(validSceneFixture), Is.True, "The rollback fixture requires an existing serialized Unity scene.");
            File.Copy(validSceneFixture, preview, true);
            Write(preview + ".meta", "guid: 3c2a7bfd4d1b6f8c0e5a9b3d7f1c468a");
            Write(manifest, "old-manifest");
            Write(Path.Combine(candidate, "candidate-receipt.json"), "old-candidate");
        }

        private void MutateAllOwnedTargets()
        {
            Delete(output); Write(Path.Combine(output, "new.prefab"), "new-prefab"); Write(Path.Combine(output, "new.prefab.meta"), "guid: 4d3b8ace5e2c7f9d1a6b0c4e8f2d579b");
            Write(recipe, "new-recipe"); Write(recipe + ".meta", "guid: 5e4c9bdf6f3d8a0e2b7c1d5f9a3e68ac");
            Write(preview, "new-preview"); Write(preview + ".meta", "guid: 6f5d0cae7a4e9b1f3c8d2e6a0b4f79bd");
            Write(manifest, "new-manifest");
            Delete(candidate); Write(Path.Combine(candidate, "candidate-receipt.json"), "new-candidate");
        }

        private static string[] Capture(params string[] targets)
        {
            return targets.SelectMany(target => CaptureOne(target)).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private static string[] CaptureOne(string target)
        {
            if (File.Exists(target)) return new[] { Entry(target) }.Concat(File.Exists(target + ".meta") ? new[] { Entry(target + ".meta") } : Array.Empty<string>()).ToArray();
            if (!Directory.Exists(target)) return Array.Empty<string>();
            return Directory.GetFiles(target, "*", SearchOption.AllDirectories).Select(Entry).Concat(File.Exists(target + ".meta") ? new[] { Entry(target + ".meta") } : Array.Empty<string>()).ToArray();
        }

        private static string Entry(string path) { return path + "=" + Convert.ToBase64String(File.ReadAllBytes(path)); }
        private static void Write(string path, string value) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, value); }
        private static void Delete(string target) { if (Directory.Exists(target)) Directory.Delete(target, true); else if (File.Exists(target)) File.Delete(target); if (File.Exists(target + ".meta")) File.Delete(target + ".meta"); }
    }
}
