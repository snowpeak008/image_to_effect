using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public sealed class W24S5CandidateRevisionTransactionTests
    {
        private const string EffectId = "w24_candidate_revision_probe";
        private const int ContractRevision = 73191;
        private const string C0Root = "Assets/VFX/Generated/w24_candidate_revision_probe";
        private const string C0Runtime = C0Root + "/VFX_w24_candidate_revision_probe.prefab";
        private const string C0Preview = "Assets/VFX/Preview/W24_CandidateRevisionProbe_C0.unity";
        private const string ManifestPath = "ProjectSettings/VFXComposer/BuildManifests/w24_candidate_revision_probe.manifest.json";
        private const string C0CandidateRoot = "docs/vfx-candidates/w24_candidate_revision_probe/C0";
        private const string C0ReceiptPath = C0CandidateRoot + "/candidate-receipt.json";
        private static readonly string RevisionNamespace = "R" + ContractRevision;
        private static bool testPathsOwnedByFixture;
        private bool candidatesAssetRootPreexisting;
        private bool candidatesAssetRootMetaPreexisting;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            ValidateRevisionCleanupBoundary();
            var conflicts = TestOwnedAbsolutePaths().Where(path => File.Exists(path) || Directory.Exists(path)).ToArray();
            if (conflicts.Length != 0) Assert.Fail("Refusing to overwrite or clean pre-existing candidate-transaction probe paths: " + string.Join(" | ", conflicts));
            candidatesAssetRootPreexisting = Directory.Exists(ProjectAbsolute("Assets/VFX/Candidates"));
            candidatesAssetRootMetaPreexisting = File.Exists(ProjectAbsolute("Assets/VFX/Candidates.meta"));
            testPathsOwnedByFixture = true;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            W24S5CandidateRevisionTransaction.ResetCommitHooksForTests();
            if (!testPathsOwnedByFixture) return;
            Cleanup();
            var candidatesRoot = ProjectAbsolute("Assets/VFX/Candidates");
            if (!candidatesAssetRootPreexisting && Directory.Exists(candidatesRoot) && !Directory.EnumerateFileSystemEntries(candidatesRoot).Any())
            {
                Directory.Delete(candidatesRoot);
                if (!candidatesAssetRootMetaPreexisting) DeleteFile(ProjectAbsolute("Assets/VFX/Candidates.meta"));
            }
            testPathsOwnedByFixture = false;
        }

        [SetUp]
        public void SetUp()
        {
            W24S5CandidateRevisionTransaction.ResetCommitHooksForTests();
            Cleanup();
            CreateC0();
        }

        [TearDown]
        public void TearDown()
        {
            W24S5CandidateRevisionTransaction.ResetCommitHooksForTests();
            Cleanup();
        }

        [Test]
        public void OrdinaryRequestCannotSelfIssueAuthority_ButTerminalSubtreeDoesNotBreakAuthorizedReplay()
        {
            var request = StageCandidate(1);
            WriteRepository(C0CandidateRoot + "/terminal/machine-gate-report.json", "{\"failed\":true}\n");
            WriteRepository(C0CandidateRoot + "/terminal/machine-fail-receipt.json", "{\"route\":\"MACHINE_FAIL\"}\n");

            var blocked = W24S5CandidateRevisionTransaction.Evaluate(request);
            Assert.That(blocked.Succeeded, Is.False);
            Assert.That(blocked.Errors.Any(value => value.Contains("gate-issued MACHINE_FAIL authority")), Is.True, string.Join(" | ", blocked.Errors));

            var testOnlyAuthorized = W24S5CandidateRevisionTransaction.Evaluate(request, TestAuthority(C0ReceiptPath, ContractRevision, 0));
            Assert.That(testOnlyAuthorized.Succeeded, Is.True, string.Join(" | ", testOnlyAuthorized.Errors));
            Assert.That(testOnlyAuthorized.CandidateRoot, Is.EqualTo(CandidateRoot(1)), "The exact terminal subtree must be excluded only from the candidate static file-set replay.");
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot(1))), Is.False);
        }

        [Test]
        public void TestOnlyGateAuthority_CommitsC1ThenC2_WriteOnceAndExhaustsWithoutC3()
        {
            var c0Hash = TreeHash(RepositoryAbsolute(C0CandidateRoot));
            var c1Request = StageCandidate(1);
            var c0Authority = TestAuthority(C0ReceiptPath, ContractRevision, 0);
            var c1Approval = W24S5CandidateRevisionTransaction.Evaluate(c1Request, c0Authority);
            Assert.That(c1Approval.Succeeded, Is.True, string.Join(" | ", c1Approval.Errors));
            Assert.That(c1Approval.CandidateRoot, Is.EqualTo(CandidateRoot(1)));
            var c1 = W24S5CandidateRevisionTransaction.Commit(c1Approval.Approval);
            Assert.That(c1.Succeeded, Is.True, string.Join(" | ", c1.Errors));
            Assert.That(TreeHash(RepositoryAbsolute(C0CandidateRoot)), Is.EqualTo(c0Hash), "C0 bytes changed while creating C1.");

            var c1Receipt = ParseRepository(c1.CandidateReceiptPath);
            AssertPendingReceipt(c1Receipt, "C1", 1);
            var c1Hash = TreeHash(RepositoryAbsolute(CandidateRoot(1)));
            var replay = W24S5CandidateRevisionTransaction.Evaluate(c1Request, c0Authority);
            Assert.That(replay.Succeeded, Is.False, "C1 path is write-once and cannot be replayed.");

            var c2Request = StageCandidate(2);
            var c1Authority = TestAuthority(c1.CandidateReceiptPath, ContractRevision, 1);
            var c2Approval = W24S5CandidateRevisionTransaction.Evaluate(c2Request, c1Authority);
            Assert.That(c2Approval.Succeeded, Is.True, string.Join(" | ", c2Approval.Errors));
            var c2 = W24S5CandidateRevisionTransaction.Commit(c2Approval.Approval);
            Assert.That(c2.Succeeded, Is.True, string.Join(" | ", c2.Errors));
            Assert.That(TreeHash(RepositoryAbsolute(C0CandidateRoot)), Is.EqualTo(c0Hash));
            Assert.That(TreeHash(RepositoryAbsolute(CandidateRoot(1))), Is.EqualTo(c1Hash), "C1 bytes changed while creating C2.");
            AssertPendingReceipt(ParseRepository(c2.CandidateReceiptPath), "C2", 2);

            var approvalReplay = W24S5CandidateRevisionTransaction.Commit(c2Approval.Approval);
            Assert.That(approvalReplay.Succeeded, Is.False);
            Assert.That(approvalReplay.Errors.Any(value => value.Contains("already consumed")), Is.True, string.Join(" | ", approvalReplay.Errors));

            var exhaustedRequest = Copy(c2Request);
            exhaustedRequest.PreviousCandidateReceiptPath = c2.CandidateReceiptPath;
            exhaustedRequest.PreviousCandidateReceiptFileHash = c2.CandidateReceiptFileHash;
            var c2Authority = TestAuthority(c2.CandidateReceiptPath, ContractRevision, 2);
            var exhausted = W24S5CandidateRevisionTransaction.Evaluate(exhaustedRequest, c2Authority);
            Assert.That(exhausted.Succeeded, Is.False);
            Assert.That(exhausted.Errors.Any(value => value.Contains("C2 is exhausted")), Is.True, string.Join(" | ", exhausted.Errors));
            Assert.That(Directory.Exists(RepositoryAbsolute("docs/vfx-candidates/" + EffectId + "/" + RevisionNamespace + "/C3")), Is.False);
        }

        [Test]
        public void Approval_ReplaysReceiptAndOwnedOutputs_AndLeavesNoCandidateOnDrift()
        {
            var c0Hash = TreeHash(RepositoryAbsolute(C0CandidateRoot));
            var request = StageCandidate(1);
            var authority = TestAuthority(C0ReceiptPath, ContractRevision, 0);
            var evaluated = W24S5CandidateRevisionTransaction.Evaluate(request, authority);
            Assert.That(evaluated.Succeeded, Is.True, string.Join(" | ", evaluated.Errors));

            File.AppendAllText(ProjectAbsolute(Runtime(1)), "drift", new UTF8Encoding(false));
            var blocked = W24S5CandidateRevisionTransaction.Commit(evaluated.Approval);

            Assert.That(blocked.Succeeded, Is.False);
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot(1))), Is.False, "Atomic candidate directory must not appear after replay failure.");
            Assert.That(TreeHash(RepositoryAbsolute(C0CandidateRoot)), Is.EqualTo(c0Hash));
        }

        [Test]
        public void Approval_ReplaysPreviousReceipt_AndRejectsReceiptDrift()
        {
            var request = StageCandidate(1);
            var evaluated = W24S5CandidateRevisionTransaction.Evaluate(request, TestAuthority(C0ReceiptPath, ContractRevision, 0));
            Assert.That(evaluated.Succeeded, Is.True, string.Join(" | ", evaluated.Errors));
            File.AppendAllText(RepositoryAbsolute(C0ReceiptPath), " ", new UTF8Encoding(false));

            var blocked = W24S5CandidateRevisionTransaction.Commit(evaluated.Approval);

            Assert.That(blocked.Succeeded, Is.False);
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot(1))), Is.False);
        }

        [Test]
        public void Commit_RepositoryScopedCreateNewLockContention_FailsClosedBeforeFinalReplay()
        {
            var request = StageCandidate(1);
            var evaluated = W24S5CandidateRevisionTransaction.Evaluate(request, TestAuthority(C0ReceiptPath, ContractRevision, 0));
            Assert.That(evaluated.Succeeded, Is.True, string.Join(" | ", evaluated.Errors));
            var lockPath = W24S5CandidateRevisionTransaction.RepositoryCommitLockPathForTests;

            W24S5CandidateRevisionResult blocked;
            using (W24S5CandidateRevisionTransaction.AcquireRepositoryCommitLockForTests())
            {
                Assert.That(File.Exists(lockPath), Is.True, "The competing repository-scoped lock must be materialized with CreateNew.");
                blocked = W24S5CandidateRevisionTransaction.Commit(evaluated.Approval);
            }

            Assert.That(blocked.Succeeded, Is.False);
            Assert.That(blocked.Errors.Any(value => value.Contains("repository commit lock")), Is.True, string.Join(" | ", blocked.Errors));
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot(1))), Is.False);
            Assert.That(File.Exists(lockPath), Is.False, "The owning competing handle must remove its lock on release.");
        }

        [Test]
        public void Commit_FinalWriteHoldsRepositoryLockThroughPublish_ThenReleasesIt()
        {
            var request = StageCandidate(1);
            var evaluated = W24S5CandidateRevisionTransaction.Evaluate(request, TestAuthority(C0ReceiptPath, ContractRevision, 0));
            Assert.That(evaluated.Succeeded, Is.True, string.Join(" | ", evaluated.Errors));
            var lockPath = W24S5CandidateRevisionTransaction.RepositoryCommitLockPathForTests;
            var sawFinalWrite = false;
            W24S5CandidateRevisionTransaction.BeforeCandidatePublishForTests = (pending, target, parent) =>
            {
                sawFinalWrite = true;
                Assert.That(target, Is.EqualTo(RepositoryAbsolute(CandidateRoot(1))));
                Assert.That(Path.GetDirectoryName(target), Is.EqualTo(parent));
                Assert.That(Directory.Exists(target), Is.False, "The immutable target must not be visible before the atomic Directory.Move.");
                Assert.That(Directory.Exists(pending), Is.True);
                Assert.That(File.Exists(Path.Combine(pending, W24S5CandidateRevisionTransaction.CandidateReceiptName)), Is.True);
                Assert.That(File.Exists(lockPath), Is.True, "The repository lock must remain present until the final publish.");
                Assert.Throws<IOException>(() =>
                {
                    using (new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                }, "The repository lock handle must still be exclusive immediately before Directory.Move.");
            };

            W24S5CandidateRevisionResult committed;
            try { committed = W24S5CandidateRevisionTransaction.Commit(evaluated.Approval); }
            finally { W24S5CandidateRevisionTransaction.ResetCommitHooksForTests(); }

            Assert.That(committed.Succeeded, Is.True, string.Join(" | ", committed.Errors));
            Assert.That(sawFinalWrite, Is.True);
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot(1))), Is.True);
            Assert.That(File.Exists(lockPath), Is.False, "The repository lock must be deleted from the Commit finally block.");
        }

        [Test]
        public void Commit_FinalParentReparseRevalidationRejectsBeforeDirectoryMove()
        {
            var request = StageCandidate(1);
            var evaluated = W24S5CandidateRevisionTransaction.Evaluate(request, TestAuthority(C0ReceiptPath, ContractRevision, 0));
            Assert.That(evaluated.Succeeded, Is.True, string.Join(" | ", evaluated.Errors));
            string parentMarkedAsReparse = null;
            W24S5CandidateRevisionTransaction.BeforeCandidatePublishForTests = (pending, target, parent) => parentMarkedAsReparse = Path.GetFullPath(parent);
            W24S5CandidateRevisionTransaction.TreatPathAsReparsePointForTests = path => parentMarkedAsReparse != null && string.Equals(Path.GetFullPath(path), parentMarkedAsReparse, StringComparison.OrdinalIgnoreCase);

            W24S5CandidateRevisionResult blocked;
            try { blocked = W24S5CandidateRevisionTransaction.Commit(evaluated.Approval); }
            finally { W24S5CandidateRevisionTransaction.ResetCommitHooksForTests(); }

            Assert.That(blocked.Succeeded, Is.False);
            Assert.That(blocked.Errors.Any(value => value.Contains("reparse point")), Is.True, string.Join(" | ", blocked.Errors));
            Assert.That(parentMarkedAsReparse, Is.Not.Null, "The simulated parent replacement must occur only at the final pre-publish seam.");
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot(1))), Is.False, "The target must remain absent when final parent-chain validation fails.");
            Assert.That(Directory.GetDirectories(parentMarkedAsReparse, ".C1.pending-*", SearchOption.TopDirectoryOnly), Is.Empty, "The rejected pending tree must be removed.");
            Assert.That(File.Exists(W24S5CandidateRevisionTransaction.RepositoryCommitLockPathForTests), Is.False, "The transaction lock must still release on a final validation failure.");
        }

        [TestCase("capture-tool.bundle.json")]
        [TestCase("capture-tool-sources/0000.source")]
        public void C1BundleOrSourceSnapshotDrift_IsRejectedBeforeC2(string candidateLocalPath)
        {
            var c1Request = StageCandidate(1);
            var c1Approval = W24S5CandidateRevisionTransaction.Evaluate(c1Request, TestAuthority(C0ReceiptPath, ContractRevision, 0));
            Assert.That(c1Approval.Succeeded, Is.True, string.Join(" | ", c1Approval.Errors));
            var c1 = W24S5CandidateRevisionTransaction.Commit(c1Approval.Approval);
            Assert.That(c1.Succeeded, Is.True, string.Join(" | ", c1.Errors));

            var c2Request = StageCandidate(2);
            File.AppendAllText(RepositoryAbsolute(CandidateRoot(1) + "/" + candidateLocalPath), "drift", new UTF8Encoding(false));
            var rejected = W24S5CandidateRevisionTransaction.Evaluate(c2Request, TestAuthority(c1.CandidateReceiptPath, ContractRevision, 1));

            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Errors.Any(value => value.Contains("capture-tool")), Is.True, string.Join(" | ", rejected.Errors));
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot(2))), Is.False);
        }

        [Test]
        public void PathsHashesAndUnownedTraceMappings_AreRejectedBeforeWrite()
        {
            var authority = TestAuthority(C0ReceiptPath, ContractRevision, 0);
            var wrongRoot = StageCandidate(1);
            wrongRoot.OwnedOutputRoot = "Assets/VFX/Generated/" + EffectId + "/C1";
            Assert.That(W24S5CandidateRevisionTransaction.Evaluate(wrongRoot, authority).Succeeded, Is.False);

            var badHash = StageCandidate(1);
            badHash.ProductionManifestFileHash = Hash("not the manifest");
            Assert.That(W24S5CandidateRevisionTransaction.Evaluate(badHash, authority).Succeeded, Is.False);

            var wrongBundle = StageCandidate(1);
            wrongBundle.CaptureToolBundlePath = "docs/vfx-contracts/capture-tools/alternate.bundle.json";
            Assert.That(W24S5CandidateRevisionTransaction.Evaluate(wrongBundle, authority).Succeeded, Is.False);

            AddUnownedC0TraceMapping();
            authority = TestAuthority(C0ReceiptPath, ContractRevision, 0);
            var unowned = StageCandidate(1);
            var rejected = W24S5CandidateRevisionTransaction.Evaluate(unowned, authority);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Errors.Any(value => value.Contains("owned-output plan")), Is.True, string.Join(" | ", rejected.Errors));
            Assert.That(Directory.Exists(RepositoryAbsolute(CandidateRoot(1))), Is.False);
        }

        private static void AssertPendingReceipt(JObject receipt, string candidateId, int revision)
        {
            Assert.That((string)receipt["candidateId"], Is.EqualTo(candidateId));
            Assert.That((int)receipt["candidateRevision"], Is.EqualTo(revision));
            Assert.That((string)receipt["contractRevisionNamespace"], Is.EqualTo(RevisionNamespace));
            Assert.That((string)receipt["candidateStatus"], Is.EqualTo(candidateId + "_CAPTURE_PENDING"));
            Assert.That((string)receipt["infrastructureStatus"], Is.EqualTo("TEST_ONLY_TRANSACTION_INFRASTRUCTURE"));
            Assert.That((string)receipt["visualStatus"], Is.EqualTo("VISUAL_PENDING"));
            Assert.That(receipt["visualQaRecordPath"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That(receipt["visualQaRecordFileHash"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That(receipt["userVerdictRecordPath"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That(receipt["userVerdictRecordFileHash"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That((string)receipt["evidenceRoot"], Is.EqualTo(CandidateRoot(revision) + "/evidence"));
            Assert.That((int)receipt["evidenceRevision"], Is.EqualTo(0));
            Assert.That((string)receipt["maturityLevel"], Is.EqualTo("L2_MAXIMUM_PENDING"));
            var authority = (JObject)receipt["advanceAuthority"];
            Assert.That((string)authority["route"], Is.EqualTo("MACHINE_FAIL"));
            Assert.That((string)authority["issuerVersion"], Is.EqualTo("w24-s5-test-machine-failure/1"));
            Assert.That((string)authority["productionIssuerStatus"], Is.EqualTo("FAILURE_ISSUER_PENDING"));
            Assert.That((bool)authority["testOnly"], Is.True);
            Assert.That(authority["failureReceiptPath"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That(authority["failureReceiptFileHash"].Type, Is.EqualTo(JTokenType.Null));
            var contract = ParseRepository((string)receipt["contractPath"]);
            Assert.That((string)contract.SelectToken("extensions.captureBindingStatus"), Is.EqualTo("FROZEN_PRE_" + candidateId));
            Assert.That((string)contract.SelectToken("extensions.visualStatus"), Is.EqualTo("VISUAL_PENDING"));
            Assert.That((string)contract.SelectToken("captureProfile.cameraSerializedReference"), Is.EqualTo((string)receipt["previewScenePath"] + "#MainCamera"));
            Assert.That((string)contract.SelectToken("extensions.typedDiagnostics.frozenView.sceneSerializedReference"), Is.EqualTo((string)receipt["previewScenePath"]));
            Assert.That((string)contract.SelectToken("extensions.typedDiagnostics.frozenView.cameraSerializedReference"), Is.EqualTo((string)receipt["previewScenePath"] + "#MainCamera"));
            Assert.That((string)contract.SelectToken("captureProfile.prefabManifestSerializedReference"), Is.EqualTo((string)receipt["productionManifestSnapshotPath"] + "#buildHash"));
            Assert.That((string)contract.SelectToken("captureProfile.prefabManifestHash"), Is.EqualTo((string)receipt["buildHash"]));
            var bundle = ParseRepository((string)receipt["captureToolBundleSnapshotPath"]);
            Assert.That((string)contract.SelectToken("captureProfile.captureToolVersion"), Is.EqualTo((string)bundle["toolVersion"]));
        }

        private static W24S5CandidateFailureAuthority TestAuthority(string receiptPath, int contractRevision, int candidateRevision)
        {
            return W24S5CandidateFailureAuthority.IssueMachineFailureForTests(EffectId, receiptPath, FileHashRepository(receiptPath), contractRevision, candidateRevision);
        }

        private static W24S5CandidateRevisionRequest StageCandidate(int revision)
        {
            var root = AssetRoot(revision);
            var runtime = Runtime(revision);
            var preview = Preview(revision);
            var runtimeGuid = GuidFor("runtime-" + revision);
            var previewGuid = GuidFor("preview-" + revision);
            WriteAsset(runtime, "prefab-" + revision + "\n", runtimeGuid);
            WriteAsset(preview, "scene-" + revision + "\n", previewGuid);
            var manifest = Manifest(runtime, runtimeGuid, new[]
            {
                Owned(runtime, runtimeGuid, "GameObject"),
                Owned(preview, previewGuid, "SceneAsset")
            }, RawHash("candidate-build-" + revision));
            WriteProject(ManifestPath, Serialize(manifest));

            var sourcePath = SourcePath(revision);
            WriteRepository(sourcePath, "// capture source " + revision + "\n");
            var bundle = new JObject
            {
                ["bundleVersion"] = "w24-capture-tool-bundle/1",
                ["toolVersion"] = "w24-candidate-probe/" + revision,
                ["sources"] = new JArray(new JObject { ["path"] = sourcePath, ["sha256"] = FileHashRepository(sourcePath) })
            };
            var bundlePath = BundlePath(revision);
            WriteRepository(bundlePath, Serialize(bundle));
            return new W24S5CandidateRevisionRequest
            {
                EffectId = EffectId,
                PreviousCandidateReceiptPath = revision == 1 ? C0ReceiptPath : CandidateRoot(revision - 1) + "/candidate-receipt.json",
                PreviousCandidateReceiptFileHash = revision == 1 ? FileHashRepository(C0ReceiptPath) : FileHashRepository(CandidateRoot(revision - 1) + "/candidate-receipt.json"),
                ProductionManifestPath = ManifestPath,
                ProductionManifestFileHash = FileHashProject(ManifestPath),
                OwnedOutputRoot = root,
                RuntimeEntryPath = runtime,
                PreviewScenePath = preview,
                CaptureToolBundlePath = bundlePath,
                CaptureToolBundleFileHash = FileHashRepository(bundlePath)
            };
        }

        private static void CreateC0()
        {
            var runtimeGuid = GuidFor("c0-runtime");
            WriteAsset(C0Runtime, "c0-prefab\n", runtimeGuid);
            WriteAsset(C0Preview, "c0-scene\n", GuidFor("c0-preview"));
            var manifest = Manifest(C0Runtime, runtimeGuid, new[] { Owned(C0Runtime, runtimeGuid, "GameObject") }, RawHash("c0-build"));
            WriteProject(ManifestPath, Serialize(manifest));

            var contract = JObject.Parse(File.ReadAllText(RepositoryAbsolute("docs/vfx-contracts/sustained_flame_3d.contract.json")));
            contract["effectId"] = EffectId;
            contract["contractRevision"] = ContractRevision;
            var capture = (JObject)contract["captureProfile"];
            capture["cameraSerializedReference"] = C0Preview + "#MainCamera";
            capture["sceneSerializedReference"] = C0Preview;
            capture["sceneHash"] = FileHashProject(C0Preview);
            capture["prefabManifestSerializedReference"] = ManifestPath + "#buildHash";
            capture["prefabManifestHash"] = "sha256:" + (string)manifest["buildHash"];
            var extensions = (JObject)contract["extensions"];
            extensions["captureBindingStatus"] = "FROZEN_PRE_C0";
            extensions["visualStatus"] = "VISUAL_PENDING";
            extensions["candidateId"] = "C0";
            extensions["candidateStatus"] = "C0_CAPTURE_PENDING";
            extensions["candidateReceipt"] = C0ReceiptPath;
            extensions["runtimeEntry"] = C0Runtime;
            extensions["previewScene"] = C0Preview;
            extensions["manifest"] = ManifestPath;
            extensions["implementationTrace"] = C0CandidateRoot + "/implementation-trace.json";
            extensions["typedDiagnostics"] = new JObject
            {
                ["frozenView"] = new JObject
                {
                    ["viewId"] = "candidate_revision_probe_main",
                    ["sceneSerializedReference"] = C0Preview,
                    ["cameraSerializedReference"] = C0Preview + "#MainCamera",
                    ["fovDegrees"] = 60
                }
            };
            contract["contractHash"] = VfxDesignContractJson.ComputeContractHash(contract.ToString(Formatting.None));
            var contractText = Serialize(contract);
            VfxDesignContract parsedContract;
            var report = VfxDesignContractJson.ValidateJson(contractText, out parsedContract);
            Assert.That(report.HasErrors, Is.False, string.Join(" | ", report.Issues.Select(value => value.Code + ":" + value.Message)));

            var traceText = File.ReadAllText(RepositoryAbsolute("docs/vfx-traces/sustained_flame_3d.implementation-trace.json"));
            traceText = traceText.Replace("sustained_flame_3d", EffectId).Replace("Assets/VFX/Effects/Aura/" + EffectId + "/VFX_" + EffectId + ".prefab", C0Runtime);
            var trace = JObject.Parse(traceText);
            trace["traceStatus"] = "C0_CAPTURE_PENDING";
            trace["contractRevision"] = ContractRevision;
            trace["contractHash"] = parsedContract.ContractHash;
            trace["buildHash"] = "sha256:" + (string)manifest["buildHash"];
            trace["captureProfileHash"] = "sha256:" + RecipeCanonicalizer.ComputeSha256(capture.ToString(Formatting.None));
            trace["runtimeEntryAssetPath"] = C0Runtime;
            trace["runtimeEntryGuid"] = runtimeGuid;
            trace["candidateRevision"] = 0;
            trace["evidenceRevision"] = 0;
            foreach (var item in ((JArray)trace["requirementTraces"]).OfType<JObject>()) { item.Remove("authorityEvidence"); item.Remove("crossEvidence"); }
            var frozenTrace = Serialize(trace);
            Assert.DoesNotThrow(() => VfxImplementationTraceJson.FromJson(frozenTrace));

            var contractPath = C0CandidateRoot + "/design-contract.json";
            var tracePath = C0CandidateRoot + "/implementation-trace.json";
            var snapshotPath = C0CandidateRoot + "/bootstrap-manifest.json";
            WriteRepository(contractPath, contractText);
            WriteRepository(tracePath, frozenTrace);
            WriteRepository(snapshotPath, Serialize(manifest));
            var receipt = new JObject
            {
                ["candidateVersion"] = "w24-candidate/1.0",
                ["candidateId"] = "C0",
                ["candidateRevision"] = 0,
                ["candidateStatus"] = "C0_CAPTURE_PENDING",
                ["effectId"] = EffectId,
                ["productionManifestPath"] = ManifestPath,
                ["bootstrapManifestSnapshotPath"] = snapshotPath,
                ["bootstrapManifestSnapshotFileHash"] = FileHashRepository(snapshotPath),
                ["ownedOutputs"] = manifest["ownedOutputs"].DeepClone(),
                ["buildHash"] = "sha256:" + (string)manifest["buildHash"],
                ["runtimeEntryPath"] = C0Runtime,
                ["runtimeEntryGuid"] = runtimeGuid,
                ["previewScenePath"] = C0Preview,
                ["previewSceneHash"] = FileHashProject(C0Preview),
                ["contractPath"] = contractPath,
                ["contractFileHash"] = FileHashRepository(contractPath),
                ["contractHash"] = parsedContract.ContractHash,
                ["tracePath"] = tracePath,
                ["traceFileHash"] = FileHashRepository(tracePath),
                ["captureProfileHash"] = (string)trace["captureProfileHash"],
                ["visualStatus"] = "VISUAL_PENDING"
            };
            WriteRepository(C0ReceiptPath, Serialize(receipt));
        }

        private static void AddUnownedC0TraceMapping()
        {
            var tracePath = C0CandidateRoot + "/implementation-trace.json";
            var trace = ParseRepository(tracePath);
            var first = (JObject)((JArray)trace["requirementTraces"])[0];
            var objects = (JArray)first["objects"];
            var clone = (JObject)objects[0].DeepClone();
            clone["assetPath"] = C0Root + "/not-owned.asset";
            clone["componentInstanceId"] = C0Root + "/not-owned.asset#Probe";
            objects.Add(clone);
            WriteRepository(tracePath, Serialize(trace));
            var receipt = ParseRepository(C0ReceiptPath);
            receipt["traceFileHash"] = FileHashRepository(tracePath);
            WriteRepository(C0ReceiptPath, Serialize(receipt));
        }

        private static JObject Manifest(string runtime, string runtimeGuid, IEnumerable<JObject> owned, string rawBuildHash)
        {
            return new JObject
            {
                ["manifestVersion"] = 1,
                ["effectId"] = EffectId,
                ["buildHash"] = rawBuildHash,
                ["runtimeEntry"] = new JObject { ["kind"] = "prefab", ["path"] = runtime, ["guid"] = runtimeGuid },
                ["ownedOutputs"] = new JArray(owned)
            };
        }

        private static JObject Owned(string path, string guid, string assetType)
        {
            return new JObject { ["path"] = path, ["guid"] = guid, ["assetType"] = assetType, ["sha256"] = RawFileHash(path) };
        }

        private static W24S5CandidateRevisionRequest Copy(W24S5CandidateRevisionRequest value)
        {
            return new W24S5CandidateRevisionRequest
            {
                EffectId = value.EffectId,
                PreviousCandidateReceiptPath = value.PreviousCandidateReceiptPath,
                PreviousCandidateReceiptFileHash = value.PreviousCandidateReceiptFileHash,
                ProductionManifestPath = value.ProductionManifestPath,
                ProductionManifestFileHash = value.ProductionManifestFileHash,
                OwnedOutputRoot = value.OwnedOutputRoot,
                RuntimeEntryPath = value.RuntimeEntryPath,
                PreviewScenePath = value.PreviewScenePath,
                CaptureToolBundlePath = value.CaptureToolBundlePath,
                CaptureToolBundleFileHash = value.CaptureToolBundleFileHash
            };
        }

        private static string CandidateRoot(int revision) { return "docs/vfx-candidates/" + EffectId + "/" + RevisionNamespace + "/C" + revision; }
        private static string AssetRoot(int revision) { return "Assets/VFX/Candidates/" + RevisionNamespace + "/C" + revision + "/" + EffectId; }
        private static string Runtime(int revision) { return AssetRoot(revision) + "/VFX_" + EffectId + ".prefab"; }
        private static string Preview(int revision) { return AssetRoot(revision) + "/Preview.unity"; }
        private static string BundlePath(int revision) { return "docs/vfx-contracts/capture-tools/" + EffectId + "." + RevisionNamespace + ".C" + revision + ".bundle.json"; }
        private static string SourcePath(int revision) { return "tools/vfx/tests/" + EffectId + "." + RevisionNamespace + ".C" + revision + ".source.cs"; }

        private static void Cleanup()
        {
            if (!testPathsOwnedByFixture) throw new InvalidOperationException("Refusing to clean candidate-transaction probe paths before exclusive fixture ownership is established.");
            ValidateRevisionCleanupBoundary();
            DeleteDirectory(RepositoryAbsolute("docs/vfx-candidates/" + EffectId));
            DeleteDirectory(ProjectAbsolute(C0Root));
            DeleteFile(ProjectAbsolute(C0Root + ".meta"));
            DeleteFile(ProjectAbsolute(C0Preview)); DeleteFile(ProjectAbsolute(C0Preview + ".meta"));
            var revisionRoot = ProjectAbsolute("Assets/VFX/Candidates/" + RevisionNamespace);
            DeleteDirectory(revisionRoot);
            DeleteFile(revisionRoot + ".meta");
            for (var revision = 1; revision <= 2; revision++)
            {
                DeleteFile(RepositoryAbsolute(BundlePath(revision)));
                DeleteFile(RepositoryAbsolute(SourcePath(revision)));
            }
            DeleteFile(ProjectAbsolute(ManifestPath));
        }

        private static IEnumerable<string> TestOwnedAbsolutePaths()
        {
            yield return RepositoryAbsolute("docs/vfx-candidates/" + EffectId);
            yield return ProjectAbsolute(C0Root);
            yield return ProjectAbsolute(C0Root + ".meta");
            yield return ProjectAbsolute(C0Preview);
            yield return ProjectAbsolute(C0Preview + ".meta");
            var revisionRoot = ProjectAbsolute("Assets/VFX/Candidates/" + RevisionNamespace);
            yield return revisionRoot;
            yield return revisionRoot + ".meta";
            for (var revision = 1; revision <= 2; revision++)
            {
                yield return RepositoryAbsolute(BundlePath(revision));
                yield return RepositoryAbsolute(SourcePath(revision));
            }
            yield return ProjectAbsolute(ManifestPath);
        }

        private static void ValidateRevisionCleanupBoundary()
        {
            var candidatesRoot = ProjectAbsolute("Assets/VFX/Candidates").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var revisionRoot = ProjectAbsolute("Assets/VFX/Candidates/" + RevisionNamespace).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(Path.GetDirectoryName(revisionRoot), candidatesRoot, StringComparison.OrdinalIgnoreCase) || !string.Equals(Path.GetFileName(revisionRoot), RevisionNamespace, StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing to clean a candidate revision root outside the exact test-owned namespace.");
        }

        private static void WriteAsset(string relative, string text, string guid)
        {
            WriteProject(relative, text);
            WriteProject(relative + ".meta", "fileFormatVersion: 2\nguid: " + guid + "\n");
        }
        private static void WriteRepository(string relative, string text) { Write(RepositoryAbsolute(relative), text); }
        private static void WriteProject(string relative, string text) { Write(ProjectAbsolute(relative), text); }
        private static void Write(string absolute, string text) { Directory.CreateDirectory(Path.GetDirectoryName(absolute)); File.WriteAllText(absolute, text, new UTF8Encoding(false)); }
        private static void DeleteFile(string path) { if (File.Exists(path)) File.Delete(path); }
        private static void DeleteDirectory(string path) { if (Directory.Exists(path)) Directory.Delete(path, true); }
        private static JObject ParseRepository(string relative) { return JObject.Parse(File.ReadAllText(RepositoryAbsolute(relative))); }
        private static string Serialize(JToken value) { return value.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n"; }
        private static string RawFileHash(string assetPath) { return RawBytes(File.ReadAllBytes(ProjectAbsolute(assetPath))); }
        private static string FileHashRepository(string relative) { return HashBytes(File.ReadAllBytes(RepositoryAbsolute(relative))); }
        private static string FileHashProject(string relative) { return HashBytes(File.ReadAllBytes(ProjectAbsolute(relative))); }
        private static string Hash(string text) { return HashBytes(new UTF8Encoding(false).GetBytes(text)); }
        private static string RawHash(string text) { return RawBytes(new UTF8Encoding(false).GetBytes(text)); }
        private static string HashBytes(byte[] bytes) { return "sha256:" + RawBytes(bytes); }
        private static string RawBytes(byte[] bytes) { using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2"))); }
        private static string GuidFor(string text) { return RawHash(text).Substring(0, 32); }
        private static string TreeHash(string root)
        {
            var builder = new StringBuilder();
            foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(value => value, StringComparer.OrdinalIgnoreCase)) builder.Append(HashBytes(File.ReadAllBytes(file))).Append("  ").Append(file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar).Replace('\\', '/')).Append('\n');
            return Hash(builder.ToString());
        }
        private static string ProjectRoot { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); } }
        private static string RepositoryRoot { get { return Path.GetFullPath(Path.Combine(ProjectRoot, "..")); } }
        private static string ProjectAbsolute(string relative) { return Path.GetFullPath(Path.Combine(ProjectRoot, relative.Replace('/', Path.DirectorySeparatorChar))); }
        private static string RepositoryAbsolute(string relative) { return Path.GetFullPath(Path.Combine(RepositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar))); }
    }
}
