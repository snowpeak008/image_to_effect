using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace VFXComposer.Editor.W24.S5
{
    internal sealed class W24S5DescriptorStructureReplayRequest
    {
        internal string CandidateReceiptPath;
        internal string CandidateReceiptFileHash;
        internal int EvidenceRevision;
        internal string EvidenceRevisionDescriptorPath;
        internal string EvidenceRevisionDescriptorFileHash;
    }

    internal sealed class W24S5DescriptorStructureReplayResult
    {
        internal const string InvalidStatus = "INVALID";
        internal const string EvaluatorRuntimePendingStatus = "EVALUATOR_RUNTIME_PENDING";
        internal const string TestOnlyDescriptorStructureReplayedStatus = "TEST_ONLY_DESCRIPTOR_STRUCTURE_REPLAYED";
        internal const string EvaluatorProvenancePending = "EVALUATOR_PROVENANCE_PENDING";
        internal string Status = InvalidStatus;
        internal string EvaluatorStatus = EvaluatorProvenancePending;
        internal string EvidenceRevisionDescriptorPath;
        internal string EvidenceRevisionDescriptorFileHash;
        internal string EvidenceRevisionDescriptorSelfHash;
        internal string StructuralReplayFingerprint;
        internal readonly List<string> Errors = new List<string>();
        internal bool IsReadOnlyStructureReplay
        {
            get { return string.Equals(Status, TestOnlyDescriptorStructureReplayedStatus, StringComparison.Ordinal); }
        }
    }

#if UNITY_INCLUDE_TESTS
    internal sealed class W24S5DescriptorStructureTestRegistry
    {
        internal string EffectId;
        internal string DescriptorSchemaId;
        internal string DescriptorSchemaPath;
        internal string DescriptorSchemaFileHash;
        internal string WriterBundlePath;
        internal string WriterBundleFileHash;
        internal string WriterBundleTypedHash;
        internal string CaptureToolBundlePath;
        internal string CaptureToolBundleFileHash;
        internal string CaptureToolBundleCanonicalHash;
    }
#endif

    /// <summary>
    /// Phase-B descriptor-structure replay scaffold. Production returns pending before any I/O.
    /// Tests may install descriptor-schema, writer-bundle, and capture-tool trust roots for bounded read-only replay.
    /// No evaluator verdict, terminal artifact, receipt, or advancement authority is produced.
    /// </summary>
    internal static class W24S5MachineFailureProducer
    {
        internal const string ProducerVersion = "w24-s5-descriptor-structure-replay-scaffold/2";
        internal const string ProductionRegistryState = "EVALUATOR_RUNTIME_PENDING";
        internal const string LegacyS0bDescriptorSchema = "w24-s5-evidence-revision-legacy-c0-s0b/1";
        internal const int MaxDocumentBytes = 1024 * 1024;
        internal const int MaxSnapshotFileBytes = 16 * 1024 * 1024;
        internal const int MaxRawFileBytes = 16 * 1024 * 1024;
        internal const int MaxDescriptorFiles = 512;
        internal const int MaxDescriptorDirectories = 64;
        internal const int MaxDescriptorDepth = 8;
        internal const long MaxDescriptorTreeBytes = 160L * 1024L * 1024L;
        internal const int MaxRawFiles = 512;
        internal const int MaxRawDirectories = 256;
        internal const int MaxRawDepth = 12;
        internal const long MaxRawTreeBytes = 1024L * 1024L * 1024L;
        internal const int MaxSourceRecords = 128;
        internal const int MaxCheckRecords = 512;

        private const int MaxPathCharacters = 512;
        private const int MaxPathSegmentCharacters = 128;
        private const int MaxRevision = 1000000;
        private const string DescriptorStatus = "RAW_CAPTURE_SEALED";
        private const string LegacyRawLayout = "LEGACY_C0_FLAT_E1";
        private const string SourceSetSchema = "w24-s5-source-set/1";
        private const string SealedFileSetSchema = "w24-s5-sealed-file-set/1";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private sealed class Registry
        {
            internal string EffectId, DescriptorSchemaId, DescriptorSchemaPath, DescriptorSchemaFileHash;
            internal string WriterBundlePath, WriterBundleFileHash, WriterBundleTypedHash;
            internal string CaptureToolBundlePath, CaptureToolBundleFileHash, CaptureToolBundleCanonicalHash;
            internal bool TestOnly;
        }

        private sealed class PinnedFile { internal byte[] Bytes; internal string Hash; internal long Length; }
        private sealed class FileIdentity { internal string LocalPath; internal string Hash; internal long Length; }
        private sealed class EvidenceReplay
        {
            internal W24S5CandidateEvidenceReader.CandidateReplayAuthority Candidate;
            internal string DescriptorPath, DescriptorFileHash, DescriptorSelfHash, DescriptorSchema;
            internal string EvaluationInputTypedHash, RawFileSetTypedHash, Fingerprint;
        }
#if UNITY_INCLUDE_TESTS
        private static readonly object TestRegistrySync = new object();
        private static Registry configuredTestRegistry;
        internal static Action<string> BeforeSecondReplayForTests;
        internal static Func<string, bool> TreatPathAsReparsePointForTests;
        internal static void ConfigureRegistryForTests(W24S5DescriptorStructureTestRegistry value)
        {
            if (value == null) throw new ArgumentNullException("value");
            var registry = new Registry
            {
                EffectId = value.EffectId, DescriptorSchemaId = value.DescriptorSchemaId,
                DescriptorSchemaPath = value.DescriptorSchemaPath, DescriptorSchemaFileHash = value.DescriptorSchemaFileHash,
                WriterBundlePath = value.WriterBundlePath, WriterBundleFileHash = value.WriterBundleFileHash,
                WriterBundleTypedHash = value.WriterBundleTypedHash,
                CaptureToolBundlePath = value.CaptureToolBundlePath, CaptureToolBundleFileHash = value.CaptureToolBundleFileHash,
                CaptureToolBundleCanonicalHash = value.CaptureToolBundleCanonicalHash,
                TestOnly = true
            };
            ValidateRegistryShape(registry);
            lock (TestRegistrySync) configuredTestRegistry = registry;
        }
        internal static void ResetTestHooks()
        {
            lock (TestRegistrySync) configuredTestRegistry = null;
            BeforeSecondReplayForTests = null;
            TreatPathAsReparsePointForTests = null;
        }
#endif

        internal static W24S5DescriptorStructureReplayResult ReplayDescriptorStructure(W24S5DescriptorStructureReplayRequest request)
        {
            var result = new W24S5DescriptorStructureReplayResult();
            try
            {
                ValidateRequestShape(request);
                Registry registry;
                if (!TryResolveRegistry(out registry))
                {
                    result.Status = W24S5DescriptorStructureReplayResult.EvaluatorRuntimePendingStatus;
                    result.Errors.Add("Production descriptor/evaluator registry remains fail-closed: " + ProductionRegistryState + ".");
                    return result;
                }
                var firstReplay = ReplayEvidence(request, registry);
#if UNITY_INCLUDE_TESTS
                var beforeSecond = BeforeSecondReplayForTests;
                if (beforeSecond != null) beforeSecond(request.EvidenceRevisionDescriptorPath);
#endif
                var secondReplay = ReplayEvidence(request, registry);
                if (!Same(firstReplay.Fingerprint, secondReplay.Fingerprint))
                    throw new InvalidDataException("Candidate, descriptor, raw evidence, or snapshot structure changed between bounded read-only replays.");
                result.Status = W24S5DescriptorStructureReplayResult.TestOnlyDescriptorStructureReplayedStatus;
                result.EvaluatorStatus = W24S5DescriptorStructureReplayResult.EvaluatorProvenancePending;
                result.EvidenceRevisionDescriptorPath = secondReplay.DescriptorPath;
                result.EvidenceRevisionDescriptorFileHash = secondReplay.DescriptorFileHash;
                result.EvidenceRevisionDescriptorSelfHash = secondReplay.DescriptorSelfHash;
                result.StructuralReplayFingerprint = secondReplay.Fingerprint;
                return result;
            }
            catch (Exception error)
            {
                if (!ExpectedInputFailure(error)) throw;
                result.Errors.Add(string.IsNullOrWhiteSpace(error.Message) ? "Descriptor structure replay is invalid." : error.Message);
                return result;
            }
        }

        private static EvidenceReplay ReplayEvidence(W24S5DescriptorStructureReplayRequest request, Registry registry)
        {
            ValidateRegistryShape(registry);
            ReplaySchemaTrustRoots(registry);
            var candidateResult = W24S5CandidateEvidenceReader.ReplayCandidateOnly(new W24S5CandidateEvidenceReadRequest
            {
                CandidateReceiptPath = request.CandidateReceiptPath,
                CandidateReceiptFileHash = request.CandidateReceiptFileHash,
                EvidenceRevision = request.EvidenceRevision
            });
            if (!candidateResult.IsValidCandidateReadOnly)
                throw new InvalidDataException("Candidate-only replay did not issue immutable authority: " + string.Join(" | ", candidateResult.Errors.ToArray()));
            var candidate = candidateResult.Authority;
            if (!registry.TestOnly || !Same(registry.EffectId, candidate.EffectId))
                throw new InvalidDataException("Test-only machine-gate registry is not exact authority for this effect.");
            if (!Same(candidate.CandidateVersion, "w24-candidate/1.0") || !Same(candidate.CandidateId, "C0")
                || candidate.CandidateRevision != 0 || request.EvidenceRevision != 1)
                throw new InvalidDataException("Phase B currently accepts only real Phase-A legacy C0/E1 descriptors; E2/revisioned publication remains pending.");
            var exactDescriptorPath = candidate.CandidateRoot + "/evidence/E1/evidence-revision.json";
            if (!Same(request.EvidenceRevisionDescriptorPath, exactDescriptorPath))
                throw new InvalidDataException("Descriptor path is not the exact candidate-local E1 evidence-revision.json path.");

            var descriptorFile = ReadRepositoryPinned(request.EvidenceRevisionDescriptorPath, request.EvidenceRevisionDescriptorFileHash, "evidence revision descriptor", MaxDocumentBytes);
            var descriptor = Parse(descriptorFile.Bytes, "evidence revision descriptor");
            RequireExactly(descriptor, "schema", "descriptorStatus", "writer", "effectId", "candidateId", "candidateRevision", "contractRevision", "evidenceRevision", "candidate", "rawCapture", "captureTool", "evaluationInput", "predecessor", "selfHashEncoding", "selfHash");
            RequireExactString(descriptor, "schema", registry.DescriptorSchemaId);
            if (!Same(registry.DescriptorSchemaId, LegacyS0bDescriptorSchema))
                throw new InvalidDataException("Only the exact Phase-A S0b descriptor replay is implemented; S3 typed evaluation remains fail-closed pending.");
            RequireExactString(descriptor, "descriptorStatus", DescriptorStatus);
            RequireExactString(descriptor, "effectId", candidate.EffectId);
            RequireExactString(descriptor, "candidateId", "C0");
            if (RequiredLong(descriptor, "candidateRevision", 0, 0) != 0
                || RequiredLong(descriptor, "contractRevision", 1, MaxRevision) != candidate.ContractRevision
                || RequiredLong(descriptor, "evidenceRevision", 1, 1) != 1)
                throw new InvalidDataException("Descriptor candidate/Contract/evidence identity differs from reader authority.");
            RequireExactString(descriptor, "selfHashEncoding", W24TypedBinaryCanonicalEncoding.EncodingName);
            var descriptorSelfHash = VerifyTypedSelfHash(descriptor, "Phase-A descriptor");
            VerifyCandidateProjection(RequiredObject(descriptor, "candidate"), candidate);
            var predecessor = RequiredObject(descriptor, "predecessor");
            RequireExactly(predecessor, "kind"); RequireExactString(predecessor, "kind", "NONE");

            var expectedFiles = new HashSet<string>(StringComparer.Ordinal) { request.EvidenceRevisionDescriptorPath };
            var captureProjection = RequiredObject(descriptor, "captureTool");
            var rawReplay = ReplayRawCapture(RequiredObject(descriptor, "rawCapture"), candidate, captureProjection, registry);
            ReplayWriterSnapshots(RequiredObject(descriptor, "writer"), candidate, registry, expectedFiles);
            ReplayCaptureSnapshots(captureProjection, candidate, registry, expectedFiles);
            var evaluationInput = RequiredObject(descriptor, "evaluationInput");
            ReplayEvaluationInput(evaluationInput, candidate, registry, rawReplay);
            VerifyDescriptorFileSet(Parent(request.EvidenceRevisionDescriptorPath), expectedFiles);
            var replay = new EvidenceReplay
            {
                Candidate = candidate, DescriptorPath = request.EvidenceRevisionDescriptorPath,
                DescriptorFileHash = descriptorFile.Hash, DescriptorSelfHash = descriptorSelfHash,
                DescriptorSchema = (string)descriptor["schema"], EvaluationInputTypedHash = TypedHash(evaluationInput, null),
                RawFileSetTypedHash = rawReplay.FileSetTypedHash
            };
            replay.Fingerprint = W24TypedBinaryCanonicalEncoding.Hash(new JObject
            {
                ["schema"] = "w24-s5-descriptor-structure-replay-fingerprint/1",
                ["candidateReceiptFileHash"] = candidate.CandidateReceiptFileHash,
                ["descriptorFileHash"] = replay.DescriptorFileHash,
                ["descriptorSelfHash"] = replay.DescriptorSelfHash,
                ["rawFileSetTypedHash"] = replay.RawFileSetTypedHash,
                ["evaluationInputTypedHash"] = replay.EvaluationInputTypedHash,
                ["descriptorSchemaFileHash"] = registry.DescriptorSchemaFileHash,
                ["writerBundleFileHash"] = registry.WriterBundleFileHash,
                ["writerBundleTypedHash"] = registry.WriterBundleTypedHash,
                ["captureToolBundleFileHash"] = registry.CaptureToolBundleFileHash,
                ["captureToolBundleCanonicalHash"] = registry.CaptureToolBundleCanonicalHash
            });
            return replay;
        }

        private static void VerifyCandidateProjection(JObject value, W24S5CandidateEvidenceReader.CandidateReplayAuthority candidate)
        {
            RequireExactly(value, "receiptPath", "receiptFileHash", "receiptVersion", "contractPath", "contractFileHash", "contractHash",
                "pendingTracePath", "pendingTraceFileHash", "bootstrapManifestSnapshotPath", "bootstrapManifestSnapshotFileHash",
                "buildHash", "captureProfileHash", "runtimeEntryPath", "runtimeEntryGuid", "previewScenePath", "previewSceneFileHash");
            RequireExactPath(value, "receiptPath", candidate.CandidateReceiptPath);
            RequireExactHash(value, "receiptFileHash", candidate.CandidateReceiptFileHash);
            RequireExactString(value, "receiptVersion", candidate.CandidateVersion);
            RequireExactPath(value, "contractPath", candidate.ContractPath);
            RequireExactHash(value, "contractFileHash", candidate.ContractFileHash);
            RequireExactHash(value, "contractHash", candidate.ContractHash);
            RequireExactPath(value, "pendingTracePath", candidate.PendingTracePath);
            RequireExactHash(value, "pendingTraceFileHash", candidate.PendingTraceFileHash);
            RequireExactPath(value, "bootstrapManifestSnapshotPath", candidate.ManifestSnapshotPath);
            RequireExactHash(value, "bootstrapManifestSnapshotFileHash", candidate.ManifestSnapshotFileHash);
            RequireExactHash(value, "buildHash", candidate.BuildHash);
            RequireExactHash(value, "captureProfileHash", candidate.CaptureProfileHash);
            RequireExactPath(value, "runtimeEntryPath", candidate.RuntimeEntryPath);
            RequireExactString(value, "runtimeEntryGuid", candidate.RuntimeEntryGuid);
            RequireExactPath(value, "previewScenePath", candidate.PreviewScenePath);
            RequireExactHash(value, "previewSceneFileHash", candidate.PreviewSceneFileHash);
        }

        private static W24S5EvidenceRevisionWriter.LegacyRawReplayAuthority ReplayRawCapture(
            JObject raw,
            W24S5CandidateEvidenceReader.CandidateReplayAuthority candidate,
            JObject captureProjection,
            Registry registry)
        {
            RequireExactly(raw, "layout", "root", "captureMetadataPath", "captureMetadataFileHash", "evidenceSealPath", "evidenceSealFileHash",
                "evidenceSealHash", "evidenceLockPath", "evidenceLockFileHash", "diagnosticPassManifestPath",
                "diagnosticPassManifestFileHash", "artifactCount", "totalBytes", "fileSetTypedHash");
            RequireExactString(raw, "layout", LegacyRawLayout);
            var root = "artifacts/vfx-evidence/" + candidate.EffectId + "/C0";
            var replay = W24S5EvidenceRevisionWriter.ReplayLegacyRawReadOnly(candidate, new W24S5LegacyRawReplayPins
            {
                CaptureToolBundlePath = registry.CaptureToolBundlePath,
                CaptureToolVersion = RequiredVersion(captureProjection, "toolVersion"),
                CaptureToolCanonicalHash = RequiredHash(captureProjection, "bundleCanonicalHash"),
                AllowTypedS3Records = false
#if UNITY_INCLUDE_TESTS
                , TreatPathAsReparsePointForTests = TreatPathAsReparsePointForTests
#endif
            });
            RequireExactPath(raw, "root", root);
            RequireExactPath(raw, "captureMetadataPath", replay.CaptureMetadataPath);
            RequireExactHash(raw, "captureMetadataFileHash", replay.CaptureMetadataFileHash);
            RequireExactPath(raw, "evidenceSealPath", replay.EvidenceSealPath);
            RequireExactHash(raw, "evidenceSealFileHash", replay.EvidenceSealFileHash);
            RequireExactHash(raw, "evidenceSealHash", replay.EvidenceSealHash);
            RequireExactPath(raw, "evidenceLockPath", replay.EvidenceLockPath);
            RequireExactHash(raw, "evidenceLockFileHash", replay.EvidenceLockFileHash);
            RequireExactPath(raw, "diagnosticPassManifestPath", replay.DiagnosticManifestPath);
            RequireExactHash(raw, "diagnosticPassManifestFileHash", replay.DiagnosticManifestFileHash);
            if (RequiredLong(raw, "artifactCount", 4, MaxRawFiles) != replay.ArtifactCount
                || RequiredLong(raw, "totalBytes", 1, MaxRawTreeBytes) != replay.TotalBytes)
                throw new InvalidDataException("Raw descriptor file count/total bytes differ from shared bounded replay.");
            RequireExactHash(raw, "fileSetTypedHash", replay.FileSetTypedHash);
            return replay;
        }

        private static void ReplayWriterSnapshots(
            JObject writer,
            W24S5CandidateEvidenceReader.CandidateReplayAuthority candidate,
            Registry registry,
            HashSet<string> expectedFiles)
        {
            RequireExactly(writer, "writerId", "writerVersion", "bundleSnapshotPath", "bundleSnapshotFileHash", "bundleTypedHash",
                "sourceSnapshots", "sourceSetTypedHash", "descriptorSchemaSnapshotPath", "descriptorSchemaSnapshotFileHash");
            var revisionRoot = candidate.CandidateRoot + "/evidence/E1";
            var bundlePath = revisionRoot + "/snapshots/writer/writer.bundle.json";
            RequireExactPath(writer, "bundleSnapshotPath", bundlePath);
            var bundleFile = ReadRepositoryPinned(bundlePath, RequiredHash(writer, "bundleSnapshotFileHash"), "writer bundle snapshot", MaxDocumentBytes);
            var trustedBundleFile = ReadRepositoryPinned(registry.WriterBundlePath, registry.WriterBundleFileHash, "trusted Phase-A writer bundle", MaxDocumentBytes);
            if (!bundleFile.Bytes.SequenceEqual(trustedBundleFile.Bytes))
                throw new InvalidDataException("Writer bundle snapshot differs from the test registry trust root bytes.");
            expectedFiles.Add(bundlePath);
            var bundle = Parse(bundleFile.Bytes, "writer bundle snapshot");
            RequireExactly(bundle, "schema", "writerId", "writerVersion", "sources", "typedBundleHashEncoding", "typedBundleHash");
            RequireExactString(bundle, "schema", "w24-s5-evidence-revision-writer-bundle/1");
            RequireExactString(bundle, "writerId", RequiredBoundedToken(writer, "writerId"));
            RequireExactString(bundle, "writerVersion", RequiredVersion(writer, "writerVersion"));
            RequireExactString(bundle, "typedBundleHashEncoding", W24TypedBinaryCanonicalEncoding.EncodingName);
            var typedBundleHash = VerifyTypedFieldHash(bundle, "typedBundleHash", "writer bundle snapshot");
            RequireExactHash(writer, "bundleTypedHash", typedBundleHash);
            if (!Same(typedBundleHash, registry.WriterBundleTypedHash))
                throw new InvalidDataException("Writer bundle typed hash differs from the test registry trust root.");
            var sources = RequiredArray(bundle, "sources", 1, MaxSourceRecords);
            var snapshots = RequiredArray(writer, "sourceSnapshots", 1, MaxSourceRecords);
            var sourceSetHash = ReplaySourceSnapshots(sources, snapshots, revisionRoot + "/snapshots/writer/sources", expectedFiles, "writer");
            RequireExactHash(writer, "sourceSetTypedHash", sourceSetHash);

            var schemaName = "w24-s5-evidence-revision-legacy-c0-s0b-v1.schema.json";
            var schemaPath = revisionRoot + "/snapshots/schema/" + schemaName;
            RequireExactPath(writer, "descriptorSchemaSnapshotPath", schemaPath);
            var snapshot = ReadRepositoryPinned(schemaPath, RequiredHash(writer, "descriptorSchemaSnapshotFileHash"), "descriptor schema snapshot", MaxDocumentBytes);
            var trustRoot = ReadRepositoryPinned(registry.DescriptorSchemaPath, registry.DescriptorSchemaFileHash, "descriptor schema trust root", MaxDocumentBytes);
            if (!snapshot.Bytes.SequenceEqual(trustRoot.Bytes)) throw new InvalidDataException("Descriptor schema snapshot differs from the registry trust root.");
            expectedFiles.Add(schemaPath);
        }

        private static void ReplayCaptureSnapshots(
            JObject capture,
            W24S5CandidateEvidenceReader.CandidateReplayAuthority candidate,
            Registry registry,
            HashSet<string> expectedFiles)
        {
            RequireExactly(capture, "toolVersion", "bundleSnapshotPath", "bundleSnapshotFileHash", "bundleCanonicalHash", "sourceSnapshots", "sourceSetTypedHash");
            var revisionRoot = candidate.CandidateRoot + "/evidence/E1";
            var bundlePath = revisionRoot + "/snapshots/capture-tool/capture-tool.bundle.json";
            RequireExactPath(capture, "bundleSnapshotPath", bundlePath);
            var bundleFile = ReadRepositoryPinned(bundlePath, RequiredHash(capture, "bundleSnapshotFileHash"), "capture-tool bundle snapshot", MaxDocumentBytes);
            var trustedBundleFile = ReadRepositoryPinned(registry.CaptureToolBundlePath, registry.CaptureToolBundleFileHash, "trusted Phase-A capture-tool bundle", MaxDocumentBytes);
            if (!bundleFile.Bytes.SequenceEqual(trustedBundleFile.Bytes))
                throw new InvalidDataException("Capture-tool bundle snapshot differs from the test registry trust root bytes.");
            expectedFiles.Add(bundlePath);
            var bundle = Parse(bundleFile.Bytes, "capture-tool bundle snapshot");
            RequireExactly(bundle, "bundleVersion", "toolVersion", "sources", "configuration");
            RequireExactString(bundle, "bundleVersion", "w24-capture-tool-bundle/1");
            RequireExactString(bundle, "toolVersion", RequiredVersion(capture, "toolVersion"));
            var canonical = Hash(StrictUtf8.GetBytes(CanonicalJson(bundle)));
            RequireExactHash(capture, "bundleCanonicalHash", canonical);
            if (!Same(canonical, registry.CaptureToolBundleCanonicalHash))
                throw new InvalidDataException("Capture-tool canonical hash differs from the test registry trust root.");
            var sources = RequiredArray(bundle, "sources", 1, MaxSourceRecords);
            var snapshots = RequiredArray(capture, "sourceSnapshots", 1, MaxSourceRecords);
            var sourceSetHash = ReplaySourceSnapshots(sources, snapshots, revisionRoot + "/snapshots/capture-tool/sources", expectedFiles, "capture-tool");
            RequireExactHash(capture, "sourceSetTypedHash", sourceSetHash);
        }

        private static string ReplaySourceSnapshots(
            JArray bundleSources,
            JArray snapshots,
            string snapshotRoot,
            HashSet<string> expectedFiles,
            string label)
        {
            if (bundleSources.Count != snapshots.Count) throw new InvalidDataException(label + " source bundle/snapshot counts differ.");
            var sourceSet = new JArray();
            var paths = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < bundleSources.Count; index++)
            {
                var source = RequiredArrayObject(bundleSources[index], label + " bundle source");
                RequireExactly(source, "path", "sha256");
                var sourcePath = RequiredRepositoryPath(source, "path");
                var sourceHash = RequiredHash(source, "sha256");
                if (!paths.Add(sourcePath)) throw new InvalidDataException(label + " bundle repeats a source path.");
                var snapshot = RequiredArrayObject(snapshots[index], label + " source snapshot");
                RequireExactly(snapshot, "ordinal", "sourcePath", "sourceSha256", "snapshotPath", "snapshotFileHash");
                if (RequiredLong(snapshot, "ordinal", 0, MaxSourceRecords - 1) != index) throw new InvalidDataException(label + " source ordinal is noncanonical.");
                RequireExactPath(snapshot, "sourcePath", sourcePath);
                RequireExactHash(snapshot, "sourceSha256", sourceHash);
                var snapshotPath = snapshotRoot + "/" + index.ToString("D4", CultureInfo.InvariantCulture) + ".source";
                RequireExactPath(snapshot, "snapshotPath", snapshotPath);
                var snapshotHash = RequiredHash(snapshot, "snapshotFileHash");
                RequireExactHash(snapshot, "snapshotFileHash", sourceHash);
                ReadRepositoryPinned(snapshotPath, snapshotHash, label + " source snapshot", MaxSnapshotFileBytes);
                expectedFiles.Add(snapshotPath);
                sourceSet.Add(new JObject { ["ordinal"] = index, ["path"] = sourcePath, ["sha256"] = sourceHash });
            }
            return W24TypedBinaryCanonicalEncoding.Hash(new JObject { ["schema"] = SourceSetSchema, ["sources"] = sourceSet });
        }

        private static void ReplayEvaluationInput(
            JObject value,
            W24S5CandidateEvidenceReader.CandidateReplayAuthority candidate,
            Registry registry,
            W24S5EvidenceRevisionWriter.LegacyRawReplayAuthority raw)
        {
            if (!Same(registry.DescriptorSchemaId, LegacyS0bDescriptorSchema))
                throw new InvalidDataException("S3 render-metrics evaluator replay is not implemented; its registry remains fail-closed pending an exact typed DAG rerun.");
            var rawRoot = "artifacts/vfx-evidence/" + candidate.EffectId + "/C0";
            if (raw == null || !Same(raw.Root, rawRoot)) throw new InvalidDataException("S0b evaluation replay lacks its exact raw-capture authority.");
            RequireExactly(value, "schema", "operatorCommandPath", "operatorCommandFileHash", "semanticTelemetryPath", "semanticTelemetryFileHash",
                "receiverOffPath", "receiverOffFileHash", "receiverOnPath", "receiverOnFileHash", "receiverSummaryPath", "receiverSummaryFileHash", "replayPolicyVersion");
            RequireExactString(value, "schema", "w24-s5-eval-input-s0b-legacy/1");
            VerifyS0bEvaluationRecord(value, "operatorCommandPath", "operatorCommandFileHash", rawRoot + "/diagnostics/operator-command.json",
                raw.RequireSupplementalRecordHash("formal-capture-command", "diagnostics/operator-command.json"), "S0b operator command", MaxDocumentBytes);
            VerifyS0bEvaluationRecord(value, "semanticTelemetryPath", "semanticTelemetryFileHash", rawRoot + "/diagnostics/semantic-telemetry.json",
                raw.RequireSemanticRecordHash("semantic-telemetry", "diagnostics/semantic-telemetry.json"), "S0b semantic telemetry", MaxDocumentBytes);
            VerifyS0bEvaluationRecord(value, "receiverOffPath", "receiverOffFileHash", rawRoot + "/diagnostics/receiver-light-off.png",
                raw.RequireSupplementalRecordHash("receiver-light-off", "diagnostics/receiver-light-off.png"), "S0b receiver-off image", MaxRawFileBytes);
            VerifyS0bEvaluationRecord(value, "receiverOnPath", "receiverOnFileHash", rawRoot + "/diagnostics/receiver-light-on.png",
                raw.RequireSupplementalRecordHash("receiver-light-on", "diagnostics/receiver-light-on.png"), "S0b receiver-on image", MaxRawFileBytes);
            VerifyS0bEvaluationRecord(value, "receiverSummaryPath", "receiverSummaryFileHash", rawRoot + "/diagnostics/receiver-light-ab.json",
                raw.RequireSupplementalRecordHash("receiver-linear-luminance-ab", "diagnostics/receiver-light-ab.json"), "S0b receiver summary", MaxDocumentBytes);
            RequireExactString(value, "replayPolicyVersion", "w24-s0b-descriptor-only/1");
        }

        private static void VerifyS0bEvaluationRecord(JObject input, string pathField, string hashField, string expectedPath,
            string expectedHash, string label, int maximumBytes)
        {
            RequireExactPath(input, pathField, expectedPath);
            RequireExactHash(input, hashField, expectedHash);
            var pinned = ReplayExternalPin(input, pathField, hashField, expectedPath, label, maximumBytes);
            if (maximumBytes == MaxDocumentBytes) Parse(pinned.Bytes, label);
        }

        private static PinnedFile ReplayExternalPin(JObject value, string pathField, string hashField, string expectedPath, string label, int maximumBytes)
        {
            RequireExactPath(value, pathField, expectedPath);
            return ReadRepositoryPinned(expectedPath, RequiredHash(value, hashField), label, maximumBytes);
        }


        private static void ValidateRequestShape(W24S5DescriptorStructureReplayRequest request)
        {
            if (request == null || !SafeRepositoryPath(request.CandidateReceiptPath)
                || !request.CandidateReceiptPath.StartsWith("docs/vfx-candidates/", StringComparison.Ordinal)
                || !request.CandidateReceiptPath.EndsWith("/candidate-receipt.json", StringComparison.Ordinal)
                || !CanonicalHash(request.CandidateReceiptFileHash)
                || request.EvidenceRevision < 1 || request.EvidenceRevision > 2
                || !SafeRepositoryPath(request.EvidenceRevisionDescriptorPath)
                || !request.EvidenceRevisionDescriptorPath.EndsWith("/evidence/E" + request.EvidenceRevision.ToString(CultureInfo.InvariantCulture) + "/evidence-revision.json", StringComparison.Ordinal)
                || !CanonicalHash(request.EvidenceRevisionDescriptorFileHash))
                throw new InvalidDataException("Request must pin an exact candidate receipt and E1/E2 descriptor path plus separate physical SHA-256 hashes.");
        }

        private static bool TryResolveRegistry(out Registry registry)
        {
#if UNITY_INCLUDE_TESTS
            lock (TestRegistrySync)
            {
                if (configuredTestRegistry != null)
                {
                    registry = CloneRegistry(configuredTestRegistry);
                    return true;
                }
            }
#endif
            registry = null;
            return false;
        }

        private static Registry CloneRegistry(Registry value)
        {
            return new Registry
            {
                EffectId = value.EffectId, DescriptorSchemaId = value.DescriptorSchemaId,
                DescriptorSchemaPath = value.DescriptorSchemaPath, DescriptorSchemaFileHash = value.DescriptorSchemaFileHash,
                WriterBundlePath = value.WriterBundlePath, WriterBundleFileHash = value.WriterBundleFileHash,
                WriterBundleTypedHash = value.WriterBundleTypedHash,
                CaptureToolBundlePath = value.CaptureToolBundlePath, CaptureToolBundleFileHash = value.CaptureToolBundleFileHash,
                CaptureToolBundleCanonicalHash = value.CaptureToolBundleCanonicalHash,
                TestOnly = value.TestOnly
            };
        }

        private static void ValidateRegistryShape(Registry value)
        {
            if (value == null || !Regex.IsMatch(value.EffectId ?? string.Empty, "^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$", RegexOptions.CultureInvariant)
                || !Same(value.DescriptorSchemaId, LegacyS0bDescriptorSchema)
                || !SafeRepositoryPath(value.DescriptorSchemaPath) || !CanonicalHash(value.DescriptorSchemaFileHash)
                || !SafeRepositoryPath(value.WriterBundlePath) || !CanonicalHash(value.WriterBundleFileHash) || !CanonicalHash(value.WriterBundleTypedHash)
                || !SafeRepositoryPath(value.CaptureToolBundlePath) || !CanonicalHash(value.CaptureToolBundleFileHash) || !CanonicalHash(value.CaptureToolBundleCanonicalHash)
                || !value.TestOnly)
                throw new InvalidDataException("Test-only descriptor-structure registry shape is invalid or incomplete.");
        }

        private static void ReplaySchemaTrustRoots(Registry registry)
        {
            VerifySchemaRoot(registry.DescriptorSchemaPath, registry.DescriptorSchemaFileHash, registry.DescriptorSchemaId, "descriptor schema");
        }

        private static void VerifySchemaRoot(string path, string hash, string id, string label)
        {
            var schema = Parse(ReadRepositoryPinned(path, hash, label, MaxDocumentBytes).Bytes, label);
            if (!Same((string)schema["$schema"], "https://json-schema.org/draft/2020-12/schema")
                || !Same((string)schema["$id"], id) || !Same((string)schema["type"], "object")
                || (bool?)schema["additionalProperties"] != false)
                throw new InvalidDataException(label + " does not expose the exact compiled Draft 2020-12 trust root.");
        }

        private static void VerifyDescriptorFileSet(string revisionRoot, HashSet<string> expected)
        {
            var absoluteRoot = RepositoryAbsolute(revisionRoot);
            EnsureDirectory(absoluteRoot, "descriptor revision root", RepositoryRoot());
            var expectedDirectories = ExpectedRepositoryDirectories(revisionRoot, expected);
            var actualDirectories = new HashSet<string>(StringComparer.Ordinal) { revisionRoot };
            var files = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<Tuple<string, int>>(); pending.Push(Tuple.Create(absoluteRoot, 0));
            var directories = 1; long bytes = 0;
            while (pending.Count != 0)
            {
                var current = pending.Pop(); EnsureDirectory(current.Item1, "descriptor revision directory", RepositoryRoot());
                foreach (var entry in Directory.EnumerateFileSystemEntries(current.Item1))
                {
                    RejectReparse(entry);
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        if (++directories > MaxDescriptorDirectories || current.Item2 + 1 > MaxDescriptorDepth)
                            throw new InvalidDataException("Descriptor tree exceeds its directory/depth bound.");
                        var relativeDirectory = RepositoryRelative(entry);
                        if (!actualDirectories.Add(relativeDirectory)) throw new InvalidDataException("Descriptor tree repeats a normalized directory path.");
                        pending.Push(Tuple.Create(entry, current.Item2 + 1));
                    }
                    else
                    {
                        if (files.Count >= MaxDescriptorFiles) throw new InvalidDataException("Descriptor tree exceeds its file-count bound.");
                        var identity = HashRegularFile(entry, "descriptor tree file", MaxSnapshotFileBytes);
                        bytes = checked(bytes + identity.Length);
                        if (bytes > MaxDescriptorTreeBytes) throw new InvalidDataException("Descriptor tree exceeds its aggregate byte bound.");
                        var relative = RepositoryRelative(entry);
                        if (!files.Add(relative)) throw new InvalidDataException("Descriptor tree repeats a normalized path.");
                    }
                }
            }
            if (!files.SetEquals(expected)) throw new InvalidDataException("Descriptor tree contains a missing, extra, or path-drifted file.");
            if (!actualDirectories.SetEquals(expectedDirectories))
                throw new InvalidDataException("Descriptor tree contains a missing, extra, or empty undeclared directory.");
        }

        private static HashSet<string> ExpectedRepositoryDirectories(string revisionRoot, IEnumerable<string> files)
        {
            var output = new HashSet<string>(StringComparer.Ordinal) { revisionRoot };
            var prefix = revisionRoot + "/";
            foreach (var file in files)
            {
                if (!SafeRepositoryPath(file) || !file.StartsWith(prefix, StringComparison.Ordinal))
                    throw new InvalidOperationException("Compiled descriptor file registry escaped its revision root.");
                var local = file.Substring(prefix.Length);
                foreach (var directory in LocalDirectoryChain(local)) output.Add(prefix + directory);
            }
            return output;
        }

        private static IEnumerable<string> LocalDirectoryChain(string localFile)
        {
            var slash = localFile.LastIndexOf('/');
            while (slash > 0)
            {
                var directory = localFile.Substring(0, slash);
                yield return directory;
                slash = directory.LastIndexOf('/');
            }
        }


        private static PinnedFile ReadRepositoryPinned(string path, string hash, string label, int maximumBytes)
        {
            if (!SafeRepositoryPath(path) || !CanonicalHash(hash)) throw new InvalidDataException(label + " path/hash is unsafe.");
            return ReadAbsolutePinned(RepositoryAbsolute(path), hash, label, maximumBytes);
        }

        private static PinnedFile ReadAbsolutePinned(string absolute, string hash, string label, int maximumBytes)
        {
            if (!CanonicalHash(hash)) throw new InvalidDataException(label + " hash is not canonical.");
            var value = ReadAbsoluteUnpinned(absolute, label, maximumBytes);
            if (!Same(value.Hash, hash)) throw new InvalidDataException(label + " physical bytes differ from their immutable pin.");
            return value;
        }

        private static PinnedFile ReadAbsoluteUnpinned(string absolute, string label, int maximumBytes)
        {
            var identity = HashRegularFile(absolute, label, maximumBytes);
            if (identity.Length > int.MaxValue) throw new InvalidDataException(label + " cannot be materialized within its byte bound.");
            var bytes = new byte[(int)identity.Length];
            using (var stream = new FileStream(absolute, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var offset = 0;
                while (offset < bytes.Length)
                {
                    var read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read <= 0) throw new InvalidDataException(label + " changed length while being read.");
                    offset += read;
                }
                if (stream.ReadByte() != -1) throw new InvalidDataException(label + " grew while being read.");
            }
            RejectReparse(absolute);
            var hash = Hash(bytes);
            if (!Same(hash, identity.Hash)) throw new InvalidDataException(label + " changed while being read.");
            return new PinnedFile { Bytes = bytes, Hash = hash, Length = bytes.LongLength };
        }

        private static FileIdentity HashRegularFile(string absolute, string label, int maximumBytes)
        {
            if (!File.Exists(absolute)) throw new FileNotFoundException(label + " is missing.", absolute);
            EnsureNoReparseAtOrAbove(absolute, RepositoryRoot());
            RejectReparse(absolute);
            long length; string hash;
            using (var stream = new FileStream(absolute, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sha = SHA256.Create())
            {
                length = stream.Length;
                if (length < 0 || length > maximumBytes) throw new InvalidDataException(label + " exceeds its byte bound.");
                hash = "sha256:" + string.Concat(sha.ComputeHash(stream).Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
                if (stream.Length != length) throw new InvalidDataException(label + " changed length while hashing.");
            }
            RejectReparse(absolute);
            return new FileIdentity { Hash = hash, Length = length };
        }

        private static void EnsureDirectory(string absolute, string label, string boundary)
        {
            if (!Directory.Exists(absolute)) throw new DirectoryNotFoundException(label + " is missing: " + absolute);
            EnsureNoReparseAtOrAbove(absolute, boundary);
            var attributes = File.GetAttributes(absolute);
            if ((attributes & FileAttributes.Directory) == 0 || IsReparse(absolute, attributes))
                throw new InvalidDataException(label + " is not a regular non-reparse directory.");
        }

        private static void EnsureNoReparseAtOrAbove(string path, string boundary)
        {
            var stop = Path.GetFullPath(boundary).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var current = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            while (true)
            {
                if ((File.Exists(current) || Directory.Exists(current)) && IsReparse(current, File.GetAttributes(current)))
                    throw new InvalidDataException("Input/output path is reparse-backed.");
                if (SamePath(current, stop)) return;
                var parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || SamePath(parent, current)) throw new InvalidDataException("Path escaped its checked filesystem boundary.");
                current = parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        private static void RejectReparse(string path)
        {
            if (IsReparse(path, File.GetAttributes(path))) throw new InvalidDataException("Machine-gate input/output contains a reparse-backed entry.");
        }

        private static bool IsReparse(string path, FileAttributes attributes)
        {
#if UNITY_INCLUDE_TESTS
            var hook = TreatPathAsReparsePointForTests;
            if (hook != null && hook(Path.GetFullPath(path))) return true;
#endif
            return (attributes & FileAttributes.ReparsePoint) != 0;
        }
        private static JObject Parse(byte[] bytes, string label)
        {
            string text;
            try { text = StrictUtf8.GetString(bytes); }
            catch (DecoderFallbackException error) { throw new InvalidDataException(label + " is not strict UTF-8.", error); }
            return W24StrictJsonText.ParseObject(text, "W24 S5 machine gate " + label);
        }

        private static string CanonicalJson(JToken value)
        {
            if (value is JObject obj)
            {
                var sorted = new JObject();
                foreach (var property in obj.Properties().OrderBy(item => item.Name, StringComparer.Ordinal))
                    sorted.Add(property.Name, JToken.Parse(CanonicalJson(property.Value)));
                return sorted.ToString(Formatting.None);
            }
            if (value is JArray array) return new JArray(array.Select(item => JToken.Parse(CanonicalJson(item)))).ToString(Formatting.None);
            return value.ToString(Formatting.None);
        }

        private static string TypedHash(JObject value, string removedField)
        {
            var clone = (JObject)value.DeepClone();
            if (!string.IsNullOrEmpty(removedField)) clone.Remove(removedField);
            return W24TypedBinaryCanonicalEncoding.Hash(NormalizeTypedNumbers(clone));
        }

        private static string VerifyTypedSelfHash(JObject value, string label)
        {
            var claimed = RequiredHash(value, "selfHash");
            if (!W24TypedBinaryCanonicalEncoding.Verify(claimed, NormalizeTypedNumbers(RemoveField(value, "selfHash"))))
                throw new InvalidDataException(label + " typed self-hash is invalid.");
            return claimed;
        }

        private static string VerifyTypedFieldHash(JObject value, string field, string label)
        {
            var claimed = RequiredHash(value, field);
            if (!W24TypedBinaryCanonicalEncoding.Verify(claimed, NormalizeTypedNumbers(RemoveField(value, field))))
                throw new InvalidDataException(label + " typed hash is invalid.");
            return claimed;
        }

        private static JObject RemoveField(JObject value, string field)
        {
            var clone = (JObject)value.DeepClone(); clone.Remove(field); return clone;
        }

        private static JToken NormalizeTypedNumbers(JToken token)
        {
            if (token is JObject obj) { var copy = new JObject(); foreach (var property in obj.Properties()) copy.Add(property.Name, NormalizeTypedNumbers(property.Value)); return copy; }
            if (token is JArray array) return new JArray(array.Select(NormalizeTypedNumbers));
            if (token.Type == JTokenType.Float) return new JValue(Convert.ToDouble(((JValue)token).Value, CultureInfo.InvariantCulture));
            return token.DeepClone();
        }

        private static void RequireExactly(JObject value, params string[] fields)
        {
            if (value == null) throw new InvalidDataException("JSON object is missing.");
            var expected = new HashSet<string>(fields, StringComparer.Ordinal);
            var actual = new HashSet<string>(value.Properties().Select(item => item.Name), StringComparer.Ordinal);
            if (expected.Count != fields.Length) throw new InvalidOperationException("Compiled JSON field schema repeats a field.");
            if (!expected.SetEquals(actual)) throw new InvalidDataException("JSON object field set is not exact; expected " + string.Join(",", fields) + ".");
        }
        private static JObject RequiredObject(JObject value, string field) { var token = value[field]; if (token == null || token.Type != JTokenType.Object) throw new InvalidDataException(field + " must be an object."); return (JObject)token; }
        private static JObject RequiredArrayObject(JToken value, string label) { if (value == null || value.Type != JTokenType.Object) throw new InvalidDataException(label + " must be an object."); return (JObject)value; }
        private static JArray RequiredArray(JObject value, string field, int minimum, int maximum) { var token = value[field]; if (token == null || token.Type != JTokenType.Array) throw new InvalidDataException(field + " must be an array."); var array = (JArray)token; if (array.Count < minimum || array.Count > maximum) throw new InvalidDataException(field + " count exceeds its bound."); return array; }
        private static string RequiredString(JObject value, string field, int maximum) { var token = value[field]; if (token == null || token.Type != JTokenType.String) throw new InvalidDataException(field + " must be a string."); var text = (string)token; if (string.IsNullOrEmpty(text) || text.Length > maximum || text.Any(char.IsControl)) throw new InvalidDataException(field + " is outside its text bound."); return text; }
        private static void RequireExactString(JObject value, string field, string expected) { if (!Same(RequiredString(value, field, 4096), expected)) throw new InvalidDataException(field + " has an unsupported value."); }
        private static string RequiredHash(JObject value, string field) { var text = RequiredString(value, field, 71); if (!CanonicalHash(text)) throw new InvalidDataException(field + " is not canonical SHA-256."); return text; }
        private static string RequiredRepositoryPath(JObject value, string field) { var text = RequiredString(value, field, MaxPathCharacters); if (!SafeRepositoryPath(text)) throw new InvalidDataException(field + " is not a safe repository path."); return text; }
        private static string RequiredLocalPath(JObject value, string field) { var text = RequiredRepositoryPath(value, field); if (Same(text, "bound") || text.StartsWith("bound/", StringComparison.Ordinal)) throw new InvalidDataException(field + " is not an allowed sealed local path."); return text; }
        private static string RequiredBoundedToken(JObject value, string field) { var text = RequiredString(value, field, 96); if (!Regex.IsMatch(text, "^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant)) throw new InvalidDataException(field + " is not a bounded ASCII token."); return text; }
        private static string RequiredVersion(JObject value, string field) { var text = RequiredString(value, field, 96); if (!Regex.IsMatch(text, "^[a-z0-9][a-z0-9._-]*/[0-9]+(?:\\.[0-9]+){0,3}$", RegexOptions.CultureInvariant)) throw new InvalidDataException(field + " is not a version token."); return text; }
        private static bool RequiredBool(JObject value, string field) { var token = value[field] as JValue; if (token == null || token.Type != JTokenType.Boolean || !(token.Value is bool)) throw new InvalidDataException(field + " must be Boolean."); return (bool)token.Value; }
        private static long RequiredLong(JObject value, string field, long minimum, long maximum) { var token = value[field] as JValue; if (token == null || token.Type != JTokenType.Integer) throw new InvalidDataException(field + " must be integer."); long number; try { number = Convert.ToInt64(token.Value, CultureInfo.InvariantCulture); } catch (Exception error) when (error is InvalidCastException || error is OverflowException || error is FormatException) { throw new InvalidDataException(field + " is not a signed 64-bit integer.", error); } if (number < minimum || number > maximum) throw new InvalidDataException(field + " is outside its integer bound."); return number; }
        private static long RequiredArrayLong(JArray value, int index, long minimum, long maximum, string label) { if (value == null || index < 0 || index >= value.Count) throw new InvalidDataException(label + " is missing."); return RequiredArrayInteger(value[index], minimum, maximum, label); }
        private static long RequiredArrayInteger(JToken token, long minimum, long maximum, string label) { var scalar = token as JValue; if (scalar == null || scalar.Type != JTokenType.Integer) throw new InvalidDataException(label + " must be integer."); long number; try { number = Convert.ToInt64(scalar.Value, CultureInfo.InvariantCulture); } catch (Exception error) when (error is InvalidCastException || error is OverflowException || error is FormatException) { throw new InvalidDataException(label + " is not a signed 64-bit integer.", error); } if (number < minimum || number > maximum) throw new InvalidDataException(label + " is outside its integer bound."); return number; }
        private static double RequiredFiniteNumber(JToken token, string label) { var scalar = token as JValue; if (scalar == null || token.Type != JTokenType.Float && token.Type != JTokenType.Integer) throw new InvalidDataException(label + " must be numeric."); double number; try { number = Convert.ToDouble(scalar.Value, CultureInfo.InvariantCulture); } catch (Exception error) when (error is InvalidCastException || error is OverflowException || error is FormatException) { throw new InvalidDataException(label + " is not representable as finite Double.", error); } if (double.IsNaN(number) || double.IsInfinity(number)) throw new InvalidDataException(label + " must be finite."); return number; }
        private static string RequiredAbsolutePath(JObject value, string field) { var text = RequiredString(value, field, 4096); if (!Path.IsPathRooted(text)) throw new InvalidDataException(field + " must be an absolute recorder provenance path."); return text; }
        private static string RequiredGuid(JObject value, string field) { var text = RequiredString(value, field, 32); if (text.Length != 32 || text.Any(item => !LowerHex(item))) throw new InvalidDataException(field + " is not a canonical Unity GUID."); return text; }
        private static string RequiredRawHash(JObject value, string field) { var text = RequiredString(value, field, 64); if (text.Length != 64 || text.Any(item => !LowerHex(item))) throw new InvalidDataException(field + " is not a raw lowercase SHA-256."); return text; }
        private static void RequireExactPath(JObject value, string field, string expected) { if (!Same(RequiredRepositoryPath(value, field), expected)) throw new InvalidDataException(field + " differs from its exact persisted identity."); }
        private static void RequireExactHash(JObject value, string field, string expected) { if (!Same(RequiredHash(value, field), expected)) throw new InvalidDataException(field + " differs from its exact persisted hash."); }

        private static string RepositoryRoot() { return Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName; }
        private static string ProjectRoot() { return Directory.GetParent(Application.dataPath).FullName; }
        private static string ProjectAbsolute(string relative) { return CheckedAbsolute(ProjectRoot(), relative); }
        private static string RepositoryAbsolute(string relative) { return CheckedAbsolute(RepositoryRoot(), relative); }
        private static string CheckedAbsolute(string root, string relative) { if (!SafeRepositoryPath(relative)) throw new InvalidDataException("Repository path is unsafe."); var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar; var absolute = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))); var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal; if (!absolute.StartsWith(prefix, comparison)) throw new InvalidDataException("Repository path escaped its boundary."); return absolute; }
        private static string RepositoryRelative(string absolute) { var prefix = Path.GetFullPath(RepositoryRoot()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar; var full = Path.GetFullPath(absolute); var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal; if (!full.StartsWith(prefix, comparison)) throw new InvalidDataException("File escaped repository root."); return full.Substring(prefix.Length).Replace('\\', '/'); }
        private static bool SafeRepositoryPath(string value) { if (string.IsNullOrWhiteSpace(value) || value.Length > MaxPathCharacters || Path.IsPathRooted(value) || value.IndexOf('\\') >= 0 || value.IndexOf(':') >= 0) return false; var parts = value.Split('/'); return parts.Length > 0 && parts.All(part => part.Length > 0 && part.Length <= MaxPathSegmentCharacters && part != "." && part != ".." && part.All(AsciiPathCharacter)); }
        private static bool SafeLocalPath(string value) { return SafeRepositoryPath(value) && !value.StartsWith("/", StringComparison.Ordinal); }
        private static bool AsciiPathCharacter(char value) { return value >= 'A' && value <= 'Z' || value >= 'a' && value <= 'z' || value >= '0' && value <= '9' || value == '_' || value == '.' || value == '-'; }
        private static bool CanonicalHash(string value) { return value != null && value.Length == 71 && value.StartsWith("sha256:", StringComparison.Ordinal) && value.Substring(7).All(LowerHex); }
        private static bool LowerHex(char value) { return value >= '0' && value <= '9' || value >= 'a' && value <= 'f'; }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return "sha256:" + string.Concat(sha.ComputeHash(bytes).Select(item => item.ToString("x2", CultureInfo.InvariantCulture))); }
        private static string Parent(string path) { var value = Path.GetDirectoryName(path); if (string.IsNullOrEmpty(value)) throw new InvalidDataException("Repository path has no parent."); return value.Replace('\\', '/'); }
        private static bool Same(string left, string right) { return string.Equals(left, right, StringComparison.Ordinal); }
        private static bool SameExactAbsolutePath(string left, string right) { try { return string.Equals(Path.GetFullPath(left).Replace('\\', '/').TrimEnd('/'), Path.GetFullPath(right).Replace('\\', '/').TrimEnd('/'), StringComparison.Ordinal); } catch { return false; } }
        private static bool SamePath(string left, string right) { try { var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal; return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), comparison); } catch { return false; } }
        private static bool ExpectedInputFailure(Exception error) { return error is InvalidDataException || error is IOException || error is UnauthorizedAccessException || error is SecurityException || error is JsonException || error is FormatException || error is OverflowException || error is InvalidCastException || error is NotSupportedException || error is ArgumentException || error is CryptographicException || error is DecoderFallbackException || error is EncoderFallbackException; }
    }
}
