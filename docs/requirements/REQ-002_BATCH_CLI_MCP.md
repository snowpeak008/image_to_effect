# REQ-002：CLI / MCP 批量生成入口产品需求（PRD）

> 状态：DRAFT（待主 agent 验收）｜任务卡：R2｜创建：2026-08-29｜作者：开发子 agent
>
> 上游：`docs/plans/OPTIMIZATION_MASTER_PLAN.md` Phase 1 任务卡 R2。对应实现任务：**F4（CLI 批量入口）、F5（MCP 入口）**；依赖 F1（Recipe 结构化生成通道，见 REQ-001）与 F3（Jobs 串行队列，见 `docs/requirements/REQ-003_JOBS_QUEUE.md`）。
>
> 分工：本文只定义"批量入口面（清单格式、CLI、MCP）与批量语义"；任务生命周期、单并发调度、持久化、取消归 REQ-003。两文档共享同一套 Protocol Job DTO 词表。
>
> 本文档为纯新增需求文档，不修改任何现有文件；与主计划表述冲突时以主计划裁决。

---

## 1. 背景

- 产品目标三大缺口之二是"MCP/CLI 批量生成入口"（主计划 §1）。
- 现状：唯一受支持的生成路线是 Unity Editor 内的 `project/Packages/com.vfxcomposer.unity/Editor/UI/VfxCompilerWindow.cs` 手工 Validate / Dry Run / Build；AI 参与只经人工贴报告回路（`docs/ai-workflow/README.md`）。没有任何命令行或程序化批量入口。
- 已有可复用底座：
  - Unity batchmode 调用与项目锁检测：`tools/Invoke-Unity.ps1`（锁占用 exit 73、超时精确 PID 终止 exit 124、NUnit 结果闸）。
  - Recipe 校验：`project/Packages/com.vfxcomposer.unity/Editor/Validation/RecipeValidator.cs`（错误格式 `{code, severity, path, message, actualValue, allowedRange}`）。
  - 结构幂等构建：`project/Packages/com.vfxcomposer.unity/Editor/Build/VfxCompiler.cs`（build hash、Dry Run unchanged、原子替换）。
  - 顺序执行先例：`docs/allwork/00_INDEX_AND_ACCEPTANCE.md`（"按文件编号从小到大逐个开发"的顺序队列治理模式）。
  - MCP 结构 stub：`project/Packages/com.vfxcomposer.unity/Editor/W24/S6/External/W24S6McpOperationEnvelope.cs`（仅 DryRun/ReadOnly 的结构校验，无 transport，无执行）。
  - Worker 命令合同：`src/VFXComposer.Protocol/Commands/`（`ValidateRecipeCommand`、`BuildCandidateCommand`、`CancelJobCommand`）与 Job 事件合同 `src/VFXComposer.Protocol/Jobs/`。

## 2. 目标与非目标

### 2.1 目标

- **O1** 用户可以用一个"需求清单文件"一次性描述多个特效需求（文字 prompt 或现成 recipe），由 CLI 一条命令批量生成。
- **O2** AI 客户端（如 Claude/Codex 等 MCP 客户端）可以经 MCP 工具提交同样的批量/单条生成请求并查询任务状态。
- **O3** CLI 与 MCP 是同一执行层上的两个薄适配器：解析、校验、入队、查询逻辑只实现一次。
- **O4** 批量语义确定：严格顺序执行、单条失败默认继续（可选中止）、幂等与断点续跑。
- **O5** 不引入任何新的安全面：不绕过 recipe 校验、不扩大写入范围、不新增网络监听。

### 2.2 非目标

- **N1** 不做远程/多机/多用户调用：CLI 与 MCP 都只服务当前 Windows 登录用户在本机的显式动作。
- **N2** 不做并行构建：并发恒为 1，由 REQ-003 队列保证（根本原因见 §9.1）。
- **N3** 不做 HTTP/TCP API、不做 Web 面板、不做定时/守护调度。
- **N4** 不做 AI 生成结果的自动视觉验收；视觉验收权仍属用户（`docs/allwork/00_INDEX_AND_ACCEPTANCE.md` §1.2）。
- **N5** 不提供绕过校验、直写任意路径、提升 authority 的任何工具或开关。
- **N6** 不在本入口内实现 prompt→recipe 生成本身（属 F1）、受限构建本身（属 F2/R4）、队列本身（属 F3）。
- **N7** 不改变 ADR-005/006 既有边界：Desktop/入口进程不直接读写 Unity 项目文件；AI 通道无 fallback、零自动网络。
- **N8** 不承诺真实付费 provider 的可用性；批量执行中 AI 调用失败按单条失败处理。

## 3. 对 v1 "无 MCP / 无 CLI" 历史决定的显式推翻

### 3.1 被推翻的决定

