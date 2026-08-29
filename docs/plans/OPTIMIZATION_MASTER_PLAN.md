# 开发执行原子计划（主计划）

> 状态：ACTIVE　|　创建：2026-08-29　|　管理者：主 agent（仅派发/调度/验收，不执行开发）
>
> 本计划是当前优化与功能开发的唯一顺序依据。每个原子任务由子 agent 独立完成，允许并行开发与并行验收，但必须遵守各任务卡的文件 allow-list 与依赖关系。

## 1. 背景与目标

仓库现状（2026-08-29 核实）：

- `master` 仅有 phase2 baseline 提交；U0–U6（普通用户 Broker/Worker 只读链路）与 A0–A6（AI 双通道）的全部实现在 `codex/usermode-integration` 分支及 `D:\wt\` 下的 worktree 中，未合并回 master。
- 产品目标功能三大缺口：①对话生成单个特效的端到端链路（AI 输出 → Recipe → 校验 → 构建 Prefab）；②MCP/CLI 批量生成入口；③受机器配置限制的串行 Jobs 队列。
- 特效编译核心（`VfxCompiler`、`Impact2DCompiler`、`Area2DCompiler`、`S12SlashCompiler` 等）已在 Unity 包 `project/Packages/com.vfxcomposer.unity` 内实现并有测试，产物写入根为 `Assets/VFX/Generated`（另有共享资产目录 `Assets/VFX/Shared`，写入政策由 ADR-007 裁决）。

总目标按顺序分三段：**需求内容补全 → 代码优化 → 新功能开发**。

## 2. 治理模型

| 角色 | 职责 |
|---|---|
| 主 agent | 制定/维护本计划；派发任务；调度并行；验收（可派发独立审计子 agent 并行验收）；合并分支；更新任务状态。 |
| 开发子 agent | 按任务卡完成开发，只允许改动 allow-list 内文件；完成后提交报告（改动清单、测试结果、自检结论）。 |
| 审计子 agent | 只读验收：核对 allow-list、跑构建/测试、对照验收标准，输出 PASS/FAIL 与问题清单。 |

执行规则：

1. **原子性**：每个任务卡一次派发、一次交付；发现问题允许一次返工回合，返工仍不过则任务标记 FAIL 并由主 agent 重新拆解。
2. **并行安全**：可并行的任务其 allow-list 不得相交；涉及大范围代码改动的任务在独立 git worktree/分支（`task/<id>-<slug>`，worktree 置于 `D:\wt\`）中进行；纯新增文档任务可直接在主工作区新增文件（禁止改已有文件）。
3. **提交纪律**：开发子 agent 不得直接提交到 master；合并由主 agent 在验收 PASS 后执行。
4. **验收基线**：Release 构建 0 warning/0 error（仓库已开 `TreatWarningsAsErrors`）、锁定 restore、相关测试全绿、符合 `docs/plans/CODING_STANDARDS.md`。
5. 本计划与旧治理文档（receipt/独立审计/gate 全套）冲突时，以本计划为准：保留 `eng/run-phase2-gate.ps1` 作为里程碑级质量闸，日常任务验收用第 4 条基线，不再要求 receipt 官僚层。
6. **子 agent 模型策略**（用户定版，2026-08-29）：后续派发的子 agent 一律使用 **Claude Opus 5 Thinking (High)**；仅限 Claude 系模型，禁止其他厂商模型。轻量文档修订不再单独派发子 agent，由主 agent 直接完成。已在途任务（F3、O4、F1 审计）维持原模型不变。

## 3. 阶段 DAG 与并行泳道

```mermaid
flowchart LR
  subgraph Phase0[Phase 0 基线收敛]
    P01[P0-1 基线合并]
    P02[P0-2 文档对齐]
    P01 --> P02
  end
  subgraph Phase1[Phase 1 需求补全]
    R1[R1 对话生成特效 PRD]
    R2[R2 CLI/MCP 批量 PRD]
    R3[R3 Jobs 串行队列 PRD]
    R4[R4 ADR-007 写入安全设计]
    R1 --> R4
  end
  subgraph Phase2[Phase 2 代码优化]
    O1[O1 工作区与残留清理]
    O2[O2 治理与 gate 精简]
    O3[O3 构建测试基线报告]
  end
  subgraph Phase3[Phase 3 新功能]
    F1[F1 Recipe 结构化生成通道]
    F2[F2 受限构建执行]
    F3[F3 Jobs 串行队列]
    F4[F4 CLI 批量入口]
    F5[F5 MCP 入口]
    F6[F6 端到端验收]
  end
  P01 --> O1 & O2 & O3
  P01 --> F1
  R1 --> F1
  R4 --> F2
  F1 --> F2
  R3 --> F3
  P01 --> F3
  R2 --> F4
  F1 --> F4
  F3 --> F4
  F4 --> F5
  F2 --> F6
  F3 --> F6
  F4 --> F6
