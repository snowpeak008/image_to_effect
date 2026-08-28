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
    /// <summary>The complete production request surface for an evidence-revision write.</summary>
    internal sealed class W24S5EvidenceRevisionWriteRequest
    {
        internal string CandidateReceiptPath;
        internal string CandidateReceiptFileHash;
        internal int EvidenceRevision;
    }

    /// <summary>A descriptor publication result. It is never a PASS/FAIL or terminal authority.</summary>
    internal sealed class W24S5EvidenceRevisionWriteResult
    {
        internal const string InvalidStatus = "INVALID";
        internal const string RegistryPendingStatus = "REGISTRY_PENDING";
        internal const string TestOnlyDescriptorWriterStatus = "TEST_ONLY_DESCRIPTOR_WRITER";
        internal const string PublicationRollbackFatalStatus = "PUBLICATION_ROLLBACK_FATAL";

        internal string Status = InvalidStatus;
        internal string DescriptorPath;
        internal string DescriptorFileHash;
        internal readonly List<string> Errors = new List<string>();
        internal bool Succeeded
        {
            get { return string.Equals(Status, TestOnlyDescriptorWriterStatus, StringComparison.Ordinal); }
        }
    }

    /// <summary>
    /// Caller-supplied trust pins for the shared legacy raw replay.  These pins are not authority;
    /// successful replay still requires a reader-issued candidate capability and exact raw bytes.
    /// </summary>
    internal sealed class W24S5LegacyRawReplayPins
    {
        internal string CaptureToolBundlePath;
        internal string CaptureToolVersion;
        internal string CaptureToolCanonicalHash;
        internal bool AllowTypedS3Records;
#if UNITY_INCLUDE_TESTS
        internal Func<string, bool> TreatPathAsReparsePointForTests;
#endif
    }

#if UNITY_INCLUDE_TESTS
    /// <summary>
    /// Test-compiled registry pins. Production deliberately has no populated registry in Phase A.
    /// Tests must provide every hash; configuring paths alone never creates trust.
    /// </summary>
    internal sealed class W24S5EvidenceRevisionTestRegistry
    {
        internal string EffectId;
        internal string Route;
        internal string WriterId;
        internal string WriterVersion;
        internal string WriterBundlePath;
        internal string WriterBundleFileHash;
        internal string WriterBundleTypedHash;
        internal string DescriptorSchemaId;
        internal string DescriptorSchemaPath;
        internal string DescriptorSchemaFileHash;
        internal string CaptureToolBundlePath;
        internal string CaptureToolBundleFileHash;
        internal string MetricsToolPath;
        internal string MetricsToolFileHash;
        internal double? LegacyMultiviewMinDepthSpan;
    }