1. `docs/ai-workflow/README.md` 末段："No MCP integration exists in v1. There is no S9 CLI/BatchMode entry point… This is intentional."
2. `docs/EXECUTION_PLAN.md` §9.1 第 4 条："CLI/BatchMode 入口仅在手工贴报告的往返变得烦人时才做，否则推迟到 S11 之后。MCP 明确不做。"

### 3.2 推翻理由

1. **前提已变化**：v1 决定的语境是 S9"单个 Recipe、人在回路"的工作流，人工贴报告足够；现在产品目标明确要求"批量生成多个特效"（主计划 §1 缺口②），逐条人工往返在批量规模下不成立。
2. **底座已就绪**：v1 时不存在可复用的队列合同与受限执行面；现在 Protocol Job/Command DTO、batchmode 脚本、幂等构建、MCP envelope 约束模型均已存在，新增入口是薄适配而非"未经验证的命令面"。
3. **治理模型已升级**：主计划把 F4/F5 列为正式任务并配套 R4（ADR-007 写入安全）先行，命令面不再是无安全设计的旁路。

### 3.3 推翻的边界（保留不变的部分）

1. 只推翻"不做 CLI/MCP 入口"这一条；**不推翻**"AI 只产 Recipe/Patch JSON、经审核编译器落地"的总边界（`docs/allwork/00_INDEX_AND_ACCEPTANCE.md` §3.4）。
2. **不推翻** `W24S6McpOperationEnvelope` stub 自身的 DryRun/ReadOnly 限制：该 stub 保持原样，本需求另建新的工具面（§7.3）。
3. **不推翻**"Editor 内 Validate/Build 工作流仍受支持"：CLI/MCP 是新增入口，不是替代。
4. `docs/ai-workflow/README.md` 属本任务 allow-list 之外，无法就地标注 superseded；需要后续文档对齐任务补一行指针（列入 §15 缺口清单）。

## 4. 用户流程

### 4.1 CLI 主流程

1. 用户编写需求清单文件（§5），例如 `batches/fire-pack.batch.json`。
2. `vfxc batch validate batches/fire-pack.batch.json` —— 本地校验清单结构与语义，零网络、零写入。
3. `vfxc batch run batches/fire-pack.batch.json` —— 逐条入队（REQ-003 队列），前台跟踪进度，逐条输出状态行。
4. 全部条目终态后输出汇总，并写批次报告文件；退出码反映整体结果（§6.5）。
5. 用户在 Unity Editor 或 Desktop Library 中查看 `project/Assets/VFX/Generated/` 下产物（构建写入仍由 F2 完成，入口不写）。

### 4.2 MCP 主流程

1. MCP 客户端以子进程 + stdio 方式启动本 MCP server（无网络监听）。
2. 客户端枚举工具（§7.2），调用 `vfx_submit_batch` 或 `vfx_generate_effect` 提交。
3. 提交立即返回 `batchId`/`jobId`；客户端用 `vfx_job_status` / `vfx_batch_status` 轮询。
4. 终态后调用 `vfx_get_batch_report` 取结构化汇总。

### 4.3 失败往返流程

1. 批次结束后报告中某条目 `FAILED`，附 S4 格式错误（错误码 + 精确路径 + 当前值 + 允许范围）。
2. 用户（或 AI 客户端）据此修正清单中该条目。
3. `vfxc batch run … --resume`：已成功条目按幂等键跳过，只重跑未成功条目（§9.3）。

## 5. 需求清单文件格式

### 5.1 设计原则

- 未知字段一律**报错拒绝**，不静默忽略（S4 教训：AI 臆造字段必须被抓住）。
- 所有字段有界；条目数、文本长度、路径形态全部有上限。
- 校验为手写 C#（结构 + 语义两层）；`.schema.json` 描述文件仅作为给 AI 阅读的文档，与 S3 定版决策（不引入 JSON Schema 验证库）一致。
- 条目分两种：`prompt`（文字需求，走 F1 生成通道产 recipe 草稿再校验构建）与 `recipe`（现成 recipe 文件，直接校验构建）。`recipe` 条目同时是断点续跑与复现实验的载体。

### 5.2 schema 草案（`vfxcomposer.batch-manifest/1`）

```json
{
  "schemaVersion": "vfxcomposer.batch-manifest/1",
  "batchId": "fire-pack-w35",
  "onFailure": "continue",
  "defaults": {
    "dimension": "2d",
    "targetProfile": "mobile_medium"
  },
  "items": [
    {
      "itemId": "fireball-big-slow",
      "kind": "prompt",
      "prompt": "一个更大、更慢、火星更多的火球",
      "constraints": {
        "archetype": "projectile",
        "element": "fire",
        "randomSeed": 42
      }
    },
    {
      "itemId": "frost-impact-default",
      "kind": "recipe",
      "recipePath": "recipes/frost_impact_2d.default.json"
    }
  ]
}
```

### 5.3 字段约束表

