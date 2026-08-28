using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace VFXComposer.Editor.W24.S4
{
    /// <summary>
    /// S4 is intentionally an evidence inventory, not a visual review or migration executor.
    /// It never assigns L0-L4: all entries remain VISUAL_PENDING until the formal/user flow decides.
    /// </summary>
    public enum W24S4MigrationRoute { LegacyRetain, WaiverReview, RebuildCandidate, QuarantineReview }
    public enum W24S4PlanMode { DryRun, Apply }

    [Serializable]
    public sealed class W24S4InventoryEntry
    {
        public string EffectId;
        public string GeneratedDirectory;
        public string ManifestPath;
        public string RuntimeEntryPath;
        public string RuntimeEntryGuid;
        public string BuildHash;
        public string RulesVersion;
        public string Enforcement;
        public bool HasGeneratedDirectory;
        public bool HasManifest;
        public bool HasRecipe;
        public bool HasContract;
        public bool HasTrace;
        public bool HasPreview;
        public bool HasEvidence;
        public bool RuntimeEntryExists;
        public bool RuntimeEntryGuidVerified;
        public bool RuntimeEntryOwnedHashVerified;
        public bool AllOwnedOutputsVerified;
        public bool HasRuntimeEntry;
        public bool IsOwnershipVerified;
        public int RiskScore;
        public string RiskBand;
        public W24S4MigrationRoute SuggestedRoute;
        public string SuggestedBatch;
        // This is a workflow state, not a visual verdict or L-level.
        public string VisualStatus = "VISUAL_PENDING";
        public string[] RiskReasons = Array.Empty<string>();
        public string[] AuditWarnings = Array.Empty<string>();
        public string[] CarrierKeys = Array.Empty<string>();
    }

    [Serializable]
    public sealed class W24S4CarrierReuse
    {
        public string CarrierKey;
        public string[] EffectIds = Array.Empty<string>();
        public string ReviewReason;
    }

    [Serializable]
    public sealed class W24S4AuditInventory
    {
        public const string Schema = "w24-s4/audit-inventory-v1";
        public string SchemaVersion = Schema;
        public string ProjectRoot;
        public W24S4InventoryEntry[] Entries = Array.Empty<W24S4InventoryEntry>();
        public W24S4CarrierReuse[] CarrierReuse = Array.Empty<W24S4CarrierReuse>();
        public string InventoryHash;
    }

    [Serializable]
    public sealed class W24S4MigrationOperation
    {
        public string EffectId;
        public string BatchId;
        public W24S4MigrationRoute Route;
        public string RuntimeEntryPath;
        public string ExpectedRuntimeEntryGuid;
        public string ExpectedRuntimeEntryHash;
        public bool OwnershipVerified;
        public bool RequiresExplicitUserDecision = true;
        public string[] Preconditions = Array.Empty<string>();
    }

    [Serializable]
    public sealed class W24S4MigrationPlan
    {
        public const string Schema = "w24-s4/migration-plan-v2";
        public string SchemaVersion = Schema;
        public W24S4PlanMode Mode = W24S4PlanMode.DryRun;
        public string ProjectRoot;
        public string Adr001Status;
        public string Adr001DecisionMaker;
        public string Adr001DocumentHash;
        public bool PrefabCopyAndSharedDependenciesAdrApproved { get { return string.Equals(Adr001Status, "Accepted", StringComparison.Ordinal) && !string.IsNullOrEmpty(Adr001DecisionMaker) && !string.Equals(Adr001DecisionMaker, "待填写", StringComparison.Ordinal); } }
        public string InventoryHash;
        public W24S4MigrationOperation[] Operations = Array.Empty<W24S4MigrationOperation>();
        public string PlanHash;
    }

    /// <summary>Opaque user approval; callers must obtain it from an explicit user action, never infer it.</summary>
    public sealed class W24S4UserDecisionToken
    {
        internal W24S4UserDecisionToken(
            string tokenId,
            string inventoryHash,
            string planHash,
            string batchId,
            string userIdentity,
            IEnumerable<string> approvedEffectIds,
            IEnumerable<string> approvedOperationHashes)
        {
            TokenId = tokenId;
            InventoryHash = inventoryHash;
            PlanHash = planHash;
            BatchId = batchId;
            UserIdentity = userIdentity;
            this.approvedEffectIds = (approvedEffectIds ?? Enumerable.Empty<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            this.approvedOperationHashes = (approvedOperationHashes ?? Enumerable.Empty<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
        public string TokenId { get; private set; }
        public string InventoryHash { get; private set; }
        public string PlanHash { get; private set; }
        public string BatchId { get; private set; }
        public string UserIdentity { get; private set; }
        private readonly string[] approvedEffectIds;
        private readonly string[] approvedOperationHashes;
        public IReadOnlyList<string> ApprovedEffectIds { get { return Array.AsReadOnly(approvedEffectIds); } }
        public IReadOnlyList<string> ApprovedOperationHashes { get { return Array.AsReadOnly(approvedOperationHashes); } }
        internal bool Approves(string effectId, string operationHash)
        {
            return approvedEffectIds.Contains(effectId, StringComparer.Ordinal) && approvedOperationHashes.Contains(operationHash, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// The future UI/user-verdict boundary is the only production caller that may issue a token.
    /// Keeping issuance internal prevents Runtime or external automation from manufacturing approval.
    /// </summary>
    internal static class W24S4UserDecisionAuthority
    {
        internal static W24S4UserDecisionToken Issue(
            string tokenId,
            W24S4MigrationPlan plan,
            string batchId,
            string userIdentity,
            IEnumerable<W24S4MigrationOperation> approvedOperations)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (plan.Mode != W24S4PlanMode.Apply || !string.Equals(plan.PlanHash, W24S4MigrationAudit.ComputePlanHash(plan), StringComparison.Ordinal))
                throw new InvalidOperationException("S4 user decisions can bind only an intact Apply plan promoted from the reviewed dry-run.");
            if (!W24S4MigrationAudit.VerifyCurrentAdr001Decision(plan))
                throw new InvalidOperationException("S4 apply remains frozen until the repository ADR-001 is Accepted by a named decision maker.");
            if (string.IsNullOrWhiteSpace(tokenId) || string.IsNullOrWhiteSpace(batchId) || string.IsNullOrWhiteSpace(userIdentity))
                throw new ArgumentException("S4 user decision identity, batch, and user are required.");
            var operations = (approvedOperations ?? Enumerable.Empty<W24S4MigrationOperation>()).ToArray();
            if (operations.Length == 0) throw new ArgumentException("S4 user decision must explicitly list at least one operation.", nameof(approvedOperations));
            return new W24S4UserDecisionToken(
                tokenId,
                plan.InventoryHash,
                plan.PlanHash,
                batchId,
                userIdentity,
                operations.Select(operation => operation == null ? null : operation.EffectId).Where(value => !string.IsNullOrEmpty(value)),
                operations.Select(W24S4MigrationAudit.ComputeOperationHash));
        }
    }

    /// <summary>
    /// Deliberately injected so S4 cannot write historical Unity bytes by itself. An integration may
    /// mutate only after this policy has verified ownership and a user token; failures must rollback.
    /// </summary>
    public interface IW24S4MigrationTransaction
    {
        /// <summary>Must re-read the live GUID/hash ownership proof immediately before mutation.</summary>
        bool ReverifyOwnership(W24S4MigrationOperation operation);
        void Begin(W24S4MigrationOperation operation);
        void Apply(W24S4MigrationOperation operation);
        void Commit();
        void Rollback();
    }

    public static class W24S4MigrationPolicy
    {
        public static bool CanApply(W24S4MigrationPlan plan, W24S4MigrationOperation operation, W24S4UserDecisionToken token)
        {
            var operationHash = operation == null ? null : W24S4MigrationAudit.ComputeOperationHash(operation);
            return plan != null && operation != null && token != null && plan.Mode == W24S4PlanMode.Apply
                && plan.PrefabCopyAndSharedDependenciesAdrApproved
                && operation.Route == W24S4MigrationRoute.RebuildCandidate
                && operation.RequiresExplicitUserDecision && operation.OwnershipVerified
                && W24S4MigrationAudit.VerifyCurrentAdr001Decision(plan)
                && Same(plan.InventoryHash, token.InventoryHash) && Same(operation.BatchId, token.BatchId)
                && Same(plan.PlanHash, token.PlanHash) && Same(plan.PlanHash, W24S4MigrationAudit.ComputePlanHash(plan))
                && plan.Operations != null && plan.Operations.Any(candidate => Same(W24S4MigrationAudit.ComputeOperationHash(candidate), operationHash))
                && token.Approves(operation.EffectId, operationHash)
                && !string.IsNullOrEmpty(token.TokenId) && !string.IsNullOrEmpty(token.UserIdentity)
                && !string.IsNullOrEmpty(operation.ExpectedRuntimeEntryGuid) && !string.IsNullOrEmpty(operation.ExpectedRuntimeEntryHash);
        }

        public static void Apply(W24S4MigrationPlan plan, W24S4MigrationOperation operation, W24S4UserDecisionToken token, IW24S4MigrationTransaction transaction)
        {
            if (!CanApply(plan, operation, token)) throw new InvalidOperationException("S4 migration requires Apply mode, verified ownership, and an explicit matching user-decision token.");
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (!transaction.ReverifyOwnership(operation)) throw new InvalidOperationException("S4 migration ownership changed since dry-run; apply is refused.");
            try
            {
                transaction.Begin(operation);
                transaction.Apply(operation);
                transaction.Commit();
            }
            catch (Exception primary)
            {
                try { transaction.Rollback(); }
                catch (Exception rollback)
                {
                    throw new AggregateException("S4 migration and rollback both failed; preserve the transaction snapshot for manual recovery.", primary, rollback);
                }
                throw;
            }
        }

        private static bool Same(string a, string b) { return string.Equals(a, b, StringComparison.Ordinal); }
    }

    public static class W24S4MigrationAudit
    {
        private const string Generated = "Assets/VFX/Generated";
        private const string Manifests = "ProjectSettings/VFXComposer/BuildManifests";

        public static W24S4AuditInventory ScanProject(string projectRoot)
        {
            if (string.IsNullOrEmpty(projectRoot)) throw new ArgumentException("Project root is required.", nameof(projectRoot));
            var root = Path.GetFullPath(projectRoot);
            var generated = Path.Combine(root, Generated.Replace('/', Path.DirectorySeparatorChar));
            var manifests = Path.Combine(root, Manifests.Replace('/', Path.DirectorySeparatorChar));
            var ids = new SortedSet<string>(StringComparer.Ordinal);
            if (Directory.Exists(generated)) foreach (var path in Directory.GetDirectories(generated)) ids.Add(Path.GetFileName(path));
            if (Directory.Exists(manifests)) foreach (var path in Directory.GetFiles(manifests, "*.manifest.json")) ids.Add(Path.GetFileName(path).Replace(".manifest.json", string.Empty));

            var entries = ids.Select(id => ScanEntry(root, id, generated, manifests)).OrderBy(x => x.EffectId, StringComparer.Ordinal).ToArray();
            var inventory = new W24S4AuditInventory { ProjectRoot = Normalize(root), Entries = entries };
            inventory.CarrierReuse = FindCarrierReuse(entries);
            inventory.InventoryHash = ComputeInventoryHash(inventory);
            return inventory;
        }

        public static W24S4MigrationPlan CreateDryRunPlan(W24S4AuditInventory inventory)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            var operations = inventory.Entries.OrderBy(x => x.SuggestedBatch, StringComparer.Ordinal).ThenBy(x => x.EffectId, StringComparer.Ordinal)
                .Select(entry => new W24S4MigrationOperation {
                    EffectId = entry.EffectId, BatchId = entry.SuggestedBatch, Route = entry.SuggestedRoute,
                    RuntimeEntryPath = entry.RuntimeEntryPath, ExpectedRuntimeEntryGuid = entry.RuntimeEntryGuid,
                    ExpectedRuntimeEntryHash = entry.RuntimeEntryOwnedHashVerified ? HashFileFromProject(inventory.ProjectRoot, entry.RuntimeEntryPath) : null,
                    OwnershipVerified = entry.IsOwnershipVerified,
                    Preconditions = Preconditions(entry)
                }).ToArray();
            var adr = ReadAdr001Decision(inventory.ProjectRoot);
            var plan = new W24S4MigrationPlan
            {
                ProjectRoot = Normalize(Path.GetFullPath(inventory.ProjectRoot)),
                Adr001Status = adr.Status,
                Adr001DecisionMaker = adr.DecisionMaker,
                Adr001DocumentHash = adr.DocumentHash,
                InventoryHash = inventory.InventoryHash,
                Operations = operations
            };
            plan.PlanHash = ComputePlanHash(plan);
            return plan;
        }

        internal static W24S4MigrationPlan PromoteForUserDecision(W24S4MigrationPlan dryRun)
        {
            if (dryRun == null || dryRun.Mode != W24S4PlanMode.DryRun || !Same(dryRun.PlanHash, ComputePlanHash(dryRun)))
                throw new InvalidOperationException("Only an intact S4 dry-run plan can be promoted for an explicit user decision.");
            if (!VerifyCurrentAdr001Decision(dryRun))
                throw new InvalidOperationException("ADR-001 is not currently Accepted by a named decision maker; S4 Apply remains frozen.");
            var apply = new W24S4MigrationPlan
            {
                Mode = W24S4PlanMode.Apply,
                ProjectRoot = dryRun.ProjectRoot,
                Adr001Status = dryRun.Adr001Status,
                Adr001DecisionMaker = dryRun.Adr001DecisionMaker,
                Adr001DocumentHash = dryRun.Adr001DocumentHash,
                InventoryHash = dryRun.InventoryHash,
                Operations = (dryRun.Operations ?? Array.Empty<W24S4MigrationOperation>()).Select(CloneOperation).ToArray()
            };
            apply.PlanHash = ComputePlanHash(apply);
            return apply;
        }

        public static string RenderMarkdownReport(W24S4AuditInventory inventory, W24S4MigrationPlan plan)
        {
            if (inventory == null || plan == null) throw new ArgumentNullException(inventory == null ? nameof(inventory) : nameof(plan));
            var builder = new StringBuilder();
            builder.AppendLine("# W24 S4 既有资产只读审计报告");
            builder.AppendLine();
            builder.AppendLine("- 审计模式：只读；迁移计划默认 `DryRun`，未执行任何资产写入。");
            builder.AppendLine("- 视觉状态：所有条目均为 `VISUAL_PENDING`；本报告不产生 L0–L4 结论或视觉通过。");
            builder.AppendLine("- Inventory hash: `" + inventory.InventoryHash + "`");
            builder.AppendLine("- Plan hash: `" + plan.PlanHash + "`");
            builder.AppendLine();
            builder.AppendLine("| EffectId | 风险 | 路由 | 批次 | 合同/Trace | Preview/证据 |");
            builder.AppendLine("|---|---:|---|---|---|---|");
            foreach (var entry in inventory.Entries)
                builder.AppendLine("| `" + entry.EffectId + "` | " + entry.RiskScore + " (" + entry.RiskBand + ") | " + entry.SuggestedRoute + " | " + entry.SuggestedBatch + " | " + YesNo(entry.HasContract) + "/" + YesNo(entry.HasTrace) + " | " + YesNo(entry.HasPreview) + "/" + YesNo(entry.HasEvidence) + " |");
            builder.AppendLine();
            builder.AppendLine("## 执行约束");
            builder.AppendLine();
            builder.AppendLine("每一项迁移都必须重新核验 Runtime Entry 的 GUID、owned-output SHA-256 与 Manifest 所有权；并要求与 inventory hash、批次匹配的显式用户决策 token。事务失败必须 Rollback。此 dry-run 没有 Apply 权限。");
            if (inventory.CarrierReuse.Length > 0) {
                builder.AppendLine(); builder.AppendLine("## 通用载体复用抽样"); builder.AppendLine();
                foreach (var carrier in inventory.CarrierReuse) builder.AppendLine("- `" + carrier.CarrierKey + "`: " + string.Join(", ", carrier.EffectIds) + " — " + carrier.ReviewReason);
            }
            return builder.ToString();
        }

        private static W24S4InventoryEntry ScanEntry(string root, string effectId, string generatedRoot, string manifestRoot)
        {
            var generatedDirectory = Path.Combine(generatedRoot, effectId);
            var manifestPath = Path.Combine(manifestRoot, effectId + ".manifest.json");
            var entry = new W24S4InventoryEntry { EffectId = effectId, GeneratedDirectory = ProjectPath(root, generatedDirectory), ManifestPath = ProjectPath(root, manifestPath), HasGeneratedDirectory = Directory.Exists(generatedDirectory), HasManifest = File.Exists(manifestPath) };
            var reasons = new List<string>(); var warnings = new List<string>(); var carriers = new List<string>();
            Manifest manifest = entry.HasManifest ? ReadManifest(manifestPath) : null;
            if (!entry.HasManifest) reasons.Add("缺少权威 BuildManifest。");
            else if (manifest == null || !Same(effectId, manifest.effectId)) reasons.Add("BuildManifest 不可解析或 effectId 与目录不一致。");
            else {
                entry.Enforcement = manifest.enforcement;
                entry.RulesVersion = manifest.rulesVersion;
                entry.BuildHash = CanonicalHash(manifest.buildHash);
                if (manifest.manifestVersion < 1 || manifest.recipeVersion < 1 || manifest.recipeRevision < 1) reasons.Add("BuildManifest 版本或 Recipe 版本字段无效。");
                if (!IsCanonicalHash(CanonicalHash(manifest.recipeHash))) reasons.Add("Recipe hash 非 canonical SHA-256。");
                if (!IsCanonicalHash(entry.BuildHash)) reasons.Add("Build hash 非 canonical SHA-256。");
                if (string.IsNullOrEmpty(manifest.compilerVersion) || string.IsNullOrEmpty(manifest.unityVersion)) reasons.Add("BuildManifest 缺少 compilerVersion 或 unityVersion。");
                entry.HasRecipe = ProjectFileExists(root, manifest.sourceRecipePath);
                entry.HasRuntimeEntry = manifest.runtimeEntry != null && Same("prefab", manifest.runtimeEntry.kind) && ProjectPathIsSafe(root, manifest.runtimeEntry.path);
                entry.RuntimeEntryPath = manifest.runtimeEntry == null ? null : manifest.runtimeEntry.path;
                entry.RuntimeEntryGuid = manifest.runtimeEntry == null ? null : NormalizeGuid(manifest.runtimeEntry.guid);
                if (!entry.HasRecipe) reasons.Add("未找到 Manifest 声明的 Recipe（只读路径核验）。");
                if (string.IsNullOrEmpty(entry.RulesVersion)) reasons.Add("BuildManifest 未声明 production rulesVersion。");
                if (string.IsNullOrEmpty(entry.Enforcement)) reasons.Add("BuildManifest 未声明 production enforcement 模式。");
                if (!entry.HasRuntimeEntry) reasons.Add("Runtime Entry 缺失、非 prefab 或路径越界。");
                else VerifyRuntimeOwnership(root, manifest, entry, reasons);
                foreach (var warning in manifest.audit ?? Array.Empty<Audit>()) if (warning != null && !string.IsNullOrEmpty(warning.code)) warnings.Add(warning.code + ": " + (warning.message ?? string.Empty));
                foreach (var dependency in manifest.dependencies ?? Array.Empty<Dependency>()) if (dependency != null && IsCarrier(dependency.path)) carriers.Add(dependency.path.Replace('\\', '/'));
                foreach (var template in manifest.templates ?? Array.Empty<Template>()) if (template != null && IsCarrier(template.assetPath)) carriers.Add(template.assetPath.Replace('\\', '/'));
                if (Same("legacy_audit", entry.Enforcement)) reasons.Add("legacy_audit 仅表示兼容审计，不等于符合新生产规则。");
            }
            var repositoryRoot = Directory.GetParent(root) == null ? null : Directory.GetParent(root).FullName;
            entry.HasContract = HasEffectBoundFile(root, effectId, "Assets/VFX/Contracts", "ProjectSettings/VFXComposer/W24/Contracts")
                || HasRepositoryEffectBoundFile(repositoryRoot, effectId, "docs/vfx-contracts");
            entry.HasTrace = HasEffectBoundFile(root, effectId, "Assets/VFX/Traces", "ProjectSettings/VFXComposer/W24/Traces")
                || HasRepositoryEffectBoundFile(repositoryRoot, effectId, "docs/vfx-traces");
            entry.HasPreview = HasNamedArtifact(root, effectId, "Assets/VFX/Preview", "Assets/VFX/Previews");
            entry.HasEvidence = HasNamedArtifact(root, effectId, "Assets/VFX/Evidence", "ProjectSettings/VFXComposer/W24/Evidence");
            if (!entry.HasContract) reasons.Add("未发现可绑定的 W24 设计合同；不得由审计补造合同。");
            if (!entry.HasTrace) reasons.Add("未发现可绑定的 Implementation Trace；不得推断实现语义。");
            if (!entry.HasPreview) reasons.Add("未发现权威 Preview 线索。");
            if (!entry.HasEvidence) reasons.Add("未发现四路证据包线索。");
            entry.AuditWarnings = warnings.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            entry.CarrierKeys = carriers.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            entry.IsOwnershipVerified = entry.HasManifest && entry.HasRuntimeEntry && entry.RuntimeEntryExists && entry.RuntimeEntryGuidVerified && entry.RuntimeEntryOwnedHashVerified && entry.AllOwnedOutputsVerified;
            entry.RiskReasons = reasons.Distinct(StringComparer.Ordinal).ToArray();
            entry.RiskScore = Score(entry, warnings.Count); entry.RiskBand = entry.RiskScore >= 70 ? "HIGH" : entry.RiskScore >= 35 ? "MEDIUM" : "LOW";
            entry.SuggestedRoute = Route(entry); entry.SuggestedBatch = Batch(entry.SuggestedRoute);
            return entry;
        }

        private static void VerifyRuntimeOwnership(string root, Manifest manifest, W24S4InventoryEntry entry, List<string> reasons)
        {
            entry.AllOwnedOutputsVerified = VerifyAllOwnedOutputs(root, manifest, entry.EffectId, reasons);
            string absolute; if (!TryProjectPath(root, manifest.runtimeEntry.path, out absolute)) { reasons.Add("Runtime Entry 路径越界。"); return; }
            entry.RuntimeEntryExists = File.Exists(absolute);
            if (!entry.RuntimeEntryExists) { reasons.Add("Runtime Entry 文件不存在。"); return; }
            var output = (manifest.ownedOutputs ?? Array.Empty<OwnedOutput>()).FirstOrDefault(x => x != null && Same(x.path, manifest.runtimeEntry.path));
            entry.RuntimeEntryGuidVerified = output != null && Same(NormalizeGuid(output.guid), entry.RuntimeEntryGuid) && Same(ReadGuid(absolute + ".meta"), entry.RuntimeEntryGuid);
            entry.RuntimeEntryOwnedHashVerified = output != null && IsCanonicalHash(CanonicalHash(output.sha256)) && Same(CanonicalHash(output.sha256), HashFile(absolute));
            if (!entry.RuntimeEntryGuidVerified) reasons.Add("Runtime Entry GUID 与 Manifest ownedOutputs 未能独立核验。");
            if (!entry.RuntimeEntryOwnedHashVerified) reasons.Add("Runtime Entry SHA-256 与 Manifest ownedOutputs 未能独立核验。");
        }

        private static bool VerifyAllOwnedOutputs(string root, Manifest manifest, string effectId, List<string> reasons)
        {
            var outputs = manifest.ownedOutputs ?? Array.Empty<OwnedOutput>();
            if (outputs.Length == 0)
            {
                reasons.Add("ownedOutputs 为空，无法建立迁移所有权边界。");
                return false;
            }
            var valid = true;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var output in outputs)
            {
                string absolute;
                if (output == null || string.IsNullOrEmpty(output.path) || !seen.Add(output.path) || !TryProjectPath(root, output.path, out absolute))
                {
                    reasons.Add("ownedOutputs 含空值、重复项或越界路径。"); valid = false; continue;
                }
                if (string.IsNullOrEmpty(output.assetType)) { reasons.Add("ownedOutputs 缺少 assetType：" + output.path); valid = false; }
                if (!File.Exists(absolute)) { reasons.Add("ownedOutputs 文件不存在：" + output.path); valid = false; continue; }
                var expectedGuid = NormalizeGuid(output.guid);
                if (string.IsNullOrEmpty(expectedGuid) || !Same(ReadGuid(absolute + ".meta"), expectedGuid)) { reasons.Add("ownedOutputs GUID 无法核验：" + output.path); valid = false; }
                if (!Same(CanonicalHash(output.sha256), HashFile(absolute))) { reasons.Add("ownedOutputs SHA-256 无法核验：" + output.path); valid = false; }
                if (Same("strict", manifest.enforcement) && !output.path.StartsWith("Assets/VFX/Generated/" + effectId + "/", StringComparison.Ordinal))
                {
                    reasons.Add("strict owned output 不在 EffectId 独立输出目录：" + output.path); valid = false;
                }
            }
            return valid;
        }

        private static int Score(W24S4InventoryEntry e, int warningCount)
        {
            var score = 0;
            if (!e.HasManifest) score += 40;
            if (!e.HasRuntimeEntry || !e.RuntimeEntryExists) score += 30;
            if (!e.RuntimeEntryGuidVerified || !e.RuntimeEntryOwnedHashVerified) score += 20;
            if (!e.AllOwnedOutputsVerified) score += 20;
            if (!e.HasContract) score += 10; if (!e.HasTrace) score += 10; if (!e.HasPreview) score += 7; if (!e.HasEvidence) score += 8;
            if (Same("legacy_audit", e.Enforcement)) score += 8;
            return Math.Min(100, score + Math.Min(12, warningCount * 2));
        }

        private static W24S4MigrationRoute Route(W24S4InventoryEntry entry)
        {
            if (!entry.IsOwnershipVerified) return W24S4MigrationRoute.QuarantineReview;
            if (Same("legacy_audit", entry.Enforcement)) return W24S4MigrationRoute.LegacyRetain;
            if (entry.AuditWarnings.Length > 0) return W24S4MigrationRoute.WaiverReview;
            return W24S4MigrationRoute.RebuildCandidate;
        }
        private static string Batch(W24S4MigrationRoute route)
        {
            switch (route) { case W24S4MigrationRoute.QuarantineReview: return "B0-quarantine-review"; case W24S4MigrationRoute.LegacyRetain: return "B1-legacy-preservation"; case W24S4MigrationRoute.WaiverReview: return "B2-waiver-review"; default: return "B3-rebuild-candidates"; }
        }
        private static string[] Preconditions(W24S4InventoryEntry entry)
        {
            return new[] { "Explicit user decision token matches inventory hash and batch.", "Reverify runtime entry GUID and owned-output SHA-256 immediately before apply.", "Snapshot transaction state and rollback on every failure.", "Do not rewrite historical Recipe, Manifest, Generated, or evidence bytes." };
        }

        private static W24S4CarrierReuse[] FindCarrierReuse(IEnumerable<W24S4InventoryEntry> entries)
        {
            return entries.SelectMany(entry => entry.CarrierKeys.Select(key => new { key, entry.EffectId })).GroupBy(x => x.key, StringComparer.Ordinal)
                .Where(group => group.Select(x => x.EffectId).Distinct(StringComparer.Ordinal).Count() >= 3)
                .OrderBy(group => group.Key, StringComparer.Ordinal).Select(group => new W24S4CarrierReuse { CarrierKey = group.Key, EffectIds = group.Select(x => x.EffectId).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray(), ReviewReason = "共享依赖/模板复用仅作抽样信号；需用户与视觉 QA 复核，不能据此判定视觉同质化。" }).ToArray();
        }
        private static bool IsCarrier(string path) { return !string.IsNullOrEmpty(path) && (path.IndexOf("Templates/", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("Shared/", StringComparison.OrdinalIgnoreCase) >= 0); }
        private static bool HasNamedArtifact(string root, string effectId, params string[] folders) { return folders.Any(folder => { var path = Path.Combine(root, folder.Replace('/', Path.DirectorySeparatorChar)); return Directory.Exists(path) && Directory.GetFiles(path, "*", SearchOption.AllDirectories).Any(file => Path.GetFileName(file).IndexOf(effectId, StringComparison.OrdinalIgnoreCase) >= 0); }); }
        private static bool HasEffectBoundFile(string root, string effectId, params string[] folders) { return folders.Any(folder => { var path = Path.Combine(root, folder.Replace('/', Path.DirectorySeparatorChar)); return Directory.Exists(path) && Directory.GetFiles(path, "*.json", SearchOption.AllDirectories).Any(file => FileMatchesEffect(file, effectId)); }); }
        private static bool HasRepositoryEffectBoundFile(string repositoryRoot, string effectId, string folder)
        {
            if (string.IsNullOrEmpty(repositoryRoot)) return false;
            var path = Path.GetFullPath(Path.Combine(repositoryRoot, folder.Replace('/', Path.DirectorySeparatorChar)));
            var prefix = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix + "docs" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(path)) return false;
            return Directory.GetFiles(path, "*.json", SearchOption.AllDirectories).Any(file => FileMatchesEffect(file, effectId));
        }
        private static bool FileMatchesEffect(string path, string effectId) { try { return File.ReadAllText(path).IndexOf("\"effectId\": \"" + effectId + "\"", StringComparison.Ordinal) >= 0 || Path.GetFileName(path).IndexOf(effectId, StringComparison.OrdinalIgnoreCase) >= 0; } catch { return false; } }
        private static bool ProjectFileExists(string root, string projectPath) { string absolute; return TryProjectPath(root, projectPath, out absolute) && File.Exists(absolute); }
        private static string ProjectPath(string root, string absolute) { if (string.IsNullOrEmpty(absolute)) return null; var full = Path.GetFullPath(absolute); var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar; return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? Normalize(full.Substring(prefix.Length)) : Normalize(full); }
        private static bool ProjectPathIsSafe(string root, string projectPath) { string ignored; return TryProjectPath(root, projectPath, out ignored); }
        private static bool TryProjectPath(string root, string projectPath, out string absolute) { absolute = null; if (string.IsNullOrEmpty(projectPath) || Path.IsPathRooted(projectPath) || !projectPath.StartsWith("Assets/", StringComparison.Ordinal) || projectPath.Contains("..")) return false; var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar; var candidate = Path.GetFullPath(Path.Combine(prefix, projectPath.Replace('/', Path.DirectorySeparatorChar))); if (!candidate.StartsWith(prefix + "Assets" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false; absolute = candidate; return true; }
        private static Manifest ReadManifest(string path)
        {
            try
            {
                var root = JObject.Parse(File.ReadAllText(path), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                return root.ToObject<Manifest>();
            }
            catch { return null; }
        }
        private static string ReadGuid(string metaPath) { try { var line = File.ReadAllLines(metaPath).FirstOrDefault(x => x.StartsWith("guid:", StringComparison.Ordinal)); return line == null ? null : NormalizeGuid(line.Substring(5).Trim()); } catch { return null; } }
        private static string HashFileFromProject(string root, string path) { string absolute; return TryProjectPath(root, path, out absolute) && File.Exists(absolute) ? HashFile(absolute) : null; }
        private static string HashFile(string path) { using (var sha = SHA256.Create()) return "sha256:" + BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty).ToLowerInvariant(); }
        private static string ComputeInventoryHash(W24S4AuditInventory inventory) { var text = inventory.SchemaVersion + "\n" + string.Join("\n", inventory.Entries.OrderBy(x => x.EffectId, StringComparer.Ordinal).Select(x => x.EffectId + "|" + x.RulesVersion + "|" + x.Enforcement + "|" + x.RiskScore + "|" + x.SuggestedRoute + "|" + x.IsOwnershipVerified + "|" + string.Join(",", x.CarrierKeys))); return HashText(text); }
        internal static bool VerifyCurrentAdr001Decision(W24S4MigrationPlan plan)
        {
            if (plan == null || string.IsNullOrEmpty(plan.ProjectRoot)) return false;
            try
            {
                var current = ReadAdr001Decision(plan.ProjectRoot);
                return Same(current.Status, "Accepted")
                    && !string.IsNullOrWhiteSpace(current.DecisionMaker)
                    && !Same(current.DecisionMaker, "待填写")
                    && Same(current.Status, plan.Adr001Status)
                    && Same(current.DecisionMaker, plan.Adr001DecisionMaker)
                    && Same(current.DocumentHash, plan.Adr001DocumentHash);
            }
            catch { return false; }
        }

        private static Adr001Decision ReadAdr001Decision(string projectRoot)
        {
            var result = new Adr001Decision { Status = "Missing", DecisionMaker = string.Empty, DocumentHash = null };
            if (string.IsNullOrEmpty(projectRoot)) return result;
            var repository = Directory.GetParent(Path.GetFullPath(projectRoot));
            if (repository == null) return result;
            var path = Path.Combine(repository.FullName, "docs", "rules", "ADR-001_PREFAB_COPY_AND_SHARED_DEPENDENCIES.md");
            if (!File.Exists(path)) return result;
            var lines = File.ReadAllLines(path, Encoding.UTF8);
            result.Status = ExtractAdrValue(lines.FirstOrDefault(line => line.StartsWith("状态：", StringComparison.Ordinal))) ?? "Unknown";
            result.DecisionMaker = ExtractAdrValue(lines.FirstOrDefault(line => line.StartsWith("决策人：", StringComparison.Ordinal))) ?? string.Empty;
            result.DocumentHash = HashFile(path);
            return result;
        }

        private static string ExtractAdrValue(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;
            var separator = line.IndexOf('：');
            var value = separator < 0 ? line : line.Substring(separator + 1);
            value = value.Trim();
            var annotation = value.IndexOf('（');
            if (annotation >= 0) value = value.Substring(0, annotation).Trim();
            return value.Trim('`', '*', '_', ' ');
        }

        private static W24S4MigrationOperation CloneOperation(W24S4MigrationOperation source)
        {
            if (source == null) return null;
            return new W24S4MigrationOperation
            {
                EffectId = source.EffectId,
                BatchId = source.BatchId,
                Route = source.Route,
                RuntimeEntryPath = source.RuntimeEntryPath,
                ExpectedRuntimeEntryGuid = source.ExpectedRuntimeEntryGuid,
                ExpectedRuntimeEntryHash = source.ExpectedRuntimeEntryHash,
                OwnershipVerified = source.OwnershipVerified,
                RequiresExplicitUserDecision = source.RequiresExplicitUserDecision,
                Preconditions = (source.Preconditions ?? Array.Empty<string>()).ToArray()
            };
        }

        internal static string ComputePlanHash(W24S4MigrationPlan plan)
        {
            if (plan == null) return null;
            return HashText(string.Join("\n", new[]
            {
                plan.SchemaVersion ?? string.Empty,
                plan.Mode.ToString(),
                plan.ProjectRoot ?? string.Empty,
                plan.Adr001Status ?? string.Empty,
                plan.Adr001DecisionMaker ?? string.Empty,
                plan.Adr001DocumentHash ?? string.Empty,
                plan.InventoryHash ?? string.Empty,
                string.Join("\n", (plan.Operations ?? Array.Empty<W24S4MigrationOperation>()).Select(ComputeOperationHash))
            }));
        }
        internal static string ComputeOperationHash(W24S4MigrationOperation operation)
        {
            if (operation == null) return null;
            return HashText(string.Join("|", new[]
            {
                operation.EffectId ?? string.Empty,
                operation.BatchId ?? string.Empty,
                operation.Route.ToString(),
                operation.RuntimeEntryPath ?? string.Empty,
                operation.ExpectedRuntimeEntryGuid ?? string.Empty,
                operation.ExpectedRuntimeEntryHash ?? string.Empty,
                operation.OwnershipVerified.ToString(),
                operation.RequiresExplicitUserDecision.ToString(),
                string.Join(";", operation.Preconditions ?? Array.Empty<string>())
            }));
        }
        private static string HashText(string text) { using (var sha = SHA256.Create()) return "sha256:" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", string.Empty).ToLowerInvariant(); }
        private static string CanonicalHash(string value) { if (string.IsNullOrEmpty(value)) return null; var result = value.StartsWith("sha256:", StringComparison.Ordinal) ? value : "sha256:" + value; return IsCanonicalHash(result) ? result : null; }
        private static bool IsCanonicalHash(string value) { return value != null && value.Length == 71 && value.StartsWith("sha256:", StringComparison.Ordinal) && value.Skip(7).All(x => (x >= '0' && x <= '9') || (x >= 'a' && x <= 'f')); }
        private static string NormalizeGuid(string value) { return value == null ? null : value.ToLowerInvariant(); }
        private static string Normalize(string path) { return path == null ? null : path.Replace('\\', '/'); }
        private static bool Same(string a, string b) { return string.Equals(a, b, StringComparison.Ordinal); }
        private static string YesNo(bool value) { return value ? "是" : "否"; }

        [Serializable] private sealed class Manifest { public int manifestVersion; public string effectId; public string enforcement; public string rulesVersion; public int recipeVersion; public int recipeRevision; public string recipeHash; public string buildHash; public string compilerVersion; public string unityVersion; public string sourceRecipePath; public RuntimeEntry runtimeEntry; public OwnedOutput[] ownedOutputs; public Dependency[] dependencies; public Template[] templates; public Audit[] audit; }
        [Serializable] private sealed class RuntimeEntry { public string kind; public string path; public string guid; }
        [Serializable] private sealed class OwnedOutput { public string path; public string guid; public string assetType; public string sha256; }
        [Serializable] private sealed class Dependency { public string path; }
        [Serializable] private sealed class Template { public string assetPath; }
        [Serializable] private sealed class Audit { public string code; public string message; }
        private sealed class Adr001Decision { public string Status; public string DecisionMaker; public string DocumentHash; }
    }
}
