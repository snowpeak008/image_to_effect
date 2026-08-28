# W24 S4 报告：既有资产只读审计与迁移框架

状态：**真实文件系统只读盘点与隔离 Unity EditMode 验证均已完成**；`W24S4MigrationAuditTests` 通过 `12/12`。未启动、终止或干扰用户的 canonical Unity GUI；未迁移、重建、移动、删除或改写任何既有 Unity 资产。

## 已实现

- `project/Packages/com.vfxcomposer.unity/Editor/W24/S4/W24S4MigrationAudit.cs`
  - 以 Generated 目录和权威 Manifest 并集全量枚举正式条目；
  - 校验 Runtime Entry 项目内路径、`.meta` GUID、Recipe 路径，以及 **全部** `ownedOutputs[]` 的安全路径、类型、GUID 与 SHA-256；strict 条目的拥有物还必须位于自己的 `Assets/VFX/Generated/<effectId>/`；
  - 只读消费已有 W24 合同/Trace、Preview、证据线索；
  - 输出确定性 inventory hash、风险原因、共享载体抽样和四类建议路由/批次；
  - 输出默认 `DryRun` 的迁移计划；没有用户 token、所有权核验和可回滚事务时拒绝 Apply；token 同时绑定 inventory hash、plan hash、batch、effectId 和完整 operation hash，批准集合以只读视图暴露，不能在改写计划后复用；
  - `Apply` 不能由调用者手填布尔值开启：计划会读取并绑定仓库 `ADR-001` 的原文字节 hash、状态和具名决策人；只有权威文件为 `Accepted` 时才能从完整 DryRun 派生新的 Apply plan，ADR 字节随后变化会立即使 token 失效；
  - 从不产出 L0–L4 或视觉 pass，所有结果保持 `VISUAL_PENDING`。
- `project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S4MigrationAuditTests.cs`
  - 覆盖确定性/只读与不自动分级；
  - 覆盖 owned-output 篡改进入 quarantine；
  - 覆盖 legacy 保留而非批准；
  - 覆盖默认 dry-run、ADR Proposed/字节变化拒绝、错误 token 拒绝、篡改计划/跨条目 token 拒绝、正确 token+所有权的事务提交、异常 rollback，以及主事务和 rollback 双失败均保留诊断。
- `docs/vfx-migration/W24_S4_READONLY_AUDIT_AND_DRY_RUN.md`
  - 记录输入、风险路由、事务约束及待用户填写的批次裁决表。
- `tools/vfx/w24_s4_readonly_audit.py`
  - 不依赖 Unity/AssetDatabase 的只读镜像审计器；逐份核验 Manifest、运行入口、`.meta` GUID、全部 owned output 的 SHA-256、Recipe 路径、现有 W24 合同/Trace、Preview/证据线索及共享载体引用；仅允许显式、write-once 地向 `docs/` 写出审计记录。
- `tools/vfx/tests/test_w24_s4_readonly_audit.py`
  - 覆盖确定性、`VISUAL_PENDING`/M3 冻结、manifest-only quarantine、legacy retain 与 write-once 输出拒绝。

## 当前真实只读盘点（2026-08-25）

当前工作树字节的最新只读 inventory hash 为
`sha256:93b3e9406e0095b70ef911fed259f7c02e977a0b5232a83f61aa2311c8dab233`。它不使用先前的估计数字，
不把合同/Trace/Preview/证据缺失伪造成失败，也不赋予 L 级。

当前权威 write-once 快照为：

- [`W24_S4_READONLY_INVENTORY_20260825T144334Z.json`](../vfx-migration/W24_S4_READONLY_INVENTORY_20260825T144334Z.json)，
  文件 `sha256:f235f9325b23fb26808d3f3106dcbcf1b9bdae06d9934f15174704b3833807bf`；
- [`W24_S4_READONLY_INVENTORY_20260825T144334Z.md`](../vfx-migration/W24_S4_READONLY_INVENTORY_20260825T144334Z.md)，
  文件 `sha256:ac8ccfcb503f04168d134b350bc070ee42d18502a88b505c3b53f8fe5058ddf7`。

此前 write-once 的 221 条目快照继续作为历史证据保留，未被当前结果覆盖：

- [`W24_S4_READONLY_INVENTORY_20260825.json`](../vfx-migration/W24_S4_READONLY_INVENTORY_20260825.json)
- [`W24_S4_READONLY_INVENTORY_20260825.md`](../vfx-migration/W24_S4_READONLY_INVENTORY_20260825.md)

