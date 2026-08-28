# W24 S4 既有资产审计与迁移 Dry-Run 框架

## 目的与边界

`W24S4MigrationAudit` 只枚举正式 Runtime Entry、Generated 目录、权威 BuildManifest、Recipe、可选 W24 合同/Implementation Trace、Preview 和证据线索。它是文件系统只读盘点，不读取画面，不修改 Unity 资产，也不对审美、视觉通过或 L0–L4 作出裁决。

每个结果固定为 `VISUAL_PENDING`。风险分只用于排序与用户抽样建议；它不是质量分、视觉 QA 结果或批准。

## 输入与确定性

- 正式条目集合：`Assets/VFX/Generated/*` 与 `ProjectSettings/VFXComposer/BuildManifests/*.manifest.json` 的 EffectId 并集。
- Runtime Entry：从 Manifest 的 `runtimeEntry` 读取；审计逐项核验全部 `ownedOutputs[]` 的项目内路径、assetType、`.meta` GUID 和 SHA-256，而不是只核验入口 Prefab。`strict` 条目的拥有物还必须留在自身 `Assets/VFX/Generated/<effectId>/` 下。
- 合同/Trace：仅在 `Assets/VFX/Contracts`、`ProjectSettings/VFXComposer/W24/Contracts`、`Assets/VFX/Traces`、`ProjectSettings/VFXComposer/W24/Traces` 已存在时消费；审计绝不生成、补全或修改它们。
- Preview/Evidence：只检查受限目录中 effectId 命名线索，缺失只能成为审计风险，不能被解释为视觉失败。
- 清单、共享载体组和 dry-run 操作均按 ordinal EffectId 排序并绑定 SHA-256 inventory hash；同一文件快照得到相同输出。

## 风险与路由

风险分由 Manifest/Runtime 所有权核验、合同/Trace、Preview/证据线索、legacy 标记和已有机器审计 warning 构成。它只用于把人工精力放在高风险项。

| 建议路由 | 条件 | 含义 |
|---|---|---|
| `QuarantineReview` | Manifest、Runtime Entry、GUID 或 owned-output hash 无法核验 | 隔离复核；不移动、不删除、不重写 |
| `LegacyRetain` | 已核验但 `enforcement=legacy_audit` | 保留历史资产和字节；仅准备用户审查材料 |
| `WaiverReview` | 严格条目有已有结构/规则 warning | 收集 waiver/设计决定，不自动放宽规则 |
| `RebuildCandidate` | 其余可核验条目 | 仅列为未来合同化/重建候选；不是批准重建 |

共享 Template/Shared dependency 被三个及以上条目引用时会出现在载体复用抽样表。该表只提示“需要看样本”，不能证明同质化或视觉不合格。

## 迁移权限与事务

`CreateDryRunPlan` 永远生成 `DryRun`。框架本身没有文件写入器，不能改写历史 Recipe、Manifest、Generated、Preview 或 evidence bytes。

任何未来集成要执行单项操作，都必须同时满足：

1. 由固定仓库路径读取 `ADR-001`；只有状态为 `Accepted`、决策人已具名且文档 SHA-256 与 DryRun 完全一致时，内部边界才能从 DryRun 派生新的 `Apply` plan。调用者不能手填批准布尔值，ADR 后续改字节会使既有 token 失效；
2. 由 Editor 内部用户决策边界签发 `W24S4UserDecisionToken`，并同时精确绑定 inventory hash、plan hash、batchId、effectId 与完整 operation hash；
3. Apply 前重新核验 Runtime Entry GUID 与全部 owned-output SHA-256；
4. 由实现 `IW24S4MigrationTransaction` 的适配器提供 `Begin → Apply → Commit`；任何异常必调 `Rollback`；
5. 仍遵守 ADR-001 为 `Proposed` 时冻结 M3 的限制；当前仓库因此不存在合法 Apply plan。

token 缺失、批次/hash 不匹配、条目未列入批准集合、计划或操作被改写、所有权不成立、默认 dry-run 或事务失败，均不得迁移。事务与 rollback 若同时失败，两个异常都会被保留供人工恢复，不允许 rollback 掩盖首错。框架不会自动授予 L 级，也不会将 `VISUAL_PENDING` 改为 pass。

## 人工决策表（待用户）

| Batch | 用户决策 token | 保留 | 重做 | 废弃 | 延后 | 备注 |
|---|---|---:|---:|---:|---:|---|
| B0-quarantine-review | 待用户 |  |  |  |  |  |
| B1-legacy-preservation | 待用户 |  |  |  |  |  |
| B2-waiver-review | 待用户 |  |  |  |  |  |
| B3-rebuild-candidates | 待用户 |  |  |  |  |  |

只有用户填写明确决定并产生 token 后，才可由独立、已批准的事务执行器处理对应批次；S4 本身不执行迁移裁决。
