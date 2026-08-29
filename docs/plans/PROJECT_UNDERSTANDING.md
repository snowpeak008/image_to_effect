# 项目理解（主 agent 视角，2026-08-29）

> 本文是主 agent 对项目的完整理解快照，供中断后恢复开发时快速重建上下文。与 `OPTIMIZATION_MASTER_PLAN.md`（计划）、`SESSION_LOG_2026-08-29.md`（本次对话记录）、`DEV_MEMORY.md`（开发记忆与恢复指南）配套阅读。

## 1. 项目是什么

VFX Composer：通过**文字对话生成单个 Unity 特效**、通过 **CLI/MCP 批量生成多个特效**的本地创作工具。受机器限制（Unity 同一项目只能开一个编辑器实例），批量生成必须走**单并发串行任务队列**。

远程仓库：https://github.com/snowpeak008/image_to_effect （2026-08-29 首次推送）。

## 2. 仓库真相（本次会话最重要的发现）

- 会话开始时 `master` 只有一个 baseline 提交（`038d1b0e`）；架构文档描述的全部实现（U0–U6 普通用户只读链路 + A0–A6 AI 双通道，450 个测试）都在 `codex/u*`、`codex/a*` 分支和 `D:\wt\` 下的 16 个 worktree 里，**从未合并**。任务 P0-1 已把集成分支 `codex/usermode-integration` 合入 master（merge `3375a8fe`），此谜题已解决。
- `docs/PROJECT_ARCHITECTURE_AND_DEVELOPMENT.md` 是对合并后状态的忠实描述（阅读入口）。
- `docs/coordination/` 等历史文档中的 "P1 ACTIVE"、"Phase 2 NO-GO"、"unborn master" 均为已退役的 pre-U0 特权路线历史，已全部加 superseded 标注（P0-2/2b/2c）。

## 3. 架构与代码地图（经核实）

| 层 | 位置 | 现状 |
|---|---|---|
| Desktop UI | `apps/VFXComposer.Desktop`（Avalonia，8 页面） | 项目会话 + AI Create/Preview/Settings 已实现；Create 的"对话生成特效"与 Jobs 队列是缺口（F1/F3） |
| 会话链路 | `src/VFXComposer.Client` → `services/VFXComposer.Broker` → `services/VFXComposer.UnityWorker` | 普通用户、显式选择项目、只读（LIBRARY_INDEX/MANIFEST/CONTRACT/TRACE 四类文档白名单） |
| AI 通道 | `src/VFXComposer.AI.Contracts` + `src/VFXComposer.AI.Providers` | ChatLlm 与 ImageGeneration 两条独立显式通道，零自动网络、无 fallback、DPAPI secret、OpaqueEndpoint |
| 协议 | `src/VFXComposer.Protocol` | 含 Phase 3 的 Job/Command DTO 合同（`JobStatus` 六态闭集、`CancelJobCommand` 等）——只有合同，没有执行实现 |
| 特效编译核心 | `project/Packages/com.vfxcomposer.unity/Editor/` | **真实可用**：`VfxCompiler`（v1 recipe→Prefab，写入根 `Assets/VFX/Generated`）、`Impact2DCompiler`/`Area2DCompiler`/`S12SlashCompiler`、`RecipeValidator`（L2 权威校验）；由 Unity batchmode `-executeMethod` 驱动（`tools/Invoke-Unity.ps1`，exit 73=项目锁被占） |
| 质量闸 | `eng/run-phase2-gate.ps1`（重闸）+ `eng/run-task-acceptance.ps1`（轻闸，O2 新增） | 日常任务用轻闸；里程碑收口/协议变更/发布前用重闸 |

关键代码事实（审计核实过）：
- 写入面封闭三成员：`Assets/VFX/Generated/**` + `ProjectSettings/VFXComposer/BuildManifests/<id>.manifest.json` + `Assets/VFX/Recipes/<id>.json` 构建溯源单文件（ADR-007 v1.2）。
- `Assets/VFX/Shared` 构建期只读；`Impact2D/Area2D` 的 `SharedLibrary.Ensure()` 是**强制覆盖收敛**而非幂等补齐，故首版 AI 构建范围只限 v1 `VfxCompiler` 域。
- 构建约定：.NET SDK 8.0.420 锁定、C# 12、TreatWarningsAsErrors、NuGet 仅本地批准 feed（`.codex_tmp/w24-phase1-approved-feed`，39 包，**未跟踪**——新 worktree 必须从主工作区复制）、Windows PowerShell 5.1（不支持 `&&`）。

## 4. 产品缺口与路线（详见主计划）

- **F1 对话生成 Recipe**（开发中）：prompt 模板 + 模板目录快照 + 严格解析 + 手写 L1 校验 + 1+N 重试预算，产物是待确认草稿，不写项目。
- **F2 受限构建**：batchmode 短生命进程执行 v1 编译，写入面按 ADR-007；前置决策：生产闸走 `BuildProduction` 还是新增等价受限入口。
- **F3 Jobs 串行队列**：单并发、持久化、崩溃恢复（`DISCONNECTED` 定局不自动重跑）；前置决策：执行器宿主形态。
- **F4 CLI / F5 MCP**：共用同一执行层；MCP 仅 stdio；前置决策：MCP SDK feed 准入 vs 手写 JSON-RPC。
- **F6 端到端验收**。

需求定版文件：`docs/requirements/REQ-001_CHAT_TO_VFX.md`（23 条需求）、`REQ-002_BATCH_CLI_MCP.md`（21 条）、`REQ-003_JOBS_QUEUE.md`（18 条）；安全裁决：`docs/rules/ADR-007_CONTROLLED_PROJECT_MUTATION.md`。全部经独立审计 PASS。

## 5. 治理模型（本次会话建立）

主 agent 只管理不开发：派发开发子 agent（带精确 allow-list）→ 交付报告 → 主 agent 初审或独立审计子 agent 只读验收（可并行）→ 一次返工回合 → PASS 后主 agent 提交/合并/推送。代码任务在 `D:\wt\` 独立 worktree（分支 `task/<ID>-<slug>`）进行；文档任务可在主工作区新增文件。验收基线：Release 0 warning/0 error + 相关测试全绿 + allow-list 合规 + `CODING_STANDARDS.md`。
