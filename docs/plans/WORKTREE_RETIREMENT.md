# Worktree 退役建议清单

> 任务：O1（工作区与残留清理）　|　盘点日期：2026-08-29　|　性质：仅建议，不执行任何删除
>
> 盘点基准：master @ `e606b570`（docs(plan): add optimization master plan and coding standards）。
> 证据命令：`git worktree list`、`git branch -vv`、`git branch --merged master`、`git log master..<branch> --oneline`、`git -C <worktree> status --porcelain`、`git cherry master <branch>`、`git diff master...<branch> --stat`。

## 1. 结论摘要

- `D:\wt\` 下共 17 个本仓库 worktree + 1 个无关目录（`tom_doc_review_dd0f9ff`，独立仓库克隆）。
- **14 个 worktree 可退役**：分支已完全并入 master 且工作区干净。
- **3 个 worktree 需保留**：`i2s-o2`、`i2s-o3`（在途任务 O2/O3），`i2s-m1`（分支未并入且有未提交改动）。
- 本地分支 16 个（不含 master）：**14 个已并入 master（可删除）**，2 个未并入（`codex/m1-protocol-unity`、`codex/m2-production-read`），保留待主 agent 决断。

## 2. Worktree 逐项建议

### 2.1 可退役（分支已完全并入 master，工作区干净）— 14 个

以下 worktree 检出的分支均出现在 `git branch --merged master` 输出中（即分支 tip 是 master 的祖先），且 `git status --porcelain` 为空：

| Worktree | 检出分支 | 建议 |
|---|---|---|
| `D:\wt\i2s-a1` | `codex/a1-ai-provider-core` | 可退役删除 |
| `D:\wt\i2s-a2` | `codex/a2-chat-channel` | 可退役删除 |
| `D:\wt\i2s-a3` | `codex/a3-image-channel` | 可退役删除 |
| `D:\wt\i2s-a4` | `codex/a4-desktop-ai` | 可退役删除 |
| `D:\wt\i2s-a5` | `codex/a5-ai-local-e2e` | 可退役删除 |
| `D:\wt\i2s-ai-plan` | `codex/ai-provider-future-plan` | 可退役删除 |
| `D:\wt\i2s-architecture-doc` | `codex/project-architecture-overview` | 可退役删除 |
| `D:\wt\i2s-integration` | `codex/usermode-integration` | 可退役删除（P0-1 已合并该集成分支，merge `3375a8fe`） |
| `D:\wt\i2s-m2` | `codex/u0-user-mode-architecture` | 可退役删除（注意：目录名叫 m2，检出的却是已并入的 u0 分支） |
| `D:\wt\i2s-u1` | `codex/u1-unity-worker-connector` | 可退役删除 |
| `D:\wt\i2s-u2` | `codex/u2-user-mode-session` | 可退役删除 |
| `D:\wt\i2s-u3` | `codex/u3-project-selection-read` | 可退役删除 |
| `D:\wt\i2s-u4` | `codex/u4-desktop-integration` | 可退役删除 |
| `D:\wt\i2s-u5` | `codex/u5-local-e2e` | 可退役删除 |

### 2.2 需保留 — 3 个

| Worktree | 检出分支 | 原因 |
|---|---|---|
| `D:\wt\i2s-o2` | `task/O2-acceptance-script` | O1 盘点期间由主 agent 新开的在途任务 worktree（O2 已派发） |
| `D:\wt\i2s-o3` | `task/O3-baseline` | 在途任务 worktree（O3 已派发）；其中 3 个 `packages.lock.json` 修改是 O3 任务卡内的锁文件漂移修复，属预期 |
| `D:\wt\i2s-m1` | `codex/m1-protocol-unity` | 分支未并入（见 3.2），且工作区有 **7 项未提交改动**（`project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/` 下的 Production 连接器代码与协议 codec 修改、EditMode 测试），删除 worktree 将丢失这些 WIP |

### 2.3 范围外 — 1 个

- `D:\wt\tom_doc_review_dd0f9ff`：独立仓库克隆（remote 为 `https://github.com/snowpeak008/Tom_doc.git`，detached HEAD @ `dd0f9ff`），与 image_to_smart 无任何 git 关联。不在本次退役范围，是否保留由用户自行决定。

## 3. 本地分支逐项建议

### 3.1 已完全并入 master（可删除）— 14 个

`git branch --merged master`（2026-08-29 执行）列出以下分支，其 tip 均为 master 祖先，删除不丢失任何提交：

`codex/a1-ai-provider-core`、`codex/a2-chat-channel`、`codex/a3-image-channel`、`codex/a4-desktop-ai`、`codex/a5-ai-local-e2e`、`codex/ai-provider-future-plan`、`codex/project-architecture-overview`、`codex/u0-user-mode-architecture`、`codex/u1-unity-worker-connector`、`codex/u2-user-mode-session`、`codex/u3-project-selection-read`、`codex/u4-desktop-integration`、`codex/u5-local-e2e`、`codex/usermode-integration`。

注意：其中多数分支仍被 2.1 节的 worktree 检出（`git branch -vv` 中带 `+` 标记），须先 `git worktree remove` 对应 worktree 才能删分支。

### 3.2 未并入 master（保留，待主 agent 决断）— 2 个

**`codex/m1-protocol-unity`**（worktree：`D:\wt\i2s-m1`）

- `git log master..codex/m1-protocol-unity --oneline` → 领先 1 个提交：`4bb8db2c docs: rebase Unity locator projection DAG`（2026-08-28）。
- `git cherry master` 确认该提交无 patch 等价物在 master 上。
- `git diff master...HEAD --stat`：仅改 `docs/coordination/` 下 6 份文档（+227/−116）。
- 另有 worktree 内 7 项未提交改动（Unity Worker Production 连接器代码，见 2.2）。
- 建议：**不确定**。已提交部分只动 `docs/coordination/`，很可能被 P0-2 文档对齐任务覆盖/取代；但未提交的 Unity 代码是否还有价值需主 agent 判断。在决断前保留分支与 worktree。

**`codex/m2-production-read`**（无 worktree）

- `git log master..codex/m2-production-read --oneline` → 领先 1 个提交：`fa8843be M2-wip: close ownership and worker supervision`（2026-08-28）。
- `git cherry master` 确认无 patch 等价物在 master 上。
- `git diff master...HEAD --stat`：+1880/−205，新增 `services/VFXComposer.Broker/Ipc/WorkerProcessSupervisor.cs`、`Ipc/WorkerGlobalOwnershipRegistry.cs`、`Native/WindowsJobObject.cs` 等生产模式（production-mode）Broker 监督代码及测试；经核实这些文件在 master 上均不存在。
- 建议：**未并入需保留**（分支删除会丢失约 1900 行实现）。但架构上 master 已走 user-mode 路线（u0 提交即"simplify phase2 to user-mode architecture"），该 WIP 可能已被路线取代——是否收编或归档由主 agent 决断。

## 4. 执行提示（供主 agent 参考，本任务不执行）

1. 退役顺序：先 `git worktree remove <path>`（干净工作区可直接删），再 `git branch -d <branch>`（已并入分支用 `-d` 即可，git 会自动拒绝未并入的误删）。
2. 本清单为 2026-08-29 快照；O2/O3 等在途任务的 worktree 状态会变化，执行退役前应重跑 `git worktree list` 与 `git branch --merged master` 复核。