```

并行泳道：P0-1 与 R1/R2/R3 可同时开工（R 系列只新增文档）；O1/O2/O3 相互并行；F1 与 F3 相互并行；F2 与 F4 在 F1 完成后并行。

## 4. 原子任务卡

### Phase 0 基线收敛

**P0-1 基线合并**
- 目标：将 `codex/usermode-integration` 合并进 `master`，使 U/A 两条线的实现成为主线事实。
- 依赖：无。
- 交付物：master 上的 merge 提交；Release 构建与全量测试结果报告。
- allow-list：git 合并涉及的全部 tracked 文件（不新增功能代码）；根目录未跟踪的 `PROJECT_ARCHITECTURE_AND_DEVELOPMENT.md` 与分支同名提交冲突时，以分支版本为准。
- 验收标准：merge 完成无冲突残留；`dotnet build VFXComposer.sln -c Release` 0 warning/0 error；`dotnet test` 全绿（记录各项目通过数）。

**P0-2 文档对齐**（依赖 P0-1）
- 目标：消除 `docs/coordination/`（仍写 P1 active、Phase 2 NO-GO）与架构文档、实际代码之间的矛盾；标注 superseded 段落。
- 交付物：更新后的 coordination 三份文档 + 顶部状态摘要。
- allow-list：`docs/coordination/**`、`PROJECT_ARCHITECTURE_AND_DEVELOPMENT.md`（仅追加"当前计划入口"指向本文件）。
- 验收标准：三份文档当前状态段一致指向本计划；无残留"active P1"类误导表述。

### Phase 1 需求补全

**R1 对话生成特效 PRD**
- 目标：定义"文字对话 → 单个特效"的完整产品需求：用户流程、输入输出、与现有能力（Chat 通道、Recipe schema、`RecipeValidator`、Unity 编译器）的映射、缺口清单、验收场景。
- 依赖：无（并行于 P0-1；代码参考读 `D:\wt\i2s-integration`）。
- 交付物：`docs/requirements/REQ-001_CHAT_TO_VFX.md`。
- allow-list：仅新增该文件。
- 验收标准：覆盖正常流/失败流/非目标；每条需求可测试；缺口与 F1/F2 任务能一一对应。

**R2 CLI/MCP 批量生成 PRD**
- 目标：定义批量需求入口：需求清单格式、CLI 命令面、MCP 工具面、与 Jobs 队列的关系、安全边界（不绕过校验与写入限制）。
- 依赖：无。
- 交付物：`docs/requirements/REQ-002_BATCH_CLI_MCP.md`。
- allow-list：仅新增该文件。
- 验收标准：CLI 与 MCP 共用同一执行层；批量语义（顺序、失败继续/中止、幂等）明确。

**R3 Jobs 串行队列 PRD**
- 目标：定义受机器配置限制的单并发任务队列：任务生命周期、持久化、进度/取消、Unity 单实例锁的调度约束、Desktop Jobs 页展示需求。
- 依赖：无。
- 交付物：`docs/requirements/REQ-003_JOBS_QUEUE.md`。
- allow-list：仅新增该文件。
- 验收标准：与 Protocol 现有 Job DTO（`JobProgress`、`CancelJobCommand` 等）兼容；崩溃恢复语义明确。

**R4 ADR-007 项目写入安全设计**（依赖 R1）
- 目标：为"AI 产物写入 Unity 项目"建立新 ADR：路径 containment（限 `Assets/VFX/Generated`，`Assets/VFX/Shared` 政策一并裁决）、原子写入/回滚、覆盖策略、威胁模型增量、fail-closed 行为。
- 交付物：`docs/rules/ADR-007_CONTROLLED_PROJECT_MUTATION.md`。
- allow-list：仅新增该文件。
- 验收标准：不推翻 ADR-005/006 既有边界；每个写入路径有明确的拒绝条件。

### Phase 2 代码优化（均依赖 P0-1）

**O1 工作区与残留清理**
- 目标：清理 master 上分支切换残留（空目录等）、确认 `.gitignore` 覆盖 `.codex_tmp` 等产物目录、盘点 `D:\wt\` 各 worktree 哪些可退役。
- 交付物：清理提交 + worktree 退役建议清单（文档）。
- 验收标准：`git status` 干净；构建不受影响。

**O2 治理与 gate 精简**
- 目标：保留 `eng/run-phase2-gate.ps1` 质量闸能力，产出一个日常可用的轻量验收脚本（构建+测试+schema 校验），并写明何时用重闸、何时用轻闸。
- 交付物：`eng/run-task-acceptance.ps1` + 使用说明。
- 验收标准：轻闸在干净 master 上一次跑通；不修改重闸行为。

**O3 构建与测试基线报告**
- 目标：在合并后的 master 上完整跑一遍构建、全量 .NET 测试、Unity EditMode 测试（如可行），形成新基线数字，供后续任务对照。
- 交付物：`docs/plans/BASELINE_REPORT.md`。
- 验收标准：数字可复现（附命令）；失败项如实记录并分类（阻塞/非阻塞）。

**O4 Unity 既有测试失败 triage**（依赖 O3；F2 的前置）
- 目标：对 O3 发现的 Unity EditMode 8 个确定性既有失败逐个定位根因并处置：属"陈旧 pin/fixture/注册表与现状不同步"的直接修复；属真实缺陷或需产品决策的，出报告待主 agent 裁决。八项症状：契约 sha256 pin 不符 ×2、错误码清单不同步、preview 场景缺 driver 组件 ×2、W24FS107≠W24FS109、句柄暴露检查、状态注册断言。
- allow-list：`project/Packages/com.vfxcomposer.unity/**` 中与八项失败直接相关的测试/fixture/契约 pin/注册表文件；`docs/plans/UNITY_TEST_TRIAGE.md`（新建报告）。
- 验收标准：EditMode 全绿，或"修复 N 项 + 明确豁免清单（每项有根因与裁决请求）"；.NET 侧零改动。

### Phase 3 新功能

**F1 Recipe 结构化生成通道**（依赖 P0-1、R1）
- 目标：在 Chat 通道之上实现面向特效的结构化生成：Recipe schema 约束的 prompt 模板、AI 输出解析、schema 校验、失败重试话术；产物为 recipe JSON 草稿（不写 Unity 项目）。
- 建议位置：`src/VFXComposer.AI.Providers` 新子域 + Desktop Create 页接线。
- 验收标准：给定固定 mock AI 响应，解析/校验/错误路径测试全绿；不产生自动网络请求。

**F2 受限构建执行**（依赖 F1、R4）
- 目标：把 recipe 草稿交给 Unity 侧编译器构建 Prefab。执行体按 ADR-007 裁决：用户显式触发的 Unity batchmode 短生命进程（扩展 `Invoke-Unity.ps1` 纪律），不复用只读链路、不给 Worker 加写权限；写入面为封闭清单 `Assets/VFX/Generated/**` + `ProjectSettings/VFXComposer/BuildManifests/*.manifest.json`，`Assets/VFX/Shared` 构建期只读。
- 范围（ADR-007 裁决）：首版限 v1 `VfxCompiler` 模板域；`Impact2DCompiler`/`Area2DCompiler` 因无条件 `Ensure()` 覆盖 Shared，在提供只验证模式前不进 AI 构建范围。
- ~~生产闸初版决策（复用 `BuildProduction`）~~ **已被 F2 停手报告推翻并重新定版（主 agent，2026-08-29，ADR-007 v1.2）**：F2 核实 `BuildProduction` 对 AI recipe 无可达成功路径（formal 分支要求 docs/ 契约与 trace，legacy 分支被 `CommitFormalManifest` 拒绝 E24S5-092 成死路，全仓零成功调用点）；且 strict E8014 要求 recipe 已在 `Assets/VFX/Recipes/**` 溯源，与项目外暂存矛盾。裁决采纳候选方案 A：①ADR-007 写入面增补成员 3（`Assets/VFX/Recipes/<Sanitize(effectId)>.json` 构建溯源单文件，仅构建入口哈希复验后原子写入）；②构建走 legacy `Build` + 执行器层计划绑定提交（DryRun 计划 → 提交前复核 recipeHash/revision/buildHash/输出路径，等价 `MatchesExactPlan` 语义），不走 `W24S5ProductionGate`；③legacy 批准死路记录为 W24 既有缺陷，保持 fail-closed 不修。附带定版：Unity 侧无 catalog 版本号（`templateCatalogVersion` 为自由文本），F2 的 catalog 比对降级为**记录性**字段（构建结果记录 recipe 声称的 catalog 版本文本 + 派生身份哈希如模板 id/version/assetGuid），硬性门槛保持"逐模板存在性 + L2 权威校验"（模板漂移必然在此失败）。
- 清单可移植性已定版（主 agent，2026-08-29，v1）：**还原纪律方案**——`dependencyHash` 属机器相关导入产物哈希，构建/测试轮次后的 dependencyHash-only 漂移一律 `git checkout` 还原不入提交；"生成区零 diff"断言实现为排除 `dependencyHash` 字段的语义比较。把 `dependencyHash` 移出入库清单的 schema 改造记为后续债务（F6 后评估）。若 F2 实施中发现还原纪律不可行（如该哈希有契约消费者），停手报告。
- 验收标准：一个样例 recipe 端到端产出 Prefab；越界路径写入被拒绝且有测试（含 Windows 保留名等负向用例）。

**F3 Jobs 串行队列**（依赖 P0-1、R3）
- 目标：实现单并发持久化任务队列 + Desktop Jobs 页（列表/进度/取消），复用 Protocol Job DTO。
- 宿主形态已定版（主 agent，2026-08-29）：执行层做成**库**（新程序集 `src/VFXComposer.Jobs`，仅依赖 Protocol）+ 跨进程 durable 单写者锁（复用 `ProviderConfigurationRevisionLock` 模式）；Desktop/CLI/MCP 各入口在进程内宿主执行器，锁保证全局并发=1；不引入常驻服务与新网络面。CLI `--detach` 语义由 F4 在此模型上实现（提交进程退出后队列状态在 store 中，接续执行由下一个宿主进程恢复）。
- 验收标准：入队/执行/取消/崩溃恢复测试全绿；并发提交时严格串行执行。

**F3b Jobs 队列加固**（依赖 F3；F4 的前置。来源：F3 审计非阻塞建议 1/2/4/6/7）
- 目标：① `JobQueueHost` 执行循环补非 `JobQueueException` 兜底（磁盘/权限故障时把当前 job 以稳定码定局后继续循环，不得让队列静默停摆或留下 RUNNING 悬挂 + 锁占用）；② 宿主无注册 executor 时不领取 job（或不取执行器锁），消除 Desktop 零 executor 宿主抢锁把未来 CLI job 判死（VFXJ0006）的前向风险；③ `SystemJobProcessInspector` 补"启动时间不匹配则不终止"的真实子进程测试（REQ-003-08 防 PID 复用路径），并在注释说明 1 秒容差来源；④ 产物数 ≤64 与 payload ≤65536 的越界拒绝负向测试。
- allow-list：`src/VFXComposer.Jobs/**`、`src/VFXComposer.Jobs.Tests/**`、`apps/VFXComposer.Desktop/App.axaml.cs`（仅②需要时）、`apps/VFXComposer.Desktop.Tests/**`（仅②需要时）。
- 验收标准：构建 0/0；全量测试全绿（≥532）；①②各有专项测试。

**F4 CLI 批量入口**（依赖 F1、F3、F3b、R2）
- 目标：独立 CLI（新 console 工程），读取需求清单文件，逐条入队生成，输出进度与结果汇总。
- 验收标准：样例清单批量跑通；单条失败不中断整批（按 R2 语义）。

**F3c Jobs itemId 持久化**（依赖 F3；F6 前置。来源：F4 交付已知限制 1）
- 目标：`JobRecord` 持久化 `itemId`（目前只参与幂等键派生不落库），使 `queue list`/`batch status`/Desktop Jobs 页可按 REQ-003 §9.1 展示 itemId。store schema 变更需显式处理版本（升版或兼容读取，方案自定并说明理由，保持未知版本 fail-closed 纪律）。
- allow-list：`src/VFXComposer.Jobs/**`、`src/VFXComposer.Jobs.Tests/**`、Desktop Jobs 页文件（`JobsViewModel`/`JobListItemViewModel`/`JobsView.axaml*`）、`apps/VFXComposer.Desktop.Tests/**`。CLI 侧展示归 F5，不在本卡。
- 验收标准：构建 0/0；全量测试全绿；itemId 跨崩溃恢复/重新入队保持；版本处理有专项测试。

**F5 MCP 入口**（依赖 F4）
- 目标：MCP server 暴露"生成特效/查询任务"工具，复用 F4 执行层（`VFXComposer.Batch.Core`）；仅 stdio transport，不引入新网络面。
- 底座已定版（主 agent，2026-08-29）：**手写 stdio JSON-RPC**（不引入官方 MCP C# SDK——不在批准 feed，且 REQ-002 §7 工具面是闭集手写量可控）；零新 NuGet。
- 随卡收纳 F4 审计建议：①增补 `batch cancel <batchId>`（执行层方法 + CLI 命令 + MCP 工具 `vfx_cancel_batch`，见 REQ-002 §6.2 勘误）；②`BatchVerdict.Pending` 在退出码映射中显式处理（不落默认 0）；③CLI notice 输出附带 `JobQueueException` 稳定码。
- 验收标准：MCP 客户端可发起生成并查询任务状态（stdio 往返测试，mock 通道）；工具面与 REQ-002 §7 一致（含新增 `vfx_cancel_batch`）；redaction 与零网络测试同 CLI 标准。

**F6 端到端验收**（依赖 F2、F3、F4、F5；任务卡已细化定稿 2026-08-29，待 F2 合入后派发）
- 目标：对话生成单个特效 + CLI 批量生成多个特效两条主流程的 E2E 测试与人工验收脚本；同时清算 F1/F3/F4/F5 审计遗留的全部必做项。
- 必做清算项（来源见各审计处置段）：
  - ① `BatchQueueReportBuilder.EntryLabel` 改 `job.ItemId ?? job.JobId` 并修正失实注释（F5 建议①，决定 MCP `vfx_get_batch_report` 满足 REQ-003 §9.1）。
  - ② CLI/MCP 双面等价性测试改构造性：反射遍历 `JobRecord` 属性 + 显式排除清单（`JobId`/`RequestId`/`IdempotencyKey`/时间戳/`SourceEntry`），当前 `ItemId` 已漏在断言外（F5 建议②）。
  - ③ IL 扫描面（`NoProjectAccessSurfaceTests.ProductAssemblies`）纳入 `vfxc`/`vfxc-mcp` 两个入口装配体（F4 建议⑤ + F5 建议⑤合并）。
  - ④ MCP 侧 `vfx_validate_manifest` 零网络证据补齐（F5 建议⑥）；生产组合根零网络真实用例（F4 建议⑤余项）。
  - ⑤ `batches/` 样例清单 + schema 入库（F4 建议⑧），作为 E2E 批量流程的固定输入。
- 裁量项（时间允许则做，否则逐条记录不做理由）：F5 建议③（`single-` 保留前缀或注释降级）、④（派生清单 onFailure 断言）、⑦（MCP Ctrl+C 假象）、⑧（initialize 必需成员）、⑨（常量重复）；F4 建议④（ValidationFailed/ChannelFailed 区分码）；F3 建议 3（执行器锁跨进程真杀测试）；F1 建议②（Desktop 裸 catch 稳定码治理）。
- E2E 范围：流程一（Desktop 对话 → recipe 草稿 → 确认 → 构建 → Prefab 三件套核验）可用 mock 通道 + 真实 Unity batchmode；流程二（CLI 清单批量 → 队列串行 → 报告/退出码核验）+ MCP 冒烟（提交/状态/取消往返）。人工验收脚本写入 `eng/`。
- allow-list：`tests/**`（新 E2E 工程）、`batches/**`（样例清单与 schema）、`eng/**`（验收脚本）、清算项①-④涉及的 `src/VFXComposer.Batch.Core/**`、`apps/VFXComposer.Cli*/**`、`apps/VFXComposer.Mcp*/**`、`src/VFXComposer.*.Tests/**`；禁止改动 Jobs 核心语义与 Unity 包生产代码。
- 验收标准：两条主流程在干净环境一次跑通并有记录；必做清算项①-⑤全部落地有测试；Release 全量测试不回退；EditMode 基线不回退；dependencyHash 还原纪律照旧。

## 5. 任务状态板

| 任务 | 状态 | 派发对象 | 验收 |
|---|---|---|---|
| P0-1 | DONE | 开发子 agent | 初审 PASS（merge `3375a8fe`，构建 0/0，测试 450/450）；O3 复跑作独立复核 |
| P0-2 | DONE | 开发子 agent | 主 agent 验收 PASS（5 文件纯追加标注，历史数字零改动）；范围外遗留（stage-notes/ADR-004/ai-workflow README 指针）追加为 P0-2b |
| R1 | DONE | 开发子 agent | 独立审计 PASS（映射 19 项属实、无阻塞问题）；3 条建议已微调完毕（v0.2），REQ-001 已合入 |
| R2 | DONE | 开发子 agent | 独立审计 PASS；4 条建议已微调（v0.2），REQ-002 已合入 |
| R3 | DONE | 开发子 agent | 独立审计 PASS；同轮微调（v0.2），REQ-003 已合入 |
| R4 | DONE | 开发子 agent | 独立审计 PASS；6 条建议已落实（v1.1），ADR-007 转 ACCEPTED 合入。Phase 1 需求补全全部关闭 |
| O1 | DONE | 开发子 agent | 主 agent 验收 PASS（.gitignore 补齐、空目录清理、退役清单交付；worktree/分支退役延后到 O2/O3 合并后执行，`codex/m1`、`codex/m2` 两个未并入分支暂保留） |
| O2 | DONE | 开发子 agent | 主 agent 验收 PASS（脚本三条路径实测；`-SkipLockedRestore` 为 O3 修复前过渡开关，O3 合并后停用） |
| O3 | DONE | 开发子 agent | 验收 PASS 并合并（`1aba917f`）：锁修复无版本变化、锁定 restore 18/18、450/450 独立复核一致；轻闸 `-SkipLockedRestore` 不再需要 |
| O4 | DONE | 开发子 agent | 返工轮验收 PASS：EditMode **658 = 604/0/54 归零**（54 跳过中恰 1 条为 R-4 显式豁免），合并 `见 git log`。返工轮补充裁决：R-5 双闭集细化批准（W17W18 直接子目录是分包层非 effectId，"下探型/自持清单型"两闭集均为收紧）；R-2 实际重封 7 文件（pin 连锁到 3 契约自哈希与配对 trace，均由生产代码计算并双算核验）；4 篇 stage-notes 的旧哈希叙述记录属历史文档，不清理。**Phase 2 全部关闭，F2 解锁**。历史备注：**双交付事件**：被判卡死而重派的首实例实际在长跑 Unity 测试，21:17 也交付了（分支 `task/O4-unity-test-triage`，6 修 2 裁决，EditMode 602/2/53）；主 agent 择定 O4b 路线（首实例对 sustained flame pin 做了级联 re-pin，与既定 R-4 豁免裁决相悖；两实例独立得出相同 preview 根因，互为佐证）。首实例分支暂留参考，O4b 合入后退役。O4b 首轮交付：2 项已修（错误码清单补登 14 码、W24FS107 断言修正且严格度上升），EditMode 596→599 通过。主 agent 裁决（2026-08-29）：①ERROR_CODES.md 越界追认批准；②R-1 批准拆分 driver 类到独立文件（纯搬移）；③R-2 批准 S3 侧最小重封（约 4 文件，allow-list 相应扩展）；④R-4 sustained flame pin 豁免（漂移早于仓库历史、重封牵连 111 文件）——测试以显式 Ignore + 理由注释落地，指向 TRIAGE 文档；⑤R-3 批准 LeaseRoot 枚举替换句柄参数（零行为改动）；⑥R-5 定版：注册表维护显式容器目录闭集（当前 3 个名字），容器自身免同名清单、其子目录照常校验，未知无清单目录仍 fail-closed。⑦dependencyHash 漂移记入 F2 开工前置决策 |
| F1 | DONE | 开发子 agent | 独立审计 PASS（复跑：锁定 restore 18/18、构建 0/0、全量 483/483 全绿、24 文件全部在 allow-list）；合并 `fd7b508f` 并推送；worktree 与分支已退役。3 条非阻塞建议见下 |
| F3 | DONE | 开发子 agent | 独立审计 PASS（合并态复跑 532/532 全绿、构建 0/0、46 文件全在 allow-list、共享文件纯加法）；合并 `2b71eb9` 已推送；worktree/分支已退役。**REQ-003-12 裁决：条件豁免成立**——Worker 取消映射分支仅在 F2 弃 batchmode 改走 Worker 路线时才需交付，batchmode 分支（精确 PID 终止+临时目录清理）已交付有测试；若未来 Worker 化需重开条目 |
| F3b | DONE | 开发子 agent | 主 agent 初审 PASS（小任务免独立审计）：7 文件全在 allow-list，合并态 538/538 全绿、构建 0/0，交付含反向变异验证；新增 VFXJ0016；零 executor 宿主定版为纯观察者（不取锁不恢复不领取）。合并 `e36d5a8d` 已推送，worktree/分支已退役 |
| F4 | DONE | 开发子 agent | 独立审计 PASS（合并态 625/625 全绿、36 文件纯新增、退出码 8 码逐码测试、路径逃逸 9 形态负向、6 条已知限制核实属实）；合并 `9d4e23ed` 已推送；worktree/分支已退役 |
| F3c | DONE | 开发子 agent | 主 agent 初审 PASS（9 文件全在 allow-list，合并态 635/635 全绿、构建 0/0）：store schema 升版 `/2`（版本 1 按 VFXJ0009 fail-closed，itemId 不可恢复故不做兼容读取）、`JobRecord` 纯加法 API（Batch.Core/Cli 零改动通过编译）、Jobs 页两态展示。合并 `c24c9de7` 已推送，worktree/分支已退役。注意：开发机旧 job store 首次读取会拒绝，删除 `%LocalAppData%\VFXComposer\Jobs` 重建即可 |
| F5 | DONE | 开发子 agent | 独立审计 PASS（Release 复跑 692/692 全绿、真实管道冒烟 6 项符合预期、39 文件全在 allow-list、零新 NuGet）；合并 `0a5b918f` 已推送，worktree/分支已退役。台账补正：F5 实为 **4 笔提交**（含首笔 `60fdd80a` 批次取消执行层）。工具数裁决：8 个正确（任务卡"5+1=6"系主 agent 起草笔误，REQ-002-10 已勘误为 8）。**验收注意：测试复跑必须用 Release 配置**——Debug 下 Broker/LocalE2E 38 条会因 U4FS001（校验器要求 Release Broker.exe）失败，属既有设计非缺陷 |
| F2 | IN_PROGRESS | 开发子 agent | 首轮按"不可行则停手"条款正确停手（交付 F1 审计建议①的哈希互比测试 `97312cd4`，693/693 全绿）：报告两个真实阻塞（`BuildProduction` 无可达成功路径；strict E8014 与项目外暂存矛盾）+ 三候选方案。主 agent 裁决采纳方案 A（ADR-007 升 v1.2 增补写入面成员 3，构建走 legacy `Build` + 计划绑定提交），已续派同一 agent 继续（worktree `D:\wt\i2s-f2` 与分支保留） |
| F6 | BLOCKED | — | 等 F2（F5/F3c 已完成） |

已知非阻塞遗留（P0-1 交付报告）：①`services/VFXComposer.Broker.HandleProbe` 与 `services/VFXComposer.Broker.Tests` 的 `packages.lock.json` 自 baseline 起与引用图不同步（归入 O3）；②`.gitignore` 未覆盖 `tests/**` 构建产物（归入 O1）。

F1 审计非阻塞建议（后续任务顺带处理）：①补一条直接互比 PlainText/StructuredOutput 两形态哈希的测试（可并入 F2）；②`CreateViewModel` 的 `SendChatAsync`/`GenerateRecipeAsync` 末尾裸 `catch` 无稳定错误码——master 既有风格，统一治理另立小任务；③Desktop 侧未来可自动优选 `StructuredOutput` 形态（增强，非需求）。

F3 审计非阻塞建议处置：建议 1/2/4/6/7 收进 F3b 任务卡；建议 3（执行器锁跨进程真杀测试，可仿 `RevisionLockHost` 先例）留给 F6 端到端验收裁量；建议 5（Jobs 页批次分组折叠交互）登记为 v1 已知限制，不排期；建议 8（lock 文件末行换行）为 NuGet 生成物，忽略。

F5 审计非阻塞建议处置（11 条）：①`BatchQueueReportBuilder.EntryLabel` 改 `job.ItemId ?? job.JobId` 并修正失实注释（F3c 已落 ItemId）→ **F6 必做**（决定 `vfx_get_batch_report` 满足 REQ-003 §9.1）；②等价性测试改构造性（反射遍历 + 显式排除清单）→ F6；⑤IL 扫描面纳入 `vfxc`/`vfxc-mcp` 装配体（与 F4 建议⑤合并）→ F6；⑥MCP 侧 validate 零网络测试 → F6；③batchId `single-` 前缀无保留机制（注释断言过强，实际风险低）、④派生清单 onFailure 无断言、⑦Ctrl+C 假象、⑧initialize 必需成员按可选处理、⑨常量重复 → F6 裁量；⑩状态板已补正 4 提交；⑪一文件多类型与仓库既有风格一致，不计违规。

F4 审计非阻塞建议处置：①批次级取消（REQ-002 内部不一致，已记勘误）→ F5 任务卡；②Pending 退出码显式映射 → F5；③notice 附带 VFXJ 稳定码 → F5；④ValidationFailed/ChannelFailed 区分码（需 Jobs 新码）→ F6 裁量；⑤生产组合根零网络真实用例 → F6；⑥`Cli.csproj` 引用 `AI.Providers` 记为**有意偏离**（组合根需绑定 `AiDesktopRuntimeFactory` 门面，`Batch.Core` 保持干净；若 F5 遇到同样问题再考虑抽独立装配体）；⑦argv 回显有界，无动作；⑧F6 allow-list 应纳入 `batches/` 样例清单与 schema。F4 已知限制 1（JobRecord 无 itemId）→ 新任务卡 F3c。

运维事件（2026-08-29 约 19:00）：`D:\wt\` 下全部在途 worktree（i2s-f1/i2s-f3/i2s-o4）被外部清空一次（F3 交付报告推测为 worktree 退役清理波及在途目录）。F3 agent 用 `git worktree repair` + 重建源码恢复并改为逐单元提交；F1 提交未受损（已合入 master），其 worktree 已退役。**教训：worktree 退役只能由主 agent 在确认无在途任务共用 `D:\wt\` 时执行；并行 worktree 验收前先 `git status` 核实磁盘完整性；开发子 agent 应逐逻辑单元提交。**

> 状态板由主 agent 在每次派发/验收后更新。
