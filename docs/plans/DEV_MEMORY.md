# 开发记忆与恢复指南

> 暂停时间：2026-08-29 18:20 前后。恢复开发时按 §3 步骤执行即可无缝续接。
> 文件夹导航：`OPTIMIZATION_MASTER_PLAN.md`（总计划+任务卡+状态板）｜`CODING_STANDARDS.md`（验收标准）｜`PROJECT_UNDERSTANDING.md`（项目理解）｜`SESSION_LOG_2026-08-29.md`（本次对话记录）｜`WORKTREE_RETIREMENT.md`（worktree 退役清单）｜`BASELINE_REPORT.md`（O3 交付后出现）。

## 1. 暂停时刻的精确状态

**master = `fa069166`，已推送 GitHub（snowpeak008/image_to_effect），工作区除下述待提交项外干净。**

已关闭：P0-1、P0-2（含 2b）、O1、O2、R1（v0.3 微调在途）、R2、R3、R4（v1.1 微调在途）。

### 在途任务（暂停时仍在后台运行的子 agent）

| 任务 | 内容 | 完成后处置 |
|---|---|---|
| ~~ADR-007 v1.1 微调~~ | 已完成并转 ACCEPTED，已合入 | 完成 |
| ~~REQ-001 v0.3~~ | 已完成，已合入 | 完成 |
| O3 基线（worktree `D:\wt\i2s-o3`，分支 `task/O3-baseline`） | 修 HandleProbe/Broker.Tests 锁文件漂移 + 全量构建/测试基线报告 | 验收后合并入 master，然后可停用轻闸的 `-SkipLockedRestore` |
| F1 开发（worktree `D:\wt\i2s-f1`，分支 `task/F1-recipe-generation`） | Recipe 结构化生成通道（模板快照/生成服务/契约/Create 页/测试） | 交付后派独立审计（代码任务必须审计），PASS 后合并 |

### 工作区待提交文件

无——小任务批（ADR-007 v1.1 / REQ-001 v0.3 / rules README / CODING_STANDARDS §3.2 更新）已全部合入推送；剩余在途仅 O3 与 F1 两个 worktree 任务，交付物在各自分支上，等用户回来后验收合并。

## 2. 待主 agent / 用户拍板的决策（按优先级）

1. **F3 执行器宿主形态**（派发 F3 前定版）：倾向方案——执行层做成库 + 跨进程单写者 durable lock（复用 `ProviderConfigurationRevisionLock` 模式），Desktop/CLI/MCP 三入口各自在进程内宿主执行器，锁保证全局单并发；避免常驻服务，符合 fail-closed 与"无新网络面"。备选：独立宿主进程（利于 `--detach`，但引入进程管理复杂度）。
2. **F2 生产闸**（派发 F2 前定版）：走既有 `BuildProduction`（含 `W24S5ProductionGateRequest` contract-first 准入）还是新增等价受限入口。
3. **F5 MCP 实现底座**：官方 MCP C# SDK 不在批准 feed → 要么走 feed 准入流程，要么手写 stdio JSON-RPC（REQ-002 §7 工具面是闭集，手写量可控，倾向手写）。
4. **batchmode 冷启动延迟**（用户拍板）：分钟级冷启动若不可接受，需另立 ADR 引入常驻可写 Unity 会话；当前按短生命进程走。
5. **旧分支处置**：`codex/m1-protocol-unity`（worktree 有未提交 Unity Worker 生产连接器 WIP）、`codex/m2-production-read`（约 1900 行生产模式 Broker 监督代码）——倾向归档保留不收编；14 个已并入 worktree/分支等 O2/O3/F1 的 worktree 用完后按 `WORKTREE_RETIREMENT.md` 统一退役。

## 3. 恢复开发步骤

1. 查看在途四个子 agent 的完成通知/交付报告（若会话已断，直接看各 worktree 分支的提交与工作区 diff：`git -C D:\wt\i2s-o3 log --oneline master..HEAD`，`git -C D:\wt\i2s-f1 log --oneline master..HEAD`，主工作区 `git status`）。
2. 验收并提交小任务批：ADR-007 v1.1（确认状态 ACCEPTED）+ REQ-001 v0.3 + `docs/rules/README.md`，一个 docs 提交推送。随后把 `CODING_STANDARDS.md` §3.2 的"裁决前只读"措辞更新为引用 ADR-007 定版规则（主 agent 自己改，属管理文件）。
3. 验收 O3：核对锁文件 diff 与基线数字 → 合并 `task/O3-baseline` → 验证 `dotnet restore -p:RestoreLockedMode=true` 全绿 → 推送 → 通告轻闸停用 `-SkipLockedRestore`。
4. 验收 F1：先跑轻闸（`eng/run-task-acceptance.ps1`，此时应可用锁定模式），再派**独立审计子 agent**（代码任务必须独立审计：allow-list 合规、测试质量、redaction、零自动网络）→ 一次返工回合 → 合并推送。
5. 按 §2 拍板决策 1，派发 F3（依赖 R3✓、P0-1✓）；F1 合并后派发 F2（依赖 F1✓、R4✓，注意 F2 任务卡里的生产闸决策与 Windows 保留名负向测试要求）。
6. 之后按主计划 DAG：F4（依赖 F1/F3/R2）→ F5 → F6。每步遵循 §5 治理模型（见 `PROJECT_UNDERSTANDING.md`）。

## 4. 派发子 agent 的模板要点（沿用本次会话有效实践）

- 任务书必含：先读主计划任务卡 + CODING_STANDARDS；精确 allow-list；worktree 指令（代码任务）+ **复制批准 feed**（`.codex_tmp/w24-phase1-approved-feed` 未跟踪，新 worktree 没有它 restore 必失败）；PowerShell 5.1 语法警告；禁止 push/合并/越界；交付报告格式（改动清单/命令与数字/已知限制）。
- 文档任务在主工作区只新增文件、禁 git 写操作，由主 agent 验收后提交。
- 交付后：主 agent 初审（小任务）或派只读独立审计（PRD/ADR/代码），建议级问题 resume 原作者一轮微调，微调后由主 agent 提交推送并更新状态板。
- 已知踩坑：并行任务的工作区 diff 会互相混入 `git status`，验收时只 add 本任务 allow-list 文件；构建副作用会改写漂移的锁文件（O3 修复合并前），非本任务文件用 `git checkout` 还原。