| 字段 | 约束 |
|---|---|
| `schemaVersion` | 必填，精确等于 `vfxcomposer.batch-manifest/1`；不识别的版本拒绝 |
| `batchId` | 必填，lower-kebab/snake token，≤96 字符（对齐 `W24S6McpOperationEnvelope.IsSchemaToken` 及 `recipeId`/`artifactId` 等具体位点的 96 上限；`Guard.Token` 默认上限为 128，本清单取更严的 96） |
| `onFailure` | 可选，`continue`（默认）或 `abort` |
| `defaults` | 可选，仅允许 `constraints` 白名单键，作为各 `prompt` 条目缺省值 |
| `items` | 必填，1–64 条（上限对齐 `JobCompletion.MaximumArtifactCount` 的有界风格）；`itemId` 批内唯一 |
| `items[].itemId` | 必填，token ≤96 |
| `items[].kind` | 必填，`prompt` 或 `recipe`，闭集 |
| `items[].prompt` | `kind=prompt` 必填，1 字节–8 KiB UTF-8（上限对齐 `OpaqueEndpoint` 的 8 KiB 有界风格） |
| `items[].constraints` | 可选，白名单键：`archetype` / `dimension`（`2d`\|`3d`）/ `element` / `style` / `targetProfile` / `randomSeed`（int）；未知键拒绝 |
| `items[].recipePath` | `kind=recipe` 必填，相对清单文件所在目录；拒绝绝对路径、`..` 段、`\\`、盘符、UNC/device/ADS 形态；必须以 `.json` 结尾（路径规则对齐 `W24S6McpOperationEnvelopePolicy.IsSafeProjectPath` 风格） |
| 任意层未知字段 | 拒绝，错误含精确 JSON 路径 |

### 5.4 校验规则

- **结构层**：必填、类型、枚举闭集、唯一性、边界、未知字段。
- **语义层**：`recipePath` 可解析且文件是严格 JSON object；`constraints` 值域（如 `dimension` ∈ {2d,3d}）；`prompt` 条目在 F1 通道不可用时整批拒绝（fail-closed，不静默降级）。
- 错误报告复用 S4 验证报告格式：`{ code, severity, path, message, actualValue, allowedRange }`（`docs/EXECUTION_PLAN.md` §4.1 第 5 条），错误码前缀建议 `B1xx`（清单结构）/ `B2xx`（清单语义），与现有 `E1xx` recipe 错误码不冲突。

## 6. CLI 命令面

### 6.1 形态

- 新 console 工程（建议 `apps/VFXComposer.Cli`，可执行名 `vfxc`；最终位置由 F4 设计定，须加入 `VFXComposer.sln` 并继承中央包管理，见 `docs/plans/CODING_STANDARDS.md` §1.4）。
- net8.0，`TreatWarningsAsErrors` 全局生效；仅依赖执行层库与 Protocol，不引用 UnityEditor、不引用 AI adapter 内部实现。

### 6.2 命令表

| 命令 | 作用 | 网络 | 写入 |
|---|---|---|---|
| `vfxc batch validate <manifest>` | 校验清单结构与语义，输出报告，不入队 | 零 | 零 |
| `vfxc batch run <manifest>` | 逐条入队并前台跟踪至批次终态 | 仅条目执行中的显式 AI 调用（经 F1） | 仅经 F2 构建路径 |
| `vfxc batch status <batchId>` | 查询批次汇总 | 零 | 零 |
| `vfxc job status <jobId>` | 查询单任务状态/进度/诊断 | 零 | 零 |
| `vfxc job cancel <jobId>` | 请求取消（语义见 REQ-003 §8） | 零 | 零 |
| `vfxc queue list` | 当前队列快照（含队列级状态） | 零 | 零 |

### 6.3 `batch run` 关键参数

| 参数 | 语义 |
|---|---|
| `--on-failure continue\|abort` | 覆盖清单 `onFailure`；CLI 参数优先 |
| `--resume` | 按幂等键跳过已 `SUCCEEDED` 条目（§9.3） |
| `--force` | 忽略幂等跳过，全部重跑 |
| `--dry-run` | 每条只执行到"校验 + Dry Run 构建计划"，不写任何资产 |
| `--detach` | 入队后立即返回，不前台跟踪（批次策略由队列执行器强制执行，见 §9.2） |
| `--json` | 以 NDJSON 输出事件流（§6.4） |
| `--report <path>` | 指定批次报告输出路径；默认写在清单旁 `<manifest>.report.json` |

### 6.4 输出契约

