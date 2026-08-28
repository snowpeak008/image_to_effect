using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using VFXComposer.Editor.W24.S5;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W24S5MachineFailureProducerTests
    {
        private const string EffectId = "w24_evidence_writer_probe";
        private const string CandidateRoot = "docs/vfx-candidates/" + EffectId + "/C0";
        private const string CandidateReceipt = CandidateRoot + "/candidate-receipt.json";
        private const string DescriptorPath = CandidateRoot + "/evidence/E1/evidence-revision.json";
        private const string RawRoot = "artifacts/vfx-evidence/" + EffectId + "/C0";
        private const string SchemaPath = "docs/schemas/w24-s5-evidence-revision-legacy-c0-s0b-v1.schema.json";
        private const string InputRoot = "tools/vfx/tests/w24_evidence_writer_probe_inputs";
        private const string WriterBundlePath = InputRoot + "/writer.bundle.json";
        private const string CaptureToolBundlePath = InputRoot + "/capture.bundle.json";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private W24S5EvidenceRevisionWriterTests writerHarness;
        private W24S5DescriptorStructureReplayRequest request;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            writerHarness = new W24S5EvidenceRevisionWriterTests();
            writerHarness.OneTimeSetUp();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            W24S5MachineFailureProducer.ResetTestHooks();
            if (writerHarness != null) writerHarness.OneTimeTearDown();
        }

        [SetUp]
        public void SetUp()
        {
            writerHarness.SetUp();
            var writeRequest = InvokeWriter<W24S5EvidenceRevisionWriteRequest>("WriteRequest", 1);
            var written = W24S5EvidenceRevisionWriter.Write(writeRequest);
            Assert.That(written.Succeeded, Is.True, string.Join(" | ", written.Errors));
            ConfigureStructureRegistry();
            request = CurrentRequest();
        }

        [TearDown]
        public void TearDown()
        {
            W24S5MachineFailureProducer.ResetTestHooks();
            writerHarness.TearDown();
        }

        [Test]
        public void ProductionRegistryPending_ReturnsBeforeAnyInputIo_AndPreservesWholeInScopeTrees()
        {
            W24S5MachineFailureProducer.ResetTestHooks();
            var before = WholeInScopeTreeSnapshot();
            var missingRoot = "docs/vfx-candidates/w24_phase_b_missing_probe/C0";
            var pending = W24S5MachineFailureProducer.ReplayDescriptorStructure(new W24S5DescriptorStructureReplayRequest
            {
                CandidateReceiptPath = missingRoot + "/candidate-receipt.json",
                CandidateReceiptFileHash = ZeroHash,
                EvidenceRevision = 1,
                EvidenceRevisionDescriptorPath = missingRoot + "/evidence/E1/evidence-revision.json",
                EvidenceRevisionDescriptorFileHash = ZeroHash
            });
            Assert.That(pending.Status, Is.EqualTo(W24S5DescriptorStructureReplayResult.EvaluatorRuntimePendingStatus));
            Assert.That(pending.EvaluatorStatus, Is.EqualTo(W24S5DescriptorStructureReplayResult.EvaluatorProvenancePending));
            CollectionAssert.AreEqual(before, WholeInScopeTreeSnapshot());
            Assert.That(Directory.Exists(RepositoryAbsolute(missingRoot)), Is.False);
        }

        [Test]
        public void PublicSurface_IsHonestReadOnlyStructureReplay_WithoutVerdictOrAuthority()
        {
            var requestFields = typeof(W24S5DescriptorStructureReplayRequest).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                "CandidateReceiptFileHash", "CandidateReceiptPath", "EvidenceRevision",
                "EvidenceRevisionDescriptorFileHash", "EvidenceRevisionDescriptorPath"
            }, requestFields);
            var resultNames = typeof(W24S5DescriptorStructureReplayResult)
                .GetMembers(BindingFlags.Instance | BindingFlags.NonPublic)
                .Select(item => item.Name).ToArray();
            foreach (var forbidden in new[] { "Verdict", "Route", "Report", "Receipt", "Terminal", "Authority", "Transition", "Advance" })
                Assert.That(resultNames.Any(name => name.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase) >= 0), Is.False, forbidden);
            var methods = typeof(W24S5MachineFailureProducer).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(item => !item.IsPrivate).Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(new[] { "ConfigureRegistryForTests", "ReplayDescriptorStructure", "ResetTestHooks" }, methods);
        }

        [Test]
        public void SharedLegacyRawReplay_IsSingleImplementationAndOpaquePrivateIssuerProjection()
        {
            Assert.That(W24S5EvidenceRevisionWriter.SharedLegacyRawReplayVersion,
                Is.EqualTo("w24-s5-shared-legacy-raw-replay/1"));
            Assert.That(W24S5MachineFailureProducer.ProducerVersion,
                Is.EqualTo("w24-s5-descriptor-structure-replay-scaffold/2"));
            var authorityType = typeof(W24S5EvidenceRevisionWriter.LegacyRawReplayAuthority);
            var constructor = authorityType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(object), typeof(object) }, null);
            Assert.That(constructor, Is.Not.Null);
            var constructionError = Assert.Throws<TargetInvocationException>(() => constructor.Invoke(new object[] { new object(), new object() }));
            Assert.That(constructionError.InnerException, Is.TypeOf<InvalidOperationException>());

            var properties = authorityType.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            CollectionAssert.AreEquivalent(new[]
            {
                "Root", "CaptureMetadataPath", "CaptureMetadataFileHash", "EvidenceSealPath", "EvidenceSealFileHash",
                "EvidenceSealHash", "EvidenceLockPath", "EvidenceLockFileHash", "DiagnosticManifestPath",
                "DiagnosticManifestFileHash", "ArtifactCount", "TotalBytes", "FileSetTypedHash"
            }, properties.Select(item => item.Name));
            Assert.That(properties.All(item => !item.CanWrite), Is.True);
            var callableMethods = authorityType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(item => !item.IsPrivate && !item.IsSpecialName).ToArray();
            CollectionAssert.AreEquivalent(new[] { "RequireSemanticRecordHash", "RequireSupplementalRecordHash" },
                callableMethods.Select(item => item.Name));
            var exposedTypes = properties.Select(item => item.PropertyType)
                .Concat(callableMethods.Select(item => item.ReturnType))
                .Concat(callableMethods.SelectMany(item => item.GetParameters().Select(parameter => parameter.ParameterType)));
            Assert.That(exposedTypes.Any(type => type == typeof(byte[]) || typeof(JToken).IsAssignableFrom(type)), Is.False,
                "Opaque raw replay must not expose mutable JSON tokens or raw byte buffers.");
            foreach (var forbidden in new[] { "Verdict", "Route", "Report", "Receipt", "Terminal", "Transition", "Advance" })
                Assert.That(properties.Concat<MemberInfo>(callableMethods)
                    .Any(item => item.Name.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase) >= 0), Is.False, forbidden);

            var machineSource = File.ReadAllText(RepositoryAbsolute("project/Packages/com.vfxcomposer.unity/Editor/W24/S5/W24S5MachineFailureProducer.cs"));
            Assert.That(machineSource, Does.Contain("W24S5EvidenceRevisionWriter.ReplayLegacyRawReadOnly"));
            foreach (var removedDuplicate in new[]
            {
                "VerifyRecorderMetadataProvenance", "VerifyRecorderCaptureProfile", "VerifyRecorderSourceHashes",
                "VerifyMetadataFrames", "VerifyMetadataSemantic", "VerifyMetadataSupplemental", "EnumerateRawTree"
            })
                Assert.That(machineSource, Does.Not.Contain(removedDuplicate), removedDuplicate);
            var writerSource = File.ReadAllText(RepositoryAbsolute("project/Packages/com.vfxcomposer.unity/Editor/W24/S5/W24S5EvidenceRevisionWriter.cs"));
            Assert.That(CountOccurrences(writerSource, "private static RawReplay ReplayLegacyRaw("), Is.EqualTo(1));
        }

        [Test]
        public void SourceContainsNoPublisherOrMachineVerdictSurface()
        {
            var source = File.ReadAllText(RepositoryAbsolute("project/Packages/com.vfxcomposer.unity/Editor/W24/S5/W24S5MachineFailureProducer.cs"));
            foreach (var forbidden in new[]
            {
                "EvaluateAndPublish", "MACHINE_PASS", "MACHINE_FAIL", "Directory.CreateDirectory", "Directory.Delete",
                "Directory.Move", "File.Append", "File.Copy", "File.Create", "File.Delete", "File.Move", "File.OpenWrite",
                "File.Replace", "File.Set", "File.Write", "FileMode.Create", "FileMode.OpenOrCreate", "new StreamWriter"
            })
                Assert.That(source.Contains(forbidden), Is.False, "Read-only scaffold contains forbidden publisher/verdict surface: " + forbidden);
        }

        [Test]
        public void RealPhaseADescriptor_ReplaysTwice_ReadOnly_AndLeavesEvaluatorPending()
        {
            var before = WholeInScopeTreeSnapshot();
            var result = W24S5MachineFailureProducer.ReplayDescriptorStructure(request);
            Assert.That(result.IsReadOnlyStructureReplay, Is.True, string.Join(" | ", result.Errors));
            Assert.That(result.Status, Is.EqualTo(W24S5DescriptorStructureReplayResult.TestOnlyDescriptorStructureReplayedStatus));
            Assert.That(result.EvaluatorStatus, Is.EqualTo(W24S5DescriptorStructureReplayResult.EvaluatorProvenancePending));
            Assert.That(result.EvidenceRevisionDescriptorPath, Is.EqualTo(DescriptorPath));
            Assert.That(result.EvidenceRevisionDescriptorFileHash, Is.EqualTo(HashFile(RepositoryAbsolute(DescriptorPath))));
            Assert.That(result.EvidenceRevisionDescriptorSelfHash, Is.EqualTo((string)ParseRepository(DescriptorPath)["selfHash"]));
            Assert.That(result.StructuralReplayFingerprint, Does.Match("^sha256:[0-9a-f]{64}$"));
            CollectionAssert.AreEqual(before, WholeInScopeTreeSnapshot());
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot + "/evidence/E1/terminal")), Is.False);
        }

        [Test]
        public void SelfConsistentDescriptorEvaluationPinSwap_IsRejectedByMetadataDag()
        {
            RewriteDescriptor(value =>
            {
                var input = (JObject)value["evaluationInput"];
                input["operatorCommandPath"] = RawRoot + "/diagnostics/semantic-telemetry.json";
                input["operatorCommandFileHash"] = (string)input["semanticTelemetryFileHash"];
            });
            var result = W24S5MachineFailureProducer.ReplayDescriptorStructure(CurrentRequest());
            Assert.That(result.Status, Is.EqualTo(W24S5DescriptorStructureReplayResult.InvalidStatus));
            Assert.That(result.Errors.Single(), Does.Contain("exact persisted identity"));
        }

        [Test]
        public void SelfConsistentRecorderCaptureToolProvenanceSwap_IsRejected()
        {
            RewriteRecorderCaptureToolProvenance();
            SynchronizeDescriptorRawPins();
            var result = W24S5MachineFailureProducer.ReplayDescriptorStructure(CurrentRequest());
            Assert.That(result.Status, Is.EqualTo(W24S5DescriptorStructureReplayResult.InvalidStatus));
            Assert.That(result.Errors.Single(), Does.Contain("descriptor capture-tool"));
        }

        [Test]
        public void SelfConsistentRecorderCaptureToolPathSwap_IsRejected()
        {
            var metadataPath = RepositoryAbsolute(RawRoot + "/capture-metadata.json");
            var sealPath = RepositoryAbsolute(RawRoot + "/evidence-seal.json");
            var metadata = JObject.Parse(File.ReadAllText(metadataPath, StrictUtf8));
            var oldSeal = JObject.Parse(File.ReadAllText(sealPath, StrictUtf8));
            ((JObject)metadata.SelectToken("sourceHashes.captureTool"))["path"] = RepositoryAbsolute(SchemaPath).Replace('\\', '/');
            File.WriteAllText(metadataPath, metadata.ToString(Formatting.None), StrictUtf8);
            File.Delete(sealPath);
            InvokeWriter<object>("WriteSeal",
                (JObject)metadata["sourceHashes"],
                (string)oldSeal.SelectToken("provenance.operatorCommandHash"),
                (string)oldSeal.SelectToken("provenance.captureToolSha256"),
                (string)metadata["captureProfileSha256"]);
            SynchronizeDescriptorRawPins();
            var result = W24S5MachineFailureProducer.ReplayDescriptorStructure(CurrentRequest());
            Assert.That(result.Status, Is.EqualTo(W24S5DescriptorStructureReplayResult.InvalidStatus));
            Assert.That(result.Errors.Single(), Does.Contain("capture-tool source differs"));
        }

        [Test]
        public void DescriptorAndRawEmptyDirectories_AreRejectedAsUndeclared()
        {
            Directory.CreateDirectory(RepositoryAbsolute(CandidateRoot + "/evidence/E1/snapshots/empty-undeclared"));
            var descriptorResult = W24S5MachineFailureProducer.ReplayDescriptorStructure(request);
            Assert.That(descriptorResult.Status, Is.EqualTo(W24S5DescriptorStructureReplayResult.InvalidStatus));
            Assert.That(descriptorResult.Errors.Single(), Does.Contain("directory"));

            Directory.Delete(RepositoryAbsolute(CandidateRoot + "/evidence/E1/snapshots/empty-undeclared"));
            Directory.CreateDirectory(RepositoryAbsolute(RawRoot + "/empty-undeclared"));
            var rawResult = W24S5MachineFailureProducer.ReplayDescriptorStructure(request);
            Assert.That(rawResult.Status, Is.EqualTo(W24S5DescriptorStructureReplayResult.InvalidStatus));
            Assert.That(rawResult.Errors.Single(), Does.Contain("directory"));
        }

        [Test]
        public void SnapshotByteTamper_AndSecondReplayRace_AreRejected()
        {
            var descriptor = ParseRepository(DescriptorPath);
            var snapshotPath = (string)descriptor.SelectToken("captureTool.sourceSnapshots[0].snapshotPath");
            File.AppendAllText(RepositoryAbsolute(snapshotPath), "tamper", StrictUtf8);
            Assert.That(W24S5MachineFailureProducer.ReplayDescriptorStructure(request).Status,
                Is.EqualTo(W24S5DescriptorStructureReplayResult.InvalidStatus));

            writerHarness.TearDown();
            writerHarness.SetUp();
            var written = W24S5EvidenceRevisionWriter.Write(InvokeWriter<W24S5EvidenceRevisionWriteRequest>("WriteRequest", 1));
            Assert.That(written.Succeeded, Is.True, string.Join(" | ", written.Errors));
            ConfigureStructureRegistry();
            request = CurrentRequest();
            W24S5MachineFailureProducer.BeforeSecondReplayForTests = ignored =>
                File.AppendAllText(RepositoryAbsolute((string)ParseRepository(DescriptorPath).SelectToken("writer.sourceSnapshots[0].snapshotPath")), "race", StrictUtf8);
            Assert.That(W24S5MachineFailureProducer.ReplayDescriptorStructure(request).Status,
                Is.EqualTo(W24S5DescriptorStructureReplayResult.InvalidStatus));
        }

        [Test]
        public void ExcludedLegacyBoundTree_MayChangeWithoutPollutingDescriptorStructureReplay()
        {
            var boundFile = RepositoryAbsolute(RawRoot + "/bound/test-only.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(boundFile));
            File.WriteAllText(boundFile, "before", StrictUtf8);
            W24S5MachineFailureProducer.BeforeSecondReplayForTests = ignored => File.AppendAllText(boundFile, "-after", StrictUtf8);
            var result = W24S5MachineFailureProducer.ReplayDescriptorStructure(request);
            Assert.That(result.Status, Is.EqualTo(W24S5DescriptorStructureReplayResult.TestOnlyDescriptorStructureReplayedStatus),
                string.Join(" | ", result.Errors));
            W24S5MachineFailureProducer.BeforeSecondReplayForTests = null;
            var after = W24S5MachineFailureProducer.ReplayDescriptorStructure(request);
            Assert.That(after.Status, Is.EqualTo(W24S5DescriptorStructureReplayResult.TestOnlyDescriptorStructureReplayedStatus),
                string.Join(" | ", after.Errors));
            Assert.That(after.StructuralReplayFingerprint, Is.EqualTo(result.StructuralReplayFingerprint));
            Assert.That(File.ReadAllText(boundFile, StrictUtf8), Is.EqualTo("before-after"));
        }

        [Test]
        public void ReparseBackedDescriptorPath_IsRejectedByBoundedReplay()
        {
            var descriptorAbsolute = Path.GetFullPath(RepositoryAbsolute(DescriptorPath));
            W24S5MachineFailureProducer.TreatPathAsReparsePointForTests =
                path => string.Equals(Path.GetFullPath(path), descriptorAbsolute, StringComparison.OrdinalIgnoreCase);
            var result = W24S5MachineFailureProducer.ReplayDescriptorStructure(request);
            Assert.That(result.Status, Is.EqualTo(W24S5DescriptorStructureReplayResult.InvalidStatus));
            Assert.That(result.Errors.Single(), Does.Contain("reparse"));
        }

        [Test]
        public void DescriptorPhysicalHashAndTypedSelfHash_AreVerifiedSeparately()
        {
            var descriptor = ParseRepository(DescriptorPath);
            ((JObject)descriptor["candidate"])["buildHash"] = ZeroHash;
            WriteRepository(DescriptorPath, descriptor);
            var physicalMismatch = W24S5MachineFailureProducer.ReplayDescriptorStructure(request);
            Assert.That(physicalMismatch.Errors.Single(), Does.Contain("physical bytes"));

            request = CurrentRequest();
            var selfMismatch = W24S5MachineFailureProducer.ReplayDescriptorStructure(request);
            Assert.That(selfMismatch.Errors.Single(), Does.Contain("typed self-hash"));
        }

        private static void ConfigureStructureRegistry()
        {
            W24S5MachineFailureProducer.ResetTestHooks();
            var descriptor = ParseRepository(DescriptorPath);
            W24S5MachineFailureProducer.ConfigureRegistryForTests(new W24S5DescriptorStructureTestRegistry
            {
                EffectId = EffectId,
                DescriptorSchemaId = W24S5MachineFailureProducer.LegacyS0bDescriptorSchema,
                DescriptorSchemaPath = SchemaPath,
                DescriptorSchemaFileHash = HashFile(RepositoryAbsolute(SchemaPath)),
                WriterBundlePath = WriterBundlePath,
                WriterBundleFileHash = HashFile(RepositoryAbsolute(WriterBundlePath)),
                WriterBundleTypedHash = (string)descriptor.SelectToken("writer.bundleTypedHash"),
                CaptureToolBundlePath = CaptureToolBundlePath,
                CaptureToolBundleFileHash = HashFile(RepositoryAbsolute(CaptureToolBundlePath)),
                CaptureToolBundleCanonicalHash = (string)descriptor.SelectToken("captureTool.bundleCanonicalHash")
            });
        }

        private static W24S5DescriptorStructureReplayRequest CurrentRequest()
        {
            return new W24S5DescriptorStructureReplayRequest
            {
                CandidateReceiptPath = CandidateReceipt,
                CandidateReceiptFileHash = HashFile(RepositoryAbsolute(CandidateReceipt)),
                EvidenceRevision = 1,
                EvidenceRevisionDescriptorPath = DescriptorPath,
                EvidenceRevisionDescriptorFileHash = HashFile(RepositoryAbsolute(DescriptorPath))
            };
        }

        private static void RewriteDescriptor(Action<JObject> mutation)
        {
            var descriptor = ParseRepository(DescriptorPath);
            mutation(descriptor);
            descriptor.Remove("selfHash");
            descriptor["selfHash"] = W24TypedBinaryCanonicalEncoding.Hash(descriptor);
            WriteRepository(DescriptorPath, descriptor);
        }

        private static void RewriteRecorderCaptureToolProvenance()
        {
            var metadataPath = RepositoryAbsolute(RawRoot + "/capture-metadata.json");
            var sealPath = RepositoryAbsolute(RawRoot + "/evidence-seal.json");
            var metadata = JObject.Parse(File.ReadAllText(metadataPath, StrictUtf8));
            var oldSeal = JObject.Parse(File.ReadAllText(sealPath, StrictUtf8));
            var replacement = HashBytes(StrictUtf8.GetBytes("different-test-only-capture-tool"));
            ((JObject)metadata.SelectToken("sourceHashes.captureTool"))["sha256"] = replacement;
            File.WriteAllText(metadataPath, metadata.ToString(Formatting.None), StrictUtf8);
            File.Delete(sealPath);
            InvokeWriter<object>("WriteSeal",
                (JObject)metadata["sourceHashes"],
                (string)oldSeal.SelectToken("provenance.operatorCommandHash"),
                replacement,
                (string)metadata["captureProfileSha256"]);
        }

        private static void SynchronizeDescriptorRawPins()
        {
            RewriteDescriptor(descriptor =>
            {
                var raw = (JObject)descriptor["rawCapture"];
                raw["captureMetadataFileHash"] = HashFile(RepositoryAbsolute(RawRoot + "/capture-metadata.json"));
                raw["evidenceSealFileHash"] = HashFile(RepositoryAbsolute(RawRoot + "/evidence-seal.json"));
                raw["evidenceSealHash"] = (string)ParseRepository(RawRoot + "/evidence-seal.json")["sealHash"];
                raw["evidenceLockFileHash"] = HashFile(RepositoryAbsolute(RawRoot + "/evidence-lock.json"));
                raw["diagnosticPassManifestFileHash"] = HashFile(RepositoryAbsolute(RawRoot + "/diagnostic-pass-manifest.json"));
                var files = Directory.GetFiles(RepositoryAbsolute(RawRoot), "*", SearchOption.AllDirectories)
                    .Where(path => path.IndexOf(Path.DirectorySeparatorChar + "bound" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) < 0)
                    .ToArray();
                raw["artifactCount"] = files.Length;
                raw["totalBytes"] = files.Aggregate<string, long>(0, (sum, path) => checked(sum + new FileInfo(path).Length));
                raw["fileSetTypedHash"] = InvokeWriter<string>("ComputeRawFileSetTypedHash");
            });
        }

        private static T InvokeWriter<T>(string name, params object[] arguments)
        {
            var method = typeof(W24S5EvidenceRevisionWriterTests).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Missing Phase-A fixture helper: " + name);
            var value = method.Invoke(null, arguments);
            return value == null ? default(T) : (T)value;
        }

        private static JObject ParseRepository(string relative)
        {
            return JObject.Parse(File.ReadAllText(RepositoryAbsolute(relative), StrictUtf8));
        }

        private static void WriteRepository(string relative, JObject value)
        {
            File.WriteAllText(RepositoryAbsolute(relative), value.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n", StrictUtf8);
        }

        private static string[] WholeInScopeTreeSnapshot()
        {
            var output = new List<string>();
            foreach (var relativeRoot in new[] { "docs/vfx-candidates", "artifacts/vfx-evidence" })
            {
                var root = RepositoryAbsolute(relativeRoot);
                if (!Directory.Exists(root)) { output.Add("MISSING " + relativeRoot); continue; }
                foreach (var directory in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
                    output.Add("D " + Relative(root, directory));
                foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                    output.Add("F " + Relative(root, file) + " " + HashFile(file));
            }
            return output.OrderBy(item => item, StringComparer.Ordinal).ToArray();
        }

        private static string Relative(string root, string path)
        {
            var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).Substring(prefix.Length).Replace('\\', '/');
        }

        private static string RepositoryRoot
        {
            get { return Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName; }
        }

        private static string RepositoryAbsolute(string relative)
        {
            return Path.GetFullPath(Path.Combine(RepositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return "sha256:" + string.Concat(sha.ComputeHash(stream).Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static string HashBytes(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return "sha256:" + string.Concat(sha.ComputeHash(bytes).Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static int CountOccurrences(string value, string needle)
        {
            var count = 0;
            var offset = 0;
            while ((offset = value.IndexOf(needle, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += needle.Length;
            }
            return count;
        }

        private static string ZeroHash { get { return "sha256:" + new string('0', 64); } }
    }
}
