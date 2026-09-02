# ADR-008：Desktop 构建闭环（独立构建宿主子进程，Desktop 保持零项目 I/O）

状态：`ACCEPTED`（主 agent 验收转正，2026-09-03，F8c 派发前置；原 `PROPOSED` 为任务卡 R6 交付物）
日期：2026-09-01（定版）/ 2026-09-03（转 ACCEPTED）
规范架构令牌：`DESKTOP_BUILD_CLOSED_LOOP_V1`

本 ADR 兼容并继承 ADR-005（`USER_MODE_LOCAL_CREATIVE_TOOL_V1`）、ADR-006（`AI_PROVIDER_TWO_CHANNEL_ROUTING_V1`）与 ADR-007（`CONTROLLED_PROJECT_MUTATION_V1`），不重开任何已关闭结论。它裁决的是唯一一个新问题：**用户在 Desktop 确认草稿之后，如何不离开 Desktop 完成受限构建**，同时不破坏「Desktop 零直接项目 I/O」的机器可验证边界。

## 1. 背景

REQ-004 §4 非目标 5 把「Desktop 内构建面」显式排除出 F8a/F8b 范围并指向本 ADR（设计）与 F8c（实现）。裁决基于以下经核实的代码事实（2026-09-01 master `2aa62bbb`）：

1. **工程引用图**：`apps/VFXComposer.Desktop` 引用 `Client`/`Protocol`/`AI.Contracts`/`AI.Providers`/`Jobs`，**不引用 `Batch.Core`**；CLI 与 MCP 引用 `Batch.Core` 并由它提供两类 payload 执行器（`RecipeGenerationJobExecutor`、`RecipeBuildJobExecutor`）。
2. **F3b 定版**：Desktop 的 `JobQueueHost` 以零 executor 构造，是纯观察者——不取执行器锁、不领取 job、锁被他人持有时降级为提交/观察面（`App.axaml.cs` 的 `JOBS_EXECUTOR_STANDBY` 分支）。
3. **IL 扫描面**：`apps/VFXComposer.Desktop.Tests/NoProjectAccessSurfaceTests.cs` 对 Client + Desktop 两个产品程序集做全量 IL 级扫描：禁止 `System.IO.*`/`System.Net.*`/`System.Environment`/Unity 前缀类型引用、禁止含 `Unity`/`ProjectPath`/`Pipe`/`Http` 等片段的标识符、禁止项目路径样字面量。既有豁免是**恰三处的封闭清单**（`PrivateImagePreviewDecoder.DecodeAsync` 的 Stream、`UiPreferencesStore` 的当前用户存储、U4 的三个 Client 管道类型），每处豁免都有精确上下文匹配与「防前缀影子」负向测试。
4. **`Batch.Core` 的能力面天然踩中全部禁区**：`UnityBuildHostLocator`（`Environment.GetEnvironmentVariable` + 目录走查 + `ProjectPath` 属性）、`UnityProjectLockProbe`（独占句柄探测 `project/Temp/UnityLockfile` + `Process` 存活核验）、`RecipeBuildOrchestrator`（scratch 目录 staging 写入 + 启动 `tools/Invoke-Unity.ps1` 子进程）。类型名本身（`Unity*`、`ProjectPath`）就命中标识符禁区——Desktop 只要在 IL 里引用这些成员即违规。
5. **执行器锁**：`JobExecutorLock` 是跨进程单写者 durable lock（`FileShare.None` 句柄，进程亡即 OS 释放）；每 store 至多一个执行宿主，第二实例 fail-closed（CLI 已有 `ObservingForeignExecutor` 降级语义）。
6. **草稿构建断层（新确认）**：`BatchRecipeBuildPayload.Create(draftId, recipeJson)` 的 **draft-backed 形态在生产代码中零调用点**（唯一生产调用方 `BatchSubmissionService` 恒传 `draftId: null` 的 manifest 形态）。`RecipeBuildOrchestrator` 对 draft-backed payload 的 `DraftNotFound`/`DraftNotConfirmed`/`DraftHashMismatch` 复验与 `MarkBuilt`/`MarkBuildFailed` 状态推进逻辑齐备且有测试，但没有任何入口生产这种 payload。**后果**：Desktop 确认的草稿即使用户「切命令行」，也只能手工导出 recipe JSON、写 batch manifest、走 `vfxc batch run`——该路径不携带 `draftId`，构建成败**不回写草稿状态**，`ConfirmedAwaitingBuild` 永不迁移到 `Built`/`BuildFailed`。所谓「闭环」首先是把这条断链接上，其次才是省掉切命令行。
7. **锁探测接线现状**：`UnityProjectLockProbe` 仅在 CLI 前台跑批注册了 build-capable executor 时接入 `JobQueueHost`（`JobStoreQueueSession.TryStartExecutors`）；探测 Busy 时 job 保持 QUEUED、队列进入 `WaitingProjectLock` 有界退避，不失败不抢锁；构建 wrapper 自身重复同一检查（exit 73）作为第二道防线。