- 默认：人读的逐条状态行（itemId、状态、进度百分比、稳定错误码）。
- `--json`：每行一个 JSON 事件，字段命名与 `JobProgress` / `JobLogEvent` / `JobCompletion` 的 `JsonPropertyName` 保持一致（`state`、`progressPermille`、`outcome`、`diagnostic` 等），终了输出一个批次汇总对象。
- 批次报告文件：新 schema `vfxcomposer.batch-report/1`，逐条目含 `itemId`、`jobId`、`outcome`（词表 = `JobCompletionOutcomes` ∪ 报告层专用值 `SKIPPED_IDEMPOTENT`）、`diagnostic`（稳定码）、构建产物 identity 计数（`JobArtifact` 无位置信息，报告只含 identity/hash，产物定位见 §15 缺口）。

### 6.5 退出码表

| 码 | 含义 | 依据 |
|---|---|---|
| 0 | 全部条目成功（或幂等跳过） | — |
| 10 | continue 模式下批次完成但有失败条目 | F4 验收"单条失败不中断整批" |
| 11 | abort 模式下批次被中止 | — |
| 64 | 参数/用法错误 | 对齐 `tools/Invoke-Unity.ps1` 的 64 |
| 65 | 清单解析/校验失败 | sysexits `EX_DATAERR` |
| 69 | 队列执行器/存储不可用 | sysexits `EX_UNAVAILABLE` |
| 73 | 等待项目锁超时（用户长期开着编辑器且指定了有限等待） | 对齐 `tools/Invoke-Unity.ps1` 的 73 |
| 130 | 用户 Ctrl+C 中断（已入队任务不回滚，遵循 `--detach` 等价语义） | POSIX 惯例 |

### 6.6 redaction 要求

CLI 的 stdout/stderr、`--json` 流与批次报告一律不得出现：prompt 原文、secret/token、raw endpoint、Unity 项目绝对路径。条目一律以 `itemId`/`recipeId`/稳定错误码指代（继承 `docs/plans/CODING_STANDARDS.md` §3.1）。prompt 原文只存在于用户自己的清单文件与队列存储（REQ-003 §7）。

## 7. MCP 工具面

### 7.1 形态

- 新 MCP server 工程（建议 `apps/VFXComposer.McpServer`；最终位置由 F5 设计定）。
- **transport 仅 stdio**：由 MCP 客户端作为子进程启动；禁止 TCP/HTTP/WebSocket listener，禁止任何环境变量信任根。这是"不引入新网络面"的落地形态。
- 无参数直接启动时行为对齐 Broker 的 fail-closed 惯例：不服务、稳定错误、非零退出。

### 7.2 工具表（闭集）

| 工具 | 参数 | 返回 | 等价 CLI |
|---|---|---|---|
| `vfx_validate_manifest` | 清单 JSON 内容（≤512 KiB） | 校验报告 | `batch validate` |
| `vfx_submit_batch` | 清单 JSON 内容 + `onFailure` 覆盖（可选） | `batchId` + 逐条 `jobId` | `batch run --detach` |
| `vfx_generate_effect` | 单条条目（§5.3 的 `items[]` 元素形态） | `jobId` | 单条清单的 `batch run --detach` |
| `vfx_batch_status` | `batchId` | 批次汇总 | `batch status` |
| `vfx_job_status` | `jobId` | 状态/进度/诊断 | `job status` |
| `vfx_cancel_job` | `jobId` | 取消受理结果 | `job cancel` |
| `vfx_get_batch_report` | `batchId` | `vfxcomposer.batch-report/1` 内容 | 读报告文件 |

- 所有参数有界、未知字段拒绝；所有返回经 §6.6 同一 redaction 规则。
- 工具集为闭集：新增工具必须走需求变更，禁止运行时动态注册。

### 7.3 与既有 MCP stub 的关系

- `W24S6McpOperationEnvelope`（schema `w24-s6/mcp-operation-envelope-v2`）保持原样：它只接受 DryRun/ReadOnly、`approvalToken` 必须为空、无 transport。本需求**不复用**该 schema，也**不放宽**其枚举。
- 新工具面**继承其约束模型**：操作种类 allow-list、数量有界（1–16 的同风格上界）、内容 hash 绑定、稳定错误码（stub 用 `W24MCP0xx`，新面另立前缀建议 `VFXMCP0xx`）。
- 写入权来源唯一：MCP 工具只能"提交到队列"；实际写入发生在队列执行器经 F2 受限构建路径。工具面不存在 approval/authority 参数，出现即拒绝。

### 7.4 授权模型

MCP 客户端可用的能力 = 当前用户在本机能用 CLI 做的事，不多不少。不存在"MCP 专属"能力，也不存在需要额外 token 的能力分级。

## 8. 共用执行层架构要求

