using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using VFXComposer.Editor.W24.S4;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W24S4MigrationAuditTests
    {
        private string repositoryRoot;
        private string root;

        [SetUp]
        public void SetUp()
        {
            repositoryRoot = Path.Combine(Path.GetTempPath(), "w24-s4-audit-" + Guid.NewGuid().ToString("N"));
            root = Path.Combine(repositoryRoot, "project");
            Directory.CreateDirectory(root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(repositoryRoot)) Directory.Delete(repositoryRoot, true);
        }

        [Test]
        public void ScanProject_IsDeterministic_ReadOnly_AndNeverAssignsAMaturityLevel()
        {
            CreateFormalEntry("strict_effect", "strict", true);
            var first = W24S4MigrationAudit.ScanProject(root);
            var second = W24S4MigrationAudit.ScanProject(root);
            var entry = first.Entries.Single();

            Assert.That(first.InventoryHash, Is.EqualTo(second.InventoryHash));
            Assert.That(entry.VisualStatus, Is.EqualTo("VISUAL_PENDING"));
            Assert.That(entry.IsOwnershipVerified, Is.True);
            Assert.That(entry.RiskScore, Is.EqualTo(0));
            Assert.That(entry.SuggestedRoute, Is.EqualTo(W24S4MigrationRoute.RebuildCandidate));
            Assert.That(File.Exists(Path.Combine(root, "Assets", "VFX", "Generated", "strict_effect", "VFX_strict_effect.prefab")), Is.True);
        }

        [Test]
        public void ScanProject_RejectsTamperedOwnedOutput_AndRoutesOnlyToQuarantineReview()
        {
            var prefab = CreateFormalEntry("tampered_effect", "strict", true);
            File.AppendAllText(prefab, "tampered");

            var entry = W24S4MigrationAudit.ScanProject(root).Entries.Single();

            Assert.That(entry.IsOwnershipVerified, Is.False);
            Assert.That(entry.SuggestedRoute, Is.EqualTo(W24S4MigrationRoute.QuarantineReview));
            Assert.That(entry.RiskReasons.Any(reason => reason.Contains("SHA-256")), Is.True);
            Assert.That(entry.VisualStatus, Is.EqualTo("VISUAL_PENDING"));
        }

        [Test]
        public void ScanProject_RejectsTamperedNonEntryOwnedOutput()
        {
            CreateFormalEntry("aux_tampered", "strict", true, true);
            var auxiliary = Path.Combine(root, "Assets", "VFX", "Generated", "aux_tampered", "local.asset");
            File.AppendAllText(auxiliary, "tampered");

            var entry = W24S4MigrationAudit.ScanProject(root).Entries.Single();

            Assert.That(entry.RuntimeEntryOwnedHashVerified, Is.True, "the runtime entry itself is intentionally unchanged");
            Assert.That(entry.AllOwnedOutputsVerified, Is.False);
            Assert.That(entry.IsOwnershipVerified, Is.False);
            Assert.That(entry.SuggestedRoute, Is.EqualTo(W24S4MigrationRoute.QuarantineReview));
        }

        [Test]
        public void LegacyAudit_RemainsLegacyRetention_AndDoesNotBecomeAnApproval()
        {
            CreateFormalEntry("legacy_effect", "legacy_audit", false);
            var entry = W24S4MigrationAudit.ScanProject(root).Entries.Single();

            Assert.That(entry.SuggestedRoute, Is.EqualTo(W24S4MigrationRoute.LegacyRetain));
            Assert.That(entry.RiskReasons.Any(reason => reason.Contains("legacy_audit")), Is.True);
            Assert.That(entry.VisualStatus, Is.EqualTo("VISUAL_PENDING"));
        }

        [Test]
        public void DryRunPlan_RequiresMatchingExplicitUserTokenAndOwnershipBeforeApply()
        {
            CreateFormalEntry("approved_effect", "strict", true);
            WriteAdr("Accepted", "test-decision-maker");
            var inventory = W24S4MigrationAudit.ScanProject(root);
            var dryRun = W24S4MigrationAudit.CreateDryRunPlan(inventory);
            var transaction = new RecordingTransaction();

            Assert.That(dryRun.Mode, Is.EqualTo(W24S4PlanMode.DryRun));
            Assert.Throws<InvalidOperationException>(() => W24S4UserDecisionAuthority.Issue("decision-invalid", dryRun, dryRun.Operations.Single().BatchId, "user", dryRun.Operations));
            var plan = W24S4MigrationAudit.PromoteForUserDecision(dryRun);
            var operation = plan.Operations.Single();
            var token = W24S4UserDecisionAuthority.Issue("decision-1", plan, operation.BatchId, "user", new[] { operation });
            Assert.That(W24S4MigrationPolicy.CanApply(plan, operation, token), Is.True);
            Assert.That(transaction.BeginCount, Is.EqualTo(0));

            var mismatched = new W24S4UserDecisionToken("decision-2", "sha256:" + new string('0', 64), plan.PlanHash, operation.BatchId, "user", new[] { operation.EffectId }, new[] { W24S4MigrationAudit.ComputeOperationHash(operation) });
            Assert.That(W24S4MigrationPolicy.CanApply(plan, operation, mismatched), Is.False);
            Assert.Throws<InvalidOperationException>(() => W24S4MigrationPolicy.Apply(plan, operation, mismatched, transaction));
            Assert.That(transaction.BeginCount, Is.EqualTo(0));

            W24S4MigrationPolicy.Apply(plan, operation, token, transaction);
            Assert.That(transaction.BeginCount, Is.EqualTo(1));
            Assert.That(transaction.ApplyCount, Is.EqualTo(1));
            Assert.That(transaction.CommitCount, Is.EqualTo(1));
            Assert.That(transaction.RollbackCount, Is.EqualTo(0));
        }

        [Test]
        public void DefaultPlan_CannotApplyWhileAdr001RemainsUnapproved()
        {
            CreateFormalEntry("adr_frozen", "strict", true);
            WriteAdr("Proposed", "待填写");
            var inventory = W24S4MigrationAudit.ScanProject(root);
            var plan = W24S4MigrationAudit.CreateDryRunPlan(inventory);

            Assert.That(plan.PrefabCopyAndSharedDependenciesAdrApproved, Is.False);
            Assert.Throws<InvalidOperationException>(() => W24S4MigrationAudit.PromoteForUserDecision(plan));
        }

        [Test]
        public void ApplyToken_IsInvalidatedWhenAuthoritativeAdrBytesChange()
        {
            CreateFormalEntry("adr_changed", "strict", true);
            WriteAdr("Accepted", "test-decision-maker");
            var plan = W24S4MigrationAudit.PromoteForUserDecision(W24S4MigrationAudit.CreateDryRunPlan(W24S4MigrationAudit.ScanProject(root)));
            var operation = plan.Operations.Single();
            var token = W24S4UserDecisionAuthority.Issue("decision-adr-change", plan, operation.BatchId, "user", new[] { operation });

            WriteAdr("Proposed", "待填写");

            Assert.That(W24S4MigrationPolicy.CanApply(plan, operation, token), Is.False);
        }

        [Test]
        public void ApplyToken_IsBoundToExactPlanAndOperationBytes()
        {
            CreateFormalEntry("bound_effect", "strict", true);
            WriteAdr("Accepted", "test-decision-maker");
            var inventory = W24S4MigrationAudit.ScanProject(root);
            var plan = W24S4MigrationAudit.PromoteForUserDecision(W24S4MigrationAudit.CreateDryRunPlan(inventory));
            var operation = plan.Operations.Single();
            var token = W24S4UserDecisionAuthority.Issue("decision-bound", plan, operation.BatchId, "user", new[] { operation });

            operation.Route = W24S4MigrationRoute.QuarantineReview;

            Assert.That(W24S4MigrationPolicy.CanApply(plan, operation, token), Is.False);
            Assert.Throws<InvalidOperationException>(() => W24S4MigrationPolicy.Apply(plan, operation, token, new RecordingTransaction()));
        }

        [Test]
        public void BatchToken_CannotAuthorizeAnUnlistedOperation()
        {
            CreateFormalEntry("approved_one", "strict", true);
            CreateFormalEntry("unapproved_two", "strict", true);
            WriteAdr("Accepted", "test-decision-maker");
            var inventory = W24S4MigrationAudit.ScanProject(root);
            var plan = W24S4MigrationAudit.PromoteForUserDecision(W24S4MigrationAudit.CreateDryRunPlan(inventory));
            var approved = plan.Operations.Single(value => value.EffectId == "approved_one");
            var unapproved = plan.Operations.Single(value => value.EffectId == "unapproved_two");
            var token = W24S4UserDecisionAuthority.Issue("decision-one", plan, approved.BatchId, "user", new[] { approved });

            Assert.That(W24S4MigrationPolicy.CanApply(plan, approved, token), Is.True);
            Assert.That(W24S4MigrationPolicy.CanApply(plan, unapproved, token), Is.False);
        }

        [Test]
        public void Apply_RollsBackWhenTransactionFails()
        {
            CreateFormalEntry("rollback_effect", "strict", true);
            WriteAdr("Accepted", "test-decision-maker");
            var inventory = W24S4MigrationAudit.ScanProject(root);
            var plan = W24S4MigrationAudit.PromoteForUserDecision(W24S4MigrationAudit.CreateDryRunPlan(inventory));
            var operation = plan.Operations.Single();
            var token = W24S4UserDecisionAuthority.Issue("decision-3", plan, operation.BatchId, "user", new[] { operation });
            var transaction = new RecordingTransaction { ThrowOnApply = true };

            Assert.Throws<InvalidOperationException>(() => W24S4MigrationPolicy.Apply(plan, operation, token, transaction));
            Assert.That(transaction.RollbackCount, Is.EqualTo(1));
            Assert.That(transaction.CommitCount, Is.EqualTo(0));
        }

        [Test]
        public void Apply_PreservesBothPrimaryAndRollbackFailures()
        {
            CreateFormalEntry("double_failure", "strict", true);
            WriteAdr("Accepted", "test-decision-maker");
            var inventory = W24S4MigrationAudit.ScanProject(root);
            var plan = W24S4MigrationAudit.PromoteForUserDecision(W24S4MigrationAudit.CreateDryRunPlan(inventory));
            var operation = plan.Operations.Single();
            var token = W24S4UserDecisionAuthority.Issue("decision-double", plan, operation.BatchId, "user", new[] { operation });
            var transaction = new RecordingTransaction { ThrowOnApply = true, ThrowOnRollback = true };

            var error = Assert.Throws<AggregateException>(() => W24S4MigrationPolicy.Apply(plan, operation, token, transaction));
            Assert.That(error.InnerExceptions.Count, Is.EqualTo(2));
            Assert.That(transaction.RollbackCount, Is.EqualTo(1));
        }

        [Test]
        public void Apply_RefusesWhenLiveOwnershipCannotBeReverified()
        {
            CreateFormalEntry("reverify_effect", "strict", true);
            WriteAdr("Accepted", "test-decision-maker");
            var inventory = W24S4MigrationAudit.ScanProject(root);
            var plan = W24S4MigrationAudit.PromoteForUserDecision(W24S4MigrationAudit.CreateDryRunPlan(inventory));
            var operation = plan.Operations.Single();
            var token = W24S4UserDecisionAuthority.Issue("decision-4", plan, operation.BatchId, "user", new[] { operation });
            var transaction = new RecordingTransaction { OwnershipStillValid = false };

            Assert.Throws<InvalidOperationException>(() => W24S4MigrationPolicy.Apply(plan, operation, token, transaction));
            Assert.That(transaction.BeginCount, Is.EqualTo(0));
            Assert.That(transaction.ApplyCount, Is.EqualTo(0));
        }

        private string CreateFormalEntry(string effectId, string enforcement, bool createW24Evidence, bool includeAuxiliary = false)
        {
            var generated = Path.Combine(root, "Assets", "VFX", "Generated", effectId); Directory.CreateDirectory(generated);
            var prefab = Path.Combine(generated, "VFX_" + effectId + ".prefab"); File.WriteAllText(prefab, "%YAML 1.1\n");
            var guid = "0123456789abcdef0123456789abcdef"; File.WriteAllText(prefab + ".meta", "fileFormatVersion: 2\nguid: " + guid + "\n");
            var recipe = Path.Combine(root, "Assets", "VFX", "Recipes", effectId + ".json"); Directory.CreateDirectory(Path.GetDirectoryName(recipe)); File.WriteAllText(recipe, "{}");
            var auxiliary = Path.Combine(generated, "local.asset");
            if (includeAuxiliary) { File.WriteAllText(auxiliary, "local"); File.WriteAllText(auxiliary + ".meta", "fileFormatVersion: 2\nguid: fedcba9876543210fedcba9876543210\n"); }
            var auxiliaryOutput = includeAuxiliary ? ",{\"path\":\"Assets/VFX/Generated/" + effectId + "/local.asset\",\"guid\":\"fedcba9876543210fedcba9876543210\",\"assetType\":\"TextAsset\",\"sha256\":\"" + FileHash(auxiliary) + "\"}" : string.Empty;
            var manifest = "{\n" +
                "\"manifestVersion\":1,\n\"effectId\":\"" + effectId + "\",\n\"enforcement\":\"" + enforcement + "\",\n\"rulesVersion\":\"1.0-draft\",\n\"recipeVersion\":1,\n\"recipeRevision\":1,\n\"recipeHash\":\"" + HexHash("recipe-" + effectId) + "\",\n\"buildHash\":\"" + HexHash("build-" + effectId) + "\",\n\"compilerVersion\":\"test-1\",\n\"unityVersion\":\"2022.3.62f3c1\",\n" +
                "\"sourceRecipePath\":\"Assets/VFX/Recipes/" + effectId + ".json\",\n" +
                "\"runtimeEntry\":{\"kind\":\"prefab\",\"path\":\"Assets/VFX/Generated/" + effectId + "/VFX_" + effectId + ".prefab\",\"guid\":\"" + guid + "\"},\n" +
                "\"ownedOutputs\":[{\"path\":\"Assets/VFX/Generated/" + effectId + "/VFX_" + effectId + ".prefab\",\"guid\":\"" + guid + "\",\"assetType\":\"GameObject\",\"sha256\":\"" + FileHash(prefab) + "\"}" + auxiliaryOutput + "],\n" +
                "\"dependencies\":[{\"path\":\"Assets/VFX/Shared/Textures/common.png\"}]\n}";
            var manifestPath = Path.Combine(root, "ProjectSettings", "VFXComposer", "BuildManifests", effectId + ".manifest.json"); Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)); File.WriteAllText(manifestPath, manifest);
            if (createW24Evidence)
            {
                Write(Path.Combine(root, "Assets", "VFX", "Contracts", effectId + ".json"), "{\"effectId\": \"" + effectId + "\"}");
                Write(Path.Combine(root, "Assets", "VFX", "Traces", effectId + ".json"), "{\"effectId\": \"" + effectId + "\"}");
                Write(Path.Combine(root, "Assets", "VFX", "Preview", effectId + ".unity"), "scene");
                Write(Path.Combine(root, "Assets", "VFX", "Evidence", effectId + ".json"), "evidence");
            }
            return prefab;
        }

        private void WriteAdr(string status, string decisionMaker)
        {
            Write(Path.Combine(repositoryRoot, "docs", "rules", "ADR-001_PREFAB_COPY_AND_SHARED_DEPENDENCIES.md"),
                "# ADR-001\n\n状态：`" + status + "`\n决策人：" + decisionMaker + "\n");
        }

        private static void Write(string path, string content) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, content); }
        private static string FileHash(string path) { return HexHash(File.ReadAllBytes(path)); }
        private static string HexHash(string text) { return HexHash(System.Text.Encoding.UTF8.GetBytes(text)); }
        private static string HexHash(byte[] value) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(value)).Replace("-", string.Empty).ToLowerInvariant(); }

        private sealed class RecordingTransaction : IW24S4MigrationTransaction
        {
            public int BeginCount; public int ApplyCount; public int CommitCount; public int RollbackCount; public bool ThrowOnApply; public bool ThrowOnRollback; public bool OwnershipStillValid = true;
            public bool ReverifyOwnership(W24S4MigrationOperation operation) { return OwnershipStillValid; }
            public void Begin(W24S4MigrationOperation operation) { BeginCount++; }
            public void Apply(W24S4MigrationOperation operation) { ApplyCount++; if (ThrowOnApply) throw new InvalidOperationException("injected failure"); }
            public void Commit() { CommitCount++; }
            public void Rollback() { RollbackCount++; if (ThrowOnRollback) throw new InvalidOperationException("injected rollback failure"); }
        }
    }
}