该历史快照记录 221 条目，inventory hash 为
`sha256:05f35702a4073ed6a6dc8e0b6f8439c1e4b10194ddfe77af8ae742f04340b903`；它不再代表当前 220 份
Manifest 的工作树状态，但其原始字节和当时结论仍然有效。

| 指标 | 数量 | 结论 |
|---|---:|---|
| Generated Effect 目录 | 208 | 已纳入扫描入口 |
| 可解析权威 BuildManifest | 220 | 与 Generated 目录取并集，不静默忽略 manifest-only 条目 |
| 正式条目并集 | 220 | 全量逐项只读核验完成 |
| `QuarantineReview` | 12 | Manifest-only；缺 Recipe/Runtime Entry，不能迁移 |
| `LegacyRetain` | 3 | 所有权可核验且为 `legacy_audit`；仅保留供后续用户审查 |
| `RebuildCandidate` | 205 | 所有权可核验；只是未来合同化/重建候选，不是批准重建 |
| 缺 W24 Contract / Trace | 220 / 220 | 现有的四份 W24 基线合同/Trace 尚未对应到旧 Manifest EffectId；审计不补造 |
| 缺正式四路证据线索 | 220 | 不是视觉失败；全部保持 `VISUAL_PENDING` |
| 缺 Preview 命名线索 | 219 | 只是受限目录中的名称线索，不等于相机或视觉通过 |
| ADR-001 / M3 | `Proposed` / frozen | 没有合法 Apply 路径 |

当前扫描也解释了 `legacy_audit` 的数字差异：当前 Manifest 共 15 份为 `legacy_audit`，其中 12 份正是 manifest-only，故先按所有权失败路由到 B0；仅其余 3 份进入 B1。该差异是路由优先级（所有权失败优先于 legacy 保留），不是降低或删除历史资产。

12 个待 B0 复核的 Manifest-only EffectId 为：`fireball_2d_s8test`、`fireball_3d_s10test`、`i1_river_comet`、`i2_glass_spark`、`i3_brazier_bead`、`i4_rail_flare`、`s11_a5`、`s11_a6`、`s9_canonical_patch_export_base`、`s9_cohort_k_final_k1`、`s9_cohort_k_final_k2`、`s9_cohort_k_final_k3`。它们暂列 `QuarantineReview`，不是删除建议。

## 结论与未执行项

1. 现有资产没有被修改、移动、删除、重建或标成 L 级；历史 bytes 保持原样。
2. S4 的风险分与批次仅供用户抽样复核；不是“保留/重做/废弃”裁决。
3. 已运行 Python 只读审计器，其 targeted tests 为 `3/3 passed`；随后在隔离 shadow Unity 中串行执行
   `W24S4MigrationAuditTests`。当前权威 r8 结果为 `12/12 passed / 0 failed / 0 skipped /
   0 inconclusive`，进程正常以代码 `0` 退出。首要耐久索引为
   `D:/WorkWork/Assist/image_to_smart/artifacts/vfx-evidence/w24-stage-focused/run-20260825T143716Z/receipt.json`
   (`sha256:de258073b98e2cec9642eb4400543cee0f16231463a8a4a737c7424ac5c74fcc`)；其 `9 gates /
   21 files` hash/count 复验为 `0` issues。字节绑定的完整 XML 为
   `D:/WorkWork/Assist/image_to_smart/artifacts/vfx-evidence/w24-stage-focused/run-20260825T143716Z/s4-migration-audit.xml`
   (`sha256:e91518ed8130022970b0053d66cb13c0f226e58103edfe79e28c5123ee317e7a`)。r1 XML
   `D:/WorkWork/Assist/image_to_smart/.codex_tmp/w24-stage-regression-results/r1/s4-migration-audit.xml`
   (`sha256:87611b8d0a73e88e4e4057c47562e4a7e7577fe8c5a2b8659026d775e5de0801`) 虽记录
   12/12 assertions，但当次进程超时，故是被 r8 正常退出结果取代的负向 harness 证据，不能单独算作进程门通过。两次运行均不接触 canonical Unity GUI，也没有执行 Apply 或写入迁移资产。
4. ADR-001 仍为 `Proposed`；共享资产/Prefab 结构迁移（M3）继续冻结。当前代码会独立重读固定 ADR 路径、核验状态/具名决策人/文档 SHA-256，不接受请求字段或 UI 布尔值冒充批准。
5. Token 构造和签发边界现为 Editor 程序集内部 API；未来 S6 UI 只能针对当前 plan 的明确条目签发。外部 Runtime、CLI 或 MCP 不能自行构造迁移批准。