1. 单一执行层库（建议 `src/VFXComposer.Batch.Core`）承载：清单解析与校验、幂等键计算、入队/查询/取消的队列客户端 API、批次报告生成。
2. CLI 与 MCP server 均为薄适配器：只做参数绑定、输出格式化、退出码/工具响应映射；**禁止**在任一入口内重复实现解析、校验、入队逻辑。
3. Desktop（Chat 单条生成，F1/F2 路线）提交任务也经同一队列客户端 API，保证三入口合流（REQ-003 §6）。
4. 一致性可测试：同一清单内容经 CLI 与经 MCP 提交，产生的队列条目（除 id/时间戳外）与最终报告等价；该等价性必须有自动化测试。
5. 执行层不引用 UnityEditor；对 Unity 的一切操作经 F2 定版路径（batchmode `tools/Invoke-Unity.ps1` 路线或 Worker `ValidateRecipeCommand`/`BuildCandidateCommand` 命令面）。

## 9. 批量语义

### 9.1 严格顺序执行

- 清单内条目顺序 = 入队顺序 = 执行顺序；不提供乱序、优先级、并行开关。
- 根本原因：Unity 同一项目同时只能被一个编辑器实例打开（`docs/EXECUTION_PLAN.md` §3.2 已知限制），全局并发 = 1 由 REQ-003 队列强制。
- 产品先例：`docs/allwork/00_INDEX_AND_ACCEPTANCE.md` 的"按编号逐个开发"顺序治理，本入口把同一纪律程序化。

### 9.2 失败策略

- 默认 `continue`：单条失败（校验失败、生成失败、构建失败、AI 调用失败）记录终态后继续下一条；批次完成后 CLI exit 10。
- 可选 `abort`：某条目 `FAILED` 后，同批次剩余 `QUEUED` 条目由**队列执行器**逐个取消，落 `CANCELLED` 终态（诊断标注批次中止原因）；CLI exit 11。
- 批次策略随批次记录持久化在队列存储中，由执行器强制执行，不依赖入口进程存活（`--detach`、CLI 被杀、MCP 客户端断开均不影响策略生效）。
- 词表纪律：不发明新状态；中止条目就是 `CANCELLED`（`JobCompletionOutcomes` 闭集），区分靠诊断码。

### 9.3 幂等与断点续跑

- 条目幂等键 = `sha256(batchId + itemId + 条目规范化内容)`；规范化规则复用 S4 的 build hash 纪律（键排序、数值稳定格式化）。
- 派生规则：现有代码中的互异校验位于 `CommandEnvelope.RequireDistinctIdentityTokens`（`requestId`/`commandId`/`idempotencyKey` 三者互异）与 `JobCorrelation`（`jobId` 与 origin 三 id 结构互异）；`JobIdentity` 本身仅对各字段做 `Guard.Token` 校验，**不做**互异校验。作为本 PRD 的附加要求（严于现有代码、方向安全）：入队时由队列客户端按角色后缀派生各 id，保证 `JobIdentity` 三字段同样互异。
- `--resume`：提交前查询队列存储，条目幂等键已有 `SUCCEEDED` 终态的条目不再入队，报告标 `SKIPPED_IDEMPOTENT`。条目幂等键在队列存储中的落点为独立持久化字段，与队列 token 性质的 `idempotencyKey` 分离，见 REQ-003 §7.1。
- `--force`：忽略跳过，全部重新入队；构建层的第二重幂等（同 recipe → Dry Run unchanged → 资产无 diff）仍然生效，重复构建无副作用（S6 结论）。
- 清单内容变了则幂等键变，自动视为新条目，不会误跳过。

### 9.4 取消

- 批次取消 = 对批内全部非终态 job 逐个发起取消；单条取消语义（排队即时取消 / 运行中协作取消 / 终态幂等 no-op）全部由 REQ-003 §8 定义，映射 `CancelJobCommand`。

## 10. 安全边界

1. **校验不可绕过**：不存在 `--skip-validation` 或等价工具参数；每条目必经 `RecipeValidator`（prompt 条目在 F1 产出 recipe 草稿后同样必经）。
2. **写入 containment**：CLI/MCP 进程自身对 Unity 项目零写入；一切项目写入只发生在队列执行器经 F2 受限构建路径，范围限定生成区。注：现行 `docs/plans/CODING_STANDARDS.md` §3.2 已定为写入范围限定 `Assets/VFX/Generated`（与 `VfxCompiler.GeneratedRoot` 一致），`Assets/VFX/Shared` 政策由 ADR-007 裁决、裁决前只读；`docs/rules/ADR-007_CONTROLLED_PROJECT_MUTATION.md` 已交付（审计中），其写入面为 `Assets/VFX/Generated` + `ProjectSettings/VFXComposer/BuildManifests` 的封闭清单，`Assets/VFX/Shared` 对 AI 构建期一律只读。本文以该定版为准。
3. **不引入新网络面**：CLI 是本地进程；MCP 仅 stdio；两者均无 listener、无自动 probe、无 health check。批次执行中的 AI 网络调用是"用户显式提交任务"触发的，符合"网络请求只能由用户/任务的显式动作触发"（CODING_STANDARDS §3.3）；`validate`/`status`/`list`/`report` 类操作零网络。
4. **secret 边界**：入口不接受、不存储、不回显任何 endpoint/secret；AI 路由完全复用现有 DPAPI/SecretRef 配置（ADR-006），入口无 provider 选择能力（无 fallback 原则不变）。
5. **有界输入**：清单 ≤64 条、prompt ≤8 KiB、清单文件 ≤512 KiB、token ≤96 字符；超界拒绝。
6. **redaction**：见 §6.6，适用于 CLI 输出、MCP 返回、报告、日志四处。
7. **批量构建授权（与 REQ-001-14 的衔接）**：批次提交动作（`vfxc batch run` / MCP `vfx_submit_batch` / `vfx_generate_effect`）即构成对批内全部条目构建的显式授权；REQ-001-14 的逐草稿确认闸仅适用于 Chat 单条流程，不适用于批量入口。批量场景下用户授权的粒度是"整批"，由提交动作一次性给出；批量执行路径不出现逐条确认交互，也不因此绕过任何校验（本节第 1 条不变）。

