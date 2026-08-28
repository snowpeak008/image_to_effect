using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using VFXComposer.Editor.W24.S5;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W24S5CandidateEvidenceReaderTests
    {
        private static readonly string[] LegacyEffects =
        {
            "sustained_flame_3d",
            "w24_moving_projectile_trail",
            "w24_weapon_socket_fragments",
            "w24_real_light_receivers"
        };

        [Test]
        public void MissingOrOutOfRangeRequest_IsInvalidWithoutThrowing()
        {
            var missing = W24S5CandidateEvidenceReader.Read(new W24S5CandidateEvidenceReadRequest
            {
                CandidateReceiptPath = "docs/vfx-candidates/missing_effect/C0/candidate-receipt.json",
                CandidateReceiptFileHash = "sha256:" + new string('0', 64),
                EvidenceRevision = 1
            });
            Assert.That(missing.Status, Is.EqualTo(W24S5CandidateEvidenceReadResult.InvalidStatus));
            Assert.That(missing.Snapshot, Is.Null);
            Assert.That(missing.Errors, Is.Not.Empty);

            foreach (var revision in new[] { 0, 3 })
            {
                var invalidRevision = W24S5CandidateEvidenceReader.Read(new W24S5CandidateEvidenceReadRequest
                {
                    CandidateReceiptPath = "docs/vfx-candidates/missing_effect/C0/candidate-receipt.json",
                    CandidateReceiptFileHash = "sha256:" + new string('0', 64),
                    EvidenceRevision = revision
                });
                Assert.That(invalidRevision.Status, Is.EqualTo(W24S5CandidateEvidenceReadResult.InvalidStatus));
                Assert.That(invalidRevision.Snapshot, Is.Null);
                Assert.That(invalidRevision.Errors.Single(), Does.Contain("evidence revision E1 or E2"));
            }
        }

        [Test]
        public void ExistingLegacyC0Candidates_ReplayCandidateIdentityButRejectMissingPreVerdictEvidenceDescriptor()
        {
            var available = LegacyEffects
                .Select(effect => new
                {
                    Effect = effect,
                    Receipt = "docs/vfx-candidates/" + effect + "/C0/candidate-receipt.json"
                })
                .Where(item => File.Exists(Absolute(item.Receipt)))
                .ToArray();
            if (available.Length == 0)
                Assert.Ignore("This checkout has no frozen W24 legacy C0 candidates; run this fixture in the isolated W24 shadow that owns them.");

            Assert.That(available.Select(item => item.Effect), Is.EquivalentTo(LegacyEffects),
                "A partial legacy C0 set cannot be used as a production-reader credential.");
            foreach (var item in available)
            {
                var before = SnapshotFiles("docs/vfx-candidates/" + item.Effect + "/C0");
                var result = W24S5CandidateEvidenceReader.Read(new W24S5CandidateEvidenceReadRequest
                {
                    CandidateReceiptPath = item.Receipt,
                    CandidateReceiptFileHash = HashFile(Absolute(item.Receipt)),
                    EvidenceRevision = 1
                });
                CollectionAssert.AreEqual(before, SnapshotFiles("docs/vfx-candidates/" + item.Effect + "/C0"));
                Assert.That(result.Status, Is.EqualTo(W24S5CandidateEvidenceReadResult.InvalidStatus), item.Effect);
                Assert.That(result.Snapshot, Is.Not.Null, "Candidate replay must complete before the missing pre-verdict evidence descriptor is reported: " + item.Effect + " :: " + string.Join(" | ", result.Errors));
                Assert.That(result.Snapshot.EffectId, Is.EqualTo(item.Effect));
                Assert.That(result.Snapshot.CandidateId, Is.EqualTo("C0"));
                Assert.That(result.Snapshot.CandidateRevision, Is.EqualTo(0));
                Assert.That(result.Snapshot.EvidenceRevision, Is.EqualTo(1));
                Assert.That(result.Errors.Single(), Does.Contain("no immutable E1 revision descriptor"));
            }
        }

        [Test]
        public void ExistingLegacyC0_E2IsExplicitlyUnsupportedAndNeverInvented()
        {
            var effect = LegacyEffects.FirstOrDefault(value => File.Exists(Absolute("docs/vfx-candidates/" + value + "/C0/candidate-receipt.json")));
            if (effect == null)
                Assert.Ignore("This checkout has no frozen W24 legacy C0 candidate.");
            var receipt = "docs/vfx-candidates/" + effect + "/C0/candidate-receipt.json";
            var result = W24S5CandidateEvidenceReader.Read(new W24S5CandidateEvidenceReadRequest
            {
                CandidateReceiptPath = receipt,
                CandidateReceiptFileHash = HashFile(Absolute(receipt)),
                EvidenceRevision = 2
            });
            Assert.That(result.Status, Is.EqualTo(W24S5CandidateEvidenceReadResult.InvalidStatus));
            Assert.That(result.Snapshot, Is.Not.Null, effect + " :: " + string.Join(" | ", result.Errors));
            Assert.That(result.Errors.Single(), Does.Contain("defines no legacy C0 E2 namespace"));
        }

        [Test]
        public void ReaderSource_HasNoFilesystemOrAssetMutationSurface()
        {
            var source = File.ReadAllText(Absolute("project/Packages/com.vfxcomposer.unity/Editor/W24/S5/W24S5CandidateEvidenceReader.cs"));
            foreach (var forbidden in new[]
            {
                "FileMode.Create", "FileMode.Append", "File.Write", "File.Copy", "File.Move", "File.Delete",
                "Directory.CreateDirectory", "Directory.Move", "Directory.Delete", "StreamWriter", "AssetDatabase."
            })
                Assert.That(source, Does.Not.Contain(forbidden), "Read-only candidate/evidence replay must not expose mutation: " + forbidden);
            Assert.That(source, Does.Contain("VALID_READ_ONLY"));
            Assert.That(source, Does.Contain("no immutable E1 revision descriptor"));
            Assert.That(source, Does.Contain("no committed C1/C2 E1/E2 transition schema"));
        }

        private static string[] SnapshotFiles(string relativeRoot)
        {
            var root = Absolute(relativeRoot);
            return Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => path.Substring(root.Length).Replace('\\', '/') + "|" + HashFile(path))
                .ToArray();
        }

        private static string HashFile(string path)
        {
            using (var input = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return "sha256:" + string.Concat(sha.ComputeHash(input).Select(value => value.ToString("x2")));
        }

        private static string Absolute(string relative)
        {
            return Path.GetFullPath(Path.Combine(RepositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string RepositoryRoot
        {
            get { return Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName; }
        }
    }
}