## 2. 决策

### 2.1 路线定版：独立构建宿主子进程（路线 B）

Desktop 构建闭环采用**独立宿主子进程**形态：

1. 新增一个**非用户面的构建宿主可执行体**（`apps/VFXComposer.BuildHost`，console 工程，仅引现有库，零新 NuGet 包）。它是 Desktop 的私有执行体，不注册进任何文档化命令面（REQ-002 的 7 命令 + 8 工具闭集不变，满足 REQ-004-40）。
2. 用户在 Desktop 对一个 `ConfirmedAwaitingBuild` 草稿发出**显式「构建」动作**后，Desktop 启动一个宿主子进程，以进程参数传递恰两个身份：`draftId` 与该草稿的 `canonicalSha256`。**不传 recipe 字节、不传输出路径、不传项目路径**——与 Protocol 命令「仅哈希身份」的合同风格一致，宿主不信任调用方。
3. 宿主子进程完成全部触边工作：经 `AiDesktopRuntimeFactory.CreateCurrentUser()` 打开共享草稿 store，按 `draftId` 取记录并复验状态与哈希（复用 `RecipeBuildOrchestrator` 的既有 `DraftNotFound`/`DraftNotConfirmed`/`DraftHashMismatch` 判定）；构造 draft-backed `BatchRecipeBuildPayload` 入队；以 `RecipeBuildJobExecutor` + `UnityProjectLockProbe` 宿主队列执行；构建终局经 `MarkBuilt`/`MarkBuildFailed` 回写草稿状态；随后进程退出、释放执行器锁。**每次构建动作一个宿主进程，短生命**，与 ADR-007 §2.3 的「短生命 batchmode 执行体」纪律同构（宿主是执行编排层，batchmode Unity 进程仍是唯一写入执行体）。
4. Desktop 对构建进度的呈现**只经既有只读面**：Jobs 页读共享 job store 快照（`IJobQueueReader`），Create/草稿视图读草稿 store 状态。Desktop 不解析宿主 stdout、不与宿主建立管道或任何新 IPC——宿主的可观测状态以 job store 与草稿 store 为唯一权威，进程退出码仅作兜底诊断（映射稳定码，不携带路径或内容）。

### 2.2 F3b 零 executor 裁决：不重开

Desktop 进程自身**维持** F3b 定版的纯观察者语义：零 executor、不取执行器锁、不领取 job。执行器宿主职责整体落在宿主子进程。这消除了路线 A（见 §3.1）里「Desktop 常驻进程长期持有执行器锁、与 CLI 前台跑批互斥」的新并发面：宿主子进程持锁窗口 = 一次构建的时长，与 CLI 前台跑批持锁的既有语义完全同型。

两执行宿主并发时的行为沿用既有单写者语义，无新裁决：宿主子进程启动时锁被 CLI 持有 → 宿主 fail-closed 退出并以稳定码报告（job 已入队，留待持锁方或下一宿主执行——draft-backed 条目对任何宿主等价，因为 payload 自足）；反之 CLI 降级为 `ObservingForeignExecutor` 观察者。

### 2.3 IL 扫描策略：不豁免 `Batch.Core`，Desktop 侧恰一个新豁免类型

**否决**「Desktop 引用 `Batch.Core` + 给扫描器开豁免」（见 §3.2）。定版：

1. Desktop **继续不引用** `Batch.Core`。`NoProjectAccessSurfaceTests` 的禁区清单、程序集清单、既有三处豁免全部不动。
2. Desktop 新增**恰一个**豁免类型：`VFXComposer.Desktop.Services.BuildHostLauncher`（F8c 可微调命名，但必须是单类型闭集）。其唯一职责是启动宿主子进程：定位与 Desktop 同目录部署的宿主可执行文件、以 `draftId` + 哈希为参数启动 `System.Diagnostics.Process`、登记子进程退出码。豁免面为该类型上下文内的 `System.IO`（可执行文件定位）与 `System.Diagnostics.Process`（既有禁区不含 `Process`，但豁免声明仍显式列出以防未来收紧扫描时静默漂移）。
3. 豁免实现必须复刻既有三例的全部纪律：类型全名精确匹配 + 编译器生成嵌套类型识别 + 「防前缀影子」负向测试 + 「豁免闭集恰等于声明清单」断言；网络、Unity 类型、项目路径样字面量、管道/监听器在该类型内**依然全部禁止**（launcher 定位的是自身部署目录下的宿主 exe，不是 Unity 项目）。
4. 该豁免与 `UiPreferencesStore` 先例同性质：豁免的是「当前用户自有资源」（自身部署目录 + 自有子进程），不是项目访问面。U4「Desktop 禁项目访问」的语义零让步。