## 11. 编号功能需求

| 编号 | 需求（均可测试） | 优先级 |
|---|---|---|
| REQ-002-01 | 实现 `vfxcomposer.batch-manifest/1` 清单解析与两层校验；未知字段、越界、坏枚举一律拒绝，错误含精确 JSON 路径与 S4 五元组格式 | P0 |
| REQ-002-02 | 支持 `prompt` 与 `recipe` 两种条目；`prompt` 条目经 F1 通道产 recipe 草稿，F1 不可用时整批 fail-closed 拒绝 | P0 |
| REQ-002-03 | `batch validate` 零网络、零写入、零入队；可在无队列执行器时独立运行 | P0 |
| REQ-002-04 | 新 console 工程提供 §6.2 全部 6 个命令 | P0 |
| REQ-002-05 | 退出码严格符合 §6.5 表，且每个码有自动化测试 | P0 |
| REQ-002-06 | `--json` NDJSON 事件流字段命名与 Protocol Job DTO 的 `JsonPropertyName` 一致 | P1 |
| REQ-002-07 | 批次报告 `vfxcomposer.batch-report/1` 落盘，逐条含 outcome/诊断/产物 identity 计数 | P0 |
| REQ-002-08 | CLI 输出、MCP 返回、报告、日志四处均无 prompt 原文、secret、raw endpoint、项目绝对路径；有负向测试 | P0 |
| REQ-002-09 | MCP server 仅 stdio transport；不创建任何网络 listener；无参数启动 fail-closed | P0 |
| REQ-002-10 | MCP 工具集为 §7.2 的 7 个闭集工具；参数有界、未知字段拒绝 | P0 |
| REQ-002-11 | 每个 MCP 工具与对应 CLI 命令调用同一执行层 API；同输入产生等价队列条目与报告，有一致性测试 | P0 |
| REQ-002-12 | 工具/命令面不存在任何 authority/approval/skip 参数；传入即拒绝并返回稳定错误码 | P0 |
| REQ-002-13 | 清单序 = 入队序 = 执行序；并发提交多批次时批间也严格串行（队列全局 FIFO，REQ-003） | P0 |
| REQ-002-14 | 失败策略 `continue`（默认）/`abort`；abort 由队列执行器强制执行且不依赖入口进程存活；中止条目落 `CANCELLED` + 批次中止诊断码 | P0 |
| REQ-002-15 | 条目幂等键按 §9.3 计算并持久化；`--resume` 跳过已成功条目、`--force` 全量重跑 | P0 |
| REQ-002-16 | 对同一清单重复 `batch run`：默认幂等跳过，产物零 diff（依托 S6 构建幂等） | P1 |
| REQ-002-17 | 支持批次级与单条取消入口，语义委托 REQ-003 | P0 |
| REQ-002-18 | 无绕过校验路径：所有会触发构建的条目在构建前均有 `RecipeValidator` 通过记录 | P0 |
| REQ-002-19 | CLI/MCP 进程自身对 Unity 项目目录零写入（负向测试：监控进程写句柄/写路径断言） | P0 |
| REQ-002-20 | AI 网络调用仅发生在条目执行期；`validate`/`status`/`list`/`report` 操作有零网络测试 | P0 |
| REQ-002-21 | 批次提交即构成对批内全部条目构建的显式授权（§10 第 7 条）；REQ-001-14 逐草稿确认闸仅适用 Chat 单条流程。可测试判定：`batch run` 与 MCP 提交全程无逐条确认交互即完成全部构建；Chat 单条流程的确认闸行为不受本入口影响 | P0 |

## 12. 失败与边界行为汇总