#endif

    /// <summary>
    /// Phase-A writer for one immutable, pre-verdict evidence descriptor. It only accepts a
    /// reader-issued candidate replay authority, replays the legacy raw seal, snapshots every
    /// executable/schema input, and atomically publishes E1. It cannot evaluate, transition,
    /// write terminal records, or issue candidate-advance authority.
    /// </summary>
    internal static class W24S5EvidenceRevisionWriter
    {
        internal const string WriterVersion = "w24-s5-evidence-revision-writer/1";
        internal const string SharedLegacyRawReplayVersion = "w24-s5-shared-legacy-raw-replay/1";
        internal const string S0bRoute = "LEGACY_C0_S0B";
        internal const string S3Route = "LEGACY_C0_S3";
        internal const string ProductionRegistryState = "REGISTRY_PENDING";
        internal const string DescriptorName = "evidence-revision.json";
        internal const string RepositoryLockName = ".w24-s5-evidence-revision-writer.lock";

        private const string LegacyCandidateVersion = "w24-candidate/1.0";
        private const string S0bSchema = "w24-s5-evidence-revision-legacy-c0-s0b/1";
        private const string S3Schema = "w24-s5-evidence-revision-legacy-c0-s3/1";
        private const string DescriptorStatus = "RAW_CAPTURE_SEALED";
        private const string LegacyRawLayout = "LEGACY_C0_FLAT_E1";
        private const string CaptureMetadataSchema = "w24-s0a-capture-evidence/v1";
        private const string EvidenceSealSchema = "w24-s0a-final-evidence-seal/v1";
        private const string EvidenceLockSchema = "w24-s0a-evidence-lock/v1";
        private const string DiagnosticManifestSchema = "w24-s0a-diagnostic-pass-manifest/v1";
        private const string MetricsInputSchema = "w24-render-metrics-input/v1";
        private const string MetricsReportSchema = "w24-render-metrics-report/v1";
        private const string SourceSetSchema = "w24-s5-source-set/1";
        private const string SealedFileSetSchema = "w24-s5-sealed-file-set/1";
        private const string TypedRawSetSchema = "w24-s5-typed-raw-set/1";
        private const string S0bReplayPolicy = "w24-s0b-descriptor-only/1";
        private const string WriterBundleSchema = "w24-s5-evidence-revision-writer-bundle/1";

        private const int MaxJsonBytes = 1024 * 1024;
        private const int MaxSnapshotSourceBytes = 16 * 1024 * 1024;
        private const int MaxRawFileBytes = 16 * 1024 * 1024;
        private const int MaxPythonExecutableBytes = 64 * 1024 * 1024;
        private const int MaxRawFiles = 512;
        private const int MaxRawDirectories = 256;
        private const int MaxRawDepth = 12;
        private const long MaxRawBytes = 1024L * 1024L * 1024L;
        private const int MaxSourceRecords = 128;
        private const int MaxRecordCount = 512;
        private const int MaxPathCharacters = 512;
        private const int MaxPathSegmentCharacters = 128;
        private const int MaxDescriptorTokenCharacters = 96;
        private const int MaxTextCharacters = 1024 * 1024;
        private const int MaxRevision = 1000000;
        private const long MaxSnapshotSourceAggregateBytes = 128L * 1024L * 1024L;
        private const long MaxPreparedTreeBytes = 160L * 1024L * 1024L;
        private const long MaxRequestReplayBytes = MaxRawBytes + MaxPreparedTreeBytes;

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly object LegacyRawReplayAuthorityIssuer = new object();

        private sealed class Registry
        {
            internal string EffectId;
            internal string Route;
            internal string WriterId;
            internal string WriterVersion;
            internal string WriterBundlePath;
            internal string WriterBundleFileHash;
            internal string WriterBundleTypedHash;
            internal string DescriptorSchemaId;
            internal string DescriptorSchemaPath;
            internal string DescriptorSchemaFileHash;
            internal string CaptureToolBundlePath;
            internal string CaptureToolBundleFileHash;
            internal string MetricsToolPath;
            internal string MetricsToolFileHash;
            internal double? LegacyMultiviewMinDepthSpan;
            internal bool TestOnly;
        }

        private sealed class PinnedFile
        {
            internal byte[] Bytes;
            internal string Hash;
            internal long Length;
        }

        private sealed class FileIdentity
        {
            internal string LocalPath;
            internal string Hash;
            internal long Length;
        }

        private sealed class SourceReplay
        {
            internal int Ordinal;
            internal string Path;
            internal string Hash;
            internal byte[] Bytes;
        }

        private sealed class BundleReplay
        {
            internal string ToolVersion;
            internal string CanonicalHash;
            internal byte[] Bytes;
            internal string FileHash;
            internal readonly List<SourceReplay> Sources = new List<SourceReplay>();
        }

        private sealed class WriterReplay
        {
            internal string WriterId;
            internal string WriterVersion;
            internal string TypedBundleHash;
            internal byte[] Bytes;
            internal string FileHash;
            internal readonly List<SourceReplay> Sources = new List<SourceReplay>();
        }

        private sealed class MetadataReplay
        {
            internal JObject Metadata;
            internal JObject Contract;
            internal JObject Manifest;
            internal JObject SourceHashes;
            internal string RecorderCaptureProfileHash;
            internal readonly Dictionary<string, string> SealArtifacts = new Dictionary<string, string>(StringComparer.Ordinal);
            internal readonly Dictionary<string, JObject> TypedRawByFile = new Dictionary<string, JObject>(StringComparer.Ordinal);
            internal readonly List<JObject> MetricInputs = new List<JObject>();
            internal readonly List<JObject> MetricReports = new List<JObject>();
            internal readonly List<JObject> SemanticTelemetry = new List<JObject>();
            internal readonly List<JObject> SupplementalDiagnostics = new List<JObject>();
        }

        private sealed class RawReplay
        {
            internal string Root;
            internal string CaptureMetadataPath;
            internal string CaptureMetadataFileHash;
            internal string EvidenceSealPath;
            internal string EvidenceSealFileHash;
            internal string EvidenceSealHash;
            internal string EvidenceLockPath;
            internal string EvidenceLockFileHash;
            internal string DiagnosticManifestPath;
            internal string DiagnosticManifestFileHash;
            internal int ArtifactCount;
            internal long TotalBytes;
            internal string FileSetTypedHash;
            internal MetadataReplay Metadata;
            internal Dictionary<string, FileIdentity> Files;
        }

        /// <summary>
        /// Private-issuer structural replay capability shared with Phase B.  It intentionally
        /// exposes only immutable scalar projections and exact-record hash lookups; mutable JSON,
        /// raw bytes, evaluator outcomes, routes, terminal records, and transition authority never
        /// cross this boundary.
        /// </summary>
        internal sealed class LegacyRawReplayAuthority
        {
            private readonly RawReplay value;

            internal LegacyRawReplayAuthority(object issuer, object replay)
            {
                if (!ReferenceEquals(issuer, LegacyRawReplayAuthorityIssuer))
                    throw new InvalidOperationException("Legacy raw replay authority is writer-issued only.");
                value = replay as RawReplay;
                if (value == null) throw new ArgumentNullException("replay");
            }

            internal string Root { get { return value.Root; } }
            internal string CaptureMetadataPath { get { return value.CaptureMetadataPath; } }
            internal string CaptureMetadataFileHash { get { return value.CaptureMetadataFileHash; } }
            internal string EvidenceSealPath { get { return value.EvidenceSealPath; } }
            internal string EvidenceSealFileHash { get { return value.EvidenceSealFileHash; } }
            internal string EvidenceSealHash { get { return value.EvidenceSealHash; } }
            internal string EvidenceLockPath { get { return value.EvidenceLockPath; } }
            internal string EvidenceLockFileHash { get { return value.EvidenceLockFileHash; } }
            internal string DiagnosticManifestPath { get { return value.DiagnosticManifestPath; } }
            internal string DiagnosticManifestFileHash { get { return value.DiagnosticManifestFileHash; } }
            internal int ArtifactCount { get { return value.ArtifactCount; } }
            internal long TotalBytes { get { return value.TotalBytes; } }
            internal string FileSetTypedHash { get { return value.FileSetTypedHash; } }

            internal string RequireSemanticRecordHash(string kind, string file)
            {
                return RequiredHash(SingleRecord(value.Metadata.SemanticTelemetry, "kind", kind, file), "sha256");
            }

            internal string RequireSupplementalRecordHash(string kind, string file)
            {
                return RequiredHash(SingleRecord(value.Metadata.SupplementalDiagnostics, "kind", kind, file), "sha256");
            }

        }

        private sealed class S3Replay
        {
            internal string MetricsInputPath;
            internal string MetricsInputHash;
            internal string MetricsReportPath;
            internal string MetricsReportHash;
            internal string RequiredMatrixHash;
            internal string TypedRawSetHash;
            internal JObject Environment;
            internal byte[] MetricsToolBytes;
            internal string MetricsToolHash;
        }

        private sealed class Prepared
        {
            internal string DescriptorRelativePath;
            internal string DescriptorHash;
            internal readonly Dictionary<string, byte[]> Files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            internal readonly Dictionary<string, FileIdentity> ExpectedFiles = new Dictionary<string, FileIdentity>(StringComparer.Ordinal);
            internal string Route;
            internal string SchemaId;
            internal string EffectId;
            internal int ContractRevision;
            internal string CandidateRoot;
            internal string Fingerprint;

            internal void ReleasePayloads()
            {
                Files.Clear();
            }
        }

        private sealed class PrepareBudget
        {
            internal long RequestBytes;
            internal long SourceBytes;
            internal long PreparedBytes;

            internal void AddRequest(long bytes, string label)
            {
                if (bytes < 0) throw new InvalidDataException(label + " has a negative byte length.");
                RequestBytes = checked(RequestBytes + bytes);
                if (RequestBytes > EffectiveBudget(MaxRequestReplayBytes))
                    throw new InvalidDataException("Evidence-revision request exceeds its aggregate replay-byte budget.");
            }

            internal void AddSource(long bytes, string label)
            {
                SourceBytes = checked(SourceBytes + bytes);
                if (SourceBytes > EffectiveBudget(MaxSnapshotSourceAggregateBytes))
                    throw new InvalidDataException("Evidence-revision source snapshots exceed their aggregate byte budget.");
                AddRequest(bytes, label);
            }

            internal void AddPrepared(long bytes)
            {
                PreparedBytes = checked(PreparedBytes + bytes);
                if (PreparedBytes > EffectiveBudget(MaxPreparedTreeBytes))
                    throw new InvalidDataException("Evidence-revision prepared tree exceeds its aggregate byte budget.");
            }
        }

#if UNITY_INCLUDE_TESTS
        private static readonly object TestRegistrySync = new object();
        private static Registry configuredTestRegistry;
        internal static Action<string> BeforeSecondReplayForTests;
        internal static Action<string> AfterPublishMoveForTests;
        internal static Action<string> BeforeQuarantineMoveForTests;
        internal static Action<string> AfterLockCreateForTests;
        internal static Func<string, bool> TreatPathAsReparsePointForTests;
        internal static long? AggregateBudgetLimitForTests;

        [ThreadStatic] private static Func<string, bool> activeSharedRawReparseHook;

        internal static void ConfigureRegistryForTests(W24S5EvidenceRevisionTestRegistry value)
        {
            if (value == null) throw new ArgumentNullException("value");
            var copy = new Registry
            {
                EffectId = value.EffectId,
                Route = value.Route,
                WriterId = value.WriterId,
                WriterVersion = value.WriterVersion,
                WriterBundlePath = value.WriterBundlePath,
                WriterBundleFileHash = value.WriterBundleFileHash,
                WriterBundleTypedHash = value.WriterBundleTypedHash,
                DescriptorSchemaId = value.DescriptorSchemaId,
                DescriptorSchemaPath = value.DescriptorSchemaPath,
                DescriptorSchemaFileHash = value.DescriptorSchemaFileHash,
                CaptureToolBundlePath = value.CaptureToolBundlePath,
                CaptureToolBundleFileHash = value.CaptureToolBundleFileHash,
                MetricsToolPath = value.MetricsToolPath,
                MetricsToolFileHash = value.MetricsToolFileHash,
                LegacyMultiviewMinDepthSpan = value.LegacyMultiviewMinDepthSpan,
                TestOnly = true
            };
            ValidateRegistryShape(copy);
            lock (TestRegistrySync) configuredTestRegistry = copy;
        }

        internal static void VerifyMetricCheckContractProjectionForTests(JObject check, JObject block, JArray evidence, double legacyMultiviewMinDepthSpan)
        {
            if (check == null || block == null || evidence == null) throw new ArgumentNullException("test metric projection input");
            if (double.IsNaN(legacyMultiviewMinDepthSpan) || double.IsInfinity(legacyMultiviewMinDepthSpan))
                throw new InvalidDataException("Test legacy multiview capture policy must be finite.");
            var registry = new Dictionary<string, JObject>(StringComparer.Ordinal);
            foreach (var token in evidence)
            {
                var item = RequiredArrayObject(token, "test metric evidence");
                var id = RequiredMetricToken(item, "id");
                if (registry.ContainsKey(id)) throw new InvalidDataException("Test metric evidence repeats an ID.");
                registry.Add(id, item);
            }
            var kind = RequiredMetricToken(check, "kind");
            if (!KnownMetricKind(kind)) throw new InvalidDataException("Test metric projection kind is unsupported.");
            var plan = RequiredObject(block, "metricPlan");
            RequireExactMetricPlanInputFields(plan, kind);
            RequireCheckIdMatchesPlan(RequiredMetricToken(check, "id"), plan);
            var consumed = new HashSet<string>(StringComparer.Ordinal);
            CollectExactMetricEvidenceReferences(check, kind, registry, consumed, block, legacyMultiviewMinDepthSpan);
            if (!new HashSet<string>(registry.Keys, StringComparer.Ordinal).SetEquals(consumed))
                throw new InvalidDataException("Test metric projection does not consume its exact evidence set.");
        }

        internal static void VerifyMetricsReportForTests(JObject report, JObject input, string inputHash, string expectedTool)
        {
            VerifyMetricsReport(report, input, inputHash, expectedTool);
        }

        internal static void ResetTestHooks()
        {
            lock (TestRegistrySync) configuredTestRegistry = null;
            BeforeSecondReplayForTests = null;
            AfterPublishMoveForTests = null;
            BeforeQuarantineMoveForTests = null;
            AfterLockCreateForTests = null;
            TreatPathAsReparsePointForTests = null;
            AggregateBudgetLimitForTests = null;
            activeSharedRawReparseHook = null;
        }

        internal static string RepositoryLockPathForTests
        {
            get { return RepositoryLockPath(); }
        }
#endif

        /// <summary>
        /// Shared read-only facade used by Phase A and Phase B.  It replays the one authoritative
        /// raw validator and returns only a private-issuer structural capability.
        /// </summary>
        internal static LegacyRawReplayAuthority ReplayLegacyRawReadOnly(
            W24S5CandidateEvidenceReader.CandidateReplayAuthority authority,
            W24S5LegacyRawReplayPins pins)
        {
            return ReplayLegacyRawReadOnly(authority, pins, new PrepareBudget());
        }

        private static LegacyRawReplayAuthority ReplayLegacyRawReadOnly(
            W24S5CandidateEvidenceReader.CandidateReplayAuthority authority,
            W24S5LegacyRawReplayPins pins,
            PrepareBudget budget)
        {
            ValidateLegacyRawPins(authority, pins);
#if UNITY_INCLUDE_TESTS
            var previousHook = activeSharedRawReparseHook;
            activeSharedRawReparseHook = pins.TreatPathAsReparsePointForTests;
#endif
            try
            {
                return new LegacyRawReplayAuthority(LegacyRawReplayAuthorityIssuer, ReplayLegacyRaw(authority, pins, budget));
            }
            finally
            {
#if UNITY_INCLUDE_TESTS
                activeSharedRawReparseHook = previousHook;
#endif
            }
        }

        internal static W24S5EvidenceRevisionWriteResult Write(W24S5EvidenceRevisionWriteRequest request)
        {
            var result = new W24S5EvidenceRevisionWriteResult();
            string pending = null;
            string pendingParent = null;
            string publishedTarget = null;
            FileStream lockStream = null;
            Exception publicationFailure = null;
            try
            {
                ValidateRequest(request);
                if (request.EvidenceRevision != 1)
                    throw new InvalidDataException("Phase A rejects E2: no opaque recapture authority is accepted or constructible by this writer.");
                if (LooksRevisionedCandidatePath(request.CandidateReceiptPath))
                    throw new InvalidDataException("Phase A rejects C1/C2 test-only candidate transaction namespaces.");

                var replay = W24S5CandidateEvidenceReader.ReplayCandidateOnly(ToReaderRequest(request));
                if (!replay.IsValidCandidateReadOnly)
                    throw new InvalidDataException("Candidate-only replay did not issue immutable read-only authority.");
                var authority = replay.Authority;
                RequireLegacyC0Authority(authority);

                Registry registry;
                if (!TryResolveRegistry(authority.EffectId, out registry))
                {
                    result.Status = W24S5EvidenceRevisionWriteResult.RegistryPendingStatus;
                    result.Errors.Add("Production evidence-revision registry is fail-closed: REGISTRY_PENDING.");
                    return result;
                }

                var targetRelative = authority.CandidateRoot + "/evidence/E1";
                var target = RepositoryAbsolute(targetRelative);
                pendingParent = RepositoryAbsolute(authority.CandidateRoot + "/evidence");
                var lockPath = RepositoryLockPath();
                EnsureDirectory(RepositoryAbsolute("docs/vfx-candidates"), "candidate registry root", RepositoryRoot());
                lockStream = AcquireLock(lockPath, request);

                EnsureDirectory(RepositoryAbsolute(authority.CandidateRoot), "candidate root", RepositoryRoot());
                EnsureNoReparseAtOrAbove(Path.GetDirectoryName(pendingParent), RepositoryRoot());
                Directory.CreateDirectory(pendingParent);
                EnsureDirectory(pendingParent, "candidate evidence root", RepositoryRoot());
                if (Directory.Exists(target) || File.Exists(target))
                    throw new IOException("Evidence revision E1 is write-once and already exists.");

                pending = Path.Combine(pendingParent, ".E1.pending-" + Guid.NewGuid().ToString("N"));
                if (!IsDirectPendingChild(pending, pendingParent)) throw new InvalidDataException("Pending descriptor path is outside its exact evidence parent.");
                Directory.CreateDirectory(pending);
                EnsureDirectory(pending, "descriptor pending directory", RepositoryRoot());

                var first = Prepare(authority, registry);
                WritePreparedTree(first, pending);
                first.ReleasePayloads();
                VerifyPreparedTree(first, pending);

#if UNITY_INCLUDE_TESTS
                var hook = BeforeSecondReplayForTests;
                if (hook != null) hook(first.DescriptorRelativePath);
#endif

                var secondReplay = W24S5CandidateEvidenceReader.ReplayCandidateOnly(ToReaderRequest(request));
                if (!secondReplay.IsValidCandidateReadOnly)
                    throw new InvalidDataException("Candidate bytes changed before descriptor publication.");
                RequireLegacyC0Authority(secondReplay.Authority);
                var second = Prepare(secondReplay.Authority, registry);
                var samePreparedInputs = Same(first.Fingerprint, second.Fingerprint);
                second.ReleasePayloads();
                if (!samePreparedInputs)
                    throw new InvalidDataException("Candidate, raw capture, bundle, schema, tool, or environment input changed before publication.");
                VerifyPreparedTree(first, pending);
                EnsureDirectory(pendingParent, "candidate evidence root before publish", RepositoryRoot());
                EnsureDirectory(pending, "descriptor pending directory before publish", RepositoryRoot());
                if (Directory.Exists(target) || File.Exists(target)) throw new IOException("Evidence revision E1 appeared during publication.");

                Directory.Move(pending, target);
                pending = null;
                publishedTarget = target;
#if UNITY_INCLUDE_TESTS
                var afterMove = AfterPublishMoveForTests;
                if (afterMove != null) afterMove(target);
#endif
                VerifyPreparedTree(first, target);
                publishedTarget = null;
                result.Status = W24S5EvidenceRevisionWriteResult.TestOnlyDescriptorWriterStatus;
                result.DescriptorPath = first.DescriptorRelativePath;
                result.DescriptorFileHash = first.DescriptorHash;
                return result;
            }
            catch (Exception error)
            {
                publicationFailure = error;
                if (!ExpectedInputFailure(error)) throw;
                result.Errors.Add(string.IsNullOrWhiteSpace(error.Message) ? "Evidence revision descriptor write is invalid." : error.Message);
                return result;
            }
            finally
            {
                Exception rollbackFailure = null;
                if (!string.IsNullOrEmpty(publishedTarget))
                {
                    try { pending = QuarantineOwnedPublishedTarget(publishedTarget, pendingParent); }
                    catch (Exception error) { rollbackFailure = error; }
                }
                TryDeleteOwnedPending(pending, pendingParent);
                try { if (lockStream != null) lockStream.Dispose(); }
                catch (Exception error)
                {
                    if (rollbackFailure == null) throw;
                    rollbackFailure = new AggregateException("Publication rollback and writer-lock disposal both failed.", rollbackFailure, error);
                }
                if (rollbackFailure != null)
                {
                    var message = W24S5EvidenceRevisionWriteResult.PublicationRollbackFatalStatus
                        + ": post-Move verification failed and the invocation-owned E1 could not be moved out of the formal namespace.";
                    if (publicationFailure != null) throw new AggregateException(message, publicationFailure, rollbackFailure);
                    throw new InvalidOperationException(message, rollbackFailure);
                }
            }
        }

        private static Prepared Prepare(W24S5CandidateEvidenceReader.CandidateReplayAuthority authority, Registry registry)
        {
            ValidateRegistryForAuthority(registry, authority);
            var budget = new PrepareBudget();
            var writer = ReplayWriterBundle(registry, budget);
            var capture = ReplayCaptureBundle(registry, authority, budget);
            var raw = ReplayLegacyRaw(authority, LegacyRawPins(registry, capture), budget);
            var revisionRoot = authority.CandidateRoot + "/evidence/E1";
            var prepared = new Prepared
            {
                DescriptorRelativePath = revisionRoot + "/" + DescriptorName,
                Route = registry.Route,
                SchemaId = registry.DescriptorSchemaId,
                EffectId = authority.EffectId,
                ContractRevision = authority.ContractRevision,
                CandidateRoot = authority.CandidateRoot
            };

            var writerBundleSnapshot = revisionRoot + "/snapshots/writer/writer.bundle.json";
            AddPreparedFile(prepared, budget, LocalSnapshotPath(writerBundleSnapshot, revisionRoot), writer.Bytes);
            var writerSnapshots = SnapshotSources(prepared, budget, revisionRoot, revisionRoot + "/snapshots/writer/sources", writer.Sources);
            var schemaFileName = registry.Route == S0bRoute
                ? "w24-s5-evidence-revision-legacy-c0-s0b-v1.schema.json"
                : "w24-s5-evidence-revision-legacy-c0-s3-v1.schema.json";
            var schemaSnapshot = revisionRoot + "/snapshots/schema/" + schemaFileName;
            var schemaBytes = ReadRepositoryPinned(registry.DescriptorSchemaPath, registry.DescriptorSchemaFileHash, "compiled descriptor schema", MaxJsonBytes).Bytes;
            VerifySchemaTrustRoot(schemaBytes, registry.DescriptorSchemaId);
            budget.AddRequest(schemaBytes.LongLength, "compiled descriptor schema");
            AddPreparedFile(prepared, budget, LocalSnapshotPath(schemaSnapshot, revisionRoot), schemaBytes);

            var captureBundleSnapshot = revisionRoot + "/snapshots/capture-tool/capture-tool.bundle.json";
            AddPreparedFile(prepared, budget, LocalSnapshotPath(captureBundleSnapshot, revisionRoot), capture.Bytes);
            var captureSnapshots = SnapshotSources(prepared, budget, revisionRoot, revisionRoot + "/snapshots/capture-tool/sources", capture.Sources);

            JObject evaluation;
            S3Replay s3 = null;
            if (registry.Route == S0bRoute)
            {
                evaluation = BuildS0bEvaluation(raw);
            }
            else
            {
                s3 = ReplayS3(authority, registry, capture, raw, budget);
                var toolSnapshot = revisionRoot + "/snapshots/evaluation/render_metrics.py";
                var environmentSnapshot = revisionRoot + "/snapshots/evaluation/metrics-environment.json";
                AddPreparedFile(prepared, budget, LocalSnapshotPath(toolSnapshot, revisionRoot), s3.MetricsToolBytes);
                var environmentBytes = Serialize(s3.Environment);
                AddPreparedFile(prepared, budget, LocalSnapshotPath(environmentSnapshot, revisionRoot), environmentBytes);
                evaluation = new JObject
                {
                    ["schema"] = "w24-s5-eval-input-s3-render-metrics/1",
                    ["metricsInputPath"] = s3.MetricsInputPath,
                    ["metricsInputFileHash"] = s3.MetricsInputHash,
                    ["capturedMetricsReportPath"] = s3.MetricsReportPath,
                    ["capturedMetricsReportFileHash"] = s3.MetricsReportHash,
                    ["metricsToolSnapshotPath"] = toolSnapshot,
                    ["metricsToolSnapshotFileHash"] = s3.MetricsToolHash,
                    ["metricsEnvironmentPath"] = environmentSnapshot,
                    ["metricsEnvironmentFileHash"] = Hash(environmentBytes),
                    ["requiredEvidenceMatrixHash"] = s3.RequiredMatrixHash,
                    ["typedRawSetHash"] = s3.TypedRawSetHash
                };
            }

            var descriptor = new JObject
            {
                ["schema"] = registry.DescriptorSchemaId,
                ["descriptorStatus"] = DescriptorStatus,
                ["writer"] = new JObject
                {
                    ["writerId"] = writer.WriterId,
                    ["writerVersion"] = writer.WriterVersion,
                    ["bundleSnapshotPath"] = writerBundleSnapshot,
                    ["bundleSnapshotFileHash"] = writer.FileHash,
                    ["bundleTypedHash"] = writer.TypedBundleHash,
                    ["sourceSnapshots"] = writerSnapshots,
                    ["sourceSetTypedHash"] = SourceSetTypedHash(writer.Sources),
                    ["descriptorSchemaSnapshotPath"] = schemaSnapshot,
                    ["descriptorSchemaSnapshotFileHash"] = Hash(schemaBytes)
                },
                ["effectId"] = authority.EffectId,
                ["candidateId"] = authority.CandidateId,
                ["candidateRevision"] = authority.CandidateRevision,
                ["contractRevision"] = authority.ContractRevision,
                ["evidenceRevision"] = 1,
                ["candidate"] = BuildCandidateProjection(authority),
                ["rawCapture"] = BuildRawProjection(raw),
                ["captureTool"] = new JObject
                {
                    ["toolVersion"] = capture.ToolVersion,
                    ["bundleSnapshotPath"] = captureBundleSnapshot,
                    ["bundleSnapshotFileHash"] = capture.FileHash,
                    ["bundleCanonicalHash"] = capture.CanonicalHash,
                    ["sourceSnapshots"] = captureSnapshots,
                    ["sourceSetTypedHash"] = SourceSetTypedHash(capture.Sources)
                },
                ["evaluationInput"] = evaluation,
                ["predecessor"] = new JObject { ["kind"] = "NONE" },
                ["selfHashEncoding"] = W24TypedBinaryCanonicalEncoding.EncodingName
            };
            descriptor["selfHash"] = TypedHash(descriptor);
            ValidateDescriptorCompiledSemantics(descriptor, prepared);
            var descriptorBytes = Serialize(descriptor);
            prepared.DescriptorHash = Hash(descriptorBytes);
            AddPreparedFile(prepared, budget, DescriptorName, descriptorBytes);
            prepared.Fingerprint = PreparedFingerprint(prepared);
            return prepared;
        }

        private static JObject BuildCandidateProjection(W24S5CandidateEvidenceReader.CandidateReplayAuthority value)
        {
            return new JObject
            {
                ["receiptPath"] = value.CandidateReceiptPath,
                ["receiptFileHash"] = value.CandidateReceiptFileHash,
                ["receiptVersion"] = value.CandidateVersion,
                ["contractPath"] = value.ContractPath,
                ["contractFileHash"] = value.ContractFileHash,
                ["contractHash"] = value.ContractHash,
                ["pendingTracePath"] = value.PendingTracePath,
                ["pendingTraceFileHash"] = value.PendingTraceFileHash,
                ["bootstrapManifestSnapshotPath"] = value.ManifestSnapshotPath,
                ["bootstrapManifestSnapshotFileHash"] = value.ManifestSnapshotFileHash,
                ["buildHash"] = value.BuildHash,
                ["captureProfileHash"] = value.CaptureProfileHash,
                ["runtimeEntryPath"] = value.RuntimeEntryPath,
                ["runtimeEntryGuid"] = value.RuntimeEntryGuid,
                ["previewScenePath"] = value.PreviewScenePath,
                ["previewSceneFileHash"] = value.PreviewSceneFileHash
            };
        }

        private static JObject BuildRawProjection(RawReplay value)
        {
            return new JObject
            {
                ["layout"] = LegacyRawLayout,
                ["root"] = value.Root,
                ["captureMetadataPath"] = value.CaptureMetadataPath,
                ["captureMetadataFileHash"] = value.CaptureMetadataFileHash,
                ["evidenceSealPath"] = value.EvidenceSealPath,
                ["evidenceSealFileHash"] = value.EvidenceSealFileHash,
                ["evidenceSealHash"] = value.EvidenceSealHash,
                ["evidenceLockPath"] = value.EvidenceLockPath,
                ["evidenceLockFileHash"] = value.EvidenceLockFileHash,
                ["diagnosticPassManifestPath"] = value.DiagnosticManifestPath,
                ["diagnosticPassManifestFileHash"] = value.DiagnosticManifestFileHash,
                ["artifactCount"] = value.ArtifactCount,
                ["totalBytes"] = value.TotalBytes,
                ["fileSetTypedHash"] = value.FileSetTypedHash
            };
        }

        private static JObject BuildS0bEvaluation(RawReplay raw)
        {
            var metadata = raw.Metadata;
            if (metadata.TypedRawByFile.Count != 0 || metadata.MetricInputs.Count != 0 || metadata.MetricReports.Count != 0)
                throw new InvalidDataException("S0b descriptor route rejects typed S3 metrics records.");
            var command = SingleRecord(metadata.SupplementalDiagnostics, "kind", "formal-capture-command", "diagnostics/operator-command.json");
            var telemetry = SingleRecord(metadata.SemanticTelemetry, "kind", "semantic-telemetry", "diagnostics/semantic-telemetry.json");
            var off = SingleRecord(metadata.SupplementalDiagnostics, "kind", "receiver-light-off", "diagnostics/receiver-light-off.png");
            var on = SingleRecord(metadata.SupplementalDiagnostics, "kind", "receiver-light-on", "diagnostics/receiver-light-on.png");
            var summary = SingleRecord(metadata.SupplementalDiagnostics, "kind", "receiver-linear-luminance-ab", "diagnostics/receiver-light-ab.json");
            ParseRawJson(raw, (string)command["file"], "S0b operator command");
            ParseRawJson(raw, (string)telemetry["file"], "S0b semantic telemetry");
            ParseRawJson(raw, (string)summary["file"], "S0b receiver summary");
            return new JObject
            {
                ["schema"] = "w24-s5-eval-input-s0b-legacy/1",
                ["operatorCommandPath"] = raw.Root + "/" + (string)command["file"],
                ["operatorCommandFileHash"] = (string)command["sha256"],
                ["semanticTelemetryPath"] = raw.Root + "/" + (string)telemetry["file"],
                ["semanticTelemetryFileHash"] = (string)telemetry["sha256"],
                ["receiverOffPath"] = raw.Root + "/" + (string)off["file"],
                ["receiverOffFileHash"] = (string)off["sha256"],
                ["receiverOnPath"] = raw.Root + "/" + (string)on["file"],
                ["receiverOnFileHash"] = (string)on["sha256"],
                ["receiverSummaryPath"] = raw.Root + "/" + (string)summary["file"],
                ["receiverSummaryFileHash"] = (string)summary["sha256"],
                ["replayPolicyVersion"] = S0bReplayPolicy
            };
        }

        private static WriterReplay ReplayWriterBundle(Registry registry, PrepareBudget budget)
        {
            var file = ReadRepositoryPinned(registry.WriterBundlePath, registry.WriterBundleFileHash, "compiled writer bundle", MaxJsonBytes);
            budget.AddRequest(file.Length, "compiled writer bundle");
            var root = Parse(file.Bytes, "compiled writer bundle");
            RequireExactly(root, "schema", "writerId", "writerVersion", "sources", "typedBundleHashEncoding", "typedBundleHash");
            RequireExactString(root, "schema", WriterBundleSchema);
            var writerId = RequiredBoundedToken(root, "writerId");
            var writerVersion = RequiredVersion(root, "writerVersion");
            RequireExactString(root, "typedBundleHashEncoding", W24TypedBinaryCanonicalEncoding.EncodingName);
            var typed = RequiredHash(root, "typedBundleHash");
            var body = (JObject)root.DeepClone(); body.Remove("typedBundleHash");
            if (!Same(typed, TypedHash(body)) || !Same(typed, registry.WriterBundleTypedHash)
                || !Same(writerId, registry.WriterId) || !Same(writerVersion, registry.WriterVersion))
                throw new InvalidDataException("Compiled writer bundle identity or typed self-seal is invalid.");
            var output = new WriterReplay { WriterId = writerId, WriterVersion = writerVersion, TypedBundleHash = typed, Bytes = file.Bytes, FileHash = file.Hash };
            ReplaySourceRegistry(RequiredArray(root, "sources", 1, MaxSourceRecords), output.Sources, "writer bundle source", budget);
            return output;
        }

        private static BundleReplay ReplayCaptureBundle(Registry registry, W24S5CandidateEvidenceReader.CandidateReplayAuthority authority, PrepareBudget budget)
        {
            var file = ReadRepositoryPinned(registry.CaptureToolBundlePath, registry.CaptureToolBundleFileHash, "compiled capture-tool bundle", MaxJsonBytes);
            budget.AddRequest(file.Length, "compiled capture-tool bundle");
            var root = Parse(file.Bytes, "compiled capture-tool bundle");
            RequireExactly(root, "bundleVersion", "toolVersion", "sources", "configuration");
            RequireExactString(root, "bundleVersion", "w24-capture-tool-bundle/1");
            var toolVersion = RequiredVersion(root, "toolVersion");
            var configuration = RequiredObject(root, "configuration");
            RequireExactly(configuration, "authority", "emittedEvidenceExcludedFromIdentity", "candidatePathsExcludedFromIdentity");
            RequiredString(configuration, "authority", 4096);
            if (RequiredBool(configuration, "emittedEvidenceExcludedFromIdentity") != true || RequiredBool(configuration, "candidatePathsExcludedFromIdentity") != true)
                throw new InvalidDataException("Capture-tool bundle does not exclude emitted evidence and candidate paths from its identity.");
            var canonicalHash = Hash(StrictUtf8.GetBytes(CanonicalJson(root)));
            var contract = Parse(ReadRepositoryPinned(authority.ContractPath, authority.ContractFileHash, "candidate Contract for capture bundle", MaxJsonBytes).Bytes, "candidate Contract for capture bundle");
            if (!Same((string)contract.SelectToken("captureProfile.captureToolVersion"), toolVersion)
                || !Same((string)contract.SelectToken("captureProfile.captureToolHash"), canonicalHash))
                throw new InvalidDataException("Candidate Contract does not bind the compiled capture-tool bundle version and canonical hash.");
            if (registry.Route == S3Route && !Same((string)contract.SelectToken("extensions.captureToolBundle"), registry.CaptureToolBundlePath))
                throw new InvalidDataException("S3 Contract capture-tool bundle path differs from the compiled registry.");
            var extensions = contract["extensions"] as JObject;
            if (registry.Route == S0bRoute && extensions != null
                && extensions.Properties().Any(property => Same(property.Name, "typedDiagnostics") || Same(property.Name, "captureToolBundle")))
                throw new InvalidDataException("S0b descriptor route rejects a Contract declaring typedDiagnostics or captureToolBundle authority.");
            var output = new BundleReplay { ToolVersion = toolVersion, CanonicalHash = canonicalHash, Bytes = file.Bytes, FileHash = file.Hash };
            ReplaySourceRegistry(RequiredArray(root, "sources", 1, MaxSourceRecords), output.Sources, "capture-tool bundle source", budget);
            return output;
        }

        private static void ReplaySourceRegistry(JArray values, List<SourceReplay> output, string label, PrepareBudget budget)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < values.Count; index++)
            {
                var item = RequiredArrayObject(values[index], label);
                RequireExactly(item, "path", "sha256");
                var path = RequiredRepositoryPath(item, "path");
                var hash = RequiredHash(item, "sha256");
                if (!seen.Add(path)) throw new InvalidDataException(label + " registry repeats a path.");
                var file = ReadRepositoryPinned(path, hash, label, MaxSnapshotSourceBytes);
                budget.AddSource(file.Length, label);
                output.Add(new SourceReplay { Ordinal = index, Path = path, Hash = hash, Bytes = file.Bytes });
            }
        }

        private static RawReplay ReplayLegacyRaw(W24S5CandidateEvidenceReader.CandidateReplayAuthority authority, W24S5LegacyRawReplayPins pins, PrepareBudget budget)
        {
            var root = "artifacts/vfx-evidence/" + authority.EffectId + "/C0";
            var captureMetadataPath = root + "/capture-metadata.json";
            var sealPath = root + "/evidence-seal.json";
            var lockPath = root + "/evidence-lock.json";
            var diagnosticPath = root + "/diagnostic-pass-manifest.json";
            var metadataFile = ReadRepositoryUnpinned(captureMetadataPath, "raw capture metadata", MaxJsonBytes);
            var sealFile = ReadRepositoryUnpinned(sealPath, "raw evidence seal", MaxJsonBytes);
            var lockFile = ReadRepositoryUnpinned(lockPath, "raw evidence lock", MaxJsonBytes);
            var diagnosticFile = ReadRepositoryUnpinned(diagnosticPath, "raw diagnostic manifest", MaxJsonBytes);
            var metadata = Parse(metadataFile.Bytes, "raw capture metadata");
            var seal = Parse(sealFile.Bytes, "raw evidence seal");
            var evidenceLock = Parse(lockFile.Bytes, "raw evidence lock");
            var diagnostic = Parse(diagnosticFile.Bytes, "raw diagnostic manifest");

            RequireExactlyInOrder(seal, "schema", "candidateId", "captureProfileSha256", "artifacts", "provenance", "sealHash");
            RequireExactString(seal, "schema", EvidenceSealSchema);
            RequireExactString(seal, "candidateId", "C0");
            var recorderProfileHash = RequiredHash(seal, "captureProfileSha256");
            var sealHash = RequiredHash(seal, "sealHash");
            var provenance = RequiredObject(seal, "provenance");
            RequireExactlyInOrder(provenance, "operatorCommandHash", "captureToolSha256", "sourceHashesSha256", "captureMetadataSha256");
            RequiredHash(provenance, "operatorCommandHash");
            if (!Same(RequiredHash(provenance, "captureToolSha256"), pins.CaptureToolCanonicalHash)
                || !Same(RequiredHash(provenance, "captureMetadataSha256"), metadataFile.Hash))
                throw new InvalidDataException("Raw seal provenance does not bind descriptor capture-tool or metadata bytes.");
            var sealBody = (JObject)seal.DeepClone(); sealBody.Remove("sealHash");
            if (!Same(Hash(StrictUtf8.GetBytes(sealBody.ToString(Formatting.None))), sealHash))
                throw new InvalidDataException("Raw evidence sealHash does not match the original compact body algorithm.");

            var sealedArtifacts = new Dictionary<string, string>(StringComparer.Ordinal);
            var artifactArray = RequiredArray(seal, "artifacts", 3, MaxRawFiles - 1);
            string previous = null;
            foreach (var token in artifactArray)
            {
                var item = RequiredArrayObject(token, "raw seal artifact");
                RequireExactlyInOrder(item, "file", "sha256");
                var local = RequiredLocalPath(item, "file");
                var hash = RequiredHash(item, "sha256");
                if (Same(local, "evidence-seal.json") || sealedArtifacts.ContainsKey(local)
                    || previous != null && string.CompareOrdinal(previous, local) >= 0)
                    throw new InvalidDataException("Raw seal artifact registry is duplicate, unsorted, or self-referential.");
                sealedArtifacts.Add(local, hash); previous = local;
            }

            var expected = new HashSet<string>(sealedArtifacts.Keys, StringComparer.Ordinal) { "evidence-seal.json" };
            var files = EnumerateRawTree(root, expected);
            if (!expected.SetEquals(files.Keys))
                throw new InvalidDataException("Raw tree contains a missing, extra, or unsealed file (legacy bound/ alone is excluded).");
            foreach (var item in sealedArtifacts)
                if (!Same(files[item.Key].Hash, item.Value)) throw new InvalidDataException("Raw artifact bytes differ from evidence-seal.json: " + item.Key);
            if (!Same(files["evidence-seal.json"].Hash, sealFile.Hash)) throw new InvalidDataException("Raw evidence seal changed during enumeration.");

            RequireExactlyInOrder(evidenceLock, "schema", "candidateId", "captureProfileSha256");
            RequireExactString(evidenceLock, "schema", EvidenceLockSchema);
            RequireExactString(evidenceLock, "candidateId", "C0");
            if (!Same(RequiredHash(evidenceLock, "captureProfileSha256"), recorderProfileHash))
                throw new InvalidDataException("Raw evidence lock profile differs from the final seal.");
            VerifyDiagnosticManifest(diagnostic);

            var replay = VerifyMetadata(authority, pins, metadata, diagnostic, metadataFile.Hash, diagnosticFile.Hash, recorderProfileHash, provenance, sealedArtifacts, root);
            var total = files.Values.Aggregate<FileIdentity, long>(0, (current, item) => checked(current + item.Length));
            budget.AddRequest(total, "legacy raw sealed file set");
            var command = SingleRecord(replay.SupplementalDiagnostics, "kind", "formal-capture-command", "diagnostics/operator-command.json");
            if (!Same(RequiredHash(provenance, "operatorCommandHash"), RequiredHash(command, "sha256")))
                throw new InvalidDataException("Raw seal operatorCommandHash does not bind the authoritative supplemental formal-capture-command bytes.");
            var fileSet = new JObject
            {
                ["schema"] = SealedFileSetSchema,
                ["files"] = new JArray(files.Values.OrderBy(item => item.LocalPath, StringComparer.Ordinal).Select(item => new JObject
                {
                    ["path"] = item.LocalPath, ["sha256"] = item.Hash, ["byteLength"] = item.Length
                }))
            };
            return new RawReplay
            {
                Root = root,
                CaptureMetadataPath = captureMetadataPath,
                CaptureMetadataFileHash = metadataFile.Hash,
                EvidenceSealPath = sealPath,
                EvidenceSealFileHash = sealFile.Hash,
                EvidenceSealHash = sealHash,
                EvidenceLockPath = lockPath,
                EvidenceLockFileHash = lockFile.Hash,
                DiagnosticManifestPath = diagnosticPath,
                DiagnosticManifestFileHash = diagnosticFile.Hash,
                ArtifactCount = files.Count,
                TotalBytes = total,
                FileSetTypedHash = W24TypedBinaryCanonicalEncoding.Hash(fileSet),
                Metadata = replay,
                Files = files
            };
        }

        private static MetadataReplay VerifyMetadata(
            W24S5CandidateEvidenceReader.CandidateReplayAuthority authority,
            W24S5LegacyRawReplayPins pins,
            JObject metadata,
            JObject diagnostic,
            string metadataHash,
            string diagnosticHash,
            string recorderProfileHash,
            JObject provenance,
            Dictionary<string, string> sealedArtifacts,
            string rawRoot)
        {
            RequireExactlyInOrder(metadata,
                "schema", "candidateId", "captureModePolicy", "executedInBatchMode", "frameRetentionPolicy",
                "retainedFrameIndices", "retainedFrameIndicesSha256", "formalPlayerLoop", "captureProfile",
                "captureProfileSha256", "sourceHashes", "diagnosticPassManifest", "typedRawDiagnostics",
                "metricInputs", "metricReports", "semanticTelemetry", "supplementalDiagnostics", "frames");
            RequireExactString(metadata, "schema", CaptureMetadataSchema);
            RequireExactString(metadata, "candidateId", "C0");
            RequiredString(metadata, "captureModePolicy", 1024);
            RequiredString(metadata, "frameRetentionPolicy", 2048);
            if (!RequiredBool(metadata, "executedInBatchMode")) throw new InvalidDataException("Raw capture was not graphics-backed batchmode evidence.");
            var formalLoop = RequiredObject(metadata, "formalPlayerLoop");
            RequireExactlyInOrder(formalLoop, "observedSerial", "consumedSerial", "allObservedFramesConsumed");
            var observed = RequiredLong(formalLoop, "observedSerial", 1, long.MaxValue);
            var consumed = RequiredLong(formalLoop, "consumedSerial", 1, long.MaxValue);
            if (observed != consumed || !RequiredBool(formalLoop, "allObservedFramesConsumed"))
                throw new InvalidDataException("Raw capture sealed with incomplete PlayerLoop consumption.");

            var profile = RequiredObject(metadata, "captureProfile");
            VerifyRecorderCaptureProfile(profile, metadata);
            if (!Same(RequiredHash(metadata, "captureProfileSha256"), recorderProfileHash)
                || !Same(Hash(StrictUtf8.GetBytes(profile.ToString(Formatting.None))), recorderProfileHash))
                throw new InvalidDataException("Recorder capture profile hash does not match its exact field-order bytes.");

            var sources = RequiredObject(metadata, "sourceHashes");
            VerifySourceHashes(authority, pins, sources);
            if (!Same(RequiredHash(provenance, "sourceHashesSha256"), Hash(StrictUtf8.GetBytes(sources.ToString(Formatting.None)))))
                throw new InvalidDataException("Raw seal sourceHashesSha256 differs from exact recorder source bytes.");
            if (!Same(RequiredHash(provenance, "captureMetadataSha256"), metadataHash))
                throw new InvalidDataException("Raw seal metadata pin changed during replay.");
            var diagnosticReference = RequiredObject(metadata, "diagnosticPassManifest");
            RequireExactlyInOrder(diagnosticReference, "file", "sha256");
            if (!Same(RequiredLocalPath(diagnosticReference, "file"), "diagnostic-pass-manifest.json")
                || !Same(RequiredHash(diagnosticReference, "sha256"), diagnosticHash))
                throw new InvalidDataException("Capture metadata diagnostic manifest pin is invalid.");

            var output = new MetadataReplay
            {
                Metadata = metadata,
                Contract = Parse(ReadRepositoryPinned(authority.ContractPath, authority.ContractFileHash, "candidate Contract provenance", MaxJsonBytes).Bytes, "candidate Contract provenance"),
                Manifest = Parse(ReadRepositoryPinned(authority.ManifestSnapshotPath, authority.ManifestSnapshotFileHash, "candidate Manifest provenance", MaxJsonBytes).Bytes, "candidate Manifest provenance"),
                SourceHashes = sources,
                RecorderCaptureProfileHash = recorderProfileHash
            };
            foreach (var pair in sealedArtifacts) output.SealArtifacts.Add(pair.Key, pair.Value);
            var declared = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["evidence-lock.json"] = sealedArtifacts.TryGetValue("evidence-lock.json", out var lockHash) ? lockHash : null,
                ["capture-metadata.json"] = metadataHash,
                ["diagnostic-pass-manifest.json"] = diagnosticHash
            };
            if (declared["evidence-lock.json"] == null) throw new InvalidDataException("Raw seal omits evidence-lock.json.");

            VerifyFrames(RequiredArray(metadata, "frames", 1, MaxRecordCount), diagnostic, declared, sealedArtifacts);
            VerifySemanticRecords(RequiredArray(metadata, "semanticTelemetry", 0, MaxRecordCount), output.SemanticTelemetry, declared, sealedArtifacts);
            VerifySupplementalRecords(RequiredArray(metadata, "supplementalDiagnostics", 0, MaxRecordCount), output.SupplementalDiagnostics, declared, sealedArtifacts);
            if (pins.AllowTypedS3Records)
            {
                VerifyTypedRawRecords(RequiredArray(metadata, "typedRawDiagnostics", 0, MaxRecordCount), output.TypedRawByFile, declared, sealedArtifacts, diagnostic);
                VerifyMetricInputRecords(RequiredArray(metadata, "metricInputs", 0, 4), output.MetricInputs, declared, sealedArtifacts);
                VerifyMetricReportRecords(RequiredArray(metadata, "metricReports", 0, 4), output.MetricReports, declared, sealedArtifacts);
            }
            else if (RequiredArray(metadata, "typedRawDiagnostics", 0, MaxRecordCount).Count != 0
                || RequiredArray(metadata, "metricInputs", 0, 4).Count != 0
                || RequiredArray(metadata, "metricReports", 0, 4).Count != 0)
            {
                throw new InvalidDataException("S0b replay rejects typed-raw and S3 metrics metadata records.");
            }
            if (!new HashSet<string>(declared.Keys, StringComparer.Ordinal).SetEquals(sealedArtifacts.Keys))
                throw new InvalidDataException("Capture metadata does not exactly account for every pre-seal artifact.");
            foreach (var pair in declared)
                if (!Same(sealedArtifacts[pair.Key], pair.Value)) throw new InvalidDataException("Capture metadata artifact hash differs from the raw seal: " + pair.Key);
            return output;
        }

        private static void VerifyRecorderCaptureProfile(JObject profile, JObject metadata)
        {
            RequireExactlyInOrder(profile,
                "profileVersion", "unityVersion", "urpVersion", "graphicsApi", "graphicsDevice", "graphicsDriverVersion",
                "renderTextureFormat", "rendererAsset", "volume", "scenePath", "serializedCameraReference", "resolution",
                "fps", "background", "colorSpace", "hdr", "msaa", "bloom", "toneMapping", "canonicalSeed",
                "robustnessSeeds", "retainedFrameIndices", "retainedFrameIndicesSha256");
            foreach (var field in new[] { "profileVersion", "unityVersion", "urpVersion", "graphicsApi", "graphicsDevice", "graphicsDriverVersion", "renderTextureFormat", "scenePath", "serializedCameraReference", "colorSpace" })
                RequiredString(profile, field, 1024);
            foreach (var field in new[] { "rendererAsset", "volume" })
            {
                var source = RequiredObject(profile, field); RequireExactlyInOrder(source, "reference", "sha256");
                RequiredString(source, "reference", 1024); RequiredHash(source, "sha256");
            }
            var resolution = RequiredArray(profile, "resolution", 2, 2);
            RequiredArrayLong(resolution, 0, 1, 16384, "capture resolution width");
            RequiredArrayLong(resolution, 1, 1, 16384, "capture resolution height");
            RequiredLong(profile, "fps", 1, 1000);
            var background = RequiredArray(profile, "background", 4, 4);
            foreach (var item in background) RequiredFiniteNumber(item, "capture background");
            RequiredBool(profile, "hdr"); RequiredBool(profile, "msaa");
            var bloom = RequiredObject(profile, "bloom"); RequireExactlyInOrder(bloom, "value", "validation");
            RequiredBool(bloom, "value"); RequireExactString(bloom, "validation", "caller-frozen");
            var tone = RequiredObject(profile, "toneMapping"); RequireExactlyInOrder(tone, "value", "validation");
            RequiredString(tone, "value", 128); RequireExactString(tone, "validation", "caller-frozen");
            RequiredLong(profile, "canonicalSeed", 0, uint.MaxValue);
            var robustness = RequiredArray(profile, "robustnessSeeds", 2, 2);
            foreach (var item in robustness) RequiredArrayNumber(item, 0, uint.MaxValue, "capture robustness seed");
            var retained = RequiredArray(profile, "retainedFrameIndices", 1, MaxRecordCount);
            var retainedMetadata = RequiredArray(metadata, "retainedFrameIndices", 1, MaxRecordCount);
            if (!JToken.DeepEquals(retained, retainedMetadata)) throw new InvalidDataException("Recorder profile and metadata retained-frame registries differ.");
            var indices = retained.Select(item => RequiredArrayNumber(item, 0, int.MaxValue, "retained frame index")).ToArray();
            if (indices.Distinct().Count() != indices.Length) throw new InvalidDataException("Retained frame registry repeats an index.");
            var hash = Hash(StrictUtf8.GetBytes(string.Join(",", indices.Select(value => value.ToString(CultureInfo.InvariantCulture)))));
            if (!Same(RequiredHash(profile, "retainedFrameIndicesSha256"), hash)
                || !Same(RequiredHash(metadata, "retainedFrameIndicesSha256"), hash))
                throw new InvalidDataException("Retained frame registry hash is invalid.");
        }

        private static void VerifySourceHashes(W24S5CandidateEvidenceReader.CandidateReplayAuthority authority, W24S5LegacyRawReplayPins pins, JObject sources)
        {
            RequireExactlyInOrder(sources, "scene", "prefab", "manifest", "captureTool");
            var scene = RequiredObject(sources, "scene"); RequireExactlyInOrder(scene, "path", "sha256");
            var prefab = RequiredObject(sources, "prefab"); RequireExactlyInOrder(prefab, "path", "guid", "sha256");
            var manifest = RequiredObject(sources, "manifest"); RequireExactlyInOrder(manifest, "path", "sha256", "buildHash");
            var tool = RequiredObject(sources, "captureTool"); RequireExactlyInOrder(tool, "path", "version", "sha256");
            var manifestSnapshot = Parse(ReadRepositoryPinned(authority.ManifestSnapshotPath, authority.ManifestSnapshotFileHash, "source Manifest snapshot", MaxJsonBytes).Bytes, "source Manifest snapshot");
            var runtime = RequiredObject(manifestSnapshot, "runtimeEntry");
            var ownedMatches = RequiredArray(manifestSnapshot, "ownedOutputs", 1, 256).OfType<JObject>()
                .Where(item => Same((string)item["path"], authority.RuntimeEntryPath)).ToArray();
            if (ownedMatches.Length != 1)
                throw new InvalidDataException("Source Manifest snapshot does not contain one exact Runtime Entry output.");
            var owned = ownedMatches[0];
            if (!SameExactAbsolutePath(RequiredAbsolutePath(scene, "path"), ProjectAbsolute(authority.PreviewScenePath))
                || !Same(RequiredHash(scene, "sha256"), authority.PreviewSceneFileHash))
                throw new InvalidDataException("Recorder scene source differs from the candidate Preview Scene.");
            if (owned == null || !SameExactAbsolutePath(RequiredAbsolutePath(prefab, "path"), ProjectAbsolute(authority.RuntimeEntryPath))
                || !Same(RequiredGuid(prefab, "guid"), authority.RuntimeEntryGuid)
                || !Same(RequiredHash(prefab, "sha256"), "sha256:" + RequiredRawHash(owned, "sha256")))
                throw new InvalidDataException("Recorder prefab source differs from the candidate Runtime Entry.");
            if (!SameExactAbsolutePath(RequiredAbsolutePath(manifest, "path"), ProjectAbsolute(authority.ProductionManifestPath))
                || !Same(RequiredHash(manifest, "sha256"), authority.ManifestSnapshotFileHash)
                || !Same(RequiredHash(manifest, "buildHash"), authority.BuildHash))
                throw new InvalidDataException("Recorder Manifest/build source differs from the immutable C0 snapshot.");
            if (!SameExactAbsolutePath(RequiredAbsolutePath(tool, "path"), RepositoryAbsolute(pins.CaptureToolBundlePath))
                || !Same(RequiredVersion(tool, "version"), pins.CaptureToolVersion)
                || !Same(RequiredHash(tool, "sha256"), pins.CaptureToolCanonicalHash))
                throw new InvalidDataException("Recorder capture-tool source differs from the compiled bundle.");
        }

        private static void VerifyDiagnosticManifest(JObject root)
        {
            RequireExactlyInOrder(root, "schema", "passes");
            RequireExactString(root, "schema", DiagnosticManifestSchema);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in RequiredArray(root, "passes", 1, MaxRecordCount))
            {
                var pass = RequiredArrayObject(token, "diagnostic pass");
                var fields = pass.Properties().Select(value => value.Name).ToArray();
                if (fields.SequenceEqual(new[] { "passId", "encoding", "purpose" }, StringComparer.Ordinal))
                {
                    // typed raw pass
                }
                else if (fields.SequenceEqual(new[] { "passId", "encoding", "purpose", "camera", "clear", "cullingMask", "format" }, StringComparer.Ordinal))
                {
                    RequiredString(pass, "camera", 512); RequiredString(pass, "clear", 512);
                    RequiredLong(pass, "cullingMask", int.MinValue, uint.MaxValue); RequiredString(pass, "format", 128);
                }
                else throw new InvalidDataException("Diagnostic pass manifest field set is not an exact recorder variant.");
                var id = RequiredProtocolToken(pass, "passId"); RequiredProtocolToken(pass, "encoding"); RequiredString(pass, "purpose", 2048);
                if (!seen.Add(id)) throw new InvalidDataException("Diagnostic pass manifest repeats a passId.");
            }
        }

        private static void VerifyFrames(JArray frames, JObject diagnosticManifest, Dictionary<string, string> declared, Dictionary<string, string> seal)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var passIds = new HashSet<string>(RequiredArray(diagnosticManifest, "passes", 1, MaxRecordCount)
                .OfType<JObject>().Select(item => (string)item["passId"]), StringComparer.Ordinal);
            foreach (var token in frames)
            {
                var frame = RequiredArrayObject(token, "capture frame");
                RequireExactlyInOrder(frame, "frameIndex", "simulationTime", "state", "seed", "beauty", "diagnostics");
                var index = RequiredLong(frame, "frameIndex", 0, int.MaxValue);
                RequiredFiniteNumber(frame["simulationTime"], "frame simulationTime");
                RequiredString(frame, "state", 256);
                var seed = RequiredLong(frame, "seed", 0, uint.MaxValue);
                if (!keys.Add(seed.ToString(CultureInfo.InvariantCulture) + ":" + index.ToString(CultureInfo.InvariantCulture)))
                    throw new InvalidDataException("Capture frame registry repeats seed/frame identity.");
                var beauty = RequiredObject(frame, "beauty"); RequireExactlyInOrder(beauty, "file", "sha256");
                AddDeclared(declared, seal, RequiredLocalPath(beauty, "file"), RequiredHash(beauty, "sha256"), "Beauty frame");
                foreach (var item in RequiredArray(frame, "diagnostics", 1, MaxRecordCount))
                {
                    var diagnostic = RequiredArrayObject(item, "frame diagnostic");
                    RequireExactlyInOrder(diagnostic, "passId", "file", "sha256", "foregroundPixels", "method");
                    var passId = RequiredProtocolToken(diagnostic, "passId");
                    if (!passIds.Contains(passId)) throw new InvalidDataException("Frame diagnostic references an undeclared passId.");
                    RequiredLong(diagnostic, "foregroundPixels", 0, long.MaxValue); RequiredString(diagnostic, "method", 4096);
                    AddDeclared(declared, seal, RequiredLocalPath(diagnostic, "file"), RequiredHash(diagnostic, "sha256"), "frame diagnostic");
                }
            }
        }

        private static void VerifySemanticRecords(JArray records, List<JObject> output, Dictionary<string, string> declared, Dictionary<string, string> seal)
        {
            foreach (var token in records)
            {
                var record = RequiredArrayObject(token, "semantic telemetry record");
                RequireExactlyInOrder(record, "kind", "description", "file", "sha256");
                RequiredProtocolToken(record, "kind"); RequiredString(record, "description", 4096);
                AddDeclared(declared, seal, RequiredLocalPath(record, "file"), RequiredHash(record, "sha256"), "semantic telemetry");
                output.Add(record);
            }
        }

        private static void VerifySupplementalRecords(JArray records, List<JObject> output, Dictionary<string, string> declared, Dictionary<string, string> seal)
        {
            foreach (var token in records)
            {
                var record = RequiredArrayObject(token, "supplemental diagnostic record");
                var fields = record.Properties().Select(value => value.Name).ToArray();
                var unobserved = fields.SequenceEqual(new[] { "kind", "description", "file", "sha256" }, StringComparer.Ordinal);
                var observed = fields.SequenceEqual(new[] { "kind", "description", "file", "sha256", "observedPlayerLoop" }, StringComparer.Ordinal);
                if (!unobserved && !observed) throw new InvalidDataException("Supplemental diagnostic record field set is not exact.");
                RequiredProtocolToken(record, "kind"); RequiredString(record, "description", 4096);
                if (observed) VerifyObservedToken(RequiredObject(record, "observedPlayerLoop"), false);
                AddDeclared(declared, seal, RequiredLocalPath(record, "file"), RequiredHash(record, "sha256"), "supplemental diagnostic");
                output.Add(record);
            }
        }

        private static void VerifyTypedRawRecords(JArray records, Dictionary<string, JObject> output, Dictionary<string, string> declared, Dictionary<string, string> seal, JObject diagnosticManifest)
        {
            var passMap = RequiredArray(diagnosticManifest, "passes", 1, MaxRecordCount).OfType<JObject>()
                .ToDictionary(item => (string)item["passId"], item => (string)item["encoding"], StringComparer.Ordinal);
            foreach (var token in records)
            {
                var record = RequiredArrayObject(token, "typed raw diagnostic record");
                RequireExactlyInOrder(record, "kind", "passId", "encoding", "description", "derivedFrom", "file", "sha256", "observedPlayerLoop");
                RequireExactString(record, "kind", "diagnostic");
                var pass = RequiredProtocolToken(record, "passId"); var encoding = RequiredProtocolToken(record, "encoding");
                RequiredString(record, "description", 4096); RequiredString(record, "derivedFrom", 4096);
                var file = RequiredLocalPath(record, "file"); var hash = RequiredHash(record, "sha256");
                if (!file.StartsWith("diagnostics/", StringComparison.Ordinal) || !passMap.TryGetValue(pass, out var declaredEncoding) || !Same(declaredEncoding, encoding)
                    || output.ContainsKey(file)) throw new InvalidDataException("Typed raw diagnostic path/pass/encoding registry is invalid.");
                VerifyObservedToken(RequiredObject(record, "observedPlayerLoop"), true);
                AddDeclared(declared, seal, file, hash, "typed raw diagnostic"); output.Add(file, record);
            }
        }

        private static void VerifyMetricInputRecords(JArray records, List<JObject> output, Dictionary<string, string> declared, Dictionary<string, string> seal)
        {
            foreach (var token in records)
            {
                var record = RequiredArrayObject(token, "metrics input record");
                RequireExactlyInOrder(record, "kind", "file", "sha256", "expectedToolSha256", "metricsEnvironmentSha256");
                RequireExactString(record, "kind", "metrics-input");
                var file = RequiredLocalPath(record, "file"); if (!file.StartsWith("diagnostics/", StringComparison.Ordinal)) throw new InvalidDataException("Metrics input path is outside diagnostics/.");
                AddDeclared(declared, seal, file, RequiredHash(record, "sha256"), "metrics input");
                RequiredHash(record, "expectedToolSha256"); RequiredHash(record, "metricsEnvironmentSha256"); output.Add(record);
            }
        }

        private static void VerifyMetricReportRecords(JArray records, List<JObject> output, Dictionary<string, string> declared, Dictionary<string, string> seal)
        {
            foreach (var token in records)
            {
                var record = RequiredArrayObject(token, "metrics report record");
                RequireExactlyInOrder(record, "kind", "passId", "encoding", "file", "sha256", "inputFile", "inputFileSha256", "analysisInputSha256", "expectedToolSha256");
                RequireExactString(record, "kind", "diagnostic"); RequireExactString(record, "passId", "metrics-report"); RequireExactString(record, "encoding", "json");
                var file = RequiredLocalPath(record, "file"); var input = RequiredLocalPath(record, "inputFile");
                if (!file.StartsWith("diagnostics/", StringComparison.Ordinal) || !input.StartsWith("diagnostics/", StringComparison.Ordinal)) throw new InvalidDataException("Metrics report/input path is outside diagnostics/.");
                AddDeclared(declared, seal, file, RequiredHash(record, "sha256"), "metrics report");
                RequiredHash(record, "inputFileSha256"); RequiredHash(record, "analysisInputSha256"); RequiredHash(record, "expectedToolSha256"); output.Add(record);
            }
        }

        private static void VerifyObservedToken(JObject value, bool viewRequired)
        {
            if (viewRequired) RequireExactlyInOrder(value, "serial", "frame", "time", "logicalFrameIndex", "seed", "viewId");
            else RequireExactlyInOrder(value, "serial", "frame", "time", "logicalFrameIndex", "seed");
            RequiredLong(value, "serial", 1, long.MaxValue); RequiredLong(value, "frame", 0, long.MaxValue);
            RequiredFiniteNumber(value["time"], "observed PlayerLoop time"); RequiredLong(value, "logicalFrameIndex", 0, int.MaxValue); RequiredLong(value, "seed", 0, uint.MaxValue);
            if (viewRequired) RequiredProtocolToken(value, "viewId");
        }

        private static S3Replay ReplayS3(W24S5CandidateEvidenceReader.CandidateReplayAuthority authority, Registry registry, BundleReplay capture, RawReplay raw, PrepareBudget budget)
        {
            var metadata = raw.Metadata;
            if (metadata.TypedRawByFile.Count == 0 || metadata.MetricInputs.Count != 1 || metadata.MetricReports.Count != 1)
                throw new InvalidDataException("S3 descriptor route requires typed raw diagnostics and exactly one sealed metrics input/report pair.");
            var inputRecord = metadata.MetricInputs[0]; var reportRecord = metadata.MetricReports[0];
            var inputLocal = (string)inputRecord["file"]; var reportLocal = (string)reportRecord["file"];
            if (!Same(inputLocal, "diagnostics/metrics-input.json") || !Same(reportLocal, "diagnostics/metrics-report.json")
                || !Same((string)reportRecord["inputFile"], inputLocal)
                || !Same((string)reportRecord["inputFileSha256"], (string)inputRecord["sha256"]))
                throw new InvalidDataException("S3 metrics input/report record linkage is noncanonical.");
            var input = ParseRawJson(raw, inputLocal, "S3 metrics input");
            var report = ParseRawJson(raw, reportLocal, "S3 metrics report");
            RequireExactly(input, "schema", "effectId", "candidateId", "contractRevision", "contractSha256", "captureProfileSha256", "recorderCaptureProfileSha256", "captureToolBundlePath", "captureToolBundleSha256", "expectedToolSha256", "metricsEnvironment", "requiredEvidenceMatrix", "requiredEvidenceMatrixSha256", "evidence", "checks");
            RequireExactString(input, "schema", MetricsInputSchema); RequireExactString(input, "effectId", authority.EffectId); RequireExactString(input, "candidateId", "C0");
            if (RequiredLong(input, "contractRevision", 1, MaxRevision) != authority.ContractRevision
                || !Same(RequiredHash(input, "contractSha256"), authority.ContractHash)
                || !Same(RequiredHash(input, "captureProfileSha256"), authority.CaptureProfileHash)
                || !Same(RequiredHash(input, "recorderCaptureProfileSha256"), metadata.RecorderCaptureProfileHash)
                || !Same(RequiredRepositoryPath(input, "captureToolBundlePath"), registry.CaptureToolBundlePath)
                || !Same(RequiredHash(input, "captureToolBundleSha256"), capture.CanonicalHash))
                throw new InvalidDataException("S3 metrics input candidate/Contract/profile/bundle identity is invalid.");
            var expectedTool = RequiredHash(input, "expectedToolSha256");
            if (!Same(expectedTool, RequiredHash(inputRecord, "expectedToolSha256")) || !Same(expectedTool, registry.MetricsToolFileHash))
                throw new InvalidDataException("S3 metrics input expected-tool pin differs from recorder and registry.");
            var toolSources = capture.Sources.Where(item => Same(item.Path, registry.MetricsToolPath) && Same(item.Hash, expectedTool)).ToArray();
            if (toolSources.Length != 1) throw new InvalidDataException("Capture-tool bundle does not uniquely contain the compiled metrics tool.");
            var contractTool = metadata.Contract.SelectToken("extensions.typedDiagnostics.metricsTool") as JObject;
            if (contractTool == null) throw new InvalidDataException("S3 Contract omits typedDiagnostics.metricsTool.");
            RequireExactly(contractTool, "path", "sha256");
            if (!Same(RequiredRepositoryPath(contractTool, "path"), registry.MetricsToolPath) || !Same(RequiredHash(contractTool, "sha256"), expectedTool))
                throw new InvalidDataException("S3 Contract metrics-tool pin differs from the compiled source.");

            var environment = RequiredObject(input, "metricsEnvironment");
            VerifyMetricsEnvironment(environment, RequiredHash(inputRecord, "metricsEnvironmentSha256"), metadata.Contract, budget);
            var matrix = RequiredArray(input, "requiredEvidenceMatrix", 1, MaxRecordCount);
            var matrixHash = Hash(StrictUtf8.GetBytes(CanonicalJson(matrix)));
            if (!Same(RequiredHash(input, "requiredEvidenceMatrixSha256"), matrixHash)) throw new InvalidDataException("S3 required evidence matrix hash is invalid.");
            var contractMatrix = metadata.Contract.SelectToken("extensions.typedDiagnostics.requiredEvidenceMatrix") as JArray;
            if (contractMatrix == null || !Same(CanonicalJson(contractMatrix), CanonicalJson(matrix)))
                throw new InvalidDataException("S3 input matrix differs from the Contract-frozen matrix.");
            VerifyS3RegistryAndMatrix(input, metadata.TypedRawByFile, matrix, metadata.Contract, registry.LegacyMultiviewMinDepthSpan.Value);

            var inputCanonicalHash = Hash(StrictUtf8.GetBytes(CanonicalJson(input)));
            if (!Same(inputCanonicalHash, RequiredHash(reportRecord, "analysisInputSha256"))) throw new InvalidDataException("S3 metrics analysis-input pin is invalid.");
            VerifyMetricsReport(report, input, inputCanonicalHash, expectedTool);
            if (!Same((string)reportRecord["expectedToolSha256"], expectedTool)) throw new InvalidDataException("S3 metrics report recorder tool pin is invalid.");
            var rawSet = new JObject
            {
                ["schema"] = TypedRawSetSchema,
                ["records"] = new JArray(metadata.TypedRawByFile.Values.OrderBy(item => (string)item["file"], StringComparer.Ordinal).Select(item => item.DeepClone()))
            };
            return new S3Replay
            {
                MetricsInputPath = raw.Root + "/" + inputLocal,
                MetricsInputHash = (string)inputRecord["sha256"],
                MetricsReportPath = raw.Root + "/" + reportLocal,
                MetricsReportHash = (string)reportRecord["sha256"],
                RequiredMatrixHash = matrixHash,
                TypedRawSetHash = W24TypedBinaryCanonicalEncoding.Hash(NormalizeTypedNumbers(rawSet)),
                Environment = (JObject)environment.DeepClone(),
                MetricsToolBytes = toolSources[0].Bytes,
                MetricsToolHash = expectedTool
            };
        }

        private static void VerifyMetricsEnvironment(JObject environment, string recorderHash, JObject contract, PrepareBudget budget)
        {
            RequireExactly(environment, "pythonExecutablePath", "pythonExecutableSha256", "pythonVersion", "numpyVersion", "pillowVersion", "environmentSha256");
            var executable = RequiredString(environment, "pythonExecutablePath", 4096);
            if (!Path.IsPathRooted(executable)) throw new InvalidDataException("Metrics Python executable path is not absolute.");
            var executableHash = RequiredHash(environment, "pythonExecutableSha256");
            RequiredString(environment, "pythonVersion", 256); RequiredString(environment, "numpyVersion", 256); RequiredString(environment, "pillowVersion", 256);
            var body = (JObject)environment.DeepClone(); var environmentHash = RequiredHash(body, "environmentSha256"); body.Remove("environmentSha256");
            if (!Same(environmentHash, recorderHash) || !Same(environmentHash, Hash(StrictUtf8.GetBytes(CanonicalJson(body)))))
                throw new InvalidDataException("Metrics environment self-hash or recorder pin is invalid.");
            var frozen = contract.SelectToken("extensions.typedDiagnostics.metricsEnvironment") as JObject;
            if (frozen == null || !Same(CanonicalJson(frozen), CanonicalJson(environment)))
                throw new InvalidDataException("Metrics environment differs from the Contract-frozen observation.");
            var executableIdentity = HashAbsolutePinned(Path.GetFullPath(executable.Replace('/', Path.DirectorySeparatorChar)), executableHash, "frozen Python executable", MaxPythonExecutableBytes);
            budget.AddRequest(executableIdentity.Length, "frozen Python executable");
        }

        private static void VerifyS3RegistryAndMatrix(JObject input, Dictionary<string, JObject> rawByFile, JArray matrix, JObject contract, double legacyMultiviewMinDepthSpan)
        {
            var registry = RequiredArray(input, "evidence", 1, MaxRecordCount);
            var ids = new HashSet<string>(StringComparer.Ordinal); var paths = new HashSet<string>(StringComparer.Ordinal);
            var byId = new Dictionary<string, JObject>(StringComparer.Ordinal);
            foreach (var token in registry)
            {
                var item = RequiredArrayObject(token, "S3 metrics evidence registry");
                RequireExactly(item, "id", "path", "sha256", "kind", "passId", "encoding", "seed", "logicalFrameIndex", "playerLoopSerial", "playerLoopFrame", "playerLoopTime", "viewId", "derivedFrom");
                var id = RequiredProtocolToken(item, "id"); var path = RequiredLocalPath(item, "path");
                RequireExactString(item, "kind", "diagnostic");
                if (!ids.Add(id) || !paths.Add(path) || !rawByFile.TryGetValue(path, out var raw)) throw new InvalidDataException("S3 metrics registry is duplicate or not bijective with typed raw diagnostics.");
                var observed = RequiredObject(raw, "observedPlayerLoop");
                if (!Same(RequiredHash(item, "sha256"), RequiredHash(raw, "sha256"))
                    || !Same(RequiredProtocolToken(item, "passId"), RequiredProtocolToken(raw, "passId"))
                    || !Same(RequiredProtocolToken(item, "encoding"), RequiredProtocolToken(raw, "encoding"))
                    || RequiredLong(item, "seed", 0, uint.MaxValue) != RequiredLong(observed, "seed", 0, uint.MaxValue)
                    || RequiredLong(item, "logicalFrameIndex", 0, int.MaxValue) != RequiredLong(observed, "logicalFrameIndex", 0, int.MaxValue)
                    || RequiredLong(item, "playerLoopSerial", 1, long.MaxValue) != RequiredLong(observed, "serial", 1, long.MaxValue)
                    || RequiredLong(item, "playerLoopFrame", 0, long.MaxValue) != RequiredLong(observed, "frame", 0, long.MaxValue)
                    || !JToken.DeepEquals(item["playerLoopTime"], observed["time"])
                    || !Same(RequiredProtocolToken(item, "viewId"), RequiredProtocolToken(observed, "viewId"))
                    || !Same(RequiredString(item, "derivedFrom", 4096), RequiredString(raw, "derivedFrom", 4096)))
                    throw new InvalidDataException("S3 metrics registry provenance differs from sealed typed raw metadata.");
                byId.Add(id, item);
            }
            if (paths.Count != rawByFile.Count) throw new InvalidDataException("S3 metrics registry omits sealed typed raw diagnostics.");
            var matrixIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in matrix)
            {
                var row = RequiredArrayObject(token, "S3 required evidence matrix row");
                RequireExactly(row, "evidenceId", "passId", "seed", "viewId", "logicalFrameIndex");
                var id = RequiredProtocolToken(row, "evidenceId");
                if (!matrixIds.Add(id) || !byId.TryGetValue(id, out var item)
                    || !Same(RequiredProtocolToken(row, "passId"), RequiredProtocolToken(item, "passId"))
                    || RequiredLong(row, "seed", 0, uint.MaxValue) != RequiredLong(item, "seed", 0, uint.MaxValue)
                    || !Same(RequiredProtocolToken(row, "viewId"), RequiredProtocolToken(item, "viewId"))
                    || RequiredLong(row, "logicalFrameIndex", 0, int.MaxValue) != RequiredLong(item, "logicalFrameIndex", 0, int.MaxValue))
                    throw new InvalidDataException("S3 required evidence matrix does not exactly resolve its typed registry row.");
            }
            if (!matrixIds.SetEquals(ids)) throw new InvalidDataException("S3 required evidence matrix and typed registry IDs differ.");
            var checks = RequiredArray(input, "checks", 1, MaxRecordCount);
            var typed = contract.SelectToken("extensions.typedDiagnostics") as JObject;
            if (typed == null) throw new InvalidDataException("S3 Contract omits the typedDiagnostics metric-plan registry.");
            var plans = ReadFrozenMetricPlans(typed);
            var checkIds = new HashSet<string>(StringComparer.Ordinal);
            var consumed = new HashSet<string>(StringComparer.Ordinal);
            var observedKinds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in checks)
            {
                var check = RequiredArrayObject(token, "S3 metrics check");
                var id = RequiredMetricToken(check, "id");
                var kind = RequiredMetricToken(check, "kind");
                if (!checkIds.Add(id)) throw new InvalidDataException("S3 metrics checks repeat an ID.");
                if (!plans.TryGetValue(kind, out var block))
                    throw new InvalidDataException("S3 metrics check kind is unknown or lacks an exact Contract metricPlan binding: " + kind);
                RequireCheckIdMatchesPlan(id, RequiredObject(block, "metricPlan"));
                CollectExactMetricEvidenceReferences(check, kind, byId, consumed, block, legacyMultiviewMinDepthSpan);
                observedKinds.Add(kind);
            }
            if (!observedKinds.SetEquals(plans.Keys))
                throw new InvalidDataException("S3 frozen checks and Contract metricPlan kinds are not an exact mapping.");
            if (!ids.SetEquals(consumed)) throw new InvalidDataException("S3 metrics checks do not consume every and only typed evidence ID.");
            VerifyFrozenRequirementCheckMapping(plans, checks.OfType<JObject>().ToArray(), contract);
        }

        private static void VerifyFrozenRequirementCheckMapping(Dictionary<string, JObject> plans, JObject[] checks, JObject contract)
        {
            var contractRequirements = new HashSet<string>(RequiredArray(contract, "requirements", 1, MaxRecordCount)
                .OfType<JObject>().Select(item => (string)item["designRequirementId"]).Where(ProtocolToken), StringComparer.Ordinal);
            var assigned = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in plans)
            {
                var block = pair.Value;
                var matching = checks.Where(check => Same((string)check["kind"], pair.Key)).ToArray();
                if (matching.Length == 0) throw new InvalidDataException("S3 Contract metricPlan has no frozen input check: " + pair.Key);
                var requirements = new List<string>();
                var single = (string)block["requirementId"];
                if (!string.IsNullOrEmpty(single)) requirements.Add(single);
                requirements.AddRange((block["requirementIds"] as JArray ?? new JArray()).Values<string>().Where(value => !string.IsNullOrEmpty(value)));
                requirements = requirements.Distinct(StringComparer.Ordinal).ToList();
                if (requirements.Count == 0 || requirements.Any(item => !contractRequirements.Contains(item)))
                    throw new InvalidDataException("S3 Contract metricPlan requirement binding is absent from the Contract requirements registry.");
                if (requirements.Count == 1)
                {
                    foreach (var check in matching) assigned.Add((string)check["id"]);
                    continue;
                }

                var receiverRecords = (block["receiverIds"] as JArray ?? new JArray()).OfType<JObject>().ToArray();
                var declaredReceivers = new HashSet<long>();
                foreach (var receiver in receiverRecords)
                    if (!declaredReceivers.Add(RequiredLong(receiver, "id", 0, int.MaxValue))) throw new InvalidDataException("S3 Contract receiverIds repeat or are invalid.");
                var explicitMappings = (block["perRequirementCheckMapping"] as JArray ?? new JArray()).OfType<JObject>().ToArray();
                if (explicitMappings.Length != 0)
                {
                    var declaredRequirements = new HashSet<string>(requirements, StringComparer.Ordinal);
                    var mappedRequirements = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var mapping in explicitMappings)
                    {
                        RequireExactly(mapping, "requirementId", "receiverIds");
                        var requirement = RequiredString(mapping, "requirementId", 128);
                        if (!declaredRequirements.Contains(requirement) || !mappedRequirements.Add(requirement))
                            throw new InvalidDataException("S3 per-requirement check mapping repeats or names an undeclared requirement.");
                        var receivers = RequiredArray(mapping, "receiverIds", 1, 512);
                        var accepted = new HashSet<long>();
                        for (var index = 0; index < receivers.Count; index++)
                        {
                            var receiver = RequiredArrayNumber(receivers[index], 0, int.MaxValue, "mapped receiver ID");
                            if (!accepted.Add(receiver) || !declaredReceivers.Contains(receiver))
                                throw new InvalidDataException("S3 per-requirement check mapping contains a duplicate or unknown receiver ID.");
                        }
                        var resolved = matching.Where(check => check["receiverId"] != null && accepted.Contains(RequiredLong(check, "receiverId", 0, int.MaxValue))).ToArray();
                        if (resolved.Length == 0) throw new InvalidDataException("S3 per-requirement mapping resolves no frozen check.");
                        foreach (var check in resolved) assigned.Add((string)check["id"]);
                    }
                    if (!declaredRequirements.SetEquals(mappedRequirements))
                        throw new InvalidDataException("S3 per-requirement check mapping does not cover the exact Contract requirement set.");
                    continue;
                }

                if (receiverRecords.Length != requirements.Count)
                    throw new InvalidDataException("S3 multi-requirement metricPlan lacks an unambiguous receiver/check mapping.");
                for (var index = 0; index < requirements.Count; index++)
                {
                    var receiver = RequiredLong(receiverRecords[index], "id", 0, int.MaxValue);
                    var resolved = matching.Where(check => check["receiverId"] != null && RequiredLong(check, "receiverId", 0, int.MaxValue) == receiver).ToArray();
                    if (resolved.Length == 0) throw new InvalidDataException("S3 receiver requirement resolves no frozen check.");
                    foreach (var check in resolved) assigned.Add((string)check["id"]);
                }
            }
            var checkIds = new HashSet<string>(checks.Select(check => (string)check["id"]), StringComparer.Ordinal);
            if (!checkIds.SetEquals(assigned)) throw new InvalidDataException("Every S3 frozen check must map to an explicit Contract requirement metricPlan.");
        }

        private static Dictionary<string, JObject> ReadFrozenMetricPlans(JObject typed)
        {
            var output = new Dictionary<string, JObject>(StringComparer.Ordinal);
            var metricsTool = RequiredObject(typed, "metricsTool");
            RequireExactly(metricsTool, "path", "sha256");
            var metricsToolPath = RequiredRepositoryPath(metricsTool, "path");
            RequiredHash(metricsTool, "sha256");
            foreach (var property in typed.Properties())
            {
                if (Same(property.Name, "metricsTool") || Same(property.Name, "metricsEnvironment") || Same(property.Name, "requiredEvidenceMatrix")) continue;
                var block = property.Value as JObject;
                var plan = block == null ? null : block["metricPlan"] as JObject;
                if (plan == null) continue;
                var kind = RequiredMetricToken(plan, "kind");
                if (!KnownMetricKind(kind) || output.ContainsKey(kind))
                    throw new InvalidDataException("S3 Contract contains an unknown or duplicate metricPlan kind: " + kind);
                if (!Same(RequiredRepositoryPath(plan, "tool"), metricsToolPath))
                    throw new InvalidDataException("S3 Contract metricPlan tool differs from typedDiagnostics.metricsTool.");
                RequireExactString(plan, "bridge", "W24MetricsEvidenceDag");
                RequireExactMetricPlanInputFields(plan, kind);
                RequiredString(plan, "checkIdPattern", 256);
                var requirementCount = 0;
                var single = block["requirementId"];
                if (single != null)
                {
                    if (single.Type != JTokenType.String || !ProtocolToken((string)single))
                        throw new InvalidDataException("S3 Contract metricPlan has an invalid requirementId.");
                    requirementCount++;
                }
                var multiple = block["requirementIds"] as JArray;
                if (multiple != null)
                {
                    var unique = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var item in multiple)
                    {
                        if (item.Type != JTokenType.String || !ProtocolToken((string)item) || !unique.Add((string)item))
                            throw new InvalidDataException("S3 Contract metricPlan has invalid or duplicate requirementIds.");
                    }
                    requirementCount += unique.Count;
                }
                if (requirementCount == 0) throw new InvalidDataException("S3 Contract metricPlan is not bound to any requirement.");
                output.Add(kind, block);
            }
            if (output.Count == 0) throw new InvalidDataException("S3 Contract has no supported frozen metricPlan.");
            return output;
        }

        private static void RequireExactMetricPlanInputFields(JObject plan, string kind)
        {
            var expected = MetricPlanInputFields(kind);
            var actual = RequiredArray(plan, "inputFields", expected.Length, expected.Length);
            var values = actual.Select((item, index) => item.Type == JTokenType.String ? (string)item : null).ToArray();
            if (values.Any(string.IsNullOrEmpty) || !values.SequenceEqual(expected, StringComparer.Ordinal))
                throw new InvalidDataException("S3 Contract metricPlan inputFields differ from the frozen kind-specific mapping: " + kind);
        }

        private static string[] MetricPlanInputFields(string kind)
        {
            switch (kind)
            {
                case "trail": return new[] { "trail", "historyProjectedPx", "radiusPx", "maxMeanNearestDistancePx", "minCorridorCoverage" };
                case "fragment_tracks": return new[] { "frames", "fragmentIds", "maxTrajectoryCorrelation", "minPairwiseDistanceVariationRatio", "rejectSingleRigidBody" };
                case "multiview_3d": return new[] { "views.objectIds", "views.depth", "objectId", "minDepthSpan", "minParallaxPx", "requireParallax" };
                case "receiver_luminance_ldr": return new[] { "on", "off", "receiverIds", "effectMask", "receiverId", "minLinearLuminanceDelta" };
                default: throw new InvalidDataException("Unsupported S3 metricPlan kind: " + kind);
            }
        }

        private static bool KnownMetricKind(string kind)
        {
            return Same(kind, "trail") || Same(kind, "fragment_tracks") || Same(kind, "multiview_3d") || Same(kind, "receiver_luminance_ldr");
        }

        private static void RequireCheckIdMatchesPlan(string checkId, JObject plan)
        {
            var pattern = RequiredString(plan, "checkIdPattern", 256);
            var expression = "^" + Regex.Escape(pattern)
                .Replace("\\{seed}", "[0-9]+")
                .Replace("\\{logicalFrame}", "[0-9]+")
                .Replace("\\{objectId}", "[0-9]+")
                .Replace("\\{receiver}", "[a-z0-9_.-]+") + "$";
            if (expression.IndexOf("\\{", StringComparison.Ordinal) >= 0 || !Regex.IsMatch(checkId, expression, RegexOptions.CultureInvariant))
                throw new InvalidDataException("S3 metrics check ID does not match its Contract-frozen checkIdPattern.");
        }

        private static void CollectExactMetricEvidenceReferences(JObject check, string kind, Dictionary<string, JObject> registry, HashSet<string> consumed, JObject block, double legacyMultiviewMinDepthSpan)
        {
            switch (kind)
            {
                case "trail":
                    RequireExactly(check, "id", "kind", "trail", "historyProjectedPx", "radiusPx", "maxMeanNearestDistancePx", "minCorridorCoverage");
                    var trailEvidence = AddMetricEvidenceReference(check, "trail", registry, consumed);
                    var history = RequiredArray(check, "historyProjectedPx", 1, 4096);
                    foreach (var point in history)
                    {
                        var pair = point as JArray;
                        if (pair == null || pair.Count != 2) throw new InvalidDataException("S3 trail historyProjectedPx must contain exact two-number points.");
                        RequiredFiniteNumber(pair[0], "trail history x"); RequiredFiniteNumber(pair[1], "trail history y");
                    }
                    RequiredFiniteNumber(check["radiusPx"], "trail radiusPx");
                    RequiredFiniteNumber(check["maxMeanNearestDistancePx"], "trail maxMeanNearestDistancePx");
                    RequiredFiniteNumber(check["minCorridorCoverage"], "trail minCorridorCoverage");
                    VerifyTrailContractProjection(check, block, trailEvidence, history);
                    break;
                case "fragment_tracks":
                    RequireExactly(check, "id", "kind", "frames", "fragmentIds", "maxTrajectoryCorrelation", "minPairwiseDistanceVariationRatio", "rejectSingleRigidBody");
                    var frameEvidence = AddMetricEvidenceArray(check, "frames", 2, registry, consumed);
                    var fragmentIds = RequiredArray(check, "fragmentIds", 2, 512);
                    var seenFragments = new HashSet<long>();
                    for (var index = 0; index < fragmentIds.Count; index++)
                        if (!seenFragments.Add(RequiredArrayNumber(fragmentIds[index], 0, uint.MaxValue, "fragment ID"))) throw new InvalidDataException("S3 fragment IDs repeat.");
                    RequiredFiniteNumber(check["maxTrajectoryCorrelation"], "maxTrajectoryCorrelation");
                    RequiredFiniteNumber(check["minPairwiseDistanceVariationRatio"], "minPairwiseDistanceVariationRatio");
                    if (!RequiredBool(check, "rejectSingleRigidBody")) throw new InvalidDataException("S3 fragment_tracks must reject a single rigid body.");
                    VerifyFragmentContractProjection(check, block, frameEvidence, fragmentIds);
                    break;
                case "multiview_3d":
                    RequireExactly(check, "id", "kind", "objectId", "carrier", "minDepthSpan", "minParallaxPx", "requireParallax", "views");
                    RequiredLong(check, "objectId", 0, uint.MaxValue); RequiredString(check, "carrier", 64);
                    RequiredFiniteNumber(check["minDepthSpan"], "multiview minDepthSpan"); RequiredFiniteNumber(check["minParallaxPx"], "multiview minParallaxPx"); RequiredBool(check, "requireParallax");
                    var viewEvidence = new List<Tuple<JObject, JObject>>();
                    foreach (var view in RequiredArray(check, "views", 2, 16))
                    {
                        var objectValue = RequiredArrayObject(view, "multiview view"); RequireExactly(objectValue, "objectIds", "depth");
                        viewEvidence.Add(Tuple.Create(
                            AddMetricEvidenceReference(objectValue, "objectIds", registry, consumed),
                            AddMetricEvidenceReference(objectValue, "depth", registry, consumed)));
                    }
                    VerifyMultiviewContractProjection(check, block, viewEvidence, legacyMultiviewMinDepthSpan);
                    break;
                case "receiver_luminance_ldr":
                    RequireExactly(check, "id", "kind", "on", "off", "receiverIds", "effectMask", "receiverId", "minLinearLuminanceDelta");
                    var receiverEvidence = new[]
                    {
                        AddMetricEvidenceReference(check, "on", registry, consumed),
                        AddMetricEvidenceReference(check, "off", registry, consumed),
                        AddMetricEvidenceReference(check, "receiverIds", registry, consumed),
                        AddMetricEvidenceReference(check, "effectMask", registry, consumed)
                    };
                    var receiverId = RequiredLong(check, "receiverId", 0, int.MaxValue); RequiredFiniteNumber(check["minLinearLuminanceDelta"], "minimum receiver luminance delta");
                    var declaredReceivers = block["receiverIds"] as JArray;
                    if (declaredReceivers == null || !declaredReceivers.OfType<JObject>().Any(item => (long?)item["id"] == receiverId))
                        throw new InvalidDataException("S3 receiver check names an ID absent from its Contract block.");
                    VerifyReceiverContractProjection(check, block, receiverEvidence, receiverId);
                    break;
                default:
                    throw new InvalidDataException("Unsupported S3 metrics check kind: " + kind);
            }
        }

        private static JObject[] AddMetricEvidenceArray(JObject check, string field, int minimum, Dictionary<string, JObject> registry, HashSet<string> consumed)
        {
            var output = new List<JObject>();
            foreach (var token in RequiredArray(check, field, minimum, MaxRecordCount))
            {
                if (token.Type != JTokenType.String || !MetricToken((string)token) || !registry.TryGetValue((string)token, out var evidence))
                    throw new InvalidDataException("S3 metrics check has an unknown evidence reference in " + field + ".");
                consumed.Add((string)token);
                output.Add(evidence);
            }
            return output.ToArray();
        }

        private static JObject AddMetricEvidenceReference(JObject check, string field, Dictionary<string, JObject> registry, HashSet<string> consumed)
        {
            var id = RequiredMetricToken(check, field);
            if (!registry.TryGetValue(id, out var evidence)) throw new InvalidDataException("S3 metrics check has an unknown evidence reference in " + field + ".");
            consumed.Add(id);
            return evidence;
        }

        private static void VerifyTrailContractProjection(JObject check, JObject block, JObject evidence, JArray history)
        {
            var thresholds = RequiredObject(block, "thresholds");
            RequireSameFiniteNumber(check, "radiusPx", thresholds, "corridorRadiusPixels", "trail corridor radius");
            RequireSameFiniteNumber(check, "maxMeanNearestDistancePx", thresholds, "maximumMeanNearestHistoryDistancePixels", "trail nearest-history threshold");
            RequireSameFiniteNumber(check, "minCorridorCoverage", thresholds, "corridorCoverageMinimum", "trail corridor coverage threshold");
            if (history.Count < RequiredLong(thresholds, "minimumHistorySamples", 1, 4096))
                throw new InvalidDataException("S3 trail history has fewer samples than the Contract threshold.");

            var plan = RequiredObject(block, "metricPlan");
            var seedPlan = RequiredObject(block, "seedConsumptionPlan");
            var seed = RequiredLong(evidence, "seed", 0, uint.MaxValue);
            var frame = RequiredLong(evidence, "logicalFrameIndex", 0, int.MaxValue);
            var seeds = RequiredLongSequence(seedPlan, "orderedSeeds", 1, MaxRecordCount, 0, uint.MaxValue, "trail ordered seed");
            var frames = RequiredLongSequence(plan, "retainedTravelFrames", 1, MaxRecordCount, 0, int.MaxValue, "trail retained frame");
            if (!seeds.Contains(seed) || !frames.Contains(frame)) throw new InvalidDataException("S3 trail check seed/frame is outside the Contract-frozen plan.");
            var view = RequiredObject(block, "frozenView");
            var viewId = RequiredProtocolToken(view, "viewId");
            RequireEvidenceContext(evidence, seed, frame, viewId, "trail evidence");
            RequireExactCheckId(RequiredMetricToken(check, "id"), plan,
                new KeyValuePair<string, string>("seed", seed.ToString(CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("logicalFrame", frame.ToString(CultureInfo.InvariantCulture)));
        }

        private static void VerifyFragmentContractProjection(JObject check, JObject block, JObject[] evidence, JArray fragmentIds)
        {
            var expectedFragments = RequiredLongSequence(block, "fragmentIds", 2, 512, 0, uint.MaxValue, "fragment ID");
            var actualFragments = fragmentIds.Select(item => RequiredArrayNumber(item, 0, uint.MaxValue, "fragment ID")).ToArray();
            if (!actualFragments.SequenceEqual(expectedFragments)) throw new InvalidDataException("S3 fragment IDs differ from the Contract-frozen ordered IDs.");
            var thresholds = RequiredObject(block, "thresholds");
            RequireSameFiniteNumber(check, "maxTrajectoryCorrelation", thresholds, "maxTrajectoryCorrelation", "fragment trajectory threshold");
            RequireSameFiniteNumber(check, "minPairwiseDistanceVariationRatio", thresholds, "minPairwiseDistanceVariationRatio", "fragment distance threshold");
            if (RequiredBool(check, "rejectSingleRigidBody") != RequiredBool(thresholds, "rejectSingleRigidBody"))
                throw new InvalidDataException("S3 fragment single-rigid-body policy differs from the Contract.");

            var expectedFrames = RequiredLongSequence(block, "frames", 2, MaxRecordCount, 0, int.MaxValue, "fragment logical frame");
            if (evidence.Length != expectedFrames.Length) throw new InvalidDataException("S3 fragment evidence frame count differs from the Contract.");
            var seed = RequiredLong(evidence[0], "seed", 0, uint.MaxValue);
            var viewId = RequiredProtocolToken(block, "frontViewId");
            for (var index = 0; index < evidence.Length; index++) RequireEvidenceContext(evidence[index], seed, expectedFrames[index], viewId, "fragment evidence");
            RequireExactCheckId(RequiredMetricToken(check, "id"), RequiredObject(block, "metricPlan"),
                new KeyValuePair<string, string>("seed", seed.ToString(CultureInfo.InvariantCulture)));
        }

        private static void VerifyMultiviewContractProjection(JObject check, JObject block, List<Tuple<JObject, JObject>> evidence, double legacyMultiviewMinDepthSpan)
        {
            var objectId = RequiredLong(check, "objectId", 0, uint.MaxValue);
            var requiredObjects = RequiredArray(block, "requiredObjectIds", 1, 512).Select(item =>
            {
                var value = RequiredArrayObject(item, "required multiview object");
                return RequiredLong(value, "id", 0, uint.MaxValue);
            }).ToArray();
            if (requiredObjects.Distinct().Count() != requiredObjects.Length || !requiredObjects.Contains(objectId))
                throw new InvalidDataException("S3 multiview object ID is absent from the Contract-frozen registry.");
            var parallaxIds = RequiredLongSequence(block, "parallaxRequiredObjectIds", 0, 512, 0, uint.MaxValue, "parallax-required object ID");
            if (RequiredBool(check, "requireParallax") != parallaxIds.Contains(objectId))
                throw new InvalidDataException("S3 multiview parallax policy differs from the Contract object registry.");
            var thresholds = RequiredObject(block, "thresholds");
            RequireSameFiniteNumber(check, "minParallaxPx", thresholds, "minimumCentroidParallaxPixelsAcrossViews", "multiview parallax threshold");
            if (RequiredFiniteNumber(check["minDepthSpan"], "legacy multiview minDepthSpan") != legacyMultiviewMinDepthSpan)
                throw new InvalidDataException("S3 legacy multiview minDepthSpan differs from the gate-owned capture-tool policy.");
            if (!Same(RequiredString(check, "carrier", 64), "mesh"))
                throw new InvalidDataException("S3 multiview fixed carrier projection is noncanonical.");

            var views = RequiredArray(block, "frozenViews", 2, 16).Select(item => RequiredArrayObject(item, "frozen multiview view")).ToArray();
            if (views.Length != evidence.Count) throw new InvalidDataException("S3 multiview evidence view count differs from the Contract.");
            var plan = RequiredObject(block, "metricPlan");
            var frame = RequiredLong(plan, "logicalFrame", 0, int.MaxValue);
            var seedPlan = RequiredObject(block, "seedConsumptionPlan");
            var seeds = RequiredLongSequence(seedPlan, "orderedSeeds", 1, MaxRecordCount, 0, uint.MaxValue, "multiview ordered seed");
            var seed = RequiredLong(evidence[0].Item1, "seed", 0, uint.MaxValue);
            if (!seeds.Contains(seed)) throw new InvalidDataException("S3 multiview evidence seed is outside the Contract plan.");
            for (var index = 0; index < evidence.Count; index++)
            {
                var viewId = RequiredProtocolToken(views[index], "viewId");
                var objectIds = evidence[index].Item1;
                var depth = evidence[index].Item2;
                RequireEvidenceSlot(objectIds, "object-id", "id_uint", "-object-id-", "_object-id.npy", "w24diagnosticobjectregistration", "multiview objectIds");
                RequireEvidenceSlot(depth, "depth-linear", "linear_float", "-depth-", "_linear-depth.npy", RequiredLocalPath(objectIds, "path"), "multiview depth");
                RequireEvidenceContext(objectIds, seed, frame, viewId, "multiview object-ID evidence");
                RequireEvidenceContext(depth, seed, frame, viewId, "multiview depth evidence");
            }
            RequireExactCheckId(RequiredMetricToken(check, "id"), plan,
                new KeyValuePair<string, string>("seed", seed.ToString(CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("objectId", objectId.ToString(CultureInfo.InvariantCulture)));
        }

        private static void VerifyReceiverContractProjection(JObject check, JObject block, JObject[] evidence, long receiverId)
        {
            var receivers = RequiredArray(block, "receiverIds", 1, 512).Select(item => RequiredArrayObject(item, "Contract receiver ID")).ToArray();
            var receiver = receivers.SingleOrDefault(item => (long?)item["id"] == receiverId);
            if (receiver == null) throw new InvalidDataException("S3 receiver check is absent from the Contract-frozen receiver registry.");
            var thresholds = RequiredObject(block, "thresholds");
            RequireSameFiniteNumber(check, "minLinearLuminanceDelta", thresholds, "minimumLinearLuminanceDelta", "receiver luminance threshold");
            var seedPlan = RequiredObject(block, "seedConsumptionPlan");
            var seeds = RequiredLongSequence(seedPlan, "orderedSeeds", 1, MaxRecordCount, 0, uint.MaxValue, "receiver ordered seed");
            var frame = RequiredLong(seedPlan, "logicalFrame", 0, int.MaxValue);
            var seed = RequiredLong(evidence[0], "seed", 0, uint.MaxValue);
            if (!seeds.Contains(seed)) throw new InvalidDataException("S3 receiver evidence seed is outside the Contract plan.");
            var viewId = RequiredProtocolToken(RequiredObject(block, "frozenView"), "viewId");
            RequireEvidenceSlot(evidence[0], "receiver-linear-ldr", "linear_ldr", "-receiver-on-", "_receiver-on-linear-ldr.npy", RequiredLocalPath(evidence[1], "path"), "receiver on");
            RequireEvidenceSlot(evidence[1], "receiver-linear-ldr", "linear_ldr", "-receiver-off-", "_receiver-off-linear-ldr.npy", RequiredLocalPath(evidence[2], "path"), "receiver off");
            RequireEvidenceSlot(evidence[2], "receiver-id", "id_uint", "-receiver-id-", "_receiver-id.npy", "receiver-probe-registration", "receiverIds");
            RequireEvidenceSlot(evidence[3], "effect-mask", "mask_binary", "-effect-mask-", "_effect-mask.npy", "runtime-entry-renderer-set", "effectMask");
            foreach (var item in evidence) RequireEvidenceContext(item, seed, frame, viewId, "receiver evidence");
            var role = RequiredMetricToken(receiver, "role");
            if (!role.StartsWith("receiver_", StringComparison.Ordinal) || role.Length == "receiver_".Length)
                throw new InvalidDataException("S3 Contract receiver role cannot bind the checkIdPattern.");
            RequireExactCheckId(RequiredMetricToken(check, "id"), RequiredObject(block, "metricPlan"),
                new KeyValuePair<string, string>("receiver", role.Substring("receiver_".Length)),
                new KeyValuePair<string, string>("seed", seed.ToString(CultureInfo.InvariantCulture)));
        }

        private static void RequireEvidenceSlot(JObject evidence, string passId, string encoding, string idMarker, string pathSuffix, string derivedFrom, string label)
        {
            var id = RequiredMetricToken(evidence, "id");
            var path = RequiredLocalPath(evidence, "path");
            if (!Same(RequiredProtocolToken(evidence, "passId"), passId)
                || !Same(RequiredProtocolToken(evidence, "encoding"), encoding)
                || id.IndexOf(idMarker, StringComparison.Ordinal) < 0
                || !path.EndsWith(pathSuffix, StringComparison.Ordinal)
                || !Same(RequiredString(evidence, "derivedFrom", 4096), derivedFrom))
                throw new InvalidDataException("S3 " + label + " evidence does not bind its exact passId/encoding/semantic slot/provenance role.");
        }

        private static void RequireEvidenceContext(JObject evidence, long seed, long frame, string viewId, string label)
        {
            if (RequiredLong(evidence, "seed", 0, uint.MaxValue) != seed
                || RequiredLong(evidence, "logicalFrameIndex", 0, int.MaxValue) != frame
                || !Same(RequiredProtocolToken(evidence, "viewId"), viewId))
                throw new InvalidDataException("S3 " + label + " differs from the Contract-frozen seed/frame/view projection.");
        }

        private static long[] RequiredLongSequence(JObject value, string field, int minimumCount, int maximumCount, long minimum, long maximum, string label)
        {
            var array = RequiredArray(value, field, minimumCount, maximumCount);
            var output = new long[array.Count];
            var seen = new HashSet<long>();
            for (var index = 0; index < array.Count; index++)
            {
                output[index] = RequiredArrayNumber(array[index], minimum, maximum, label);
                if (!seen.Add(output[index])) throw new InvalidDataException("S3 Contract repeats " + label + ".");
            }
            return output;
        }

        private static void RequireSameFiniteNumber(JObject actual, string actualField, JObject expected, string expectedField, string label)
        {
            if (RequiredFiniteNumber(actual[actualField], label) != RequiredFiniteNumber(expected[expectedField], "Contract " + label))
                throw new InvalidDataException("S3 " + label + " differs from the Contract-frozen value.");
        }

        private static void RequireExactCheckId(string checkId, JObject plan, params KeyValuePair<string, string>[] replacements)
        {
            var expected = RequiredString(plan, "checkIdPattern", 256);
            foreach (var replacement in replacements) expected = expected.Replace("{" + replacement.Key + "}", replacement.Value);
            if (Regex.IsMatch(expected, "\\{[^{}]+\\}", RegexOptions.CultureInvariant) || !Same(checkId, expected))
                throw new InvalidDataException("S3 metrics check ID does not exactly project its Contract-frozen seed/frame/object/receiver identity.");
        }

        private static void VerifyMetricsReport(JObject report, JObject input, string inputHash, string expectedTool)
        {
            var route = RequiredString(report, "route", 64);
            if (Same(route, "MEASURED")) RequireExactly(report, "schema", "route", "machineGatesPassed", "checks", "inputSha256", "toolSha256", "sealedReportEncoding", "sealedReportHash");
            else if (Same(route, "EVIDENCE_INVALID")) RequireExactly(report, "schema", "route", "machineGatesPassed", "reason", "checks", "inputSha256", "toolSha256", "sealedReportEncoding", "sealedReportHash");
            else throw new InvalidDataException("Captured metrics report route is unsupported.");
            RequireExactString(report, "schema", MetricsReportSchema);
            if (!Same(RequiredHash(report, "inputSha256"), inputHash) || !Same(RequiredHash(report, "toolSha256"), expectedTool))
                throw new InvalidDataException("Captured metrics report input/tool identity is invalid.");
            RequireExactString(report, "sealedReportEncoding", W24TypedBinaryCanonicalEncoding.EncodingName);
            var clone = (JObject)report.DeepClone(); var claimed = RequiredHash(clone, "sealedReportHash"); clone.Remove("sealedReportHash");
            if (!W24TypedBinaryCanonicalEncoding.Verify(claimed, NormalizeTypedNumbers(clone))) throw new InvalidDataException("Captured metrics report typed self-seal is invalid.");
            var resultTokens = RequiredArray(report, "checks", 0, MaxRecordCount);
            var results = resultTokens.Select(item => RequiredArrayObject(item, "captured metrics report check")).ToArray();
            if (Same(route, "EVIDENCE_INVALID"))
            {
                RequiredString(report, "reason", 16384);
                if (RequiredBool(report, "machineGatesPassed") || resultTokens.Count != 0) throw new InvalidDataException("EVIDENCE_INVALID metrics report must be non-passing and check-free.");
                return;
            }
            var specs = RequiredArray(input, "checks", 1, MaxRecordCount).OfType<JObject>().ToDictionary(item => (string)item["id"], item => (string)item["kind"], StringComparer.Ordinal);
            var resultIds = new HashSet<string>(StringComparer.Ordinal); var allPass = true;
            foreach (var result in results)
            {
                var id = RequiredProtocolToken(result, "id"); var kind = RequiredProtocolToken(result, "kind");
                var pass = RequiredBool(result, "pass"); allPass &= pass;
                if (!resultIds.Add(id) || !specs.TryGetValue(id, out var expectedKind) || !Same(kind, expectedKind))
                    throw new InvalidDataException("MEASURED metrics result IDs/kinds differ from frozen checks.");
            }
            if (!resultIds.SetEquals(specs.Keys) || RequiredBool(report, "machineGatesPassed") != allPass)
                throw new InvalidDataException("MEASURED metrics report check set or aggregate gate bit is inconsistent.");
        }

        private static JArray SnapshotSources(Prepared prepared, PrepareBudget budget, string revisionRoot, string snapshotRoot, List<SourceReplay> sources)
        {
            var output = new JArray();
            foreach (var source in sources.OrderBy(item => item.Ordinal))
            {
                var path = snapshotRoot + "/" + source.Ordinal.ToString("D4", CultureInfo.InvariantCulture) + ".source";
                AddPreparedFile(prepared, budget, LocalSnapshotPath(path, revisionRoot), source.Bytes);
                output.Add(new JObject
                {
                    ["ordinal"] = source.Ordinal,
                    ["sourcePath"] = source.Path,
                    ["sourceSha256"] = source.Hash,
                    ["snapshotPath"] = path,
                    ["snapshotFileHash"] = Hash(source.Bytes)
                });
            }
            return output;
        }

        private static string SourceSetTypedHash(IEnumerable<SourceReplay> sources)
        {
            var value = new JObject
            {
                ["schema"] = SourceSetSchema,
                ["sources"] = new JArray(sources.OrderBy(item => item.Ordinal).Select(item => new JObject
                {
                    ["ordinal"] = item.Ordinal, ["path"] = item.Path, ["sha256"] = item.Hash
                }))
            };
            return W24TypedBinaryCanonicalEncoding.Hash(value);
        }

        private static JArray RequireArrayForReadback(JObject value, string field)
        {
            return RequiredArray(value, field, 0, MaxRecordCount);
        }

        private static void WritePreparedTree(Prepared prepared, string pending)
        {
            foreach (var pair in prepared.Files.Where(item => !Same(item.Key, DescriptorName)).OrderBy(item => item.Key, StringComparer.Ordinal))
                WriteNew(Path.Combine(pending, pair.Key.Replace('/', Path.DirectorySeparatorChar)), pair.Value);
            var descriptor = prepared.Files[DescriptorName];
            WriteNew(Path.Combine(pending, DescriptorName), descriptor); // descriptor is always last
        }

        private static void VerifyPreparedTree(Prepared prepared, string root)
        {
            EnsureDirectory(root, "descriptor tree", RepositoryRoot());
            var actual = EnumeratePublishedTree(root);
            if (!new HashSet<string>(prepared.ExpectedFiles.Keys, StringComparer.Ordinal).SetEquals(actual.Keys))
                throw new InvalidDataException("Descriptor tree contains a missing, extra, or path-drifted file.");
            foreach (var pair in prepared.ExpectedFiles)
            {
                var identity = actual[pair.Key];
                if (!Same(identity.Hash, pair.Value.Hash) || identity.Length != pair.Value.Length)
                    throw new InvalidDataException("Descriptor snapshot bytes changed during readback: " + pair.Key);
            }
            var descriptorFile = ReadAbsoluteUnpinned(Path.Combine(root, DescriptorName), "descriptor readback", MaxJsonBytes);
            var descriptor = Parse(descriptorFile.Bytes, "descriptor readback");
            RequireExactly(descriptor, "schema", "descriptorStatus", "writer", "effectId", "candidateId", "candidateRevision", "contractRevision", "evidenceRevision", "candidate", "rawCapture", "captureTool", "evaluationInput", "predecessor", "selfHashEncoding", "selfHash");
            RequireExactString(descriptor, "descriptorStatus", DescriptorStatus);
            RequireExactString(descriptor, "selfHashEncoding", W24TypedBinaryCanonicalEncoding.EncodingName);
            ValidateDescriptorCompiledSemantics(descriptor, prepared);
            var clone = (JObject)descriptor.DeepClone(); var claimed = RequiredHash(clone, "selfHash"); clone.Remove("selfHash");
            if (!W24TypedBinaryCanonicalEncoding.Verify(claimed, NormalizeTypedNumbers(clone))) throw new InvalidDataException("Descriptor typed self-hash failed readback.");
            if (!Same(descriptorFile.Hash, prepared.DescriptorHash)) throw new InvalidDataException("Descriptor physical bytes failed readback.");
            RequireArrayForReadback(RequiredObject(descriptor, "writer"), "sourceSnapshots");
            RequireArrayForReadback(RequiredObject(descriptor, "captureTool"), "sourceSnapshots");
        }

        private static Dictionary<string, FileIdentity> EnumeratePublishedTree(string absoluteRoot)
        {
            var output = new Dictionary<string, FileIdentity>(StringComparer.Ordinal);
            var pending = new Stack<Tuple<string, string, int>>(); pending.Push(Tuple.Create(absoluteRoot, string.Empty, 0));
            var directories = 1;
            while (pending.Count != 0)
            {
                var current = pending.Pop(); EnsureDirectory(current.Item1, "descriptor directory", RepositoryRoot());
                foreach (var entry in Directory.EnumerateFileSystemEntries(current.Item1))
                {
                    RejectReparse(entry);
                    var local = string.IsNullOrEmpty(current.Item2) ? Path.GetFileName(entry) : current.Item2 + "/" + Path.GetFileName(entry);
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        if (++directories > 64 || current.Item3 + 1 > 8) throw new InvalidDataException("Descriptor tree exceeds its directory/depth bound.");
                        pending.Push(Tuple.Create(entry, local, current.Item3 + 1));
                    }
                    else
                    {
                        if (output.Count >= 512) throw new InvalidDataException("Descriptor tree exceeds its file-count bound.");
                        var file = ReadAbsoluteUnpinned(entry, "descriptor snapshot", MaxSnapshotSourceBytes);
                        output.Add(local, new FileIdentity { LocalPath = local, Hash = file.Hash, Length = file.Length });
                    }
                }
            }
            return output;
        }

        private static Dictionary<string, FileIdentity> EnumerateRawTree(string relativeRoot, HashSet<string> expectedFiles)
        {
            var absoluteRoot = RepositoryAbsolute(relativeRoot); EnsureDirectory(absoluteRoot, "legacy raw root", RepositoryRoot());
            if (expectedFiles == null || expectedFiles.Count == 0) throw new InvalidOperationException("Compiled raw evidence file registry is empty.");
            var expectedDirectories = ExpectedLocalDirectories(expectedFiles, "raw evidence");
            var actualDirectories = new HashSet<string>(StringComparer.Ordinal) { string.Empty };
            var output = new Dictionary<string, FileIdentity>(StringComparer.Ordinal);
            var pending = new Stack<Tuple<string, string, int>>(); pending.Push(Tuple.Create(absoluteRoot, string.Empty, 0));
            var directories = 1; long bytes = 0;
            while (pending.Count != 0)
            {
                var current = pending.Pop(); EnsureDirectory(current.Item1, "legacy raw directory", RepositoryRoot());
                foreach (var entry in Directory.EnumerateFileSystemEntries(current.Item1))
                {
                    RejectReparse(entry);
                    var local = string.IsNullOrEmpty(current.Item2) ? Path.GetFileName(entry) : current.Item2 + "/" + Path.GetFileName(entry);
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        if (string.IsNullOrEmpty(current.Item2) && Same(local, "bound")) continue;
                        if (++directories > MaxRawDirectories || current.Item3 + 1 > MaxRawDepth) throw new InvalidDataException("Legacy raw tree exceeds its directory/depth bound.");
                        if (!actualDirectories.Add(local)) throw new InvalidDataException("Legacy raw tree repeats a normalized directory path.");
                        pending.Push(Tuple.Create(entry, local, current.Item3 + 1));
                    }
                    else
                    {
                        if (output.Count >= MaxRawFiles || !SafeLocalPath(local)) throw new InvalidDataException("Legacy raw tree exceeds its file-count bound or has an unsafe path.");
                        var identity = HashRegularFile(entry, "raw artifact", MaxRawFileBytes);
                        bytes = checked(bytes + identity.Length); if (bytes > MaxRawBytes) throw new InvalidDataException("Legacy raw tree exceeds its total-byte bound.");
                        identity.LocalPath = local; if (output.ContainsKey(local)) throw new InvalidDataException("Legacy raw tree repeats a normalized path."); output.Add(local, identity);
                    }
                }
            }
            if (!actualDirectories.SetEquals(expectedDirectories))
                throw new InvalidDataException("Legacy raw tree contains a missing, extra, or empty undeclared directory (direct bound/ alone is excluded).");
            return output;
        }

        private static HashSet<string> ExpectedLocalDirectories(IEnumerable<string> files, string label)
        {
            var output = new HashSet<string>(StringComparer.Ordinal) { string.Empty };
            foreach (var file in files)
            {
                if (!SafeLocalPath(file)) throw new InvalidOperationException("Compiled " + label + " file registry contains an unsafe path.");
                var slash = file.LastIndexOf('/');
                while (slash > 0)
                {
                    var directory = file.Substring(0, slash);
                    output.Add(directory);
                    slash = directory.LastIndexOf('/');
                }
            }
            return output;
        }

        private static JObject ParseRawJson(RawReplay raw, string local, string label)
        {
            if (!raw.Files.TryGetValue(local, out var identity) || identity.Length > MaxJsonBytes) throw new InvalidDataException(label + " is missing or exceeds the JSON bound.");
            return Parse(ReadRepositoryPinned(raw.Root + "/" + local, identity.Hash, label, MaxJsonBytes).Bytes, label);
        }

        private static JObject SingleRecord(IEnumerable<JObject> values, string field, string expected, string file)
        {
            var matches = values.Where(item => Same((string)item[field], expected) && Same((string)item["file"], file)).ToArray();
            if (matches.Length != 1) throw new InvalidDataException("Required sealed S0b record is missing or duplicated: " + expected);
            return matches[0];
        }

        private static void AddDeclared(Dictionary<string, string> declared, Dictionary<string, string> seal, string file, string hash, string label)
        {
            if (declared.ContainsKey(file) || !seal.TryGetValue(file, out var sealedHash) || !Same(hash, sealedHash))
                throw new InvalidDataException(label + " is duplicate or not exactly hash-bound by the raw seal: " + file);
            declared.Add(file, hash);
        }

        private static W24S5LegacyRawReplayPins LegacyRawPins(Registry registry, BundleReplay capture)
        {
            return new W24S5LegacyRawReplayPins
            {
                CaptureToolBundlePath = registry.CaptureToolBundlePath,
                CaptureToolVersion = capture.ToolVersion,
                CaptureToolCanonicalHash = capture.CanonicalHash,
                AllowTypedS3Records = Same(registry.Route, S3Route)
#if UNITY_INCLUDE_TESTS
                , TreatPathAsReparsePointForTests = TreatPathAsReparsePointForTests
#endif
            };
        }

        private static void ValidateLegacyRawPins(
            W24S5CandidateEvidenceReader.CandidateReplayAuthority authority,
            W24S5LegacyRawReplayPins pins)
        {
            if (authority == null || !Same(authority.CandidateVersion, LegacyCandidateVersion)
                || !Same(authority.CandidateId, "C0") || authority.CandidateRevision != 0 || authority.EvidenceRevision != 1
                || pins == null || !SafeRepositoryPath(pins.CaptureToolBundlePath)
                || !VersionToken(pins.CaptureToolVersion) || !CanonicalHash(pins.CaptureToolCanonicalHash))
                throw new InvalidDataException("Shared raw replay requires one reader-issued legacy C0/E1 authority and exact capture-tool pins.");
        }

        private static void ValidateRequest(W24S5EvidenceRevisionWriteRequest request)
        {
            if (request == null || !SafeRepositoryPath(request.CandidateReceiptPath)
                || !request.CandidateReceiptPath.StartsWith("docs/vfx-candidates/", StringComparison.Ordinal)
                || !request.CandidateReceiptPath.EndsWith("/candidate-receipt.json", StringComparison.Ordinal)
                || !CanonicalHash(request.CandidateReceiptFileHash) || request.EvidenceRevision < 1 || request.EvidenceRevision > 2)
                throw new InvalidDataException("Writer request must pin one canonical candidate receipt and evidence revision E1 or E2.");
        }

        private static void RequireLegacyC0Authority(W24S5CandidateEvidenceReader.CandidateReplayAuthority authority)
        {
            if (authority == null || !Same(authority.CandidateVersion, LegacyCandidateVersion) || !Same(authority.CandidateId, "C0")
                || authority.CandidateRevision != 0 || authority.EvidenceRevision != 1)
                throw new InvalidDataException("Phase A only accepts reader-issued legacy C0/E1 candidate authority.");
        }

        private static bool TryResolveRegistry(string effectId, out Registry registry)
        {
#if UNITY_INCLUDE_TESTS
            lock (TestRegistrySync)
            {
                if (configuredTestRegistry != null && Same(configuredTestRegistry.EffectId, effectId))
                {
                    registry = CloneRegistry(configuredTestRegistry); return true;
                }
            }
#endif
            registry = null; return false;
        }

        private static Registry CloneRegistry(Registry value)
        {
            return new Registry
            {
                EffectId = value.EffectId, Route = value.Route, WriterId = value.WriterId, WriterVersion = value.WriterVersion,
                WriterBundlePath = value.WriterBundlePath, WriterBundleFileHash = value.WriterBundleFileHash, WriterBundleTypedHash = value.WriterBundleTypedHash,
                DescriptorSchemaId = value.DescriptorSchemaId, DescriptorSchemaPath = value.DescriptorSchemaPath, DescriptorSchemaFileHash = value.DescriptorSchemaFileHash,
                CaptureToolBundlePath = value.CaptureToolBundlePath, CaptureToolBundleFileHash = value.CaptureToolBundleFileHash,
                MetricsToolPath = value.MetricsToolPath, MetricsToolFileHash = value.MetricsToolFileHash,
                LegacyMultiviewMinDepthSpan = value.LegacyMultiviewMinDepthSpan, TestOnly = value.TestOnly
            };
        }

        private static void ValidateRegistryForAuthority(Registry registry, W24S5CandidateEvidenceReader.CandidateReplayAuthority authority)
        {
            ValidateRegistryShape(registry);
            if (!registry.TestOnly || !Same(registry.EffectId, authority.EffectId)) throw new InvalidDataException("Phase-A registry is not exact test-only authority for this effect.");
        }

        private static void ValidateRegistryShape(Registry value)
        {
            if (value == null || !Regex.IsMatch(value.EffectId ?? string.Empty, "^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$")
                || !Same(value.Route, S0bRoute) && !Same(value.Route, S3Route)
                || !DescriptorToken(value.WriterId) || !VersionToken(value.WriterVersion)
                || !SafeRepositoryPath(value.WriterBundlePath) || !CanonicalHash(value.WriterBundleFileHash) || !CanonicalHash(value.WriterBundleTypedHash)
                || !SafeRepositoryPath(value.DescriptorSchemaPath) || !CanonicalHash(value.DescriptorSchemaFileHash)
                || !SafeRepositoryPath(value.CaptureToolBundlePath) || !CanonicalHash(value.CaptureToolBundleFileHash))
                throw new InvalidDataException("Compiled evidence-revision registry shape is invalid.");
            var expectedSchema = Same(value.Route, S0bRoute) ? S0bSchema : S3Schema;
            if (!Same(value.DescriptorSchemaId, expectedSchema)) throw new InvalidDataException("Compiled registry route/schema ID differs.");
            if (Same(value.Route, S3Route) && (!SafeRepositoryPath(value.MetricsToolPath) || !CanonicalHash(value.MetricsToolFileHash)
                || !value.LegacyMultiviewMinDepthSpan.HasValue || double.IsNaN(value.LegacyMultiviewMinDepthSpan.Value)
                || double.IsInfinity(value.LegacyMultiviewMinDepthSpan.Value)))
                throw new InvalidDataException("Compiled S3 registry lacks an exact metrics-tool or legacy multiview capture-policy pin.");
            if (Same(value.Route, S0bRoute) && value.LegacyMultiviewMinDepthSpan.HasValue)
                throw new InvalidDataException("Compiled S0b registry cannot declare an S3 legacy multiview capture policy.");
        }

        private static void ValidateDescriptorCompiledSemantics(JObject descriptor, Prepared prepared)
        {
            RequireExactly(descriptor, "schema", "descriptorStatus", "writer", "effectId", "candidateId", "candidateRevision", "contractRevision", "evidenceRevision", "candidate", "rawCapture", "captureTool", "evaluationInput", "predecessor", "selfHashEncoding", "selfHash");
            RequireExactString(descriptor, "schema", prepared.SchemaId);
            RequireExactString(descriptor, "descriptorStatus", DescriptorStatus);
            RequireExactString(descriptor, "effectId", prepared.EffectId);
            RequireExactString(descriptor, "candidateId", "C0");
            if (RequiredLong(descriptor, "candidateRevision", 0, 0) != 0
                || RequiredLong(descriptor, "contractRevision", 1, MaxRevision) != prepared.ContractRevision
                || RequiredLong(descriptor, "evidenceRevision", 1, 1) != 1)
                throw new InvalidDataException("Descriptor revision identity is outside the legacy C0/E1 Phase-A schema.");
            RequireExactString(descriptor, "selfHashEncoding", W24TypedBinaryCanonicalEncoding.EncodingName);
            RequiredHash(descriptor, "selfHash");
            var revisionRoot = prepared.CandidateRoot + "/evidence/E1";
            var rawRoot = "artifacts/vfx-evidence/" + prepared.EffectId + "/C0";

            var predecessor = RequiredObject(descriptor, "predecessor");
            RequireExactly(predecessor, "kind"); RequireExactString(predecessor, "kind", "NONE");

            var candidate = RequiredObject(descriptor, "candidate");
            RequireExactly(candidate, "receiptPath", "receiptFileHash", "receiptVersion", "contractPath", "contractFileHash", "contractHash", "pendingTracePath", "pendingTraceFileHash", "bootstrapManifestSnapshotPath", "bootstrapManifestSnapshotFileHash", "buildHash", "captureProfileHash", "runtimeEntryPath", "runtimeEntryGuid", "previewScenePath", "previewSceneFileHash");
            RequireExactRepositoryPath(candidate, "receiptPath", prepared.CandidateRoot + "/candidate-receipt.json"); RequiredHash(candidate, "receiptFileHash");
            RequireExactString(candidate, "receiptVersion", LegacyCandidateVersion);
            RequireExactRepositoryPath(candidate, "contractPath", prepared.CandidateRoot + "/design-contract.json"); RequiredHash(candidate, "contractFileHash"); RequiredHash(candidate, "contractHash");
            RequireExactRepositoryPath(candidate, "pendingTracePath", prepared.CandidateRoot + "/implementation-trace.json"); RequiredHash(candidate, "pendingTraceFileHash");
            RequireExactRepositoryPath(candidate, "bootstrapManifestSnapshotPath", prepared.CandidateRoot + "/bootstrap-manifest.json"); RequiredHash(candidate, "bootstrapManifestSnapshotFileHash");
            RequiredHash(candidate, "buildHash"); RequiredHash(candidate, "captureProfileHash");
            RequiredAssetPath(candidate, "runtimeEntryPath"); RequiredGuid(candidate, "runtimeEntryGuid"); RequiredAssetPath(candidate, "previewScenePath"); RequiredHash(candidate, "previewSceneFileHash");

            var raw = RequiredObject(descriptor, "rawCapture");
            RequireExactly(raw, "layout", "root", "captureMetadataPath", "captureMetadataFileHash", "evidenceSealPath", "evidenceSealFileHash", "evidenceSealHash", "evidenceLockPath", "evidenceLockFileHash", "diagnosticPassManifestPath", "diagnosticPassManifestFileHash", "artifactCount", "totalBytes", "fileSetTypedHash");
            RequireExactString(raw, "layout", LegacyRawLayout); RequireExactRepositoryPath(raw, "root", rawRoot);
            RequireExactRepositoryPath(raw, "captureMetadataPath", rawRoot + "/capture-metadata.json"); RequiredHash(raw, "captureMetadataFileHash");
            RequireExactRepositoryPath(raw, "evidenceSealPath", rawRoot + "/evidence-seal.json"); RequiredHash(raw, "evidenceSealFileHash"); RequiredHash(raw, "evidenceSealHash");
            RequireExactRepositoryPath(raw, "evidenceLockPath", rawRoot + "/evidence-lock.json"); RequiredHash(raw, "evidenceLockFileHash");
            RequireExactRepositoryPath(raw, "diagnosticPassManifestPath", rawRoot + "/diagnostic-pass-manifest.json"); RequiredHash(raw, "diagnosticPassManifestFileHash");
            RequiredLong(raw, "artifactCount", 4, MaxRawFiles); RequiredLong(raw, "totalBytes", 1, MaxRawBytes); RequiredHash(raw, "fileSetTypedHash");

            var writer = RequiredObject(descriptor, "writer");
            RequireExactly(writer, "writerId", "writerVersion", "bundleSnapshotPath", "bundleSnapshotFileHash", "bundleTypedHash", "sourceSnapshots", "sourceSetTypedHash", "descriptorSchemaSnapshotPath", "descriptorSchemaSnapshotFileHash");
            RequiredBoundedToken(writer, "writerId"); RequiredVersion(writer, "writerVersion");
            RequireExactRepositoryPath(writer, "bundleSnapshotPath", revisionRoot + "/snapshots/writer/writer.bundle.json"); RequiredHash(writer, "bundleSnapshotFileHash"); RequiredHash(writer, "bundleTypedHash");
            ValidateDescriptorSourceSnapshots(RequiredArray(writer, "sourceSnapshots", 1, MaxSourceRecords), revisionRoot + "/snapshots/writer/sources");
            RequiredHash(writer, "sourceSetTypedHash");
            var schemaFile = Same(prepared.Route, S0bRoute) ? "w24-s5-evidence-revision-legacy-c0-s0b-v1.schema.json" : "w24-s5-evidence-revision-legacy-c0-s3-v1.schema.json";
            RequireExactRepositoryPath(writer, "descriptorSchemaSnapshotPath", revisionRoot + "/snapshots/schema/" + schemaFile); RequiredHash(writer, "descriptorSchemaSnapshotFileHash");

            var capture = RequiredObject(descriptor, "captureTool");
            RequireExactly(capture, "toolVersion", "bundleSnapshotPath", "bundleSnapshotFileHash", "bundleCanonicalHash", "sourceSnapshots", "sourceSetTypedHash");
            RequiredVersion(capture, "toolVersion"); RequireExactRepositoryPath(capture, "bundleSnapshotPath", revisionRoot + "/snapshots/capture-tool/capture-tool.bundle.json");
            RequiredHash(capture, "bundleSnapshotFileHash"); RequiredHash(capture, "bundleCanonicalHash");
            ValidateDescriptorSourceSnapshots(RequiredArray(capture, "sourceSnapshots", 1, MaxSourceRecords), revisionRoot + "/snapshots/capture-tool/sources"); RequiredHash(capture, "sourceSetTypedHash");

            var evaluation = RequiredObject(descriptor, "evaluationInput");
            if (Same(prepared.Route, S0bRoute))
            {
                RequireExactly(evaluation, "schema", "operatorCommandPath", "operatorCommandFileHash", "semanticTelemetryPath", "semanticTelemetryFileHash", "receiverOffPath", "receiverOffFileHash", "receiverOnPath", "receiverOnFileHash", "receiverSummaryPath", "receiverSummaryFileHash", "replayPolicyVersion");
                RequireExactString(evaluation, "schema", "w24-s5-eval-input-s0b-legacy/1");
                RequireExactRepositoryPath(evaluation, "operatorCommandPath", rawRoot + "/diagnostics/operator-command.json"); RequiredHash(evaluation, "operatorCommandFileHash");
                RequireExactRepositoryPath(evaluation, "semanticTelemetryPath", rawRoot + "/diagnostics/semantic-telemetry.json"); RequiredHash(evaluation, "semanticTelemetryFileHash");
                RequireExactRepositoryPath(evaluation, "receiverOffPath", rawRoot + "/diagnostics/receiver-light-off.png"); RequiredHash(evaluation, "receiverOffFileHash");
                RequireExactRepositoryPath(evaluation, "receiverOnPath", rawRoot + "/diagnostics/receiver-light-on.png"); RequiredHash(evaluation, "receiverOnFileHash");
                RequireExactRepositoryPath(evaluation, "receiverSummaryPath", rawRoot + "/diagnostics/receiver-light-ab.json"); RequiredHash(evaluation, "receiverSummaryFileHash"); RequireExactString(evaluation, "replayPolicyVersion", S0bReplayPolicy);
            }
            else
            {
                RequireExactly(evaluation, "schema", "metricsInputPath", "metricsInputFileHash", "capturedMetricsReportPath", "capturedMetricsReportFileHash", "metricsToolSnapshotPath", "metricsToolSnapshotFileHash", "metricsEnvironmentPath", "metricsEnvironmentFileHash", "requiredEvidenceMatrixHash", "typedRawSetHash");
                RequireExactString(evaluation, "schema", "w24-s5-eval-input-s3-render-metrics/1");
                RequireExactRepositoryPath(evaluation, "metricsInputPath", rawRoot + "/diagnostics/metrics-input.json"); RequiredHash(evaluation, "metricsInputFileHash");
                RequireExactRepositoryPath(evaluation, "capturedMetricsReportPath", rawRoot + "/diagnostics/metrics-report.json"); RequiredHash(evaluation, "capturedMetricsReportFileHash");
                RequireExactRepositoryPath(evaluation, "metricsToolSnapshotPath", revisionRoot + "/snapshots/evaluation/render_metrics.py"); RequiredHash(evaluation, "metricsToolSnapshotFileHash");
                RequireExactRepositoryPath(evaluation, "metricsEnvironmentPath", revisionRoot + "/snapshots/evaluation/metrics-environment.json"); RequiredHash(evaluation, "metricsEnvironmentFileHash");
                RequiredHash(evaluation, "requiredEvidenceMatrixHash"); RequiredHash(evaluation, "typedRawSetHash");
            }
        }

        private static void ValidateDescriptorSourceSnapshots(JArray sources, string expectedRoot)
        {
            for (var index = 0; index < sources.Count; index++)
            {
                var item = RequiredArrayObject(sources[index], "descriptor source snapshot");
                RequireExactly(item, "ordinal", "sourcePath", "sourceSha256", "snapshotPath", "snapshotFileHash");
                if (RequiredLong(item, "ordinal", 0, MaxSourceRecords - 1) != index) throw new InvalidDataException("Descriptor source snapshot ordinals are noncanonical.");
                RequiredRepositoryPath(item, "sourcePath"); RequiredHash(item, "sourceSha256");
                RequireExactRepositoryPath(item, "snapshotPath", expectedRoot + "/" + index.ToString("D4", CultureInfo.InvariantCulture) + ".source"); RequiredHash(item, "snapshotFileHash");
            }
        }

        private static void VerifySchemaTrustRoot(byte[] bytes, string expectedId)
        {
            var schema = Parse(bytes, "compiled descriptor schema");
            if (!Same((string)schema["$schema"], "https://json-schema.org/draft/2020-12/schema") || !Same((string)schema["$id"], expectedId)
                || schema["type"] == null || !Same((string)schema["type"], "object") || (bool?)schema["additionalProperties"] != false)
                throw new InvalidDataException("Descriptor schema snapshot identity/root semantics differ from the compiled trust root.");
            var properties = schema["properties"] as JObject;
            if (properties == null || !Same((string)properties.SelectToken("candidateId.const"), "C0")
                || (long?)properties.SelectToken("candidateRevision.const") != 0
                || (long?)properties.SelectToken("evidenceRevision.const") != 1
                || !Same((string)properties.SelectToken("rawCapture.$ref"), "#/$defs/legacyE1RawCapture")
                || !Same((string)properties.SelectToken("predecessor.$ref"), "#/$defs/e1Predecessor"))
                throw new InvalidDataException("Descriptor schema snapshot is not the compiled legacy C0/E1-only schema.");
        }

        private static FileStream AcquireLock(string absolute, W24S5EvidenceRevisionWriteRequest request)
        {
            EnsureNoReparseAtOrAbove(Path.GetDirectoryName(absolute), RepositoryRoot());
            var stream = new FileStream(absolute, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.WriteThrough | FileOptions.DeleteOnClose);
            try
            {
#if UNITY_INCLUDE_TESTS
                var hook = AfterLockCreateForTests;
                if (hook != null) hook(absolute);
#endif
                var token = Guid.NewGuid().ToString("N");
                var bytes = StrictUtf8.GetBytes("ownerToken=" + token + "\n" + request.CandidateReceiptPath + "\n" + request.CandidateReceiptFileHash + "\nE" + request.EvidenceRevision.ToString(CultureInfo.InvariantCulture) + "\n");
                stream.Write(bytes, 0, bytes.Length); stream.Flush(true); return stream;
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        private static void WriteNew(string absolute, byte[] bytes)
        {
            var parent = Path.GetDirectoryName(absolute); EnsureNoReparseAtOrAbove(parent, RepositoryRoot()); Directory.CreateDirectory(parent); EnsureDirectory(parent, "snapshot parent", RepositoryRoot());
            using (var stream = new FileStream(absolute, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
            }
            RejectReparse(absolute);
        }

        private static void TryDeleteOwnedPending(string pending, string parent)
        {
            try
            {
                if (string.IsNullOrEmpty(pending) || string.IsNullOrEmpty(parent) || !Directory.Exists(pending) || !IsDirectPendingChild(pending, parent)) return;
                if (ContainsPhysicalReparse(pending)) return;
                Directory.Delete(pending, true);
            }
            catch { /* fail-closed result already returned; never broaden cleanup */ }
        }

        private static string QuarantineOwnedPublishedTarget(string target, string parent)
        {
            if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(parent)
                || !SamePath(Path.GetDirectoryName(Path.GetFullPath(target)), Path.GetFullPath(parent))
                || !Same(Path.GetFileName(target), "E1"))
                throw new InvalidOperationException("Invocation-owned publication target is not the exact formal E1 child.");
            if (!Directory.Exists(target))
            {
                if (File.Exists(target)) throw new InvalidOperationException("Formal E1 was replaced by a non-directory before rollback.");
                return null; // another actor already removed the formal namespace
            }
            if (IsPhysicalReparse(target)) throw new InvalidOperationException("Formal E1 became reparse-backed before rollback.");
            var quarantine = Path.Combine(parent, ".E1.rollback-" + Guid.NewGuid().ToString("N"));
            if (!IsDirectOwnedWorkingChild(quarantine, parent) || Directory.Exists(quarantine) || File.Exists(quarantine))
                throw new InvalidOperationException("Invocation-owned rollback quarantine path is invalid or already exists.");
#if UNITY_INCLUDE_TESTS
            var hook = BeforeQuarantineMoveForTests;
            if (hook != null) hook(target);
#endif
            Directory.Move(target, quarantine);
            if (Directory.Exists(target) || !Directory.Exists(quarantine))
                throw new IOException("Formal E1 rollback move did not establish the exact quarantine namespace.");
            return quarantine;
        }

        private static bool ContainsPhysicalReparse(string root)
        {
            if (IsPhysicalReparse(root)) return true;
            var pending = new Stack<Tuple<string, int>>(); pending.Push(Tuple.Create(Path.GetFullPath(root), 0));
            var directories = 1; var files = 0;
            while (pending.Count != 0)
            {
                var current = pending.Pop();
                foreach (var entry in Directory.EnumerateFileSystemEntries(current.Item1))
                {
                    if (IsPhysicalReparse(entry)) return true;
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        if (current.Item2 + 1 > 8 || ++directories > 64)
                            throw new InvalidDataException("Owned pending cleanup tree exceeds its bounded directory/depth policy.");
                        pending.Push(Tuple.Create(entry, current.Item2 + 1));
                    }
                    else if (++files > 512)
                    {
                        throw new InvalidDataException("Owned pending cleanup tree exceeds its bounded file policy.");
                    }
                }
            }
            return false;
        }

        private static bool IsDirectPendingChild(string pending, string parent)
        {
            return IsDirectOwnedWorkingChild(pending, parent);
        }

        private static bool IsDirectOwnedWorkingChild(string path, string parent)
        {
            if (!SamePath(Path.GetDirectoryName(Path.GetFullPath(path)), Path.GetFullPath(parent))) return false;
            var name = Path.GetFileName(path);
            return name.StartsWith(".E1.pending-", StringComparison.Ordinal) && name.Length == ".E1.pending-".Length + 32
                || name.StartsWith(".E1.rollback-", StringComparison.Ordinal) && name.Length == ".E1.rollback-".Length + 32;
        }

        private static string PreparedFingerprint(Prepared prepared)
        {
            var value = new JObject
            {
                ["schema"] = "w24-s5-evidence-revision-prepared-tree/1",
                ["files"] = new JArray(prepared.ExpectedFiles.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => new JObject
                {
                    ["path"] = item.Key, ["sha256"] = item.Value.Hash, ["byteLength"] = item.Value.Length
                }))
            };
            return W24TypedBinaryCanonicalEncoding.Hash(value);
        }

        private static void AddPreparedFile(Prepared prepared, PrepareBudget budget, string localPath, byte[] bytes)
        {
            if (prepared == null || budget == null || bytes == null || !SafeLocalPath(localPath) || localPath.StartsWith("bound/", StringComparison.Ordinal))
                throw new InvalidDataException("Prepared descriptor file has an unsafe path or missing bytes.");
            if (prepared.Files.Count >= 512 || prepared.Files.ContainsKey(localPath))
                throw new InvalidDataException("Prepared descriptor tree exceeds its file-count bound or repeats a path.");
            budget.AddPrepared(bytes.LongLength);
            prepared.Files.Add(localPath, bytes);
            prepared.ExpectedFiles.Add(localPath, new FileIdentity { LocalPath = localPath, Hash = Hash(bytes), Length = bytes.LongLength });
        }

        private static string LocalSnapshotPath(string repositoryPath, string revisionRoot)
        {
            var prefix = revisionRoot.TrimEnd('/') + "/";
            if (!repositoryPath.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidDataException("Snapshot path escaped the E1 namespace.");
            return repositoryPath.Substring(prefix.Length);
        }

        private static W24S5CandidateEvidenceReadRequest ToReaderRequest(W24S5EvidenceRevisionWriteRequest value)
        {
            return new W24S5CandidateEvidenceReadRequest
            {
                CandidateReceiptPath = value.CandidateReceiptPath,
                CandidateReceiptFileHash = value.CandidateReceiptFileHash,
                EvidenceRevision = value.EvidenceRevision
            };
        }

        private static bool LooksRevisionedCandidatePath(string path)
        {
            return Regex.IsMatch(path ?? string.Empty, "^docs/vfx-candidates/[a-z][a-z0-9_]*?/R[1-9][0-9]*/C[12]/candidate-receipt\\.json$");
        }

        private static string RepositoryLockPath() { return Path.Combine(RepositoryAbsolute("docs/vfx-candidates"), RepositoryLockName); }
        private static string RepositoryRoot() { return Directory.GetParent(ProjectRoot()).FullName; }
        private static string ProjectRoot() { return Directory.GetParent(Application.dataPath).FullName; }
        private static string RepositoryAbsolute(string relative) { return CheckedAbsolute(RepositoryRoot(), relative); }
        private static string ProjectAbsolute(string relative) { return CheckedAbsolute(ProjectRoot(), relative); }
        private static string CheckedAbsolute(string root, string relative)
        {
            if (!SafeRepositoryPath(relative)) throw new InvalidDataException("Repository/project-relative path is unsafe.");
            var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var absolute = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!absolute.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Path escaped its filesystem boundary.");
            return absolute;
        }

        private static PinnedFile ReadRepositoryPinned(string path, string hash, string label, int maximumBytes)
        {
            if (!CanonicalHash(hash)) throw new InvalidDataException(label + " hash is not canonical.");
            var value = ReadRepositoryUnpinned(path, label, maximumBytes);
            if (!Same(value.Hash, hash)) throw new InvalidDataException(label + " bytes differ from their immutable pin.");
            return value;
        }

        private static PinnedFile ReadRepositoryUnpinned(string path, string label, int maximumBytes)
        {
            if (!SafeRepositoryPath(path)) throw new InvalidDataException(label + " path is unsafe.");
            return ReadAbsoluteUnpinned(RepositoryAbsolute(path), label, maximumBytes);
        }

        private static FileIdentity HashAbsolutePinned(string absolute, string hash, string label, int maximumBytes)
        {
            if (!CanonicalHash(hash)) throw new InvalidDataException(label + " hash is not canonical.");
            var first = HashRegularFile(absolute, label, maximumBytes);
            var second = HashRegularFile(absolute, label, maximumBytes);
            if (!Same(first.Hash, second.Hash) || first.Length != second.Length || !Same(second.Hash, hash))
                throw new InvalidDataException(label + " bytes differ from their immutable pin or changed during replay.");
            return second;
        }

        private static PinnedFile ReadAbsoluteUnpinned(string absolute, string label, int maximumBytes)
        {
            var value = HashRegularFile(absolute, label, maximumBytes);
            var bytes = new byte[(int)value.Length];
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
            if (!Same(hash, value.Hash)) throw new InvalidDataException(label + " changed while being read.");
            return new PinnedFile { Bytes = bytes, Hash = hash, Length = bytes.LongLength };
        }

        private static FileIdentity HashRegularFile(string absolute, string label, int maximumBytes)
        {
            if (!File.Exists(absolute)) throw new FileNotFoundException(label + " is missing.", absolute);
            RejectReparse(absolute); EnsureNoReparseAtOrAbove(absolute, Path.IsPathRooted(absolute) ? Path.GetPathRoot(absolute) : RepositoryRoot());
            long length; string hash;
            using (var stream = new FileStream(absolute, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sha = SHA256.Create())
            {
                length = stream.Length;
                if (length < 0 || length > maximumBytes) throw new InvalidDataException(label + " exceeds its byte bound.");
                hash = "sha256:" + string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
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
            if ((attributes & FileAttributes.Directory) == 0 || IsReparse(absolute, attributes)) throw new InvalidDataException(label + " is not a regular non-reparse directory.");
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
            if (IsReparse(path, File.GetAttributes(path))) throw new InvalidDataException("Evidence-revision input/output contains a reparse-backed entry.");
        }

        private static bool IsReparse(string path, FileAttributes attributes)
        {
#if UNITY_INCLUDE_TESTS
            var hook = activeSharedRawReparseHook ?? TreatPathAsReparsePointForTests;
            if (hook != null && hook(Path.GetFullPath(path))) return true;
#endif
            return (attributes & FileAttributes.ReparsePoint) != 0;
        }

        private static bool IsPhysicalReparse(string path)
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }

        private static JObject Parse(byte[] bytes, string label)
        {
            string text;
            try { text = StrictUtf8.GetString(bytes); }
            catch (DecoderFallbackException error) { throw new InvalidDataException(label + " is not strict UTF-8.", error); }
            return W24StrictJsonText.ParseObject(text, "W24 S5 descriptor writer " + label);
        }

        private static byte[] Serialize(JToken value)
        {
            return StrictUtf8.GetBytes(value.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n");
        }

        private static string CanonicalJson(JToken value)
        {
            if (value is JObject obj)
            {
                var sorted = new JObject();
                foreach (var property in obj.Properties().OrderBy(item => item.Name, StringComparer.Ordinal)) sorted.Add(property.Name, JToken.Parse(CanonicalJson(property.Value)));
                return sorted.ToString(Formatting.None);
            }
            if (value is JArray array) return new JArray(array.Select(item => JToken.Parse(CanonicalJson(item)))).ToString(Formatting.None);
            return value.ToString(Formatting.None);
        }

        private static string TypedHash(JObject value)
        {
            var clone = (JObject)value.DeepClone(); clone.Remove("selfHash");
            return W24TypedBinaryCanonicalEncoding.Hash(NormalizeTypedNumbers(clone));
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
            var expected = new HashSet<string>(fields, StringComparer.Ordinal);
            var actual = new HashSet<string>(value.Properties().Select(item => item.Name), StringComparer.Ordinal);
            if (!expected.SetEquals(actual)) throw new InvalidDataException("JSON object field set is not exact; expected " + string.Join(",", fields) + ".");
        }

        private static void RequireExactlyInOrder(JObject value, params string[] fields)
        {
            RequireExactly(value, fields);
            if (!value.Properties().Select(item => item.Name).SequenceEqual(fields, StringComparer.Ordinal))
                throw new InvalidDataException("Recorder JSON field order differs from its frozen byte producer: " + string.Join(",", fields) + ".");
        }

        private static JObject RequiredObject(JObject value, string field) { var token = value[field]; if (token == null || token.Type != JTokenType.Object) throw new InvalidDataException(field + " must be an object."); return (JObject)token; }
        private static JObject RequiredArrayObject(JToken token, string label) { if (token == null || token.Type != JTokenType.Object) throw new InvalidDataException(label + " must be an object."); return (JObject)token; }
        private static JArray RequiredArray(JObject value, string field, int minimum, int maximum) { var token = value[field]; if (token == null || token.Type != JTokenType.Array) throw new InvalidDataException(field + " must be an array."); var array = (JArray)token; if (array.Count < minimum || array.Count > maximum) throw new InvalidDataException(field + " count exceeds its bound."); return array; }
        private static string RequiredString(JObject value, string field, int maximum) { var token = value[field]; if (token == null || token.Type != JTokenType.String) throw new InvalidDataException(field + " must be a string."); var text = (string)token; if (string.IsNullOrEmpty(text) || text.Length > maximum || text.Length > MaxTextCharacters || text.Any(char.IsControl)) throw new InvalidDataException(field + " is outside its text bound."); return text; }
        private static void RequireExactString(JObject value, string field, string expected) { if (!Same(RequiredString(value, field, 16384), expected)) throw new InvalidDataException(field + " has an unsupported value."); }
        private static string RequiredHash(JObject value, string field) { var text = RequiredString(value, field, 71); if (!CanonicalHash(text)) throw new InvalidDataException(field + " is not canonical SHA-256."); return text; }
        private static string RequiredRawHash(JObject value, string field) { var text = RequiredString(value, field, 64); if (text.Length != 64 || text.Any(item => !LowerHex(item))) throw new InvalidDataException(field + " is not raw lowercase SHA-256."); return text; }
        private static string RequiredGuid(JObject value, string field) { var text = RequiredString(value, field, 32); if (text.Length != 32 || text.Any(item => !LowerHex(item))) throw new InvalidDataException(field + " is not a lowercase Unity GUID."); return text; }
        private static string RequiredRepositoryPath(JObject value, string field) { var text = RequiredString(value, field, MaxPathCharacters); if (!SafeRepositoryPath(text)) throw new InvalidDataException(field + " is not a safe repository path."); return text; }
        private static string RequiredLocalPath(JObject value, string field) { var text = RequiredString(value, field, MaxPathCharacters); if (!SafeLocalPath(text) || Same(text, "bound") || text.StartsWith("bound/", StringComparison.Ordinal)) throw new InvalidDataException(field + " is not a safe sealed local path."); return text; }
        private static string RequiredAssetPath(JObject value, string field) { var text = RequiredRepositoryPath(value, field); if (!text.StartsWith("Assets/", StringComparison.Ordinal)) throw new InvalidDataException(field + " is not a canonical Assets path."); return text; }
        private static string RequiredAbsolutePath(JObject value, string field) { var text = RequiredString(value, field, 4096); if (!Path.IsPathRooted(text)) throw new InvalidDataException(field + " must be an absolute recorder provenance path."); return text; }
        private static void RequireExactRepositoryPath(JObject value, string field, string expected) { if (!Same(RequiredRepositoryPath(value, field), expected)) throw new InvalidDataException(field + " differs from its exact Phase-A namespace."); }
        private static string RequiredToken(JObject value, string field, int maximum) { var text = RequiredString(value, field, maximum); if (!ProtocolToken(text)) throw new InvalidDataException(field + " is not a protocol token."); return text; }
        private static string RequiredBoundedToken(JObject value, string field) { var text = RequiredString(value, field, MaxDescriptorTokenCharacters); if (!DescriptorToken(text)) throw new InvalidDataException(field + " is not a bounded ASCII descriptor token."); return text; }
        private static string RequiredProtocolToken(JObject value, string field) { return RequiredToken(value, field, 128); }
        private static string RequiredMetricToken(JObject value, string field) { var text = RequiredString(value, field, 128); if (!MetricToken(text)) throw new InvalidDataException(field + " is not a lowercase metrics token."); return text; }
        private static string RequiredVersion(JObject value, string field) { var text = RequiredString(value, field, MaxDescriptorTokenCharacters); if (!VersionToken(text)) throw new InvalidDataException(field + " is not a version token."); return text; }
        private static bool RequiredBool(JObject value, string field) { var token = value[field] as JValue; if (token == null || token.Type != JTokenType.Boolean || !(token.Value is bool)) throw new InvalidDataException(field + " must be Boolean."); return (bool)token.Value; }
        private static long RequiredLong(JObject value, string field, long minimum, long maximum) { var token = value[field] as JValue; if (token == null || token.Type != JTokenType.Integer) throw new InvalidDataException(field + " must be an integer."); long number; try { number = Convert.ToInt64(token.Value, CultureInfo.InvariantCulture); } catch (Exception error) when (error is InvalidCastException || error is OverflowException || error is FormatException) { throw new InvalidDataException(field + " is not a supported signed 64-bit integer.", error); } if (number < minimum || number > maximum) throw new InvalidDataException(field + " is outside its integer bound."); return number; }
        private static long RequiredArrayLong(JArray value, int index, long minimum, long maximum, string label) { return RequiredArrayNumber(value[index], minimum, maximum, label); }
        private static long RequiredArrayNumber(JToken token, long minimum, long maximum, string label) { if (!(token is JValue value) || value.Type != JTokenType.Integer) throw new InvalidDataException(label + " must be integer."); long number; try { number = Convert.ToInt64(value.Value, CultureInfo.InvariantCulture); } catch (Exception error) when (error is InvalidCastException || error is OverflowException || error is FormatException) { throw new InvalidDataException(label + " is not a supported signed 64-bit integer.", error); } if (number < minimum || number > maximum) throw new InvalidDataException(label + " is outside its bound."); return number; }
        private static double RequiredFiniteNumber(JToken token, string label) { if (!(token is JValue value) || value.Type != JTokenType.Integer && value.Type != JTokenType.Float) throw new InvalidDataException(label + " must be numeric."); var number = Convert.ToDouble(value.Value, CultureInfo.InvariantCulture); if (double.IsNaN(number) || double.IsInfinity(number)) throw new InvalidDataException(label + " must be finite."); return number; }

        private static bool SafeRepositoryPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > MaxPathCharacters || Path.IsPathRooted(value) || value.IndexOf('\\') >= 0 || value.IndexOf(':') >= 0) return false;
            var parts = value.Split('/'); return parts.Length > 0 && parts.All(part => part.Length > 0 && part.Length <= MaxPathSegmentCharacters && part != "." && part != ".." && part.All(AsciiPathCharacter));
        }
        private static bool SafeLocalPath(string value) { return SafeRepositoryPath(value) && !value.StartsWith("/", StringComparison.Ordinal); }
        private static bool ProtocolToken(string value) { return !string.IsNullOrEmpty(value) && value.Length <= 128 && value.All(item => char.IsLetterOrDigit(item) || item == '-' || item == '_' || item == '.'); }
        private static bool MetricToken(string value) { return !string.IsNullOrEmpty(value) && value.Length <= 128 && value[0] >= 'a' && value[0] <= 'z' && value.All(item => item >= 'a' && item <= 'z' || item >= '0' && item <= '9' || item == '-' || item == '_' || item == '.'); }
        private static bool DescriptorToken(string value) { return value != null && value.Length <= MaxDescriptorTokenCharacters && Regex.IsMatch(value, "^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant); }
        private static bool AsciiPathCharacter(char value) { return value >= 'A' && value <= 'Z' || value >= 'a' && value <= 'z' || value >= '0' && value <= '9' || value == '_' || value == '.' || value == '-'; }
        private static bool VersionToken(string value) { return value != null && value.Length <= MaxDescriptorTokenCharacters && Regex.IsMatch(value, "^[a-z0-9][a-z0-9._-]*/[0-9]+(?:\\.[0-9]+){0,3}$"); }
        private static bool CanonicalHash(string value) { return value != null && value.Length == 71 && value.StartsWith("sha256:", StringComparison.Ordinal) && value.Substring(7).All(LowerHex); }
        private static bool LowerHex(char value) { return value >= '0' && value <= '9' || value >= 'a' && value <= 'f'; }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return "sha256:" + string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2", CultureInfo.InvariantCulture))); }
        private static long EffectiveBudget(long production)
        {
#if UNITY_INCLUDE_TESTS
            var configured = AggregateBudgetLimitForTests;
            if (configured.HasValue && configured.Value > 0) return Math.Min(production, configured.Value);
#endif
            return production;
        }
        private static bool Same(string left, string right) { return string.Equals(left, right, StringComparison.Ordinal); }
        private static bool SamePath(string left, string right) { try { return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase); } catch { return false; } }
        private static bool SameExactAbsolutePath(string left, string right) { try { return string.Equals(Path.GetFullPath(left).Replace('\\', '/').TrimEnd('/'), Path.GetFullPath(right).Replace('\\', '/').TrimEnd('/'), StringComparison.Ordinal); } catch { return false; } }
        private static bool ExpectedInputFailure(Exception error) { return error is InvalidDataException || error is IOException || error is UnauthorizedAccessException || error is SecurityException || error is JsonException || error is FormatException || error is OverflowException || error is InvalidCastException || error is NotSupportedException || error is ArgumentException || error is CryptographicException || error is DecoderFallbackException; }
    }
}