### 2.4 Unity 锁探测接线：留在宿主子进程，Desktop 只做文案提醒

`UnityProjectLockProbe` 的接线方式与 CLI 前台跑批完全一致：宿主子进程注册 build executor 时经 `UnityBuildHostLocator.TryLocate()` 接入真实探测；探测 Busy → job 保持 QUEUED、队列 `WaitingProjectLock` 有界退避；`Invoke-Unity.ps1` 的 exit 73 不抢锁语义是第二道防线。**Desktop 进程不做任何锁探测**（探测本身就是对 `project/Temp/UnityLockfile` 的句柄操作，属项目 I/O）；Desktop 的「构建前请关闭 Unity 编辑器」提醒是纯文案（F8a2 已交付 catalog 键），权威判定只在宿主与 wrapper。为避免「编辑器一直开着、job 无限 QUEUED、用户无感」的体验黑洞，F8c 必须把 `WaitingProjectLock` 队列态在 Desktop Jobs/构建视图显式呈现（读快照即可，零新探测面），并允许用户取消（既有 `RequestCancel` 面）。

### 2.5 draft-backed 构建通路语义定版（断层修复）

1. draft-backed `BatchRecipeBuildPayload` 自本 ADR 起有唯一生产者：构建宿主子进程。入队与执行可在同进程内先后发生，但 payload 必须完整自足（携带 recipe 字节 + 哈希），使宿主崩溃后条目可被任何后续宿主恢复执行——这是 Jobs 既有崩溃恢复语义的直接复用。
2. 一次显式构建动作恰构建一个 `draftId`。不做「自动构建全部 backlog」：`ListConfirmedAwaitingBuild()` 的 backlog 面留给 CLI/MCP 与未来批量场景（REQ-004 AC-17 语义不变）。
3. 状态回写沿用 orchestrator 既有语义：成功 → `MarkBuilt`，失败 → `MarkBuildFailed`，两者哈希绑定；草稿 store 回写失败不改变构建事实（产物与 manifest 已落盘），以稳定码记入 job 时间线（既有 `DraftTransitionFailed` 处理保持）。
4. 与 F8b2 版本链的衔接：本 ADR 不预设 store 升版细节；宿主取草稿走 `IRecipeDraftStore` 契约面，F8b2 升版后语义由 REQ-004 §8（三入口同版本部署 + `Superseded` 不进 backlog + 不因 `origin` 拒绝构建）约束。RG-6 跨进程写冲突的行为定义仍归 F8b2，本 ADR 只新增一个与 CLI 同型的进程类写者，不改变冲突面的性质。

### 2.6 ADR-005 / ADR-007 合规论证

| 边界 | 论证 |
|---|---|
| ADR-005「Desktop 零直接 Unity 项目 I/O」 | Desktop 进程的新增面仅为：启动一个自有子进程 + 读两个当前用户 store（均为既有面）。项目锁探测、staging、Unity 进程编排全部在宿主子进程。IL 扫描继续机器验证这一点（§2.3）。 |
| ADR-005「Unity 是 Unity API 与项目内容的唯一所有者」 | 不变：宿主子进程只做编排，写入执行体仍是 batchmode Unity 进程（ADR-007 §2.3 原样）。 |
| ADR-005 只读链路 | Broker/Worker 会话、命令面、只读白名单零改动；宿主不经任何 Broker/Worker 通道。 |
| ADR-007「触发者只能是用户显式动作」 | 链条为：显式确认（哈希绑定）→ 显式「构建」点击 → 宿主启动。零后台构建、零定时构建；Desktop 启动、导航、模式切换不触发宿主。 |
| ADR-007 §2.3「recipe 字节受控投递、入口不信任调用方」 | 加强而非放松：Desktop 连暂存文件都不写（现状 F2 设计中由 Desktop 写暂存的表述被本路线替代——宿主自己从 store 取字节、自己 staging、batchmode 入口再复验哈希）。哈希一致性依然在执行体内部重建。 |
| ADR-007 全局单写者 | 执行器锁 + 项目锁探测 + wrapper exit 73 三层不变，宿主子进程作为持锁者的生命周期短于等于 CLI 前台跑批。 |
| ADR-006 零自动网络 | 构建全程零 AI 请求；宿主只注册 build executor 时不构造任何网络客户端（generation executor 不注册——宿主不是生成入口）。 |