| 情形 | 行为 |
|---|---|
| 清单未知字段 / 越界 / 坏枚举 | `batch validate`/`run` 拒绝整份清单，exit 65，不入队任何条目 |
| `recipePath` 逃逸（`..`、绝对路径、UNC） | 结构校验拒绝，B1xx 错误码 + 精确路径 |
| F1 通道不可用而清单含 prompt 条目 | 整批 fail-closed 拒绝（不静默只跑 recipe 条目） |
| 单条校验/构建失败（continue） | 该条 `FAILED` + 诊断，继续后续条目，最终 exit 10 |
| 单条失败（abort） | 剩余 `QUEUED` 条目 `CANCELLED`，exit 11 |
| 队列执行器/存储不可用 | `run` 立即 exit 69，不部分入队 |
| Unity 编辑器占用项目 | 任务保持排队，队列进入等待项目锁状态（REQ-003 §6.3）；CLI 前台显示等待原因 |
| CLI 前台被 Ctrl+C | exit 130；已入队条目继续由执行器处理（等价 `--detach`） |
| MCP 客户端断开 | 已提交任务不受影响；server 进程随 stdio 关闭而退出 |
| 重复提交同幂等键条目 | 默认跳过并报告 `SKIPPED_IDEMPOTENT`；`--force` 重跑 |

## 13. 验收场景（Given / When / Then）

1. **批量成功**
   Given 一份含 3 个合法条目（2 个 `recipe` + 1 个 `prompt`，F1 用 mock 响应）的清单，队列空闲且编辑器关闭；
   When 执行 `vfxc batch run`；
   Then 3 个 job 严格按清单顺序依次 `QUEUED→RUNNING→SUCCEEDED`，exit 0，报告 3 条 `SUCCEEDED`，生成区出现 3 个产物且各自 Dry Run 复跑为 unchanged。
2. **单条失败默认继续**
   Given 同上清单但第 2 条参数越界；
   When 以默认 `continue` 执行；
   Then 第 2 条 `FAILED` 且诊断含错误码、精确路径、当前值、允许范围；第 1、3 条 `SUCCEEDED`；exit 10；第 2 条对生成区零写入。
3. **可选中止**
   Given 同上清单；
   When 以 `--on-failure abort` 执行；
   Then 第 2 条 `FAILED` 后第 3 条从 `QUEUED` 落 `CANCELLED`（诊断为批次中止码），从未进入 `RUNNING`；exit 11。
4. **幂等断点续跑**
   Given 场景 2 执行完毕后修复第 2 条；
   When 以 `--resume` 重跑同一清单；
   Then 第 1、3 条 `SKIPPED_IDEMPOTENT` 不入队，仅第 2 条执行并 `SUCCEEDED`；再次全量重跑生成区零 diff。
5. **MCP 与 CLI 等价**
   Given 同一份清单内容；
   When 分别经 `vfxc batch run --detach` 与 MCP `vfx_submit_batch` 提交；
   Then 两次产生的队列条目除 id/时间戳外字段等价；`vfx_job_status` 与 `vfxc job status` 对同一 job 返回一致状态；MCP 报告与 CLI 报告结构一致。
6. **越界路径拒绝**
   Given 清单中某条目 `recipePath` 为 `..\\..\\ProjectSettings\\x.json`；
   When 执行 `batch validate`；
   Then 整份清单被拒绝（exit 65），错误定位到该条目路径字段，无任何任务入队、无任何网络与写入发生。

## 14. 与现有代码/schema 的映射表

| 本文概念 | 现有代码/schema | 关系 |
|---|---|---|
| 清单条目 → 任务 | `src/VFXComposer.Protocol/Jobs/JobIdentity.cs`、`JobCorrelation.cs`、`Commands/CommandEnvelope.cs` | 每条目产生一个 job；互异校验来自 `CommandEnvelope.RequireDistinctIdentityTokens` 与 `JobCorrelation` 的结构互异（`JobIdentity` 不做互异校验）；派生保证 `JobIdentity` 三字段互异为本 PRD 附加要求（§9.3） |
| 条目校验 | `project/Packages/com.vfxcomposer.unity/Editor/Validation/RecipeValidator.cs` | 复用，不可绕过 |
| 条目构建 | `project/Packages/com.vfxcomposer.unity/Editor/Build/VfxCompiler.cs` + `tools/Invoke-Unity.ps1`，或 Worker 命令面 `ValidateRecipeCommand`/`BuildCandidateCommand` | 执行底座，具体路线由 F2 定版 |
| 进度/日志/产物/完成事件 | `src/VFXComposer.Protocol/Jobs/JobProgress.cs`、`JobLogEvent.cs`、`JobArtifact.cs`、`JobCompletion.cs` | `--json` 与报告字段命名对齐其 `JsonPropertyName` |
| 取消 | `src/VFXComposer.Protocol/Commands/CancelJobCommand.cs` | 经 REQ-003 §8 映射 |
| 失败/中止词表 | `JobCompletionOutcomes`（`JobWireVocabulary.cs`） | 闭集复用，不新增状态 |
| MCP 约束先例 | `project/Packages/com.vfxcomposer.unity/Editor/W24/S6/External/W24S6McpOperationEnvelope.cs` | 继承约束模型（allow-list、有界、hash 绑定、稳定码）；不复用其 schema，不放宽其枚举 |
| 顺序执行先例 | `docs/allwork/00_INDEX_AND_ACCEPTANCE.md` §2 | 产品语义一致：编号顺序、逐个执行 |
| 错误报告格式 | `docs/EXECUTION_PLAN.md` §4.1 第 5 条（S4 五元组） | 清单校验错误与条目失败诊断共用 |
| prompt → recipe | F1 通道（REQ-001） | `prompt` 条目的执行体，本文不重复定义 |
| AI 路由/secret | `src/VFXComposer.AI.Providers`（ADR-006） | 完全复用；入口零 provider 能力 |
| 项目锁/退出码惯例 | `tools/Invoke-Unity.ps1`（73/124/64） | CLI 退出码与锁语义对齐 |

