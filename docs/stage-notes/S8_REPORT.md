# S8 阶段纪要：Patch 与局部重建

> 状态：**主 Agent 独立验收通过**  
> 执行日期：2026-08-22  
> 范围：S8 Patch、revision、历史、影响报告、最小 Editor UI 与回归；未进入 S9。

## Patch 契约

- 顶层严格为 S2 实测的裸 JSON 操作数组；不接受 `operations` 包装。
- allow-list：`replace`、`add`、`remove`、`enable`、`disable`。每项只接受所需字段：`replace`/`add` 必须含 `value`；`remove`/`enable`/`disable` 禁止 `value`；任何未知字段拒绝。
- 只允许稳定 ID 路径，绝不解析 RFC 6902 数组下标、`..`、`~` 转义或任意 JSON Pointer：
  - replace：`/stages/{stageId}/modules/{moduleId}/parameters/{param}`
  - add/remove/module enable/disable：`/stages/{stageId}/modules/{moduleId}`
  - stage enable/disable：`/stages/{stageId}`
- `add` 只可添加完整模块对象，值内 `id` 必须匹配路径 ID；全量 Recipe/Manifest/Catalog 验证会拒绝未知 template、kind 不匹配、未知字段和缺失/越界参数。
- 最终全量验证错误会以错误的稳定 path 反向归因到最后触及该 path 的唯一 operation（包括 add 模块根下的深层错误）；多条错误不能唯一归因或全局预算等无关联路径时，`FailedOperationIndex` 保持 null，并明确标为 `post-patch validation (unattributed)`，绝不猜测最后一个 op。
- v1 required 规则已定：`travel/core` 且 `kind: energy_body` 不可 remove；默认 `travel/embers` 可 remove。没有资产、任意文件或任意属性路径 Patch。
- `expectedRevision` 由 API/UI 独立传入，不放进 Patch 数组。revision 不匹配为 `E707 /revision`，零写入；成功只递增一次。旧 Patch 重放因此被拒绝。

## 事务与构建边界

`VfxPatchService` 先在深拷贝 JSON 上顺序应用全部操作，再跑 Parser、`RecipeValidator`、Budget 和 Compiler Dry Run/binding allow-list。任何失败包含稳定路径和失败 op index，尚未写 Recipe/history/Generated/Templates。

全量验证和既有 history 格式通过后，先在系统临时目录对该 Recipe 的**精确** Generated 输出目录作字节快照（含目录是否存在、目录 `.meta`、全部递归文件和 `.meta`）。随后才调用 S6 事务性 Build。Build、Build 后 hook、Recipe/history 原子写入、import 任一步失败，都会恢复 Recipe/history 原始文本并将 Generated 恢复为快照；首次原本不存在输出时会删除新建的精确目录及 `.meta`。不以“重新 build 旧 Recipe”替代恢复。成功才删除系统临时备份；不在 Assets 留 `.pending`/backup/temp。快照创建失败被报告为 `E710 /transaction/snapshot`（不启动 Build）；文本、Generated 和备份清理的回滚失败均明确报告为 `E711` 及精确 rollback path，提示可能需要人工恢复，绝不静默吞掉异常。

首版明确采用 **Compiler 全量重建 fallback**：不宣称资产级局部写盘。影响报告逐个输出所有 stable `stage/module` 为 create/update/remove/unchanged，并额外输出 stage-level 状态（例如 stage enable/disable 会标记该 stage update，供 Controller flag/build 变化可见）；rate `18 → 9` 时只有 `travel/embers` 为 module update，其余模块 unchanged。

## UI 与样例

- `Tools/VFX Composer/Compiler` 新增 Patch TextAsset、Expected Revision、Validate Patch、Apply Patch。Patch 输入改变或 Validate 有 error 时 Apply 按钮禁用；报告显示 revision、错误、失败 op index、全部影响项和 full rebuild fallback 提示。
- 正式样例：[fireball-2d.embers-half.patch.json](../../project/Assets/VFX/Recipes/Patches/fireball-2d.embers-half.patch.json) 只提供 `rate: 9` Patch，没有修改默认 Recipe（仍为 revision 1/rate 18）。

## 自动化证据

新增 `VfxPatchTests` 覆盖五种 operation、空/裸数组、未知 op/字段、索引/转义/`..`/每种 op 的 trailing path、缺目标、类型/范围、required remove、unknown template/kind、revision conflict、重放、revision +1、history 与失败 hash 保持。额外覆盖多 op 最终 `rate` 越界准确归到 op 0、坏 add 的深层 template 错误归到 add、全局 budget 错误显式不归因。内部 hook 分别注入 S6 Build commit failure、Build 成功后 Recipe 写入与 history 写入之间的失败（已有输出和首次无输出）；均断言 Recipe/history/Generated hash、Generated 直属目录集合和既有 Prefab GUID 完整恢复，无 pending/backup/temp。另以可注入 snapshot provider 覆盖 capture/restore 故障的 `E710`/`E711` 可追溯行为。

A6 真实模板集成路径使用独立 `fireball_2d_s8test` Recipe/output：先 Build 默认值，再以 expected revision 1 应用 rate `18 → 9`。断言 Generated Prefab `Travel/Core/Embers` 的 ParticleSystem rate 为 9，影响项只有 embers 为 update，结构快照在将 `rate=9` 归一回 `rate=18` 后完全相同，所有 Templates 文件 SHA-256 不变。失败 Patch 后 Recipe（含 history）、Generated 和 Templates 的文件 hash 保持完全一致。测试 teardown 仅清理该测试 Recipe/output/history，固定 S7 Preview Prefab/Scene 不在清理范围。

| 命令 | 实测结果 |
|---|---|
| `cmd /c tools\\compile-check.bat` | 退出码 0 |
| `cmd /c tools\\run-tests.bat EditMode` | 47 total / 0 failed |
| `cmd /c tools\\run-tests.bat PlayMode` | 4 total / 0 failed |

S6/S7 的 EditMode 与 PlayMode 回归均包含在上述全量测试中。S8 未修改 Runtime 程序集、固定 Preview Scene 或其保留 Generated Prefab GUID。

## 主 Agent 独立验收

主 Agent 没有直接采信开发侧自测。首轮审查退回了 Generated 跨文件事务、严格路径长度、空 Patch 与 stage-level 影响报告问题；第二轮又退回了最终验证错误误归因和回滚异常被静默吞掉的问题。整改后逐项复核实现、测试与本纪要，并在无 Unity 进程的环境中独立执行全部门禁：

| 独立门禁 | 结果 |
|---|---|
| `cmd /c tools\\compile-check.bat` | 退出码 0 |
| `cmd /c tools\\run-tests.bat EditMode` | 47 total / 0 failed |
| `cmd /c tools\\run-tests.bat PlayMode` | 4 total / 0 failed |

验收后确认：没有 S8 测试 Recipe、history、`.pending`、backup 或临时 Generated 目录残留；正式 Generated 只保留 S7 固定预览使用的 `fireball_2d`，其 Prefab GUID 仍为 `edfdb8327c7bd234c94f0f4338c35816` 且场景引用一致。首版采用“全量重建 + 精确影响报告”的计划内降级，不把它表述成资产级局部写盘。由此判定 A6 与 S8 退出条件通过，可以进入 S9。