## 3. 备选方案与否决理由

### 3.1 路线 A：Desktop 进程内宿主 build executor（引 `Batch.Core`）

否决。代价三重且每重都实质：

1. **IL 边界大洞**：Desktop 的 IL 将直接引用 `UnityBuildHostLocator`/`UnityProjectLockProbe`/`RecipeBuildOrchestrator` 等成员，标识符禁区（`Unity`、`ProjectPath`）与类型禁区（`System.IO`、`Environment`、`Process` 链）需要为一整族调用点开豁免。豁免面从「三个单一职责类型」膨胀为「一条执行链」，`NoProjectAccessSurfaceTests` 的「Desktop 禁项目访问」语义从机器可验证退化为豁免清单的人工审阅。
2. **F3b 裁决被迫重开**：Desktop 常驻进程持执行器锁，Desktop 开着时 CLI 前台跑批永远降级为观察者，持锁窗口从「一次构建」变成「Desktop 进程生命周期」；Desktop 崩溃时锁虽由 OS 释放，但 RUNNING 悬挂 job 的恢复者变成了下一个 Desktop 实例——把队列执行健康度绑在 UI 进程存活上。
3. **ADR-005 字面冲突**：锁探测的独占句柄操作与 staging 写入发生在 Desktop 进程内，「Desktop remains free of direct Unity project I/O」需要重新解释。路线 B 用一个子进程边界把这三个问题全部消解，成本仅为一个新 console 工程 + 一个豁免类型。

### 3.2 Desktop 只入队、由「下一个碰巧运行的执行宿主」执行

否决。draft-backed payload 构造（`BatchRecipeBuildPayload.Create` 用 `MemoryStream`）本身踩 `System.IO` 禁区，Desktop 侧仍需豁免或把构造逻辑改写为无 IO 形态；更重要的是「入队后等待未知宿主」没有闭环体验——没有任何进程在跑执行器时，job 无限 QUEUED，与现状断层只差一步。宿主子进程方案以同等实现成本直接给出确定性执行。

### 3.3 给 `vfxc` 新增「构建草稿」命令

否决。违反 REQ-004-40（REQ-002 的 7 命令闭集不变）；且把 Desktop 私有执行体伪装成用户命令面，会诱发脚本依赖，将来收缩即破坏兼容。宿主可执行体不进任何文档化命令面，帮助文本、REQ-002、MCP 工具清单零改动。

### 3.4 路线 C：维持现状（诚实提示切命令行）

否决为终态，但**保留为 F8c 交付前的过渡态**。现状的诚实提示（F8a2 已交付）指向的 manifest 工作流不回写草稿状态（§1 事实 6），「闭环」根本不成立——`ConfirmedAwaitingBuild` 是死状态。若 F8c 因故延期，此断层必须在状态板上保持可见，不得以「提示已诚实」视为已解决。

### 3.5 常驻构建服务 / 常驻可写 Unity 会话

否决（重申 ADR-007 §3.3）：batchmode 冷启动延迟由队列语义呈现（`WaitingProjectLock`/进度事件），不以常驻会话换启动速度；引入常驻写会话需另立 ADR 由用户拍板。

## 4. 威胁模型影响

继承 ADR-005 的信任/不设防划分与 ADR-007 的全部写入面威胁裁决（L1/L2 校验、路径注入、TOCTOU、并发、中断——均在宿主/batchmode 层原样生效）。本 ADR 新增的面仅两个：

| 面 | 载体 | 防线 | fail-closed 反应 |
|---|---|---|---|
| Desktop→宿主的参数投递被篡改或伪造 | 进程启动参数（`draftId` + 哈希） | 参数只是身份不是授权：宿主从共享 store 自取记录，独立复验状态（必须 `ConfirmedAwaitingBuild`）与哈希（`CanonicalSha256` 精确判等）；伪造/漂移的身份在触碰队列前被拒 | `DraftNotFound`/`DraftNotConfirmed`/`DraftHashMismatch` 稳定码，零入队零写入 |
| 宿主可执行文件被替换 | 部署目录 | 同用户恶意代码不设防（继承 ADR-005）；意外漂移靠 release 完整性与「launcher 只启动自身部署目录内固定名称的宿主」纪律缓解 | 宿主缺失 → launcher 稳定码失败，零构建 |

「一个新的持项目锁进程类型」不成立为新面：宿主持锁行为与 CLI 前台跑批（F2/F3 已裁决）同型，仅触发入口不同。