## 15. 缺口清单（对应主计划任务）

| 缺口 | 现状 | 对应任务 |
|---|---|---|
| 批量执行层库（清单解析、幂等键、队列客户端、报告） | 不存在 | F4 |
| CLI console 工程与 6 命令 | 不存在 | F4 |
| 批次报告 schema `vfxcomposer.batch-report/1` | 不存在 | F4 |
| MCP server 工程（stdio、7 工具） | 不存在；仅有无 transport 的结构 stub | F5 |
| MCP C# SDK 依赖进入本地批准 feed（或决定手写 stdio JSON-RPC） | 未决 | F5 前置决策 |
| 串行队列与队列存储 | 不存在（Desktop Jobs 页为占位） | F3（REQ-003） |
| prompt→recipe 生成通道 | 不存在 | F1 |
| 受限构建执行（写入 containment 落地） | 不存在；写入面已由 ADR-007（已交付、审计中）定版 | F2 / R4 |
| 批次级策略（onFailure）在队列侧的持久化与执行器强制执行 | 队列合同中无批次概念 | F3（REQ-003 §7 已列） |
| 产物定位面（`JobArtifact` 无位置，报告只能给 identity；用户找产物需 Build Manifest 读取路径） | 只读 manifest 读取面存在于 Worker（`ReadDocumentQuery`），未接入报告 | F3/F4 设计决策 |
| `docs/ai-workflow/README.md` 的"无 MCP/CLI"表述加 superseded 指针 | 本任务禁改已有文件 | 后续文档对齐任务（P0-2 同类） |

## 16. 开放问题与风险

1. **写入区路径定版进展**：现行编码规范 §3.2 已统一为 `Assets/VFX/Generated`（Shared 由 ADR-007 裁决、裁决前只读），ADR-007 已交付（审计中，写入面为 Generated + BuildManifests 封闭清单、Shared 对 AI 构建期只读）。残余风险仅为主计划等旧文档中 `Assets/Generated` 表述未同步；以 ADR-007 为唯一裁决。
2. **MCP 依赖与批准 feed 冲突**：NuGet 只允许本地批准 feed（CODING_STANDARDS §1.3）；官方 MCP SDK 若不能入 feed，F5 需手写 stdio JSON-RPC（工作量上升但面更小）。
3. **F1 语义先行**：`prompt` 条目的质量与重试话术完全取决于 F1（REQ-001）；本文只约定 fail-closed 与错误透传，不为 F1 兜底。
4. **"批次"是队列的新一等概念**：现有 Protocol Job 合同无批次分组；REQ-003 已把 `batchId` 与批次策略列为队列存储字段，若 F3 砍掉该字段，则 `abort`/`--detach` 语义必须回退为"入口进程存活期内强制执行"，需回改本文 §9.2。
5. **长期占用的编辑器**：用户开着编辑器时队列会长期等待（REQ-003 §6.3 的可见等待态）；CLI 前台默认无限等待、`--lock-timeout` 之类有限等待参数留给 F4 设计裁量（本文仅保留 exit 73 语义）。

## 17. 变更记录

| 版本 | 日期 | 变更内容 |
|---|---|---|
| v0.1 | 2026-08-29 | 初版（任务卡 R2） |
| v0.2 | 2026-08-29 | 审计微调：互异约束引用归属更正（§5.3/§9.3/§14，`JobIdentity` 不做互异校验，派生互异降级为本 PRD 附加要求；`Guard.Token` 默认上限 128 写准）；写入区引用陈述更新为现行编码规范 §3.2 与已交付的 ADR-007（§10.2/§15/§16.1）；新增批量构建授权条款与 REQ-002-21，明确与 REQ-001-14 确认闸的适用边界（§10.7/§11）；`--resume` 条目幂等键存储落点交叉引用 REQ-003 §7.1（§9.3） |
