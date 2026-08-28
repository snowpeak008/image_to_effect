using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using VFXComposer.Editor.Validation;
using VFXComposer.Editor.W24.S1;
using VFXComposer.Editor.W24.S5;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W24S5EvidenceRevisionWriterTests
    {
        private const string EffectId = "w24_evidence_writer_probe";
        private const int ContractRevision = 17;
        private const string CandidateRoot = "docs/vfx-candidates/" + EffectId + "/C0";
        private const string CandidateReceipt = CandidateRoot + "/candidate-receipt.json";
        private const string BootstrapContract = "docs/vfx-contracts/" + EffectId + ".contract.json";
        private const string BootstrapTrace = "docs/vfx-traces/" + EffectId + ".implementation-trace.json";
        private const string AssetRoot = "Assets/VFX/Generated/" + EffectId;
        private const string RuntimeAsset = AssetRoot + "/VFX_" + EffectId + ".prefab";
        private const string PreviewAsset = "Assets/VFX/Preview/W24_EvidenceWriterProbe.unity";
        private const string ProductionManifest = "ProjectSettings/VFXComposer/BuildManifests/" + EffectId + ".manifest.json";
        private const string RawRoot = "artifacts/vfx-evidence/" + EffectId + "/C0";
        private const string InputRoot = "tools/vfx/tests/w24_evidence_writer_probe_inputs";
        private const string WriterSource = InputRoot + "/writer.source.cs";
        private const string WriterBundle = InputRoot + "/writer.bundle.json";
        private const string CaptureSource = InputRoot + "/capture.source.cs";
        private const string CaptureBundle = InputRoot + "/capture.bundle.json";
        private const string MetricsTool = InputRoot + "/render_metrics.py";
        private const string S0bSchemaPath = "docs/schemas/w24-s5-evidence-revision-legacy-c0-s0b-v1.schema.json";
        private const string S3SchemaPath = "docs/schemas/w24-s5-evidence-revision-legacy-c0-s3-v1.schema.json";
        private const string DescriptorPath = CandidateRoot + "/evidence/E1/evidence-revision.json";
        private const string CaptureToolVersion = "w24-test-capture/1";
        private const double LegacyMultiviewMinDepthSpan = 0.0d;
        private const string WriterId = "W24S5EvidenceRevisionWriter.TEST_ONLY";
        private const string WriterVersion = "w24-s5-evidence-revision-writer/1";
        private static bool ownsFixturePaths;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var conflicts = OwnedRoots().Where(path => Directory.Exists(path) || File.Exists(path)).ToArray();
            if (conflicts.Length != 0) Assert.Fail("Refusing to overwrite pre-existing descriptor-writer probe paths: " + string.Join(" | ", conflicts));
            ownsFixturePaths = true;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            W24S5EvidenceRevisionWriter.ResetTestHooks();
            if (ownsFixturePaths) Cleanup();
            ownsFixturePaths = false;
        }

        [SetUp]
        public void SetUp()
        {
            if (!ownsFixturePaths) Assert.Fail("Descriptor-writer fixture does not own its scratch namespace.");
            W24S5EvidenceRevisionWriter.ResetTestHooks();
            Cleanup();
            Assert.That(File.Exists(W24S5EvidenceRevisionWriter.RepositoryLockPathForTests), Is.False,
                "The global writer lock belongs to another operation and must never be deleted by this fixture.");
            CreateS0bFixture();
            ConfigureS0bRegistry();
        }

        [TearDown]
        public void TearDown()
        {
            W24S5EvidenceRevisionWriter.ResetTestHooks();
            Cleanup();
        }

        [Test]
        public void CandidateOnlyReplay_IsOpaqueAndDoesNotChangeExistingReadBehavior()
        {
            var request = ReaderRequest(1);
            var candidateOnly = W24S5CandidateEvidenceReader.ReplayCandidateOnly(request);
            Assert.That(candidateOnly.Status, Is.EqualTo(W24S5CandidateOnlyReplayResult.ValidCandidateReadOnlyStatus));
            Assert.That(candidateOnly.Authority, Is.Not.Null);
            Assert.That(candidateOnly.Authority.EffectId, Is.EqualTo(EffectId));

            var ordinary = W24S5CandidateEvidenceReader.Read(request);
            Assert.That(ordinary.Status, Is.EqualTo(W24S5CandidateEvidenceReadResult.InvalidStatus));
            Assert.That(ordinary.Snapshot, Is.Not.Null);
            Assert.That(ordinary.Errors.Single(), Does.Contain("no immutable E1 revision descriptor"));
        }

        [Test]
        public void CandidateReplayAuthority_RejectsCallerConstruction()
        {
            Assert.Throws<InvalidOperationException>(() =>
                new W24S5CandidateEvidenceReader.CandidateReplayAuthority(new object(), new W24S5CandidateEvidenceSnapshot()));
        }

        [Test]
        public void S0bE1_WritesExactAtomicTreeAndTypedSelfSeal_WithoutMutatingInputs()
        {
            var candidateBefore = TreeSnapshot(RepositoryAbsolute(CandidateRoot));
            var rawBefore = TreeSnapshot(RepositoryAbsolute(RawRoot));
            var result = W24S5EvidenceRevisionWriter.Write(WriteRequest(1));
            Assert.That(result.Succeeded, Is.True, string.Join(" | ", result.Errors));
            Assert.That(result.Status, Is.EqualTo(W24S5EvidenceRevisionWriteResult.TestOnlyDescriptorWriterStatus));
            Assert.That(result.DescriptorPath, Is.EqualTo(DescriptorPath));
            Assert.That(result.DescriptorFileHash, Is.EqualTo(HashFile(RepositoryAbsolute(DescriptorPath))));
            CollectionAssert.AreEqual(candidateBefore, TreeSnapshot(RepositoryAbsolute(CandidateRoot), "evidence/E1"));
            CollectionAssert.AreEqual(rawBefore, TreeSnapshot(RepositoryAbsolute(RawRoot)));

            var descriptor = ParseRepository(DescriptorPath);
            CollectionAssert.AreEquivalent(new[]
            {
                "schema", "descriptorStatus", "writer", "effectId", "candidateId", "candidateRevision", "contractRevision",
                "evidenceRevision", "candidate", "rawCapture", "captureTool", "evaluationInput", "predecessor", "selfHashEncoding", "selfHash"
            }, descriptor.Properties().Select(value => value.Name));
            var clone = (JObject)descriptor.DeepClone(); var selfHash = (string)clone["selfHash"]; clone.Remove("selfHash");
            Assert.That(W24TypedBinaryCanonicalEncoding.Verify(selfHash, clone), Is.True);
            Assert.That((string)descriptor["descriptorStatus"], Is.EqualTo("RAW_CAPTURE_SEALED"));
            Assert.That((string)descriptor.SelectToken("evaluationInput.replayPolicyVersion"), Is.EqualTo("w24-s0b-descriptor-only/1"));
            Assert.That(descriptor.ToString(Formatting.None), Does.Not.Contain("PASS").And.Not.Contain("FAIL"));

            var expected = new[]
            {
                "evidence-revision.json",
                "snapshots/capture-tool/capture-tool.bundle.json",
                "snapshots/capture-tool/sources/0000.source",
                "snapshots/schema/w24-s5-evidence-revision-legacy-c0-s0b-v1.schema.json",
                "snapshots/writer/sources/0000.source",
                "snapshots/writer/writer.bundle.json"
            };
            CollectionAssert.AreEqual(expected, RelativeFiles(RepositoryAbsolute(CandidateRoot + "/evidence/E1")));
            Assert.That((string)descriptor.SelectToken("rawCapture.fileSetTypedHash"), Is.EqualTo(ComputeRawFileSetTypedHash()));
            AssertCleanAtomicScaffolding();
        }

        [Test]
        public void S3E1_WritesFrozenEvaluationInputsAndExactS3SchemaRoute()
        {
            UpgradeFixtureToS3();
            ConfigureS3Registry();
            var result = W24S5EvidenceRevisionWriter.Write(WriteRequest(1));
            Assert.That(result.Succeeded, Is.True, string.Join(" | ", result.Errors));
            var descriptor = ParseRepository(DescriptorPath);
            Assert.That((string)descriptor["schema"], Is.EqualTo("w24-s5-evidence-revision-legacy-c0-s3/1"));
            Assert.That((string)descriptor["descriptorStatus"], Is.EqualTo("RAW_CAPTURE_SEALED"));
            Assert.That(descriptor.ToString(Formatting.None), Does.Not.Contain("PASS").And.Not.Contain("FAIL"),
                "An S3 descriptor structurally archives historical inputs and never grants a verdict.");
            Assert.That((string)descriptor.SelectToken("evaluationInput.schema"), Is.EqualTo("w24-s5-eval-input-s3-render-metrics/1"));
            Assert.That((string)descriptor.SelectToken("evaluationInput.metricsToolSnapshotPath"),
                Is.EqualTo(CandidateRoot + "/evidence/E1/snapshots/evaluation/render_metrics.py"));
            Assert.That((string)descriptor.SelectToken("evaluationInput.metricsEnvironmentPath"),
                Is.EqualTo(CandidateRoot + "/evidence/E1/snapshots/evaluation/metrics-environment.json"));
            Assert.That(HashFile(RepositoryAbsolute(CandidateRoot + "/evidence/E1/snapshots/evaluation/render_metrics.py")),
                Is.EqualTo(HashFile(RepositoryAbsolute(MetricsTool))));
            Assert.That(HashFile(RepositoryAbsolute(CandidateRoot + "/evidence/E1/snapshots/evaluation/metrics-environment.json")),
                Is.EqualTo((string)descriptor.SelectToken("evaluationInput.metricsEnvironmentFileHash")));
            CollectionAssert.Contains(RelativeFiles(RepositoryAbsolute(CandidateRoot + "/evidence/E1")),
                "snapshots/schema/w24-s5-evidence-revision-legacy-c0-s3-v1.schema.json");
            AssertCleanAtomicScaffolding();
        }

        [Test]
        public void S3RouteRejectsS0bOnlyRawCaptureWithoutPublication()
        {
            UpgradeFixtureToS3();
            StripTypedRawCaptureToS0bShape();
            ConfigureS3Registry();
            var result = W24S5EvidenceRevisionWriter.Write(WriteRequest(1));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Single(), Does.Contain("requires typed raw diagnostics"));
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot + "/evidence/E1")), Is.False);
            AssertCleanAtomicScaffolding();
        }

        [Test]
        public void S3RouteRejectsSyntheticMetricKindEvenWhenSealed()
        {
            UpgradeFixtureToS3(true);
            ConfigureS3Registry();
            var result = W24S5EvidenceRevisionWriter.Write(WriteRequest(1));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Single(), Does.Contain("unknown or duplicate metricPlan kind"));
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot + "/evidence/E1")), Is.False);
            AssertCleanAtomicScaffolding();
        }

        [Test]
        public void S0bRouteRejectsSealedS3TypedRecordsWithoutPublication()
        {
            UpgradeFixtureToS3();
            ConfigureS0bRegistry();
            var result = W24S5EvidenceRevisionWriter.Write(WriteRequest(1));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(W24S5EvidenceRevisionWriteResult.InvalidStatus));
            Assert.That(result.Errors.Single(), Does.Contain("S0b descriptor route rejects a Contract declaring typedDiagnostics or captureToolBundle authority"));
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot + "/evidence/E1")), Is.False);
            AssertCleanAtomicScaffolding();
        }

        [TestCase("typedDiagnostics")]
        [TestCase("captureToolBundle")]
        public void S0bRouteRejectsContractOnlyTypedAuthorityDeclarations(string declaration)
        {
            AddForbiddenS0bContractDeclaration(declaration);
            var result = W24S5EvidenceRevisionWriter.Write(WriteRequest(1));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(W24S5EvidenceRevisionWriteResult.InvalidStatus));
            Assert.That(result.Errors.Single(), Does.Contain("S0b descriptor route rejects a Contract declaring typedDiagnostics or captureToolBundle authority"));
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot + "/evidence/E1")), Is.False);
            AssertCleanAtomicScaffolding();
        }

        [TestCase("trail")]
        [TestCase("fragment_tracks")]
        [TestCase("multiview_3d")]
        [TestCase("receiver_luminance_ldr")]
        public void S3MetricCheckRejectsContractFrozenValueSwap(string kind)
        {
            var probe = MetricProjectionProbe(kind);
            Assert.DoesNotThrow(() => W24S5EvidenceRevisionWriter.VerifyMetricCheckContractProjectionForTests(probe.Check, probe.Block, probe.Evidence, LegacyMultiviewMinDepthSpan));
            MutateMetricProjectionValue(probe);
            Assert.Throws<InvalidDataException>(() => W24S5EvidenceRevisionWriter.VerifyMetricCheckContractProjectionForTests(probe.Check, probe.Block, probe.Evidence, LegacyMultiviewMinDepthSpan));
        }

        [Test]
        public void MultiviewObjectIdsAndDepthSlotSwap_IsRejected()
        {
            var probe = MetricProjectionProbe("multiview_3d");
            var view = (JObject)((JArray)probe.Check["views"])[0];
            var objectIds = view["objectIds"];
            view["objectIds"] = view["depth"];
            view["depth"] = objectIds;
            var error = Assert.Throws<InvalidDataException>(() => W24S5EvidenceRevisionWriter.VerifyMetricCheckContractProjectionForTests(probe.Check, probe.Block, probe.Evidence, LegacyMultiviewMinDepthSpan));
            Assert.That(error.Message, Does.Contain("exact passId/encoding/semantic slot/provenance role"));
        }

        [TestCase("on_off")]
        [TestCase("ids_mask")]
        public void ReceiverSemanticSlotSwap_IsRejected(string swap)
        {
            var probe = MetricProjectionProbe("receiver_luminance_ldr");
            var first = swap == "on_off" ? "on" : "receiverIds";
            var second = swap == "on_off" ? "off" : "effectMask";
            var value = probe.Check[first];
            probe.Check[first] = probe.Check[second];
            probe.Check[second] = value;
            var error = Assert.Throws<InvalidDataException>(() => W24S5EvidenceRevisionWriter.VerifyMetricCheckContractProjectionForTests(probe.Check, probe.Block, probe.Evidence, LegacyMultiviewMinDepthSpan));
            Assert.That(error.Message, Does.Contain("exact passId/encoding/semantic slot/provenance role"));
        }

        [Test]
        public void LegacyMultiviewMinDepthSpan_ArchivesCapturePolicyDespiteContractSemanticMismatchWithoutVerdict()
        {
            var probe = MetricProjectionProbe("multiview_3d");
            Assert.That((double)probe.Check["minDepthSpan"], Is.EqualTo(LegacyMultiviewMinDepthSpan));
            Assert.That((double)probe.Block.SelectToken("thresholds.minimumLinearDepth"), Is.EqualTo(0.0001d));
            Assert.DoesNotThrow(() => W24S5EvidenceRevisionWriter.VerifyMetricCheckContractProjectionForTests(
                probe.Check, probe.Block, probe.Evidence, LegacyMultiviewMinDepthSpan));
            Assert.That(probe.Check["pass"], Is.Null);
            Assert.That(probe.Check["verdict"], Is.Null);
        }

        [Test]
        public void LegacyMultiviewMinDepthSpanRegistryPolicySwap_IsRejected()
        {
            var probe = MetricProjectionProbe("multiview_3d");
            var error = Assert.Throws<InvalidDataException>(() => W24S5EvidenceRevisionWriter.VerifyMetricCheckContractProjectionForTests(
                probe.Check, probe.Block, probe.Evidence, 0.0001d));
            Assert.That(error.Message, Does.Contain("gate-owned capture-tool policy"));
        }

        [TestCase("MEASURED")]
        [TestCase("EVIDENCE_INVALID")]
        public void MetricsReportRejectsNonObjectCheckTokensAfterValidSelfSeal(string route)
        {
            var input = new JObject { ["checks"] = new JArray(new JObject { ["id"] = "trail-1", ["kind"] = "trail" }) };
            var inputHash = HashCanonical(input);
            var toolHash = HashText("frozen metrics tool");
            var report = ReportProbe(route, inputHash, toolHash,
                new JArray(new JObject { ["id"] = "trail-1", ["kind"] = "trail", ["pass"] = true }, new JValue("not-an-object")));
            Assert.Throws<InvalidDataException>(() => W24S5EvidenceRevisionWriter.VerifyMetricsReportForTests(report, input, inputHash, toolHash));
        }

        [Test]
        public void EvidenceInvalidReportRequiresExactlyZeroCheckTokens()
        {
            var input = new JObject { ["checks"] = new JArray(new JObject { ["id"] = "trail-1", ["kind"] = "trail" }) };
            var inputHash = HashCanonical(input);
            var toolHash = HashText("frozen metrics tool");
            var report = ReportProbe("EVIDENCE_INVALID", inputHash, toolHash,
                new JArray(new JObject { ["id"] = "trail-1", ["kind"] = "trail", ["pass"] = false }));
            var error = Assert.Throws<InvalidDataException>(() => W24S5EvidenceRevisionWriter.VerifyMetricsReportForTests(report, input, inputHash, toolHash));
            Assert.That(error.Message, Does.Contain("check-free"));
        }

        [Test]
        public void CompiledRegistryDescriptorTokenBoundaryMatchesSchemas()
        {
            Assert.DoesNotThrow(() => W24S5EvidenceRevisionWriter.ConfigureRegistryForTests(TestRegistry(new string('A', 96))));
            Assert.Throws<InvalidDataException>(() => W24S5EvidenceRevisionWriter.ConfigureRegistryForTests(TestRegistry(new string('A', 97))));
        }

        [Test]
        public void ProductionRegistry_IsExplicitlyPendingAndWritesNothing()
        {
            W24S5EvidenceRevisionWriter.ResetTestHooks();
            var result = W24S5EvidenceRevisionWriter.Write(WriteRequest(1));
            Assert.That(result.Status, Is.EqualTo(W24S5EvidenceRevisionWriteResult.RegistryPendingStatus));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Single(), Does.Contain("REGISTRY_PENDING"));
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot + "/evidence/E1")), Is.False);
            AssertCleanAtomicScaffolding();
        }

        [TestCase("tamper")]
        [TestCase("extra")]
        [TestCase("missing")]
        [TestCase("oversize")]
        public void RawTreeTamperExtraMissingAndOversize_AreInvalidAndPublishNothing(string mutation)
        {
            if (mutation == "tamper") File.AppendAllText(RepositoryAbsolute(RawRoot + "/frames/seed_1/frame_00000_beauty.png"), "tamper", StrictUtf8);
            else if (mutation == "extra") WriteRepository(RawRoot + "/unsealed-extra.bin", new byte[] { 1 });
            else if (mutation == "missing") File.Delete(RepositoryAbsolute(RawRoot + "/frames/seed_1/frame_00000_beauty.png"));
            else
            {
                var path = RepositoryAbsolute(RawRoot + "/oversize.bin");
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) stream.SetLength(16L * 1024L * 1024L + 1L);
            }
            var result = W24S5EvidenceRevisionWriter.Write(WriteRequest(1));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(W24S5EvidenceRevisionWriteResult.InvalidStatus));
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot + "/evidence/E1")), Is.False);
            AssertCleanAtomicScaffolding();
        }

        [Test]
        public void CandidateSwapBeforePublish_IsDetectedBySecondOpaqueReplay()
        {
            W24S5EvidenceRevisionWriter.BeforeSecondReplayForTests = _ => File.AppendAllText(RepositoryAbsolute(CandidateReceipt), " ", StrictUtf8);
            var result = W24S5EvidenceRevisionWriter.Write(WriteRequest(1));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Single(), Does.Contain("Candidate bytes changed"));
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot + "/evidence/E1")), Is.False);
            AssertCleanAtomicScaffolding();
        }

        [Test]
        public void CaptureSourceSwapBeforePublish_IsDetectedBySecondInputReplay()
        {
            W24S5EvidenceRevisionWriter.BeforeSecondReplayForTests = _ => File.AppendAllText(RepositoryAbsolute(CaptureSource), "// swapped\n", StrictUtf8);
            var result = W24S5EvidenceRevisionWriter.Write(WriteRequest(1));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Single(), Does.Contain("capture-tool bundle source"));
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot + "/evidence/E1")), Is.False);
            AssertCleanAtomicScaffolding();
        }

        [Test]
        public void S0bSealOperatorCommandHashSwap_IsRejected()
        {
            var metadata = ParseRepository(RawRoot + "/capture-metadata.json");
            var telemetryHash = (string)((JArray)metadata["semanticTelemetry"])[0]["sha256"];
            var oldSeal = ParseRepository(RawRoot + "/evidence-seal.json");
            File.Delete(RepositoryAbsolute(RawRoot + "/evidence-seal.json"));
            WriteSeal((JObject)metadata["sourceHashes"], telemetryHash, (string)oldSeal.SelectToken("provenance.captureToolSha256"), (string)metadata["captureProfileSha256"]);

            var result = W24S5EvidenceRevisionWriter.Write(WriteRequest(1));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Single(), Does.Contain("operatorCommandHash"));
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot + "/evidence/E1")), Is.False);
            AssertCleanAtomicScaffolding();
        }

        [Test]
        public void LegacyBoundDirectory_IsExcludedWithoutWeakeningExactRawFileSet()
        {
            WriteRepository(RawRoot + "/bound/legacy-observation.bin", new byte[] { 4, 2 });
            var result = W24S5EvidenceRevisionWriter.Write(WriteRequest(1));
            Assert.That(result.Succeeded, Is.True, string.Join(" | ", result.Errors));
            Assert.That(File.Exists(RepositoryAbsolute(RawRoot + "/bound/legacy-observation.bin")), Is.True);
            Assert.That((string)ParseRepository(DescriptorPath).SelectToken("rawCapture.fileSetTypedHash"), Is.EqualTo(ComputeRawFileSetTypedHash()));
            AssertCleanAtomicScaffolding();
        }

        [Test]
        public void VirtualReparseAtRawRoot_IsRejectedWithoutPublication()
        {
            var raw = Path.GetFullPath(RepositoryAbsolute(RawRoot));
            W24S5EvidenceRevisionWriter.TreatPathAsReparsePointForTests = path => string.Equals(Path.GetFullPath(path), raw, StringComparison.OrdinalIgnoreCase);
            var result = W24S5EvidenceRevisionWriter.Write(WriteRequest(1));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Single(), Does.Contain("reparse"));
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot + "/evidence/E1")), Is.False);
            AssertCleanAtomicScaffolding();
        }

        [Test]
        public void LockContentionAndExistingE1_AreWriteOnceFailures()
        {
            var lockPath = W24S5EvidenceRevisionWriter.RepositoryLockPathForTests;
            using (var held = new FileStream(lockPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                var blocked = W24S5EvidenceRevisionWriter.Write(WriteRequest(1));
                Assert.That(blocked.Succeeded, Is.False);
                Assert.That(blocked.Status, Is.EqualTo(W24S5EvidenceRevisionWriteResult.InvalidStatus));
                Assert.That(File.Exists(lockPath), Is.True, "A contender must not delete the lock it did not acquire.");
            }
            File.Delete(lockPath);

            Directory.CreateDirectory(RepositoryAbsolute(CandidateRoot + "/evidence/E1"));
            var existing = W24S5EvidenceRevisionWriter.Write(WriteRequest(1));
            Assert.That(existing.Succeeded, Is.False);
            Assert.That(existing.Errors.Single(), Does.Contain("write-once"));
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot + "/evidence/E1")), Is.True);
            Assert.That(File.Exists(lockPath), Is.False);
        }

        [Test]
        public void LockCommitFailure_DisposesHandleOwnedDeleteOnCloseLock()
        {
            W24S5EvidenceRevisionWriter.AfterLockCreateForTests = _ => throw new IOException("injected lock commit failure");
            var result = W24S5EvidenceRevisionWriter.Write(WriteRequest(1));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Single(), Does.Contain("injected lock commit failure"));
            Assert.That(File.Exists(W24S5EvidenceRevisionWriter.RepositoryLockPathForTests), Is.False);
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot + "/evidence/E1")), Is.False);
        }

        [Test]
        public void AggregatePreparedAndSourceBudget_FailsClosedBeforePublication()
        {
            W24S5EvidenceRevisionWriter.AggregateBudgetLimitForTests = 8;
            var result = W24S5EvidenceRevisionWriter.Write(WriteRequest(1));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Single(), Does.Contain("aggregate"));
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot + "/evidence/E1")), Is.False);
            AssertCleanAtomicScaffolding();
        }

        [Test]
        public void BroadenedPinnedLegacySchema_IsRejectedByCompiledE1Semantics()
        {
            var schema = ParseRepository(S0bSchemaPath);
            ((JObject)schema.SelectToken("properties.evidenceRevision")).RemoveAll();
            ((JObject)schema.SelectToken("properties.evidenceRevision"))["enum"] = new JArray(1, 2);
            var path = InputRoot + "/broadened-s0b.schema.json";
            WriteRepository(path, Serialize(schema));
            ConfigureS0bRegistry(path);

            var result = W24S5EvidenceRevisionWriter.Write(WriteRequest(1));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Single(), Does.Contain("E1-only schema"));
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot + "/evidence/E1")), Is.False);
            AssertCleanAtomicScaffolding();
        }

        [Test]
        public void PostMoveVerificationFailure_QuarantinesAndRemovesFormalE1()
        {
            W24S5EvidenceRevisionWriter.AfterPublishMoveForTests = target =>
                File.AppendAllText(Path.Combine(target, "evidence-revision.json"), " ", StrictUtf8);
            var result = W24S5EvidenceRevisionWriter.Write(WriteRequest(1));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot + "/evidence/E1")), Is.False,
                "A post-Move readback failure must not leave a formal E1 namespace.");
            AssertCleanAtomicScaffolding();
        }

        [Test]
        public void PostMoveVerificationAndQuarantineFailure_ThrowsCompositeFatalInsteadOfReturningInvalid()
        {
            W24S5EvidenceRevisionWriter.AfterPublishMoveForTests = _ => throw new IOException("injected post-Move verification failure");
            W24S5EvidenceRevisionWriter.BeforeQuarantineMoveForTests = _ => throw new IOException("injected quarantine move failure");
            var error = Assert.Throws<AggregateException>(() => W24S5EvidenceRevisionWriter.Write(WriteRequest(1)));
            Assert.That(error.Message, Does.Contain(W24S5EvidenceRevisionWriteResult.PublicationRollbackFatalStatus));
            var messages = error.Flatten().InnerExceptions.Select(item => item.Message).ToArray();
            Assert.That(messages, Has.Some.Contains("injected post-Move verification failure"));
            Assert.That(messages, Has.Some.Contains("injected quarantine move failure"));
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot + "/evidence/E1")), Is.True,
                "A failed quarantine is a fatal escaped state, never an ordinary INVALID result or claimed rollback success.");
            Assert.That(File.Exists(W24S5EvidenceRevisionWriter.RepositoryLockPathForTests), Is.False);
        }

        [Test]
        public void E2AndRevisionedC1C2Namespaces_AreRejectedBeforeAnyWrite()
        {
            var e2 = W24S5EvidenceRevisionWriter.Write(WriteRequest(2));
            Assert.That(e2.Succeeded, Is.False);
            Assert.That(e2.Errors.Single(), Does.Contain("rejects E2"));
            foreach (var revision in new[] { 1, 2 })
            {
                var result = W24S5EvidenceRevisionWriter.Write(new W24S5EvidenceRevisionWriteRequest
                {
                    CandidateReceiptPath = "docs/vfx-candidates/" + EffectId + "/R17/C" + revision + "/candidate-receipt.json",
                    CandidateReceiptFileHash = "sha256:" + new string('0', 64),
                    EvidenceRevision = 1
                });
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Errors.Single(), Does.Contain("rejects C1/C2"));
            }
            AssertCleanAtomicScaffolding();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MalformedAndBigIntegerPlayerLoopSerial_ReturnInvalidInsteadOfEscaping(bool bigInteger)
        {
            ResealMetadata(metadata =>
            {
                ((JObject)metadata["formalPlayerLoop"])["observedSerial"] = bigInteger
                    ? new JValue(new BigInteger(long.MaxValue) + BigInteger.One)
                    : new JValue("not-an-integer");
            });
            W24S5EvidenceRevisionWriteResult result = null;
            Assert.DoesNotThrow(() => result = W24S5EvidenceRevisionWriter.Write(WriteRequest(1)));
            Assert.That(result.Status, Is.EqualTo(W24S5EvidenceRevisionWriteResult.InvalidStatus));
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot + "/evidence/E1")), Is.False);
            AssertCleanAtomicScaffolding();
        }

        [Test]
        public void HostilePendingTreeCleanup_IsBoundedAndPreservesUnknownTree()
        {
            string injected = null;
            W24S5EvidenceRevisionWriter.BeforeSecondReplayForTests = _ =>
            {
                injected = Directory.GetDirectories(RepositoryAbsolute(CandidateRoot + "/evidence"), ".E1.pending-*", SearchOption.TopDirectoryOnly).Single();
                for (var index = 0; index < 513; index++) File.WriteAllBytes(Path.Combine(injected, "hostile-" + index.ToString("D4") + ".bin"), new byte[] { 1 });
                File.AppendAllText(RepositoryAbsolute(CandidateReceipt), " ", StrictUtf8);
            };
            var result = W24S5EvidenceRevisionWriter.Write(WriteRequest(1));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(injected, Is.Not.Null);
            Assert.That(Directory.Exists(injected), Is.True, "Bounded cleanup must preserve an injected tree beyond its owned policy rather than traversing/deleting it.");
            Assert.That(File.Exists(W24S5EvidenceRevisionWriter.RepositoryLockPathForTests), Is.False);
        }

        private static void CreateS0bFixture()
        {
            WriteRepository(WriterSource, StrictUtf8.GetBytes("// test-only descriptor writer source\n"));
            WriteRepository(CaptureSource, StrictUtf8.GetBytes("// test-only capture source\n"));
            var writer = new JObject
            {
                ["schema"] = "w24-s5-evidence-revision-writer-bundle/1",
                ["writerId"] = WriterId,
                ["writerVersion"] = WriterVersion,
                ["sources"] = new JArray(new JObject { ["path"] = WriterSource, ["sha256"] = HashFile(RepositoryAbsolute(WriterSource)) }),
                ["typedBundleHashEncoding"] = W24TypedBinaryCanonicalEncoding.EncodingName
            };
            writer["typedBundleHash"] = W24TypedBinaryCanonicalEncoding.Hash(writer);
            WriteRepository(WriterBundle, Serialize(writer));

            var capture = new JObject
            {
                ["bundleVersion"] = "w24-capture-tool-bundle/1",
                ["toolVersion"] = CaptureToolVersion,
                ["sources"] = new JArray(new JObject { ["path"] = CaptureSource, ["sha256"] = HashFile(RepositoryAbsolute(CaptureSource)) }),
                ["configuration"] = new JObject
                {
                    ["authority"] = "TEST_ONLY_DESCRIPTOR_WRITER synthetic structural capture",
                    ["emittedEvidenceExcludedFromIdentity"] = true,
                    ["candidatePathsExcludedFromIdentity"] = true
                }
            };
            WriteRepository(CaptureBundle, Serialize(capture));
            CreateCandidate(HashCanonical(capture));
            CreateRawCapture(HashCanonical(capture));
        }

        private static void AddForbiddenS0bContractDeclaration(string declaration)
        {
            var contract = ParseRepository(CandidateRoot + "/design-contract.json");
            var extensions = (JObject)contract["extensions"];
            if (declaration == "typedDiagnostics") extensions[declaration] = new JObject();
            else if (declaration == "captureToolBundle") extensions[declaration] = CaptureBundle;
            else throw new ArgumentOutOfRangeException(nameof(declaration));
            contract["contractHash"] = VfxDesignContractJson.ComputeContractHash(contract.ToString(Formatting.None));
            WriteRepositoryReplace(CandidateRoot + "/design-contract.json", Serialize(contract));

            var trace = ParseRepository(CandidateRoot + "/implementation-trace.json");
            trace["contractHash"] = (string)contract["contractHash"];
            WriteRepositoryReplace(CandidateRoot + "/implementation-trace.json", Serialize(trace));
            var receipt = ParseRepository(CandidateReceipt);
            receipt["contractFileHash"] = HashFile(RepositoryAbsolute(CandidateRoot + "/design-contract.json"));
            receipt["contractHash"] = (string)contract["contractHash"];
            receipt["traceFileHash"] = HashFile(RepositoryAbsolute(CandidateRoot + "/implementation-trace.json"));
            WriteRepositoryReplace(CandidateReceipt, Serialize(receipt));
        }

        private static W24S5EvidenceRevisionTestRegistry TestRegistry(string writerId)
        {
            var writer = ParseRepository(WriterBundle);
            return new W24S5EvidenceRevisionTestRegistry
            {
                EffectId = EffectId,
                Route = W24S5EvidenceRevisionWriter.S0bRoute,
                WriterId = writerId,
                WriterVersion = WriterVersion,
                WriterBundlePath = WriterBundle,
                WriterBundleFileHash = HashFile(RepositoryAbsolute(WriterBundle)),
                WriterBundleTypedHash = (string)writer["typedBundleHash"],
                DescriptorSchemaId = "w24-s5-evidence-revision-legacy-c0-s0b/1",
                DescriptorSchemaPath = S0bSchemaPath,
                DescriptorSchemaFileHash = HashFile(RepositoryAbsolute(S0bSchemaPath)),
                CaptureToolBundlePath = CaptureBundle,
                CaptureToolBundleFileHash = HashFile(RepositoryAbsolute(CaptureBundle))
            };
        }

        private static JObject ReportProbe(string route, string inputHash, string toolHash, JArray checks)
        {
            var report = new JObject
            {
                ["schema"] = "w24-render-metrics-report/v1",
                ["route"] = route,
                ["machineGatesPassed"] = route == "MEASURED",
                ["checks"] = checks,
                ["inputSha256"] = inputHash,
                ["toolSha256"] = toolHash,
                ["sealedReportEncoding"] = W24TypedBinaryCanonicalEncoding.EncodingName
            };
            if (route == "EVIDENCE_INVALID") report["reason"] = "test-only invalid evidence route";
            report["sealedReportHash"] = W24TypedBinaryCanonicalEncoding.Hash(report);
            return report;
        }

        private static ProjectionProbe MetricProjectionProbe(string kind)
        {
            switch (kind)
            {
                case "trail":
                    return new ProjectionProbe
                    {
                        Check = new JObject
                        {
                            ["id"] = "trail-1-18", ["kind"] = kind, ["trail"] = "raw-trail",
                            ["historyProjectedPx"] = new JArray(new JArray(0.0d, 0.0d), new JArray(1.0d, 1.0d)),
                            ["radiusPx"] = 8.0d, ["maxMeanNearestDistancePx"] = 8.0d, ["minCorridorCoverage"] = 0.8d
                        },
                        Block = new JObject
                        {
                            ["frozenView"] = new JObject { ["viewId"] = "main" },
                            ["thresholds"] = new JObject
                            {
                                ["minimumHistorySamples"] = 2, ["corridorRadiusPixels"] = 8.0d,
                                ["maximumMeanNearestHistoryDistancePixels"] = 8.0d, ["corridorCoverageMinimum"] = 0.8d
                            },
                            ["seedConsumptionPlan"] = new JObject { ["orderedSeeds"] = new JArray(1) },
                            ["metricPlan"] = new JObject
                            {
                                ["checkIdPattern"] = "trail-{seed}-{logicalFrame}", ["retainedTravelFrames"] = new JArray(18),
                                ["inputFields"] = new JArray("trail", "historyProjectedPx", "radiusPx", "maxMeanNearestDistancePx", "minCorridorCoverage")
                            }
                        },
                        Evidence = new JArray(EvidenceProbe("raw-trail", 1, 18, "main"))
                    };
                case "fragment_tracks":
                    return new ProjectionProbe
                    {
                        Check = new JObject
                        {
                            ["id"] = "fragment-1", ["kind"] = kind,
                            ["frames"] = new JArray("raw-fragment-54", "raw-fragment-63", "raw-fragment-72"),
                            ["fragmentIds"] = new JArray(201, 202, 203), ["maxTrajectoryCorrelation"] = 0.98d,
                            ["minPairwiseDistanceVariationRatio"] = 0.05d, ["rejectSingleRigidBody"] = true
                        },
                        Block = new JObject
                        {
                            ["fragmentIds"] = new JArray(201, 202, 203), ["frames"] = new JArray(54, 63, 72), ["frontViewId"] = "front",
                            ["thresholds"] = new JObject
                            {
                                ["maxTrajectoryCorrelation"] = 0.98d, ["minPairwiseDistanceVariationRatio"] = 0.05d,
                                ["rejectSingleRigidBody"] = true
                            },
                            ["metricPlan"] = new JObject
                            {
                                ["checkIdPattern"] = "fragment-{seed}",
                                ["inputFields"] = new JArray("frames", "fragmentIds", "maxTrajectoryCorrelation", "minPairwiseDistanceVariationRatio", "rejectSingleRigidBody")
                            }
                        },
                        Evidence = new JArray(
                            EvidenceProbe("raw-fragment-54", 1, 54, "front"), EvidenceProbe("raw-fragment-63", 1, 63, "front"),
                            EvidenceProbe("raw-fragment-72", 1, 72, "front"))
                    };
                case "multiview_3d":
                    return new ProjectionProbe
                    {
                        Check = new JObject
                        {
                            ["id"] = "binding-1-101", ["kind"] = kind, ["objectId"] = 101, ["carrier"] = "mesh",
                            ["minDepthSpan"] = LegacyMultiviewMinDepthSpan, ["minParallaxPx"] = 1.0d, ["requireParallax"] = true,
                            ["views"] = new JArray(
                                new JObject { ["objectIds"] = "c-object-id-1-front-f72", ["depth"] = "c-depth-1-front-f72" },
                                new JObject { ["objectIds"] = "c-object-id-1-oblique-f72", ["depth"] = "c-depth-1-oblique-f72" })
                        },
                        Block = new JObject
                        {
                            ["requiredObjectIds"] = new JArray(new JObject { ["id"] = 10 }, new JObject { ["id"] = 101 }),
                            ["parallaxRequiredObjectIds"] = new JArray(101),
                            ["thresholds"] = new JObject { ["minimumLinearDepth"] = 0.0001d, ["minimumCentroidParallaxPixelsAcrossViews"] = 1.0d },
                            ["frozenViews"] = new JArray(new JObject { ["viewId"] = "front" }, new JObject { ["viewId"] = "oblique" }),
                            ["seedConsumptionPlan"] = new JObject { ["orderedSeeds"] = new JArray(1) },
                            ["metricPlan"] = new JObject
                            {
                                ["checkIdPattern"] = "binding-{seed}-{objectId}", ["logicalFrame"] = 72,
                                ["inputFields"] = new JArray("views.objectIds", "views.depth", "objectId", "minDepthSpan", "minParallaxPx", "requireParallax")
                            }
                        },
                        Evidence = new JArray(
                            SlotEvidenceProbe("c-object-id-1-front-f72", 1, 72, "front", "object-id", "id_uint", "diagnostics/seed_1/frame_00072_front_object-id.npy", "w24diagnosticobjectregistration"),
                            SlotEvidenceProbe("c-depth-1-front-f72", 1, 72, "front", "depth-linear", "linear_float", "diagnostics/seed_1/frame_00072_front_linear-depth.npy", "diagnostics/seed_1/frame_00072_front_object-id.npy"),
                            SlotEvidenceProbe("c-object-id-1-oblique-f72", 1, 72, "oblique", "object-id", "id_uint", "diagnostics/seed_1/frame_00072_oblique_object-id.npy", "w24diagnosticobjectregistration"),
                            SlotEvidenceProbe("c-depth-1-oblique-f72", 1, 72, "oblique", "depth-linear", "linear_float", "diagnostics/seed_1/frame_00072_oblique_linear-depth.npy", "diagnostics/seed_1/frame_00072_oblique_object-id.npy"))
                    };
                case "receiver_luminance_ldr":
                    return new ProjectionProbe
                    {
                        Check = new JObject
                        {
                            ["id"] = "receiver-a-1", ["kind"] = kind, ["on"] = "d-receiver-on-1-f24", ["off"] = "d-receiver-off-1-f24",
                            ["receiverIds"] = "d-receiver-id-1-f24", ["effectMask"] = "d-effect-mask-1-f24", ["receiverId"] = 11,
                            ["minLinearLuminanceDelta"] = 0.001d
                        },
                        Block = new JObject
                        {
                            ["receiverIds"] = new JArray(new JObject { ["id"] = 11, ["role"] = "receiver_a" }),
                            ["thresholds"] = new JObject { ["minimumLinearLuminanceDelta"] = 0.001d },
                            ["seedConsumptionPlan"] = new JObject { ["orderedSeeds"] = new JArray(1), ["logicalFrame"] = 24 },
                            ["frozenView"] = new JObject { ["viewId"] = "main" },
                            ["metricPlan"] = new JObject
                            {
                                ["checkIdPattern"] = "receiver-{receiver}-{seed}",
                                ["inputFields"] = new JArray("on", "off", "receiverIds", "effectMask", "receiverId", "minLinearLuminanceDelta")
                            }
                        },
                        Evidence = new JArray(
                            SlotEvidenceProbe("d-receiver-on-1-f24", 1, 24, "main", "receiver-linear-ldr", "linear_ldr", "diagnostics/seed_1/frame_00024_receiver-on-linear-ldr.npy", "diagnostics/seed_1/frame_00024_receiver-off-linear-ldr.npy"),
                            SlotEvidenceProbe("d-receiver-off-1-f24", 1, 24, "main", "receiver-linear-ldr", "linear_ldr", "diagnostics/seed_1/frame_00024_receiver-off-linear-ldr.npy", "diagnostics/seed_1/frame_00024_receiver-id.npy"),
                            SlotEvidenceProbe("d-receiver-id-1-f24", 1, 24, "main", "receiver-id", "id_uint", "diagnostics/seed_1/frame_00024_receiver-id.npy", "receiver-probe-registration"),
                            SlotEvidenceProbe("d-effect-mask-1-f24", 1, 24, "main", "effect-mask", "mask_binary", "diagnostics/seed_1/frame_00024_effect-mask.npy", "runtime-entry-renderer-set"))
                    };
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static JObject EvidenceProbe(string id, int seed, int frame, string view)
        {
            return new JObject { ["id"] = id, ["seed"] = seed, ["logicalFrameIndex"] = frame, ["viewId"] = view };
        }

        private static JObject SlotEvidenceProbe(string id, int seed, int frame, string view, string passId, string encoding, string path, string derivedFrom)
        {
            var value = EvidenceProbe(id, seed, frame, view);
            value["passId"] = passId;
            value["encoding"] = encoding;
            value["path"] = path;
            value["derivedFrom"] = derivedFrom;
            return value;
        }

        private static void MutateMetricProjectionValue(ProjectionProbe probe)
        {
            switch ((string)probe.Check["kind"])
            {
                case "trail": probe.Check["radiusPx"] = 7.0d; break;
                case "fragment_tracks": ((JArray)probe.Check["fragmentIds"])[1] = 999; break;
                case "multiview_3d": probe.Check["minParallaxPx"] = 2.0d; break;
                case "receiver_luminance_ldr": probe.Check["minLinearLuminanceDelta"] = 0.002d; break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private sealed class ProjectionProbe
        {
            internal JObject Check;
            internal JObject Block;
            internal JArray Evidence;
        }

        private static void UpgradeFixtureToS3(bool syntheticKind = false)
        {
            WriteRepository(MetricsTool, StrictUtf8.GetBytes("# test-only frozen metrics tool\n"));
            var metricsToolHash = HashFile(RepositoryAbsolute(MetricsTool));
            var capture = ParseRepository(CaptureBundle);
            ((JArray)capture["sources"]).Add(new JObject { ["path"] = MetricsTool, ["sha256"] = metricsToolHash });
            WriteRepositoryReplace(CaptureBundle, Serialize(capture));
            var captureBundleHash = HashCanonical(capture);

            var environmentBody = new JObject
            {
                ["pythonExecutablePath"] = Forward(RepositoryAbsolute(MetricsTool)),
                ["pythonExecutableSha256"] = metricsToolHash,
                ["pythonVersion"] = "TEST_ONLY",
                ["numpyVersion"] = "TEST_ONLY",
                ["pillowVersion"] = "TEST_ONLY"
            };
            var environment = (JObject)environmentBody.DeepClone();
            environment["environmentSha256"] = HashCanonical(environmentBody);
            var matrix = new JArray(new JObject
            {
                ["evidenceId"] = "raw-one", ["passId"] = "typed-probe", ["seed"] = 1,
                ["viewId"] = "main", ["logicalFrameIndex"] = 0
            });

            var contract = ParseRepository(CandidateRoot + "/design-contract.json");
            var captureProfile = (JObject)contract["captureProfile"];
            captureProfile["captureToolHash"] = captureBundleHash;
            var extensions = (JObject)contract["extensions"];
            extensions["captureToolBundle"] = CaptureBundle;
            var metricKind = syntheticKind ? "synthetic" : "trail";
            var metricPlan = new JObject
            {
                ["tool"] = MetricsTool,
                ["bridge"] = "W24MetricsEvidenceDag",
                ["kind"] = metricKind,
                ["checkIdPattern"] = syntheticKind ? "synthetic-{seed}" : "trail-{seed}",
                ["retainedTravelFrames"] = new JArray(0),
                ["inputFields"] = syntheticKind
                    ? new JArray("evidenceId")
                    : new JArray("trail", "historyProjectedPx", "radiusPx", "maxMeanNearestDistancePx", "minCorridorCoverage")
            };
            extensions["typedDiagnostics"] = new JObject
            {
                ["metricsTool"] = new JObject { ["path"] = MetricsTool, ["sha256"] = metricsToolHash },
                ["metricsEnvironment"] = environment.DeepClone(),
                ["requiredEvidenceMatrix"] = matrix.DeepClone(),
                ["trailCorridor"] = new JObject
                {
                    ["requirementId"] = "REQ-LIGHT-RECEIVER",
                    ["frozenView"] = new JObject { ["viewId"] = "main" },
                    ["thresholds"] = new JObject
                    {
                        ["minimumHistorySamples"] = 2,
                        ["corridorRadiusPixels"] = 8.0d,
                        ["maximumMeanNearestHistoryDistancePixels"] = 8.0d,
                        ["corridorCoverageMinimum"] = 0.8d
                    },
                    ["seedConsumptionPlan"] = new JObject { ["orderedSeeds"] = new JArray(1) },
                    ["metricPlan"] = metricPlan
                }
            };
            contract["contractHash"] = VfxDesignContractJson.ComputeContractHash(contract.ToString(Formatting.None));
            WriteRepositoryReplace(CandidateRoot + "/design-contract.json", Serialize(contract));
            var captureProfileHash = "sha256:" + RecipeCanonicalizer.ComputeSha256(captureProfile.ToString(Formatting.None));

            var trace = ParseRepository(CandidateRoot + "/implementation-trace.json");
            trace["contractHash"] = (string)contract["contractHash"];
            trace["captureProfileHash"] = captureProfileHash;
            WriteRepositoryReplace(CandidateRoot + "/implementation-trace.json", Serialize(trace));
            var receipt = ParseRepository(CandidateReceipt);
            receipt["contractFileHash"] = HashFile(RepositoryAbsolute(CandidateRoot + "/design-contract.json"));
            receipt["contractHash"] = (string)contract["contractHash"];
            receipt["traceFileHash"] = HashFile(RepositoryAbsolute(CandidateRoot + "/implementation-trace.json"));
            receipt["captureProfileHash"] = captureProfileHash;
            WriteRepositoryReplace(CandidateReceipt, Serialize(receipt));

            var oldSeal = ParseRepository(RawRoot + "/evidence-seal.json");
            File.Delete(RepositoryAbsolute(RawRoot + "/evidence-seal.json"));
            var diagnostic = ParseRepository(RawRoot + "/diagnostic-pass-manifest.json");
            ((JArray)diagnostic["passes"]).Add(new JObject
            {
                ["passId"] = "typed-probe", ["encoding"] = "json", ["purpose"] = "synthetic typed-metrics fixture"
            });
            WriteRepositoryReplace(RawRoot + "/diagnostic-pass-manifest.json", Compact(diagnostic));
            var diagnosticHash = HashFile(RepositoryAbsolute(RawRoot + "/diagnostic-pass-manifest.json"));
            var typedHash = WriteRawJson("diagnostics/typed-probe.json", new JObject { ["schema"] = "w24-test-typed-raw/1", ["value"] = 1 });
            var observed = new JObject
            {
                ["serial"] = 1, ["frame"] = 1, ["time"] = 0.1d, ["logicalFrameIndex"] = 0, ["seed"] = 1, ["viewId"] = "main"
            };
            var typedRecord = new JObject
            {
                ["kind"] = "diagnostic", ["passId"] = "typed-probe", ["encoding"] = "json",
                ["description"] = "synthetic typed raw", ["derivedFrom"] = "frames/seed_1/frame_00000_effect-only.png",
                ["file"] = "diagnostics/typed-probe.json", ["sha256"] = typedHash, ["observedPlayerLoop"] = observed.DeepClone()
            };
            var evidence = new JArray(new JObject
            {
                ["id"] = "raw-one", ["path"] = "diagnostics/typed-probe.json", ["sha256"] = typedHash,
                ["kind"] = "diagnostic", ["passId"] = "typed-probe", ["encoding"] = "json", ["seed"] = 1,
                ["logicalFrameIndex"] = 0, ["playerLoopSerial"] = 1, ["playerLoopFrame"] = 1,
                ["playerLoopTime"] = 0.1d, ["viewId"] = "main", ["derivedFrom"] = "frames/seed_1/frame_00000_effect-only.png"
            });
            var metadata = ParseRepository(RawRoot + "/capture-metadata.json");
            ((JObject)metadata.SelectToken("sourceHashes.captureTool"))["sha256"] = captureBundleHash;
            ((JObject)metadata["diagnosticPassManifest"])["sha256"] = diagnosticHash;
            metadata["typedRawDiagnostics"] = new JArray(typedRecord);
            var input = new JObject
            {
                ["schema"] = "w24-render-metrics-input/v1", ["effectId"] = EffectId, ["candidateId"] = "C0",
                ["contractRevision"] = ContractRevision, ["contractSha256"] = (string)contract["contractHash"],
                ["captureProfileSha256"] = captureProfileHash, ["recorderCaptureProfileSha256"] = (string)metadata["captureProfileSha256"],
                ["captureToolBundlePath"] = CaptureBundle, ["captureToolBundleSha256"] = captureBundleHash,
                ["expectedToolSha256"] = metricsToolHash, ["metricsEnvironment"] = environment.DeepClone(),
                ["requiredEvidenceMatrix"] = matrix.DeepClone(), ["requiredEvidenceMatrixSha256"] = HashCanonical(matrix),
                ["evidence"] = evidence,
                ["checks"] = new JArray(syntheticKind
                    ? new JObject { ["id"] = "synthetic-1", ["kind"] = "synthetic", ["evidenceId"] = "raw-one" }
                    : new JObject
                    {
                        ["id"] = "trail-1", ["kind"] = "trail", ["trail"] = "raw-one",
                        ["historyProjectedPx"] = new JArray(new JArray(0.0d, 0.0d), new JArray(1.0d, 1.0d)),
                        ["radiusPx"] = 8.0d, ["maxMeanNearestDistancePx"] = 8.0d, ["minCorridorCoverage"] = 0.8d
                    })
            };
            var inputHash = WriteRawJson("diagnostics/metrics-input.json", input);
            var inputCanonicalHash = HashCanonical(input);
            var report = new JObject
            {
                ["schema"] = "w24-render-metrics-report/v1", ["route"] = "MEASURED", ["machineGatesPassed"] = true,
                ["checks"] = new JArray(new JObject { ["id"] = syntheticKind ? "synthetic-1" : "trail-1", ["kind"] = metricKind, ["pass"] = true }), ["inputSha256"] = inputCanonicalHash,
                ["toolSha256"] = metricsToolHash, ["sealedReportEncoding"] = W24TypedBinaryCanonicalEncoding.EncodingName
            };
            report["sealedReportHash"] = W24TypedBinaryCanonicalEncoding.Hash(report);
            var reportHash = WriteRawJson("diagnostics/metrics-report.json", report);
            metadata["metricInputs"] = new JArray(new JObject
            {
                ["kind"] = "metrics-input", ["file"] = "diagnostics/metrics-input.json", ["sha256"] = inputHash,
                ["expectedToolSha256"] = metricsToolHash, ["metricsEnvironmentSha256"] = (string)environment["environmentSha256"]
            });
            metadata["metricReports"] = new JArray(new JObject
            {
                ["kind"] = "diagnostic", ["passId"] = "metrics-report", ["encoding"] = "json",
                ["file"] = "diagnostics/metrics-report.json", ["sha256"] = reportHash,
                ["inputFile"] = "diagnostics/metrics-input.json", ["inputFileSha256"] = inputHash,
                ["analysisInputSha256"] = inputCanonicalHash, ["expectedToolSha256"] = metricsToolHash
            });
            WriteRepositoryReplace(RawRoot + "/capture-metadata.json", Compact(metadata));
            var provenance = (JObject)oldSeal["provenance"];
            WriteSeal((JObject)metadata["sourceHashes"], (string)provenance["operatorCommandHash"], captureBundleHash, (string)metadata["captureProfileSha256"]);
        }

        private static void StripTypedRawCaptureToS0bShape()
        {
            var oldSeal = ParseRepository(RawRoot + "/evidence-seal.json");
            var provenance = (JObject)oldSeal["provenance"];
            var metadata = ParseRepository(RawRoot + "/capture-metadata.json");

            foreach (var relative in new[]
                     {
                         "diagnostics/typed-probe.json",
                         "diagnostics/metrics-input.json",
                         "diagnostics/metrics-report.json"
                     })
            {
                var absolute = RepositoryAbsolute(RawRoot + "/" + relative);
                if (File.Exists(absolute)) File.Delete(absolute);
            }

            metadata["typedRawDiagnostics"] = new JArray();
            metadata["metricInputs"] = new JArray();
            metadata["metricReports"] = new JArray();

            var diagnostic = ParseRepository(RawRoot + "/diagnostic-pass-manifest.json");
            diagnostic["passes"] = new JArray(((JArray)diagnostic["passes"])
                .OfType<JObject>()
                .Where(value => !string.Equals((string)value["passId"], "typed-probe", StringComparison.Ordinal))
                .Select(value => value.DeepClone()));
            WriteRepositoryReplace(RawRoot + "/diagnostic-pass-manifest.json", Compact(diagnostic));
            ((JObject)metadata["diagnosticPassManifest"])["sha256"] =
                HashFile(RepositoryAbsolute(RawRoot + "/diagnostic-pass-manifest.json"));
            WriteRepositoryReplace(RawRoot + "/capture-metadata.json", Compact(metadata));

            File.Delete(RepositoryAbsolute(RawRoot + "/evidence-seal.json"));
            WriteSeal(
                (JObject)metadata["sourceHashes"],
                (string)provenance["operatorCommandHash"],
                (string)provenance["captureToolSha256"],
                (string)metadata["captureProfileSha256"]);
        }

        private static void CreateCandidate(string captureBundleHash)
        {
            var runtimeGuid = GuidFor("runtime");
            WriteProjectAsset(RuntimeAsset, StrictUtf8.GetBytes("synthetic prefab bytes\n"), runtimeGuid);
            WriteProjectAsset(PreviewAsset, StrictUtf8.GetBytes("synthetic preview bytes\n"), GuidFor("preview"));

            var baseContractText = File.ReadAllText(RepositoryAbsolute("docs/vfx-contracts/sustained_flame_3d.contract.json"), StrictUtf8)
                .Replace("sustained_flame_3d", EffectId)
                .Replace("Assets/VFX/Preview/VFXPREVIEW_SustainedFlame.unity", PreviewAsset)
                .Replace("Assets/VFX/Effects/Aura/" + EffectId + "/VFX_" + EffectId + ".prefab", RuntimeAsset);
            var bootstrapContract = JObject.Parse(baseContractText);
            var bootstrapCapture = (JObject)bootstrapContract["captureProfile"];
            bootstrapCapture["cameraSerializedReference"] = PreviewAsset + "#MainCamera";
            bootstrapCapture["sceneSerializedReference"] = PreviewAsset;
            bootstrapCapture["prefabManifestSerializedReference"] = ProductionManifest + "#buildHash";
            bootstrapCapture["captureToolVersion"] = CaptureToolVersion;
            bootstrapCapture["captureToolHash"] = captureBundleHash;
            var bootstrapExtensions = (JObject)bootstrapContract["extensions"];
            bootstrapExtensions["runtimeEntry"] = RuntimeAsset;
            bootstrapExtensions["previewScene"] = PreviewAsset;
            bootstrapExtensions["recipe"] = "Assets/VFX/Recipes/Aura/" + EffectId + ".default.json";
            bootstrapExtensions["implementationTrace"] = BootstrapTrace;
            bootstrapContract["contractHash"] = VfxDesignContractJson.ComputeContractHash(bootstrapContract.ToString(Formatting.None));
            WriteRepository(BootstrapContract, Serialize(bootstrapContract));

            var baseTraceText = File.ReadAllText(RepositoryAbsolute("docs/vfx-traces/sustained_flame_3d.implementation-trace.json"), StrictUtf8)
                .Replace("sustained_flame_3d", EffectId)
                .Replace("Assets/VFX/Effects/Aura/" + EffectId + "/VFX_" + EffectId + ".prefab", RuntimeAsset);
            var bootstrapTrace = JObject.Parse(baseTraceText);
            bootstrapTrace["effectId"] = EffectId;
            bootstrapTrace["contractRevision"] = ContractRevision;
            bootstrapTrace["contractHash"] = (string)bootstrapContract["contractHash"];
            bootstrapTrace["runtimeEntryAssetPath"] = RuntimeAsset;
            WriteRepository(BootstrapTrace, Serialize(bootstrapTrace));

            var rawBuild = RawHash("test build");
            var manifest = FullManifest(runtimeGuid, rawBuild, bootstrapContract, bootstrapTrace);
            WriteProject(ProductionManifest, Serialize(manifest));

            var candidateContract = (JObject)bootstrapContract.DeepClone();
            var candidateCapture = (JObject)candidateContract["captureProfile"];
            candidateCapture["sceneHash"] = HashFile(ProjectAbsolute(PreviewAsset));
            candidateCapture["prefabManifestHash"] = "sha256:" + rawBuild;
            var extensions = (JObject)candidateContract["extensions"];
            extensions["captureBindingStatus"] = "FROZEN_PRE_C0";
            extensions["visualStatus"] = "VISUAL_PENDING";
            extensions["candidateId"] = "C0";
            extensions["candidateStatus"] = "C0_CAPTURE_PENDING";
            extensions["bootstrapContractPath"] = BootstrapContract;
            extensions["bootstrapContractFileHash"] = HashFile(RepositoryAbsolute(BootstrapContract));
            extensions["bootstrapTracePath"] = BootstrapTrace;
            extensions["bootstrapTraceFileHash"] = HashFile(RepositoryAbsolute(BootstrapTrace));
            extensions["implementationTrace"] = CandidateRoot + "/implementation-trace.json";
            extensions["candidateReceipt"] = CandidateReceipt;
            candidateContract["contractHash"] = VfxDesignContractJson.ComputeContractHash(candidateContract.ToString(Formatting.None));
            var contractText = Serialize(candidateContract);
            WriteRepository(CandidateRoot + "/design-contract.json", contractText);

            var candidateTrace = (JObject)bootstrapTrace.DeepClone();
            candidateTrace["traceStatus"] = "C0_CAPTURE_PENDING";
            candidateTrace["candidateRevision"] = 0;
            candidateTrace["evidenceRevision"] = 0;
            candidateTrace["contractRevision"] = ContractRevision;
            candidateTrace["contractHash"] = (string)candidateContract["contractHash"];
            candidateTrace["buildHash"] = "sha256:" + rawBuild;
            candidateTrace["captureProfileHash"] = "sha256:" + RecipeCanonicalizer.ComputeSha256(candidateCapture.ToString(Formatting.None));
            candidateTrace["runtimeEntryAssetPath"] = RuntimeAsset;
            candidateTrace["runtimeEntryGuid"] = runtimeGuid;
            WriteRepository(CandidateRoot + "/implementation-trace.json", Serialize(candidateTrace));
            WriteRepository(CandidateRoot + "/bootstrap-manifest.json", Serialize(manifest));

            var receipt = new JObject
            {
                ["candidateVersion"] = "w24-candidate/1.0", ["candidateId"] = "C0", ["candidateRevision"] = 0,
                ["candidateStatus"] = "C0_CAPTURE_PENDING", ["effectId"] = EffectId,
                ["bootstrapContractPath"] = BootstrapContract, ["bootstrapContractFileHash"] = HashFile(RepositoryAbsolute(BootstrapContract)),
                ["bootstrapContractHash"] = (string)bootstrapContract["contractHash"], ["bootstrapContractRevision"] = ContractRevision,
                ["bootstrapTracePath"] = BootstrapTrace, ["bootstrapTraceFileHash"] = HashFile(RepositoryAbsolute(BootstrapTrace)),
                ["productionManifestPath"] = ProductionManifest,
                ["bootstrapManifestSnapshotPath"] = CandidateRoot + "/bootstrap-manifest.json",
                ["bootstrapManifestSnapshotFileHash"] = HashFile(RepositoryAbsolute(CandidateRoot + "/bootstrap-manifest.json")),
                ["ownedOutputs"] = manifest["ownedOutputs"].DeepClone(), ["buildHash"] = "sha256:" + rawBuild,
                ["runtimeEntryPath"] = RuntimeAsset, ["runtimeEntryGuid"] = runtimeGuid,
                ["previewScenePath"] = PreviewAsset, ["previewSceneHash"] = HashFile(ProjectAbsolute(PreviewAsset)),
                ["contractPath"] = CandidateRoot + "/design-contract.json", ["contractFileHash"] = HashFile(RepositoryAbsolute(CandidateRoot + "/design-contract.json")),
                ["contractHash"] = (string)candidateContract["contractHash"],
                ["tracePath"] = CandidateRoot + "/implementation-trace.json", ["traceFileHash"] = HashFile(RepositoryAbsolute(CandidateRoot + "/implementation-trace.json")),
                ["captureProfileHash"] = (string)candidateTrace["captureProfileHash"], ["visualStatus"] = "VISUAL_PENDING"
            };
            WriteRepository(CandidateReceipt, Serialize(receipt));
        }

        private static JObject FullManifest(string runtimeGuid, string rawBuild, JObject bootstrapContract, JObject bootstrapTrace)
        {
            var contractHash = HashFile(RepositoryAbsolute(BootstrapContract));
            var traceHash = HashFile(RepositoryAbsolute(BootstrapTrace));
            return new JObject
            {
                ["manifestVersion"] = 1, ["rulesVersion"] = "w24-test-rules/1", ["enforcement"] = "strict",
                ["effectId"] = EffectId, ["archetype"] = "sustained", ["recipeVersion"] = 1, ["recipeRevision"] = 1,
                ["recipeHash"] = RawHash("recipe"), ["buildHash"] = rawBuild, ["compilerVersion"] = "w24-test-compiler/1",
                ["unityVersion"] = "2022.3.62f3c1", ["sourceRecipePath"] = "Assets/VFX/Recipes/Aura/" + EffectId + ".default.json",
                ["runtimeEntry"] = new JObject { ["kind"] = "prefab", ["path"] = RuntimeAsset, ["guid"] = runtimeGuid },
                ["ownedOutputs"] = new JArray(new JObject { ["path"] = RuntimeAsset, ["guid"] = runtimeGuid, ["assetType"] = "GameObject", ["sha256"] = RawFileHash(ProjectAbsolute(RuntimeAsset)) }),
                ["dependencies"] = new JArray(),
                ["cost"] = new JObject
                {
                    ["particles"] = 1, ["particleSystems"] = 1, ["renderers"] = 1, ["materials"] = 1, ["trails"] = 0,
                    ["duration"] = 1.0d, ["localTextureBytes"] = 0, ["dependencyResidentTextureBytes"] = 0,
                    ["gameObjects"] = 1, ["maxDepth"] = 1
                },
                ["audit"] = new JArray(),
                ["formalProduction"] = new JObject
                {
                    ["contractPath"] = BootstrapContract, ["contractFileHash"] = contractHash,
                    ["contractHash"] = (string)bootstrapContract["contractHash"], ["contractRevision"] = ContractRevision,
                    ["tracePath"] = BootstrapTrace, ["traceFileHash"] = traceHash, ["visualStatus"] = "VISUAL_PENDING",
                    ["evidenceCorpusPath"] = JValue.CreateNull(), ["evidenceCorpusHash"] = JValue.CreateNull(),
                    ["userVerdictRecordPath"] = JValue.CreateNull(), ["userVerdictRecordHash"] = JValue.CreateNull(),
                    ["visualQaRecordPath"] = JValue.CreateNull(), ["visualQaRecordHash"] = JValue.CreateNull(),
                    ["s0aStatusRecordPath"] = JValue.CreateNull(), ["s0aStatusRecordHash"] = JValue.CreateNull(),
                    ["admissionPhase"] = "PRE_C0_FIRST_FORMAL_BUILD"
                },
                ["generatedAtUtc"] = "2026-08-26T00:00:00Z"
            };
        }

        private static void CreateRawCapture(string captureBundleHash)
        {
            var profile = RecorderProfile();
            var profileHash = HashBytes(StrictUtf8.GetBytes(profile.ToString(Formatting.None)));
            var lockObject = new JObject { ["schema"] = "w24-s0a-evidence-lock/v1", ["candidateId"] = "C0", ["captureProfileSha256"] = profileHash };
            WriteRepository(RawRoot + "/evidence-lock.json", Compact(lockObject));
            var commandHash = WriteRawJson("diagnostics/operator-command.json", new JObject { ["schema"] = "w24-test-command/1", ["candidateId"] = "C0" });
            var telemetryHash = WriteRawJson("diagnostics/semantic-telemetry.json", new JObject { ["schema"] = "w24-test-telemetry/1", ["state"] = "sealed" });
            var offHash = WriteRaw("diagnostics/receiver-light-off.png", new byte[] { 137, 80, 78, 71, 1 });
            var onHash = WriteRaw("diagnostics/receiver-light-on.png", new byte[] { 137, 80, 78, 71, 2 });
            var summaryHash = WriteRawJson("diagnostics/receiver-light-ab.json", new JObject { ["schema"] = "w24-test-receiver/1", ["delta"] = 0.1d });
            var beautyHash = WriteRaw("frames/seed_1/frame_00000_beauty.png", new byte[] { 137, 80, 78, 71, 3 });
            var effectHash = WriteRaw("frames/seed_1/frame_00000_effect-only.png", new byte[] { 137, 80, 78, 71, 4 });
            var diagnostic = new JObject
            {
                ["schema"] = "w24-s0a-diagnostic-pass-manifest/v1",
                ["passes"] = new JArray(new JObject
                {
                    ["passId"] = "effect-only-rgba", ["encoding"] = "rgba8_png", ["purpose"] = "synthetic structural probe",
                    ["camera"] = "same serialized authority Camera", ["clear"] = "transparent black", ["cullingMask"] = 1, ["format"] = "RGBA32 PNG"
                })
            };
            WriteRepository(RawRoot + "/diagnostic-pass-manifest.json", Compact(diagnostic));
            var diagnosticHash = HashFile(RepositoryAbsolute(RawRoot + "/diagnostic-pass-manifest.json"));
            var receipt = ParseRepository(CandidateReceipt);
            var sourceHashes = new JObject
            {
                ["scene"] = new JObject { ["path"] = Forward(ProjectAbsolute(PreviewAsset)), ["sha256"] = HashFile(ProjectAbsolute(PreviewAsset)) },
                ["prefab"] = new JObject { ["path"] = Forward(ProjectAbsolute(RuntimeAsset)), ["guid"] = (string)receipt["runtimeEntryGuid"], ["sha256"] = "sha256:" + RawFileHash(ProjectAbsolute(RuntimeAsset)) },
                ["manifest"] = new JObject { ["path"] = Forward(ProjectAbsolute(ProductionManifest)), ["sha256"] = (string)receipt["bootstrapManifestSnapshotFileHash"], ["buildHash"] = (string)receipt["buildHash"] },
                ["captureTool"] = new JObject { ["path"] = Forward(RepositoryAbsolute(CaptureBundle)), ["version"] = CaptureToolVersion, ["sha256"] = captureBundleHash }
            };
            var token = new JObject { ["serial"] = 1, ["frame"] = 1, ["time"] = 0.1d, ["logicalFrameIndex"] = 0, ["seed"] = 1 };
            var metadata = new JObject
            {
                ["schema"] = "w24-s0a-capture-evidence/v1", ["candidateId"] = "C0",
                ["captureModePolicy"] = "graphics-device batchmode required; -nographics prohibited; synchronized ReadPixels",
                ["executedInBatchMode"] = true,
                ["frameRetentionPolicy"] = "retained-keyframes-only; synthetic structural fixture",
                ["retainedFrameIndices"] = new JArray(0), ["retainedFrameIndicesSha256"] = HashText("0"),
                ["formalPlayerLoop"] = new JObject { ["observedSerial"] = 1, ["consumedSerial"] = 1, ["allObservedFramesConsumed"] = true },
                ["captureProfile"] = profile, ["captureProfileSha256"] = profileHash, ["sourceHashes"] = sourceHashes,
                ["diagnosticPassManifest"] = new JObject { ["file"] = "diagnostic-pass-manifest.json", ["sha256"] = diagnosticHash },
                ["typedRawDiagnostics"] = new JArray(), ["metricInputs"] = new JArray(), ["metricReports"] = new JArray(),
                ["semanticTelemetry"] = new JArray(new JObject { ["kind"] = "semantic-telemetry", ["description"] = "synthetic sealed telemetry", ["file"] = "diagnostics/semantic-telemetry.json", ["sha256"] = telemetryHash }),
                ["supplementalDiagnostics"] = new JArray(
                    new JObject { ["kind"] = "formal-capture-command", ["description"] = "synthetic command", ["file"] = "diagnostics/operator-command.json", ["sha256"] = commandHash },
                    ObservedRecord("receiver-light-off", "diagnostics/receiver-light-off.png", offHash, token),
                    ObservedRecord("receiver-light-on", "diagnostics/receiver-light-on.png", onHash, token),
                    ObservedRecord("receiver-linear-luminance-ab", "diagnostics/receiver-light-ab.json", summaryHash, token)),
                ["frames"] = new JArray(new JObject
                {
                    ["frameIndex"] = 0, ["simulationTime"] = 0.0d, ["state"] = "steady", ["seed"] = 1,
                    ["beauty"] = new JObject { ["file"] = "frames/seed_1/frame_00000_beauty.png", ["sha256"] = beautyHash },
                    ["diagnostics"] = new JArray(new JObject
                    {
                        ["passId"] = "effect-only-rgba", ["file"] = "frames/seed_1/frame_00000_effect-only.png", ["sha256"] = effectHash,
                        ["foregroundPixels"] = 1, ["method"] = "same-serialized-camera; synthetic structural fixture"
                    })
                })
            };
            WriteRepository(RawRoot + "/capture-metadata.json", Compact(metadata));
            WriteSeal(sourceHashes, commandHash, captureBundleHash, profileHash);
        }

        private static JObject RecorderProfile()
        {
            return new JObject
            {
                ["profileVersion"] = "w24-test-profile/v1", ["unityVersion"] = "2022.3.62f3c1", ["urpVersion"] = "14.0.12",
                ["graphicsApi"] = "Direct3D11", ["graphicsDevice"] = "synthetic", ["graphicsDriverVersion"] = "synthetic",
                ["renderTextureFormat"] = "ARGB32",
                ["rendererAsset"] = new JObject { ["reference"] = "synthetic-renderer", ["sha256"] = HashText("renderer") },
                ["volume"] = new JObject { ["reference"] = "synthetic-volume", ["sha256"] = HashText("volume") },
                ["scenePath"] = PreviewAsset, ["serializedCameraReference"] = PreviewAsset + "#MainCamera",
                ["resolution"] = new JArray(960, 540), ["fps"] = 60, ["background"] = new JArray(0.035d, 0.04d, 0.055d, 1.0d),
                ["colorSpace"] = "Linear", ["hdr"] = false, ["msaa"] = false,
                ["bloom"] = new JObject { ["value"] = false, ["validation"] = "caller-frozen" },
                ["toneMapping"] = new JObject { ["value"] = "None", ["validation"] = "caller-frozen" },
                ["canonicalSeed"] = 1, ["robustnessSeeds"] = new JArray(2, 3), ["retainedFrameIndices"] = new JArray(0),
                ["retainedFrameIndicesSha256"] = HashText("0")
            };
        }

        private static JObject ObservedRecord(string kind, string file, string hash, JObject token)
        {
            return new JObject
            {
                ["kind"] = kind, ["description"] = "synthetic observed diagnostic", ["file"] = file, ["sha256"] = hash,
                ["observedPlayerLoop"] = token.DeepClone()
            };
        }

        private static void WriteSeal(JObject sourceHashes, string commandHash, string captureBundleHash, string profileHash)
        {
            var files = Directory.GetFiles(RepositoryAbsolute(RawRoot), "*", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith("evidence-seal.json", StringComparison.Ordinal))
                .Select(path => new { Path = path, Local = Relative(RepositoryAbsolute(RawRoot), path) })
                .OrderBy(item => item.Local, StringComparer.Ordinal).ToArray();
            var body = new JObject
            {
                ["schema"] = "w24-s0a-final-evidence-seal/v1", ["candidateId"] = "C0", ["captureProfileSha256"] = profileHash,
                ["artifacts"] = new JArray(files.Select(item => new JObject { ["file"] = item.Local, ["sha256"] = HashFile(item.Path) })),
                ["provenance"] = new JObject
                {
                    ["operatorCommandHash"] = commandHash, ["captureToolSha256"] = captureBundleHash,
                    ["sourceHashesSha256"] = HashBytes(StrictUtf8.GetBytes(sourceHashes.ToString(Formatting.None))),
                    ["captureMetadataSha256"] = HashFile(RepositoryAbsolute(RawRoot + "/capture-metadata.json"))
                }
            };
            body["sealHash"] = HashBytes(StrictUtf8.GetBytes(body.ToString(Formatting.None)));
            WriteRepository(RawRoot + "/evidence-seal.json", Compact(body));
        }

        private static void ResealMetadata(Action<JObject> mutation)
        {
            var metadata = ParseRepository(RawRoot + "/capture-metadata.json"); mutation(metadata);
            WriteRepositoryReplace(RawRoot + "/capture-metadata.json", Compact(metadata));
            var oldSeal = ParseRepository(RawRoot + "/evidence-seal.json");
            var provenance = (JObject)oldSeal["provenance"];
            File.Delete(RepositoryAbsolute(RawRoot + "/evidence-seal.json"));
            WriteSeal((JObject)metadata["sourceHashes"], (string)provenance["operatorCommandHash"], (string)provenance["captureToolSha256"], (string)metadata["captureProfileSha256"]);
        }

        private static void ConfigureS0bRegistry(string schemaPath = S0bSchemaPath)
        {
            var writer = ParseRepository(WriterBundle);
            W24S5EvidenceRevisionWriter.ConfigureRegistryForTests(new W24S5EvidenceRevisionTestRegistry
            {
                EffectId = EffectId, Route = W24S5EvidenceRevisionWriter.S0bRoute, WriterId = WriterId, WriterVersion = WriterVersion,
                WriterBundlePath = WriterBundle, WriterBundleFileHash = HashFile(RepositoryAbsolute(WriterBundle)), WriterBundleTypedHash = (string)writer["typedBundleHash"],
                DescriptorSchemaId = "w24-s5-evidence-revision-legacy-c0-s0b/1", DescriptorSchemaPath = schemaPath, DescriptorSchemaFileHash = HashFile(RepositoryAbsolute(schemaPath)),
                CaptureToolBundlePath = CaptureBundle, CaptureToolBundleFileHash = HashFile(RepositoryAbsolute(CaptureBundle))
            });
        }

        private static void ConfigureS3Registry()
        {
            var writer = ParseRepository(WriterBundle);
            W24S5EvidenceRevisionWriter.ConfigureRegistryForTests(new W24S5EvidenceRevisionTestRegistry
            {
                EffectId = EffectId, Route = W24S5EvidenceRevisionWriter.S3Route, WriterId = WriterId, WriterVersion = WriterVersion,
                WriterBundlePath = WriterBundle, WriterBundleFileHash = HashFile(RepositoryAbsolute(WriterBundle)), WriterBundleTypedHash = (string)writer["typedBundleHash"],
                DescriptorSchemaId = "w24-s5-evidence-revision-legacy-c0-s3/1", DescriptorSchemaPath = S3SchemaPath, DescriptorSchemaFileHash = HashFile(RepositoryAbsolute(S3SchemaPath)),
                CaptureToolBundlePath = CaptureBundle, CaptureToolBundleFileHash = HashFile(RepositoryAbsolute(CaptureBundle)),
                MetricsToolPath = MetricsTool, MetricsToolFileHash = HashFile(RepositoryAbsolute(MetricsTool)),
                LegacyMultiviewMinDepthSpan = LegacyMultiviewMinDepthSpan
            });
        }

        private static string ComputeRawFileSetTypedHash()
        {
            var root = RepositoryAbsolute(RawRoot);
            var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Where(path => !Relative(root, path).StartsWith("bound/", StringComparison.Ordinal))
                .OrderBy(path => Relative(root, path), StringComparer.Ordinal)
                .Select(path => new JObject { ["path"] = Relative(root, path), ["sha256"] = HashFile(path), ["byteLength"] = new FileInfo(path).Length });
            return W24TypedBinaryCanonicalEncoding.Hash(new JObject { ["schema"] = "w24-s5-sealed-file-set/1", ["files"] = new JArray(files) });
        }

        private static void AssertCleanAtomicScaffolding()
        {
            Assert.That(File.Exists(W24S5EvidenceRevisionWriter.RepositoryLockPathForTests), Is.False);
            var evidence = RepositoryAbsolute(CandidateRoot + "/evidence");
            if (Directory.Exists(evidence))
            {
                Assert.That(Directory.GetDirectories(evidence, ".E1.pending-*", SearchOption.TopDirectoryOnly), Is.Empty);
                Assert.That(Directory.GetDirectories(evidence, ".E1.rollback-*", SearchOption.TopDirectoryOnly), Is.Empty);
            }
        }

        private static W24S5EvidenceRevisionWriteRequest WriteRequest(int evidenceRevision)
        {
            return new W24S5EvidenceRevisionWriteRequest { CandidateReceiptPath = CandidateReceipt, CandidateReceiptFileHash = HashFile(RepositoryAbsolute(CandidateReceipt)), EvidenceRevision = evidenceRevision };
        }

        private static W24S5CandidateEvidenceReadRequest ReaderRequest(int evidenceRevision)
        {
            return new W24S5CandidateEvidenceReadRequest { CandidateReceiptPath = CandidateReceipt, CandidateReceiptFileHash = HashFile(RepositoryAbsolute(CandidateReceipt)), EvidenceRevision = evidenceRevision };
        }

        private static IEnumerable<string> OwnedRoots()
        {
            yield return RepositoryAbsolute("docs/vfx-candidates/" + EffectId);
            yield return RepositoryAbsolute(BootstrapContract);
            yield return RepositoryAbsolute(BootstrapTrace);
            yield return RepositoryAbsolute("artifacts/vfx-evidence/" + EffectId);
            yield return RepositoryAbsolute(InputRoot);
            yield return ProjectAbsolute(AssetRoot);
            yield return ProjectAbsolute(AssetRoot + ".meta");
            yield return ProjectAbsolute(PreviewAsset);
            yield return ProjectAbsolute(PreviewAsset + ".meta");
            yield return ProjectAbsolute(ProductionManifest);
        }

        private static void Cleanup()
        {
            if (!ownsFixturePaths) return;
            DeleteDirectory(RepositoryAbsolute("docs/vfx-candidates/" + EffectId));
            DeleteFile(RepositoryAbsolute(BootstrapContract)); DeleteFile(RepositoryAbsolute(BootstrapTrace));
            DeleteDirectory(RepositoryAbsolute("artifacts/vfx-evidence/" + EffectId)); DeleteDirectory(RepositoryAbsolute(InputRoot));
            DeleteDirectory(ProjectAbsolute(AssetRoot)); DeleteFile(ProjectAbsolute(AssetRoot + ".meta"));
            DeleteFile(ProjectAbsolute(PreviewAsset)); DeleteFile(ProjectAbsolute(PreviewAsset + ".meta")); DeleteFile(ProjectAbsolute(ProductionManifest));
        }

        private static void DeleteDirectory(string path) { if (Directory.Exists(path)) Directory.Delete(path, true); }
        private static void DeleteFile(string path) { if (File.Exists(path)) File.Delete(path); }
        private static void WriteProjectAsset(string path, byte[] bytes, string guid) { WriteProject(path, bytes); WriteProject(path + ".meta", StrictUtf8.GetBytes("fileFormatVersion: 2\nguid: " + guid + "\n")); }
        private static void WriteRepository(string path, string text) { WriteRepository(path, StrictUtf8.GetBytes(text)); }
        private static void WriteRepository(string path, byte[] bytes) { WriteNew(RepositoryAbsolute(path), bytes); }
        private static void WriteProject(string path, string text) { WriteProject(path, StrictUtf8.GetBytes(text)); }
        private static void WriteProject(string path, byte[] bytes) { WriteNew(ProjectAbsolute(path), bytes); }
        private static void WriteNew(string path, byte[] bytes) { Directory.CreateDirectory(Path.GetDirectoryName(path)); using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) stream.Write(bytes, 0, bytes.Length); }
        private static void WriteRepositoryReplace(string path, string text) { File.WriteAllText(RepositoryAbsolute(path), text, StrictUtf8); }
        private static void WriteRepositoryReplace(string path, byte[] bytes) { File.WriteAllBytes(RepositoryAbsolute(path), bytes); }
        private static string WriteRaw(string local, byte[] bytes) { WriteRepository(RawRoot + "/" + local, bytes); return HashBytes(bytes); }
        private static string WriteRawJson(string local, JObject value) { var bytes = Compact(value); WriteRepository(RawRoot + "/" + local, bytes); return HashBytes(bytes); }
        private static byte[] Compact(JToken value) { return StrictUtf8.GetBytes(value.ToString(Formatting.None)); }
        private static string Serialize(JToken value) { return value.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n"; }
        private static JObject ParseRepository(string path) { return JObject.Parse(File.ReadAllText(RepositoryAbsolute(path), StrictUtf8), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }); }
        private static string HashCanonical(JToken value) { return HashBytes(StrictUtf8.GetBytes(CanonicalJson(value))); }
        private static string CanonicalJson(JToken value) { if (value is JObject obj) { var sorted = new JObject(); foreach (var property in obj.Properties().OrderBy(item => item.Name, StringComparer.Ordinal)) sorted.Add(property.Name, JToken.Parse(CanonicalJson(property.Value))); return sorted.ToString(Formatting.None); } if (value is JArray array) return new JArray(array.Select(item => JToken.Parse(CanonicalJson(item)))).ToString(Formatting.None); return value.ToString(Formatting.None); }
        private static string HashText(string value) { return HashBytes(StrictUtf8.GetBytes(value)); }
        private static string HashFile(string path) { using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return "sha256:" + string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2", CultureInfo.InvariantCulture))); }
        private static string HashBytes(byte[] bytes) { using (var sha = SHA256.Create()) return "sha256:" + string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2", CultureInfo.InvariantCulture))); }
        private static string RawHash(string value) { return HashText(value).Substring("sha256:".Length); }
        private static string RawFileHash(string path) { return HashFile(path).Substring("sha256:".Length); }
        private static string GuidFor(string value) { return RawHash(value).Substring(0, 32); }
        private static string Forward(string path) { return Path.GetFullPath(path).Replace('\\', '/'); }
        private static string Relative(string root, string path) { return Path.GetFullPath(path).Substring(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar).Length).TrimStart(Path.DirectorySeparatorChar).Replace('\\', '/'); }
        private static string[] RelativeFiles(string root) { return Directory.GetFiles(root, "*", SearchOption.AllDirectories).Select(path => Relative(root, path)).OrderBy(path => path, StringComparer.Ordinal).ToArray(); }
        private static string[] TreeSnapshot(string root, string excluded = null) { var prefix = string.IsNullOrEmpty(excluded) ? null : excluded.TrimEnd('/') + "/"; return Directory.GetFiles(root, "*", SearchOption.AllDirectories).Select(path => new { Path = Relative(root, path), Hash = HashFile(path) }).Where(item => prefix == null || !item.Path.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(item => item.Path, StringComparer.Ordinal).Select(item => item.Path + "|" + item.Hash).ToArray(); }
        private static string ProjectRoot { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); } }
        private static string RepositoryRoot { get { return Path.GetFullPath(Path.Combine(ProjectRoot, "..")); } }
        private static string ProjectAbsolute(string path) { return Path.GetFullPath(Path.Combine(ProjectRoot, path.Replace('/', Path.DirectorySeparatorChar))); }
        private static string RepositoryAbsolute(string path) { return Path.GetFullPath(Path.Combine(RepositoryRoot, path.Replace('/', Path.DirectorySeparatorChar))); }
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
    }
}