## 5. fail-closed 行为汇总

| 条件 | 行为 | 码/出处 |
|---|---|---|
| 宿主可执行文件缺失/无法启动 | 构建动作失败，零入队 | F8c 登记稳定码（launcher 层） |
| `draftId` 不存在 / 状态非 `ConfirmedAwaitingBuild` / 哈希不符 | 宿主拒绝，零入队零写入 | `VFXB1002`/`VFXB1003`/`VFXB1004`（既有） |
| 执行器锁被他宿主持有 | 宿主不执行：条目留在队列（payload 自足，任何后续宿主可执行），以稳定码退出 | `ExecutorLockUnavailable`（既有） |
| Unity 编辑器持项目锁 | job 保持 QUEUED，队列 `WaitingProjectLock` 有界退避；Desktop 显式呈现该态并允许取消 | 既有 F3/F3b 语义 + `Invoke-Unity.ps1` exit 73 |
| 宿主进程被杀/崩溃 | 执行器锁由 OS 释放；RUNNING 悬挂 job 由下一执行宿主按 Jobs 既有崩溃恢复语义定局；batchmode 中断残留由 ADR-007 §2.4 清扫收敛 | 既有 D3/F3 语义 |
| 构建失败 | `MarkBuildFailed`（哈希绑定），精确失败码经 failure artifact 存续于队列条目 | `VFXB1xxx` + `FailureArtifactPrefix`（既有） |
| 草稿状态回写失败 | 构建事实不回滚，稳定码入时间线 | `VFXB1012`（既有） |
| Desktop 进程在构建期间退出 | 不影响构建：宿主与 batchmode 进程独立存活，结果落 job store 与草稿 store，Desktop 重启后经既有只读面可见 | 设计属性，F8c 须有测试 |

## 6. 不作声明清单

1. **不防同用户攻击者**（继承 ADR-005/007）：不防同用户恶意代码替换宿主 exe、篡改 store 文件、注入进程。
2. **不新增用户命令面**：宿主可执行体不是 CLI，不出现在帮助文本、REQ-002、MCP 工具清单；其参数形态可在 F8c 后续版本自由变更，无兼容承诺。
3. **不解决 RG-6**：草稿 store 跨进程写冲突（last-write-wins）的行为定义归 F8b2；本 ADR 的宿主写者与既有 CLI 写者同型，不扩大冲突面性质。
4. **不承诺构建时延**：batchmode 冷启动仍是分钟级（ADR-007 遗留点 5 原样），闭环指状态闭环而非即时构建。
5. **不激活 Protocol 命令为写入通道、不给 Worker 写权限、不让 Desktop 直写项目**：ADR-007 §6 第 5 条边界原样保留。
6. **不构成实现验收**：本 ADR 是 F8c 的规范输入；F8c 须按 §5 逐条交付负向测试（含 launcher 失败、身份伪造拒绝、锁互斥、Desktop 退出不中断构建），并按 CODING_STANDARDS 独立审计。

## 7. 生效条件与 F8c 任务要点

**生效条件**：主 agent 验收本 ADR 后转 `ACCEPTED`，成为 F8c 任务卡的规范输入。

**F8c 必做清单**（实现取舍在卡内自由，语义以本 ADR 为准）：

1. 新 console 工程 `apps/VFXComposer.BuildHost`（加入 `VFXComposer.sln`，继承中央包管理，零新 NuGet 包），实现 §2.1 第 3 条全链；draft-backed payload 首个生产调用点在此落地。
2. Desktop `BuildHostLauncher` 豁免类型 + `NoProjectAccessSurfaceTests` 豁免扩展（复刻既有三例纪律：精确匹配、影子拒绝、闭集断言）。
3. 草稿视图「构建」动作（仅 `ConfirmedAwaitingBuild` 可用）+ `WaitingProjectLock` 显式呈现 + 取消面接线；文案走双语 catalog。
4. §5 全表负向测试；F8a2 的「切命令行」诚实提示在闭环可用后改为指向应用内构建（保留命令行路径的说明作为补充而非唯一路径）。
5. 与 F8b2 的顺序约束：F8c 依赖 R6（本 ADR）与 F8b3（主计划 §7 DAG 不变）；若 F8b2 先行升版 store，宿主取草稿路径按 REQ-004 §8 回归。

## 8. 变更记录

| 版本 | 日期 | 变更 |
|---|---|---|
| v0.1 | 2026-09-01 | 初版（任务卡 R6 交付，主 agent 直写——子 agent 派发不可用期间按 DEV_MEMORY 预案执行） |
